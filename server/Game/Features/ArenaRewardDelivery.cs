using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class ArenaRewardDelivery
    {
        private sealed class Schedule {internal DateTime Last;}
        private static readonly ConditionalWeakTable<GameState,Schedule> schedules=new ConditionalWeakTable<GameState,Schedule>();
        private static bool initialized;
        internal static void Initialize(){initialized=true;}
        private static bool Ready(GameState p){return initialized && Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        // One probe per healthy session per 30 seconds, at most one payment from each family.
        // New sessions naturally probe immediately; durable payment receipts own replay protection.
        internal static bool Tick(GameState p) {
            if(!Ready(p) || !Monitor.TryEnter(p.ItemSyncRoot))return false;
            try {
                if(!Ready(p))return false;
                var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena delivery requires UTC.");
                var schedule=schedules.GetOrCreateValue(p);
                if(schedule.Last!=default(DateTime) && now>=schedule.Last && now-schedule.Last<TimeSpan.FromSeconds(30))return false;
                schedule.Last=now;bool daily,custom,ranking;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT
EXISTS(SELECT 1 FROM revora_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL),
EXISTS(SELECT 1 FROM revora_arena_custom_rewards WHERE uid=@uid AND eligible AND paid_at IS NULL),
EXISTS(SELECT 1 FROM revora_arena_rank_rewards WHERE uid=@uid AND paid_at IS NULL)","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader()){r.Read();daily=r.GetBoolean(0);custom=r.GetBoolean(1);ranking=r.GetBoolean(2);}}
                if(daily)ArenaDailyRewards.Pay(p);
                if(TeamArenaDailyRewards.Initialized && Ready(p))TeamArenaDailyRewards.Pay(p);
                if(custom && Ready(p))ArenaCustomRewards.Pay(p);
                if(ranking && Ready(p))ArenaRankingSettlement.Pay(p);
                return true;
            }catch(Exception ex) {
                // Probe failure has made no monetary mutation; payment methods contain/fence their own failures.
                Console.WriteLine("[arena-delivery] uid="+p.Entity.UID+" error="+ex.GetType().Name);return false;
            }finally{Monitor.Exit(p.ItemSyncRoot);}
        }
    }
}
