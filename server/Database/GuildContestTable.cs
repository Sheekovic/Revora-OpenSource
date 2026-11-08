using System;
using System.Collections.Generic;
using Npgsql;
using Revora.Game.Features;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Database
{
    // Persistence prerequisite only. Runtime-owned entry and monster producers
    // must be integrated before this ledger can enable an event.
    internal static class GuildContestTable
    {
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_guild_contests(
id uuid PRIMARY KEY,starts_at timestamptz NOT NULL UNIQUE,ends_at timestamptz NOT NULL,
state text NOT NULL CHECK(state IN('open','frozen')),CHECK(ends_at>starts_at));
ALTER TABLE revora_guild_contests DROP CONSTRAINT IF EXISTS revora_guild_contests_state_check;
ALTER TABLE revora_guild_contests ADD CONSTRAINT revora_guild_contests_state_check CHECK(state IN('open','frozen','cancelled'));
CREATE TABLE IF NOT EXISTS revora_guild_contest_entries(
run uuid NOT NULL REFERENCES revora_guild_contests(id),uid bigint NOT NULL,guild_id bigint NOT NULL CHECK(guild_id BETWEEN 1 AND 65535),
band smallint NOT NULL CHECK(band BETWEEN 0 AND 3),map bigint NOT NULL CHECK(map>0),entered_at timestamptz NOT NULL,
PRIMARY KEY(run,uid));
ALTER TABLE revora_guild_contest_entries ADD COLUMN IF NOT EXISTS active boolean NOT NULL DEFAULT true;
CREATE TABLE IF NOT EXISTS revora_guild_contest_maps(
run uuid NOT NULL REFERENCES revora_guild_contests(id),band smallint NOT NULL CHECK(band BETWEEN 0 AND 3),
map bigint NOT NULL CHECK(map BETWEEN 11000 AND 59999),PRIMARY KEY(run,band),UNIQUE(run,map));
CREATE TABLE IF NOT EXISTS revora_guild_contest_transfers(
run uuid NOT NULL REFERENCES revora_guild_contests(id),uid bigint NOT NULL,band smallint NOT NULL,
map bigint NOT NULL,guild_id bigint NOT NULL,return_map integer NOT NULL,return_x integer NOT NULL,return_y integer NOT NULL,
state text NOT NULL CHECK(state IN('reserved','entered','returned')),reserved_at timestamptz NOT NULL,
PRIMARY KEY(run,uid),FOREIGN KEY(run,band) REFERENCES revora_guild_contest_maps(run,band));
CREATE UNIQUE INDEX IF NOT EXISTS revora_guild_contest_active_transfer ON revora_guild_contest_transfers(uid) WHERE state<>'returned';
CREATE TABLE IF NOT EXISTS revora_guild_contest_lives(
run uuid NOT NULL REFERENCES revora_guild_contests(id),life uuid NOT NULL,band smallint NOT NULL CHECK(band BETWEEN 0 AND 3),
map bigint NOT NULL CHECK(map>0),PRIMARY KEY(run,life));
CREATE TABLE IF NOT EXISTS revora_guild_contest_kills(
run uuid NOT NULL,life uuid NOT NULL,uid bigint NOT NULL,guild_id bigint NOT NULL,earned_at timestamptz NOT NULL,
PRIMARY KEY(run,life),FOREIGN KEY(run,life) REFERENCES revora_guild_contest_lives(run,life),
FOREIGN KEY(run,uid) REFERENCES revora_guild_contest_entries(run,uid));
CREATE TABLE IF NOT EXISTS revora_guild_contest_seeds(
run uuid NOT NULL REFERENCES revora_guild_contests(id),rank smallint NOT NULL CHECK(rank BETWEEN 1 AND 8),
guild_id bigint NOT NULL,points bigint NOT NULL CHECK(points>0),reached_at timestamptz NOT NULL,
PRIMARY KEY(run,rank),UNIQUE(run,guild_id));
CREATE TABLE IF NOT EXISTS revora_guild_contest_owners(
run uuid NOT NULL REFERENCES revora_guild_contests(id),guild_id bigint NOT NULL,
eligible boolean NOT NULL DEFAULT true,PRIMARY KEY(run,guild_id));
CREATE INDEX IF NOT EXISTS revora_guild_contest_owner_live ON revora_guild_contest_owners(guild_id) WHERE eligible;
CREATE OR REPLACE FUNCTION revora_guild_contest_dissolved() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
BEGIN
IF TG_OP='DELETE' THEN
UPDATE revora_guild_contest_owners SET eligible=false WHERE guild_id=OLD.id AND eligible;
RETURN OLD;
ELSIF NEW.id IS DISTINCT FROM OLD.id THEN
UPDATE revora_guild_contest_owners SET eligible=false WHERE guild_id IN(OLD.id,NEW.id) AND eligible;
END IF;
RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='guilds'::regclass AND tgname='revora_guild_contest_dissolved') THEN
CREATE TRIGGER revora_guild_contest_dissolved AFTER DELETE OR UPDATE OF id ON guilds
FOR EACH ROW EXECUTE FUNCTION revora_guild_contest_dissolved();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='guilds'::regclass AND tgname='revora_guild_contest_dissolved' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=25 AND tgfoid='revora_guild_contest_dissolved()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='guilds'::regclass AND attname='id' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_guild_contest_dissolved';
END IF; END $revora_trigger$;
CREATE TABLE IF NOT EXISTS revora_guild_contest_prizes(
run uuid NOT NULL,guild_id bigint NOT NULL,uid bigint NOT NULL,rank smallint NOT NULL CHECK(rank BETWEEN 1 AND 8),
silver bigint NOT NULL CHECK(silver IN(60000000,50000000,40000000,35000000,30000000,25000000,20000000,15000000)),
claimed_at timestamptz NOT NULL,PRIMARY KEY(run,guild_id),
FOREIGN KEY(run,guild_id) REFERENCES revora_guild_contest_seeds(run,guild_id));");t.Commit();
            }}
        }
        private static void Bound(NpgsqlConnection c,NpgsqlTransaction t) {
            Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
        }
        // Bound only on confirmed arrival, never inferred from historical scores.
        internal static bool BindGuild(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint guild) {
            if(t==null || !ReferenceEquals(t.Connection,c))throw new InvalidOperationException("Guild contest binding requires a transaction.");
            using(var q=Q.Command(c,t,"SELECT id FROM guilds WHERE id=@guild FOR SHARE","guild",(long)guild))if(q.ExecuteScalar()==null)return false;
            Q.Execute(c,t,"INSERT INTO revora_guild_contest_owners(run,guild_id) VALUES(@run,@guild) ON CONFLICT DO NOTHING","run",run,"guild",(long)guild);
            using(var q=Q.Command(c,t,"SELECT eligible FROM revora_guild_contest_owners WHERE run=@run AND guild_id=@guild FOR SHARE","run",run,"guild",(long)guild))return Equals(q.ExecuteScalar(),true);
        }
        internal static Guid Unclaimed(uint guild,DateTime utc) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Bound(c,t);
                using(var q=Q.Command(c,t,@"SELECT s.run FROM revora_guild_contest_seeds s
JOIN revora_guild_contests r ON r.id=s.run JOIN revora_guild_contest_owners o ON o.run=s.run AND o.guild_id=s.guild_id
WHERE s.guild_id=@guild AND o.eligible AND r.state='frozen' AND r.ends_at<=@now
AND NOT EXISTS(SELECT 1 FROM revora_guild_contest_prizes p WHERE p.run=s.run AND p.guild_id=s.guild_id)
ORDER BY r.starts_at LIMIT 1","guild",(long)guild,"now",utc)) {
                    var value=q.ExecuteScalar();t.Commit();return value==null?Guid.Empty:(Guid)value;
                }
            }}
        }
        // Caller commits the receipt and treasury delta together, then projects
        // the returned balance while retaining session and guild ownership.
        internal static int Prize(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint uid,uint guild,DateTime utc,out ulong funds) {
            funds=0;
            if(t==null || !ReferenceEquals(t.Connection,c))throw new InvalidOperationException("Guild contest prize requires a transaction.");
            if(run==Guid.Empty || uid==0 || guild==0 || utc.Kind!=DateTimeKind.Utc)return 0;
            Bound(c,t);
            using(var q=Q.Command(c,t,"SELECT id FROM revora_guild_contests WHERE id=@run AND state='frozen' AND ends_at<=@now FOR UPDATE","run",run,"now",utc))if(q.ExecuteScalar()==null)return 0;
            string name;int office;
            using(var q=Q.Command(c,t,"SELECT name,guildrank FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank IN(990,1000) FOR UPDATE","uid",(long)uid,"guild",(long)guild))using(var r=q.ExecuteReader()) {
                if(!r.Read())return 0;name=r.GetString(0);office=Convert.ToInt32(r[1]);
            }
            decimal balance;
            using(var q=Q.Command(c,t,"SELECT silverfund,leadername FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild))using(var r=q.ExecuteReader()) {
                if(!r.Read() || office==1000 && r.GetString(1)!=name)return 0;balance=Convert.ToDecimal(r[0]);
            }
            using(var q=Q.Command(c,t,"SELECT eligible FROM revora_guild_contest_owners WHERE run=@run AND guild_id=@guild FOR SHARE","run",run,"guild",(long)guild))if(!Equals(q.ExecuteScalar(),true))return 0;
            using(var q=Q.Command(c,t,"SELECT uid FROM revora_guild_contest_prizes WHERE run=@run AND guild_id=@guild","run",run,"guild",(long)guild))
                if(q.ExecuteScalar()!=null){funds=checked((ulong)balance);return 2;}
            int rank;
            using(var q=Q.Command(c,t,"SELECT rank FROM revora_guild_contest_seeds WHERE run=@run AND guild_id=@guild","run",run,"guild",(long)guild)) {
                var value=q.ExecuteScalar();if(value==null)return 0;rank=Convert.ToInt32(value);
            }
            long amount=GuildPkRules.ContestPrize(rank);
            if(balance<0 || balance>long.MaxValue-amount)return 0;
            if(Q.Execute(c,t,@"INSERT INTO revora_guild_contest_prizes(run,guild_id,uid,rank,silver,claimed_at)
VALUES(@run,@guild,@uid,@rank,@silver,@now)","run",run,"guild",(long)guild,"uid",(long)uid,"rank",rank,"silver",amount,"now",utc)!=1)
                throw new InvalidOperationException("Guild contest prize receipt was not saved.");
            balance+=amount;
            if(Q.Execute(c,t,"UPDATE guilds SET silverfund=@fund WHERE id=@guild","fund",balance,"guild",(long)guild)!=1)
                throw new InvalidOperationException("Guild contest treasury prize was not saved.");
            funds=checked((ulong)balance);return 1;
        }
        internal static bool Kill(Guid run,Guid life,uint uid,uint guild,DateTime utc) {
            if(run==Guid.Empty || life==Guid.Empty || uid==0 || guild==0 || utc.Kind!=DateTimeKind.Utc)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Bound(c,t);bool open;
                using(var q=Q.Command(c,t,"SELECT state='open' AND @now>=starts_at AND @now<ends_at FROM revora_guild_contests WHERE id=@run FOR UPDATE","run",run,"now",utc)) {
                    object state=q.ExecuteScalar();if(state==null)return false;open=(bool)state;
                }
                // A committed death decision remains recoverable after closing.
                using(var q=Q.Command(c,t,"SELECT uid=@uid AND guild_id=@guild FROM revora_guild_contest_kills WHERE run=@run AND life=@life",
                    "run",run,"life",life,"uid",(long)uid,"guild",(long)guild)) {
                    object receipt=q.ExecuteScalar();if(receipt!=null){t.Commit();return (bool)receipt;}
                }
                if(!open)return false;
                using(var q=Q.Command(c,t,@"SELECT e.uid FROM entities e JOIN guilds g ON g.id=e.guildid
WHERE e.uid=@uid AND g.id=@guild AND e.guildrank BETWEEN 1 AND 1000 FOR SHARE OF e,g",
                    "uid",(long)uid,"guild",(long)guild))if(q.ExecuteScalar()==null)return false;
                int inserted=Q.Execute(c,t,@"INSERT INTO revora_guild_contest_kills(run,life,uid,guild_id,earned_at)
SELECT p.run,l.life,p.uid,p.guild_id,@now FROM revora_guild_contest_entries p
JOIN revora_guild_contest_lives l ON l.run=p.run AND l.band=p.band AND l.map=p.map
JOIN revora_guild_contests r ON r.id=p.run
JOIN revora_guild_contest_owners o ON o.run=p.run AND o.guild_id=p.guild_id AND o.eligible
WHERE p.run=@run AND p.uid=@uid AND p.guild_id=@guild AND l.life=@life AND p.active
AND p.entered_at>=r.starts_at-interval '5 minutes' AND p.entered_at<=@now",
                    "run",run,"life",life,"uid",(long)uid,"guild",(long)guild,"now",utc);
                if(inserted!=1)return false;t.Commit();return true;
            }}
        }
        internal static GuildPkRules.Score[] Scores(Guid run) {
            if(run==Guid.Empty)return new GuildPkRules.Score[0];
            var scores=new List<GuildPkRules.Score>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Bound(c,t);
                // One statement sees one committed snapshot. Frozen standings
                // come exclusively from the immutable final seeds.
                using(var q=Q.Command(c,t,@"SELECT guild_id,points,reached_at FROM (
SELECT k.guild_id,count(*) AS points,max(k.earned_at) AS reached_at
FROM revora_guild_contest_kills k JOIN revora_guild_contests r ON r.id=k.run
WHERE r.id=@run AND r.state='open' GROUP BY k.guild_id
UNION ALL
SELECT s.guild_id,s.points,s.reached_at FROM revora_guild_contest_seeds s
JOIN revora_guild_contests r ON r.id=s.run WHERE r.id=@run AND r.state='frozen'
) scores ORDER BY points DESC,reached_at,guild_id LIMIT 8","run",run))using(var r=q.ExecuteReader())
                    while(r.Read())scores.Add(new GuildPkRules.Score(checked((uint)r.GetInt64(0)),r.GetInt64(1),r.GetFieldValue<DateTimeOffset>(2).UtcDateTime));
                t.Commit();
            }}
            return scores.ToArray();
        }
        internal static uint[] Freeze(Guid run,DateTime utc) {
            if(run==Guid.Empty || utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Invalid guild contest finalization.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Bound(c,t);bool frozen;
                using(var q=Q.Command(c,t,"SELECT state,ends_at<=@now FROM revora_guild_contests WHERE id=@run FOR UPDATE","run",run,"now",utc))using(var r=q.ExecuteReader()) {
                    if(!r.Read())throw new InvalidOperationException("Unknown guild contest.");
                    string state=r.GetString(0);
                    if(state!="open" && state!="frozen")throw new InvalidOperationException("Guild contest was cancelled.");
                    frozen=state=="frozen";if(!frozen && !r.GetBoolean(1))throw new InvalidOperationException("Guild contest has not ended.");
                }
                if(!frozen) {
                    var scores=new List<GuildPkRules.Score>();
                    using(var q=Q.Command(c,t,"SELECT guild_id,count(*),max(earned_at) FROM revora_guild_contest_kills WHERE run=@run GROUP BY guild_id","run",run))using(var r=q.ExecuteReader())
                        while(r.Read())scores.Add(new GuildPkRules.Score(checked((uint)r.GetInt64(0)),r.GetInt64(1),r.GetFieldValue<DateTimeOffset>(2).UtcDateTime));
                    uint[] seeds=GuildPkRules.Qualify(scores);
                    for(int i=0;i<seeds.Length;i++) {
                        var score=scores.Find(s=>s.Guild==seeds[i]);
                        if(Q.Execute(c,t,"INSERT INTO revora_guild_contest_seeds(run,rank,guild_id,points,reached_at) VALUES(@run,@rank,@guild,@points,@reached)",
                            "run",run,"rank",i+1,"guild",(long)score.Guild,"points",score.Points,"reached",score.Reached)!=1)throw new InvalidOperationException("Guild seed was not saved.");
                    }
                    if(Q.Execute(c,t,"UPDATE revora_guild_contests SET state='frozen' WHERE id=@run AND state='open'","run",run)!=1)throw new InvalidOperationException("Guild contest ownership changed.");
                }
                var result=new List<uint>();using(var q=Q.Command(c,t,"SELECT guild_id FROM revora_guild_contest_seeds WHERE run=@run ORDER BY rank","run",run))using(var r=q.ExecuteReader())while(r.Read())result.Add(checked((uint)r.GetInt64(0)));
                t.Commit();return result.ToArray();
            }}
        }
    }
}
