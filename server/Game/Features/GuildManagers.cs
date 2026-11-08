using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures.Society;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features {
    // Manager and category Supervisor selection share one transaction. Only ledger-owned
    // automatic offices may be displaced; manual and legacy offices retain their seats.
    internal static class GuildManagers {
        private static bool enabled;
        private static int running;
        private static uint cursor;
        private static DateTime nextPulse;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static int Capacity(byte level) {
            return level>=1 && level<=9?new[]{1,1,2,2,4,4,6,6,8}[level-1]:0;
        }
        internal static int SupervisorCapacity(byte level) {
            return level>=8 && level<=9?2:level>=4 && level<=7?1:0;
        }
        // Explicit Revora category priority; Guide has no verified earning source.
        private static readonly Enums.GuildMemberRank[] Categories={
            Enums.GuildMemberRank.TSupervisor,Enums.GuildMemberRank.OSupervisor,
            Enums.GuildMemberRank.CPSupervisor,Enums.GuildMemberRank.ASupervisor,
            Enums.GuildMemberRank.SSupervisor,Enums.GuildMemberRank.PKSupervisor,
            Enums.GuildMemberRank.RoseSupervisor,Enums.GuildMemberRank.LilySupervisor};
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_guild_managers(
uid bigint PRIMARY KEY,guild_id bigint NOT NULL,receipt uuid NOT NULL UNIQUE,
appointed_at timestamptz NOT NULL DEFAULT now(),CHECK(uid>0 AND guild_id>0));
CREATE TABLE IF NOT EXISTS revora_guild_supervisors(
uid bigint PRIMARY KEY,guild_id bigint NOT NULL,rank integer NOT NULL CHECK(rank IN(851,852,853,855,856,857,858,859)),
receipt uuid NOT NULL UNIQUE,appointed_at timestamptz NOT NULL DEFAULT now(),CHECK(uid>0 AND guild_id>0));");}
            enabled=true;
        }
        // One guild per pulse; no worker can overlap another pulse. Busy owners
        // are retried on a later round without waiting on their inventory locks.
        internal static void Tick() {
            if(!enabled || Interlocked.CompareExchange(ref running,1,0)!=0)return;
            try {
                DateTime now=Clock();if(now<nextPulse)return;nextPulse=now.AddSeconds(5);
                var ids=Kernel.Guilds.Keys.OrderBy(id=>id).ToArray();if(ids.Length==0)return;
                uint id=ids.FirstOrDefault(value=>value>cursor);if(id==0)id=ids[0];cursor=id;
                Guild guild;if(Kernel.Guilds.TryGetValue(id,out guild))Reconcile(guild);
            } finally {Volatile.Write(ref running,0);}
        }
        private sealed class Owner {
            internal Guild.Member Member;internal GameState Player;
            internal bool Lease,Held;internal Enums.GuildMemberRank Before,After;
            internal decimal Score;internal readonly Dictionary<Enums.GuildMemberRank,decimal> Categories=new Dictionary<Enums.GuildMemberRank,decimal>();
        }
        private static bool Current(Guild guild,Owner[] owners) {
            Guild current;
            if(!Kernel.Guilds.TryGetValue(guild.ID,out current) || !ReferenceEquals(current,guild) || guild.Members.Count!=owners.Length)return false;
            foreach(var owner in owners) {
                Guild.Member member;GameState player;
                Kernel.GamePool.TryGetValue(owner.Member.ID,out player);
                if(!guild.Members.TryGetValue(owner.Member.ID,out member) || !ReferenceEquals(member,owner.Member)
                    || member.GuildID!=guild.ID || !ReferenceEquals(player,owner.Player) || member.Rank!=owner.Before)return false;
                if(player!=null && (!Q.Player(player,false) || !ReferenceEquals(player.Guild,guild)
                    || !ReferenceEquals(player.AsMember,member) || player.Entity.GuildID!=guild.ID || player.Entity.GuildRank!=(ushort)owner.Before))return false;
            }
            return true;
        }
        internal static bool Reconcile(Guild guild) {
            if(!enabled || guild==null)return false;
            var owners=guild.Members.Values.OrderBy(m=>m.ID).Take(801).Select(m=>new Owner{Member=m,Before=m.Rank,After=m.Rank}).ToArray();
            if(owners.Length==0 || owners.Length>800)return false;
            bool gate=false,commitAttempt=false;
            try {
                foreach(var owner in owners) {
                    if(!(owner.Lease=SessionInstall.TryEnter(owner.Member.ID)))return false;
                    Kernel.GamePool.TryGetValue(owner.Member.ID,out owner.Player);
                    if(owner.Player!=null && (owner.Player.ItemSyncRoot==null || !(owner.Held=Monitor.TryEnter(owner.Player.ItemSyncRoot))))return false;
                }
                if(!(gate=Monitor.TryEnter(GuildLeadership.Gate(guild))) || !Current(guild,owners))return false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    // Character rows precede the guild row, matching appointments and donations.
                    using(var q=Q.Command(c,t,@"SELECT uid,guildrank,COALESCE(guildsilverdonation,0),COALESCE(guildconquerpointdonation,0),
COALESCE(arsenaldonation,0),COALESCE(guildpkdonation,0) FROM entities WHERE guildid=@guild ORDER BY uid FOR UPDATE","guild",(long)guild.ID))
                    using(var r=q.ExecuteReader()) {
                        int index=0;
                        while(r.Read()) {
                            if(index>=owners.Length)return false;var owner=owners[index++];
                            if(Convert.ToUInt32(r[0])!=owner.Member.ID || Convert.ToUInt16(r[1])!=(ushort)owner.Before)return false;
                            var saved=new Guild.Member(guild.ID){SilverDoantion=Convert.ToUInt64(r[2]),ConquerPointDonation=Convert.ToUInt64(r[3]),
                                Arsenal_Donation=Convert.ToUInt64(r[4]),PKDoantion=Convert.ToUInt64(r[5])};
                            // Do not appoint from values a stale autosave could immediately overwrite.
                            if(saved.SilverDoantion!=owner.Member.SilverDoantion || saved.ConquerPointDonation!=owner.Member.ConquerPointDonation
                                || saved.Arsenal_Donation!=owner.Member.Arsenal_Donation || saved.PKDoantion!=owner.Member.PKDoantion)return false;
                            owner.Score=saved.DonationScore;
                            owner.Categories[Enums.GuildMemberRank.CPSupervisor]=saved.ConquerPointDonation;
                            owner.Categories[Enums.GuildMemberRank.ASupervisor]=saved.Arsenal_Donation;
                            owner.Categories[Enums.GuildMemberRank.SSupervisor]=saved.SilverDoantionMath;
                            owner.Categories[Enums.GuildMemberRank.PKSupervisor]=saved.PKDoantion;
                        }
                        if(index!=owners.Length)return false;
                    }
                    using(var q=Q.Command(c,t,"SELECT id FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild.ID))
                        if(q.ExecuteScalar()==null)return false;
                    int level=1;
                    using(var q=Q.Command(c,t,@"SELECT a.arsenal_id FROM pt_arsenal a
WHERE a.syn_id=@guild AND a.arsenal_id BETWEEN 1 AND 8 AND a.arsenal_unlocked=1
AND (SELECT COALESCE(sum(i.arsenal_donation::numeric),0) FROM pt_arsenal_inscribed i
WHERE i.syn_id=a.syn_id AND i.iten_atype=a.arsenal_id)>=5000000 GROUP BY a.arsenal_id","guild",(long)guild.ID))
                    using(var r=q.ExecuteReader())while(r.Read())level++;
                    var automatic=new HashSet<uint>();
                    using(var q=Q.Command(c,t,"SELECT uid FROM revora_guild_managers WHERE guild_id=@guild ORDER BY uid FOR UPDATE","guild",(long)guild.ID))
                    using(var r=q.ExecuteReader())while(r.Read())automatic.Add(checked(Convert.ToUInt32(r[0])));
                    var supervisors=new Dictionary<uint,Enums.GuildMemberRank>();
                    using(var q=Q.Command(c,t,"SELECT uid,rank FROM revora_guild_supervisors WHERE guild_id=@guild ORDER BY uid FOR UPDATE","guild",(long)guild.ID))
                    using(var r=q.ExecuteReader())while(r.Read())supervisors.Add(checked(Convert.ToUInt32(r[0])),(Enums.GuildMemberRank)Convert.ToUInt16(r[1]));
                    var byId=owners.ToDictionary(o=>o.Member.ID);
                    // Flower delivery takes character locks before balance locks, in this same order.
                    using(var q=Q.Command(c,t,@"SELECT f.uid,f.totals FROM revora_flower_balances f JOIN entities e ON e.uid=f.uid
WHERE e.guildid=@guild ORDER BY f.uid FOR UPDATE OF f","guild",(long)guild.ID))
                    using(var r=q.ExecuteReader())while(r.Read()) {
                        var owner=byId[Convert.ToUInt32(r[0])];var totals=(long[])r[1];
                        if(totals.Length!=4 || totals.Any(v=>v<0))throw new InvalidOperationException("Invalid saved flowers.");
                        owner.Categories[Enums.GuildMemberRank.RoseSupervisor]=totals[0];
                        owner.Categories[Enums.GuildMemberRank.LilySupervisor]=totals[1];
                        owner.Categories[Enums.GuildMemberRank.OSupervisor]=totals[2];
                        owner.Categories[Enums.GuildMemberRank.TSupervisor]=totals[3];
                    }
                    Func<Owner,bool> managed=o=>o.Before==Enums.GuildMemberRank.Manager && automatic.Contains(o.Member.ID)
                        || supervisors.ContainsKey(o.Member.ID) && supervisors[o.Member.ID]==o.Before;
                    Func<Owner,bool> eligible=o=>o.Before==Enums.GuildMemberRank.Member || managed(o);
                    foreach(var owner in owners.Where(managed))owner.After=Enums.GuildMemberRank.Member;
                    int reserved=owners.Count(o=>o.Before==Enums.GuildMemberRank.Manager && !managed(o));
                    foreach(var owner in owners.Where(o=>eligible(o) && o.Score>0)
                        .OrderByDescending(o=>o.Score).ThenBy(o=>o.Member.ID).Take(Math.Max(0,Capacity((byte)level)-reserved)))
                        owner.After=Enums.GuildMemberRank.Manager;
                    foreach(var rank in Categories) {
                        int seats=Math.Max(0,SupervisorCapacity((byte)level)-owners.Count(o=>o.Before==rank && !managed(o)));
                        foreach(var owner in owners.Where(o=>eligible(o) && o.After==Enums.GuildMemberRank.Member
                            && o.Categories.ContainsKey(rank) && o.Categories[rank]>0)
                            .OrderByDescending(o=>o.Categories[rank]).ThenBy(o=>o.Member.ID).Take(seats))owner.After=rank;
                    }
                    // Retire orphaned or manually replaced receipts without touching the replacement office.
                    foreach(uint uid in automatic.Where(uid=>!byId.ContainsKey(uid) || byId[uid].After!=Enums.GuildMemberRank.Manager))
                        if(Q.Execute(c,t,"DELETE FROM revora_guild_managers WHERE uid=@uid AND guild_id=@guild","uid",(long)uid,"guild",(long)guild.ID)!=1)
                            throw new InvalidOperationException("Manager ownership removal failed.");
                    foreach(var entry in supervisors.Where(entry=>!byId.ContainsKey(entry.Key) || byId[entry.Key].After!=entry.Value))
                        if(Q.Execute(c,t,"DELETE FROM revora_guild_supervisors WHERE uid=@uid AND guild_id=@guild","uid",(long)entry.Key,"guild",(long)guild.ID)!=1)
                            throw new InvalidOperationException("Supervisor ownership removal failed.");
                    foreach(var owner in owners) {
                        uint uid=owner.Member.ID;
                        if(owner.After==owner.Before)continue;
                        if(Q.Execute(c,t,"UPDATE entities SET guildrank=@after WHERE uid=@uid AND guildid=@guild AND guildrank=@before",
                            "after",(int)owner.After,"uid",(long)uid,"guild",(long)guild.ID,"before",(int)owner.Before)!=1)
                            throw new InvalidOperationException("Automatic office rank write failed.");
                        Guid receipt=Guid.NewGuid();
                        if(owner.After==Enums.GuildMemberRank.Manager) {
                            if(Q.Execute(c,t,@"INSERT INTO revora_guild_managers(uid,guild_id,receipt) VALUES(@uid,@guild,@receipt)
ON CONFLICT(uid) DO UPDATE SET guild_id=EXCLUDED.guild_id,receipt=EXCLUDED.receipt,appointed_at=now()",
                                "uid",(long)uid,"guild",(long)guild.ID,"receipt",receipt)!=1)throw new InvalidOperationException("Manager ownership write failed.");
                            ProgressionEvidence.Complete(c,t,uid,10223,"guild-manager-v1",receipt.ToString("N"),1);
                        } else if(Categories.Contains(owner.After)) {
                            if(Q.Execute(c,t,@"INSERT INTO revora_guild_supervisors(uid,guild_id,rank,receipt) VALUES(@uid,@guild,@rank,@receipt)
ON CONFLICT(uid) DO UPDATE SET guild_id=EXCLUDED.guild_id,rank=EXCLUDED.rank,receipt=EXCLUDED.receipt,appointed_at=now()",
                                "uid",(long)uid,"guild",(long)guild.ID,"rank",(int)owner.After,"receipt",receipt)!=1)throw new InvalidOperationException("Supervisor ownership write failed.");
                            ProgressionEvidence.Complete(c,t,uid,10224,"guild-supervisor-v1",receipt.ToString("N"),1);
                        }
                    }
                    if(!Current(guild,owners))throw new InvalidOperationException("Manager session changed.");
                    commitAttempt=true;t.Commit();
                }}
                foreach(var owner in owners.Where(o=>o.After!=o.Before)) {
                    owner.Member.Rank=owner.After;
                    if(owner.Player!=null)owner.Player.Entity.GuildRank=(ushort)owner.After;
                }
                GuildLeadership.Recount(guild);GuildTable.BuildDonationRankings(guild);
                foreach(var owner in owners.Where(o=>o.After!=o.Before && o.Player!=null)) {
                    guild.SendGuild(owner.Player);Q.Refresh(owner.Player);owner.Player.Screen.FullWipe();owner.Player.Screen.Reload(null);
                }
                return true;
            } catch(Exception ex) {
                var databaseError=ex as Npgsql.PostgresException;
                Console.WriteLine("[guild-manager] guild="+guild.ID+" error="+ex.GetType().Name+(databaseError==null?"":" sqlstate="+databaseError.SqlState));
                if(commitAttempt)foreach(var owner in owners.Where(o=>o.After!=o.Before && o.Player!=null))Q.Fail(owner.Player,"guild-manager",ex);
                return false;
            } finally {
                if(gate)Monitor.Exit(GuildLeadership.Gate(guild));
                foreach(var owner in owners.Reverse()) {
                    if(owner.Held)Monitor.Exit(owner.Player.ItemSyncRoot);
                    if(owner.Lease)SessionInstall.Exit(owner.Member.ID);
                }
            }
        }
    }
}
