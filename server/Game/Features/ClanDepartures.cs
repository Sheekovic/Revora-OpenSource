using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class ClanDepartures {
        private static bool Actor(GameState p,Clan clan,bool kick){Clan live;return Q.Player(p) && clan!=null && p.Entity.ClanId==clan.ID
            && Kernel.Clans.TryGetValue(clan.ID,out live) && ReferenceEquals(live,clan)
            && (kick?p.Entity.ClanRank==Clan.Ranks.ClanLeader && clan.LeaderId==p.Entity.UID:p.Entity.ClanRank!=Clan.Ranks.ClanLeader);}
        internal static bool Handle(byte[] packet,GameState actor){
            if(packet==null || packet.Length<5)return true;byte action=packet[4];if(action!=22 && action!=23)return false;
            if(packet.Length<26 || packet.Length>1024 || BitConverter.ToUInt16(packet,0)!=packet.Length-8 || BitConverter.ToUInt16(packet,2)!=1312
                || Encoding.ASCII.GetString(packet,packet.Length-8,8)!="TQClient" || !Q.Player(actor))return true;
            bool kick=action==22;var clan=actor.Entity.GetClan;if(!Actor(actor,clan,kick))return true;
            uint target=actor.Entity.UID;
            if(kick){int n=packet[17];if(n<1 || n>16 || 18+n>packet.Length-8)return true;
                string name=Encoding.Default.GetString(packet,18,n);var matches=clan.Members.Where(x=>x.Value.Name==name).ToArray();
                if(matches.Length!=1)return true;target=matches[0].Key;if(target==actor.Entity.UID || target==clan.LeaderId)return true;}
            var ids=new[]{actor.Entity.UID,target}.Distinct().OrderBy(x=>x).ToArray();var leases=new List<uint>();var locks=new List<object>();GameState member=null;
            try{
                foreach(uint uid in ids){if(!SessionInstall.TryEnter(uid))return true;leases.Add(uid);}
                Kernel.GamePool.TryGetValue(target,out member);
                if(member!=null && (member.Entity==null || member.ItemSyncRoot==null))return true;
                var online=new[]{actor,member}.Where(p=>p!=null).Distinct().OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in online){if(p.ItemSyncRoot==null || !Monitor.TryEnter(p.ItemSyncRoot))return true;locks.Add(p.ItemSyncRoot);}
                if(!Actor(actor,clan,kick) || member!=null && (!Q.Player(member,false) || member.Entity.ClanId!=clan.ID))return true;
                ClanMember existing;if(!clan.Members.TryGetValue(target,out existing) || existing.Rank==Clan.Ranks.ClanLeader)return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    foreach(uint uid in ids)using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Clan character missing.");
                    using(var q=Q.Command(c,t,"SELECT leaderid FROM clans WHERE identifier=@clan FOR UPDATE","clan",(long)clan.ID))
                        if(Convert.ToUInt32(q.ExecuteScalar())!=clan.LeaderId)return true;
                    if(kick)using(var q=Q.Command(c,t,"SELECT clanid,clanrank FROM entities WHERE uid=@uid","uid",(long)actor.Entity.UID))
                    using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToUInt32(r[0])!=clan.ID || Convert.ToInt32(r[1])!=100)return true;
                    if(!Actor(actor,clan,kick))return true;
                    if(Q.Execute(c,t,"UPDATE entities SET clanid=0,clanrank=0,clandonation=0 WHERE uid=@uid AND clanid=@clan AND clanrank<>100",
                        "uid",(long)target,"clan",(long)clan.ID)!=1)throw new InvalidOperationException("Clan departure preimage changed.");
                    t.Commit();
                }}
                ClanMember removed;clan.Members.TryRemove(target,out removed);
                if(member!=null){ClanMembership.Invalidate(member);member.Entity.ClanId=0;member.Entity.ClanRank=Clan.Ranks.None;member.Entity.ClanName="";member.Entity.ClanSharedBp=0;
                    var response=new byte[90];Writer.WriteUInt16(82,0,response);Writer.WriteUInt16(1312,2,response);response[4]=action;Writer.WriteUInt32(target,8,response);member.Send(response);
                    member.SendScreenSpawn(member.Entity,true);member.Send(kick?"You have been removed from "+clan.Name+".":"You have left "+clan.Name+".");}
                actor.Send(kick?existing.Name+" has been removed from "+clan.Name+".":"Your clan membership has ended.");
            }catch(Exception ex){Q.Fail(actor,"clan-departure",ex);if(member!=null && member!=actor)Q.Fail(member,"clan-departure",ex);}
            finally{for(int n=locks.Count-1;n>=0;n--)Monitor.Exit(locks[n]);for(int n=leases.Count-1;n>=0;n--)SessionInstall.Exit(leases[n]);}
            return true;
        }
    }
}
