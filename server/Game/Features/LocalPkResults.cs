using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Rules=Revora.Game.Features.LocalPkRules;

namespace Revora.Game.Features
{
    internal static class LocalPkResults
    {
        internal sealed class Standing {internal uint Uid;internal long Damage;internal bool Alive;}
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_pk_wars(
run varchar(40) PRIMARY KEY,kind integer NOT NULL CHECK(kind IN (0,1)),bracket integer NOT NULL CHECK(bracket BETWEEN 0 AND 3),
starts_at timestamptz NOT NULL,join_until timestamptz NOT NULL,deadline timestamptz NOT NULL,state varchar(16) NOT NULL CHECK(state IN ('open','complete','cancelled')),
winner bigint NOT NULL DEFAULT 0,ended_at timestamptz,expires_at timestamptz);
CREATE TABLE IF NOT EXISTS revora_pk_entries(run varchar(40) REFERENCES revora_pk_wars(run),uid bigint NOT NULL,level integer NOT NULL,
admitted_at timestamptz,competitive boolean NOT NULL DEFAULT false,alive boolean NOT NULL DEFAULT false,damage bigint NOT NULL DEFAULT 0 CHECK(damage>=0),PRIMARY KEY(run,uid));
CREATE TABLE IF NOT EXISTS revora_pk_claims(run varchar(40) PRIMARY KEY REFERENCES revora_pk_wars(run),uid bigint NOT NULL,rewards bigint[] NOT NULL,
cp bigint NOT NULL,rvp bigint NOT NULL,gold numeric(20,0) NOT NULL,claimed_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_pk_legacy_status(uid bigint NOT NULL,kind integer NOT NULL,old_time numeric(20,0) NOT NULL,PRIMARY KEY(uid,kind,old_time));
INSERT INTO revora_pk_legacy_status(uid,kind,old_time) SELECT entityid,status,time FROM status WHERE status IN (1,2,3,4,5,6,7,8,20,21,22,34,203) ON CONFLICT DO NOTHING;
ALTER TABLE revora_pk_wars DROP CONSTRAINT IF EXISTS revora_pk_wars_kind_check;
ALTER TABLE revora_pk_wars ADD CONSTRAINT revora_pk_wars_kind_check CHECK(kind IN (0,1) OR kind BETWEEN 10 AND 22);
ALTER TABLE revora_pk_wars ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_pk_entries ADD COLUMN IF NOT EXISTS class_id integer NOT NULL DEFAULT 0;
UPDATE revora_pk_wars SET state='cancelled',ended_at=CURRENT_TIMESTAMP WHERE state='open';");}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_pk_returns(
run varchar(40) NOT NULL,uid bigint NOT NULL,arena integer NOT NULL CHECK(arena>0),prior_mode integer NOT NULL,
returned boolean NOT NULL DEFAULT false,PRIMARY KEY(run,uid),FOREIGN KEY(run,uid) REFERENCES revora_pk_entries(run,uid));
CREATE INDEX IF NOT EXISTS revora_pk_pending_return ON revora_pk_returns(uid) WHERE NOT returned;");}
            BattlePowerTournament.Initialize();
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
                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_pk_returns WHERE uid=@uid AND run<>@run AND NOT returned","uid",(long)p.Entity.UID,"run",run))
                    if(Convert.ToInt64(q.ExecuteScalar())!=0)throw new InvalidOperationException("Prior PK return is pending.");
                if(Q.Execute(c,t,@"INSERT INTO revora_pk_returns(run,uid,arena,prior_mode)
SELECT @run,@uid,@arena,@mode WHERE EXISTS(SELECT 1 FROM revora_pk_entries e JOIN revora_pk_wars w ON w.run=e.run WHERE e.run=@run AND e.uid=@uid AND w.state='open' AND (w.kind IN(0,1) OR w.kind BETWEEN 10 AND 19))
ON CONFLICT(run,uid) DO UPDATE SET arena=EXCLUDED.arena WHERE NOT revora_pk_returns.returned AND revora_pk_returns.prior_mode=EXCLUDED.prior_mode",
                    "run",run,"uid",(long)p.Entity.UID,"arena",(int)arena,"mode",(int)mode)!=1)throw new InvalidOperationException("PK return reservation failed.");
                t.Commit();
            }}
        }
        internal static void CompleteReturn(string run,GameState p) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                ReturnOwner(c,t,p);
                using(var q=Q.Command(c,t,"SELECT arena FROM revora_pk_returns WHERE run=@run AND uid=@uid AND NOT returned FOR UPDATE","run",run,"uid",(long)p.Entity.UID)){
                    object arena=q.ExecuteScalar();if(arena==null){t.Commit();return;}
                    if(Convert.ToUInt16(arena)==p.Entity.MapID)throw new InvalidOperationException("PK return still in arena.");
                }
                if(Q.Execute(c,t,@"UPDATE revora_pk_returns r SET returned=true WHERE r.run=@run AND r.uid=@uid AND NOT returned
AND EXISTS(SELECT 1 FROM entities e WHERE e.uid=r.uid AND e.mapid=@map AND e.x=@x AND e.y=@y)",
                    "run",run,"uid",(long)p.Entity.UID,"map",(int)p.Entity.MapID,"x",(int)p.Entity.X,"y",(int)p.Entity.Y)!=1)throw new InvalidOperationException("PK return destination was not saved.");
                t.Commit();
            }}
        }
        internal static void RecoverReturn(GameState p) {
            string run=null;int mode=0;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT run,prior_mode FROM revora_pk_returns WHERE uid=@uid AND NOT returned ORDER BY run DESC LIMIT 1","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(r.Read()){run=r.GetString(0);mode=r.GetInt32(1);}}
            if(run==null)return;
            if(mode<0 || mode>byte.MaxValue || !Enum.IsDefined(typeof(Enums.PKMode),(byte)mode))throw new InvalidOperationException("Invalid PK return mode.");
            p.Entity.OnDeath=null;p.Entity.Teleport(1002,408,330);p.Entity.BringToLife();
            p.Entity.PKMode=(Enums.PKMode)mode;p.Send(new Data(true){UID=p.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)mode});
            if(p.Entity.MapID!=1002 || p.Entity.X!=408 || p.Entity.Y!=330 || !EntityTable.SaveEntity(p))throw new InvalidOperationException("PK login return was not saved.");
            CompleteReturn(run,p);
        }
        private static string Lock(NpgsqlConnection c,NpgsqlTransaction t,string run) {
            using(var q=Q.Command(c,t,"SELECT state FROM revora_pk_wars WHERE run=@run FOR UPDATE","run",run))return q.ExecuteScalar() as string;
        }
        internal static bool Open(string run,int kind,int bracket,DateTime start) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"INSERT INTO revora_pk_wars(run,kind,bracket,starts_at,join_until,deadline,state,valuable_reward,bp_valuable_reward) VALUES(@run,@kind,@bracket,@start,@join,@end,'open',true,@bp) ON CONFLICT DO NOTHING",
                    "run",run,"bp",Rules.BpWar(kind),"kind",kind,"bracket",bracket,"start",start,"join",start.AddMinutes(Rules.JoinMinutes(kind)),"end",start.AddMinutes(Rules.Duration(kind)));
                using(var q=Q.Command(c,t,"SELECT state='open' AND kind=@kind AND bracket=@bracket AND starts_at=@start FROM revora_pk_wars WHERE run=@run",
                    "run",run,"kind",kind,"bracket",bracket,"start",start)){bool ok=Equals(q.ExecuteScalar(),true);t.Commit();return ok;}
            }}
        }
        internal static bool Register(string run,uint uid,int level,DateTime now,int classId=0) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                if(Lock(c,t,run)!="open")return false;
                using(var q=Q.Command(c,t,"SELECT starts_at<=@now AND join_until>@now AND bracket=@bracket AND (kind IN (0,1) OR kind=@class) FROM revora_pk_wars WHERE run=@run","run",run,"now",now,"bracket",LocalPkWar.Bracket(level),"class",Rules.ClassKind(classId)))if(level<1 || !Equals(q.ExecuteScalar(),true))return false;
                using(var q=Q.Command(c,t,"SELECT admitted_at IS NULL AND level=@level AND class_id=@class FROM revora_pk_entries WHERE run=@run AND uid=@uid","run",run,"uid",(long)uid,"level",level,"class",classId)) {
                    var prior=q.ExecuteScalar();if(prior!=null){t.Commit();return Equals(prior,true);}
                }
                using(var q=Q.Command(c,t,"SELECT count(*)<64 FROM revora_pk_entries WHERE run=@run","run",run))if(!Equals(q.ExecuteScalar(),true))return false;
                Q.Execute(c,t,"INSERT INTO revora_pk_entries(run,uid,level,class_id) VALUES(@run,@uid,@level,@class)","run",run,"uid",(long)uid,"level",level,"class",classId);t.Commit();return true;
            }}
        }
        internal static void Admit(string run,uint uid,DateTime now) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                if(Lock(c,t,run)!="open")throw new InvalidOperationException("PK run closed during admission.");
                using(var q=Q.Command(c,t,"SELECT starts_at<=@now AND join_until>@now FROM revora_pk_wars WHERE run=@run","run",run,"now",now))if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("PK admission window closed.");
                if(Q.Execute(c,t,"UPDATE revora_pk_entries SET admitted_at=COALESCE(admitted_at,@now),alive=true WHERE run=@run AND uid=@uid","now",now,"run",run,"uid",(long)uid)!=1)throw new InvalidOperationException("PK entrant missing.");t.Commit();
            }}
        }
        internal static void Competitive(string run,int kind,IEnumerable<uint> participants) {
            var ids=participants.Distinct().Select(i=>(long)i).ToArray();if(ids.Length<2)return;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                if(Lock(c,t,run)!="open")return;
                using(var q=Q.Command(c,t,"SELECT kind=@kind FROM revora_pk_wars WHERE run=@run","run",run,"kind",kind))if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("PK field kind mismatch.");
                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_pk_entries WHERE run=@run AND uid=ANY(@ids) AND admitted_at IS NOT NULL","run",run,"ids",ids))
                    if(Convert.ToInt32(q.ExecuteScalar())!=ids.Length)throw new InvalidOperationException("PK field includes an unadmitted entrant.");
                foreach(long uid in ids) {
                    if(kind==0)ProgressionEvidence.Complete(c,t,(uint)uid,11116,"weekly-pk-v1",run,1);
                    else if(kind==1)ProgressionEvidence.Complete(c,t,(uint)uid,11117,"monthly-pk-v1",run,1);
                    else if(Rules.BpWar(kind))ProgressionEvidence.Complete(c,t,(uint)uid,11114,"bp-pk-v1",run,1);
                }
                Q.Execute(c,t,"UPDATE revora_pk_entries SET competitive=true WHERE run=@run AND uid=ANY(@ids)","run",run,"ids",ids);t.Commit();
            }}
        }
        internal static bool Finish(string run,int kind,uint winner,IEnumerable<Standing> standings,DateTime now,bool cancelled) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                string state=Lock(c,t,run);if(state=="cancelled")return false;
                if(state=="complete"){using(var q=Q.Command(c,t,"SELECT winner=@uid AND winner<>0 FROM revora_pk_wars WHERE run=@run","run",run,"uid",(long)winner))return Equals(q.ExecuteScalar(),true);}
                if(state!="open")throw new InvalidOperationException("PK result has no open run.");
                foreach(var entry in standings)Q.Execute(c,t,"UPDATE revora_pk_entries SET alive=@alive,damage=@damage WHERE run=@run AND uid=@uid",
                    "alive",entry.Alive,"damage",entry.Damage,"run",run,"uid",(long)entry.Uid);
                bool earned=false;
                if(!cancelled && winner!=0)using(var q=Q.Command(c,t,@"SELECT
(SELECT count(*) FROM revora_pk_entries WHERE run=@run AND admitted_at IS NOT NULL)>=2
AND (SELECT count(*) FROM revora_pk_entries WHERE run=@run AND alive)=1
AND EXISTS(SELECT 1 FROM revora_pk_entries WHERE run=@run AND uid=@uid AND alive AND competitive AND damage>0)
AND EXISTS(SELECT 1 FROM revora_pk_wars WHERE run=@run AND kind=@kind AND starts_at+@minimum*INTERVAL '1 minute'<=@now AND deadline>=@now)",
                    "run",run,"uid",(long)winner,"kind",kind,"now",now,"minimum",Rules.WinMinutes(kind)))earned=Equals(q.ExecuteScalar(),true);
                if(earned) {
                    if(kind==0)ProgressionEvidence.Complete(c,t,winner,11118,"weekly-pk-v1",run,1);
                    else if(kind==1)ProgressionEvidence.Complete(c,t,winner,11119,"monthly-pk-v1",run,1);
                    else if(Rules.ClassWar(kind))ProgressionEvidence.Complete(c,t,winner,11131,"class-pk-v1",run,1);
                }
                Q.Execute(c,t,"UPDATE revora_pk_wars SET state=@state,winner=@winner,ended_at=@now,expires_at=@expiry WHERE run=@run",
                    "run",run,"state",cancelled?"cancelled":"complete","winner",earned?(long)winner:0L,"now",now,"expiry",earned?(object)now.AddDays(Rules.Days(kind)):DBNull.Value);
                t.Commit();return earned;
            }}
        }
        internal static Dictionary<int,DateTime> Honors(uint uid,DateTime now) {
            var result=new Dictionary<int,DateTime>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT kind,max(expires_at) FROM revora_pk_wars
WHERE winner=@uid AND kind NOT BETWEEN 20 AND 22 AND state='complete' AND ended_at<=@now AND expires_at>@now GROUP BY kind","uid",(long)uid,"now",now))
            using(var r=q.ExecuteReader())while(r.Read())result.Add(r.GetInt32(0),r.GetFieldValue<DateTimeOffset>(1).UtcDateTime);}return result;
        }
        internal static string Claim(GameState p,DateTime now) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("PK winner character missing.");
                string run;int kind;bool valuable;
                using(var q=Q.Command(c,t,@"SELECT w.run,w.kind,w.valuable_reward FROM revora_pk_wars w WHERE winner=@uid AND state='complete' AND kind NOT BETWEEN 20 AND 22
AND NOT EXISTS(SELECT 1 FROM revora_pk_claims p WHERE p.run=w.run) ORDER BY ended_at,run LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader()){if(!r.Read())return "No unclaimed champion prize is waiting for you.";run=r.GetString(0);kind=r.GetInt32(1);valuable=r.GetBoolean(2);}
                int balls=Rules.Balls(kind);long cp=Rules.Cps(kind),rvp=Rules.Rvps(kind);decimal gold=Rules.Gold(kind);
                int slots=balls+(valuable?1:0);
                if(p.Inventory.Count+slots>40)return "Make "+slots+" places for your prizes. They will wait.";
                long nextCp=p.Entity.ConquerPoints+cp,nextRvp=p.Entity.RevoraPoint+rvp;decimal nextGold=p.Entity.Money+gold;
                if(nextCp>uint.MaxValue || nextRvp>uint.MaxValue || nextGold>Revora.Game.Entity.MaximumSilver)return "Spend some of your saved funds before claiming this prize.";
                SilverBalance.AssertSaved(c,t,p);
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@gold WHERE uid=@uid AND conquerpoints=@before AND revorapoint=@oldrvp",
                    "cp",nextCp,"rvp",nextRvp,"gold",nextGold,"uid",(long)p.Entity.UID,"before",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint)!=1)throw new InvalidOperationException("PK prize balance changed.");
                var rewards=Enumerable.Range(0,balls).Select(i=>Q.New(722136)).ToList();if(valuable)rewards.Add(QuestSoulReward.Create());
                var items=rewards.ToArray();foreach(var item in items)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"INSERT INTO revora_pk_claims(run,uid,rewards,cp,rvp,gold,claimed_at) VALUES(@run,@uid,@items,@cp,@rvp,@gold,@now)",
                    "run",run,"uid",(long)p.Entity.UID,"items",items.Select(i=>(long)i.UID).ToArray(),"cp",cp,"rvp",rvp,"gold",gold,"now",now)!=1)throw new InvalidOperationException("PK prize receipt was not saved.");
                t.Commit();p.Entity.ApplyCommittedConquerPoints((uint)nextCp);p.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);SilverBalance.ApplyCommitted(p,(ulong)nextGold);Q.Install(p,new uint[0],items);
                return valuable?"Your champion's purse and bound P7 soul are yours. May your next battle be worthy of them.":"Your champion's purse is yours. May your next battle be worthy of it.";
            }}
        }
    }
}
