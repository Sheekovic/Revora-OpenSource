using System;
using System.Reflection;
using Npgsql;
using Revora;
using Revora.Database;

internal static class CheckCandidateArenaStartup
{
    static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Candidate Arena assertion failed."); }
    static int Main()
    {
        string password = Environment.GetEnvironmentVariable("REVORA_DATABASE_PASSWORD");
        Environment.SetEnvironmentVariable("REVORA_DATABASE_PASSWORD", null);
        string phase = "identity";
        try
        {
            Require(!string.IsNullOrEmpty(password));
            var options = new NpgsqlConnectionStringBuilder { Host="127.0.0.1", Port=25439,
                Database="revora_candidate_game", Username="revora_candidate_runtime", Password=password,
                Pooling=false, Timeout=5, CommandTimeout=15 };
            DataHolder.ConnectionString = options.ConnectionString;
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = new NpgsqlCommand(@"SELECT current_user='revora_candidate_runtime'
AND current_database()='revora_candidate_game' AND inet_server_addr()='127.0.0.1'::inet
AND inet_server_port()=25439 AND NOT EXISTS(SELECT 1 FROM entities)
AND NOT EXISTS(SELECT 1 FROM arena) AND NOT EXISTS(SELECT 1 FROM teamarena)
AND NOT (SELECT rolsuper FROM pg_roles WHERE rolname=current_user)", c)) Require((bool)q.ExecuteScalar());
            }
            var startup = typeof(Server).Assembly.GetType("Revora.Game.Features.ArenaStartupPreparation", true);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            phase = "catalog-preflight";
            Require(ConquerItemInformation.BaseInformations == null);
            bool missingCatalogRejected = false;
            try { startup.GetMethod("Prepare", flags).Invoke(null, new object[] { 2 }); }
            catch (TargetInvocationException ex) { missingCatalogRejected = ex.InnerException is InvalidOperationException; }
            Require(missingCatalogRejected && !(bool)startup.GetProperty("Prepared", flags).GetValue(null, null));
            phase = "catalog-load";
            Constants.ServerName = "Slayer";
            ConquerItemInformation.Load();
            phase = "worker-preflight";
            // A sentinel exercises the composition guard without launching pools.
            Server.Thread = (Revora.Thread)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
            bool workersRejected = false;
            try { startup.GetMethod("Prepare", flags).Invoke(null, new object[] { 2 }); }
            catch (TargetInvocationException ex) { workersRejected = ex.InnerException is InvalidOperationException; }
            finally { Server.Thread = null; }
            Require(workersRejected && !(bool)startup.GetProperty("Prepared", flags).GetValue(null, null));
            phase = "prepare";
            startup.GetMethod("Prepare", flags).Invoke(null, new object[] { 2 });
            Require((bool)startup.GetProperty("Prepared", flags).GetValue(null, null));
            phase = "repeat-prepare";
            startup.GetMethod("Prepare", flags).Invoke(null, new object[] { 2 });
            Require((bool)startup.GetProperty("Prepared", flags).GetValue(null, null));
            phase = "cache-load";
            ArenaTable.Load(); TeamArenaTable.Load();
            Require(Revora.Game.Qualifier.ArenaStatistics.Count == 0 && Revora.Game.TeamQualifier.ArenaStatistics.Count == 0);
            Require(Kernel.GamePool.Count == 0 && Server.AuthServer == null && Server.GameServer == null);
            System.Console.WriteLine("Missing catalog and existing worker owner rejected; catalog-first cold Arena preparation, repeated preparation and both cache loaders passed; no players or listeners started.");
            return 0;
        }
        catch (Exception ex)
        {
            var cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            System.Console.Error.WriteLine("Candidate Arena phase=" + phase + " error=" + cause.GetType().Name);
            var frame = new System.Diagnostics.StackTrace(cause, false).GetFrame(0);
            if (frame != null) System.Console.Error.WriteLine("Failure method=" + frame.GetMethod().DeclaringType.FullName + "." + frame.GetMethod().Name);
            return 1;
        }
        finally { DataHolder.ConnectionString = ""; password = null; NpgsqlConnection.ClearAllPools(); }
    }
}
