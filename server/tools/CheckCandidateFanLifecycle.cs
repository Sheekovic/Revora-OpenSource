using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime fanNow;
    static void PrepareFan(string phase) {
        fanNow=phase=="fan-capture"?DateTime.UtcNow:DateTime.SpecifyKind((DateTime)Sql("SELECT stamp AT TIME ZONE 'UTC' FROM revora_fan_process_probe"),DateTimeKind.Utc);
        Feature("AttackTalismanStore").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>fanNow));
    }
    static GameState FanPlayer(){var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Call(Feature("AttackTalismanQuest"),"OnLogin",p);Require(TowerStage()==10);return p;}
    static void FanTalk(GameState p,uint npc,byte option) {
        if(npc==4565 || npc>=9910410 && npc<=9910414)p.BindWorldMap(1015);
        else if(npc==4583)p.BindWorldMap(1000);else if(npc==4581 || npc==4582 || npc==9910430)p.BindWorldMap(1011);
        HeartTalk(p,npc,option);
    }
    static void FanTick(){fanNow=fanNow.AddSeconds(2);Call(Feature("AttackTalismanQuest"),"Tick");}
    static void FanKill(GameState p){FanTick();EliminationHit(p,p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=9905400 && e.MonsterInfo.ID<9905407 && !e.Dead));FanTick();}
    static long FanStage(){return Convert.ToInt64(Sql("SELECT stage FROM revora_attack_talisman WHERE uid=@uid","uid",(long)FirstUid));}
    static void FanCapture() {
        var p=FanPlayer();var daily=Feature("DailyWorldBosses");DateTime now=DateTime.Now.Date.AddDays(1).ToUniversalTime();
        daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Call(daily,"Tick");
        now=DateTime.SpecifyKind((DateTime)Sql("SELECT min(planned) FROM revora_daily_boss_runs WHERE planned>=@now AND state=0","now",now),DateTimeKind.Utc).AddSeconds(1);
        Call(daily,"Tick");var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
        var boss=(Entity)active.GetType().GetField("Entity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
        p.BindWorldMap(boss.MapID);EliminationHit(p,boss);ulong before=p.Entity.Money;
        FanTalk(p,4565,0);FanTalk(p,4565,80);FanTalk(p,4565,81);FanTalk(p,4582,1);FanTalk(p,4581,1);FanTalk(p,4581,1);
        Require(FanStage()==2 && p.Inventory.Objects.Count(i=>i.ID==721800)==1 && p.Entity.Money==before+100-10000);
        int hunted=0;
        foreach(var snake in p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && (e.MonsterInfo.ID==6 || e.MonsterInfo.ID==65) && !e.Dead).Take(100).ToArray()) {
            EliminationHit(p,snake);hunted++;if(p.Inventory.Objects.Count(i=>i.ID==710220)==5)break;
        }
        Require(hunted>=5 && p.Inventory.Objects.Count(i=>i.ID==710220)==5);
        FanTalk(p,4582,1);FanTalk(p,4565,81);FanTalk(p,4583,1);FanTalk(p,4583,2);Require(FanStage()==5 && p.Map.BaseID==1025);
        Sql("CREATE TABLE revora_fan_process_probe(stamp timestamptz NOT NULL,silver bigint NOT NULL,fingerprint text); INSERT INTO revora_fan_process_probe(stamp,silver) VALUES(@now,@silver)","now",new DateTimeOffset(fanNow),"silver",(long)before);
        System.Console.WriteLine("[fan-process] hunted="+hunted);System.Console.WriteLine("[fan-process] fan-capture-passed");Environment.Exit(0);
    }
    static void FanFinish() {
        var p=FanPlayer();bool replay=FanStage()==11;
        if(!replay) {
            Require(FanStage()==5 && p.Entity.MapID==1000);ulong silver=p.Entity.Money;
            FanTalk(p,4583,2);Require(p.Map.BaseID==1025 && p.Entity.Money==silver);
            p.Entity.X=54;p.Entity.Y=50;FanKill(p);p.Entity.X=58;p.Entity.Y=50;FanKill(p);TowerUse(p,721806);Require(p.Entity.MapID==1000);
            FanTalk(p,4583,1);TowerUse(p,721802);TowerUse(p,721804);FanTalk(p,4582,1);Require(FanStage()==7);
            p.Entity.X=790;p.Entity.Y=460;for(int i=0;i<5;i++)FanKill(p);FanTalk(p,4582,1);Require(FanStage()==8);
            FanTalk(p,9910430,1);p.Entity.X=475;p.Entity.Y=844;TowerUse(p,721799);FanTalk(p,9910401,1);p.Entity.X=477;p.Entity.Y=844;FanKill(p);Require(p.Entity.MapID==1011);
            FanTalk(p,4582,1);int site=Convert.ToInt32(Sql("SELECT courtesan_site FROM revora_attack_talisman WHERE uid=@uid","uid",(long)FirstUid));
            var points=(ushort[,])Feature("AttackTalismanStore").GetField("CourtesanSites",Hidden).GetValue(null);p.BindWorldMap(1015);p.Entity.X=(ushort)(points[site,0]-2);p.Entity.Y=points[site,1];FanKill(p);
            for(uint npc=9910410;npc<9910414;npc++)FanTalk(p,npc,1);FanTalk(p,9910414,1);Require(FanStage()==10 && p.Map.BaseID==3937);
            int winning=Convert.ToInt32(Sql("SELECT box_winner FROM revora_attack_talisman WHERE uid=@uid","uid",(long)FirstUid)),wrong=(winning+1)%3;
            FanTalk(p,9910420u+(uint)wrong,1);p.Entity.X=(ushort)(52+wrong*6);p.Entity.Y=53;FanKill(p);FanTalk(p,9910420u+(uint)winning,1);FanTalk(p,9910423,1);Require(p.Entity.MapID==1015);
        }
        FanTalk(p,4565,81);FanTalk(p,4565,81);Require(FanStage()==11);
        Require((bool)Sql(@"SELECT EXISTS(SELECT 1 FROM revora_attack_talisman q JOIN entities e ON e.uid=q.uid WHERE q.uid=@uid AND cardinality(q.rewards)=2 AND q.cave_paid
AND e.money=(SELECT silver-20000+900 FROM revora_fan_process_probe)
AND (SELECT count(*) FROM items i WHERE i.entityid=q.uid AND i.uid=ANY(q.rewards) AND i.bound=1 AND i.id IN(201009,q.soul))=2)
AND (SELECT count(*) FROM revora_attack_talisman_purchases WHERE uid=@uid AND price=10000)=1","uid",(long)FirstUid));
        Require(p.Inventory.Objects.Count(i=>i.ID==201009 && i.Bound)==1 && p.Inventory.Objects.Count(i=>i.ID==202008 && i.Bound)==1);
        string fingerprint=(string)Sql("SELECT md5(stage::text||':'||soul::text||':'||rewards::text) FROM revora_attack_talisman WHERE uid=@uid","uid",(long)FirstUid);
        if(replay)Require((string)Sql("SELECT fingerprint FROM revora_fan_process_probe")==fingerprint);
        else Sql("UPDATE revora_fan_process_probe SET stamp=@now,fingerprint=@value","now",new DateTimeOffset(fanNow),"value",fingerprint);
        System.Console.WriteLine("[fan-process] fan-finish-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fan process did not close.");
    }
}
