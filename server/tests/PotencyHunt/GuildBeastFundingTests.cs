using System;
using System.Linq;
using System.Threading.Tasks;
using Revora.Game;
using Revora.Database;
internal static partial class PotencyHuntTests {
    static void VerifyGuildBeastFunding() {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildBeastFunding",true);
        Query("ALTER TABLE entities ADD COLUMN guildid bigint,ADD COLUMN guildrank integer");
        Query("CREATE TABLE guilds(id bigint PRIMARY KEY,leadername text,silverfund bigint CHECK(silverfund>=0))");
        Query("CREATE TABLE revora_guild_battles(day varchar(10) PRIMARY KEY,state text,keeper bigint,starts_at timestamptz,ends_at timestamptz)");
        Call(type,null,"Initialize");
        var noon=new DateTime(2034,1,9,12,0,0,DateTimeKind.Local).ToUniversalTime();
        var ended=new DateTime(2034,1,8,16,0,0,DateTimeKind.Local).ToUniversalTime();
        Query("INSERT INTO entities(uid,name,guildid,guildrank) VALUES(51,'OrdinaryLeader',71,@rank),(52,'Member',71,0),(53,'OtherLeader',72,@rank)","rank",(int)Revora.Game.Enums.GuildMemberRank.GuildLeader);
        Query("INSERT INTO guilds VALUES(71,'OrdinaryLeader',1500000),(72,'OtherLeader',1500000)");
        Query("INSERT INTO revora_guild_battles VALUES('2034-01-08','won',71,@start,@end)","start",ended.AddHours(-1),"end",ended);
        Func<uint,uint,DateTime,string> reserve=(uid,guild,now)=>{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){var result=Call(type,null,"Reserve",c,t,uid,guild,now).ToString();t.Commit();return result;}}};
        Check(reserve(52,71,noon)=="Rejected" && reserve(53,72,noon)=="Rejected","saved member and losing leader cannot fund the Guild Beast");
        var results=new string[8];try{Parallel.For(0,results.Length,i=>results[i]=reserve(51,71,noon));}catch(AggregateException e){throw e.Flatten().InnerExceptions[0];}
        Check(results.Count(x=>x=="Funded")==1 && results.Count(x=>x=="AlreadyFunded")==7,"concurrent repeat reservations create exactly one daily funding entitlement");
        Check(Scalar("SELECT silverfund FROM guilds WHERE id=71",0)==1000000 && Scalar("SELECT count(*) FROM revora_guild_beast_days",0)==1,"daily reservation deducts exactly 500000 saved guild silver");
        Check(reserve(51,71,new DateTime(2034,1,10,21,0,0,DateTimeKind.Local).ToUniversalTime())=="Rejected","funding closes at the exact 21:00 spawn boundary");
        Query("UPDATE guilds SET silverfund=499999 WHERE id=71");
        Check(reserve(51,71,noon.AddDays(1))=="Rejected" && Scalar("SELECT count(*) FROM revora_guild_beast_days",0)==1,"insufficient funds leave no reserved day");
        Query("UPDATE guilds SET silverfund=1000000 WHERE id=71");
        Query("CREATE FUNCTION refuse_beast_debit() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER refuse_beast_debit BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION refuse_beast_debit()");
        bool failed=false;try{reserve(51,71,noon.AddDays(1));}catch(InvalidOperationException){failed=true;}
        finally{Query("DROP TRIGGER refuse_beast_debit ON guilds;DROP FUNCTION refuse_beast_debit()");}
        Check(failed && Scalar("SELECT count(*) FROM revora_guild_beast_days",0)==1 && Scalar("SELECT silverfund FROM guilds WHERE id=71",0)==1000000,"failed debit rolls back the new entitlement and preserves funds");
        Check(reserve(51,71,noon.AddDays(1))=="Funded","a fresh transaction retries a rolled-back reservation successfully");
        Query("UPDATE guilds SET leadername='Replacement' WHERE id=71");
        Check(reserve(51,71,noon.AddDays(2))=="Rejected","stale leader membership cannot override the saved guild leader");
        Query("UPDATE guilds SET leadername='OrdinaryLeader' WHERE id=71");
        Query("INSERT INTO revora_guild_battles VALUES('2034-01-10','empty',0,@start,@end)","start",noon.AddDays(1).AddHours(-2),"end",noon.AddDays(1).AddHours(-1));
        Check(reserve(51,71,noon.AddDays(2))=="Rejected","a newer empty battle invalidates the previous winning guild");
        Query("DELETE FROM revora_guild_battles WHERE day='2034-01-10'");
        Check(reserve(51,71,ended.AddDays(7))=="Rejected","a result cannot authorize funding after its seven-day lifetime");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            Check(Call(type,null,"Reserve",c,t,(uint)51,(uint)71,noon).ToString()=="AlreadyFunded","reopened database session observes the paid day without repaying");t.Commit();}}
        var runs=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildBeastRuns",true);Call(runs,null,"Initialize");
        Query("CREATE TABLE IF NOT EXISTS revora_achievement_evidence(uid bigint NOT NULL,achievement_id integer NOT NULL,source varchar(48),source_key varchar(96),rule_version integer,completed_at timestamptz DEFAULT CURRENT_TIMESTAMP,PRIMARY KEY(uid,achievement_id))");
        Func<string,object[],object> run=(method,args)=>{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            var values=new object[args.Length+2];values[0]=c;values[1]=t;Array.Copy(args,0,values,2,args.Length);var result=Call(runs,null,method,values);t.Commit();return result;}}};
        var start=noon.AddHours(9);Guid life=Guid.NewGuid();
        Check(!(bool)run("Bind",new object[]{"2034-01-09",life,(uint)401001,start.AddTicks(-1)}) && (bool)run("Bind",new object[]{"2034-01-09",life,(uint)401001,start}),
            "paid beast can bind exactly at its start, never before");
        Guid replacement=Guid.NewGuid();Check((bool)run("Bind",new object[]{"2034-01-09",replacement,(uint)401002,start.AddMinutes(1)})
            && !(bool)run("Defeat",new object[]{"2034-01-09",life,(uint)401001,(uint)51,start.AddMinutes(2)}),"restart rebind invalidates the previous runtime beast life");
        var outcomes=new bool[8];Parallel.For(0,8,i=>outcomes[i]=(bool)run("Defeat",new object[]{"2034-01-09",replacement,(uint)401002,(uint)(i%2==0?51:53),start.AddMinutes(2)}));
        Check(outcomes.Count(x=>x)==1 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10303",0)==1,
            "competing accepted deaths commit one daily result and one killer achievement");
        uint killer=(uint)Scalar("SELECT killer FROM revora_guild_beast_days WHERE day='2034-01-09'",0);
        var snapshot=run("Read",new object[]{"2034-01-09"});
        Check((string)snapshot.GetType().GetField("State",Instance).GetValue(snapshot)=="killed"
            && (Guid)snapshot.GetType().GetField("Life",Instance).GetValue(snapshot)==replacement
            && (uint)snapshot.GetType().GetField("Killer",Instance).GetValue(snapshot)==killer,
            "fresh database read retains the exact accepted beast life and killer");
        Check(!(bool)run("MarkClaim",new object[]{"2034-01-09",killer,(uint)9000,start}),"clock rollback cannot claim before the saved kill settlement");

        run("Advance",new object[]{start.AddHours(4)});
        Check((bool)run("MarkClaim",new object[]{"2034-01-09",killer,(uint)9001,start.AddHours(4)}) && !(bool)run("MarkClaim",new object[]{"2034-01-09",killer,(uint)9002,start.AddHours(4)}),
            "killer settlement remains claimable after midnight but records one delivery marker");
        var tomorrow=start.AddDays(1);Guid second=Guid.NewGuid();run("Bind",new object[]{"2034-01-10",second,(uint)401003,tomorrow});
        Query("CREATE FUNCTION refuse_daily_beast_evidence() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER refuse_daily_beast_evidence BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION refuse_daily_beast_evidence()");
        bool refused=false;try{run("Defeat",new object[]{"2034-01-10",second,(uint)401003,(uint)52,tomorrow.AddMinutes(30)});}catch(InvalidOperationException){refused=true;}
        finally{Query("DROP TRIGGER refuse_daily_beast_evidence ON revora_achievement_evidence;DROP FUNCTION refuse_daily_beast_evidence()");}
        Check(refused && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE day='2034-01-10' AND state='active' AND killer IS NULL",0)==1,
            "failed achievement write rolls back accepted kill settlement for retry");
        Check(!(bool)run("Defeat",new object[]{"2034-01-10",second,(uint)401003,(uint)52,tomorrow.AddHours(1)}),"a kill at the exact end cannot defeat the timeout");
        run("Advance",new object[]{tomorrow.AddHours(1)});
        Check(!(bool)run("MarkClaim",new object[]{"2034-01-10",(uint)53,(uint)9003,tomorrow.AddHours(1)}) && !(bool)run("MarkClaim",new object[]{"2034-01-10",(uint)52,(uint)9003,tomorrow.AddHours(1)}),
            "fallback reward rejects another guild and ordinary members");
        Query("UPDATE entities SET guildrank=@rank WHERE uid=52","rank",(int)Revora.Game.Enums.GuildMemberRank.DeputyLeader);
        Check((bool)run("MarkClaim",new object[]{"2034-01-10",(uint)52,(uint)9003,tomorrow.AddHours(1)}) && !(bool)run("MarkClaim",new object[]{"2034-01-10",(uint)51,(uint)9004,tomorrow.AddHours(1)}),
            "first eligible deputy fallback marker excludes a second leader claim");
        Check(reserve(51,71,noon.AddDays(2))=="Funded","a later paid day is independent of prior rewards");
        var midnight=tomorrow.AddDays(1).AddHours(3);run("Advance",new object[]{midnight});
        Check(!(bool)run("MarkClaim",new object[]{"2034-01-11",(uint)51,(uint)9005,midnight})
            && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE day='2034-01-11' AND state='expired'",0)==1,"missed funded days expire without an invented kill or late fallback");
    }
}
