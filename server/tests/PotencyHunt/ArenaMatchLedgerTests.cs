using System;
using System.Threading.Tasks;
using Revora;
using Revora.Database;
using Revora.Game;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;

internal static partial class PotencyHuntTests
{
    static readonly Type ArenaLedger=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaMatchLedger");
    static Group LedgerGroup(Map town) {var group=new Group(ArenaPlayer(town),ArenaPlayer(town));group.Import();return group;}
    static long LedgerRows(Group group,string state) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM revora_arena_matches WHERE id=@id AND state=@state",c)) {
            q.Parameters.AddWithValue("id",ArenaGuid(group));q.Parameters.AddWithValue("state",state);return Convert.ToInt64(q.ExecuteScalar());}}
    }
    static Guid ArenaGuid(Group group){return (Guid)Field(group,"ParticipationKey");}
    static bool FinishLedger(Group group,bool commit=true) {
        lock(Field(group,"ParticipationSync"))using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            bool changed=(bool)Call(ArenaLedger,null,"Finish",c,t,group);if(commit)t.Commit();else t.Rollback();return changed;
        }}
    }
    static void VerifyArenaLedger(string runtime,Map town) {
        VerifyArenaParticipation(runtime,town);Call(ArenaLedger,null,"Initialize");Call(ArenaLedger,null,"Initialize");
        var now=new DateTime(2040,1,4,20,0,0,DateTimeKind.Utc);ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            var unopened=new Group(ArenaPlayer(town),ArenaPlayer(town));unopened.End();
            Check(!(bool)Call(ArenaLedger,null,"Start",unopened) && !FinishLedger(unopened),"unimported invitation outcome cannot create earned-match evidence");
            var group=LedgerGroup(town);int count=0;
            Parallel.For(0,8,i=>{if((bool)Call(ArenaLedger,null,"Start",group))System.Threading.Interlocked.Increment(ref count);});
            Check(count==8 && LedgerRows(group,"active")==1,"repeated imported starts preserve one durable match identity");
            Check(!FinishLedger(group),"an unfinished fight cannot create a terminal result");group.End(group.Player2);
            Check(FinishLedger(group,false) && LedgerRows(group,"active")==1 && LedgerRows(group,"finished")==0,"parent rollback preserves active match instead of a partial result");
            Query("CREATE FUNCTION suppress_arena_result() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_arena_result BEFORE UPDATE ON revora_arena_matches FOR EACH ROW EXECUTE FUNCTION suppress_arena_result()");
            bool rejected=false;try{FinishLedger(group);}catch(InvalidOperationException){rejected=true;}
            finally{Query("DROP TRIGGER suppress_arena_result ON revora_arena_matches;DROP FUNCTION suppress_arena_result()");}
            Check(rejected && LedgerRows(group,"active")==1,"suppressed result write cannot report success");
            now=now.AddDays(-1);Check(!FinishLedger(group),"clock rollback cannot finish before durable admission");now=now.AddDays(2);
            int finished=0;Parallel.For(0,8,i=>{if(FinishLedger(group))System.Threading.Interlocked.Increment(ref finished);});
            Check(finished==1 && LedgerRows(group,"finished")==1,"concurrent result callbacks record one winner and loser");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT winner,loser,day FROM revora_arena_matches WHERE id=@id",c)) {
                q.Parameters.AddWithValue("id",ArenaGuid(group));using(var r=q.ExecuteReader()){r.Read();Check(r.GetInt64(0)==group.Player1.Entity.UID && r.GetInt64(1)==group.Player2.Entity.UID && r.GetString(2)=="20400105","result freezes actual participants and Pacific completion day");}}}
            Call(ArenaLedger,null,"Initialize");Check(!FinishLedger(group),"schema reload cannot replay an already finished result");
            Parallel.For(0,8,i=>group.End(i%2==0?group.Player1:group.Player2));
            Check(ReferenceEquals(group.Winner,group.Player1) && ReferenceEquals(group.Loser,group.Player2),"concurrent repeated endings cannot change the frozen result");
            var missing=LedgerGroup(town);missing.End(missing.Player2);Check(!FinishLedger(missing),"completed but unrecorded admission cannot manufacture earned evidence");
            var exported=LedgerGroup(town);Call(ArenaLedger,null,"Start",exported);exported.End(exported.Player2);exported.Export();
            Check(!FinishLedger(exported) && LedgerRows(exported,"active")==1,"result must be persisted before Export destroys registered match ownership");
            var foreign=LedgerGroup(town);Call(ArenaLedger,null,"Start",foreign);foreign.End(foreign.Player2);foreign.Winner=ArenaPlayer(town);
            Check(!FinishLedger(foreign),"foreign winner cannot take a registered result");
            var detached=LedgerGroup(town);Call(ArenaLedger,null,"Start",detached);detached.End(detached.Player2);detached.Player2.QualifierGroup=null;
            Check(!FinishLedger(detached),"lost participant ownership cannot mint a result");
            var fault=LedgerGroup(town);
            Query("CREATE FUNCTION suppress_arena_start() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_arena_start BEFORE INSERT ON revora_arena_matches FOR EACH ROW EXECUTE FUNCTION suppress_arena_start()");
            rejected=false;try{Call(ArenaLedger,null,"Start",fault);}catch(InvalidOperationException){rejected=true;}
            finally{Query("DROP TRIGGER suppress_arena_start ON revora_arena_matches;DROP FUNCTION suppress_arena_start()");}
            Check(rejected && LedgerRows(fault,"active")==0,"failed admission receipt leaves no fabricated start");
            Check((bool)Call(ArenaLedger,null,"Start",fault),"admission can retry after rollback");
        }finally{ArenaLedger.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }
}
