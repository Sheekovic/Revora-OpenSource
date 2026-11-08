using System;
using Npgsql;

internal static class ProvisionCandidateRuntime
{
    static NpgsqlConnection Open(string user, string password)
    {
        var options = new NpgsqlConnectionStringBuilder {
            Host = "127.0.0.1", Port = 25439, Database = "revora_candidate_game",
            Username = user, Password = password, Pooling = false, Timeout = 5, CommandTimeout = 15
        };
        var connection = new NpgsqlConnection(options.ConnectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }
    static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Candidate runtime validation failed."); }
    static int Main(string[] args)
    {
        string password = Environment.GetEnvironmentVariable("REVORA_DATABASE_PASSWORD");
        string bootstrap = Environment.GetEnvironmentVariable("REVORA_CANDIDATE_BOOTSTRAP_PASSWORD");
        Environment.SetEnvironmentVariable("REVORA_DATABASE_PASSWORD", null);
        Environment.SetEnvironmentVariable("REVORA_CANDIDATE_BOOTSTRAP_PASSWORD", null);
        try
        {
            bool create = args.Length == 1 && args[0] == "--create";
            Require(create || (args.Length == 1 && args[0] == "--verify"));
            Require(!string.IsNullOrEmpty(password));
            if (create)
            using (var connection = Open("revora_candidate_admin", bootstrap))
            using (var transaction = connection.BeginTransaction())
            {
                using (var identity = new NpgsqlCommand("SELECT current_setting('data_directory')='C:/Revora/DisposablePostgres/moon-box-candidate/data' AND inet_server_port()=25439 AND NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='revora_candidate_runtime')", connection, transaction))
                    Require((bool)identity.ExecuteScalar());
                using (var role = new NpgsqlCommand("CREATE ROLE revora_candidate_runtime LOGIN INHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION CONNECTION LIMIT 20 IN ROLE revora_candidate_game", connection, transaction)) role.ExecuteNonQuery();
                // Bind the password as data. The fixed server-side block uses %L
                // quoting because ALTER ROLE does not accept a bind parameter.
                using (var setting = new NpgsqlCommand("SELECT set_config('revora.candidate_password',@password,true)", connection, transaction))
                { setting.Parameters.AddWithValue("password", password); setting.ExecuteNonQuery(); }
                using (var credential = new NpgsqlCommand("DO $$ BEGIN EXECUTE format('ALTER ROLE revora_candidate_runtime PASSWORD %L',current_setting('revora.candidate_password')); END $$", connection, transaction)) credential.ExecuteNonQuery();
                transaction.Commit();
            }
            using (var connection = Open("revora_candidate_runtime", password))
            {
                using (var identity = new NpgsqlCommand("SELECT current_user='revora_candidate_runtime' AND current_database()='revora_candidate_game' AND inet_server_addr()='127.0.0.1'::inet AND inet_server_port()=25439 AND EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication) AND pg_has_role(current_user,'revora_candidate_game','USAGE') AND NOT pg_has_role(current_user,'revora_candidate_admin','MEMBER')", connection))
                    Require((bool)identity.ExecuteScalar());
                // Verify inherited ownership permits the real startup's DDL and
                // catalog DML. Every probe change rolls back.
                using (var transaction = connection.BeginTransaction())
                {
                    using (var write = new NpgsqlCommand("CREATE TABLE revora_candidate_privilege_probe(value integer); INSERT INTO revora_candidate_privilege_probe VALUES(1); ALTER TABLE accounts ADD COLUMN revora_candidate_privilege_probe integer; UPDATE configuration SET experiencerate=experiencerate WHERE false", connection, transaction)) write.ExecuteNonQuery();
                    using (var read = new NpgsqlCommand("SELECT count(*)=1 FROM revora_candidate_privilege_probe", connection, transaction)) Require((bool)read.ExecuteScalar());
                    transaction.Rollback();
                }
                bool denied = false;
                try { using (var elevate = new NpgsqlCommand("SET ROLE revora_candidate_admin", connection)) elevate.ExecuteNonQuery(); }
                catch (PostgresException error) { if (error.SqlState != "42501") throw; denied = true; }
                Require(denied);
                using (var clean = new NpgsqlCommand("SELECT to_regclass('public.revora_candidate_privilege_probe') IS NULL AND NOT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='accounts' AND column_name='revora_candidate_privilege_probe')", connection)) Require((bool)clean.ExecuteScalar());
            }
            bool wrongRejected = false;
            try { using (var wrong = Open("revora_candidate_runtime", Guid.NewGuid().ToString("N"))) { } }
            catch (PostgresException error) { if (error.SqlState != "28P01") throw; wrongRejected = true; }
            Require(wrongRejected);
            Console.WriteLine("Candidate runtime login verified: authentication, catalog access, transactional DDL, rollback and denied bootstrap role.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("Candidate runtime setup/check failed (" + error.GetType().Name + "); raw database details suppressed."); return 1; }
        finally { password = bootstrap = null; }
    }
}
