using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Npgsql;
using Revora;
using Revora.Database;
using Console = System.Console;
using Thread = System.Threading.Thread;

internal static class OriginRealmTests
{
    static readonly BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    static int checks;
    static Type Origin;
    static Type Tournament;
    static Type Ctf;
    static Type Bind;
    static Type Epk;
    static Type Tpk;
    static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        checks++;
        Console.WriteLine("PASS: " + name);
        Console.Out.Flush();
    }
    static object Call(Type type, string method, params object[] args)
    {
        try { return type.GetMethod(method, Hidden).Invoke(null, args); }
        catch (TargetInvocationException e)
        {
            if (e.InnerException != null) throw e.InnerException;
            throw;
        }
    }
    static object Call(string method, params object[] args) { return Call(Origin, method, args); }
    static object TCall(string method, params object[] args) { return Call(Tournament, method, args); }
    static object CCall(string method, params object[] args) { return Call(Ctf, method, args); }
    static object BCall(string method, params object[] args) { return Call(Bind, method, args); }
    static object ECall(string method, params object[] args) { return Call(Epk, method, args); }
    static object PCall(string method, params object[] args) { return Call(Tpk, method, args); }
    static void Seat(string realm, uint uid, Guid owner)
    {
        Call("RegisterIdentity", realm, uid, "T" + uid.ToString());
        Call("OpenSession", realm, uid, owner);
    }
    static uint[] Five(uint start)
    {
        return new uint[] { start, start + 1, start + 2, start + 3, start + 4 };
    }
    static void SeatFive(string realm, uint start, Guid owner)
    {
        uint[] ids = Five(start);
        for (int i = 0; i < ids.Length; i++) Seat(realm, ids[i], owner);
    }
    static void Query(string sql, params object[] values)
    {
        using (var c = DataHolder.MySqlConnection)
        {
            c.Open();
            using (var q = new NpgsqlCommand(sql, c))
            {
                for (int i = 0; i < values.Length; i += 2) q.Parameters.AddWithValue((string)values[i], values[i + 1]);
                q.ExecuteNonQuery();
            }
        }
    }
    static long Scalar(string sql, params object[] values)
    {
        using (var c = DataHolder.MySqlConnection)
        {
            c.Open();
            using (var q = new NpgsqlCommand(sql, c))
            {
                for (int i = 0; i < values.Length; i += 2) q.Parameters.AddWithValue((string)values[i], values[i + 1]);
                return Convert.ToInt64(q.ExecuteScalar());
            }
        }
    }
    static string DeliverySnapshot(Guid delivery)
    {
        using (var c = DataHolder.MySqlConnection)
        {
            c.Open();
            using (var q = new NpgsqlCommand("SELECT row_to_json(o)::text FROM revora_origin_outbox o WHERE delivery_id=@id", c))
            {
                q.Parameters.AddWithValue("id", delivery);
                return (string)q.ExecuteScalar();
            }
        }
    }
    static string ResultSnapshot(Guid run)
    {
        using (var c = DataHolder.MySqlConnection)
        {
            c.Open();
            using (var q = new NpgsqlCommand(@"SELECT json_build_object(
'run',(SELECT row_to_json(r) FROM revora_origin_runs r WHERE run_id=@run),
'placements',(SELECT json_agg(p ORDER BY p.realm,p.uid) FROM revora_origin_placements p WHERE run_id=@run),
'outbox',(SELECT json_agg(o ORDER BY o.realm,o.uid) FROM revora_origin_outbox o WHERE run_id=@run))::text", c))
            {
                q.Parameters.AddWithValue("run", run);
                return (string)q.ExecuteScalar();
            }
        }
    }
    static int Main()
    {
        string original = null;
        bool created = false;
        try
        {
            var ini = new IniFile("Configuracion.ini");
            string host = ini.ReadString("MySql", "Host"), port = ini.ReadString("MySql", "Port");
            if (!string.IsNullOrEmpty(port) && host.IndexOf("Port=", StringComparison.OrdinalIgnoreCase) < 0)
                host = host.Trim().TrimEnd(';') + ";Port=" + port;
            DataHolder.CreateConnection(ini.ReadString("MySql", "Username"), Environment.GetEnvironmentVariable("REVORA_DATABASE_PASSWORD") ?? ini.ReadString("MySql", "Password"),
                ini.ReadString("MySql", "Database"), host);
            original = DataHolder.ConnectionString;
            var endpoint = new NpgsqlConnectionStringBuilder(original);
            if (endpoint.Host != "127.0.0.1" || endpoint.Port != 25439 || !endpoint.Database.StartsWith("revora_candidate_", StringComparison.Ordinal))
                throw new InvalidOperationException("Origin tests require the private candidate database.");
            Query("DROP SCHEMA IF EXISTS revora_origin_validation CASCADE");
            Query("CREATE SCHEMA revora_origin_validation");
            created = true;
            var builder = new NpgsqlConnectionStringBuilder(original);
            builder.SearchPath = "revora_origin_validation";
            DataHolder.ConnectionString = builder.ConnectionString;
            Origin = typeof(AccountTable).Assembly.GetType("Revora.Game.Features.OriginRealms");
            Tournament = typeof(AccountTable).Assembly.GetType("Revora.Game.Features.OriginTournaments");
            Ctf = typeof(AccountTable).Assembly.GetType("Revora.Game.Features.OriginCtf");
            Bind = typeof(AccountTable).Assembly.GetType("Revora.Game.Features.OriginBind");
            Epk = typeof(AccountTable).Assembly.GetType("Revora.Game.Features.OriginElitePk");
            Tpk = typeof(AccountTable).Assembly.GetType("Revora.Game.Features.OriginTeamPk");
            Call("Initialize");
            Origin.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => DateTime.UtcNow));
            Tournament.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => DateTime.UtcNow));
            Call("RegisterRealm", "origin-a");
            Call("RegisterRealm", "origin-b");
            Call("RegisterIdentity", "origin-a", 44010u, "LegacyA");
            Call("RegisterIdentity", "origin-b", 44010u, "LegacyB");
            Guid legacyRun = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0001");
            Guid legacyDelivery = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0002");
            Query(@"CREATE TABLE revora_origin_runs(
run_id uuid PRIMARY KEY,event_key varchar(48) NOT NULL,
state varchar(12) NOT NULL CHECK(state IN ('open','started','finalized')),
opened_at timestamptz NOT NULL,started_at timestamptz NULL,finalized_at timestamptz NULL);
CREATE TABLE revora_origin_entrants(
run_id uuid NOT NULL REFERENCES revora_origin_runs(run_id),
realm varchar(32) NOT NULL,uid bigint NOT NULL CHECK(uid>0),
enrolled_at timestamptz NOT NULL,PRIMARY KEY(run_id,realm,uid),
FOREIGN KEY(realm,uid) REFERENCES revora_origin_identities(realm,uid));
CREATE TABLE revora_origin_placements(
run_id uuid NOT NULL REFERENCES revora_origin_runs(run_id),
realm varchar(32) NOT NULL,uid bigint NOT NULL,placement integer NOT NULL CHECK(placement>0),
PRIMARY KEY(run_id,realm,uid),UNIQUE(run_id,placement),
FOREIGN KEY(run_id,realm,uid) REFERENCES revora_origin_entrants(run_id,realm,uid));
CREATE TABLE revora_origin_outbox(
delivery_id uuid PRIMARY KEY,run_id uuid NOT NULL,
realm varchar(32) NOT NULL,uid bigint NOT NULL,
payload varchar(32) NOT NULL,created_at timestamptz NOT NULL,acked_at timestamptz NULL,
UNIQUE(run_id,realm,uid),
FOREIGN KEY(run_id,realm,uid) REFERENCES revora_origin_placements(run_id,realm,uid));");
            Query("INSERT INTO revora_origin_runs(run_id,event_key,state,opened_at,started_at) VALUES(@run,'tournament-v1','started',TIMESTAMPTZ '2035-03-01 08:00:00+00',TIMESTAMPTZ '2035-03-01 09:00:00+00')",
                "run", legacyRun);
            Query("INSERT INTO revora_origin_entrants(run_id,realm,uid,enrolled_at) VALUES(@run,'origin-a',44010,TIMESTAMPTZ '2035-03-01 08:30:00+00'),(@run,'origin-b',44010,TIMESTAMPTZ '2035-03-01 08:30:00+00')",
                "run", legacyRun);
            Query("INSERT INTO revora_origin_placements(run_id,realm,uid,placement) VALUES(@run,'origin-a',44010,1),(@run,'origin-b',44010,2)",
                "run", legacyRun);
            Query("INSERT INTO revora_origin_outbox(delivery_id,run_id,realm,uid,payload,created_at,acked_at) VALUES(@delivery,@run,'origin-a',44010,'placement:1',TIMESTAMPTZ '2035-03-01 10:00:00+00',NULL)",
                "delivery", legacyDelivery, "run", legacyRun);
            TCall("Initialize");
            Check(Scalar("SELECT count(*) FROM revora_origin_quarantine_runs WHERE run_id=@run AND event_key='tournament-v1' AND state='started' AND coordinator IS NULL AND opened_at=TIMESTAMPTZ '2035-03-01 08:00:00+00' AND started_at=TIMESTAMPTZ '2035-03-01 09:00:00+00'",
                "run", legacyRun) == 1,
                "unowned pre-owner runs are quarantined without inventing a coordinator");
            Check(Scalar("SELECT count(*) FROM revora_origin_quarantine_entrants WHERE run_id=@run AND enrolled_at=TIMESTAMPTZ '2035-03-01 08:30:00+00' AND owner IS NULL",
                "run", legacyRun) == 2,
                "quarantine preserves both origin identities and enrollment time");
            Check(Scalar("SELECT count(*) FROM revora_origin_quarantine_placements WHERE run_id=@run AND ((realm='origin-a' AND placement=1) OR (realm='origin-b' AND placement=2))",
                "run", legacyRun) == 2,
                "quarantine preserves frozen placements");
            Check(Scalar("SELECT count(*) FROM revora_origin_quarantine_outbox WHERE delivery_id=@delivery AND run_id=@run AND payload='placement:1' AND created_at=TIMESTAMPTZ '2035-03-01 10:00:00+00' AND acked_at IS NULL AND owner IS NULL",
                "delivery", legacyDelivery, "run", legacyRun) == 1,
                "quarantine preserves delivery identity, payload, timestamp and unacked state");
            Check(Scalar("SELECT count(*) FROM revora_origin_runs WHERE run_id=@run", "run", legacyRun) == 0
                && Scalar("SELECT count(*) FROM revora_origin_outbox WHERE delivery_id=@delivery", "delivery", legacyDelivery) == 0,
                "active unowned runs and pending deliveries are not left live");
            Check(Scalar(@"SELECT count(*) FROM information_schema.columns
WHERE table_schema=current_schema() AND table_name='revora_origin_runs' AND column_name='coordinator' AND is_nullable='NO'") == 1
                && Scalar(@"SELECT count(*) FROM information_schema.columns
WHERE table_schema=current_schema() AND table_name='revora_origin_outbox' AND column_name='owner' AND is_nullable='NO'") == 1,
                "upgraded ownership columns are NOT NULL");
            Check(Scalar(@"SELECT count(*) FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname=current_schema() AND t.relname='revora_origin_runs' AND c.conname='revora_origin_runs_coordinator_nonempty'") == 1
                && Scalar(@"SELECT count(*) FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname=current_schema() AND t.relname='revora_origin_outbox' AND c.conname='revora_origin_outbox_owner_nonempty'") == 1,
                "nonempty owner constraints are attached to this schema's tables");
            bool emptyCoordinator = false;
            try { Query("INSERT INTO revora_origin_runs(run_id,event_key,coordinator,state,opened_at) VALUES('aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0099','tournament-v1','00000000-0000-0000-0000-000000000000','open',TIMESTAMPTZ '2035-03-01 08:00:00+00')"); }
            catch (Exception) { emptyCoordinator = true; }
            Check(emptyCoordinator && Scalar("SELECT count(*) FROM revora_origin_runs WHERE run_id='aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0099'") == 0,
                "upgraded runs reject an empty coordinator");
            Query("INSERT INTO revora_origin_runs(run_id,event_key,coordinator,state,opened_at) VALUES('aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0098','tournament-v1','aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0097','open',TIMESTAMPTZ '2035-03-01 08:00:00+00')");
            bool emptyOwner = false;
            try { Query("INSERT INTO revora_origin_entrants(run_id,realm,uid,owner,enrolled_at) VALUES('aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0098','origin-a',44010,'00000000-0000-0000-0000-000000000000',TIMESTAMPTZ '2035-03-01 08:30:00+00')"); }
            catch (Exception) { emptyOwner = true; }
            Check(emptyOwner && Scalar("SELECT count(*) FROM revora_origin_entrants WHERE run_id='aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0098'") == 0,
                "upgraded entrants reject an empty owner");
            Query("INSERT INTO revora_origin_entrants(run_id,realm,uid,owner,enrolled_at) VALUES('aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0098','origin-a',44010,'aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0097',TIMESTAMPTZ '2035-03-01 08:30:00+00')");
            Query("INSERT INTO revora_origin_placements(run_id,realm,uid,placement) VALUES('aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0098','origin-a',44010,1)");
            bool emptyDelivery = false;
            try { Query("INSERT INTO revora_origin_outbox(delivery_id,run_id,realm,uid,owner,payload,created_at) VALUES('aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0096','aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0098','origin-a',44010,'00000000-0000-0000-0000-000000000000','placement:1',TIMESTAMPTZ '2035-03-01 10:00:00+00')"); }
            catch (Exception) { emptyDelivery = true; }
            Check(emptyDelivery && Scalar("SELECT count(*) FROM revora_origin_outbox WHERE delivery_id='aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0096'") == 0,
                "upgraded outbox rows reject an empty owner");
            Query("DELETE FROM revora_origin_placements WHERE run_id='aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0098'");
            Query("DELETE FROM revora_origin_entrants WHERE run_id='aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0098'");
            Query("DELETE FROM revora_origin_runs WHERE run_id='aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0098'");
            bool legacyStart = false;
            try { TCall("Start", legacyRun, Guid.NewGuid()); }
            catch (InvalidOperationException) { legacyStart = true; }
            Check(legacyStart, "archived unowned runs cannot be started");
            bool legacyAck = false;
            try { TCall("Ack", legacyDelivery, Guid.NewGuid()); }
            catch (InvalidOperationException) { legacyAck = true; }
            Check(legacyAck, "archived unowned deliveries cannot be acknowledged");
            bool badRealm = false;
            try { Call("RegisterRealm", "origin a"); }
            catch (ArgumentException) { badRealm = true; }
            Check(badRealm, "origin realm names reject spaces");
            Call("RegisterRealm", "origin-a");
            Call("RegisterRealm", "origin-b");
            Call("RegisterRealm", "origin-a");
            Check(Scalar("SELECT count(*) FROM revora_origin_realms") == 2, "two origin realms persist distinctly");
            const uint uid = 44001;
            Call("RegisterIdentity", "origin-a", uid, "HeroA");
            Call("RegisterIdentity", "origin-b", uid, "HeroB");
            Check(Scalar("SELECT count(*) FROM revora_origin_identities WHERE uid=@uid", "uid", (long)uid) == 2,
                "the same character number is a distinct identity on each origin");
            Call("RegisterIdentity", "origin-a", uid, "RenamedA");
            Check(Scalar("SELECT count(*) FROM revora_origin_identities WHERE realm='origin-a' AND uid=@uid AND name='RenamedA'", "uid", (long)uid) == 1
                && Scalar("SELECT count(*) FROM revora_origin_identities WHERE realm='origin-b' AND uid=@uid AND name='HeroB'", "uid", (long)uid) == 1,
                "a later trusted name updates the same origin identity without changing its uid");
            bool unknown = false;
            try { Call("OpenSession", "origin-a", 44002u, Guid.NewGuid()); }
            catch (InvalidOperationException) { unknown = true; }
            Check(unknown, "an unregistered identity cannot open a session");
            Guid ownerA = Guid.NewGuid(), ownerB = Guid.NewGuid();
            Guid sessionA = (Guid)Call("OpenSession", "origin-a", uid, ownerA);
            Guid sessionB = (Guid)Call("OpenSession", "origin-b", uid, ownerB);
            Check(sessionA != Guid.Empty && sessionB != Guid.Empty && sessionA != sessionB,
                "each origin opens its own session for the same character number");
            bool stolen = false;
            try { Call("OpenSession", "origin-a", uid, ownerB); }
            catch (InvalidOperationException) { stolen = true; }
            Check(stolen, "a second owner cannot open a live origin session");
            bool hijack = false;
            try { Call("Reconnect", "origin-a", uid, ownerB); }
            catch (InvalidOperationException) { hijack = true; }
            Check(hijack, "a second owner cannot reconnect a live origin session");
            Check((Guid)Call("Reconnect", "origin-a", uid, ownerA) == sessionA,
                "the owning process reconnects the original session");
            Call("CloseSession", "origin-a", uid, ownerA, sessionA);
            Guid resumed = (Guid)Call("OpenSession", "origin-a", uid, ownerA);
            Check(resumed != sessionA, "a closed session is replaced by a new owned session");
            bool guest = false;
            try { Call("Admit", "foundation-v1", "origin-a", uid, ownerB); }
            catch (InvalidOperationException) { guest = true; }
            Check(guest, "admission requires the owning origin session");
            Check((bool)Call("Admit", "foundation-v1", "origin-a", uid, ownerA),
                "the owning origin session can admit its identity");
            Check(!(bool)Call("Admit", "foundation-v1", "origin-a", uid, ownerA),
                "replayed admission does not duplicate the event row");
            Check(Scalar("SELECT count(*) FROM revora_origin_admissions WHERE event_key='foundation-v1' AND realm='origin-a' AND uid=@uid", "uid", (long)uid) == 1,
                "one admission receipt exists after replay");
            bool missing = false;
            try { Call("RecordReceipt", "foundation-v1", "origin-b", uid); }
            catch (InvalidOperationException) { missing = true; }
            Check(missing, "a receipt cannot be recorded without admission");
            Check((bool)Call("RecordReceipt", "foundation-v1", "origin-a", uid),
                "an admitted identity records one foundation receipt");
            Check(!(bool)Call("RecordReceipt", "foundation-v1", "origin-a", uid),
                "replayed receipt does not duplicate the reward");
            Check(Scalar("SELECT count(*) FROM revora_origin_receipts WHERE event_key='foundation-v1' AND uid=@uid", "uid", (long)uid) == 1,
                "duplicate-reward protection keeps one receipt per origin identity");
            Call("Admit", "foundation-v1", "origin-b", uid, ownerB);
            Check((bool)Call("RecordReceipt", "foundation-v1", "origin-b", uid),
                "the other origin records its own receipt for the same character number");
            Check(Scalar("SELECT count(*) FROM revora_origin_receipts WHERE event_key='foundation-v1' AND uid=@uid", "uid", (long)uid) == 2,
                "canonical identity is realm plus character number");
            Query("CREATE FUNCTION reject_origin_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_origin_receipt BEFORE INSERT ON revora_origin_receipts FOR EACH ROW EXECUTE FUNCTION reject_origin_receipt()");
            Call("RegisterIdentity", "origin-a", 44003u, "HeroC");
            Guid ownerC = Guid.NewGuid();
            Call("OpenSession", "origin-a", 44003u, ownerC);
            Call("Admit", "foundation-v1", "origin-a", 44003u, ownerC);
            bool rolled = false;
            try { Call("RecordReceipt", "foundation-v1", "origin-a", 44003u); }
            catch (InvalidOperationException) { rolled = true; }
            catch (Exception) { rolled = true; }
            finally { Query("DROP TRIGGER reject_origin_receipt ON revora_origin_receipts; DROP FUNCTION reject_origin_receipt()"); }
            Check(rolled && Scalar("SELECT count(*) FROM revora_origin_receipts WHERE uid=44003") == 0,
                "failed receipt insert rolls back the duplicate-reward write");
            Check(Scalar("SELECT count(*) FROM revora_origin_admissions WHERE uid=44003") == 1,
                "a failed receipt leaves the committed admission for retry");
            Call("RegisterIdentity", "origin-a", 44004u, "HeroD");
            Guid first = Guid.Empty, second = Guid.Empty;
            int opened = 0;
            var one = Guid.NewGuid();
            var two = Guid.NewGuid();
            var barrier = new Barrier(2);
            Thread t1 = new Thread(() =>
            {
                barrier.SignalAndWait();
                try { first = (Guid)Call("OpenSession", "origin-a", 44004u, one); Interlocked.Increment(ref opened); }
                catch (InvalidOperationException) { }
            });
            Thread t2 = new Thread(() =>
            {
                barrier.SignalAndWait();
                try { second = (Guid)Call("OpenSession", "origin-a", 44004u, two); Interlocked.Increment(ref opened); }
                catch (InvalidOperationException) { }
            });
            t1.Start(); t2.Start(); t1.Join(); t2.Join();
            Check(opened == 1 && ((first != Guid.Empty) ^ (second != Guid.Empty)),
                "concurrent openers leave exactly one owned session");
            Check(Scalar("SELECT count(*) FROM revora_origin_sessions WHERE uid=44004 AND closed_at IS NULL") == 1,
                "contention cannot create two live sessions for one identity");
            int originChecks = checks;
            Guid coordinator = Guid.NewGuid(), stranger = Guid.NewGuid();
            Guid run = (Guid)TCall("OpenRun", "tournament-v1", coordinator);
            Check((bool)TCall("Enroll", run, "origin-a", uid, ownerA), "owning session enrolls origin-a");
            Check(!(bool)TCall("Enroll", run, "origin-a", uid, ownerA), "replayed enrollment does not duplicate the frozen identity");
            bool lone = false;
            try { TCall("Start", run, coordinator); }
            catch (InvalidOperationException) { lone = true; }
            Check(lone, "a single origin cannot start the tournament");
            Check((bool)TCall("Enroll", run, "origin-b", uid, ownerB), "owning session enrolls a second origin");
            bool foreignStart = false;
            try { TCall("Start", run, stranger); }
            catch (InvalidOperationException) { foreignStart = true; }
            Check(foreignStart && Scalar("SELECT count(*) FROM revora_origin_runs WHERE run_id=@run AND state='open'", "run", run) == 1,
                "a foreign coordinator cannot start the tournament");
            Check((bool)TCall("Start", run, coordinator), "two origins start the tournament");
            Check(!(bool)TCall("Start", run, coordinator), "replayed start does not rewrite the run");
            bool frozen = false;
            try { TCall("Enroll", run, "origin-a", 44003u, ownerC); }
            catch (InvalidOperationException) { frozen = true; }
            Check(frozen, "started roster rejects additional entrants");
            bool incomplete = false;
            try { TCall("Finalize", run, new string[] { "origin-a:44001:1" }, coordinator); }
            catch (InvalidOperationException) { incomplete = true; }
            Check(incomplete, "finalize requires every frozen entrant");
            bool foreignFinal = false;
            try { TCall("Finalize", run, new string[] { "origin-a:44001:1", "origin-b:44001:2" }, stranger); }
            catch (InvalidOperationException) { foreignFinal = true; }
            Check(foreignFinal && Scalar("SELECT count(*) FROM revora_origin_runs WHERE run_id=@run AND state='started'", "run", run) == 1,
                "a foreign coordinator cannot finalize the tournament");
            Check((bool)TCall("Finalize", run, new string[] { "origin-a:44001:1", "origin-b:44001:2" }, coordinator),
                "finalize writes immutable placements and outbox rows");
            Check(!(bool)TCall("Finalize", run, new string[] { "origin-a:44001:1", "origin-b:44001:2" }, coordinator),
                "replayed finalize does not rewrite placements");
            string finalizedSnapshot = ResultSnapshot(run);
            bool conflictingResult = false;
            try { TCall("Finalize", run, new string[] { "origin-a:44001:2", "origin-b:44001:1" }, coordinator); }
            catch (InvalidOperationException) { conflictingResult = true; }
            Check(conflictingResult && ResultSnapshot(run) == finalizedSnapshot,
                "conflicting winner retry is rejected without changing result or delivery rows");
            bool incompleteReplay = false;
            try { TCall("Finalize", run, new string[] { "origin-a:44001:1" }, coordinator); }
            catch (InvalidOperationException) { incompleteReplay = true; }
            Check(incompleteReplay && ResultSnapshot(run) == finalizedSnapshot,
                "finalized replay must still cover the complete frozen roster");
            bool foreignReplay = false;
            try { TCall("Finalize", run, new string[] { "origin-a:44001:1", "origin-b:99999:2" }, coordinator); }
            catch (InvalidOperationException) { foreignReplay = true; }
            Check(foreignReplay && ResultSnapshot(run) == finalizedSnapshot,
                "a finalized run does not accept a different identity as an idempotent replay");
            Check(!(bool)TCall("Finalize", run, new string[] { "origin-b:044001:2", "origin-a:44001:1" }, coordinator)
                && ResultSnapshot(run) == finalizedSnapshot,
                "equivalent reordered numeric identities replay without rewriting the result");
            Check(Scalar("SELECT count(*) FROM revora_origin_placements WHERE run_id=@run", "run", run) == 2
                && (int)TCall("PendingDeliveries", run) == 2,
                "result outbox holds one unacked delivery per origin");
            Guid delivery = (Guid)TCall("NextDelivery", run);
            bool foreignAck = false;
            try { TCall("Ack", delivery, stranger); }
            catch (InvalidOperationException) { foreignAck = true; }
            Check(foreignAck && (int)TCall("PendingDeliveries", run) == 2,
                "a foreign origin cannot acknowledge another origin's delivery");
            Check((bool)TCall("Ack", delivery, ownerA), "first outbox acknowledgement commits");
            Check(!(bool)TCall("Ack", delivery, ownerA), "replayed acknowledgement is idempotent");
            Check((int)TCall("PendingDeliveries", run) == 1, "unacked deliveries remain recoverable");
            Check((bool)TCall("Ack", (Guid)TCall("NextDelivery", run), ownerB) && (int)TCall("PendingDeliveries", run) == 0,
                "recoverable outbox drains exactly once per delivery");
            Call("RegisterIdentity", "origin-a", 44006u, "HeroE");
            Call("RegisterIdentity", "origin-a", 44007u, "HeroF");
            Guid ownerE = Guid.NewGuid(), ownerF = Guid.NewGuid();
            Call("OpenSession", "origin-a", 44006u, ownerE);
            Call("OpenSession", "origin-a", 44007u, ownerF);
            Guid same = (Guid)TCall("OpenRun", "tournament-v1", coordinator);
            TCall("Enroll", same, "origin-a", 44006u, ownerE);
            TCall("Enroll", same, "origin-a", 44007u, ownerF);
            bool sameOrigin = false;
            try { TCall("Start", same, coordinator); }
            catch (InvalidOperationException) { sameOrigin = true; }
            Check(sameOrigin, "two identities from one origin cannot start");
            Guid race = (Guid)TCall("OpenRun", "tournament-v1", coordinator);
            TCall("Enroll", race, "origin-a", uid, ownerA);
            TCall("Enroll", race, "origin-b", uid, ownerB);
            int started = 0;
            var startGate = new Barrier(2);
            Thread s1 = new Thread(() => { startGate.SignalAndWait(); if ((bool)TCall("Start", race, coordinator)) Interlocked.Increment(ref started); });
            Thread s2 = new Thread(() => { startGate.SignalAndWait(); if ((bool)TCall("Start", race, coordinator)) Interlocked.Increment(ref started); });
            s1.Start(); s2.Start(); s1.Join(); s2.Join();
            Check(started == 1 && Scalar("SELECT count(*) FROM revora_origin_runs WHERE run_id=@run AND state='started'", "run", race) == 1,
                "concurrent start leaves one authenticated transition");
            Query("CREATE FUNCTION reject_origin_place() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_origin_place BEFORE INSERT ON revora_origin_placements FOR EACH ROW EXECUTE FUNCTION reject_origin_place()");
            bool placeFail = false;
            try { TCall("Finalize", race, new string[] { "origin-a:44001:1", "origin-b:44001:2" }, coordinator); }
            catch (InvalidOperationException) { placeFail = true; }
            catch (Exception) { placeFail = true; }
            finally { Query("DROP TRIGGER reject_origin_place ON revora_origin_placements; DROP FUNCTION reject_origin_place()"); }
            Check(placeFail && Scalar("SELECT count(*) FROM revora_origin_runs WHERE run_id=@run AND state='started'", "run", race) == 1
                && Scalar("SELECT count(*) FROM revora_origin_outbox WHERE run_id=@run", "run", race) == 0,
                "failed placement insert rolls back finalize and outbox");
            int finalized = 0, conflicts = 0;
            Exception finalError = null;
            var finalGate = new Barrier(2);
            Thread[] finalizers = new Thread[2];
            string[][] finalResults = {
                new string[] { "origin-a:44001:1", "origin-b:44001:2" },
                new string[] { "origin-a:44001:2", "origin-b:44001:1" }
            };
            for (int index = 0; index < finalizers.Length; index++)
            {
                int candidate = index;
                finalizers[index] = new Thread(() => {
                    try
                    {
                        finalGate.SignalAndWait();
                        if ((bool)TCall("Finalize", race, finalResults[candidate], coordinator))
                            Interlocked.Increment(ref finalized);
                    }
                    catch (InvalidOperationException) { Interlocked.Increment(ref conflicts); }
                    catch (Exception ex) { finalError = ex; }
                });
                finalizers[index].Start();
            }
            foreach (var finalizer in finalizers) finalizer.Join();
            finalGate.Dispose();
            Check(finalError == null && finalized == 1 && conflicts == 1
                && Scalar("SELECT count(*) FROM revora_origin_placements WHERE run_id=@run", "run", race) == 2
                && (int)TCall("PendingDeliveries", race) == 2,
                "competing final results commit one complete result and explicitly reject the conflict");
            int tournamentChecks = checks - originChecks;
            CCall("Initialize");
            Call("RegisterIdentity", "origin-b", 44011u, "HeroG");
            Guid ownerG = Guid.NewGuid();
            Call("OpenSession", "origin-b", 44011u, ownerG);
            Guid ctfRun = (Guid)TCall("OpenRun", "cs-ctf-v1", coordinator);
            TCall("Enroll", ctfRun, "origin-a", uid, ownerA);
            TCall("Enroll", ctfRun, "origin-b", 44011u, ownerG);
            TCall("Start", ctfRun, coordinator);
            TCall("Finalize", ctfRun, new string[] { "origin-a:44001:1", "origin-b:44011:2" }, coordinator);
            bool wrongEvent = false;
            try { CCall("Capture", run, coordinator); }
            catch (InvalidOperationException) { wrongEvent = true; }
            Check(wrongEvent, "CS-CTF capture rejects a non CS-CTF run");
            Check((bool)CCall("Capture", ctfRun, coordinator), "CS-CTF capture records Conqueror and Radiant Wings for placement 1");
            Check(!(bool)CCall("Capture", ctfRun, coordinator), "replayed CS-CTF capture does not duplicate awards");
            Check(Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND uid=44001 AND title_key IN (20030004,60020021)", "run", ctfRun) == 2,
                "champion identity stores Conqueror and Radiant Wings");
            Check(Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND uid=44011", "run", ctfRun) == 0,
                "placement 2 does not receive CS-CTF wardrobe awards");
            bool foreignCapture = false;
            try { CCall("Capture", ctfRun, stranger); }
            catch (InvalidOperationException) { foreignCapture = true; }
            Check(foreignCapture, "a foreign coordinator cannot capture CS-CTF awards");
            bool emptyAwardOwner = false;
            try
            {
                Query(@"INSERT INTO revora_origin_ctf_awards(run_id,realm,uid,owner,title_key,earned_at)
VALUES(@run,'origin-b',44011,'00000000-0000-0000-0000-000000000000',20030004,TIMESTAMPTZ '2035-03-01 10:00:00+00')",
                    "run", ctfRun);
            }
            catch (Exception) { emptyAwardOwner = true; }
            Check(emptyAwardOwner, "upgraded CS-CTF awards reject empty owner");
            Query(@"CREATE TABLE IF NOT EXISTS revora_event_awards (
uid bigint NOT NULL CHECK(uid > 0), source varchar(48) NOT NULL,
source_key varchar(96) NOT NULL, title_key integer NOT NULL,
earned_at timestamptz NOT NULL, expires_at timestamptz NULL,
revoked boolean NOT NULL DEFAULT false,
CHECK(expires_at IS NULL OR expires_at > earned_at),
PRIMARY KEY(uid, source, source_key))");
            CCall("Initialize");
            Check(Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND uid=44001 AND NOT award_recorded", "run", ctfRun) == 2,
                "restarted CS-CTF initialize preserves unrecorded champion awards");
            var foreignUid = (List<string>)CCall("Deliver", "origin-b", uid, ownerB);
            Check(foreignUid.Count == 0 && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND uid=44001 AND NOT award_recorded", "run", ctfRun) == 2,
                "equal uid on another origin cannot consume the champion awards");
            bool foreignDeliver = false;
            try { CCall("Deliver", "origin-a", uid, ownerB); }
            catch (InvalidOperationException) { foreignDeliver = true; }
            Check(foreignDeliver && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND uid=44001 AND NOT award_recorded", "run", ctfRun) == 2,
                "a foreign origin owner cannot consume the champion awards");
            var firstDeliver = (List<string>)CCall("Deliver", "origin-a", uid, ownerA);
            Check(firstDeliver.Count == 2 && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND uid=44001 AND award_recorded", "run", ctfRun) == 2
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44001 AND source='cs-ctf-revora-v1' AND expires_at IS NULL") == 2,
                "authenticated origin-a delivery grants Conqueror and Radiant Wings once");
            var replayDeliver = (List<string>)CCall("Deliver", "origin-a", uid, ownerA);
            Check(replayDeliver.Count == 0 && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44001 AND source='cs-ctf-revora-v1'") == 2,
                "replayed origin-authenticated delivery does not duplicate grants");
            int captured = 0;
            Guid ctfRace = (Guid)TCall("OpenRun", "cs-ctf-v1", coordinator);
            TCall("Enroll", ctfRace, "origin-a", uid, ownerA);
            TCall("Enroll", ctfRace, "origin-b", 44011u, ownerG);
            TCall("Start", ctfRace, coordinator);
            TCall("Finalize", ctfRace, new string[] { "origin-a:44001:1", "origin-b:44011:2" }, coordinator);
            var capGate = new Barrier(2);
            Thread c1 = new Thread(() => { capGate.SignalAndWait(); if ((bool)CCall("Capture", ctfRace, coordinator)) Interlocked.Increment(ref captured); });
            Thread c2 = new Thread(() => { capGate.SignalAndWait(); if ((bool)CCall("Capture", ctfRace, coordinator)) Interlocked.Increment(ref captured); });
            c1.Start(); c2.Start(); c1.Join(); c2.Join();
            Check(captured == 1 && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run", "run", ctfRace) == 2,
                "concurrent CS-CTF capture writes one champion pair");
            int delivered = 0;
            var dGate = new Barrier(2);
            Thread d1 = new Thread(() => { dGate.SignalAndWait(); if (((List<string>)CCall("Deliver", "origin-a", uid, ownerA)).Count > 0) Interlocked.Increment(ref delivered); });
            Thread d2 = new Thread(() => { dGate.SignalAndWait(); if (((List<string>)CCall("Deliver", "origin-a", uid, ownerA)).Count > 0) Interlocked.Increment(ref delivered); });
            d1.Start(); d2.Start(); d1.Join(); d2.Join();
            Check(delivered == 1 && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND award_recorded", "run", ctfRace) == 2
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44001 AND source='cs-ctf-revora-v1'") == 4,
                "concurrent origin-authenticated delivery writes one champion pair");
            Guid ctfFail = (Guid)TCall("OpenRun", "cs-ctf-v1", coordinator);
            TCall("Enroll", ctfFail, "origin-a", uid, ownerA);
            TCall("Enroll", ctfFail, "origin-b", 44011u, ownerG);
            TCall("Start", ctfFail, coordinator);
            TCall("Finalize", ctfFail, new string[] { "origin-a:44001:1", "origin-b:44011:2" }, coordinator);
            Check((bool)CCall("Capture", ctfFail, coordinator), "rollback fixture captures a third champion pair");
            Query("CREATE FUNCTION reject_ctf_grant() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_ctf_grant BEFORE INSERT ON revora_event_awards FOR EACH ROW EXECUTE FUNCTION reject_ctf_grant()");
            bool grantFail = false;
            try { CCall("Deliver", "origin-a", uid, ownerA); }
            catch (InvalidOperationException) { grantFail = true; }
            catch (Exception) { grantFail = true; }
            finally { Query("DROP TRIGGER reject_ctf_grant ON revora_event_awards; DROP FUNCTION reject_ctf_grant()"); }
            Check(grantFail && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND NOT award_recorded", "run", ctfFail) == 2
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44001 AND source='cs-ctf-revora-v1'") == 4,
                "failed CS-CTF grant rolls back origin-authenticated delivery");
            Guid dual = (Guid)TCall("OpenRun", "cs-ctf-v1", coordinator);
            TCall("Enroll", dual, "origin-b", uid, ownerB);
            TCall("Enroll", dual, "origin-a", 44006u, ownerE);
            TCall("Start", dual, coordinator);
            TCall("Finalize", dual, new string[] { "origin-b:44001:1", "origin-a:44006:2" }, coordinator);
            Check((bool)CCall("Capture", dual, coordinator), "equal-uid origin-b can win a later CS-CTF run");
            Check(((List<string>)CCall("Deliver", "origin-b", uid, ownerB)).Count == 2,
                "origin-b delivery grants the later equal-uid champion pair");
            Check((int)CCall("VisibleCount", uid, "origin-a") == 2 && (int)CCall("VisibleCount", uid, "origin-b") == 2,
                "each origin sees its own Conqueror and Radiant Wings when origin-b is later");
            Query("UPDATE revora_event_awards SET earned_at=earned_at + interval '3 days' WHERE uid=44001 AND source='cs-ctf-revora-v1' AND source_key LIKE 'origin-a:%'");
            Check((int)CCall("VisibleCount", uid, "origin-a") == 2 && (int)CCall("VisibleCount", uid, "origin-b") == 2,
                "each origin sees its own Conqueror and Radiant Wings when origin-a is later");
            Guid keyRun = (Guid)TCall("OpenRun", "cs-ctf-v1", coordinator);
            TCall("Enroll", keyRun, "origin-a", 44006u, ownerE);
            TCall("Enroll", keyRun, "origin-b", 44011u, ownerG);
            TCall("Start", keyRun, coordinator);
            TCall("Finalize", keyRun, new string[] { "origin-a:44006:1", "origin-b:44011:2" }, coordinator);
            Check((bool)CCall("Capture", keyRun, coordinator), "legacy-key fixture captures origin-a placement 1");
            DateTime keyEarned = DateTime.UtcNow;
            Query(@"INSERT INTO revora_event_awards(uid,source,source_key,title_key,earned_at,expires_at)
VALUES(44006,'cs-ctf-revora-v1',@k1,20030004,@earned,NULL),(44006,'cs-ctf-revora-v1',@k2,60020021,@earned,NULL)",
                "k1", keyRun.ToString() + ":20030004", "k2", keyRun.ToString() + ":60020021", "earned", keyEarned);
            Query("UPDATE revora_origin_ctf_awards SET award_recorded=true WHERE run_id=@run", "run", keyRun);
            CCall("Initialize");
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44006 AND source='cs-ctf-revora-v1' AND source_key=@key",
                "key", "origin-a:" + keyRun.ToString() + ":20030004") == 1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44006 AND source='cs-ctf-revora-v1' AND source_key=@key",
                "key", "origin-a:" + keyRun.ToString() + ":60020021") == 1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44006 AND source='cs-ctf-revora-v1' AND source_key=@key",
                "key", keyRun.ToString() + ":20030004") == 0,
                "pre-upgrade run:title awards migrate to realm:run:title once");
            CCall("Initialize");
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44006 AND source='cs-ctf-revora-v1'") == 2
                && ((List<string>)CCall("Deliver", "origin-a", 44006u, ownerE)).Count == 0
                && (int)CCall("VisibleCount", 44006u, "origin-a") == 2,
                "restarted migration keeps one visible pre-upgrade award pair without redelivery");
            Call("RegisterRealm", "bind-a");
            Call("RegisterRealm", "bind-b");
            Call("RegisterIdentity", "bind-a", 44020u, "BindA");
            Call("RegisterIdentity", "bind-b", 44020u, "BindB");
            Guid process = Guid.NewGuid(), foreign = Guid.NewGuid();
            Call("OpenSession", "bind-a", 44020u, foreign);
            Call("OpenSession", "bind-b", 44020u, ownerB);
            Bind.GetField("Owner", Hidden).SetValue(null, process);
            using (var competitor = DataHolder.MySqlConnection)
            {
                competitor.Open();
                using (var command = new NpgsqlCommand("SELECT pg_advisory_lock(6720, hashtext('bind-a'))", competitor)) command.ExecuteNonQuery();
                try
                {
                    bool refused = false;
                    try { BCall("Configure", "bind-a"); }
                    catch (InvalidOperationException) { refused = true; }
                    Check(refused && Bind.GetField("Realm", Hidden).GetValue(null) == null
                        && Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='bind-a' AND uid=44020 AND closed_at IS NULL") == 1,
                        "competing live realm lock prevents recovery and preserves its session");
                }
                finally { using (var command = new NpgsqlCommand("SELECT pg_advisory_unlock(6720, hashtext('bind-a'))", competitor)) command.ExecuteNonQuery(); }
            }
            BCall("Configure", "bind-a");
            Check(Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='bind-a' AND uid=44020 AND closed_at IS NULL") == 0
                && Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='bind-b' AND uid=44020 AND closed_at IS NULL") == 1,
                "restart recovers leftover sessions for this origin only");
            BCall("Configure", "origin a");
            Check(Bind.GetField("Realm", Hidden).GetValue(null) == null,
                "an invalid origin token disables the production bind");
            Bind.GetField("Owner", Hidden).SetValue(null, process);
            BCall("Configure", "bind-a");
            Check((Guid)Call("OpenSession", "bind-a", 44020u, process) != Guid.Empty,
                "after restart this origin can open a process-owned session");
            Call("RegisterIdentity", "bind-a", 44021u, "NewBind");
            bool stale = false;
            try { Call("OpenSession", "bind-a", 44021u, foreign); }
            catch (InvalidOperationException) { stale = true; }
            Check(stale, "recovered realm rejects a stale process opening sessions");
            Guid replacement = Guid.NewGuid();
            using (var inFlight = DataHolder.MySqlConnection)
            {
                inFlight.Open();
                using (var transaction = inFlight.BeginTransaction())
                {
                    Call("RequireRealmOwner", inFlight, transaction, "bind-a", process);
                    Bind.GetField("Owner", Hidden).SetValue(null, replacement);
                    Exception recoveryError = null;
                    var recovery = new Thread(() => { try { BCall("Configure", "bind-a"); } catch (Exception error) { recoveryError = error; } });
                    recovery.Start();
                    try
                    {
                        var wait = System.Diagnostics.Stopwatch.StartNew();
                        long blocked = 0;
                        while (blocked == 0 && wait.Elapsed.TotalSeconds < 5)
                        {
                            blocked = Scalar("SELECT count(*) FROM pg_locks WHERE locktype='advisory' AND classid=6721 AND objid=(hashtext('bind-a')::bigint & 4294967295) AND mode='ExclusiveLock' AND NOT granted");
                            if (blocked == 0) Thread.Sleep(10);
                        }
                        Check(blocked == 1 && Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='bind-a' AND uid=44020 AND owner=@owner AND closed_at IS NULL", "owner", process) == 1,
                            "recovery waits for in-flight owned-session transactions before closing sessions");
                    }
                    finally { transaction.Rollback(); Check(recovery.Join(10000), "recovery completes after the owned-session transaction releases its lock"); }
                    if (recoveryError != null) throw recoveryError;
                }
            }
            bool oldReconnect = false;
            try { Call("Reconnect", "bind-a", 44020u, process); }
            catch (InvalidOperationException) { oldReconnect = true; }
            Check(oldReconnect && Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='bind-a' AND closed_at IS NULL") == 0,
                "replacement owner atomically fences the former process and closes its sessions");
            Guid replacementSession = (Guid)Call("OpenSession", "bind-a", 44020u, replacement);
            BCall("Configure", "bind-a");
            Check((Guid)Call("Reconnect", "bind-a", 44020u, replacement) == replacementSession,
                "repeated owner configuration preserves the current session");
            var lease = (NpgsqlConnection)Bind.GetField("lease", Hidden).GetValue(null);
            int leasePid;
            using (var command = new NpgsqlCommand("SELECT pg_backend_pid()", lease)) leasePid = Convert.ToInt32(command.ExecuteScalar());
            Query("SELECT pg_terminate_backend(@pid)", "pid", leasePid);
            bool lostLease = false;
            try { BCall("Recover"); }
            catch (NpgsqlException) { lostLease = true; }
            catch (InvalidOperationException) { lostLease = true; }
            Check(lostLease && Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='bind-a' AND uid=44020 AND closed_at IS NULL") == 1,
                "broken lease connection cannot run recovery through a different pooled connection");
            Bind.GetField("Owner", Hidden).SetValue(null, Guid.NewGuid());
            BCall("Configure", "bind-a");
            Check(Scalar("SELECT count(*) FROM revora_origin_sessions WHERE realm='bind-a' AND closed_at IS NULL") == 0,
                "replacement can acquire a disconnected lease and recover its abandoned sessions");
            BCall("Configure", "");
            Call("RegisterIdentity", "origin-a", 44030u, "CtfRestart");
            Guid ctfOld = Guid.NewGuid(), ctfNew = Guid.NewGuid();
            Guid ctfOldSession = (Guid)Call("OpenSession", "origin-a", 44030u, ctfOld);
            Guid ctfHold = (Guid)TCall("OpenRun", "cs-ctf-v1", coordinator);
            TCall("Enroll", ctfHold, "origin-a", 44030u, ctfOld);
            TCall("Enroll", ctfHold, "origin-b", 44011u, ownerG);
            TCall("Start", ctfHold, coordinator);
            TCall("Finalize", ctfHold, new string[] { "origin-a:44030:1", "origin-b:44011:2" }, coordinator);
            Check((bool)CCall("Capture", ctfHold, coordinator), "restart fixture captures pending CS-CTF awards");
            Call("CloseSession", "origin-a", 44030u, ctfOld, ctfOldSession);
            Check((Guid)Call("OpenSession", "origin-a", 44030u, ctfNew) != Guid.Empty,
                "a later process owner opens the same CS-CTF origin identity");
            bool ctfStaleOwner = false;
            try { CCall("Deliver", "origin-a", 44030u, ctfOld); }
            catch (InvalidOperationException) { ctfStaleOwner = true; }
            Check(ctfStaleOwner && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND NOT award_recorded", "run", ctfHold) == 2,
                "the enrolled process owner cannot consume CS-CTF awards after restart");
            var ctfRestartDeliver = (List<string>)CCall("Deliver", "origin-a", 44030u, ctfNew);
            Check(ctfRestartDeliver.Count == 2 && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND award_recorded AND owner=@owner", "run", ctfHold, "owner", ctfNew) == 2,
                "a later process owner delivers pending CS-CTF awards once");
            Check(((List<string>)CCall("Deliver", "origin-a", 44030u, ctfNew)).Count == 0
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44030 AND source='cs-ctf-revora-v1'") == 2,
                "replayed restart delivery does not duplicate CS-CTF grants");
            Guid restartedDelivery = (Guid)TCall("NextDelivery", ctfHold);
            bool staleAck = false;
            try { TCall("Ack", restartedDelivery, ctfOld); }
            catch (InvalidOperationException) { staleAck = true; }
            Check(staleAck && (int)TCall("PendingDeliveries", ctfHold) == 2,
                "old origin owner cannot acknowledge a result after session replacement");
            Query(@"CREATE FUNCTION suppress_origin_ack() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER suppress_origin_ack BEFORE UPDATE ON revora_origin_outbox FOR EACH ROW EXECUTE FUNCTION suppress_origin_ack();");
            bool suppressedAck = false;
            try { TCall("Ack", restartedDelivery, ctfNew); }
            catch (InvalidOperationException) { suppressedAck = true; }
            finally { Query("DROP TRIGGER suppress_origin_ack ON revora_origin_outbox; DROP FUNCTION suppress_origin_ack()"); }
            Check(suppressedAck && (int)TCall("PendingDeliveries", ctfHold) == 2,
                "suppressed acknowledgement cannot masquerade as an idempotent replay");
            Check((bool)TCall("Ack", restartedDelivery, ctfNew)
                && Scalar("SELECT count(*) FROM revora_origin_outbox WHERE delivery_id=@id AND owner=@owner AND acked_at IS NOT NULL", "id", restartedDelivery, "owner", ctfNew) == 1,
                "current origin session owner acknowledges a pre-restart delivery");
            string acknowledgedSnapshot = DeliverySnapshot(restartedDelivery);
            Check(!(bool)TCall("Ack", restartedDelivery, ctfNew) && (int)TCall("PendingDeliveries", ctfHold) == 1
                && acknowledgedSnapshot == DeliverySnapshot(restartedDelivery),
                "replacement-owner acknowledgement replay preserves the entire saved delivery");
            bool otherOriginAck = false;
            try { TCall("Ack", (Guid)TCall("NextDelivery", ctfHold), ctfNew); }
            catch (InvalidOperationException) { otherOriginAck = true; }
            Check(otherOriginAck && (int)TCall("PendingDeliveries", ctfHold) == 1,
                "replacement owner cannot acknowledge another origin identity");
            Call("RegisterIdentity", "origin-a", 44033u, "CtfRace");
            Guid ctfRaceOld = Guid.NewGuid(), ctfRaceNew = Guid.NewGuid();
            Guid ctfRaceSession = (Guid)Call("OpenSession", "origin-a", 44033u, ctfRaceOld);
            Guid ctfHoldRace = (Guid)TCall("OpenRun", "cs-ctf-v1", coordinator);
            TCall("Enroll", ctfHoldRace, "origin-a", 44033u, ctfRaceOld);
            TCall("Enroll", ctfHoldRace, "origin-b", 44011u, ownerG);
            TCall("Start", ctfHoldRace, coordinator);
            TCall("Finalize", ctfHoldRace, new string[] { "origin-a:44033:1", "origin-b:44011:2" }, coordinator);
            Check((bool)CCall("Capture", ctfHoldRace, coordinator), "concurrent restart fixture captures pending CS-CTF awards");
            Call("CloseSession", "origin-a", 44033u, ctfRaceOld, ctfRaceSession);
            Call("OpenSession", "origin-a", 44033u, ctfRaceNew);
            int ctfRestarted = 0;
            var ctfRestartGate = new Barrier(2);
            Thread cr1 = new Thread(() => { ctfRestartGate.SignalAndWait(); if (((List<string>)CCall("Deliver", "origin-a", 44033u, ctfRaceNew)).Count > 0) Interlocked.Increment(ref ctfRestarted); });
            Thread cr2 = new Thread(() => { ctfRestartGate.SignalAndWait(); if (((List<string>)CCall("Deliver", "origin-a", 44033u, ctfRaceNew)).Count > 0) Interlocked.Increment(ref ctfRestarted); });
            cr1.Start(); cr2.Start(); cr1.Join(); cr2.Join();
            Check(ctfRestarted == 1 && Scalar("SELECT count(*) FROM revora_origin_ctf_awards WHERE run_id=@run AND award_recorded", "run", ctfHoldRace) == 2
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44033 AND source='cs-ctf-revora-v1'") == 2,
                "concurrent restart delivery writes one CS-CTF champion pair");
            Guid concurrentDelivery = (Guid)TCall("NextDelivery", ctfHoldRace);
            int acknowledgements = 0, acknowledgementErrors = 0;
            using (var ackGate = new Barrier(2))
            {
                ThreadStart acknowledge = () =>
                {
                    try { ackGate.SignalAndWait(); if ((bool)TCall("Ack", concurrentDelivery, ctfRaceNew)) Interlocked.Increment(ref acknowledgements); }
                    catch (Exception) { Interlocked.Increment(ref acknowledgementErrors); }
                };
                var firstAck = new Thread(acknowledge); var secondAck = new Thread(acknowledge);
                firstAck.Start(); secondAck.Start();
                bool firstJoined = firstAck.Join(10000), secondJoined = secondAck.Join(10000);
                Check(firstJoined && secondJoined && acknowledgementErrors == 0 && acknowledgements == 1
                    && (int)TCall("PendingDeliveries", ctfHoldRace) == 1,
                    "concurrent replacement-owner acknowledgements commit exactly once");
            }
            int ctfChecks = checks - originChecks - tournamentChecks;
            ECall("Initialize");
            // The isolated bind-a recovery must leave these CTF sessions alive.
            Call("Reconnect", "origin-a", uid, ownerA);
            Call("Reconnect", "origin-a", 44006u, ownerE);
            Call("RegisterIdentity", "origin-b", 44021u, "HeroI");
            Guid ownerI = Guid.NewGuid();
            Call("OpenSession", "origin-b", 44021u, ownerI);
            Guid epkRun = (Guid)TCall("OpenRun", "cs-epk-v1", coordinator);
            TCall("Enroll", epkRun, "origin-a", uid, ownerA);
            TCall("Enroll", epkRun, "origin-b", 44011u, ownerG);
            TCall("Enroll", epkRun, "origin-a", 44006u, ownerE);
            TCall("Enroll", epkRun, "origin-b", 44021u, ownerI);
            TCall("Start", epkRun, coordinator);
            TCall("Finalize", epkRun, new string[] { "origin-a:44001:1", "origin-b:44011:2", "origin-a:44006:3", "origin-b:44021:4" }, coordinator);
            Check((int)ECall("TitleFor", 4) == 20382038 && (int)ECall("TitleFor", 8) == 20382038 && (int)ECall("TitleFor", 9) == 0,
                "Expert covers placements 4 through 8");
            bool epkWrongEvent = false;
            try { ECall("Capture", ctfRun, coordinator); }
            catch (InvalidOperationException) { epkWrongEvent = true; }
            Check(epkWrongEvent, "CS Elite PK capture rejects a non Elite PK run");
            Check((bool)ECall("Capture", epkRun, coordinator), "CS Elite PK capture records the four placement titles");
            Check(!(bool)ECall("Capture", epkRun, coordinator), "replayed CS Elite PK capture does not duplicate awards");
            Check(Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND uid=44001 AND title_key=20352035", "run", epkRun) == 1
                && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND uid=44011 AND title_key=20362036", "run", epkRun) == 1
                && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND uid=44006 AND title_key=20372037", "run", epkRun) == 1
                && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND uid=44021 AND title_key=20382038", "run", epkRun) == 1,
                "placements 1, 2, 3 and 4 store Legendary, Peerless, Outstanding and Expert");
            Check(Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND uid=44001 AND title_key=20382038", "run", epkRun) == 0,
                "placement 1 does not receive Expert");
            bool epkEmptyOwner = false;
            try
            {
                Query(@"INSERT INTO revora_origin_epk_awards(run_id,realm,uid,owner,title_key,earned_at)
VALUES(@run,'origin-b',44011,'00000000-0000-0000-0000-000000000000',20362036,TIMESTAMPTZ '2035-03-01 10:00:00+00')",
                    "run", epkRun);
            }
            catch (Exception) { epkEmptyOwner = true; }
            Check(epkEmptyOwner, "upgraded CS Elite PK awards reject empty owner");
            ECall("Initialize");
            Check(Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND uid=44001 AND NOT award_recorded", "run", epkRun) == 1,
                "restarted CS Elite PK initialize preserves unrecorded placement awards");
            var epkForeignUid = (List<string>)ECall("Deliver", "origin-b", uid, ownerB);
            Check(epkForeignUid.Count == 0 && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND uid=44001 AND NOT award_recorded", "run", epkRun) == 1,
                "equal uid on another origin cannot consume the Elite PK awards");
            bool epkForeignDeliver = false;
            try { ECall("Deliver", "origin-a", uid, ownerB); }
            catch (InvalidOperationException) { epkForeignDeliver = true; }
            Check(epkForeignDeliver && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND uid=44001 AND NOT award_recorded", "run", epkRun) == 1,
                "a foreign origin owner cannot consume the Elite PK awards");
            var epkFirst = (List<string>)ECall("Deliver", "origin-a", uid, ownerA);
            Check(epkFirst.Count == 1 && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND uid=44001 AND award_recorded", "run", epkRun) == 1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44001 AND source='cs-epk-revora-v1' AND title_key=20352035 AND expires_at-earned_at=interval '336 hours'") == 1,
                "authenticated origin-a delivery grants Legendary once for fourteen days");
            Check(((List<string>)ECall("Deliver", "origin-b", 44011u, ownerG)).Count == 1
                && ((List<string>)ECall("Deliver", "origin-a", 44006u, ownerE)).Count == 1
                && ((List<string>)ECall("Deliver", "origin-b", 44021u, ownerI)).Count == 1,
                "authenticated delivery grants Peerless, Outstanding and Expert");
            var epkReplay = (List<string>)ECall("Deliver", "origin-a", uid, ownerA);
            Check(epkReplay.Count == 0 && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44001 AND source='cs-epk-revora-v1'") == 1,
                "replayed origin-authenticated Elite PK delivery does not duplicate grants");
            int epkCaptured = 0;
            Guid epkRace = (Guid)TCall("OpenRun", "cs-epk-v1", coordinator);
            TCall("Enroll", epkRace, "origin-a", uid, ownerA);
            TCall("Enroll", epkRace, "origin-b", 44011u, ownerG);
            TCall("Start", epkRace, coordinator);
            TCall("Finalize", epkRace, new string[] { "origin-a:44001:1", "origin-b:44011:2" }, coordinator);
            var epkCapGate = new Barrier(2);
            Thread e1 = new Thread(() => { epkCapGate.SignalAndWait(); if ((bool)ECall("Capture", epkRace, coordinator)) Interlocked.Increment(ref epkCaptured); });
            Thread e2 = new Thread(() => { epkCapGate.SignalAndWait(); if ((bool)ECall("Capture", epkRace, coordinator)) Interlocked.Increment(ref epkCaptured); });
            e1.Start(); e2.Start(); e1.Join(); e2.Join();
            Check(epkCaptured == 1 && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run", "run", epkRace) == 2,
                "concurrent CS Elite PK capture writes one placement set");
            int epkDelivered = 0;
            var epkDGate = new Barrier(2);
            Thread ed1 = new Thread(() => { epkDGate.SignalAndWait(); if (((List<string>)ECall("Deliver", "origin-a", uid, ownerA)).Count > 0) Interlocked.Increment(ref epkDelivered); });
            Thread ed2 = new Thread(() => { epkDGate.SignalAndWait(); if (((List<string>)ECall("Deliver", "origin-a", uid, ownerA)).Count > 0) Interlocked.Increment(ref epkDelivered); });
            ed1.Start(); ed2.Start(); ed1.Join(); ed2.Join();
            Check(epkDelivered == 1 && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND award_recorded AND uid=44001", "run", epkRace) == 1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44001 AND source='cs-epk-revora-v1'") == 2,
                "concurrent origin-authenticated Elite PK delivery writes one Legendary");
            Guid epkFail = (Guid)TCall("OpenRun", "cs-epk-v1", coordinator);
            TCall("Enroll", epkFail, "origin-a", uid, ownerA);
            TCall("Enroll", epkFail, "origin-b", 44011u, ownerG);
            TCall("Start", epkFail, coordinator);
            TCall("Finalize", epkFail, new string[] { "origin-a:44001:1", "origin-b:44011:2" }, coordinator);
            Check((bool)ECall("Capture", epkFail, coordinator), "rollback fixture captures a later Elite PK champion");
            Query("CREATE FUNCTION reject_epk_grant() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_epk_grant BEFORE INSERT ON revora_event_awards FOR EACH ROW WHEN (NEW.source='cs-epk-revora-v1') EXECUTE FUNCTION reject_epk_grant()");
            bool epkGrantFail = false;
            try { ECall("Deliver", "origin-a", uid, ownerA); }
            catch (InvalidOperationException) { epkGrantFail = true; }
            catch (Exception) { epkGrantFail = true; }
            finally { Query("DROP TRIGGER reject_epk_grant ON revora_event_awards; DROP FUNCTION reject_epk_grant()"); }
            Check(epkGrantFail && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND NOT award_recorded AND uid=44001", "run", epkFail) == 1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44001 AND source='cs-epk-revora-v1'") == 2,
                "failed CS Elite PK grant rolls back origin-authenticated delivery");
            Guid epkDual = (Guid)TCall("OpenRun", "cs-epk-v1", coordinator);
            TCall("Enroll", epkDual, "origin-b", uid, ownerB);
            TCall("Enroll", epkDual, "origin-a", 44006u, ownerE);
            TCall("Start", epkDual, coordinator);
            TCall("Finalize", epkDual, new string[] { "origin-b:44001:1", "origin-a:44006:2" }, coordinator);
            Check((bool)ECall("Capture", epkDual, coordinator), "equal-uid origin-b can win a later Elite PK run");
            Check(((List<string>)ECall("Deliver", "origin-b", uid, ownerB)).Count == 1,
                "origin-b delivery grants the later equal-uid Legendary");
            Check((int)ECall("VisibleCount", uid, "origin-a") == 1 && (int)ECall("VisibleCount", uid, "origin-b") == 1,
                "each origin sees its own Legendary when origin-b is later");
            Query("UPDATE revora_event_awards SET earned_at=earned_at - interval '3 days', expires_at=expires_at - interval '3 days' WHERE uid=44001 AND source='cs-epk-revora-v1' AND source_key LIKE 'origin-b:%'");
            Check((int)ECall("VisibleCount", uid, "origin-a") == 1 && (int)ECall("VisibleCount", uid, "origin-b") == 1,
                "each origin sees its own Legendary when origin-a is later");
            Call("RegisterIdentity", "origin-a", 44034u, "EpkRestart");
            Guid epkOld = Guid.NewGuid(), epkNew = Guid.NewGuid();
            Guid epkOldSession = (Guid)Call("OpenSession", "origin-a", 44034u, epkOld);
            Guid epkHold = (Guid)TCall("OpenRun", "cs-epk-v1", coordinator);
            TCall("Enroll", epkHold, "origin-a", 44034u, epkOld);
            TCall("Enroll", epkHold, "origin-b", 44011u, ownerG);
            TCall("Start", epkHold, coordinator);
            TCall("Finalize", epkHold, new string[] { "origin-a:44034:1", "origin-b:44011:2" }, coordinator);
            Check((bool)ECall("Capture", epkHold, coordinator), "restart fixture captures pending CS Elite PK awards");
            Call("CloseSession", "origin-a", 44034u, epkOld, epkOldSession);
            Check((Guid)Call("OpenSession", "origin-a", 44034u, epkNew) != Guid.Empty,
                "a later process owner opens the same Elite PK origin identity");
            bool epkStaleOwner = false;
            try { ECall("Deliver", "origin-a", 44034u, epkOld); }
            catch (InvalidOperationException) { epkStaleOwner = true; }
            Check(epkStaleOwner && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND NOT award_recorded", "run", epkHold) == 2,
                "the enrolled process owner cannot consume Elite PK awards after restart");
            var epkRestartDeliver = (List<string>)ECall("Deliver", "origin-a", 44034u, epkNew);
            Check(epkRestartDeliver.Count == 1 && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND award_recorded AND owner=@owner AND uid=44034", "run", epkHold, "owner", epkNew) == 1,
                "a later process owner delivers pending Legendary once");
            Check(((List<string>)ECall("Deliver", "origin-a", 44034u, epkNew)).Count == 0
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44034 AND source='cs-epk-revora-v1'") == 1,
                "replayed restart delivery does not duplicate Elite PK grants");
            Call("RegisterIdentity", "origin-a", 44035u, "EpkRace");
            Guid epkRaceOld = Guid.NewGuid(), epkRaceNew = Guid.NewGuid();
            Guid epkRaceSession = (Guid)Call("OpenSession", "origin-a", 44035u, epkRaceOld);
            Guid epkHoldRace = (Guid)TCall("OpenRun", "cs-epk-v1", coordinator);
            TCall("Enroll", epkHoldRace, "origin-a", 44035u, epkRaceOld);
            TCall("Enroll", epkHoldRace, "origin-b", 44011u, ownerG);
            TCall("Start", epkHoldRace, coordinator);
            TCall("Finalize", epkHoldRace, new string[] { "origin-a:44035:1", "origin-b:44011:2" }, coordinator);
            Check((bool)ECall("Capture", epkHoldRace, coordinator), "concurrent restart fixture captures pending CS Elite PK awards");
            Call("CloseSession", "origin-a", 44035u, epkRaceOld, epkRaceSession);
            Call("OpenSession", "origin-a", 44035u, epkRaceNew);
            int epkRestarted = 0;
            var epkRestartGate = new Barrier(2);
            Thread er1 = new Thread(() => { epkRestartGate.SignalAndWait(); if (((List<string>)ECall("Deliver", "origin-a", 44035u, epkRaceNew)).Count > 0) Interlocked.Increment(ref epkRestarted); });
            Thread er2 = new Thread(() => { epkRestartGate.SignalAndWait(); if (((List<string>)ECall("Deliver", "origin-a", 44035u, epkRaceNew)).Count > 0) Interlocked.Increment(ref epkRestarted); });
            er1.Start(); er2.Start(); er1.Join(); er2.Join();
            Check(epkRestarted == 1 && Scalar("SELECT count(*) FROM revora_origin_epk_awards WHERE run_id=@run AND award_recorded AND uid=44035", "run", epkHoldRace) == 1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44035 AND source='cs-epk-revora-v1'") == 1,
                "concurrent restart delivery writes one Legendary");
            DateTime epkNow = DateTime.UtcNow.AddDays(20);
            Epk.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => epkNow));
            try
            {
                Check((int)ECall("VisibleCount", uid, "origin-a") == 0 && (int)ECall("VisibleCount", uid, "origin-b") == 0,
                    "CS Elite PK titles expire after fourteen days");
            }
            finally { Epk.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => DateTime.UtcNow)); }
            int epkChecks = checks - originChecks - tournamentChecks - ctfChecks;
            PCall("Initialize");
            Guid ownerTA = Guid.NewGuid(), ownerTB = Guid.NewGuid();
            SeatFive("origin-a", 44171u, ownerTA);
            SeatFive("origin-b", 44176u, ownerTB);
            Guid tpkGate = (Guid)TCall("OpenRun", "cs-team-pk-v1", coordinator);
            PCall("FormTeam", tpkGate, "origin-a", Five(44171u), ownerTA);
            PCall("FormTeam", tpkGate, "origin-b", Five(44176u), ownerTB);
            bool tpkGeneric = false;
            try { TCall("Enroll", tpkGate, "origin-a", 44172u, ownerTA); }
            catch (InvalidOperationException) { tpkGeneric = true; }
            Check(tpkGeneric && Scalar("SELECT count(*) FROM revora_origin_entrants WHERE run_id=@run AND uid=44172", "run", tpkGate) == 0
                && Scalar("SELECT count(*) FROM revora_origin_entrants WHERE run_id=@run", "run", tpkGate) == 2,
                "generic enroll cannot add a non-captain to CS Team PK");
            TCall("Start", tpkGate, coordinator);
            Query(@"INSERT INTO revora_origin_entrants(run_id,realm,uid,owner,enrolled_at)
VALUES(@run,'origin-a',44172,@owner,@now),(@run,'origin-a',44173,@owner,@now)",
                "run", tpkGate, "owner", ownerTA, "now", DateTime.UtcNow);
            TCall("Finalize", tpkGate, new string[] { "origin-a:44171:1", "origin-b:44176:2", "origin-a:44172:3", "origin-a:44173:4" }, coordinator);
            bool tpkMalformed = false;
            try { PCall("Capture", tpkGate, coordinator); }
            catch (InvalidOperationException) { tpkMalformed = true; }
            Check(tpkMalformed && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run", "run", tpkGate) == 0
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE source='cs-tpk-revora-v1' AND uid IN (44171,44172,44173)") == 0,
                "Capture rejects a Team PK run whose placements are not stored captains");
            SeatFive("origin-a", 44101u, ownerTA);
            SeatFive("origin-b", 44106u, ownerTB);
            SeatFive("origin-a", 44111u, ownerTA);
            SeatFive("origin-b", 44116u, ownerTB);
            Guid tpkRun = (Guid)TCall("OpenRun", "cs-team-pk-v1", coordinator);
            PCall("FormTeam", tpkRun, "origin-a", Five(44101u), ownerTA);
            PCall("FormTeam", tpkRun, "origin-b", Five(44106u), ownerTB);
            PCall("FormTeam", tpkRun, "origin-a", Five(44111u), ownerTA);
            PCall("FormTeam", tpkRun, "origin-b", Five(44116u), ownerTB);
            TCall("Start", tpkRun, coordinator);
            bool tpkLateRoster = false;
            try { PCall("FormTeam", tpkRun, "origin-a", Five(44121u), ownerTA); }
            catch (InvalidOperationException) { tpkLateRoster = true; }
            catch (ArgumentException) { tpkLateRoster = true; }
            Check(tpkLateRoster, "CS Team PK roster cannot change after start");
            TCall("Finalize", tpkRun, new string[] { "origin-a:44101:1", "origin-b:44106:2", "origin-a:44111:3", "origin-b:44116:4" }, coordinator);
            Check((int)PCall("TitleFor", 4) == 20622062 && (int)PCall("TitleFor", 8) == 20622062 && (int)PCall("TitleFor", 9) == 0,
                "Elite Team covers team placements 4 through 8");
            bool tpkWrongEvent = false;
            try { PCall("Capture", epkHold, coordinator); }
            catch (InvalidOperationException) { tpkWrongEvent = true; }
            Check(tpkWrongEvent, "CS Team PK capture rejects a non Team PK run");
            Check((bool)PCall("Capture", tpkRun, coordinator), "CS Team PK capture records every roster member");
            Check(!(bool)PCall("Capture", tpkRun, coordinator), "replayed CS Team PK capture does not duplicate awards");
            Check(Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND title_key=20592059", "run", tpkRun) == 5
                && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND title_key=20602060", "run", tpkRun) == 5
                && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND title_key=20612061", "run", tpkRun) == 5
                && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND title_key=20622062", "run", tpkRun) == 5,
                "team placements 1, 2, 3 and 4 store King, Dominator, Power and Elite Team for five members each");
            Check(Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND uid=44101 AND title_key=20622062", "run", tpkRun) == 0
                && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND uid=44102 AND title_key=20592059", "run", tpkRun) == 1,
                "placement 1 does not receive Elite Team and a non-captain receives King Team");
            bool tpkShort = false;
            Guid tpkShortRun = (Guid)TCall("OpenRun", "cs-team-pk-v1", coordinator);
            SeatFive("origin-a", 44141u, ownerTA);
            try { PCall("FormTeam", tpkShortRun, "origin-a", new uint[] { 44141u, 44142u, 44143u, 44144u }, ownerTA); }
            catch (ArgumentException) { tpkShort = true; }
            Check(tpkShort, "CS Team PK rejects a four-member roster");
            bool tpkEmptyOwner = false;
            try
            {
                Query(@"INSERT INTO revora_origin_tpk_awards(run_id,realm,uid,owner,title_key,earned_at)
VALUES(@run,'origin-a',44101,'00000000-0000-0000-0000-000000000000',20592059,TIMESTAMPTZ '2035-03-01 10:00:00+00')",
                    "run", tpkRun);
            }
            catch (Exception) { tpkEmptyOwner = true; }
            Check(tpkEmptyOwner, "upgraded CS Team PK awards reject empty owner");
            PCall("Initialize");
            Check(Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND uid=44101 AND NOT award_recorded", "run", tpkRun) == 1,
                "restarted CS Team PK initialize preserves unrecorded roster awards");
            Call("RegisterIdentity", "origin-b", 44101u, "TwinKing");
            Call("OpenSession", "origin-b", 44101u, ownerTB);
            var tpkForeignUid = (List<string>)PCall("Deliver", "origin-b", 44101u, ownerTB);
            Check(tpkForeignUid.Count == 0 && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND uid=44101 AND NOT award_recorded", "run", tpkRun) == 1,
                "equal uid on another origin cannot consume the Team PK awards");
            bool tpkForeignDeliver = false;
            try { PCall("Deliver", "origin-a", 44101u, ownerTB); }
            catch (InvalidOperationException) { tpkForeignDeliver = true; }
            Check(tpkForeignDeliver && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND uid=44101 AND NOT award_recorded", "run", tpkRun) == 1,
                "a foreign origin owner cannot consume the Team PK awards");
            var tpkFirst = (List<string>)PCall("Deliver", "origin-a", 44101u, ownerTA);
            Check(tpkFirst.Count == 1 && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND uid=44101 AND award_recorded", "run", tpkRun) == 1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44101 AND source='cs-tpk-revora-v1' AND title_key=20592059 AND expires_at-earned_at=interval '336 hours'") == 1,
                "authenticated captain delivery grants King Team once for fourteen days");
            Check(((List<string>)PCall("Deliver", "origin-a", 44102u, ownerTA)).Count == 1,
                "authenticated non-captain delivery grants King Team");
            var tpkReplay = (List<string>)PCall("Deliver", "origin-a", 44101u, ownerTA);
            Check(tpkReplay.Count == 0 && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44101 AND source='cs-tpk-revora-v1'") == 1,
                "replayed origin-authenticated Team PK delivery does not duplicate grants");
            SeatFive("origin-a", 44121u, ownerTA);
            SeatFive("origin-b", 44126u, ownerTB);
            int tpkCaptured = 0;
            Guid tpkRace = (Guid)TCall("OpenRun", "cs-team-pk-v1", coordinator);
            PCall("FormTeam", tpkRace, "origin-a", Five(44121u), ownerTA);
            PCall("FormTeam", tpkRace, "origin-b", Five(44126u), ownerTB);
            TCall("Start", tpkRace, coordinator);
            TCall("Finalize", tpkRace, new string[] { "origin-a:44121:1", "origin-b:44126:2" }, coordinator);
            var tpkCapGate = new Barrier(2);
            Thread tp1 = new Thread(() => { tpkCapGate.SignalAndWait(); if ((bool)PCall("Capture", tpkRace, coordinator)) Interlocked.Increment(ref tpkCaptured); });
            Thread tp2 = new Thread(() => { tpkCapGate.SignalAndWait(); if ((bool)PCall("Capture", tpkRace, coordinator)) Interlocked.Increment(ref tpkCaptured); });
            tp1.Start(); tp2.Start(); tp1.Join(); tp2.Join();
            Check(tpkCaptured == 1 && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run", "run", tpkRace) == 10,
                "concurrent CS Team PK capture writes one roster set");
            int tpkDelivered = 0;
            var tpkDGate = new Barrier(2);
            Thread td1 = new Thread(() => { tpkDGate.SignalAndWait(); if (((List<string>)PCall("Deliver", "origin-a", 44121u, ownerTA)).Count > 0) Interlocked.Increment(ref tpkDelivered); });
            Thread td2 = new Thread(() => { tpkDGate.SignalAndWait(); if (((List<string>)PCall("Deliver", "origin-a", 44121u, ownerTA)).Count > 0) Interlocked.Increment(ref tpkDelivered); });
            td1.Start(); td2.Start(); td1.Join(); td2.Join();
            Check(tpkDelivered == 1 && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND award_recorded AND uid=44121", "run", tpkRace) == 1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44121 AND source='cs-tpk-revora-v1'") == 1,
                "concurrent origin-authenticated Team PK delivery writes one King Team");
            Guid tpkFail = (Guid)TCall("OpenRun", "cs-team-pk-v1", coordinator);
            PCall("FormTeam", tpkFail, "origin-a", Five(44121u), ownerTA);
            PCall("FormTeam", tpkFail, "origin-b", Five(44126u), ownerTB);
            TCall("Start", tpkFail, coordinator);
            TCall("Finalize", tpkFail, new string[] { "origin-a:44121:1", "origin-b:44126:2" }, coordinator);
            Check((bool)PCall("Capture", tpkFail, coordinator), "rollback fixture captures a later Team PK roster");
            Query("CREATE FUNCTION reject_tpk_grant() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_tpk_grant BEFORE INSERT ON revora_event_awards FOR EACH ROW WHEN (NEW.source='cs-tpk-revora-v1') EXECUTE FUNCTION reject_tpk_grant()");
            bool tpkGrantFail = false;
            try { PCall("Deliver", "origin-a", 44122u, ownerTA); }
            catch (InvalidOperationException) { tpkGrantFail = true; }
            catch (Exception) { tpkGrantFail = true; }
            finally { Query("DROP TRIGGER reject_tpk_grant ON revora_event_awards; DROP FUNCTION reject_tpk_grant()"); }
            Check(tpkGrantFail && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND NOT award_recorded AND uid=44122", "run", tpkFail) == 1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44122 AND source='cs-tpk-revora-v1'") == 0,
                "failed CS Team PK grant rolls back origin-authenticated delivery");
            Guid tpkDual = (Guid)TCall("OpenRun", "cs-team-pk-v1", coordinator);
            PCall("FormTeam", tpkDual, "origin-b", new uint[] { 44101u, 44106u, 44107u, 44108u, 44109u }, ownerTB);
            PCall("FormTeam", tpkDual, "origin-a", Five(44111u), ownerTA);
            TCall("Start", tpkDual, coordinator);
            TCall("Finalize", tpkDual, new string[] { "origin-b:44101:1", "origin-a:44111:2" }, coordinator);
            Check((bool)PCall("Capture", tpkDual, coordinator), "equal-uid origin-b can win a later Team PK run");
            Check(((List<string>)PCall("Deliver", "origin-b", 44101u, ownerTB)).Count == 1,
                "origin-b delivery grants the later equal-uid King Team");
            Check((int)PCall("VisibleCount", 44101u, "origin-a") == 1 && (int)PCall("VisibleCount", 44101u, "origin-b") == 1,
                "each origin sees its own King Team when origin-b is later");
            Query("UPDATE revora_event_awards SET earned_at=earned_at - interval '3 days', expires_at=expires_at - interval '3 days' WHERE uid=44101 AND source='cs-tpk-revora-v1' AND source_key LIKE 'origin-b:%'");
            Check((int)PCall("VisibleCount", 44101u, "origin-a") == 1 && (int)PCall("VisibleCount", 44101u, "origin-b") == 1,
                "each origin sees its own King Team when origin-a is later");
            SeatFive("origin-a", 44151u, ownerTA);
            SeatFive("origin-b", 44156u, ownerTB);
            Guid tpkOld = Guid.NewGuid(), tpkNew = Guid.NewGuid();
            for (uint id = 44151u; id <= 44155u; id++)
            {
                Guid live;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    using (var q = new NpgsqlCommand("SELECT session_id FROM revora_origin_sessions WHERE realm='origin-a' AND uid=@uid AND closed_at IS NULL", c))
                    {
                        q.Parameters.AddWithValue("uid", (long)id);
                        live = (Guid)q.ExecuteScalar();
                    }
                }
                Call("CloseSession", "origin-a", id, ownerTA, live);
                Call("OpenSession", "origin-a", id, tpkNew);
            }
            Guid tpkHold = (Guid)TCall("OpenRun", "cs-team-pk-v1", coordinator);
            bool tpkStaleForm = false;
            try { PCall("FormTeam", tpkHold, "origin-a", Five(44151u), ownerTA); }
            catch (InvalidOperationException) { tpkStaleForm = true; }
            Check(tpkStaleForm, "the enrolled process owner cannot form a Team PK roster after restart");
            PCall("FormTeam", tpkHold, "origin-a", Five(44151u), tpkNew);
            PCall("FormTeam", tpkHold, "origin-b", Five(44156u), ownerTB);
            TCall("Start", tpkHold, coordinator);
            TCall("Finalize", tpkHold, new string[] { "origin-a:44151:1", "origin-b:44156:2" }, coordinator);
            Check((bool)PCall("Capture", tpkHold, coordinator), "restart fixture captures pending CS Team PK awards");
            bool tpkStaleOwner = false;
            try { PCall("Deliver", "origin-a", 44151u, ownerTA); }
            catch (InvalidOperationException) { tpkStaleOwner = true; }
            Check(tpkStaleOwner && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND uid=44151 AND NOT award_recorded", "run", tpkHold) == 1,
                "the enrolled process owner cannot consume Team PK awards after restart");
            var tpkRestartDeliver = (List<string>)PCall("Deliver", "origin-a", 44151u, tpkNew);
            Check(tpkRestartDeliver.Count == 1 && Scalar("SELECT count(*) FROM revora_origin_tpk_awards WHERE run_id=@run AND award_recorded AND owner=@owner AND uid=44151", "run", tpkHold, "owner", tpkNew) == 1,
                "a later process owner delivers pending King Team once");
            Check(((List<string>)PCall("Deliver", "origin-a", 44151u, tpkNew)).Count == 0
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44151 AND source='cs-tpk-revora-v1'") == 1,
                "replayed restart delivery does not duplicate Team PK grants");
            int tpkRestarted = 0;
            var tpkRestartGate = new Barrier(2);
            Thread tr1 = new Thread(() => { tpkRestartGate.SignalAndWait(); if (((List<string>)PCall("Deliver", "origin-a", 44152u, tpkNew)).Count > 0) Interlocked.Increment(ref tpkRestarted); });
            Thread tr2 = new Thread(() => { tpkRestartGate.SignalAndWait(); if (((List<string>)PCall("Deliver", "origin-a", 44152u, tpkNew)).Count > 0) Interlocked.Increment(ref tpkRestarted); });
            tr1.Start(); tr2.Start(); tr1.Join(); tr2.Join();
            Check(tpkRestarted == 1 && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=44152 AND source='cs-tpk-revora-v1'") == 1,
                "concurrent restart delivery writes one King Team");
            DateTime tpkNow = DateTime.UtcNow.AddDays(20);
            Tpk.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => tpkNow));
            try
            {
                Check((int)PCall("VisibleCount", 44101u, "origin-a") == 0 && (int)PCall("VisibleCount", 44101u, "origin-b") == 0,
                    "CS Team PK titles expire after fourteen days");
            }
            finally { Tpk.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => DateTime.UtcNow)); }
            Console.WriteLine("Origin realm checks passed: " + originChecks);
            Console.WriteLine("Origin tournament checks passed: " + tournamentChecks);
            Console.WriteLine("Origin CTF checks passed: " + ctfChecks);
            Console.WriteLine("Origin Elite PK checks passed: " + epkChecks);
            Console.WriteLine("Origin Team PK checks passed: " + (checks - originChecks - tournamentChecks - ctfChecks - epkChecks));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Exception: " + ex.Message);
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            if (Bind != null) BCall("Configure", "");
            if (original != null) DataHolder.ConnectionString = original;
            if (created)
            {
                try { Query("DROP SCHEMA revora_origin_validation CASCADE"); }
                catch { }
            }
        }
    }
}
