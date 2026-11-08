using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class ClanLeadership {
        private sealed class Member {internal uint Uid,Donation;internal string Name,Spouse;internal byte Level,Class;internal int Rank;}
        private static Dictionary<uint,Member> Read(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint clan){
            var rows=new Dictionary<uint,Member>();using(var q=Q.Command(c,t,"SELECT uid,name,spouse,clanrank,level,class,COALESCE(clandonation,0) FROM entities WHERE clanid=@clan ORDER BY uid","clan",(long)clan))
            using(var r=q.ExecuteReader())while(r.Read()){var m=new Member{Uid=Convert.ToUInt32(r[0]),Name=Convert.ToString(r[1]),Spouse=Convert.ToString(r[2]),Rank=Convert.ToInt32(r[3]),Level=Convert.ToByte(r[4]),Class=Convert.ToByte(r[5]),Donation=Convert.ToUInt32(r[6])};rows.Add(m.Uid,m);}return rows;
        }
        private static bool Ready(GameState actor,GameState target,Clan clan){Clan live;return Q.Player(actor) && Q.Player(target,false) && actor!=target && clan!=null
            && actor.Entity.ClanId==clan.ID && target.Entity.ClanId==clan.ID && actor.Entity.ClanRank==Clan.Ranks.ClanLeader && clan.LeaderId==actor.Entity.UID
            && Kernel.Clans.TryGetValue(clan.ID,out live) && ReferenceEquals(live,clan);}
        internal static bool Handle(byte[] packet,GameState actor){
            if(packet==null || packet.Length<5)return true;if(packet[4]!=21)return false;
            if(packet.Length<26 || packet.Length>1024 || BitConverter.ToUInt16(packet,0)!=packet.Length-8 || BitConverter.ToUInt16(packet,2)!=1312
                || Encoding.ASCII.GetString(packet,packet.Length-8,8)!="TQClient" || !Q.Player(actor))return true;
            int n=packet[17];if(n<1 || n>16 || 18+n>packet.Length-8)return true;string name=Encoding.Default.GetString(packet,18,n);
            var clan=actor.Entity.GetClan;if(clan==null)return true;var matches=clan.Members.Where(x=>x.Value.Name==name).ToArray();if(matches.Length!=1)return true;
            uint targetUid=matches[0].Key;GameState target;if(!Kernel.GamePool.TryGetValue(targetUid,out target) || !Ready(actor,target,clan))return true;
            var leases=new List<uint>();var locks=new List<object>();var online=new List<GameState>();bool relations=false;
            try{uint[] ids;using(var c=DataHolder.MySqlConnection){c.Open();ids=Read(c,null,clan.ID).Keys.OrderBy(x=>x).ToArray();}
                if(!ids.Contains(actor.Entity.UID) || !ids.Contains(targetUid))return true;
                foreach(uint uid in ids){if(!SessionInstall.TryEnter(uid))return true;leases.Add(uid);}
                foreach(uint uid in ids){GameState member;if(Kernel.GamePool.TryGetValue(uid,out member)){
                    if(member.Entity==null || member.ItemSyncRoot==null || !Monitor.TryEnter(member.ItemSyncRoot))return true;
                    locks.Add(member.ItemSyncRoot);online.Add(member);if(!Q.Player(member,false) || member.Entity.ClanId!=clan.ID)return true;}}
                if(!Monitor.TryEnter(Clan.RelationshipSync))return true;relations=true;
                if(!Ready(actor,target,clan) || target.Entity.Name!=name)return true;
                Dictionary<uint,Member> rows;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    foreach(uint uid in ids)using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid))if(q.ExecuteScalar()==null)return true;
                    using(var q=Q.Command(c,t,"SELECT leaderid FROM clans WHERE identifier=@clan FOR UPDATE","clan",(long)clan.ID))if(Convert.ToUInt32(q.ExecuteScalar())!=actor.Entity.UID)return true;
                    rows=Read(c,t,clan.ID);if(!ids.SequenceEqual(rows.Keys.OrderBy(x=>x)) || rows[actor.Entity.UID].Rank!=100 || rows[targetUid].Name!=name || !Ready(actor,target,clan))return true;
                    if(Q.Execute(c,t,"UPDATE clans SET leaderid=@next,leadername=@name WHERE identifier=@clan AND leaderid=@old",
                        "next",(long)targetUid,"name",name,"clan",(long)clan.ID,"old",(long)actor.Entity.UID)!=1)throw new InvalidOperationException("Clan leader was not saved.");
                    if(Q.Execute(c,t,"UPDATE entities SET clanrank=CASE WHEN uid=@next THEN 100 WHEN spouse=@name THEN 11 ELSE 10 END WHERE clanid=@clan",
                        "next",(long)targetUid,"name",name,"clan",(long)clan.ID)!=ids.Length)throw new InvalidOperationException("Clan ranks were not saved.");
                    if(!Ready(actor,target,clan) || target.Entity.Name!=name)throw new InvalidOperationException("Clan transfer context changed.");t.Commit();
                }}
                clan.LeaderId=targetUid;clan.LeaderName=name;
                foreach(var row in rows.Values){ClanMember member;if(!clan.Members.TryGetValue(row.Uid,out member)){
                    member=new ClanMember{UID=row.Uid,Identifier=row.Uid,Name=row.Name,Class=row.Class,Level=row.Level,Donation=row.Donation};clan.Members.TryAdd(row.Uid,member);}
                    member.Rank=row.Uid==targetUid?Clan.Ranks.ClanLeader:row.Spouse==name?Clan.Ranks.Spouse:Clan.Ranks.Member;}
                foreach(var member in online){ClanMembership.Invalidate(member);ClanDisbanding.Invalidate(member);member.Entity.ClanRank=clan.Members[member.Entity.UID].Rank;
                    member.SendScreenSpawn(member.Entity,true);lock(clan){clan.Build(member,Clan.Types.Info);member.Send(clan);member.Send(new ClanMembers(clan));}}
                actor.Send("You have entrusted "+clan.Name+" to "+name+".");target.Send("You now lead "+clan.Name+".");
            }catch(Exception ex){foreach(var member in online)Q.Fail(member,"clan-leadership",ex);if(!online.Contains(actor))Q.Fail(actor,"clan-leadership",ex);}
            finally{if(relations)Monitor.Exit(Clan.RelationshipSync);for(int i=locks.Count-1;i>=0;i--)Monitor.Exit(locks[i]);for(int i=leases.Count-1;i>=0;i--)SessionInstall.Exit(leases[i]);}
            return true;
        }
    }
}
