using System;
using System.Collections.Generic;
using System.Globalization;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Authenticated multi-origin tournament coordination and recoverable result
    // delivery. Placement outbox is not a wardrobe grant. CS Team PK admission is
    // OriginTeamPk.FormTeam only.
    internal static class OriginTournaments
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;

        internal static void Initialize()
        {
            OriginRealms.Initialize();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var t = c.BeginTransaction())
                {
                    Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_origin_runs(
run_id uuid PRIMARY KEY,event_key varchar(48) NOT NULL,
coordinator uuid NOT NULL CHECK(coordinator<>'00000000-0000-0000-0000-000000000000'),
state varchar(12) NOT NULL CHECK(state IN ('open','started','finalized')),
opened_at timestamptz NOT NULL,started_at timestamptz NULL,finalized_at timestamptz NULL,
CHECK(started_at IS NULL OR started_at>=opened_at),
CHECK(finalized_at IS NULL OR (started_at IS NOT NULL AND finalized_at>=started_at)));
CREATE TABLE IF NOT EXISTS revora_origin_entrants(
run_id uuid NOT NULL REFERENCES revora_origin_runs(run_id),
realm varchar(32) NOT NULL,uid bigint NOT NULL CHECK(uid>0),
owner uuid NOT NULL CHECK(owner<>'00000000-0000-0000-0000-000000000000'),
enrolled_at timestamptz NOT NULL,
PRIMARY KEY(run_id,realm,uid),
FOREIGN KEY(realm,uid) REFERENCES revora_origin_identities(realm,uid));
CREATE TABLE IF NOT EXISTS revora_origin_placements(
run_id uuid NOT NULL REFERENCES revora_origin_runs(run_id),
realm varchar(32) NOT NULL,uid bigint NOT NULL,placement integer NOT NULL CHECK(placement>0),
PRIMARY KEY(run_id,realm,uid),
UNIQUE(run_id,placement),
FOREIGN KEY(run_id,realm,uid) REFERENCES revora_origin_entrants(run_id,realm,uid));
CREATE TABLE IF NOT EXISTS revora_origin_outbox(
delivery_id uuid PRIMARY KEY,run_id uuid NOT NULL,
realm varchar(32) NOT NULL,uid bigint NOT NULL,
owner uuid NOT NULL CHECK(owner<>'00000000-0000-0000-0000-000000000000'),
payload varchar(32) NOT NULL,created_at timestamptz NOT NULL,acked_at timestamptz NULL,
UNIQUE(run_id,realm,uid),
FOREIGN KEY(run_id,realm,uid) REFERENCES revora_origin_placements(run_id,realm,uid));
CREATE TABLE IF NOT EXISTS revora_origin_quarantine_runs(
run_id uuid PRIMARY KEY,event_key varchar(48) NOT NULL,coordinator uuid NULL,
state varchar(12) NOT NULL,opened_at timestamptz NOT NULL,started_at timestamptz NULL,
finalized_at timestamptz NULL,archived_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_origin_quarantine_entrants(
run_id uuid NOT NULL,realm varchar(32) NOT NULL,uid bigint NOT NULL,
owner uuid NULL,enrolled_at timestamptz NOT NULL,PRIMARY KEY(run_id,realm,uid));
CREATE TABLE IF NOT EXISTS revora_origin_quarantine_placements(
run_id uuid NOT NULL,realm varchar(32) NOT NULL,uid bigint NOT NULL,
placement integer NOT NULL,PRIMARY KEY(run_id,realm,uid));
CREATE TABLE IF NOT EXISTS revora_origin_quarantine_outbox(
delivery_id uuid PRIMARY KEY,run_id uuid NOT NULL,realm varchar(32) NOT NULL,
uid bigint NOT NULL,owner uuid NULL,payload varchar(32) NOT NULL,
created_at timestamptz NOT NULL,acked_at timestamptz NULL);
ALTER TABLE revora_origin_runs ADD COLUMN IF NOT EXISTS coordinator uuid;
ALTER TABLE revora_origin_entrants ADD COLUMN IF NOT EXISTS owner uuid;
ALTER TABLE revora_origin_outbox ADD COLUMN IF NOT EXISTS owner uuid;");
                    DateTime now = Clock();
                    Q.Execute(c, t, @"INSERT INTO revora_origin_quarantine_runs(run_id,event_key,coordinator,state,opened_at,started_at,finalized_at,archived_at)
SELECT r.run_id,r.event_key,r.coordinator,r.state,r.opened_at,r.started_at,r.finalized_at,@now
FROM revora_origin_runs r WHERE r.run_id IN (
SELECT run_id FROM revora_origin_runs WHERE coordinator IS NULL OR coordinator='00000000-0000-0000-0000-000000000000'
UNION SELECT run_id FROM revora_origin_entrants WHERE owner IS NULL OR owner='00000000-0000-0000-0000-000000000000'
UNION SELECT run_id FROM revora_origin_outbox WHERE owner IS NULL OR owner='00000000-0000-0000-0000-000000000000')
ON CONFLICT (run_id) DO NOTHING", "now", now);
                    Q.Execute(c, t, @"INSERT INTO revora_origin_quarantine_entrants(run_id,realm,uid,owner,enrolled_at)
SELECT e.run_id,e.realm,e.uid,e.owner,e.enrolled_at FROM revora_origin_entrants e
WHERE e.run_id IN (SELECT run_id FROM revora_origin_quarantine_runs)
ON CONFLICT (run_id,realm,uid) DO NOTHING;
INSERT INTO revora_origin_quarantine_placements(run_id,realm,uid,placement)
SELECT p.run_id,p.realm,p.uid,p.placement FROM revora_origin_placements p
WHERE p.run_id IN (SELECT run_id FROM revora_origin_quarantine_runs)
ON CONFLICT (run_id,realm,uid) DO NOTHING;
INSERT INTO revora_origin_quarantine_outbox(delivery_id,run_id,realm,uid,owner,payload,created_at,acked_at)
SELECT o.delivery_id,o.run_id,o.realm,o.uid,o.owner,o.payload,o.created_at,o.acked_at FROM revora_origin_outbox o
WHERE o.run_id IN (SELECT run_id FROM revora_origin_quarantine_runs)
ON CONFLICT (delivery_id) DO NOTHING;");
                    Q.Execute(c, t, @"DELETE FROM revora_origin_outbox WHERE run_id IN (SELECT run_id FROM revora_origin_quarantine_runs);
DELETE FROM revora_origin_placements WHERE run_id IN (SELECT run_id FROM revora_origin_quarantine_runs);
DELETE FROM revora_origin_entrants WHERE run_id IN (SELECT run_id FROM revora_origin_quarantine_runs);
DELETE FROM revora_origin_runs WHERE run_id IN (SELECT run_id FROM revora_origin_quarantine_runs);");
                    Q.Execute(c, t, @"ALTER TABLE revora_origin_runs ALTER COLUMN coordinator SET NOT NULL;
ALTER TABLE revora_origin_entrants ALTER COLUMN owner SET NOT NULL;
ALTER TABLE revora_origin_outbox ALTER COLUMN owner SET NOT NULL;");
                    Q.Execute(c, t, @"DO $$ BEGIN
IF NOT EXISTS (
SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname=current_schema() AND t.relname='revora_origin_runs' AND c.conname='revora_origin_runs_coordinator_nonempty') THEN
ALTER TABLE revora_origin_runs ADD CONSTRAINT revora_origin_runs_coordinator_nonempty CHECK (coordinator<>'00000000-0000-0000-0000-000000000000');
END IF;
IF NOT EXISTS (
SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname=current_schema() AND t.relname='revora_origin_entrants' AND c.conname='revora_origin_entrants_owner_nonempty') THEN
ALTER TABLE revora_origin_entrants ADD CONSTRAINT revora_origin_entrants_owner_nonempty CHECK (owner<>'00000000-0000-0000-0000-000000000000');
END IF;
IF NOT EXISTS (
SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname=current_schema() AND t.relname='revora_origin_outbox' AND c.conname='revora_origin_outbox_owner_nonempty') THEN
ALTER TABLE revora_origin_outbox ADD CONSTRAINT revora_origin_outbox_owner_nonempty CHECK (owner<>'00000000-0000-0000-0000-000000000000');
END IF;
END $$;");
                    using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_origin_runs WHERE coordinator IS NULL OR coordinator='00000000-0000-0000-0000-000000000000')
OR EXISTS(SELECT 1 FROM revora_origin_entrants WHERE owner IS NULL OR owner='00000000-0000-0000-0000-000000000000')
OR EXISTS(SELECT 1 FROM revora_origin_outbox WHERE owner IS NULL OR owner='00000000-0000-0000-0000-000000000000')"))
                        if (object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Origin tournament ownership migration left unowned rows.");
                    t.Commit();
                }
            }
        }

        internal static void LockRun(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, Guid run)
        {
            Q.Execute(c, t, "SELECT pg_advisory_xact_lock(6719, hashtext(@id))", "id", "run:" + run.ToString());
        }

        private static string StateOf(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, Guid run)
        {
            using (var q = Q.Command(c, t, "SELECT state FROM revora_origin_runs WHERE run_id=@run FOR UPDATE", "run", run))
            {
                object state = q.ExecuteScalar();
                if (state == null) throw new InvalidOperationException("Origin tournament is unknown.");
                return (string)state;
            }
        }

        internal static void RequireCoordinator(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, Guid run, Guid coordinator)
        {
            if (coordinator == Guid.Empty) throw new ArgumentException("Origin tournament coordinator is invalid.");
            using (var q = Q.Command(c, t, "SELECT coordinator FROM revora_origin_runs WHERE run_id=@run", "run", run))
            {
                object value = q.ExecuteScalar();
                if (value == null) throw new InvalidOperationException("Origin tournament is unknown.");
                if ((Guid)value != coordinator) throw new InvalidOperationException("Origin tournament is not owned.");
            }
        }

        internal static Guid OpenRun(string eventKey, Guid coordinator)
        {
            if (!OriginRealms.Token(eventKey, 48)) throw new ArgumentException("Origin tournament event is invalid.");
            if (coordinator == Guid.Empty) throw new ArgumentException("Origin tournament coordinator is invalid.");
            Guid run = Guid.NewGuid();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    if (Q.Execute(c, t, @"INSERT INTO revora_origin_runs(run_id,event_key,coordinator,state,opened_at)
VALUES(@run,@event,@coordinator,'open',@now)",
                        "run", run, "event", eventKey, "coordinator", coordinator, "now", Clock()) != 1)
                        throw new InvalidOperationException("Origin tournament was not opened.");
                    t.Commit();
                    return run;
                }
            }
        }

        internal static bool Enroll(Guid run, string realm, uint uid, Guid owner)
        {
            if (run == Guid.Empty) throw new ArgumentException("Origin tournament is invalid.");
            if (!OriginRealms.Token(realm, 32) || uid == 0 || owner == Guid.Empty)
                throw new ArgumentException("Origin identity is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    LockRun(c, t, run);
                    OriginRealms.LockIdentity(c, t, realm, uid);
                    using (var q = Q.Command(c, t, "SELECT event_key FROM revora_origin_runs WHERE run_id=@run", "run", run))
                    {
                        object eventKey = q.ExecuteScalar();
                        if (eventKey == null) throw new InvalidOperationException("Origin tournament is unknown.");
                        if ((string)eventKey == OriginTeamPk.EventKey)
                            throw new InvalidOperationException("Origin Team PK admission uses FormTeam.");
                    }
                    if (StateOf(c, t, run) != "open") throw new InvalidOperationException("Origin tournament roster is frozen.");
                    OriginRealms.RequireOwnedSession(c, t, realm, uid, owner);
                    int added = Q.Execute(c, t, @"INSERT INTO revora_origin_entrants(run_id,realm,uid,owner,enrolled_at)
VALUES(@run,@realm,@uid,@owner,@now) ON CONFLICT(run_id,realm,uid) DO NOTHING",
                        "run", run, "realm", realm, "uid", (long)uid, "owner", owner, "now", Clock());
                    t.Commit();
                    return added == 1;
                }
            }
        }

        internal static bool Start(Guid run, Guid coordinator)
        {
            if (run == Guid.Empty) throw new ArgumentException("Origin tournament is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    LockRun(c, t, run);
                    RequireCoordinator(c, t, run, coordinator);
                    string state = StateOf(c, t, run);
                    if (state == "started" || state == "finalized") { t.Commit(); return false; }
                    if (state != "open") throw new InvalidOperationException("Origin tournament cannot start.");
                    using (var q = Q.Command(c, t, "SELECT count(DISTINCT realm),count(*) FROM revora_origin_entrants WHERE run_id=@run", "run", run))
                    using (var r = q.ExecuteReader())
                    {
                        r.Read();
                        if (r.GetInt64(0) < 2 || r.GetInt64(1) < 2)
                            throw new InvalidOperationException("Origin tournament needs distinct origins.");
                    }
                    if (Q.Execute(c, t, "UPDATE revora_origin_runs SET state='started',started_at=@now WHERE run_id=@run AND state='open'",
                        "now", Clock(), "run", run) != 1)
                        throw new InvalidOperationException("Origin tournament did not start.");
                    t.Commit();
                    return true;
                }
            }
        }

        internal static bool Finalize(Guid run, string[] rows, Guid coordinator)
        {
            if (run == Guid.Empty) throw new ArgumentException("Origin tournament is invalid.");
            if (rows == null || rows.Length == 0) throw new ArgumentException("Origin tournament placements are invalid.");
            var seen = new Dictionary<string, int>();
            var places = new Dictionary<int, bool>();
            for (int i = 0; i < rows.Length; i++)
            {
                string row = rows[i];
                if (string.IsNullOrEmpty(row)) throw new ArgumentException("Origin tournament placements are invalid.");
                string[] parts = row.Split(':');
                if (parts.Length != 3) throw new ArgumentException("Origin tournament placements are invalid.");
                uint uid;
                int place;
                if (!OriginRealms.Token(parts[0], 32) || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uid) || uid == 0
                    || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out place) || place < 1 || place > rows.Length)
                    throw new ArgumentException("Origin tournament placements are invalid.");
                string key = parts[0] + ":" + uid.ToString(CultureInfo.InvariantCulture);
                if (seen.ContainsKey(key) || places.ContainsKey(place)) throw new ArgumentException("Origin tournament placements are invalid.");
                seen[key] = place;
                places[place] = true;
            }
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    LockRun(c, t, run);
                    RequireCoordinator(c, t, run, coordinator);
                    string state = StateOf(c, t, run);
                    if (state == "finalized")
                    {
                        // Only the identical saved result is an idempotent retry.
                        // A different winner or roster must not appear accepted
                        // to a recovering competition coordinator.
                        int matched = 0;
                        using (var q = Q.Command(c, t,
                            "SELECT realm,uid,placement FROM revora_origin_placements WHERE run_id=@run", "run", run))
                        using (var r = q.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                string key = r.GetString(0) + ":" + r.GetInt64(1).ToString(CultureInfo.InvariantCulture);
                                int requested;
                                if (!seen.TryGetValue(key, out requested) || requested != r.GetInt32(2))
                                    throw new InvalidOperationException("Origin tournament result conflicts with the saved result.");
                                matched++;
                            }
                        }
                        if (matched != seen.Count)
                            throw new InvalidOperationException("Origin tournament result conflicts with the saved result.");
                        t.Commit();
                        return false;
                    }
                    if (state != "started") throw new InvalidOperationException("Origin tournament is not started.");
                    int enrolled;
                    using (var q = Q.Command(c, t, "SELECT count(*) FROM revora_origin_entrants WHERE run_id=@run", "run", run))
                        enrolled = Convert.ToInt32(q.ExecuteScalar());
                    if (enrolled != rows.Length) throw new InvalidOperationException("Origin tournament placements must cover the frozen roster.");
                    foreach (var pair in seen)
                    {
                        string[] id = pair.Key.Split(':');
                        using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_origin_entrants WHERE run_id=@run AND realm=@realm AND uid=@uid)",
                            "run", run, "realm", id[0], "uid", long.Parse(id[1], CultureInfo.InvariantCulture)))
                            if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Origin tournament placement is not enrolled.");
                    }
                    if (Q.Execute(c, t, "UPDATE revora_origin_runs SET state='finalized',finalized_at=@now WHERE run_id=@run AND state='started'",
                        "now", Clock(), "run", run) != 1)
                        throw new InvalidOperationException("Origin tournament did not finalize.");
                    foreach (var pair in seen)
                    {
                        string[] id = pair.Key.Split(':');
                        if (Q.Execute(c, t, @"INSERT INTO revora_origin_placements(run_id,realm,uid,placement)
VALUES(@run,@realm,@uid,@place)",
                            "run", run, "realm", id[0], "uid", long.Parse(id[1], CultureInfo.InvariantCulture), "place", pair.Value) != 1)
                            throw new InvalidOperationException("Origin tournament placement was not saved.");
                        if (Q.Execute(c, t, @"INSERT INTO revora_origin_outbox(delivery_id,run_id,realm,uid,owner,payload,created_at)
SELECT @delivery,@run,@realm,@uid,e.owner,@payload,@now
FROM revora_origin_entrants e WHERE e.run_id=@run AND e.realm=@realm AND e.uid=@uid",
                            "delivery", Guid.NewGuid(), "run", run, "realm", id[0], "uid", long.Parse(id[1], CultureInfo.InvariantCulture),
                            "payload", "placement:" + pair.Value.ToString(CultureInfo.InvariantCulture), "now", Clock()) != 1)
                            throw new InvalidOperationException("Origin tournament outbox was not saved.");
                    }
                    t.Commit();
                    return true;
                }
            }
        }

        internal static int PendingDeliveries(Guid run)
        {
            if (run == Guid.Empty) throw new ArgumentException("Origin tournament is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = Q.Command(c, null, "SELECT count(*) FROM revora_origin_outbox WHERE run_id=@run AND acked_at IS NULL", "run", run))
                    return Convert.ToInt32(q.ExecuteScalar());
            }
        }

        internal static Guid NextDelivery(Guid run)
        {
            if (run == Guid.Empty) throw new ArgumentException("Origin tournament is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = Q.Command(c, null, @"SELECT delivery_id FROM revora_origin_outbox
WHERE run_id=@run AND acked_at IS NULL ORDER BY realm,uid LIMIT 1", "run", run))
                {
                    object id = q.ExecuteScalar();
                    if (id == null) throw new InvalidOperationException("Origin tournament outbox is empty.");
                    return (Guid)id;
                }
            }
        }

        internal static bool Ack(Guid delivery, Guid owner)
        {
            if (delivery == Guid.Empty) throw new ArgumentException("Origin tournament delivery is invalid.");
            if (owner == Guid.Empty) throw new ArgumentException("Origin tournament delivery owner is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    Guid run;
                    string realm;
                    uint uid;
                    using (var q = Q.Command(c, t, "SELECT run_id,realm,uid FROM revora_origin_outbox WHERE delivery_id=@delivery", "delivery", delivery))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Origin tournament delivery is unknown.");
                        run = r.GetGuid(0);
                        realm = r.GetString(1);
                        uid = checked((uint)r.GetInt64(2));
                    }
                    LockRun(c, t, run);
                    OriginRealms.LockIdentity(c, t, realm, uid);
                    // Enrollment's process may have stopped. Authorize the
                    // current session for this exact origin-qualified identity.
                    OriginRealms.RequireOwnedSession(c, t, realm, uid, owner);
                    int updated = Q.Execute(c, t, "UPDATE revora_origin_outbox SET acked_at=@now,owner=@owner WHERE delivery_id=@delivery AND acked_at IS NULL",
                        "now", Clock(), "delivery", delivery, "owner", owner);
                    if (updated != 1)
                    {
                        using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_origin_outbox WHERE delivery_id=@delivery AND acked_at IS NOT NULL)", "delivery", delivery))
                            if (!object.Equals(q.ExecuteScalar(), true))
                                throw new InvalidOperationException("Origin tournament acknowledgement was not saved.");
                    }
                    t.Commit();
                    return updated == 1;
                }
            }
        }
    }
}
