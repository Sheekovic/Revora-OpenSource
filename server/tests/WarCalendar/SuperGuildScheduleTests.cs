using System;
using Console=System.Console;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static class SuperGuildScheduleTests {
    const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static int checks;
    static DateTime now;
    static object Call(string name,params object[] args) {try{return typeof(SuperGuildWar).GetMethod(name,Hidden).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static void Set(string name,object value){typeof(SuperGuildWar).GetField(name,Hidden).SetValue(null,value);}
    static object Sql(string sql,params object[] args){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<args.Length;i+=2)q.Parameters.AddWithValue((string)args[i],args[i+1]);return q.ExecuteScalar();}}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    static long Count(string where="true"){return Convert.ToInt64(Sql("SELECT count(*) FROM revora_super_guild_battles WHERE "+where));}
    static void Tick(){Call("Tick");}
    static DateTime Local(int day,int hour,int minute,int second){return new DateTime(2026,9,day,hour,minute,second,DateTimeKind.Local);}
    static int Main(){
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("Private candidate required.");
        DataHolder.ConnectionString=b.ConnectionString;Sql("CREATE SCHEMA super_guild_schedule_fixture");
        string prior=Environment.CurrentDirectory;
        try {
            b.SearchPath="super_guild_schedule_fixture";DataHolder.ConnectionString=b.ConnectionString;
            Environment.CurrentDirectory=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";ConquerItemInformation.Load();
            Server.Vars=new VariableVault();
            SuperGuildWar.Pole=new SobNpcSpawn{UID=811,MapID=1234,Hitpoints=1000,MaxHitpoints=1000};
            SuperGuildWar.LeftGate=new SobNpcSpawn{UID=516076,MapID=1234,Hitpoints=1000,MaxHitpoints=1000};
            SuperGuildWar.RightGate=new SobNpcSpawn{UID=516077,MapID=1234,Hitpoints=1000,MaxHitpoints=1000};
            Set("Clock",(Func<DateTime>)(()=>now));now=Local(2,19,59,55).ToUniversalTime();
            Call("InitializeLedger");
            typeof(SuperGuildWar).Assembly.GetType("Revora.Game.Features.SuperGuildWarPurses").GetMethod("Initialize",Hidden).Invoke(null,null);
            Check(!(bool)Call("IsOpeningTime",Local(1,20,0,1)),"Tuesday cannot open Wednesday war");
            Check(!(bool)Call("IsOpeningTime",Local(2,19,59,59)),"early opening refused");
            Check(!(bool)Call("IsOpeningTime",Local(2,20,0,0)),"existing first-second boundary retained");
            Check((bool)Call("IsOpeningTime",Local(2,20,0,59)),"last opening second accepted");
            Check(!(bool)Call("IsOpeningTime",Local(2,20,1,0)),"late opening refused");
            var id=(Guid)Call("ScheduledIdentity",Local(2,20,0,1));
            Check(id==(Guid)Call("ScheduledIdentity",Local(2,0,0,0)) && id!=(Guid)Call("ScheduledIdentity",Local(9,20,0,1)),"one identity per local date");
            Tick();Check(Count()==0 && !SuperGuildWar.IsWar,"pre-window timer creates no battle");
            SuperGuildWar.PoleKeeper1=new Revora.Game.ConquerStructures.Society.Guild("previous winner"){ID=92811};SuperGuildWar.KeeperID=92811;
            now=Local(2,20,0,1).ToUniversalTime();Parallel.For(0,12,i=>Tick());
            Check(Count()==1 && SuperGuildWar.IsWar,"concurrent due ticks create one battle");
            Check((DateTime)Sql("SELECT closes_at AT TIME ZONE 'UTC' FROM revora_super_guild_battles WHERE id=@id","id",id)==Local(2,21,0,0).ToUniversalTime(),"21:00 UTC-converted deadline is stored");
            Check(Convert.ToInt64(Sql("SELECT keeper FROM revora_super_guild_battles WHERE id=@id","id",id))==0 && SuperGuildWar.PoleKeeper1==null && SuperGuildWar.KeeperID==0,"scheduled battle starts without inherited victory");
            var started=SuperGuildWar.StartTime;SuperGuildWar.IsWar=false;Set("pendingStartId",id);Set("pendingScheduledUntil",Local(2,21,0,0).ToUniversalTime());
            now=now.AddSeconds(4);Tick();Check(Count()==1 && SuperGuildWar.IsWar && SuperGuildWar.StartTime==started,"lost start installation reconciles original row");
            Call("InitializeLedger");now=now.AddSeconds(4);Tick();
            Check(!SuperGuildWar.IsWar && Count("state='cancelled'")==1 && Count()==1,"restart cancels and cannot reopen the same slot");
            now=Local(9,20,0,1).ToUniversalTime();Tick();Check(SuperGuildWar.IsWar && Count()==2,"next Wednesday opens independently");
            Sql("CREATE FUNCTION reject_close() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.state='empty' THEN RAISE EXCEPTION 'fixture'; END IF; RETURN NEW; END $$;CREATE TRIGGER reject_close BEFORE UPDATE ON revora_super_guild_battles FOR EACH ROW EXECUTE PROCEDURE reject_close();");
            now=Local(9,21,4,0).ToUniversalTime();bool failed=false;try{Tick();}catch(PostgresException){failed=true;}
            Check(failed && SuperGuildWar.IsWar && Count("state='active'")==1,"failed late settlement stays retryable");
            Sql("DROP TRIGGER reject_close ON revora_super_guild_battles;DROP FUNCTION reject_close();");
            now=now.AddSeconds(4);Tick();Check(!SuperGuildWar.IsWar && Count("state='empty' AND purses_frozen")==1,"late poll closes and seals exactly once");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_super_guild_purses"))==0,"empty scheduled battle fabricates no prizes");
            Call("InitializeLedger");now=Local(9,20,0,20).ToUniversalTime();Tick();Check(Count()==2 && !SuperGuildWar.IsWar,"completed date cannot reopen after restart");
            now=Local(16,20,0,1).ToUniversalTime();
            Sql("CREATE FUNCTION reject_open() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture'; END $$;CREATE TRIGGER reject_open BEFORE INSERT ON revora_super_guild_battles FOR EACH ROW EXECUTE PROCEDURE reject_open();");
            failed=false;try{Tick();}catch(PostgresException){failed=true;}
            Check(failed && !SuperGuildWar.IsWar && Count()==2,"failed opening grants no active battle");
            Sql("DROP TRIGGER reject_open ON revora_super_guild_battles;DROP FUNCTION reject_open();");
            now=Local(16,21,5,0).ToUniversalTime();Tick();Check(Count()==2 && !SuperGuildWar.IsWar,"uncommitted start cannot appear after its deadline");
            now=Local(23,20,0,1).ToUniversalTime();id=(Guid)Call("ScheduledIdentity",now.ToLocalTime());
            Set("pendingStartId",id);Set("pendingScheduledUntil",Local(23,21,0,0).ToUniversalTime());Call("SaveStart");
            Set("pendingStartId",id);Set("pendingScheduledUntil",Local(23,21,0,0).ToUniversalTime());
            now=Local(23,21,5,0).ToUniversalTime();Tick();
            Check(!SuperGuildWar.IsWar && Count()==3 && Count("state='empty'")==2,"late lost-reply recovery settles the saved battle without reopening");
            SuperGuildWar.Start();Check(SuperGuildWar.IsWar && Count("state='active' AND closes_at IS NULL")==1,"explicit manual starts retain their separate lifecycle");
            SuperGuildWar.End();Check(!SuperGuildWar.IsWar && Count("state='empty'")==3,"manual end still seals its original result");
            Console.WriteLine("Super Guild schedule checks passed: "+checks);return 0;
        } finally {
            Environment.CurrentDirectory=prior;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            Sql("DROP SCHEMA super_guild_schedule_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
}
