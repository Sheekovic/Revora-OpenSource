using System;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    // Inactive until coordinated reset, payout and lifecycle cutover.
    internal static class ArenaRankingSettlement
    {
        private static bool initialized;
        private static readonly object ScheduleGate=new object();
        private static DateTime lastProbe;
        // The match scheduler drains owned completions first. A pending result leaves
        // its day open; retry later without touching player caches or abandoning it.
        internal static bool Tick() {
            if(!ArenaStartupPreparation.Prepared || !initialized || !Monitor.TryEnter(ScheduleGate))return false;
            try {
                if(!ArenaStartupPreparation.Prepared)return false;
                var now=ArenaMatchLedger.Clock();
                if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena ranking schedule requires UTC.");
                if(lastProbe!=default(DateTime) && now>=lastProbe && now-lastProbe<TimeSpan.FromSeconds(30))return false;
                lastProbe=now;
                ClosePending(1);
                return true;
            }catch(Exception ex) {
                Console.WriteLine("[arena-ranking] error="+ex.GetType().Name);
                return false;
            }finally{Monitor.Exit(ScheduleGate);}
        }
        internal static uint Honor(uint rank){return rank>=1 && rank<=1000?(1001-rank)*1000:0;}
        internal static void Initialize() {
            ArenaBotStore.Initialize();
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_rank_days(
day varchar(8) PRIMARY KEY,sealed_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_arena_rank_rewards(
day varchar(8) NOT NULL REFERENCES revora_arena_rank_days(day),uid bigint NOT NULL,rank bigint NOT NULL CHECK(rank>0),
points bigint NOT NULL CHECK(points BETWEEN 0 AND 4294967295),honor bigint NOT NULL CHECK(honor BETWEEN 0 AND 1000000),
source_run uuid NOT NULL REFERENCES revora_arena_matches(id),PRIMARY KEY(day,uid),UNIQUE(day,rank));
ALTER TABLE revora_arena_rank_rewards ADD COLUMN IF NOT EXISTS paid_at timestamptz;
CREATE INDEX IF NOT EXISTS revora_arena_rank_pending ON revora_arena_rank_rewards(uid,day) WHERE paid_at IS NULL;
");}initialized=true;
        }
        private static bool Ready(GameState p){return initialized && Q.Player(p) && p.ArenaStatistic!=null && p.TeamArenaStatistic!=null
            && p.ArenaStatistic.EntityID==p.Entity.UID && p.TeamArenaStatistic.EntityID==p.Entity.UID
            && p.ArenaStatistic.CurrentHonor==p.TeamArenaStatistic.CurrentHonor && p.ArenaStatistic.HistoryHonor==p.TeamArenaStatistic.HistoryHonor
            && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        internal static void Pay(GameState p) {
            if(!Ready(p))return;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p))return;uint before=p.CurrentHonor,history=p.HistoryHonor,award;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);string day;long rank;
                    using(var q=Q.Command(c,t,"SELECT day,rank,honor FROM revora_arena_rank_rewards WHERE uid=@uid AND paid_at IS NULL ORDER BY day LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){if(!r.Read())return;day=r.GetString(0);rank=r.GetInt64(1);award=checked((uint)r.GetInt64(2));}
                    if(rank<1 || award!=(rank>uint.MaxValue?0:Honor((uint)rank)))throw new InvalidOperationException("Arena ranking award differs from its saved rank.");
                    if(before>uint.MaxValue-award || history>uint.MaxValue-award)return;
                    foreach(string table in new[]{"arena","teamarena"}) {
                        using(var q=Q.Command(c,t,"SELECT COALESCE(currenthonor,0),COALESCE(historyhonor,0) FROM "+table+" WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToUInt32(r[0])!=before || Convert.ToUInt32(r[1])!=history || r.Read())throw new InvalidOperationException("Arena ranking Honor mirror requires reload.");
                        if(Q.Execute(c,t,"UPDATE "+table+" SET currenthonor=@honor,historyhonor=@history WHERE entityid=@uid",
                            "honor",(long)before+award,"history",(long)history+award,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Arena ranking Honor was not saved.");
                    }
                    if(Q.Execute(c,t,"UPDATE revora_arena_rank_rewards SET paid_at=@now WHERE uid=@uid AND day=@day AND paid_at IS NULL",
                        "now",ArenaMatchLedger.Clock(),"uid",(long)p.Entity.UID,"day",day)!=1)throw new InvalidOperationException("Arena ranking payment receipt changed.");
                    if(award>0)ProgressionEvidence.Complete(c,t,p.Entity.UID,10320,"honor-arena-ranking",day,1);
                    if(!Ready(p) || p.CurrentHonor!=before || p.HistoryHonor!=history)throw new InvalidOperationException("Arena ranking recipient changed.");t.Commit();
                }}
                p.CurrentHonor=before+award;p.HistoryHonor=history+award;p.ArenaStatistic.Send(p);p.TeamArenaStatistic.Send(p);Q.Refresh(p);
                if(award!=0)p.Send("Your daily Arena ranking earned "+award+" Honor Points.");
            }catch(Exception ex){ArenaCacheRecovery.Fail(p,"arena-ranking-pay",ex);}
        }
        internal static void EnsureOpen(NpgsqlConnection c,NpgsqlTransaction t,string day) {
            if(!initialized)return;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_arena_rank_days WHERE day=@day)","day",day))
                if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Arena result day was already settled.");
        }
        internal static int ClosePending(int limit) {
            if(limit<1 || limit>31)throw new ArgumentOutOfRangeException("limit");
            var now=ArenaMatchLedger.Clock();if(!initialized || now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena ranking catch-up requires initialized UTC state.");
            var days=new List<string>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT DISTINCT m.day FROM revora_arena_matches m
WHERE m.state='finished' AND m.day<@today AND NOT EXISTS(SELECT 1 FROM revora_arena_rank_days d WHERE d.day=m.day)
ORDER BY m.day LIMIT @limit","today",MagnoliaQuest.Day(now),"limit",limit))using(var r=q.ExecuteReader())while(r.Read())days.Add(r.GetString(0));}
            int settled=0;foreach(string day in days)if(Close(day))settled++;return settled;
        }
        internal static bool Close(string day) {
            DateTime parsed;var now=ArenaMatchLedger.Clock();
            if(!initialized || now.Kind!=DateTimeKind.Utc || day==null || day.Length!=8 || !DateTime.TryParseExact(day,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed)
                || string.CompareOrdinal(day,MagnoliaQuest.Day(now))>=0)throw new InvalidOperationException("Arena ranking requires a completed Pacific day.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                // Prevent result insertion/update racing the snapshot; no player caches are mutated.
                Q.Execute(c,t,"LOCK TABLE revora_arena_matches IN SHARE ROW EXCLUSIVE MODE");
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_arena_rank_days WHERE day=@day)","day",day))if(Equals(q.ExecuteScalar(),true))return false;
                using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_arena_matches m LEFT JOIN revora_arena_result_intents i ON i.run=m.id
WHERE m.state='active' AND NOT EXISTS(SELECT 1 FROM revora_arena_abandoned a WHERE a.run=m.id)
AND COALESCE(i.day,to_char(m.started_at AT TIME ZONE 'America/Los_Angeles','YYYYMMDD'))<=@day)","day",day))
                    if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Pending Arena outcomes must be recovered before ranking.");
                using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_arena_result_statistics s JOIN revora_arena_matches m ON m.id=s.run
WHERE m.state='finished' AND m.day=@day AND NOT s.ordering_verified)","day",day))
                    if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Historical Arena result ordering is unverified.");
                if(Q.Execute(c,t,"INSERT INTO revora_arena_rank_days(day,sealed_at) VALUES(@day,@now)","day",day,"now",now)!=1)throw new InvalidOperationException("Arena rank day was not saved.");
                long expected;using(var q=Q.Command(c,t,@"SELECT count(DISTINCT s.uid) FROM revora_arena_result_statistics s JOIN revora_arena_matches m ON m.id=s.run WHERE m.state='finished' AND m.day=@day AND NOT EXISTS(SELECT 1 FROM revora_arena_bots b WHERE b.uid=s.uid)","day",day))expected=Convert.ToInt64(q.ExecuteScalar());
                int inserted=Q.Execute(c,t,@"WITH last_result AS (
SELECT DISTINCT ON(s.uid) s.uid,s.run,s.points_after FROM revora_arena_result_statistics s JOIN revora_arena_matches m ON m.id=s.run
WHERE m.state='finished' AND m.day=@day AND NOT EXISTS(SELECT 1 FROM revora_arena_bots b WHERE b.uid=s.uid) ORDER BY s.uid,s.result_order DESC), ranked AS (
SELECT uid,run,points_after,row_number() OVER(ORDER BY points_after DESC,uid) AS rank FROM last_result)
INSERT INTO revora_arena_rank_rewards(day,uid,rank,points,honor,source_run)
SELECT @day,uid,rank,points_after,CASE WHEN rank<=1000 THEN (1001-rank)*1000 ELSE 0 END,run FROM ranked","day",day);
                if(inserted!=expected)throw new InvalidOperationException("Arena rank rewards were not all saved.");
                t.Commit();return true;
            }}
        }
    }
}
