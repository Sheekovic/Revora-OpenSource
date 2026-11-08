using System;
using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Revora.Database;

namespace Revora.Game.Features
{
    internal static class ElitePkResults
    {
        internal sealed class Entry {
            internal uint Uid; internal int Level, Power; internal Guid Seed;
        }
        internal sealed class Pair {
            internal uint Left, Right; internal int Slot; internal bool Bronze;
        }
        private static T Transaction<T>(Func<NpgsqlConnection,NpgsqlTransaction,T> work) {
            using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) {
                T result=work(c,t);t.Commit();return result;
            }}
        }
        private static NpgsqlCommand Command(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] args) {
            var q=new NpgsqlCommand(sql,c,t);
            for(int i=0;i<args.Length;i+=2) {
                if(args[i+1] is DateTime) {
                    var utc=(DateTime)args[i+1];if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Elite PK requires UTC.");
                    q.Parameters.Add((string)args[i],NpgsqlDbType.TimestampTz).Value=utc;
                } else q.Parameters.AddWithValue((string)args[i],args[i+1]);
            }
            return q;
        }
        private static int Execute(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] args) {
            using(var q=Command(c,t,sql,args))return q.ExecuteNonQuery();
        }
        internal static void Initialize() {
            Transaction((c,t)=>{Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_epk_runs(
run varchar(32) PRIMARY KEY, cycle varchar(16) NOT NULL, bracket integer NOT NULL CHECK(bracket BETWEEN 0 AND 3),
starts_at timestamptz NOT NULL, deadline timestamptz NOT NULL, state varchar(16) NOT NULL,
winner bigint NOT NULL DEFAULT 0, outcome varchar(32) NOT NULL DEFAULT 'pending', completed_at timestamptz,
CHECK(state IN ('registration','running','complete','cancelled')));
CREATE TABLE IF NOT EXISTS revora_epk_entries(
run varchar(32) REFERENCES revora_epk_runs(run), cycle varchar(16) NOT NULL, uid bigint CHECK(uid>0),
level integer NOT NULL, power integer NOT NULL, seed uuid NOT NULL,
PRIMARY KEY(run,uid), UNIQUE(cycle,uid));
CREATE TABLE IF NOT EXISTS revora_epk_matches(
run varchar(32) REFERENCES revora_epk_runs(run), round integer CHECK(round>0), slot integer CHECK(slot>=0),
left_uid bigint NOT NULL, right_uid bigint NOT NULL, winner bigint, left_damage bigint NOT NULL DEFAULT 0,
right_damage bigint NOT NULL DEFAULT 0, outcome varchar(32) NOT NULL DEFAULT 'pending',
PRIMARY KEY(run,round,slot), CHECK(winner IS NULL OR winner=0 OR winner=left_uid OR winner=right_uid));
ALTER TABLE revora_epk_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_epk_runs ADD COLUMN IF NOT EXISTS local_wardrobe boolean NOT NULL DEFAULT false;
UPDATE revora_epk_runs SET state='cancelled',outcome='server-restart' WHERE state IN ('registration','running');");ElitePkPlacements.Initialize(c,t);ElitePkPrizes.Initialize(c,t);return true;});
        }
        internal static bool Open(string key,string cycle,int bracket,DateTime starts,DateTime deadline) {
            return Transaction((c,t)=> {
                Execute(c,t,@"INSERT INTO revora_epk_runs(run,cycle,bracket,starts_at,deadline,state,valuable_reward,local_wardrobe)
VALUES(@run,@cycle,@bracket,@starts,@end,'registration',true,true) ON CONFLICT DO NOTHING",
                    "run",key,"cycle",cycle,"bracket",bracket,"starts",starts,"end",deadline);
                // An uncertain commit can be retried; Initialize closes older-process runs.
                using(var q=Command(c,t,@"SELECT state='registration' AND cycle=@cycle AND bracket=@bracket
AND starts_at=@starts AND deadline=@end FROM revora_epk_runs WHERE run=@run",
                    "run",key,"cycle",cycle,"bracket",bracket,"starts",starts,"end",deadline))return Equals(q.ExecuteScalar(),true);
            });
        }
        private static string LockRun(NpgsqlConnection c,NpgsqlTransaction t,string run) {
            using(var q=Command(c,t,"SELECT state FROM revora_epk_runs WHERE run=@run FOR UPDATE","run",run))return q.ExecuteScalar() as string;
        }
        internal static bool Register(string run,Entry entry,DateTime utc) {
            return Transaction((c,t)=> {
                if(entry.Uid==0 || LockRun(c,t,run)!="registration")return false;
                using(var q=Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_epk_entries WHERE run=@run AND uid=@uid)","run",run,"uid",(long)entry.Uid))
                    if(Equals(q.ExecuteScalar(),true))return true;
                using(var q=Command(c,t,"SELECT count(*)<32 FROM revora_epk_entries WHERE run=@run","run",run))
                    if(!Equals(q.ExecuteScalar(),true))return false;
                return Execute(c,t,@"INSERT INTO revora_epk_entries(run,cycle,uid,level,power,seed)
SELECT run,cycle,@uid,@level,@power,@seed FROM revora_epk_runs WHERE run=@run AND starts_at>@now
ON CONFLICT DO NOTHING","run",run,"uid",(long)entry.Uid,"level",entry.Level,"power",entry.Power,"seed",entry.Seed,"now",utc)==1;
            });
        }
        internal static List<Entry> Begin(string run,DateTime utc) {
            return Transaction((c,t)=> {
                string state=LockRun(c,t,run);
                if(state!="registration" && state!="running")throw new InvalidOperationException("Elite PK run is closed.");
                using(var q=Command(c,t,"SELECT starts_at<=@now AND deadline>@now FROM revora_epk_runs WHERE run=@run","run",run,"now",utc))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Elite PK start is outside its window.");
                Execute(c,t,"UPDATE revora_epk_runs SET state='running' WHERE run=@run","run",run);
                var entries=new List<Entry>();
                using(var q=Command(c,t,"SELECT uid,level,power,seed FROM revora_epk_entries WHERE run=@run ORDER BY seed,uid","run",run))
                using(var r=q.ExecuteReader())while(r.Read())entries.Add(new Entry{Uid=Convert.ToUInt32(r[0]),Level=r.GetInt32(1),Power=r.GetInt32(2),Seed=r.GetGuid(3)});
                return entries;
            });
        }
        internal static void Round(string run,int round,IEnumerable<Pair> pairs) {
            Transaction((c,t)=> {
                if(LockRun(c,t,run)!="running")throw new InvalidOperationException("Elite PK run is closed.");
                foreach(var pair in pairs) {
                    Execute(c,t,@"INSERT INTO revora_epk_matches(run,round,slot,left_uid,right_uid,kind)
VALUES(@run,@round,@slot,@left,@right,@kind) ON CONFLICT DO NOTHING","run",run,"round",round,"slot",pair.Slot,"left",(long)pair.Left,"right",(long)pair.Right,"kind",pair.Bronze?"bronze":"elimination");
                    using(var q=Command(c,t,"SELECT left_uid=@left AND right_uid=@right AND kind=@kind FROM revora_epk_matches WHERE run=@run AND round=@round AND slot=@slot",
                        "run",run,"round",round,"slot",pair.Slot,"left",(long)pair.Left,"right",(long)pair.Right,"kind",pair.Bronze?"bronze":"elimination"))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Elite PK pairing replay mismatch.");
                }
                return true;
            });
        }
        internal static void Resolve(string run,int round,int slot,uint winner,long leftDamage,long rightDamage,string reason) {
            Transaction((c,t)=> {
                if(LockRun(c,t,run)!="running")throw new InvalidOperationException("Elite PK run is closed.");
                if(leftDamage<0 || rightDamage<0)throw new ArgumentException("Negative Elite PK damage.");
                Execute(c,t,@"UPDATE revora_epk_matches SET winner=@winner,left_damage=@left,right_damage=@right,outcome=@reason
WHERE run=@run AND round=@round AND slot=@slot AND winner IS NULL","run",run,"round",round,"slot",slot,"winner",(long)winner,"left",leftDamage,"right",rightDamage,"reason",reason);
                using(var q=Command(c,t,@"SELECT winner=@winner AND left_damage=@left AND right_damage=@right AND outcome=@reason
FROM revora_epk_matches WHERE run=@run AND round=@round AND slot=@slot","run",run,"round",round,"slot",slot,"winner",(long)winner,"left",leftDamage,"right",rightDamage,"reason",reason))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Elite PK result replay mismatch.");
                return true;
            });
        }
        internal static bool Finish(string run,uint winner,DateTime utc,bool cancelled) {
            return Transaction((c,t)=> {
                string state=LockRun(c,t,run);
                if(state=="cancelled")return false;
                if(state=="complete") {
                    using(var q=Command(c,t,"SELECT winner=@winner AND outcome='champion' FROM revora_epk_runs WHERE run=@run","run",run,"winner",(long)winner))return Equals(q.ExecuteScalar(),true);
                }
                if(state!="running" && state!="registration")throw new InvalidOperationException("Missing Elite PK run.");
                int bracket;
                using(var q=Command(c,t,"SELECT bracket FROM revora_epk_runs WHERE run=@run","run",run))bracket=Convert.ToInt32(q.ExecuteScalar());
                bool earned=!cancelled && winner!=0 && ElitePkPlacements.Record(c,t,run,winner,utc);
                if(earned)ProgressionEvidence.Complete(c,t,winner,11123,"elite-pk-champion-v1",run,1);
                if(earned && bracket==3)ProgressionEvidence.Grant(c,t,winner,"elite-pk-local-v1",run,60010020,utc);
                if(Execute(c,t,"UPDATE revora_epk_runs SET state=@state,winner=@winner,outcome=@outcome,completed_at=@now WHERE run=@run",
                    "run",run,"state",cancelled?"cancelled":"complete","winner",earned?(long)winner:0L,"outcome",cancelled?"interrupted":earned?"champion":"uncontested","now",utc)!=1)throw new InvalidOperationException("Elite final result was not saved.");
                return earned;
            });
        }
    }
}
