using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class QuizResults
    {
        internal sealed class Run {internal string Id;internal DateTime Start;internal int[] Deck;internal bool Finished;}
        private sealed class Result {internal uint Uid;internal int Correct,Score,Count,Rank,Prize;internal long Elapsed;}
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_quiz_runs(
id varchar(10) PRIMARY KEY,starts_at timestamptz NOT NULL,deck integer[] NOT NULL CHECK(cardinality(deck)=20),
finished boolean NOT NULL DEFAULT false,rule_version integer NOT NULL CHECK(rule_version=1));
CREATE TABLE IF NOT EXISTS revora_quiz_entries(
run varchar(10) REFERENCES revora_quiz_runs(id),uid bigint CHECK(uid>0),PRIMARY KEY(run,uid));
CREATE TABLE IF NOT EXISTS revora_quiz_answers(
run varchar(10),uid bigint,question integer CHECK(question BETWEEN 0 AND 19),choice integer NOT NULL CHECK(choice BETWEEN 0 AND 3),
correct boolean NOT NULL,score integer NOT NULL CHECK(score BETWEEN 0 AND 40),elapsed integer NOT NULL CHECK(elapsed BETWEEN 0 AND 29999),
PRIMARY KEY(run,uid,question),FOREIGN KEY(run,uid) REFERENCES revora_quiz_entries(run,uid));
CREATE TABLE IF NOT EXISTS revora_quiz_results(
run varchar(10),uid bigint,correct integer NOT NULL CHECK(correct BETWEEN 0 AND 20),answered integer NOT NULL CHECK(answered BETWEEN correct AND 20),
score integer NOT NULL CHECK(score BETWEEN 0 AND 800),elapsed bigint NOT NULL,rank integer NOT NULL CHECK(rank BETWEEN 0 AND 256),
prize integer NOT NULL CHECK(prize BETWEEN 0 AND 3),claimed boolean NOT NULL DEFAULT false,rewards bigint[] NOT NULL DEFAULT '{}',
PRIMARY KEY(run,uid),FOREIGN KEY(run,uid) REFERENCES revora_quiz_entries(run,uid));
ALTER TABLE revora_quiz_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;");}
        }
        internal static Run Read(NpgsqlConnection c,NpgsqlTransaction t,string id,bool exclusive=false) {
            string sql=t==null?"SELECT starts_at,deck,finished,rule_version FROM revora_quiz_runs WHERE id=@id":exclusive?
                "SELECT starts_at,deck,finished,rule_version FROM revora_quiz_runs WHERE id=@id FOR UPDATE":
                "SELECT starts_at,deck,finished,rule_version FROM revora_quiz_runs WHERE id=@id FOR SHARE";
            using(var q=Q.Command(c,t,sql,"id",id))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())return null;
                var run=new Run{Id=id,Start=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime,Deck=r.GetFieldValue<int[]>(1),Finished=r.GetBoolean(2)};
                if(r.GetInt32(3)!=1 || run.Deck.Length!=20 || run.Deck.Distinct().Count()!=20 || run.Deck.Any(n=>n<0 || n>=QuizQuestionsV1.All.Length))throw new InvalidOperationException("Quiz version or question deck invalid.");
                return run;
            }
        }
        internal static void Ensure(string id,DateTime start) {
            var deck=Enumerable.Range(0,QuizQuestionsV1.All.Length).ToArray();
            for(int i=deck.Length-1;i>0;i--){int j=Kernel.Random.Next(i+1),swap=deck[i];deck[i]=deck[j];deck[j]=swap;}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"INSERT INTO revora_quiz_runs(id,starts_at,deck,rule_version,valuable_reward) VALUES(@id,@start,@deck,1,true) ON CONFLICT(id) DO NOTHING","id",id,"start",start,"deck",deck.Take(20).ToArray());}
        }
        internal static int Question(Run run,DateTime utc) {return utc<run.Start || utc>=run.Start.AddMinutes(10)?-1:(int)((utc-run.Start).TotalSeconds/30);}
        internal static string Join(GameState p,string id) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var run=Read(c,t,id,true);var now=QuizShowEvent.Clock();
                if(run==null || run.Finished || now<run.Start.AddMinutes(-5) || now>=run.Start)return "The next show opens five minutes before its first question.";
                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_quiz_entries WHERE run=@run","run",id))if(Convert.ToInt32(q.ExecuteScalar())>=256)return "My courtyard is full for this show.";
                Q.Execute(c,t,"INSERT INTO revora_quiz_entries(run,uid) VALUES(@run,@uid) ON CONFLICT DO NOTHING","run",id,"uid",(long)p.Entity.UID);t.Commit();
                return "Your place is saved. Stay nearby; twenty questions await you.";
            }}
        }
        internal static bool Entered(NpgsqlConnection c,NpgsqlTransaction t,string id,uint uid) {
            string sql=t==null?"SELECT uid FROM revora_quiz_entries WHERE run=@run AND uid=@uid":"SELECT uid FROM revora_quiz_entries WHERE run=@run AND uid=@uid FOR UPDATE";
            using(var q=Q.Command(c,t,sql,"run",id,"uid",(long)uid))return q.ExecuteScalar()!=null;
        }
        internal static string Answer(GameState p,string id,int question,int choice) {
            if(question<0 || question>=20 || choice<0 || choice>=4)return "Choose one of the four answers.";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var run=Read(c,t,id);if(run==null || run.Finished || !Entered(c,t,id,p.Entity.UID))return "You have no active place in this show.";
                var now=QuizShowEvent.Clock();if(Question(run,now)!=question)return "That question has ended. Ask for the current one.";
                bool correct=QuizQuestionsV1.All[run.Deck[question]].Correct==choice;
                int elapsed=(int)(now-run.Start.AddSeconds(question*30)).TotalMilliseconds;
                int score=correct?40-(elapsed/1000):0;
                int added=Q.Execute(c,t,@"INSERT INTO revora_quiz_answers(run,uid,question,choice,correct,score,elapsed)
VALUES(@run,@uid,@question,@choice,@correct,@score,@elapsed) ON CONFLICT DO NOTHING","run",id,"uid",(long)p.Entity.UID,"question",question,"choice",choice,"correct",correct,"score",score,"elapsed",elapsed);
                if(added==0)return "Your answer is already sealed.";
                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_quiz_answers WHERE run=@run AND uid=@uid AND correct","run",id,"uid",(long)p.Entity.UID))
                    if(Convert.ToInt32(q.ExecuteScalar())>=5)ProgressionEvidence.Complete(c,t,p.Entity.UID,10120,"quiz-local-v1",id,1);
                t.Commit();Q.Refresh(p);return "Your answer is sealed. Wait for the next question.";
            }}
        }
        internal static uint[] Finish(string id) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var run=Read(c,t,id,true);if(run==null || run.Finished || QuizShowEvent.Clock()<run.Start.AddMinutes(10))return new uint[0];
                var rows=new List<Result>();
                using(var q=Q.Command(c,t,@"SELECT e.uid,count(a.question),count(a.question) FILTER(WHERE a.correct),coalesce(sum(a.score),0),coalesce(sum(a.elapsed),0)
FROM revora_quiz_entries e LEFT JOIN revora_quiz_answers a ON a.run=e.run AND a.uid=e.uid
WHERE e.run=@run GROUP BY e.uid ORDER BY e.uid LIMIT 257","run",id))
                using(var r=q.ExecuteReader())while(r.Read())rows.Add(new Result{Uid=Convert.ToUInt32(r[0]),Count=Convert.ToInt32(r[1]),Correct=Convert.ToInt32(r[2]),Score=Convert.ToInt32(r[3]),Elapsed=Convert.ToInt64(r[4])});
                if(rows.Count>256)throw new InvalidOperationException("Quiz participant bound exceeded.");
                var ranked=rows.Where(r=>r.Correct>=5).OrderByDescending(r=>r.Score).ThenByDescending(r=>r.Correct).ThenBy(r=>r.Elapsed).ToArray();
                for(int i=0;i<ranked.Length;i++) {
                    var row=ranked[i];bool tied=i>0 && ranked[i-1].Score==row.Score && ranked[i-1].Correct==row.Correct && ranked[i-1].Elapsed==row.Elapsed;
                    row.Rank=tied?ranked[i-1].Rank:i+1;row.Prize=ranked.Length<2?1:row.Rank==1?3:row.Rank==2?2:1;
                }
                foreach(var row in rows) {
                    Q.Execute(c,t,@"INSERT INTO revora_quiz_results(run,uid,correct,answered,score,elapsed,rank,prize)
VALUES(@run,@uid,@correct,@answered,@score,@elapsed,@rank,@prize)","run",id,"uid",(long)row.Uid,"correct",row.Correct,"answered",row.Count,"score",row.Score,"elapsed",row.Elapsed,"rank",row.Rank,"prize",row.Prize);
                    if(ranked.Length>=2 && row.Rank==1)ProgressionEvidence.Complete(c,t,row.Uid,11128,"quiz-local-v1",id,1);
                }
                Q.Execute(c,t,"UPDATE revora_quiz_runs SET finished=true WHERE id=@id","id",id);t.Commit();return rows.Select(r=>r.Uid).ToArray();
            }}
        }
        internal static string Claim(GameState p) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                string id;int count;bool valuable;
                using(var q=Q.Command(c,t,"SELECT r.run,r.prize,s.valuable_reward FROM revora_quiz_results r JOIN revora_quiz_runs s ON s.id=r.run WHERE r.uid=@uid AND NOT r.claimed AND r.prize>0 ORDER BY r.run LIMIT 1 FOR UPDATE OF r","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader()){if(!r.Read())return "No prize is waiting. Answer five questions correctly in a show to earn one.";id=r.GetString(0);count=r.GetInt32(1);valuable=r.GetBoolean(2);}
                if(count<1 || count>3)throw new InvalidOperationException("Quiz prize invalid.");
                int slots=count+(valuable?1:0);
                if(p.Inventory.Count+slots>40)return "Make "+slots+" spaces in your pack, then return for your prize.";
                var rewards=Enumerable.Range(0,count).Select(_=>Q.New(722136)).ToList();
                if(valuable)rewards.Add(QuestSoulReward.Create());
                var items=rewards.ToArray();foreach(var item in items)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"UPDATE revora_quiz_results SET claimed=true,rewards=@rewards WHERE run=@run AND uid=@uid AND NOT claimed","rewards",items.Select(i=>(long)i.UID).ToArray(),"run",id,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Quiz claim receipt was not saved.");
                t.Commit();Q.Install(p,new uint[0],items);return "Well answered. Your "+count+" EXP Balls"+(valuable?" and bound P7 soul":"")+" are yours.";
            }}
        }
    }
}
