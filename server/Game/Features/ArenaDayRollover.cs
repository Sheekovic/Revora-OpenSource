using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    // Transaction participant; lifecycle must call before admitting an idle player to a new day's match.
    internal static class ArenaDayRollover
    {
        internal static bool Initialized {get;private set;}
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_day_rollovers(
uid bigint NOT NULL,day varchar(8) NOT NULL,points_before bigint NOT NULL,points_after bigint NOT NULL,
retained_results boolean NOT NULL,rolled_at timestamptz NOT NULL,PRIMARY KEY(uid,day));
ALTER TABLE revora_arena_day_rollovers ADD COLUMN IF NOT EXISTS team_points_before bigint;");}Initialized=true;
        }
        internal static bool Managed(uint uid) {
            if(!Initialized && !TeamArenaResultStatistics.Initialized)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();return Managed(uid,c);}
        }
        internal static bool Managed(uint uid,Npgsql.NpgsqlConnection c) {
            if(TeamArenaResultStatistics.Managed(uid,c))return true;
            if(!Initialized)return false;
            using(var q=Q.Command(c,null,@"SELECT EXISTS(SELECT 1 FROM revora_arena_day_rollovers WHERE uid=@uid)
OR EXISTS(SELECT 1 FROM revora_arena_result_statistics WHERE uid=@uid)
OR EXISTS(SELECT 1 FROM revora_arena_outcome_snapshots WHERE uid=@uid)","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static bool Invitation(GameState p,GameState opponent) {return opponent!=null && ArenaActors.Player(opponent)
            && Monitor.IsEntered(p.ItemSyncRoot) && Monitor.IsEntered(opponent.ItemSyncRoot)
            && opponent.ArenaStatistic!=null && p.ArenaStatistic.AcceptBox && opponent.ArenaStatistic.AcceptBox
            && p.ArenaState==Qualifier.WaitForOther && opponent.ArenaState==Qualifier.WaitForOther
            && ReferenceEquals(p.ArenaStatistic.PlayWith,opponent) && ReferenceEquals(opponent.ArenaStatistic.PlayWith,p);}
        private static bool Ready(GameState p,GameState opponent){return Initialized && ArenaActors.Player(p)
            && p.QualifierGroup==null && p.TeamQualifierGroup==null && p.WatchingGroup==null && p.TeamWatchingGroup==null
            && !Kernel.DisconnectPool.ContainsKey(p.Entity.UID) && p.ArenaStatistic!=null && p.TeamArenaStatistic!=null
            && p.ArenaStatistic.EntityID==p.Entity.UID && p.TeamArenaStatistic.EntityID==p.Entity.UID
            && (opponent==null?p.ArenaStatistic.PlayWith==null:Invitation(p,opponent)) && p.TeamArenaStatistic.PlayWith==null
            && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        internal static bool Prepare(GameState p) {return PrepareCore(p,null);}
        // Both actor locks are held by the accepted-invitation caller. Neither
        // player has entered a match; unrelated invitations remain ineligible.
        internal static bool PrepareAccepted(GameState a,GameState b) {
            if(ReferenceEquals(a,b) || !Ready(a,b) || !Ready(b,a))return false;
            var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena admission requires UTC.");
            return PrepareCore(a,b) && PrepareCore(b,a) && Ready(a,b) && Ready(b,a)
                && MagnoliaQuest.Day(now)==MagnoliaQuest.Day(ArenaMatchLedger.Clock());
        }
        private static bool PrepareCore(GameState p,GameState opponent) {
            if(!Ready(p,opponent))return false;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p,opponent))return false;
                var now=ArenaMatchLedger.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena rollover requires UTC.");
                string day=MagnoliaQuest.Day(now),previous=DateTime.ParseExact(day,"yyyyMMdd",CultureInfo.InvariantCulture).AddDays(-1).ToString("yyyyMMdd",CultureInfo.InvariantCulture);
                byte level=p.Entity.Level;var stats=p.ArenaStatistic;var before=ArenaResultIntents.Statistics(stats);uint points,matches,wins,rank=0,lastPoints=0,lastWins=0,lastLosses=0;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    if(TeamArenaResultStatistics.Pending(c,t,p.Entity.UID))return false;
                    using(var q=Q.Command(c,t,"SELECT level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))if(Convert.ToByte(q.ExecuteScalar())!=level)throw new InvalidOperationException("Arena rollover level requires reload.");
                    using(var q=Q.Command(c,t,"SELECT max(day) FROM revora_arena_day_rollovers WHERE uid=@uid","uid",(long)p.Entity.UID)) {
                        var latest=q.ExecuteScalar() as string;if(latest!=null && string.CompareOrdinal(latest,day)>=0)return latest==day;
                    }
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_arena_matches m WHERE m.state='active' AND (m.player1=@uid OR m.player2=@uid)
AND NOT EXISTS(SELECT 1 FROM revora_arena_abandoned a WHERE a.run=m.id))","uid",(long)p.Entity.UID))if(Equals(q.ExecuteScalar(),true))return false;
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_arena_matches WHERE state='finished' AND day=@day)
AND NOT EXISTS(SELECT 1 FROM revora_arena_rank_days WHERE day=@day)","day",previous))if(Equals(q.ExecuteScalar(),true))return false;
                    using(var q=Q.Command(c,t,"SELECT arenapoints,todaywin,todaybattles,totalwin,totallose FROM arena WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())if(!r.Read() || Enumerable.Range(0,5).Any(i=>Convert.ToInt64(r[i])!=before[i]) || r.Read())throw new InvalidOperationException("Arena rollover statistic preimage changed.");
                    // Legacy independent loaders could leave divergent mirrors before this player
                    // entered the durable lifecycle. Only that first adoption may reconcile them.
                    bool managed=Managed(p.Entity.UID,c);uint teamBefore;
                    using(var q=Q.Command(c,t,"SELECT arenapoints FROM teamarena WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){
                        if(!r.Read())throw new InvalidOperationException("Arena rollover point mirror missing.");
                        teamBefore=Convert.ToUInt32(r[0]);
                        if((managed && teamBefore!=stats.ArenaPoints) || r.Read())throw new InvalidOperationException("Arena rollover point mirror changed.");
                    }
                    using(var q=Q.Command(c,t,@"SELECT count(*),count(*) FILTER(WHERE s.won) FROM revora_arena_result_statistics s JOIN revora_arena_matches m ON m.id=s.run
WHERE s.uid=@uid AND m.state='finished' AND m.day=@day","uid",(long)p.Entity.UID,"day",day))
                    using(var r=q.ExecuteReader()){r.Read();matches=checked((uint)r.GetInt64(0));wins=checked((uint)r.GetInt64(1));}
                    using(var q=Q.Command(c,t,"SELECT rank,points FROM revora_arena_rank_rewards WHERE uid=@uid AND day=@day","uid",(long)p.Entity.UID,"day",previous))
                    using(var r=q.ExecuteReader())if(r.Read()){rank=checked((uint)r.GetInt64(0));lastPoints=checked((uint)r.GetInt64(1));}
                    using(var q=Q.Command(c,t,@"SELECT count(*) FILTER(WHERE s.won),count(*) FILTER(WHERE NOT s.won) FROM revora_arena_result_statistics s JOIN revora_arena_matches m ON m.id=s.run
WHERE s.uid=@uid AND m.state='finished' AND m.day=@day","uid",(long)p.Entity.UID,"day",previous))
                    using(var r=q.ExecuteReader()){r.Read();lastWins=checked((uint)r.GetInt64(0));lastLosses=checked((uint)r.GetInt64(1));}
                    bool retained=matches>0;
                    if(!retained && TeamArenaResultStatistics.Initialized)
                        using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_statistics s
JOIN revora_team_arena_results m ON m.id=s.run WHERE s.uid=@uid
AND (m.completed_at AT TIME ZONE 'America/Los_Angeles')::date=to_date(@day,'YYYYMMDD'))",
                            "uid",(long)p.Entity.UID,"day",day))retained=Equals(q.ExecuteScalar(),true);
                    points=retained?stats.ArenaPoints:ArenaTable.ArenaPointFill(level);
                    if(Q.Execute(c,t,@"UPDATE arena SET arenapoints=@points,todaywin=@wins,todaybattles=@matches,lastseasonrank=@rank,lastseasonarenapoints=@lastpoints,
lastseasonwin=@lastwins,lastseasonlose=@lastlosses,arenapointfill=@stamp WHERE entityid=@uid","points",(long)points,"wins",(long)wins,"matches",(long)matches,
                        "rank",(long)rank,"lastpoints",(long)lastPoints,"lastwins",(long)lastWins,"lastlosses",(long)lastLosses,"stamp",now.Ticks,"uid",(long)p.Entity.UID)!=1
                        || Q.Execute(c,t,"UPDATE teamarena SET arenapoints=@points WHERE entityid=@uid","points",(long)points,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Arena rollover was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_arena_day_rollovers(uid,day,points_before,points_after,retained_results,rolled_at,team_points_before)
VALUES(@uid,@day,@before,@after,@retained,@now,@teamBefore)","uid",(long)p.Entity.UID,"day",day,"before",before[0],"after",(long)points,"retained",retained,"now",now,"teamBefore",(long)teamBefore)!=1)
                        throw new InvalidOperationException("Arena rollover receipt was not saved.");
                    if(!Ready(p,opponent) || p.Entity.Level!=level || !ReferenceEquals(stats,p.ArenaStatistic) || !before.SequenceEqual(ArenaResultIntents.Statistics(stats)))throw new InvalidOperationException("Arena rollover owner changed.");
                    if(MagnoliaQuest.Day(ArenaMatchLedger.Clock())!=day)return false;t.Commit();
                }}
                p.ArenaPoints=points;stats.TodayWin=wins;stats.TodayBattles=matches;stats.LastSeasonRank=rank;stats.LastSeasonArenaPoints=lastPoints;
                stats.LastSeasonWin=lastWins;stats.LastSeasonLose=lastLosses;stats.LastArenaPointFill=now;
                stats.Send(p);p.TeamArenaStatistic.Send(p);return true;
            }catch(Exception ex){ArenaCacheRecovery.Fail(p,"arena-day-rollover",ex);return false;}
        }
    }
}
