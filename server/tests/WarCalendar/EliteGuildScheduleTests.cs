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
internal static class EliteGuildScheduleTests {
    const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static int checks;
    static DateTime now;
    static object Call(string name,params object[] args) {try{return typeof(EliteGuildWar).GetMethod(name,Hidden).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static void Set(string name,object value){typeof(EliteGuildWar).GetField(name,Hidden).SetValue(null,value);}
    static object Sql(string sql,params object[] args){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<args.Length;i+=2)q.Parameters.AddWithValue((string)args[i],args[i+1]);return q.ExecuteScalar();}}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    static long Count(string where="true"){return Convert.ToInt64(Sql("SELECT count(*) FROM revora_elite_guild_battles WHERE "+where));}
    static void Tick(){Call("Tick");}
    static DateTime Local(int day,int hour,int minute,int second){return new DateTime(2026,9,day,hour,minute,second,DateTimeKind.Local);}
    static int Main(){
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("Private candidate required.");
        DataHolder.ConnectionString=b.ConnectionString;Sql("CREATE SCHEMA elite_guild_schedule_fixture");
        string prior=Environment.CurrentDirectory;
        try {
            b.SearchPath="elite_guild_schedule_fixture";DataHolder.ConnectionString=b.ConnectionString;
            Environment.CurrentDirectory=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";ConquerItemInformation.Load();
            Server.Vars=new VariableVault();
            EliteGuildWar.Poles=new SobNpcSpawn{UID=88816,MapID=2071,Hitpoints=1000,MaxHitpoints=1000};
            Set("Clock",(Func<DateTime>)(()=>now));now=Local(7,19,59,55).ToUniversalTime();
            Call("InitializeLedger");
            Check(!(bool)Call("IsOpeningTime",Local(9,21,1,5)),"Wednesday cannot open Elite war");
            Check(!(bool)Call("IsOpeningTime",Local(7,21,1,4)),"early opening refused");
            Check(!(bool)Call("IsOpeningTime",Local(7,21,0,59)),"opening minute boundary retained");
            Check((bool)Call("IsOpeningTime",Local(7,21,1,59)),"last opening second accepted");
            Check(!(bool)Call("IsOpeningTime",Local(7,21,2,0)),"late opening refused");
            Check(!(bool)Call("IsOpeningTime",Local(12,21,1,5)),"Saturday cannot open Elite war");
            foreach(int day in new[]{6,7,8,10,11})Check((bool)Call("IsOpeningTime",Local(day,21,1,5)),"eligible weekday opens: "+day);
            var id=(Guid)Call("ScheduledIdentity",Local(7,21,1,5));
            Check(id==(Guid)Call("ScheduledIdentity",Local(7,0,0,0)) && id!=(Guid)Call("ScheduledIdentity",Local(14,21,1,5)),"one identity per local date");
            Tick();Check(Count()==0 && !EliteGuildWar.IsWar,"pre-window timer creates no battle");
            EliteGuildWar.PoleKeeper=new Revora.Game.ConquerStructures.Society.Guild("previous winner"){ID=92811};EliteGuildWar.KeeperID=92811;
            now=Local(7,21,1,5).ToUniversalTime();Parallel.For(0,12,i=>Tick());
            Check(Count()==1 && EliteGuildWar.IsWar,"concurrent due ticks create one battle");
            Check((DateTime)Sql("SELECT closes_at AT TIME ZONE 'UTC' FROM revora_elite_guild_battles WHERE id=@id","id",id)==Local(7,22,0,0).ToUniversalTime(),"22:00 UTC-converted deadline is stored");
            Check(Convert.ToInt64(Sql("SELECT keeper FROM revora_elite_guild_battles WHERE id=@id","id",id))==0 && EliteGuildWar.PoleKeeper==null && EliteGuildWar.KeeperID==0,"scheduled battle starts without inherited victory");
            var started=EliteGuildWar.StartTime;EliteGuildWar.IsWar=false;Set("pendingStartId",id);Set("pendingScheduledUntil",Local(7,22,0,0).ToUniversalTime());
            now=now.AddSeconds(4);Tick();Check(Count()==1 && EliteGuildWar.IsWar && EliteGuildWar.StartTime==started,"lost start installation reconciles original row");
            Call("InitializeLedger");now=now.AddSeconds(4);Tick();
            Check(!EliteGuildWar.IsWar && Count("state='cancelled'")==1 && Count()==1,"restart cancels and cannot reopen the same slot");
            now=Local(14,21,1,5).ToUniversalTime();Tick();Check(EliteGuildWar.IsWar && Count()==2,"next eligible day opens independently");
            now=Local(14,21,50,0).ToUniversalTime();Tick();
            Check((bool)typeof(EliteGuildWar).GetField("closingWarningSent",Hidden).GetValue(null),"closing reminder fires before the saved deadline");
            Sql("CREATE FUNCTION reject_close() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.state='empty' THEN RAISE EXCEPTION 'fixture'; END IF; RETURN NEW; END $$;CREATE TRIGGER reject_close BEFORE UPDATE ON revora_elite_guild_battles FOR EACH ROW EXECUTE PROCEDURE reject_close();");
            now=Local(14,22,4,0).ToUniversalTime();bool failed=false;try{Tick();}catch(PostgresException){failed=true;}
            Check(failed && EliteGuildWar.IsWar && Count("state='active'")==1,"failed late settlement stays retryable");
            Sql("DROP TRIGGER reject_close ON revora_elite_guild_battles;DROP FUNCTION reject_close();");
            now=now.AddSeconds(4);Tick();Check(!EliteGuildWar.IsWar && Count("state='empty'")==1,"late poll closes and seals exactly once");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_elite_guild_battles WHERE gold<>0 OR rvp<>0 OR soul_type<>0"))==0,"empty scheduled battle fabricates no prizes");
            Call("InitializeLedger");now=Local(14,21,1,20).ToUniversalTime();Tick();Check(Count()==2 && !EliteGuildWar.IsWar,"completed date cannot reopen after restart");
            now=Local(21,21,1,5).ToUniversalTime();
            Sql("CREATE FUNCTION reject_open() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture'; END $$;CREATE TRIGGER reject_open BEFORE INSERT ON revora_elite_guild_battles FOR EACH ROW EXECUTE PROCEDURE reject_open();");
            failed=false;try{Tick();}catch(PostgresException){failed=true;}
            Check(failed && !EliteGuildWar.IsWar && Count()==2,"failed opening grants no active battle");
            Sql("DROP TRIGGER reject_open ON revora_elite_guild_battles;DROP FUNCTION reject_open();");
            now=Local(21,22,5,0).ToUniversalTime();Tick();Check(Count()==2 && !EliteGuildWar.IsWar,"uncommitted start cannot appear after its deadline");
            now=Local(28,21,1,5).ToUniversalTime();id=(Guid)Call("ScheduledIdentity",now.ToLocalTime());
            Set("pendingStartId",id);Set("pendingScheduledUntil",Local(28,22,0,0).ToUniversalTime());Call("SaveStart");
            Set("pendingStartId",id);Set("pendingScheduledUntil",Local(28,22,0,0).ToUniversalTime());
            now=Local(28,22,5,0).ToUniversalTime();Tick();
            Check(!EliteGuildWar.IsWar && Count()==3 && Count("state='empty'")==2,"late lost-reply recovery settles the saved battle without reopening");
            EliteGuildWar.Start();Check(EliteGuildWar.IsWar && Count("state='active' AND closes_at IS NULL")==1,"explicit manual starts retain their separate lifecycle");
            EliteGuildWar.End();Check(!EliteGuildWar.IsWar && Count("state='empty'")==3,"manual end still seals its original result");
            Console.WriteLine("Elite Guild schedule checks passed: "+checks);return 0;
        } finally {
            Environment.CurrentDirectory=prior;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            Sql("DROP SCHEMA elite_guild_schedule_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
}
