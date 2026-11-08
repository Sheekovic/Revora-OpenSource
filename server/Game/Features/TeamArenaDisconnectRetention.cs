using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Group=Revora.Game.TeamQualifier.QualifierList.QualifierGroup;
namespace Revora.Game.Features
{
    // Owned imports join CheckGroups scheduling; Attach-only callers must tick explicitly.
    internal sealed class TeamArenaDisconnectRetention : IArenaShutdownRetention
    {
        private sealed class Entry {
            internal Group Group;internal GameState[] Players;internal HashSet<GameState> Requested=new HashSet<GameState>();
            internal volatile bool Preparing,Rejected,Tracked;
        }
        private readonly object gate=new object(),tickGate=new object();
        private static readonly object scheduleGate=new object();
        private static readonly Queue<TeamArenaDisconnectRetention> scheduled=new Queue<TeamArenaDisconnectRetention>();
        private bool scheduledOwner;
        private static int scheduledTicks;
        internal static bool HasScheduled { get { lock(scheduleGate) return scheduled.Count != 0 || scheduledTicks != 0; } }
        // Register under the instance gate; retire under the same gate after ticking.
        private void Schedule() {
            if(scheduledOwner)return;
            lock(scheduleGate){scheduled.Enqueue(this);scheduledOwner=true;}
        }
        internal static void TickScheduled() {
            int count;lock(scheduleGate)count=Math.Min(16,scheduled.Count);
            for(int n=0;n<count;n++) {
                TeamArenaDisconnectRetention owner;
                lock(scheduleGate){if(scheduled.Count==0)break;owner=scheduled.Dequeue();scheduledTicks++;}
                try{owner.Tick(16);}
                finally{lock(owner.gate)lock(scheduleGate){
                    scheduledTicks--;
                    if(owner.groups.Count==0)owner.scheduledOwner=false;else scheduled.Enqueue(owner);
                }}
            }
        }
        private readonly Dictionary<Group,Entry> groups=new Dictionary<Group,Entry>();
        private readonly Queue<Entry> queue=new Queue<Entry>();
        private readonly Dictionary<GameState,Entry> players=new Dictionary<GameState,Entry>();
        private readonly TeamArenaCompletionPump pump;
        private readonly int capacity;
        private readonly Action<GameState,bool> resume;
        internal TeamArenaDisconnectRetention(int capacity,Action<GameState,bool> resume) {
            if(capacity<1 || capacity>4096)throw new ArgumentOutOfRangeException("capacity");
            this.capacity=capacity;this.resume=resume??((p,requested)=>{if(requested)p.ResumeArenaShutdown();else p.Disconnect();});
            pump=new TeamArenaCompletionPump(capacity);
        }
        internal int Count {get{lock(gate)return groups.Count;}}
        private static bool Settled(Group group){return Volatile.Read(ref group.DurableResultReady)!=0 || group.DurableAdmissionCancelled;}
        internal bool Import(Group group) {
            using (var admission = ArenaShutdownDrain.Enter()) {
                return admission != null && ImportCore(group);
            }
        }
        private bool ImportCore(Group group) {
            return group!=null && !ArenaShutdownDrain.Stopping && ArenaStartupPreparation.Prepared && TeamArenaAdmissions.Initialized
                && TeamArenaResultStatistics.Initialized && group.ImportWithRetention(this);
        }
        // The group supplies one roster snapshot, also used by its reservation and transfer.
        internal bool Enter(Group group,GameState[] roster,Action import) {
            if(ArenaShutdownDrain.Stopping || !ArenaStartupPreparation.Prepared || !TeamArenaAdmissions.Initialized || !TeamArenaResultStatistics.Initialized
                || !Monitor.TryEnter(group.ResultSync))return false;
            var owners=roster.OrderBy(p=>p.Entity.UID).ToArray();int acquired=0;Entry entry=null;
            try {
                if(group.ImportStarted || group.Done || owners.Length<2 || owners.Select(p=>p.Entity.UID).Distinct().Count()!=owners.Length)return false;
                foreach(var p in owners){if(!Monitor.TryEnter(p.ItemSyncRoot))return false;acquired++;}
                if(owners.Any(p=>!QuestInventory.Player(p,false) || p.ShutdownStarted || Kernel.DisconnectPool.ContainsKey(p.Entity.UID)
                    || p.QualifierGroup!=null || p.TeamQualifierGroup!=null || p.WatchingGroup!=null || p.TeamWatchingGroup!=null))return false;
                lock(gate) {
                    if(groups.Count>=capacity || groups.ContainsKey(group) || owners.Any(p=>p.ArenaShutdownRetention!=null || players.ContainsKey(p)))return false;
                    entry=new Entry{Group=group,Players=owners,Preparing=true};groups.Add(group,entry);queue.Enqueue(entry);
                    group.CompletionOwner=this;
                    Schedule();
                    foreach(var p in owners){players.Add(p,entry);p.ArenaShutdownRetention=this;}
                }
                if(owners.Any(p=>TeamQualifier.SignupWithdrawals.Pending(p)) || !TeamArenaDayRollover.PrepareAdmission(group,owners)
                    || owners.Any(p=>TeamQualifier.SignupWithdrawals.Pending(p)))return false;
                import();return group.ResultAdmissionReady && !group.AdmissionInterrupted;
            }catch(Exception ex){Console.WriteLine("Owned team Arena entry deferred: "+ex.GetType().Name);return false;}
            finally {
                try {
                    if(entry!=null) {
                        entry.Rejected=!group.ImportStarted;
                        entry.Tracked=entry.Rejected || pump.Track(group);
                    }
                }finally {
                    if(entry!=null)entry.Preparing=false;
                    for(int n=acquired-1;n>=0;n--)Monitor.Exit(owners[n].ItemSyncRoot);Monitor.Exit(group.ResultSync);
                }
            }
        }
        internal bool Attach(Group group) {
            if(group==null || !Monitor.TryEnter(group.ResultSync))return false;
            GameState[] owners=null;int acquired=0;
            try {
                if(!group.ResultAdmissionReady || !group.ResultMapRegistered || group.Done)return false;
                owners=group.Participants(true).Concat(group.Participants(false)).OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in owners){if(!Monitor.TryEnter(p.ItemSyncRoot))return false;acquired++;}
                if(owners.Length<2 || owners.Any(p=>!QuestInventory.Player(p,false) || p.ShutdownStarted || Kernel.DisconnectPool.ContainsKey(p.Entity.UID) || !ReferenceEquals(p.TeamQualifierGroup,group)))return false;
                lock(gate) {
                    if(groups.ContainsKey(group))return true;
                    if(groups.Count>=capacity || owners.Any(p=>p.ArenaShutdownRetention!=null || players.ContainsKey(p)))return false;
                    if(!pump.Track(group))return false;
                    var entry=new Entry{Group=group,Players=owners,Tracked=true};groups.Add(group,entry);queue.Enqueue(entry);
                    foreach(var p in owners){players.Add(p,entry);p.ArenaShutdownRetention=this;}
                    return true;
                }
            }finally{for(int n=acquired-1;n>=0;n--)Monitor.Exit(owners[n].ItemSyncRoot);Monitor.Exit(group.ResultSync);}
        }
        public bool ShouldDefer(GameState player) {
            Entry entry;
            lock(gate) {
                if(!players.TryGetValue(player,out entry))return false;
                entry.Requested.Add(player);
            }
            GameState canonical;
            if(!Kernel.GamePool.TryGetValue(player.Entity.UID,out canonical) || !ReferenceEquals(canonical,player))return false;
            // Keep the existing login barrier while the canonical actor remains available to recovery.
            Kernel.DisconnectPool.TryAdd(player.Entity.UID,player);
            if(entry.Preparing)return true;
            if(entry.Rejected || Settled(entry.Group))return false;
            try{TeamArenaResultCoordinator.ObserveDisconnect(entry.Group,player);}
            catch(Exception ex){Console.WriteLine("Team shutdown observation deferred: "+ex.GetType().Name);}
            return !Settled(entry.Group);
        }
        internal int Tick(int budget) {
            if(budget<1 || budget>128)throw new ArgumentOutOfRangeException("budget");
            if(!Monitor.TryEnter(tickGate))return 0;
            try {
                pump.Tick(budget);
                int count;lock(gate)count=Math.Min(budget,queue.Count);
                int released=0;
                for(int n=0;n<count;n++) {
                    Entry entry;lock(gate)entry=queue.Dequeue();
                    if(entry.Preparing){lock(gate)queue.Enqueue(entry);continue;}
                    if(entry.Rejected) {
                        try{entry.Group.RejectUnimported();}
                        catch(Exception ex){Console.WriteLine("Arena invitation cleanup deferred: "+ex.GetType().Name);}
                    }
                    if(!entry.Tracked)entry.Tracked=pump.Track(entry.Group);
                    if(!entry.Rejected && !Settled(entry.Group)){lock(gate)queue.Enqueue(entry);continue;}
                    foreach(var p in entry.Players) {
                        if(!Monitor.TryEnter(p.ItemSyncRoot))continue;
                        try {
                        bool requested;lock(gate){if(!players.ContainsKey(p))continue;requested=entry.Requested.Contains(p);}
                        GameState canonical;
                        bool original=Kernel.GamePool.TryGetValue(p.Entity.UID,out canonical) && ReferenceEquals(canonical,p);
                        if(ReferenceEquals(p.ArenaShutdownRetention,this))p.ArenaShutdownRetention=null;
                        try {
                            if(original && (requested || p.Entity.PersistenceRequiresReload)) {
                                resume(p,requested);
                                if(!p.ShutdownStarted && Kernel.GamePool.TryGetValue(p.Entity.UID,out canonical) && ReferenceEquals(canonical,p)) {
                                    if(p.ArenaShutdownRetention==null)p.ArenaShutdownRetention=this;
                                    continue;
                                }
                            }
                            else if(!original)p.ReleaseSessionRegistration(true);
                        }catch(Exception ex) {
                            if(p.ArenaShutdownRetention==null)p.ArenaShutdownRetention=this;
                            Console.WriteLine("Team shutdown resumption deferred: "+ex.GetType().Name);continue;
                        }
                        lock(gate)players.Remove(p);released++;
                        }finally{Monitor.Exit(p.ItemSyncRoot);}
                    }
                    lock(gate) {
                        if(entry.Group.ResultCleanupCompleted && entry.Players.All(p=>!players.ContainsKey(p)))groups.Remove(entry.Group);
                        else queue.Enqueue(entry);
                    }
                }
                return released;
            }finally{Monitor.Exit(tickGate);}
        }
    }
}
