using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using Revora.Application.Authentication;
using Revora.Domain.Accounts;
using Revora.Gameplay.Models;
using Revora.Gameplay.Services;
using Revora.Host.Hosting;
using Revora.Host.Gameplay;
using Revora.Host.Logging;
using Revora.NativeLogin.Core;
using Revora.Persistence.PostgreSql;
using Revora.Protocol5165;

if (args is ["--economy-only", _, _])
{
    await GenuineEncryptedItemEconomyAsync(Path.GetFullPath(args[1]), args[2],
        Environment.GetEnvironmentVariable("REVORA_TEST_DB_USER") ?? "revora-test");
    return 0;
}

if (args is ["--combat-only", _, _])
{
    await GenuineEncryptedCombatAsync(args[1], args[2]);
    return 0;
}

if (args is ["--ninja-combat-only", _, _])
{
    await GenuineEncryptedNinjaCombatAsync(args[1], args[2]);
    return 0;
}

if (args is ["--disabled-combat-host-only", _, _, _, _])
{
    await ProductionShapedDisabledCombatHostAsync(Path.GetFullPath(args[1]), args[2], args[3],
        Path.GetFullPath(args[4]));
    return 0;
}

if (args.Length != 2) throw new ArgumentException("Usage: Revora.PlayableTests <Revora.Host> <temporary-database>");
string hostExecutable = Path.GetFullPath(args[0]);
string databaseName = args[1];
string databaseUsername = Environment.GetEnvironmentVariable("REVORA_TEST_DB_USER") ?? "revora-test";
string canary = "Synthetic-Password-Canary-2026!";
string exactPasswordCanary = "exactpass123";
int accountPort = FreePort();
int gamePort = FreePort();
while (gamePort == accountPort) gamePort = FreePort();
string work = Path.Combine(Path.GetTempPath(), $"revora-playable-{Guid.NewGuid():N}");
Directory.CreateDirectory(Path.Combine(work, "config"));
Directory.CreateDirectory(Path.Combine(work, "data"));
Directory.CreateDirectory(Path.Combine(work, "logs"));
File.WriteAllText(Path.Combine(work, "config", "revora.json"), JsonSerializer.Serialize(new
{
    InstanceName = "Synthetic validation",
    RealmName = "Revora Development",
    Environment = "Test",
    RuntimeAdapter = "PostgreSqlSyntheticTest",
    AccountListener = new { Address = "127.0.0.1", Port = accountPort },
    GameListener = new { Address = "127.0.0.1", Port = gamePort },
    MaximumPlayers = 8,
    ShutdownTimeoutSeconds = 10,
    SaveIntervalSeconds = 300,
    RegistrationEnabled = false,
    MaintenanceMode = true,
    ApplyMigrations = false,
    SecretFile = "config/revora.secrets.json",
}));
string secretPath = Path.Combine(work, "config", "revora.secrets.json");
File.WriteAllText(secretPath, JsonSerializer.Serialize(new { DatabaseMode = "LocalPeerSocket", PostgreSqlSocketPath = "/var/run/postgresql", PostgreSqlUsername = databaseUsername, PostgreSqlDatabase = databaseName }));
if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(secretPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

var databaseBuilder = new NpgsqlConnectionStringBuilder { Host = "/var/run/postgresql", Database = databaseName, Username = databaseUsername, Pooling = false };
var hasher = new AdaptivePasswordHasher();
Guid accountA = Guid.NewGuid(), accountB = Guid.NewGuid(), accountC = Guid.NewGuid(), accountD = Guid.NewGuid(),
    accountE = Guid.NewGuid(), accountF = Guid.NewGuid(), accountG = Guid.NewGuid(), accountH = Guid.NewGuid();
await using (var connection = new NpgsqlConnection(databaseBuilder.ConnectionString))
{
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("INSERT INTO identity.game_accounts(account_id,username,normalized_username,password_hash,password_algorithm,password_version) VALUES($1,$2,lower($2),$3,'pbkdf2-sha512',1),($4,$5,lower($5),$6,'pbkdf2-sha512',1),($7,$8,lower($8),$9,'pbkdf2-sha512',1),($10,$11,lower($11),$12,'pbkdf2-sha512',1)", connection);
    command.Parameters.AddWithValue(accountA); command.Parameters.AddWithValue("synthetic_a"); command.Parameters.AddWithValue(hasher.Hash(canary));
    command.Parameters.AddWithValue(accountB); command.Parameters.AddWithValue("synthetic_b"); command.Parameters.AddWithValue(hasher.Hash(canary));
    command.Parameters.AddWithValue(accountC); command.Parameters.AddWithValue("synthetic_c"); command.Parameters.AddWithValue(hasher.Hash(canary));
    command.Parameters.AddWithValue(accountD); command.Parameters.AddWithValue("exact5165d"); command.Parameters.AddWithValue(hasher.Hash(exactPasswordCanary));
    await command.ExecuteNonQueryAsync();
    await using var names = new NpgsqlCommand("INSERT INTO identity.game_accounts(account_id,username,normalized_username,password_hash,password_algorithm,password_version) VALUES($1,$2,lower($2),$3,'pbkdf2-sha512',1),($4,$5,lower($5),$6,'pbkdf2-sha512',1)", connection);
    names.Parameters.AddWithValue(accountE); names.Parameters.AddWithValue("exact5165e"); names.Parameters.AddWithValue(hasher.Hash(exactPasswordCanary));
    names.Parameters.AddWithValue(accountF); names.Parameters.AddWithValue("exact5165f"); names.Parameters.AddWithValue(hasher.Hash(exactPasswordCanary));
    await names.ExecuteNonQueryAsync();
    await using var gameplayAccounts = new NpgsqlCommand("INSERT INTO identity.game_accounts(account_id,username,normalized_username,password_hash,password_algorithm,password_version) VALUES($1,$2,lower($2),$3,'pbkdf2-sha512',1),($4,$5,lower($5),$6,'pbkdf2-sha512',1)", connection);
    gameplayAccounts.Parameters.AddWithValue(accountG); gameplayAccounts.Parameters.AddWithValue("gameplay5165g"); gameplayAccounts.Parameters.AddWithValue(hasher.Hash(exactPasswordCanary));
    gameplayAccounts.Parameters.AddWithValue(accountH); gameplayAccounts.Parameters.AddWithValue("gameplay5165h"); gameplayAccounts.Parameters.AddWithValue(hasher.Hash(exactPasswordCanary));
    await gameplayAccounts.ExecuteNonQueryAsync();
}

string characterSuffix = Guid.NewGuid().ToString("N")[..6];
await Genuine5165CharacterWorldAsync(databaseUsername, databaseName, accountD, "exact5165d", $"A{characterSuffix}", exactPasswordCanary);
await Genuine5165CharacterWorldAsync(databaseUsername, databaseName, accountE, "exact5165e", $"B{characterSuffix}", exactPasswordCanary);
await Genuine5165CharacterWorldAsync(databaseUsername, databaseName, accountF, "exact5165f", $"C{characterSuffix}", exactPasswordCanary);
await IdentityOwnershipInvariantsAsync(databaseUsername, databaseName, accountA, accountD, accountE, exactPasswordCanary);
await GameplayVerticalSliceAsync(databaseUsername, databaseName, accountG, accountH);
await GenuineEncryptedNpcGoodbyeAsync(databaseUsername, databaseName, accountG, "gameplay5165g", exactPasswordCanary);

await using (var durableStore = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName))
{
    ulong legacy = await durableStore.IssueLegacyHandoffAsync(accountA, TimeSpan.FromMinutes(1), default);
    Assert(await durableStore.ConsumeLegacyHandoffAsync(legacy, default) == accountA, "Legacy handoff was not consumed.");
    Assert(await durableStore.ConsumeLegacyHandoffAsync(legacy, default) is null, "Legacy handoff replay succeeded.");
    ulong expired = await durableStore.IssueLegacyHandoffAsync(accountA, TimeSpan.FromSeconds(-1), default);
    Assert(await durableStore.ConsumeLegacyHandoffAsync(expired, default) is null, "Expired legacy handoff succeeded.");

    Task<CharacterState?> firstCreate = durableStore.TryCreateCharacterAsync(accountC, "ConcurrentC", 1003, 10, default);
    Task<CharacterState?> secondCreate = durableStore.TryCreateCharacterAsync(accountC, "concurrentc", 1004, 20, default);
    int created = 0, rejected = 0;
    foreach (Task<CharacterState?> attempt in new[] { firstCreate, secondCreate })
    {
        if (await attempt is null) rejected++;
        else created++;
    }
    Assert(created == 1 && rejected == 1, "Concurrent character uniqueness was not atomic.");
    CharacterState stored = await durableStore.GetCharacterAsync(accountC, default) ?? throw new InvalidOperationException("Durable character missing.");
    Assert(stored.LegacyUid > 0 && stored.Mesh is 1003 or 1004 && stored.Profession is 10 or 20 && stored.Level == 1,
        "Durable 5165 character fields were not restored.");
}

var results = new List<string>();
results.Add("legacy handoff digest, replay, expiry, and atomic character uniqueness");
HostProcess host = await HostProcess.StartAsync(hostExecutable, work);
try
{
    string tokenA = await LoginAsync(accountPort, "synthetic_a", canary, fragmented: true); results.Add("fragmented durable login");
    string tokenB = await LoginAsync(accountPort, "synthetic_b", canary, fragmented: false); results.Add("second durable login");

    await AssertWrongRealmAsync(gamePort, tokenB); results.Add("wrong realm rejected");
    await AssertMalformedAsync(accountPort); results.Add("malformed length rejected");

    await using TestConnection gameA = await TestConnection.ConnectAsync(gamePort);
    await gameA.SendAsync(10, new { Token = tokenA, Realm = "Revora Development" });
    Frame authA = await gameA.ReadAsync(); Assert(authA.Type == 11 && Json(authA).GetProperty("Success").GetBoolean(), "Account A handoff failed.");
    await gameA.SendAsync(12, new { Name = "SyntheticA" }); Frame charA = await gameA.ReadAsync(); Assert(charA.Type == 13, "Character A creation failed.");
    Guid characterA = Json(charA).GetProperty("Id").GetGuid();
    await gameA.SendAsync(14, new { }); Assert((await gameA.ReadAsync()).Type == 15, "Character A entry failed.");

    await using TestConnection gameB = await TestConnection.ConnectAsync(gamePort);
    await gameB.SendAsync(10, new { Token = tokenB, Realm = "Revora Development" });
    Frame authB = await gameB.ReadAsync(); Assert(authB.Type == 11 && Json(authB).GetProperty("Success").GetBoolean(), "Account B handoff failed.");
    await gameB.SendAsync(12, new { Name = "SyntheticB" }); Assert((await gameB.ReadAsync()).Type == 13, "Character B creation failed.");
    await gameB.SendAsync(14, new { }); Assert((await gameB.ReadAsync()).Type == 15, "Character B entry failed.");
    Assert((await gameA.ReadAsync()).Type == 100, "A did not observe B enter range.");
    Assert((await gameB.ReadAsync()).Type == 100, "B did not receive existing nearby A.");
    results.Add("two-client enter-range visibility");

    await gameA.SendCoalescedAsync((16, new { X = 431, Y = 378, Direction = 2 }), (18, new { Text = "synthetic hello" }));
    Frame movedA = await gameA.ReadAsync(); Assert(movedA.Type == 17 && Json(movedA).GetProperty("X").GetInt32() == 431, "Authoritative move failed.");
    Frame observedMove = await gameB.ReadAsync(); Assert(observedMove.Type == 101, "B did not observe A movement.");
    Frame chatA = await gameA.ReadAsync(); Frame chatB = await gameB.ReadAsync(); Assert(chatA.Type == 102 && chatB.Type == 102, "Local chat broadcast failed.");
    results.Add("movement, coalescing, and local chat");

    for (int index = 0; index < 100; index++)
    {
        int x = index % 2 == 0 ? 430 : 431;
        await gameA.SendAsync(16, new { X = x, Y = 378, Direction = (short)(index % 8) });
        Assert((await gameA.ReadAsync()).Type == 17, "Soak movement response missing.");
        Assert((await gameB.ReadAsync()).Type == 101, "Soak visibility response missing.");
    }
    results.Add("bounded 100-move soak");

    gameB.Abort();
    Assert((await gameA.ReadAsync()).Type == 103, "A did not observe B disconnect.");
    results.Add("abrupt disconnect cleanup");

    await AssertReplayAsync(gamePort, tokenA); results.Add("single-use handoff replay rejected");

    await host.StopAsync();
    Assert(!host.Output.Contains(canary, StringComparison.Ordinal), "Credential canary leaked into Host output.");
    host = await HostProcess.StartAsync(hostExecutable, work);
    string reconnectToken = await LoginAsync(accountPort, "synthetic_a", canary, fragmented: false);
    await using TestConnection reconnect = await TestConnection.ConnectAsync(gamePort);
    await reconnect.SendAsync(10, new { Token = reconnectToken, Realm = "Revora Development" });
    Frame reconnectResult = await reconnect.ReadAsync();
    JsonElement persisted = Json(reconnectResult).GetProperty("Character");
    Assert(persisted.GetProperty("Id").GetGuid() == characterA && persisted.GetProperty("X").GetInt32() is 430 or 431, "Persisted character state was not restored.");
    results.Add("reconnect persistence");

    await host.StopAsync();
    Assert(host.Output.Split('\n').Count(line => line == "[Revora] READY") == 1, "READY marker count was not one for the final cycle.");
    Assert(host.Output.Split('\n').Count(line => line == "[Revora] STOPPED") == 1, "STOPPED marker count was not one for the final cycle.");
    results.Add("clean lifecycle markers");
}
finally
{
    await host.DisposeAsync();
    Directory.Delete(work, recursive: true);
}

foreach (string result in results) Console.WriteLine($"PASS {result}");
Console.WriteLine($"Revora.PlayableTests: {results.Count + 5}/{results.Count + 5} passed (three exact-client-derived named world paths, one immutable-identity suite, one gameplay vertical slice, and {results.Count} lifecycle checks).");
return 0;

static async Task GameplayVerticalSliceAsync(string databaseUsername, string databaseName, Guid firstAccount, Guid secondAccount)
{
    await using var store = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName);
    CharacterState first = await store.CreateCharacterAsync(firstAccount, "MoverOne", 1003, 100, default);
    CharacterState second = await store.CreateCharacterAsync(secondAccount, "MoverTwo", 1004, 40, default);
    string connectionString = PostgreSqlEconomyRuntime.BuildLocalPeerConnectionString(
        "/var/run/postgresql", databaseUsername, databaseName);
    await using (var connection = new NpgsqlConnection(connectionString))
    {
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync("SET LOCAL ROLE revora_migrator");
        int ordinal = 0;
        foreach ((CharacterState owner, uint itemType, EquipmentSlot slot) in new[]
        {
            (first, 118109u, EquipmentSlot.Headwear), (first, 181305u, EquipmentSlot.Garment),
            (first, 130109u, EquipmentSlot.Armor), (first, 410339u, EquipmentSlot.RightHand),
            (first, 420339u, EquipmentSlot.LeftHand),
            (second, 123109u, EquipmentSlot.Headwear), (second, 181315u, EquipmentSlot.Garment),
            (second, 135109u, EquipmentSlot.Armor), (second, 601301u, EquipmentSlot.RightHand),
            (second, 601301u, EquipmentSlot.LeftHand),
        })
        {
            Guid itemId = Guid.NewGuid();
            int legacyItemUid = checked(800_000_000 + ordinal++);
            await ExecuteAsync(
                "INSERT INTO game.item_instances(item_instance_id,legacy_item_uid,owner_character_id,item_source_id,item_type,color) " +
                "SELECT $1,$2,$3,source_id,item_type,$4 FROM gameplay_reference.item_types WHERE item_type=$5 " +
                "ORDER BY source_id LIMIT 1",
                itemId, legacyItemUid, owner.Id, 3, checked((long)itemType));
            await ExecuteAsync(
                "INSERT INTO game.item_locations(item_instance_id,owner_character_id,location_kind,slot) " +
                "VALUES($1,$2,'equipment',$3)", itemId, owner.Id, checked((short)slot));
        }
        await transaction.CommitAsync();

        async Task ExecuteAsync(string sql, params object[] values)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            foreach (object value in values) command.Parameters.AddWithValue(value);
            int affected = await command.ExecuteNonQueryAsync();
            if (sql.StartsWith("INSERT INTO game.item_instances", StringComparison.Ordinal) && affected != 1)
                throw new InvalidOperationException("A reviewed peer-appearance item definition was unavailable.");
        }
    }

    await using ItemEconomyRuntime5165 economy = await ItemEconomyRuntime5165.CreateAsync(connectionString, default);
    ItemEconomySession5165 firstEconomy = await economy.OpenSessionAsync(
        new AccountId(firstAccount), new CharacterUid(checked((uint)first.LegacyUid)), default);
    ItemEconomySession5165 secondEconomy = await economy.OpenSessionAsync(
        new AccountId(secondAccount), new CharacterUid(checked((uint)second.LegacyUid)), default);
    GamePlayerAppearance5165 firstAppearance = await firstEconomy.ReadAppearanceAsync(default);
    GamePlayerAppearance5165 secondAppearance = await secondEconomy.ReadAppearanceAsync(default);
    Assert(firstAppearance == new GamePlayerAppearance5165(Headwear: 118109, Garment: 181305,
            Armor: 130109, LeftHand: 420339, RightHand: 410339,
            ArmorColor: 3, LeftHandColor: 3, HeadwearColor: 3) &&
        secondAppearance == new GamePlayerAppearance5165(Headwear: 123109, Garment: 181315,
            Armor: 135109, LeftHand: 601301, RightHand: 601301,
            ArmorColor: 3, LeftHandColor: 3, HeadwearColor: 3),
        "The two authenticated PostgreSQL equipment snapshots were not hydrated before screen entry.");
    var map = new TestMapValidation5165(blocked: [(432, 378)]);
    var traces = new List<PlayerPeerSpawnTrace5165>();
    var world = new GameplayWorld5165(new MapCatalog5165([map]), new(), traces.Add);
    var firstPackets = new List<byte[]>();
    var secondPackets = new List<byte[]>();
    var firstSession = new GameplaySession5165(first, Capture(firstPackets),
        jumpRate: new(20, TimeSpan.FromSeconds(1)), appearance: firstAppearance,
        peerAppearanceCanary: true);
    var secondSession = new GameplaySession5165(second, Capture(secondPackets), appearance: secondAppearance);

    await world.EnterAsync(firstSession, default);
    await world.EnterAsync(secondSession, default);
    Assert(world.SessionCount == 2 && firstPackets.Count == 1 && secondPackets.Count == 1,
        "The two-client world did not synchronize initial nearby-player visibility.");
    Assert(traces.Count == 2 && traces.All(trace => trace.Reason == "screen-entry") &&
        traces.All(trace => trace.PlaintextHex.Length > 276),
        "The production-shaped canary did not preserve both exact plaintext peer-spawn frames.");
    GamePlayerSpawn5165 firstSaw = GamePacketsAfterAuthentication5165.ParsePlayerSpawnAsExactClient(firstPackets[0]);
    GamePlayerSpawn5165 secondSaw = GamePacketsAfterAuthentication5165.ParsePlayerSpawnAsExactClient(secondPackets[0]);
    Assert(firstSaw.Id == GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(
            checked((uint)second.LegacyUid)) &&
        firstSaw.Headwear == secondAppearance.Headwear && firstSaw.Garment == secondAppearance.Garment &&
        firstSaw.Armor == secondAppearance.Armor && firstSaw.LeftHand == secondAppearance.LeftHand &&
        firstSaw.RightHand == secondAppearance.RightHand &&
        secondSaw.Id == GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(
            checked((uint)first.LegacyUid)) &&
        secondSaw.Headwear == firstAppearance.Headwear && secondSaw.Garment == firstAppearance.Garment &&
        secondSaw.Armor == firstAppearance.Armor && secondSaw.LeftHand == firstAppearance.LeftHand &&
        secondSaw.RightHand == firstAppearance.RightHand,
        "The two authenticated characters did not receive each other's authoritative equipped peer spawn.");
    firstPackets.Clear(); secondPackets.Clear();

    var equippedAppearance = new GamePlayerAppearance5165(Headwear: 111309, Garment: 181305,
        Armor: 130309, LeftHand: 601339, RightHand: 601339,
        ArmorColor: 3, LeftHandColor: 4, HeadwearColor: 5);
    await world.RefreshAppearanceAsync(firstSession, equippedAppearance, default);
    Assert(firstPackets.Count == 0 && secondPackets.Count == 1,
        "An authoritative equipment appearance refresh was not limited to visible peers.");
    GamePlayerSpawn5165 equippedPeer = GamePacketsAfterAuthentication5165.ParsePlayerSpawnAsExactClient(secondPackets[0]);
    Assert(equippedPeer.Id == GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(
            checked((uint)first.LegacyUid)) && equippedPeer.Headwear == 111309 &&
        equippedPeer.Garment == 181305 && equippedPeer.Armor == 130309 &&
        equippedPeer.LeftHand == 601339 && equippedPeer.RightHand == 601339,
        "The encrypted-world peer refresh did not retain authoritative equipment visuals.");
    secondPackets.Clear();
    await world.RefreshAppearanceAsync(firstSession, equippedAppearance, default);
    Assert(secondPackets.Count == 0, "An unchanged equipment appearance emitted a duplicate peer refresh.");
    await world.RefreshAppearanceAsync(firstSession, default, default);
    Assert(firstPackets.Count == 0 && secondPackets.Count == 1,
        "An authoritative Unequip appearance refresh was not broadcast to the visible peer.");
    GamePlayerSpawn5165 unequippedPeer =
        GamePacketsAfterAuthentication5165.ParsePlayerSpawnAsExactClient(secondPackets[0]);
    Assert(unequippedPeer.Id == GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(
            checked((uint)first.LegacyUid)) && unequippedPeer.Headwear == 0 &&
        unequippedPeer.Garment == 0 && unequippedPeer.Armor == 0 &&
        unequippedPeer.LeftHand == 0 && unequippedPeer.RightHand == 0,
        "The peer-visible Unequip refresh retained stale equipment fields.");
    secondPackets.Clear();

    GameMovement5165 east = new(6, checked((uint)first.LegacyUid), 0, 10);
    CharacterState walked = await world.WalkAsync(firstSession, east, store, default);
    Assert(walked.X == 431 && walked.Y == 378 && firstPackets.Count == 1 && secondPackets.Count == 1,
        "Authoritative walking was not persisted and broadcast to both clients.");
    Assert(GamePacketsAfterAuthentication5165.ParseMovement(firstPackets[0]).Id == first.LegacyUid &&
        GamePacketsAfterAuthentication5165.ParseMovement(secondPackets[0]).Id ==
            GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(checked((uint)first.LegacyUid)) &&
        GamePacketsAfterAuthentication5165.ParseMovement(secondPackets[0]).Direction % 8 == 6,
        "The exact 20/10005 movement broadcast was invalid.");
    firstPackets.Clear(); secondPackets.Clear();

    await AssertInvalidDataAsync(() => world.WalkAsync(firstSession,
        new GameMovement5165(6, checked((uint)first.LegacyUid), 0, 11), store, default));
    CharacterState afterBlocked = await store.GetCharacterAsync(firstAccount, default)
        ?? throw new InvalidOperationException("Movement test character disappeared.");
    Assert(afterBlocked.X == 431 && afterBlocked.Y == 378 && afterBlocked.Version == walked.Version,
        "A blocked map cell changed persistent position.");
    await AssertInvalidDataAsync(() => world.WalkAsync(firstSession,
        new GameMovement5165(6, checked((uint)first.LegacyUid), 9, 12), store, default));
    await AssertInvalidDataAsync(() => world.WalkAsync(firstSession,
        new GameMovement5165(6, checked((uint)second.LegacyUid), 0, 13), store, default));

    GameAction5165 jump = new(checked((uint)first.LegacyUid), (uint)440 | ((uint)381 << 16), 12,
        GamePacketsAfterAuthentication5165.JumpAction, 6, 431, 378);
    JumpOutcome5165 jumpResult = await world.JumpAsync(firstSession, jump, store, default);
    CharacterState jumped = jumpResult.Character;
    Assert(jumpResult.Accepted && jumped.X == 440 && jumped.Y == 381 && firstPackets.Count == 1 && secondPackets.Count == 1,
        "Authoritative jumping was not persisted and synchronized.");
    Assert(GamePacketsAfterAuthentication5165.ParseAction(firstPackets[0]).Action == 137,
        "The exact jump broadcast was invalid.");
    Assert(GamePacketsAfterAuthentication5165.ParseAction(secondPackets[0]).Id ==
        GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(checked((uint)first.LegacyUid)),
        "The canary peer jump did not retain the player presentation identity.");
    JumpOutcome5165 excessive = await world.JumpAsync(firstSession,
        new GameAction5165(checked((uint)first.LegacyUid), (uint)500 | ((uint)500 << 16), 14,
            GamePacketsAfterAuthentication5165.JumpAction, 6, 440, 381), store, default);
    Assert(!excessive.Accepted && excessive.Category == "distance" && firstSession.Character.X == 440 &&
        GamePacketsAfterAuthentication5165.ParseAction(firstPackets[^1]).Action == 108,
        "An excessive jump did not preserve the session and return an authoritative correction.");
    JumpOutcome5165 blockedJump = await world.JumpAsync(firstSession,
        new GameAction5165(checked((uint)first.LegacyUid), (uint)432 | ((uint)378 << 16), 15,
            GamePacketsAfterAuthentication5165.JumpAction, 2, 440, 381), store, default);
    Assert(!blockedJump.Accepted && blockedJump.Category == "map-validation" && firstSession.Character.X == 440 &&
        GamePacketsAfterAuthentication5165.ParseAction(firstPackets[^1]).Action == 108,
        "A blocked jump did not preserve the session and return an authoritative correction.");

    firstPackets.Clear(); secondPackets.Clear();
    GameAction5165 leaveView = new(checked((uint)first.LegacyUid), (uint)449 | ((uint)381 << 16), 16,
        GamePacketsAfterAuthentication5165.JumpAction, 6, 440, 381);
    JumpOutcome5165 outsideResult = await world.JumpAsync(firstSession, leaveView, store, default);
    CharacterState outside = outsideResult.Character;
    Assert(outside.X == 449 && outside.Y == 381 && !firstSession.IsVisible(second.LegacyUid) &&
        !secondSession.IsVisible(first.LegacyUid), "Leaving view did not remove the symmetric screen membership.");
    Assert(firstPackets.Count == 2 && secondPackets.Count == 2 &&
        GamePacketsAfterAuthentication5165.ParseAction(firstPackets[1]).Action ==
            GamePacketsAfterAuthentication5165.RemoveEntityAction &&
        GamePacketsAfterAuthentication5165.ParseAction(firstPackets[1]).Id ==
            GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(checked((uint)second.LegacyUid)) &&
        GamePacketsAfterAuthentication5165.ParseAction(secondPackets[1]).Action ==
            GamePacketsAfterAuthentication5165.RemoveEntityAction &&
        GamePacketsAfterAuthentication5165.ParseAction(secondPackets[1]).Id ==
            GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(checked((uint)first.LegacyUid)),
        "Leaving view did not send the exact-client-supported action-135 removal to both screens.");

    firstPackets.Clear(); secondPackets.Clear();
    GameAction5165 enterView = new(checked((uint)first.LegacyUid), (uint)440 | ((uint)381 << 16), 17,
        GamePacketsAfterAuthentication5165.JumpAction, 2, 449, 381);
    jumped = (await world.JumpAsync(firstSession, enterView, store, default)).Character;
    Assert(firstSession.IsVisible(second.LegacyUid) && secondSession.IsVisible(first.LegacyUid) &&
        firstPackets.Count == 2 && secondPackets.Count == 1 &&
        GamePacketsAfterAuthentication5165.ParsePlayerSpawnAsExactClient(firstPackets[1]).Id ==
            GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(checked((uint)second.LegacyUid)) &&
        GamePacketsAfterAuthentication5165.ParsePlayerSpawnAsExactClient(secondPackets[0]).Id ==
            GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(checked((uint)first.LegacyUid)),
        "Entering view did not create symmetric screen membership and exact 10014 spawns.");

    world.Leave(firstSession); world.Leave(secondSession);
    Assert(world.SessionCount == 0, "World sessions did not clean up deterministically.");

    var staleWorld = new GameplayWorld5165(new TestMapValidation5165());
    var staleSession = new GameplaySession5165(first, (_, _) => throw new IOException("closed peer"));
    var healthySession = new GameplaySession5165(second, Capture(secondPackets));
    await staleWorld.EnterAsync(staleSession, default);
    await staleWorld.EnterAsync(healthySession, default);
    Assert(staleWorld.SessionCount == 1,
        "A closed nearby peer disrupted the healthy session instead of being removed deterministically.");
    staleWorld.Leave(healthySession);

    long simulatedTicks = 0;
    var stableWorld = new GameplayWorld5165(new TestMapValidation5165());
    firstPackets.Clear(); secondPackets.Clear();
    firstSession = new GameplaySession5165(jumped, Capture(firstPackets),
        new BoundedEventRate5165(30, TimeSpan.FromSeconds(1), () => simulatedTicks),
        new BoundedEventRate5165(4, TimeSpan.FromSeconds(1), () => simulatedTicks));
    secondSession = new GameplaySession5165(secondSession.Character, Capture(secondPackets));
    await stableWorld.EnterAsync(firstSession, default);
    await stableWorld.EnterAsync(secondSession, default);
    firstPackets.Clear(); secondPackets.Clear();
    for (int jumpIndex = 0; jumpIndex < 20; jumpIndex++)
    {
        simulatedTicks += Stopwatch.Frequency;
        CharacterState before = firstSession.Character;
        int targetX = before.X == 440 ? 441 : 440;
        JumpOutcome5165 result = await stableWorld.JumpAsync(firstSession,
            new GameAction5165(checked((uint)first.LegacyUid),
                checked((uint)targetX | ((uint)before.Y << 16)), checked((uint)(200 + jumpIndex)),
                GamePacketsAfterAuthentication5165.JumpAction, 6, checked((ushort)before.X), checked((ushort)before.Y)),
            store, default);
        Assert(result.Accepted && result.Character.X == targetX,
            "A consecutive valid genuine-layout jump was not accepted.");
    }
    Assert(firstPackets.Count == 20 && secondPackets.Count == 20,
        "Twenty consecutive jumps were not synchronized to both clients.");
    firstPackets.Clear(); secondPackets.Clear();
    for (int secondIndex = 0; secondIndex < 120; secondIndex++)
    {
        simulatedTicks += Stopwatch.Frequency;
        if (secondIndex % 6 == 0)
        {
            CharacterState beforeJump = firstSession.Character;
            int targetY = beforeJump.Y == 381 ? 382 : 381;
            JumpOutcome5165 mixedJump = await stableWorld.JumpAsync(firstSession,
                new GameAction5165(checked((uint)first.LegacyUid),
                    checked((uint)beforeJump.X | ((uint)targetY << 16)), checked((uint)(400 + secondIndex)),
                    GamePacketsAfterAuthentication5165.JumpAction, 0,
                    checked((ushort)beforeJump.X), checked((ushort)beforeJump.Y)), store, default);
            Assert(mixedJump.Accepted, "The two-minute mixed movement soak rejected a valid jump.");
        }
        uint direction = secondIndex % 2 == 0 ? 6u : 2u;
        await stableWorld.WalkAsync(firstSession,
            new GameMovement5165(direction, checked((uint)first.LegacyUid), 0, checked((uint)(100 + secondIndex))),
            store, default);
    }
    Assert(firstPackets.Count == 140 && secondPackets.Count == 140,
        "The deterministic two-minute mixed multi-client session lost authoritative movement broadcasts.");
    stableWorld.Leave(firstSession); stableWorld.Leave(secondSession);
    CharacterState reconnect = await store.GetCharacterAsync(firstAccount, default)
        ?? throw new InvalidOperationException("Reconnect position was missing.");
    Assert(reconnect.Id == first.Id && reconnect.LegacyUid == first.LegacyUid && reconnect.X == 440 && reconnect.Y == 381,
        "Reconnect did not restore the saved valid position and immutable identity.");

    GameplayReferenceRows loadedReferences = await store.LoadPortalNpcReferencesAsync([1002, 1004], default);
    Assert(loadedReferences.Portals.Select(row => row.LegacyId).Distinct().Count() == loadedReferences.Portals.Count &&
        loadedReferences.Npcs.Select(row => row.LegacyUid).Distinct().Count() == loadedReferences.Npcs.Count,
        "The active portal/NPC release contains duplicate legacy identities.");
    long[] fixturePortalIds = [64, 73, 220, 221, 222];
    uint[] fixtureNpcIds = [400, 10000, 10001, 10022, 35500];
    var imported = new GameplayReferenceRows(
        loadedReferences.Portals.Where(row => fixturePortalIds.Contains(row.LegacyId)).ToArray(),
        loadedReferences.Npcs.Where(row => fixtureNpcIds.Contains(row.LegacyUid)).ToArray());
    Assert(imported.Portals.Count == 5 && imported.Npcs.Count == 5,
        "The reviewed portal/NPC import was not idempotent and provenance-bounded.");
    CharacterState portalFirst = await store.TransitionMapAsync(first.Id, 1002, 401, 387, 0,
        reconnect.Version, default) ?? throw new InvalidOperationException("Portal fixture setup failed.");
    CharacterState secondCurrent = await store.GetCharacterAsync(secondAccount, default)
        ?? throw new InvalidOperationException("Second portal fixture character disappeared.");
    CharacterState portalSecond = await store.TransitionMapAsync(second.Id, 1002, 402, 387, 0,
        secondCurrent.Version, default) ?? throw new InvalidOperationException("Second portal fixture setup failed.");
    var portalMaps = new MapCatalog5165(
        [new TestMapValidation5165(1002), new TestMapValidation5165(1004, 100, 100)],
        new Dictionary<int, int> { [1002] = 1002, [1004] = 1004 });
    var portalReferences = new GameplayReferenceCatalog5165(
        imported.Portals.Select(row => new PortalReference5165(row.LegacyId, row.SourceMap, row.SourceX, row.SourceY,
            row.DestinationMap, row.DestinationX, row.DestinationY)),
        imported.Npcs.Select(row => new NpcReference5165(row.LegacyUid, row.Type, row.Flags, row.Face,
            row.MapId, row.X, row.Y)));
    var portalWorld = new GameplayWorld5165(portalMaps, portalReferences);
    var portalNpcRouter = new NpcActivationRouter5165(portalWorld, guideNpcUid: 10001);
    firstPackets.Clear(); secondPackets.Clear();
    firstSession = new GameplaySession5165(portalFirst, Capture(firstPackets));
    secondSession = new GameplaySession5165(portalSecond, Capture(secondPackets));
    await portalWorld.EnterAsync(firstSession, default);
    await portalWorld.EnterAsync(secondSession, default);
    firstPackets.Clear(); secondPackets.Clear();
    byte[] genuinePortalRequest = ExactClientPortalRequest(
        checked((uint)portalFirst.LegacyUid), 401, 387, 401, 387, 4, 500);
    GameAction5165 parsedPortalRequest = GamePacketsAfterAuthentication5165.ParseAction(genuinePortalRequest);
    PortalTransition5165 enteredArena = await portalWorld.UsePortalAsync(firstSession,
        parsedPortalRequest, store, default);
    Assert(enteredArena.Character.MapId == 1004 && enteredArena.DestinationX == 51 && enteredArena.DestinationY == 70 &&
        !enteredArena.DestinationCorrected && secondPackets.Count == 1 &&
        GamePacketsAfterAuthentication5165.ParseAction(secondPackets[0]).Action ==
            GamePacketsAfterAuthentication5165.RemoveEntityAction,
        "The valid portal did not atomically remove the player from the old screen and enter map 1004.");
    Assert(firstPackets.Count == 4 &&
        GamePacketsAfterAuthentication5165.ParseAction(firstPackets[0]).Action == GamePacketsAfterAuthentication5165.FlyMapAction &&
        BinaryPrimitives.ReadUInt16LittleEndian(firstPackets[1].AsSpan(2)) == GamePacketsAfterAuthentication5165.MapInfoPacketId &&
        firstPackets.Skip(2).Select(packet => GamePacketsAfterAuthentication5165.ParseNpcSpawnAsExactClient(packet))
            .Select(npc => npc.Id).SequenceEqual([400u, 10001u]),
        "The exact 86/1110 transition and nearby NPC spawn order was invalid.");
    CharacterState persistedPortal = await store.GetCharacterAsync(firstAccount, default)
        ?? throw new InvalidOperationException("Portal destination persistence disappeared.");
    Assert(persistedPortal.MapId == 1004 && persistedPortal.X == 51 && persistedPortal.Y == 70,
        "Reconnect would not restore the portal destination.");
    CryptographicOperations.ZeroMemory(genuinePortalRequest);

    firstPackets.Clear();
    CharacterState walkedOnDestination = await portalWorld.WalkAsync(firstSession,
        new GameMovement5165(2, checked((uint)portalFirst.LegacyUid), 0, 504), store, default);
    JumpOutcome5165 jumpedOnDestination = await portalWorld.JumpAsync(firstSession,
        new GameAction5165(checked((uint)portalFirst.LegacyUid), (uint)48 | ((uint)70 << 16), 505,
            GamePacketsAfterAuthentication5165.JumpAction, 2,
            checked((ushort)walkedOnDestination.X), checked((ushort)walkedOnDestination.Y)), store, default);
    CharacterState destinationReconnect = await store.GetCharacterAsync(firstAccount, default)
        ?? throw new InvalidOperationException("Destination-map movement persistence disappeared.");
    Assert(walkedOnDestination.MapId == 1004 && walkedOnDestination.X == 50 && walkedOnDestination.Y == 70 &&
        jumpedOnDestination.Accepted && jumpedOnDestination.Character.MapId == 1004 &&
        jumpedOnDestination.Character.X == 48 && jumpedOnDestination.Character.Y == 70 &&
        destinationReconnect.Id == first.Id && destinationReconnect.LegacyUid == first.LegacyUid &&
        destinationReconnect.MapId == 1004 && destinationReconnect.X == 48 && destinationReconnect.Y == 70,
        "Walking, jumping, or reconnect persistence regressed after the map-1004 transition.");

    firstPackets.Clear();
    Assert((await portalNpcRouter.ActivateAsync(firstSession, null,
        new NpcInteraction5165(10001, 0, 0, 0), default)).Accepted, "The reviewed guide activation was rejected.");
    Assert(firstPackets.Count == 5 && firstPackets.All(packet =>
            BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)) == GamePacketsAfterAuthentication5165.NpcDialogPacketId),
        "The bounded NPC dialog did not send the exact text/option/face/finish sequence.");
    byte[] dialogAnswer = ExactNpcDialogAnswer(1);
    NpcDialogAnswer5165 parsedAnswer = GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(dialogAnswer);
    await portalWorld.SelectNpcDialogOptionAsync(firstSession, parsedAnswer, default);
    Assert(firstPackets.Count == 9, "The safe NPC option did not return a bounded closeable dialog.");
    byte[] goodbye = ExactNpcDialogAnswer(byte.MaxValue);
    await portalWorld.SelectNpcDialogOptionAsync(firstSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(goodbye), default);
    byte[] differentGoodbye = ExactNpcDialogAnswerWithText(byte.MaxValue, (byte)'x');
    await AssertInvalidDataAsync(() => portalWorld.SelectNpcDialogOptionAsync(firstSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(differentGoodbye), default));
    await portalWorld.SelectNpcDialogOptionAsync(firstSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(goodbye), default);
    await AssertInvalidDataAsync(() => portalWorld.SelectNpcDialogOptionAsync(firstSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(goodbye), default));
    byte[] dialogClose = ExactNpcDialogClose();
    await AssertInvalidDataAsync(() => portalWorld.SelectNpcDialogOptionAsync(firstSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(dialogClose), default));
    Assert(firstSession.ActiveNpc is null,
        "The exact duplicate Goodbye did not close idempotently.");
    Assert((await portalNpcRouter.ActivateAsync(firstSession, null,
        new NpcInteraction5165(10001, 0, 0, 0), default)).Accepted, "The reviewed guide reactivation was rejected.");
    await portalWorld.SelectNpcDialogOptionAsync(firstSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(goodbye), default);
    await portalWorld.SelectNpcDialogOptionAsync(firstSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(dialogClose), default);
    await AssertInvalidDataAsync(() => portalWorld.SelectNpcDialogOptionAsync(firstSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(dialogClose), default));
    await AssertInvalidDataAsync(() => portalWorld.SelectNpcDialogOptionAsync(secondSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(dialogClose), default));
    CryptographicOperations.ZeroMemory(differentGoodbye);
    CryptographicOperations.ZeroMemory(goodbye);
    CryptographicOperations.ZeroMemory(dialogClose);
    Assert(!(await portalNpcRouter.ActivateAsync(secondSession, null,
        new NpcInteraction5165(10001, 0, 0, 0), default)).Accepted,
        "A wrong-map guide activation was accepted.");
    Assert(!(await portalNpcRouter.ActivateAsync(firstSession, null,
        new NpcInteraction5165(0xfefefefe, 0, 0, 0), default)).Accepted,
        "A forged NPC UID was accepted.");
    var invisibleSession = new GameplaySession5165(firstSession.Character, Capture(firstPackets));
    Assert(!(await portalNpcRouter.ActivateAsync(invisibleSession, null,
        new NpcInteraction5165(10001, 0, 0, 0), default)).Accepted,
        "An NPC outside the authenticated visibility set was accepted.");
    var distantSession = new GameplaySession5165(firstSession.Character with { X = 1, Y = 1 }, Capture(firstPackets));
    distantSession.MarkNpcVisible(10001);
    Assert(!(await portalNpcRouter.ActivateAsync(distantSession, null,
        new NpcInteraction5165(10001, 0, 0, 0), default)).Accepted,
        "An excessive-distance NPC activation was accepted.");
    dialogAnswer[10] = 99;
    await AssertInvalidDataAsync(() => portalWorld.SelectNpcDialogOptionAsync(firstSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(dialogAnswer), default));
    CryptographicOperations.ZeroMemory(dialogAnswer);

    long npcRateTicks = 0;
    var rateWorld = new GameplayWorld5165(portalMaps, portalReferences);
    var rateRouter = new NpcActivationRouter5165(rateWorld, guideNpcUid: 10001);
    var rateSession = new GameplaySession5165(firstSession.Character, Capture(firstPackets),
        npcDialogRate: new BoundedEventRate5165(4, TimeSpan.FromSeconds(1), () => npcRateTicks));
    await rateWorld.EnterAsync(rateSession, default);
    Assert((await rateRouter.ActivateAsync(rateSession, null,
        new NpcInteraction5165(10001, 0, 0, 0), default)).Accepted, "The rate fixture guide activation was rejected.");
    await rateWorld.SelectNpcDialogOptionAsync(rateSession, parsedAnswer, default);
    byte[] rateGoodbye = ExactNpcDialogAnswer(byte.MaxValue);
    await rateWorld.SelectNpcDialogOptionAsync(rateSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(rateGoodbye), default);
    await rateWorld.SelectNpcDialogOptionAsync(rateSession,
        GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(rateGoodbye), default);
    Assert(!(await rateRouter.ActivateAsync(rateSession, null,
        new NpcInteraction5165(10001, 0, 0, 0), default)).Accepted,
        "The bounded guide activation rate was not enforced.");
    npcRateTicks += Stopwatch.Frequency;
    Assert((await rateRouter.ActivateAsync(rateSession, null,
        new NpcInteraction5165(10001, 0, 0, 0), default)).Accepted,
        "The bounded guide activation rate did not recover.");
    CryptographicOperations.ZeroMemory(rateGoodbye);
    rateWorld.Leave(rateSession);

    firstPackets.Clear();
    PortalTransition5165 returned = await portalWorld.UsePortalAsync(firstSession,
        new GameAction5165(checked((uint)portalFirst.LegacyUid), (uint)51 | ((uint)73 << 16), 501,
            GamePacketsAfterAuthentication5165.PortalAction, 0, 51, 70), store, default);
    Assert(returned.Character.MapId == 1002 && firstPackets.Any(packet => packet.Length == 32 &&
        GamePacketsAfterAuthentication5165.ParseAction(packet).Action == GamePacketsAfterAuthentication5165.RemoveEntityAction),
        "Leaving the NPC map did not despawn its visible NPC.");
    await AssertInvalidDataAsync(() => portalWorld.UsePortalAsync(firstSession,
        new GameAction5165(checked((uint)portalFirst.LegacyUid), (uint)900 | ((uint)900 << 16), 502,
            GamePacketsAfterAuthentication5165.PortalAction, 0, 402, 391), store, default));
    await AssertInvalidDataAsync(() => portalWorld.UsePortalAsync(firstSession,
        new GameAction5165(checked((uint)portalFirst.LegacyUid), (uint)402 | ((uint)391 << 16), 506,
            GamePacketsAfterAuthentication5165.PortalAction, 0, 900, 900), store, default));
    portalWorld.Leave(firstSession); portalWorld.Leave(secondSession);

    var correctedMaps = new MapCatalog5165(
        [new TestMapValidation5165(1002), new TestMapValidation5165(1004, 100, 100, [(51, 70)])]);
    var correctedWorld = new GameplayWorld5165(correctedMaps, portalReferences);
    firstPackets.Clear();
    firstSession = new GameplaySession5165(returned.Character with { X = 401, Y = 387 }, Capture(firstPackets));
    CharacterState correctedSetup = await store.TransitionMapAsync(first.Id, 1002, 401, 387, 0,
        returned.Character.Version, default) ?? throw new InvalidOperationException("Corrected portal setup failed.");
    firstSession.Character = correctedSetup;
    await correctedWorld.EnterAsync(firstSession, default);
    PortalTransition5165 corrected = await correctedWorld.UsePortalAsync(firstSession,
        new GameAction5165(checked((uint)portalFirst.LegacyUid), (uint)401 | ((uint)387 << 16), 503,
            GamePacketsAfterAuthentication5165.PortalAction, 0, 401, 387), store, default);
    Assert(corrected.DestinationCorrected && corrected.Character.MapId == 1004 &&
        (corrected.DestinationX != 51 || corrected.DestinationY != 70),
        "A blocked portal destination was not corrected to a bounded accessible cell.");
    correctedWorld.Leave(firstSession);
    Console.WriteLine("PASS reviewed portals, multi-map persistence, NPC visibility, and bounded dialog flow");

    byte[] dmap = BuildDMapFixture(3, 2, blockedX: 1, blockedY: 1);
    DMapValidation5165 parsed = DMapValidation5165.Parse(dmap, 1002);
    Assert(parsed.Width == 3 && parsed.Height == 2 && parsed.CanStand(0, 0) && !parsed.CanStand(1, 1),
        "The licensed DMap collision reader did not validate accessibility.");
    "DMAP101\0"u8.CopyTo(dmap);
    parsed = DMapValidation5165.Parse(dmap, 1004);
    Assert(parsed.MapId == 1004 && parsed.Width == 3 && parsed.Height == 2,
        "The exact forum DMap header was not accepted.");
    BinaryPrimitives.WriteUInt32LittleEndian(dmap, 1003);
    parsed = DMapValidation5165.Parse(dmap, 1004);
    Assert(parsed.Width == 3 && parsed.Height == 2, "The owner-catalog numeric 1003 DMap header was not accepted.");
    "DMAP100\0"u8.CopyTo(dmap);
    parsed = DMapValidation5165.Parse(dmap, 1004);
    Assert(parsed.Width == 3 && parsed.Height == 2, "The owner-catalog DMAP100 header was not accepted.");
    dmap[280] ^= 1;
    AssertThrowsSync<InvalidDataException>(() => DMapValidation5165.Parse(dmap, 1002));
    CryptographicOperations.ZeroMemory(dmap);
    Console.WriteLine("PASS exact 5165 two-client movement, jump, map validation, screen sync, persistence, and reconnect");

    static Func<byte[], CancellationToken, Task> Capture(List<byte[]> destination) => (packet, _) =>
    {
        destination.Add(packet.ToArray());
        CryptographicOperations.ZeroMemory(packet);
        return Task.CompletedTask;
    };

    static byte[] ExactClientPortalRequest(uint uid, ushort sourceX, ushort sourceY,
        ushort destinationX, ushort destinationY, ushort direction, uint timestamp)
    {
        byte[] packet = new byte[GamePacketsAfterAuthentication5165.ActionPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, GamePacketsAfterAuthentication5165.ActionPacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GamePacketsAfterAuthentication5165.ActionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), sourceX);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10), sourceY);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), GamePacketsAfterAuthentication5165.PortalAction);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), direction);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(20), destinationX);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(22), destinationY);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(28), uint.MaxValue);
        return packet;
    }
}

static byte[] BuildDMapFixture(int width, int height, int blockedX, int blockedY)
{
    byte[] data = new byte[276 + height * (width * 6 + 4)];
    BinaryPrimitives.WriteUInt32LittleEndian(data, 1004);
    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(268), checked((uint)width));
    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(272), checked((uint)height));
    int offset = 276;
    for (int y = 0; y < height; y++)
    {
        uint checksum = 0;
        for (int x = 0; x < width; x++)
        {
            ushort mask = checked((ushort)(x == blockedX && y == blockedY ? 1 : 0));
            ushort terrain = 0;
            short altitude = 0;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), mask);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 2), terrain);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 4), altitude);
            offset += 6;
            unchecked { checksum += (uint)(mask * (terrain + y + 1) + ((altitude + 2) * (x + 1 + terrain))); }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), checksum);
        offset += 4;
    }
    return data;
}

static byte[] ExactNpcDialogAnswer(byte option)
{
    byte[] packet = new byte[16];
    BinaryPrimitives.WriteUInt16LittleEndian(packet, 16);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GamePacketsAfterAuthentication5165.NpcDialogPacketId);
    packet[10] = option;
    packet[11] = 101;
    return packet;
}

static byte[] ExactNpcDialogAnswerWithText(byte option, byte ascii)
{
    byte[] packet = ExactNpcDialogAnswer(option);
    packet[12] = 1;
    packet[13] = 1;
    packet[14] = ascii;
    return packet;
}

static byte[] ExactNpcDialogClose()
{
    byte[] packet = new byte[GamePacketsAfterAuthentication5165.NpcDialogCloseLength];
    BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GamePacketsAfterAuthentication5165.NpcDialogPacketId);
    packet[11] = 102;
    return packet;
}

static async Task AssertInvalidDataAsync(Func<Task> action)
{
    try { await action(); }
    catch (InvalidDataException) { return; }
    throw new InvalidOperationException("Gameplay boundary did not fail closed.");
}

static void AssertThrowsSync<TException>(Action action) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

static async Task Genuine5165CharacterWorldAsync(string databaseUsername, string databaseName, Guid accountId,
    string accountUsername, string characterName, string password)
{
    int accountPort = FreePort(), gamePort = FreePort();
    while (gamePort == accountPort) gamePort = FreePort();
    using var logOutput = new StringWriter();
    using var logError = new StringWriter();
    var log = new SafeConsoleLog(logOutput, logError);
    await using var store = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName);
    var account = new Genuine5165RuntimeComponent("ExactAccount", IPAddress.Loopback, accountPort, 4, true,
        "10.0.0.1", gamePort, "127.0.0.1", store, log);
    var game = new Genuine5165RuntimeComponent("ExactGame", IPAddress.Loopback, gamePort, 4, false,
        "10.0.0.1", gamePort, "127.0.0.1", store, log,
        new GameplayWorld5165(new TestMapValidation5165()));
    await account.StartAsync(default);
    await game.StartAsync(default);
    try
    {
        ulong firstToken = await ExactAccountLoginAsync(accountPort, accountUsername, password);
        uint seamlessUid = await ExactCharacterCreationAndWorldAsync(gamePort, firstToken, characterName);
        CharacterState created = await store.GetCharacterAsync(accountId, default)
            ?? throw new InvalidOperationException("Exact-client registration did not commit a character.");
        Assert(created.LegacyUid == seamlessUid && created.Name == characterName && created.Mesh == 1003 && created.Profession == 100 &&
            created.MapId == 1002 && created.X == 430 && created.Y == 378,
            "Exact-client registration persisted the wrong starter state.");
        string seamlessLog = logOutput.ToString();
        Assert(Count(seamlessLog, "Account1086Validated") == 1 && Count(seamlessLog, "GameConnectionAccepted") == 1 &&
            Count(seamlessLog, "Game1052Validated") == 1 && Count(seamlessLog, "CharacterCreated") == 1 &&
            Count(seamlessLog, "CharacterRegistrationSessionPromoted") == 1,
            "Seamless onboarding did not use exactly one Account login, Game connection, DH/1052 path, handoff, and character commit.");
        long sequenceBeforeDuplicate = await LegacyUidSequenceAsync(databaseUsername, databaseName);
        CharacterState? duplicate = await store.TryCreateCharacterAsync(accountId, characterName.ToLowerInvariant(), 1003, 100, default);
        long sequenceAfterDuplicate = await LegacyUidSequenceAsync(databaseUsername, databaseName);
        Assert(duplicate is null && sequenceBeforeDuplicate == sequenceAfterDuplicate,
            "A duplicate exact-client Create was not rejected before allocating another legacy UID.");

        ulong secondToken = await ExactAccountLoginAsync(accountPort, accountUsername, password);
        await ExactWorldEntryAsync(gamePort, secondToken, checked((uint)created.LegacyUid), characterName);
        CharacterState persisted = await store.GetCharacterAsync(accountId, default)
            ?? throw new InvalidOperationException("Exact-client character disappeared after reconnect.");
        Assert(persisted.Id == created.Id && persisted.LegacyUid == created.LegacyUid &&
            persisted.MapId == 1002 && persisted.X == 440 && persisted.Y == 381,
            "Reconnect did not retain the single committed character and its authoritative movement position.");

        string safeLog = logOutput.ToString();
        Assert(Count(safeLog, "Account1086Validated") == 2 && Count(safeLog, "GameConnectionAccepted") == 2 &&
            Count(safeLog, "Game1052Validated") == 2 && Count(safeLog, "GameNewRoleSent") == 1 &&
            Count(safeLog, "CharacterCreated") == 1,
            "The separate existing-character path did not use one fresh login while preserving the original character and suppressing NEW_ROLE.");
        Assert(safeLog.Contains("CharacterRegistration1001Validated", StringComparison.Ordinal) &&
            safeLog.Contains("CharacterCreated", StringComparison.Ordinal) &&
            safeLog.Contains("CharacterInformationSent", StringComparison.Ordinal) &&
            Count(safeLog, "FirstWorldEntrySent") == 2 &&
            Count(safeLog, "category=quest-status") == 2 &&
            safeLog.Contains("32/10010 action 74 and 16/1110", StringComparison.Ordinal),
            "The genuine production path did not reach every required registration/world event.");
        Assert(!safeLog.Contains(password, StringComparison.Ordinal), "The exact-client password canary leaked into logs.");
        Console.WriteLine($"PASS exact-client-derived seamless creation and existing-character starter-world sequences ({characterName})");
    }
    catch
    {
        Console.Error.Write(logOutput.ToString());
        Console.Error.Write(logError.ToString());
        throw;
    }
    finally
    {
        await account.StopAsync(default);
        await game.StopAsync(default);
    }
}

static async Task GenuineEncryptedNpcGoodbyeAsync(string databaseUsername, string databaseName, Guid accountId,
    string accountUsername, string password)
{
    int accountPort = FreePort(), gamePort = FreePort();
    while (gamePort == accountPort) gamePort = FreePort();
    using var output = new StringWriter();
    var log = new SafeConsoleLog(output, TextWriter.Null);
    await using var store = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName);
    CharacterState current = await store.GetCharacterAsync(accountId, default)
        ?? throw new InvalidOperationException("The encrypted NPC fixture character is missing.");
    CharacterState positioned = await store.TransitionMapAsync(current.Id, 1004, 56, 33, 0, current.Version, default)
        ?? throw new InvalidOperationException("The encrypted NPC fixture position could not be established.");
    GameplayReferenceRows imported = await store.LoadPortalNpcReferencesAsync([1002, 1004], default);
    var references = new GameplayReferenceCatalog5165(
        imported.Portals.Select(row => new PortalReference5165(row.LegacyId, row.SourceMap, row.SourceX, row.SourceY,
            row.DestinationMap, row.DestinationX, row.DestinationY)),
        imported.Npcs.Select(row => new NpcReference5165(row.LegacyUid, row.Type, row.Flags, row.Face,
            row.MapId, row.X, row.Y)));
    NpcReference5165 npc = references.Npcs(1004).SingleOrDefault(candidate => candidate.LegacyUid == 10000) ??
        throw new InvalidOperationException("The encrypted NPC fixture is missing the explicit guide NPC.");
    Assert(Math.Max(Math.Abs(npc.X - positioned.X), Math.Abs(npc.Y - positioned.Y)) <= GameplayWorld5165.ViewRadius,
        "The explicit guide NPC is not visible from the encrypted fixture position.");
    int visibleNpcCount = references.Npcs(1004).Count(candidate =>
        Math.Max(Math.Abs(candidate.X - positioned.X), Math.Abs(candidate.Y - positioned.Y)) <= GameplayWorld5165.ViewRadius);
    var maps = new MapCatalog5165([new TestMapValidation5165(1002), new TestMapValidation5165(1004, 100, 100)],
        new Dictionary<int, int> { [1002] = 1002, [1004] = 1004 });
    var account = new Genuine5165RuntimeComponent("ExactNpcAccount", IPAddress.Loopback, accountPort, 4, true,
        "10.0.0.1", gamePort, "127.0.0.1", store, log);
    var game = new Genuine5165RuntimeComponent("ExactNpcGame", IPAddress.Loopback, gamePort, 4, false,
        "10.0.0.1", gamePort, "127.0.0.1", store, log, new GameplayWorld5165(maps, references));
    await account.StartAsync(default);
    await game.StartAsync(default);
    try
    {
        ulong token = await ExactAccountLoginAsync(accountPort, accountUsername, password);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, gamePort);
        NetworkStream stream = client.GetStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using ExactGameClient5165 exact = await ExactGameClient5165.AcceptAsync(stream, timeout.Token);
        byte[] reply = exact.CreateDhReply();
        byte[] connect = ExactGameConnect(exact, token);
        try
        {
            await stream.WriteAsync(reply, timeout.Token);
            await stream.WriteAsync(connect, timeout.Token);
            await ReadAndZeroAsync(exact, stream, timeout.Token); // entrance ANSWER_OK
            byte[] information = await exact.ReadServerPacketAsync(stream, timeout.Token);
            uint uid;
            try { uid = GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(information).Id; }
            finally { CryptographicOperations.ZeroMemory(information); }
            Assert(uid == positioned.LegacyUid, "The encrypted NPC fixture selected the wrong immutable character UID.");

            byte[] enter = ExactAction(uid, GamePacketsAfterAuthentication5165.EnterMapAction);
            byte[] quest = ExactObservedQuestStatus();
            byte[] encryptedEnter = exact.EncryptClientPacket(enter);
            byte[] encryptedQuest = exact.EncryptClientPacket(quest);
            byte[] initialCoalesced = [.. encryptedEnter, .. encryptedQuest];
            await stream.WriteAsync(initialCoalesced, timeout.Token);
            CryptographicOperations.ZeroMemory(enter);
            CryptographicOperations.ZeroMemory(quest);
            CryptographicOperations.ZeroMemory(encryptedEnter);
            CryptographicOperations.ZeroMemory(encryptedQuest);
            CryptographicOperations.ZeroMemory(initialCoalesced);
            await ReadAndZeroAsync(exact, stream, timeout.Token); // action 74
            await ReadAndZeroAsync(exact, stream, timeout.Token); // map 1110
            for (int index = 0; index < visibleNpcCount; index++)
                await ReadAndZeroAsync(exact, stream, timeout.Token);

            async Task OpenAndDrainAsync()
            {
                byte[] interaction = ExactNpcInteraction(npc.LegacyUid);
                byte[] encrypted = exact.EncryptClientPacket(interaction);
                try { await stream.WriteAsync(encrypted, timeout.Token); }
                finally { CryptographicOperations.ZeroMemory(interaction); CryptographicOperations.ZeroMemory(encrypted); }
                for (int index = 0; index < 5; index++)
                    await ReadAndZeroAsync(exact, stream, timeout.Token);
            }

            async Task AssertMovementStillAliveAsync(uint direction, uint timestamp)
            {
                byte[] movement = GamePacketsAfterAuthentication5165.EncodeMovement(direction, uid, 0, timestamp);
                byte[] encrypted = exact.EncryptClientPacket(movement);
                try { await stream.WriteAsync(encrypted, timeout.Token); }
                finally { CryptographicOperations.ZeroMemory(movement); CryptographicOperations.ZeroMemory(encrypted); }
                byte[] authoritative = await exact.ReadServerPacketAsync(stream, timeout.Token);
                try
                {
                    GameMovement5165 parsed = GamePacketsAfterAuthentication5165.ParseMovement(authoritative);
                    Assert(parsed.Id == uid && parsed.Direction == direction,
                        "Movement after encrypted NPC dialog closure was not processed.");
                }
                finally { CryptographicOperations.ZeroMemory(authoritative); }
            }

            // Production evidence: the genuine client sends two identical
            // option-255/action-101 records. Deliver them in one socket write.
            await OpenAndDrainAsync();
            byte[] selection = exact.EncryptClientPacket(ExactNpcDialogAnswer(byte.MaxValue));
            byte[] selectionReplay = exact.EncryptClientPacket(ExactNpcDialogAnswer(byte.MaxValue));
            byte[] coalesced = [.. selection, .. selectionReplay];
            await stream.WriteAsync(coalesced, timeout.Token);
            CryptographicOperations.ZeroMemory(selection);
            CryptographicOperations.ZeroMemory(selectionReplay);
            CryptographicOperations.ZeroMemory(coalesced);
            await AssertMovementStillAliveAsync(6, 700);

            // Fragmentation: split the same observed records across several
            // writes, including splits inside the encrypted four-byte header.
            await OpenAndDrainAsync();
            selection = exact.EncryptClientPacket(ExactNpcDialogAnswer(byte.MaxValue));
            selectionReplay = exact.EncryptClientPacket(ExactNpcDialogAnswer(byte.MaxValue));
            byte[] fragmented = [.. selection, .. selectionReplay];
            await stream.WriteAsync(fragmented.AsMemory(0, 1), timeout.Token);
            await stream.WriteAsync(fragmented.AsMemory(1, 2), timeout.Token);
            await stream.WriteAsync(fragmented.AsMemory(3, 9), timeout.Token);
            await stream.WriteAsync(fragmented.AsMemory(12), timeout.Token);
            CryptographicOperations.ZeroMemory(selection);
            CryptographicOperations.ZeroMemory(selectionReplay);
            CryptographicOperations.ZeroMemory(fragmented);
            await AssertMovementStillAliveAsync(2, 701);

            // A distinct action-102 close is accepted once only as confirmation
            // of a dialog that was just closed.
            await OpenAndDrainAsync();
            selection = exact.EncryptClientPacket(ExactNpcDialogAnswer(byte.MaxValue));
            byte[] close = exact.EncryptClientPacket(ExactNpcDialogClose());
            await stream.WriteAsync(selection, timeout.Token);
            await stream.WriteAsync(close, timeout.Token);
            CryptographicOperations.ZeroMemory(selection);
            CryptographicOperations.ZeroMemory(close);
            await AssertMovementStillAliveAsync(6, 702);
        }
        finally { CryptographicOperations.ZeroMemory(reply); CryptographicOperations.ZeroMemory(connect); }

        await Task.Delay(100);
        string safe = output.ToString();
        Assert(Count(safe, "Npc2032FrameDecoded") == 6 &&
            Count(safe, "option=255; action=101; trailing=all-zero") == 5 &&
            Count(safe, "option=0; action=102; trailing=all-zero") == 1 &&
            Count(safe, "Npc2032Accepted") == 6 && !safe.Contains("BoundedConnectionRejected", StringComparison.Ordinal),
            "The real encrypted Game reader did not preserve the exact Goodbye framing sequence.");
        Console.WriteLine("PASS encrypted exact-client NPC Goodbye single replay, fragmentation, close confirmation, and post-close movement");
    }
    finally
    {
        await account.StopAsync(default);
        await game.StopAsync(default);
    }

    static byte[] ExactNpcInteraction(uint npcId)
    {
        byte[] packet = new byte[GamePacketsAfterAuthentication5165.NpcInteractionPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GamePacketsAfterAuthentication5165.NpcInteractionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), npcId);
        return packet;
    }

    static async Task ReadAndZeroAsync(ExactGameClient5165 exact, NetworkStream stream, CancellationToken token)
    {
        byte[] packet = await exact.ReadServerPacketAsync(stream, token);
        CryptographicOperations.ZeroMemory(packet);
    }
}

static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;

static async Task IdentityOwnershipInvariantsAsync(string databaseUsername, string databaseName, Guid unownedAccount,
    Guid firstAccount, Guid secondAccount, string password)
{
    await using var store = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName);
    CharacterState first = await store.GetCharacterAsync(firstAccount, default)
        ?? throw new InvalidOperationException("First identity-invariant character missing.");
    CharacterState second = await store.GetCharacterAsync(secondAccount, default)
        ?? throw new InvalidOperationException("Second identity-invariant character missing.");
    long sequenceBefore = await LegacyUidSequenceAsync(databaseUsername, databaseName);
    Assert(await store.TryCreateCharacterAsync(unownedAccount, first.Name.ToLowerInvariant(), 1003, 100, default) is null,
        "A case-insensitive character name was assigned to a second account.");
    Assert(await LegacyUidSequenceAsync(databaseUsername, databaseName) == sequenceBefore,
        "Rejected cross-account duplicate creation allocated a legacy UID.");

    var builder = new NpgsqlConnectionStringBuilder { Host = "/var/run/postgresql", Database = databaseName, Username = databaseUsername, Pooling = false };
    await using (var connection = new NpgsqlConnection(builder.ConnectionString))
    {
        await connection.OpenAsync();
        await using var renameAccount = new NpgsqlCommand("UPDATE identity.game_accounts SET username='renamed5165d',normalized_username='renamed5165d' WHERE account_id=$1", connection);
        renameAccount.Parameters.AddWithValue(firstAccount);
        Assert(await renameAccount.ExecuteNonQueryAsync() == 1, "The test account rename did not execute.");
        await using var renameCharacter = new NpgsqlCommand("UPDATE game.characters SET name='SHEEKOX' WHERE account_id=$1", connection);
        renameCharacter.Parameters.AddWithValue(firstAccount);
        Assert(await renameCharacter.ExecuteNonQueryAsync() == 1, "The test character rename did not execute.");
        await using var ownership = new NpgsqlCommand("SELECT count(*) FROM game.characters c JOIN identity.game_accounts a ON a.account_id=c.account_id WHERE c.account_id=$1 AND a.username='renamed5165d' AND c.name='SHEEKOX'", connection);
        ownership.Parameters.AddWithValue(firstAccount);
        Assert(Convert.ToInt32(await ownership.ExecuteScalarAsync()) == 1,
            "Username or character-name changes orphaned immutable AccountId ownership.");
    }
    CharacterState renamed = await store.GetCharacterAsync(firstAccount, default)
        ?? throw new InvalidOperationException("Renamed character lookup by AccountId failed.");
    Assert(renamed.Id == first.Id && renamed.LegacyUid == first.LegacyUid && renamed.Name == "SHEEKOX",
        "A display-name change altered immutable character identity.");

    await ExactForeignCharacterUidRejectedAsync(databaseUsername, databaseName, "exact5165e", password,
        checked((uint)first.LegacyUid), checked((uint)second.LegacyUid));
    Console.WriteLine("PASS immutable AccountId ownership, names, UID isolation, and foreign-action rejection");
}

static async Task ExactForeignCharacterUidRejectedAsync(string databaseUsername, string databaseName,
    string accountUsername, string password, uint foreignUid, uint expectedUid)
{
    int accountPort = FreePort(), gamePort = FreePort();
    while (gamePort == accountPort) gamePort = FreePort();
    using var output = new StringWriter();
    var log = new SafeConsoleLog(output, TextWriter.Null);
    await using var store = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName);
    var account = new Genuine5165RuntimeComponent("ExactAccount", IPAddress.Loopback, accountPort, 4, true,
        "10.0.0.1", gamePort, "127.0.0.1", store, log);
    var game = new Genuine5165RuntimeComponent("ExactGame", IPAddress.Loopback, gamePort, 4, false,
        "10.0.0.1", gamePort, "127.0.0.1", store, log,
        new GameplayWorld5165(new TestMapValidation5165()));
    await account.StartAsync(default);
    await game.StartAsync(default);
    try
    {
        ulong token = await ExactAccountLoginAsync(accountPort, accountUsername, password);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, gamePort);
        NetworkStream stream = client.GetStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using ExactGameClient5165 exact = await ExactGameClient5165.AcceptAsync(stream, timeout.Token);
        byte[] reply = exact.CreateDhReply();
        byte[] connect = ExactGameConnect(exact, token);
        try
        {
            await stream.WriteAsync(reply, timeout.Token);
            await stream.WriteAsync(connect, timeout.Token);
            byte[] entrance = await exact.ReadServerPacketAsync(stream, timeout.Token);
            byte[] information = await exact.ReadServerPacketAsync(stream, timeout.Token);
            try
            {
                Assert(GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(information).Id == expectedUid,
                    "The server selected a character outside the authenticated AccountId.");
            }
            finally { CryptographicOperations.ZeroMemory(entrance); CryptographicOperations.ZeroMemory(information); }
            byte[] action = ExactAction(foreignUid, 74);
            byte[] encrypted = exact.EncryptClientPacket(action);
            try { await stream.WriteAsync(encrypted, timeout.Token); }
            finally { CryptographicOperations.ZeroMemory(action); CryptographicOperations.ZeroMemory(encrypted); }
            byte[] closed = new byte[1];
            Assert(await stream.ReadAsync(closed, timeout.Token) == 0,
                "A packet referencing another account's character UID did not fail closed.");
        }
        finally { CryptographicOperations.ZeroMemory(reply); CryptographicOperations.ZeroMemory(connect); }
        await Task.Delay(50);
        Assert(output.ToString().Contains("protocol-validation", StringComparison.Ordinal),
            "Foreign character UID rejection was not safely classified.");
    }
    finally { await account.StopAsync(default); await game.StopAsync(default); }
}

static async Task<long> LegacyUidSequenceAsync(string username, string databaseName)
{
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = "/var/run/postgresql",
        Database = databaseName,
        Username = username,
        Pooling = false,
    };
    await using var connection = new NpgsqlConnection(builder.ConnectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("SELECT last_value FROM game.characters_legacy_uid_seq", connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

static async Task<ulong> ExactAccountLoginAsync(int port, string username, string password)
{
    byte[] plaintext = new byte[AccountPackets5165.LoginLength];
    byte[] encrypted = new byte[plaintext.Length];
    byte[] passwordBlock = new byte[16];
    byte[] transformedPassword = new byte[16];
    byte[] encryptedResponse = new byte[AccountPackets5165.ConnectSuccessLength];
    byte[] response = new byte[encryptedResponse.Length];
    try
    {
        BinaryPrimitives.WriteUInt16LittleEndian(plaintext, AccountPackets5165.LoginLength);
        BinaryPrimitives.WriteUInt16LittleEndian(plaintext.AsSpan(2), AccountPackets5165.LoginPacketId);
        System.Text.Encoding.ASCII.GetBytes(username, plaintext.AsSpan(4, 16));
        System.Text.Encoding.ASCII.GetBytes(password, passwordBlock);
        LegacyPasswordCipher5165.EncryptForTest(passwordBlock, transformedPassword);
        transformedPassword.CopyTo(plaintext, 132);
        "Revora"u8.CopyTo(plaintext.AsSpan(260));
        var cipher = new AccountCipher5165();
        cipher.EncryptClientForTest(plaintext, encrypted);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, port);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(encrypted.AsMemory(0, 137));
        await stream.WriteAsync(encrypted.AsMemory(137));
        await stream.ReadExactlyAsync(encryptedResponse);
        cipher.DecryptServerForTest(encryptedResponse, response);
        AccountConnectSuccess5165 success = AccountPackets5165.ParseSuccessAsExactClient(response);
        return ((ulong)success.HandoffWord << 32) | success.AccountWord;
    }
    finally
    {
        CryptographicOperations.ZeroMemory(plaintext);
        CryptographicOperations.ZeroMemory(encrypted);
        CryptographicOperations.ZeroMemory(passwordBlock);
        CryptographicOperations.ZeroMemory(transformedPassword);
        CryptographicOperations.ZeroMemory(encryptedResponse);
        CryptographicOperations.ZeroMemory(response);
    }
}

static async Task<uint> ExactCharacterCreationAndWorldAsync(int port, ulong token, string characterName)
{
    using var client = new TcpClient(AddressFamily.InterNetwork);
    await client.ConnectAsync(IPAddress.Loopback, port);
    NetworkStream stream = client.GetStream();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using ExactGameClient5165 exact = await ExactGameClient5165.AcceptAsync(stream, timeout.Token);
    byte[] reply = exact.CreateDhReply();
    byte[] connect = ExactGameConnect(exact, token);
    try
    {
        await stream.WriteAsync(reply, timeout.Token);
        await stream.WriteAsync(connect, timeout.Token);
        byte[] newRole = await exact.ReadServerPacketAsync(stream, timeout.Token);
        try
        {
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(newRole.AsSpan(2)) == 1004 &&
                System.Text.Encoding.ASCII.GetString(newRole).Contains("NEW_ROLE", StringComparison.Ordinal),
                "The zero-character path did not send NEW_ROLE.");
        }
        finally { CryptographicOperations.ZeroMemory(newRole); }

        byte[] registration = ExactClient5165RegistrationBuilder.BuildSmallMaleTaoist(characterName, (uint)token);
        byte[] encryptedRegistration = exact.EncryptClientPacket(registration);
        try
        {
            await stream.WriteAsync(encryptedRegistration.AsMemory(0, 3), timeout.Token);
            await stream.WriteAsync(encryptedRegistration.AsMemory(3, 57), timeout.Token);
            await stream.WriteAsync(encryptedRegistration.AsMemory(60), timeout.Token);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(registration);
            CryptographicOperations.ZeroMemory(encryptedRegistration);
        }
        byte[] answer = await exact.ReadServerPacketAsync(stream, timeout.Token);
        byte[] information = await exact.ReadServerPacketAsync(stream, timeout.Token);
        uint characterUid;
        try
        {
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(answer.AsSpan(2)) == 1004 &&
                System.Text.Encoding.ASCII.GetString(answer).Contains("ANSWER_OK", StringComparison.Ordinal),
                "The exact registration response was not ANSWER_OK.");
            UserInformation5165 exactUser = GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(information);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(information.AsSpan(2)) == 1006 && exactUser.Name == characterName,
                "Same-session registration did not continue directly with exact 1006 character information.");
            characterUid = exactUser.Id;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(answer);
            CryptographicOperations.ZeroMemory(information);
        }

        byte[] enterMap = ExactAction(characterUid, 74);
        byte[] encryptedEnterMap = exact.EncryptClientPacket(enterMap);
        byte[] questStatus = ExactObservedQuestStatus();
        byte[] encryptedQuestStatus = exact.EncryptClientPacket(questStatus);
        await stream.WriteAsync(encryptedEnterMap.AsMemory(0, 3), timeout.Token);
        await stream.WriteAsync(encryptedEnterMap.AsMemory(3), timeout.Token);
        await stream.WriteAsync(encryptedQuestStatus, timeout.Token);
        CryptographicOperations.ZeroMemory(enterMap);
        CryptographicOperations.ZeroMemory(encryptedEnterMap);
        CryptographicOperations.ZeroMemory(questStatus);
        CryptographicOperations.ZeroMemory(encryptedQuestStatus);
        byte[] action = await exact.ReadServerPacketAsync(stream, timeout.Token);
        byte[] map = await exact.ReadServerPacketAsync(stream, timeout.Token);
        try
        {
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(action.AsSpan(2)) == 10010 &&
                BinaryPrimitives.ReadUInt16LittleEndian(action.AsSpan(16)) == 74 &&
                BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan(2)) == 1110 &&
                BinaryPrimitives.ReadUInt32LittleEndian(map.AsSpan(4)) == 1002,
                "Same-session registration did not reach the authoritative starter-map sequence.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(action);
            CryptographicOperations.ZeroMemory(map);
        }

        foreach (ushort initializationAction in new ushort[] { 75, 76, 77, 78, 97 })
        {
            byte[] request = ExactAction(characterUid, initializationAction);
            byte[] encryptedRequest = exact.EncryptClientPacket(request);
            await stream.WriteAsync(encryptedRequest, timeout.Token);
            byte[] echoed = await exact.ReadServerPacketAsync(stream, timeout.Token);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(echoed.AsSpan(2)) == 10010 &&
                BinaryPrimitives.ReadUInt16LittleEndian(echoed.AsSpan(16)) == initializationAction,
                "The same registration Game connection did not preserve the exact initialization action chain.");
            CryptographicOperations.ZeroMemory(request);
            CryptographicOperations.ZeroMemory(encryptedRequest);
            CryptographicOperations.ZeroMemory(echoed);
        }

        byte[] replay = ExactClient5165RegistrationBuilder.BuildSmallMaleTaoist(characterName, (uint)token);
        byte[] encryptedReplay = exact.EncryptClientPacket(replay);
        await stream.WriteAsync(encryptedReplay, timeout.Token);
        CryptographicOperations.ZeroMemory(replay);
        CryptographicOperations.ZeroMemory(encryptedReplay);
        byte[] closed = new byte[1];
        Assert(await stream.ReadAsync(closed, timeout.Token) == 0,
            "A repeated 1001 was not rejected after the same session entered the world.");
        return characterUid;
    }
    finally
    {
        CryptographicOperations.ZeroMemory(reply);
        CryptographicOperations.ZeroMemory(connect);
    }
}

static async Task ExactWorldEntryAsync(int port, ulong token, uint characterUid, string characterName)
{
    using var client = new TcpClient(AddressFamily.InterNetwork);
    await client.ConnectAsync(IPAddress.Loopback, port);
    NetworkStream stream = client.GetStream();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using ExactGameClient5165 exact = await ExactGameClient5165.AcceptAsync(stream, timeout.Token);
    byte[] reply = exact.CreateDhReply();
    byte[] connect = ExactGameConnect(exact, token);
    try
    {
        await stream.WriteAsync(reply, timeout.Token);
        await stream.WriteAsync(connect, timeout.Token);
        byte[] entrance = await exact.ReadServerPacketAsync(stream, timeout.Token);
        byte[] information = await exact.ReadServerPacketAsync(stream, timeout.Token);
        try
        {
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(entrance.AsSpan(2)) == 1004 &&
                System.Text.Encoding.ASCII.GetString(entrance).Contains("ANSWER_OK", StringComparison.Ordinal),
                "The reconnect entrance response was invalid.");
            UserInformation5165 exactUser = GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(information);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(information.AsSpan(2)) == 1006 &&
                information.Length == 94 + characterName.Length && exactUser.Id == characterUid && exactUser.Name == characterName &&
                exactUser.Spouse == "None" && information[GamePacketsAfterAuthentication5165.UserInfoStringPackOffset] == 2,
                "The reconnect character information was invalid.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entrance);
            CryptographicOperations.ZeroMemory(information);
        }

        // The exact 5165 handler only constructs action 74 after parsing the
        // complete fixed region and the string pack registered at offset 87.
        byte[] enterMap = ExactAction(characterUid, 74);
        byte[] encryptedEnterMap = exact.EncryptClientPacket(enterMap);
        byte[] questStatus = ExactObservedQuestStatus();
        byte[] encryptedQuestStatus = exact.EncryptClientPacket(questStatus);
        byte[] coalescedObservedOrder = new byte[encryptedEnterMap.Length + encryptedQuestStatus.Length];
        encryptedEnterMap.CopyTo(coalescedObservedOrder, 0);
        encryptedQuestStatus.CopyTo(coalescedObservedOrder, encryptedEnterMap.Length);
        await stream.WriteAsync(coalescedObservedOrder, timeout.Token);
        CryptographicOperations.ZeroMemory(enterMap);
        CryptographicOperations.ZeroMemory(encryptedEnterMap);
        CryptographicOperations.ZeroMemory(questStatus);
        CryptographicOperations.ZeroMemory(encryptedQuestStatus);
        CryptographicOperations.ZeroMemory(coalescedObservedOrder);
        byte[] action = await exact.ReadServerPacketAsync(stream, timeout.Token);
        byte[] map = await exact.ReadServerPacketAsync(stream, timeout.Token);
        try
        {
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(action.AsSpan(2)) == 10010 &&
                BinaryPrimitives.ReadUInt16LittleEndian(action.AsSpan(16)) == 74,
                "The authoritative enter-map action was invalid.");
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan(2)) == 1110 &&
                BinaryPrimitives.ReadUInt32LittleEndian(map.AsSpan(4)) == 1002,
                "The starter map response was invalid.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(action);
            CryptographicOperations.ZeroMemory(map);
        }

        foreach (ushort initializationAction in new ushort[] { 75, 76, 77, 78, 97 })
        {
            byte[] request = ExactAction(characterUid, initializationAction);
            byte[] encryptedRequest = exact.EncryptClientPacket(request);
            await stream.WriteAsync(encryptedRequest, timeout.Token);
            byte[] echoed = await exact.ReadServerPacketAsync(stream, timeout.Token);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(echoed.AsSpan(2)) == 10010 &&
                BinaryPrimitives.ReadUInt16LittleEndian(echoed.AsSpan(16)) == initializationAction,
                "The existing-character Game connection did not preserve the exact initialization action chain.");
            CryptographicOperations.ZeroMemory(request);
            CryptographicOperations.ZeroMemory(encryptedRequest);
            CryptographicOperations.ZeroMemory(echoed);
        }

        // Exercise the actual encrypted production session after world entry,
        // not just the isolated gameplay coordinator. Direction 6 is east in
        // the exact client table, so the authoritative walk ends at 431,378.
        byte[] walk = GamePacketsAfterAuthentication5165.EncodeMovement(6, characterUid, 0, 0x12345678);
        byte[] encryptedWalk = exact.EncryptClientPacket(walk);
        await stream.WriteAsync(encryptedWalk, timeout.Token);
        byte[] authoritativeWalk = await exact.ReadServerPacketAsync(stream, timeout.Token);
        GameMovement5165 parsedWalk = GamePacketsAfterAuthentication5165.ParseMovement(authoritativeWalk);
        Assert(parsedWalk.Id == characterUid && parsedWalk.Direction == 6 && parsedWalk.Mode == 0,
            "The encrypted production path did not return the authoritative movement packet.");

        byte[] jump = GamePacketsAfterAuthentication5165.EncodeJump(characterUid, 440, 381, 431, 378, 6, 0x12345679);
        byte[] encryptedJump = exact.EncryptClientPacket(jump);
        await stream.WriteAsync(encryptedJump, timeout.Token);
        byte[] authoritativeJump = await exact.ReadServerPacketAsync(stream, timeout.Token);
        GameAction5165 parsedJump = GamePacketsAfterAuthentication5165.ParseAction(authoritativeJump);
        Assert(parsedJump.Id == characterUid && parsedJump.Action == GamePacketsAfterAuthentication5165.JumpAction &&
            parsedJump.X == 431 && parsedJump.Y == 378 && parsedJump.Data == ((uint)440 | ((uint)381 << 16)),
            "The encrypted production path did not return the authoritative jump packet.");

        CryptographicOperations.ZeroMemory(walk);
        CryptographicOperations.ZeroMemory(encryptedWalk);
        CryptographicOperations.ZeroMemory(authoritativeWalk);
        CryptographicOperations.ZeroMemory(jump);
        CryptographicOperations.ZeroMemory(encryptedJump);
        CryptographicOperations.ZeroMemory(authoritativeJump);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(reply);
        CryptographicOperations.ZeroMemory(connect);
    }
}

static async Task ProductionShapedDisabledCombatHostAsync(string hostExecutable, string databaseName,
    string databaseUsername, string dataDirectory)
{
    Assert(File.Exists(hostExecutable), "The production-shaped Host artifact is missing.");
    Assert(Directory.Exists(Path.Combine(dataDirectory, "maps")), "The reviewed map directory is missing.");
    string work = Path.Combine(Path.GetTempPath(), $"revora-disabled-combat-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(work, "config"));
    Directory.CreateDirectory(Path.Combine(work, "logs"));
    Directory.CreateDirectory(Path.Combine(work, "data", "maps"));
    string sourceMaps = Path.Combine(dataDirectory, "maps");
    foreach (string map in Directory.EnumerateFiles(sourceMaps, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(sourceMaps, map);
        string destination = Path.GetFullPath(Path.Combine(work, "data", "maps", relative));
        if (!destination.StartsWith(Path.GetFullPath(Path.Combine(work, "data", "maps")) +
                Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("The reviewed map fixture escaped its isolated root.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(map, destination);
    }
    int accountPort = FreePort(), gamePort = FreePort();
    while (gamePort == accountPort) gamePort = FreePort();
    await File.WriteAllTextAsync(Path.Combine(work, "config", "revora.json"), JsonSerializer.Serialize(new
    {
        InstanceName = "Disabled combat production adapter test",
        RealmName = "Revora",
        Environment = "OwnerTest",
        RuntimeAdapter = "PostgreSql5165",
        AccountListener = new { Address = "127.0.0.1", Port = accountPort },
        GameListener = new { Address = "127.0.0.1", Port = gamePort },
        AdvertisedGameAddress = "10.0.0.1",
        OwnerSourceAddress = "127.0.0.1",
        MaximumPlayers = 4,
        ShutdownTimeoutSeconds = 10,
        SaveIntervalSeconds = 300,
        RegistrationEnabled = false,
        MaintenanceMode = true,
        ApplyMigrations = false,
        DataDirectory = "data",
        LogDirectory = "logs",
        SecretFile = "config/revora.secrets.json"
    }));
    string secret = Path.Combine(work, "config", "revora.secrets.json");
    await File.WriteAllTextAsync(secret, JsonSerializer.Serialize(new
    {
        DatabaseMode = "LocalPeerSocket", PostgreSqlSocketPath = "/var/run/postgresql",
        PostgreSqlUsername = databaseUsername, PostgreSqlDatabase = databaseName
    }));
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(secret, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    try
    {
        await using HostProcess host = await HostProcess.StartAsync(hostExecutable, work);
        await host.StopAsync();
        Assert(host.Output.Split('\n').Count(line => line == "[Revora] READY") == 1 &&
            host.Output.Split('\n').Count(line => line == "[Revora] STOPPED") == 1,
            "The production adapter did not preserve the disabled-combat lifecycle.");
    }
    finally { Directory.Delete(work, recursive: true); }
    Console.WriteLine("PASS production-shaped PostgreSql5165 Host starts and stops with combat and Ninja disabled");
}

static async Task GenuineEncryptedCombatAsync(string databaseName, string databaseUsername)
{
    const uint physicalMonsterUid = 599_996;
    const uint magicMonsterUid = 599_997;
    string username = $"combat{Guid.NewGuid():N}"[..15];
    string password = "combatpass5165";
    Guid accountId = Guid.NewGuid();
    var connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = "/var/run/postgresql", Database = databaseName, Username = databaseUsername,
        Pooling = false
    }.ConnectionString;
    var hasher = new AdaptivePasswordHasher();
    await using (var setup = new NpgsqlConnection(connectionString))
    {
        await setup.OpenAsync();
        await using NpgsqlTransaction transaction = await setup.BeginTransactionAsync();
        await ExecuteAsync("INSERT INTO identity.game_accounts(account_id,username,normalized_username,password_hash,password_algorithm,password_version) " +
            "VALUES($1,$2,lower($2),$3,'pbkdf2-sha512',1)", accountId, username, hasher.Hash(password));
        await ExecuteAsync("UPDATE gameplay.feature_gates SET gate_state='enabled',required_release_id=(SELECT release_id FROM gameplay_reference.catalog_releases WHERE release_state='active') " +
            "WHERE feature_key IN ('world.catalog.complete','items.definitions','inventory','currency.ledger','equipment.stats','shops','warehouse','monsters','combat.pk','combat.runtime'); " +
            "UPDATE gameplay.feature_rollouts SET rollout_scope='global' WHERE feature_key IN " +
            "('world.catalog.complete','items.definitions','inventory','currency.ledger','equipment.stats','shops','warehouse','monsters','combat.pk'); " +
            "UPDATE gameplay.feature_rollouts SET rollout_scope='canary' WHERE feature_key='combat.runtime'");
        await transaction.CommitAsync();

        async Task ExecuteAsync(string sql, params object[] values)
        {
            await using var command = new NpgsqlCommand(sql, setup, transaction);
            foreach (object value in values) command.Parameters.AddWithValue(value);
            await command.ExecuteNonQueryAsync();
        }
    }

    await using var store = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName);
    CharacterState character = await store.CreateCharacterAsync(accountId,
        $"C{accountId:N}"[..11], 1003, 10, default);
    character = await store.TransitionMapAsync(character.Id, 1002, 453, 455, 0, character.Version, default)
        ?? throw new InvalidOperationException("Combat fixture relocation failed.");
    await using (var setup = new NpgsqlConnection(connectionString))
    {
        await setup.OpenAsync();
        await using NpgsqlTransaction transaction = await setup.BeginTransactionAsync();
        await ExecuteAsync("INSERT INTO game.character_attributes(character_id,stamina) VALUES($1,100) ON CONFLICT DO NOTHING", character.Id);
        await ExecuteAsync("INSERT INTO game.inventory_profiles(character_id,slot_capacity) VALUES($1,40) ON CONFLICT DO NOTHING", character.Id);
        await ExecuteAsync("INSERT INTO game.character_vitals(character_id,current_health,current_mana) VALUES($1,10000,0) " +
            "ON CONFLICT(character_id) DO UPDATE SET current_health=10000", character.Id);
        await ExecuteAsync("INSERT INTO game.character_stat_snapshots(character_id,character_row_version,inventory_revision,maximum_health,maximum_mana,minimum_attack,maximum_attack,defense,magic_attack,magic_defense,accuracy,dodge) " +
            "VALUES($1,$2,0,100,0,1,1,0,0,0,0,0) ON CONFLICT DO NOTHING", character.Id, character.Version);
        await ExecuteAsync("INSERT INTO gameplay.feature_canaries(feature_key,account_id,character_id,legacy_character_uid,enabled_by) VALUES('combat.runtime',$1,$2,$3,'encrypted-combat-test')", accountId, character.Id, character.LegacyUid);
        await ExecuteAsync("SET LOCAL ROLE revora_migrator");
        await ExecuteAsync("UPDATE gameplay_reference.monster_spawn_regions SET activation_category='event-gated'");
        await ExecuteAsync("UPDATE gameplay_reference.monster_types SET maximum_health=1,behavior=0,minimum_silver=0,maximum_silver=0 " +
            "WHERE monster_type=1");
        await ExecuteAsync("INSERT INTO gameplay_reference.monster_drop_rules(source_id,monster_type,ordinal,item_source_id,item_type,chance_basis_points,canonical_record_sha256) " +
            "SELECT t.source_id,1,32767,i.source_id,150005,1000000,repeat('f',64) FROM gameplay_reference.monster_types t " +
            "CROSS JOIN LATERAL (SELECT source_id FROM gameplay_reference.item_types WHERE item_type=150005 ORDER BY source_id LIMIT 1) i " +
            "WHERE t.monster_type=1 ORDER BY t.source_id LIMIT 1 ON CONFLICT DO NOTHING");
        await ExecuteAsync("RESET ROLE");
        await ExecuteAsync("INSERT INTO gameplay_reference.monster_spawn_regions(source_id,spawn_region_id,monster_type,map_source_id,map_id,requested_population,minimum_x,minimum_y,maximum_x,maximum_y,enabled,activation_category,canonical_record_sha256) " +
            "SELECT t.source_id,9000,1,m.source_id,1002,1,454,455,454,455,true,'normal',repeat('e',64) " +
            "FROM gameplay_reference.monster_types t CROSS JOIN LATERAL " +
            "(SELECT source_id FROM gameplay_reference.maps WHERE legacy_map_id=1002 ORDER BY source_id LIMIT 1) m " +
            "WHERE t.monster_type=1 ORDER BY t.source_id LIMIT 1 ON CONFLICT(source_id,spawn_region_id) DO UPDATE " +
            "SET enabled=true,activation_category='normal'");
        await ExecuteAsync("INSERT INTO gameplay_reference.monster_spawn_regions(source_id,spawn_region_id,monster_type,map_source_id,map_id,requested_population,minimum_x,minimum_y,maximum_x,maximum_y,enabled,activation_category,canonical_record_sha256) " +
            "SELECT t.source_id,9001,107,m.source_id,1002,1,456,455,456,455,true,'normal',repeat('d',64) " +
            "FROM gameplay_reference.monster_types t CROSS JOIN LATERAL " +
            "(SELECT source_id FROM gameplay_reference.maps WHERE legacy_map_id=1002 ORDER BY source_id LIMIT 1) m " +
            "WHERE t.monster_type=107 ORDER BY t.source_id LIMIT 1 ON CONFLICT(source_id,spawn_region_id) DO UPDATE " +
            "SET enabled=true,activation_category='normal'");
        await ExecuteAsync("SET LOCAL ROLE revora_migrator");
        await ExecuteAsync("INSERT INTO gameplay_reference.monster_runtime_uid_assignments(source_id,spawn_region_id,ordinal,legacy_uid) " +
            "SELECT source_id,spawn_region_id,0,$1 FROM gameplay_reference.monster_spawn_regions WHERE spawn_region_id=9000 " +
            "ON CONFLICT(source_id,spawn_region_id,ordinal) DO UPDATE SET legacy_uid=excluded.legacy_uid",
            checked((int)physicalMonsterUid));
        await ExecuteAsync("INSERT INTO gameplay_reference.monster_runtime_uid_assignments(source_id,spawn_region_id,ordinal,legacy_uid) " +
            "SELECT source_id,spawn_region_id,0,$1 FROM gameplay_reference.monster_spawn_regions WHERE spawn_region_id=9001 " +
            "ON CONFLICT(source_id,spawn_region_id,ordinal) DO UPDATE SET legacy_uid=excluded.legacy_uid",
            checked((int)magicMonsterUid));
        await ExecuteAsync("RESET ROLE");
        await transaction.CommitAsync();

        async Task ExecuteAsync(string sql, params object[] values)
        {
            await using var command = new NpgsqlCommand(sql, setup, transaction);
            foreach (object value in values) command.Parameters.AddWithValue(value);
            await command.ExecuteNonQueryAsync();
        }
    }

    using var output = new StringWriter();
    var log = new SafeConsoleLog(output, TextWriter.Null);
    await using var combat = new LiveCombatRuntime5165(connectionString, log,
        new MapCatalog5165([new TestMapValidation5165()]));
    await combat.InitializeAsync(default);
    await using ItemEconomyRuntime5165 economy = await ItemEconomyRuntime5165.CreateAsync(connectionString, default);
    var world = new GameplayWorld5165(new MapCatalog5165([new TestMapValidation5165(1002)]), new());
    int accountPort = FreePort(), gamePort = FreePort();
    while (gamePort == accountPort) gamePort = FreePort();
    var account = new Genuine5165RuntimeComponent("CombatAccount", IPAddress.Loopback, accountPort, 4, true,
        "10.0.0.1", gamePort, "127.0.0.1", store, log);
    var game = new Genuine5165RuntimeComponent("CombatGame", IPAddress.Loopback, gamePort, 4, false,
        "10.0.0.1", gamePort, "127.0.0.1", store, log, world, economy, combat);
    await account.StartAsync(default); await game.StartAsync(default);
    try
    {
        ulong token = await ExactAccountLoginAsync(accountPort, username, password);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, gamePort);
        NetworkStream stream = client.GetStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using ExactGameClient5165 exact = await ExactGameClient5165.AcceptAsync(stream, timeout.Token);
        byte[] dh = exact.CreateDhReply(); byte[] connect = ExactGameConnect(exact, token);
        await stream.WriteAsync(dh, timeout.Token); await stream.WriteAsync(connect, timeout.Token);
        CryptographicOperations.ZeroMemory(dh); CryptographicOperations.ZeroMemory(connect);
        await ReadIdAsync(1004); await ReadIdAsync(1006);
        await SendAsync(ExactAction(checked((uint)character.LegacyUid), 74));
        await ReadUntilIdAsync(10010, 12); await ReadIdAsync(1110);
        byte[] monsterPacket = await ReadUntilIdAsync(CombatPackets5165.EntitySpawnPacketId, 6);
        MonsterSpawn5165 monster = CombatPackets5165.ParseMonsterSpawnAsExactClient(monsterPacket);
        Assert(monster.Uid == physicalMonsterUid && monster.Health == 1 && monster.X == 454 && monster.Y == 455,
            "The encrypted Host did not deliver the reviewed monster entity.");
        byte[] magicFrame = await ReadUntilIdAsync(NinjaPackets5165.SkillUsePacketId, 16);
        SkillUse5165 magic = NinjaPackets5165.ParseSkillUseAsExactClient(magicFrame);
        Assert(magic.ActorUid == magicMonsterUid && magic.SkillId == 1001 &&
            magic.Targets.Count == 1 && magic.Targets[0].TargetUid == checked((uint)character.LegacyUid),
            "The encrypted Host did not present the reviewed monster magic attack to the exact-client fixture.");
        byte[] attack = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(attack, 32);
        BinaryPrimitives.WriteUInt16LittleEndian(attack.AsSpan(2), NinjaPackets5165.InteractionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(attack.AsSpan(4), 0x51560001);
        BinaryPrimitives.WriteUInt32LittleEndian(attack.AsSpan(8), checked((uint)character.LegacyUid));
        BinaryPrimitives.WriteUInt32LittleEndian(attack.AsSpan(12), monster.Uid);
        // The genuine client can submit the last displayed coordinates while an
        // authoritative monster movement is already in flight. Combat must bind by UID
        // and validate current server distance instead of requiring byte-for-byte equality.
        BinaryPrimitives.WriteUInt16LittleEndian(attack.AsSpan(16), checked((ushort)(monster.X - 1)));
        BinaryPrimitives.WriteUInt16LittleEndian(attack.AsSpan(18), monster.Y);
        BinaryPrimitives.WriteUInt32LittleEndian(attack.AsSpan(20), NinjaPackets5165.MeleeAction);
        byte[] replayAttack = attack.ToArray();
        await SendFragmentedAsync(attack);
        byte[] response = await ReadUntilAttackFromAsync(checked((uint)character.LegacyUid), 8);
        Assert(BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(24)) == 1,
            "The encrypted Host did not return authoritative bounded damage.");
        byte[] groundSpawn = await ReadUntilIdAsync(GroundItemPackets5165.PacketId, 4);
        GroundItemAction5165 visibleDrop = GroundItemPackets5165.Parse(groundSpawn);
        Assert(visibleDrop.Action == GroundItemPackets5165.SpawnAction && visibleDrop.ItemType == 150005,
            "The encrypted Host did not broadcast the committed ground item.");
        byte[] pickup = new byte[GroundItemPackets5165.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(pickup, GroundItemPackets5165.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(pickup.AsSpan(2), GroundItemPackets5165.PacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(pickup.AsSpan(4), visibleDrop.GroundUid);
        BinaryPrimitives.WriteUInt32LittleEndian(pickup.AsSpan(8), 0x7914A09A);
        BinaryPrimitives.WriteUInt16LittleEndian(pickup.AsSpan(12), visibleDrop.X);
        BinaryPrimitives.WriteUInt16LittleEndian(pickup.AsSpan(14), visibleDrop.Y);
        pickup[18] = checked((byte)GroundItemPackets5165.ItemCategory);
        await SendFragmentedAsync(pickup);
        await ReadUntilGroundActionAsync(visibleDrop.GroundUid, GroundItemPackets5165.RemoveAction, 12);
        await ReadUntilIdAsync(ItemEconomyPackets5165.ItemInfoPacketId, 8);
        byte[] semanticallyInvalidPickup = new byte[GroundItemPackets5165.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(semanticallyInvalidPickup, GroundItemPackets5165.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(semanticallyInvalidPickup.AsSpan(2), GroundItemPackets5165.PacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(semanticallyInvalidPickup.AsSpan(4), visibleDrop.GroundUid);
        semanticallyInvalidPickup[18] = checked((byte)GroundItemPackets5165.RemoveAction);
        await SendCoalescedAsync(semanticallyInvalidPickup,
            GamePacketsAfterAuthentication5165.EncodeMovement(6,
                checked((uint)character.LegacyUid), 0, 0x51560005));
        await ReadUntilIdAsync(GamePacketsAfterAuthentication5165.MovementPacketId, 8);
        await SendCoalescedAsync(replayAttack, GamePacketsAfterAuthentication5165.EncodeMovement(6,
            checked((uint)character.LegacyUid), 0, 0x51560002)); // replay + movement in one TCP write
        await ReadUntilIdAsync(GamePacketsAfterAuthentication5165.MovementPacketId, 8);
        byte[] malformed = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(malformed, 32);
        BinaryPrimitives.WriteUInt16LittleEndian(malformed.AsSpan(2), NinjaPackets5165.InteractionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(4), 0x51560003);
        BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(8), checked((uint)character.LegacyUid));
        BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(12), monster.Uid);
        BinaryPrimitives.WriteUInt16LittleEndian(malformed.AsSpan(16), monster.X);
        BinaryPrimitives.WriteUInt16LittleEndian(malformed.AsSpan(18), monster.Y);
        BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(20), NinjaPackets5165.MeleeAction);
        malformed[31] = 1;
        await SendCoalescedAsync(malformed, GamePacketsAfterAuthentication5165.EncodeMovement(6,
            checked((uint)character.LegacyUid), 0, 0x51560004));
        await ReadUntilIdAsync(GamePacketsAfterAuthentication5165.MovementPacketId, 8);

        async Task SendAsync(byte[] plain)
        {
            byte[] encrypted = exact.EncryptClientPacket(plain);
            await stream.WriteAsync(encrypted, timeout.Token);
            CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(encrypted);
        }
        async Task SendFragmentedAsync(byte[] plain)
        {
            byte[] encrypted = exact.EncryptClientPacket(plain);
            int split = Math.Min(7, encrypted.Length);
            await stream.WriteAsync(encrypted.AsMemory(0, split), timeout.Token);
            await stream.WriteAsync(encrypted.AsMemory(split), timeout.Token);
            CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(encrypted);
        }
        async Task SendCoalescedAsync(byte[] firstPlain, byte[] secondPlain)
        {
            byte[] firstEncrypted = exact.EncryptClientPacket(firstPlain);
            byte[] secondEncrypted = exact.EncryptClientPacket(secondPlain);
            byte[] combined = new byte[firstEncrypted.Length + secondEncrypted.Length];
            firstEncrypted.CopyTo(combined, 0); secondEncrypted.CopyTo(combined, firstEncrypted.Length);
            await stream.WriteAsync(combined, timeout.Token);
            CryptographicOperations.ZeroMemory(firstPlain); CryptographicOperations.ZeroMemory(secondPlain);
            CryptographicOperations.ZeroMemory(firstEncrypted); CryptographicOperations.ZeroMemory(secondEncrypted);
            CryptographicOperations.ZeroMemory(combined);
        }
        async Task<byte[]> ReadIdAsync(ushort expected)
        {
            byte[] packet = await exact.ReadServerPacketAsync(stream, timeout.Token);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)) == expected,
                $"Expected {expected}; received {BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2))}.");
            return packet;
        }
        async Task<byte[]> ReadUntilIdAsync(ushort expected, int maximumFrames)
        {
            for (int index = 0; index < maximumFrames; index++)
            {
                byte[] packet = await exact.ReadServerPacketAsync(stream, timeout.Token);
                if (BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)) == expected) return packet;
            }
            throw new InvalidOperationException($"Expected packet {expected} was not received within {maximumFrames} frames.");
        }
        async Task ReadUntilGroundActionAsync(uint uid, ushort action, int maximumFrames)
        {
            for (int index = 0; index < maximumFrames; index++)
            {
                byte[] packet = await exact.ReadServerPacketAsync(stream, timeout.Token);
                if (BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)) != GroundItemPackets5165.PacketId)
                    continue;
                GroundItemAction5165 candidate = GroundItemPackets5165.Parse(packet);
                if (candidate.GroundUid == uid && candidate.Action == action) return;
            }
            throw new InvalidOperationException($"Ground item {uid} did not receive action {action}.");
        }
        async Task<byte[]> ReadUntilAttackFromAsync(uint attackerUid, int maximumFrames)
        {
            for (int index = 0; index < maximumFrames; index++)
            {
                byte[] packet = await exact.ReadServerPacketAsync(stream, timeout.Token);
                if (BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)) ==
                        NinjaPackets5165.InteractionPacketId && packet.Length >= 28 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8)) == attackerUid)
                    return packet;
            }
            throw new InvalidOperationException($"No authoritative attack response from {attackerUid} was received.");
        }
    }
    finally { await account.StopAsync(default); await game.StopAsync(default); }
    Assert(output.ToString().Contains("Combat1022Accepted", StringComparison.Ordinal) &&
        !output.ToString().Contains(password, StringComparison.Ordinal),
        "The encrypted combat audit evidence was missing or unsafe.");
    Console.WriteLine("PASS genuine encrypted 5165 monster spawn, normal combat, replay and movement-after-attack campaign");
}

static async Task GenuineEncryptedNinjaCombatAsync(string databaseName, string databaseUsername)
{
    const uint ninjaMonsterUid = 599_998;
    string username = $"ninja{Guid.NewGuid():N}"[..15];
    const string password = "ninjapass5165";
    Guid accountId = Guid.NewGuid(), targetAccountId = Guid.NewGuid();
    var connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = "/var/run/postgresql", Database = databaseName, Username = databaseUsername, Pooling = false
    }.ConnectionString;
    var hasher = new AdaptivePasswordHasher();
    await using (var setup = new NpgsqlConnection(connectionString))
    {
        await setup.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO identity.game_accounts(account_id,username,normalized_username,password_hash,password_algorithm,password_version) " +
            "VALUES($1,$2,lower($2),$3,'pbkdf2-sha512',1),($4,$5,lower($5),$6,'pbkdf2-sha512',1)", setup);
        command.Parameters.AddWithValue(accountId); command.Parameters.AddWithValue(username);
        command.Parameters.AddWithValue(hasher.Hash(password)); command.Parameters.AddWithValue(targetAccountId);
        command.Parameters.AddWithValue($"target{Guid.NewGuid():N}"[..15]);
        command.Parameters.AddWithValue(hasher.Hash(password));
        await command.ExecuteNonQueryAsync();
        await using (var enable = new NpgsqlCommand(
            "UPDATE gameplay.feature_gates SET gate_state='enabled',required_release_id=" +
            "(SELECT release_id FROM gameplay_reference.catalog_releases WHERE release_state='active') " +
            "WHERE feature_key='ninja.native'; " +
            "UPDATE gameplay.feature_rollouts SET rollout_scope='canary' WHERE feature_key='ninja.native'", setup))
            await enable.ExecuteNonQueryAsync();
        await using (var authorize = new NpgsqlCommand(
            "INSERT INTO gameplay.account_feature_canaries(feature_key,account_id,enabled_by) " +
            "VALUES('ninja.native',$1,'encrypted-ninja-test')", setup))
        {
            authorize.Parameters.AddWithValue(accountId);
            await authorize.ExecuteNonQueryAsync();
        }
    }

    await using var store = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName);
    CharacterState ninja = await store.CreateCharacterAsync(accountId, $"N{Guid.NewGuid():N}"[..15], 1003, 50, default);
    CharacterState target = await store.CreateCharacterAsync(targetAccountId, $"T{Guid.NewGuid():N}"[..15], 1004, 40, default);
    ninja = await store.TransitionMapAsync(ninja.Id, 1002, 453, 455, 0, ninja.Version, default)
        ?? throw new InvalidOperationException("Ninja fixture relocation failed.");
    target = await store.TransitionMapAsync(target.Id, 1002, 454, 455, 0, target.Version, default)
        ?? throw new InvalidOperationException("Ninja target relocation failed.");

    await using (var setup = new NpgsqlConnection(connectionString))
    {
        await setup.OpenAsync();
        await using NpgsqlTransaction transaction = await setup.BeginTransactionAsync();
        await ExecuteAsync("UPDATE game.characters SET level=120 WHERE character_id IN ($1,$2)", ninja.Id, target.Id);
        await ExecuteAsync("INSERT INTO game.character_vitals(character_id,current_health,current_mana) VALUES($1,100,0),($2,100,0) ON CONFLICT DO NOTHING",
            ninja.Id, target.Id);
        await ExecuteAsync("INSERT INTO game.character_attributes(character_id,stamina) VALUES($1,100),($2,100) ON CONFLICT DO NOTHING",
            ninja.Id, target.Id);
        await ExecuteAsync("INSERT INTO game.character_stat_snapshots(character_id,character_row_version,inventory_revision," +
            "maximum_health,maximum_mana,minimum_attack,maximum_attack,defense,magic_attack,magic_defense,accuracy,dodge) " +
            "VALUES($1,1,0,100,0,20,40,5,0,0,50,5),($2,1,0,100,0,20,40,5,0,0,50,5) " +
            "ON CONFLICT(character_id) DO UPDATE SET maximum_health=excluded.maximum_health," +
            "maximum_mana=excluded.maximum_mana,minimum_attack=excluded.minimum_attack," +
            "maximum_attack=excluded.maximum_attack,defense=excluded.defense," +
            "magic_attack=excluded.magic_attack,magic_defense=excluded.magic_defense," +
            "accuracy=excluded.accuracy,dodge=excluded.dodge", ninja.Id, target.Id);
        await ExecuteAsync("UPDATE game.character_vitals SET current_health=100,row_version=1 WHERE character_id IN ($1,$2)", ninja.Id, target.Id);
        await ExecuteAsync("UPDATE game.character_attributes SET stamina=100 WHERE character_id=$1", ninja.Id);
        await ExecuteAsync("INSERT INTO gameplay_reference.item_types(source_id,item_type,safe_name,item_class,equipment_slot,required_profession,required_level,minimum_attack,maximum_attack,maximum_durability,silver_price,canonical_record_sha256) " +
            "VALUES('51650034-0000-4e56-8000-000000000001',601000,'MetalKatana',601,4,50,5,7,20,1599,102,repeat('f',64)) ON CONFLICT DO NOTHING");
        foreach (short slot in new short[] { 4, 5 })
        {
            Guid item = Guid.NewGuid();
            await ExecuteAsync("INSERT INTO game.item_instances(item_instance_id,owner_character_id,item_source_id,item_type,durability,maximum_durability) " +
                "VALUES($1,$2,'51650034-0000-4e56-8000-000000000001',601000,1599,1599)", item, ninja.Id);
            await ExecuteAsync("INSERT INTO game.item_locations(item_instance_id,owner_character_id,location_kind,slot) VALUES($1,$2,'equipment',$3)",
                item, ninja.Id, slot);
        }
        await ExecuteAsync("INSERT INTO game.character_skills(character_id,magic_source_id,magic_type,level) " +
            "SELECT $1,source_id,magic_type,0 FROM (SELECT DISTINCT ON (magic_type) source_id,magic_type FROM gameplay_reference.magic_types " +
            "WHERE magic_type=ANY($2) AND level=0 ORDER BY magic_type,source_id) s ON CONFLICT DO NOTHING",
            ninja.Id, new[] { 6000, 6001, 6002, 6003, 6004, 6010, 6011 });
        await ExecuteAsync("UPDATE gameplay.feature_gates SET gate_state='enabled',required_release_id=(SELECT release_id FROM gameplay_reference.catalog_releases WHERE release_state='active') " +
            "WHERE feature_key IN ('world.catalog.complete','items.definitions','inventory','equipment.stats','monsters','combat.pk','skills','combat.runtime','ninja.native'); " +
            "UPDATE gameplay.feature_rollouts SET rollout_scope='global' WHERE feature_key IN " +
            "('world.catalog.complete','items.definitions','inventory','equipment.stats','monsters','combat.pk','skills'); " +
            "UPDATE gameplay.feature_rollouts SET rollout_scope='canary' WHERE feature_key IN ('combat.runtime','ninja.native')");
        await ExecuteAsync("INSERT INTO gameplay.feature_canaries(feature_key,account_id,character_id,legacy_character_uid,enabled_by) VALUES " +
            "('combat.runtime',$1,$2,$3,'encrypted-ninja-test'),('ninja.native',$1,$2,$3,'encrypted-ninja-test')," +
            "('combat.runtime',$4,$5,$6,'encrypted-ninja-test')", accountId, ninja.Id, ninja.LegacyUid,
            targetAccountId, target.Id, target.LegacyUid);
        await ExecuteAsync("UPDATE gameplay_reference.monster_spawn_regions SET enabled=false; " +
            "UPDATE gameplay_reference.monster_types SET behavior=0,maximum_health=10000 WHERE monster_type=1; " +
            "INSERT INTO gameplay_reference.monster_spawn_regions(source_id,spawn_region_id,monster_type,map_source_id,map_id," +
            "requested_population,minimum_x,minimum_y,maximum_x,maximum_y,enabled,canonical_record_sha256,activation_category) " +
            "SELECT t.source_id,9100,1,m.source_id,1002,1,454,456,454,456,true,repeat('d',64),'normal' " +
            "FROM gameplay_reference.monster_types t JOIN gameplay_reference.maps m ON m.legacy_map_id=1002 " +
            "WHERE t.monster_type=1 ORDER BY t.source_id,m.source_id LIMIT 1 " +
            "ON CONFLICT(source_id,spawn_region_id) DO UPDATE SET enabled=true,activation_category='normal'," +
            "minimum_x=454,minimum_y=456,maximum_x=454,maximum_y=456,requested_population=1");
        await ExecuteAsync("SET LOCAL ROLE revora_migrator");
        await ExecuteAsync("INSERT INTO gameplay_reference.monster_runtime_uid_assignments(source_id,spawn_region_id,ordinal,legacy_uid) " +
            "SELECT source_id,spawn_region_id,0,$1 FROM gameplay_reference.monster_spawn_regions WHERE spawn_region_id=9100 " +
            "ON CONFLICT(source_id,spawn_region_id,ordinal) DO UPDATE SET legacy_uid=excluded.legacy_uid",
            checked((int)ninjaMonsterUid));
        await ExecuteAsync("RESET ROLE");
        await transaction.CommitAsync();

        async Task ExecuteAsync(string sql, params object[] values)
        {
            await using var command = new NpgsqlCommand(sql, setup, transaction);
            foreach (object value in values) command.Parameters.AddWithValue(value);
            await command.ExecuteNonQueryAsync();
        }
    }

    using var output = new StringWriter();
    var log = new SafeConsoleLog(output, TextWriter.Null);
    await using var combat = new LiveCombatRuntime5165(connectionString, log,
        new MapCatalog5165([new TestMapValidation5165()]), new HashSet<int> { 1002 });
    await combat.InitializeAsync(default);
    var targetPackets = new List<byte[]>();
    var targetSession = new GameplaySession5165(target, (packet, _) =>
    {
        targetPackets.Add(packet.ToArray());
        return Task.CompletedTask;
    });
    Assert(await combat.EnterPlayerAsync(new AccountId(targetAccountId), targetSession, default),
        "The authenticated encrypted Ninja PvP target was not registered.");
    await combat.EnterStaminaAsync(new AccountId(targetAccountId), targetSession, default);
    combat.MarkStaminaActivity(targetSession, StaminaActivity.Sitting);
    var world = new GameplayWorld5165(new MapCatalog5165([new TestMapValidation5165(1002)]), new());
    int accountPort = FreePort(), gamePort = FreePort();
    while (gamePort == accountPort) gamePort = FreePort();
    var account = new Genuine5165RuntimeComponent("NinjaAccount", IPAddress.Loopback, accountPort, 4, true,
        "10.0.0.1", gamePort, "127.0.0.1", store, log);
    var game = new Genuine5165RuntimeComponent("NinjaGame", IPAddress.Loopback, gamePort, 4, false,
        "10.0.0.1", gamePort, "127.0.0.1", store, log, world, null, combat);
    await account.StartAsync(default); await game.StartAsync(default);
    try
    {
        ulong token = await ExactAccountLoginAsync(accountPort, username, password);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, gamePort);
        NetworkStream stream = client.GetStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using ExactGameClient5165 exact = await ExactGameClient5165.AcceptAsync(stream, timeout.Token);
        byte[] dh = exact.CreateDhReply(); byte[] connect = ExactGameConnect(exact, token);
        await stream.WriteAsync(dh, timeout.Token); await stream.WriteAsync(connect, timeout.Token);
        CryptographicOperations.ZeroMemory(dh); CryptographicOperations.ZeroMemory(connect);
        await ReadIdAsync(1004); await ReadIdAsync(1006);
        await SendAsync(ExactAction(checked((uint)ninja.LegacyUid), 74));
        await ReadIdAsync(10010); await ReadIdAsync(1110);
        while (true)
        {
            byte[] spawn = await ReadAnyAsync();
            if (BinaryPrimitives.ReadUInt16LittleEndian(spawn.AsSpan(2)) != CombatPackets5165.EntitySpawnPacketId)
                continue;
            MonsterSpawn5165 decoded = CombatPackets5165.ParseMonsterSpawnAsExactClient(spawn);
            if (decoded.Uid == ninjaMonsterUid) break;
        }
        foreach (ushort skill in new ushort[] { 6000, 6001, 6002, 6003, 6004, 6010, 6011 })
        {
            SkillInformation5165 learned = NinjaPackets5165.ParseSkillInformation(
                await ReadIdAsync(NinjaPackets5165.SkillInformationPacketId));
            Assert(learned.SkillId == skill && learned.SkillLevel == 0,
                "The initial encrypted Ninja skill list was not ordered and authoritative.");
        }

        byte[] pvp = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(pvp, 32);
        BinaryPrimitives.WriteUInt16LittleEndian(pvp.AsSpan(2), NinjaPackets5165.InteractionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(pvp.AsSpan(4), 0x5fff0001);
        BinaryPrimitives.WriteUInt32LittleEndian(pvp.AsSpan(8), checked((uint)ninja.LegacyUid));
        BinaryPrimitives.WriteUInt32LittleEndian(pvp.AsSpan(12), checked((uint)target.LegacyUid));
        BinaryPrimitives.WriteUInt16LittleEndian(pvp.AsSpan(16), 454);
        BinaryPrimitives.WriteUInt16LittleEndian(pvp.AsSpan(18), 455);
        BinaryPrimitives.WriteUInt32LittleEndian(pvp.AsSpan(20), NinjaPackets5165.MeleeAction);
        await SendAsync(pvp);
        byte[] sittingInterrupted = await ReadIdAsync(10010);
        Assert(BinaryPrimitives.ReadUInt32LittleEndian(sittingInterrupted.AsSpan(4)) ==
                checked((uint)target.LegacyUid) &&
            BinaryPrimitives.ReadUInt16LittleEndian(sittingInterrupted.AsSpan(16)) == 100,
            "The encrypted physical PvP hit did not broadcast the target's standing transition first.");
        byte[] pvpResponse = await ReadIdAsync(NinjaPackets5165.InteractionPacketId);
        Assert(BinaryPrimitives.ReadUInt32LittleEndian(pvpResponse.AsSpan(12)) == checked((uint)target.LegacyUid) &&
            BinaryPrimitives.ReadUInt32LittleEndian(pvpResponse.AsSpan(24)) > 1,
            "The encrypted normal PvP response targeted the wrong entity.");
        Assert(targetPackets.Any(packet => packet.Length == 32 &&
                BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)) == NinjaPackets5165.InteractionPacketId &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(12)) == checked((uint)target.LegacyUid) &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(24)) > 1),
            "The encrypted normal PvP result was not broadcast to the nearby target session.");
        Assert(targetPackets.Any(packet => packet.Length == 36 &&
                BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)) == ItemEconomyPackets5165.StatusPacketId &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)) == checked((uint)target.LegacyUid) &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(12)) == 8 &&
                BinaryPrimitives.ReadUInt64LittleEndian(packet.AsSpan(16)) == 70),
            "The encrypted physical PvP hit did not synchronize the atomic sitting-Stamina interruption.");

        await UseAppliedSkillAsync(6000, ninjaMonsterUid, 454, 456, 0x60000001);
        byte[] regeneratedStamina = await ReadIdAsync(ItemEconomyPackets5165.StatusPacketId);
        Assert(BinaryPrimitives.ReadUInt32LittleEndian(regeneratedStamina.AsSpan(4)) ==
                checked((uint)ninja.LegacyUid) &&
            BinaryPrimitives.ReadUInt32LittleEndian(regeneratedStamina.AsSpan(12)) == 8 &&
            BinaryPrimitives.ReadUInt64LittleEndian(regeneratedStamina.AsSpan(16)) == 73,
            "The encrypted Host did not resume standing Stamina regeneration after Twofold's quiet period.");
        await ResetStaminaAsync();
        await UseAppliedSkillAsync(6001, ninjaMonsterUid, 454, 456, 0x60000002);
        await ResetStaminaAsync();
        await UseAppliedSkillAsync(6002, checked((uint)target.LegacyUid), 454, 455, 0x60000003);
        await ResetStaminaAsync();
        await UseAppliedSkillAsync(6003, checked((uint)ninja.LegacyUid), 453, 455, 0x60000004);
        await ResetStaminaAsync();
        await UseAppliedSkillAsync(6010, checked((uint)ninja.LegacyUid), 453, 455, 0x60000005);
        await ResetStaminaAsync();
        await UseAppliedSkillAsync(6011, checked((uint)ninja.LegacyUid), 453, 455, 0x60000006);
        await Task.Delay(550, timeout.Token);
        byte[] fatalStrikeHit = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(fatalStrikeHit, 32);
        BinaryPrimitives.WriteUInt16LittleEndian(fatalStrikeHit.AsSpan(2), NinjaPackets5165.InteractionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(fatalStrikeHit.AsSpan(4), 0x6000000b);
        BinaryPrimitives.WriteUInt32LittleEndian(fatalStrikeHit.AsSpan(8), checked((uint)ninja.LegacyUid));
        BinaryPrimitives.WriteUInt32LittleEndian(fatalStrikeHit.AsSpan(12), ninjaMonsterUid);
        BinaryPrimitives.WriteUInt16LittleEndian(fatalStrikeHit.AsSpan(16), 454);
        BinaryPrimitives.WriteUInt16LittleEndian(fatalStrikeHit.AsSpan(18), 456);
        BinaryPrimitives.WriteUInt32LittleEndian(fatalStrikeHit.AsSpan(20), NinjaPackets5165.MeleeAction);
        await SendAsync(fatalStrikeHit);
        await ReadProgressionStatusAsync();
        byte[] proficiency = await ReadIdAsync(CombatPackets5165.ProficiencyPacketId);
        Assert(BinaryPrimitives.ReadUInt32LittleEndian(proficiency.AsSpan(4)) == 601 &&
            BinaryPrimitives.ReadUInt16LittleEndian(proficiency.AsSpan(8)) == 0 &&
            BinaryPrimitives.ReadUInt32LittleEndian(proficiency.AsSpan(12)) == 0,
            "The first authoritative dual-Katana hit did not create the evidenced zero-level proficiency state.");
        byte[] fatalStrikeResult = await ReadIdAsync(NinjaPackets5165.InteractionPacketId);
        Assert(BinaryPrimitives.ReadUInt32LittleEndian(fatalStrikeResult.AsSpan(24)) > 5,
            "Fatal Strike did not modify the next authoritative monster hit.");

        // The client request cannot declare flying state. A validated server-side
        // class transition owns it, and Archer Bane must clear it after acceptance.
        await ResetStaminaAsync();
        Assert(await combat.SetPlayerFlyingStateAsync(new AccountId(targetAccountId), targetSession,
            true, timeout.Token), "The authoritative flying-state fixture was not applied.");
        await UseAppliedSkillAsync(6004, checked((uint)target.LegacyUid), 454, 455, 0x60000007);
        await SendAsync(GamePacketsAfterAuthentication5165.EncodeMovement(6,
            checked((uint)ninja.LegacyUid), 0, 0x60000008));
        await ReadIdAsync(GamePacketsAfterAuthentication5165.MovementPacketId);
        byte[] malformedSkill = ExactClientSkillAttack(checked((uint)ninja.LegacyUid),
            ninjaMonsterUid, 454, 456, 6000, 0, 0x60000009);
        malformedSkill[31] = 1;
        await SendAsync(malformedSkill);
        await SendAsync(GamePacketsAfterAuthentication5165.EncodeMovement(6,
            checked((uint)ninja.LegacyUid), 0, 0x6000000a));
        await ReadIdAsync(GamePacketsAfterAuthentication5165.MovementPacketId);

        async Task UseAppliedSkillAsync(ushort skill, uint targetUid, ushort x, ushort y, uint sequence)
        {
            await SendAsync(ExactClientSkillAttack(checked((uint)ninja.LegacyUid), targetUid, x, y,
                skill, 0, sequence));
            if (targetUid == ninjaMonsterUid && skill is 6000 or 6001)
                await ReadProgressionStatusAsync();
            SkillUse5165 use = NinjaPackets5165.ParseSkillUseAsExactClient(
                await ReadIdAsync(NinjaPackets5165.SkillUsePacketId));
            Assert(use.SkillId == skill && use.ActorUid == checked((uint)ninja.LegacyUid),
                $"The encrypted Ninja skill {skill} response was invalid.");
            if (skill == (ushort)NinjaSkillId.TwofoldBlades)
                Assert(use.Targets.Count == 1 && use.Targets[0].Damage > 1,
                    "Twofold Blades still used the former hard-coded one-damage base.");
            await ReadIdAsync(ItemEconomyPackets5165.StatusPacketId);
            SkillInformation5165 information = NinjaPackets5165.ParseSkillInformation(
                await ReadIdAsync(NinjaPackets5165.SkillInformationPacketId));
            Assert(information.SkillId == skill, "The persisted skill update did not match the request.");
        }
        async Task ResetStaminaAsync()
        {
            await using var reset = new NpgsqlConnection(connectionString);
            await reset.OpenAsync();
            await using var command = new NpgsqlCommand(
                "UPDATE game.character_attributes SET stamina=100 WHERE character_id=$1", reset);
            command.Parameters.AddWithValue(ninja.Id);
            await command.ExecuteNonQueryAsync();
        }
        async Task ReadProgressionStatusAsync()
        {
            byte[] packet = await ReadIdAsync(ItemEconomyPackets5165.StatusPacketId);
            Assert(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)) ==
                    checked((uint)ninja.LegacyUid) &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(12)) == 5,
                "The encrypted monster hit did not deliver authoritative character EXP first.");
        }
        async Task SendAsync(byte[] plain)
        {
            byte[] encrypted = exact.EncryptClientPacket(plain);
            await stream.WriteAsync(encrypted, timeout.Token);
            CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(encrypted);
        }
        async Task<byte[]> ReadAnyAsync() => await exact.ReadServerPacketAsync(stream, timeout.Token);
        async Task<byte[]> ReadIdAsync(ushort expected)
        {
            byte[] packet = await ReadAnyAsync();
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)) == expected,
                $"Expected {expected}; received {BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2))}.");
            return packet;
        }
    }
    finally
    {
        await account.StopAsync(default); await game.StopAsync(default);
        combat.LeaveStamina(targetSession);
        await combat.LeavePlayerAsync(targetSession, default);
    }
    string audit = output.ToString();
    foreach (ushort skill in new ushort[] { 6000, 6001, 6002, 6003, 6004, 6010, 6011 })
        Assert(audit.Contains($"skill={skill}", StringComparison.Ordinal) &&
            audit.Contains("Ninja1022Accepted", StringComparison.Ordinal), $"Skill {skill} was not audited.");
    Assert(!audit.Contains(password, StringComparison.Ordinal), "The encrypted Ninja audit leaked a credential.");
    Console.WriteLine("PASS genuine encrypted 5165 Ninja skills through Host (all seven applied with authoritative flying state)");
}

static byte[] ExactClientSkillAttack(uint sender, uint target, ushort x, ushort y,
    ushort skill, ushort level, uint timestamp)
{
    var packet = new byte[32];
    BinaryPrimitives.WriteUInt16LittleEndian(packet, 32);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), NinjaPackets5165.InteractionPacketId);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), timestamp);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), sender);
    uint targetIntermediate = unchecked((target + 0x746f4ae6u) ^ 0x5f2d2463u ^ sender);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), RotateLeft(targetIntermediate, 13));
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), unchecked((ushort)
        (RotateRight16((ushort)(x + 0x22ee), 1) ^ (ushort)sender ^ 0x2ed6)));
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), unchecked((ushort)
        (RotateRight16((ushort)(y + 0x8922), 5) ^ (ushort)sender ^ 0xb99b)));
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(20), NinjaPackets5165.MagicAction);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(24), unchecked((ushort)
        (RotateRight16((ushort)(skill + 0xeb42), 3) ^ 0x915d ^ (ushort)sender)));
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(26), level);
    return packet;
}

static ushort RotateRight16(ushort value, int count) =>
    unchecked((ushort)((value >> count) | (value << (16 - count))));
static uint RotateLeft(uint value, int count) => (value << count) | (value >> (32 - count));

static async Task GenuineEncryptedItemEconomyAsync(string hostExecutable, string databaseName, string databaseUsername)
{
    Assert(File.Exists(hostExecutable), "The reviewed Host artifact is missing.");
    string runSuffix = Guid.NewGuid().ToString("N")[..8];
    string username = $"econ{runSuffix}";
    const string password = "economypass12";
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = "/var/run/postgresql", Database = databaseName, Username = databaseUsername, Pooling = false,
    };
    var hasher = new AdaptivePasswordHasher();
    Guid accountId = Guid.NewGuid();
    await using (var connection = new NpgsqlConnection(builder.ConnectionString))
    {
        await connection.OpenAsync();
        await using (var accountInsert = new NpgsqlCommand(
            "INSERT INTO identity.game_accounts(account_id,username,normalized_username,password_hash,password_algorithm,password_version) " +
            "VALUES($1,$2,lower($2),$3,'pbkdf2-sha512',1)", connection))
        {
            accountInsert.Parameters.AddWithValue(accountId);
            accountInsert.Parameters.AddWithValue(username);
            accountInsert.Parameters.AddWithValue(hasher.Hash(password));
            await accountInsert.ExecuteNonQueryAsync();
        }
    }

    CharacterState character;
    await using (var setupStore = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName))
    {
        character = await setupStore.TryCreateCharacterAsync(accountId, $"E{runSuffix}", 1003, 100, default)
            ?? throw new InvalidOperationException("The encrypted economy fixture character was not created.");
        character = await setupStore.TransitionMapAsync(character.Id, 1002, 412, 352, 0, character.Version, default)
            ?? throw new InvalidOperationException("The encrypted economy fixture position was not established.");
    }

    Guid releaseId;
    await using (var connection = new NpgsqlConnection(builder.ConnectionString))
    {
        await connection.OpenAsync();
        await using (var release = new NpgsqlCommand(
            "SELECT release_id FROM gameplay_reference.catalog_releases WHERE manifest_sha256=$1", connection))
        {
            release.Parameters.AddWithValue("2cd8ab6c2c80ba1ba8ef3a0d8f825110e29838a60f9e8492ef401da7cb8e64be");
            releaseId = (Guid)(await release.ExecuteScalarAsync() ?? throw new InvalidOperationException("The reviewed catalog release is missing."));
        }
        const string features = "'world.catalog.complete','items.definitions','inventory','currency.ledger','equipment.stats','shops','warehouse','progression.services'";
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync("SET LOCAL ROLE revora_migrator");
        await ExecuteAsync("UPDATE gameplay_reference.catalog_releases SET release_state='retired',activated_at=NULL WHERE release_state='active' AND release_id<>$1", releaseId);
        await ExecuteAsync("UPDATE gameplay_reference.catalog_releases SET release_state='active',activated_at=clock_timestamp() WHERE release_id=$1", releaseId);
        await ExecuteAsync($"UPDATE gameplay.feature_gates SET gate_state='enabled',required_release_id=$1,enabled_version=enabled_version+1,changed_by='encrypted-economy-test' WHERE feature_key IN ({features})", releaseId);
        await ExecuteAsync($"UPDATE gameplay.feature_rollouts SET rollout_scope='canary',changed_at=clock_timestamp(),changed_by='encrypted-economy-test' WHERE feature_key IN ({features})");
        await ExecuteAsync("INSERT INTO game.inventory_profiles(character_id,slot_capacity) VALUES($1,40) ON CONFLICT(character_id) DO NOTHING", character.Id);
        // Deliberately retain the genuine production legacy-character shape: no V016
        // character_attributes row. Equipment stat persistence must still produce the
        // exact client's valid initial-health floor rather than disconnect after commit.
        await ExecuteAsync($"INSERT INTO gameplay.feature_canaries(feature_key,account_id,character_id,legacy_character_uid,enabled_by) " +
            $"SELECT feature_key,$1,$2,$3,'encrypted-economy-test' FROM gameplay.feature_rollouts WHERE feature_key IN ({features}) " +
            $"AND feature_key<>'progression.services' ON CONFLICT DO NOTHING",
            accountId, character.Id, character.LegacyUid);
        // The genuine Ninja canary was authorized while its account still had
        // zero characters. Exercise the immutable account-level rollout path
        // through both session opening and the transactional purchase check.
        await ExecuteAsync("INSERT INTO gameplay.account_feature_canaries(feature_key,account_id,enabled_by) " +
            "VALUES('progression.services',$1,'encrypted-economy-test') ON CONFLICT DO NOTHING", accountId);
        // Exercise RevoraMaster's canonical Ninja stat family through the real
        // encrypted Host path. Character creation itself is covered by the
        // dedicated Ninja campaign; this fixture owns progression/economy.
        await ExecuteAsync("UPDATE game.characters SET profession=50 WHERE character_id=$1", character.Id);
        await ExecuteAsync("INSERT INTO game.gameplay_role_assignments(account_id,character_id,role,granted_by) VALUES($1,$2,'gm','encrypted-economy-test')",
            accountId, character.Id);
        await transaction.CommitAsync();

        async Task ExecuteAsync(string sql, params object[] values)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            foreach (object value in values) command.Parameters.AddWithValue(value);
            await command.ExecuteNonQueryAsync();
        }
    }

    string economyConnection = PostgreSqlEconomyRuntime.BuildLocalPeerConnectionString(
        "/var/run/postgresql", databaseUsername, databaseName);
    await using ItemEconomyRuntime5165 economy = await ItemEconomyRuntime5165.CreateAsync(economyConnection, default);
    await using var gameplayStore = new PostgreSqlGameplayStore(economyConnection);
    var actor = new GameplayActor(new AccountId(accountId), new CharacterUid(checked((uint)character.LegacyUid)));
    var grant = new CurrencyService(gameplayStore);
    EqualResult(GameplayResultCode.Success, (await grant.AdjustAsync(
        new GameplayCommandId(Guid.NewGuid()), actor,
        CurrencyKind.Silver, 10_000, default)).Code);

    await using var store = new PostgreSqlGameDataStore("/var/run/postgresql", databaseUsername, databaseName);
    GameplayReferenceRows rows = await store.LoadPortalNpcReferencesAsync([1002, 1004], default);
    var references = new GameplayReferenceCatalog5165(
        rows.Portals.Select(row => new PortalReference5165(row.LegacyId, row.SourceMap, row.SourceX, row.SourceY,
            row.DestinationMap, row.DestinationX, row.DestinationY)),
        rows.Npcs.Select(row => new NpcReference5165(row.LegacyUid, row.Type, row.Flags, row.Face,
            row.MapId, row.X, row.Y)));
    int visibleNpcCount = references.Npcs(1002).Count(candidate =>
        Math.Max(Math.Abs(candidate.X - character.X), Math.Abs(candidate.Y - character.Y)) <= GameplayWorld5165.ViewRadius);
    var maps = new MapCatalog5165([new TestMapValidation5165(1002), new TestMapValidation5165(1004, 100, 100)],
        new Dictionary<int, int> { [1002] = 1002, [1004] = 1004 });
    var world = new GameplayWorld5165(maps, references);
    int accountPort = FreePort(), gamePort = FreePort();
    while (gamePort == accountPort) gamePort = FreePort();
    using var output = new StringWriter();
    var log = new SafeConsoleLog(output, TextWriter.Null);
    var account = new Genuine5165RuntimeComponent("EconomyAccount", IPAddress.Loopback, accountPort, 4, true,
        "10.0.0.1", gamePort, "127.0.0.1", store, log);
    var game = new Genuine5165RuntimeComponent("EconomyGame", IPAddress.Loopback, gamePort, 4, false,
        "10.0.0.1", gamePort, "127.0.0.1", store, log, world, economy);
    await account.StartAsync(default);
    await game.StartAsync(default);
    try
    {
        uint itemUid = await RunEconomyConnectionAsync(phase: 0);
        GameplayPlayerSnapshot equippedPersisted = await ReadSnapshotAsync();
        Assert(equippedPersisted.Items.Single().LegacyUid.Value == itemUid &&
            equippedPersisted.Items.Single().Location == ItemLocation.Equipped(EquipmentSlot.Ring),
            "The exact Item UID was not persisted in the ring slot across disconnect.");
        await using (var verification = new NpgsqlConnection(builder.ConnectionString))
        {
            await verification.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT (SELECT count(*) FROM game.character_attributes WHERE character_id=$1)," +
                "(SELECT maximum_health FROM game.character_stat_snapshots WHERE character_id=$1)", verification);
            command.Parameters.AddWithValue(character.Id);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
            Assert(await reader.ReadAsync() && reader.GetInt64(0) == 0 && reader.GetInt32(1) == 100,
                "The legacy-character equipment regression did not persist the exact initial-health floor.");
        }
        _ = await RunEconomyConnectionAsync(phase: 1);
        GameplayPlayerSnapshot persisted = await ReadSnapshotAsync();
        Assert(persisted.Items.Single().LegacyUid.Value == itemUid &&
            persisted.Items.Single().Location.Container == ItemContainer.Warehouse,
            "The exact Item UID was not persisted in the warehouse across disconnect.");
        _ = await RunEconomyConnectionAsync(phase: 2);
        GameplayPlayerSnapshot final = await ReadSnapshotAsync();
        Assert(final.Items.Count == 0 && final.Character.Balance(CurrencyKind.Silver) == 9_993,
            "The encrypted buy/deposit/reconnect/withdraw/sell transaction did not persist the authoritative result.");
        CharacterState latest = await store.GetCharacterAsync(accountId, default)
            ?? throw new InvalidOperationException("The encrypted campaign character disappeared.");
        character = await store.TransitionMapAsync(latest.Id, 1002, 349, 359, 0, latest.Version, default)
            ?? throw new InvalidOperationException("The RevoraMaster test position was not established.");
        visibleNpcCount = references.Npcs(1002).Count(candidate =>
            Math.Max(Math.Abs(candidate.X - character.X), Math.Abs(candidate.Y - character.Y)) <= GameplayWorld5165.ViewRadius);
        _ = await RunEconomyConnectionAsync(phase: 3);
        _ = await RunEconomyConnectionAsync(phase: 4);
    }
    catch
    {
        Console.Error.WriteLine(output.ToString());
        throw;
    }
    finally
    {
        await account.StopAsync(default);
        await game.StopAsync(default);
    }
    string safe = output.ToString();
    string[] requiredAuditMarkers =
    [
        "Item1009Handled", "ServerClock1033Sent", "Ping1009Timing",
        "responseTimestamp=exact-echo", "clientResponseReceive=not-transmitted",
        "Warehouse1102Handled",
    ];
    string[] missingAuditMarkers = requiredAuditMarkers
        .Where(marker => !safe.Contains(marker, StringComparison.Ordinal)).ToArray();
    string pingAuditEvidence = string.Join(" | ", safe.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(line => line.Contains("Ping1009Timing", StringComparison.Ordinal)));
    Assert(missingAuditMarkers.Length == 0 && !safe.Contains(password, StringComparison.Ordinal),
        $"The encrypted runtime did not record sanitized campaign completion (missing={string.Join(',', missingAuditMarkers)}; credentialLeak={safe.Contains(password, StringComparison.Ordinal)}; pingEvidence={pingAuditEvidence}).");
    Console.WriteLine("PASS genuine encrypted 5165 inventory, equip, buy/sell, warehouse, RevoraMaster, GM caps and reconnect campaign");

    async Task<GameplayPlayerSnapshot> ReadSnapshotAsync() => await gameplayStore.ExecuteAsync(transaction =>
    {
        CharacterGameplayState state = transaction.FindCharacter(actor.CharacterUid) ??
            throw new InvalidOperationException("The encrypted economy fixture character disappeared.");
        return new GameplayPlayerSnapshot(state, transaction.GetItems(actor.CharacterUid));
    }, default);

    async Task<uint> RunEconomyConnectionAsync(int phase)
    {
        ulong token = await ExactAccountLoginAsync(accountPort, username, password);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, gamePort);
        NetworkStream stream = client.GetStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using ExactGameClient5165 exact = await ExactGameClient5165.AcceptAsync(stream, timeout.Token);
        byte[] dh = exact.CreateDhReply();
        byte[] connect = ExactGameConnect(exact, token);
        await stream.WriteAsync(dh, timeout.Token);
        await stream.WriteAsync(connect, timeout.Token);
        CryptographicOperations.ZeroMemory(dh); CryptographicOperations.ZeroMemory(connect);
        await ReadAndAssertIdAsync(1004); // entrance
        byte[] userInformation = await ReadAndAssertIdAsync(1006); // character
        UserInformation5165 exactUserInformation = GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(userInformation);
        CryptographicOperations.ZeroMemory(userInformation);
        uint persistedItemUid = 0;
        if (phase == 1)
        {
            byte[] initialItem = await ReadAndAssertIdAsync(1008);
            persistedItemUid = BinaryPrimitives.ReadUInt32LittleEndian(initialItem.AsSpan(4));
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(initialItem.AsSpan(18)) == 6,
                "Reconnect did not restore the authoritative ring equipment position.");
            CryptographicOperations.ZeroMemory(initialItem);
            await ReadAndAssertIdAsync(10017);
            await ReadAndAssertIdAsync(10017);
        }
        else if (phase == 4)
        {
            Assert(exactUserInformation.Level == 15 && exactUserInformation.Profession == 21,
                "Reconnect did not restore the reviewed second-rebirth level/class state in 1006.");
            for (int index = 0; index < 8; index++) await ReadAndAssertIdAsync(1008);
            await ReadAndAssertIdAsync(10017);
            await ReadAndAssertIdAsync(10017);
        }
        else
        {
            // The warehoused item is intentionally omitted from initial inventory delivery.
            await ReadAndAssertIdAsync(10017);
            await ReadAndAssertIdAsync(10017);
        }
        byte[] serverClock = await ReadAndAssertIdAsync(ClientTimingPackets5165.ServerClockPacketId);
        ClientClockFields5165 clockFields = ClientTimingPackets5165.ParseAsExactClient(serverClock);
        Assert(clockFields.Year >= 2026 && clockFields.Month is >= 1 and <= 12 &&
            clockFields.Day is >= 1 and <= 31,
            "The encrypted runtime did not deliver a valid Cairo calendar frame.");
        CryptographicOperations.ZeroMemory(serverClock);
        byte[] enter = ExactAction(checked((uint)character.LegacyUid), 74);
        byte[] quest = ExactObservedQuestStatus();
        await SendCoalescedAsync(enter, quest);
        await ReadAndAssertIdAsync(10010);
        await ReadAndAssertIdAsync(1110);
        for (int index = 0; index < visibleNpcCount; index++)
        {
            byte[] npc = await exact.ReadServerPacketAsync(stream, timeout.Token);
            CryptographicOperations.ZeroMemory(npc);
        }

        if (phase == 0)
        {
            await SendAsync(ExactNpcActivation(13671));
            for (int index = 0; index < 4; index++) await ReadAndAssertIdAsync(2032);
            await SendCoalescedAsync(ExactNpcDialogAnswer(byte.MaxValue),
                GamePacketsAfterAuthentication5165.EncodeMovement(6,
                    checked((uint)character.LegacyUid), 0, 0x0ff0));
            await ReadAndAssertIdAsync(GamePacketsAfterAuthentication5165.MovementPacketId);
            await SendAsync(ExactNpcActivation(1));
            byte[] shopOpen = await ReadAndAssertIdAsync(10010);
            GameAction5165 shopAction = GamePacketsAfterAuthentication5165.ParseAction(shopOpen);
            Assert(shopAction.Action == GamePacketsAfterAuthentication5165.OpenShopAction &&
                shopAction.Id == checked((uint)character.LegacyUid) && shopAction.Data == 1,
                "The actual shop NPC activation did not route to action 113.");
            CryptographicOperations.ZeroMemory(shopOpen);
            await SendAsync(ExactItemAction(8, 0, ItemEconomyPackets5165.WarehouseOpen, 0, 0x0ff1));
            await ReadAndAssertIdAsync(1004);
            await SendAsync(ExactNpcActivation(1));
            await ReadAndAssertIdAsync(10010);
            // Genuine patch-5165 action 27 is an item-UI keepalive, not an unsupported action.
            await SendAsync(ExactItemAction(7, 8, ItemEconomyPackets5165.Ping, 0, 0x1000));
            byte[] ping = await ReadAndAssertIdAsync(1009);
            Assert(BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(4)) == 7 &&
                BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(8)) == 8 &&
                BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(12)) == ItemEconomyPackets5165.Ping &&
                BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(16)) == 0x1000,
                "The genuine item action-27 monotonic send tick was not echoed unchanged.");
            CryptographicOperations.ZeroMemory(ping);
            byte[] buy = ExactItemAction(1, 150005, ItemEconomyPackets5165.ShopBuy, 1, 0x1001);
            await SendFragmentedAsync(buy);
            byte[] purchased = await ReadAndAssertIdAsync(1008);
            uint uid = BinaryPrimitives.ReadUInt32LittleEndian(purchased.AsSpan(4));
            CryptographicOperations.ZeroMemory(purchased);
            await ReadAndAssertIdAsync(10017);

            byte[] replay = ExactItemAction(1, 150005, ItemEconomyPackets5165.ShopBuy, 1, 0x1001);
            byte[] walk = GamePacketsAfterAuthentication5165.EncodeMovement(6,
                checked((uint)character.LegacyUid), 0, 0x2001);
            await SendCoalescedAsync(replay, walk);
            await ReadAndAssertIdAsync(1008);
            await ReadAndAssertIdAsync(GamePacketsAfterAuthentication5165.MovementPacketId);

            await SendFragmentedAsync(ExactItemAction(uid, 6, ItemEconomyPackets5165.Equip, 0, 0x1002));
            byte[] inventoryRemoval = await ReadAndAssertIdAsync(1009);
            Assert(BinaryPrimitives.ReadUInt32LittleEndian(inventoryRemoval.AsSpan(12)) ==
                ItemEconomyPackets5165.InventoryRemove,
                "Equip did not remove the item from the inventory UI first.");
            CryptographicOperations.ZeroMemory(inventoryRemoval);
            byte[] equipped = await ReadAndAssertIdAsync(1008);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(equipped.AsSpan(16)) == 1 &&
                BinaryPrimitives.ReadUInt16LittleEndian(equipped.AsSpan(18)) == 6,
                "The equipped slot was not authoritative.");
            CryptographicOperations.ZeroMemory(equipped);
            byte[] equippedUpdate = await ReadAndAssertIdAsync(1008);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(equippedUpdate.AsSpan(16)) == 3 &&
                BinaryPrimitives.ReadUInt16LittleEndian(equippedUpdate.AsSpan(18)) == 6,
                "The genuine equipment update did not use the ring position.");
            CryptographicOperations.ZeroMemory(equippedUpdate);
            await ReadAndAssertIdAsync(10017); // maximum health refresh
            await ReadAndAssertIdAsync(10017); // maximum mana refresh
            await SendAsync(ExactItemAction(uid, 6, ItemEconomyPackets5165.Equip, 0, 0x1002));
            byte[] equipReplay = await ReadAndAssertIdAsync(1008);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(equipReplay.AsSpan(18)) == 6,
                "A replayed Equip did not return the authoritative ring correction.");
            CryptographicOperations.ZeroMemory(equipReplay);
            return uid;
        }
        else if (phase == 1)
        {
            uint uid = persistedItemUid;
            await AssertUnequipAsync(uid, 0x1100);
            await SendAsync(ExactItemAction(uid, 6, ItemEconomyPackets5165.Unequip, 0, 0x1100));
            byte[] unequipReplay = await ReadAndAssertIdAsync(1008);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(unequipReplay.AsSpan(18)) == 0,
                "A replayed Unequip did not return the authoritative inventory correction.");
            CryptographicOperations.ZeroMemory(unequipReplay);
            for (int cycle = 0; cycle < 5; cycle++)
            {
                await AssertEquipAsync(uid, checked((uint)(0x1200 + cycle * 2)), coalescedPing: cycle == 0);
                await AssertUnequipAsync(uid, checked((uint)(0x1201 + cycle * 2)));
            }
            await AssertMovementAsync(0);
            await SendFragmentedAsync(ExactNpcActivation(8));
            byte[] warehousePopup = await ReadAndAssertIdAsync(10010);
            GameAction5165 warehouseAction = GamePacketsAfterAuthentication5165.ParseAction(warehousePopup);
            Assert(warehouseAction.Action == GamePacketsAfterAuthentication5165.OpenWarehouseAction &&
                warehouseAction.Id == checked((uint)character.LegacyUid) && warehouseAction.Data == 4,
                "The actual Warehouseman activation did not route to action 126.");
            CryptographicOperations.ZeroMemory(warehousePopup);
            await SendAsync(ExactItemAction(8, 0, ItemEconomyPackets5165.WarehouseOpen, 0, 0x1004));
            byte[] openWarehouse = await ReadAndAssertIdAsync(1009);
            Assert(BinaryPrimitives.ReadUInt32LittleEndian(openWarehouse.AsSpan(8)) == 0,
                "Warehouse savings incorrectly mirrored carried silver.");
            CryptographicOperations.ZeroMemory(openWarehouse);
            // Production proved that the genuine client requests contents with action 0 and selector
            // 10 after accepting 20/1009. An empty vault has no list response in the reviewed donor.
            await SendCoalescedAsync(ExactWarehouseAction(8, 0, 0),
                GamePacketsAfterAuthentication5165.EncodeMovement(6,
                    checked((uint)character.LegacyUid), 0, 0x1004));
            await ReadAndAssertIdAsync(GamePacketsAfterAuthentication5165.MovementPacketId);
            await SendAsync(ExactItemAction(1, 150005, ItemEconomyPackets5165.ShopBuy, 1, 0x1005));
            await ReadAndAssertIdAsync(1004);
            await SendAsync(ExactWarehouseAction(8, 99, 0));
            await ReadAndAssertIdAsync(1004);
            await SendAsync(ExactWarehouseAction(8, 1, uid));
            await AssertInventoryRemovalAsync(uid);
            await AssertWarehouseLiveAddAsync(uid, 150005);
            await SendAsync(ExactWarehouseAction(8, 1, uid));
            await AssertInventoryRemovalAsync(uid);
            await AssertWarehouseLiveAddAsync(uid, 150005);
            // The genuine owner session exposed a lifetime-global packet-hash collision: after a
            // valid withdrawal, the client emits byte-identical deposit bytes. Prove that this is a
            // new legitimate transition while an immediate duplicate remains correction-only.
            await SendAsync(ExactWarehouseAction(8, 2, uid));
            await AssertWarehouseLiveRemoveAsync(uid); await ReadAndAssertIdAsync(1008);
            await SendAsync(ExactWarehouseAction(8, 1, uid));
            await AssertInventoryRemovalAsync(uid);
            await AssertWarehouseLiveAddAsync(uid, 150005);
            await AssertMovementAsync(1);
            return uid;
        }
        else if (phase == 2)
        {
            await SendCoalescedAsync(ExactNpcActivation(8),
                ExactItemAction(8, 0, ItemEconomyPackets5165.WarehouseOpen, 0, 0x3001));
            byte[] warehousePopup = await ReadAndAssertIdAsync(10010);
            Assert(GamePacketsAfterAuthentication5165.ParseAction(warehousePopup).Action ==
                GamePacketsAfterAuthentication5165.OpenWarehouseAction,
                "Reconnect did not route the actual Warehouseman activation.");
            CryptographicOperations.ZeroMemory(warehousePopup);
            await ReadAndAssertIdAsync(1009);
            await SendAsync(ExactWarehouseAction(8, 0, 0));
            byte[] list = await ReadAndAssertIdAsync(1102);
            Assert(list.Length == 48 && BinaryPrimitives.ReadUInt32LittleEndian(list.AsSpan(12)) == 1,
                "The persisted warehouse list was not returned.");
            uint uid = BinaryPrimitives.ReadUInt32LittleEndian(list.AsSpan(16));
            CryptographicOperations.ZeroMemory(list);
            await SendAsync(ExactWarehouseAction(8, 2, uid));
            await AssertWarehouseLiveRemoveAsync(uid); await ReadAndAssertIdAsync(1008);
            await SendAsync(ExactWarehouseAction(8, 2, uid));
            await AssertWarehouseLiveRemoveAsync(uid); await ReadAndAssertIdAsync(1008);
            await AssertMovementAsync(2);
            await SendAsync(ExactNpcActivation(1));
            byte[] shopOpen = await ReadAndAssertIdAsync(10010);
            Assert(GamePacketsAfterAuthentication5165.ParseAction(shopOpen).Action ==
                GamePacketsAfterAuthentication5165.OpenShopAction,
                "Reconnect did not route the actual shop NPC activation.");
            CryptographicOperations.ZeroMemory(shopOpen);
            await SendAsync(ExactItemAction(1, uid, ItemEconomyPackets5165.ShopSell, 0, 0x3002));
            await ReadAndAssertIdAsync(1009); await ReadAndAssertIdAsync(10017);
            await SendAsync(GamePacketsAfterAuthentication5165.EncodeTalk(2000, "@logout"));
            byte[] logoutAcknowledgement = await ReadAndAssertIdAsync(1004);
            Assert(GamePacketsAfterAuthentication5165.ParseClientTalkText(logoutAcknowledgement) == "Logging out safely.",
                "The reviewed GM self-logout acknowledgement was not returned.");
            CryptographicOperations.ZeroMemory(logoutAcknowledgement);
            byte[] end = new byte[1];
            Assert(await stream.ReadAsync(end, timeout.Token) == 0,
                "The reviewed self-logout did not close only the authenticated Game connection.");
            return uid;
        }
        else if (phase == 3)
        {
            byte[] rejectedTalk = ExactClientTalk(2000, "@givemeall");
            rejectedTalk[^1] = 1;
            await SendAsync(rejectedTalk);
            byte[] rejectedTalkResponse = await ReadAndAssertIdAsync(1004);
            Assert(GamePacketsAfterAuthentication5165.ParseClientTalkText(rejectedTalkResponse) ==
                "That command is not available.",
                "A semantically invalid Talk frame did not receive the bounded non-disconnecting response.");
            CryptographicOperations.ZeroMemory(rejectedTalkResponse);
            await AssertMovementAsync(0);

            await SendAsync(ExactClientTalk(2001, "@givemeall"));
            byte[] wrongChannelResponse = await ReadAndAssertIdAsync(1004);
            Assert(GamePacketsAfterAuthentication5165.ParseClientTalkText(wrongChannelResponse) ==
                "That command is not available.",
                "A reviewed GM command was not rejected on the Whisper channel.");
            CryptographicOperations.ZeroMemory(wrongChannelResponse);

            await SendAsync(ExactClientTalk(2000, "@givemeall"));
            byte[] silver = await ReadAndAssertIdAsync(10017);
            byte[] cps = await ReadAndAssertIdAsync(10017);
            Assert(BinaryPrimitives.ReadUInt64LittleEndian(silver.AsSpan(16)) ==
                (ulong)PostgreSqlProgressionRuntime.MaximumSafeSilver &&
                BinaryPrimitives.ReadUInt64LittleEndian(cps.AsSpan(16)) ==
                (ulong)PostgreSqlProgressionRuntime.MaximumSafeConquerPoints,
                "The encrypted GM command did not return the reviewed balance caps.");
            CryptographicOperations.ZeroMemory(silver); CryptographicOperations.ZeroMemory(cps);
            await ReadAndAssertIdAsync(1004);

            ulong afterMaximum = await PurchaseProgressionAsync(1, null, expectedItems: 0,
                expectedProfession: 55); // Ninja Master
            // Reproduce the genuine pre-fix state: the paid level jump was
            // committed, but the character remained InternNinja. The second
            // selection must repair only profession lineage at zero cost.
            await using (var reconciliation = new NpgsqlConnection(builder.ConnectionString))
            {
                await reconciliation.OpenAsync();
                await using (var clearReceipts = new NpgsqlCommand(
                    "DELETE FROM game.ninja_promotion_receipts WHERE character_id=$1", reconciliation))
                {
                    clearReceipts.Parameters.AddWithValue(character.Id);
                    await clearReceipts.ExecuteNonQueryAsync();
                }
                await using var resetProfession = new NpgsqlCommand(
                    "UPDATE game.characters SET profession=50 WHERE character_id=$1", reconciliation);
                resetProfession.Parameters.AddWithValue(character.Id);
                Assert(await resetProfession.ExecuteNonQueryAsync() == 1,
                    "The isolated Ninja reconciliation fixture was not established.");
            }
            ulong afterReconciliation = await PurchaseProgressionAsync(1, null, expectedItems: 0,
                expectedProfession: 55);
            Assert(afterReconciliation == afterMaximum,
                "The already-paid maximum-level Ninja reconciliation deducted CPs twice.");
            await PurchaseProgressionAsync(2, 11, expectedItems: 0);    // first rebirth: Trojan
            await PurchaseProgressionAsync(1, null, expectedItems: 0);  // restore eligibility
            await PurchaseProgressionAsync(3, 12, expectedItems: 0);    // second rebirth: Warrior
            await PurchaseProgressionAsync(4, 16, expectedItems: 8);    // full native Ninja loadout
            await AssertMovementAsync(0);
            return 0;
        }
        else
        {
            Assert(exactUserInformation.Level == 15 && exactUserInformation.Profession == 21,
                "The encrypted reconnect did not preserve the progression state.");
            await AssertMovementAsync(0);
            return 0;
        }

        async Task<ulong> PurchaseProgressionAsync(byte mainOption, byte? classOption, int expectedItems,
            ushort? expectedProfession = null)
        {
            await SendAsync(ExactNpcActivation(ProgressionServiceSession5165.NpcUid));
            for (int index = 0; index < 8; index++) await ReadAndAssertIdAsync(2032);
            await SendAsync(ExactNpcDialogAnswer(mainOption));
            // Genuine production traffic closes the replaced page after the
            // selection. The progression route must survive this page-close
            // acknowledgement so the following page can accept its option.
            await SendAsync(ExactNpcDialogAnswer(byte.MaxValue));
            if (classOption is not null)
            {
                int classPagePackets = mainOption == 4 ? 10 : 9;
                for (int index = 0; index < classPagePackets; index++) await ReadAndAssertIdAsync(2032);
                await SendAsync(ExactNpcDialogAnswer(classOption.Value));
                await SendAsync(ExactNpcDialogAnswer(byte.MaxValue));
            }
            for (int index = 0; index < 5; index++) await ReadAndAssertIdAsync(2032);
            await SendFragmentedAsync(ExactNpcDialogAnswer(100));
            byte[] cpUpdate = await ReadAndAssertIdAsync(10017);
            ulong resultingConquerPoints = BinaryPrimitives.ReadUInt64LittleEndian(cpUpdate.AsSpan(16));
            CryptographicOperations.ZeroMemory(cpUpdate);
            if (expectedItems == 0)
            {
                bool levelObserved = false;
                bool professionObserved = expectedProfession is null;
                for (int index = 0; index < 11; index++)
                {
                    byte[] status = await ReadAndAssertIdAsync(10017);
                    uint statusType = BinaryPrimitives.ReadUInt32LittleEndian(status.AsSpan(12));
                    ulong value = BinaryPrimitives.ReadUInt64LittleEndian(status.AsSpan(16));
                    levelObserved |= statusType == 12;
                    professionObserved |= statusType == 7 && value == expectedProfession;
                    CryptographicOperations.ZeroMemory(status);
                }
                Assert(levelObserved, "The encrypted progression response omitted the level status.");
                Assert(professionObserved,
                    "The encrypted maximum-level Ninja response omitted the Ninja Master profession status.");
            }
            else
            {
                for (int index = 0; index < expectedItems; index++) await ReadAndAssertIdAsync(1008);
            }
            for (int index = 0; index < 4; index++) await ReadAndAssertIdAsync(2032);
            return resultingConquerPoints;
        }

        async Task AssertEquipAsync(uint uid, uint timestamp, bool coalescedPing = false)
        {
            if (coalescedPing)
                await SendCoalescedAsync(
                    ExactItemAction(uid, 6, ItemEconomyPackets5165.Equip, 0, timestamp),
                    ExactItemAction(7, 8, ItemEconomyPackets5165.Ping, 0, timestamp + 1));
            else
                await SendAsync(ExactItemAction(uid, 6, ItemEconomyPackets5165.Equip, 0, timestamp));
            byte[] remove = await ReadAndAssertIdAsync(1009);
            Assert(BinaryPrimitives.ReadUInt32LittleEndian(remove.AsSpan(12)) == 3,
                "Equip did not send the exact inventory-removal action.");
            CryptographicOperations.ZeroMemory(remove);
            byte[] add = await ReadAndAssertIdAsync(1008);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(add.AsSpan(16)) == 1 &&
                BinaryPrimitives.ReadUInt16LittleEndian(add.AsSpan(18)) == 6,
                "Equip did not add the item at the exact ring position.");
            CryptographicOperations.ZeroMemory(add);
            byte[] update = await ReadAndAssertIdAsync(1008);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(update.AsSpan(16)) == 3 &&
                BinaryPrimitives.ReadUInt16LittleEndian(update.AsSpan(18)) == 6,
                "Equip did not update the exact ring position.");
            CryptographicOperations.ZeroMemory(update);
            await ReadAndAssertIdAsync(10017);
            await ReadAndAssertIdAsync(10017);
            if (coalescedPing)
            {
                byte[] ping = await ReadAndAssertIdAsync(1009);
                Assert(BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(12)) == ItemEconomyPackets5165.Ping,
                    "A coalesced genuine ping was not handled after Equip.");
                CryptographicOperations.ZeroMemory(ping);
            }
        }

        async Task AssertUnequipAsync(uint uid, uint timestamp)
        {
            await SendAsync(ExactItemAction(uid, 6, ItemEconomyPackets5165.Unequip, 0, timestamp));
            byte[] inventory = await ReadAndAssertIdAsync(1008);
            Assert(BinaryPrimitives.ReadUInt16LittleEndian(inventory.AsSpan(16)) == 1 &&
                BinaryPrimitives.ReadUInt16LittleEndian(inventory.AsSpan(18)) == 0,
                "Unequip did not return the item to inventory Position zero.");
            CryptographicOperations.ZeroMemory(inventory);
            byte[] clear = await ReadAndAssertIdAsync(1009);
            Assert(BinaryPrimitives.ReadUInt32LittleEndian(clear.AsSpan(8)) == 6 &&
                BinaryPrimitives.ReadUInt32LittleEndian(clear.AsSpan(12)) == ItemEconomyPackets5165.Unequip,
                "Unequip did not clear the exact ring position.");
            CryptographicOperations.ZeroMemory(clear);
            await ReadAndAssertIdAsync(10017);
            await ReadAndAssertIdAsync(10017);
        }

        async Task AssertMovementAsync(int index)
        {
            byte[] walk = GamePacketsAfterAuthentication5165.EncodeMovement((byte)(index % 8),
                checked((uint)character.LegacyUid), 0, checked((uint)(0x5000 + index)));
            await SendAsync(walk);
            await ReadAndAssertIdAsync(GamePacketsAfterAuthentication5165.MovementPacketId);
        }

        async Task AssertInventoryRemovalAsync(uint uid)
        {
            byte[] packet = await ReadAndAssertIdAsync(1009);
            Assert(packet.Length == 28 &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)) == uid &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8)) == 0 &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(12)) ==
                    ItemEconomyPackets5165.InventoryRemove,
                "A warehouse deposit did not immediately remove the exact item from Inventory.");
            CryptographicOperations.ZeroMemory(packet);
        }

        async Task AssertWarehouseLiveAddAsync(uint uid, uint itemType)
        {
            byte[] packet = await ReadAndAssertIdAsync(1102);
            // Exact client receive handler 0x587cfd selects action 0, reads the item count at
            // offset 12, then consumes a 32-byte row beginning at offset 16. The owner donor's
            // AddWHItem emits this same one-row representation. Byte 9 is not consulted.
            Assert(packet.Length == 48 && packet[8] == 0 && packet[9] == 0 &&
                BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(10)) == 0 &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(12)) == 1 &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(16)) == uid &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(20)) == itemType,
                "The live warehouse add did not satisfy the exact client's one-row UI update layout.");
            CryptographicOperations.ZeroMemory(packet);
        }

        async Task AssertWarehouseLiveRemoveAsync(uint uid)
        {
            byte[] packet = await ReadAndAssertIdAsync(1102);
            Assert(packet.Length == 48 && packet[8] == 2 && packet[9] == 0 &&
                BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(10)) == 0 &&
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(12)) == uid &&
                packet.AsSpan(16).IndexOfAnyExcept((byte)0) < 0,
                "The live warehouse removal did not satisfy the exact client's action-2 layout.");
            CryptographicOperations.ZeroMemory(packet);
        }

        async Task SendAsync(byte[] plain)
        {
            byte[] encrypted = exact.EncryptClientPacket(plain);
            try { await stream.WriteAsync(encrypted, timeout.Token); }
            finally { CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(encrypted); }
        }

        async Task SendFragmentedAsync(byte[] plain)
        {
            byte[] encrypted = exact.EncryptClientPacket(plain);
            try
            {
                await stream.WriteAsync(encrypted.AsMemory(0, 3), timeout.Token);
                await stream.WriteAsync(encrypted.AsMemory(3, 5), timeout.Token);
                await stream.WriteAsync(encrypted.AsMemory(8), timeout.Token);
            }
            finally { CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(encrypted); }
        }

        async Task SendCoalescedAsync(byte[] firstPlain, byte[] secondPlain)
        {
            byte[] first = exact.EncryptClientPacket(firstPlain);
            byte[] second = exact.EncryptClientPacket(secondPlain);
            byte[] both = [.. first, .. second];
            try { await stream.WriteAsync(both, timeout.Token); }
            finally
            {
                CryptographicOperations.ZeroMemory(firstPlain); CryptographicOperations.ZeroMemory(secondPlain);
                CryptographicOperations.ZeroMemory(first); CryptographicOperations.ZeroMemory(second);
                CryptographicOperations.ZeroMemory(both);
            }
        }

        async Task<byte[]> ReadAndAssertIdAsync(ushort expected)
        {
            byte[] packet = await exact.ReadServerPacketAsync(stream, timeout.Token);
            ushort actual = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2));
            if (actual != expected)
            {
                CryptographicOperations.ZeroMemory(packet);
                throw new InvalidOperationException($"Expected encrypted server packet {expected}; received {actual}.");
            }
            return packet;
        }
    }

    static byte[] ExactClientTalk(ushort channel, string words)
    {
        // Production-observed approved client: field lengths 9,4,0,10 followed by three zero reserved bytes.
        byte[] canonical = GamePacketsAfterAuthentication5165.EncodeTalk(channel, words);
        byte[] packet = new byte[canonical.Length + 3];
        canonical.CopyTo(packet, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        CryptographicOperations.ZeroMemory(canonical);
        return packet;
    }

    static byte[] ExactItemAction(uint subject, uint command, uint action, uint argument, uint timestamp)
    {
        byte[] packet = new byte[28];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 28);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1009);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), subject);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), command);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), action);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), timestamp);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(20), argument);
        return packet;
    }

    static byte[] ExactWarehouseAction(uint npc, byte action, uint itemUid)
    {
        // Exact approved-client constructor at 0x587c89 emits a fixed 48-byte 1102 mutation.
        byte[] packet = new byte[48];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 48);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1102);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), npc);
        packet[8] = action;
        packet[9] = ItemEconomyPackets5165.TwinCityWarehouseSelector;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), itemUid);
        return packet;
    }

    static byte[] ExactNpcActivation(uint npcUid)
    {
        byte[] packet = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 16);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),
            GamePacketsAfterAuthentication5165.NpcInteractionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), npcUid);
        return packet;
    }

    static void EqualResult(GameplayResultCode expected, GameplayResultCode actual)
    {
        if (expected != actual) throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
    }

}

static byte[] ExactGameConnect(ExactGameClient5165 exact, ulong token)
{
    byte[] packet = new byte[28];
    BinaryPrimitives.WriteUInt16LittleEndian(packet, 28);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1052);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), (uint)token);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), (uint)(token >> 32));
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), 124);
    "English"u8.CopyTo(packet.AsSpan(14));
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(24), 5165);
    byte[] encrypted = exact.EncryptClientPacket(packet);
    CryptographicOperations.ZeroMemory(packet);
    return encrypted;
}

static byte[] ExactAction(uint characterUid, ushort action)
{
    byte[] packet = new byte[32];
    BinaryPrimitives.WriteUInt16LittleEndian(packet, 32);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 10010);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), characterUid);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), 0x10203040);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), action);
    return packet;
}

static byte[] ExactObservedQuestStatus()
{
    byte[] packet = new byte[304];
    BinaryPrimitives.WriteUInt16LittleEndian(packet, 304);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1134);
    return packet;
}

static async Task<string> LoginAsync(int port, string username, string password, bool fragmented)
{
    await using TestConnection connection = await TestConnection.ConnectAsync(port);
    if (fragmented) await connection.SendFragmentedAsync(1, new { Username = username, Password = password, Realm = "Revora Development" });
    else await connection.SendAsync(1, new { Username = username, Password = password, Realm = "Revora Development" });
    Frame response = await connection.ReadAsync(); JsonElement json = Json(response);
    Assert(response.Type == 2 && json.GetProperty("Success").GetBoolean(), "Synthetic login failed.");
    return json.GetProperty("Token").GetString() ?? throw new InvalidOperationException("Handoff token missing.");
}

static async Task AssertWrongRealmAsync(int port, string token)
{
    await using TestConnection connection = await TestConnection.ConnectAsync(port);
    await connection.SendAsync(10, new { Token = token, Realm = "Wrong Realm" });
    Assert(!Json(await connection.ReadAsync()).GetProperty("Success").GetBoolean(), "Wrong realm was accepted.");
}

static async Task AssertReplayAsync(int port, string token)
{
    await using TestConnection connection = await TestConnection.ConnectAsync(port);
    await connection.SendAsync(10, new { Token = token, Realm = "Revora Development" });
    Assert(!Json(await connection.ReadAsync()).GetProperty("Success").GetBoolean(), "Consumed handoff token replay succeeded.");
}

static async Task AssertMalformedAsync(int port)
{
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port);
    await client.GetStream().WriteAsync(new byte[] { 3, 0, 1, 0 });
    byte[] one = new byte[1];
    int read = await client.GetStream().ReadAsync(one).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
    Assert(read == 0, "Malformed frame did not close the connection.");
}

static JsonElement Json(Frame frame) => JsonDocument.Parse(frame.Body).RootElement.Clone();
static int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

readonly record struct Frame(ushort Type, byte[] Body);

sealed class TestConnection : IAsyncDisposable
{
    private readonly TcpClient client = new();
    private NetworkStream Stream => client.GetStream();
    public static async Task<TestConnection> ConnectAsync(int port) { var connection = new TestConnection(); await connection.client.ConnectAsync(IPAddress.Loopback, port); return connection; }
    public Task SendAsync<T>(ushort type, T payload) => Stream.WriteAsync(Encode(type, payload)).AsTask();
    public async Task SendFragmentedAsync<T>(ushort type, T payload) { byte[] packet = Encode(type, payload); foreach (byte value in packet) await Stream.WriteAsync(new[] { value }); }
    public async Task SendCoalescedAsync<T1,T2>((ushort Type,T1 Payload) first,(ushort Type,T2 Payload) second) { byte[] a=Encode(first.Type,first.Payload), b=Encode(second.Type,second.Payload), all=new byte[a.Length+b.Length]; a.CopyTo(all,0); b.CopyTo(all,a.Length); await Stream.WriteAsync(all); }
    public async Task<Frame> ReadAsync() { byte[] header=new byte[4]; await Stream.ReadExactlyAsync(header).AsTask().WaitAsync(TimeSpan.FromSeconds(5)); int length=BinaryPrimitives.ReadUInt16LittleEndian(header); TestAssert.That(length is >=4 and <=4096,"Invalid response length."); byte[] body=new byte[length-4]; await Stream.ReadExactlyAsync(body).AsTask().WaitAsync(TimeSpan.FromSeconds(5)); return new Frame(BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2)),body); }
    public void Abort() => client.Dispose();
    public ValueTask DisposeAsync() { client.Dispose(); return ValueTask.CompletedTask; }
    private static byte[] Encode<T>(ushort type,T payload) { byte[] body=JsonSerializer.SerializeToUtf8Bytes(payload), packet=new byte[body.Length+4]; BinaryPrimitives.WriteUInt16LittleEndian(packet,checked((ushort)packet.Length)); BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),type); body.CopyTo(packet,4); return packet; }
}

sealed class HostProcess : IAsyncDisposable
{
    private readonly Process process;
    private Task<string>? stdout;
    private readonly Task<string> stderr;
    private HostProcess(Process process) { this.process=process; stderr=process.StandardError.ReadToEndAsync(); }
    public string Output { get; private set; } = string.Empty;
    public static async Task<HostProcess> StartAsync(string executable,string work)
    {
        var start=new ProcessStartInfo(executable,"--config config/revora.json") { WorkingDirectory=work,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false };
        var host=new HostProcess(Process.Start(start) ?? throw new InvalidOperationException("Host process failed to start."));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!timeout.IsCancellationRequested)
        {
            string? line=await host.process.StandardOutput.ReadLineAsync(timeout.Token);
            if (line=="[Revora] READY") { host.Output="[Revora] READY\n"; host.stdout=host.process.StandardOutput.ReadToEndAsync(); return host; }
            if (line is null) throw new InvalidOperationException($"Host exited before readiness. Safe stdout: {host.Output} Safe stderr: {await host.stderr}");
            host.Output+=line+'\n';
        }
        throw new TimeoutException("Host readiness timed out.");
    }
    public async Task StopAsync()
    {
        if (process.HasExited) return;
        await process.StandardInput.WriteLineAsync("stop"); await process.StandardInput.FlushAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Output += await (stdout ?? Task.FromResult(string.Empty)) + await stderr;
            throw new TimeoutException($"Host did not stop within 15 seconds. Safe output: {Output}");
        }
        Output += await (stdout ?? Task.FromResult(string.Empty)) + await stderr;
        TestAssert.That(process.ExitCode==0,"Host did not exit cleanly.");
    }
    public async ValueTask DisposeAsync() { if (!process.HasExited) { process.Kill(entireProcessTree:true); await process.WaitForExitAsync(); } process.Dispose(); }
}

static class TestAssert
{
    public static void That(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
