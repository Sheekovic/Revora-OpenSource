using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Rank=Revora.Game.Enums.GuildMemberRank;

namespace Revora.Game.Features
{
    internal static class GuildWarPurses
    {
        internal sealed class Recipient {internal uint Uid,Guild,Cp,Rvp;internal ulong Gold;internal Rank Rank;internal int Tokens,Honor;}
        private static bool initialized;
        private static readonly object pollGate=new object();
        private static DateTime nextPoll;
        private static long cursor;
        private static readonly HashSet<Rank> memberRanks=new HashSet<Rank>{Rank.Member,Rank.Agent,Rank.Aide,Rank.ArsenalAgent,Rank.ArsFollower,Rank.ASupervisor,
            Rank.CPAgent,Rank.CPFollower,Rank.CPSupervisor,Rank.DeputySteward,Rank.DLeaderAide,Rank.Follower,Rank.GSupervisor,Rank.GuideAgent,Rank.GuideFollower,
            Rank.HDeputyLeader,Rank.HonoraryManager,Rank.HonorarySteward,Rank.HonorarySuperv,Rank.LilyAgent,Rank.LilyFollower,Rank.LilySupervisor,Rank.LSpouseAide,
            Rank.Manager,Rank.ManagerAide,Rank.ManagerSpouse,Rank.OrchidAgent,Rank.OrchidFollower,Rank.OSupervisor,Rank.PKAgent,Rank.PKFollower,Rank.PKSupervisor,
            Rank.RoseAgent,Rank.RoseFollower,Rank.RoseSupervisor,Rank.SeniorMember,Rank.SilverAgent,Rank.SilverFollower,Rank.SSupervisor,Rank.Steward,
            Rank.StewardSpouse,Rank.Supervisor,Rank.SupervisorAide,Rank.SupervSpouse,Rank.TSupervisor,Rank.TulipAgent,Rank.TulipFollower};
        internal static void Initialize() {
            if(initialized)return;
            foreach(uint id in new uint[]{3302411,723467})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Guild purse catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,@"ALTER TABLE revora_guild_battles ADD COLUMN IF NOT EXISTS purses_frozen boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_guild_purses(
day varchar(10) NOT NULL REFERENCES revora_guild_battles(day),uid bigint NOT NULL CHECK(uid>0),guild_id bigint NOT NULL CHECK(guild_id>0),rank integer NOT NULL,
cp bigint NOT NULL CHECK(cp BETWEEN 0 AND 4294967295),rvp bigint NOT NULL CHECK(rvp BETWEEN 0 AND 4294967295),gold bigint NOT NULL CHECK(gold>=0),
tokens integer NOT NULL CHECK(tokens IN(0,2,4)),honor integer NOT NULL CHECK(honor IN(9,10)),earned_at timestamptz NOT NULL,honor_until timestamptz NOT NULL,
claimed_at timestamptz,rewards bigint[],PRIMARY KEY(day,uid),CHECK((claimed_at IS NULL)=(rewards IS NULL)));
ALTER TABLE revora_guild_purses ADD COLUMN IF NOT EXISTS soul_type integer NOT NULL DEFAULT 0 CHECK(soul_type=0 OR soul_type BETWEEN 800000 AND 899999);
CREATE INDEX IF NOT EXISTS revora_guild_purse_pending ON revora_guild_purses(uid,day) WHERE claimed_at IS NULL;
CREATE INDEX IF NOT EXISTS revora_guild_purse_paid ON revora_guild_purses(uid,day) WHERE claimed_at IS NOT NULL;
UPDATE revora_guild_battles SET purses_frozen=true WHERE state<>'active' AND NOT purses_frozen;");t.Commit();
            }}initialized=true;
        }
        private static bool Present(GameState p,Guild guild) {
            Guild actual;Guild.Member m;return Q.Player(p,false) && guild!=null && ReferenceEquals(p.Guild,guild) && p.Entity.GuildID==guild.ID
                && Kernel.Guilds.TryGetValue(guild.ID,out actual) && ReferenceEquals(actual,guild)
                && guild.Members.TryGetValue(p.Entity.UID,out m) && ReferenceEquals(m,p.AsMember) && m.ID==p.Entity.UID && m.GuildID==guild.ID
                && m.Name==p.Entity.Name && p.Entity.GuildRank==(ushort)m.Rank && p.Entity.MapID==1038 && p.Map.ID==1038 && p.Map.BaseID==1038;
        }
        internal static List<Recipient> Snapshot(uint winner) {
            var result=new List<Recipient>();Guild guild;if(!initialized || winner==0 || !Kernel.Guilds.TryGetValue(winner,out guild))return result;
            // A leadership transfer must not split this snapshot into two leaders.
            lock(GuildLeadership.Gate(guild))
            foreach(var p in Kernel.GamePool.Values)if(Present(p,guild)) {
                var rank=p.AsMember.Rank;bool leader=rank==Rank.GuildLeader,spouse=rank==Rank.LeaderSpouse;
                bool officer=spouse || rank==Rank.DeputyLeader || rank==Rank.DLeaderSpouse;
                if(leader && !GuildLeadership.Leader(p))continue;
                if(!leader && !officer && !memberRanks.Contains(rank))continue;
                result.Add(new Recipient{Uid=p.Entity.UID,Guild=winner,Rank=rank,Cp=leader || officer?Config.GuildWarCp:Config.GuildWarMemberCp,
                    Rvp=leader?Config.GuildWarLeaderRevoraPoints:officer?Config.GuildWarOfficerRevoraPoints:Config.GuildWarMemberRevoraPoints,
                    Gold=leader || officer?Config.GuildWarGold:Config.GuildWarMemberGold,Tokens=leader?4:officer?2:0,Honor=leader || spouse?9:10});
            }
            return result.OrderBy(r=>r.Uid).ToList();
        }
        internal static void LockOwners(NpgsqlConnection c,NpgsqlTransaction t,List<Recipient> recipients) {
            if(recipients.Count==0)return;
            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=ANY(@ids) ORDER BY uid FOR UPDATE","ids",recipients.Select(r=>(long)r.Uid).ToArray()))
            using(var r=q.ExecuteReader()){int count=0;while(r.Read())count++;if(count!=recipients.Count)throw new InvalidOperationException("Guild purse recipient missing.");}
        }
        internal static void Freeze(NpgsqlConnection c,NpgsqlTransaction t,string day,uint winner,DateTime end,List<Recipient> recipients) {
            foreach(var r in recipients) {
                if(r.Guild!=winner || r.Gold>Entity.MaximumSilver)throw new InvalidOperationException("Guild purse configuration changed.");
                if(Q.Execute(c,t,@"INSERT INTO revora_guild_purses(day,uid,guild_id,rank,cp,rvp,gold,tokens,honor,earned_at,honor_until,soul_type)
VALUES(@day,@uid,@guild,@rank,@cp,@rvp,@gold,@tokens,@honor,@end,@until,@soul)","day",day,"uid",(long)r.Uid,"guild",(long)r.Guild,"rank",(int)r.Rank,
                    "cp",(long)r.Cp,"rvp",(long)r.Rvp,"gold",checked((long)r.Gold),"tokens",r.Tokens,"honor",r.Honor,"end",end,"until",end.AddDays(6),"soul",(int)QuestSoulReward.Create().ID)!=1)
                    throw new InvalidOperationException("Guild purse entitlement not saved.");
                ProgressionEvidence.Complete(c,t,r.Uid,10302,"guild-war-occupation-v1",day,1);
            }
            if(Q.Execute(c,t,"UPDATE revora_guild_battles SET purses_frozen=true WHERE day=@day AND NOT purses_frozen","day",day)!=1)
                throw new InvalidOperationException("Guild purse snapshot not sealed.");
        }
        internal static string Claim(GameState p) {
            if(!initialized || !Q.Player(p,false) || p.Trade==null || p.Trade.InTrade || p.Booth!=null)return "Your war purse will wait.";
            if(!Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Guild purse requires its owner lock.");
            var now=GuildSeasonWar.Clock();string day;long cp,rvp,gold;int tokens,honor;uint soulType;DateTime honorUntil;ConquerItem[] items;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Guild purse owner missing.");
                using(var q=Q.Command(c,t,@"SELECT day,cp,rvp,gold,tokens,honor,honor_until,soul_type FROM revora_guild_purses
WHERE uid=@uid AND claimed_at IS NULL AND earned_at<=@now ORDER BY day LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID,"now",now))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read())return "No unclaimed war purse awaits you.";day=r.GetString(0);cp=r.GetInt64(1);rvp=r.GetInt64(2);gold=r.GetInt64(3);
                    tokens=r.GetInt32(4);honor=r.GetInt32(5);honorUntil=r.GetFieldValue<DateTimeOffset>(6).UtcDateTime;soulType=checked((uint)r.GetInt32(7));
                }
                ConquerItemBaseInformation soulInfo;
                if(soulType!=0 && (!ConquerItemInformation.BaseInformations.TryGetValue(soulType,out soulInfo) || soulInfo.PurificationLevel!=7))
                    throw new InvalidOperationException("Guild purse soul catalog changed.");
                int places=tokens+1+(soulType==0?0:1);
                if(p.Inventory.Count+places>40)return "Make "+places+" places in your bag. Your war purse is safe.";
                long nextCp=p.Entity.ConquerPoints+cp,nextRvp=p.Entity.RevoraPoint+rvp;decimal nextGold=p.Entity.Money+(decimal)gold;
                if(nextCp>uint.MaxValue || nextRvp>uint.MaxValue || nextGold>Entity.MaximumSilver)return "Make room in your purse before collecting the war rewards.";
                SilverBalance.AssertSaved(c,t,p);
                using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Guild purse inventory requires reload.");
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@gold WHERE uid=@uid AND conquerpoints=@oldcp AND revorapoint=@oldrvp",
                    "cp",nextCp,"rvp",nextRvp,"gold",nextGold,"uid",(long)p.Entity.UID,"oldcp",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint)!=1)
                    throw new InvalidOperationException("Guild purse balance changed.");
                items=Enumerable.Range(0,tokens+1).Select(i=>Q.New(i==0?3302411u:723467u,true)).ToArray();
                foreach(var item in items) {
                    item.Days=1;item.DayStamp=now.ToLocalTime();item.TimeLeftInMinutes=86400;
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,days,daystamp)
VALUES(@item,@type,@uid,0,0,1,1,@dur,@max,'1',@stamp)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,
                        "dur",(int)item.Durability,"max",(int)item.MaximDurability,"stamp",item.DayStamp.ToBinary())!=1)throw new InvalidOperationException("Guild purse item not saved.");
                }
                if(soulType!=0){var soul=Q.New(soulType,true);Q.Insert(c,t,p.Entity.UID,soul);items=items.Concat(new[]{soul}).ToArray();}
                if(honorUntil>now) {
                    long expiry=checked((long)Kernel.ToDateTimeInt(honorUntil.ToLocalTime()));
                    Q.Execute(c,t,"DELETE FROM status WHERE entityid=@uid AND status IN(9,10) AND time<=@expiry","uid",(long)p.Entity.UID,"expiry",expiry);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM status WHERE entityid=@uid AND status IN(9,10) AND time>@expiry","uid",(long)p.Entity.UID,"expiry",expiry))
                        if(Convert.ToInt64(q.ExecuteScalar())==0 && Q.Execute(c,t,"INSERT INTO status(entityid,status,time) VALUES(@uid,@honor,@expiry)",
                            "uid",(long)p.Entity.UID,"honor",honor,"expiry",expiry)!=1)throw new InvalidOperationException("Guild purse honor not saved.");
                }
                if(Q.Execute(c,t,"UPDATE revora_guild_purses SET claimed_at=@now,rewards=@items WHERE day=@day AND uid=@uid AND claimed_at IS NULL",
                    "now",now,"items",items.Select(i=>(long)i.UID).ToArray(),"day",day,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Guild purse claim not saved.");
                ProgressionEvidence.Complete(c,t,p.Entity.UID,10228,"guild-war-purse-v1",day,1);
                if(!Q.Player(p,false) || p.Trade.InTrade || p.Booth!=null)throw new InvalidOperationException("Guild purse session changed.");
                t.Commit();p.Entity.ApplyCommittedConquerPoints((uint)nextCp);p.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);SilverBalance.ApplyCommitted(p,(ulong)nextGold);
            }}
            Q.Install(p,new uint[0],items);SynchronizeHonor(p,now);Q.Refresh(p);return "Your guild-war purse is yours. Spend it well.";
        }
        internal static void Recover(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            if(!initialized)return;
            using(var q=Q.Command(c,t,"SELECT day FROM revora_guild_purses WHERE uid=@uid AND claimed_at IS NOT NULL AND NOT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10228) ORDER BY day LIMIT 1","uid",(long)uid)){
                string day=q.ExecuteScalar() as string;if(day!=null)ProgressionEvidence.Complete(c,t,uid,10228,"guild-war-purse-v1",day,1);
            }
        }
        internal static void SynchronizeHonor(GameState p,DateTime now) {
            if(!initialized || !Q.Player(p,false))return;
            int honor=0;DateTime end=DateTime.MinValue;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT status,time FROM status WHERE entityid=@uid AND status IN(9,10) ORDER BY time DESC LIMIT 1","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(r.Read()){honor=Convert.ToInt32(r[0]);end=Kernel.FromDateTimeInt(Convert.ToUInt64(r[1])).ToUniversalTime();}}
            p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.TopGuildLeader);p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.TopDeputyLeader);
            if(end>now)p.Entity.AddFlag(honor==9?(ulong)FlagsUpdate.Flags.TopGuildLeader:(ulong)FlagsUpdate.Flags.TopDeputyLeader,
                checked((int)Math.Min(int.MaxValue,(end-now).TotalSeconds)),false);
        }
        internal static void Tick() {
            SuperGuildWarPurses.Tick();
            Sistemas.Quarantine.Tick(DateTime.UtcNow);
            Sistemas.QuarantineAdmissions.Tick();
            Sistemas.QuarantinePurses.Tick();
            if(!initialized || !Monitor.TryEnter(pollGate))return;
            try {
                var now=GuildSeasonWar.Clock();if(now<nextPoll)return;nextPoll=now.AddSeconds(10);
                var owners=Kernel.GamePool.Values.Where(p=>Q.Player(p,false)).Select(p=>(long)p.Entity.UID).ToArray();if(owners.Length==0)return;
                var due=new List<uint>();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT DISTINCT uid FROM revora_guild_purses
WHERE uid=ANY(@owners) AND uid>@cursor AND claimed_at IS NULL AND earned_at<=@now ORDER BY uid LIMIT 32","owners",owners,"cursor",cursor,"now",now))
                using(var r=q.ExecuteReader())while(r.Read())due.Add(Convert.ToUInt32(r[0]));}
                if(due.Count==0){cursor=0;return;}cursor=due.Last();
                foreach(uint uid in due){GameState p;if(!Kernel.GamePool.TryGetValue(uid,out p) || !Q.Player(p,false) || !Monitor.TryEnter(p.ItemSyncRoot))continue;
                    try{string result=Claim(p);if(result=="Your guild-war purse is yours. Spend it well.")p.Send(result);}catch(Exception ex){Q.Fail(p,"guild-purse",ex);}finally{Monitor.Exit(p.ItemSyncRoot);}}
            }catch(Exception ex){Console.WriteLine("[guild-purse] poll_failure="+ex.GetType().Name);}finally{Monitor.Exit(pollGate);}
        }
    }
}
