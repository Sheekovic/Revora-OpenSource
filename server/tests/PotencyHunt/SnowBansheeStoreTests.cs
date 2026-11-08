using System;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static readonly Type SnowStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.SnowBansheeStore");
    static object SnowGate {get{return SnowStore.GetField("Gate",Hidden).GetValue(null);}}
    static object SnowCall(string method,params object[] args) {lock(SnowGate)return Call(SnowStore,null,method,args);}
    static object SnowAcquire(Map map,Guid lease) {return SnowCall("Acquire",map,lease,new Func<bool>(()=>true));}
    static bool SnowSave(Map map,object run,long sequence,uint hp,ushort x=370,ushort y=365) {
        return (bool)SnowCall("Checkpoint",map,run,sequence,hp,x,y,new Func<bool>(()=>true));
    }
    static bool SnowVictory(Map map,object run,bool commit=true,Func<bool> proof=null) {
        lock(SnowGate)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            bool changed=(bool)Call(SnowStore,null,"Victory",c,t,map,run,700001u,new uint[]{700002,700001},(ushort)370,(ushort)365,proof??new Func<bool>(()=>true));
            if(changed)using(var q=new NpgsqlCommand("INSERT INTO snow_fixture_rewards(run) VALUES(@run)",c,t)) {
                q.Parameters.AddWithValue("run",(Guid)Field(run,"Id"));q.ExecuteNonQuery();
            }
            if(commit)t.Commit();return changed;
        }}
    }
    static long SnowCount(string clause) {return Scalar("SELECT count(*) FROM revora_snow_banshee_runs WHERE "+clause,0);}
    static void SnowRefuses(Action action,string description) {
        bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}
        Check(rejected,description);
    }
    static void VerifySnowBansheeStore(string runtime) {
        var map=World(runtime,1927);var now=new DateTime(2026,9,21,13,17,0,DateTimeKind.Utc);
        SnowStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            SnowRefuses(()=>Call(SnowStore,null,"Initialize",map),"Banshee storage rejects missing lifecycle ownership");
            SnowCall("Initialize",map);SnowCall("Initialize",map);
            Query("CREATE TABLE snow_fixture_rewards(run uuid PRIMARY KEY REFERENCES revora_snow_banshee_runs(id))");
            Check(!map.Floor[341,362,MapObjectType.Player] && (bool)SnowCall("Site",map,(ushort)370,(ushort)365),"reviewed public Banshee clearing replaces the blocked guide coordinate");
            bool clear=true;for(int dx=-12;dx<=12;dx++)for(int dy=-12;dy<=12;dy++)if(dx*dx+dy*dy<=144 && !map.Floor[370+dx,365+dy,MapObjectType.Player])clear=false;
            Check(clear,"Banshee center has a twelve-tile walkable attack clearing");
            var metadata=MapsTable.MapInformations[1927];metadata.Owner=99;MapsTable.MapInformations[1927]=metadata;
            SnowRefuses(()=>SnowAcquire(map,Guid.NewGuid()),"owned map metadata cannot acquire a public Banshee");metadata.Owner=0;MapsTable.MapInformations[1927]=metadata;
            SnowRefuses(()=>SnowAcquire(map,Guid.Empty),"empty actor lease cannot create a run");
            now=DateTime.SpecifyKind(now,DateTimeKind.Unspecified);
            SnowRefuses(()=>SnowAcquire(map,Guid.NewGuid()),"ambiguous local time cannot schedule Banshee");now=DateTime.SpecifyKind(now,DateTimeKind.Utc);
            int proofs=0;
            SnowRefuses(()=>SnowCall("Acquire",map,Guid.NewGuid(),new Func<bool>(()=>++proofs==1)),"changed spawn ownership rolls back run and schedule");
            Check(SnowCount("true")==0,"failed spawn leaves no durable run");
            var lease=Guid.NewGuid();var run=SnowAcquire(map,lease);Guid id=(Guid)Field(run,"Id");
            Check((uint)Field(run,"Hp")==100000000 && (ushort)Field(run,"X")==370 && SnowCount("state='active'")==1,"first due spawn starts one full-health persisted run");
            Check((Guid)Field(SnowAcquire(map,lease),"Id")==id,"repeated same-actor acquisition cannot stack bosses");
            Check(SnowSave(map,run,2,73000000,371,365) && !SnowSave(map,run,1,90000000),"older health checkpoints cannot restore superseded damage");
            Check(SnowSave(map,run,3,75000000),"a newer healing checkpoint can raise health within the cap");
            SnowRefuses(()=>SnowSave(map,run,4,0),"zero health requires atomic victory, not a checkpoint");
            SnowRefuses(()=>SnowSave(map,run,4,100000001),"over-cap health is rejected");
            SnowRefuses(()=>SnowSave(map,run,4,500,65535,65535),"wide coordinate arithmetic rejects distant wrapped coordinates");
            SnowRefuses(()=>SnowSave(map,run,4,500,341,362),"blocked terrain cannot be saved as a recovery location");
            var resumed=SnowAcquire(map,Guid.NewGuid());
            Check((Guid)Field(resumed,"Id")==id && (uint)Field(resumed,"Hp")==75000000 && (long)Field(resumed,"Sequence")==0,"actor reconstruction retains run and health with a fresh checkpoint generation");
            Check(!SnowSave(map,run,100,100000000) && !SnowVictory(map,run),"replaced actor cannot heal or defeat its successor");
            Check(SnowSave(map,resumed,1,64000000),"reconstructed actor can save new damage");
            now=now.AddHours(6);SnowCall("Initialize",map);
            Check((Guid)Field(SnowAcquire(map,(Guid)Field(resumed,"Lease")),"Id")==id && SnowCount("state='active'")==1,"elapsed refresh intervals and repeated initialization never duplicate a surviving boss");
            Check(SnowVictory(map,resumed,false) && SnowCount("state='active'")==1 && Scalar("SELECT count(*) FROM snow_fixture_rewards",0)==0,"uncommitted rewards roll back victory and cooldown together");
            Query(@"CREATE FUNCTION snow_fixture_fail() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'fixture reward unavailable'; END; $$ LANGUAGE plpgsql;
CREATE TRIGGER snow_fixture_fail BEFORE INSERT ON snow_fixture_rewards FOR EACH ROW EXECUTE PROCEDURE snow_fixture_fail();");
            bool failed=false;try{SnowVictory(map,resumed);}catch(PostgresException){failed=true;}
            Query("DROP TRIGGER snow_fixture_fail ON snow_fixture_rewards;DROP FUNCTION snow_fixture_fail()");
            Check(failed && SnowCount("state='active'")==1 && Scalar("SELECT count(*) FROM snow_fixture_rewards",0)==0,"reward write failure leaves the boss active and claimable");
            proofs=0;SnowRefuses(()=>SnowVictory(map,resumed,true,()=>++proofs==1),"changed victory authority aborts all durable changes");
            Check(SnowCount("state='active'")==1,"victory proof failure preserves active state");
            var contenders=new object[8];Parallel.For(0,8,i=>contenders[i]=SnowAcquire(map,Guid.NewGuid()));
            Check(contenders.All(r=>(Guid)Field(r,"Id")==id) && SnowCount("true")==1,"concurrent recovery claims retain a single encounter identity");
            resumed=SnowAcquire(map,Guid.NewGuid());var wins=new bool[8];Parallel.For(0,8,i=>wins[i]=SnowVictory(map,resumed));
            Check(wins.Count(x=>x)==1 && SnowCount("state='victory'")==1 && Scalar("SELECT count(*) FROM snow_fixture_rewards",0)==1,"concurrent death callbacks commit exactly one victory and reward receipt");
            Check(!SnowSave(map,resumed,500,1) && !SnowVictory(map,resumed),"retired generation cannot revive or replay a defeated boss");
            Check(Scalar("SELECT count(*) FROM revora_snow_banshee_runs WHERE hp=0 AND killer=700001 AND party=ARRAY[700001,700002]::bigint[]",0)==1,"victory preserves exact killer and canonical frozen recipients");
            SnowCall("Initialize",map);Check(SnowAcquire(map,Guid.NewGuid())==null,"restart after victory retains the respawn delay");
            now=now.AddMinutes(30).AddTicks(-10);Check(SnowAcquire(map,Guid.NewGuid())==null,"boss remains absent one microsecond before the cooldown ends");
            now=now.AddTicks(10);var next=SnowAcquire(map,Guid.NewGuid());
            Check(next!=null && (Guid)Field(next,"Id")!=id && (uint)Field(next,"Hp")==100000000 && SnowCount("state='active'")==1,"exact thirty-minute boundary creates a fresh full-health encounter");
            Check(!SnowSave(map,resumed,501,1) && !SnowVictory(map,resumed),"previous run cannot mutate the next scheduled boss");
        } finally {SnowStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }
}
