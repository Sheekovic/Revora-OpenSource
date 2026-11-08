using System;
using System.Collections.Generic;
using Revora.Game.Features;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Database
{
    // Durable schedules and standings. Creating a bracket is not entry,
    // a played match, a placement or a reward.
    internal static class GuildPkSchedule
    {
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_guild_pk_weeks(
contest uuid PRIMARY KEY REFERENCES revora_guild_contests(id),timezone text NOT NULL,
phase text NOT NULL CHECK(phase IN('groups','finals','complete','cancelled')));
CREATE TABLE IF NOT EXISTS revora_guild_pk_matches(
id uuid PRIMARY KEY,contest uuid NOT NULL REFERENCES revora_guild_pk_weeks(contest),
round smallint NOT NULL CHECK(round BETWEEN 1 AND 4),bracket smallint NOT NULL CHECK(bracket BETWEEN 0 AND 3),
first_guild bigint NOT NULL,second_guild bigint NOT NULL,starts_at timestamptz NOT NULL,ends_at timestamptz NOT NULL,
state text NOT NULL CHECK(state IN('scheduled','active','complete','cancelled')),
winner bigint,first_score bigint NOT NULL DEFAULT 0 CHECK(first_score>=0),second_score bigint NOT NULL DEFAULT 0 CHECK(second_score>=0),
CHECK(first_guild<>second_guild),CHECK(ends_at>starts_at),CHECK(winner IS NULL OR winner IN(0,first_guild,second_guild)),
UNIQUE(contest,round,bracket,first_guild,second_guild),
FOREIGN KEY(contest,first_guild) REFERENCES revora_guild_contest_seeds(run,guild_id),
FOREIGN KEY(contest,second_guild) REFERENCES revora_guild_contest_seeds(run,guild_id));
CREATE TABLE IF NOT EXISTS revora_guild_pk_slots(
contest uuid NOT NULL,round smallint NOT NULL,guild_id bigint NOT NULL,
match uuid NOT NULL REFERENCES revora_guild_pk_matches(id),PRIMARY KEY(contest,round,guild_id));
CREATE TABLE IF NOT EXISTS revora_guild_pk_maps(
run uuid PRIMARY KEY REFERENCES revora_guild_pk_matches(id),map bigint NOT NULL CHECK(map BETWEEN 11000 AND 59999));
CREATE TABLE IF NOT EXISTS revora_guild_pk_transfers(
run uuid NOT NULL REFERENCES revora_guild_pk_maps(run),uid bigint NOT NULL,map bigint NOT NULL,guild_id bigint NOT NULL,admission uuid NOT NULL,
return_x integer NOT NULL,return_y integer NOT NULL,state text NOT NULL CHECK(state IN('reserved','entered','returned')),
reserved_at timestamptz NOT NULL,PRIMARY KEY(run,uid));
CREATE UNIQUE INDEX IF NOT EXISTS revora_guild_pk_active_transfer ON revora_guild_pk_transfers(uid) WHERE state<>'returned';
CREATE TABLE IF NOT EXISTS revora_guild_pk_entries(
run uuid NOT NULL,uid bigint NOT NULL,guild_id bigint NOT NULL,admission uuid NOT NULL,map bigint NOT NULL,
entered_at timestamptz NOT NULL,active boolean NOT NULL DEFAULT true,deaths integer NOT NULL DEFAULT 0 CHECK(deaths BETWEEN 0 AND 20),
PRIMARY KEY(run,uid),FOREIGN KEY(run,uid) REFERENCES revora_guild_pk_transfers(run,uid));
CREATE TABLE IF NOT EXISTS revora_guild_pk_standings(
contest uuid NOT NULL,guild_id bigint NOT NULL,bracket smallint NOT NULL CHECK(bracket IN(0,1)),
place smallint NOT NULL CHECK(place BETWEEN 1 AND 4),points integer NOT NULL CHECK(points BETWEEN 0 AND 9),
seed smallint NOT NULL CHECK(seed BETWEEN 1 AND 8),PRIMARY KEY(contest,guild_id),UNIQUE(contest,bracket,place),
FOREIGN KEY(contest,guild_id) REFERENCES revora_guild_contest_seeds(run,guild_id));
CREATE TABLE IF NOT EXISTS revora_guild_pk_placements(
contest uuid NOT NULL,guild_id bigint NOT NULL,rank smallint NOT NULL CHECK(rank BETWEEN 1 AND 8),
match uuid NOT NULL REFERENCES revora_guild_pk_matches(id),settled_at timestamptz NOT NULL,
PRIMARY KEY(contest,rank),UNIQUE(contest,guild_id),FOREIGN KEY(contest,guild_id) REFERENCES revora_guild_contest_seeds(run,guild_id));
CREATE TABLE IF NOT EXISTS revora_guild_pk_prizes(
contest uuid NOT NULL,guild_id bigint NOT NULL,uid bigint NOT NULL,rank smallint NOT NULL,
silver bigint NOT NULL CHECK(silver>0),cps integer NOT NULL CHECK(cps>0),claimed_at timestamptz NOT NULL,
PRIMARY KEY(contest,guild_id),FOREIGN KEY(contest,guild_id) REFERENCES revora_guild_pk_placements(contest,guild_id));");
                t.Commit();
            }}
        }
        internal static void Recover(DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild PK recovery requires UTC.");
            var pending=new List<Guid>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT r.id FROM revora_guild_contests r
WHERE r.state='frozen' AND r.ends_at<=@now AND r.starts_at>=@now-interval '7 days'
AND NOT EXISTS(SELECT 1 FROM revora_guild_pk_weeks w WHERE w.contest=r.id)
AND NOT EXISTS(SELECT 1 FROM revora_guild_contest_seeds s WHERE s.run=r.id AND NOT EXISTS(
SELECT 1 FROM revora_guild_contest_owners o WHERE o.run=s.run AND o.guild_id=s.guild_id))
ORDER BY r.starts_at LIMIT 64","now",utc))using(var r=q.ExecuteReader())while(r.Read())pending.Add(r.GetGuid(0));}
            foreach(var run in pending)Create(run);
        }
        internal static bool Participate(Guid run,uint uid,Guid admission,uint opponent,Guid other,DateTime utc) {
            if(uid==0 || opponent==0 || uid==opponent || utc.Kind!=DateTimeKind.Utc)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                using(var q=Q.Command(c,t,"SELECT id FROM revora_guild_pk_matches WHERE id=@run AND state='active' AND @now>=starts_at AND @now<ends_at FOR SHARE","run",run,"now",utc))if(q.ExecuteScalar()==null)return false;
                long guild;DateTime entered;
                using(var q=Q.Command(c,t,@"SELECT p.guild_id,p.entered_at FROM revora_guild_pk_entries p
JOIN revora_guild_pk_entries o ON o.run=p.run AND o.guild_id<>p.guild_id AND o.uid=@opponent AND o.admission=@other
JOIN entities e ON e.uid=p.uid AND e.guildid=p.guild_id AND e.mapid=p.map AND e.guildrank BETWEEN 1 AND 1000
JOIN entities oe ON oe.uid=o.uid AND oe.guildid=o.guild_id AND oe.mapid=o.map AND oe.guildrank BETWEEN 1 AND 1000
JOIN revora_guild_admissions a ON a.uid=p.uid AND a.guild_id=p.guild_id AND a.admission=p.admission
JOIN revora_guild_admissions oa ON oa.uid=o.uid AND oa.guild_id=o.guild_id AND oa.admission=o.admission
JOIN revora_guild_pk_maps f ON f.run=p.run AND f.map=p.map AND f.map=o.map JOIN revora_guild_pk_matches m ON m.id=p.run
JOIN revora_guild_contest_owners g ON g.run=m.contest AND g.guild_id=p.guild_id AND g.eligible
JOIN revora_guild_contest_owners og ON og.run=m.contest AND og.guild_id=o.guild_id AND og.eligible
WHERE p.run=@run AND p.uid=@uid AND p.admission=@admission AND p.active AND o.active AND p.deaths<20 AND o.deaths<20
AND a.joined_at<=@cutoff AND oa.joined_at<=@cutoff AND p.guild_id IN(m.first_guild,m.second_guild) AND o.guild_id IN(m.first_guild,m.second_guild)
FOR SHARE OF p,o,e,oe,a,oa,g,og","run",run,"uid",(long)uid,"admission",admission,"opponent",(long)opponent,"other",other,"cutoff",utc.AddDays(-7)))using(var r=q.ExecuteReader()) {
                    if(!r.Read())return false;guild=r.GetInt64(0);entered=r.GetFieldValue<DateTimeOffset>(1).UtcDateTime;
                }
                Q.Execute(c,t,@"INSERT INTO revora_guild_pk_participation(uid,run,guild_id,admission,opponent,opponent_admission,entered_at,accepted_at)
VALUES(@uid,@run,@guild,@admission,@opponent,@other,@entered,@now) ON CONFLICT(uid) DO NOTHING",
                    "uid",(long)uid,"run",run,"guild",guild,"admission",admission,"opponent",(long)opponent,"other",other,"entered",entered,"now",utc);
                using(var q=Q.Command(c,t,"SELECT uid FROM revora_guild_pk_participation WHERE uid=@uid","uid",(long)uid))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Guild PK participation was not saved.");
                t.Commit();return true;
            }}
        }
        internal static int Prize(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Guid contest,uint uid,uint guild,DateTime utc,out ulong silver,out uint cps) {
            silver=0;cps=0;if(t==null || !ReferenceEquals(t.Connection,c))throw new InvalidOperationException("Guild PK prize requires a transaction.");
            if(uid==0 || guild==0 || utc.Kind!=DateTimeKind.Utc)return 0;
            Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
            DateTime start;string zone;
            using(var q=Q.Command(c,t,@"SELECT c.starts_at,w.timezone FROM revora_guild_pk_weeks w JOIN revora_guild_contests c ON c.id=w.contest
WHERE w.contest=@run AND w.phase='complete' FOR UPDATE OF w","run",contest))using(var r=q.ExecuteReader()) {
                if(!r.Read())return 0;start=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;zone=r.GetString(1);
            }
            string name;int rank;
            using(var q=Q.Command(c,t,"SELECT name,guildrank FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank IN(990,1000) FOR UPDATE","uid",(long)uid,"guild",(long)guild))using(var r=q.ExecuteReader()) {
                if(!r.Read())return 0;name=r.GetString(0);rank=Convert.ToInt32(r[1]);
            }
            decimal balance,points;
            using(var q=Q.Command(c,t,"SELECT silverfund,conquerpointfund,leadername FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild))using(var r=q.ExecuteReader()) {
                if(!r.Read() || rank==1000 && name!=r.GetString(2))return 0;balance=Convert.ToDecimal(r[0]);points=Convert.ToDecimal(r[1]);
            }
            using(var q=Q.Command(c,t,"SELECT eligible FROM revora_guild_contest_owners WHERE run=@run AND guild_id=@guild FOR SHARE","run",contest,"guild",(long)guild))if(!Equals(q.ExecuteScalar(),true))return 0;
            using(var q=Q.Command(c,t,"SELECT uid FROM revora_guild_pk_prizes WHERE contest=@run AND guild_id=@guild","run",contest,"guild",(long)guild))if(q.ExecuteScalar()!=null) {
                silver=checked((ulong)balance);cps=checked((uint)points);return 2;
            }
            if(!GuildPkRules.PrizeWindow(start,TimeZoneInfo.FindSystemTimeZoneById(zone),utc))return 0;
            using(var q=Q.Command(c,t,"SELECT rank FROM revora_guild_pk_placements WHERE contest=@run AND guild_id=@guild","run",contest,"guild",(long)guild)) {
                var value=q.ExecuteScalar();if(value==null)return 0;rank=Convert.ToInt32(value);
            }
            long amount=GuildPkRules.FinalSilver(rank);uint cpAmount=GuildPkRules.FinalCps(rank);
            if(balance<0 || balance>long.MaxValue-amount || points<0 || points>uint.MaxValue-cpAmount)return 0;
            if(Q.Execute(c,t,"INSERT INTO revora_guild_pk_prizes VALUES(@run,@guild,@uid,@rank,@silver,@cps,@now)","run",contest,"guild",(long)guild,"uid",(long)uid,"rank",rank,"silver",amount,"cps",(long)cpAmount,"now",utc)!=1)
                throw new InvalidOperationException("Guild PK prize receipt was not saved.");
            balance+=amount;points+=cpAmount;
            if(Q.Execute(c,t,"UPDATE guilds SET silverfund=@silver,conquerpointfund=@cps WHERE id=@guild","guild",(long)guild,"silver",balance,"cps",points)!=1)
                throw new InvalidOperationException("Guild PK treasury prize was not saved.");
            silver=checked((ulong)balance);cps=checked((uint)points);return 1;
        }
        internal static void Progress(DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild PK progression requires UTC.");
            var weeks=new List<Guid>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT w.contest FROM revora_guild_pk_weeks w
JOIN revora_guild_contests c ON c.id=w.contest WHERE w.phase IN('groups','finals') AND c.ends_at<=@now
AND c.starts_at>=@now-interval '7 days' ORDER BY c.starts_at,w.contest LIMIT 16","now",utc))using(var r=q.ExecuteReader())while(r.Read())weeks.Add(r.GetGuid(0));}
            foreach(var week in weeks)Advance(week,utc);
        }
        internal static bool Advance(Guid contest,DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild PK progression requires UTC.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                string phase,zone;DateTime start,end;
                using(var q=Q.Command(c,t,@"SELECT w.phase,w.timezone,c.starts_at,c.ends_at FROM revora_guild_pk_weeks w
JOIN revora_guild_contests c ON c.id=w.contest WHERE w.contest=@run FOR UPDATE OF w","run",contest))using(var r=q.ExecuteReader()) {
                    if(!r.Read())return false;phase=r.GetString(0);zone=r.GetString(1);start=r.GetFieldValue<DateTimeOffset>(2).UtcDateTime;end=r.GetFieldValue<DateTimeOffset>(3).UtcDateTime;
                }
                if(utc<end || phase=="complete" || phase=="cancelled")return false;
                // No arena was opened before its admission window shut. There is
                // no observed match to turn into a win, draw or participation.
                Q.Execute(c,t,@"UPDATE revora_guild_pk_matches m SET state='cancelled' WHERE contest=@run AND state='scheduled'
AND starts_at<=@now AND NOT EXISTS(SELECT 1 FROM revora_guild_pk_maps f WHERE f.run=m.id)","run",contest,"now",utc);
                var seeds=new List<uint>();
                using(var q=Q.Command(c,t,"SELECT rank,guild_id FROM revora_guild_contest_seeds WHERE run=@run ORDER BY rank","run",contest))using(var r=q.ExecuteReader())while(r.Read()) {
                    if(Convert.ToInt32(r[0])!=seeds.Count+1)throw new InvalidOperationException("Guild PK seed order is incomplete.");seeds.Add(checked((uint)r.GetInt64(1)));
                }
                var results=new List<GuildPkRules.Result>();
                using(var q=Q.Command(c,t,@"SELECT m.round,m.bracket,m.first_guild,m.second_guild,m.state,m.winner,
r.run IS NOT NULL AND r.winner IS NOT DISTINCT FROM m.winner AND r.first_score=m.first_score AND r.second_score=m.second_score
FROM revora_guild_pk_matches m LEFT JOIN revora_guild_pk_results r ON r.run=m.id WHERE m.contest=@run AND m.round<4 ORDER BY m.round,m.bracket,m.first_guild FOR SHARE OF m","run",contest))using(var r=q.ExecuteReader())while(r.Read()) {
                    string state=r.GetString(4);
                    if(state=="active" || state=="scheduled"){r.Close();t.Commit();return false;}
                    if(state=="complete" && !r.GetBoolean(6))throw new InvalidOperationException("Guild PK terminal result lacks its receipt.");
                    results.Add(new GuildPkRules.Result(new GuildPkRules.Pair(Convert.ToInt32(r[0]),Convert.ToInt32(r[1]),checked((uint)r.GetInt64(2)),checked((uint)r.GetInt64(3))),
                        r.IsDBNull(5)?0:checked((uint)r.GetInt64(5)),state=="complete"));
                }
                if(phase=="finals") {
                    var finals=new List<GuildPkRules.Result>();var ids=new Dictionary<int,Guid>();
                    using(var q=Q.Command(c,t,@"SELECT m.id,m.bracket,m.first_guild,m.second_guild,m.state,m.winner,
r.run IS NOT NULL AND r.winner IS NOT DISTINCT FROM m.winner AND r.first_score=m.first_score AND r.second_score=m.second_score
FROM revora_guild_pk_matches m LEFT JOIN revora_guild_pk_results r ON r.run=m.id WHERE m.contest=@run AND m.round=4 ORDER BY m.bracket FOR SHARE OF m","run",contest))using(var r=q.ExecuteReader())while(r.Read()) {
                        string state=r.GetString(4);if(state=="active" || state=="scheduled"){r.Close();t.Commit();return false;}
                        if(state=="complete" && !r.GetBoolean(6))throw new InvalidOperationException("Guild PK final lacks its result receipt.");
                        int bracket=Convert.ToInt32(r[1]);ids.Add(bracket,r.GetGuid(0));
                        finals.Add(new GuildPkRules.Result(new GuildPkRules.Pair(4,bracket,checked((uint)r.GetInt64(2)),checked((uint)r.GetInt64(3))),r.IsDBNull(5)?0:checked((uint)r.GetInt64(5)),state=="complete"));
                    }
                    var placements=GuildPkRules.FinalPlacements(seeds.ToArray(),results,finals);
                    foreach(var place in placements)if(Q.Execute(c,t,"INSERT INTO revora_guild_pk_placements(contest,guild_id,rank,match,settled_at) VALUES(@run,@guild,@rank,@match,@now)",
                        "run",contest,"guild",(long)place.Guild,"rank",place.Rank,"match",ids[place.Match.Group],"now",utc)!=1)throw new InvalidOperationException("Guild PK placement was not saved.");
                    if(Q.Execute(c,t,"UPDATE revora_guild_pk_weeks SET phase='complete' WHERE contest=@run AND phase='finals'","run",contest)!=1)throw new InvalidOperationException("Guild PK final ownership changed.");
                    t.Commit();return true;
                }
                var standings=GuildPkRules.GroupStandings(seeds.ToArray(),results);int[] places={0,0};
                foreach(var standing in standings)if(Q.Execute(c,t,@"INSERT INTO revora_guild_pk_standings(contest,guild_id,bracket,place,points,seed)
VALUES(@run,@guild,@bracket,@place,@points,@seed)","run",contest,"guild",(long)standing.Guild,"bracket",standing.Group,
                    "place",++places[standing.Group],"points",standing.Points,"seed",standing.Seed)!=1)throw new InvalidOperationException("Guild PK standings were not saved.");
                var pairs=GuildPkRules.FinalMatches(seeds.ToArray(),results);var begins=GuildPkRules.MatchStart(start,4,TimeZoneInfo.FindSystemTimeZoneById(zone));
                foreach(var pair in pairs) {
                    var id=Guid.NewGuid();
                    if(Q.Execute(c,t,@"INSERT INTO revora_guild_pk_matches(id,contest,round,bracket,first_guild,second_guild,starts_at,ends_at,state)
VALUES(@id,@run,4,@group,@first,@second,@start,@end,'scheduled')","id",id,"run",contest,"group",pair.Group,"first",(long)pair.First,"second",(long)pair.Second,"start",begins,"end",begins.AddHours(2))!=1)
                        throw new InvalidOperationException("Guild PK final was not saved.");
                    foreach(var guild in new[]{pair.First,pair.Second})if(Q.Execute(c,t,"INSERT INTO revora_guild_pk_slots(contest,round,guild_id,match) VALUES(@run,4,@guild,@id)",
                        "run",contest,"guild",(long)guild,"id",id)!=1)throw new InvalidOperationException("Guild PK final slot was not saved.");
                }
                if(Q.Execute(c,t,"UPDATE revora_guild_pk_weeks SET phase=@phase WHERE contest=@run AND phase='groups'","run",contest,"phase",pairs.Length==0?"complete":"finals")!=1)
                    throw new InvalidOperationException("Guild PK week ownership changed.");
                t.Commit();return true;
            }}
        }
        internal static void Create(Guid contest) {
            if(contest==Guid.Empty)throw new ArgumentException("Missing Guild PK qualifier.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                DateTime start;
                using(var q=Q.Command(c,t,"SELECT starts_at,state FROM revora_guild_contests WHERE id=@run FOR UPDATE","run",contest))using(var r=q.ExecuteReader()) {
                    if(!r.Read() || r.GetString(1)!="frozen")throw new InvalidOperationException("Guild PK requires frozen qualification.");
                    start=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;
                }
                using(var q=Q.Command(c,t,"SELECT contest FROM revora_guild_pk_weeks WHERE contest=@run","run",contest))
                    if(q.ExecuteScalar()!=null){t.Commit();return;}
                var seeds=new List<uint>();
                using(var q=Q.Command(c,t,@"SELECT s.rank,s.guild_id,o.guild_id FROM revora_guild_contest_seeds s
LEFT JOIN revora_guild_contest_owners o ON o.run=s.run AND o.guild_id=s.guild_id
WHERE s.run=@run ORDER BY s.rank","run",contest))using(var r=q.ExecuteReader())while(r.Read()) {
                    if(Convert.ToInt32(r[0])!=seeds.Count+1 || r.IsDBNull(2))throw new InvalidOperationException("Guild PK qualification ownership is incomplete.");
                    seeds.Add(checked((uint)r.GetInt64(1)));
                }
                var zone=TimeZoneInfo.Local;var pairs=GuildPkRules.GroupMatches(seeds.ToArray());
                // Validate the weekday even for a sparse field with no group match.
                GuildPkRules.MatchStart(start,1,zone);
                Q.Execute(c,t,"INSERT INTO revora_guild_pk_weeks(contest,timezone,phase) VALUES(@run,@zone,'groups')","run",contest,"zone",zone.Id);
                foreach(var pair in pairs) {
                    var id=Guid.NewGuid();var begins=GuildPkRules.MatchStart(start,pair.Round,zone);
                    if(Q.Execute(c,t,@"INSERT INTO revora_guild_pk_matches(id,contest,round,bracket,first_guild,second_guild,starts_at,ends_at,state)
VALUES(@id,@run,@round,@group,@first,@second,@start,@end,'scheduled')",
                        "id",id,"run",contest,"round",pair.Round,"group",pair.Group,"first",(long)pair.First,"second",(long)pair.Second,"start",begins,"end",begins.AddHours(2))!=1)
                        throw new InvalidOperationException("Guild PK match was not saved.");
                    foreach(var guild in new[]{pair.First,pair.Second})
                        if(Q.Execute(c,t,"INSERT INTO revora_guild_pk_slots(contest,round,guild_id,match) VALUES(@run,@round,@guild,@id)",
                            "run",contest,"round",pair.Round,"guild",(long)guild,"id",id)!=1)throw new InvalidOperationException("Guild PK participant slot was not saved.");
                }
                t.Commit();
            }}
        }
    }
}
