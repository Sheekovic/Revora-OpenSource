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
    const string FreshWeeklyRun="pk-0-20410608-3";
    static string FreshWeeklySnapshot(GameState p,GameState[] players){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'war',(SELECT row_to_json(r) FROM revora_pk_wars r WHERE run=@run),'entries',(SELECT json_agg(r ORDER BY uid) FROM revora_pk_entries r WHERE run=@run),'claim',(SELECT row_to_json(r) FROM revora_pk_claims r WHERE run=@run),'returns',(SELECT json_agg(r ORDER BY uid) FROM revora_pk_returns r WHERE run=@run))::text","prior",FreshTournamentTrainingSnapshot(p,players),"run",FreshWeeklyRun));}
    static void FreshWeeklySave(GameState p,GameState[] players,int stage){foreach(var who in players)Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_fresh_weekly_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshWeeklySnapshot(p,players));}
    static void FreshWeeklyJourney(string phase){
        bool prepare=phase.EndsWith("prepare"),earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();var peer=players[3];Server.GamePool=players;Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w) && !w.Entity.Dead));
        var type=Feature("LocalPkWar");var current=type.GetField("Current",Hidden).GetValue(null);var instance=BindingFlags.NonPublic|BindingFlags.Instance;DateTime start=new DateTime(2041,6,9,3,0,0,DateTimeKind.Utc),now=start;
        if(prepare){
            Require(FreshTournamentTrainingSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_tournament_training_probe WHERE done")) && (bool)Sql("SELECT to_regclass('revora_fresh_weekly_probe') IS NULL"));
            FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];int store=Math.Max(0,p.Inventory.Count-36);Require(bank.Count+store<=60);
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count<=36);
            FreshClassicTalk(p,1002,2314,0);FreshClassicTalk(peer,1002,2314,0);Sql("CREATE TABLE revora_fresh_weekly_probe(stage int NOT NULL,snapshot text);INSERT INTO revora_fresh_weekly_probe VALUES(0,NULL)");FreshWeeklySave(p,players,0);
        }else{
            Require(FreshWeeklySnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_weekly_probe")));
            type.GetField("clock",instance).SetValue(current,(Func<DateTime>)(()=>now));GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Action<double> tick=minutes=>{now=start.AddMinutes(minutes);type.GetMethod("Tick",instance).Invoke(current,null);};
            if(earn){
                Require((int)Sql("SELECT stage FROM revora_fresh_weekly_probe")==0 && p.Inventory.Count<=36 && peer.Entity.Level==130 && p.Entity.Level==132);p.Entity.PKMode=Enums.PKMode.Peace;peer.Entity.PKMode=Enums.PKMode.Peace;
                tick(0);HeartTalk(p,2314,1);HeartTalk(peer,2314,1);tick(.02);Require(p.Map.BaseID==1505 && p.Entity.MapID==peer.Entity.MapID);tick(28);Require(p.Map.BaseID==1508 && peer.Map.BaseID==1508);tick(40);Require(p.Map.BaseID==700 && peer.Map.BaseID==700 && p.Entity.MapID==peer.Entity.MapID);tick(40.2);
                p.Attackable=peer.Attackable=true;p.CantAttack=peer.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;var timer=System.Diagnostics.Stopwatch.StartNew();
                while(!peer.Entity.Dead && p.Map.BaseID==700 && hits<5000 && timer.Elapsed.TotalSeconds<90){p.Entity.X=peer.Entity.X;p.Entity.Y=(ushort)(peer.Entity.Y+1);p.Screen.Add(peer.Entity);peer.Screen.Add(p.Entity);p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);uint hp=peer.Entity.Hitpoints;Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=peer.Entity.UID,X=peer.Entity.X,Y=peer.Entity.Y,AttackType=Attack.Melee},p);hits++;if(hits==1)Require(peer.Entity.Hitpoints<hp || peer.Entity.MapID==1002);}tick(40.3);
                Require((long)Sql("SELECT count(*) FROM revora_pk_wars WHERE run=@run AND state='complete' AND winner=@uid AND valuable_reward","run",FreshWeeklyRun,"uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_pk_entries WHERE run=@run AND competitive AND admitted_at IS NOT NULL","run",FreshWeeklyRun)==2 && (long)Sql("SELECT count(*) FROM revora_pk_claims WHERE run=@run","run",FreshWeeklyRun)==0 && (long)Sql("SELECT count(*) FROM revora_pk_returns WHERE run=@run AND returned","run",FreshWeeklyRun)==2);
                Require(p.Entity.MapID==1002 && peer.Entity.MapID==1002 && !peer.Entity.Dead && p.Entity.PKMode==Enums.PKMode.Peace && peer.Entity.PKMode==Enums.PKMode.Peace);FreshWeeklySave(p,players,1);Console.WriteLine("[fresh-weekly] calculated-hits="+hits);
            }else{
                now=start.AddMinutes(41);Call(type,"OnLogin",p);Call(type,"OnLogin",peer);
                if(claim){
                    Require((int)Sql("SELECT stage FROM revora_fresh_weekly_probe")==1);uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,2314,3);var added=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();Require(added.Length==4 && added.All(i=>i.Bound) && added.Count(i=>i.ID==722136)==3 && added.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1 && old.All(id=>p.Inventory.ContainsUID(id)) && p.Entity.ConquerPoints==cp+100000 && p.Entity.RevoraPoint==rp+1000 && p.Entity.Money==gold+1000000);FreshClassicTalk(peer,1002,2314,0);FreshWeeklySave(p,players,2);
                }else Require(phase.EndsWith("reload") && (int)Sql("SELECT stage FROM revora_fresh_weekly_probe")==2);
                string paid=FreshWeeklySnapshot(p,players);HeartTalk(p,2314,3);HeartTalk(p,2314,3);HeartTalk(peer,2314,3);Require(paid==FreshWeeklySnapshot(p,players));var rewards=(long[])Sql("SELECT rewards FROM revora_pk_claims WHERE run=@run AND uid=@uid","run",FreshWeeklyRun,"uid",(long)p.Entity.UID);Require(rewards.Length==4 && rewards.All(id=>p.Inventory.Objects.Any(i=>i.UID==id && i.Bound)));
                foreach(var who in players){var saved=(long[])Sql("SELECT coalesce(array_agg(uid ORDER BY uid),ARRAY[]::bigint[]) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND coalesce(inwardrobe,0)=0","uid",(long)who.Entity.UID);Require(saved.SequenceEqual(who.Inventory.Objects.Select(i=>(long)i.UID).OrderBy(id=>id)) && EntityTable.SaveEntity(who));}Require(FreshWeeklySnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_weekly_probe")));
            }
            foreach(int id in new[]{11116,11118})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id AND source='weekly-pk-v1' AND source_key=@run","uid",(long)p.Entity.UID,"id",id,"run",FreshWeeklyRun)==1);
        }
        Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();if(prepare || earn || claim){Environment.Exit(0);return;}Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Weekly PK did not close.");
    }
}
