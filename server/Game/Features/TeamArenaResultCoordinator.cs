using System;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;
using Group = Revora.Game.TeamQualifier.QualifierList.QualifierGroup;

namespace Revora.Game.Features
{
    // Staged: lifecycle must defer legacy export until this result is committed.
    internal static class TeamArenaResultCoordinator
    {
        internal enum Outcome { NotReady, Busy, Committed, AlreadyCommitted, Failed, Retained }
        private static bool Ready(GameState p, Group group)
        {
            return Q.Player(p,false) && ReferenceEquals(p.TeamQualifierGroup,group)
                && p.ArenaStatistic != null && p.TeamArenaStatistic != null
                && p.ArenaStatistic.EntityID == p.Entity.UID && p.TeamArenaStatistic.EntityID == p.Entity.UID
                && !ArenaCacheRecovery.RequiresReload(p.ArenaStatistic) && !ArenaCacheRecovery.RequiresReload(p.TeamArenaStatistic);
        }
        internal static Outcome RecoverFenced(Group group)
        {
            if (group == null || !TeamArenaResultStatistics.Initialized) return Outcome.NotReady;
            if (!Monitor.TryEnter(group.ResultSync)) return Outcome.Busy;
            GameState[] owners = null; int acquired = 0;
            try
            {
                Group registered;
                if (group.Done != group.Finished || !group.ResultAdmissionReady || !group.Inside || !group.ResultMapRegistered
                    || !TeamQualifier.QualifierList.Groups.TryGetValue(group.ID,out registered) || !ReferenceEquals(registered,group)) return Outcome.NotReady;
                owners = group.Participants(true).Concat(group.Participants(false)).OrderBy(p => p.Entity.UID).ToArray();
                if (owners.Length < 2) return Outcome.NotReady;
                foreach (var p in owners)
                {
                    if (p == null || p.ItemSyncRoot == null) return Outcome.NotReady;
                    if (!Monitor.TryEnter(p.ItemSyncRoot)) return Outcome.Busy;
                    acquired++;
                }
                var fenced = owners.Where(p => TeamArenaResultStatistics.FencedOwner(p,group)).ToArray();
                if (fenced.Length == 0 || owners.Any(p => !fenced.Contains(p) && !Ready(p,group))) return Outcome.NotReady;
                if (Volatile.Read(ref group.DurableResultReady) != 0) return Outcome.AlreadyCommitted;
                if (!group.Done)
                {
                    var pending = Volatile.Read(ref group.PendingDeparture);
                    if (pending == null)
                    {
                        if (!group.HasTimedOut(MatchClock())) return Outcome.NotReady;
                        group.End(); // Uses the normal damage comparison and retained-departure precedence.
                    }
                    else
                    {
                        if (pending.Observed.Kind != DateTimeKind.Utc
                            || (!ReferenceEquals(pending.Player,group.Player1) && !ReferenceEquals(pending.Player,group.Player2))) return Outcome.NotReady;
                        // Cache invalidation must not erase the first observed terminal
                        // departure while completion was waiting for the original roster.
                        group.End(pending.Player);
                        if (!group.Done || !group.Finished || !ReferenceEquals(group.Loser,pending.Player)) return Outcome.NotReady;
                    }
                    if (!group.Done || !group.Finished) return Outcome.NotReady;
                }
                try
                {
                    // This result updates the entire roster atomically. Invalidate remaining
                    // original caches before repairing it; never publish through mixed states.
                    foreach(var p in owners.Where(p => !fenced.Contains(p)))
                        ArenaCacheRecovery.Fail(p,"team-arena-mixed-recovery",new InvalidOperationException("Team result requires roster reload."));
                    TeamArenaResultStatistics.CaptureFenced(group);
                    bool recovered = TeamArenaStartupRecovery.RecoverFenced(group);
                    Volatile.Write(ref group.DurableResultReady,1);
                    return recovered ? Outcome.Committed : Outcome.AlreadyCommitted;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fenced team recovery failed: " + ex.GetType().Name);
                    return Outcome.Failed;
                }
            }
            finally
            {
                for (int n = acquired - 1; n >= 0; n--) Monitor.Exit(owners[n].ItemSyncRoot);
                Monitor.Exit(group.ResultSync);
            }
        }
        internal static Func<Time32> MatchClock = () => Time32.Now;
        internal static Func<Time32> ReturnClock = () => Time32.Now;
        internal static Outcome CancelInterrupted(Group group)
        {
            if(group==null || !TeamArenaAdmissions.Initialized)return Outcome.NotReady;
            if(!Monitor.TryEnter(group.ResultSync))return Outcome.Busy;
            GameState[] owners=null;int acquired=0;
            try {
                if(group.DurableAdmissionCancelled)return Outcome.AlreadyCommitted;
                Group registered;
                if(!group.DurableAdmissionReserved || !group.AdmissionInterrupted || group.ResultAdmissionReady || group.Done || group.Finished
                    || !TeamQualifier.QualifierList.Groups.TryGetValue(group.ID,out registered) || !ReferenceEquals(registered,group))return Outcome.NotReady;
                owners=group.ReservationParticipants.OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in owners){if(!Monitor.TryEnter(p.ItemSyncRoot))return Outcome.Busy;acquired++;}
                try {
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        TeamArenaAdmissions.Cancel(c,t,group,owners);t.Commit();
                    }}
                    group.DurableAdmissionCancelled=true;return Outcome.Committed;
                }catch(Exception ex){Console.WriteLine("Team admission cancellation deferred: "+ex.GetType().Name);return Outcome.Failed;}
            }finally {
                for(int n=acquired-1;n>=0;n--)Monitor.Exit(owners[n].ItemSyncRoot);
                Monitor.Exit(group.ResultSync);
            }
        }
        internal static Outcome Cleanup(Group group)
        {
            if (group == null || (Volatile.Read(ref group.DurableResultReady) == 0 && !group.DurableAdmissionCancelled)) return Outcome.NotReady;
            if (!Monitor.TryEnter(group.ResultSync)) return Outcome.Busy;
            try
            {
                if (group.ResultCleanupCompleted) return Outcome.AlreadyCommitted;
                Group registered;
                if ((!group.DurableAdmissionCancelled && (!group.Done || !group.Finished || !group.ResultMapRegistered))
                    || !TeamQualifier.QualifierList.Groups.TryGetValue(group.ID,out registered) || !ReferenceEquals(registered,group)) return Outcome.NotReady;
                group.Export();
                return group.ResultCleanupCompleted ? Outcome.Committed : Outcome.NotReady;
            }
            finally { Monitor.Exit(group.ResultSync); }
        }
        internal sealed class Departure
        {
            internal readonly GameState Player;
            internal readonly DateTime Observed;
            internal Departure(GameState player, DateTime observed) { Player=player;Observed=observed; }
        }
        internal static Outcome Complete(Group group) { return CompleteCore(group,null,false,false); }
        internal static Outcome RetryDeparture(Group group)
        {
            if (group == null || Volatile.Read(ref group.PendingDeparture) == null) return Outcome.NotReady;
            return CompleteCore(group,null,true,false);
        }
        // Called before teardown releases the canonical actor. Retained means keep
        // that original session available for the eventual whole-roster result.
        internal static Outcome ObserveDisconnect(Group group, GameState player)
        {
            if(!TeamArenaResultStatistics.Initialized || group==null || !group.RecordDisconnect(player))return Outcome.NotReady;
            if(group.Done)return Complete(group);
            return Volatile.Read(ref group.PendingDeparture)==null ? Outcome.Retained : RetryDeparture(group);
        }
        internal static Outcome EndForPlayer(Group group, GameState loser)
        {
            if (!TeamArenaResultStatistics.Initialized || group == null || loser == null
                || (!ReferenceEquals(loser,group.Player1) && !ReferenceEquals(loser,group.Player2))) return Outcome.NotReady;
            // Do not acquire match/actor locks or perform database work under this gate.
            lock (group.DepartureSync)
            {
                Group registered;
                if (!Ready(loser,group) || !group.ResultAdmissionReady || !group.Inside || !group.ResultMapRegistered
                    || !TeamQualifier.QualifierList.Groups.TryGetValue(group.ID,out registered) || !ReferenceEquals(registered,group)) return Outcome.NotReady;
                if (!group.Done && group.PendingDeparture == null)
                {
                    var observed=ArenaMatchLedger.Clock();
                    if (observed.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Team departure requires UTC.");
                    Volatile.Write(ref group.PendingDeparture,new Departure(loser,observed));
                }
            }
            return CompleteCore(group,loser,true,false);
        }
        internal static Outcome EndTimedOut(Group group) { return CompleteCore(group,null,true,true); }
        private static Outcome CompleteCore(Group group, GameState loser, bool end, bool timeout)
        {
            if (group == null || !TeamArenaResultStatistics.Initialized) return Outcome.NotReady;
            if (!Monitor.TryEnter(group.ResultSync)) return Outcome.Busy;
            GameState[] owners = null; int acquired = 0;
            try
            {
                if (Volatile.Read(ref group.DurableResultReady) != 0) return Outcome.AlreadyCommitted;
                var pending=Volatile.Read(ref group.PendingDeparture);
                if (pending != null) { loser=pending.Player;end=true;timeout=false; }
                Group registered;
                if ((!end && (!group.Done || !group.Finished)) || !group.Inside || !group.ResultMapRegistered
                    || !TeamQualifier.QualifierList.Groups.TryGetValue(group.ID,out registered) || !ReferenceEquals(registered,group)) return Outcome.NotReady;
                if (timeout && !group.Done && !group.HasTimedOut(MatchClock())) return Outcome.NotReady;
                var first = group.Participants(true);var second = group.Participants(false);
                if (!first.Contains(group.Player1) || !second.Contains(group.Player2) || first.Intersect(second).Any()) return Outcome.NotReady;
                owners = first.Concat(second).OrderBy(p => p.Entity.UID).ToArray();
                foreach (var p in owners)
                {
                    if (p == null || p.ItemSyncRoot == null) return Outcome.NotReady;
                    if (!Monitor.TryEnter(p.ItemSyncRoot)) return Outcome.Busy;
                    acquired++;
                }
                if (owners.Any(p => !Ready(p,group))) return Outcome.NotReady;
                try
                {
                    // Freeze only after all original admitted owners are locked and valid.
                    if (!group.Done && end) { if (timeout) group.End(); else group.End(loser); }
                    if (!group.Done || !group.Finished) return Outcome.NotReady;
                    TeamArenaResultStatistics.Capture(group);
                    TeamArenaResultStatistics.Snapshot[] rows; bool projection;
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            rows = TeamArenaResultStatistics.Record(c,t,group);
                            projection = rows.Length != 0 || TeamArenaResultStatistics.ProjectionMatches(c,t,group);
                            t.Commit();
                        }
                    }
                    Volatile.Write(ref group.DurableResultReady,1);
                    if (!projection) throw new InvalidOperationException("Committed team result requires cache reload.");
                    TeamArenaResultStatistics.Publish(group,rows);
                    group.NotifyCommittedOutcome();
                    return rows.Length == 0 ? Outcome.AlreadyCommitted : Outcome.Committed;
                }
                catch (Exception ex)
                {
                    foreach (var p in owners) ArenaCacheRecovery.Fail(p,"team-arena-result",ex);
                    return Outcome.Failed;
                }
            }
            finally
            {
                for (int n = acquired - 1; n >= 0; n--) Monitor.Exit(owners[n].ItemSyncRoot);
                Monitor.Exit(group.ResultSync);
            }
        }
    }
}
