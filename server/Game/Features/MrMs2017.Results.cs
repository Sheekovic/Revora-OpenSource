using System;
using System.Collections.Generic;
using System.Globalization;
using Npgsql;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static partial class MrMs2017
    {
        private static readonly object PollGate=new object();
        private static DateTime nextPoll;
        internal static void Tick()
        {
            if(!initialized || !System.Threading.Monitor.TryEnter(PollGate))return;
            try
            {
                DateTime now=Clock();
                if(now<nextPoll)return;
                nextPoll=now.AddSeconds(30);
                ProcessDue();
            }
            catch(Exception ex){Console.WriteLine("[mrms2017] calendar_failure="+ex.GetType().Name);}
            finally{System.Threading.Monitor.Exit(PollGate);}
        }
        internal static int ProcessDue()
        {
            // Entrants create their season transactionally. Only months with saved
            // activity need recovery, so downtime cannot create empty prize seasons.
            string current=SeasonOf(Clock());
            var pending=new List<string>();
            using(var c=Database.DataHolder.MySqlConnection)
            {
                c.Open();
                using(var q=Q.Command(c,null,@"SELECT season FROM revora_mrms2017_seasons
WHERE closed_at IS NULL AND season<@current ORDER BY season LIMIT 12","current",current))
                using(var r=q.ExecuteReader())while(r.Read())pending.Add(r.GetString(0));
            }
            int closed=0;
            foreach(string season in pending)if(Freeze(season))closed++;
            return closed;
        }
        internal static DateTime CloseOf(string season)
        {
            DateTime local;
            if (!DateTime.TryParseExact(season,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out local) || local.Year<2000 || local.Year>9998)
                throw new ArgumentException("Invalid contest month.");
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.AddMonths(1),DateTimeKind.Unspecified),TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        }
        private static int LockKey(string season)
        {
            CloseOf(season);
            return 1900000+int.Parse(season.Replace("-",""),CultureInfo.InvariantCulture);
        }
        internal static bool Freeze(string season)
        {
            DateTime close=CloseOf(season);
            if(Clock().Kind!=DateTimeKind.Utc || Clock()<close) throw new InvalidOperationException("Contest has not ended.");
            using(var c=Database.DataHolder.MySqlConnection)
            {
                c.Open();using(var t=c.BeginTransaction())
                {
                    Q.Execute(c,t,"SELECT pg_advisory_xact_lock(6711,@key)","key",LockKey(season));
                    Q.Execute(c,t,"INSERT INTO revora_mrms2017_seasons(season) VALUES(@season) ON CONFLICT DO NOTHING","season",season);
                    using(var q=Q.Command(c,t,"SELECT closed_at FROM revora_mrms2017_seasons WHERE season=@season FOR UPDATE","season",season))
                    {
                        object stored=q.ExecuteScalar();
                        if(stored==null) throw new InvalidOperationException("Contest season missing.");
                        if(stored!=DBNull.Value)
                        {
                            if(Convert.ToDateTime(stored).ToUniversalTime()!=close) throw new InvalidOperationException("Contest close changed.");
                            t.Commit();return false;
                        }
                    }
                    using(var q=Q.Command(c,t,@"WITH ranked AS (
SELECT e.uid,e.contest,SUM(d.points)::bigint AS support,
row_number() OVER(PARTITION BY e.contest ORDER BY SUM(d.points) DESC,e.registered_at,e.uid) AS place
FROM revora_mrms2017_entrants e JOIN revora_mrms2017_donations d ON d.season=e.season AND d.candidate=e.uid
WHERE e.season=@season GROUP BY e.uid,e.contest,e.registered_at),
saved AS (INSERT INTO revora_mrms2017_results(season,uid,contest,placement,support)
SELECT @season,uid,contest,place,support FROM ranked WHERE place<=3 RETURNING 1)
SELECT (SELECT count(*) FROM ranked WHERE place<=3)=(SELECT count(*) FROM saved)","season",season))
                        if(!object.Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Contest results not saved.");
                    if(Q.Execute(c,t,"UPDATE revora_mrms2017_seasons SET closed_at=@close WHERE season=@season AND closed_at IS NULL","close",close,"season",season)!=1)
                        throw new InvalidOperationException("Contest close not saved.");
                    t.Commit();return true;
                }
            }
        }
        internal static List<string> RecoverAward(NpgsqlConnection c,NpgsqlTransaction t,uint uid,DateTime utc)
        {
            var notices=new List<string>();
            var pending=new List<Tuple<string,string,int,DateTime>>();
            using(var q=Q.Command(c,t,@"SELECT r.season,r.contest,r.placement,s.closed_at
FROM revora_mrms2017_results r JOIN revora_mrms2017_seasons s USING(season)
WHERE r.uid=@uid AND NOT r.award_recorded AND s.closed_at<=@now
ORDER BY s.closed_at LIMIT 12 FOR UPDATE OF r","uid",(long)uid,"now",utc))
            using(var r=q.ExecuteReader())while(r.Read())pending.Add(Tuple.Create(r.GetString(0),r.GetString(1),r.GetInt32(2),r.GetFieldValue<DateTimeOffset>(3).UtcDateTime));
            foreach(var result in pending)
            {
                if(result.Item4!=CloseOf(result.Item1) || result.Item3<1 || result.Item3>3 || (result.Item2!="mr" && result.Item2!="ms"))
                    throw new InvalidOperationException("Invalid saved contest result.");
                var grants=new List<Tuple<string,int,int>>();
                grants.Add(Tuple.Create("mrms-2017-local-v1",result.Item2=="ms"?20442044:20452045,360));
                if(result.Item3==1)grants.Add(Tuple.Create("mrms-2017-champion-v1",result.Item2=="ms"?20402040:20412041,365));
                grants.Add(Tuple.Create("mrms-2017-wing-"+result.Item3+"-v1",60126012,result.Item3==1?0:result.Item3==2?360:180));
                foreach(var grant in grants)
                {
                    if(!StorageManager.Known(grant.Item2/10000,grant.Item2%10000))throw new InvalidOperationException("Contest award missing from catalog.");
                    string key=result.Item1+":"+grant.Item2.ToString(CultureInfo.InvariantCulture);
                    if(!ProgressionEvidence.Grant(c,t,uid,grant.Item1,key,grant.Item2,result.Item4))
                    {
                        using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_event_awards WHERE uid=@uid AND source=@source
AND source_key=@key AND title_key=@title AND earned_at=@earned AND expires_at IS NOT DISTINCT FROM @expiry)",
                            "uid",(long)uid,"source",grant.Item1,"key",key,"title",grant.Item2,"earned",result.Item4,
                            "expiry",grant.Item3==0?(object)DBNull.Value:result.Item4.AddDays(grant.Item3)))
                            if(!object.Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Contest award not saved.");
                    }
                }
                if(Q.Execute(c,t,"UPDATE revora_mrms2017_results SET award_recorded=true WHERE season=@season AND uid=@uid AND NOT award_recorded","season",result.Item1,"uid",(long)uid)!=1)
                    throw new InvalidOperationException("Contest award receipt not saved.");
                notices.Add("Your flower-contest prizes await in the wardrobe.");
            }
            return notices;
        }
    }
}
