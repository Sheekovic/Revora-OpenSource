using System;
using System.Reflection;
using Npgsql;
using Revora.Database;
using Q=Revora.Sistemas.Quarantine;
internal static partial class LedgerTests {
    static int checks;
    static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static object Call(string name){return typeof(Q).GetMethod(name,Hidden).Invoke(null,null);}
    static object Sql(string sql){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c))return q.ExecuteScalar();}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;}
    static int Main(){
        string schema="quarantine_ledger_fixture";
        var builder=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(builder.Host!="127.0.0.1" || builder.Port!=25439 || builder.Database!="revora_candidate_game")throw new InvalidOperationException("private database required");
        DataHolder.ConnectionString=builder.ConnectionString;Sql("CREATE SCHEMA quarantine_ledger_fixture");
        try {
            builder.SearchPath=schema;DataHolder.ConnectionString=builder.ConnectionString;
            Call("Initialize");Revora.Server.GamePool=new Revora.Client.GameState[0];
            Q.Start();var id=typeof(Q).GetField("roundId",Hidden).GetValue(null);
            Call("SaveStart");Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_rounds WHERE state='active'"))==1,"start retries reuse round");
            Call("Initialize");Check(Q.Started && Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_rounds WHERE state='active'"))==1,"same-process initialization preserves active round");
            typeof(HitEligibilityTests).GetMethod("Main",Hidden).Invoke(null,null);
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_rounds WHERE state='won' AND white_score=12 AND black_score=8 AND cardinality(winners)=0"))==1,"terminal snapshot retained");
            Q.Start();Q.WhiteScore=4;Q.BlackScore=2;
            Sql("CREATE FUNCTION reject_end() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.state<>'active' THEN RAISE EXCEPTION 'fixture rejection'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_end BEFORE UPDATE ON revora_quarantine_rounds FOR EACH ROW EXECUTE PROCEDURE reject_end();");
            bool failed=false;try{Q.End();}catch(PostgresException){failed=true;}
            Check(failed && Q.Started && (int)typeof(Q).GetField("lifecycleOwner",Hidden).GetValue(null)==0,"failed finalization retains active round and releases runtime owner");
            Check(!Q.HandleQuarantine(null,null,null),"pending result freezes scoring");
            Sql("DROP TRIGGER reject_end ON revora_quarantine_rounds;DROP FUNCTION reject_end();");
            Call("SaveResult");Call("SaveResult"); // Reconcile an already committed terminal write before runtime installation.
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_rounds WHERE state='won' AND white_score=4 AND black_score=2"))==1,"terminal write retries preserve one result");
            Q.End();Check(!Q.Started,"saved terminal retry finishes runtime end");
            Q.Start();typeof(Q).GetField("ledgerReady",Hidden).SetValue(null,false);Call("Initialize");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_rounds WHERE state='active'"))==0,"component startup cancels interrupted rounds");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_rounds WHERE state='cancelled' AND cardinality(winners)=0"))==1,"interrupted round invents no winner");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_rounds WHERE state='won' AND white_score=4 AND black_score=2"))==1,"recovery preserves terminal result");
            VerifyPurses();
            VerifyTournamentWorld();
            Console.WriteLine("Quarantine ledger checks passed: "+checks);return 0;
        } catch(Exception error){Console.WriteLine("Quarantine ledger failure: "+error.GetType().Name+" "+(error is TargetInvocationException && error.InnerException!=null?error.InnerException.GetType().Name: ""));throw;}
        finally {NpgsqlConnection.ClearAllPools();builder.SearchPath="public";DataHolder.ConnectionString=builder.ConnectionString;Sql("DROP SCHEMA quarantine_ledger_fixture CASCADE");DataHolder.ConnectionString=null;}
    }
}
