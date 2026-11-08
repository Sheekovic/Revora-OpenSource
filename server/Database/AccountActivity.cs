using System;
using System.Data;

namespace Revora.Database
{
    // Authentication, session observations and previous-process session recovery.
    // Title eligibility still comes from saved return receipts, not this table alone.
    // Serving and recovery run only on the connection that holds the database advisory lease.
    internal static class AccountActivity
    {
        private static readonly object LeaseGate = new object();
        private static Npgsql.NpgsqlConnection lease;
        private static bool fenced;
        // Production homecoming identity. Tests may select another object id so
        // they do not take or release the live process lock.
        internal static int LeaseClassId = 6711;
        internal static int LeaseObjectId = 1;
        internal static Action<Npgsql.NpgsqlConnection> AfterLeaseHeld;
        internal sealed class LeaseLostException : InvalidOperationException
        {
            internal LeaseLostException(string message) : base(message) { }
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Game.Features.QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_account_activity(
account_name text PRIMARY KEY, first_auth timestamptz NOT NULL,last_auth timestamptz NOT NULL,
auth_count bigint NOT NULL CHECK(auth_count>0));
CREATE TABLE IF NOT EXISTS revora_account_sessions(
 session_id uuid PRIMARY KEY,account_name text NOT NULL REFERENCES revora_account_activity(account_name),
 character_uid bigint NOT NULL,opened_at timestamptz NOT NULL,closed_at timestamptz NULL,
 CHECK(closed_at IS NULL OR closed_at>=opened_at));
CREATE INDEX IF NOT EXISTS revora_account_sessions_open ON revora_account_sessions(account_name) WHERE closed_at IS NULL;
CREATE INDEX IF NOT EXISTS revora_account_sessions_history ON revora_account_sessions(account_name,closed_at);
ALTER TABLE revora_account_sessions ADD COLUMN IF NOT EXISTS owner uuid;
CREATE TABLE IF NOT EXISTS revora_account_returns(
 account_name text NOT NULL REFERENCES revora_account_activity(account_name),
 auth_number bigint NOT NULL,character_uid bigint NOT NULL,
 inactive_since timestamptz NOT NULL,returned_at timestamptz NOT NULL,
 PRIMARY KEY(account_name,auth_number),CHECK(returned_at-inactive_since>=interval '30 days'));
ALTER TABLE revora_account_returns ADD COLUMN IF NOT EXISTS character_snapshot jsonb;
ALTER TABLE revora_account_returns ADD COLUMN IF NOT EXISTS expires_at timestamptz;
ALTER TABLE revora_account_returns ADD COLUMN IF NOT EXISTS title_key integer;
ALTER TABLE revora_account_returns ADD COLUMN IF NOT EXISTS award_recorded boolean NOT NULL DEFAULT false;
CREATE INDEX IF NOT EXISTS revora_account_returns_pending ON revora_account_returns(character_uid) WHERE NOT award_recorded AND title_key>0;");
            }
            lock (LeaseGate)
            {
                AcquireLocked();
                PauseForTest();
                ProtectLease(() => Recover(lease));
            }
        }

        // Session advisory lock on an unpooled connection. Intentional Relinquish
        // may acquire again. A dropped backend fences this process until exit.
        internal static void Relinquish()
        {
            lock (LeaseGate)
            {
                if (fenced || lease == null)
                {
                    CloseLeaseLocked();
                    return;
                }
                try { HoldLocked(); }
                catch (LeaseLostException) { return; }
                CloseLeaseLocked();
            }
        }

        internal static int BackendPid()
        {
            lock (LeaseGate)
            {
                HoldLocked();
                int pid = 0;
                ProtectLease(() =>
                {
                    using (var q = new Npgsql.NpgsqlCommand("SELECT pg_backend_pid()", lease))
                        pid = Convert.ToInt32(q.ExecuteScalar());
                });
                return pid;
            }
        }

        private static void CloseLeaseLocked()
        {
            if (lease == null) return;
            try { lease.Close(); }
            catch { }
            finally { lease.Dispose(); lease = null; }
        }

        private static void FenceLocked()
        {
            fenced = true;
            CloseLeaseLocked();
        }

        internal static bool IsFenced()
        {
            lock (LeaseGate) return fenced;
        }

        private static void PauseForTest()
        {
            var hook = AfterLeaseHeld;
            if (hook != null) hook(lease);
        }

        private static Npgsql.NpgsqlCommand LeaseHeldCommand()
        {
            var command = new Npgsql.NpgsqlCommand(
                "SELECT EXISTS(SELECT 1 FROM pg_catalog.pg_locks WHERE locktype='advisory'"
                + " AND classid=CAST(@class AS oid) AND objid=CAST(@object AS oid)"
                + " AND objsubid=2 AND granted AND pid=pg_backend_pid())", lease);
            command.Parameters.AddWithValue("class", LeaseClassId);
            command.Parameters.AddWithValue("object", LeaseObjectId);
            return command;
        }

        private static bool LeaseStillHeld()
        {
            if (fenced || lease == null || lease.State != ConnectionState.Open) return false;
            try
            {
                using (var q = LeaseHeldCommand())
                    return Equals(q.ExecuteScalar(), true);
            }
            catch { return false; }
        }

        private static void ProtectLease(Action work)
        {
            try { work(); }
            catch (LeaseLostException) { throw; }
            catch (Exception error)
            {
                if (error is InvalidOperationException && LeaseStillHeld()) throw;
                FenceLocked();
                throw new LeaseLostException("Homecoming process lease was lost.");
            }
        }

        private static void HoldLocked()
        {
            if (fenced) throw new LeaseLostException("This process is fenced after Homecoming lease loss.");
            if (lease == null) throw new LeaseLostException("Homecoming process lease is not held.");
            if (lease.State != ConnectionState.Open)
            {
                FenceLocked();
                throw new LeaseLostException("Homecoming process lease was lost.");
            }
            try
            {
                using (var q = LeaseHeldCommand())
                {
                    object held = q.ExecuteScalar();
                    if (!Equals(held, true))
                    {
                        FenceLocked();
                        throw new LeaseLostException("Homecoming process lease was lost.");
                    }
                }
            }
            catch (LeaseLostException) { throw; }
            catch (Exception error)
            {
                if (error is InvalidOperationException && LeaseStillHeld()) throw;
                FenceLocked();
                throw new LeaseLostException("Homecoming process lease was lost.");
            }
        }

        private static void AcquireLocked()
        {
            if (fenced) throw new LeaseLostException("This process is fenced after Homecoming lease loss.");
            if (lease != null)
            {
                HoldLocked();
                return;
            }
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(DataHolder.ConnectionString) { Pooling = false };
            var connection = new Npgsql.NpgsqlConnection(builder.ConnectionString);
            connection.Open();
            try
            {
                using (var q = new Npgsql.NpgsqlCommand("SELECT pg_try_advisory_lock(@class,@object)", connection))
                {
                    q.Parameters.AddWithValue("class", LeaseClassId);
                    q.Parameters.AddWithValue("object", LeaseObjectId);
                    if (!Equals(q.ExecuteScalar(), true))
                        throw new InvalidOperationException("Another live process owns Homecoming sessions.");
                }
                lease = connection;
            }
            catch
            {
                connection.Close(); connection.Dispose();
                throw;
            }
        }

        // One identity per live process. Startup closes sessions this process does not own
        // only after Acquire succeeds. Same-process reinitialize keeps its own sessions.
        private static readonly Guid ProcessId = Guid.NewGuid();
        private sealed class Presence
        {
            internal readonly Guid Id = Guid.NewGuid();
            internal bool Opened, Closed;
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Client.GameState, Presence> Sessions =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Client.GameState, Presence>();

        private static void Recover(Npgsql.NpgsqlConnection c)
        {
            using (var t = c.BeginTransaction())
            {
                Game.Features.QuestInventory.Execute(c, t, @"UPDATE revora_account_sessions
SET closed_at=GREATEST(opened_at,clock_timestamp())
WHERE closed_at IS NULL AND owner IS DISTINCT FROM @owner", "owner", ProcessId);
                using (var q = Game.Features.QuestInventory.Command(c, t,
                    "SELECT EXISTS(SELECT 1 FROM revora_account_sessions WHERE closed_at IS NULL AND owner IS DISTINCT FROM @owner)",
                    "owner", ProcessId))
                {
                    if (Equals(q.ExecuteScalar(), true))
                        throw new InvalidOperationException("Interrupted sessions were not recovered.");
                }
                t.Commit();
            }
        }

        internal static void Open(Client.GameState player)
        {
            var presence = Sessions.GetValue(player, key => new Presence());
            lock (presence)
            {
                if (presence.Closed) throw new InvalidOperationException("Session already closed.");
                if (presence.Opened) return;
                if (player.Fake || player.Entity == null || player.Account == null ||
                    player.Entity.UID != player.Account.EntityID)
                    throw new InvalidOperationException("Session identity is missing.");
                SessionOpened(player.Account, presence.Id);
                try { Game.Features.OriginBind.Login(player); }
                catch (Exception ex) { Console.WriteLine("[origin-bind] login error=" + ex.GetType().Name); }
                presence.Opened = true;
            }
        }

        internal static void Close(Client.GameState player)
        {
            var presence = Sessions.GetValue(player, key => new Presence());
            lock (presence)
            {
                presence.Closed = true; // A delayed login cannot reopen a disconnected session.
                if (!presence.Opened) return;
                SessionClosed(presence.Id);
                try { Game.Features.OriginBind.Logout(player); }
                catch (Exception ex) { Console.WriteLine("[origin-bind] logout error=" + ex.GetType().Name); }
                presence.Opened = false;
            }
        }

        internal static void SessionOpened(AccountTable account, Guid session)
        {
            if (session == Guid.Empty || account == null || !account.exists || account.EntityID == 0)
                throw new InvalidOperationException("Session identity is missing.");
            lock (LeaseGate)
            {
                HoldLocked();
                PauseForTest();
                ProtectLease(() =>
                {
                    using (var t = lease.BeginTransaction())
                    {
                        using (var q = Game.Features.QuestInventory.Command(lease,t,
                            "SELECT username FROM accounts WHERE lower(username)=lower(@name) FOR UPDATE", "name",account.Username))
                        using (var r = q.ExecuteReader())
                        {
                            if (!r.Read() || r.Read()) throw new InvalidOperationException("Session account identity is ambiguous or missing.");
                        }
                        if (Game.Features.QuestInventory.Execute(lease,t,@"INSERT INTO revora_account_sessions
(session_id,account_name,character_uid,opened_at,owner)
SELECT @session,lower(a.username),a.entityid,clock_timestamp(),@owner FROM accounts a
JOIN revora_account_activity h ON h.account_name=lower(a.username)
WHERE lower(a.username)=lower(@name) AND a.entityid=@uid
AND (SELECT count(*) FROM accounts WHERE lower(username)=lower(@name))=1",
                            "session",session,"name",account.Username,"uid",(long)account.EntityID,"owner",ProcessId) != 1)
                            throw new InvalidOperationException("Session presence was not saved.");
                        t.Commit();
                    }
                });
            }
        }

        internal static void SessionClosed(Guid session)
        {
            lock (LeaseGate)
            {
                if (fenced || lease == null) return;
                try { HoldLocked(); }
                catch (LeaseLostException) { return; }
                PauseForTest();
                try
                {
                    ProtectLease(() =>
                    {
                        using (var t = lease.BeginTransaction())
                        {
                            Game.Features.QuestInventory.Execute(lease,t,@"UPDATE revora_account_sessions
SET closed_at=GREATEST(opened_at,clock_timestamp()) WHERE session_id=@session AND closed_at IS NULL",
                                "session",session);
                            using (var q = Game.Features.QuestInventory.Command(lease,t,
                                "SELECT closed_at IS NOT NULL FROM revora_account_sessions WHERE session_id=@session", "session",session))
                            {
                                if (!object.Equals(q.ExecuteScalar(), true))
                                    throw new InvalidOperationException("Session closure was not saved.");
                            }
                            t.Commit();
                        }
                    });
                }
                catch (LeaseLostException) { }
            }
        }

        // Snapshot rule v1: raw VIP 0 is Earth Knight; VIP 1-7 keep the login floor and one-day expiry.
        // Never consult the character's later mutable profile to choose a return title.
        internal static int ReturnTitle(string snapshot, DateTime returnedUtc)
        {
            if (string.IsNullOrEmpty(snapshot) || returnedUtc.Kind != DateTimeKind.Utc) return 0;
            var values = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(snapshot);
            if (values==null) return 0;
            long version,level,reborn,vip,days,reset;
            if (!ReadNumber(values,"version",out version) || version!=1 ||
                !ReadNumber(values,"level",out level) || level<1 || level>255 ||
                !ReadNumber(values,"reborn",out reborn) || reborn<0 || reborn>2 ||
                !ReadNumber(values,"vip_level",out vip) || vip<0 || vip>7 ||
                !ReadNumber(values,"vip_days",out days) || days<0 || days>255 ||
                !ReadNumber(values,"last_reset",out reset) || reset<0 || reset>DateTime.MaxValue.Ticks-TimeSpan.TicksPerDay ||
                (level<120 && reborn==0)) return 0;
            object storedZone;
            string zone=values.TryGetValue("timezone",out storedZone)?storedZone as string:null;
            if (string.IsNullOrEmpty(zone)) return 0;
            var localReturn=TimeZoneInfo.ConvertTimeFromUtc(returnedUtc,TimeZoneInfo.FindSystemTimeZoneById(zone));
            if (vip==0) return 20282028; // Frozen raw VIP 0 is Earth Knight before the login floor.
            vip=Math.Max(4,vip); // Existing login minimum for VIP 1-7; this does not change VIP economics.
            if (days==1 && vip>=6 && localReturn>=new DateTime(reset).AddDays(1)) vip=5;
            if (vip<=4) return 20302030;
            if (vip<=6) return 20292029;
            return 20312031;
        }
        private static bool ReadNumber(System.Collections.Generic.Dictionary<string,object> values,string key,out long value)
        {
            object stored; value=0;
            if (!values.TryGetValue(key,out stored) || stored==null) return false;
            return long.TryParse(Convert.ToString(stored,System.Globalization.CultureInfo.InvariantCulture),System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,out value);
        }
        private static void FreezeReturnTitle(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,string name)
        {
            long sequence; int title;
            using (var q=Game.Features.QuestInventory.Command(c,t,@"SELECT r.auth_number,r.character_snapshot::text,r.returned_at
FROM revora_account_returns r JOIN revora_account_activity a ON a.account_name=r.account_name
WHERE r.account_name=@name AND r.auth_number=a.auth_count+1", "name",name))
            using (var r=q.ExecuteReader())
            {
                if (!r.Read()) return;
                sequence=r.GetInt64(0);
                title=ReturnTitle(r.IsDBNull(1)?null:r.GetString(1),r.GetFieldValue<DateTimeOffset>(2).UtcDateTime);
            }
            if (title!=0 && !StorageManager.Known(title/10000,title%10000))
                throw new InvalidOperationException("Homecoming catalog mapping is missing.");
            if (Game.Features.QuestInventory.Execute(c,t,@"UPDATE revora_account_returns SET title_key=@title
WHERE account_name=@name AND auth_number=@sequence AND title_key IS NULL",
                "title",title,"name",name,"sequence",sequence)!=1)
                throw new InvalidOperationException("Return title snapshot was not saved.");
        }

        internal static void Authenticated(AccountTable account)
        {
            if (account == null || !account.exists || string.IsNullOrWhiteSpace(account.Username))
                throw new InvalidOperationException("Authenticated account identity is missing.");
            lock (LeaseGate)
            {
                HoldLocked();
                PauseForTest();
                ProtectLease(() =>
                {
                using (var t = lease.BeginTransaction())
                {
                    // Read the stored canonical identity. Reject ambiguous legacy names.
                    string name = null;
                    using (var q = Game.Features.QuestInventory.Command(lease,t,
                        "SELECT lower(username),entityid FROM accounts WHERE lower(username)=lower(@name) FOR UPDATE",
                        "name",account.Username,"uid",(long)account.EntityID))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read() || Convert.ToInt64(r.GetValue(1)) != account.EntityID)
                            throw new InvalidOperationException("Authenticated account identity changed or no longer exists.");
                        name = r.GetString(0);
                        if (r.Read()) throw new InvalidOperationException("Authenticated account identity is ambiguous.");
                    }
                    // Freeze prior evidence before updating last_auth. The account row lock
                    // serializes case variants and session openings for this identity.
                    using (var receipt = Game.Features.QuestInventory.Command(lease,t,@"WITH candidate AS (
SELECT h.account_name,h.auth_count+1 AS auth_number,CAST(@uid AS bigint) AS character_uid,
 GREATEST(h.first_auth,h.last_auth,(SELECT max(closed_at) FROM revora_account_sessions s WHERE s.account_name=h.account_name)) AS inactive_since,
 statement_timestamp() AS returned_at,
 CASE WHEN e.uid IS NULL THEN NULL ELSE jsonb_build_object(
 'version',1,'level',e.level,'reborn',e.reborn,'vip_level',e.viplevel,
 'vip_days',e.vipdays,'vip_level_days',e.vipleveldays,'last_reset',e.lastresettime,'timezone',@zone) END AS character_snapshot
FROM revora_account_activity h LEFT JOIN entities e ON e.uid=@uid WHERE h.account_name=@name
AND NOT EXISTS(SELECT 1 FROM revora_account_sessions s WHERE s.account_name=h.account_name AND s.closed_at IS NULL)
AND GREATEST(h.first_auth,h.last_auth,(SELECT max(closed_at) FROM revora_account_sessions s WHERE s.account_name=h.account_name))
 <=statement_timestamp()-interval '30 days'), saved AS (
 INSERT INTO revora_account_returns(account_name,auth_number,character_uid,inactive_since,returned_at,character_snapshot,expires_at)
 SELECT account_name,auth_number,character_uid,inactive_since,returned_at,character_snapshot,returned_at+interval '720 hours'
 FROM candidate RETURNING 1)
SELECT (SELECT count(*) FROM candidate)=(SELECT count(*) FROM saved)", "name",name,"uid",(long)account.EntityID,"zone",TimeZoneInfo.Local.Id))
                    {
                        if (!object.Equals(receipt.ExecuteScalar(), true))
                            throw new InvalidOperationException("Return observation was not saved.");
                    }
                    FreezeReturnTitle(lease,t,name);
                    if (Game.Features.QuestInventory.Execute(lease,t,@"INSERT INTO revora_account_activity(account_name,first_auth,last_auth,auth_count)
VALUES(@name,clock_timestamp(),clock_timestamp(),1)
ON CONFLICT(account_name) DO UPDATE SET last_auth=GREATEST(revora_account_activity.last_auth,EXCLUDED.last_auth),
auth_count=revora_account_activity.auth_count+1", "name",name) != 1)
                        throw new InvalidOperationException("Authenticated account observation was not saved.");
                    t.Commit();
                }
                });
            }
        }
    }
}
