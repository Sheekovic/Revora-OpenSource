using System;
using Console=System.Console;
using System.Linq;
using Npgsql;
using Revora;
using Revora.Database;
using Revora.Union;
using Revora.Network.GamePackets.Union;
using Guild=Revora.Game.ConquerStructures.Society.Guild;
internal static class CheckUnionRecovery {
    static int checks;
    static object Sql(string sql,params object[] values){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<values.Length;i+=2)q.Parameters.AddWithValue((string)values[i],values[i+1]);return q.ExecuteScalar();}}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    static void Load(){Kernel.Unions.Clear();UnionTable.Load();}
    static void Valid(){
        Load();var u=Kernel.Unions[91];
        Check(Kernel.Unions.Count==2 && u.ID==91 && u.LeaderUID==5001 && u.LeaderName=="KingCandidate","saved union and leader identities restore");
        Check(u.Name=="Revora Union" && u.Title=="Crown" && u.Bulletin=="Stand together" && u.Declaration=="Defend our realm","metadata hydrates without persistence setters");
        Check(u.GoldBricks==123 && u.UnionFund==3500000000UL,"saved economic values restore exactly without debit or rewrite");
        Check(u.Coreofficals.SequenceEqual(new uint[]{11,12,13,14,15,16,17,18}) && u.Harem.Length==10 && u.Harem[9]==99 && u.Guards.Length==20 && u.Guards[19]==199,"all eight officer slots and complete harem/guard rosters survive");
        Check(u.Guilds.SequenceEqual(new uint[]{101,102}) && !u.Guilds.Contains(103),"guild association derives from saved guild projection in stable order");
        Check(Kernel.Unions[92].Coreofficals.SequenceEqual(new uint[]{21,22,23,24,25,26,0,0}),"legacy six-slot office strings pad only missing final slots");
        var original=Kernel.Unions;
        bool refused=false;try{UnionTable.Load();}catch(InvalidOperationException){refused=true;}
        Check(refused && ReferenceEquals(original,Kernel.Unions) && ReferenceEquals(u,Kernel.Unions[91]),"startup loader refuses to overwrite a populated live cache");
    }
    static int Main(string[] args){
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(args.Length!=1 || (args[0]!="capture" && args[0]!="verify") || b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("Fixed private fixture required.");
        DataHolder.ConnectionString=b.ConnectionString;
        try {
            if(args[0]=="capture")Sql("CREATE SCHEMA revora_kingdom_recovery_fixture;COMMENT ON SCHEMA revora_kingdom_recovery_fixture IS 'revora-owned-kingdom-recovery-v1'");
            Check(Equals(Sql("SELECT obj_description(oid,'pg_namespace') FROM pg_namespace WHERE nspname='revora_kingdom_recovery_fixture'"),"revora-owned-kingdom-recovery-v1"),"fixed fixture ownership verified");
            b.SearchPath="revora_kingdom_recovery_fixture";DataHolder.ConnectionString=b.ConnectionString;
            if(args[0]=="capture") {
                Sql("CREATE TABLE unions(LIKE public.unions INCLUDING ALL);CREATE TABLE fingerprint(value text NOT NULL)");
                Sql("INSERT INTO unions(id,goldbricks,unionfund,name,title,leadername,leaderuid,bulletin,declaration,coreofficals,harem,guards) VALUES(91,123,3500000000,'Revora Union','Crown','KingCandidate',5001,'Stand together','Defend our realm',@core,@harem,@guards),(92,0,0,'Legacy Union','','LegacyLeader',5002,'','','21#22#23#24#25#26',@harem,@guards)","core","11#12#13#14#15#16#17#18","harem",string.Join("#",Enumerable.Range(90,10)),"guards",string.Join("#",Enumerable.Range(180,20)));
                Sql("CREATE FUNCTION reject_loader_write() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RAISE EXCEPTION ''Union loading must be read-only''; END';CREATE TRIGGER reject_loader_write BEFORE INSERT OR UPDATE OR DELETE ON unions FOR EACH ROW EXECUTE FUNCTION reject_loader_write()");
            }
            Kernel.Guilds.Add(102,new Guild("fixture-two"){ID=102,UnionID=91});Kernel.Guilds.Add(101,new Guild("fixture-one"){ID=101,UnionID=91});Kernel.Guilds.Add(103,new Guild("outsider"){ID=103,UnionID=92});
            Valid();
            if(args[0]=="capture") {
                foreach(string bad in new[]{"0#0","0#0#0#0#0#0#0#0#0#-1","0#0#0#0#0#0#0#0#0#4294967296"}) {
                    Sql("ALTER TABLE unions DISABLE TRIGGER reject_loader_write;UPDATE unions SET harem=@bad WHERE id=92;ALTER TABLE unions ENABLE TRIGGER reject_loader_write","bad",bad);
                    Kernel.Unions.Clear();var empty=Kernel.Unions;bool refused=false;
                    try{UnionTable.Load();}catch(FormatException){refused=true;}catch(OverflowException){refused=true;}catch(InvalidOperationException){refused=true;}
                    Check(refused && Kernel.Unions.Count==0 && ReferenceEquals(empty,Kernel.Unions),"malformed late row cannot publish partial union state");
                }
                Sql("ALTER TABLE unions DISABLE TRIGGER reject_loader_write;UPDATE unions SET harem=@roster WHERE id=92;ALTER TABLE unions ENABLE TRIGGER reject_loader_write","roster",string.Join("#",Enumerable.Range(90,10)));
                Valid();Sql("INSERT INTO fingerprint SELECT jsonb_agg(to_jsonb(u) ORDER BY id)::text FROM unions u");
            } else {
                Check(Equals(Sql("SELECT value FROM fingerprint"),Sql("SELECT jsonb_agg(to_jsonb(u) ORDER BY id)::text FROM unions u")),"fresh process and PostgreSQL restart preserve exact saved rows");
                NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
                Sql("DROP SCHEMA revora_kingdom_recovery_fixture CASCADE");
            }
            Console.WriteLine("Union recovery "+args[0]+" passed: "+checks);return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
        finally{NpgsqlConnection.ClearAllPools();DataHolder.ConnectionString=null;}
    }
}
