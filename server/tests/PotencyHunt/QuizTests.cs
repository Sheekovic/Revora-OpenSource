using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type QuizEvent=typeof(Entity).Assembly.GetType("Revora.Game.Features.QuizShowEvent");
    static readonly Type QuizStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.QuizResults");
    static readonly Type QuizBank=typeof(Entity).Assembly.GetType("Revora.Game.Features.QuizQuestionsV1");
    static DateTime quizNow,quizStart;
    static string quizId;
    static void QuizNpc(GameState p,byte option) {MoonNpc(p,900000062,option);}
    static void QuizAt(DateTime start) {quizStart=start;quizNow=start.AddMinutes(-5);quizId=(string)Call(QuizEvent,null,"Identity",start);Call(QuizEvent,null,"Tick");}
    static long QuizRows(GameState p,string table) {
        string sql;
        switch(table) {
            case "revora_quiz_entries":sql="SELECT count(*) FROM revora_quiz_entries WHERE uid=@uid";break;
            case "revora_quiz_answers":sql="SELECT count(*) FROM revora_quiz_answers WHERE uid=@uid";break;
            case "revora_quiz_results":sql="SELECT count(*) FROM revora_quiz_results WHERE uid=@uid";break;
            default:throw new ArgumentException("Unknown fixture table.");
        }
        return Scalar(sql,p.Entity.UID);
    }
    static int QuizRight(int question) {
        int index;using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT deck[@question] FROM revora_quiz_runs WHERE id=@id",c)) {
            q.Parameters.AddWithValue("question",question+1);q.Parameters.AddWithValue("id",quizId);index=Convert.ToInt32(q.ExecuteScalar());
        }}
        var all=(Array)QuizBank.GetField("All",Hidden).GetValue(null);return (int)Field(all.GetValue(index),"Correct");
    }
    static void QuizAnswer(GameState p,int question,bool correct,int elapsed) {
        quizNow=quizStart.AddSeconds(question*30).AddMilliseconds(elapsed);QuizNpc(p,1);
        int answer=QuizRight(question);if(!correct)answer=(answer+1)%4;QuizNpc(p,(byte)(10+question*4+answer));
    }
    static bool QuizSoul(GameState p) {
        var souls=p.Inventory.Objects.Where(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7).ToArray();
        return souls.Length==1 && souls[0].Bound && Scalar("SELECT count(*) FROM revora_quiz_results r JOIN items i ON i.uid=ANY(r.rewards) WHERE r.uid=@uid AND r.claimed AND i.id BETWEEN 800000 AND 899999 AND i.bound=1",p.Entity.UID)==1;
    }
    static void VerifyQuiz(string runtime,Map town) {
        Query("ALTER TABLE entities ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN level integer DEFAULT 140,ADD COLUMN mapid integer DEFAULT 1002,ADD COLUMN x integer DEFAULT 0,ADD COLUMN y integer DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");Query("CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id=2000");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(6400000);Server.GamePool=new GameState[0];
        QuizEvent.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>quizNow));Call(QuizEvent,null,"Initialize");Call(QuizEvent,null,"PlaceNpc");
        Check(town.Npcs.ContainsKey(900000062) && Kernel.GetDistance(430,278,town.Npcs[900000062].X,town.Npcs[900000062].Y)<=8,"ordinary players can reach the quiz teacher in Twin City");
        Check(Call(QuizEvent,null,"Window",new DateTime(2026,9,18,21,0,0,DateTimeKind.Utc))==null,"weekday time does not open the weekend show");
        var start=new DateTime(2026,9,19,21,0,0,DateTimeKind.Utc);
        Check(Call(QuizEvent,null,"Window",start.AddMinutes(-5).AddTicks(-1))==null && (DateTime)Call(QuizEvent,null,"Window",start.AddMinutes(-5))==start
            && Call(QuizEvent,null,"Window",start.AddMinutes(10))==null,"registration and final-question boundaries are exclusive at their closing instant");
        Check((DateTime)Call(QuizEvent,null,"Window",new DateTime(2026,12,19,22,0,0,DateTimeKind.Utc))==new DateTime(2026,12,19,22,0,0,DateTimeKind.Utc),"winter weekend schedule follows Pacific standard time");
        QuizAt(start);var a=MoonPlayer(town);var b=MoonPlayer(town);var wrong=MoonPlayer(town);var outsider=MoonPlayer(town);
        outsider.Fake=true;QuizNpc(outsider,2);outsider.Fake=false;outsider.ActiveNpc=900000062;outsider.Entity.X=1;outsider.Entity.Y=1;Npcs.GetDialog(new NpcRequest(true){NpcID=900000062,OptionID=2},outsider);
        Check(QuizRows(outsider,"revora_quiz_entries")==0,"fake and remote requests cannot enroll in the show");
        QuizNpc(a,2);MoonNear(a,900000062);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=900000062,OptionID=2},a));QuizNpc(b,2);QuizNpc(wrong,2);
        Check(QuizRows(a,"revora_quiz_entries")==1 && QuizRows(b,"revora_quiz_entries")==1,"concurrent enrollment reserves one ordinary-player place");
        quizNow=start;QuizNpc(outsider,2);QuizNpc(outsider,1);QuizNpc(outsider,10);
        Check(QuizRows(outsider,"revora_quiz_entries")==0 && QuizRows(outsider,"revora_quiz_answers")==0,"late enrollment and answers from a nonparticipant are rejected");
        QuizNpc(a,10);Check(QuizRows(a,"revora_quiz_answers")==0,"an answer requires a server-issued current question");
        foreach(var bytes in new[]{new byte[0],new byte[4],new byte[9],new byte[100]})OpenQuiz.Process(bytes,a);
        Check(QuizRows(a,"revora_quiz_answers")==0,"disabled native 2068 input cannot crash or award quiz progress");
        QuizAnswer(a,0,true,100);byte replay=(byte)(10+QuizRight(0));MoonNear(a,900000062);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=900000062,OptionID=replay},a));QuizNpc(a,1);QuizNpc(a,(byte)(10+(QuizRight(0)+1)%4));
        Check(QuizRows(a,"revora_quiz_answers")==1,"replayed and changed submissions cannot replace a sealed answer");
        quizNow=start.AddSeconds(30);QuizNpc(a,replay);Check(QuizRows(a,"revora_quiz_answers")==1,"an expired question's answer cannot become the next question's answer");
        QuizNpc(a,5);quizNow=start.AddSeconds(31);Call(QuizEvent,null,"Tick");
        Check(a.ActiveNpc==0 && QuizRows(a,"revora_quiz_answers")==1,"leaving the quiz table pauses automatic prompts without inventing answers");
        QuizAnswer(b,0,true,1100);QuizAnswer(wrong,0,false,2100);
        for(int q=1;q<4;q++){QuizAnswer(a,q,true,100);QuizAnswer(b,q,true,1100);QuizAnswer(wrong,q,false,2100);}
        Check(!HasAchievement(a,10120) && !HasAchievement(wrong,10120),"four correct or only wrong answers earn no five-answer achievement");
        Query("ALTER TABLE revora_quiz_answers ADD CONSTRAINT quiz_answer_failure CHECK(question<4) NOT VALID");QuizAnswer(a,4,true,100);
        Check(Fenced(a) && QuizRows(a,"revora_quiz_answers")==4 && !HasAchievement(a,10120),"failed fifth-answer write rolls back progress and fences the player");
        Query("ALTER TABLE revora_quiz_answers DROP CONSTRAINT quiz_answer_failure");DemonReset(a);QuizAnswer(a,4,true,100);
        Check(QuizRows(a,"revora_quiz_answers")==5 && HasAchievement(a,10120),"fifth distinct correct answer and achievement commit together");
        QuizAnswer(b,4,true,1100);QuizAnswer(wrong,4,false,2100);
        string deck;using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT array_to_string(deck,',') FROM revora_quiz_runs WHERE id=@id",c)){q.Parameters.AddWithValue("id",quizId);deck=(string)q.ExecuteScalar();}}
        Call(QuizEvent,null,"Initialize");Call(QuizEvent,null,"Tick");QuizNpc(a,1);QuizNpc(a,(byte)(26+QuizRight(4)));
        Check(QuizRows(a,"revora_quiz_answers")==5,"restart reconstructs a sealed current answer instead of permitting replay");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT array_to_string(deck,',') FROM revora_quiz_runs WHERE id=@id",c)){q.Parameters.AddWithValue("id",quizId);Check((string)q.ExecuteScalar()==deck,"restart preserves the session's exact randomized question order");}}
        for(int q=5;q<20;q++){QuizAnswer(a,q,true,100);QuizAnswer(b,q,true,1100);QuizAnswer(wrong,q,false,2100);}
        Check(QuizRows(a,"revora_quiz_answers")==20 && QuizRows(b,"revora_quiz_answers")==20 && !HasAchievement(a,11128),"twenty answers never grant winner evidence before settlement");
        QuizNpc(a,3);Check(a.Inventory.Count==0,"unfinished show cannot pay a prize");
        quizNow=start.AddMinutes(10).AddTicks(-1);Call(QuizStore,null,"Finish",quizId);Check(QuizRows(a,"revora_quiz_results")==0,"settlement cannot run before the last question closes");
        Query("ALTER TABLE revora_quiz_results ADD CONSTRAINT quiz_result_failure CHECK(prize=0) NOT VALID");quizNow=start.AddMinutes(10);Call(QuizEvent,null,"Tick");
        Check(QuizRows(a,"revora_quiz_results")==0 && !HasAchievement(a,11128),"failed settlement creates neither partial results nor winner evidence");
        Query("ALTER TABLE revora_quiz_results DROP CONSTRAINT quiz_result_failure");quizNow=quizNow.AddSeconds(31);Call(QuizEvent,null,"Tick");
        Check(QuizRows(a,"revora_quiz_results")==1 && HasAchievement(a,11128) && !HasAchievement(b,11128),"recovered settlement awards only the faster competitive winner");
        Check(Scalar("SELECT score FROM revora_quiz_results WHERE uid=@uid",a.Entity.UID)==800 && Scalar("SELECT prize FROM revora_quiz_results WHERE uid=@uid",b.Entity.UID)==2 && Scalar("SELECT prize FROM revora_quiz_results WHERE uid=@uid",wrong.Entity.UID)==0,"persisted scores and prizes distinguish winner, runner-up and unsuccessful answers");
        Parallel.For(0,12,i=>Call(QuizStore,null,"Finish",quizId));Check(QuizRows(a,"revora_quiz_results")==1,"concurrent settlement replay cannot duplicate results");
        var full=Enumerable.Range(0,37).Select(i=>Pack(a,1088001,1)).ToArray();QuizNpc(a,3);Check(a.Inventory.Count==37 && Scalar("SELECT count(*) FROM revora_quiz_results WHERE uid=@uid AND claimed",a.Entity.UID)==0,"three free slots cannot partially claim three EXP Balls plus a P7 soul");
        foreach(var item in full){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);a.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        Query("ALTER TABLE items ADD CONSTRAINT quiz_soul_failure CHECK(id<800000 OR id>=900000) NOT VALID");QuizNpc(a,3);
        Check(Fenced(a) && a.Inventory.Count==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",a.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_quiz_results WHERE uid=@uid AND claimed",a.Entity.UID)==0,
            "failed soul insertion rolls back EXP Balls and leaves the entire prize unclaimed");
        Query("ALTER TABLE items DROP CONSTRAINT quiz_soul_failure");DemonReset(a);
        Query("ALTER TABLE revora_quiz_results ADD CONSTRAINT quiz_claim_failure CHECK(NOT claimed) NOT VALID");QuizNpc(a,3);
        Check(Fenced(a) && a.Inventory.Count==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",a.Entity.UID)==0,"failed prize receipt rolls back created items and fences further actions");
        Query("ALTER TABLE revora_quiz_results DROP CONSTRAINT quiz_claim_failure");DemonReset(a);MoonNear(a,900000062);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=900000062,OptionID=3},a));
        Check(a.Inventory.Count==4 && a.Inventory.Objects.Count(i=>i.ID==722136 && i.Bound)==3 && QuizSoul(a),"concurrent winner claims create exactly three bound EXP Balls and one bound P7 soul");
        var earnedUids=a.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();
        foreach(var item in a.Inventory.Objects)a.Inventory.Remove(item.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(a);a.Entity.MyAchievement=new Achievement(a.Entity);a.Entity.MyAchievement.Load("");a.Entity.MyAchievement.Send();QuizNpc(a,3);
        Check(a.Inventory.Count==4 && QuizSoul(a) && HasAchievement(a,10120) && HasAchievement(a,11128),"ordinary item and achievement reload retain awards without repaying the claim");
        Check(a.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(earnedUids),"item reload and claim replay preserve the exact earned soul and EXP Ball UIDs");
        QuizNpc(b,3);QuizNpc(wrong,3);Check(b.Inventory.Count==3 && QuizSoul(b) && wrong.Inventory.Count==0,"runner-up receives two balls and a bound P7 soul; unsuccessful entrant receives none");
        QuizAt(new DateTime(2026,9,20,4,0,0,DateTimeKind.Utc));var solo=MoonPlayer(town);QuizNpc(solo,2);
        for(int q=0;q<5;q++)QuizAnswer(solo,q,true,100);quizNow=quizStart.AddMinutes(10);Call(QuizEvent,null,"Tick");QuizNpc(solo,3);
        Check(solo.Inventory.Count==2 && QuizSoul(solo) && HasAchievement(solo,10120) && !HasAchievement(solo,11128),"solo practice earns its answer reward but no competitive winner achievement");
        QuizAt(new DateTime(2026,9,20,11,0,0,DateTimeKind.Utc));var tie1=MoonPlayer(town);var tie2=MoonPlayer(town);QuizNpc(tie1,2);QuizNpc(tie2,2);
        for(int q=0;q<5;q++){QuizAnswer(tie1,q,true,100);QuizAnswer(tie2,q,true,100);}quizNow=quizStart.AddMinutes(10);Call(QuizEvent,null,"Tick");QuizNpc(tie1,3);QuizNpc(tie2,3);
        Check(HasAchievement(tie1,11128) && HasAchievement(tie2,11128) && tie1.Inventory.Count==4 && tie2.Inventory.Count==4 && QuizSoul(tie1) && QuizSoul(tie2),"exact competitive ties share first place without character-ID favoritism");
        QuizAt(new DateTime(2026,9,21,4,0,0,DateTimeKind.Utc));
        Query("INSERT INTO revora_quiz_entries(run,uid) SELECT @run,generate_series(2000000,2000255)","run",quizId);
        QuizNpc(outsider,2);Check(QuizRows(outsider,"revora_quiz_entries")==0,"the participant cap rejects a 257th entrant");
        QuizAt(new DateTime(2026,9,26,21,0,0,DateTimeKind.Utc));var legacy=MoonPlayer(town);
        Query("UPDATE revora_quiz_runs SET valuable_reward=false WHERE id=@id","id",quizId);QuizNpc(legacy,2);
        Call(QuizEvent,null,"Initialize");Call(QuizEvent,null,"Tick");
        for(int q=0;q<5;q++)QuizAnswer(legacy,q,true,100);quizNow=quizStart.AddMinutes(10);Call(QuizEvent,null,"Tick");QuizNpc(legacy,3);QuizNpc(legacy,3);
        Check(legacy.Inventory.Count==1 && legacy.Inventory.Objects.All(i=>i.ID==722136 && i.Bound)
            && Scalar("SELECT cardinality(rewards) FROM revora_quiz_results WHERE uid=@uid AND claimed",legacy.Entity.UID)==1,
            "older show survives restart and earns its original EXP-only prize once");

    }
}
