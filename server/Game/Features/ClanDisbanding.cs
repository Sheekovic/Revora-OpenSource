using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class ClanDisbanding {
        private sealed class Offer {internal Entity Body;internal Clan Clan;internal DateTime Until;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool Ready(GameState p,Clan clan){Clan live;return Q.Near(p,9450,1002) && p.Map.BaseID==1002 && clan!=null
            && p.Entity.ClanId==clan.ID && p.Entity.ClanRank==Clan.Ranks.ClanLeader && clan.LeaderId==p.Entity.UID
            && Kernel.Clans.TryGetValue(clan.ID,out live) && ReferenceEquals(live,clan);}
        private static bool Current(GameState p,Offer offer){Offer active;return offers.TryGetValue(p,out active) && ReferenceEquals(active,offer)
            && ReferenceEquals(offer.Body,p.Entity) && Clock()<offer.Until && Ready(p,offer.Clan);}
        internal static void Invalidate(GameState p){
            if(!Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Disband invalidation requires its owner lock.");offers.Remove(p);
        }
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || p.Entity==null || p.ItemSyncRoot==null || p.ActiveNpc!=9450 || request==null)return false;
            if(request.OptionID==255){lock(p.ItemSyncRoot)offers.Remove(p);return false;}
            if(request.OptionID!=9 && request.OptionID!=19)return false;
            if(request.NpcID!=0 && request.NpcID!=9450)return true;
            if(request.OptionID==19){Confirm(p);return true;}
            lock(p.ItemSyncRoot){var clan=p.Entity.GetClan;if(!Ready(p,clan))return true;
                offers.Remove(p);offers.Add(p,new Offer{Body=p.Entity,Clan=clan,Until=Clock().AddMinutes(2)});
                var dialog=new Npcs(p);dialog.Text("Dissolving "+clan.Name+" will end the clan for every member. Is this your decision?");
                dialog.Option("Dissolve the clan.",19);dialog.Option("Keep my clan.",255);dialog.Send();}
            return true;
        }
        private static uint[] Members(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint id){var ids=new List<uint>();
            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE clanid=@clan ORDER BY uid","clan",(long)id))
            using(var r=q.ExecuteReader())while(r.Read())ids.Add(Convert.ToUInt32(r[0]));return ids.ToArray();}
        internal static bool Confirm(GameState p){
            Offer offer;if(p==null || p.ItemSyncRoot==null || !offers.TryGetValue(p,out offer) || !ReferenceEquals(offer.Body,p.Entity)
                || Clock()>=offer.Until || !Ready(p,offer.Clan))return false;
            var clan=offer.Clan;var leases=new List<uint>();var locks=new List<object>();var online=new List<GameState>();bool relations=false;
            try{uint[] ids;using(var c=DataHolder.MySqlConnection){c.Open();ids=Members(c,null,clan.ID);}
                if(!ids.Contains(p.Entity.UID))return false;
                foreach(uint uid in ids){if(!SessionInstall.TryEnter(uid))return false;leases.Add(uid);}
                foreach(uint uid in ids){GameState member;if(Kernel.GamePool.TryGetValue(uid,out member)){
                    if(member.Entity==null || member.ItemSyncRoot==null || !Monitor.TryEnter(member.ItemSyncRoot))return false;
                    locks.Add(member.ItemSyncRoot);online.Add(member);if(!Q.Player(member,false) || member.Entity.ClanId!=clan.ID)return false;}}
                if(!Monitor.TryEnter(Clan.RelationshipSync))return false;relations=true;
                if(!Current(p,offer))return false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    foreach(uint uid in ids)using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid))if(q.ExecuteScalar()==null)return false;
                    using(var q=Q.Command(c,t,"SELECT leaderid FROM clans WHERE identifier=@clan FOR UPDATE","clan",(long)clan.ID))if(Convert.ToUInt32(q.ExecuteScalar())!=p.Entity.UID)return false;
                    using(var q=Q.Command(c,t,"SELECT clanrank FROM entities WHERE uid=@uid AND clanid=@clan","uid",(long)p.Entity.UID,"clan",(long)clan.ID))if(Convert.ToInt32(q.ExecuteScalar())!=100)return false;
                    if(!ids.SequenceEqual(Members(c,t,clan.ID)) || !Current(p,offer))return false;
                    ClanFounding.ReserveIds(c,t,clan.ID);
                    if(Q.Execute(c,t,"UPDATE entities SET clanid=0,clanrank=0,clandonation=0 WHERE clanid=@clan","clan",(long)clan.ID)!=ids.Length)
                        throw new InvalidOperationException("Not all clan memberships were cleared.");
                    Q.Execute(c,t,"DELETE FROM clanrelation WHERE clanid=@clan OR associatedid=@clan","clan",(long)clan.ID);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM clanrelation WHERE clanid=@clan OR associatedid=@clan)","clan",(long)clan.ID))if(!Equals(q.ExecuteScalar(),false))throw new InvalidOperationException("Clan links were not cleared.");
                    if(Q.Execute(c,t,"DELETE FROM clans WHERE identifier=@clan AND leaderid=@uid","clan",(long)clan.ID,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Clan was not deleted.");
                    if(!Current(p,offer))throw new InvalidOperationException("Disband confirmation changed.");
                    t.Commit();
                }}
                offers.Remove(p);uint retired=clan.ID;Kernel.Clans.Remove(retired);clan.Members.Clear();clan.Allies.Clear();clan.Enemies.Clear();
                foreach(var peer in Kernel.Clans.Values){peer.Allies.Remove(retired);peer.Enemies.Remove(retired);}
                foreach(var member in online){ClanMembership.Invalidate(member);member.Entity.ClanId=0;member.Entity.ClanRank=Clan.Ranks.None;member.Entity.ClanName="";member.Entity.ClanSharedBp=0;
                    var packet=new byte[90];Writer.WriteUInt16(82,0,packet);Writer.WriteUInt16(1312,2,packet);packet[4]=23;Writer.WriteUInt32(member.Entity.UID,8,packet);member.Send(packet);member.SendScreenSpawn(member.Entity,true);member.Send(clan.Name+" has been dissolved.");}
                Kernel.SendWorldMessage(new Message(p.Entity.Name+" has dissolved "+clan.Name+".",System.Drawing.Color.Yellow,Message.World),Server.GamePool);return true;
            }catch(Exception ex){foreach(var member in online)Q.Fail(member,"clan-disband",ex);if(!online.Contains(p))Q.Fail(p,"clan-disband",ex);return false;}
            finally{if(relations)Monitor.Exit(Clan.RelationshipSync);for(int n=locks.Count-1;n>=0;n--)Monitor.Exit(locks[n]);for(int n=leases.Count-1;n>=0;n--)SessionInstall.Exit(leases[n]);}
        }
    }
}
