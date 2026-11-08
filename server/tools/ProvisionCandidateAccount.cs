using System;
using Npgsql;
using Revora.Database;

internal static class ProvisionCandidateAccount
{
    static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Candidate account check failed."); }
    static int Main(string[] args)
    {
        string databasePassword = Environment.GetEnvironmentVariable("REVORA_DATABASE_PASSWORD");
        string accountPassword = Environment.GetEnvironmentVariable("REVORA_CANDIDATE_ACCOUNT_PASSWORD");
        Environment.SetEnvironmentVariable("REVORA_DATABASE_PASSWORD", null);
        Environment.SetEnvironmentVariable("REVORA_CANDIDATE_ACCOUNT_PASSWORD", null);
        try
        {
            bool create = args.Length == 1 && args[0] == "--create";
            Require(create || (args.Length == 1 && args[0] == "--verify"));
            Require(!string.IsNullOrEmpty(databasePassword) && !string.IsNullOrEmpty(accountPassword));
            var options = new NpgsqlConnectionStringBuilder {
                Host = "127.0.0.1", Port = 25439, Database = "revora_candidate_game",
                Username = "revora_candidate_runtime", Password = databasePassword,
                Pooling = false, Timeout = 5, CommandTimeout = 15
            };
            DataHolder.ConnectionString = options.ConnectionString;
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open();
                using (var identity = new NpgsqlCommand("SELECT current_user='revora_candidate_runtime' AND current_database()='revora_candidate_game' AND inet_server_addr()='127.0.0.1'::inet AND inet_server_port()=25439 AND NOT EXISTS(SELECT 1 FROM entities)", connection))
                    Require((bool)identity.ExecuteScalar());
                if (create)
                {
                    using (var empty = new NpgsqlCommand("SELECT NOT EXISTS(SELECT 1 FROM accounts)", connection)) Require((bool)empty.ExecuteScalar());
                    var account = new AccountTable("MoonBoxTester") { Password = accountPassword, State = AccountTable.AccountState.Player };
                    account.Insert();
                    Require(account.HasSrpVerifier && account.Password == "");
                }
                using (var stored = new NpgsqlCommand("SELECT (SELECT count(*) FROM accounts)=1 AND EXISTS(SELECT 1 FROM accounts a JOIN revora_account_srp s ON s.username=a.username WHERE a.username='MoonBoxTester' AND a.state=2 AND a.entityid=0 AND a.password='!revora-srp6711-v1!' AND octet_length(s.salt)=32 AND octet_length(s.verifier)=32)", connection))
                    Require((bool)stored.ExecuteScalar());
            }
            var reloaded = new AccountTable("moonboxtester");
            Require(reloaded.exists && reloaded.State == AccountTable.AccountState.Player && reloaded.EntityID == 0 && reloaded.Password == "" && reloaded.HasSrpVerifier);
            var exchange = reloaded.BeginSrp(reloaded.Username);
            byte[] a;
            byte[] proof = SrpVerifierTests.Proof(exchange.CreateChallenge(), reloaded.Username, accountPassword, out a);
            Require(exchange.TryVerify(a, proof));
            Require(!exchange.TryVerify(a, proof));
            Array.Clear(proof, 0, proof.Length);
            var wrong = reloaded.BeginSrp(reloaded.Username);
            proof = SrpVerifierTests.Proof(wrong.CreateChallenge(), reloaded.Username, Guid.NewGuid().ToString("N"), out a);
            Require(!wrong.TryVerify(a, proof));
            Array.Clear(proof, 0, proof.Length);
            Console.WriteLine("Ordinary candidate account verified: stored verifier, fresh reload, valid proof, rejected password and replay. No character created.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("Candidate account preparation failed (" + error.GetType().Name + "); raw database details suppressed."); return 1; }
        finally { databasePassword = accountPassword = null; DataHolder.ConnectionString = null; }
    }
}
