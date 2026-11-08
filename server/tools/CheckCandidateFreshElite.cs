using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshEliteSnapshot(GameState p,GameState[] players,string run){return Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'event',@event::json,'claims',(SELECT json_agg(r ORDER BY uid) FROM revora_epk_prize_claims r WHERE run=@run))::text","prior",FreshMonthlySnapshot(p,players),"event",EliteClaimEventSnapshot(run),"run",run));}
    static void FreshEliteSave(GameState p,GameState[] players,int stage,string run){foreach(var who in players)Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_fresh_elite_probe SET stage=@stage,run=@run,snapshot=@snapshot","stage",stage,"run",run,"snapshot",FreshEliteSnapshot(p,players,run));}
    static void FreshEliteJourney(string phase){
        bool prepare=phase.EndsWith("prepare"),earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");var p=FreshLoadSaved(true,true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();var peer=players[3];Server.GamePool=players;Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w) && !w.Entity.Dead));

        if(prepare){
            Require(FreshMonthlySnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_monthly_probe WHERE stage=2")) && (bool)Sql("SELECT to_regclass('revora_fresh_elite_probe') IS NULL") && peer.Inventory.Count<=28);
            int store=Math.Max(0,p.Inventory.Count-16);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.DesertCity];Require(bank.Count+store<=60);FreshClassicTalk(p,1000,10011,0);
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10011,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count<=16);
            FreshClassicTalk(p,1002,448582,0);FreshClassicTalk(peer,1002,448582,0);Sql("CREATE TABLE revora_fresh_elite_probe(stage int NOT NULL,run text,snapshot text);INSERT INTO revora_fresh_elite_probe VALUES(0,NULL,NULL)");FreshEliteSave(p,players,0,"");
        }else{
            string run=Convert.ToString(Sql("SELECT run FROM revora_fresh_elite_probe"));Require(FreshEliteSnapshot(p,players,run)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_elite_probe")));
            localEliteNow=new DateTime(2041,7,6,2,55,0,DateTimeKind.Utc);var type=Feature("ElitePkEvent");var arena=type.GetField("Current",Hidden).GetValue(null);type.GetField("clock",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(arena,(Func<DateTime>)(()=>localEliteNow));GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>localEliteNow));
            if(earn){
                Require((int)Sql("SELECT stage FROM revora_fresh_elite_probe")==0);p.Entity.PKMode=peer.Entity.PKMode=Enums.PKMode.Peace;ElitePulse(arena,0);HeartTalk(p,448582,3);HeartTalk(peer,448582,3);
                var group=((IEnumerable)EliteField(arena,"groups")).Cast<object>().Single(g=>(int)EliteField(g,"Id")==3);run=(string)EliteField(group,"Run");Require((long)Sql("SELECT count(*) FROM revora_epk_entries WHERE run=@run","run",run)==2);ElitePulse(arena,300);int hits=0;
                for(int step=0;step<20 && !(bool)EliteField(group,"Done");step++){
                    ElitePulse(arena,11);ElitePulse(arena,11);
                    foreach(var match in ((IEnumerable)EliteField(group,"Matches")).Cast<object>().Where(m=>(bool)EliteField(m,"Imported") && EliteField(m,"Winner")==null).ToArray()){
                        Require(p.Entity.MapID==peer.Entity.MapID);p.Attackable=peer.Attackable=true;p.CantAttack=peer.CantAttack=Time32.Now.AddSeconds(-1);p.Entity.X=(ushort)(peer.Entity.X+1);p.Entity.Y=peer.Entity.Y;p.Screen.Add(peer.Entity);
                        for(int n=0;n<200 && !peer.Entity.Dead && p.Entity.MapID==peer.Entity.MapID;n++){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=peer.Entity.UID,X=peer.Entity.X,Y=peer.Entity.Y,AttackType=Attack.Melee},p);hits++;}Require(peer.Entity.Dead || EliteField(match,"Winner")!=null);
                    }ElitePulse(arena,1);
                }
                Require(hits>0 && (bool)EliteField(group,"Done") && (bool)Sql("SELECT state='complete' AND winner=@uid AND outcome='champion' FROM revora_epk_runs WHERE run=@run","run",run,"uid",(long)p.Entity.UID));Require((long)Sql("SELECT count(*) FROM revora_epk_matches WHERE run=@run AND winner=@uid AND outcome='defeat' AND left_damage+right_damage>0","run",run,"uid",(long)p.Entity.UID)>0 && (long)Sql("SELECT count(*) FROM revora_epk_prize_claims WHERE run=@run","run",run)==0);
                Require(p.Entity.MapID==1002 && peer.Entity.MapID==1002 && !p.Entity.Dead && !peer.Entity.Dead && p.Entity.PKMode==Enums.PKMode.Peace && peer.Entity.PKMode==Enums.PKMode.Peace);FreshEliteSave(p,players,1,run);Console.WriteLine("[fresh-elite] run="+run+" calculated-hits="+hits);
            }else{
                localEliteNow=localEliteNow.AddHours(1);
                if(claim){
                    Require((int)Sql("SELECT stage FROM revora_fresh_elite_probe")==1);
                    foreach(var who in new[]{p,peer}){uint cp=who.Entity.ConquerPoints;ulong xp=who.Entity.Experience;byte level=who.Entity.Level;var old=who.Inventory.Objects.Select(i=>i.UID).ToArray();ulong expected=DataHolder.LevelExperience(level)/(who==p?1UL:2UL);FreshClassicTalk(who,1002,448582,6);HeartTalk(who,448582,7);var added=who.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();Require(added.Length==(who==p?24:12) && who.Entity.ConquerPoints==cp+5000000 && (who.Entity.Level>level || who.Entity.Experience>xp) && added.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1 && old.All(id=>who.Inventory.ContainsUID(id)));Require(Convert.ToUInt64(Sql("SELECT experience FROM revora_epk_prize_claims WHERE run=@run AND uid=@uid","run",run,"uid",(long)who.Entity.UID))==expected);var ids=(long[])Sql("SELECT rewards FROM revora_epk_prize_claims WHERE run=@run AND uid=@uid","run",run,"uid",(long)who.Entity.UID);Require(ids.OrderBy(id=>id).SequenceEqual(added.Select(i=>(long)i.UID).OrderBy(id=>id)));Console.WriteLine("[fresh-elite] uid="+who.Entity.UID+" items="+added.Length+" experience="+expected+" level="+who.Entity.Level);}
                    FreshEliteSave(p,players,2,run);
                }else Require(phase.EndsWith("reload") && (int)Sql("SELECT stage FROM revora_fresh_elite_probe")==2);
                string paid=FreshEliteSnapshot(p,players,run);foreach(var who in new[]{p,peer}){HeartTalk(who,448582,7);HeartTalk(who,448582,6);HeartTalk(who,448582,7);var ids=(long[])Sql("SELECT rewards FROM revora_epk_prize_claims WHERE run=@run AND uid=@uid","run",run,"uid",(long)who.Entity.UID);Require(ids.All(id=>who.Inventory.ContainsUID((uint)id)));}Require(paid==FreshEliteSnapshot(p,players,run));
                foreach(var who in players){var saved=(long[])Sql("SELECT coalesce(array_agg(uid ORDER BY uid),ARRAY[]::bigint[]) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND coalesce(inwardrobe,0)=0","uid",(long)who.Entity.UID);Require(saved.SequenceEqual(who.Inventory.Objects.Select(i=>(long)i.UID).OrderBy(id=>id)) && EntityTable.SaveEntity(who));}Require(FreshEliteSnapshot(p,players,run)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_elite_probe")));
            }
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11123","uid",(long)p.Entity.UID)==1);
        }
        Require(players.All(w=>!PirateFenced(w)));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();if(prepare || earn || claim){Environment.Exit(0);return;}Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Elite PK did not close.");
    }
}
