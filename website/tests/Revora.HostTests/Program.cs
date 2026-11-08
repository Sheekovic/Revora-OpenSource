using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Npgsql;
using Revora.Host.Console;
using Revora.Host.Configuration;
using Revora.Host.Gameplay;
using Revora.Host.Hosting;
using Revora.Host.Logging;
using Revora.Domain.Accounts;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;
using Revora.Persistence.PostgreSql;

namespace Revora.HostTests;

internal static class Program
{
    private static int _passed;

    private static async Task Main()
    {
        await Run("successful READY then clean STOPPED", SuccessfulLifecycle);
        await Run("occupied Account port", OccupiedAccountPort);
        await Run("occupied Game port and no partial READY", OccupiedGamePort);
        await Run("unexpected Account failure propagation", () => UnexpectedFailure("AccountServer"));
        await Run("unexpected Game failure propagation", () => UnexpectedFailure("GameServer"));
        await Run("two isolated loopback instances", TwoIsolatedInstances);
        await Run("bounded shutdown with connected client", BoundedShutdownWithClient);
        await Run("save flush during clean shutdown", SaveDuringShutdown);
        await Run("stdin stop requests graceful shutdown", StdinStop);
        await Run("malformed console input rejected", MalformedConsoleInput);
        await Run("evidence-supported NEW_ROLE packet layout", NewRoleLayout);
        await Run("exact 5165 item/economy packet layouts", ItemEconomyPacketLayouts);
        await Run("exact 5165 clock, ping and FPS timing semantics", ClientTimingSemantics);
        await Run("exact 5165 progression status and user-information layouts", ProgressionPacketLayouts);
        await Run("external service data paths are reviewed", ReviewedServicePaths);

        string? approvedMap = Environment.GetEnvironmentVariable("REVORA_APPROVED_MAP1002");
        if (!string.IsNullOrWhiteSpace(approvedMap))
        {
            await Run("approved private map 1002 parses and contains starter position", () =>
            {
                DMapValidation5165 map = DMapValidation5165.LoadApprovedMap1002(approvedMap);
                Equal(1002, map.MapId);
                if (!map.CanStand(430, 378))
                    throw new InvalidOperationException("The approved starter position is not accessible.");
                if (!map.CanStand(349, 359))
                    throw new InvalidOperationException("The reviewed RevoraMaster position is not accessible.");
                return Task.CompletedTask;
            });
        }

        string? approvedForum = Environment.GetEnvironmentVariable("REVORA_APPROVED_MAP1004");
        if (!string.IsNullOrWhiteSpace(approvedForum))
        {
            await Run("approved private map 1004 parses and contains portal destination", () =>
            {
                DMapValidation5165 map = DMapValidation5165.Load(approvedForum, 1004,
                    DMapValidation5165.ApprovedMap1004Sha256, DMapValidation5165.ApprovedMap1004Length);
                Equal(1004, map.MapId);
                if (!map.CanStand(51, 70))
                    throw new InvalidOperationException("The reviewed portal destination is not accessible.");
                return Task.CompletedTask;
            });
        }

        string? ownerMapRoot = Environment.GetEnvironmentVariable("REVORA_OWNER_MAP_ROOT");
        string? ownerMapManifest = Environment.GetEnvironmentVariable("REVORA_OWNER_MAP_MANIFEST");
        if (!string.IsNullOrWhiteSpace(ownerMapRoot) && !string.IsNullOrWhiteSpace(ownerMapManifest))
        {
            await Run("complete hash-pinned owner map catalog loads without executing donor code", () =>
            {
                MapCatalog5165 catalog = MapCatalog5165.LoadReviewedOwnerCatalog(ownerMapRoot,
                    ownerMapManifest);
                foreach (int mapId in new[] { 1000, 1001, 1002, 1004, 1011, 1020, 1038, 1210, 2024, 3030 })
                    if (!catalog.TryGet(mapId, out _))
                        throw new InvalidOperationException($"Reviewed owner map {mapId} did not load.");
                return Task.CompletedTask;
            });
            string? reviewedSpawns = Environment.GetEnvironmentVariable("REVORA_OWNER_SPAWNS");
            if (!string.IsNullOrWhiteSpace(reviewedSpawns))
                await Run("complete normal-world monster population resolves to DMap-valid cells", () =>
                    ResolveOwnerMonsterPopulation(ownerMapRoot, ownerMapManifest, reviewedSpawns));
        }

        string? monsterDatabase = Environment.GetEnvironmentVariable("REVORA_MONSTER_TEST_DATABASE");
        if (!string.IsNullOrWhiteSpace(monsterDatabase) && !string.IsNullOrWhiteSpace(ownerMapRoot) &&
            !string.IsNullOrWhiteSpace(ownerMapManifest))
            await Run("complete PostgreSQL owner monster runtime starts and stops under bounded load", async () =>
            {
                string socket = Environment.GetEnvironmentVariable("REVORA_MONSTER_TEST_SOCKET") ??
                    "/var/run/postgresql";
                string user = Environment.GetEnvironmentVariable("REVORA_MONSTER_TEST_USER") ?? "revora-game";
                string connection = Revora.Persistence.PostgreSql.PostgreSqlEconomyRuntime
                    .BuildLocalPeerConnectionString(socket, user, monsterDatabase);
                MapCatalog5165 catalog = MapCatalog5165.LoadReviewedOwnerCatalog(ownerMapRoot,
                    ownerMapManifest);
                long before = GC.GetTotalMemory(true);
                var output = new StringWriter();
                await using var runtime = new LiveCombatRuntime5165(connection,
                    new SafeConsoleLog(output, output), catalog);
                Stopwatch elapsed = Stopwatch.StartNew();
                await runtime.InitializeAsync(CancellationToken.None);
                elapsed.Stop();
                Equal(49_334, runtime.EntityCount);
                ActiveRegionEvidence active = await ExerciseActiveRegionAsync(runtime, connection,
                    socket, user, monsterDatabase);
                MonsterRuntimeMetrics metrics = active.Metrics;
                // The active-region snapshot includes the authenticated player in addition to the
                // complete normal-world monster population.
                Equal(49_335, metrics.ActiveEntities);
                if (metrics.ActiveGroundItems > 10_000)
                    throw new InvalidOperationException("Recovered ground-item state exceeded its bounded population.");
                long retained = GC.GetTotalMemory(true) - before;
                if (elapsed.Elapsed > TimeSpan.FromSeconds(30) || retained > 512L * 1024 * 1024)
                    throw new InvalidOperationException(
                        $"The bounded runtime exceeded its startup budget (elapsed={elapsed.Elapsed}; retained={retained}).");
                Console.WriteLine($"EVIDENCE: owner monster runtime population={runtime.EntityCount}; recoveredGround={metrics.ActiveGroundItems}; startupMs={elapsed.ElapsedMilliseconds}; retainedBytes={retained}; activeWindowMs={active.WallMilliseconds}; cpuMs={active.CpuMilliseconds}; aiTicks={metrics.AiTicks}; attacks={metrics.Attacks}; outboundPackets={active.OutboundPackets}; databaseTransactions={active.DatabaseTransactions}; schedulerDelayMs={metrics.MaximumSchedulerDelayMilliseconds}");
                if (metrics.AiTicks == 0 || active.CpuMilliseconds > 5_000 ||
                    metrics.MaximumSchedulerDelayMilliseconds > 1_000 || active.DatabaseTransactions > 10_000)
                    throw new InvalidOperationException(
                        $"The active-region runtime exceeded its bounded production budget (cpuMs={active.CpuMilliseconds}; aiTicks={metrics.AiTicks}; databaseTransactions={active.DatabaseTransactions}; schedulerDelayMs={metrics.MaximumSchedulerDelayMilliseconds}).");
            });

        var artifact = Environment.GetEnvironmentVariable("REVORA_PUBLISHED_HOST");
        if (!string.IsNullOrWhiteSpace(artifact))
        {
            await Run("published executable actual SIGTERM and secret redaction", () => PublishedSigterm(artifact));
        }

        Console.WriteLine($"Revora.HostTests: {_passed} passed");
    }

    private static async Task ResolveOwnerMonsterPopulation(string mapRoot, string manifest,
        string reviewedSpawns)
    {
        MapCatalog5165 catalog = MapCatalog5165.LoadReviewedOwnerCatalog(mapRoot, manifest);
        byte[] bytes = await File.ReadAllBytesAsync(reviewedSpawns);
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes,
                new JsonDocumentOptions { MaxDepth = 8 });
            var eventTypes = new HashSet<int>
                { 150,244,247,300,409,500,501,700,701,702,703,704,3821,3822,3823,4152,4170,4171,4172,8423,8424 };
            var eventMaps = new HashSet<int> { 2020,8000,8001,8002,8003,8004,8005,8006 };
            var occupied = new HashSet<(int MapId, ushort X, ushort Y)>();
            int generatorId = 0, generators = 0, population = 0;
            foreach (JsonElement row in document.RootElement.EnumerateArray())
            {
                generatorId++;
                int monster = row.GetProperty("MonsterId").GetInt32();
                int mapId = row.GetProperty("MapId").GetInt32();
                if (eventTypes.Contains(monster) || eventMaps.Contains(mapId) ||
                    !catalog.TryGet(mapId, out IMapValidation5165? map)) continue;
                var generator = new OwnerMonsterGenerator(generatorId, monster, mapId,
                    row.GetProperty("MinimumX").GetInt32(), row.GetProperty("MinimumY").GetInt32(),
                    row.GetProperty("MaximumX").GetInt32(), row.GetProperty("MaximumY").GetInt32(),
                    row.GetProperty("Count").GetInt32(), true, "normal").Validate();
                for (int ordinal = 0; ordinal < generator.Population; ordinal++)
                {
                    (ushort x, ushort y) = LiveCombatRuntime5165.ResolveSpawnCell(map!, generator,
                        ordinal, occupied);
                    occupied.Add((mapId, x, y));
                    population++;
                }
                generators++;
            }
            Equal(968, generators);
            Equal(49_334, population);
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }

    private sealed record ActiveRegionEvidence(MonsterRuntimeMetrics Metrics, long WallMilliseconds,
        long CpuMilliseconds, long DatabaseTransactions, long OutboundPackets);

    private static async Task<ActiveRegionEvidence> ExerciseActiveRegionAsync(
        LiveCombatRuntime5165 runtime, string connectionString, string socket, string user,
        string database)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var account = new NpgsqlCommand(
            "SELECT account_id FROM identity.game_accounts WHERE normalized_username='pgplayone'", connection);
        if (await account.ExecuteScalarAsync() is not Guid accountId)
            return new(runtime.GetMetrics(), 0, 0, 0, 0);
        long beforeTransactions = await DatabaseTransactionsAsync(connection);
        await using var store = new PostgreSqlGameDataStore(socket, user, database);
        CharacterState character = await store.GetCharacterAsync(accountId, default)
            ?? throw new InvalidOperationException("The isolated active-region identity disappeared.");
        await ExecuteFixtureAsync(
            "INSERT INTO game.character_attributes(character_id,stamina) VALUES($1,100) ON CONFLICT DO NOTHING",
            character.Id);
        await ExecuteFixtureAsync(
            "INSERT INTO game.character_vitals(character_id,current_health,current_mana) VALUES($1,100,0) ON CONFLICT DO NOTHING",
            character.Id);
        await ExecuteFixtureAsync(
            "INSERT INTO game.character_stat_snapshots(character_id,character_row_version,inventory_revision," +
            "maximum_health,maximum_mana,minimum_attack,maximum_attack,defense,magic_attack,magic_defense,accuracy,dodge) " +
            "VALUES($1,$2,0,100,0,1,1,0,0,0,0,0) ON CONFLICT DO NOTHING",
            character.Id, character.Version);
        long packets = 0;
        var session = new GameplaySession5165(character, (_, _) =>
        {
            Interlocked.Increment(ref packets);
            return Task.CompletedTask;
        });
        if (!await runtime.EnterPlayerAsync(new AccountId(accountId), session, default))
            throw new InvalidOperationException("The isolated active-region combat gate was unavailable.");
        await runtime.EnterStaminaAsync(new AccountId(accountId), session, default);
        TimeSpan beforeCpu = Process.GetCurrentProcess().TotalProcessorTime;
        Stopwatch wall = Stopwatch.StartNew();
        await Task.Delay(TimeSpan.FromSeconds(3));
        wall.Stop();
        long cpu = checked((long)(Process.GetCurrentProcess().TotalProcessorTime - beforeCpu).TotalMilliseconds);
        MonsterRuntimeMetrics metrics = runtime.GetMetrics();
        runtime.LeaveStamina(session);
        await runtime.LeavePlayerAsync(session, default);
        long transactions = Math.Max(0, await DatabaseTransactionsAsync(connection) - beforeTransactions);
        return new(metrics, wall.ElapsedMilliseconds, cpu, transactions, packets);

        async Task ExecuteFixtureAsync(string sql, params object[] values)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            foreach (object value in values) command.Parameters.AddWithValue(value);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<long> DatabaseTransactionsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT xact_commit+xact_rollback FROM pg_stat_database WHERE datname=current_database()", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Task NewRoleLayout()
    {
        byte[] packet = Genuine5165RuntimeComponent.EncodeNewRoleForEvidence();
        Equal(packet.Length, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(packet));
        Equal((ushort)1004, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)));
        Equal((ushort)2101, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(8)));
        Equal((byte)4, packet[24]);
        string text = System.Text.Encoding.ASCII.GetString(packet);
        if (!text.Contains("SYSTEM", StringComparison.Ordinal) || !text.Contains("ALLUSERS", StringComparison.Ordinal) ||
            !text.Contains("NEW_ROLE", StringComparison.Ordinal)) throw new InvalidOperationException("NEW_ROLE string list is invalid.");
        return Task.CompletedTask;
    }

    private static Task ItemEconomyPacketLayouts()
    {
        byte[] action = new byte[ItemEconomyPackets5165.ItemActionLength];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(action, checked((ushort)action.Length));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(action.AsSpan(2), ItemEconomyPackets5165.ItemActionPacketId);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(action.AsSpan(4), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(action.AsSpan(8), 150005);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(action.AsSpan(12), ItemEconomyPackets5165.ShopBuy);
        action[20] = 1;
        ItemAction5165 parsed = ItemEconomyPackets5165.ParseItemAction(action);
        Equal(1u, parsed.Subject); Equal(150005u, parsed.Command); Equal(1u, parsed.Argument);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(action.AsSpan(12), 0);
        Equal(0u, ItemEconomyPackets5165.ParseItemAction(action).Action);

        var definition = new ItemDefinition(new ItemDefinitionId(150005), "IronRing", 100,
            EquipmentSlot.Ring, new ItemRequirements(), new ItemStatBlock());
        var item = new ItemInstance(new ItemInstanceId(Guid.NewGuid()), new LegacyItemUid(90_000_001),
            definition.Id, new AccountId(Guid.NewGuid()), new CharacterUid(1), ItemLocation.Inventory(2), 100);
        byte[] info = ItemEconomyPackets5165.EncodeItemInfo(item, definition);
        Equal(48, info.Length);
        Equal((ushort)1008, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(info.AsSpan(2)));
        Equal(item.LegacyUid.Value, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(info.AsSpan(4)));
        Equal((ushort)0, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(info.AsSpan(18)));
        ItemInstance equippedItem = item with { Location = ItemLocation.Equipped(EquipmentSlot.Ring) };
        byte[] equippedInfo = ItemEconomyPackets5165.EncodeItemInfo(equippedItem, definition);
        Equal((ushort)6, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(equippedInfo.AsSpan(18)));

        byte[] warehouseAdd = ItemEconomyPackets5165.EncodeWarehouseLiveAdd(8, item,
            new SingleItemCatalog(definition));
        Equal(48, warehouseAdd.Length);
        Equal((byte)0, warehouseAdd[8]);
        Equal((byte)0, warehouseAdd[9]);
        Equal(1u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(warehouseAdd.AsSpan(12)));
        Equal(item.LegacyUid.Value,
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(warehouseAdd.AsSpan(16)));
        Equal(item.DefinitionId.Value,
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(warehouseAdd.AsSpan(20)));
        Equal(true, ExactClientAcceptsWarehouseLiveAdd(warehouseAdd));
        byte[] obsoleteLiveAdd = new byte[48];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(obsoleteLiveAdd, 48);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(obsoleteLiveAdd.AsSpan(2), 1102);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(obsoleteLiveAdd.AsSpan(4), 8);
        obsoleteLiveAdd[8] = 1;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(obsoleteLiveAdd.AsSpan(12), item.LegacyUid.Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(obsoleteLiveAdd.AsSpan(16), item.DefinitionId.Value);
        Equal(false, ExactClientAcceptsWarehouseLiveAdd(obsoleteLiveAdd));
        byte[] warehouseRemove = ItemEconomyPackets5165.EncodeWarehouseLiveRemove(8, item);
        Equal((byte)2, warehouseRemove[8]);
        Equal((byte)0, warehouseRemove[9]);
        Equal(item.LegacyUid.Value,
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(warehouseRemove.AsSpan(12)));
        Equal(-1, warehouseRemove.AsSpan(16).IndexOfAnyExcept((byte)0));

        byte[] ping = ItemEconomyPackets5165.EncodeItemAction(7, 8, ItemEconomyPackets5165.Ping, 115);
        Equal(7u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(4)));
        Equal(8u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(8)));
        Equal(27u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(12)));
        Equal(115u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(16)));

        byte[] warehouse = new byte[48];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(warehouse, 48);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(warehouse.AsSpan(2), 1102);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(warehouse.AsSpan(4), 8);
        warehouse[8] = 1;
        warehouse[9] = ItemEconomyPackets5165.TwinCityWarehouseSelector;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(warehouse.AsSpan(12), item.LegacyUid.Value);
        WarehouseAction5165 warehouseAction = ItemEconomyPackets5165.ParseWarehouseAction(warehouse);
        Equal((byte)1, warehouseAction.Action); Equal(8u, warehouseAction.NpcUid);
        Equal(ItemEconomyPackets5165.TwinCityWarehouseSelector, warehouseAction.WarehouseSelector);
        warehouse[8] = 99;
        Equal((byte)99, ItemEconomyPackets5165.ParseWarehouseAction(warehouse).Action);
        byte[] syntheticShortWarehouse = warehouse[..16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(syntheticShortWarehouse, 16);
        try { _ = ItemEconomyPackets5165.ParseWarehouseAction(syntheticShortWarehouse); }
        catch (InvalidDataException) { goto ExactWarehouseTail; }
        throw new InvalidOperationException("The obsolete synthetic 16-byte client mutation was accepted.");
    ExactWarehouseTail:
        warehouse[16] = 1;
        try { _ = ItemEconomyPackets5165.ParseWarehouseAction(warehouse); }
        catch (InvalidDataException) { warehouse[16] = 0; goto ExactWarehouseDone; }
        throw new InvalidOperationException("A warehouse mutation with a nonzero exact-client tail was accepted.");
    ExactWarehouseDone:
        byte[] status = ItemEconomyPackets5165.EncodeStatus(1, 4, 10_000);
        Equal(36, status.Length); Equal((ushort)10017,
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(status.AsSpan(2)));

        action[24] = 1;
        try { _ = ItemEconomyPackets5165.ParseItemAction(action); }
        catch (InvalidDataException) { return Task.CompletedTask; }
        throw new InvalidOperationException("An item action with nonzero reserved bytes was accepted.");

        static bool ExactClientAcceptsWarehouseLiveAdd(ReadOnlySpan<byte> packet) =>
            packet.Length == 48 && packet[8] == 0 &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]) == 1 &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]) != 0 &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(packet[20..]) != 0;
    }

    private static Task ProgressionPacketLayouts()
    {
        byte[] packet = Revora.NativeLogin.Core.GamePacketsAfterAuthentication5165.EncodeUserInfo(
            2, "SHEEKOVIC", 1003, 142, 130, 2_000_000_000, 2_000_000_000, 0,
            0, 47, 75, 245, 30, 2_000, 7_350, 0);
        Revora.NativeLogin.Core.UserInformation5165 parsed =
            Revora.NativeLogin.Core.GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(packet);
        Equal((byte)130, parsed.Level); Equal((byte)142, parsed.Profession);
        Equal(2_000_000_000u, parsed.Silver); Equal(2_000_000_000u, parsed.ConquerPoints);
        Equal((ushort)30, parsed.AttributePoints);
        byte[] level = ItemEconomyPackets5165.EncodeStatus(2, 12, 130);
        Equal((ushort)10017, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(level.AsSpan(2)));
        Equal(12u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(level.AsSpan(12)));
        Equal(130UL, System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(level.AsSpan(16)));
        return Task.CompletedTask;
    }

    private static Task ClientTimingSemantics()
    {
        TimeZoneInfo cairo = ClientTimingPackets5165.ResolveCairoTimeZone();
        DateTimeOffset winterUtc = new(2026, 1, 15, 10, 20, 30, TimeSpan.Zero);
        DateTimeOffset summerUtc = new(2026, 7, 15, 10, 20, 30, TimeSpan.Zero);
        Equal(TimeSpan.FromHours(2), cairo.GetUtcOffset(winterUtc));
        Equal(TimeSpan.FromHours(3), cairo.GetUtcOffset(summerUtc));

        byte[] winter = ClientTimingPackets5165.EncodeServerClock(winterUtc, cairo);
        Equal("24000904000000007E000000000000000F0000000F0000000C000000140000001E000000",
            Convert.ToHexString(winter));
        ClientClockFields5165 winterFields = ClientTimingPackets5165.ParseAsExactClient(winter);
        Equal((ushort)36, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(winter));
        Equal((ushort)1033, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(winter.AsSpan(2)));
        Equal(126u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(winter.AsSpan(8)));
        Equal(0u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(winter.AsSpan(12)));
        Equal(2026, winterFields.Year); Equal(1, winterFields.Month); Equal(15, winterFields.Day);
        Equal(12, winterFields.Hour); Equal(20, winterFields.Minute); Equal(30, winterFields.Second);
        ClientClockFields5165 summerFields = ClientTimingPackets5165.ParseAsExactClient(
            ClientTimingPackets5165.EncodeServerClock(summerUtc, cairo));
        Equal(13, summerFields.Hour);

        Equal(1970, ClientTimingPackets5165.ParseAsExactClient(
            ClientTimingPackets5165.EncodeServerClock(DateTimeOffset.UnixEpoch, cairo)).Year);
        byte[] zeroFields = new byte[ClientTimingPackets5165.ServerClockLength];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(zeroFields, 36);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(zeroFields.AsSpan(2), 1033);
        try { _ = ClientTimingPackets5165.ParseAsExactClient(zeroFields); }
        catch (InvalidDataException) { goto InvalidClockRejected; }
        throw new InvalidOperationException("An all-zero epoch/calendar frame was accepted.");

    InvalidClockRejected:
        const long unixSeconds = 1_788_000_000;
        DateTimeOffset fromSeconds = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        DateTimeOffset fromMilliseconds = DateTimeOffset.FromUnixTimeMilliseconds(unixSeconds * 1000);
        DateTimeOffset fromTicks = new(new DateTime(fromSeconds.UtcTicks, DateTimeKind.Utc));
        Equal(fromSeconds, fromMilliseconds); Equal(fromSeconds, fromTicks);
        ClientClockFields5165 secondsFields = ClientTimingPackets5165.ParseAsExactClient(
            ClientTimingPackets5165.EncodeServerClock(fromSeconds, cairo));
        ClientClockFields5165 millisecondsMistake = ClientTimingPackets5165.ParseAsExactClient(
            ClientTimingPackets5165.EncodeServerClock(DateTimeOffset.FromUnixTimeMilliseconds(unixSeconds), cairo));
        if (secondsFields.Year <= 2020 || millisecondsMistake.Year != 1970)
            throw new InvalidOperationException("Seconds/milliseconds confusion was not exposed by the fixture.");

        byte[] ping = ItemEconomyPackets5165.EncodeItemAction(2, 0, ItemEconomyPackets5165.Ping, 0xfffffff8);
        Equal("1C00F10302000000000000001B000000F8FFFFFF0000000000000000",
            Convert.ToHexString(ping));
        Equal(0xfffffff8u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(ping.AsSpan(16)));
        Equal(24u, ClientTimingPackets5165.CalculateClientRttMilliseconds(0xfffffff8, 0x10));
        Equal(true, ClientTimingPackets5165.IsPlausibleClientRtt(172));
        Equal(false, ClientTimingPackets5165.IsPlausibleClientRtt(unchecked((uint)-1)));
        Equal(false, ClientTimingPackets5165.IsPlausibleClientRtt(uint.MaxValue));

        // Wall-clock movement cannot affect either measurement: both exact-client models accept
        // only same-machine UInt32 monotonic samples. This also exercises counter wraparound.
        DateTimeOffset wallClockBefore = new(2026, 10, 29, 23, 59, 59, TimeSpan.Zero);
        DateTimeOffset wallClockAfter = wallClockBefore.AddHours(-12);
        if (wallClockAfter >= wallClockBefore)
            throw new InvalidOperationException("The wall-clock discontinuity fixture is invalid.");
        Equal(24u, ClientTimingPackets5165.CalculateClientRttMilliseconds(0xfffffff8, 0x10));

        // Approved client 0x4bb0c9 samples timeGetTime(), computes 1000 / elapsed, then averages
        // with the previous value. No wall-clock or server field participates in this FPS value.
        Equal(10u, ExactClientFpsSample(1_000, 1_050, 0));
        Equal(15u, ExactClientFpsSample(1_050, 1_100, 10));
        Equal(20u, ExactClientFpsSample(0xfffffff0, 0x18, 15));
        return Task.CompletedTask;

        static uint ExactClientFpsSample(uint previousMonotonicTick, uint currentMonotonicTick,
            uint previousFps)
        {
            uint elapsed = unchecked(currentMonotonicTick - previousMonotonicTick);
            if (elapsed == 0) throw new InvalidDataException("A zero FPS sampling window is invalid.");
            return checked(((1000u / elapsed) + previousFps) / 2u);
        }
    }

    private static Task ReviewedServicePaths()
    {
        new RevoraHostOptions { DataDirectory = "../data", LogDirectory = "../logs" }.Validate();
        try { new RevoraHostOptions { DataDirectory = "../../private" }.Validate(); }
        catch (InvalidDataException) { return Task.CompletedTask; }
        throw new InvalidOperationException("An unreviewed service data path was accepted.");
    }

    private static async Task Run(string name, Func<Task> test)
    {
        await test();
        _passed++;
        Console.WriteLine($"PASS: {name}");
    }

    private static async Task SuccessfulLifecycle()
    {
        var output = new StringWriter();
        var persistence = new InMemoryPersistenceRuntime();
        var coordinator = Coordinator(output, persistence,
            Listener("AccountServer", FreePort()), Listener("GameServer", FreePort()));
        await coordinator.StartAsync(CancellationToken.None);
        await coordinator.StopAsync(TimeSpan.FromSeconds(2));
        Equal(HealthState.Stopped, coordinator.State);
        Equal(1, Count(output.ToString(), "[Revora] READY"));
        Equal(1, Count(output.ToString(), "[Revora] STOPPED"));
    }

    private static async Task OccupiedAccountPort()
    {
        using var occupied = Occupy(out var port);
        var listener = Listener("AccountServer", port);
        await ThrowsAsync<SocketException>(() => listener.StartAsync(CancellationToken.None));
        Equal(HealthState.Failed, listener.GetHealth().State);
    }

    private static async Task OccupiedGamePort()
    {
        using var occupied = Occupy(out var port);
        var output = new StringWriter();
        var coordinator = Coordinator(output, new InMemoryPersistenceRuntime(),
            Listener("AccountServer", FreePort()), Listener("GameServer", port));
        await ThrowsAsync<SocketException>(() => coordinator.StartAsync(CancellationToken.None));
        Equal(0, Count(output.ToString(), "[Revora] READY"));
        Equal(HealthState.Failed, coordinator.State);
    }

    private static async Task UnexpectedFailure(string failedName)
    {
        var output = new StringWriter();
        var persistence = new FakeComponent("Persistence");
        var account = new FakeComponent("AccountServer");
        var game = new FakeComponent("GameServer");
        var coordinator = new RevoraCoordinator(persistence, account, game, game, new SafeConsoleLog(output, output));
        await coordinator.StartAsync(CancellationToken.None);
        (failedName == "AccountServer" ? account : game).Fail();
        await coordinator.WaitForShutdownRequestAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        Equal(true, coordinator.UnexpectedFailure);
        Equal(HealthState.Failed, coordinator.State);
        Equal(true, output.ToString().Contains("UnexpectedComponentFailure", StringComparison.Ordinal));
        await coordinator.StopAsync(TimeSpan.FromSeconds(1));
    }

    private static async Task TwoIsolatedInstances()
    {
        var firstPersistence = new InMemoryPersistenceRuntime();
        var secondPersistence = new InMemoryPersistenceRuntime();
        var first = Coordinator(new StringWriter(), firstPersistence,
            Listener("AccountServer", FreePort()), Listener("GameServer", FreePort()));
        var second = Coordinator(new StringWriter(), secondPersistence,
            Listener("AccountServer", FreePort()), Listener("GameServer", FreePort()));
        await Task.WhenAll(first.StartAsync(CancellationToken.None), second.StartAsync(CancellationToken.None));
        await first.SaveAsync(CancellationToken.None);
        Equal(1, firstPersistence.SaveCount);
        Equal(0, secondPersistence.SaveCount);
        await Task.WhenAll(first.StopAsync(TimeSpan.FromSeconds(2)), second.StopAsync(TimeSpan.FromSeconds(2)));
    }

    private static async Task BoundedShutdownWithClient()
    {
        var accountPort = FreePort();
        var coordinator = Coordinator(new StringWriter(), new InMemoryPersistenceRuntime(),
            Listener("AccountServer", accountPort), Listener("GameServer", FreePort()));
        await coordinator.StartAsync(CancellationToken.None);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, accountPort);
        var stopwatch = Stopwatch.StartNew();
        await coordinator.StopAsync(TimeSpan.FromMilliseconds(100));
        Equal(true, stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        Equal(HealthState.Stopped, coordinator.State);
    }

    private static async Task SaveDuringShutdown()
    {
        var persistence = new InMemoryPersistenceRuntime();
        var coordinator = Coordinator(new StringWriter(), persistence,
            Listener("AccountServer", FreePort()), Listener("GameServer", FreePort()));
        await coordinator.StartAsync(CancellationToken.None);
        await coordinator.StopAsync(TimeSpan.FromSeconds(2));
        Equal(1, persistence.SaveCount);
    }

    private static async Task MalformedConsoleInput()
    {
        var output = new StringWriter();
        var persistence = new FakeComponent("Persistence");
        var account = new FakeComponent("AccountServer");
        var game = new FakeComponent("GameServer");
        var coordinator = new RevoraCoordinator(persistence, account, game, game, new SafeConsoleLog(output, output));
        var console = new OperatorConsole(coordinator, new SafeConsoleLog(output, output), TextReader.Null, output);
        await console.ExecuteAsync("; touch /tmp/revora-should-not-exist", CancellationToken.None);
        Equal(false, persistence.Saved);
        Equal(true, output.ToString().Contains("Unknown command", StringComparison.Ordinal));
    }

    private static async Task StdinStop()
    {
        var output = new StringWriter();
        var persistence = new FakeComponent("Persistence");
        var account = new FakeComponent("AccountServer");
        var game = new FakeComponent("GameServer");
        var coordinator = new RevoraCoordinator(persistence, account, game, game, new SafeConsoleLog(output, output));
        await coordinator.StartAsync(CancellationToken.None);
        var console = new OperatorConsole(coordinator, new SafeConsoleLog(output, output), TextReader.Null, output);
        await console.ExecuteAsync("stop", CancellationToken.None);
        await coordinator.WaitForShutdownRequestAsync(CancellationToken.None);
        await coordinator.StopAsync(TimeSpan.FromSeconds(1));
        Equal(1, Count(output.ToString(), "[Revora] STOPPED"));
        Equal(true, persistence.Saved);
    }

    private static async Task PublishedSigterm(string artifact)
    {
        var root = Path.Combine(Path.GetTempPath(), $"Revora Host Test {Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "config"));
        Directory.CreateDirectory(Path.Combine(root, "data"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        var accountPort = FreePort();
        var gamePort = FreePort();
        var canary = "REVORA_SECRET_CANARY_92741";
        var config = $$"""
        {"Environment":"Test","RuntimeAdapter":"InMemory","AccountListener":{"Address":"127.0.0.1","Port":{{accountPort}}},"GameListener":{"Address":"127.0.0.1","Port":{{gamePort}}},"ShutdownTimeoutSeconds":5,"SecretFile":"config/revora.secrets.json"}
        """;
        await File.WriteAllTextAsync(Path.Combine(root, "config", "revora.json"), config);
        var secretPath = Path.Combine(root, "config", "revora.secrets.json");
        await File.WriteAllTextAsync(secretPath, $"{{\"Canary\":\"{canary}\"}}");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(secretPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        var lines = new ConcurrentQueue<string>();
        using var process = Process.Start(new ProcessStartInfo(artifact, "--config config/revora.json")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("Could not start published host.");
        process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) lines.Enqueue(eventArgs.Data); };
        process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) lines.Enqueue(eventArgs.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await WaitUntil(() => lines.Count(line => line == "[Revora] READY") == 1, TimeSpan.FromSeconds(10));
        using var signal = Process.Start(new ProcessStartInfo("/bin/kill", $"-TERM {process.Id}") { UseShellExecute = false })
            ?? throw new InvalidOperationException("Could not signal published host.");
        await signal.WaitForExitAsync();
        Equal(0, signal.ExitCode);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var combined = string.Join('\n', lines);
        Equal(0, process.ExitCode);
        Equal(1, Count(combined, "[Revora] READY"));
        Equal(1, Count(combined, "[Revora] STOPPED"));
        Equal(false, combined.Contains(canary, StringComparison.Ordinal));
        Directory.Delete(root, recursive: true);
    }

    private static LoopbackTcpRuntimeComponent Listener(string name, int port) =>
        new(name, IPAddress.Loopback, port, 20);

    private static RevoraCoordinator Coordinator(StringWriter output, IPersistenceRuntime persistence,
        LoopbackTcpRuntimeComponent account, LoopbackTcpRuntimeComponent game) =>
        new(persistence, account, game, game, new SafeConsoleLog(output, output));

    private static TcpListener Occupy(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static int FreePort()
    {
        using var listener = Occupy(out var port);
        return port;
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var started = Stopwatch.StartNew();
        while (!condition())
        {
            if (started.Elapsed > timeout) throw new TimeoutException("Condition was not reached.");
            await Task.Delay(20);
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
    }

    private static async Task ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try { await action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed class FakeComponent : IPersistenceRuntime, IPlayerCountProvider
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private HealthState _state = HealthState.Stopped;
        public FakeComponent(string name) => Name = name;
        public string Name { get; }
        public Task Completion => _completion.Task;
        public int PlayerCount => 0;
        public bool Saved { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) { _state = HealthState.Ready; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) { _state = HealthState.Stopped; _completion.TrySetResult(); return Task.CompletedTask; }
        public Task SaveAsync(CancellationToken cancellationToken) { Saved = true; return Task.CompletedTask; }
        public void Fail() { _state = HealthState.Failed; _completion.TrySetException(new InvalidOperationException("Test-only failure.")); }
        public ComponentHealth GetHealth() => new(Name, _state, "Test component.");
    }

    private sealed class SingleItemCatalog(ItemDefinition definition) : IItemDefinitionCatalog
    {
        public ItemDefinition? Find(ItemDefinitionId id) => id == definition.Id ? definition : null;
    }
}
