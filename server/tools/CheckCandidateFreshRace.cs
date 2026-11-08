using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshRaceSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'skills',(SELECT json_agg(r ORDER BY entityid,id) FROM skills r WHERE entityid IN(@uid,@peer)),'lessons',(SELECT json_agg(r ORDER BY learned_at,id) FROM revora_mount_lessons r WHERE uid IN(@uid,@peer)),'kits',(SELECT json_agg(r ORDER BY purchased_at,id) FROM revora_mount_kit_purchases r WHERE uid IN(@uid,@peer)),'vaults',(SELECT json_agg(r ORDER BY entityid) FROM variablevault r WHERE entityid IN(@uid,@peer)),'runs',(SELECT json_agg(r ORDER BY id) FROM revora_steed_runs r WHERE id IN(SELECT run FROM revora_steed_results WHERE uid IN(@uid,@peer))),'results',(SELECT json_agg(r ORDER BY run,uid) FROM revora_steed_results r WHERE uid IN(@uid,@peer)),'claims',(SELECT json_agg(r ORDER BY uid,id) FROM revora_steed_claims r WHERE uid IN(@uid,@peer)))::text","prior",FreshQuizSnapshot(p),"uid",(long)p.Entity.UID,"peer",Convert.ToInt64(Sql("SELECT peer FROM revora_fresh_honor_probe"))));}
    static void FreshRaceJourney(string phase){
        bool prepare=phase.EndsWith("prepare"),earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        uint peerUid=Convert.ToUInt32(Sql("SELECT peer FROM revora_fresh_honor_probe"));var peer=FreshContributor(peerUid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE uid=@uid","uid",(long)peerUid)));var players=new[]{p,peer};Server.GamePool=players;Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w) && !w.Entity.Dead));
        if(prepare){
            Require((bool)Sql("SELECT to_regclass('revora_fresh_race_probe') IS NULL") && FreshQuizSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_quiz_probe WHERE stage=2")));
            Require(p.Equipment.TryGetItem(ConquerItem.Steed)!=null && peer.Equipment.TryGetItem(ConquerItem.Steed)==null && !peer.Inventory.Objects.Any(i=>i.ID==300000) && peer.Inventory.Count<=35 && p.Inventory.Count<=39);
            uint price=Revora.Game.Features.RevoraMasterSystem.MountKitPrice,cp=p.Entity.ConquerPoints,other=peer.Entity.ConquerPoints;Require(price>0 && cp>=price);FreshClassicTalk(p,1002,900000001,0);FreshClassicTalk(peer,1002,900000001,0);
            CareerTradePacket(p,Trade.Request,peerUid);CareerTradePacket(peer,Trade.Request,p.Entity.UID);CareerTradePacket(p,Trade.SetConquerPoints,price);CareerTradePacket(p,Trade.Accept,0);CareerTradePacket(peer,Trade.Accept,0);Require(p.Entity.ConquerPoints==cp-price && peer.Entity.ConquerPoints==other+price && !p.Trade.InTrade && !peer.Trade.InTrade);
            var prior=peer.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(peer,900000001,4);HeartTalk(peer,900000001,14);HeartTalk(peer,900000001,14);var kit=peer.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(kit.Length==5 && kit.All(i=>i.Plus==12 && i.Bless==7) && peer.Entity.ConquerPoints==other && prior.All(id=>peer.Inventory.ContainsUID(id)));
            var horse=kit.Single(i=>i.ID==300000);ItemHandler.EquipItem(new ItemUsage(true){UID=horse.UID,dwParam=ConquerItem.Steed,Action=ItemUsage.Mode.EquipItem},peer);Require(peer.Equipment.TryGetItem(ConquerItem.Steed).UID==horse.UID);
            foreach(var who in players){bool known=who.Spells.ContainsKey(7001);ulong silver=who.Entity.Money;FreshClassicTalk(who,1002,9881,5);HeartTalk(who,9881,5);Require(who.Spells.ContainsKey(7001) && who.Entity.Money==silver-(known?0ul:50000ul));Require(EntityTable.SaveEntity(who));}
            Sql("CREATE TABLE revora_fresh_race_probe(stage int NOT NULL,snapshot text NOT NULL);INSERT INTO revora_fresh_race_probe VALUES(1,@snapshot)","snapshot",FreshRaceSnapshot(p));Console.WriteLine("[fresh-race] mount-kit-cp="+price+" paid-by-earned-trade");
        }else{
            Require(FreshRaceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_race_probe")));var system=typeof(Revora.Game.Features.SteedRaceSystem);
            if(earn){
                Require((int)Sql("SELECT stage FROM revora_fresh_race_probe")==1 && players.All(w=>w.Spells.ContainsKey(7001) && w.Equipment.TryGetItem(ConquerItem.Steed)!=null));Call(system,"ResetCycle");system.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddMinutes(1));Revora.Game.Features.SteedRaceSystem.Tick();foreach(var who in players)FreshClassicTalk(who,1002,900000012,1);
                system.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddSeconds(-1));Revora.Game.Features.SteedRaceSystem.Tick();var race=Server.Thread.SteedRace;Require(SteedRace.isOn && !SteedRace.GateOpen && players.All(w=>w.Entity.MapID==SteedRace.MAPID));ushort track=SteedRace.MAPID;
                race.FinishRace(p);Require((uint)p["steed-claim"]==0);system.GetField("PeaceUntil",Hidden).SetValue(null,new Time32(0));Revora.Game.Features.SteedRaceSystem.Tick();Require(SteedRace.GateOpen);
                for(int index=0;index<players.Length;index++){var who=players[index];int steps=ProcessRaceWalk(who,(ushort)SteedRace.Settings[3],(ushort)SteedRace.Settings[4],(int)SteedRace.Settings[5]);race.FinishRace(who);Require(steps>100 && who.Entity.MapID==1002 && (uint)who["steed-claim"]==(ulong)(index+1));Console.WriteLine("[fresh-race] uid="+who.Entity.UID+" track="+track+" accepted-steps="+steps);}
                race.End();foreach(var who in players)Require(EntityTable.SaveEntity(who));Require((long)Sql("SELECT count(*) FROM revora_steed_results WHERE uid IN(@uid,@peer) AND rank IN(1,2) AND claimed_at IS NULL","uid",(long)p.Entity.UID,"peer",(long)peerUid)==2);Sql("UPDATE revora_fresh_race_probe SET stage=2,snapshot=@snapshot","snapshot",FreshRaceSnapshot(p));
            }else if(claim){
                Require((int)Sql("SELECT stage FROM revora_fresh_race_probe")==2);
                foreach(var who in players){uint cp=who.Entity.ConquerPoints,rp=who.Entity.RevoraPoint;ulong silver=who.Entity.Money;var before=who.Inventory.Objects.Select(i=>i.UID).ToArray();uint gainCp=Convert.ToUInt32(Sql("SELECT cp FROM revora_steed_results WHERE uid=@uid AND claimed_at IS NULL","uid",(long)who.Entity.UID)),gainRp=Convert.ToUInt32(Sql("SELECT rvp FROM revora_steed_results WHERE uid=@uid AND claimed_at IS NULL","uid",(long)who.Entity.UID));ulong gainSilver=Convert.ToUInt64(Sql("SELECT gold FROM revora_steed_results WHERE uid=@uid AND claimed_at IS NULL","uid",(long)who.Entity.UID));HeartTalk(who,900000012,2);var added=who.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(added.Length==1 && added[0].Bound && ConquerItemInformation.BaseInformations[added[0].ID].PurificationLevel==7 && who.Entity.ConquerPoints==cp+gainCp && who.Entity.RevoraPoint==rp+gainRp && who.Entity.Money==silver+gainSilver && before.All(id=>who.Inventory.ContainsUID(id)) && (uint)who["steed-claim"]==0);string paid=FreshRaceSnapshot(p);HeartTalk(who,900000012,2);Require(paid==FreshRaceSnapshot(p));Require(EntityTable.SaveEntity(who));Console.WriteLine("[fresh-race] uid="+who.Entity.UID+" cp="+gainCp+" rvp="+gainRp+" silver="+gainSilver);}
                Sql("UPDATE revora_fresh_race_probe SET stage=3,snapshot=@snapshot","snapshot",FreshRaceSnapshot(p));
            }else{
                Require(phase.EndsWith("reload") && (int)Sql("SELECT stage FROM revora_fresh_race_probe")==3);foreach(var who in players){uint item=Convert.ToUInt32(Sql("SELECT item_uid FROM revora_steed_claims WHERE uid=@uid","uid",(long)who.Entity.UID));Require(who.Inventory.ContainsUID(item) && who.Spells.ContainsKey(7001) && who.Equipment.TryGetItem(ConquerItem.Steed)!=null);HeartTalk(who,900000012,2);}Require(FreshRaceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_race_probe")));
            }
        }
        if(phase.EndsWith("reload")){
            foreach(int id in new[]{11113,11122})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id AND source='steed-course-v1'","uid",(long)p.Entity.UID,"id",id)==1);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11113 AND source='steed-course-v1'","uid",(long)peerUid)==1);
            Require((long)Sql("SELECT count(*) FROM revora_steed_results WHERE uid IN(@uid,@peer) AND rank IN(1,2) AND course_steps=1208 AND course_tiles>100 AND claimed_at IS NOT NULL","uid",(long)p.Entity.UID,"peer",(long)peerUid)==2);
            Console.WriteLine("[fresh-race] cold-lessons="+Sql("SELECT count(*) FROM revora_mount_lessons WHERE uid IN(@uid,@peer) AND skill=7001","uid",(long)p.Entity.UID,"peer",(long)peerUid)+" lesson-silver="+Sql("SELECT coalesce(sum(silver),0) FROM revora_mount_lessons WHERE uid IN(@uid,@peer) AND skill=7001","uid",(long)p.Entity.UID,"peer",(long)peerUid));
        }
        Require(players.All(w=>!PirateFenced(w)));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();if(prepare || earn || claim){Environment.Exit(0);return;}Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Race did not close.");
    }
}
