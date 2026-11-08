using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime towerNow;
    static void PrepareTower(string phase) {
        towerNow=phase=="tower-capture"?DateTime.UtcNow:DateTime.SpecifyKind((DateTime)Sql("SELECT stamp AT TIME ZONE 'UTC' FROM revora_tower_process_probe"),DateTimeKind.Utc);
        Feature("DefenseTalismanStore").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>towerNow));
    }
    static void TowerTalk(GameState p,uint npc,byte option) {
        if(npc!=4567 && npc!=4568 && npc!=4574 && npc!=4578)p.BindWorldMap(npc==4572 || npc==4573?(ushort)1000:(ushort)1015);
        HeartTalk(p,npc,option);
    }
    static void TowerTick(){towerNow=towerNow.AddSeconds(2);Call(Feature("DefenseTalismanQuest"),"Tick");}
    static void TowerKill(GameState p) {
        var body=p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=9905200 && e.MonsterInfo.ID<9905211 && !e.Dead);
        EliminationHit(p,body);TowerTick();
    }
    static void TowerUse(GameState p,uint id){ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==id),p);}
    static GameState TowerPlayer() {
        Require((bool)Feature("DefenseTalismanQuest").GetProperty("Enabled",Hidden).GetValue(null,null));
        Require((bool)Feature("AttackTalismanQuest").GetProperty("Enabled",Hidden).GetValue(null,null));
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Call(Feature("DefenseTalismanQuest"),"OnLogin",p);return p;
    }
    static long TowerStage(){return Convert.ToInt64(Sql("SELECT stage FROM revora_defense_talisman WHERE uid=@uid","uid",(long)FirstUid));}
    static void TowerCapture() {
        Player(Kernel.Maps[1002]);nextUid=FirstUid;var p=TowerPlayer();
        TowerTalk(p,4565,1);TowerTalk(p,4565,1);Require(p.Map.BaseID==1782);
        p.Entity.X=40;p.Entity.Y=57;TowerTick();TowerKill(p);Require(TowerStage()==1);
        Sql("CREATE TABLE revora_tower_process_probe(stamp timestamptz NOT NULL,fingerprint text); INSERT INTO revora_tower_process_probe(stamp) VALUES(@now)","now",new DateTimeOffset(towerNow));
        System.Console.WriteLine("[tower-process] tower-capture-passed");Environment.Exit(0);
    }
    static void TowerFinish() {
        var p=TowerPlayer();bool replay=TowerStage()==10;
        if(!replay) {
            Require(TowerStage()==1 && p.Entity.MapID==1015);
            TowerTalk(p,4565,1);TowerTalk(p,4567,1);Require(TowerStage()==1);
            p.Entity.X=40;p.Entity.Y=57;TowerTick();for(int i=0;i<5;i++)TowerKill(p);
            TowerTalk(p,4567,1);TowerTalk(p,4566,1);Require(TowerStage()==3);
            for(int i=0;i<3;i++){TowerTalk(p,4579,1);p.Entity.X=310;p.Entity.Y=380;TowerUse(p,721779);TowerKill(p);}
            TowerTalk(p,4569,1);TowerTalk(p,4575,1);TowerTalk(p,4576,1);TowerTalk(p,4577,1);Require(TowerStage()==4 && p.Map.BaseID==1784);
            TowerTalk(p,4568,1);TowerTalk(p,4568,2);p.BindWorldMap(1015);p.Entity.X=400;p.Entity.Y=650;
            towerNow=towerNow.AddMinutes(10-towerNow.Minute%10);TowerTick();TowerKill(p);TowerTalk(p,4577,1);TowerTalk(p,4568,1);Require(TowerStage()==5);
            TowerTalk(p,4572,1);p.Entity.X=166;p.Entity.Y=226;TowerTick();TowerKill(p);TowerKill(p);
            TowerTalk(p,4566,1);TowerTalk(p,4566,1);Require(TowerStage()==7 && p.Map.BaseID==1783);
            p.Entity.X=40;p.Entity.Y=57;TowerTick();for(int i=0;i<5;i++)TowerKill(p);
            TowerTalk(p,4574,1);TowerTalk(p,4573,1);Require(TowerStage()==8 && p.Map.BaseID==1785);
            ushort[,] stands={{96,124},{70,64},{113,126},{62,85}};
            for(int i=0;i<4;i++){p.Entity.X=stands[i,0];p.Entity.Y=stands[i,1];TowerTick();TowerKill(p);if(i==1 || i==3){p.Entity.X=(ushort)(i==1?70:62);p.Entity.Y=(ushort)(i==1?56:77);TowerUse(p,721786);}}
            Require(p.Map.BaseID==1787);TowerTick();TowerKill(p);
            for(byte i=10;i<14;i++)TowerTalk(p,4578,i);TowerTalk(p,4578,1);Require(TowerStage()==9 && p.Entity.MapID==1015);
        }
        TowerTalk(p,4565,1);TowerTalk(p,4565,1);Require(TowerStage()==10);
        Require(p.Inventory.Objects.Count(i=>i.ID==202008 && i.Bound)==1);
        Require((bool)Sql(@"SELECT EXISTS(SELECT 1 FROM revora_defense_talisman q WHERE q.uid=@uid AND cardinality(q.rewards)=2
AND (SELECT count(*) FROM items i WHERE i.entityid=q.uid AND i.uid=ANY(q.rewards) AND i.bound=1 AND i.id IN(202008,q.soul))=2)
AND (SELECT count(*) FROM revora_defense_talisman_purchases WHERE uid=@uid AND price=30000)=3","uid",(long)FirstUid));
        string fingerprint=(string)Sql("SELECT md5(stage::text||':'||soul::text||':'||rewards::text) FROM revora_defense_talisman WHERE uid=@uid","uid",(long)FirstUid);
        if(replay)Require((string)Sql("SELECT fingerprint FROM revora_tower_process_probe")==fingerprint);
        else Sql("UPDATE revora_tower_process_probe SET stamp=@now,fingerprint=@value","now",new DateTimeOffset(towerNow),"value",fingerprint);
        System.Console.WriteLine("[tower-process] tower-finish-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Tower process did not close.");
    }
}
