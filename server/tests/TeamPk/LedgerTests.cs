using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Npgsql;
using Revora.Database;
using Revora.Game.Features.Tournaments;
internal static partial class TeamPkLedgerTests {
    static int checks;
    static readonly Type Ledger=typeof(TeamElitePk).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true);
    const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static object Call(string name,params object[] args) {try{return Ledger.GetMethod(name,Hidden).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static object Sql(string sql,params object[] args) {using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<args.Length;i+=2)q.Parameters.AddWithValue((string)args[i],args[i+1]);return q.ExecuteScalar();}}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;}
    static int Main(){
        var builder=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(builder.Host!="127.0.0.1" || builder.Port!=25439 || builder.Database!="revora_candidate_game")throw new InvalidOperationException("private candidate required");
        DataHolder.ConnectionString=builder.ConnectionString;Sql("CREATE SCHEMA local_team_pk_fixture");
        string prior=Environment.CurrentDirectory;
        try {
            builder.SearchPath="local_team_pk_fixture";DataHolder.ConnectionString=builder.ConnectionString;
            Call("Initialize");
            var kind=TeamElitePk.GamePackets.TeamElitePkBrackets;var skill=TeamElitePk.GamePackets.SkillElitePkBrackets;
            DateTime now=DateTime.UtcNow.AddSeconds(-10),day=new DateTime(2026,9,26);
            var run=(Guid)Call("Identity",kind,2,day);
            Check(run==(Guid)Call("Identity",kind,2,day.AddHours(23)),"same scheduled date reuses identity");
            Check(run!=(Guid)Call("Identity",skill,2,day) && run!=(Guid)Call("Identity",kind,3,day) && run!=(Guid)Call("Identity",kind,2,day.AddDays(7)),"series bracket and date have independent identities");
            Check((bool)Call("Begin",run,kind,2,now),"first run starts");
            Check(!(bool)Call("Begin",run,kind,2,now.AddSeconds(1)),"repeated start cannot create another run");
            var placements=Enumerable.Range(0,8).Select(i=>new TeamElitePk.FighterStats((uint)(920000+i),"fixture",0,null)).ToArray();
            Environment.CurrentDirectory=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";ConquerItemInformation.Load();
            var result=Call("Freeze",placements,kind,now.AddSeconds(1));
            var souls=(int[])result.GetType().GetField("Souls",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(result);
            Check(souls.Length==8 && souls.All(id=>ConquerItemInformation.BaseInformations[(uint)id].PurificationLevel==7),"each frozen prize is a loaded P7");
            Sql("CREATE FUNCTION reject_result() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.state='complete' THEN RAISE EXCEPTION 'fixture'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_result BEFORE UPDATE ON revora_local_team_pk_runs FOR EACH ROW EXECUTE PROCEDURE reject_result();");
            bool failed=false;try{Call("Finish",run,result);}catch(PostgresException){failed=true;}
            Check(failed && Equals(Sql("SELECT state FROM revora_local_team_pk_runs WHERE run=@run","run",run),"active"),"failed result remains active and retryable");
            Sql("DROP TRIGGER reject_result ON revora_local_team_pk_runs;DROP FUNCTION reject_result();");
            Call("Finish",run,result);Call("Finish",run,result);
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_runs WHERE run=@run AND state='complete' AND silver=ARRAY[1000000000,500000000,250000000,10000000,10000000,10000000,10000000,10000000]::bigint[] AND cps=ARRAY[0,0,0,0,0,0,0,0]","run",run))==1,"exact currency terms and idempotent final result persist");
            var before=(long[])Sql("SELECT leaders FROM revora_local_team_pk_runs WHERE run=@run","run",run);placements[0].LeaderUID=42;
            Check(before[0]==920000,"frozen winner survives mutable placement changes");
            var changed=Call("Freeze",placements,kind,now.AddSeconds(1));failed=false;
            try{Call("Finish",run,changed);}catch(InvalidOperationException){failed=true;}
            Check(failed && ((long[])Sql("SELECT leaders FROM revora_local_team_pk_runs WHERE run=@run","run",run))[0]==920000,"different retry cannot overwrite earned leaders");
            placements[0].LeaderUID=placements[1].LeaderUID;failed=false;
            try{Call("Freeze",placements,kind,now);}catch(InvalidOperationException){failed=true;}
            Check(failed,"duplicate placed leader rejected");
            placements[0].LeaderUID=920000;
            var skillRun=(Guid)Call("Identity",skill,2,day);Call("Begin",skillRun,skill,2,now);
            var skillResult=Call("Freeze",placements,skill,now.AddSeconds(1));Call("Finish",skillRun,skillResult);
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_runs WHERE run=@run AND cps=ARRAY[10000,7000,5000,3000,3000,3000,3000,3000] AND silver=ARRAY[0,0,0,0,0,0,0,0]::bigint[]","run",skillRun))==1,"skill terms remain CPs rather than silver");
            var interrupted=(Guid)Call("Identity",kind,1,day);Call("Begin",interrupted,kind,1,now);
            Ledger.GetField("ready",Hidden).SetValue(null,false);Call("Initialize");
            Check(Equals(Sql("SELECT state FROM revora_local_team_pk_runs WHERE run=@run","run",interrupted),"cancelled"),"component startup cancels unfinished run");
            Check(!(bool)Call("Begin",interrupted,kind,1,now) && !(bool)Call("Begin",run,kind,2,now),"cancelled and completed dates cannot reopen");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_runs WHERE state='complete'"))==2,"startup preserves both completed series");
            VerifyClaims();
            VerifyWorldBracket();
            Console.WriteLine("Local Team PK ledger checks passed: "+checks);return 0;
        } finally {Environment.CurrentDirectory=prior;NpgsqlConnection.ClearAllPools();builder.SearchPath="public";DataHolder.ConnectionString=builder.ConnectionString;Sql("DROP SCHEMA local_team_pk_fixture CASCADE");DataHolder.ConnectionString=null;}
    }
    static Revora.Client.GameState Winner(uint uid) {
        var p=new Revora.Client.GameState(null);
        p.Account=new AccountTable(null);p.Account.State=AccountTable.AccountState.Player;
        p.Equipment=new Revora.Game.ConquerStructures.Equipment(p);p.Inventory=new Revora.Game.ConquerStructures.Inventory(p);
        p.Trade=new Revora.Game.ConquerStructures.Trade();p.Entity=new Revora.Game.Entity(Revora.Game.EntityFlag.Player,false);
        p.Entity.SendUpdates=false;p.Entity.UID=uid;p.Entity.Hitpoints=100;p.Entity.Owner=p;p.Entity.X=100;p.Entity.Y=100;p.BindWorldMap(1002);
        p.Fake=false;p.Socket.Alive=false;p.Action=2;p.Entity.FullyLoaded=true;p.ActiveNpc=417171;Revora.Kernel.GamePool[uid]=p;return p;
    }
    static string Claim(Revora.Client.GameState p,bool skill=false) {return (string)Call("Claim",p,skill?TeamElitePk.GamePackets.SkillElitePkBrackets:TeamElitePk.GamePackets.TeamElitePkBrackets);}
    static void VerifyClaims() {
        Sql("CREATE TABLE entities(uid bigint PRIMARY KEY,conquerpoints bigint NOT NULL DEFAULT 0,money bigint NOT NULL DEFAULT 0);CREATE TABLE items (LIKE public.items INCLUDING ALL);INSERT INTO entities(uid) VALUES(920000),(920001),(929999)");
        var map=(Revora.Game.Map)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.Map));map.ID=map.BaseID=1002;
        map.Npcs=new System.Collections.Generic.Dictionary<uint,Revora.Interfaces.INpc>();
        foreach(uint id in new uint[]{417171,801717})map.Npcs[id]=new Revora.Network.GamePackets.NpcSpawn{UID=id,MapID=1002,X=100,Y=100};
        Revora.Kernel.Maps[1002]=map;
        var other=Winner(929999);Check(Claim(other).Contains("No unclaimed"),"unplaced ordinary character cannot claim");
        var p=Winner(920000);p.ActiveNpc=0;Check(Claim(p).Contains("clerk"),"wrong NPC cannot claim");p.ActiveNpc=417171;
        var bag=typeof(Revora.Game.ConquerStructures.Inventory).GetField("objects",BindingFlags.Instance|BindingFlags.NonPublic);
        bag.SetValue(p.Inventory,new Revora.Network.GamePackets.ConquerItem[40]);
        Check(Claim(p).Contains("bag") && Convert.ToInt64(Sql("SELECT money FROM entities WHERE uid=920000"))==0 && Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims"))==0,"full bag defers the entire purse");
        bag.SetValue(p.Inventory,new Revora.Network.GamePackets.ConquerItem[0]);
        Sql("CREATE FUNCTION reject_soul() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture'; END $$;CREATE TRIGGER reject_soul BEFORE INSERT ON items FOR EACH ROW EXECUTE PROCEDURE reject_soul();");
        Claim(p);Check(Convert.ToInt64(Sql("SELECT money FROM entities WHERE uid=920000"))==0 && Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims"))==0,"failed item write rolls back payment and receipt");
        Sql("DROP TRIGGER reject_soul ON items;DROP FUNCTION reject_soul();");p=Winner(920000);
        System.Threading.Tasks.Parallel.For(0,8,i=>Claim(p));
        Check(p.Entity.Money==1000000000 && p.Entity.ConquerPoints==0 && Convert.ToInt64(Sql("SELECT money FROM entities WHERE uid=920000"))==1000000000,"concurrent regular claims pay silver once");
        Check(p.Inventory.Count==1 && p.Inventory.Objects[0].Bound && p.Inventory.Objects[0].Days==0,"single permanent bound P7 installed");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims c JOIN revora_local_team_pk_runs r ON r.run=c.run JOIN items i ON i.uid=c.soul_uid AND i.entityid=c.uid WHERE c.uid=920000 AND c.place=1 AND i.id=r.souls[c.place] AND i.bound=1"))==1,"receipt points to the exact saved frozen soul");
        Check(Claim(p).Contains("No unclaimed") && p.Inventory.Count==1,"regular replay cannot mint another reward");
        p.ActiveNpc=801717;Claim(p,true);
        Check(p.Entity.Money==1000000000 && p.Entity.ConquerPoints==10000 && p.Inventory.Count==2 && Convert.ToInt64(Sql("SELECT conquerpoints FROM entities WHERE uid=920000"))==10000,"separate skill purse pays CPs once without replacing silver");
        Check(Claim(p,true).Contains("No unclaimed") && Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims WHERE uid=920000"))==2,"series-specific receipts prevent replay");
        Ledger.GetField("ready",Hidden).SetValue(null,false);Call("Initialize");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims WHERE uid=920000"))==2 && Convert.ToInt64(Sql("SELECT count(*) FROM items WHERE entityid=920000 AND bound=1"))==2,"component recovery retains paid receipts and owned souls");
        var second=Winner(920001);second.Trade.InTrade=true;
        Check(Claim(second).Contains("clerk") && Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims WHERE uid=920001"))==0,"trading cannot claim");
    }

}
