using System;
using System.Collections.Concurrent;
using System.Linq;
using Revora;
using Revora.Database;

internal static partial class CheckCandidateArenaLifecycle
{
    static void SummerLateCredit() {
        var summer=Feature("SummerHunt");var clock=summer.GetField("Clock",Hidden);
        DateTime before=new DateTime(2033,2,5,7,59,59,DateTimeKind.Utc),after=before.AddSeconds(2);
        var p=Player(Kernel.Maps[1002]);p.Entity.Level=120;
        clock.SetValue(null,(Func<DateTime>)(()=>before));HeartTalk(p,900000063,1);
        // Supply only the pre-final-kill counter to isolate this concurrency
        // boundary; this fixture does not claim a complete earned 120-kill run.
        Sql("UPDATE revora_summer_daily SET kills=119 WHERE uid=@uid AND day='20330204'","uid",(long)p.Entity.UID);
        int route=Convert.ToInt32(Sql("SELECT route FROM revora_summer_daily WHERE uid=@uid AND day='20330204'","uid",(long)p.Entity.UID));
        ushort map=route==0?(ushort)1015:(ushort)1001;uint target=new uint[]{18,20,57,58}[route];
        p.BindWorldMap(map);
        Sql(@"CREATE FUNCTION revora_summer_credit_probe() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN PERFORM pg_advisory_xact_lock(19330203); RETURN NEW; END $$;
CREATE TRIGGER revora_summer_credit_probe BEFORE UPDATE ON revora_summer_daily
FOR EACH ROW WHEN (OLD.kills=119 AND NEW.kills=120) EXECUTE FUNCTION revora_summer_credit_probe();");
        System.Threading.Tasks.Task pending=null;
        using(var hold=DataHolder.MySqlConnection) {
            hold.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT pg_advisory_lock(19330203)",hold))q.ExecuteNonQuery();
            try {
                ushort x=p.Entity.X,y=p.Entity.Y;
                pending=System.Threading.Tasks.Task.Run(()=>Call(summer,"Credit",p.Entity,map,x,y,target,before));
                var deadline=DateTime.UtcNow.AddSeconds(5);bool waiting=false;
                while(DateTime.UtcNow<deadline && !waiting) {
                    waiting=(bool)Sql("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND objid=19330203 AND NOT granted)");
                    if(!waiting)System.Threading.Thread.Sleep(25);
                }
                Require(waiting);
                clock.SetValue(null,(Func<DateTime>)(()=>after));p.BindWorldMap(1002);HeartTalk(p,900000063,1);
                Require((long)Sql("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid AND day='20330205' AND kills=0","uid",(long)p.Entity.UID)==1);
            }finally {using(var q=new Npgsql.NpgsqlCommand("SELECT pg_advisory_unlock(19330203)",hold))q.ExecuteNonQuery();}
        }
        Require(pending!=null && pending.Wait(5000));
        Sql("DROP TRIGGER revora_summer_credit_probe ON revora_summer_daily; DROP FUNCTION revora_summer_credit_probe()");
        var active=(ConcurrentDictionary<uint,string>)summer.GetField("active",Hidden).GetValue(null);string day;
        Require(active.TryGetValue(p.Entity.UID,out day) && day=="20330205");
        Require((int)Sql("SELECT kills FROM revora_summer_daily WHERE uid=@uid AND day='20330204'","uid",(long)p.Entity.UID)==120);
        Require((int)Sql("SELECT kills FROM revora_summer_daily WHERE uid=@uid AND day='20330205'","uid",(long)p.Entity.UID)==0);
    }
    static void SummerCleanupProcess() {
        var summer=Feature("SummerHunt");var clock=summer.GetField("Clock",Hidden);
        DateTime before=new DateTime(2033,2,2,7,59,59,DateTimeKind.Utc),after=before.AddSeconds(2);
        Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=120 WHERE uid=@uid","uid",(long)FirstUid);
        nextUid=FirstUid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player);
        clock.SetValue(null,(Func<DateTime>)(()=>before));HeartTalk(p,900000063,1);
        var active=(ConcurrentDictionary<uint,string>)summer.GetField("active",Hidden).GetValue(null);
        string day;Require(active.TryGetValue(FirstUid,out day) && day=="20330201");
        int calls=0;bool accepted=false;
        var ownerThread=System.Threading.Thread.CurrentThread;
        clock.SetValue(null,(Func<DateTime>)(()=>{
            if(System.Threading.Thread.CurrentThread!=ownerThread)return before;
            if(++calls==4) {
                // Resume a cleanup clock read after the next day's NPC acceptance.
                // The delayed read still returns the pre-midnight value.
                clock.SetValue(null,(Func<DateTime>)(()=>after));HeartTalk(p,900000063,1);accepted=true;
                Require(active.TryGetValue(FirstUid,out day) && day=="20330202");
            }
            return before;
        }));
        summer.GetField("nextCleanup",Hidden).SetValue(null,DateTime.MinValue);Call(summer,"Tick");
        Require(accepted && active.TryGetValue(FirstUid,out day) && day=="20330202");
        Require((long)Sql("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid AND kills=0 AND NOT claimed","uid",(long)FirstUid)==2);
        // Current admission survives normal cleanup; an expired admission does not.
        summer.GetField("nextCleanup",Hidden).SetValue(null,DateTime.MinValue);Call(summer,"Tick");Require(active.ContainsKey(FirstUid));
        int route=Convert.ToInt32(Sql("SELECT route FROM revora_summer_daily WHERE uid=@uid AND day='20330202'","uid",(long)FirstUid));
        ushort map=route==0?(ushort)1015:(ushort)1001;uint target=new uint[]{18,20,57,58}[route];
        p.BindWorldMap(map);var monster=p.Map.Entities.Values.First(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==target && !e.Dead);
        EliminationHit(p,monster);
        Require((int)Sql("SELECT kills FROM revora_summer_daily WHERE uid=@uid AND day='20330202'","uid",(long)FirstUid)==1);
        clock.SetValue(null,(Func<DateTime>)(()=>after.AddDays(1)));
        summer.GetField("nextCleanup",Hidden).SetValue(null,DateTime.MinValue);Call(summer,"Tick");Require(!active.ContainsKey(FirstUid));
        Require((long)Sql("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid AND NOT claimed","uid",(long)FirstUid)==2);
        Require((long)Sql("SELECT sum(kills) FROM revora_summer_daily WHERE uid=@uid","uid",(long)FirstUid)==1);
        SummerLateCredit();
        System.Console.WriteLine("[summer-process] summer-cleanup-passed");Server.CommandsAI("@close");throw new Exception("Summer cleanup did not close.");
    }
}
