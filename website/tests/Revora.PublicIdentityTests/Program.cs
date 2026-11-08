using System.Security.Cryptography;
using Revora.Persistence.PostgreSql;
using Revora.Web;

if (args.Length == 2 && args[0] == "--verify-existing")
{
    string? existingUsername = Console.ReadLine(); string? existingPassword = Console.ReadLine();
    if (string.IsNullOrEmpty(existingUsername) || string.IsNullOrEmpty(existingPassword)) return 2;
    await using var gameVerifier = new PostgreSqlGameDataStore("/var/run/postgresql", "postgres", args[1]);
    Guid? verified = await gameVerifier.AuthenticateAsync(existingUsername, existingPassword, default);
    existingPassword = string.Empty;
    Console.WriteLine(verified is null ? "FAIL genuine Account verifier rejected the disposable public account" : "PASS genuine Account verifier accepted the disposable public account");
    return verified is null ? 1 : 0;
}
if (args.Length != 1) { Console.Error.WriteLine("Usage: Revora.PublicIdentityTests <database-name>"); return 2; }
string database = args[0];
string connection = $"Host=/var/run/postgresql;Database={database};Username=postgres;Options=-c role=revora_web";
using var tokens = new IdentityTokenService(Convert.ToBase64String(Enumerable.Repeat((byte)0x5a, 32).ToArray()));
await using var store = new WebAccountStore(connection);
var tests = new List<(string Name, Func<Task> Run)>();
string suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3));
string username = "u" + suffix;
const string initialPassword = "simple123";
const string changedPassword = "changed123";
Guid accountId = Guid.Empty;
IReadOnlyList<RecoveryCode> initialCodes = tokens.NewRecoveryCodes();

tests.Add(("public registration creates active Player account", async () =>
{
    RegistrationResult result = await store.RegisterAsync(username, initialPassword, initialCodes.Select(static code => code.Digest).ToArray(), default);
    Assert(result.Created && result.Status == "active"); accountId = result.AccountId;
    await using var verify = new Npgsql.NpgsqlConnection($"Host=/var/run/postgresql;Database={database};Username=postgres"); await verify.OpenAsync();
    await using var command = verify.CreateCommand(); command.CommandText = "SELECT status='active' AND public_registration AND normalized_username=lower(username) AND EXISTS(SELECT 1 FROM identity.account_roles r WHERE r.account_id=a.account_id AND r.role='Player') FROM identity.game_accounts a WHERE account_id=$1"; command.Parameters.AddWithValue(accountId);
    Assert(Convert.ToBoolean(await command.ExecuteScalarAsync()));
}));
tests.Add(("case-insensitive concurrent duplicate is atomic", async () =>
{
    IReadOnlyList<RecoveryCode> codesA = tokens.NewRecoveryCodes(); IReadOnlyList<RecoveryCode> codesB = tokens.NewRecoveryCodes();
    Task<RegistrationResult> first = store.RegisterAsync("x" + suffix, initialPassword, codesA.Select(static code => code.Digest).ToArray(), default);
    Task<RegistrationResult> second = store.RegisterAsync(("X" + suffix).ToUpperInvariant(), initialPassword, codesB.Select(static code => code.Digest).ToArray(), default);
    RegistrationResult[] outcomes = await Task.WhenAll(first, second); Assert(outcomes.Count(static outcome => outcome.Created) == 1);
}));
tests.Add(("website and genuine game account verifier share credentials", async () =>
{
    Assert((await store.AuthenticateAsync(username, initialPassword, default))?.AccountId == accountId);
    await using var game = new PostgreSqlGameDataStore("/var/run/postgresql", "postgres", database);
    Assert(await game.AuthenticateAsync(username, initialPassword, default) == accountId);
}));
tests.Add(("logout revokes the exact website session", async () =>
{
    WebAccountIdentity identity = (await store.AuthenticateAsync(username, initialPassword, default))!;
    byte[] digest = tokens.Digest(tokens.NewToken());
    await store.CreateSessionAsync(digest, identity, DateTimeOffset.UtcNow.AddHours(1), default);
    Assert(await store.ValidateSessionAsync(digest, identity.AccountId, identity.SecurityStamp, default));
    await store.RevokeSessionAsync(digest, default);
    Assert(!await store.ValidateSessionAsync(digest, identity.AccountId, identity.SecurityStamp, default));
}));
tests.Add(("character ownership is AccountId-based", async () =>
{
    await using var connectionCheck = new Npgsql.NpgsqlConnection($"Host=/var/run/postgresql;Database={database};Username=postgres"); await connectionCheck.OpenAsync();
    await using var command = connectionCheck.CreateCommand(); command.CommandText = "INSERT INTO game.characters(character_id,account_id,realm_id,name,map_id,x,y) VALUES($1,$2,'4d1e7aac-e046-48bd-88b9-642f7eafef83',$3,1002,430,378)"; command.Parameters.AddWithValue(Guid.NewGuid()); command.Parameters.AddWithValue(accountId); command.Parameters.AddWithValue("c" + suffix); await command.ExecuteNonQueryAsync();
}));
tests.Add(("recovery code is single use and reset revokes old password", async () =>
{
    string canonical = initialCodes[0].Plaintext.Replace("-", string.Empty, StringComparison.Ordinal);
    string authorization = tokens.NewToken(); string losingAuthorization = tokens.NewToken();
    bool[] consume = await Task.WhenAll(store.ConsumeRecoveryCodeAsync(username, tokens.Digest(canonical), tokens.Digest(authorization), DateTimeOffset.UtcNow.AddMinutes(5), default), store.ConsumeRecoveryCodeAsync(username, tokens.Digest(canonical), tokens.Digest(losingAuthorization), DateTimeOffset.UtcNow.AddMinutes(5), default));
    Assert(consume.Count(static value => value) == 1);
    string winningAuthorization = consume[0] ? authorization : losingAuthorization;
    WebAccountIdentity identity = (await store.AuthenticateAsync(username, initialPassword, default))!;
    await store.CreateSessionAsync(tokens.Digest(tokens.NewToken()), identity, DateTimeOffset.UtcNow.AddHours(1), default);
    await using (var gameBeforeReset = new PostgreSqlGameDataStore("/var/run/postgresql", "postgres", database)) _ = await gameBeforeReset.IssueHandoffAsync(accountId, TimeSpan.FromMinutes(5), default);
    Assert(await store.ResetPasswordAsync(tokens.Digest(winningAuthorization), changedPassword, default));
    Assert(await store.AuthenticateAsync(username, initialPassword, default) is null);
    Assert((await store.AuthenticateAsync(username, changedPassword, default))?.AccountId == accountId);
    await using var game = new PostgreSqlGameDataStore("/var/run/postgresql", "postgres", database);
    Assert(await game.AuthenticateAsync(username, initialPassword, default) is null);
    Assert(await game.AuthenticateAsync(username, changedPassword, default) == accountId);
    await using var check = new Npgsql.NpgsqlConnection($"Host=/var/run/postgresql;Database={database};Username=postgres"); await check.OpenAsync(); await using var command = check.CreateCommand(); command.CommandText = "SELECT NOT EXISTS(SELECT 1 FROM identity.web_sessions WHERE account_id=$1 AND revoked_at IS NULL) AND NOT EXISTS(SELECT 1 FROM identity.handoff_tokens WHERE account_id=$1 AND consumed_at IS NULL)"; command.Parameters.AddWithValue(accountId); Assert(Convert.ToBoolean(await command.ExecuteScalarAsync()));
}));
tests.Add(("password reset preserves immutable ownership", async () =>
{
    await using var connectionCheck = new Npgsql.NpgsqlConnection($"Host=/var/run/postgresql;Database={database};Username=postgres"); await connectionCheck.OpenAsync();
    await using var command = connectionCheck.CreateCommand(); command.CommandText = "SELECT a.account_id=c.account_id FROM identity.game_accounts a JOIN game.characters c ON c.account_id=a.account_id WHERE a.normalized_username=$1"; command.Parameters.AddWithValue(username.ToLowerInvariant());
    Assert(Convert.ToBoolean(await command.ExecuteScalarAsync()));
}));
tests.Add(("questions alone create pending recovery and cannot reset", async () =>
{
    var answers = new[] { ((short)1, "private blue game"), ((short)2, "quiet hero"), ((short)3, "family word") };
    await store.ConfigureQuestionsAsync(accountId, answers, default);
    string pending = tokens.NewToken(); QuestionRecoveryResult result = await store.CreateQuestionRecoveryAsync(username, answers.ToDictionary(static value => value.Item1, static value => value.Item2), tokens.Digest("forged-device"), tokens.NetworkDigest(System.Net.IPAddress.Parse("203.0.113.55")), tokens.Digest(pending), default);
    Assert(result.Created && result.SupportCategory == "insufficient");
    Assert(!await store.RedeemPendingRecoveryAsync(tokens.Digest(pending), tokens.Digest(tokens.NewToken()), DateTimeOffset.UtcNow.AddMinutes(5), default));
    await using var check = new Npgsql.NpgsqlConnection($"Host=/var/run/postgresql;Database={database};Username=postgres"); await check.OpenAsync();
    await using var command = check.CreateCommand(); command.CommandText = "SELECT count(*)=3 AND bool_and(answer_hash<>ALL($2)) FROM identity.security_answers WHERE account_id=$1"; command.Parameters.AddWithValue(accountId); command.Parameters.AddWithValue(answers.Select(static answer => answer.Item2).ToArray());
    Assert(Convert.ToBoolean(await command.ExecuteScalarAsync()));
}));
tests.Add(("privacy retention removes expired device and network evidence", async () =>
{
    await store.RecordNetworkAsync(accountId, tokens.NetworkDigest(System.Net.IPAddress.Parse("198.51.100.24")), DateTimeOffset.UtcNow.AddMinutes(-1), default);
    byte[] expiredDevice = tokens.Digest("expired-test-device");
    await store.RecordDeviceAsync(accountId, expiredDevice, DateTimeOffset.UtcNow.AddDays(1), default);
    await using (var prepare = new Npgsql.NpgsqlConnection($"Host=/var/run/postgresql;Database={database};Username=postgres")) { await prepare.OpenAsync(); await using var expire = prepare.CreateCommand(); expire.CommandText = "UPDATE identity.recognized_devices SET revoked_at=clock_timestamp()-interval '31 days' WHERE token_digest=$1"; expire.Parameters.AddWithValue(expiredDevice); await expire.ExecuteNonQueryAsync(); }
    await store.CleanupExpiredAsync(default);
    await using var check = new Npgsql.NpgsqlConnection($"Host=/var/run/postgresql;Database={database};Username=postgres"); await check.OpenAsync();
    await using var command = check.CreateCommand(); command.CommandText = "SELECT NOT EXISTS(SELECT 1 FROM identity.network_history WHERE account_id=$1 AND expires_at<=clock_timestamp()) AND NOT EXISTS(SELECT 1 FROM identity.recognized_devices WHERE account_id=$1 AND expires_at<=clock_timestamp())"; command.Parameters.AddWithValue(accountId);
    Assert(Convert.ToBoolean(await command.ExecuteScalarAsync()));
}));

int failures = 0;
foreach ((string name, Func<Task> run) in tests) { try { await run(); Console.WriteLine($"PASS {name}"); } catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {name}: {exception.GetType().Name}: {exception.Message}"); } }
Console.WriteLine($"Revora.PublicIdentityTests: {tests.Count - failures}/{tests.Count} passed.");
return failures == 0 ? 0 : 1;

static void Assert(bool condition) { if (!condition) throw new InvalidOperationException("Assertion failed."); }
