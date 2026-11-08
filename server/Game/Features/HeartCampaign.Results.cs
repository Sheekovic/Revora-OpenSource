using System;
using System.Collections.Generic;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static partial class HeartCampaign
    {
        private static readonly object ResultsGate=new object();
        private static DateTime nextClosePoll;
        private static volatile bool resultsReady;
        private static void InitializeResults()
        {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_heart_scores(
weekend varchar(8) NOT NULL REFERENCES revora_heart_weekends(weekend),faction varchar(7) NOT NULL CHECK(faction IN('couples','singles')),
support bigint NOT NULL CHECK(support>=0),PRIMARY KEY(weekend,faction));
CREATE TABLE IF NOT EXISTS revora_heart_results(
weekend varchar(8) NOT NULL,uid bigint NOT NULL,faction varchar(7) NOT NULL CHECK(faction IN('couples','singles')),
support integer NOT NULL CHECK(support=300),cp integer NOT NULL CHECK(cp IN(0,5000)),rvp integer NOT NULL CHECK(rvp IN(0,1000)),
earned_at timestamptz NOT NULL,expires_at timestamptz NOT NULL,award_recorded boolean NOT NULL DEFAULT false,
PRIMARY KEY(weekend,uid),FOREIGN KEY(weekend,uid) REFERENCES revora_heart_entries(weekend,uid),
CHECK((cp=0 AND rvp=0) OR (cp=5000 AND rvp=1000)),CHECK(expires_at=earned_at+interval '720 hours'));");}
            resultsReady=true;
        }
        internal static bool Freeze(string weekend)
        {
            int key;
            if(weekend==null || weekend.Length!=8 || !int.TryParse(weekend,out key))throw new ArgumentException("Invalid Heart weekend.");
            DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new ArgumentException("Heart calendar requires UTC.");
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,"SELECT pg_advisory_xact_lock(6711,@key)","key",30000000+key);
                DateTime start,end;bool closed;
                using(var q=Q.Command(c,t,"SELECT starts_at,ends_at,closed FROM revora_heart_weekends WHERE weekend=@weekend FOR UPDATE","weekend",weekend))
                using(var r=q.ExecuteReader()){
                    if(!r.Read())return false;
                    start=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;end=r.GetFieldValue<DateTimeOffset>(1).UtcDateTime;closed=r.GetBoolean(2);
                }
                var canonical=PlanetWeekend.Current(start);
                if(canonical==null || canonical.Key!=weekend || canonical.StartUtc!=start || canonical.EndUtc!=end)throw new InvalidOperationException("Invalid saved Heart window.");
                if(closed || now<end)return false;
                if(Q.Execute(c,t,@"INSERT INTO revora_heart_scores(weekend,faction,support)
SELECT @weekend,f.faction,COUNT(r.id)*100 FROM (VALUES('couples'),('singles')) f(faction)
LEFT JOIN revora_heart_entries e ON e.weekend=@weekend AND e.faction=f.faction
LEFT JOIN revora_heart_runs r ON r.weekend=e.weekend AND r.uid=e.uid AND r.delivered_at>=@start AND r.delivered_at<@end
GROUP BY f.faction","weekend",weekend,"start",start,"end",end)!=2)throw new InvalidOperationException("Heart scores not frozen.");
                using(var q=Q.Command(c,t,@"WITH qualified AS (
SELECT e.uid,e.faction FROM revora_heart_entries e JOIN revora_heart_runs r USING(weekend,uid)
WHERE e.weekend=@weekend AND r.delivered_at>=@start AND r.delivered_at<@end GROUP BY e.uid,e.faction HAVING count(*)=3),
saved AS (INSERT INTO revora_heart_results(weekend,uid,faction,support,cp,rvp,earned_at,expires_at)
SELECT @weekend,q.uid,q.faction,300,CASE WHEN s.support=(SELECT max(support) FROM revora_heart_scores WHERE weekend=@weekend) THEN 5000 ELSE 0 END,
CASE WHEN s.support=(SELECT max(support) FROM revora_heart_scores WHERE weekend=@weekend) THEN 1000 ELSE 0 END,@end,@expires
FROM qualified q JOIN revora_heart_scores s ON s.weekend=@weekend AND s.faction=q.faction RETURNING 1)
SELECT (SELECT count(*) FROM qualified)=(SELECT count(*) FROM saved)","weekend",weekend,"start",start,"end",end,"expires",end.AddDays(30)))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Heart results not saved.");
                if(Q.Execute(c,t,"UPDATE revora_heart_weekends SET closed=true WHERE weekend=@weekend AND NOT closed","weekend",weekend)!=1)
                    throw new InvalidOperationException("Heart close marker not saved.");
                t.Commit();return true;
            }}
        }
        internal static int ProcessDue()
        {
            DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new ArgumentException("Heart calendar requires UTC.");
            var pending=new List<string>();
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT weekend FROM revora_heart_weekends WHERE NOT closed AND ends_at<=@now ORDER BY ends_at LIMIT 12","now",now))
                using(var r=q.ExecuteReader())while(r.Read())pending.Add(r.GetString(0));}
            int count=0;foreach(string key in pending)if(Freeze(key))count++;return count;
        }
        internal static void Tick()
        {
            if(!resultsReady || !System.Threading.Monitor.TryEnter(ResultsGate))return;
            try{var now=Clock();if(now<nextClosePoll)return;nextClosePoll=now.AddSeconds(30);ProcessDue();}
            catch(Exception ex){Console.WriteLine("[heart] results_failure="+ex.GetType().Name);}
            finally{System.Threading.Monitor.Exit(ResultsGate);}
        }
    }
}
