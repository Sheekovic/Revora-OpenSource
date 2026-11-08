using System;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertExpeditionStore
    {
        internal const int PillSeconds=60;
        internal const int PillInterval=10;
        internal const int PillTicks=6;

        private static DateTime PillNow(){
            var now=Clock();
            if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Desert Expedition clock must be UTC.");
            // Match PostgreSQL timestamp precision before persisting the deadline.
            return new DateTime(now.Ticks-now.Ticks%10,DateTimeKind.Utc);
        }
        internal static int PillDue(DateTime until,DateTime now,int paid){
            if(until.Kind!=DateTimeKind.Utc || now.Kind!=DateTimeKind.Utc || paid<0 || paid>PillTicks)
                throw new InvalidOperationException("Desert Expedition pill schedule is invalid.");
            DateTime start=until.AddSeconds(-PillSeconds);
            if(now<start.AddSeconds(paid*PillInterval))
                throw new InvalidOperationException("Desert Expedition pill clock moved backwards.");
            int due=now>=until?PillTicks:(int)((now-start).Ticks/TimeSpan.FromSeconds(PillInterval).Ticks);
            return due-paid;
        }
        // Revora rounding: floor each 10-percent debit, minimum one, leaving one HP.
        // Missed debits compound against the current HP at settlement, at most six.
        internal static uint PillHealth(uint hp,int ticks){
            if(ticks<0 || ticks>PillTicks)throw new ArgumentOutOfRangeException("ticks");
            for(int i=0;i<ticks && hp>1;i++)hp-=Math.Min(hp-1,Math.Max(1u,hp/10));
            return hp;
        }
        // Callers hold ItemSyncRoot before the health gate. Do not expose through
        // live hooks until compound combat/healing mutations share this boundary.
        internal static int SettlePill(GameState p,Func<bool> context){
            Ready(p,context);
            return p.Entity.WithHealthMutation(()=>{
                int ticks,paid;uint before=p.Entity.Hitpoints,after;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=2 || !run.PillUntil.HasValue)return 0;
                    ticks=PillDue(run.PillUntil.Value,PillNow(),run.PillPaid);if(ticks==0)return 0;
                    paid=run.PillPaid+ticks;after=PillHealth(before,ticks);
                    HealthBalance.Persist(c,t,p,after,p.Entity.Mana);
                    if(Q.Execute(c,t,"UPDATE revora_desert_expedition SET pill_paid=@paid WHERE uid=@uid AND life=@life AND pill_until=@until AND pill_paid=@before",
                        "paid",run.PillPaid+ticks,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"until",run.PillUntil.Value,"before",run.PillPaid)!=1)
                        throw new InvalidOperationException("Desert Expedition pill progress changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_desert_expedition_pill_payments(uid,life,until_at,paid_before,paid_after,hp_before,hp_after)
VALUES(@uid,@life,@until,@before,@after,@hp_before,@hp_after)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"until",run.PillUntil.Value,
                        "before",run.PillPaid,"after",run.PillPaid+ticks,"hp_before",(long)before,"hp_after",(long)after)!=1)
                        throw new InvalidOperationException("Desert Expedition pill receipt was not saved.");
                    Ready(p,context);t.Commit();
                }}HealthBalance.Committed(p,after,p.Entity.Mana);p.Entity.Hitpoints=after;p.Entity.HealthSynchronizationRequired=paid<PillTicks;return ticks;
            });
        }
        internal static bool ActivatePill(GameState p,uint item,Func<bool> context){
            Ready(p,context);if(p.Entity.MapID!=1001)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);
                if(run.Stage!=2 || !Material(c,t,p,item,721920))return false;
                DateTime now=PillNow();
                if(run.PillUntil.HasValue){
                    PillDue(run.PillUntil.Value,now,run.PillPaid);
                    // Neither repeated clicks nor expiry can erase unsettled HP cost.
                    if(now<run.PillUntil.Value || run.PillPaid!=PillTicks)return false;
                }
                if(Q.Execute(c,t,"UPDATE revora_desert_expedition SET pill_until=@until,pill_paid=0 WHERE uid=@uid AND life=@life AND stage=2",
                    "until",now.AddSeconds(PillSeconds),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)
                    throw new InvalidOperationException("Desert Expedition pill activation changed.");
                Ready(p,()=>context() && p.Entity.MapID==1001);t.Commit();
            }}p.Entity.HealthSynchronizationRequired=true;return true;
        }
    }
}
