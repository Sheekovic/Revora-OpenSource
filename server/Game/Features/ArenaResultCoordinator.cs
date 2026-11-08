using System;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;
namespace Revora.Game.Features
{
    // Not wired to live callbacks until disconnect/restart and legacy writer replacement are ready.
    internal static class ArenaResultCoordinator
    {
        internal static void Initialize() {
            ArenaMatchLedger.Initialize();ArenaResultStatistics.Initialize();ArenaDailyRewards.Initialize();ArenaCustomRewards.Initialize();ArenaResultIntents.Initialize();
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"CREATE TABLE IF NOT EXISTS revora_arena_result_commits(run uuid PRIMARY KEY REFERENCES revora_arena_matches(id))");}
            ArenaStartupRecovery.Initialize();ArenaRankingSettlement.Initialize();ArenaRewardDelivery.Initialize();
        }
        // Never wait for an owner while holding the match lock: combat/quit may enter in the opposite order.
        // Busy is retryable by the future lifecycle loop. Export is allowed only after Committed.
        internal enum Outcome { NotReady, Busy, Committed, AlreadyCommitted, Failed }
        internal static Outcome Complete(Group group){return CompleteCore(group,ArenaResultIntents.Cause.Result);}
        internal sealed class Departure {
            internal readonly GameState Player;internal readonly ArenaResultIntents.Cause Cause;internal readonly DateTime Observed;
            internal Departure(GameState player,ArenaResultIntents.Cause cause,DateTime observed){Player=player;Cause=cause;Observed=observed;}
        }
        internal static Outcome RetryDeparture(Group group) {
            if(group==null)return Outcome.NotReady;
            var request=Volatile.Read(ref group.PendingDeparture);
            return request==null?Outcome.NotReady:EndOwned(group,request.Player,request.Cause,false);
        }
        internal static Func<Time32> MatchClock=()=>Time32.Now;
        internal static Outcome EndTimedOut(Group group){return EndOwned(group,null,ArenaResultIntents.Cause.Timeout,true);}
        internal static Outcome EndForPlayer(Group group,GameState departing,ArenaResultIntents.Cause cause) {
            if(group==null || departing==null || !(ReferenceEquals(departing,group.Player1) || ReferenceEquals(departing,group.Player2))
                || !(cause==ArenaResultIntents.Cause.Defeat || cause==ArenaResultIntents.Cause.Forfeit || cause==ArenaResultIntents.Cause.Disconnect))return Outcome.NotReady;
            // This short gate is independent of the match/owner locks. Never hold it while
            // acquiring those locks or doing database work. FreezeOutcome uses the same gate.
            lock(group.DepartureSync) {
                if(!ArenaActors.Player(departing,false) || !group.OwnsImportedMap)return Outcome.NotReady;
                if(!group.Done && group.PendingDeparture==null) {
                    var observed=ArenaMatchLedger.Clock();if(observed.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena departure requires UTC.");
                    Volatile.Write(ref group.PendingDeparture,new Departure(departing,cause,observed));
                }
            }
            return EndOwned(group,departing,cause,false);
        }
        private static Outcome EndOwned(Group group,GameState departing,ArenaResultIntents.Cause cause,bool timeout) {
            if(group==null)return Outcome.NotReady;
            if(!Monitor.TryEnter(group.ParticipationSync))return Outcome.Busy;
            GameState[] players=null;int acquired=0;
            try {
                if(!group.OwnsImportedMap)return Outcome.NotReady;
                var pending=Volatile.Read(ref group.PendingDeparture);
                if(pending!=null){departing=pending.Player;cause=pending.Cause;timeout=false;}
                if(timeout && !group.Done && unchecked(MatchClock().Value-group.ImportTime.Value)<180000)return Outcome.NotReady;
                players=new[]{group.Player1,group.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in players) {
                    if(!ArenaActors.Player(p,false))return Outcome.NotReady;
                    if(!Monitor.TryEnter(p.ItemSyncRoot))return Outcome.Busy;acquired++;
                }
                // A pre-existing RAM ending has no new causal evidence; a saved intent retains its own cause.
                var observedCause=group.Done?ArenaResultIntents.Cause.Result:cause;
                // No RAM outcome is changed while an owner is busy or no longer canonical.
                if(!group.Done)group.FreezeOutcome(timeout?(group.Player1Damage>group.Player2Damage?group.Player2:group.Player1):departing);
                var result=CompleteCore(group,observedCause);
                if(result==Outcome.Committed && group.CompletionOwner==null)group.NotifyOutcome();
                return result;
            }finally {
                for(int n=acquired-1;n>=0;n--)Monitor.Exit(players[n].ItemSyncRoot);
                Monitor.Exit(group.ParticipationSync);
            }
        }
        // Cleanup can run after a participant has left; durable identity, not current map ownership,
        // proves the result. It never completes or invents a result as a side effect.
        internal static Outcome Cleanup(Group group) {
            if(group==null)return Outcome.NotReady;
            if(!Monitor.TryEnter(group.ParticipationSync))return Outcome.Busy;
            GameState[] owners=null;int acquired=0;
            try {
                if(group.CleanupCompleted)return Outcome.AlreadyCommitted;
                if((!group.Done && !group.DurableAdmissionCancelled) || group.Player1==null || group.Player2==null)return Outcome.NotReady;
                owners=new[]{group.Player1,group.Player2}.Concat(group.Watchers.ToArray()).Where(p=>p!=null)
                    .Distinct().OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in owners) {
                    if(p.ItemSyncRoot==null)return Outcome.NotReady;
                    if(!Monitor.TryEnter(p.ItemSyncRoot))return Outcome.Busy;acquired++;
                }
                if(!group.DurableAdmissionCancelled)using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT count(*) FROM revora_arena_matches m
WHERE m.id=@id AND m.state='finished' AND m.player1=@first AND m.player2=@second
AND m.winner=@winner AND m.loser=@loser
AND EXISTS(SELECT 1 FROM revora_arena_result_commits b WHERE b.run=m.id)
AND (SELECT count(*) FROM revora_arena_result_statistics s WHERE s.run=m.id AND s.uid IN(m.player1,m.player2))=2",
                    "id",group.ParticipationKey,"first",(long)group.Player1.Entity.UID,"second",(long)group.Player2.Entity.UID,
                    "winner",group.Winner==null?0L:(long)group.Winner.Entity.UID,"loser",group.Loser==null?0L:(long)group.Loser.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=1)return Outcome.NotReady;}
                return group.CleanupOwned(true)?Outcome.Committed:Outcome.NotReady;
            }catch(Exception ex) {
                Console.WriteLine("Arena cleanup failed: "+ex.GetType().Name);
                return Outcome.Failed;
            }finally {
                for(int n=acquired-1;n>=0;n--)Monitor.Exit(owners[n].ItemSyncRoot);
                Monitor.Exit(group.ParticipationSync);
            }
        }
        internal static Outcome RecoverFenced(Group group) {
            if(group==null)return Outcome.NotReady;
            if(!Monitor.TryEnter(group.ParticipationSync))return Outcome.Busy;
            GameState[] players=null;int acquired=0;
            try {
                if(!group.OwnsImportedMap)return Outcome.NotReady;
                players=new[]{group.Player1,group.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in players) {
                    if(!Monitor.TryEnter(p.ItemSyncRoot))return Outcome.Busy;acquired++;
                    GameState canonical;
                    if((p.Fake && !ArenaActors.IsBot(p)) || !p.Entity.FullyLoaded
                        || !Kernel.GamePool.TryGetValue(p.Entity.UID,out canonical) || !ReferenceEquals(canonical,p)
                        || p.ArenaStatistic==null || p.TeamArenaStatistic==null
                        || p.ArenaStatistic.EntityID!=p.Entity.UID || p.TeamArenaStatistic.EntityID!=p.Entity.UID)return Outcome.NotReady;
                }
                var fenced=players.Where(p=>p.Action==0 && p.Entity.PersistenceRequiresReload
                    && ArenaCacheRecovery.RequiresReload(p.ArenaStatistic) && ArenaCacheRecovery.RequiresReload(p.TeamArenaStatistic)).ToArray();
                if(fenced.Length==0 || players.Any(p=>!fenced.Contains(p) && (!ArenaActors.Player(p,false)
                    || ArenaCacheRecovery.RequiresReload(p.ArenaStatistic) || ArenaCacheRecovery.RequiresReload(p.TeamArenaStatistic))))return Outcome.NotReady;
                if(Volatile.Read(ref group.DurableResultReady)!=0)return Outcome.AlreadyCommitted;
                if(!group.Done) {
                    lock(group.DepartureSync)if(group.PendingDeparture==null) {
                        if(unchecked(MatchClock().Value-group.ImportTime.Value)<180000)return Outcome.NotReady;
                        var observed=ArenaMatchLedger.Clock();
                        if(observed.Kind!=DateTimeKind.Utc)return Outcome.NotReady;
                        var loser=group.Player1Damage>group.Player2Damage?group.Player2:group.Player1;
                        Volatile.Write(ref group.PendingDeparture,new Departure(loser,ArenaResultIntents.Cause.Timeout,observed));
                    }
                    var pending=Volatile.Read(ref group.PendingDeparture);
                    if(pending==null || pending.Observed.Kind!=DateTimeKind.Utc
                        || !(ReferenceEquals(pending.Player,group.Player1) || ReferenceEquals(pending.Player,group.Player2)))return Outcome.NotReady;
                    // The first retained departure is causal evidence even when a later
                    // cache failure prevented the normal owner path from freezing it.
                    if(!group.FreezeOutcome(pending.Player))return Outcome.NotReady;
                }
                if(fenced.Length!=players.Length) {
                    // Validate outcome evidence before invalidating a healthy original peer.
                    var departure=Volatile.Read(ref group.PendingDeparture);
                    if(departure!=null) {if(!ReferenceEquals(departure.Player,group.Loser))return Outcome.NotReady;}
                    else using(var c=DataHolder.MySqlConnection) {c.Open();using(var q=Q.Command(c,null,
                        "SELECT count(*) FROM revora_arena_result_intents WHERE run=@run AND winner=@winner AND loser=@loser",
                        "run",group.ParticipationKey,"winner",group.Winner==null?0L:(long)group.Winner.Entity.UID,
                        "loser",group.Loser==null?0L:(long)group.Loser.Entity.UID))if(Convert.ToInt64(q.ExecuteScalar())!=1)return Outcome.NotReady;}
                    foreach(var p in players.Where(p=>!fenced.Contains(p)))
                        ArenaCacheRecovery.Fail(p,"arena-mixed-recovery",new InvalidOperationException("Arena result requires both owners to reload."));
                }
                if(Volatile.Read(ref group.PendingDeparture)!=null && !ArenaResultIntents.CaptureFenced(group))return Outcome.NotReady;
                if(ArenaStartupRecovery.RecoverFenced(group))return Outcome.Committed;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT count(*) FROM revora_arena_matches m
WHERE m.id=@id AND m.state='finished' AND m.player1=@first AND m.player2=@second AND m.winner=@winner AND m.loser=@loser
AND EXISTS(SELECT 1 FROM revora_arena_result_commits b WHERE b.run=m.id)
AND (SELECT count(*) FROM revora_arena_result_statistics s WHERE s.run=m.id AND s.uid IN(m.player1,m.player2))=2",
                    "id",group.ParticipationKey,"first",(long)group.Player1.Entity.UID,"second",(long)group.Player2.Entity.UID,
                    "winner",group.Winner==null?0L:(long)group.Winner.Entity.UID,"loser",group.Loser==null?0L:(long)group.Loser.Entity.UID))
                    return Convert.ToInt64(q.ExecuteScalar())==1?Outcome.AlreadyCommitted:Outcome.NotReady;}
            }catch(Exception ex) {
                Console.WriteLine("Arena fenced recovery failed: "+ex.GetType().Name);return Outcome.Failed;
            }finally {
                for(int n=acquired-1;n>=0;n--)Monitor.Exit(players[n].ItemSyncRoot);
                Monitor.Exit(group.ParticipationSync);
            }
        }
        internal static Outcome CancelInterrupted(Group group) {
            if(group==null || !ArenaAdmissions.Initialized)return Outcome.NotReady;
            if(!Monitor.TryEnter(group.ParticipationSync))return Outcome.Busy;
            GameState[] owners=null;int acquired=0;
            try {
                if(group.DurableAdmissionCancelled)return Outcome.AlreadyCommitted;
                if(!group.DurableAdmissionReserved || !group.AdmissionInterrupted || group.Done || group.OwnsImportedMap)return Outcome.NotReady;
                owners=new[]{group.Player1,group.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in owners) {
                    if(!Monitor.TryEnter(p.ItemSyncRoot))return Outcome.Busy;acquired++;
                    GameState original;
                    if(!Kernel.GamePool.TryGetValue(p.Entity.UID,out original) || !ReferenceEquals(original,p)
                        || !ReferenceEquals(p.QualifierGroup,group))return Outcome.NotReady;
                }
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    foreach(var p in owners)SkillTable.LockOwner(c,t,p);
                    ArenaAdmissions.Cancel(c,t,group);t.Commit();group.DurableAdmissionCancelled=true;
                }}
                return Outcome.Committed;
            }catch(Exception ex){Console.WriteLine("Solo admission cancellation deferred: "+ex.GetType().Name);return Outcome.Failed;}
            finally{for(int n=acquired-1;n>=0;n--)Monitor.Exit(owners[n].ItemSyncRoot);Monitor.Exit(group.ParticipationSync);}
        }
        internal static Outcome RecoverEntry(Group group) {
            if(group==null || !ArenaAdmissions.Initialized)return Outcome.NotReady;
            if(!Monitor.TryEnter(group.ParticipationSync))return Outcome.Busy;
            GameState[] owners=null;int acquired=0;
            try {
                if(!group.RegisterLedgerOnEntry || !group.DurableAdmissionReserved)return Outcome.NotReady;
                if(!group.EntryRecoveryRequired)return group.DurableAdmissionCancelled || group.ParticipationRecorded?Outcome.AlreadyCommitted:Outcome.NotReady;
                if(!group.OwnsImportedMap)return Outcome.NotReady;
                owners=new[]{group.Player1,group.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in owners) {
                    if(!Monitor.TryEnter(p.ItemSyncRoot))return Outcome.Busy;acquired++;
                    GameState original;
                    if(!Kernel.GamePool.TryGetValue(p.Entity.UID,out original) || !ReferenceEquals(original,p)
                        || (p.Fake && !ArenaActors.IsBot(p)) || !p.Entity.FullyLoaded || p.Action!=0 || !p.Entity.PersistenceRequiresReload
                        || !ArenaCacheRecovery.RequiresReload(p.ArenaStatistic) || !ArenaCacheRecovery.RequiresReload(p.TeamArenaStatistic)
                        || p.ArenaStatistic.EntityID!=p.Entity.UID || p.TeamArenaStatistic.EntityID!=p.Entity.UID)return Outcome.NotReady;
                }
                bool adopted;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    // Fenced owners may lock durable identity for recovery, but may
                    // not use the normal gameplay/skill persistence entry point.
                    foreach(var p in owners)using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Entry recovery owner missing.");
                    bool exists;string entryDay=null;
                    using(var q=Q.Command(c,t,"SELECT player1,player2,map,started_at AT TIME ZONE 'UTC' FROM revora_arena_matches WHERE id=@run FOR UPDATE","run",group.ParticipationKey))
                    using(var r=q.ExecuteReader()) {
                        exists=r.Read();
                        if(exists)entryDay=MagnoliaQuest.Day(DateTime.SpecifyKind(r.GetDateTime(3),DateTimeKind.Utc));
                        if(exists && (r.GetInt64(0)!=group.Player1.Entity.UID || r.GetInt64(1)!=group.Player2.Entity.UID
                            || r.GetInt32(2)!=group.Player1.Map.ID))throw new InvalidOperationException("Recovered entry identity differs.");
                    }
                    adopted=exists;
                    if(adopted) {
                        ArenaAdmissions.ValidateStart(c,t,group);
                        using(var q=Q.Command(c,t,@"SELECT count(*) FROM revora_arena_matches m WHERE m.id=@run AND
(m.entry_evidence_version=0 OR (m.entry_evidence_version=1 AND (SELECT count(*) FROM revora_arena_daily_entries e WHERE e.run=m.id AND e.day=@day
AND (e.uid=m.player1 AND e.opponent=m.player2 OR e.uid=m.player2 AND e.opponent=m.player1))=2))","run",group.ParticipationKey,"day",entryDay))
                            if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Daily Arena entry evidence is incomplete.");
                        using(var q=Q.Command(c,t,@"SELECT count(*) FROM revora_arena_participation p
WHERE p.uid IN(@first,@second) AND p.rule_version=1
AND EXISTS(SELECT 1 FROM revora_achievement_evidence e WHERE e.uid=p.uid AND e.achievement_id=11032)",
                            "first",(long)group.Player1.Entity.UID,"second",(long)group.Player2.Entity.UID))
                            if(Convert.ToInt64(q.ExecuteScalar())!=2)throw new InvalidOperationException("Registered entry evidence is incomplete.");
                    }else {
                        if(group.Done)throw new InvalidOperationException("Unregistered entry already has an outcome.");
                        ArenaAdmissions.Cancel(c,t,group);
                    }
                    t.Commit();
                }}
                if(adopted)group.ParticipationRecorded=true;
                else {group.AdmissionInterrupted=true;group.DurableAdmissionCancelled=true;}
                group.EntryRecoveryRequired=false;
                return Outcome.Committed;
            }catch(Exception ex){Console.WriteLine("Arena entry recovery deferred: "+ex.GetType().Name);return Outcome.Failed;}
            finally{for(int n=acquired-1;n>=0;n--)Monitor.Exit(owners[n].ItemSyncRoot);Monitor.Exit(group.ParticipationSync);}
        }
        private static Outcome CompleteCore(Group group,ArenaResultIntents.Cause cause) {
            if(group==null)return Outcome.NotReady;
            if(!Monitor.TryEnter(group.ParticipationSync))return Outcome.Busy;
            GameState[] players=null;int acquired=0;
            try {
                if(!group.Done || !group.OwnsImportedMap || group.Player1==null || group.Player2==null)return Outcome.NotReady;
                players=new[]{group.Player1,group.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in players) {
                    if(!ArenaActors.Player(p,false))return Outcome.NotReady;
                    if(!Monitor.TryEnter(p.ItemSyncRoot))return Outcome.Busy;acquired++;
                }
                // Retain the first coordinator observation even if the first database capture fails.
                // An earlier queued departure already supplies its original time and cause.
                lock(group.DepartureSync)if(group.PendingDeparture==null) {
                    var observed=ArenaMatchLedger.Clock();if(observed.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena outcome requires UTC.");
                    Volatile.Write(ref group.PendingDeparture,new Departure(group.Loser,cause,observed));
                }
                var pending=Volatile.Read(ref group.PendingDeparture);
                if(pending!=null && !ReferenceEquals(pending.Player,group.Loser))throw new InvalidOperationException("Arena departure outcome changed.");
                if(!(pending==null?ArenaResultIntents.Capture(group,cause):ArenaResultIntents.CaptureAt(group,pending.Cause,pending.Observed)))return Outcome.NotReady;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    if(!ArenaMatchLedger.Finish(c,t,group)) {
                        // A terminal ledger alone is not enough: require this complete transaction bundle.
                        using(var q=Q.Command(c,t,@"SELECT count(*) FROM revora_arena_matches m
WHERE m.id=@id AND m.state='finished' AND m.player1=@first AND m.player2=@second
AND m.winner=@winner AND m.loser=@loser
AND EXISTS(SELECT 1 FROM revora_arena_result_commits b WHERE b.run=m.id)
AND (SELECT count(*) FROM revora_arena_result_statistics s WHERE s.run=m.id AND s.uid IN(m.player1,m.player2))=2",
                            "id",group.ParticipationKey,"first",(long)group.Player1.Entity.UID,"second",(long)group.Player2.Entity.UID,
                            "winner",group.Winner==null?0L:(long)group.Winner.Entity.UID,"loser",group.Loser==null?0L:(long)group.Loser.Entity.UID))
                            return Convert.ToInt64(q.ExecuteScalar())==1?Outcome.AlreadyCommitted:Outcome.NotReady;
                    }
                    var rows=ArenaResultStatistics.Record(c,t,group);
                    ArenaDailyRewards.Record(c,t,group);ArenaCustomRewards.Record(c,t,group);
                    if(Q.Execute(c,t,"INSERT INTO revora_arena_result_commits(run) VALUES(@run)","run",group.ParticipationKey)!=1)throw new InvalidOperationException("Arena result bundle was not saved.");
                    t.Commit();ArenaResultStatistics.Publish(rows);
                    group.ResultNotificationReady=true;group.NotifyCommittedOutcome();return Outcome.Committed;
                }}
            }catch(Exception ex) {
                // Includes post-commit projection failure. Reconnect loads durable state; never pay twice.
                if(players!=null)foreach(var p in players)if(p!=null && p.Entity!=null)ArenaCacheRecovery.Fail(p,"arena-result",ex);
                return Outcome.Failed;
            }finally {
                for(int n=acquired-1;n>=0;n--)Monitor.Exit(players[n].ItemSyncRoot);
                Monitor.Exit(group.ParticipationSync);
            }
        }
    }
}
