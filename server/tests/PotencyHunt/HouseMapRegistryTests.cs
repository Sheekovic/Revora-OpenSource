using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Database;
using Console = System.Console;

internal static class HouseMapRegistryTests
{
    static int checks;
    static readonly Type Registry = typeof(HouseTable).Assembly.GetType("Revora.Database.HouseMapRegistry");
    static object Call(string method, params object[] args)
    {
        try { return Registry.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++; Console.WriteLine("PASS: " + message);
    }
    static void Sql(string sql)
    {
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new NpgsqlCommand(sql, c)) q.ExecuteNonQuery(); }
    }
    static long Scalar(string sql)
    {
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new NpgsqlCommand(sql, c)) return Convert.ToInt64(q.ExecuteScalar()); }
    }
    static ushort Reserve(uint owner, bool commit = true)
    {
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
            ushort id = (ushort)Call("Reserve", c, t, owner); if (commit) t.Commit(); return id;
        }}
    }
    static void Reject(Action work, string message)
    {
        bool rejected = false;
        try { work(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }
    static int Main(string[] args)
    {
        bool schema = false; string original = null;
        try
        {
            var ini = new IniFile("Configuracion.ini");
            string host = ini.ReadString("MySql", "Host"), port = ini.ReadString("MySql", "Port");
            if (!string.IsNullOrEmpty(port) && host.IndexOf("Port=", StringComparison.OrdinalIgnoreCase) < 0)
                host = host.Trim().TrimEnd(';') + ";Port=" + port;
            DataHolder.CreateConnection(ini.ReadString("MySql", "Username"), ini.ReadString("MySql", "Password"), ini.ReadString("MySql", "Database"), host);
            original = DataHolder.ConnectionString;
            if (args.Length == 1 && args[0] == "--reopen-fixture")
            {
                DataHolder.ConnectionString = new NpgsqlConnectionStringBuilder(original) { SearchPath = "revora_house_validation" }.ConnectionString;
                Check(Reserve(1002) == 60002 && Reserve(66538) == 60003,
                    "new process recovers both saved house identities without runtime dictionaries");
                return 0;
            }
            if (args.Length != 0) throw new ArgumentException("Unknown fixture mode.");
            // Refuse an existing schema; only remove the schema this invocation created.
            Sql("CREATE SCHEMA revora_house_validation"); schema = true;
            var builder = new NpgsqlConnectionStringBuilder(original) { SearchPath = "revora_house_validation" };
            DataHolder.ConnectionString = builder.ConnectionString;
            Sql("CREATE TABLE entities(uid bigint PRIMARY KEY,money bigint NOT NULL DEFAULT 1000)");
            Sql("INSERT INTO entities(uid) VALUES(1002),(66538),(132074),(200001),(200002);INSERT INTO entities(uid) SELECT generate_series(300000,300031)");
            Call("Initialize"); Call("Initialize");
            DMaps.MapPaths.Add(60000, "static-fixture");
            MapsTable.MapInformations.Add(60001, new MapsTable.MapInformation { ID = 60001 });
            ushort first = Reserve(1002), second = Reserve(66538), third = Reserve(132074);
            Check(first == 60002 && second != first && third != second && third != first,
                "owners sharing a 16-bit suffix receive distinct durable maps and skip static reservations");
            var repeated = new ushort[12];
            Parallel.For(0, repeated.Length, i => repeated[i] = Reserve(1002));
            Check(repeated.All(id => id == first) && Scalar("SELECT count(*) FROM revora_house_maps WHERE uid=1002") == 1,
                "concurrent requests for one owner return one saved identity");
            var distinct = new ushort[32];
            Parallel.For(0, distinct.Length, i => distinct[i] = Reserve((uint)(300000 + i)));
            Check(distinct.Distinct().Count() == 32 && distinct.All(id => id > third), "concurrent owners cannot share a map");
            ushort rolledBack = Reserve(200001, false);
            Check(Scalar("SELECT count(*) FROM revora_house_maps WHERE uid=200001") == 0, "outer rollback leaves no abandoned reservation");
            Check(Reserve(200001) == rolledBack, "rolled-back capacity remains reusable");
            NpgsqlConnection.ClearAllPools(); Call("Initialize");
            Check(Reserve(1002) == first && Reserve(66538) == second, "fresh connections and reinitialization retain stable ownership");
            using (var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,
                Arguments = "--reopen-fixture", UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = System.IO.Directory.GetCurrentDirectory()
            }))
            {
                if (!child.WaitForExit(10000)) { child.Kill(); throw new Exception("Housing recovery child timed out."); }
                Check(child.ExitCode == 0, "durable ownership survives a separate process restart");
            }
            DMaps.MapPaths.Add(first, "new-static-conflict");
            Reject(() => Reserve(1002), "static content collision fails without silently moving an owned house");
            DMaps.MapPaths.Remove(first);
            MapsTable.MapInformations.Add(first, new MapsTable.MapInformation { ID = first, Owner = 66538 });
            Reject(() => Reserve(1002), "foreign runtime metadata cannot take over a persisted house");
            MapsTable.MapInformations.Remove(first);
            Reject(() => Reserve(400000), "unknown character cannot reserve housing capacity");
            Sql("CREATE FUNCTION suppress_house_map() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_house_map BEFORE INSERT ON revora_house_maps FOR EACH ROW EXECUTE FUNCTION suppress_house_map()");
            Reject(() => {
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    using (var q = new NpgsqlCommand("UPDATE entities SET money=0 WHERE uid=200002", c, t)) q.ExecuteNonQuery();
                    Call("Reserve", c, t, (uint)200002); t.Commit();
                }}
            }, "suppressed reservation prevents outer transaction commit");
            Sql("DROP TRIGGER suppress_house_map ON revora_house_maps;DROP FUNCTION suppress_house_map()");
            Check(Scalar("SELECT money FROM entities WHERE uid=200002") == 1000
                && Scalar("SELECT count(*) FROM revora_house_maps WHERE uid=200002") == 0,
                "failed reservation rolls back participating payment without creating an identity");
            Sql("INSERT INTO entities(uid) SELECT 1000000+id FROM generate_series(60000,65534) id");
            Sql("INSERT INTO revora_house_maps(uid,map_id) SELECT 1000000+id,id FROM generate_series(60000,65534) id WHERE NOT EXISTS(SELECT 1 FROM revora_house_maps h WHERE h.map_id=id)");
            Reject(() => Reserve(200002), "full durable map range rejects allocation without reuse");
            Check(Reserve(1002) == first, "existing owner remains available when capacity is exhausted");
            Console.WriteLine("House map registry checks passed: " + checks); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("House map fixture failed: " + ex.GetType().Name); return 1; }
        finally
        {
            if (original != null) DataHolder.ConnectionString = original;
            if (schema) Sql("DROP SCHEMA revora_house_validation CASCADE");
        }
    }
}
