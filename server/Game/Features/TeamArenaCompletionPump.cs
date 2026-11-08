using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Group=Revora.Game.TeamQualifier.QualifierList.QualifierGroup;
using Outcome=Revora.Game.Features.TeamArenaResultCoordinator.Outcome;
namespace Revora.Game.Features
{
    // The composition root owns one instance. Not activated beside legacy result writers.
    internal sealed class TeamArenaCompletionPump
    {
        private sealed class Entry {
            internal Group Group;internal DateTime LastAttempt,NextAttempt;internal bool Attempted;internal int Failures;
        }
        private readonly object gate=new object(),tickGate=new object();
        private readonly Dictionary<Guid,Entry> entries=new Dictionary<Guid,Entry>();
        private readonly Queue<Entry> queue=new Queue<Entry>();
        private readonly int capacity;
        internal Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal TeamArenaCompletionPump(int capacity) {
            if(capacity<1 || capacity>4096)throw new ArgumentOutOfRangeException("capacity");this.capacity=capacity;
        }
        internal int Count {get{lock(gate)return entries.Count;}}
        internal static bool ResultReady(Group group){return group!=null && Volatile.Read(ref group.DurableResultReady)!=0;}
        internal bool Track(Group group) {
            if(group==null)return false;
            lock(gate) {
                Entry existing;if(entries.TryGetValue(group.ResultKey,out existing))return ReferenceEquals(existing.Group,group);
                if((ResultReady(group) || group.DurableAdmissionCancelled) && group.ResultCleanupCompleted)return true;
                if((!ResultReady(group) && !group.AdmissionInterrupted && (!group.ResultMapRegistered || !group.ResultAdmissionReady)) || entries.Count>=capacity)return false;
                var entry=new Entry{Group=group};entries.Add(group.ResultKey,entry);queue.Enqueue(entry);return true;
            }
        }
        private static Outcome Advance(Group group) {
            if(!Monitor.TryEnter(group.ResultSync))return Outcome.Busy;
            try {
                if(group.AdmissionInterrupted) {
                    var cancelled=TeamArenaResultCoordinator.CancelInterrupted(group);
                    if(cancelled!=Outcome.Committed && cancelled!=Outcome.AlreadyCommitted)return cancelled;
                    return TeamArenaResultCoordinator.Cleanup(group);
                }
                if(!ResultReady(group)) {
                    Outcome result;
                    if(group.Participants(true).Concat(group.Participants(false)).Any(p=>p.Entity.PersistenceRequiresReload))
                        result=TeamArenaResultCoordinator.RecoverFenced(group);
                    else if(group.Done)result=TeamArenaResultCoordinator.Complete(group);
                    else if(Volatile.Read(ref group.PendingDeparture)!=null)result=TeamArenaResultCoordinator.RetryDeparture(group);
                    else result=TeamArenaResultCoordinator.EndTimedOut(group);
                    if(result!=Outcome.Committed && result!=Outcome.AlreadyCommitted)return result;
                }
                return TeamArenaResultCoordinator.Cleanup(group);
            }finally{Monitor.Exit(group.ResultSync);}
        }
        internal int Tick(int budget) {
            if(budget<1 || budget>128)throw new ArgumentOutOfRangeException("budget");
            if(!Monitor.TryEnter(tickGate))return 0;
            try {
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Team Arena completion scheduling requires UTC.");
                int count;lock(gate)count=Math.Min(budget,queue.Count);int completed=0;
                for(int n=0;n<count;n++) {
                    Entry entry;lock(gate)entry=queue.Dequeue();
                    if(entry.Attempted && now>=entry.LastAttempt && now<entry.NextAttempt) {lock(gate)queue.Enqueue(entry);continue;}
                    Outcome result;var attempted=now;
                    try{result=Advance(entry.Group);attempted=Clock();if(attempted.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Team Arena retry clock requires UTC.");}
                    catch(Exception ex){Console.WriteLine("Team Arena completion retry failed: "+ex.GetType().Name);result=Outcome.Failed;attempted=now;}
                    if(result==Outcome.Committed || result==Outcome.AlreadyCommitted) {
                        lock(gate)entries.Remove(entry.Group.ResultKey);completed++;continue;
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
