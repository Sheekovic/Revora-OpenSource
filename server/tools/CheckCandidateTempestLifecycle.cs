using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static void TempestTalk(GameState p,uint npc,byte option) {
        if(npc!=9910500)p.BindWorldMap(npc==8796?(ushort)1004:npc==11275?(ushort)1002:(ushort)1011);
        var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=(ushort)(actor.Y+2);p.ActiveNpc=npc;
        Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);
    }
    static GameState TempestPlayer() {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require((bool)Feature("TempestWingsQuest").GetProperty("Enabled",Hidden).GetValue(null,null));return p;
    }
    static void TempestCapture() {
        Player(Kernel.Maps[1002]);nextUid=FirstUid;var p=TempestPlayer();
        Require(p.Entity.ConquerPoints==0 && p.Inventory.Count==0);ulong initialSilver=p.Entity.Money;
        var daily=Feature("DailyWorldBosses");DateTime now=DateTime.Now.Date.AddDays(1).ToUniversalTime();
        daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Call(daily,"Tick");
        now=DateTime.SpecifyKind((DateTime)Sql("SELECT min(planned) FROM revora_daily_boss_runs WHERE planned>=@now AND state=0","now",now),DateTimeKind.Utc).AddSeconds(1);
        Call(daily,"Tick");var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
        var boss=(Entity)active.GetType().GetField("Entity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
        p.BindWorldMap(boss.MapID);EliminationHit(p,boss);
        System.Console.WriteLine("[tempest-process] bounty cp="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" silver="+p.Entity.Money);
        Require(p.Entity.ConquerPoints==1000000 && p.Entity.RevoraPoint==50000 && p.Entity.Money==initialSilver+10000000);
        Require((long)Sql("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND state=2 AND paid","uid",(long)FirstUid)==1);
        uint price=ConquerItemInformation.BaseInformations[1088001].ConquerPointsWorth;Require(price>0);
        for(int pack=0;pack<5;pack++) {
            p.BindWorldMap(1002);
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=2888,dwParam=1088001,dwExtraInfo=10},p);
            Require(p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize)==10);
            TempestTalk(p,11275,0);TempestTalk(p,11275,1);
            Require(p.Inventory.Objects.Count(i=>i.ID==720027)==pack+1);
        }
        Require(p.Entity.ConquerPoints==1000000-50*price && p.Inventory.Count==5);
        TempestTalk(p,8796,1);TempestTalk(p,10850,1);
        Require(p.Map.BaseID==1100 && p.Inventory.Count==0);
        Require(Convert.ToInt64(Sql("SELECT sum(spent) FROM revora_tempest_payments WHERE uid=@uid","uid",(long)FirstUid))==5);
        Sql("CREATE TABLE revora_tempest_process_probe(uid bigint PRIMARY KEY,cp bigint NOT NULL,fingerprint text); INSERT INTO revora_tempest_process_probe(uid,cp) VALUES(@uid,@cp)","uid",(long)FirstUid,"cp",(long)p.Entity.ConquerPoints);
        System.Console.WriteLine("[tempest-process] tempest-capture-passed");Environment.Exit(0);
    }
    static void TempestFinish() {
        var p=TempestPlayer();Require(p.Entity.MapID==1011);
        long stage=Convert.ToInt64(Sql("SELECT stage FROM revora_tempest_wings WHERE uid=@uid","uid",(long)FirstUid));
        if(stage==2) {
            Require(p.Inventory.Count==0);TempestTalk(p,10850,1);Require(p.Map.BaseID==1100);
            var end=DateTime.UtcNow.AddSeconds(4);
            while(p.Map.Entities.Count==0 && DateTime.UtcNow<end){Call(Feature("TempestWingsQuest"),"Tick");System.Threading.Thread.Sleep(50);}
            var roc=p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==9905500);
            EliminationHit(p,roc);Call(Feature("TempestWingsQuest"),"Tick");
            Require(p.Inventory.Objects.Count(i=>i.ID==3006392)==1);
            TempestTalk(p,9910500,1);Require(p.Entity.MapID==1011);
        } else Require(stage==4);
        TempestTalk(p,10850,3);TempestTalk(p,10850,3);
        Require(p.Inventory.Count==2 && p.Inventory.Objects.All(i=>i.Bound));
        Require(p.Inventory.Objects.Count(i=>i.ID==204008)==1);
        Require(p.Inventory.Objects.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
        Require((bool)Sql(@"SELECT EXISTS(SELECT 1 FROM revora_tempest_wings q JOIN revora_tempest_process_probe p ON p.uid=q.uid
JOIN entities e ON e.uid=q.uid WHERE q.stage=4 AND cardinality(q.rewards)=2 AND e.conquerpoints=p.cp
AND (SELECT count(*) FROM items i WHERE i.entityid=q.uid AND i.uid=ANY(q.rewards) AND i.bound=1 AND i.id IN(204008,q.soul))=2)
AND (SELECT count(*) FROM revora_tempest_permits WHERE uid=@uid)=1
AND (SELECT sum(spent) FROM revora_tempest_payments WHERE uid=@uid)=5
AND (SELECT count(*) FROM revora_tempest_kills WHERE uid=@uid)=1","uid",(long)FirstUid));
        string fingerprint=(string)Sql("SELECT md5(stage::text||':'||soul::text||':'||rewards::text||':'||feather_uid::text) FROM revora_tempest_wings WHERE uid=@uid","uid",(long)FirstUid);
        object previous=Sql("SELECT fingerprint FROM revora_tempest_process_probe");
        if(previous==DBNull.Value)Sql("UPDATE revora_tempest_process_probe SET fingerprint=@value","value",fingerprint);else Require((string)previous==fingerprint);
        System.Console.WriteLine("[tempest-process] tempest-finish-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Tempest process did not close.");
    }
}
