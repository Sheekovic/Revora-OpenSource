using System;
using System.Collections.Generic;
using System.Globalization;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;
using StorageManager = Revora.StorageManager;

namespace Revora.Game.Features
{
    // Origin-qualified CS Elite PK edition: placements 1/2/3/4-8 of a finalized
    // cs-epk-v1 run earn Legendary, Peerless, Outstanding and Expert. Pending
    // awards are keyed by (realm, uid); the live session owner authenticates
    // delivery after a process restart. Local Elite PK still grants only
    // Infernal Beast.
    internal static class OriginElitePk
    {
        internal const string Source = "cs-epk-revora-v1";
        internal const string EventKey = "cs-epk-v1";
        internal const int DurationDays = 14;
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;

        internal static int TitleFor(int placement)
        {
            if (placement == 1) return 20352035;
            if (placement == 2) return 20362036;
            if (placement == 3) return 20372037;
            if (placement >= 4 && placement <= 8) return 20382038;
            return 0;
        }

        internal static void Initialize()
        {
            OriginTournaments.Initialize();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var t = c.BeginTransaction())
                {
                    Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_origin_epk_awards(
run_id uuid NOT NULL REFERENCES revora_origin_runs(run_id),
realm varchar(32) NOT NULL,uid bigint NOT NULL CHECK(uid>0),
owner uuid NOT NULL,title_key integer NOT NULL CHECK(title_key IN (20352035,20362036,20372037,20382038)),
earned_at timestamptz NOT NULL,award_recorded boolean NOT NULL DEFAULT false,
PRIMARY KEY(run_id,realm,uid,title_key));
ALTER TABLE revora_origin_epk_awards ADD COLUMN IF NOT EXISTS owner uuid;");
                    Q.Execute(c, t, @"UPDATE revora_origin_epk_awards a SET owner=e.owner
FROM revora_origin_entrants e
WHERE a.run_id=e.run_id AND a.realm=e.realm AND a.uid=e.uid
AND (a.owner IS NULL OR a.owner='00000000-0000-0000-0000-000000000000');
DELETE FROM revora_origin_epk_awards WHERE owner IS NULL OR owner='00000000-0000-0000-0000-000000000000';
ALTER TABLE revora_origin_epk_awards ALTER COLUMN owner SET NOT NULL;");
                    Q.Execute(c, t, @"DO $$ BEGIN
IF NOT EXISTS (
SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname=current_schema() AND t.relname='revora_origin_epk_awards' AND c.conname='revora_origin_epk_awards_owner_nonempty') THEN
ALTER TABLE revora_origin_epk_awards ADD CONSTRAINT revora_origin_epk_awards_owner_nonempty CHECK (owner<>'00000000-0000-0000-0000-000000000000');
END IF;
END $$;
DROP INDEX IF EXISTS revora_origin_epk_pending;
CREATE INDEX IF NOT EXISTS revora_origin_epk_pending ON revora_origin_epk_awards(realm,uid) WHERE NOT award_recorded;");
                    t.Commit();
                }
            }
        }

        internal static bool Capture(Guid run, Guid coordinator)
        {
            if (run == Guid.Empty) throw new ArgumentException("Origin Elite PK run is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    OriginTournaments.LockRun(c, t, run);
                    OriginTournaments.RequireCoordinator(c, t, run, coordinator);
                    string eventKey;
                    string state;
                    DateTime earned;
                    using (var q = Q.Command(c, t, "SELECT event_key,state,finalized_at FROM revora_origin_runs WHERE run_id=@run FOR UPDATE", "run", run))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Origin Elite PK run is unknown.");
                        eventKey = r.GetString(0);
                        state = r.GetString(1);
                        if (r.IsDBNull(2)) throw new InvalidOperationException("Origin Elite PK run is not finalized.");
                        earned = r.GetFieldValue<DateTimeOffset>(2).UtcDateTime;
                    }
                    if (eventKey != EventKey) throw new InvalidOperationException("Origin Elite PK event is invalid.");
                    if (state != "finalized") throw new InvalidOperationException("Origin Elite PK run is not finalized.");
                    using (var q = Q.Command(c, t, "SELECT count(DISTINCT realm) FROM revora_origin_entrants WHERE run_id=@run", "run", run))
                        if (Convert.ToInt64(q.ExecuteScalar()) < 2) throw new InvalidOperationException("Origin Elite PK needs distinct origins.");
                    var rows = new List<Tuple<string, uint, int, Guid>>();
                    using (var q = Q.Command(c, t, @"SELECT p.realm,p.uid,p.placement,e.owner
FROM revora_origin_placements p JOIN revora_origin_entrants e
ON e.run_id=p.run_id AND e.realm=p.realm AND e.uid=p.uid
WHERE p.run_id=@run AND p.placement BETWEEN 1 AND 8
ORDER BY p.placement", "run", run))
                    using (var r = q.ExecuteReader())
                        while (r.Read())
                            rows.Add(Tuple.Create(r.GetString(0), checked((uint)r.GetInt64(1)), r.GetInt32(2), r.GetGuid(3)));
                    int expected = 0, added = 0;
                    foreach (var row in rows)
                    {
                        int title = TitleFor(row.Item3);
                        if (title == 0) continue;
                        if (row.Item4 == Guid.Empty) throw new InvalidOperationException("Origin Elite PK award owner is missing.");
                        expected++;
                        added += Q.Execute(c, t, @"INSERT INTO revora_origin_epk_awards(run_id,realm,uid,owner,title_key,earned_at)
VALUES(@run,@realm,@uid,@owner,@title,@earned) ON CONFLICT DO NOTHING",
                            "run", run, "realm", row.Item1, "uid", (long)row.Item2, "owner", row.Item4, "title", title, "earned", earned);
                    }
                    if (expected == 0) throw new InvalidOperationException("Origin Elite PK placements are missing.");
                    t.Commit();
                    return added == expected;
                }
            }
        }

        internal static int VisibleCount(uint uid, string realm)
        {
            if (!OriginRealms.Token(realm, 32) || uid == 0)
                throw new ArgumentException("Origin Elite PK delivery is invalid.");
            DateTime utc = Clock();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = Q.Command(c, null, @"SELECT count(*) FROM (
SELECT DISTINCT ON(title_key,source) title_key FROM revora_event_awards
WHERE uid=@uid AND source=@source AND NOT revoked AND earned_at<=@now
AND expires_at IS NOT NULL AND expires_at>@now
AND source_key LIKE @prefix
ORDER BY title_key,source,earned_at DESC) s",
                    "uid", (long)uid, "source", Source, "now", utc, "prefix", realm + ":%"))
                    return Convert.ToInt32(q.ExecuteScalar());
            }
        }

        internal static List<string> Deliver(string realm, uint uid, Guid owner)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    var notices = RecoverAward(c, t, uid, Clock(), realm, owner);
                    t.Commit();
                    return notices;
                }
            }
        }

        internal static List<string> RecoverAward(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, uint uid, DateTime utc, string realm, Guid owner)
        {
            var notices = new List<string>();
            if (string.IsNullOrEmpty(realm) || owner == Guid.Empty) return notices;
            if (!OriginRealms.Token(realm, 32) || uid == 0)
                throw new ArgumentException("Origin Elite PK delivery is invalid.");
            OriginRealms.LockIdentity(c, t, realm, uid);
            OriginRealms.RequireOwnedSession(c, t, realm, uid, owner);
            var pending = new List<Tuple<Guid, string, int, DateTime>>();
            using (var q = Q.Command(c, t, @"SELECT a.run_id,a.realm,a.title_key,a.earned_at
FROM revora_origin_epk_awards a JOIN revora_origin_runs r ON r.run_id=a.run_id
WHERE a.realm=@realm AND a.uid=@uid AND NOT a.award_recorded
AND r.state='finalized' AND r.event_key=@event AND a.earned_at<=@now
ORDER BY a.earned_at,a.title_key LIMIT 8 FOR UPDATE OF a",
                "realm", realm, "uid", (long)uid, "event", EventKey, "now", utc))
            using (var r = q.ExecuteReader())
                while (r.Read())
                    pending.Add(Tuple.Create(r.GetGuid(0), r.GetString(1), r.GetInt32(2), r.GetFieldValue<DateTimeOffset>(3).UtcDateTime));
            foreach (var award in pending)
            {
                if (!StorageManager.Known(award.Item3 / 10000, award.Item3 % 10000))
                    throw new InvalidOperationException("CS Elite PK award catalog mapping changed.");
                int points = StorageManager.GetTitlePoints((short)(award.Item3 / 10000), (short)(award.Item3 % 10000));
                if (award.Item3 == 20352035 && points != 500) throw new InvalidOperationException("Legendary catalog points changed.");
                if (award.Item3 == 20362036 && points != 450) throw new InvalidOperationException("Peerless catalog points changed.");
                if (award.Item3 == 20372037 && points != 400) throw new InvalidOperationException("Outstanding catalog points changed.");
                if (award.Item3 == 20382038 && points != 300) throw new InvalidOperationException("Expert catalog points changed.");
                string key = award.Item2 + ":" + award.Item1.ToString() + ":" + award.Item3.ToString(CultureInfo.InvariantCulture);
                DateTime expiry = award.Item4.AddDays(DurationDays);
                if (!ProgressionEvidence.Grant(c, t, uid, Source, key, award.Item3, award.Item4))
                {
                    using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_event_awards
WHERE uid=@uid AND source=@source AND source_key=@key AND title_key=@title AND earned_at=@earned AND expires_at=@expiry)",
                        "uid", (long)uid, "source", Source, "key", key, "title", award.Item3, "earned", award.Item4, "expiry", expiry))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("CS Elite PK award was not saved.");
                }
                if (Q.Execute(c, t, @"UPDATE revora_origin_epk_awards SET award_recorded=true, owner=@owner
WHERE run_id=@run AND realm=@realm AND uid=@uid AND title_key=@title AND NOT award_recorded",
                    "run", award.Item1, "realm", award.Item2, "uid", (long)uid, "title", award.Item3, "owner", owner) != 1)
                    throw new InvalidOperationException("CS Elite PK award receipt was not acknowledged.");
                notices.Add("Your cross-server Elite PK standing earned a wardrobe award. Find it in the wardrobe.");
            }
            return notices;
        }
    }
}
