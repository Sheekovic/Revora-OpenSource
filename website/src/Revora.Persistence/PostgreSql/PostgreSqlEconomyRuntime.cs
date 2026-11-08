using Npgsql;
using Revora.Domain.Accounts;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;

namespace Revora.Persistence.PostgreSql;

/// <summary>Read-side and rollout boundary for the bounded 5165 item/economy campaign.</summary>
public sealed class PostgreSqlEconomyRuntime : IAsyncDisposable
{
    private static readonly string[] RequiredFeatures =
    [
        "world.catalog.complete", "items.definitions", "inventory", "currency.ledger",
        "equipment.stats", "shops", "warehouse",
    ];

    private readonly NpgsqlDataSource dataSource;

    public PostgreSqlEconomyRuntime(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConnectionStringBuilder.ApplicationName = "Revora.EconomyRuntime";
        builder.ConnectionStringBuilder.Timeout = 5;
        builder.ConnectionStringBuilder.CommandTimeout = 10;
        builder.ConnectionStringBuilder.MaxPoolSize = 20;
        dataSource = builder.Build();
        Store = new PostgreSqlGameplayStore(connectionString);
    }

    public PostgreSqlGameplayStore Store { get; }

    public static string BuildLocalPeerConnectionString(string socketPath, string username, string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        return new NpgsqlConnectionStringBuilder
        {
            Host = Path.GetFullPath(socketPath),
            Database = databaseName,
            Username = username,
            Pooling = true,
            MaxPoolSize = 20,
            Timeout = 5,
            CommandTimeout = 10,
            ApplicationName = "Revora.EconomyRuntime",
        }.ConnectionString;
    }

    public ItemIdentity NextItemIdentity()
    {
        using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT nextval('game.item_instances_legacy_item_uid_seq')");
        long value = Convert.ToInt64(command.ExecuteScalar());
        return new(new ItemInstanceId(Guid.NewGuid()), new LegacyItemUid(checked((uint)value)));
    }

    public async Task EnsureWarehouseVaultAsync(GameplayActor actor, WarehouseDefinition warehouse,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "WITH reviewed AS (" +
            " SELECT w.source_id,w.legacy_warehouse_id,w.slot_capacity FROM gameplay_reference.warehouse_definitions w" +
            " JOIN gameplay_reference.warehouse_release_entries e USING(source_id,legacy_warehouse_id)" +
            " JOIN gameplay_reference.catalog_releases r USING(release_id)" +
            " WHERE r.release_state='active' AND w.legacy_warehouse_id=$3 AND w.npc_legacy_uid=$4" +
            "), owner AS (SELECT character_id FROM game.characters WHERE account_id=$1 AND legacy_uid=$2)" +
            " INSERT INTO game.warehouse_vaults(vault_id,owner_character_id,warehouse_source_id,legacy_warehouse_id,slot_capacity)" +
            " SELECT $5,owner.character_id,reviewed.source_id,reviewed.legacy_warehouse_id,reviewed.slot_capacity" +
            " FROM owner,reviewed ON CONFLICT(owner_character_id,warehouse_source_id,legacy_warehouse_id) DO NOTHING");
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        command.Parameters.AddWithValue((long)warehouse.LegacyId);
        command.Parameters.AddWithValue((long)warehouse.NpcUid);
        command.Parameters.AddWithValue(Guid.NewGuid());
        int changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (changed == 0)
        {
            await using NpgsqlCommand verify = dataSource.CreateCommand(
                "SELECT count(*) FROM game.warehouse_vaults v JOIN game.characters c ON c.character_id=v.owner_character_id" +
                " WHERE c.account_id=$1 AND c.legacy_uid=$2 AND v.legacy_warehouse_id=$3");
            verify.Parameters.AddWithValue(actor.AccountId.Value);
            verify.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
            verify.Parameters.AddWithValue((long)warehouse.LegacyId);
            if (Convert.ToInt32(await verify.ExecuteScalarAsync(cancellationToken)) != 1)
                throw new InvalidDataException("The reviewed warehouse vault could not be established.");
        }
    }

    public async Task<GameplayPlayerSnapshot?> ReadPlayerAsync(GameplayActor actor, CancellationToken cancellationToken) =>
        await Store.ExecuteAsync(transaction =>
        {
            CharacterGameplayState? character = transaction.FindCharacter(actor.CharacterUid);
            if (character is null || character.AccountId != actor.AccountId) return null;
            return new GameplayPlayerSnapshot(character, transaction.GetItems(actor.CharacterUid));
        }, cancellationToken);

    public async Task<ItemInstanceId?> ResolveOwnedItemAsync(GameplayActor actor, LegacyItemUid legacyUid,
        CancellationToken cancellationToken) =>
        await Store.ExecuteAsync(transaction =>
        {
            CharacterGameplayState? character = transaction.FindCharacter(actor.CharacterUid);
            if (character is null || character.AccountId != actor.AccountId) return null;
            ItemInstance? item = transaction.GetItems(actor.CharacterUid)
                .SingleOrDefault(candidate => candidate.LegacyUid == legacyUid);
            return item?.Id;
        }, cancellationToken);

    public async Task PersistCalculatedStatsAsync(GameplayActor actor, CalculatedCharacterStats stats,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "WITH owner AS (" +
            " SELECT c.character_id,c.row_version,coalesce(sum(i.row_version),0)::bigint AS inventory_revision" +
            " FROM game.characters c LEFT JOIN game.item_instances i ON i.owner_character_id=c.character_id" +
            " WHERE c.account_id=$1 AND c.legacy_uid=$2 GROUP BY c.character_id,c.row_version" +
            ") INSERT INTO game.character_stat_snapshots(character_id,character_row_version,inventory_revision," +
            "maximum_health,maximum_mana,minimum_attack,maximum_attack,defense,magic_attack,magic_defense,accuracy,dodge)" +
            " SELECT character_id,row_version,inventory_revision,$3,$4,$5,$6,$7,$8,$9,$10,$11 FROM owner" +
            " ON CONFLICT(character_id) DO UPDATE SET character_row_version=excluded.character_row_version," +
            "inventory_revision=excluded.inventory_revision,maximum_health=excluded.maximum_health," +
            "maximum_mana=excluded.maximum_mana,minimum_attack=excluded.minimum_attack," +
            "maximum_attack=excluded.maximum_attack,defense=excluded.defense,magic_attack=excluded.magic_attack," +
            "magic_defense=excluded.magic_defense,accuracy=excluded.accuracy,dodge=excluded.dodge," +
            "calculated_at=clock_timestamp()");
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        command.Parameters.AddWithValue(stats.MaximumHealth);
        command.Parameters.AddWithValue(stats.MaximumMana);
        command.Parameters.AddWithValue(stats.Equipment.MinimumAttack);
        command.Parameters.AddWithValue(stats.Equipment.MaximumAttack);
        command.Parameters.AddWithValue(stats.Equipment.Defense);
        command.Parameters.AddWithValue(stats.Equipment.MagicAttack);
        command.Parameters.AddWithValue(stats.Equipment.MagicDefense);
        command.Parameters.AddWithValue(stats.Equipment.Dexterity);
        command.Parameters.AddWithValue(stats.Equipment.Dodge);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidDataException("The authenticated character stat snapshot was not persisted.");
    }

    public async Task<EconomyRolloutDecision> EvaluateRolloutAsync(GameplayActor actor,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT g.feature_key,g.gate_state,r.rollout_scope," +
            "(EXISTS(SELECT 1 FROM gameplay.feature_canaries c JOIN game.characters ch ON ch.character_id=c.character_id " +
            "WHERE c.feature_key=g.feature_key AND c.account_id=$1 AND c.legacy_character_uid=$2 AND ch.account_id=$1) " +
            "OR EXISTS(SELECT 1 FROM gameplay.account_feature_canaries c JOIN game.characters ch ON ch.account_id=c.account_id " +
            "WHERE c.feature_key=g.feature_key AND c.account_id=$1 AND ch.account_id=$1 AND ch.legacy_uid=$2)) " +
            "FROM gameplay.feature_gates g JOIN gameplay.feature_rollouts r USING(feature_key) " +
            "WHERE g.feature_key=ANY($3) ORDER BY g.feature_key");
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        command.Parameters.AddWithValue(RequiredFeatures);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        int count = 0;
        bool global = true;
        while (await reader.ReadAsync(cancellationToken))
        {
            count++;
            if (!string.Equals(reader.GetString(1), "enabled", StringComparison.Ordinal))
                return new(false, "gate-disabled");
            string scope = reader.GetString(2);
            if (scope == "disabled") return new(false, "rollout-disabled");
            if (scope == "canary" && !reader.GetBoolean(3)) return new(false, "not-canary");
            global &= scope == "global";
        }
        return count == RequiredFeatures.Length
            ? new(true, global ? "global" : "canary")
            : new(false, "gate-evidence-incomplete");
    }

    public async Task<EconomyCatalogSnapshot> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        Guid releaseId;
        await using (NpgsqlCommand release = dataSource.CreateCommand(
            "SELECT release_id FROM gameplay_reference.catalog_releases WHERE release_state='active' ORDER BY activated_at DESC LIMIT 2"))
        await using (NpgsqlDataReader reader = await release.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidDataException("No active gameplay catalog release exists.");
            releaseId = reader.GetGuid(0);
            if (await reader.ReadAsync(cancellationToken)) throw new InvalidDataException("More than one active gameplay catalog release exists.");
        }

        var items = new Dictionary<ItemDefinitionId, ItemDefinition>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT i.item_type,i.safe_name,i.maximum_durability,i.equipment_slot,i.required_level,i.required_profession," +
            "i.required_strength,i.required_agility,i.minimum_attack,i.maximum_attack,i.defense,i.magic_attack,i.magic_defense," +
            "i.health_bonus,i.mana_bonus,i.accuracy,i.dodge FROM gameplay_reference.item_type_release_entries e " +
            "JOIN gameplay_reference.item_types i USING(source_id,item_type) WHERE e.release_id=$1 ORDER BY i.item_type"))
        {
            command.Parameters.AddWithValue(releaseId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = new ItemDefinitionId(checked((uint)reader.GetInt64(0)));
                short rawSlot = reader.GetInt16(3);
                EquipmentSlot? slot = rawSlot == 0 ? null : (EquipmentSlot)checked((byte)rawSlot);
                byte profession = checked((byte)reader.GetInt16(5));
                items.Add(id, new ItemDefinition(id, reader.GetString(1), checked((ushort)reader.GetInt32(2)), slot,
                    new ItemRequirements(checked((byte)reader.GetInt16(4)), profession == 0 ? null : profession,
                        checked((ushort)reader.GetInt32(6)), checked((ushort)reader.GetInt32(7))),
                    new ItemStatBlock(reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10), reader.GetInt32(11),
                        reader.GetInt32(12), reader.GetInt32(13), reader.GetInt32(14), reader.GetInt32(15), reader.GetInt32(16))));
            }
        }

        var shopRows = new Dictionary<ShopId, Dictionary<ItemDefinitionId, ShopEntry>>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT s.legacy_shop_id,s.currency_type,i.item_type,i.purchase_price,i.sale_price " +
            "FROM gameplay_reference.shop_release_entries e JOIN gameplay_reference.shop_catalogs s USING(source_id,legacy_shop_id) " +
            "JOIN gameplay_reference.shop_catalog_items i USING(source_id,legacy_shop_id) WHERE e.release_id=$1 ORDER BY s.legacy_shop_id,i.position"))
        {
            command.Parameters.AddWithValue(releaseId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var shopId = new ShopId(checked((uint)reader.GetInt64(0)));
                var itemId = new ItemDefinitionId(checked((uint)reader.GetInt64(2)));
                CurrencyKind currency = ParseCurrency(reader.GetString(1));
                if (!shopRows.TryGetValue(shopId, out Dictionary<ItemDefinitionId, ShopEntry>? entries))
                    shopRows.Add(shopId, entries = []);
                entries.Add(itemId, new ShopEntry(itemId, currency, reader.GetInt64(3), reader.GetInt64(4)).Validate());
            }
        }

        var warehouses = new Dictionary<uint, WarehouseDefinition>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT w.legacy_warehouse_id,w.npc_legacy_uid,w.slot_capacity FROM gameplay_reference.warehouse_release_entries e " +
            "JOIN gameplay_reference.warehouse_definitions w USING(source_id,legacy_warehouse_id) WHERE e.release_id=$1 ORDER BY w.legacy_warehouse_id"))
        {
            command.Parameters.AddWithValue(releaseId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                uint id = checked((uint)reader.GetInt64(0));
                warehouses.Add(id, new(id, checked((uint)reader.GetInt64(1)), checked((ushort)reader.GetInt16(2))));
            }
        }

        return new(releaseId, items,
            shopRows.ToDictionary(row => row.Key, row => new ShopDefinition(row.Key, row.Value)), warehouses);
    }

    public async ValueTask DisposeAsync()
    {
        await Store.DisposeAsync();
        await dataSource.DisposeAsync();
    }

    private static CurrencyKind ParseCurrency(string value) => value switch
    {
        "silver" => CurrencyKind.Silver,
        "conquer-points" => CurrencyKind.ConquerPoints,
        "bound-conquer-points" => CurrencyKind.BoundConquerPoints,
        _ => throw new InvalidDataException("The active shop uses an unsupported currency."),
    };
}
