using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;

internal static partial class PotencyHuntTests
{
    static readonly Type ArenaResults=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaResultStatistics");
    static Group StatGroup(Map town, bool ended=true, bool imported=true) {
        var group=imported?LedgerGroup(town):new Group(ArenaPlayer(town),ArenaPlayer(town));
        foreach(var p in new[]{group.Player1,group.Player2}) {
            p.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=p.Entity.UID};p.ArenaPoints=1000;
            p.ArenaStatistic.LastArenaPointFill=DateTime.Now;
            Query("INSERT INTO arena(entityid,entityname,level,class,arenapoints,todaywin,todaybattles,totalwin,totallose,arenapointfill) VALUES(@uid,@name,40,10,1000,0,0,0,0,@stamp);INSERT INTO teamarena(entityid,arenapoints) VALUES(@uid,1000)",
                "uid",(long)p.Entity.UID,"name",p.Entity.Name,"stamp",DateTime.Now.Ticks);
        }
        if(imported){Call(ArenaLedger,null,"Start",group);if(ended)group.End(group.Player2);}return group;
    }
    static bool CommitArenaStatistics(Group group,bool commit=true,bool daily=false,bool custom=false) {
        var players=new[]{group.Player1,group.Player2}.OrderBy(p=>p.Entity.UID).ToArray();
        lock(Field(group,"ParticipationSync"))lock(players[0].ItemSyncRoot)lock(players[1].ItemSyncRoot)
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            if(!(bool)Call(ArenaLedger,null,"Finish",c,t,group))return false;
            var snapshot=Call(ArenaResults,null,"Record",c,t,group);
            if(daily)Call(ArenaDaily,null,"Record",c,t,group);
            if(custom)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCustomRewards"),null,"Record",c,t,group);
            if(!commit){t.Rollback();return true;}
            t.Commit();Call(ArenaResults,null,"Publish",snapshot);return true;
        }}
    }
    static void VerifyArenaResults(string runtime,Map town) {
        VerifyArenaLedger(runtime,town);Query("CREATE TABLE teamarena AS TABLE public.teamarena WITH NO DATA");Call(ArenaResults,null,"Initialize");
        var p=StatGroup(town);Check(CommitArenaStatistics(p,false) && LedgerRows(p,"active")==1
            && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Player1.Entity.UID)==1000 && p.Player1.ArenaStatistic.TodayWin==0,
            "parent rollback retains both saved statistics and caches with active result");
        int wins=0;Parallel.For(0,8,n=>{if(CommitArenaStatistics(p))System.Threading.Interlocked.Increment(ref wins);});
        Check(wins==1 && p.Player1.ArenaPoints==1030 && p.Player2.ArenaPoints==970 && p.Player1.TeamArenaStatistic.ArenaPoints==1030,
            "one owned result conserves the three-percent point transfer and publishes mirrors");
        Check(p.Player1.ArenaStatistic.TodayBattles==1 && p.Player1.ArenaStatistic.TodayWin==1 && p.Player2.ArenaStatistic.TodayBattles==1
            && p.Player2.ArenaStatistic.TodayWin==0 && p.Player1.ArenaStatistic.TotalWin==1 && p.Player2.ArenaStatistic.TotalLose==1,
            "winner and loser both count a match while wins and losses stay distinct");
        ArenaTable.SaveArenaStatistics(p.Player1.ArenaStatistic);var loaded=(QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Player1.Entity.UID);
        Check(loaded.ArenaPoints==1030 && loaded.TodayBattles==1 && loaded.TodayWin==1 && loaded.TotalWin==1,
            "ordinary statistic save and production reload preserve committed points and counters");
        p.Export();var again=new Group(p.Player1,p.Player2);again.Import();Call(ArenaLedger,null,"Start",again);again.End(again.Player1);
        Check(CommitArenaStatistics(again) && again.Player1.ArenaStatistic.TodayBattles==2 && again.Player1.ArenaStatistic.TodayWin==1
            && again.Player2.ArenaStatistic.TodayBattles==2 && again.Player2.ArenaStatistic.TodayWin==1,
            "second owned match derives daily counters from distinct committed result receipts");
        foreach(string table in new[]{"revora_arena_result_statistics","arena","teamarena"}) {
            var fault=StatGroup(town);string verb=table=="revora_arena_result_statistics"?"INSERT":"UPDATE";
            string identity=table=="revora_arena_result_statistics"?"uid":"entityid";
            Query("CREATE FUNCTION suppress_arena_statistics() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW."+identity+"=(SELECT max(entityid) FROM arena) THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_arena_statistics BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_arena_statistics()");
            bool rejected=false;try{CommitArenaStatistics(fault);}catch(InvalidOperationException){rejected=true;}
            finally{Query("DROP TRIGGER suppress_arena_statistics ON "+table+";DROP FUNCTION suppress_arena_statistics()");}
            Check(rejected && LedgerRows(fault,"active")==1 && fault.Player1.ArenaPoints==1000 && fault.Player2.ArenaPoints==1000
                && Scalar("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid",fault.Player1.Entity.UID)==0
                && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",fault.Player1.Entity.UID)==1000
                && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",fault.Player1.Entity.UID)==1000,
                "failed second participant rolls back result, receipts and both statistics: "+table);
            Check(CommitArenaStatistics(fault),"failed statistic transaction retries once: "+table);
        }
        var stale=StatGroup(town);stale.Player1.ArenaStatistic.TotalWin=10;bool blocked=false;
        try{CommitArenaStatistics(stale);}catch(InvalidOperationException){blocked=true;}
        Check(blocked && LedgerRows(stale,"active")==1,"stale statistic cache cannot overwrite durable counters");
        foreach(var pair in new[]{new uint[]{0,0},new uint[]{1000,33},new uint[]{1000,34},new uint[]{uint.MaxValue,4000},new uint[]{uint.MaxValue-1,uint.MaxValue}}) {
            uint transfer=(uint)Call(ArenaResults,null,"Transfer",pair[0],pair[1]);
            Check((ulong)pair[0]+transfer<=uint.MaxValue && transfer<=pair[1] && transfer<=((ulong)pair[1]*3/100),"Arena transfer respects unsigned capacity: "+pair[0]+"/"+pair[1]);
        }
        Check((uint)Call(ArenaResults,null,"Transfer",1000u,33u)==0 && (uint)Call(ArenaResults,null,"Transfer",1000u,34u)==1,
            "Revora three-percent rounding is explicitly floored at the boundary");
    }
}
