using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Revora.Database;
internal static class CheckKingdomLedger {
    static readonly Type Ledger=typeof(DataHolder).Assembly.GetType("Revora.Game.Features.KingdomCompetitionLedger",true);
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;
    static int checks;
    static object Call(string method,params object[] args){try{return Ledger.GetMethods(Hidden).Single(m=>m.Name==method && m.GetParameters().Length==args.Length).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static object Field(object result,string name){return result.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(result);}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    static object Sql(string sql,params object[] values){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<values.Length;i+=2)q.Parameters.AddWithValue((string)values[i],values[i+1]);return q.ExecuteScalar();}}}
    static long Count(string sql){return Convert.ToInt64(Sql(sql));}
    static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
    static DateTime At(int day,int hour){return TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,day,hour,0,0,DateTimeKind.Unspecified),Zone);}
    static Guid Harvest(string day,uint uid,uint union,DateTime start){var n=(Guid)Call("BeginHarvest",day,uid,union,start);Check(n!=Guid.Empty,"server creates a unique harvest challenge");Check(!(bool)Call("FinishHarvest",day,uid,union,n,start.AddSeconds(4)),"harvest cannot complete before five seconds");Check((bool)Call("FinishHarvest",day,uid,union,n,start.AddSeconds(5)),"elapsed server challenge creates one carried brick");return n;}
    static int Main(string[] args){
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(args.Length!=1 || (args[0]!="capture" && args[0]!="verify") || b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("Fixed private fixture required.");
        DataHolder.ConnectionString=b.ConnectionString;
        try {
            if(args[0]=="capture")Sql("CREATE SCHEMA revora_kingdom_ledger_fixture;COMMENT ON SCHEMA revora_kingdom_ledger_fixture IS 'revora-owned-kingdom-ledger-v1'");
            Check(Equals(Sql("SELECT obj_description(oid,'pg_namespace') FROM pg_namespace WHERE nspname='revora_kingdom_ledger_fixture'"),"revora-owned-kingdom-ledger-v1"),"owned schema verified");
            b.SearchPath="revora_kingdom_ledger_fixture";DataHolder.ConnectionString=b.ConnectionString;
            if(args[0]=="capture")Sql("CREATE TABLE entities(uid bigint PRIMARY KEY,unionid bigint NOT NULL);CREATE TABLE unions(id bigint PRIMARY KEY,leaderuid bigint NOT NULL,goldbricks bigint NOT NULL);INSERT INTO unions VALUES(91,5001,0),(92,5002,999999);INSERT INTO entities VALUES(5001,91),(5002,92),(5003,91),(5004,0);CREATE TABLE fingerprint(value text NOT NULL)");
            Call("Initialize");var start=At(10,17);const string day="2026-10-10";
            if(args[0]=="capture") {
                Check(Call("Open",start.AddTicks(-1))==null && Call("Open",At(10,18))==null,"admission window excludes both outside boundaries");
                Check(Equals(Call("Open",start),day) && Equals(Call("Open",start),day) && Count("SELECT count(*) FROM revora_kingdom_contests")==1,"calendar retry creates one contest");
                Check(!(bool)Call("Admit",day,5004u,91u,start) && !(bool)Call("Admit",day,5001u,92u,start),"missing and forged membership cannot enroll");
                Check((bool)Call("Admit",day,5001u,91u,start) && (bool)Call("Admit",day,5002u,92u,start),"saved member identities enroll in their own unions");
                var nonce=(Guid)Call("BeginHarvest",day,5001u,91u,start);
                Check(nonce!=Guid.Empty && (Guid)Call("BeginHarvest",day,5001u,91u,start)==Guid.Empty,"repeated start preserves the pending challenge");
                Check(!(bool)Call("FinishHarvest",day,5001u,91u,Guid.NewGuid(),start.AddSeconds(5)) && !(bool)Call("FinishHarvest",day,5002u,92u,nonce,start.AddSeconds(5)),"forged and cross-owner challenge tokens are refused");
                Check(!(bool)Call("FinishHarvest",day,5001u,91u,nonce,start.AddSeconds(4)),"early challenge cannot create cargo");
                int accepted=0;Parallel.For(0,8,i=>{if((bool)Call("FinishHarvest",day,5001u,91u,nonce,start.AddSeconds(5)))Interlocked.Increment(ref accepted);});
                Check(accepted==1,"concurrent harvest callbacks produce one carried brick");
                Sql("CREATE FUNCTION reject_delivery() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_delivery BEFORE INSERT ON revora_kingdom_deliveries FOR EACH ROW EXECUTE FUNCTION reject_delivery()");
                bool failed=false;try{Call("Deliver",day,5001u,91u,nonce,start.AddSeconds(6));}catch(InvalidOperationException){failed=true;}
                Sql("DROP TRIGGER reject_delivery ON revora_kingdom_deliveries;DROP FUNCTION reject_delivery()");
                Check(failed && Count("SELECT count(*) FROM revora_kingdom_deliveries")==0 && Equals(Sql("SELECT carried FROM revora_kingdom_entries WHERE uid=5001"),true),"receipt failure rolls back cargo removal and score");
                accepted=0;Parallel.For(0,8,i=>{if((bool)Call("Deliver",day,5001u,91u,nonce,start.AddSeconds(6)))Interlocked.Increment(ref accepted);});
                Check(accepted==1 && Count("SELECT count(*) FROM revora_kingdom_deliveries")==1,"concurrent deposit retry banks one point");
                var second=Harvest(day,5002,92,start.AddSeconds(10));Check((bool)Call("Deliver",day,5002u,92u,second,start.AddSeconds(16)),"second union banks an equal score");
                Check(Call("Settle",day,At(10,18).AddTicks(-1))==null,"event cannot settle early");
                Sql("CREATE FUNCTION reject_result() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_result BEFORE UPDATE ON revora_kingdom_contests FOR EACH ROW EXECUTE FUNCTION reject_result()");
                failed=false;try{Call("Settle",day,At(10,18));}catch(InvalidOperationException){failed=true;}
                Sql("DROP TRIGGER reject_result ON revora_kingdom_contests;DROP FUNCTION reject_result()");
                Check(failed && !(bool)Field(Call("Read",day),"Final"),"suppressed result leaves the contest retryable");
                var result=Call("Settle",day,At(10,18));Check((bool)Field(result,"Final") && (uint)Field(result,"Union")==91 && (uint)Field(result,"Leader")==5001 && (long)Field(result,"Score")==1,"equal event scores choose lowest union ID; lifetime bricks do not choose the crown");
                Check(!(bool)Call("Admit",day,5003u,91u,At(10,18)) && !(bool)Call("Deliver",day,5002u,92u,second,At(10,18)),"closed contest rejects late admission and delivery");
                const string next="2026-10-11";var nextStart=At(11,17);Call("Open",nextStart);Call("Admit",next,5003u,91u,nextStart);
                var carried=Harvest(next,5003,91,nextStart);Check(carried!=Guid.Empty,"unbanked cargo remains for restart recovery test");
                Sql("INSERT INTO fingerprint SELECT jsonb_agg(to_jsonb(c) ORDER BY day)::text FROM revora_kingdom_contests c");
            } else {
                Check(Equals(Sql("SELECT value FROM fingerprint"),Sql("SELECT jsonb_agg(to_jsonb(c) ORDER BY day)::text FROM revora_kingdom_contests c")),"PostgreSQL and executable restart preserve exact contest rows");
                var result=Call("Settle",day,At(12,18));Check((bool)Field(result,"Final") && (uint)Field(result,"Union")==91 && (long)Field(result,"Score")==1 && (DateTime)Field(result,"Expires")==At(11,18),"settlement replay preserves frozen winner and next-day expiry");
                Check(Count("SELECT count(*) FROM revora_kingdom_deliveries")==2,"recovery adds no delivery receipt");
                var old=(Guid)Sql("SELECT nonce FROM revora_kingdom_entries WHERE day='2026-10-11' AND uid=5003");
                Check((bool)Call("Discard","2026-10-11",5003u) && !(bool)Call("Discard","2026-10-11",5003u),"interrupted lifecycle discards unbanked cargo exactly once");
                Check(!(bool)Call("FinishHarvest","2026-10-11",5003u,91u,old,At(11,17).AddSeconds(8)),"stale callback cannot resurrect discarded cargo");
                Sql("UPDATE entities SET unionid=92 WHERE uid=5003");
                Check(!(bool)Call("Admit","2026-10-11",5003u,92u,At(11,17)) && (Guid)Call("BeginHarvest","2026-10-11",5003u,91u,At(11,17))==Guid.Empty,"changing unions cannot switch scoring allegiance mid-contest");
                var empty=Call("Settle","2026-10-11",At(11,18));Check((bool)Field(empty,"Final") && (uint)Field(empty,"Union")==0 && (long)Field(empty,"Score")==0,"empty contest creates no ruler");
                NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;Sql("DROP SCHEMA revora_kingdom_ledger_fixture CASCADE");
            }
            Console.WriteLine("Kingdom ledger "+args[0]+" passed: "+checks);return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}finally{NpgsqlConnection.ClearAllPools();DataHolder.ConnectionString=null;}
    }
}
