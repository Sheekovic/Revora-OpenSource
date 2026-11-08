using Revora.Database;
using Revora.Network.GamePackets.Union;
using System;
using Revora.Game;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Union
{
    public class UnionTable
    {
        public static List<UnionClass.Member> LoadMembers(uint UnionID)
        {
            List<UnionClass.Member> Members = new List<UnionClass.Member>();
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("UnionID", UnionID))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    var member = new UnionClass.Member(UnionID);
                    member.ID = reader.ReadUInt32("uid");
                    member.Name = reader.ReadString("name");
                    member.Level = reader.ReadByte("Level");
                    if (Revora.Game.ConquerStructures.Nobility.Board.ContainsKey(member.ID))
                    {
                        member.NobilityRank = Revora.Game.ConquerStructures.Nobility.Board[member.ID].Rank;
                    }
                    member.Class = reader.ReadByte("Class");
                    member.UnionExploits = reader.ReadUInt32("UnionExploits");
                    member.Offical = reader.ReadUInt16("Offical");
                    member.Harem = reader.ReadUInt16("Harem");
                    member.Guards = reader.ReadUInt16("Guards");
                    member.GuildID = reader.ReadUInt32("guildid");
                    member.Mesh = uint.Parse(reader.ReadUInt16("Face").ToString() + reader.ReadUInt16("Body").ToString());
                    Members.Add(member);
                }
            }
            return Members;
        }
        public static void Load()
        {
            // Startup only. Materialize and validate the entire snapshot without UPDATEs,
            // broadcasts or a partially published cache when a later row is malformed.
            if(Kernel.Unions.Count!=0)throw new InvalidOperationException("Union startup requires an empty cache.");
            var restored=new Dictionary<uint,UnionClass>();
            using(var cmd=new MySqlCommand(MySqlCommandType.SELECT).Select("Unions"))
            using(var reader=new MySqlReader(cmd))
            {
                while(reader.Read())
                {
                    var union=UnionClass.Restore(reader);
                    foreach(var guild in Kernel.Guilds.Values.Where(g=>g.UnionID==union.ID).OrderBy(g=>g.ID))union.Guilds.Add(guild.ID);
                    restored.Add(union.ID,union);
                }
            }
            if(Kernel.Unions.Count!=0)throw new InvalidOperationException("Union cache changed during startup.");
            Kernel.Unions=restored;
        }
        public static void CreateUnion(UnionClass union)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("Unions").
            Insert("ID", union.ID).
            Insert("GoldBricks", union.GoldBricks).
            Insert("UnionFund", union.UnionFund).
            Insert("Name", union.Name).
            Insert("Title", union.Title).
            Insert("LeaderName", union.LeaderName).
            Insert("LeaderUID", union.LeaderUID).
            Insert("Bulletin", "").
            Insert("Declaration", ""))
                cmd.Execute();
        }
        public static void UpdateHarem(UnionClass union)
        {
            var builder = new StringBuilder();
            builder.Append(union.Harem[0] + "#" + union.Harem[1] + "#" + union.Harem[2] + "#" + union.Harem[3] + "#" + union.Harem[4] + "#" + union.Harem[5] + "#" + union.Harem[6] + "#" + union.Harem[7] + "#" + union.Harem[8] + "#" + union.Harem[9]);
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("Unions")
            .Set("Harem", builder.ToString()).Where("ID", union.ID))
                cmd.Execute();
        }
        public static void UpdateCoreOfficals(UnionClass union)
        {
            var builder = new StringBuilder();
            builder.Append(union.Coreofficals[0] + "#" + union.Coreofficals[1] + "#" + union.Coreofficals[2] + "#" + union.Coreofficals[3] + "#" + union.Coreofficals[4] + "#" + union.Coreofficals[5] + "#" + union.Coreofficals[6] + "#" + union.Coreofficals[7]);
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("Unions")
            .Set("CoreOfficals", builder.ToString()).Where("ID", union.ID))
                cmd.Execute();
        }
        public static void UpdateGuards(UnionClass union)
        {
            var builder = new StringBuilder();
            builder.Append(union.Guards[0] + "#" + union.Guards[1] + "#" + union.Guards[2] + "#" + union.Guards[3] + "#" + union.Guards[4] + "#" + union.Guards[5] + "#" + union.Guards[6] + "#" + union.Guards[7] + "#" + union.Guards[8] + "#" + union.Guards[9] + "#" + union.Guards[10] + "#" + union.Guards[11] + "#" + union.Guards[12] + "#" + union.Guards[13] + "#" + union.Guards[14] + "#" + union.Guards[15] + "#" + union.Guards[16] + "#" + union.Guards[17] + "#" + union.Guards[18] + "#" + union.Guards[19]);
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("Unions")
            .Set("Guards", builder.ToString()).Where("ID", union.ID))
                cmd.Execute();
        }
        public static void UpdateLeader(UnionClass union)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("Unions")
             .Set("LeaderName", union.LeaderName).Set("LeaderUID", union.LeaderUID).Where("ID", union.ID))
                cmd.Execute();
        }
        public static void UpdateData(uint UID, string column, object value)
        {
            if (value is Boolean)
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("entities").Set(column, (Boolean)value).Where("UID", UID)
                        .Execute();
            }
            else
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("entities").Set(column, value.ToString()).Where("UID", UID)
                        .Execute();
            }
        }
    }
}
namespace Revora.Game.Features
{
    using Q=QuestInventory;
    using Player=Revora.Client.GameState;
    using Guild=Revora.Game.ConquerStructures.Society.Guild;
    using U=Revora.Network.GamePackets.Union.UnionClass;
    using Monitor=System.Threading.Monitor;
    internal static class UnionFormation
    {
        internal static readonly object Sync=new object();
        internal static Action BeforeProjection=null;
        private static bool Eligible(Player p,Guild guild) {
            Guild current;Guild.Member member;
            return Q.Player(p) && guild!=null && ReferenceEquals(p.Guild,guild) && p.Union==null && p.UnionID==0 && guild.UnionID==0
                && p.Entity.GuildID==guild.ID && p.Entity.GuildRank==(ushort)Enums.GuildMemberRank.GuildLeader
                && Kernel.Guilds.TryGetValue(guild.ID,out current) && ReferenceEquals(current,guild)
                && guild.Members.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(member,p.AsMember) && ReferenceEquals(guild.Leader,member)
                && member.Rank==Enums.GuildMemberRank.GuildLeader && member.GuildID==guild.ID && guild.LeaderName==p.Entity.Name
                && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;
        }
        internal static void Create(Player p,byte[] packet) {
            if(!Q.Player(p) || packet==null || packet.Length<28 || packet[19]==0 || 20+packet[19]>packet.Length-8)return;
            string name=Encoding.Default.GetString(packet,20,packet[19]);
            if(string.IsNullOrWhiteSpace(name) || name!=name.Trim() || name.Any(char.IsControl))return;
            var guild=p.Guild;if(!Eligible(p,guild) || !SessionInstall.TryEnter(p.Entity.UID))return;
            bool owner=false,group=false,global=false;
            try {
                if(!(owner=Monitor.TryEnter(p.ItemSyncRoot)) || !(group=Monitor.TryEnter(GuildLeadership.Gate(guild)))
                    || !(global=Monitor.TryEnter(Sync)) || !Eligible(p,guild))return;
                U formed;
                using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s';SELECT pg_advisory_xact_lock(6711,3998)");
                    uint counter;
                    using(var q=Q.Command(c,t,"SELECT unionid FROM configuration FOR UPDATE"))using(var r=q.ExecuteReader()) {
                        if(!r.Read())throw new InvalidOperationException("Union allocator is missing.");counter=Convert.ToUInt32(r[0]);
                        if(r.Read())throw new InvalidOperationException("Union allocator is ambiguous.");
                    }
                    using(var q=Q.Command(c,t,"SELECT name,guildid,guildrank,COALESCE(unionid,0) FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()) {
                        if(!r.Read() || r.GetString(0)!=p.Entity.Name || Convert.ToUInt32(r[1])!=guild.ID || Convert.ToUInt16(r[2])!=1000 || Convert.ToUInt32(r[3])!=0)return;
                    }
                    using(var q=Q.Command(c,t,"SELECT leadername,unionid FROM guilds WHERE id=@id FOR UPDATE","id",(long)guild.ID))using(var r=q.ExecuteReader()) {
                        if(!r.Read() || r.GetString(0)!=p.Entity.Name || (!r.IsDBNull(1) && Convert.ToUInt32(r[1])!=0))return;
                    }
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM unions WHERE name=@name)","name",name))if(Equals(q.ExecuteScalar(),true)){p.Send("That union name is already taken.");return;}
                    uint maximum;using(var q=Q.Command(c,t,"SELECT COALESCE(MAX(id),0) FROM unions"))maximum=Convert.ToUInt32(q.ExecuteScalar());
                    uint id=checked(Math.Max(counter,maximum)+1);if(Kernel.Unions.ContainsKey(id))throw new InvalidOperationException("Union identity conflicts with the loaded cache.");
                    formed=U.Formed(id,name,p.Entity.UID,p.Entity.Name,guild.ID);
                    if(Q.Execute(c,t,@"INSERT INTO unions(id,name,leaderuid,leadername,goldbricks,unionfund,title,bulletin,declaration,coreofficals,harem,guards)
VALUES(@id,@name,@uid,@leader,0,0,'','','',@core,@harem,@guards)","id",(long)id,"name",name,"uid",(long)p.Entity.UID,"leader",p.Entity.Name,
                        "core",string.Join("#",formed.Coreofficals),"harem",string.Join("#",formed.Harem),"guards",string.Join("#",formed.Guards))!=1
                        || Q.Execute(c,t,"UPDATE entities SET unionid=@id,offical=1,harem=0,guards=0 WHERE uid=@uid AND COALESCE(unionid,0)=0 AND guildid=@guild AND guildrank=1000","id",(long)id,"uid",(long)p.Entity.UID,"guild",(long)guild.ID)!=1
                        || Q.Execute(c,t,"UPDATE guilds SET unionid=@id WHERE id=@guild AND COALESCE(unionid,0)=0 AND leadername=@leader","id",(long)id,"guild",(long)guild.ID,"leader",p.Entity.Name)!=1
                        || Q.Execute(c,t,"UPDATE configuration SET unionid=@id WHERE unionid=@before","id",(long)id,"before",(long)counter)!=1)
                        throw new InvalidOperationException("Union formation preimage changed.");
                    if(!Eligible(p,guild))throw new InvalidOperationException("Union founder ownership changed.");t.Commit();
                }}
                // Publish a new dictionary; concurrent readers keep a stable prior snapshot.
                var next=new Dictionary<uint,U>(Kernel.Unions);next.Add(formed.ID,formed);Kernel.Unions=next;
                guild.UnionID=formed.ID;if(BeforeProjection!=null)BeforeProjection();p.CommitUnionFormation(formed);
            }catch(Exception ex){Q.Fail(p,"union-formation",ex);}
            finally {
                if(global)Monitor.Exit(Sync);if(group)Monitor.Exit(GuildLeadership.Gate(guild));if(owner)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);
            }
        }
    }
}

namespace Revora.Game.Features
{
    using Q=QuestInventory;
    using Player=Revora.Client.GameState;
    using U=Revora.Network.GamePackets.Union.UnionClass;
    using Monitor=System.Threading.Monitor;
    internal static class UnionMembership
    {
        internal static Action BeforeProjection=null;
        private static bool Eligible(Player p,U union,bool join) {
            U current;
            return Q.Player(p,false) && union!=null && Kernel.Unions.TryGetValue(union.ID,out current) && ReferenceEquals(union,current)
                && p.Trade!=null && !p.Trade.InTrade && p.Booth==null
                && (join?p.Union==null && p.UnionID==0 && p.Guild==null && p.Entity.GuildID==0 && DateTime.Now>=p.Entity.PledgeStamp.AddMinutes(1)
                    :ReferenceEquals(p.Union,union) && p.UnionID==union.ID && !union.IsLeader(p.Entity.UID));
        }
        internal static void Change(Player p,uint id,bool join) {
            U union;if(!Q.Player(p,false) || !Kernel.Unions.TryGetValue(id,out union))return;
            if(!join && ReferenceEquals(p.Union,union) && union.IsLeader(p.Entity.UID)){p.Send("Pass leadership to another member before leaving.");return;}
            if(join && DateTime.Now<p.Entity.PledgeStamp.AddMinutes(1)){p.Send("Wait a minute after leaving before pledging to another union.");return;}
            if(!Eligible(p,union,join) || !SessionInstall.TryEnter(p.Entity.UID))return;
            bool owner=false,gate=false;
            try {
                if(!(owner=Monitor.TryEnter(p.ItemSyncRoot)) || !(gate=Monitor.TryEnter(UnionFormation.Sync)) || !Eligible(p,union,join))return;
                uint[] core,harem,guards;
                using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s';SELECT pg_advisory_xact_lock(6711,3998)");
                    using(var q=Q.Command(c,t,"SELECT name,COALESCE(unionid,0),COALESCE(guildid,0),COALESCE(offical,0),COALESCE(harem,0),COALESCE(guards,0) FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()) {
                        if(!r.Read() || r.GetString(0)!=p.Entity.Name || Convert.ToUInt32(r[1])!=(join?0:union.ID) || Convert.ToUInt32(r[2])!=p.Entity.GuildID
                            || Convert.ToUInt16(r[3])!=p.Offical || Convert.ToUInt16(r[4])!=p.Harem || Convert.ToUInt16(r[5])!=p.Guards)return;
                    }
                    using(var q=Q.Command(c,t,"SELECT leaderuid,coreofficals,harem,guards FROM unions WHERE id=@id FOR UPDATE","id",(long)union.ID))using(var r=q.ExecuteReader()) {
                        if(!r.Read() || Convert.ToUInt32(r[0])!=union.LeaderUID || Convert.ToUInt32(r[0])==p.Entity.UID)return;
                        core=U.RestoreOffices(r.GetString(1),8,true);harem=U.RestoreOffices(r.GetString(2),10,false);guards=U.RestoreOffices(r.GetString(3),20,false);
                    }
                    if(!core.SequenceEqual(union.Coreofficals) || !harem.SequenceEqual(union.Harem) || !guards.SequenceEqual(union.Guards))throw new InvalidOperationException("Union office cache is stale.");
                    foreach(var offices in new[]{core,harem,guards})for(int i=0;i<offices.Length;i++)if(offices[i]==p.Entity.UID)offices[i]=0;
                    if(Q.Execute(c,t,"UPDATE unions SET coreofficals=@core,harem=@harem,guards=@guards WHERE id=@id","core",string.Join("#",core),"harem",string.Join("#",harem),"guards",string.Join("#",guards),"id",(long)union.ID)!=1
                        || Q.Execute(c,t,"UPDATE entities SET unionid=@next,offical=0,harem=0,guards=0 WHERE uid=@uid AND COALESCE(unionid,0)=@before","next",join?(long)union.ID:0L,"uid",(long)p.Entity.UID,"before",join?0L:(long)union.ID)!=1)
                        throw new InvalidOperationException("Union membership was not saved completely.");
                    if(!Eligible(p,union,join))throw new InvalidOperationException("Union membership ownership changed.");t.Commit();
                }}
                union.Coreofficals=core;union.Harem=harem;union.Guards=guards;
                if(BeforeProjection!=null)BeforeProjection();p.CommitUnionMembership(join?union:null);
                if(!join)p.Entity.PledgeStamp=DateTime.Now;
                Revora.TitleRequirements.Reconcile(p,false);
                if(!join)p.Send("You have left the union.");
            }catch(Exception ex){Q.Fail(p,"union-membership",ex);}
            finally {if(gate)Monitor.Exit(UnionFormation.Sync);if(owner)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
    }
    internal static class GuildAdmission
    {
        internal static Action BeforeProjection=null;
        private static bool Eligible(Player p,Revora.Game.ConquerStructures.Society.Guild guild) {
            Revora.Game.ConquerStructures.Society.Guild current;U union;
            return Q.Player(p,false) && guild!=null && guild.ID>0 && guild.ID<=ushort.MaxValue
                && Kernel.Guilds.TryGetValue(guild.ID,out current) && ReferenceEquals(current,guild)
                && p.Guild==null && p.AsMember==null && p.Entity.GuildID==0 && !guild.Members.ContainsKey(p.Entity.UID) && !guild.BlackList.Contains(p.Entity.UID)
                && p.Trade!=null && !p.Trade.InTrade && p.Booth==null
                && (p.UnionID==0?p.Union==null:Kernel.Unions.TryGetValue(p.UnionID,out union) && ReferenceEquals(p.Union,union))
                && (p.UnionID!=0 || guild.UnionID==0 || DateTime.Now>=p.Entity.PledgeStamp.AddMinutes(1))
                && (guild.UnionID==0 || Kernel.Unions.ContainsKey(guild.UnionID));
        }
        internal static void Join(Player p,Revora.Game.ConquerStructures.Society.Guild guild) {
            if(!Eligible(p,guild))return;
            if(p.UnionID!=0 && p.UnionID!=guild.UnionID){p.Send("Leave your current union before joining this guild.");return;}
            if(!SessionInstall.TryEnter(p.Entity.UID))return;bool owner=false,group=false,gate=false;
            try {
                if(!(owner=Monitor.TryEnter(p.ItemSyncRoot)) || !(group=Monitor.TryEnter(GuildLeadership.Gate(guild))) || !(gate=Monitor.TryEnter(UnionFormation.Sync))
                    || !Eligible(p,guild) || p.UnionID!=0 && p.UnionID!=guild.UnionID || !Revora.Network.GamePackets.Guild_Advertise.PassJoinRequirements(p,guild))return;
                uint affiliation=guild.UnionID;bool newUnion=p.UnionID!=affiliation;U realm=affiliation==0?null:Kernel.Unions[affiliation];uint[] core=null,harem=null,guards=null;
                var member=new Revora.Game.ConquerStructures.Society.Guild.Member(guild.ID){ID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=Enums.GuildMemberRank.Member};
                if(Revora.Game.ConquerStructures.Nobility.Board.ContainsKey(p.Entity.UID)) {
                    member.Gender=Revora.Game.ConquerStructures.Nobility.Board[p.Entity.UID].Gender;member.NobilityRank=Revora.Game.ConquerStructures.Nobility.Board[p.Entity.UID].Rank;
                }
                using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s';SELECT pg_advisory_xact_lock(6711,3998)");
                    using(var q=Q.Command(c,t,"SELECT name,COALESCE(guildid,0),COALESCE(guildrank,0),COALESCE(unionid,0),COALESCE(offical,0),COALESCE(harem,0),COALESCE(guards,0) FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()) {
                        if(!r.Read() || r.GetString(0)!=p.Entity.Name || Convert.ToUInt32(r[1])!=0 || Convert.ToUInt16(r[2])!=p.Entity.GuildRank || Convert.ToUInt32(r[3])!=p.UnionID
                            || Convert.ToUInt16(r[4])!=p.Offical || Convert.ToUInt16(r[5])!=p.Harem || Convert.ToUInt16(r[6])!=p.Guards)return;
                    }
                    using(var q=Q.Command(c,t,"SELECT leadername,COALESCE(unionid,0) FROM guilds WHERE id=@id FOR UPDATE","id",(long)guild.ID))using(var r=q.ExecuteReader())
                        if(!r.Read() || r.GetString(0)!=guild.LeaderName || Convert.ToUInt32(r[1])!=affiliation)return;
                    if(realm!=null) {
                        using(var q=Q.Command(c,t,"SELECT leaderuid,coreofficals,harem,guards FROM unions WHERE id=@id FOR UPDATE","id",(long)affiliation))using(var r=q.ExecuteReader()) {
                            if(!r.Read() || Convert.ToUInt32(r[0])!=realm.LeaderUID || newUnion && Convert.ToUInt32(r[0])==p.Entity.UID)return;
                            core=U.RestoreOffices(r.GetString(1),8,true);harem=U.RestoreOffices(r.GetString(2),10,false);guards=U.RestoreOffices(r.GetString(3),20,false);
                        }
                        if(!core.SequenceEqual(realm.Coreofficals) || !harem.SequenceEqual(realm.Harem) || !guards.SequenceEqual(realm.Guards))throw new InvalidOperationException("Union office cache is stale.");
                        if(newUnion) {
                            foreach(var offices in new[]{core,harem,guards})for(int i=0;i<offices.Length;i++)if(offices[i]==p.Entity.UID)offices[i]=0;
                            if(Q.Execute(c,t,"UPDATE unions SET coreofficals=@core,harem=@harem,guards=@guards WHERE id=@id","core",string.Join("#",core),"harem",string.Join("#",harem),"guards",string.Join("#",guards),"id",(long)affiliation)!=1)throw new InvalidOperationException("Guild admission office cleanup was not saved.");
                        }
                    }
                    if(Q.Execute(c,t,"UPDATE entities SET guildid=@guild,guildrank=@rank,unionid=@union,offical=@office,harem=@harem,guards=@guards WHERE uid=@uid AND COALESCE(guildid,0)=0 AND COALESCE(unionid,0)=@before",
                        "guild",(long)guild.ID,"rank",(int)member.Rank,"union",(long)affiliation,"office",newUnion?0:(int)p.Offical,"harem",newUnion?0:(int)p.Harem,"guards",newUnion?0:(int)p.Guards,
                        "uid",(long)p.Entity.UID,"before",(long)p.UnionID)!=1)throw new InvalidOperationException("Guild admission was not saved.");
                    if(!Eligible(p,guild) || guild.UnionID!=affiliation)throw new InvalidOperationException("Guild admission ownership changed.");
                    WayOfHeroesClaims.RecordGuildMembership(c,t,p.Entity.UID,guild.ID,false);t.Commit();
                }}
                guild.Members.Add(member.ID,member);guild.MemberCount=(uint)guild.Members.Count;
                if(newUnion){realm.Coreofficals=core;realm.Harem=harem;realm.Guards=guards;}
                if(BeforeProjection!=null)BeforeProjection();p.CommitGuildAdmission(guild,member);
                if(newUnion)p.CommitUnionMembership(realm);Revora.TitleRequirements.Reconcile(p,false);
                guild.SendGuildMessage(new Revora.Network.GamePackets.Message(p.Entity.Name+" has joined your guild.",System.Drawing.Color.Black,Revora.Network.GamePackets.Message.Guild));
                var minimum=new Revora.Network.GamePackets.GuildMinDonations(31);minimum.AprendGuild(guild);p.Send(minimum.ToArray());
            }catch(Exception ex){Q.Fail(p,"guild-admission",ex);}
            finally {if(gate)Monitor.Exit(UnionFormation.Sync);if(group)Monitor.Exit(GuildLeadership.Gate(guild));if(owner)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
    }
    internal static class UnionGuildMembership
    {
        internal static Action BeforeProjection=null;
        private static bool Authority(Player p,Revora.Game.ConquerStructures.Society.Guild g,U union,bool join,bool expel) {
            Revora.Game.ConquerStructures.Society.Guild current;U realm;
            if(!Q.Player(p,false) || !Kernel.Guilds.TryGetValue(g.ID,out current) || !ReferenceEquals(g,current)
                || !Kernel.Unions.TryGetValue(union.ID,out realm) || !ReferenceEquals(union,realm)
                || g.UnionID!=(join?0:union.ID) || p.Trade==null || p.Trade.InTrade || p.Booth!=null)return false;
            if(expel)return !join && ReferenceEquals(p.Union,union) && p.UnionID==union.ID && p.Offical==1 && union.IsLeader(p.Entity.UID);
            return ReferenceEquals(p.Guild,g) && ReferenceEquals(p.AsMember,g.Leader) && p.Entity.GuildID==g.ID
                && p.Entity.GuildRank==1000 && g.Leader!=null && g.Leader.ID==p.Entity.UID && g.LeaderName==p.Entity.Name
                && (join?p.Union==null && p.UnionID==0 && DateTime.Now>=p.Entity.PledgeStamp.AddMinutes(1):ReferenceEquals(p.Union,union) && p.UnionID==union.ID);
        }
        internal static void Change(Player actor,uint guildId,uint unionId,bool join,bool expel) {
            Revora.Game.ConquerStructures.Society.Guild guild;U union;
            if(!Q.Player(actor,false) || !Kernel.Guilds.TryGetValue(guildId,out guild) || !Kernel.Unions.TryGetValue(unionId,out union) || !Authority(actor,guild,union,join,expel))return;
            var members=guild.Members.Values.OrderBy(m=>m.ID).ToArray();if(members.Length==0)return;
            var byId=members.ToDictionary(m=>m.ID);
            var memberIds=members.Select(m=>m.ID).ToArray();var ids=memberIds.Concat(new[]{actor.Entity.UID}).Distinct().OrderBy(x=>x).ToArray();
            var leases=new List<uint>();var locks=new List<object>();var players=new Dictionary<uint,Player>();bool group=false,gate=false;
            Func<bool> current=()=> {
                if(!Authority(actor,guild,union,join,expel) || !guild.Members.Keys.OrderBy(x=>x).SequenceEqual(memberIds))return false;
                foreach(var m in members) {
                    Revora.Game.ConquerStructures.Society.Guild.Member saved;
                    if(!guild.Members.TryGetValue(m.ID,out saved) || !ReferenceEquals(m,saved) || m.GuildID!=guild.ID)return false;
                }
                foreach(var id in ids) {Player p;Kernel.GamePool.TryGetValue(id,out p);if(!ReferenceEquals(p,players[id]) || p!=null && !Q.Player(p,false))return false;}
                return true;
            };
            try {
                foreach(var id in ids) {
                    if(!SessionInstall.TryEnter(id))return;leases.Add(id);Player p;Kernel.GamePool.TryGetValue(id,out p);players[id]=p;
                    if(p!=null){if(p.ItemSyncRoot==null || !Monitor.TryEnter(p.ItemSyncRoot))return;locks.Add(p.ItemSyncRoot);}
                }
                if(!(group=Monitor.TryEnter(GuildLeadership.Gate(guild))) || !(gate=Monitor.TryEnter(UnionFormation.Sync)) || !current())return;
                var changed=new List<uint>();uint[] core,harem,guards;
                using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s';SELECT pg_advisory_xact_lock(6711,3998)");
                    using(var q=Q.Command(c,t,"SELECT uid,name,COALESCE(guildid,0),COALESCE(guildrank,0),COALESCE(unionid,0),COALESCE(offical,0),COALESCE(harem,0),COALESCE(guards,0) FROM entities WHERE guildid=@guild OR uid=@actor ORDER BY uid FOR UPDATE","guild",(long)guild.ID,"actor",(long)actor.Entity.UID))using(var r=q.ExecuteReader()) {
                        int index=0;while(r.Read()) {
                            if(index>=ids.Length || Convert.ToUInt32(r[0])!=ids[index++])return;uint id=Convert.ToUInt32(r[0]);var p=players[id];
                            uint before=Convert.ToUInt32(r[4]);ushort official=Convert.ToUInt16(r[5]),h=Convert.ToUInt16(r[6]),guard=Convert.ToUInt16(r[7]);
                            if(p!=null && (r.GetString(1)!=p.Entity.Name || Convert.ToUInt32(r[2])!=p.Entity.GuildID || Convert.ToUInt16(r[3])!=p.Entity.GuildRank
                                || before!=p.UnionID || official!=p.Offical || h!=p.Harem || guard!=p.Guards))return;
                            Revora.Game.ConquerStructures.Society.Guild.Member member;byId.TryGetValue(id,out member);
                            if(member!=null) {
                                if(r.GetString(1)!=member.Name || Convert.ToUInt32(r[2])!=guild.ID || Convert.ToUInt16(r[3])!=(ushort)member.Rank
                                    || p!=null && (!ReferenceEquals(p.Guild,guild) || !ReferenceEquals(p.AsMember,member)))return;
                                if(join && before!=0 && before!=union.ID)return;
                                if(join?before==0:before==union.ID)changed.Add(id);
                            }
                            if(id==actor.Entity.UID && (expel?before!=union.ID || official!=1:Convert.ToUInt32(r[2])!=guild.ID || Convert.ToUInt16(r[3])!=1000))return;
                        }
                        if(index!=ids.Length)return;
                    }
                    using(var q=Q.Command(c,t,"SELECT leadername,COALESCE(unionid,0) FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild.ID))using(var r=q.ExecuteReader())
                        if(!r.Read() || r.GetString(0)!=guild.LeaderName || Convert.ToUInt32(r[1])!=(join?0:union.ID))return;
                    using(var q=Q.Command(c,t,"SELECT leaderuid,leadername,coreofficals,harem,guards FROM unions WHERE id=@id FOR UPDATE","id",(long)union.ID))using(var r=q.ExecuteReader()) {
                        if(!r.Read() || Convert.ToUInt32(r[0])!=union.LeaderUID || expel && (Convert.ToUInt32(r[0])!=actor.Entity.UID || r.GetString(1)!=actor.Entity.Name))return;
                        if(!join && memberIds.Contains(Convert.ToUInt32(r[0]))){actor.Send("Transfer union leadership outside this guild before removing it.");return;}
                        core=U.RestoreOffices(r.GetString(2),8,true);harem=U.RestoreOffices(r.GetString(3),10,false);guards=U.RestoreOffices(r.GetString(4),20,false);
                    }
                    if(!core.SequenceEqual(union.Coreofficals) || !harem.SequenceEqual(union.Harem) || !guards.SequenceEqual(union.Guards))throw new InvalidOperationException("Union office cache is stale.");
                    foreach(var offices in new[]{core,harem,guards})for(int i=0;i<offices.Length;i++)if(changed.Contains(offices[i]))offices[i]=0;
                    if(Q.Execute(c,t,"UPDATE unions SET coreofficals=@core,harem=@harem,guards=@guards WHERE id=@id","core",string.Join("#",core),"harem",string.Join("#",harem),"guards",string.Join("#",guards),"id",(long)union.ID)!=1)throw new InvalidOperationException("Guild union offices were not saved.");
                    if(changed.Count>0 && Q.Execute(c,t,"UPDATE entities SET unionid=@next,offical=0,harem=0,guards=0 WHERE uid=ANY(@uids) AND guildid=@guild AND COALESCE(unionid,0)=@before","next",join?(long)union.ID:0L,"uids",changed.Select(id=>(long)id).ToArray(),"guild",(long)guild.ID,"before",join?0L:(long)union.ID)!=changed.Count)throw new InvalidOperationException("Guild member union preimage changed.");
                    if(Q.Execute(c,t,"UPDATE guilds SET unionid=@next WHERE id=@guild AND COALESCE(unionid,0)=@before","next",join?(long)union.ID:0L,"guild",(long)guild.ID,"before",join?0L:(long)union.ID)!=1)throw new InvalidOperationException("Guild union link was not saved.");
                    if(!current())throw new InvalidOperationException("Guild union ownership changed.");t.Commit();
                }}
                union.Coreofficals=core;union.Harem=harem;union.Guards=guards;guild.UnionID=join?union.ID:0;
                var guilds=union.Guilds.Where(id=>id!=guild.ID).ToList();if(join)guilds.Add(guild.ID);union.Guilds=guilds;
                if(BeforeProjection!=null)BeforeProjection();
                foreach(var id in changed){var p=players[id];if(p!=null){p.CommitUnionMembership(join?union:null);Revora.TitleRequirements.Reconcile(p,false);}}
                if(!join && !expel)actor.Entity.PledgeStamp=DateTime.Now;
                actor.Send(join?"Your guild has pledged to the union.":"The guild has left the union.");
            }catch(Exception ex){foreach(var p in players.Values.Where(p=>p!=null && Q.Player(p,false)))Q.Fail(p,"union-guild-membership",ex);}
            finally {
                if(gate)Monitor.Exit(UnionFormation.Sync);if(group)Monitor.Exit(GuildLeadership.Gate(guild));
                for(int i=locks.Count-1;i>=0;i--)Monitor.Exit(locks[i]);for(int i=leases.Count-1;i>=0;i--)SessionInstall.Exit(leases[i]);
            }
        }
    }
    internal static class UnionExpulsion
    {
        internal static Action BeforeProjection=null;
        private static bool Leader(Player owner,U union) {
            U current;
            return Q.Player(owner,false) && union!=null && owner.UnionID==union.ID && ReferenceEquals(owner.Union,union)
                && Kernel.Unions.TryGetValue(union.ID,out current) && ReferenceEquals(current,union) && union.IsLeader(owner.Entity.UID)
                && owner.Offical==1 && owner.Trade!=null && !owner.Trade.InTrade && owner.Booth==null;
        }
        private static bool Target(Player peer,uint uid,U union) {
            Player current;
            return peer==null?!Kernel.GamePool.TryGetValue(uid,out current):Q.Player(peer,false) && peer.Entity.UID==uid && ReferenceEquals(peer.Union,union) && peer.UnionID==union.ID;
        }
        internal static void Remove(Player owner,uint uid) {
            if(!Q.Player(owner,false) || uid==0 || uid==owner.Entity.UID || !Leader(owner,owner.Union) || !SessionInstall.TryEnter(owner.Entity.UID))return;
            bool targetLease=false,firstHeld=false,secondHeld=false,gate=false;object first=null,second=null;Player peer=null;
            try {
                if(!(targetLease=SessionInstall.TryEnter(uid)))return;
                Kernel.GamePool.TryGetValue(uid,out peer);
                first=peer==null || owner.Entity.UID<uid?owner.ItemSyncRoot:peer.ItemSyncRoot;
                second=peer==null?null:owner.Entity.UID<uid?peer.ItemSyncRoot:owner.ItemSyncRoot;
                if(first==null || !(firstHeld=Monitor.TryEnter(first)) || peer!=null && (second==null || !(secondHeld=Monitor.TryEnter(second)))
                    || !(gate=Monitor.TryEnter(UnionFormation.Sync)))return;
                var union=owner.Union;if(!Leader(owner,union) || !Target(peer,uid,union))return;
                uint[] core,harem,guards;
                using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s';SELECT pg_advisory_xact_lock(6711,3998)");
                    using(var q=Q.Command(c,t,"SELECT uid,name,COALESCE(unionid,0),COALESCE(offical,0),COALESCE(harem,0),COALESCE(guards,0) FROM entities WHERE uid IN(@owner,@target) ORDER BY uid FOR UPDATE","owner",(long)owner.Entity.UID,"target",(long)uid))using(var r=q.ExecuteReader()) {
                        int rows=0;while(r.Read()) {
                            rows++;bool ruler=Convert.ToUInt32(r[0])==owner.Entity.UID;
                            if(Convert.ToUInt32(r[2])!=union.ID || ruler && (r.GetString(1)!=owner.Entity.Name || Convert.ToUInt16(r[3])!=1)
                                || !ruler && Convert.ToUInt16(r[3])==1)return;
                            if(!ruler && peer!=null && (r.GetString(1)!=peer.Entity.Name || Convert.ToUInt16(r[3])!=peer.Offical
                                || Convert.ToUInt16(r[4])!=peer.Harem || Convert.ToUInt16(r[5])!=peer.Guards))return;
                        }
                        if(rows!=2)return;
                    }
                    using(var q=Q.Command(c,t,"SELECT leaderuid,leadername,coreofficals,harem,guards FROM unions WHERE id=@id FOR UPDATE","id",(long)union.ID))using(var r=q.ExecuteReader()) {
                        if(!r.Read() || Convert.ToUInt32(r[0])!=owner.Entity.UID || r.GetString(1)!=owner.Entity.Name)return;
                        core=U.RestoreOffices(r.GetString(2),8,true);harem=U.RestoreOffices(r.GetString(3),10,false);guards=U.RestoreOffices(r.GetString(4),20,false);
                    }
                    if(!core.SequenceEqual(union.Coreofficals) || !harem.SequenceEqual(union.Harem) || !guards.SequenceEqual(union.Guards))throw new InvalidOperationException("Union office cache is stale.");
                    foreach(var offices in new[]{core,harem,guards})for(int i=0;i<offices.Length;i++)if(offices[i]==uid)offices[i]=0;
                    if(Q.Execute(c,t,"UPDATE unions SET coreofficals=@core,harem=@harem,guards=@guards WHERE id=@id AND leaderuid=@owner","core",string.Join("#",core),"harem",string.Join("#",harem),"guards",string.Join("#",guards),"id",(long)union.ID,"owner",(long)owner.Entity.UID)!=1
                        || Q.Execute(c,t,"UPDATE entities SET unionid=0,offical=0,harem=0,guards=0 WHERE uid=@uid AND unionid=@union AND COALESCE(offical,0)<>1","uid",(long)uid,"union",(long)union.ID)!=1)
                        throw new InvalidOperationException("Union expulsion was not saved completely.");
                    if(!Leader(owner,union) || !Target(peer,uid,union))throw new InvalidOperationException("Union expulsion ownership changed.");t.Commit();
                }}
                union.Coreofficals=core;union.Harem=harem;union.Guards=guards;
                if(BeforeProjection!=null)BeforeProjection();
                if(peer!=null){peer.CommitUnionMembership(null);Revora.TitleRequirements.Reconcile(peer,false);peer.Send("You have been dismissed from the union.");}
                owner.Send("The member has been dismissed.");
            }catch(Exception ex){Q.Fail(owner,"union-expulsion",ex);if(peer!=null && Q.Player(peer,false))Q.Fail(peer,"union-expulsion",ex);}
            finally {
                if(gate)Monitor.Exit(UnionFormation.Sync);if(secondHeld)Monitor.Exit(second);if(firstHeld)Monitor.Exit(first);
                if(targetLease)SessionInstall.Exit(uid);SessionInstall.Exit(owner.Entity.UID);
            }
        }
    }
    internal static class UnionOffices
    {
        internal static Action BeforeProjection=null;
        internal static bool TryHandle(byte[] packet,Player owner) {
            ushort action=Revora.BitConverter.ToUInt16(packet,4);int kind;bool assign=false,resign=false;
            switch(action) {
                case 28:case 47:kind=0;assign=true;break;
                case 24:case 48:kind=1;assign=true;break;
                case 41:case 49:kind=2;assign=true;break;
                case 29:kind=0;break;case 25:kind=1;break;case 44:kind=2;break;
                case 30:kind=0;resign=true;break;case 26:kind=1;resign=true;break;
                default:return false;
            }
            uint uid=resign?owner.Entity.UID:Revora.BitConverter.ToUInt32(packet,10);string name=null;
            if(assign) {
                if(packet[19]==0 || 20+packet[19]>packet.Length-8)return true;
                name=Encoding.Default.GetString(packet,20,packet[19]);if(string.IsNullOrWhiteSpace(name) || name!=name.Trim() || name.Any(char.IsControl))return true;
                var target=Player.GetClientFromName(name);if(target==null)return true;uid=target.Entity.UID;
            }
            Change(owner,uid,kind,Revora.BitConverter.ToUInt32(packet,14),assign,resign,name);return true;
        }
        private static int Slot(uint[] offices,int kind,uint rank) {
            if(kind==0)return rank>=2000 && rank<=2070 && (rank-2000)%10==0?(int)(rank-2000)/10:-1;
            if(kind==1)return Array.FindIndex(offices,id=>id==0);
            if(rank==4000)return 0;if(rank==4011 || rank==4012)return (int)rank-4010;if(rank>=4021 && rank<=4027)return (int)rank-4018;
            if(rank==401)return Array.FindIndex(offices,1,2,id=>id==0);if(rank==402)return Array.FindIndex(offices,3,7,id=>id==0);return -1;
        }
        private static bool Member(Player p,U union) {U current;return Q.Player(p,false) && union!=null && p.UnionID==union.ID && ReferenceEquals(p.Union,union)
            && Kernel.Unions.TryGetValue(union.ID,out current) && ReferenceEquals(current,union);}
        private static void Change(Player owner,uint uid,int kind,uint rank,bool assign,bool resign,string name) {
            var union=owner.Union;if(uid==0 || !Member(owner,union) || union.IsLeader(uid) || (!resign && (!union.IsLeader(owner.Entity.UID) || owner.Offical!=1)))return;
            var ids=new[]{owner.Entity.UID,uid}.Distinct().OrderBy(id=>id).ToArray();var leases=new List<uint>();var locks=new List<object>();var players=new Dictionary<uint,Player>();bool gate=false;
            Func<bool> current=()=> {
                if(!Member(owner,union) || union.IsLeader(uid) || !resign && (!union.IsLeader(owner.Entity.UID) || owner.Offical!=1))return false;
                foreach(var id in ids){Player p;Kernel.GamePool.TryGetValue(id,out p);if(!ReferenceEquals(p,players[id]) || p!=null && !Member(p,union))return false;}
                return !assign || players[uid]!=null && players[uid].Entity.Name==name;
            };
            try {
                foreach(var id in ids){if(!SessionInstall.TryEnter(id))return;leases.Add(id);Player p;Kernel.GamePool.TryGetValue(id,out p);players[id]=p;
                    if(p!=null){if(p.ItemSyncRoot==null || !Monitor.TryEnter(p.ItemSyncRoot))return;locks.Add(p.ItemSyncRoot);}}
                if(!(gate=Monitor.TryEnter(UnionFormation.Sync)) || !current())return;
                uint[] core,harem,guards;ushort official=0,h=0,g=0;
                using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s';SELECT pg_advisory_xact_lock(6711,3998)");
                    using(var q=Q.Command(c,t,"SELECT uid,name,COALESCE(unionid,0),COALESCE(offical,0),COALESCE(harem,0),COALESCE(guards,0) FROM entities WHERE uid=ANY(@ids) ORDER BY uid FOR UPDATE","ids",ids.Select(id=>(long)id).ToArray()))using(var r=q.ExecuteReader()) {
                        int index=0;while(r.Read()) {
                            if(index>=ids.Length || Convert.ToUInt32(r[0])!=ids[index++])return;uint id=Convert.ToUInt32(r[0]);var p=players[id];
                            ushort savedOffice=Convert.ToUInt16(r[3]),savedHarem=Convert.ToUInt16(r[4]),savedGuard=Convert.ToUInt16(r[5]);
                            if(Convert.ToUInt32(r[2])!=union.ID || !resign && id==owner.Entity.UID && savedOffice!=1 || id==uid && savedOffice==1)return;
                            if(p!=null && (r.GetString(1)!=p.Entity.Name || savedOffice!=p.Offical || savedHarem!=p.Harem || savedGuard!=p.Guards))return;
                            if(id==uid){official=savedOffice;h=savedHarem;g=savedGuard;}
                        }
                        if(index!=ids.Length)return;
                    }
                    using(var q=Q.Command(c,t,"SELECT leaderuid,leadername,coreofficals,harem,guards FROM unions WHERE id=@id FOR UPDATE","id",(long)union.ID))using(var r=q.ExecuteReader()) {
                        if(!r.Read() || Convert.ToUInt32(r[0])!=union.LeaderUID || !resign && (Convert.ToUInt32(r[0])!=owner.Entity.UID || r.GetString(1)!=owner.Entity.Name))return;
                        core=U.RestoreOffices(r.GetString(2),8,true);harem=U.RestoreOffices(r.GetString(3),10,false);guards=U.RestoreOffices(r.GetString(4),20,false);
                    }
                    if(!core.SequenceEqual(union.Coreofficals) || !harem.SequenceEqual(union.Harem) || !guards.SequenceEqual(union.Guards))throw new InvalidOperationException("Union office cache is stale.");
                    var offices=kind==0?core:kind==1?guards:harem;ushort before=kind==0?official:kind==1?g:h,after=0;
                    if(assign) {
                        int slot=Slot(offices,kind,rank);if(slot<0 || slot>=offices.Length || offices[slot]!=0 || offices.Contains(uid) || before!=0)return;
                        offices[slot]=uid;after=(ushort)(kind==0?1<<(slot+1):kind==1?512:slot==0?1024:slot<3?2048:4096);
                    } else {if(before==0 && !offices.Contains(uid))return;for(int i=0;i<offices.Length;i++)if(offices[i]==uid)offices[i]=0;}
                    if(kind==0)official=after;else if(kind==1)g=after;else h=after;
                    if(Q.Execute(c,t,"UPDATE unions SET coreofficals=@core,harem=@harem,guards=@guards WHERE id=@id","core",string.Join("#",core),"harem",string.Join("#",harem),"guards",string.Join("#",guards),"id",(long)union.ID)!=1
                        || Q.Execute(c,t,"UPDATE entities SET offical=@office,harem=@harem,guards=@guards WHERE uid=@uid AND unionid=@union AND COALESCE(offical,0)<>1","office",(int)official,"harem",(int)h,"guards",(int)g,"uid",(long)uid,"union",(long)union.ID)!=1)
                        throw new InvalidOperationException("Union office change was not saved completely.");
                    if(!current())throw new InvalidOperationException("Union office ownership changed.");t.Commit();
                }}
                union.Coreofficals=core;union.Harem=harem;union.Guards=guards;if(BeforeProjection!=null)BeforeProjection();
                var peer=players[uid];if(peer!=null){peer.CommitUnionOffice(official,h,g);Revora.TitleRequirements.Reconcile(peer,false);}
                if(kind==0)owner.Send(union.ShowLeagueImperialCourtList());else if(kind==1)owner.Send(union.ShowLeaguePalaceGuardsList(1));else owner.Send(union.ShowLeagueConcubinesList());
            }catch(Exception ex){foreach(var p in players.Values.Where(p=>p!=null && Q.Player(p,false)))Q.Fail(p,"union-offices",ex);}
            finally {if(gate)Monitor.Exit(UnionFormation.Sync);for(int i=locks.Count-1;i>=0;i--)Monitor.Exit(locks[i]);for(int i=leases.Count-1;i>=0;i--)SessionInstall.Exit(leases[i]);}
        }
    }
    internal static class UnionSuccession
    {
        internal static Action BeforeProjection=null;
        private static bool Member(Player p,U union) {
            U current;
            return Q.Player(p,false) && union!=null && p.UnionID==union.ID && ReferenceEquals(p.Union,union)
                && Kernel.Unions.TryGetValue(union.ID,out current) && ReferenceEquals(current,union)
                && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;
        }
        internal static void Transfer(Player owner,uint target) {
            if(!Q.Player(owner,false) || target==owner.Entity.UID || !SessionInstall.TryEnter(owner.Entity.UID))return;
            Player peer=null;bool peerLease=false,firstHeld=false,secondHeld=false,gate=false;object first=null,second=null;
            try {
                if(!(peerLease=SessionInstall.TryEnter(target)) || !Kernel.GamePool.TryGetValue(target,out peer))return;
                first=owner.Entity.UID<target?owner.ItemSyncRoot:peer.ItemSyncRoot;second=owner.Entity.UID<target?peer.ItemSyncRoot:owner.ItemSyncRoot;
                if(first==null || second==null || !(firstHeld=Monitor.TryEnter(first)) || !(secondHeld=Monitor.TryEnter(second))
                    || !(gate=Monitor.TryEnter(UnionFormation.Sync)))return;
                var union=owner.Union;
                if(!Member(owner,union) || !Member(peer,union) || !union.IsLeader(owner.Entity.UID))return;
                uint[] core,harem,guards;
                using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s';SELECT pg_advisory_xact_lock(6711,3998)");
                    using(var q=Q.Command(c,t,"SELECT uid,name,COALESCE(unionid,0),COALESCE(offical,0),COALESCE(harem,0),COALESCE(guards,0) FROM entities WHERE uid IN(@old,@next) ORDER BY uid FOR UPDATE","old",(long)owner.Entity.UID,"next",(long)target))using(var r=q.ExecuteReader()) {
                        int count=0;while(r.Read()) {
                            var p=Convert.ToUInt32(r[0])==owner.Entity.UID?owner:peer;count++;
                            if(r.GetString(1)!=p.Entity.Name || Convert.ToUInt32(r[2])!=union.ID || Convert.ToUInt16(r[3])!=p.Offical
                                || Convert.ToUInt16(r[4])!=p.Harem || Convert.ToUInt16(r[5])!=p.Guards)return;
                        }
                        if(count!=2)return;
                    }
                    using(var q=Q.Command(c,t,"SELECT leaderuid,leadername,coreofficals,harem,guards FROM unions WHERE id=@id FOR UPDATE","id",(long)union.ID))using(var r=q.ExecuteReader()) {
                        if(!r.Read() || Convert.ToUInt32(r[0])!=owner.Entity.UID || r.GetString(1)!=owner.Entity.Name)return;
                        core=U.RestoreOffices(r.GetString(2),8,true);harem=U.RestoreOffices(r.GetString(3),10,false);guards=U.RestoreOffices(r.GetString(4),20,false);
                    }
                    if(!core.SequenceEqual(union.Coreofficals) || !harem.SequenceEqual(union.Harem) || !guards.SequenceEqual(union.Guards))throw new InvalidOperationException("Union office cache is stale.");
                    foreach(var offices in new[]{core,harem,guards})for(int i=0;i<offices.Length;i++)if(offices[i]==target || offices[i]==owner.Entity.UID)offices[i]=0;
                    if(Q.Execute(c,t,"UPDATE unions SET leaderuid=@next,leadername=@name,coreofficals=@core,harem=@harem,guards=@guards WHERE id=@id AND leaderuid=@old",
                        "next",(long)target,"name",peer.Entity.Name,"core",string.Join("#",core),"harem",string.Join("#",harem),"guards",string.Join("#",guards),"id",(long)union.ID,"old",(long)owner.Entity.UID)!=1
                        || Q.Execute(c,t,"UPDATE entities SET offical=0,harem=0,guards=0 WHERE uid=@uid AND unionid=@union","uid",(long)owner.Entity.UID,"union",(long)union.ID)!=1
                        || Q.Execute(c,t,"UPDATE entities SET offical=1,harem=0,guards=0 WHERE uid=@uid AND unionid=@union","uid",(long)target,"union",(long)union.ID)!=1)
                        throw new InvalidOperationException("Union succession was not saved completely.");
                    if(!Member(owner,union) || !Member(peer,union) || !union.IsLeader(owner.Entity.UID))throw new InvalidOperationException("Union succession ownership changed.");t.Commit();
                }}
                union.LeaderUID=target;union.LeaderName=peer.Entity.Name;union.Coreofficals=core;union.Harem=harem;union.Guards=guards;
                KingdomCrown.RefreshAuthority();if(BeforeProjection!=null)BeforeProjection();owner.CommitUnionOffice(0,0,0);peer.CommitUnionOffice(1,0,0);
                // Reconcile current predicates now; competitive crown authority is integrated separately.
                Revora.TitleRequirements.Reconcile(owner,false);
                Revora.TitleRequirements.Reconcile(peer,false);
            }catch(Exception ex){Q.Fail(owner,"union-succession",ex);if(peer!=null && Q.Player(peer,false))Q.Fail(peer,"union-succession",ex);}
            finally {
                if(gate)Monitor.Exit(UnionFormation.Sync);if(secondHeld)Monitor.Exit(second);if(firstHeld)Monitor.Exit(first);
                if(peerLease)SessionInstall.Exit(target);SessionInstall.Exit(owner.Entity.UID);
            }
        }
    }
}
