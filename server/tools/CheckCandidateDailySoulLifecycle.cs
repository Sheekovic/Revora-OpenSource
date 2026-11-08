using System;
using System.Linq;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime dailySoulClock;
    static void PrepareDailySouls(bool reload) {
        dailySoulClock=reload?DateTime.SpecifyKind((DateTime)Sql("SELECT planned FROM revora_daily_boss_runs WHERE runkey='2030-01-12/0'"),DateTimeKind.Utc).AddSeconds(1)
            :new DateTime(2030,1,12,0,0,0,DateTimeKind.Local).ToUniversalTime();
        Feature("DailyWorldBosses").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dailySoulClock));
    }
    static void DailySoulTick() {
        var type=Feature("DailyWorldBosses");type.GetField("NextPoll",Hidden).SetValue(null,DateTime.MinValue);Call(type,"Tick");
    }
    static void DailySouls(bool reload) {
        var type=Feature("DailyWorldBosses");
        if(!reload){Player(Kernel.Maps[1002]);nextUid=FirstUid;}
        var p=Player(Kernel.Maps[1002],true);
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake);
        if(!reload) {
            dailySoulClock=DateTime.SpecifyKind((DateTime)Sql("SELECT planned FROM revora_daily_boss_runs WHERE runkey='2030-01-12/0'"),DateTimeKind.Utc).AddSeconds(1);DailySoulTick();
            var active=type.GetField("Active",Hidden).GetValue(null);Require(active!=null);
            var boss=(Entity)active.GetType().GetField("Entity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(active);
            p.BindWorldMap(boss.MapID);p.Entity.X=boss.X;p.Entity.Y=boss.Y;
            uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
            Require((bool)Call(type,"Die",boss,p.Entity));DailySoulTick();
            Require(p.Entity.ConquerPoints==cp+1000000 && p.Entity.RevoraPoint==rvp+50000 && p.Entity.Money==gold+10000000);
        } else {
            Require(p.Inventory.Count==1 && p.Inventory.Objects.Single().Bound);
            Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_daily_soul_probe"));
        }
        var drops=p.Map.FloorItems.Values.Where(d=>(string)typeof(FloorItem).GetField("DailyBossRun",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(d)=="2030-01-12/0").ToArray();
        Require(drops.Length==(reload?3:4) && drops.All(d=>d.Item.Bound && ConquerItemInformation.BaseInformations[d.Item.ID].PurificationLevel==7));
        foreach(var drop in (reload?drops:drops.Take(1))) {
            p.Entity.X=drop.X;p.Entity.Y=drop.Y;
            Call(type,"Pickup",p,drop);Call(type,"Pickup",p,drop);
            Require(p.Inventory.Objects.Any(i=>i.UID==drop.Item.UID && i.Bound));
            Require((long)Sql("SELECT count(*) FROM items WHERE uid=@item AND entityid=@uid AND bound=1","item",(long)drop.Item.UID,"uid",(long)p.Entity.UID)==1);
        }
        Require(p.Inventory.Count==(reload?4:1) && (long)Sql("SELECT count(*) FROM revora_daily_boss_loot WHERE runkey='2030-01-12/0' AND claimedby=@uid","uid",(long)p.Entity.UID)==p.Inventory.Count);
        Require(EntityTable.SaveEntity(p));
        if(!reload)Sql("CREATE TABLE revora_daily_soul_probe(fingerprint text); INSERT INTO revora_daily_soul_probe VALUES(@f)","f",TaoistFingerprint(p));
        System.Console.WriteLine("[daily-process] daily-souls"+(reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new Exception("Daily soul probe did not close.");
    }
}
