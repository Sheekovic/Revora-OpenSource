using System;
using System.Globalization;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Local origin-realm foundation: distinct (realm, uid) identities, exclusive
    // sessions, authenticated admission and idempotent receipts. This is not a
    // cross-server win and does not grant Conqueror, Radiant Wings or PK titles.
    internal static class OriginRealms
    {
        internal const string Source = "origin-realms-v1";
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;

        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_origin_realms(
realm varchar(32) PRIMARY KEY,created_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_origin_identities(
realm varchar(32) NOT NULL REFERENCES revora_origin_realms(realm),
uid bigint NOT NULL CHECK(uid>0),name varchar(16) NOT NULL,
PRIMARY KEY(realm,uid));
CREATE TABLE IF NOT EXISTS revora_origin_processes(
realm varchar(32) PRIMARY KEY REFERENCES revora_origin_realms(realm),
owner uuid NOT NULL CHECK(owner<>'00000000-0000-0000-0000-000000000000'));
CREATE TABLE IF NOT EXISTS revora_origin_sessions(
session_id uuid PRIMARY KEY,realm varchar(32) NOT NULL,uid bigint NOT NULL,
owner uuid NOT NULL CHECK(owner<>'00000000-0000-0000-0000-000000000000'),
opened_at timestamptz NOT NULL,closed_at timestamptz NULL,
CHECK(closed_at IS NULL OR closed_at>=opened_at),
FOREIGN KEY(realm,uid) REFERENCES revora_origin_identities(realm,uid));
CREATE UNIQUE INDEX IF NOT EXISTS revora_origin_sessions_open
ON revora_origin_sessions(realm,uid) WHERE closed_at IS NULL;
CREATE TABLE IF NOT EXISTS revora_origin_admissions(
event_key varchar(48) NOT NULL,realm varchar(32) NOT NULL,uid bigint NOT NULL,
owner uuid NOT NULL,admitted_at timestamptz NOT NULL,
PRIMARY KEY(event_key,realm,uid),
FOREIGN KEY(realm,uid) REFERENCES revora_origin_identities(realm,uid));
CREATE TABLE IF NOT EXISTS revora_origin_receipts(
event_key varchar(48) NOT NULL,realm varchar(32) NOT NULL,uid bigint NOT NULL,
awarded_at timestamptz NOT NULL,
PRIMARY KEY(event_key,realm,uid),
FOREIGN KEY(event_key,realm,uid) REFERENCES revora_origin_admissions(event_key,realm,uid));");
            }
        }

        internal static bool Token(string value, int limit)
        {
            if (string.IsNullOrEmpty(value) || value.Length > limit) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-')) return false;
            return true;
        }

        private static void Validate(string realm, uint uid)
        {
            if (!Token(realm, 32) || uid == 0) throw new ArgumentException("Origin identity is invalid.");
        }

        internal static void LockIdentity(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, string realm, uint uid)
        {
            Q.Execute(c, t, "SELECT pg_advisory_xact_lock(6719, hashtext(@id))",
                "id", "origin:" + realm + ":" + uid.ToString(CultureInfo.InvariantCulture));
        }

        internal static void RequireOwnedSession(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, string realm, uint uid, Guid owner)
        {
            RequireRealmOwner(c, t, realm, owner);
            using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_origin_sessions
WHERE realm=@realm AND uid=@uid AND owner=@owner AND closed_at IS NULL)",
                "realm", realm, "uid", (long)uid, "owner", owner))
                if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Origin session is not owned.");
        }

        // Shared transition lock also covers the first registration, when no
        // process row exists yet. Recovery takes the exclusive counterpart.
        internal static void RequireRealmOwner(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, string realm, Guid owner)
        {
            Q.Execute(c, t, "SELECT pg_advisory_xact_lock_shared(6721, hashtext(@realm))", "realm", realm);
            using (var q = Q.Command(c, t, "SELECT owner FROM revora_origin_processes WHERE realm=@realm", "realm", realm))
            {
                object registered = q.ExecuteScalar();
                if (registered != null && !object.Equals(registered, owner))
                    throw new InvalidOperationException("Origin process no longer owns this realm.");
            }
        }

        internal static void RegisterRealm(string realm)
        {
            if (!Token(realm, 32)) throw new ArgumentException("Origin realm is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    Q.Execute(c, t, "INSERT INTO revora_origin_realms(realm,created_at) VALUES(@realm,@now) ON CONFLICT(realm) DO NOTHING",
                        "realm", realm, "now", Clock());
                    using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_origin_realms WHERE realm=@realm)", "realm", realm))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Origin realm was not registered.");
                    t.Commit();
                }
            }
        }

        internal static void RegisterIdentity(string realm, uint uid, string name)
        {
            Validate(realm, uid);
            if (string.IsNullOrEmpty(name) || name.Length > 16) throw new ArgumentException("Origin identity name is invalid.");
            foreach (char ch in name)
                if (char.IsControl(ch)) throw new ArgumentException("Origin identity name is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    LockIdentity(c, t, realm, uid);
                    using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_origin_realms WHERE realm=@realm)", "realm", realm))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Origin realm is unknown.");
                    Q.Execute(c, t, @"INSERT INTO revora_origin_identities(realm,uid,name) VALUES(@realm,@uid,@name)
ON CONFLICT(realm,uid) DO UPDATE SET name=EXCLUDED.name
WHERE revora_origin_identities.name IS DISTINCT FROM EXCLUDED.name",
                        "realm", realm, "uid", (long)uid, "name", name);
                    using (var q = Q.Command(c, t, "SELECT name FROM revora_origin_identities WHERE realm=@realm AND uid=@uid",
                        "realm", realm, "uid", (long)uid))
                        if (!object.Equals(q.ExecuteScalar(), name))
                            throw new InvalidOperationException("Origin identity was not registered.");
                    t.Commit();
                }
            }
        }

        internal static Guid OpenSession(string realm, uint uid, Guid owner)
        {
            Validate(realm, uid);
            if (owner == Guid.Empty) throw new ArgumentException("Origin session owner is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    LockIdentity(c, t, realm, uid);
                    RequireRealmOwner(c, t, realm, owner);
                    using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_origin_identities WHERE realm=@realm AND uid=@uid)",
                        "realm", realm, "uid", (long)uid))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Origin identity is not registered.");
                    using (var q = Q.Command(c, t, "SELECT session_id FROM revora_origin_sessions WHERE realm=@realm AND uid=@uid AND closed_at IS NULL FOR UPDATE",
                        "realm", realm, "uid", (long)uid))
                        if (q.ExecuteScalar() != null) throw new InvalidOperationException("Origin session is already owned.");
                    Guid session = Guid.NewGuid();
                    if (Q.Execute(c, t, @"INSERT INTO revora_origin_sessions(session_id,realm,uid,owner,opened_at)
VALUES(@session,@realm,@uid,@owner,@now)",
                        "session", session, "realm", realm, "uid", (long)uid, "owner", owner, "now", Clock()) != 1)
                        throw new InvalidOperationException("Origin session was not opened.");
                    t.Commit();
                    return session;
                }
            }
        }

        internal static Guid Reconnect(string realm, uint uid, Guid owner)
        {
            Validate(realm, uid);
            if (owner == Guid.Empty) throw new ArgumentException("Origin session owner is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    LockIdentity(c, t, realm, uid);
                    RequireRealmOwner(c, t, realm, owner);
                    Guid session;
                    using (var q = Q.Command(c, t, "SELECT session_id,owner FROM revora_origin_sessions WHERE realm=@realm AND uid=@uid AND closed_at IS NULL FOR UPDATE",
                        "realm", realm, "uid", (long)uid))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Origin session is not open.");
                        if ((Guid)r.GetValue(1) != owner) throw new InvalidOperationException("Origin session is already owned.");
                        session = r.GetGuid(0);
                    }
                    t.Commit();
                    return session;
                }
            }
        }

        internal static void CloseSession(string realm, uint uid, Guid owner, Guid session)
        {
            Validate(realm, uid);
            if (owner == Guid.Empty || session == Guid.Empty) throw new ArgumentException("Origin session is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    LockIdentity(c, t, realm, uid);
                    RequireRealmOwner(c, t, realm, owner);
                    if (Q.Execute(c, t, @"UPDATE revora_origin_sessions SET closed_at=@now
WHERE session_id=@session AND realm=@realm AND uid=@uid AND owner=@owner AND closed_at IS NULL",
                        "now", Clock(), "session", session, "realm", realm, "uid", (long)uid, "owner", owner) != 1)
                        throw new InvalidOperationException("Origin session was not closed.");
                    t.Commit();
                }
            }
        }

        internal static bool Admit(string eventKey, string realm, uint uid, Guid owner)
        {
            if (!Token(eventKey, 48)) throw new ArgumentException("Origin event is invalid.");
            Validate(realm, uid);
            if (owner == Guid.Empty) throw new ArgumentException("Origin session owner is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    LockIdentity(c, t, realm, uid);
                    RequireOwnedSession(c, t, realm, uid, owner);
                    int added = Q.Execute(c, t, @"INSERT INTO revora_origin_admissions(event_key,realm,uid,owner,admitted_at)
VALUES(@event,@realm,@uid,@owner,@now) ON CONFLICT(event_key,realm,uid) DO NOTHING",
                        "event", eventKey, "realm", realm, "uid", (long)uid, "owner", owner, "now", Clock());
                    t.Commit();
                    return added == 1;
                }
            }
        }

        internal static bool RecordReceipt(string eventKey, string realm, uint uid)
        {
            if (!Token(eventKey, 48)) throw new ArgumentException("Origin event is invalid.");
            Validate(realm, uid);
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    LockIdentity(c, t, realm, uid);
                    using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_origin_admissions
WHERE event_key=@event AND realm=@realm AND uid=@uid)",
                        "event", eventKey, "realm", realm, "uid", (long)uid))
                        if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Origin admission is missing.");
                    int added = Q.Execute(c, t, @"INSERT INTO revora_origin_receipts(event_key,realm,uid,awarded_at)
VALUES(@event,@realm,@uid,@now) ON CONFLICT(event_key,realm,uid) DO NOTHING",
                        "event", eventKey, "realm", realm, "uid", (long)uid, "now", Clock());
                    if (added != 1)
                    {
                        using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_origin_receipts
WHERE event_key=@event AND realm=@realm AND uid=@uid)",
                            "event", eventKey, "realm", realm, "uid", (long)uid))
                            if (!object.Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Origin receipt was not saved.");
                    }
                    t.Commit();
                    return added == 1;
                }
            }
        }
    }
}
