using System;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class Marriages
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Offer {internal GameState Sender;internal Entity SenderEntity,ReceiverEntity;internal DateTime Until;internal Guid Id;}
        private sealed class State {internal Offer Pending;internal DateTime LastRequest;}
        private static readonly ConditionalWeakTable<GameState,State> states=new ConditionalWeakTable<GameState,State>();
        private static bool Ready(GameState p) {return Q.Player(p) && p.Account!=null && p.Account.State==AccountTable.AccountState.Player
            && p.Entity.Spouse=="None" && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        private static bool Pair(GameState a,GameState b) {return Ready(a) && Ready(b) && a.Entity.UID!=b.Entity.UID
            && a.Map!=null && ReferenceEquals(a.Map,b.Map) && a.Entity.MapID==b.Entity.MapID
            && Kernel.GetDistance(a.Entity.X,a.Entity.Y,b.Entity.X,b.Entity.Y)<=12
            && (Flowers.IsBoy(a.Entity.Body) && Flowers.IsGirl(b.Entity.Body) || Flowers.IsGirl(a.Entity.Body) && Flowers.IsBoy(b.Entity.Body));}
        private static bool Consent(Offer offer,GameState sender,GameState receiver) {return offer!=null && ReferenceEquals(offer.Sender,sender)
            && ReferenceEquals(offer.SenderEntity,sender.Entity) && ReferenceEquals(offer.ReceiverEntity,receiver.Entity) && Clock()<offer.Until;}
        internal static void Process(GameState player,Attack packet) {
            if(!Ready(player) || packet==null || packet.Attacker!=player.Entity.UID
                || (packet.AttackType!=Attack.MarriageRequest && packet.AttackType!=Attack.MarriageAccept))return;
            GameState peer;if(!Kernel.GamePool.TryGetValue(packet.Attacked,out peer) || !Ready(peer) || ReferenceEquals(peer,player))return;
            var first=player.Entity.UID<peer.Entity.UID?player:peer;var second=ReferenceEquals(first,player)?peer:player;
            lock(first.ItemSyncRoot)lock(second.ItemSyncRoot)try {
                if(!Pair(player,peer))return;
                var own=states.GetValue(player,_=>new State());var other=states.GetValue(peer,_=>new State());
                if(packet.AttackType==Attack.MarriageRequest) {
                    var now=Clock();if(now-own.LastRequest<TimeSpan.FromSeconds(1))return;own.LastRequest=now;
                    other.Pending=new Offer{Sender=player,SenderEntity=player.Entity,ReceiverEntity=peer.Entity,Until=now.AddMinutes(2),Id=Guid.NewGuid()};
                    peer.Send(Friendships.InvitationInfo(player,peer));
                    peer.Send(new Attack(true){AttackType=Attack.MarriageRequest,Attacker=player.Entity.UID,Attacked=peer.Entity.UID,X=peer.Entity.X,Y=peer.Entity.Y});
                    return;
                }
                var offer=own.Pending;if(!Consent(offer,peer,player))return;
                string firstName=first.Entity.Name,secondName=second.Entity.Name;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,first);SkillTable.LockOwner(c,t,second);
                    if(!Pair(player,peer) || !Consent(offer,peer,player))return;
                    foreach(var p in new[]{first,second}) {
                        string name=ReferenceEquals(p,first)?firstName:secondName;
                        using(var q=Q.Command(c,t,"SELECT name,spouse FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())if(!r.Read() || r.GetString(0)!=name || r.GetString(1)!="None" || r.Read())
                            throw new InvalidOperationException("Marriage identity requires reload.");
                    }
                    foreach(var p in new[]{first,second}) {
                        string name=ReferenceEquals(p,first)?firstName:secondName,spouse=ReferenceEquals(p,first)?secondName:firstName;
                        if(Q.Execute(c,t,"UPDATE entities SET spouse=@spouse WHERE uid=@uid AND name=@name AND spouse='None'",
                            "spouse",spouse,"uid",(long)p.Entity.UID,"name",name)!=1)throw new InvalidOperationException("Marriage link was not saved.");
                        ProgressionEvidence.Complete(c,t,p.Entity.UID,10129,"mutual-marriage",offer.Id.ToString("N"),1);
                    }
                    if(!Pair(player,peer) || !Consent(offer,peer,player) || first.Entity.Name!=firstName || second.Entity.Name!=secondName)
                        throw new InvalidOperationException("Marriage consent changed.");
                    t.Commit();
                }}
                own.Pending=null;other.Pending=null;
                first.Entity.Spouse=secondName;second.Entity.Spouse=firstName;
                Q.Refresh(first);Q.Refresh(second);
                first.Entity.Update(_String.Effect,"firework-2love",true);second.Entity.Update(_String.Effect,"firework-2love",true);
                first.Send("You are now married to "+secondName+".");second.Send("You are now married to "+firstName+".");
            }catch(Exception ex){Q.Fail(first,"marriage",ex);Q.Fail(second,"marriage",ex);}
        }
    }
}
