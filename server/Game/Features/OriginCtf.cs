using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;
using StorageManager = Revora.StorageManager;

namespace Revora.Game.Features
{
    // Origin-qualified CS-CTF edition: placement 1 of a finalized cs-ctf-v1 run
    // earns Conqueror and Radiant Wings. Pending awards are keyed by (realm, uid);
    // the live session owner authenticates delivery after a process restart.
    // Local CTF still grants only Victor.
    internal static class OriginCtf
    {
        internal const string Source = "cs-ctf-revora-v1";
        internal const string EventKey = "cs-ctf-v1";
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private sealed class DeliveryAuth { internal string Realm; internal Guid Owner; }
        private static readonly ConditionalWeakTable<GameState, DeliveryAuth> Bound = new ConditionalWeakTable<GameState, DeliveryAuth>();

        internal static void Initialize()
        {
            OriginTournaments.Initialize();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var t = c.BeginTransaction())
                {
                    Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_origin_ctf_awards(
run_id uuid NOT NULL REFERENCES revora_origin_runs(run_id),
realm varchar(32) NOT NULL,uid bigint NOT NULL CHECK(uid>0),
owner uuid NOT NULL,title_key integer NOT NULL CHECK(title_key IN (20030004,60020021)),
earned_at timestamptz NOT NULL,award_recorded boolean NOT NULL DEFAULT false,
PRIMARY KEY(run_id,realm,uid,title_key));
ALTER TABLE revora_origin_ctf_awards ADD COLUMN IF NOT EXISTS owner uuid;");
                    Q.Execute(c, t, @"UPDATE revora_origin_ctf_awards a SET owner=e.owner
FROM revora_origin_entrants e
WHERE a.run_id=e.run_id AND a.realm=e.realm AND a.uid=e.uid
AND (a.owner IS NULL OR a.owner='00000000-0000-0000-0000-000000000000');
DELETE FROM revora_origin_ctf_awards WHERE owner IS NULL OR owner='00000000-0000-0000-0000-000000000000';
ALTER TABLE revora_origin_ctf_awards ALTER COLUMN owner SET NOT NULL;");
                    Q.Execute(c, t, @"DO $$ BEGIN
IF EXISTS (
SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname=current_schema() AND t.relname='revora_origin_ctf_awards' AND c.contype='p'
AND pg_get_constraintdef(c.oid) NOT ILIKE '%realm%') THEN
ALTER TABLE revora_origin_ctf_awards DROP CONSTRAINT revora_origin_ctf_awards_pkey;
ALTER TABLE revora_origin_ctf_awards ADD PRIMARY KEY(run_id,realm,uid,title_key);
END IF;
IF NOT EXISTS (
SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname=current_schema() AND t.relname='revora_origin_ctf_awards' AND c.conname='revora_origin_ctf_awards_owner_nonempty') THEN
ALTER TABLE revora_origin_ctf_awards ADD CONSTRAINT revora_origin_ctf_awards_owner_nonempty CHECK (owner<>'00000000-0000-0000-0000-000000000000');
END IF;
END $$;
DROP INDEX IF EXISTS revora_origin_ctf_pending;
CREATE INDEX IF NOT EXISTS revora_origin_ctf_pending ON revora_origin_ctf_awards(realm,uid) WHERE NOT award_recorded;
DO $$ BEGIN
IF to_regclass('revora_event_awards') IS NULL THEN RETURN; END IF;
PERFORM 1 FROM revora_event_awards WHERE source='cs-ctf-revora-v1' FOR UPDATE;
UPDATE revora_event_awards e SET source_key=a.realm || ':' || e.source_key
FROM revora_origin_ctf_awards a
WHERE e.source='cs-ctf-revora-v1' AND e.uid=a.uid AND e.title_key=a.title_key AND a.award_recorded
AND e.source_key=a.run_id::text || ':' || a.title_key::text
AND NOT EXISTS (
SELECT 1 FROM revora_event_awards x WHERE x.uid=e.uid AND x.source=e.source
AND x.source_key=a.realm || ':' || e.source_key);
DELETE FROM revora_event_awards e USING revora_origin_ctf_awards a
WHERE e.source='cs-ctf-revora-v1' AND e.uid=a.uid AND e.title_key=a.title_key AND a.award_recorded
AND e.source_key=a.run_id::text || ':' || a.title_key::text
AND EXISTS (
SELECT 1 FROM revora_event_awards x WHERE x.uid=e.uid AND x.source=e.source
AND x.source_key=a.realm || ':' || a.run_id::text || ':' || a.title_key::text);
IF EXISTS (
SELECT 1 FROM revora_event_awards e JOIN revora_origin_ctf_awards a
ON a.uid=e.uid AND a.title_key=e.title_key AND a.award_recorded
WHERE e.source='cs-ctf-revora-v1' AND e.source_key=a.run_id::text || ':' || a.title_key::text) THEN
RAISE EXCEPTION 'CS-CTF legacy award keys were not migrated.';
END IF;
END $$;");
                    t.Commit();
                }
            }
        }

        internal static void Authenticate(GameState player, string realm, Guid owner)
        {
            if (player == null || player.Entity == null || player.Entity.UID == 0)
                throw new ArgumentException("Origin CS-CTF player is invalid.");
            if (!OriginRealms.Token(realm, 32) || owner == Guid.Empty)
                throw new ArgumentException("Origin CS-CTF delivery is invalid.");
            uint uid = player.Entity.UID;
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    OriginRealms.LockIdentity(c, t, realm, uid);
                    OriginRealms.RequireOwnedSession(c, t, realm, uid, owner);
                    t.Commit();
                }
            }
            var bound = Bound.GetValue(player, _ => new DeliveryAuth());
            bound.Realm = realm;
            bound.Owner = owner;
        }

        internal static bool TryAuthenticated(GameState player, out string realm, out Guid owner)
        {
            DeliveryAuth bound;
            if (player != null && Bound.TryGetValue(player, out bound) && bound != null
                && OriginRealms.Token(bound.Realm, 32) && bound.Owner != Guid.Empty)
            {
                realm = bound.Realm;
                owner = bound.Owner;
                return true;
            }
            realm = null;
            owner = Guid.Empty;
            return false;
        }

        internal static void Clear(GameState player)
        {
            DeliveryAuth bound;
            if (player != null && Bound.TryGetValue(player, out bound) && bound != null)
            {
                bound.Realm = null;
                bound.Owner = Guid.Empty;
            }
        }

        internal static bool Capture(Guid run, Guid coordinator)
        {
            if (run == Guid.Empty) throw new ArgumentException("Origin CTF run is invalid.");
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
                        if (!r.Read()) throw new InvalidOperationException("Origin CTF run is unknown.");
                        eventKey = r.GetString(0);
                        state = r.GetString(1);
                        if (r.IsDBNull(2)) throw new InvalidOperationException("Origin CTF run is not finalized.");
                        earned = r.GetFieldValue<DateTimeOffset>(2).UtcDateTime;
                    }
                    if (eventKey != EventKey) throw new InvalidOperationException("Origin CTF event is invalid.");
                    if (state != "finalized") throw new InvalidOperationException("Origin CTF run is not finalized.");
                    using (var q = Q.Command(c, t, "SELECT count(DISTINCT realm) FROM revora_origin_entrants WHERE run_id=@run", "run", run))
                        if (Convert.ToInt64(q.ExecuteScalar()) < 2) throw new InvalidOperationException("Origin CTF needs distinct origins.");
                    string realm;
                    uint uid;
                    Guid owner;
                    using (var q = Q.Command(c, t, @"SELECT p.realm,p.uid,e.owner
FROM revora_origin_placements p JOIN revora_origin_entrants e
ON e.run_id=p.run_id AND e.realm=p.realm AND e.uid=p.uid
WHERE p.run_id=@run AND p.placement=1", "run", run))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Origin CTF champion is missing.");
                        realm = r.GetString(0);
                        uid = checked((uint)r.GetInt64(1));
                        owner = r.GetGuid(2);
                    }
                    if (owner == Guid.Empty) throw new InvalidOperationException("Origin CTF champion owner is missing.");
                    int added = 0;
                    foreach (int title in new[] { 20030004, 60020021 })
                        added += Q.Execute(c, t, @"INSERT INTO revora_origin_ctf_awards(run_id,realm,uid,owner,title_key,earned_at)
VALUES(@run,@realm,@uid,@owner,@title,@earned) ON CONFLICT DO NOTHING",
                            "run", run, "realm", realm, "uid", (long)uid, "owner", owner, "title", title, "earned", earned);
                    t.Commit();
                    return added == 2;
                }
            }
        }

        internal static int VisibleCount(uint uid, string realm)
        {
            if (!OriginRealms.Token(realm, 32) || uid == 0)
                throw new ArgumentException("Origin CS-CTF delivery is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = Q.Command(c, null, @"SELECT count(*) FROM (
SELECT DISTINCT ON(title_key,source) title_key FROM revora_event_awards
WHERE uid=@uid AND source=@source AND NOT revoked AND expires_at IS NULL
AND source_key LIKE @prefix
ORDER BY title_key,source,earned_at DESC) s",
                    "uid", (long)uid, "source", Source, "prefix", realm + ":%"))
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
                throw new ArgumentException("Origin CS-CTF delivery is invalid.");
            OriginRealms.LockIdentity(c, t, realm, uid);
            OriginRealms.RequireOwnedSession(c, t, realm, uid, owner);
            var pending = new List<Tuple<Guid, string, int, DateTime>>();
            using (var q = Q.Command(c, t, @"SELECT a.run_id,a.realm,a.title_key,a.earned_at
FROM revora_origin_ctf_awards a JOIN revora_origin_runs r ON r.run_id=a.run_id
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
                    throw new InvalidOperationException("CS-CTF award catalog mapping changed.");
                int points = StorageManager.GetTitlePoints((short)(award.Item3 / 10000), (short)(award.Item3 % 10000));
                if (award.Item3 == 20030004 && points != 300) throw new InvalidOperationException("Conqueror catalog points changed.");
                if (award.Item3 == 60020021 && points != 0) throw new InvalidOperationException("Radiant Wings catalog points changed.");
                string key = award.Item2 + ":" + award.Item1.ToString() + ":" + award.Item3.ToString(CultureInfo.InvariantCulture);
                if (!ProgressionEvidence.Grant(c, t, uid, Source, key, award.Item3, award.Item4))
                {
                    using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_event_awards
WHERE uid=@uid AND source=@source AND source_key=@key AND title_key=@title AND earned_at=@earned AND expires_at IS NULL)",
                        "uid", (long)uid, "source", Source, "key", key, "title", award.Item3, "earned", award.Item4))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("CS-CTF award was not saved.");
                }
                if (Q.Execute(c, t, @"UPDATE revora_origin_ctf_awards SET award_recorded=true, owner=@owner
WHERE run_id=@run AND realm=@realm AND uid=@uid AND title_key=@title AND NOT award_recorded",
                    "run", award.Item1, "realm", award.Item2, "uid", (long)uid, "title", award.Item3, "owner", owner) != 1)
                    throw new InvalidOperationException("CS-CTF award receipt was not acknowledged.");
                notices.Add("Your cross-server CTF standing earned a wardrobe award. Find it in the wardrobe.");
            }
            return notices;
        }
    }
}
