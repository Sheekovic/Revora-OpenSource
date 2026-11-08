using System;
using System.Collections.Generic;
using System.Threading;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;
using Outcome=Revora.Game.Features.ArenaResultCoordinator.Outcome;
namespace Revora.Game.Features
{
    // The composition root owns one instance. Not activated beside legacy result writers.
    internal sealed class ArenaCompletionPump
    {
        private sealed class Entry {
            internal Group Group;internal DateTime LastAttempt,NextAttempt;internal bool Attempted;internal int Failures;
        }
        private readonly object gate=new object(),tickGate=new object();
        private readonly Dictionary<Guid,Entry> entries=new Dictionary<Guid,Entry>();
        private readonly Queue<Entry> queue=new Queue<Entry>();
        private readonly int capacity;
        internal Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal ArenaCompletionPump(int capacity) {
            if(capacity<1 || capacity>4096)throw new ArgumentOutOfRangeException("capacity");this.capacity=capacity;
        }
        internal int Count {get{lock(gate)return entries.Count;}}
        internal static bool ResultReady(Group group){return group!=null && Volatile.Read(ref group.DurableResultReady)!=0;}
        internal bool Track(Group group) {
            if(group==null)return false;
            lock(gate) {
                Entry existing;if(entries.TryGetValue(group.ParticipationKey,out existing))return ReferenceEquals(existing.Group,group);
                if((ResultReady(group) || group.DurableAdmissionCancelled) && group.CleanupCompleted)return true;
                if((!ResultReady(group) && !group.AdmissionInterrupted && !group.OwnsImportedMap) || entries.Count>=capacity)return false;
                var entry=new Entry{Group=group};entries.Add(group.ParticipationKey,entry);queue.Enqueue(entry);return true;
            }
        }
        private static Outcome Advance(Group group) {
            if(group.EntryRecoveryRequired) {
                var entry=ArenaResultCoordinator.RecoverEntry(group);
                if(entry!=Outcome.Committed && entry!=Outcome.AlreadyCommitted)return entry;
            }
            if(group.AdmissionInterrupted) {
                var cancelled=ArenaResultCoordinator.CancelInterrupted(group);
                if(cancelled!=Outcome.Committed && cancelled!=Outcome.AlreadyCommitted)return cancelled;
                return ArenaResultCoordinator.Cleanup(group);
            }
            if(!ResultReady(group)) {
                Outcome result;
                if(group.Player1.Entity.PersistenceRequiresReload || group.Player2.Entity.PersistenceRequiresReload)
                    result=ArenaResultCoordinator.RecoverFenced(group);
                else if(group.Done)result=ArenaResultCoordinator.Complete(group);
                else if(Volatile.Read(ref group.PendingDeparture)!=null)result=ArenaResultCoordinator.RetryDeparture(group);
                else result=ArenaResultCoordinator.EndTimedOut(group);
                if(result!=Outcome.Committed && result!=Outcome.AlreadyCommitted)return result;
                // Sticky proof belongs to this exact match, not the queue entry or character UID.
                Volatile.Write(ref group.DurableResultReady,1);
            }
            return ArenaResultCoordinator.Cleanup(group);
        }
        internal int Tick(int budget) {
            if(budget<1 || budget>128)throw new ArgumentOutOfRangeException("budget");
            if(!Monitor.TryEnter(tickGate))return 0;
            try {
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena completion scheduling requires UTC.");
                int count;lock(gate)count=Math.Min(budget,queue.Count);int completed=0;
                for(int n=0;n<count;n++) {
                    Entry entry;lock(gate)entry=queue.Dequeue();
                    if(entry.Attempted && now>=entry.LastAttempt && now<entry.NextAttempt) {lock(gate)queue.Enqueue(entry);continue;}
                    Outcome result;var attempted=now;
                    try{result=Advance(entry.Group);attempted=Clock();if(attempted.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena retry clock requires UTC.");}
                    catch(Exception ex){Console.WriteLine("Arena completion retry failed: "+ex.GetType().Name);result=Outcome.Failed;attempted=now;}
                    if(result==Outcome.Committed || result==Outcome.AlreadyCommitted) {
                        lock(gate)entries.Remove(entry.Group.ParticipationKey);completed++;continue;
                    }
                    int delay=1;
                    if(result==Outcome.Failed){delay=Math.Min(30,1<<Math.Min(entry.Failures,5));entry.Failures=Math.Min(entry.Failures+1,5);}
                    else entry.Failures=0;
                    entry.Attempted=true;entry.LastAttempt=attempted;
                    entry.NextAttempt=attempted>DateTime.MaxValue.AddSeconds(-delay)?DateTime.MaxValue:attempted.AddSeconds(delay);
                    lock(gate)queue.Enqueue(entry);
                }
                return completed;
            }finally{Monitor.Exit(tickGate);}
        }
    }
}
