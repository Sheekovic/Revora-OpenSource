using System;
using Npgsql;
using Revora.Database;

internal static class ConnectionConfigTests
{
    private static int checks;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks++;
    }

    private static void Main()
    {
        // Synthetic values only. No database connection is opened.
        foreach (string password in new[] { "", "plain", "semi;Host=other;Search Path=public", "quote\";Database=other", " space ' quote " })
        {
            DataHolder.CreateConnection("candidate_user", password, "candidate_db", "127.0.0.1;Port=25433");
            var parsed = new NpgsqlConnectionStringBuilder(DataHolder.ConnectionString);
            Check((parsed.Password ?? "") == password, "Password must round-trip unchanged.");
            Check(parsed.Host == "127.0.0.1" && parsed.Port == 25433 && parsed.Database == "candidate_db",
                "Credential punctuation must not change the database endpoint.");
            Check(string.IsNullOrEmpty(parsed.SearchPath), "Password must not introduce a search path.");
        }
        DataHolder.CreateConnection("user;Host=other", null, "db;Search Path=public", null);
        var defaults = new NpgsqlConnectionStringBuilder(DataHolder.ConnectionString);
        Check(defaults.Username == "user;Host=other" && defaults.Database == "db;Search Path=public",
            "Username and database must remain literal values.");
        Check(string.IsNullOrEmpty(defaults.Password) && defaults.Host == "127.0.0.1" && defaults.Port == 25432,
            "Legacy null/default values must be retained.");
        Check(defaults.Pooling && defaults.MaxPoolSize == 50 && defaults.MinPoolSize == 1,
            "Existing pool policy must be retained.");
        DataHolder.CreateConnection("user", "synthetic", "candidate_db", "localhost; port=25434 ");
        Check(new NpgsqlConnectionStringBuilder(DataHolder.ConnectionString).Port == 25434,
            "Existing case-insensitive host port syntax must be retained.");
        Console.WriteLine("Connection configuration checks passed: " + checks);
    }
}
