using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using NpgsqlTypes;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Revora.Database;

namespace Revora.Game.Features
{
    // One server owns the arena. PostgreSQL owns admission, objective receipts and results.
    internal static class CtfResults
    {
        internal sealed class Result { internal uint Winner; internal string Outcome; }
        internal static void Initialize()
        {
            Transaction((c, t) => {
                Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_ctf_runs(
run varchar(32) PRIMARY KEY, ends_at timestamptz NOT NULL,
state varchar(16) NOT NULL CHECK(state IN ('active','complete','cancelled')),
winner bigint NOT NULL DEFAULT 0, outcome varchar(32) NOT NULL DEFAULT 'pending');
CREATE TABLE IF NOT EXISTS revora_ctf_members(
run varchar(32) REFERENCES revora_ctf_runs(run), uid bigint CHECK(uid>0),
guild_id bigint NOT NULL CHECK(guild_id>0), contributed boolean NOT NULL DEFAULT false,
PRIMARY KEY(run,uid));
ALTER TABLE revora_ctf_members ADD COLUMN IF NOT EXISTS entered_at timestamptz;
CREATE TABLE IF NOT EXISTS revora_ctf_deliveries(
run varchar(32) NOT NULL, token uuid NOT NULL, uid bigint NOT NULL,
guild_id bigint NOT NULL, PRIMARY KEY(run,token),
FOREIGN KEY(run,uid) REFERENCES revora_ctf_members(run,uid));
ALTER TABLE revora_ctf_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_ctf_runs ADD COLUMN IF NOT EXISTS local_wardrobe boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_ctf_prizes(
run varchar(32) NOT NULL, uid bigint NOT NULL, item_type bigint NOT NULL,
item_uid bigint NOT NULL DEFAULT 0, claimed_at timestamptz,
PRIMARY KEY(run,uid), FOREIGN KEY(run,uid) REFERENCES revora_ctf_members(run,uid),
CHECK((item_uid=0 AND claimed_at IS NULL) OR (item_uid>0 AND claimed_at IS NOT NULL)));
CREATE TABLE IF NOT EXISTS revora_ctf_returns(
uid bigint PRIMARY KEY, run varchar(32) NOT NULL REFERENCES revora_ctf_runs(run),
pkmode integer NOT NULL, requested boolean NOT NULL DEFAULT false,
returned boolean NOT NULL DEFAULT false);
UPDATE revora_ctf_runs SET state='cancelled',outcome='server-restart' WHERE state='active';");
                return true;
            });
        }
        private static T Transaction<T>(Func<NpgsqlConnection,NpgsqlTransaction,T> work)
        {
            using(var c=DataHolder.MySqlConnection) { c.Open(); using(var t=c.BeginTransaction()) {
                T value=work(c,t); t.Commit(); return value;
            }}
        }
        private static NpgsqlCommand Command(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] values)
        {
            var q=new NpgsqlCommand(sql,c,t);
            for(int i=0;i<values.Length;i+=2) {
                var date=values[i+1] as DateTime?;
                if(date.HasValue) {
                    if(date.Value.Kind!=DateTimeKind.Utc)throw new ArgumentException("CTF requires UTC.");
                    q.Parameters.Add((string)values[i],NpgsqlDbType.TimestampTz).Value=date.Value;
                } else q.Parameters.AddWithValue((string)values[i],values[i+1]);
            }
            return q;
        }
        private static int Execute(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] values)
        { using(var q=Command(c,t,sql,values))return q.ExecuteNonQuery(); }
        private static bool Active(NpgsqlConnection c,NpgsqlTransaction t,string run,DateTime utc)
        {
            using(var q=Command(c,t,"SELECT state='active' AND ends_at>@now FROM revora_ctf_runs WHERE run=@run FOR UPDATE","run",run,"now",utc))
                return Equals(q.ExecuteScalar(),true);
        }
        internal static bool Start(string run,DateTime end)
        {
            return Transaction((c,t)=>Execute(c,t,"INSERT INTO revora_ctf_runs(run,ends_at,state,valuable_reward,local_wardrobe) VALUES(@run,@end,'active',true,true) ON CONFLICT DO NOTHING",
                "run",run,"end",end)==1);
        }
        internal static bool Register(string run,uint uid,uint guild,DateTime utc)
        {
            return Transaction((c,t)=> {
                if(uid==0 || guild==0 || !Active(c,t,run,utc))return false;
                Execute(c,t,"INSERT INTO revora_ctf_members(run,uid,guild_id) VALUES(@run,@uid,@guild) ON CONFLICT DO NOTHING",
                    "run",run,"uid",(long)uid,"guild",(long)guild);
                using(var q=Command(c,t,"SELECT guild_id=@guild FROM revora_ctf_members WHERE run=@run AND uid=@uid",
                    "run",run,"uid",(long)uid,"guild",(long)guild))return Equals(q.ExecuteScalar(),true);
            });
        }
        internal static bool ConfirmArrival(string run,GameState p,DateTime utc)
        {
            try {
                if(!Q.Player(p) || p.Guild==null || p.Entity.MapID!=Revora.Game.CaptureTheFlag.MapID
                    || p.Map==null || !ReferenceEquals(p.Map,Kernel.Maps[Revora.Game.CaptureTheFlag.MapID]))return false;
                return Transaction((c,t)=> {
                    using(var q=Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(Convert.ToInt32(q.ExecuteScalar())!=Revora.Game.CaptureTheFlag.MapID)throw new InvalidOperationException("CTF arrival was not saved.");
                    if(!Active(c,t,run,utc))return false;
                    if(Execute(c,t,"UPDATE revora_ctf_members SET entered_at=COALESCE(entered_at,@now) WHERE run=@run AND uid=@uid AND guild_id=@guild",
                        "run",run,"uid",(long)p.Entity.UID,"guild",(long)p.Guild.ID,"now",utc)!=1)throw new InvalidOperationException("CTF arrival membership changed.");
                    return true;
                });
            }catch(Exception ex){Q.Fail(p,"ctf-arrival",ex);return false;}
        }
        internal static bool Contribute(string run,uint uid,uint guild,DateTime utc)
        {
            return Transaction((c,t)=> Active(c,t,run,utc) && Execute(c,t,
                "UPDATE revora_ctf_members SET contributed=true WHERE run=@run AND uid=@uid AND guild_id=@guild",
                "run",run,"uid",(long)uid,"guild",(long)guild)==1);
        }
        internal static bool Deliver(string run,Guid token,uint uid,uint guild,DateTime utc)
        {
            return Transaction((c,t)=> {
                if(!Active(c,t,run,utc))return false;
                if(Execute(c,t,"UPDATE revora_ctf_members SET contributed=true WHERE run=@run AND uid=@uid AND guild_id=@guild",
                    "run",run,"uid",(long)uid,"guild",(long)guild)!=1)return false;
                Execute(c,t,@"INSERT INTO revora_ctf_deliveries(run,token,uid,guild_id) VALUES(@run,@token,@uid,@guild)
ON CONFLICT DO NOTHING","run",run,"token",token,"uid",(long)uid,"guild",(long)guild);
                // A retry after an uncertain commit must agree with the saved receipt.
                using(var q=Command(c,t,"SELECT uid=@uid AND guild_id=@guild FROM revora_ctf_deliveries WHERE run=@run AND token=@token",
                    "run",run,"token",token,"uid",(long)uid,"guild",(long)guild))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("CTF receipt mismatch.");
                ProgressionEvidence.Complete(c,t,uid,10230,"ctf-local-v1",run,1);
                return true;
            });
        }
        internal static Dictionary<uint,int> Scores(string run)
        {
            return Transaction((c,t)=> {
                var scores=new Dictionary<uint,int>();
                using(var q=Command(c,t,"SELECT guild_id,count(*)*10 FROM revora_ctf_deliveries WHERE run=@run GROUP BY guild_id","run",run))
                using(var r=q.ExecuteReader())while(r.Read())scores.Add(Convert.ToUInt32(r[0]),Convert.ToInt32(r[1]));
                return scores;
            });
        }
        internal static bool ReserveReturn(string run,GameState p) {
            return Transaction((c,t)=> {
                // The caller owns this session's item lock; serialize durable re-entry too.
                using(var q=Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("CTF character is missing.");
                return Execute(c,t,@"INSERT INTO revora_ctf_returns(uid,run,pkmode)
VALUES(@uid,@run,@mode) ON CONFLICT(uid) DO UPDATE SET run=EXCLUDED.run,
pkmode=EXCLUDED.pkmode,requested=false,returned=false WHERE revora_ctf_returns.returned",
                    "uid",(long)p.Entity.UID,"run",run,"mode",(int)p.Entity.PKMode)==1;
            });
        }
        internal static void RequestReturn(uint uid) {
            Transaction((c,t)=>Execute(c,t,"UPDATE revora_ctf_returns SET requested=true WHERE uid=@uid AND NOT returned","uid",(long)uid));
        }
        internal static void RetryReturns() {
            var pending=Transaction((c,t)=> {
                var ids=new List<uint>();
                using(var q=Command(c,t,@"SELECT r.uid FROM revora_ctf_returns r JOIN revora_ctf_runs g ON g.run=r.run
WHERE NOT r.returned AND (r.requested OR g.state<>'active')"))
                using(var reader=q.ExecuteReader())while(reader.Read())ids.Add(Convert.ToUInt32(reader[0]));
                return ids;
            });
            foreach(uint uid in pending){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p))Return(p,false);}
        }
        internal static bool Return(GameState p,bool login) {
            if(!Q.Player(p,false) || !System.Threading.Monitor.TryEnter(p.ItemSyncRoot))return false;
            bool virtue=false,pk=false;
            try {
                virtue=System.Threading.Monitor.TryEnter(p.VirtueSyncRoot);if(!virtue)return false;
                pk=System.Threading.Monitor.TryEnter(p.PkSyncRoot);if(!pk)return false;
                string run=null;int mode=0;
                using(var c=DataHolder.MySqlConnection){c.Open();
                    using(var q=Command(c,null,@"SELECT r.run,r.pkmode FROM revora_ctf_returns r JOIN revora_ctf_runs g ON g.run=r.run
WHERE r.uid=@uid AND NOT r.returned AND (@login OR r.requested OR g.state<>'active')","uid",(long)p.Entity.UID,"login",login))
                    using(var r=q.ExecuteReader())if(r.Read()){run=r.GetString(0);mode=Convert.ToInt32(r[1]);}
                }
                if(run==null)return true;
                if(mode<0 || mode>byte.MaxValue || !Enum.IsDefined(typeof(Enums.PKMode),(byte)mode))throw new InvalidOperationException("CTF saved mode invalid.");
                if(!Q.Player(p,false))return false;
                p.Entity.Teleport(1002,408,330);
                if(p.Entity.MapID!=1002 || p.Entity.X!=408 || p.Entity.Y!=330)throw new InvalidOperationException("CTF return destination failed.");
                p.Entity.PKMode=(Enums.PKMode)mode;
                p.Send(new Data(true){UID=p.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)mode});
                p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.CarryingFlag);
                if(!EntityTable.SaveEntity(p))throw new InvalidOperationException("CTF return save failed.");
                Transaction((c,t)=> {
                    if(Execute(c,t,"UPDATE revora_ctf_returns SET returned=true WHERE uid=@uid AND run=@run AND NOT returned","uid",(long)p.Entity.UID,"run",run)!=1)
                        throw new InvalidOperationException("CTF return acknowledgement failed.");
                    return true;
                });
                return true;
            }catch(Exception ex){Q.Fail(p,"ctf-return",ex);return false;}
            finally{if(pk)System.Threading.Monitor.Exit(p.PkSyncRoot);if(virtue)System.Threading.Monitor.Exit(p.VirtueSyncRoot);System.Threading.Monitor.Exit(p.ItemSyncRoot);}
        }
        internal static string Claim(GameState p) {
            if(!Q.Near(p,380,1002) || !SessionInstall.TryEnter(p.Entity.UID))return "Meet me at the Guild Controller to claim your prize.";
            try { lock(p.ItemSyncRoot) {
                if(!Q.Near(p,380,1002))return "Please try again at the Guild Controller.";
                ConquerItem item;
                using(var c=DataHolder.MySqlConnection) { c.Open(); using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    string run; uint type;
                    using(var q=Command(c,t,@"SELECT p.run,p.item_type FROM revora_ctf_prizes p
JOIN revora_ctf_runs r ON r.run=p.run WHERE p.uid=@uid AND p.claimed_at IS NULL AND r.state='complete'
ORDER BY r.ends_at,p.run LIMIT 1 FOR UPDATE OF p","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read())return "No unclaimed Capture the Flag prize awaits you.";
                        run=r.GetString(0);type=Convert.ToUInt32(r[1]);
                    }
                    if(p.Inventory.Count>=40)return "Make one space in your bag. Your prize will wait.";
                    using(var q=Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt32(q.ExecuteScalar())>=40)return "Make one space in your bag. Your prize will wait.";
                    if(!ConquerItemInformation.BaseInformations.ContainsKey(type)
                        || type<800000 || type>=900000 || ConquerItemInformation.BaseInformations[type].PurificationLevel!=7)
                        throw new InvalidOperationException("CTF saved soul catalog mismatch.");
                    item=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,item);
                    if(Execute(c,t,"UPDATE revora_ctf_prizes SET item_uid=@item,claimed_at=@now WHERE run=@run AND uid=@uid AND claimed_at IS NULL",
                        "item",(long)item.UID,"now",DateTime.UtcNow,"run",run,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("CTF prize claim was not persisted.");
                    if(!Q.Near(p,380,1002))throw new InvalidOperationException("CTF claimant changed.");
                    t.Commit();
                }}
                Q.Install(p,new uint[0],new[]{item});
                return "A bound P7 soul for your part in our victory. Well fought.";
            }} catch(Exception ex) { Q.Fail(p,"ctf-prize",ex);return "Your prize is safe. Reconnect before trying again."; }
            finally { SessionInstall.Exit(p.Entity.UID); }
        }
        internal static Result Finish(string run,DateTime utc)
        {
            return Transaction((c,t)=> {
                var result=new Result(); bool valuable,wardrobe;
                using(var q=Command(c,t,"SELECT state,winner,outcome,ends_at<=@now,valuable_reward,local_wardrobe FROM revora_ctf_runs WHERE run=@run FOR UPDATE","run",run,"now",utc))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read())throw new InvalidOperationException("Missing CTF run.");
                    result.Winner=Convert.ToUInt32(r[1]);result.Outcome=(string)r[2];
                    if((string)r[0]!="active")return result;
                    if(!r.GetBoolean(3))throw new InvalidOperationException("CTF is still running.");
                    valuable=r.GetBoolean(4);wardrobe=r.GetBoolean(5);
                }
                var scores=new Dictionary<uint,int>();
                var members=new Dictionary<uint,uint>();
                using(var q=Command(c,t,"SELECT uid,guild_id FROM revora_ctf_members WHERE run=@run AND contributed","run",run))
                using(var r=q.ExecuteReader())while(r.Read())members.Add(Convert.ToUInt32(r[0]),Convert.ToUInt32(r[1]));
                using(var q=Command(c,t,"SELECT guild_id,count(*) FROM revora_ctf_deliveries WHERE run=@run GROUP BY guild_id","run",run))
                using(var r=q.ExecuteReader())while(r.Read())scores.Add(Convert.ToUInt32(r[0]),Convert.ToInt32(r[1]));
                var ordered=scores.OrderByDescending(p=>p.Value).ToArray();
                result.Outcome=members.Values.Distinct().Count()<2?"uncontested":ordered.Length==0?"no-score":
                    ordered.Length>1 && ordered[0].Value==ordered[1].Value?"tie":"winner";
                if(result.Outcome=="winner") {
                    result.Winner=ordered[0].Key;
                    if(Execute(c,t,"INSERT INTO revora_title_ctf(id,guild_id) VALUES(1,@guild) ON CONFLICT(id) DO UPDATE SET guild_id=EXCLUDED.guild_id","guild",(long)result.Winner)!=1)
                        throw new InvalidOperationException("CTF champion was not persisted.");
                    // Snapshot the actual saved winning leader at finalization. A
                    // successor cannot inherit a past permanent tournament award.
                    if(wardrobe) {
                        string leaderName;
                        using(var q=Command(c,t,"SELECT leadername FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)result.Winner))
                            leaderName=q.ExecuteScalar() as string;
                        var leaders=new List<uint>();
                        using(var q=Command(c,t,"SELECT uid FROM entities WHERE guildid=@guild AND guildrank=@rank AND name=@name FOR SHARE",
                            "guild",(long)result.Winner,"rank",(int)Enums.GuildMemberRank.GuildLeader,"name",leaderName??""))
                        using(var r=q.ExecuteReader())while(r.Read())leaders.Add(Convert.ToUInt32(r[0]));
                        if(leaders.Count>1)throw new InvalidOperationException("CTF winning leadership is ambiguous.");
                        if(leaders.Count==1 && members.ContainsKey(leaders[0]) && members[leaders[0]]==result.Winner)
                            foreach(int key in new[]{20030004,60020021})
                                if(!ProgressionEvidence.Grant(c,t,leaders[0],"ctf-local-wardrobe-v1",run+":"+key,key,utc))
                                    throw new InvalidOperationException("CTF wardrobe reward was not saved.");
                    }
                    foreach(var member in members.Where(p=>p.Value==result.Winner).OrderBy(p=>p.Key)) {
                        ProgressionEvidence.Complete(c,t,member.Key,10301,"ctf-local-v1",run,1);
                        if(valuable && Execute(c,t,"INSERT INTO revora_ctf_prizes(run,uid,item_type) VALUES(@run,@uid,@type)",
                            "run",run,"uid",(long)member.Key,"type",(long)QuestSoulReward.Create().ID)!=1)
                            throw new InvalidOperationException("CTF prize entitlement was not persisted.");
                    }
                }
                if(Execute(c,t,"UPDATE revora_ctf_runs SET state='complete',winner=@winner,outcome=@outcome WHERE run=@run AND state='active'",
                    "run",run,"winner",(long)result.Winner,"outcome",result.Outcome)!=1)
                    throw new InvalidOperationException("CTF result was not persisted.");
                return result;
            });
        }
    }
}
