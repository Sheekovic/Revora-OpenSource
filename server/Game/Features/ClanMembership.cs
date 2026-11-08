using System;
using System.Runtime.CompilerServices;
using System.Text;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class ClanMembership {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Offer {internal GameState Sender;internal Entity SenderBody,ReceiverBody;internal Clan Clan;internal byte Action;internal DateTime Until;internal Guid Receipt;internal long SenderVersion,ReceiverVersion;}
        private sealed class State {internal Offer Pending;internal DateTime LastRequest;internal long Version;}
        private static readonly ConditionalWeakTable<GameState,State> states=new ConditionalWeakTable<GameState,State>();
        private static bool Ready(GameState p){return Q.Player(p) && p.Map!=null && ReferenceEquals(p.Entity.Owner,p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        private static bool Pair(GameState a,GameState b){return Ready(a) && Ready(b) && a!=b && ReferenceEquals(a.Map,b.Map)
            && a.Entity.MapID==b.Entity.MapID && Kernel.GetDistance(a.Entity.X,a.Entity.Y,b.Entity.X,b.Entity.Y)<=12;}
        private static bool Eligible(GameState leader,GameState member,Clan clan){Clan live;return Pair(leader,member) && clan!=null
            && Kernel.Clans.TryGetValue(clan.ID,out live) && ReferenceEquals(live,clan) && clan.LeaderId==leader.Entity.UID
            && leader.Entity.ClanId==clan.ID && leader.Entity.ClanRank==Clan.Ranks.ClanLeader && member.Entity.ClanId==0
            && !clan.Members.ContainsKey(member.Entity.UID) && clan.Members.Count<12;}
        private static bool Consent(Offer offer,GameState sender,GameState receiver,Clan clan,byte action){return offer!=null && offer.Action==action
            && ReferenceEquals(offer.Sender,sender) && ReferenceEquals(offer.SenderBody,sender.Entity) && ReferenceEquals(offer.ReceiverBody,receiver.Entity)
            && ReferenceEquals(offer.Clan,clan) && Clock()<offer.Until && states.GetValue(sender,_=>new State()).Version==offer.SenderVersion
            && states.GetValue(receiver,_=>new State()).Version==offer.ReceiverVersion;}
        internal static void Invalidate(GameState p){
            if(!System.Threading.Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Clan consent invalidation requires its owner lock.");
            var state=states.GetValue(p,_=>new State());state.Pending=null;state.Version++;
        }
        internal static bool Handle(byte[] packet,GameState p){
            if(packet==null || packet.Length<5)return true;
            byte action=packet[4];if(action<9 || action>12)return false;
            if(packet.Length<26 || packet.Length>1024 || BitConverter.ToUInt16(packet,0)!=packet.Length-8 || BitConverter.ToUInt16(packet,2)!=1312
                || Encoding.ASCII.GetString(packet,packet.Length-8,8)!="TQClient" || !Ready(p))return true;
            GameState peer;if(!Kernel.GamePool.TryGetValue(BitConverter.ToUInt32(packet,8),out peer) || !Pair(p,peer))return true;
            var first=p.Entity.UID<peer.Entity.UID?p:peer;var second=ReferenceEquals(first,p)?peer:p;
            lock(first.ItemSyncRoot)lock(second.ItemSyncRoot)try{
                if(!Pair(p,peer))return true;
                bool request=action==9 || action==11;var leader=action==9 || action==12?p:peer;var member=ReferenceEquals(leader,p)?peer:p;
                var clan=leader.Entity.GetClan;if(!Eligible(leader,member,clan))return true;
                var own=states.GetValue(p,_=>new State());var other=states.GetValue(peer,_=>new State());
                if(request){var now=Clock();if(now-own.LastRequest<TimeSpan.FromSeconds(1))return true;own.LastRequest=now;
                    other.Pending=new Offer{Sender=p,SenderBody=p.Entity,ReceiverBody=peer.Entity,Clan=clan,Action=action,Until=now.AddMinutes(2),Receipt=Guid.NewGuid(),SenderVersion=own.Version,ReceiverVersion=other.Version};
                    string[] names=action==9?new[]{clan.Name,p.Entity.Name}:new[]{p.Entity.Name};
                    int body=action==9?81+clan.Name.Length+p.Entity.Name.Length:18+p.Entity.Name.Length;
                    var response=new byte[body+8];Writer.WriteUInt16((ushort)body,0,response);Writer.WriteUInt16(1312,2,response);response[4]=action;
                    Writer.WriteUInt32(p.Entity.UID,8,response);Writer.WriteStringList(new System.Collections.Generic.List<string>(names),16,response);peer.Send(response);return true;
                }
                byte requested=(byte)(action-1);var offer=own.Pending;
                if(!Consent(offer,peer,p,clan,requested))return true;
                if(packet[16]!=1){own.Pending=null;return true;}
                Clan.Ranks rank=member.Entity.Spouse==leader.Entity.Name?Clan.Ranks.Spouse:Clan.Ranks.Member;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,first);SkillTable.LockOwner(c,t,second);
                    using(var q=Q.Command(c,t,"SELECT leaderid FROM clans WHERE identifier=@clan FOR UPDATE","clan",(long)clan.ID))
                        if(Convert.ToUInt32(q.ExecuteScalar())!=leader.Entity.UID)throw new InvalidOperationException("Clan leader changed.");
                    using(var q=Q.Command(c,t,"SELECT clanid,clanrank FROM entities WHERE uid=@uid","uid",(long)leader.Entity.UID))
                    using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToUInt32(r[0])!=clan.ID || Convert.ToInt32(r[1])!=(int)Clan.Ranks.ClanLeader)
                        throw new InvalidOperationException("Clan authority changed.");
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM entities WHERE clanid=@clan","clan",(long)clan.ID))if(Convert.ToInt64(q.ExecuteScalar())>=12)return true;
                    if(!Eligible(leader,member,clan) || !Consent(offer,peer,p,clan,requested))return true;
                    if(Q.Execute(c,t,"UPDATE entities SET clanid=@clan,clanrank=@rank,clandonation=0 WHERE uid=@uid AND COALESCE(clanid,0)=0",
                        "clan",(long)clan.ID,"rank",(int)rank,"uid",(long)member.Entity.UID)!=1)throw new InvalidOperationException("Clan membership changed.");
                    ProgressionEvidence.Complete(c,t,member.Entity.UID,10304,"clan-membership-v1",offer.Receipt.ToString("N"),1);
                    if(!Eligible(leader,member,clan) || !Consent(offer,peer,p,clan,requested))throw new InvalidOperationException("Clan consent changed.");
                    t.Commit();
                }}
                own.Pending=null;other.Pending=null;Invalidate(member);member.Entity.ClanId=clan.ID;member.Entity.ClanRank=rank;member.Entity.ClanName=clan.Name;
                if(!clan.Members.TryAdd(member.Entity.UID,new ClanMember{UID=member.Entity.UID,Identifier=member.Entity.UID,Name=member.Entity.Name,Class=member.Entity.Class,Level=member.Entity.Level,Rank=rank}))
                    throw new InvalidOperationException("Clan projection requires reload.");
                lock(clan){clan.Build(member,Clan.Types.Info);member.Send(clan);}
                member.SendScreenSpawn(member.Entity,true);Q.Refresh(member);
                member.Send("You have joined "+clan.Name+".");
            }catch(Exception ex){Q.Fail(first,"clan-membership",ex);Q.Fail(second,"clan-membership",ex);}
            return true;
        }
    }
}
