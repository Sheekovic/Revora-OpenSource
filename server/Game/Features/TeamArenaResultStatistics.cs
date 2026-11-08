using System;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using Group = Revora.Game.TeamQualifier.QualifierList.QualifierGroup;

namespace Revora.Game.Features
{
    // Transaction participant only. Do not activate before team recovery and save ownership migrate together.
    internal static class TeamArenaResultStatistics
    {
        internal static bool Initialized { get; private set; }
        internal sealed class Snapshot
        {
            internal GameState Player;
            internal TeamQualifierStatistic Stats;
            internal QualifierStatistic Solo;
            internal uint[] Before, After;
            internal bool Won, Leader;
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_team_arena_results(
id uuid PRIMARY KEY,winner bigint NOT NULL,loser bigint NOT NULL,delta integer NOT NULL CHECK(delta BETWEEN 30 AND 49),
participants integer NOT NULL CHECK(participants BETWEEN 2 AND 10),completed_at timestamptz NOT NULL,CHECK(winner<>loser));
CREATE TABLE IF NOT EXISTS revora_team_arena_statistics(
run uuid NOT NULL REFERENCES revora_team_arena_results(id),uid bigint NOT NULL,won boolean NOT NULL,leader boolean NOT NULL,
points_before bigint NOT NULL,points_after bigint NOT NULL,wins_before bigint NOT NULL,wins_after bigint NOT NULL,
battles_before bigint NOT NULL,battles_after bigint NOT NULL,totalwin_before bigint NOT NULL,totalwin_after bigint NOT NULL,
totallose_before bigint NOT NULL,totallose_after bigint NOT NULL,PRIMARY KEY(run,uid));
CREATE INDEX IF NOT EXISTS revora_team_arena_statistics_owner ON revora_team_arena_statistics(uid);
CREATE TABLE IF NOT EXISTS revora_team_arena_intents (LIKE revora_team_arena_results INCLUDING ALL);
ALTER TABLE revora_team_arena_intents ADD COLUMN IF NOT EXISTS counter_version integer NOT NULL DEFAULT 1 CHECK(counter_version IN(1,2));
CREATE TABLE IF NOT EXISTS revora_team_arena_intent_statistics (
LIKE revora_team_arena_statistics INCLUDING ALL,FOREIGN KEY(run) REFERENCES revora_team_arena_intents(id));
CREATE INDEX IF NOT EXISTS revora_team_arena_intent_owner ON revora_team_arena_intent_statistics(uid);");
            }
            TeamArenaDailyRewards.Initialize();TeamArenaDayRollover.Initialize();Initialized = true;
        }
        internal static bool Managed(uint uid)
        {
            if (!Initialized) return false;
            using (var c = DataHolder.MySqlConnection) { c.Open(); return Managed(uid, c); }
        }
        internal static bool Managed(uint uid, NpgsqlConnection c)
        {
            if (!Initialized) return false;
            if(TeamArenaAdmissions.Pending(c,null,uid))return true;
            using (var q = Q.Command(c, null, "SELECT EXISTS(SELECT 1 FROM revora_team_arena_statistics WHERE uid=@uid) OR EXISTS(SELECT 1 FROM revora_team_arena_intent_statistics WHERE uid=@uid) OR EXISTS(SELECT 1 FROM revora_team_arena_day_rollovers WHERE uid=@uid)", "uid", (long)uid))
                return Equals(q.ExecuteScalar(), true);
        }
        // Caller holds the entity row lock through the guarded mutation.
        internal static bool Pending(NpgsqlConnection c, NpgsqlTransaction t, uint uid, Guid? except = null)
        {
            if (!Initialized) return false;
            if(TeamArenaAdmissions.Pending(c,t,uid,except))return true;
            using (var q = Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_intents i
WHERE (i.winner=@uid OR i.loser=@uid OR EXISTS(SELECT 1 FROM revora_team_arena_intent_statistics own WHERE own.run=i.id AND own.uid=@uid))
AND (NOT @excepted OR i.id<>@except)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_results r WHERE r.id=i.id
AND (r.winner,r.loser,r.delta,r.participants,r.completed_at)=(i.winner,i.loser,i.delta,i.participants,i.completed_at)
AND (SELECT count(*) FROM revora_team_arena_statistics s WHERE s.run=i.id)=i.participants
AND (SELECT count(*) FROM revora_team_arena_intent_statistics s WHERE s.run=i.id)=i.participants
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_intent_statistics x LEFT JOIN revora_team_arena_statistics s ON s.run=x.run AND s.uid=x.uid
WHERE x.run=i.id AND (s.uid IS NULL OR (s.won,s.leader,s.points_before,s.points_after,s.wins_before,s.wins_after,s.battles_before,s.battles_after,s.totalwin_before,s.totalwin_after,s.totallose_before,s.totallose_after)
IS DISTINCT FROM (x.won,x.leader,x.points_before,x.points_after,x.wins_before,x.wins_after,x.battles_before,x.battles_after,x.totalwin_before,x.totalwin_after,x.totallose_before,x.totallose_after)))))",
                "uid",(long)uid,"excepted",except.HasValue,"except",except ?? Guid.Empty)) return Equals(q.ExecuteScalar(),true);
        }
        private static uint[] Values(TeamQualifierStatistic s)
        { return new[] { s.ArenaPoints, s.TodayWin, s.TodayBattles, s.TotalWin, s.TotalLose }; }
        private static uint Add(uint value, uint amount) { return (uint)Math.Min(uint.MaxValue, (ulong)value + amount); }
        internal static uint[] Calculate(uint[] before, bool won, bool leader, uint delta)
        {
            if (before == null || before.Length != 5 || delta < 30 || delta > 49) throw new ArgumentException("Invalid Team Arena reward snapshot.");
            uint amount = leader ? delta : delta - 10;
            return new[] { won ? Add(before[0], amount) : before[0] - Math.Min(before[0], amount),
                Add(before[1], won ? 1u : 0u), Add(before[2], 1), Add(before[3], won ? 1u : 0u), Add(before[4], won ? 0u : 1u) };
        }
        internal static uint[] CalculateSavedDay(NpgsqlConnection c,NpgsqlTransaction t,uint[] before,bool won,bool leader,uint delta,
            Guid run,uint uid,DateTime outcome,int version) {
            if(version!=1 && version!=2)throw new InvalidOperationException("Unknown Team Arena counter version.");
            var after=Calculate(before,won,leader,delta);if(version==1)return after;
            if(outcome.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Team Arena counters require UTC.");
            using(var q=Q.Command(c,t,@"SELECT count(*),count(*) FILTER(WHERE s.won)
FROM revora_team_arena_statistics s JOIN revora_team_arena_results m ON m.id=s.run
WHERE s.uid=@uid AND m.id<>@run
AND to_char(m.completed_at AT TIME ZONE 'America/Los_Angeles','YYYYMMDD')=@day
AND m.participants=(SELECT count(*) FROM revora_team_arena_statistics complete WHERE complete.run=m.id)",
                "uid",(long)uid,"run",run,"day",MagnoliaQuest.Day(outcome)))
            using(var r=q.ExecuteReader()){r.Read();after[2]=checked((uint)(r.GetInt64(0)+1));after[1]=checked((uint)(r.GetInt64(1)+(won?1:0)));}
            return after;
        }
        internal static bool FencedOwner(GameState p, Group group)
        {
            GameState current;
            return p != null && p.Entity != null && !p.Fake && p.Entity.FullyLoaded && p.Action == 0
                && p.Entity.PersistenceRequiresReload && p.ItemSyncRoot != null && Monitor.IsEntered(p.ItemSyncRoot)
                && Kernel.GamePool.TryGetValue(p.Entity.UID,out current) && ReferenceEquals(current,p)
                && ReferenceEquals(p.TeamQualifierGroup,group) && p.ArenaStatistic != null && p.TeamArenaStatistic != null
                && p.ArenaStatistic.EntityID == p.Entity.UID && p.TeamArenaStatistic.EntityID == p.Entity.UID
                && ArenaCacheRecovery.RequiresReload(p.ArenaStatistic) && ArenaCacheRecovery.RequiresReload(p.TeamArenaStatistic);
        }
        private static void Locked(Snapshot row, Group group, bool fenced = false)
        {
            var p = row.Player;
            if (!(fenced ? FencedOwner(p,group) : Q.Player(p, false)) || !Monitor.IsEntered(p.ItemSyncRoot) || !ReferenceEquals(p.TeamQualifierGroup, group)
                || !ReferenceEquals(p.TeamArenaStatistic, row.Stats) || !ReferenceEquals(p.ArenaStatistic, row.Solo)
                || row.Stats.EntityID != p.Entity.UID || row.Solo.EntityID != p.Entity.UID
                || p.ArenaPoints != row.Stats.ArenaPoints)
                throw new InvalidOperationException("Team Arena result requires unchanged canonical locked owners.");
        }
        private static Snapshot[] OwnedRows(NpgsqlConnection c, NpgsqlTransaction t, Group group, bool fenced = false)
        {
            if (group == null || !Monitor.IsEntered(group.ResultSync) || t == null || t.Connection != c
                || !group.Done || !group.Finished || group.Winner == null || group.Loser == null
                || ReferenceEquals(group.Winner, group.Loser) || group.OutcomeUtc.Kind != DateTimeKind.Utc)
                throw new InvalidOperationException("Team Arena requires an owned terminal result transaction.");
            var winners = group.RewardParticipants(group.Winner);
            var losers = group.RewardParticipants(group.Loser);
            if (!winners.Contains(group.Winner) || !losers.Contains(group.Loser) || winners.Intersect(losers).Any())
                throw new InvalidOperationException("Team Arena requires two admitted sides.");
            var rows = winners.Concat(losers).OrderBy(p => p.Entity.UID).Select(p => new Snapshot {
                Player = p, Stats = p.TeamArenaStatistic, Solo = p.ArenaStatistic,
                Won = winners.Contains(p), Leader = ReferenceEquals(p, group.Winner) || ReferenceEquals(p, group.Loser)
            }).ToArray();
            if (rows.Length > 10 || rows.Select(r => r.Player.Entity.UID).Distinct().Count() != rows.Length)
                throw new InvalidOperationException("Team Arena roster is invalid.");
            foreach (var row in rows)
            {
                Locked(row,group,fenced);
                if (!fenced) SkillTable.LockOwner(c,t,row.Player);
                else using (var q = Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)row.Player.Entity.UID))
                    if (q.ExecuteScalar() == null) throw new InvalidOperationException("Fenced team owner missing.");
            }
            return rows;
        }
        private static void PrepareRows(NpgsqlConnection c, NpgsqlTransaction t, Snapshot[] rows, Group group)
        {
            int version;
            using(var q=Q.Command(c,t,"SELECT counter_version FROM revora_team_arena_intents WHERE id=@run","run",group.ResultKey)) {
                var value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Team Arena counter policy is missing.");version=Convert.ToInt32(value);
            }
            foreach (var row in rows)
            {
                row.Before = Values(row.Stats);
                using (var q = Q.Command(c,t,"SELECT arenapoints,todaywin,todaybattles,totalwin,totallose FROM teamarena WHERE entityid=@uid FOR UPDATE","uid",(long)row.Player.Entity.UID))
                using (var r = q.ExecuteReader())
                    if (!r.Read() || Enumerable.Range(0,5).Any(i => (r.IsDBNull(i) ? 0u : Convert.ToUInt32(r[i])) != row.Before[i]) || r.Read())
                        throw new InvalidOperationException("Team Arena statistic preimage changed.");
                using (var q = Q.Command(c,t,"SELECT arenapoints FROM arena WHERE entityid=@uid FOR UPDATE","uid",(long)row.Player.Entity.UID))
                using (var r = q.ExecuteReader())
                    if (!r.Read() || Convert.ToUInt32(r[0]) != row.Before[0] || r.Read())
                        throw new InvalidOperationException("Team Arena point mirror changed.");
                row.After = CalculateSavedDay(c,t,row.Before,row.Won,row.Leader,group.RewardDelta,group.ResultKey,row.Player.Entity.UID,group.OutcomeUtc,version);
            }
        }
        // Called under match/owner locks, in a separate committed transaction before Record.
        internal static void Capture(Group group) { CaptureCore(group,false); }
        internal static void CaptureFenced(Group group) { CaptureCore(group,true); }
        private static void CaptureCore(Group group, bool fenced)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    var rows = OwnedRows(c, t, group, fenced);
                    TeamArenaAdmissions.ValidateOutcome(c,t,group);
                    foreach (var row in rows)
                    {
                        if (ArenaMatchLedger.Pending(c,t,row.Player.Entity.UID)) throw new InvalidOperationException("Pending solo Arena match owns this player.");
                        if (Pending(c,t,row.Player.Entity.UID,group.ResultKey)) throw new InvalidOperationException("Another Team Arena outcome still owns this player.");
                    }
                    uint delta = group.RewardDelta;
                    int added = Q.Execute(c,t,@"INSERT INTO revora_team_arena_intents(id,winner,loser,delta,participants,completed_at,counter_version)
VALUES(@id,@winner,@loser,@delta,@count,@at,2) ON CONFLICT(id) DO NOTHING", "id",group.ResultKey,
                        "winner",(long)group.Winner.Entity.UID,"loser",(long)group.Loser.Entity.UID,"delta",(int)delta,"count",rows.Length,"at",group.OutcomeUtc);
                    if (added == 0) { VerifyIntent(c,t,group,rows,false);t.Commit();return; }
                    using (var q = Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_team_arena_results WHERE id=@id)","id",group.ResultKey))
                        if (Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Committed Team Arena result has no captured intent.");
                        PrepareRows(c,t,rows,group);
                    foreach (var row in rows)
                    {
                        if (Q.Execute(c,t,@"INSERT INTO revora_team_arena_intent_statistics(run,uid,won,leader,points_before,points_after,wins_before,wins_after,
battles_before,battles_after,totalwin_before,totalwin_after,totallose_before,totallose_after)
VALUES(@run,@uid,@won,@leader,@p0,@p1,@w0,@w1,@b0,@b1,@t0,@t1,@l0,@l1)","run",group.ResultKey,"uid",(long)row.Player.Entity.UID,
                            "won",row.Won,"leader",row.Leader,"p0",(long)row.Before[0],"p1",(long)row.After[0],"w0",(long)row.Before[1],"w1",(long)row.After[1],
                            "b0",(long)row.Before[2],"b1",(long)row.After[2],"t0",(long)row.Before[3],"t1",(long)row.After[3],"l0",(long)row.Before[4],"l1",(long)row.After[4]) != 1)
                            throw new InvalidOperationException("Team Arena intent snapshot was not saved.");
                        Locked(row,group,fenced);
                        if (!row.Before.SequenceEqual(Values(row.Stats))) throw new InvalidOperationException("Team Arena intent owner changed.");
                    }
                    t.Commit();
                }
            }
        }
        private static void VerifyIntent(NpgsqlConnection c, NpgsqlTransaction t, Group group, Snapshot[] rows, bool values)
        {
            using (var q = Q.Command(c,t,"SELECT winner,loser,delta,participants FROM revora_team_arena_intents WHERE id=@id AND completed_at=@at FOR UPDATE","id",group.ResultKey,"at",group.OutcomeUtc))
            using (var r = q.ExecuteReader())
                if (!r.Read() || r.GetInt64(0) != group.Winner.Entity.UID || r.GetInt64(1) != group.Loser.Entity.UID
                    || r.GetInt32(2) != group.RewardDelta || r.GetInt32(3) != rows.Length
                    || r.Read())
                    throw new InvalidOperationException("Team Arena captured outcome changed or is missing.");
            using (var q = Q.Command(c,t,@"SELECT uid,won,leader,points_before,points_after,wins_before,wins_after,
battles_before,battles_after,totalwin_before,totalwin_after,totallose_before,totallose_after
FROM revora_team_arena_intent_statistics WHERE run=@run ORDER BY uid","run",group.ResultKey))
            using (var r = q.ExecuteReader())
            {
                foreach (var row in rows)
                {
                    if (!r.Read() || r.GetInt64(0) != row.Player.Entity.UID || r.GetBoolean(1) != row.Won || r.GetBoolean(2) != row.Leader)
                        throw new InvalidOperationException("Team Arena captured roster is incomplete.");
                    if (values && Enumerable.Range(0,5).Any(i => r.GetInt64(3+i*2) != row.Before[i] || r.GetInt64(4+i*2) != row.After[i]))
                        throw new InvalidOperationException("Team Arena captured statistic preimage changed.");
                }
                if (r.Read()) throw new InvalidOperationException("Team Arena captured roster changed.");
            }
        }
        internal static Snapshot[] Record(NpgsqlConnection c, NpgsqlTransaction t, Group group)
        {
            var rows = OwnedRows(c, t, group);
            uint delta = group.RewardDelta;
            int inserted = Q.Execute(c, t, @"INSERT INTO revora_team_arena_results(id,winner,loser,delta,participants,completed_at)
VALUES(@id,@winner,@loser,@delta,@count,@at) ON CONFLICT(id) DO NOTHING", "id", group.ResultKey,
                "winner", (long)group.Winner.Entity.UID, "loser", (long)group.Loser.Entity.UID, "delta", (int)delta,
                "count", rows.Length, "at", group.OutcomeUtc);
            if (inserted == 0)
            {
                using (var q = Q.Command(c,t,@"SELECT winner,loser,delta,participants,completed_at FROM revora_team_arena_results WHERE id=@id FOR UPDATE","id",group.ResultKey))
                using (var r = q.ExecuteReader())
                    if (!r.Read() || r.GetInt64(0) != group.Winner.Entity.UID || r.GetInt64(1) != group.Loser.Entity.UID
                        || r.GetInt32(2) != delta || r.GetInt32(3) != rows.Length || r.Read())
                        throw new InvalidOperationException("Team Arena receipt identity changed.");
                using (var q = Q.Command(c,t,"SELECT uid,won,leader FROM revora_team_arena_statistics WHERE run=@id ORDER BY uid","id",group.ResultKey))
                using (var r = q.ExecuteReader())
                {
                    foreach (var row in rows)
                        if (!r.Read() || r.GetInt64(0) != row.Player.Entity.UID || r.GetBoolean(1) != row.Won || r.GetBoolean(2) != row.Leader)
                            throw new InvalidOperationException("Team Arena receipt roster is incomplete.");
                    if (r.Read()) throw new InvalidOperationException("Team Arena receipt roster changed.");
                }
                VerifyIntent(c, t, group, rows, false);
                if (Pending(c,t,rows[0].Player.Entity.UID)) throw new InvalidOperationException("Team Arena replay bundle is inconsistent.");
                TeamArenaDailyRewards.Record(c,t,group.ResultKey);
                return new Snapshot[0];
            }
            PrepareRows(c, t, rows, group);
            VerifyIntent(c, t, group, rows, true);
            foreach (var row in rows)
            {
                if (Q.Execute(c,t,@"UPDATE teamarena SET arenapoints=@points,todaywin=@wins,todaybattles=@battles,totalwin=@totalwin,totallose=@totallose WHERE entityid=@uid",
                    "points",(long)row.After[0],"wins",(long)row.After[1],"battles",(long)row.After[2],"totalwin",(long)row.After[3],"totallose",(long)row.After[4],"uid",(long)row.Player.Entity.UID) != 1
                    || Q.Execute(c,t,"UPDATE arena SET arenapoints=@points WHERE entityid=@uid","points",(long)row.After[0],"uid",(long)row.Player.Entity.UID) != 1)
                    throw new InvalidOperationException("Team Arena result was not saved.");
                if (Q.Execute(c,t,@"INSERT INTO revora_team_arena_statistics(run,uid,won,leader,points_before,points_after,wins_before,wins_after,
battles_before,battles_after,totalwin_before,totalwin_after,totallose_before,totallose_after)
VALUES(@run,@uid,@won,@leader,@p0,@p1,@w0,@w1,@b0,@b1,@t0,@t1,@l0,@l1)","run",group.ResultKey,"uid",(long)row.Player.Entity.UID,
                    "won",row.Won,"leader",row.Leader,"p0",(long)row.Before[0],"p1",(long)row.After[0],"w0",(long)row.Before[1],"w1",(long)row.After[1],
                    "b0",(long)row.Before[2],"b1",(long)row.After[2],"t0",(long)row.Before[3],"t1",(long)row.After[3],"l0",(long)row.Before[4],"l1",(long)row.After[4]) != 1)
                    throw new InvalidOperationException("Team Arena statistic receipt was not saved.");
            }
            foreach (var row in rows)
            {
                Locked(row,group);
                if (!row.Before.SequenceEqual(Values(row.Stats))) throw new InvalidOperationException("Team Arena cache changed before commit.");
            }
            TeamArenaDailyRewards.Record(c,t,group.ResultKey);
            return rows;
        }
        internal static bool ProjectionMatches(NpgsqlConnection c, NpgsqlTransaction t, Group group)
        {
            foreach (var row in OwnedRows(c,t,group))
            {
                var current = Values(row.Stats);
                using (var q = Q.Command(c,t,"SELECT arenapoints,todaywin,todaybattles,totalwin,totallose FROM teamarena WHERE entityid=@uid FOR UPDATE","uid",(long)row.Player.Entity.UID))
                using (var r = q.ExecuteReader())
                    if (!r.Read() || Enumerable.Range(0,5).Any(i => (r.IsDBNull(i) ? 0u : Convert.ToUInt32(r[i])) != current[i]) || r.Read()) return false;
                using (var q = Q.Command(c,t,"SELECT arenapoints FROM arena WHERE entityid=@uid FOR UPDATE","uid",(long)row.Player.Entity.UID))
                using (var r = q.ExecuteReader())
                    if (!r.Read() || Convert.ToUInt32(r[0]) != current[0] || r.Read()) return false;
            }
            return true;
        }
        // Caller commits first and retains match/owner locks through projection.
        internal static void Publish(Group group, Snapshot[] rows)
        {
            if (!Monitor.IsEntered(group.ResultSync)) throw new InvalidOperationException("Team Arena projection requires its match lock.");
            foreach (var row in rows)
            {
                Locked(row,group);
                if (!row.Before.SequenceEqual(Values(row.Stats))) throw new InvalidOperationException("Team Arena projection requires reload.");
            }
            foreach (var row in rows)
            {
                row.Player.ArenaPoints = row.After[0];row.Stats.TodayWin = row.After[1];row.Stats.TodayBattles = row.After[2];
                row.Stats.TotalWin = row.After[3];row.Stats.TotalLose = row.After[4];
            }
            foreach (var row in rows) { row.Player.Send(row.Stats);row.Player.ArenaStatistic.Send(row.Player); }
        }
    }
}
