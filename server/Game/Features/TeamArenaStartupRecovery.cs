using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Joint startup preparation recovers this mode before loading either Arena cache.
    internal static class TeamArenaStartupRecovery
    {
        private static readonly object Gate = new object();
        private sealed class Row { internal uint Uid; internal bool Won, Leader; internal uint[] Before, After; }
        internal static bool Prepared { get; private set; }
        private static void RequireStartup(bool initialized = true)
        {
            if ((initialized && !TeamArenaResultStatistics.Initialized) || Server.AuthServer != null || Server.GameServer != null
                || Kernel.GamePool.Count != 0 || Kernel.DisconnectPool.Count != 0
                || Qualifier.ArenaStatistics.Count != 0 || TeamQualifier.ArenaStatistics.Count != 0
                || Qualifier.QualifierList.Groups.Count != 0 || TeamQualifier.QualifierList.Groups.Count != 0)
                throw new InvalidOperationException("Team Arena recovery requires initialized startup before listeners, sessions and Arena caches.");
        }
        internal static int Sweep(int limit)
        {
            if (limit < 1 || limit > 128) throw new ArgumentOutOfRangeException("limit");
            lock (Gate)
            {
                RequireStartup();
                var runs = new List<Guid>();
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var q = Q.Command(c,null,@"SELECT i.id FROM revora_team_arena_intents i
WHERE NOT EXISTS(SELECT 1 FROM revora_team_arena_results r WHERE r.id=i.id)
ORDER BY i.completed_at,i.id LIMIT @limit","limit",limit))
                    using (var r = q.ExecuteReader()) while (r.Read()) runs.Add(r.GetGuid(0));
                }
                int recovered = 0;
                foreach (var run in runs) if (Recover(run)) recovered++;
                return recovered;
            }
        }
        private static bool HasPending()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = Q.Command(c,null,@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_intents i
WHERE NOT EXISTS(SELECT 1 FROM revora_team_arena_results r WHERE r.id=i.id))")) return Equals(q.ExecuteScalar(),true);
            }
        }
        internal static void PrepareStartup(int batchLimit)
        {
            if (batchLimit < 1 || batchLimit > 100) throw new ArgumentOutOfRangeException("batchLimit");
            lock (Gate)
            {
                RequireStartup(false);Prepared = false;
                TeamArenaResultStatistics.Initialize();
                for (int pass = 0; pass < batchLimit; pass++)
                {
                    Sweep(128);
                    if (!HasPending()) break;
                    if (pass == batchLimit - 1) throw new InvalidOperationException("Team Arena startup recovery batch limit reached.");
                }
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var q = Q.Command(c,null,@"SELECT EXISTS(
SELECT 1 FROM revora_team_arena_results r LEFT JOIN revora_team_arena_intents i ON i.id=r.id
WHERE i.id IS NULL OR (r.winner,r.loser,r.delta,r.participants,r.completed_at)
IS DISTINCT FROM (i.winner,i.loser,i.delta,i.participants,i.completed_at)
OR (SELECT count(*) FROM revora_team_arena_statistics s WHERE s.run=r.id)<>r.participants
OR (SELECT count(*) FROM revora_team_arena_intent_statistics s WHERE s.run=r.id)<>r.participants)
OR EXISTS(SELECT 1 FROM revora_team_arena_intent_statistics i LEFT JOIN revora_team_arena_statistics s ON s.run=i.run AND s.uid=i.uid
WHERE s.uid IS NULL OR (s.won,s.leader,s.points_before,s.points_after,s.wins_before,s.wins_after,s.battles_before,s.battles_after,s.totalwin_before,s.totalwin_after,s.totallose_before,s.totallose_after)
IS DISTINCT FROM (i.won,i.leader,i.points_before,i.points_after,i.wins_before,i.wins_after,i.battles_before,i.battles_after,i.totalwin_before,i.totalwin_after,i.totallose_before,i.totallose_after))"))
                        if (Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Team Arena startup found inconsistent completed evidence.");
                }
                RequireStartup();Prepared = true;
            }
        }
        private static void RequireRecovery(TeamQualifier.QualifierList.QualifierGroup group)
        {
            if (group == null) { RequireStartup();return; }
            if (!System.Threading.Monitor.IsEntered(group.ResultSync)) throw new InvalidOperationException("Fenced team recovery requires its match lock.");
            TeamQualifier.QualifierList.QualifierGroup registered;
            var owners = group.Participants(true).Concat(group.Participants(false)).ToArray();
            if (!TeamArenaResultStatistics.Initialized || !System.Threading.Monitor.IsEntered(group.ResultSync)
                || !group.Done || !group.Finished || !group.ResultMapRegistered || owners.Length < 2
                || !TeamQualifier.QualifierList.Groups.TryGetValue(group.ID,out registered) || !ReferenceEquals(registered,group)
                || owners.Any(p => !TeamArenaResultStatistics.FencedOwner(p,group)))
                throw new InvalidOperationException("Team recovery requires unchanged fenced match owners and locks.");
        }
        internal static bool Recover(Guid run) { lock (Gate) return RecoverCore(run,null); }
        internal static bool RecoverFenced(TeamQualifier.QualifierList.QualifierGroup group)
        {
            if (group == null) throw new ArgumentNullException("group");
            return RecoverCore(group.ResultKey,group);
        }
        private static bool RecoverCore(Guid run, TeamQualifier.QualifierList.QualifierGroup group)
        {
                RequireRecovery(group);
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        TeamArenaAdmissions.ValidateCaptured(c,t,run);
                        uint winner = 0, loser = 0, delta = 0; int count = 0, version=1; bool captured;DateTime outcome=default(DateTime);
                        using (var q = Q.Command(c,t,"SELECT winner,loser,delta,participants,counter_version,completed_at AT TIME ZONE 'UTC' FROM revora_team_arena_intents WHERE id=@run FOR UPDATE","run",run))
                        using (var r = q.ExecuteReader())
                        {
                            captured = r.Read();
                            if (captured)
                            {
                                winner = checked((uint)r.GetInt64(0));loser = checked((uint)r.GetInt64(1));
                                delta = checked((uint)r.GetInt32(2));count = r.GetInt32(3);
                                version=r.GetInt32(4);outcome=DateTime.SpecifyKind(r.GetDateTime(5),DateTimeKind.Utc);
                                if (r.Read() || winner == 0 || loser == 0 || winner == loser || count < 2 || count > 10 || delta < 30 || delta > 49 || (version!=1 && version!=2))
                                    throw new InvalidOperationException("Invalid captured Team Arena outcome.");
                            }
                        }
                        if (!captured)
                        {
                            if (group != null) throw new InvalidOperationException("Fenced team recovery has no captured outcome.");
                            using (var q = Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_results WHERE id=@run)
OR EXISTS(SELECT 1 FROM revora_team_arena_statistics WHERE run=@run)
OR EXISTS(SELECT 1 FROM revora_team_arena_intent_statistics WHERE run=@run)","run",run))
                                if (Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Team Arena evidence has no captured outcome.");
                            return false;
                        }
                        var rows = new List<Row>();
                        using (var q = Q.Command(c,t,@"SELECT uid,won,leader,points_before,points_after,wins_before,wins_after,
battles_before,battles_after,totalwin_before,totalwin_after,totallose_before,totallose_after
FROM revora_team_arena_intent_statistics WHERE run=@run ORDER BY uid","run",run))
                        using (var r = q.ExecuteReader()) while (r.Read())
                        {
                            if (rows.Count >= count) throw new InvalidOperationException("Captured Team Arena roster is oversized.");
                            var row = new Row { Uid = checked((uint)r.GetInt64(0)), Won = r.GetBoolean(1), Leader = r.GetBoolean(2), Before = new uint[5], After = new uint[5] };
                            for (int i = 0; i < 5; i++) { row.Before[i] = checked((uint)r.GetInt64(3+i*2));row.After[i] = checked((uint)r.GetInt64(4+i*2)); }
                            var legacy=TeamArenaResultStatistics.Calculate(row.Before,row.Won,row.Leader,delta);
                            if (row.Uid == 0 || row.Leader != (row.Uid == winner || row.Uid == loser)
                                || (row.Uid == winner && !row.Won) || (row.Uid == loser && row.Won)
                                || (version==1?!row.After.SequenceEqual(legacy):new[]{0,3,4}.Any(i=>row.After[i]!=legacy[i])
                                    || row.After[2]==0 || row.After[1]>row.After[2] || (row.Won && row.After[1]==0)))
                                throw new InvalidOperationException("Captured Team Arena reward is inconsistent.");
                            rows.Add(row);
                        }
                        if (rows.Count != count || rows.Select(r => r.Uid).Distinct().Count() != count
                            || !rows.Any(r => r.Uid == winner) || !rows.Any(r => r.Uid == loser)
                            || rows.Count(r => r.Won) > 5 || rows.Count(r => !r.Won) > 5)
                            throw new InvalidOperationException("Captured Team Arena roster is incomplete.");
                        if (group != null)
                        {
                            var winners = group.RewardParticipants(group.Winner).Select(p => p.Entity.UID).ToArray();
                            var losers = group.RewardParticipants(group.Loser).Select(p => p.Entity.UID).ToArray();
                            if (group.ResultKey != run || group.Winner.Entity.UID != winner || group.Loser.Entity.UID != loser
                                || !rows.Select(r => r.Uid).SequenceEqual(winners.Concat(losers).OrderBy(uid => uid))
                                || rows.Any(r => r.Won != winners.Contains(r.Uid)))
                                throw new InvalidOperationException("Fenced team recovery roster changed.");
                        }
                        bool finished;
                        using (var q = Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_team_arena_results WHERE id=@run)","run",run)) finished = Equals(q.ExecuteScalar(),true);
                        if (finished)
                        {
                            using (var q = Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_results r JOIN revora_team_arena_intents i ON i.id=r.id
WHERE r.id=@run AND r.winner=i.winner AND r.loser=i.loser AND r.delta=i.delta AND r.participants=i.participants AND r.completed_at=i.completed_at)
AND (SELECT count(*) FROM revora_team_arena_statistics WHERE run=@run)=@count
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_intent_statistics i LEFT JOIN revora_team_arena_statistics s ON s.run=i.run AND s.uid=i.uid
WHERE i.run=@run AND (s.uid IS NULL OR (s.won,s.leader,s.points_before,s.points_after,s.wins_before,s.wins_after,s.battles_before,s.battles_after,s.totalwin_before,s.totalwin_after,s.totallose_before,s.totallose_after)
IS DISTINCT FROM (i.won,i.leader,i.points_before,i.points_after,i.wins_before,i.wins_after,i.battles_before,i.battles_after,i.totalwin_before,i.totalwin_after,i.totallose_before,i.totallose_after)))","run",run,"count",count))
                                if (!Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Committed Team Arena bundle is incomplete.");
                            return false;
                        }
                        foreach (var row in rows)
                        {
                            using (var q = Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)row.Uid))
                            using (var r = q.ExecuteReader()) if (!r.Read() || r.Read()) throw new InvalidOperationException("Team Arena owner is missing.");
                            using (var q = Q.Command(c,t,"SELECT arenapoints,todaywin,todaybattles,totalwin,totallose FROM teamarena WHERE entityid=@uid FOR UPDATE","uid",(long)row.Uid))
                            using (var r = q.ExecuteReader())
                                if (!r.Read() || Enumerable.Range(0,5).Any(i => (r.IsDBNull(i) ? 0u : Convert.ToUInt32(r[i])) != row.Before[i]) || r.Read())
                                    throw new InvalidOperationException("Team Arena recovery preimage changed.");
                            using (var q = Q.Command(c,t,"SELECT arenapoints FROM arena WHERE entityid=@uid FOR UPDATE","uid",(long)row.Uid))
                            using (var r = q.ExecuteReader()) if (!r.Read() || Convert.ToUInt32(r[0]) != row.Before[0] || r.Read())
                                throw new InvalidOperationException("Team Arena recovery point mirror changed.");
                        }
                        // Recompute an unfinished capture only after owning every participant.
                        // Finished replay was verified above against its immutable saved bundle;
                        // later same-day matches must not rewrite that earlier snapshot.
                        foreach(var row in rows)
                            if(!row.After.SequenceEqual(TeamArenaResultStatistics.CalculateSavedDay(c,t,row.Before,row.Won,row.Leader,delta,run,row.Uid,outcome,version)))
                                throw new InvalidOperationException("Captured Team Arena daily counters differ from saved results.");
                        if (Q.Execute(c,t,@"INSERT INTO revora_team_arena_results(id,winner,loser,delta,participants,completed_at)
SELECT id,winner,loser,delta,participants,completed_at FROM revora_team_arena_intents WHERE id=@run","run",run) != 1)
                            throw new InvalidOperationException("Team Arena recovered result was not saved.");
                        foreach (var row in rows)
                        {
                            if (Q.Execute(c,t,@"UPDATE teamarena SET arenapoints=@points,todaywin=@wins,todaybattles=@battles,totalwin=@totalwin,totallose=@totallose WHERE entityid=@uid",
                                "points",(long)row.After[0],"wins",(long)row.After[1],"battles",(long)row.After[2],"totalwin",(long)row.After[3],"totallose",(long)row.After[4],"uid",(long)row.Uid) != 1
                                || Q.Execute(c,t,"UPDATE arena SET arenapoints=@points WHERE entityid=@uid","points",(long)row.After[0],"uid",(long)row.Uid) != 1)
                                throw new InvalidOperationException("Team Arena recovered statistics were not saved.");
                        }
                        if (Q.Execute(c,t,@"INSERT INTO revora_team_arena_statistics(run,uid,won,leader,points_before,points_after,wins_before,wins_after,
battles_before,battles_after,totalwin_before,totalwin_after,totallose_before,totallose_after)
SELECT run,uid,won,leader,points_before,points_after,wins_before,wins_after,battles_before,battles_after,totalwin_before,totalwin_after,totallose_before,totallose_after
FROM revora_team_arena_intent_statistics WHERE run=@run","run",run) != count)
                            throw new InvalidOperationException("Team Arena recovered receipts are incomplete.");
                        TeamArenaDailyRewards.Record(c,t,run);
                        RequireRecovery(group);t.Commit();return true;
                    }
                }
        }
    }
}
