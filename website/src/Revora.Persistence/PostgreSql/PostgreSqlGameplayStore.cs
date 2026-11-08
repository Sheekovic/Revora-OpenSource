using System.Data;
using Npgsql;
using Revora.Domain.Accounts;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;

namespace Revora.Persistence.PostgreSql;

/// <summary>
/// Transactional persistence for the new gameplay aggregates. This store is intentionally
/// separate from PostgreSqlGameDataStore so the proven Account/world runtime remains unchanged.
/// </summary>
public sealed class PostgreSqlGameplayStore : IGameplayStore, IAsyncDisposable
{
    private const int MaximumAttempts = 3;
    private readonly NpgsqlDataSource dataSource;

    public PostgreSqlGameplayStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConnectionStringBuilder.ApplicationName = "Revora.Gameplay";
        builder.ConnectionStringBuilder.Timeout = 5;
        builder.ConnectionStringBuilder.CommandTimeout = 10;
        builder.ConnectionStringBuilder.Pooling = true;
        builder.ConnectionStringBuilder.MaxPoolSize = 20;
        dataSource = builder.Build();
    }

    public async ValueTask<TResult> ExecuteAsync<TResult>(
        Func<IGameplayTransaction, TResult> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var context = new TransactionContext(connection, transaction, cancellationToken);
            try
            {
                TResult result = operation(context);
                context.ValidateForCommit();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (PostgresException exception) when (attempt < MaximumAttempts && IsRetryable(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();

    private static bool IsRetryable(PostgresException exception) =>
        exception.SqlState is "40001" or "40P01" ||
        (exception.SqlState == "23505" &&
         string.Equals(exception.ConstraintName, "gameplay_command_receipts_pkey", StringComparison.Ordinal));

    private sealed class TransactionContext(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken) : IGameplayTransaction
    {
        private readonly Dictionary<CharacterUid, CharacterGameplayState> characters = [];
        private readonly Dictionary<CharacterUid, CharacterGameplayState> originalCharacters = [];
        private readonly Dictionary<(CharacterUid Character, CurrencyKind Currency), long> walletVersions = [];
        private readonly HashSet<CharacterUid> missingCharacters = [];
        private readonly Dictionary<ItemInstanceId, ItemInstance> items = [];
        private readonly Dictionary<ItemInstanceId, long> itemVersions = [];
        private readonly Dictionary<ItemInstanceId, Guid> itemSources = [];
        private readonly HashSet<ItemInstanceId> missingItems = [];
        private readonly Dictionary<GameplayCommandId, GameplayCommandReceipt> receipts = [];
        private readonly HashSet<GameplayCommandId> missingReceipts = [];
        private bool mutated;
        private bool receiptSaved;

        public CharacterGameplayState? FindCharacter(CharacterUid characterUid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (characters.TryGetValue(characterUid, out CharacterGameplayState? cached)) return cached;
            if (missingCharacters.Contains(characterUid)) return null;

            using NpgsqlCommand command = CreateCommand(
                "SELECT c.account_id,c.legacy_uid,COALESCE(ip.slot_capacity,40)," +
                "COALESCE((SELECT max(v.slot_capacity) FROM game.warehouse_vaults v WHERE v.owner_character_id=c.character_id),40)," +
                "c.level,c.profession,COALESCE(a.strength,0),COALESCE(a.agility,0)," +
                "COALESCE(a.vitality,0),COALESCE(a.spirit,0) " +
                "FROM game.characters c " +
                "LEFT JOIN game.inventory_profiles ip ON ip.character_id=c.character_id " +
                "LEFT JOIN game.character_attributes a ON a.character_id=c.character_id " +
                "WHERE c.legacy_uid=$1 FOR UPDATE OF c");
            command.Parameters.AddWithValue(checked((int)characterUid.Value));
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                missingCharacters.Add(characterUid);
                return null;
            }
            Guid accountId = reader.GetGuid(0);
            int inventoryCapacity = reader.GetInt16(2);
            int warehouseCapacity = reader.GetInt16(3);
            byte level = checked((byte)reader.GetInt16(4));
            byte profession = checked((byte)reader.GetInt16(5));
            ushort strength = checked((ushort)reader.GetInt32(6));
            ushort agility = checked((ushort)reader.GetInt32(7));
            ushort vitality = checked((ushort)reader.GetInt32(8));
            ushort spirit = checked((ushort)reader.GetInt32(9));
            reader.Close();

            var balances = new Dictionary<CurrencyKind, long>();
            using NpgsqlCommand wallets = CreateCommand(
                "SELECT currency_type,balance,row_version FROM game.character_wallets " +
                "WHERE character_id=(SELECT character_id FROM game.characters WHERE legacy_uid=$1) FOR UPDATE");
            wallets.Parameters.AddWithValue(checked((int)characterUid.Value));
            using NpgsqlDataReader walletReader = wallets.ExecuteReader();
            while (walletReader.Read())
            {
                CurrencyKind kind = ParseCurrency(walletReader.GetString(0));
                balances.Add(kind, walletReader.GetInt64(1));
                walletVersions.Add((characterUid, kind), walletReader.GetInt64(2));
            }
            walletReader.Close();

            var proficiencies = new Dictionary<ushort, byte>();
            using NpgsqlCommand proficiencyCommand = CreateCommand(
                "SELECT proficiency_type,level FROM game.character_proficiencies " +
                "WHERE character_id=(SELECT character_id FROM game.characters WHERE legacy_uid=$1)");
            proficiencyCommand.Parameters.AddWithValue(checked((int)characterUid.Value));
            using NpgsqlDataReader proficiencyReader = proficiencyCommand.ExecuteReader();
            while (proficiencyReader.Read())
                proficiencies.Add(checked((ushort)proficiencyReader.GetInt32(0)),
                    checked((byte)proficiencyReader.GetInt16(1)));

            var state = new CharacterGameplayState(new AccountId(accountId), characterUid,
                inventoryCapacity, warehouseCapacity, level, profession, strength, agility, vitality, spirit,
                balances, proficiencies);
            characters.Add(characterUid, state);
            originalCharacters.Add(characterUid, state);
            return state;
        }

        public ItemInstance? FindItem(ItemInstanceId itemInstanceId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (items.TryGetValue(itemInstanceId, out ItemInstance? cached)) return cached;
            if (missingItems.Contains(itemInstanceId)) return null;
            using NpgsqlCommand command = CreateCommand(ItemSelectSql +
                " WHERE i.item_instance_id=$1 AND NOT EXISTS (SELECT 1 FROM game.item_locations discarded " +
                "WHERE discarded.item_instance_id=i.item_instance_id AND discarded.location_kind='gm-discarded') " +
                "AND NOT EXISTS (SELECT 1 FROM game.consumed_item_instances consumed " +
                "WHERE consumed.item_instance_id=i.item_instance_id) FOR UPDATE OF i");
            command.Parameters.AddWithValue(itemInstanceId.Value);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                missingItems.Add(itemInstanceId);
                return null;
            }
            return CacheItem(reader);
        }

        public IReadOnlyList<ItemInstance> GetItems(CharacterUid characterUid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using NpgsqlCommand ids = CreateCommand(
                "SELECT i.item_instance_id FROM game.item_instances i " +
                "JOIN game.characters c ON c.character_id=i.owner_character_id " +
                "WHERE c.legacy_uid=$1 AND NOT EXISTS (SELECT 1 FROM game.world_ground_drops d " +
                "WHERE d.item_instance_id=i.item_instance_id AND d.state IN ('active','expired')) " +
                "AND NOT EXISTS (SELECT 1 FROM game.item_locations discarded WHERE " +
                "discarded.item_instance_id=i.item_instance_id AND discarded.location_kind='gm-discarded') " +
                "AND NOT EXISTS (SELECT 1 FROM game.consumed_item_instances consumed " +
                "WHERE consumed.item_instance_id=i.item_instance_id) " +
                "ORDER BY i.legacy_item_uid FOR UPDATE OF i");
            ids.Parameters.AddWithValue(checked((int)characterUid.Value));
            var values = new List<ItemInstanceId>();
            using (NpgsqlDataReader reader = ids.ExecuteReader())
                while (reader.Read()) values.Add(new ItemInstanceId(reader.GetGuid(0)));
            return values.Select(id => FindItem(id) ?? throw new InvalidDataException("A locked item disappeared.")).ToArray();
        }

        public GameplayCommandReceipt? FindReceipt(GameplayCommandId commandId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (receipts.TryGetValue(commandId, out GameplayCommandReceipt? receipt)) return receipt;
            if (missingReceipts.Contains(commandId)) return null;
            using NpgsqlCommand command = CreateCommand(
                "SELECT fingerprint,result_code,result_item_instance_id FROM game.gameplay_command_receipts WHERE command_id=$1");
            command.Parameters.AddWithValue(commandId.Value);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                missingReceipts.Add(commandId);
                return null;
            }
            int rawCode = reader.GetInt32(1);
            if (!Enum.IsDefined(typeof(GameplayResultCode), rawCode))
                throw new InvalidDataException("A gameplay receipt result code is invalid.");
            var result = new GameplayCommandResult((GameplayResultCode)rawCode,
                ItemInstanceId: reader.IsDBNull(2) ? null : new ItemInstanceId(reader.GetGuid(2)));
            receipt = new GameplayCommandReceipt(commandId, reader.GetString(0), result);
            receipts.Add(commandId, receipt);
            return receipt;
        }

        public void SaveCharacter(CharacterGameplayState character)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CharacterGameplayState existing = FindCharacter(character.CharacterUid) ??
                throw new InvalidOperationException("The character does not exist.");
            if (existing.AccountId != character.AccountId)
                throw new InvalidOperationException("Character ownership cannot be changed by gameplay persistence.");
            if (existing.InventoryCapacity != character.InventoryCapacity ||
                existing.WarehouseCapacity != character.WarehouseCapacity ||
                existing.Level != character.Level || existing.Profession != character.Profession ||
                existing.Strength != character.Strength || existing.Agility != character.Agility)
                throw new InvalidOperationException("SaveCharacter currently accepts wallet changes only.");
            characters[character.CharacterUid] = character;
            mutated = true;
        }

        public void AddItem(ItemInstance item)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FindItem(item.Id) is not null) throw new InvalidOperationException("The item instance already exists.");
            CharacterGameplayState owner = FindCharacter(item.CharacterUid) ??
                throw new InvalidOperationException("The item owner does not exist.");
            if (owner.AccountId != item.AccountId)
                throw new InvalidOperationException("The item AccountId does not own the Character UID.");
            Guid sourceId = ResolveDefinitionSource(item.DefinitionId);
            using NpgsqlCommand command = CreateCommand(
                "INSERT INTO game.item_instances(item_instance_id,legacy_item_uid,owner_character_id,item_source_id,item_type," +
                "stack_count,durability,maximum_durability,plus,bless,enchant,socket_one,socket_two,color,bound) " +
                "VALUES($1,$2,(SELECT character_id FROM game.characters WHERE legacy_uid=$3),$4,$5,$6,$7,$7,$8,$9,$10,$11,$12,$13,$14)");
            command.Parameters.AddWithValue(item.Id.Value);
            command.Parameters.AddWithValue(checked((int)item.LegacyUid.Value));
            command.Parameters.AddWithValue(checked((int)item.CharacterUid.Value));
            command.Parameters.AddWithValue(sourceId);
            command.Parameters.AddWithValue((long)item.DefinitionId.Value);
            command.Parameters.AddWithValue((int)item.StackCount);
            command.Parameters.AddWithValue((int)item.Durability);
            command.Parameters.AddWithValue((short)item.Plus);
            command.Parameters.AddWithValue((short)item.Bless);
            command.Parameters.AddWithValue((int)item.Enchant);
            command.Parameters.AddWithValue((int)item.SocketOne);
            command.Parameters.AddWithValue((int)item.SocketTwo);
            command.Parameters.AddWithValue((int)item.Color);
            command.Parameters.AddWithValue(item.Bound);
            RequireOne(command.ExecuteNonQuery(), "The item instance could not be inserted.");
            itemSources[item.Id] = sourceId;
            itemVersions[item.Id] = 1;
            PlaceItem(item);
            items[item.Id] = item;
            missingItems.Remove(item.Id);
            mutated = true;
        }

        public void SaveItem(ItemInstance item)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ItemInstance existing = FindItem(item.Id) ?? throw new InvalidOperationException("The item does not exist.");
            if (existing.AccountId != item.AccountId || existing.CharacterUid != item.CharacterUid ||
                existing.DefinitionId != item.DefinitionId || existing.LegacyUid != item.LegacyUid)
                throw new InvalidOperationException("Gameplay cannot change item identity or ownership through SaveItem.");
            long version = itemVersions[item.Id];
            using NpgsqlCommand update = CreateCommand(
                "UPDATE game.item_instances SET stack_count=$2,durability=$3,plus=$4,bless=$5,enchant=$6,socket_one=$7,socket_two=$8,color=$9,bound=$10," +
                "updated_at=clock_timestamp(),row_version=row_version+1 WHERE item_instance_id=$1 AND row_version=$11");
            update.Parameters.AddWithValue(item.Id.Value);
            update.Parameters.AddWithValue((int)item.StackCount);
            update.Parameters.AddWithValue((int)item.Durability);
            update.Parameters.AddWithValue((short)item.Plus);
            update.Parameters.AddWithValue((short)item.Bless);
            update.Parameters.AddWithValue((int)item.Enchant);
            update.Parameters.AddWithValue((int)item.SocketOne);
            update.Parameters.AddWithValue((int)item.SocketTwo);
            update.Parameters.AddWithValue((int)item.Color);
            update.Parameters.AddWithValue(item.Bound);
            update.Parameters.AddWithValue(version);
            RequireOne(update.ExecuteNonQuery(), "The item optimistic version was stale.");
            itemVersions[item.Id] = checked(version + 1);
            if (existing.Location != item.Location) PlaceItem(item);
            items[item.Id] = item;
            mutated = true;
        }

        public void RemoveItem(ItemInstanceId itemInstanceId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = FindItem(itemInstanceId) ?? throw new InvalidOperationException("The item does not exist.");
            using NpgsqlCommand command = CreateCommand("DELETE FROM game.item_instances WHERE item_instance_id=$1 AND row_version=$2");
            command.Parameters.AddWithValue(itemInstanceId.Value);
            command.Parameters.AddWithValue(itemVersions[itemInstanceId]);
            RequireOne(command.ExecuteNonQuery(), "The item optimistic version was stale.");
            items.Remove(itemInstanceId);
            itemVersions.Remove(itemInstanceId);
            itemSources.Remove(itemInstanceId);
            missingItems.Add(itemInstanceId);
            mutated = true;
        }

        public void SaveReceipt(GameplayCommandReceipt receipt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (receipt.Fingerprint.Length is < 1 or > 512 || receipt.Fingerprint.Any(char.IsControl))
                throw new InvalidDataException("The gameplay command fingerprint is invalid.");
            ApplyPendingBalances(receipt);
            using NpgsqlCommand command = CreateCommand(
                "INSERT INTO game.gameplay_command_receipts(command_id,fingerprint,result_code,result_item_instance_id) VALUES($1,$2,$3,$4)");
            command.Parameters.AddWithValue(receipt.CommandId.Value);
            command.Parameters.AddWithValue(receipt.Fingerprint);
            command.Parameters.AddWithValue((int)receipt.Result.Code);
            command.Parameters.AddWithValue((object?)receipt.Result.ItemInstanceId?.Value ?? DBNull.Value);
            RequireOne(command.ExecuteNonQuery(), "The gameplay command receipt was not inserted.");
            receipts[receipt.CommandId] = receipt;
            missingReceipts.Remove(receipt.CommandId);
            receiptSaved = true;
            mutated = true;
        }

        public void ValidateForCommit()
        {
            if (mutated && !receiptSaved)
                throw new InvalidOperationException("A gameplay mutation cannot commit without an idempotency receipt.");
        }

        private void ApplyPendingBalances(GameplayCommandReceipt receipt)
        {
            foreach ((CharacterUid uid, CharacterGameplayState current) in characters)
            {
                CharacterGameplayState original = originalCharacters[uid];
                foreach (CurrencyKind currency in Enum.GetValues<CurrencyKind>())
                {
                    long before = original.Balance(currency);
                    long after = current.Balance(currency);
                    if (before == after) continue;
                    long delta = checked(after - before);
                    string databaseCurrency = CurrencyName(currency);
                    if (walletVersions.TryGetValue((uid, currency), out long version))
                    {
                        using NpgsqlCommand update = CreateCommand(
                            "UPDATE game.character_wallets SET balance=$3,updated_at=clock_timestamp(),row_version=row_version+1 " +
                            "WHERE character_id=(SELECT character_id FROM game.characters WHERE legacy_uid=$1) " +
                            "AND currency_type=$2 AND row_version=$4");
                        update.Parameters.AddWithValue(checked((int)uid.Value));
                        update.Parameters.AddWithValue(databaseCurrency);
                        update.Parameters.AddWithValue(after);
                        update.Parameters.AddWithValue(version);
                        RequireOne(update.ExecuteNonQuery(), "The wallet optimistic version was stale.");
                        walletVersions[(uid, currency)] = checked(version + 1);
                    }
                    else
                    {
                        using NpgsqlCommand insert = CreateCommand(
                            "INSERT INTO game.character_wallets(character_id,currency_type,balance) " +
                            "SELECT character_id,$2,$3 FROM game.characters WHERE legacy_uid=$1");
                        insert.Parameters.AddWithValue(checked((int)uid.Value));
                        insert.Parameters.AddWithValue(databaseCurrency);
                        insert.Parameters.AddWithValue(after);
                        RequireOne(insert.ExecuteNonQuery(), "The wallet could not be inserted.");
                        walletVersions[(uid, currency)] = 1;
                    }
                    using NpgsqlCommand ledger = CreateCommand(
                        "INSERT INTO game.currency_ledger(operation_id,character_id,currency_type,delta,balance_after,reason,correlation_id) " +
                        "SELECT $1,character_id,$2,$3,$4,$5,$1 FROM game.characters WHERE legacy_uid=$6");
                    ledger.Parameters.AddWithValue(receipt.CommandId.Value);
                    ledger.Parameters.AddWithValue(databaseCurrency);
                    ledger.Parameters.AddWithValue(delta);
                    ledger.Parameters.AddWithValue(after);
                    ledger.Parameters.AddWithValue(LedgerReason(receipt.Fingerprint));
                    ledger.Parameters.AddWithValue(checked((int)uid.Value));
                    RequireOne(ledger.ExecuteNonQuery(), "The currency ledger entry could not be inserted.");
                }
                originalCharacters[uid] = current;
            }
        }

        private void PlaceItem(ItemInstance item)
        {
            using (NpgsqlCommand removeWarehouse = CreateCommand("DELETE FROM game.warehouse_items WHERE item_instance_id=$1"))
            {
                removeWarehouse.Parameters.AddWithValue(item.Id.Value);
                _ = removeWarehouse.ExecuteNonQuery();
            }
            using (NpgsqlCommand removeLocation = CreateCommand("DELETE FROM game.item_locations WHERE item_instance_id=$1"))
            {
                removeLocation.Parameters.AddWithValue(item.Id.Value);
                _ = removeLocation.ExecuteNonQuery();
            }

            if (item.Location.Container == ItemContainer.Warehouse)
            {
                (Guid vaultId, int capacity) = RequireSingleWarehouse(item.CharacterUid);
                int warehouseSlot = item.Location.Slot;
                if (warehouseSlot >= capacity) throw new InvalidDataException("The warehouse slot is outside the reviewed vault capacity.");
                using NpgsqlCommand insert = CreateCommand(
                    "INSERT INTO game.warehouse_items(vault_id,owner_character_id,slot,item_instance_id) " +
                    "SELECT $1,character_id,$2,$3 FROM game.characters WHERE legacy_uid=$4");
                insert.Parameters.AddWithValue(vaultId);
                insert.Parameters.AddWithValue((short)warehouseSlot);
                insert.Parameters.AddWithValue(item.Id.Value);
                insert.Parameters.AddWithValue(checked((int)item.CharacterUid.Value));
                RequireOne(insert.ExecuteNonQuery(), "The warehouse placement could not be inserted.");
                return;
            }

            string kind = item.Location.Container switch
            {
                ItemContainer.Inventory => "inventory",
                ItemContainer.Equipment => "equipment",
                _ => throw new InvalidDataException("The item location is not supported by PostgreSQL persistence."),
            };
            int slot = item.Location.Slot;
            if (item.Location.Container == ItemContainer.Inventory)
            {
                CharacterGameplayState owner = FindCharacter(item.CharacterUid) ??
                    throw new InvalidOperationException("The item owner does not exist.");
                if (slot >= owner.InventoryCapacity)
                    throw new InvalidDataException("The inventory slot is outside the reviewed capacity.");
            }
            using NpgsqlCommand location = CreateCommand(
                "INSERT INTO game.item_locations(item_instance_id,owner_character_id,location_kind,slot) " +
                "SELECT $1,character_id,$2,$3 FROM game.characters WHERE legacy_uid=$4");
            location.Parameters.AddWithValue(item.Id.Value);
            location.Parameters.AddWithValue(kind);
            location.Parameters.AddWithValue((short)slot);
            location.Parameters.AddWithValue(checked((int)item.CharacterUid.Value));
            RequireOne(location.ExecuteNonQuery(), "The item placement could not be inserted.");
        }

        private (Guid VaultId, int Capacity) RequireSingleWarehouse(CharacterUid characterUid)
        {
            using NpgsqlCommand command = CreateCommand(
                "SELECT vault_id,slot_capacity FROM game.warehouse_vaults " +
                "WHERE owner_character_id=(SELECT character_id FROM game.characters WHERE legacy_uid=$1) " +
                "ORDER BY vault_id LIMIT 2 FOR UPDATE");
            command.Parameters.AddWithValue(checked((int)characterUid.Value));
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (!reader.Read()) throw new InvalidOperationException("The character has no reviewed warehouse vault.");
            Guid id = reader.GetGuid(0);
            int capacity = reader.GetInt16(1);
            if (reader.Read()) throw new InvalidOperationException("The warehouse operation is ambiguous across multiple vaults.");
            return (id, capacity);
        }

        private Guid ResolveDefinitionSource(ItemDefinitionId definitionId)
        {
            using NpgsqlCommand active = CreateCommand(
                "SELECT e.source_id FROM gameplay_reference.item_type_release_entries e " +
                "JOIN gameplay_reference.catalog_releases r USING(release_id) " +
                "WHERE r.release_state='active' AND e.item_type=$1 ORDER BY e.source_id LIMIT 2");
            active.Parameters.AddWithValue((long)definitionId.Value);
            Guid[] activeRows = ReadGuidRows(active);
            if (activeRows.Length == 1) return activeRows[0];
            if (activeRows.Length > 1) throw new InvalidDataException("The active catalog duplicates an item definition.");
            using NpgsqlCommand fallback = CreateCommand(
                "SELECT source_id FROM gameplay_reference.item_types WHERE item_type=$1 ORDER BY source_id LIMIT 2");
            fallback.Parameters.AddWithValue((long)definitionId.Value);
            Guid[] rows = ReadGuidRows(fallback);
            return rows.Length == 1 ? rows[0] :
                throw new InvalidDataException("The item definition is missing or ambiguous.");
        }

        private ItemInstance CacheItem(NpgsqlDataReader reader)
        {
            var id = new ItemInstanceId(reader.GetGuid(0));
            ItemLocation location;
            if (!reader.IsDBNull(18))
            {
                string kind = reader.GetString(18);
                ushort slot = checked((ushort)reader.GetInt16(19));
                location = kind switch
                {
                    "inventory" => ItemLocation.Inventory(slot),
                    "equipment" => ItemLocation.Equipped((EquipmentSlot)checked((byte)slot)),
                    _ => throw new InvalidDataException("The item has an unsupported active placement."),
                };
            }
            else if (!reader.IsDBNull(20)) location = ItemLocation.Warehouse(checked((ushort)reader.GetInt16(20)));
            else throw new InvalidDataException("The item has no durable placement.");

            var item = new ItemInstance(id,
                new LegacyItemUid(checked((uint)reader.GetInt32(1))),
                new ItemDefinitionId(checked((uint)reader.GetInt64(3))),
                new AccountId(reader.GetGuid(15)),
                new CharacterUid(checked((uint)reader.GetInt32(16))),
                location,
                checked((ushort)reader.GetInt32(5)),
                checked((byte)reader.GetInt16(7)), checked((byte)reader.GetInt16(8)),
                checked((byte)reader.GetInt32(9)), checked((byte)reader.GetInt32(10)), checked((byte)reader.GetInt32(11)),
                checked((ushort)reader.GetInt32(4)), checked((byte)reader.GetInt32(12)), reader.GetBoolean(13));
            items[id] = item;
            itemSources[id] = reader.GetGuid(2);
            itemVersions[id] = reader.GetInt64(14);
            return item;
        }

        private NpgsqlCommand CreateCommand(string sql)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new NpgsqlCommand(sql, connection, transaction);
        }

        private static Guid[] ReadGuidRows(NpgsqlCommand command)
        {
            var rows = new List<Guid>(2);
            using NpgsqlDataReader reader = command.ExecuteReader();
            while (reader.Read()) rows.Add(reader.GetGuid(0));
            return rows.ToArray();
        }

        private static void RequireOne(int affected, string message)
        {
            if (affected != 1) throw new DBConcurrencyException(message);
        }

        private static CurrencyKind ParseCurrency(string value) => value switch
        {
            "silver" => CurrencyKind.Silver,
            "conquer-points" => CurrencyKind.ConquerPoints,
            "bound-conquer-points" => CurrencyKind.BoundConquerPoints,
            _ => throw new InvalidDataException("A wallet currency is not supported."),
        };

        private static string CurrencyName(CurrencyKind value) => value switch
        {
            CurrencyKind.Silver => "silver",
            CurrencyKind.ConquerPoints => "conquer-points",
            CurrencyKind.BoundConquerPoints => "bound-conquer-points",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

        private static string LedgerReason(string fingerprint) =>
            fingerprint.StartsWith("shop-buy|", StringComparison.Ordinal) ? "shop-buy" :
            fingerprint.StartsWith("shop-sell|", StringComparison.Ordinal) ? "shop-sell" :
            "gameplay-adjustment";

        private const string ItemSelectSql =
            "SELECT i.item_instance_id,i.legacy_item_uid,i.item_source_id,i.item_type,i.stack_count,i.durability,i.maximum_durability," +
            "i.plus,i.bless,i.enchant,i.socket_one,i.socket_two,i.color,i.bound,i.row_version,c.account_id,c.legacy_uid," +
            "c.character_id,l.location_kind,l.slot,w.slot " +
            "FROM game.item_instances i JOIN game.characters c ON c.character_id=i.owner_character_id " +
            "LEFT JOIN game.item_locations l ON l.item_instance_id=i.item_instance_id " +
            "LEFT JOIN game.warehouse_items w ON w.item_instance_id=i.item_instance_id";
    }
}
