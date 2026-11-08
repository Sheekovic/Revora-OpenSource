using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class CouplePkResults
    {
        internal sealed class Standing {internal Guid Pair;internal bool Alive;internal long Damage;}
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_cpk_runs(
id varchar(32) PRIMARY KEY,starts_at timestamptz NOT NULL,join_until timestamptz NOT NULL,deadline timestamptz NOT NULL,
state varchar(16) NOT NULL CHECK(state IN ('open','complete','cancelled')),winner uuid,ended_at timestamptz,expires_at timestamptz);
CREATE TABLE IF NOT EXISTS revora_cpk_pairs(id uuid PRIMARY KEY,run varchar(32) NOT NULL REFERENCES revora_cpk_runs(id),
husband bigint NOT NULL,wife bigint NOT NULL,CHECK(husband<>wife),admitted_at timestamptz,competitive boolean NOT NULL DEFAULT false,
alive boolean NOT NULL DEFAULT false,damage bigint NOT NULL DEFAULT 0 CHECK(damage>=0));
CREATE TABLE IF NOT EXISTS revora_cpk_entries(run varchar(32) REFERENCES revora_cpk_runs(id),uid bigint NOT NULL,
pair uuid NOT NULL REFERENCES revora_cpk_pairs(id),PRIMARY KEY(run,uid));
CREATE TABLE IF NOT EXISTS revora_cpk_claims(run varchar(32) REFERENCES revora_cpk_runs(id),uid bigint NOT NULL,
rewards bigint[] NOT NULL,claimed_at timestamptz NOT NULL,PRIMARY KEY(run,uid));
CREATE TABLE IF NOT EXISTS revora_cpk_returns(
run varchar(32) NOT NULL,uid bigint NOT NULL,arena integer NOT NULL CHECK(arena>0),prior_mode integer NOT NULL,
returned boolean NOT NULL DEFAULT false,PRIMARY KEY(run,uid),FOREIGN KEY(run,uid) REFERENCES revora_cpk_entries(run,uid));
CREATE INDEX IF NOT EXISTS revora_cpk_pending_return ON revora_cpk_returns(uid) WHERE NOT returned;
CREATE TABLE IF NOT EXISTS revora_cpk_legacy_status(uid bigint NOT NULL,old_time numeric(20,0) NOT NULL,PRIMARY KEY(uid,old_time));
INSERT INTO revora_cpk_legacy_status SELECT entityid,time FROM status WHERE status=8 ON CONFLICT DO NOTHING;
ALTER TABLE revora_cpk_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
UPDATE revora_cpk_runs SET state='cancelled',ended_at=CURRENT_TIMESTAMP WHERE state='open';");}
        }
        private static void ReturnOwner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            // Arena transitions are serialized by TournamentCombat.Gate, not the
            // inventory lock. This transaction changes only location receipts.
            if(!Q.Player(p,false))throw new InvalidOperationException("PK return owner unavailable.");
            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                if(q.ExecuteScalar()==null || !Q.Player(p,false))throw new InvalidOperationException("PK return owner changed.");
        }
        internal static void PrepareReturn(string run,GameState p,ushort arena,Enums.PKMode mode) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                ReturnOwner(c,t,p);
                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_cpk_returns WHERE uid=@uid AND run<>@run AND NOT returned","uid",(long)p.Entity.UID,"run",run))
                    if(Convert.ToInt64(q.ExecuteScalar())!=0)throw new InvalidOperationException("Prior Couple PK return is pending.");
                if(Q.Execute(c,t,@"INSERT INTO revora_cpk_returns(run,uid,arena,prior_mode)
SELECT @run,@uid,@arena,@mode WHERE EXISTS(SELECT 1 FROM revora_cpk_entries e JOIN revora_cpk_runs w ON w.id=e.run JOIN revora_cpk_pairs p ON p.id=e.pair WHERE e.run=@run AND e.uid=@uid AND w.state='open' AND (p.admitted_at IS NULL OR EXISTS(SELECT 1 FROM revora_cpk_returns r WHERE r.run=e.run AND r.uid=e.uid AND NOT r.returned)))
ON CONFLICT(run,uid) DO UPDATE SET arena=EXCLUDED.arena,returned=false,prior_mode=CASE WHEN revora_cpk_returns.returned THEN EXCLUDED.prior_mode ELSE revora_cpk_returns.prior_mode END
WHERE revora_cpk_returns.returned OR revora_cpk_returns.prior_mode=EXCLUDED.prior_mode",
                    "run",run,"uid",(long)p.Entity.UID,"arena",(int)arena,"mode",(int)mode)!=1)throw new InvalidOperationException("Couple PK return reservation failed.");
                t.Commit();
            }}
        }
        internal static void CompleteReturn(string run,GameState p) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                ReturnOwner(c,t,p);
                using(var q=Q.Command(c,t,"SELECT arena FROM revora_cpk_returns WHERE run=@run AND uid=@uid AND NOT returned FOR UPDATE","run",run,"uid",(long)p.Entity.UID)){
                    object arena=q.ExecuteScalar();if(arena==null){t.Commit();return;}
                    if(Convert.ToUInt16(arena)==p.Entity.MapID)throw new InvalidOperationException("PK return still in arena.");
                }
                if(Q.Execute(c,t,@"UPDATE revora_cpk_returns r SET returned=true WHERE r.run=@run AND r.uid=@uid AND NOT returned
AND EXISTS(SELECT 1 FROM entities e WHERE e.uid=r.uid AND e.mapid=@map AND e.x=@x AND e.y=@y)",
                    "run",run,"uid",(long)p.Entity.UID,"map",(int)p.Entity.MapID,"x",(int)p.Entity.X,"y",(int)p.Entity.Y)!=1)throw new InvalidOperationException("PK return destination was not saved.");
                t.Commit();
            }}
        }
        internal static void RecoverReturn(GameState p) {
            string run=null;int mode=0;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT run,prior_mode FROM revora_cpk_returns WHERE uid=@uid AND NOT returned ORDER BY run DESC LIMIT 1","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(r.Read()){run=r.GetString(0);mode=r.GetInt32(1);}}
            if(run==null)return;
            if(mode<0 || mode>byte.MaxValue || !Enum.IsDefined(typeof(Enums.PKMode),(byte)mode))throw new InvalidOperationException("Invalid PK return mode.");
            p.Entity.OnDeath=null;p.Entity.Teleport(1002,408,330);p.Entity.BringToLife();
            p.Entity.PKMode=(Enums.PKMode)mode;p.Send(new Data(true){UID=p.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)mode});
            if(p.Entity.MapID!=1002 || p.Entity.X!=408 || p.Entity.Y!=330 || !EntityTable.SaveEntity(p))throw new InvalidOperationException("PK login return was not saved.");
            CompleteReturn(run,p);
        }
        private static string Lock(NpgsqlConnection c,NpgsqlTransaction t,string run) {
            using(var q=Q.Command(c,t,"SELECT state FROM revora_cpk_runs WHERE id=@run FOR UPDATE","run",run))return q.ExecuteScalar() as string;
        }
        internal static bool Open(string run,DateTime start) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"INSERT INTO revora_cpk_runs(id,starts_at,join_until,deadline,state,valuable_reward) VALUES(@run,@start,@join,@end,'open',true) ON CONFLICT DO NOTHING",
                    "run",run,"start",start,"join",start.AddMinutes(9),"end",start.AddHours(1));
                using(var q=Q.Command(c,t,"SELECT state='open' AND starts_at=@start FROM revora_cpk_runs WHERE id=@run","run",run,"start",start)) {bool ok=Equals(q.ExecuteScalar(),true);t.Commit();return ok;}
            }}
        }
        private static bool Marriage(NpgsqlConnection c,NpgsqlTransaction t,uint husband,uint wife) {
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM entities a JOIN entities b ON b.uid=@wife
WHERE a.uid=@husband AND a.name<>b.name AND a.spouse=b.name AND b.spouse=a.name AND a.spouse<>'None' AND b.spouse<>'None')",
                "husband",(long)husband,"wife",(long)wife))return Equals(q.ExecuteScalar(),true);
        }
        internal static Guid Reservation(string run,uint husband,uint wife) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT id FROM revora_cpk_pairs WHERE run=@run AND husband=@husband AND wife=@wife AND admitted_at IS NULL",
                "run",run,"husband",(long)husband,"wife",(long)wife)){var value=q.ExecuteScalar();return value is Guid?(Guid)value:Guid.NewGuid();}}
        }
        internal static bool Reserve(string run,Guid pair,uint husband,uint wife,DateTime now) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                if(husband==wife || Lock(c,t,run)!="open")return false;
                using(var q=Q.Command(c,t,"SELECT starts_at<=@now AND join_until>@now FROM revora_cpk_runs WHERE id=@run","run",run,"now",now))if(!Equals(q.ExecuteScalar(),true))return false;
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=ANY(@ids) ORDER BY uid FOR UPDATE","ids",new[]{(long)husband,(long)wife}))using(var r=q.ExecuteReader()) {int count=0;while(r.Read())count++;if(count!=2)return false;}
                if(!Marriage(c,t,husband,wife))return false;
                int existing;using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_cpk_entries WHERE run=@run AND uid=ANY(@ids)","run",run,"ids",new[]{(long)husband,(long)wife}))existing=Convert.ToInt32(q.ExecuteScalar());
                if(existing!=0) {
                    if(existing!=2)return false;
                    using(var q=Q.Command(c,t,"SELECT admitted_at IS NULL FROM revora_cpk_pairs WHERE id=@pair AND run=@run AND husband=@husband AND wife=@wife",
                        "pair",pair,"run",run,"husband",(long)husband,"wife",(long)wife)){bool retry=Equals(q.ExecuteScalar(),true);t.Commit();return retry;}
                }
                using(var q=Q.Command(c,t,"SELECT count(*)<32 FROM revora_cpk_pairs WHERE run=@run","run",run))if(!Equals(q.ExecuteScalar(),true))return false;
                Q.Execute(c,t,"INSERT INTO revora_cpk_pairs(id,run,husband,wife) VALUES(@pair,@run,@husband,@wife)","pair",pair,"run",run,"husband",(long)husband,"wife",(long)wife);
                foreach(uint uid in new[]{husband,wife})Q.Execute(c,t,"INSERT INTO revora_cpk_entries(run,uid,pair) VALUES(@run,@uid,@pair)","run",run,"uid",(long)uid,"pair",pair);
                t.Commit();return true;
            }}
        }
        internal static void Admit(string run,Guid pair,DateTime now) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                if(Lock(c,t,run)!="open")throw new InvalidOperationException("Couple PK closed during admission.");
                using(var q=Q.Command(c,t,"SELECT starts_at<=@now AND join_until>@now FROM revora_cpk_runs WHERE id=@run","run",run,"now",now))if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Couple PK admission ended.");
                if(Q.Execute(c,t,"UPDATE revora_cpk_pairs SET admitted_at=COALESCE(admitted_at,@now),alive=true WHERE run=@run AND id=@pair","run",run,"pair",pair,"now",now)!=1)throw new InvalidOperationException("Couple PK pair missing.");t.Commit();
            }}
        }
        internal static void Competitive(string run,IEnumerable<Guid> pairs) {
            var ids=pairs.Distinct().ToArray();if(ids.Length<2)return;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                if(Lock(c,t,run)!="open")return;
                var players=new List<uint>();using(var q=Q.Command(c,t,"SELECT husband,wife FROM revora_cpk_pairs WHERE run=@run AND id=ANY(@ids) AND admitted_at IS NOT NULL","run",run,"ids",ids))
                using(var r=q.ExecuteReader())while(r.Read()){players.Add((uint)r.GetInt64(0));players.Add((uint)r.GetInt64(1));}
                if(players.Count!=ids.Length*2)throw new InvalidOperationException("Couple PK field includes an unadmitted pair.");
                foreach(uint uid in players)ProgressionEvidence.Complete(c,t,uid,11115,"couple-pk-npc-v1",run,1);
                Q.Execute(c,t,"UPDATE revora_cpk_pairs SET competitive=true WHERE run=@run AND id=ANY(@ids)","run",run,"ids",ids);t.Commit();
            }}
        }
        internal static bool Finish(string run,Guid winner,IEnumerable<Standing> standings,DateTime now) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                string state=Lock(c,t,run);if(state=="cancelled")return false;
                if(state=="complete"){using(var q=Q.Command(c,t,"SELECT winner=@winner FROM revora_cpk_runs WHERE id=@run","winner",winner,"run",run))return Equals(q.ExecuteScalar(),true);}
                if(state!="open")throw new InvalidOperationException("Couple PK result has no open run.");
                foreach(var p in standings)Q.Execute(c,t,"UPDATE revora_cpk_pairs SET alive=@alive,damage=@damage WHERE run=@run AND id=@pair","run",run,"pair",p.Pair,"alive",p.Alive,"damage",p.Damage);
                uint a=0,b=0;bool earned=false;
                if(winner!=Guid.Empty)using(var q=Q.Command(c,t,@"SELECT p.husband,p.wife FROM revora_cpk_pairs p JOIN revora_cpk_runs r ON r.id=p.run
WHERE p.run=@run AND p.id=@pair AND p.admitted_at IS NOT NULL AND p.competitive AND p.alive AND p.damage>0
AND r.starts_at+interval '10 minutes'<=@now AND r.deadline>=@now
AND (SELECT count(*) FROM revora_cpk_pairs WHERE run=@run AND admitted_at IS NOT NULL)>=2
AND (SELECT count(*) FROM revora_cpk_pairs WHERE run=@run AND alive)=1","run",run,"pair",winner,"now",now))
                using(var r=q.ExecuteReader())if(r.Read()){a=(uint)r.GetInt64(0);b=(uint)r.GetInt64(1);}
                if(a!=0 && Marriage(c,t,a,b)) {
                    ProgressionEvidence.Complete(c,t,a,11121,"couple-pk-npc-v1",run,1);ProgressionEvidence.Complete(c,t,b,11121,"couple-pk-npc-v1",run,1);earned=true;
                }
                Q.Execute(c,t,"UPDATE revora_cpk_runs SET state='complete',winner=@winner,ended_at=@now,expires_at=@expiry WHERE id=@run",
                    "run",run,"winner",earned?(object)winner:DBNull.Value,"now",now,"expiry",earned?(object)now.AddDays(7):DBNull.Value);t.Commit();return earned;
            }}
        }
        internal static DateTime? Honor(uint uid,DateTime now) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT max(r.expires_at) FROM revora_cpk_runs r
JOIN revora_cpk_pairs p ON p.id=r.winner WHERE r.state='complete' AND (p.husband=@uid OR p.wife=@uid) AND r.ended_at<=@now AND r.expires_at>@now","uid",(long)uid,"now",now))
            using(var r=q.ExecuteReader()){if(!r.Read() || r.IsDBNull(0))return null;return r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}}
        }
        internal static string Claim(GameState p,DateTime now) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Couple PK winner missing.");
                string run=null;bool valuable=false;using(var q=Q.Command(c,t,@"SELECT r.id,r.valuable_reward FROM revora_cpk_runs r JOIN revora_cpk_pairs p ON p.id=r.winner
WHERE r.state='complete' AND (p.husband=@uid OR p.wife=@uid) AND NOT EXISTS(SELECT 1 FROM revora_cpk_claims c WHERE c.run=r.id AND c.uid=@uid)
ORDER BY r.ended_at,r.id LIMIT 1 FOR UPDATE OF r","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())if(r.Read()){run=r.GetString(0);valuable=r.GetBoolean(1);}
                if(run==null)return "No unclaimed Couple PK prize is waiting for you.";
                int slots=valuable?3:2;if(p.Inventory.Count+slots>40)return "Make "+slots+" places for your prizes. They will wait.";
                long cp=(long)p.Entity.ConquerPoints+50000,rvp=(long)p.Entity.RevoraPoint+500;decimal gold=p.Entity.Money+500000m;
                if(cp>uint.MaxValue || rvp>uint.MaxValue || gold>Revora.Game.Entity.MaximumSilver)return "Spend some saved funds before claiming your purse.";
                SilverBalance.AssertSaved(c,t,p);
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@gold WHERE uid=@uid AND conquerpoints=@oldcp AND revorapoint=@oldrvp",
                    "cp",cp,"rvp",rvp,"gold",gold,"uid",(long)p.Entity.UID,"oldcp",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint)!=1)throw new InvalidOperationException("Couple PK balance changed.");
                var rewards=new List<ConquerItem>{Q.New(722136),Q.New(722136)};if(valuable)rewards.Add(QuestSoulReward.Create());var items=rewards.ToArray();foreach(var item in items)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"INSERT INTO revora_cpk_claims(run,uid,rewards,claimed_at) VALUES(@run,@uid,@items,@now)","run",run,"uid",(long)p.Entity.UID,"items",items.Select(i=>(long)i.UID).ToArray(),"now",now)!=1)throw new InvalidOperationException("Couple PK claim was not saved.");
                t.Commit();p.Entity.ApplyCommittedConquerPoints((uint)cp);p.Entity.ApplyCommittedRevoraPoints((uint)rvp);SilverBalance.ApplyCommitted(p,(ulong)gold);Q.Install(p,new uint[0],items);return valuable?"Your champion's purse and bound P7 soul are yours. Stand by each other, in battle and beyond.":"Your champion's purse is yours. Stand by each other, in battle and beyond.";
            }}
        }
    }
}
