using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildLeadership
    {
        private static readonly ConditionalWeakTable<Guild,object> gates=new ConditionalWeakTable<Guild,object>();
        internal static object Gate(Guild guild) {return gates.GetValue(guild,_=>new object());}
        internal static bool TryHandle(byte[] packet,GameState owner) {
            if(packet==null || packet.Length<10)return true;
            uint action=BitConverter.ToUInt32(packet,4);
            if(action!=GuildCommand.PromoteSendRequest && action!=GuildCommand.PromoteAcceptRequest && action!=GuildCommand.ConfirmPromote && action!=GuildCommand.Discharge)return false;
            if(packet.Length<35 || packet.Length>128 || BitConverter.ToUInt16(packet,0)!=packet.Length-8 || BitConverter.ToUInt16(packet,2)!=1107
                || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal || packet[24]!=1 || packet[25]==0
                || packet[25]>16 || 26+packet[25]>packet.Length-8)return true;
            for(int i=26+packet[25];i<packet.Length-8;i++)if(packet[i]!=0)return true;
            if(!Member(owner))return true;
            var name=System.Text.Encoding.Default.GetString(packet,26,packet[25]);Guild.Member target;
            var rank=action==GuildCommand.Discharge?Enums.GuildMemberRank.Member:(Enums.GuildMemberRank)BitConverter.ToUInt16(packet,8);
            if(owner.Guild.GetMember(name,out target)){
                if(rank==Enums.GuildMemberRank.GuildLeader)Transfer(owner,target);else Appoint(owner,target,rank);
            }
            return true;
        }
        private static bool Member(GameState p){
            Guild g;Guild.Member m;
            return Q.Player(p,false) && p.Guild!=null && Kernel.Guilds.TryGetValue(p.Guild.ID,out g) && ReferenceEquals(g,p.Guild)
                && p.Entity.GuildID==g.ID && g.Members.TryGetValue(p.Entity.UID,out m) && ReferenceEquals(m,p.AsMember)
                && m.GuildID==g.ID && m.Name==p.Entity.Name && p.Entity.GuildRank==(ushort)m.Rank;
        }
        internal static int Capacity(Enums.GuildMemberRank rank){
            switch(rank){
                case Enums.GuildMemberRank.DeputyLeader:return 6;
                case Enums.GuildMemberRank.HDeputyLeader:case Enums.GuildMemberRank.HonoraryManager:
                case Enums.GuildMemberRank.HonorarySuperv:case Enums.GuildMemberRank.HonorarySteward:return 4;
                case Enums.GuildMemberRank.Steward:return 3;
                case Enums.GuildMemberRank.Aide:return 6;
                case Enums.GuildMemberRank.Follower:return 10;
                case Enums.GuildMemberRank.Member:return int.MaxValue;
                default:return 0;
            }
        }
        internal static uint Price(Enums.GuildMemberRank rank){
            switch(rank){case Enums.GuildMemberRank.HDeputyLeader:return 650;case Enums.GuildMemberRank.HonoraryManager:return 320;
                case Enums.GuildMemberRank.HonorarySuperv:return 270;case Enums.GuildMemberRank.HonorarySteward:return 100;default:return 0;}
        }
        internal static bool CanAppoint(GameState owner,Enums.GuildMemberRank rank){
            if(!Member(owner) || Capacity(rank)==0)return false;
            if(Leader(owner))return true;
            if(owner.AsMember.Rank==Enums.GuildMemberRank.DeputyLeader)
                return rank==Enums.GuildMemberRank.Steward || rank==Enums.GuildMemberRank.Aide || rank==Enums.GuildMemberRank.Follower;
            return (owner.AsMember.Rank==Enums.GuildMemberRank.Manager || owner.AsMember.Rank==Enums.GuildMemberRank.HonoraryManager) && rank==Enums.GuildMemberRank.Aide;
        }
        internal static void Recount(Guild guild){
            Array.Clear(guild.RanksCounts,0,guild.RanksCounts.Length);
            foreach(var m in guild.Members.Values)if((int)m.Rank<guild.RanksCounts.Length)guild.RanksCounts[(int)m.Rank]++;
        }
        internal static bool Appoint(GameState owner,Guild.Member target,Enums.GuildMemberRank rank){
            if(!CanAppoint(owner,rank) || target==null || target.ID==owner.Entity.UID || !SessionInstall.TryEnter(owner.Entity.UID))return false;
            bool targetLease=false;
            try{
                if(!(targetLease=SessionInstall.TryEnter(target.ID)))return false;
                lock(owner.ItemSyncRoot){
                    var guild=owner.Guild;GameState peer;Kernel.GamePool.TryGetValue(target.ID,out peer);
                    object peerRoot=peer==null?null:peer.ItemSyncRoot;bool held=false;
                    if(peer!=null && (peerRoot==null || !(held=Monitor.TryEnter(peerRoot))))return false;
                    try{
                        if(!Monitor.TryEnter(Gate(guild)))return false;
                        try{
                            Guild.Member current;GameState actual;
                            if(!CanAppoint(owner,rank) || !ReferenceEquals(owner.Guild,guild) || !guild.Members.TryGetValue(target.ID,out current)
                                || !ReferenceEquals(current,target) || target.GuildID!=guild.ID || target.Rank>=owner.AsMember.Rank || target.Rank==rank
                                || owner.Trade==null || owner.Trade.InTrade || owner.Booth!=null
                                || (peer!=null && (!Member(peer) || !ReferenceEquals(peer.Guild,guild) || !ReferenceEquals(peer.AsMember,target))))return false;
                            uint price=Price(rank),points=owner.Entity.ConquerPoints;if(points<price)return false;
                            var old=target.Rank;Guid receipt=Guid.NewGuid();
                            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid IN(@actor,@target) ORDER BY uid FOR UPDATE","actor",(long)owner.Entity.UID,"target",(long)target.ID))
                                using(var r=q.ExecuteReader()){int count=0;while(r.Read())count++;if(count!=2)throw new InvalidOperationException("Appointment character missing.");}
                                using(var q=Q.Command(c,t,"SELECT leadername FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild.ID))
                                    if(!Equals(q.ExecuteScalar(),guild.LeaderName))throw new InvalidOperationException("Appointment guild leader changed.");
                                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank=@rank AND name=@name",
                                    "uid",(long)owner.Entity.UID,"guild",(long)guild.ID,"rank",(int)owner.AsMember.Rank,"name",owner.Entity.Name))
                                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Appointment authority changed.");
                                using(var q=Q.Command(c,t,"SELECT count(*) FROM entities WHERE guildid=@guild AND guildrank=@rank","guild",(long)guild.ID,"rank",(int)rank))
                                    if(Convert.ToInt64(q.ExecuteScalar())>=Capacity(rank))return false;
                                if(Q.Execute(c,t,"UPDATE entities SET guildrank=@rank WHERE uid=@uid AND guildid=@guild AND guildrank=@before AND name=@name",
                                    "rank",(int)rank,"uid",(long)target.ID,"guild",(long)guild.ID,"before",(int)old,"name",target.Name)!=1)
                                    throw new InvalidOperationException("Appointment recipient changed.");
                                if(price>0 && Q.Execute(c,t,"UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND conquerpoints=@before",
                                    "next",(long)(points-price),"uid",(long)owner.Entity.UID,"before",(long)points)!=1)throw new InvalidOperationException("Appointment payment changed.");
                                string key=receipt.ToString("N");
                                if(rank==Enums.GuildMemberRank.DeputyLeader)ProgressionEvidence.Complete(c,t,target.ID,10222,"guild-appointment-v1",key,1);
                                if(rank==Enums.GuildMemberRank.Steward)ProgressionEvidence.Complete(c,t,target.ID,10225,"guild-appointment-v1",key,1);
                                if(price>0)ProgressionEvidence.Complete(c,t,target.ID,10221,"guild-appointment-v1",key,1);
                                Kernel.GamePool.TryGetValue(target.ID,out actual);
                                if(!CanAppoint(owner,rank) || !ReferenceEquals(actual,peer) || !ReferenceEquals(guild.Members[target.ID],target))throw new InvalidOperationException("Appointment session changed.");
                                t.Commit();
                            }}
                            target.Rank=rank;if(price>0)owner.Entity.ApplyCommittedConquerPoints(points-price);
                            if(peer!=null){peer.Entity.GuildRank=(ushort)rank;guild.SendGuild(peer);Q.Refresh(peer);peer.Screen.FullWipe();peer.Screen.Reload(null);}
                            Recount(guild);guild.SendGuild(owner);owner.Send(target.Name+" now serves as "+rank+".");return true;
                        }finally{Monitor.Exit(Gate(guild));}
                    }catch(Exception ex){Q.Fail(owner,"guild-appointment",ex);if(peer!=null)Q.Fail(peer,"guild-appointment",ex);return false;}
                    finally{if(held)Monitor.Exit(peerRoot);}
                }
            }finally{if(targetLease)SessionInstall.Exit(target.ID);SessionInstall.Exit(owner.Entity.UID);}
        }
        internal static bool Leader(GameState p) {
            Guild g;Guild.Member member;
            return Q.Player(p,false) && p.Guild!=null && p.Entity.GuildID==p.Guild.ID
                && Kernel.Guilds.TryGetValue(p.Guild.ID,out g) && ReferenceEquals(g,p.Guild)
                && g.Members.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(member,p.AsMember) && member.GuildID==g.ID
                && member.Rank==Enums.GuildMemberRank.GuildLeader && p.Entity.GuildRank==(ushort)member.Rank
                && g.LeaderName==p.Entity.Name && member.Name==p.Entity.Name;
        }
        internal static bool Transfer(GameState owner,Guild.Member target) {
            if(!Leader(owner) || target==null || target.ID==owner.Entity.UID)return false;
            lock(owner.ItemSyncRoot) {
                var guild=owner.Guild;GameState peer;Kernel.GamePool.TryGetValue(target.ID,out peer);
                object peerRoot=peer==null?null:peer.ItemSyncRoot;bool held=false;
                if(peer!=null && (peerRoot==null || !(held=Monitor.TryEnter(peerRoot))))return false;
                try {lock(Gate(guild)) {
                    Guild.Member current;GameState actual;
                    if(!Leader(owner) || !ReferenceEquals(owner.Guild,guild) || !guild.Members.TryGetValue(target.ID,out current) || !ReferenceEquals(current,target)
                        || target.GuildID!=guild.ID || string.IsNullOrWhiteSpace(target.Name)
                        || (peer!=null && (!Q.Player(peer,false) || !ReferenceEquals(peer.Guild,guild) || !ReferenceEquals(peer.AsMember,target))))return false;
                    var oldMember=owner.AsMember;var oldName=owner.Entity.Name;var newName=target.Name;
                    using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        // Match claim locking: character rows precede the guild row.
                        using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid IN(@old,@next) ORDER BY uid FOR UPDATE","old",(long)owner.Entity.UID,"next",(long)target.ID))
                        using(var r=q.ExecuteReader()){int count=0;while(r.Read())count++;if(count!=2)throw new InvalidOperationException("Guild transfer character missing.");}
                        using(var q=Q.Command(c,t,"SELECT leadername FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild.ID))
                            if(!Equals(q.ExecuteScalar(),oldName))throw new InvalidOperationException("Guild leadership changed.");
                        if(Q.Execute(c,t,"UPDATE entities SET guildrank=@rank WHERE uid=@uid AND guildid=@guild AND name=@name",
                            "rank",(int)Enums.GuildMemberRank.DeputyLeader,"uid",(long)owner.Entity.UID,"guild",(long)guild.ID,"name",oldName)!=1)
                            throw new InvalidOperationException("Former leader ownership changed.");
                        if(Q.Execute(c,t,"UPDATE entities SET guildrank=@rank WHERE uid=@uid AND guildid=@guild AND name=@name",
                            "rank",(int)Enums.GuildMemberRank.GuildLeader,"uid",(long)target.ID,"guild",(long)guild.ID,"name",newName)!=1)
                            throw new InvalidOperationException("New leader membership changed.");
                        if(Q.Execute(c,t,"UPDATE guilds SET leadername=@next WHERE id=@guild AND leadername=@old",
                            "next",newName,"guild",(long)guild.ID,"old",oldName)!=1)throw new InvalidOperationException("Guild leader was not saved.");
                        Kernel.GamePool.TryGetValue(target.ID,out actual);
                        if(!Leader(owner) || !ReferenceEquals(actual,peer) || !ReferenceEquals(guild.Members[target.ID],target))throw new InvalidOperationException("Guild transfer session changed.");
                        t.Commit();
                    }}
                    oldMember.Rank=Enums.GuildMemberRank.DeputyLeader;target.Rank=Enums.GuildMemberRank.GuildLeader;
                    guild.LeaderName=newName;guild.Leader=target;owner.Entity.GuildRank=(ushort)oldMember.Rank;
                    if(peer!=null)peer.Entity.GuildRank=(ushort)target.Rank;
                    // Recount from actual members; repeated packets cannot underflow the old counters.
                    Array.Clear(guild.RanksCounts,0,guild.RanksCounts.Length);
                    foreach(var member in guild.Members.Values)if((int)member.Rank<guild.RanksCounts.Length)guild.RanksCounts[(int)member.Rank]++;
                    guild.SendGuild(owner);if(peer!=null)guild.SendGuild(peer);
                    owner.Send("Your guild now follows "+newName+".");if(peer!=null)peer.Send("Your guild has entrusted you with its banner.");
                    return true;
                }}catch(Exception ex){Q.Fail(owner,"guild-leadership",ex);if(peer!=null)Q.Fail(peer,"guild-leadership",ex);return false;}
                finally{if(held)Monitor.Exit(peerRoot);}
            }
        }
    }
}
