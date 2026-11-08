using System.Buffers.Binary;
using System.Security.Cryptography;
using Npgsql;
using Revora.Admin;
using Revora.Application.Authentication;
using Revora.Persistence.PostgreSql;

var tests = new List<(string Name, Func<Task> Run)>
{
    ("Adaptive password round trip", PasswordRoundTrip),
    ("Adaptive password rejects wrong and malformed values", PasswordRejects),
    ("TOTP accepts current and rejects invalid code", TotpRoundTrip),
    ("Admin configuration fails closed", OptionsFailClosed),
    ("Admin configuration rejects public binding", OptionsRejectPublic),
    ("Admin database role reaches reviewed status only", DatabaseStatus),
};

int failed = 0;
foreach ((string name, Func<Task> run) in tests)
{
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception exception) { failed++; Console.Error.WriteLine($"FAIL {name}: {exception.Message}"); }
}

Console.WriteLine($"Revora.AdminTests: {tests.Count - failed}/{tests.Count} passed.");
return failed == 0 ? 0 : 1;

static Task PasswordRoundTrip()
{
    var hasher = new AdaptivePasswordHasher();
    string encoded = hasher.Hash("Correct-Horse-Revora-2026");
    Assert(!encoded.Contains("Correct-Horse", StringComparison.Ordinal), "Hash contains password material.");
    Assert(hasher.Verify("Correct-Horse-Revora-2026", encoded) == PasswordVerification.Valid, "Valid password was rejected.");
    return Task.CompletedTask;
}

static Task PasswordRejects()
{
    var hasher = new AdaptivePasswordHasher();
    string encoded = hasher.Hash("Correct-Horse-Revora-2026");
    Assert(hasher.Verify("Incorrect-Horse-Revora-2026", encoded) == PasswordVerification.Failed, "Wrong password was accepted.");
    Assert(hasher.Verify("anything", "malformed") == PasswordVerification.Failed, "Malformed hash was accepted.");
    return Task.CompletedTask;
}

static Task TotpRoundTrip()
{
    var time = new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
    var service = new TotpService(time);
    byte[] secret = Enumerable.Range(1, 20).Select(x => (byte)x).ToArray();
    string code = Code(secret, time.GetUtcNow().ToUnixTimeSeconds() / 30).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    Assert(service.Verify(secret, code), "Current TOTP code was rejected.");
    Assert(!service.Verify(secret, "00000x"), "Malformed TOTP code was accepted.");
    return Task.CompletedTask;
}

static Task OptionsFailClosed()
{
    Throws(() => new AdminOptions().Validate());
    return Task.CompletedTask;
}

static Task OptionsRejectPublic()
{
    Throws(() => new AdminOptions { RuntimeMode = "Production", Url = "http://0.0.0.0:5081" }.Validate());
    return Task.CompletedTask;
}

static async Task DatabaseStatus()
{
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = Environment.GetEnvironmentVariable("REVORA_TEST_DB_SOCKET") ?? "/var/run/postgresql",
        Database = Environment.GetEnvironmentVariable("REVORA_TEST_ADMIN_DB") ?? "revora",
        Username = Environment.GetEnvironmentVariable("REVORA_TEST_ADMIN_USER") ?? "revora-admin",
        Pooling = false
    };
    await using var store = new AdminDataStore(builder.ConnectionString);
    Assert(await store.CanConnectAsync(CancellationToken.None), "Database readiness failed.");
    string status = await store.GetOperationalStatusAsync(CancellationToken.None);
    string migration = status.Split(';', StringSplitOptions.RemoveEmptyEntries).Single(part => part.StartsWith("migration=", StringComparison.Ordinal));
    Assert(int.TryParse(migration.AsSpan("migration=".Length), out int version) && version >= 3 && status.Contains("backup=success", StringComparison.Ordinal), "Database status did not report the reviewed migration and backup state.");
}

static int Code(byte[] secret, long counter)
{
    Span<byte> bytes = stackalloc byte[8];
    BinaryPrimitives.WriteInt64BigEndian(bytes, counter);
    byte[] digest = HMACSHA1.HashData(secret, bytes);
    int offset = digest[^1] & 0xf;
    return ((((digest[offset] & 0x7f) << 24) | (digest[offset + 1] << 16) | (digest[offset + 2] << 8) | digest[offset + 3]) % 1_000_000);
}

static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Throws(Action action) { try { action(); } catch (Exception) { return; } throw new InvalidOperationException("Expected failure was not raised."); }
sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
