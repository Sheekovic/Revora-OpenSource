using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using Npgsql;
using IniFile = Revora.IniFile;
using Revora.Database;

internal static class PrepareMoonBoxSchema
{
    private const string Root = @"C:\Revora\DisposablePostgres\moon-box-candidate";
    private const string Dump = @"C:\Revora\DisposablePostgres\tools-17.11-4\pgsql\bin\pg_dump.exe";
    private static int Main(string[] args)
    {
        string stage = "preflight";
        try
        {
            bool catalogs = args.Length == 1 && args[0] == "--catalogs";
            if (args.Length != 0 && !catalogs) throw new InvalidOperationException();
            // Reviewed static definitions only. In particular furniture, status,
            // itemadding and prizenpc contain ownership/state and are excluded.
            string[] catalogTables = { "configuration", "maps", "monsterinfos", "monsterspawns", "npcs", "sobnpcs", "spells", "refinery", "refineryboxes", "databaserefinery", "databaserefineryboxes", "titles", "lottery", "lotteryslayer", "premioeventos" };
            string archive = Path.Combine(Root, catalogs ? "game-catalogs.dump" : "game-schema.dump");
            string inventory = Path.Combine(Root, "source-schema-columns.tsv");
            if (!File.Exists(Path.Combine(Root, "data", "PG_VERSION")) || File.Exists(archive) || (!catalogs && File.Exists(inventory)))
                throw new InvalidOperationException();
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(Dump))
                if (BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") != "B3A92C4138D453989FBA75D98BB5448685FDDC018E1B1C47339B864FEE765407")
                    throw new InvalidOperationException();
            var ini = new IniFile("Configuracion.ini");
            string host = ini.ReadString("MySql", "Host"), port = ini.ReadString("MySql", "Port");
            if (!string.IsNullOrEmpty(port) && host.IndexOf("Port=", StringComparison.OrdinalIgnoreCase) < 0)
                host = host.Trim().TrimEnd(';') + ";Port=" + port;
            DataHolder.CreateConnection(ini.ReadString("MySql", "Username"), ini.ReadString("MySql", "Password"), ini.ReadString("MySql", "Database"), host);
            var source = new NpgsqlConnectionStringBuilder(DataHolder.ConnectionString);
            IPAddress address;
            if (!IPAddress.TryParse(source.Host, out address) || !IPAddress.IsLoopback(address) || source.Port == 25439)
                throw new InvalidOperationException();
            source.Timeout = 5;
            source.CommandTimeout = 30;
            source.Pooling = false;
            stage = "read-only metadata";
            int tables;
            using (var connection = new NpgsqlConnection(source.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction)) readOnly.ExecuteNonQuery();
                    using (var foreign = new NpgsqlCommand("SELECT count(*) FROM pg_foreign_table f JOIN pg_class c ON c.oid=f.ftrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public'", connection, transaction))
                        if (Convert.ToInt64(foreign.ExecuteScalar()) != 0) throw new InvalidOperationException();
                    using (var count = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'", connection, transaction))
                        tables = Convert.ToInt32(count.ExecuteScalar());
                    if (!catalogs)
                    using (var command = new NpgsqlCommand("SELECT table_name,column_name,data_type FROM information_schema.columns WHERE table_schema='public' ORDER BY table_name,ordinal_position", connection, transaction))
                    using (var reader = command.ExecuteReader())
                    using (var writer = new StreamWriter(new FileStream(inventory, FileMode.CreateNew, FileAccess.Write)))
                    {
                        writer.WriteLine("table\tcolumn\ttype");
                        while (reader.Read()) writer.WriteLine(reader.GetString(0) + "\t" + reader.GetString(1) + "\t" + reader.GetString(2));
                    }
                    transaction.Rollback();
                }
            }
            stage = catalogs ? "allowlisted catalog export" : "schema-only export";
            string selection = "--schema-only --schema=public";
            if (catalogs)
            {
                selection = "--data-only --strict-names";
                foreach (string table in catalogTables) selection += " --table=public." + table;
            }
            var start = new ProcessStartInfo(Dump,
                selection + " --no-owner --no-privileges --no-security-labels --no-tablespaces --no-password --format=custom --file=\"" + archive + "\"") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.EnvironmentVariables["PGHOST"] = source.Host;
            start.EnvironmentVariables["PGPORT"] = source.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            start.EnvironmentVariables["PGDATABASE"] = source.Database;
            start.EnvironmentVariables["PGUSER"] = source.Username;
            start.EnvironmentVariables["PGPASSWORD"] = source.Password ?? "";
            start.EnvironmentVariables["PGCONNECT_TIMEOUT"] = "5";
            start.EnvironmentVariables["PGOPTIONS"] = "-c default_transaction_read_only=on -c statement_timeout=30000 -c lock_timeout=5000";
            using (var process = Process.Start(start))
            {
                start.EnvironmentVariables.Remove("PGPASSWORD");
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000)) { process.Kill(); throw new TimeoutException(); }
                output.GetAwaiter().GetResult(); error.GetAwaiter().GetResult();
                if (process.ExitCode != 0) throw new InvalidOperationException();
            }
            Console.WriteLine("Read-only " + (catalogs ? "catalog" : "public schema") + " export complete; tables: " + (catalogs ? catalogTables.Length : tables) + "; archive bytes: " + new FileInfo(archive).Length);
            Console.WriteLine(catalogs ? "Only allowlisted static tables exported; no account or character records. Candidate remains stopped." : "No table rows exported; candidate remains stopped. Review schema before restoring.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Schema preparation failed at " + stage + " (" + error.GetType().Name + "). Inspect staged files before retrying; connection details suppressed.");
            return 1;
        }
        finally { DataHolder.ConnectionString = null; }
    }
}
