using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Frozen ownership/results for the owner-approved monthly Revora edition.
    // The coordinator never reconstructs missed ownership snapshots.
    internal static class FashionCompetition
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static readonly object PollGate=new object();
        private static DateTime nextPoll;
        private static volatile bool initialized;
        internal static void Initialize()
        {
            using(var c=DataHolder.MySqlConnection) {
                c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_fashion_seasons(
season varchar(7) PRIMARY KEY,closed_at timestamptz NOT NULL,snapshot_at timestamptz NOT NULL,
catalog_hash varchar(64) NOT NULL);
CREATE TABLE IF NOT EXISTS revora_fashion_designs(
season varchar(7) NOT NULL REFERENCES revora_fashion_seasons(season),uid bigint NOT NULL CHECK(uid>0),
design bigint NOT NULL,stars integer NOT NULL CHECK(stars>0),PRIMARY KEY(season,uid,design));
CREATE TABLE IF NOT EXISTS revora_fashion_results(
season varchar(7) NOT NULL REFERENCES revora_fashion_seasons(season),uid bigint NOT NULL CHECK(uid>0),
score bigint NOT NULL CHECK(score>0),placement bigint NOT NULL CHECK(placement>0),PRIMARY KEY(season,uid));
ALTER TABLE revora_fashion_results ADD COLUMN IF NOT EXISTS award_recorded boolean NOT NULL DEFAULT false;
CREATE INDEX IF NOT EXISTS revora_fashion_pending ON revora_fashion_results(uid) WHERE NOT award_recorded AND placement<=3;
CREATE TABLE IF NOT EXISTS revora_fashion_calendar(id integer PRIMARY KEY CHECK(id=1),next_close timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_fashion_missed(season varchar(7) PRIMARY KEY,closed_at timestamptz NOT NULL,observed_at timestamptz NOT NULL);");
                Q.Execute(c,null,"INSERT INTO revora_fashion_calendar(id,next_close) VALUES(1,@close) ON CONFLICT(id) DO NOTHING",
                    "close",NextMonth(Clock()));
                using(var q=Q.Command(c,null,"SELECT EXISTS(SELECT 1 FROM revora_fashion_calendar WHERE id=1)"))
                    if(!object.Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Fashion calendar was not initialized.");
                initialized=true;
            }
        }

        private static DateTime NextMonth(DateTime utc)
        {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Fashion calendar must be UTC.");
            var zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var local=TimeZoneInfo.ConvertTimeFromUtc(utc,zone);
            return TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year,local.Month,1).AddMonths(1),zone);
        }
        internal static void Tick()
        {
            if(!initialized || !System.Threading.Monitor.TryEnter(PollGate))return;
            try {
                var now=Clock();if(now<nextPoll)return;nextPoll=now.AddSeconds(10);
                for(int i=0;i<12 && ProcessDue();i++) { }
            } catch(Exception ex) { Console.WriteLine("[fashion] calendar_failure="+ex.GetType().Name); }
            finally {System.Threading.Monitor.Exit(PollGate);}
        }
        internal static bool ProcessDue()
        {
            using(var c=DataHolder.MySqlConnection) {
                c.Open();using(var t=c.BeginTransaction()) {
                    DateTime close;
                    using(var q=Q.Command(c,t,"SELECT next_close FROM revora_fashion_calendar WHERE id=1 FOR UPDATE"))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read())throw new InvalidOperationException("Fashion calendar missing.");
                        close=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;
                    }
                    var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new ArgumentException("Fashion calendar must be UTC.");
                    if(now<close){t.Commit();return false;}
                    var local=TimeZoneInfo.ConvertTimeFromUtc(close,TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
                    if(local.Day!=1 || local.TimeOfDay!=TimeSpan.Zero || local.Year<2000)
                        throw new InvalidOperationException("Fashion calendar boundary is invalid.");
                    string season=local.AddMonths(-1).ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture);
                    bool frozen;
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_fashion_seasons WHERE season=@season AND closed_at=@close)","season",season,"close",close))
                        frozen=object.Equals(q.ExecuteScalar(),true);
                    if(!frozen && now<=close.AddMinutes(1)) Freeze(close);
                    else if(!frozen) {
                        Q.Execute(c,t,"INSERT INTO revora_fashion_missed VALUES(@season,@close,@now) ON CONFLICT(season) DO NOTHING",
                            "season",season,"close",close,"now",now);
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_fashion_missed WHERE season=@season AND closed_at=@close)","season",season,"close",close))
                            if(!object.Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Missed Fashion close was not saved.");
                    }
                    // Snapshot commit precedes cursor advancement. A restart/retry observes the
                    // saved snapshot above and advances without resnapshotting or marking it missed.
                    if(Q.Execute(c,t,"UPDATE revora_fashion_calendar SET next_close=@next WHERE id=1 AND next_close=@close",
                        "next",NextMonth(close),"close",close)!=1)throw new InvalidOperationException("Fashion calendar did not advance.");
                    t.Commit();return true;
                }
            }
        }

        internal const uint GuideNpc=6002;
        internal static string Page(Client.GameState player,byte option)
        {
            if(option<237 || option>239 || !Q.Player(player) || player.ActiveNpc!=GuideNpc || player.Map==null ||
                player.Booth!=null || player.Trade==null || player.Trade.InTrade) return null;
            Interfaces.INpc npc;
            if(!player.Map.Npcs.TryGetValue(GuideNpc,out npc) || npc.MapID!=player.Entity.MapID ||
                Kernel.GetDistance(player.Entity.X,player.Entity.Y,npc.X,npc.Y)>6) return null;
            if(option==237)return "A fine collection deserves its moment. Each month, our finest collectors earn Fashion Icon, Idol or Master. Shall we see the results?";
            if(option==238)return "Permanent garments and mount armor enter automatically. Each design counts once; its stars are points. Ties share rank and skip the next places. Judging is at Pacific midnight when the month ends. Titles last 30 days from judging.";
            return Board(player.Entity.UID);
        }
        internal static bool HandleNpc(Client.GameState player,Network.GamePackets.NpcRequest request)
        {
            if(player==null || request==null || player.ActiveNpc!=GuideNpc || request.OptionID<237 || request.OptionID>239)return false;
            try {
                string text=Page(player,request.OptionID);if(text==null)return true;
                var dialog=new Npcs(player);dialog.Text(text);
                dialog.Option("The standings.",239);dialog.Option("How is it judged?",238);dialog.Option("Farewell.",255);dialog.Send();
            }catch(Exception ex){Console.WriteLine("[fashion] dialogue_failure="+ex.GetType().Name);}
            return true;
        }
        internal static string Board(uint uid)
        {
            DateTime now=Clock();string season=null;var text=new StringBuilder();
            using(var c=DataHolder.MySqlConnection) {
                c.Open();
                using(var q=Q.Command(c,null,"SELECT next_close FROM revora_fashion_calendar WHERE id=1"))
                using(var r=q.ExecuteReader()) if(r.Read()) {
                    var due=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;
                    var next=TimeZoneInfo.ConvertTimeFromUtc(due,TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
                    if(due>now)text.Append("Next judging: ").Append(next.ToString("MMM d, yyyy",System.Globalization.CultureInfo.InvariantCulture)).Append(" at midnight Pacific. ");
                    else text.Append("The next judging is being arranged. ");
                }
                using(var q=Q.Command(c,null,"SELECT season FROM revora_fashion_seasons WHERE closed_at<=@now ORDER BY closed_at DESC LIMIT 1","now",now))
                    season=q.ExecuteScalar() as string;
                if(season==null)return text.Append("No contest has been judged yet.").ToString();
                text.Append("Results for ").Append(season).Append(": ");
                using(var q=Q.Command(c,null,@"SELECT r.placement,e.name,r.score FROM revora_fashion_results r
LEFT JOIN entities e ON e.uid=r.uid WHERE r.season=@season ORDER BY r.placement,r.uid LIMIT 6","season",season))
                using(var r=q.ExecuteReader()) {
                    int count=0;while(r.Read()) {
                        string name=r.IsDBNull(1)?"Departed hero":new string(r.GetString(1).Where(ch=>!char.IsControl(ch)).Take(16).ToArray());
                        text.Append('#').Append(r.GetInt64(0)).Append(' ').Append(name).Append(" (").Append(r.GetInt64(2)).Append(" stars). ");count++;
                    }
                    if(count==0)text.Append("No eligible collections entered. ");
                    if(count==6)text.Append("First six entries shown. ");
                }
                using(var q=Q.Command(c,null,"SELECT placement,score FROM revora_fashion_results WHERE season=@season AND uid=@uid","season",season,"uid",(long)uid))
                using(var r=q.ExecuteReader()) {
                    if(r.Read())text.Append("Your place: #").Append(r.GetInt64(0)).Append(" with ").Append(r.GetInt64(1)).Append(" stars.");
                    else text.Append("Your collection was not entered in that contest.");
                }
            }
            return text.ToString();
        }

        internal static List<string> RecoverAward(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint uid,DateTime utc)
        {
            var notices=new List<string>();
            var pending=new List<Tuple<string,int,DateTime>>();
            using(var q=Q.Command(c,t,@"SELECT r.season,r.placement,s.closed_at
FROM revora_fashion_results r JOIN revora_fashion_seasons s ON s.season=r.season
WHERE r.uid=@uid AND NOT r.award_recorded AND r.placement BETWEEN 1 AND 3
AND s.closed_at<=@now AND s.closed_at+interval '720 hours'>@now
ORDER BY s.closed_at LIMIT 8 FOR UPDATE OF r","uid",(long)uid,"now",utc))
            using(var r=q.ExecuteReader()) while(r.Read())
                pending.Add(Tuple.Create(r.GetString(0),checked((int)r.GetInt64(1)),r.GetFieldValue<DateTimeOffset>(2).UtcDateTime));
            foreach(var result in pending) {
                int title=result.Item2==1?20522052:result.Item2==2?20532053:20542054;
                if(!StorageManager.Known(title/10000,title%10000) ||
                    StorageManager.GetTitlePoints((short)(title/10000),(short)(title%10000))!=(4-result.Item2)*100)
                    throw new InvalidOperationException("Fashion award catalog mapping changed.");
                const string source="fashion-revora-v1";
                if(!ProgressionEvidence.Grant(c,t,uid,source,result.Item1,title,result.Item3)) {
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_event_awards
WHERE uid=@uid AND source=@source AND source_key=@season AND title_key=@title AND earned_at=@earned AND expires_at=@expiry)",
                        "uid",(long)uid,"source",source,"season",result.Item1,"title",title,"earned",result.Item3,"expiry",result.Item3.AddDays(30)))
                        if(!object.Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Fashion award was not saved.");
                }
                if(Q.Execute(c,t,@"UPDATE revora_fashion_results SET award_recorded=true
WHERE season=@season AND uid=@uid AND NOT award_recorded","season",result.Item1,"uid",(long)uid)!=1)
                    throw new InvalidOperationException("Fashion award receipt was not acknowledged.");
                notices.Add("Your collection earned "+(result.Item2==1?"Fashion Icon":result.Item2==2?"Fashion Idol":"Fashion Master")+" for "+result.Item1+". Find your title in the wardrobe.");
            }
            return notices;
        }

        internal static bool Freeze(DateTime closedUtc)
        {
            if(closedUtc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Fashion close must be UTC.");
            var zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var local=TimeZoneInfo.ConvertTimeFromUtc(closedUtc,zone);
            if(local.Day!=1 || local.TimeOfDay!=TimeSpan.Zero || local.Year<2000)
                throw new ArgumentException("Fashion closes at Pacific month boundaries.");
            string season=local.AddMonths(-1).ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture);
            using(var c=DataHolder.MySqlConnection) {
                c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SELECT pg_advisory_xact_lock(6711,@season)","season",local.Year*100+local.Month);
                    using(var q=Q.Command(c,t,"SELECT closed_at FROM revora_fashion_seasons WHERE season=@season","season",season)) {
                        object existing=q.ExecuteScalar();
                        if(existing!=null) {
                            using(var verify=Q.Command(c,t,"SELECT closed_at=@close FROM revora_fashion_seasons WHERE season=@season","season",season,"close",closedUtc))
                                if(!object.Equals(verify.ExecuteScalar(),true))throw new InvalidOperationException("Fashion season close changed.");
                            t.Commit();return false;
                        }
                    }
                    DateTime now=Clock();
                    if(now.Kind!=DateTimeKind.Utc || now<closedUtc || now>closedUtc.AddMinutes(1))
                        throw new InvalidOperationException("Missed Fashion ownership snapshot; historical ownership cannot be inferred.");
                    if(StorageItem.Garments.Count==0 || StorageItem.Mounts.Count==0)
                        throw new InvalidOperationException("Fashion catalog is unavailable.");
                    var catalog=new SortedDictionary<long,int>();
                    foreach(var entry in StorageItem.Garments.Concat(StorageItem.Mounts)) {
                        if(entry.Key!=entry.Value.ID)throw new InvalidOperationException("Fashion catalog identity mismatch.");
                        if(entry.Value.Stars==0 || !ConquerItemInformation.BaseInformations.ContainsKey(entry.Key))continue;
                        catalog.Add(entry.Key,checked((int)entry.Value.Stars));
                    }
                    if(catalog.Count==0)throw new InvalidOperationException("Fashion catalog has no scored designs.");
                    string hash;
                    using(var sha=SHA256.Create())
                        hash=System.BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join(";",catalog.Select(x=>
                            x.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))))).Replace("-","");
                    if(Q.Execute(c,t,"INSERT INTO revora_fashion_seasons VALUES(@season,@closed,@snapshot,@hash)",
                        "season",season,"closed",closedUtc,"snapshot",now,"hash",hash)!=1)
                        throw new InvalidOperationException("Fashion season was not saved.");
                    // One statement owns one database snapshot of all durable inventory,
                    // equipment, wardrobe and warehouse ownership. Temporary/unknown Days are excluded.
                    using(var q=Q.Command(c,t,@"WITH catalog AS (
SELECT * FROM unnest(@ids::bigint[],@stars::integer[]) AS c(id,stars)), owned AS (
SELECT DISTINCT i.entityid AS uid,i.id AS design,c.stars FROM items i
JOIN entities e ON e.uid=i.entityid JOIN catalog c ON c.id=i.id
WHERE i.entityid>0 AND i.days::text='0'), saved AS (
INSERT INTO revora_fashion_designs(season,uid,design,stars)
SELECT @season,uid,design,stars FROM owned RETURNING 1)
SELECT (SELECT count(*) FROM owned)=(SELECT count(*) FROM saved)",
                        "ids",catalog.Keys.ToArray(),"stars",catalog.Values.ToArray(),"season",season))
                        if(!object.Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Fashion ownership snapshot was not saved.");
                    using(var q=Q.Command(c,t,@"WITH scores AS (
SELECT uid,sum(stars)::bigint AS score FROM revora_fashion_designs WHERE season=@season GROUP BY uid), saved AS (
INSERT INTO revora_fashion_results(season,uid,score,placement)
SELECT @season,uid,score,rank() OVER(ORDER BY score DESC) FROM scores RETURNING 1)
SELECT (SELECT count(*) FROM scores)=(SELECT count(*) FROM saved)","season",season))
                        if(!object.Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Fashion placements were not saved.");
                    t.Commit();return true;
                }
            }
        }
    }
}
