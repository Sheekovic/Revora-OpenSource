using System;
using Npgsql;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Database
{
    // The runtime combat owner must establish a genuine banner hit.
    // This ledger validates the saved participant and serializes its decision.
    internal static class GuildPkBannerTable
    {
        internal sealed class Snapshot {
            internal uint First,Second,Keeper,Winner;internal int Capture;internal long FirstScore,SecondScore;
            internal uint Hitpoints;internal long HitNumber;internal DateTime Start,End,Accounted;internal string State;
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_guild_pk_banners(
run uuid PRIMARY KEY REFERENCES revora_guild_pk_matches(id),keeper bigint NOT NULL DEFAULT 0 CHECK(keeper>=0),
capture integer NOT NULL DEFAULT 0 CHECK(capture>=0),accounted_at timestamptz NOT NULL);
ALTER TABLE revora_guild_pk_banners ADD COLUMN IF NOT EXISTS hitpoints integer NOT NULL DEFAULT 1000000 CHECK(hitpoints BETWEEN 1 AND 1000000);
ALTER TABLE revora_guild_pk_banners ADD COLUMN IF NOT EXISTS hit_number bigint NOT NULL DEFAULT 0 CHECK(hit_number>=0);
CREATE TABLE IF NOT EXISTS revora_guild_pk_banner_hits(
run uuid NOT NULL REFERENCES revora_guild_pk_banners(run),number bigint NOT NULL CHECK(number>0),
uid bigint NOT NULL,guild_id bigint NOT NULL,admission uuid NOT NULL,damage integer NOT NULL CHECK(damage>0),
hp_before integer NOT NULL CHECK(hp_before BETWEEN 1 AND 1000000),capture_before integer NOT NULL CHECK(capture_before>=0),hit_at timestamptz NOT NULL,
PRIMARY KEY(run,number),FOREIGN KEY(run,uid) REFERENCES revora_guild_pk_entries(run,uid),CHECK(damage<=hp_before));
CREATE OR REPLACE VIEW revora_guild_pk_banner_contributions AS
SELECT run,uid,guild_id,admission,sum(damage)::bigint AS damage,(sum(damage)/10000)::bigint AS exploits
FROM revora_guild_pk_banner_hits GROUP BY run,uid,guild_id,admission;
CREATE TABLE IF NOT EXISTS revora_guild_pk_captures(
run uuid NOT NULL REFERENCES revora_guild_pk_banners(run),number integer NOT NULL CHECK(number>0),
uid bigint NOT NULL,guild_id bigint NOT NULL,admission uuid NOT NULL,captured_at timestamptz NOT NULL,
PRIMARY KEY(run,number),FOREIGN KEY(run,uid) REFERENCES revora_guild_pk_entries(run,uid));
CREATE TABLE IF NOT EXISTS revora_guild_pk_results(
run uuid PRIMARY KEY REFERENCES revora_guild_pk_matches(id),winner bigint,reason text NOT NULL CHECK(reason IN('empty','elimination','lead','time')),
first_score bigint NOT NULL CHECK(first_score>=0),second_score bigint NOT NULL CHECK(second_score>=0),settled_at timestamptz NOT NULL);");}
        }
        internal static void Ensure(Guid run) {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"INSERT INTO revora_guild_pk_banners(run,accounted_at)
SELECT m.id,m.starts_at FROM revora_guild_pk_matches m JOIN revora_guild_pk_maps f ON f.run=m.id
WHERE m.id=@run AND m.state IN('scheduled','active') ON CONFLICT DO NOTHING","run",run);}
        }
        private static Snapshot Read(NpgsqlConnection c,NpgsqlTransaction t,Guid run) {
            Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
            var state=new Snapshot();
            using(var q=Q.Command(c,t,"SELECT first_guild,second_guild,first_score,second_score,starts_at,ends_at,state,winner FROM revora_guild_pk_matches WHERE id=@run FOR UPDATE","run",run))using(var r=q.ExecuteReader()) {
                if(!r.Read())throw new InvalidOperationException("Unknown Guild PK match.");
                state.First=checked((uint)r.GetInt64(0));state.Second=checked((uint)r.GetInt64(1));state.FirstScore=r.GetInt64(2);state.SecondScore=r.GetInt64(3);
                state.Start=r.GetFieldValue<DateTimeOffset>(4).UtcDateTime;state.End=r.GetFieldValue<DateTimeOffset>(5).UtcDateTime;state.State=r.GetString(6);state.Winner=r.IsDBNull(7)?0:checked((uint)r.GetInt64(7));
            }
            using(var q=Q.Command(c,t,"SELECT keeper,capture,accounted_at,hitpoints,hit_number FROM revora_guild_pk_banners WHERE run=@run FOR UPDATE","run",run))using(var r=q.ExecuteReader()) {
                if(!r.Read())throw new InvalidOperationException("Guild PK banner was not registered.");
                state.Keeper=checked((uint)r.GetInt64(0));state.Capture=r.GetInt32(1);state.Accounted=r.GetFieldValue<DateTimeOffset>(2).UtcDateTime;state.Hitpoints=checked((uint)r.GetInt32(3));state.HitNumber=r.GetInt64(4);
            }
            return state;
        }
        private static void Accrue(NpgsqlConnection c,NpgsqlTransaction t,Guid run,Snapshot state,DateTime utc) {
            if(state.State!="active" || utc<state.Start || utc<state.Accounted)return;
            var until=utc<state.End?utc:state.End;long seconds=(long)Math.Floor((until-state.Accounted).TotalSeconds);
            if(seconds<=0)return;
            if(state.Keeper!=0) {
                using(var q=Q.Command(c,t,@"SELECT g.id FROM guilds g JOIN revora_guild_contest_owners o ON o.guild_id=g.id
JOIN revora_guild_pk_matches m ON m.contest=o.run WHERE m.id=@run AND g.id=@guild AND o.eligible FOR SHARE OF g,o",
                    "run",run,"guild",(long)state.Keeper))if(q.ExecuteScalar()==null)state.Keeper=0;
            }
            if(state.Keeper==state.First)state.FirstScore=checked(state.FirstScore+seconds);
            else if(state.Keeper==state.Second)state.SecondScore=checked(state.SecondScore+seconds);
            state.Accounted=state.Accounted.AddSeconds(seconds);
            if(Q.Execute(c,t,"UPDATE revora_guild_pk_matches SET first_score=@first,second_score=@second WHERE id=@run AND state='active'",
                "run",run,"first",state.FirstScore,"second",state.SecondScore)!=1)throw new InvalidOperationException("Guild PK possession score was not saved.");
            if(Q.Execute(c,t,"UPDATE revora_guild_pk_banners SET keeper=@keeper,accounted_at=@now WHERE run=@run",
                "run",run,"keeper",(long)state.Keeper,"now",state.Accounted)!=1)throw new InvalidOperationException("Guild PK possession time was not saved.");
        }
        internal static Snapshot Pulse(Guid run,DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild PK clock requires UTC.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var state=Read(c,t,run);Accrue(c,t,run,state,utc);t.Commit();return state;
            }}
        }
        internal static Snapshot Settle(Guid run,DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild PK clock requires UTC.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var state=Read(c,t,run);
                if(state.State!="active" || utc<state.Start){t.Commit();return state;}
                Accrue(c,t,run,state,utc);int first=0,second=0;
                using(var q=Q.Command(c,t,@"SELECT p.guild_id FROM revora_guild_pk_entries p
JOIN entities e ON e.uid=p.uid AND e.guildid=p.guild_id AND e.guildrank BETWEEN 1 AND 1000 AND e.mapid=p.map
JOIN guilds g ON g.id=p.guild_id JOIN revora_guild_admissions a ON a.uid=p.uid AND a.guild_id=p.guild_id AND a.admission=p.admission
JOIN revora_guild_pk_matches m ON m.id=p.run JOIN revora_guild_contest_owners o ON o.run=m.contest AND o.guild_id=p.guild_id AND o.eligible
WHERE p.run=@run AND p.active AND p.deaths<20 ORDER BY p.uid FOR SHARE OF p,e,g,a,o","run",run))using(var r=q.ExecuteReader()) {
                    while(r.Read()){long guild=r.GetInt64(0);if(guild==state.First)first++;else if(guild==state.Second)second++;}
                }
                var decision=Revora.Game.Features.GuildPkRules.Decide(state.FirstScore,state.SecondScore,first,second,utc-state.Start,utc>=state.End);
                if(decision==null){t.Commit();return state;}
                state.Winner=decision.Side==1?state.First:decision.Side==2?state.Second:0;
                state.State=decision.Side==3?"cancelled":"complete";
                if(Q.Execute(c,t,@"INSERT INTO revora_guild_pk_results(run,winner,reason,first_score,second_score,settled_at)
VALUES(@run,CASE WHEN @winner=0 THEN NULL ELSE @winner END,@reason,@first,@second,@now)",
                    "run",run,"winner",(long)state.Winner,"reason",decision.Reason,"first",state.FirstScore,"second",state.SecondScore,"now",utc)!=1)
                    throw new InvalidOperationException("Guild PK result was not saved.");
                if(Q.Execute(c,t,"UPDATE revora_guild_pk_matches SET state=@state,winner=CASE WHEN @winner=0 THEN NULL ELSE @winner END WHERE id=@run AND state='active'",
                    "run",run,"state",state.State,"winner",(long)state.Winner)!=1)throw new InvalidOperationException("Guild PK result ownership changed.");
                t.Commit();return state;
            }}
        }
        private static bool Eligible(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint uid,uint guild,Guid admission,DateTime utc) {
                using(var q=Q.Command(c,t,@"SELECT e.uid FROM revora_guild_pk_entries p
JOIN entities e ON e.uid=p.uid AND e.guildid=p.guild_id AND e.mapid=p.map AND e.guildrank BETWEEN 1 AND 1000
JOIN guilds g ON g.id=e.guildid JOIN revora_guild_admissions a ON a.uid=e.uid AND a.guild_id=g.id AND a.admission=p.admission
JOIN revora_guild_pk_maps f ON f.run=p.run AND f.map=p.map JOIN revora_guild_pk_matches m ON m.id=p.run
JOIN revora_guild_contest_owners o ON o.run=m.contest AND o.guild_id=g.id AND o.eligible
WHERE p.run=@run AND p.uid=@uid AND p.guild_id=@guild AND p.admission=@admission AND p.active AND p.deaths<20
AND a.joined_at<=@cutoff FOR SHARE OF e,g,a,p,o",
                    "run",run,"uid",(long)uid,"guild",(long)guild,"admission",admission,"cutoff",utc.AddDays(-7)))return q.ExecuteScalar()!=null;
        }
        private static bool CaptureInside(NpgsqlConnection c,NpgsqlTransaction t,Guid run,Snapshot state,int number,uint uid,uint guild,Guid admission,DateTime utc) {
                // Resolve an uncertain commit before applying window/state rules.
                using(var q=Q.Command(c,t,"SELECT uid=@uid AND guild_id=@guild AND admission=@admission FROM revora_guild_pk_captures WHERE run=@run AND number=@number",
                    "run",run,"number",number,"uid",(long)uid,"guild",(long)guild,"admission",admission)) {
                    var receipt=q.ExecuteScalar();if(receipt!=null){return (bool)receipt;}
                }
                if(state.State!="active" || utc<state.Start || utc>=state.End || utc<state.Accounted || number!=state.Capture+1
                    || guild==state.Keeper || guild!=state.First && guild!=state.Second)return false;
                if(!Eligible(c,t,run,uid,guild,admission,utc))return false;
                Accrue(c,t,run,state,utc);
                if(Q.Execute(c,t,"INSERT INTO revora_guild_pk_captures(run,number,uid,guild_id,admission,captured_at) VALUES(@run,@number,@uid,@guild,@admission,@now)",
                    "run",run,"number",number,"uid",(long)uid,"guild",(long)guild,"admission",admission,"now",utc)!=1)throw new InvalidOperationException("Guild PK capture was not saved.");
                if(Q.Execute(c,t,"UPDATE revora_guild_pk_banners SET keeper=@guild,capture=@number,accounted_at=@now,hitpoints=1000000 WHERE run=@run AND capture=@before",
                    "run",run,"guild",(long)guild,"number",number,"now",utc,"before",state.Capture)!=1)throw new InvalidOperationException("Guild PK banner ownership changed.");
            return true;
        }
        internal static bool Strike(Guid run,long number,uint uid,uint guild,Guid admission,uint damage,uint before,DateTime utc) {
            if(number<=0 || uid==0 || guild==0 || admission==Guid.Empty || damage==0 || before==0 || before>1000000 || damage>before || utc.Kind!=DateTimeKind.Utc)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var state=Read(c,t,run);
                using(var q=Q.Command(c,t,"SELECT uid=@uid AND guild_id=@guild AND admission=@admission AND damage=@damage AND hp_before=@before FROM revora_guild_pk_banner_hits WHERE run=@run AND number=@number",
                    "run",run,"number",number,"uid",(long)uid,"guild",(long)guild,"admission",admission,"damage",(int)damage,"before",(int)before)) {
                    var saved=q.ExecuteScalar();if(saved!=null){t.Commit();return Equals(saved,true);}
                }
                if(state.State!="active" || utc<state.Start || utc>=state.End || utc<state.Accounted || state.HitNumber==long.MaxValue || number!=state.HitNumber+1
                    || before!=state.Hitpoints || guild==state.Keeper || guild!=state.First && guild!=state.Second || !Eligible(c,t,run,uid,guild,admission,utc))return false;
                if(damage==before && !CaptureInside(c,t,run,state,checked(state.Capture+1),uid,guild,admission,utc))return false;
                if(Q.Execute(c,t,@"INSERT INTO revora_guild_pk_banner_hits(run,number,uid,guild_id,admission,damage,hp_before,capture_before,hit_at)
VALUES(@run,@number,@uid,@guild,@admission,@damage,@before,@capture,@now)",
                    "run",run,"number",number,"uid",(long)uid,"guild",(long)guild,"admission",admission,"damage",(int)damage,"before",(int)before,"capture",state.Capture,"now",utc)!=1)
                    throw new InvalidOperationException("Guild PK banner damage was not saved.");
                if(Q.Execute(c,t,"UPDATE revora_guild_pk_banners SET hit_number=@number,hitpoints=@hp WHERE run=@run AND hit_number=@previous",
                    "run",run,"number",number,"hp",damage==before?1000000:(int)(before-damage),"previous",state.HitNumber)!=1)
                    throw new InvalidOperationException("Guild PK banner damage ownership changed.");
                t.Commit();return true;
            }}
        }
        internal static bool Capture(Guid run,int number,uint uid,uint guild,Guid admission,DateTime utc) {
            if(run==Guid.Empty || number<=0 || uid==0 || guild==0 || admission==Guid.Empty || utc.Kind!=DateTimeKind.Utc)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var state=Read(c,t,run);
                bool result=CaptureInside(c,t,run,state,number,uid,guild,admission,utc);
                if(!result)return false;
                t.Commit();return true;
            }}
        }
    }
}
