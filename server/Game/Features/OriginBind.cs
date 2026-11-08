using System;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Production bind of a logged-in character to this process's origin realm.
    // No extra public socket. Fail-closed when the realm is unset or a foreign
    // process owns the live session. CS-CTF, CS Elite PK and CS Team PK
    // wardrobe consumption use this bind.
    internal static class OriginBind
    {
        internal static string Realm;
        internal static Guid Owner = Guid.NewGuid();
        private sealed class Live { internal Guid Session; internal uint Uid; }
        private static readonly ConditionalWeakTable<GameState, Live> Bound = new ConditionalWeakTable<GameState, Live>();
        private static readonly object Gate = new object();
        private static Npgsql.NpgsqlConnection lease;
        private static Guid leaseOwner;

        internal static void Configure(string realm)
        {
            lock (Gate) ConfigureCore(realm);
        }

        private static void ConfigureCore(string realm)
        {
            if (realm == Realm && lease != null && leaseOwner == Owner)
            {
                Recover();
                return;
            }
            // Never return a session advisory lock to the application pool.
            Realm = null;
            if (lease != null) { lease.Dispose(); lease = null; }
            if (!OriginRealms.Token(realm, 32) || Owner == Guid.Empty)
            {
                return;
            }
            OriginRealms.Initialize();
            OriginCtf.Initialize();
            OriginElitePk.Initialize();
            OriginTeamPk.Initialize();
            OriginRealms.RegisterRealm(realm);
            var connection = new Npgsql.NpgsqlConnectionStringBuilder(DataHolder.ConnectionString) { Pooling = false };
            var candidate = new Npgsql.NpgsqlConnection(connection.ConnectionString);
            try
            {
                candidate.Open();
                using (var q = Q.Command(candidate, null, "SELECT pg_try_advisory_lock(6720, hashtext(@realm))", "realm", realm))
                    if (!object.Equals(q.ExecuteScalar(), true))
                        throw new InvalidOperationException("Another process owns this origin realm.");
                lease = candidate;
                leaseOwner = Owner;
                Realm = realm;
                Recover();
            }
            catch
            {
                Realm = null;
                lease = null;
                candidate.Dispose();
                throw;
            }
        }

        internal static void Recover()
        {
            lock (Gate) RecoverCore();
        }

        private static void RecoverCore()
        {
            if (!OriginRealms.Token(Realm, 32) || Owner == Guid.Empty) return;
            if (lease == null || leaseOwner != Owner)
                throw new InvalidOperationException("Origin recovery has no owned realm lock.");
            // Recovery and its lock use the same physical connection. A broken
            // connection cannot close another process's sessions through a pool.
            var c = lease;
            using (var t = c.BeginTransaction())
            {
                    Q.Execute(c, t, "SELECT pg_advisory_xact_lock(6721, hashtext(@realm))", "realm", Realm);
                    Q.Execute(c, t, @"INSERT INTO revora_origin_processes(realm,owner) VALUES(@realm,@owner)
ON CONFLICT(realm) DO UPDATE SET owner=EXCLUDED.owner", "realm", Realm, "owner", Owner);
                    Q.Execute(c, t, @"UPDATE revora_origin_sessions SET closed_at=@now
WHERE realm=@realm AND closed_at IS NULL AND owner IS DISTINCT FROM @owner",
                        "now", OriginRealms.Clock(), "realm", Realm, "owner", Owner);
                    using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_origin_sessions
WHERE realm=@realm AND closed_at IS NULL AND owner IS DISTINCT FROM @owner)",
                        "realm", Realm, "owner", Owner))
                        if (object.Equals(q.ExecuteScalar(), true))
                            throw new InvalidOperationException("Origin sessions were not recovered.");
                    t.Commit();
            }
        }

        internal static void Login(GameState player)
        {
            if (player == null || player.Fake || player.Entity == null || player.Entity.UID == 0) return;
            if (!OriginRealms.Token(Realm, 32) || Owner == Guid.Empty) return;
            string name = player.Entity.Name;
            if (string.IsNullOrEmpty(name)) return;
            if (name.Length > 16) name = name.Substring(0, 16);
            uint uid = player.Entity.UID;
            try { OriginRealms.RegisterIdentity(Realm, uid, name); }
            catch (ArgumentException) { return; }
            catch (InvalidOperationException) { return; }
            Guid session;
            try { session = OriginRealms.Reconnect(Realm, uid, Owner); }
            catch (InvalidOperationException)
            {
                try { session = OriginRealms.OpenSession(Realm, uid, Owner); }
                catch (InvalidOperationException) { return; }
            }
            OriginCtf.Authenticate(player, Realm, Owner);
            var live = Bound.GetValue(player, _ => new Live());
            live.Session = session;
            live.Uid = uid;
        }

        internal static void Logout(GameState player)
        {
            if (player == null) return;
            OriginCtf.Clear(player);
            Live live;
            if (!Bound.TryGetValue(player, out live) || live == null || live.Session == Guid.Empty) return;
            uint uid = live.Uid;
            Guid session = live.Session;
            live.Session = Guid.Empty;
            GameState current;
            if (Revora.Kernel.GamePool != null && Revora.Kernel.GamePool.TryGetValue(uid, out current)
                && !object.ReferenceEquals(current, player))
                return;
            if (!OriginRealms.Token(Realm, 32) || Owner == Guid.Empty) return;
            try { OriginRealms.CloseSession(Realm, uid, Owner, session); }
            catch (InvalidOperationException) { }
        }
    }
}
