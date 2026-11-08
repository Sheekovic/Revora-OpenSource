using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Session-owned requests: metadata gates never enclose actor locks or database work.
    internal sealed class ArenaSignupWithdrawal
    {
        private readonly bool team;
        private readonly object gate=new object(),tickGate=new object();
        private readonly HashSet<GameState> pending=new HashSet<GameState>();
        private readonly Queue<GameState> queue=new Queue<GameState>();
        internal ArenaSignupWithdrawal(bool team){this.team=team;}
        internal bool Pending(GameState p){lock(gate)return p!=null && pending.Contains(p);}
        internal int Count { get { lock(gate) return pending.Count; } }
        internal void Request(GameState p) {
            if(p==null || p.Entity==null || !ArenaStartupPreparation.Prepared)return;
            lock(gate) {
                if(!pending.Contains(p)) {
                    if(pending.Count>=1024)return;
                    pending.Add(p);queue.Enqueue(p);
                }
            }
            Tick();
        }
        internal void Tick() {
            if(!ArenaStartupPreparation.Prepared || !Monitor.TryEnter(tickGate))return;
            try {
                int count;lock(gate)count=Math.Min(16,queue.Count);
                for(int n=0;n<count;n++) {
                    GameState p;lock(gate)p=queue.Dequeue();bool done=false;
                    try{done=Attempt(p);}catch(Exception ex){Console.WriteLine("[arena-withdrawal] error="+ex.GetType().Name);}
                    finally{lock(gate){if(done)pending.Remove(p);else queue.Enqueue(p);}}
                }
            }finally{Monitor.Exit(tickGate);}
        }
        private void RemoveQueues(GameState p) {
            var waiting=team?TeamQualifier.WaitingPlayerList:Qualifier.WaitingPlayerList;
            var players=team?TeamQualifier.PlayerList:Qualifier.PlayerList;
            foreach(uint key in new[]{p.Entity.UID,p.Account==null?0u:p.Account.EntityID}.Distinct()) {
                var entry=new KeyValuePair<uint,GameState>(key,p);
                ((ICollection<KeyValuePair<uint,GameState>>)waiting).Remove(entry);
                ((ICollection<KeyValuePair<uint,GameState>>)players).Remove(entry);
            }
        }
        private bool Attempt(GameState p) {
            if(p.Entity==null)return true;
            if(!Monitor.TryEnter(p.ItemSyncRoot))return false;
            try {
                GameState current;
                if(!Kernel.GamePool.TryGetValue(p.Entity.UID,out current) || !ReferenceEquals(current,p)){RemoveQueues(p);return true;}
                if(!(team?Q.Player(p,false):ArenaActors.Player(p,false)))return false;
                if(team && p.TeamQualifierGroup!=null) {
                    var group=p.TeamQualifierGroup;if(group.CompletionOwner==null)return false;
                    if(!ReferenceEquals(p,group.Player1) && !ReferenceEquals(p,group.Player2))return group.WithdrawMember(p);
                    TeamArenaResultCoordinator.EndForPlayer(group,p);
                    return group.PendingDeparture!=null || group.Done;
                }
                if(!team && p.QualifierGroup!=null) {
                    var group=p.QualifierGroup;if(group.CompletionOwner==null)return false;
                    ArenaResultCoordinator.EndForPlayer(group,p,ArenaResultIntents.Cause.Forfeit);
                    return group.PendingDeparture!=null || group.Done;
                }
                var peer=team?p.TeamArenaStatistic.PlayWith:p.ArenaStatistic.PlayWith;
                if(peer!=null) {
                    if(team)TeamQualifier.RejectInvitation(peer,p);else Qualifier.RejectInvitation(peer,p);
                }
                bool cleanup=team?TeamQualifier.QualifierList.Groups.Values.Any(g=>g.OwnedEntryRequested && !g.ImportStarted && (ReferenceEquals(g.Player1,p)||ReferenceEquals(g.Player2,p)))
                    :Qualifier.QualifierList.Groups.Values.Any(g=>g.OwnedEntryRequested && !g.ImportStarted && (ReferenceEquals(g.Player1,p)||ReferenceEquals(g.Player2,p)));
                if(cleanup)return false;
                bool reciprocal=peer!=null && (team?peer.TeamArenaStatistic!=null && ReferenceEquals(peer.TeamArenaStatistic.PlayWith,p)
                    :peer.ArenaStatistic!=null && ReferenceEquals(peer.ArenaStatistic.PlayWith,p));
                if(reciprocal && (team?p.TeamArenaStatistic.PlayWith!=null:p.ArenaStatistic.PlayWith!=null)
                    && (team?p.QualifierGroup==null:p.TeamQualifierGroup==null))return false;
                RemoveQueues(p);
                if(team){p.TeamArenaStatistic.PlayWith=null;p.TeamArenaStatistic.AcceptBox=false;p.TeamArenaStatistic.HasBox=false;
                    p.TeamArenaStatistic.Status=Revora.Network.GamePackets.TeamQualifierStatistic.NotSignedUp;p.TeamArenaStatistic.Send(p);}
                else{p.ArenaStatistic.PlayWith=null;p.ArenaStatistic.AcceptBox=false;p.ArenaStatistic.Status=Revora.Network.GamePackets.QualifierStatistic.NotSignedUp;
                    p.ArenaState=Qualifier.None;p.ArenaStatistic.Send(p);}
                return true;
            }finally{Monitor.Exit(p.ItemSyncRoot);}
        }
    }
}
