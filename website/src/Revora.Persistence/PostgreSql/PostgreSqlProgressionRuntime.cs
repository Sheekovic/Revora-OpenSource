using System.Data;
using Npgsql;
using Revora.Domain.Accounts;
using Revora.Gameplay.Models;

namespace Revora.Persistence.PostgreSql;

public enum ProgressionPurchaseKind : byte
{
    MaximumLevel = 1,
    FirstRebirth = 2,
    SecondRebirth = 3,
    ClassLoadout = 4,
}

public sealed record ProgressionCharacterSnapshot(
    Guid AccountId, Guid CharacterId, uint LegacyUid, ushort Mesh, ushort Profession, byte Level,
    long Experience, ushort Strength, ushort Agility, ushort Vitality, ushort Spirit,
    int AvailableAttributePoints, byte RebirthCount, ushort OriginalProfession,
    ushort FirstRebirthProfession, ushort SecondRebirthProfession, long Silver, long ConquerPoints,
    int CurrentHealth, int CurrentMana, int MaximumHealth, int MaximumMana, long CharacterVersion);

public sealed record ProgressionMutationResult(
    bool Succeeded, bool Replayed, string Category, ProgressionCharacterSnapshot Character,
    IReadOnlyList<uint> CreatedItemUids, long ChargedConquerPoints);

public sealed record GmInventoryClearResult(
    bool Succeeded, bool Replayed, string Category, IReadOnlyList<uint> RemovedItemUids);

/// <summary>
/// Atomic PostgreSQL boundary for the reviewed RevoraMaster services. It owns no
/// socket or client state and accepts only immutable authenticated identities.
/// </summary>
public sealed class PostgreSqlProgressionRuntime : IAsyncDisposable
{
    public const byte MaximumLevel = 130;
    public const long ServiceCost = 1_000_000;
    public const long MaximumSafeSilver = 2_000_000_000;
    public const long MaximumSafeConquerPoints = 2_000_000_000;
    private readonly NpgsqlDataSource dataSource;

    public PostgreSqlProgressionRuntime(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConnectionStringBuilder.ApplicationName = "Revora.ProgressionRuntime";
        builder.ConnectionStringBuilder.Timeout = 5;
        builder.ConnectionStringBuilder.CommandTimeout = 10;
        builder.ConnectionStringBuilder.MaxPoolSize = 10;
        dataSource = builder.Build();
    }

    public async Task<bool> IsEnabledAsync(AccountId accountId, CharacterUid characterUid,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM gameplay.feature_gates g " +
            "JOIN gameplay.feature_rollouts r USING(feature_key) " +
            "WHERE g.feature_key='progression.services' AND g.gate_state='enabled' AND (" +
            "r.rollout_scope='global' OR (r.rollout_scope='canary' AND EXISTS(" +
            "SELECT 1 FROM gameplay.feature_canaries f JOIN game.characters c ON c.character_id=f.character_id " +
            "WHERE f.feature_key=g.feature_key AND f.account_id=$1 AND f.legacy_character_uid=$2 " +
            "AND c.account_id=$1 AND c.legacy_uid=$2) OR EXISTS(" +
            "SELECT 1 FROM gameplay.account_feature_canaries f JOIN game.characters c ON c.account_id=f.account_id " +
            "WHERE f.feature_key=g.feature_key AND f.account_id=$1 AND c.account_id=$1 AND c.legacy_uid=$2))))");
        command.Parameters.AddWithValue(accountId.Value);
        command.Parameters.AddWithValue(checked((int)characterUid.Value));
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<ProgressionCharacterSnapshot?> ReadAsync(AccountId accountId, CharacterUid characterUid,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(SnapshotSql);
        command.Parameters.AddWithValue(accountId.Value);
        command.Parameters.AddWithValue(checked((int)characterUid.Value));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSnapshot(reader) : null;
    }

    public async Task<ProgressionMutationResult> PurchaseAsync(Guid operationId, AccountId accountId,
        CharacterUid characterUid, ProgressionPurchaseKind kind, ushort? targetProfession,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await PurchaseOnceAsync(operationId, accountId, characterUid, kind, targetProfession,
                    cancellationToken);
            }
            catch (PostgresException exception) when (attempt < 2 &&
                exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
            {
                await Task.Yield();
            }
        }
    }

    private async Task<ProgressionMutationResult> PurchaseOnceAsync(Guid operationId, AccountId accountId,
        CharacterUid characterUid, ProgressionPurchaseKind kind, ushort? targetProfession,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
        if (targetProfession is not null && !SupportedProfession(kind, targetProfession.Value))
            throw new ArgumentOutOfRangeException(nameof(targetProfession));

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // The authenticated character row is locked before any validation or mutation. Under
        // READ COMMITTED, a waiter then observes the preceding committed receipt/state instead
        // of retaining a stale SERIALIZABLE snapshot; this yields a strict per-character order.
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await EnsureOwnedRowsAsync(connection, transaction, accountId, characterUid, cancellationToken);
        ProgressionCharacterSnapshot before = await ReadLockedAsync(connection, transaction, accountId,
            characterUid, cancellationToken);
        if (!await IsEnabledAsync(connection, transaction, accountId, characterUid, cancellationToken))
            return await RejectAsync(connection, transaction, operationId, before, kind, targetProfession,
                "service-unavailable", cancellationToken);

        string serviceKey = ServiceKey(kind);
        await using (var replay = new NpgsqlCommand(
            "SELECT result_category,charged_conquer_points,created_item_count FROM game.progression_service_receipts " +
            "WHERE operation_id=$1", connection, transaction))
        {
            replay.Parameters.AddWithValue(operationId);
            await using NpgsqlDataReader reader = await replay.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                string category = reader.GetString(0);
                long charged = reader.GetInt64(1);
                await reader.DisposeAsync();
                ProgressionCharacterSnapshot current = await ReadLockedAsync(connection, transaction, accountId,
                    characterUid, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(category is "committed" or "ninja-promotion-reconciled" or "maximum-rank-reconciled",
                    true, category, current, [], charged);
            }
        }

        // Reconcile characters who reached the cap before rank and weapon mastery
        // were integrated. This never charges or grants level-skip points again.
        if (kind == ProgressionPurchaseKind.MaximumLevel && before.Level >= MaximumLevel &&
            await NeedsMaximumRankReconciliationAsync(connection, transaction, before, cancellationToken))
        {
            ushort target = MaximumRankTarget(before.Profession) ??
                throw new InvalidDataException("The maximum-level class cannot be reconciled.");
            if (target == 55 && before.Profession < 55)
                await InsertNinjaPromotionReceiptsAsync(connection, transaction, before, cancellationToken);
            await PromoteAndMasterLoadoutWeaponsAsync(connection, transaction, before.CharacterId,
                target, cancellationToken);
            await InsertReceiptAsync(connection, transaction, operationId, before, serviceKey, null,
                "maximum-rank-reconciled", 0, 0, cancellationToken);
            await InsertAuditAsync(connection, transaction, operationId, before, "service-purchase",
                "maximum-rank-reconciled", cancellationToken);
            ProgressionCharacterSnapshot reconciled = await ReadLockedAsync(connection, transaction, accountId,
                characterUid, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, false, "maximum-rank-reconciled", reconciled, [], 0);
        }

        string? invalid = await ValidateAsync(connection, transaction, before, kind, targetProfession,
            cancellationToken);
        if (invalid is not null)
            return await RejectAsync(connection, transaction, operationId, before, kind, targetProfession,
                invalid, cancellationToken);
        if (before.ConquerPoints < ServiceCost)
            return await RejectAsync(connection, transaction, operationId, before, kind, targetProfession,
                "insufficient-conquer-points", cancellationToken);

        var created = new List<uint>();
        switch (kind)
        {
            case ProgressionPurchaseKind.MaximumLevel:
                await ApplyMaximumLevelAsync(connection, transaction, before, cancellationToken);
                break;
            case ProgressionPurchaseKind.FirstRebirth:
            case ProgressionPurchaseKind.SecondRebirth:
                await ApplyRebirthAsync(connection, transaction, before, kind, targetProfession!.Value,
                    cancellationToken);
                break;
            case ProgressionPurchaseKind.ClassLoadout:
                created.AddRange(await ApplyLoadoutAsync(connection, transaction, before,
                    targetProfession!.Value, cancellationToken));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        await SetBalanceAsync(connection, transaction, before.CharacterId, "conquer-points",
            before.ConquerPoints - ServiceCost, operationId, -ServiceCost, "progression-service", cancellationToken);
        await InsertReceiptAsync(connection, transaction, operationId, before, serviceKey, targetProfession,
            "committed", ServiceCost, created.Count, cancellationToken);
        await InsertAuditAsync(connection, transaction, operationId, before, "service-purchase", "committed",
            cancellationToken);
        ProgressionCharacterSnapshot after = await ReadLockedAsync(connection, transaction, accountId,
            characterUid, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, false, "committed", after, created, ServiceCost);
    }

    public async Task<ProgressionMutationResult> SetGmCapsAsync(Guid operationId, AccountId accountId,
        CharacterUid characterUid, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return await SetGmCapsOnceAsync(operationId, accountId, characterUid, cancellationToken); }
            catch (PostgresException exception) when (attempt < 2 &&
                exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
            {
                await Task.Yield();
            }
        }
    }

    private async Task<ProgressionMutationResult> SetGmCapsOnceAsync(Guid operationId, AccountId accountId,
        CharacterUid characterUid, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await EnsureOwnedRowsAsync(connection, transaction, accountId, characterUid, cancellationToken);
        ProgressionCharacterSnapshot before = await ReadLockedAsync(connection, transaction, accountId,
            characterUid, cancellationToken);
        await using (var role = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM game.gameplay_role_assignments WHERE account_id=$1 AND character_id=$2 " +
            "AND role='gm' AND revoked_at IS NULL)", connection, transaction))
        {
            role.Parameters.AddWithValue(accountId.Value);
            role.Parameters.AddWithValue(before.CharacterId);
            if (!Convert.ToBoolean(await role.ExecuteScalarAsync(cancellationToken)))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(false, false, "not-authorized", before, [], 0);
            }
        }

        await SetBalanceAsync(connection, transaction, before.CharacterId, "silver", MaximumSafeSilver,
            operationId, MaximumSafeSilver - before.Silver, "gm-cap", cancellationToken);
        await SetBalanceAsync(connection, transaction, before.CharacterId, "conquer-points",
            MaximumSafeConquerPoints, operationId, MaximumSafeConquerPoints - before.ConquerPoints,
            "gm-cap", cancellationToken);
        await InsertAuditAsync(connection, transaction, operationId, before, "gm-cap", "committed",
            cancellationToken);
        ProgressionCharacterSnapshot after = await ReadLockedAsync(connection, transaction, accountId,
            characterUid, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, false, "committed", after, [], 0);
    }

    public async Task<GmInventoryClearResult> ClearGmInventoryAsync(Guid operationId, AccountId accountId,
        CharacterUid characterUid, CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(operationId));
        for (int attempt = 0; ; attempt++)
        {
            try { return await ClearGmInventoryOnceAsync(operationId, accountId, characterUid, cancellationToken); }
            catch (PostgresException exception) when (attempt < 2 && exception.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or
                PostgresErrorCodes.UniqueViolation)
            { await Task.Yield(); }
        }
    }

    private async Task<GmInventoryClearResult> ClearGmInventoryOnceAsync(Guid operationId, AccountId accountId,
        CharacterUid characterUid, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await using (var replay = new NpgsqlCommand(
            "SELECT removed_item_uids,safe_category FROM game.gm_inventory_clear_receipts " +
            "WHERE operation_id=$1 AND account_id=$2 AND legacy_character_uid=$3", connection, transaction))
        {
            replay.Parameters.AddWithValue(operationId); replay.Parameters.AddWithValue(accountId.Value);
            replay.Parameters.AddWithValue(checked((int)characterUid.Value));
            await using NpgsqlDataReader reader = await replay.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                uint[] prior = reader.GetFieldValue<int[]>(0).Select(checkedUid => checked((uint)checkedUid)).ToArray();
                string replayCategory = reader.GetString(1);
                await reader.DisposeAsync(); await transaction.CommitAsync(cancellationToken);
                return new(true, true, replayCategory, prior);
            }
        }

        Guid characterId;
        await using (var owner = new NpgsqlCommand(
            "SELECT c.character_id,EXISTS(SELECT 1 FROM game.gameplay_role_assignments r " +
            "WHERE r.account_id=c.account_id AND r.character_id=c.character_id AND r.role='gm' AND r.revoked_at IS NULL) " +
            "FROM game.characters c WHERE c.account_id=$1 AND c.legacy_uid=$2 FOR UPDATE OF c", connection, transaction))
        {
            owner.Parameters.AddWithValue(accountId.Value); owner.Parameters.AddWithValue(checked((int)characterUid.Value));
            await using NpgsqlDataReader reader = await owner.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidDataException("The authenticated character is missing.");
            characterId = reader.GetGuid(0);
            if (!reader.GetBoolean(1))
            {
                await reader.DisposeAsync(); await transaction.RollbackAsync(cancellationToken);
                return new(false, false, "not-authorized", []);
            }
        }

        int[] removed;
        await using (var discard = new NpgsqlCommand(
            "WITH selected AS (SELECT l.item_instance_id,l.slot,i.legacy_item_uid " +
            "FROM game.item_locations l JOIN game.item_instances i USING(item_instance_id,owner_character_id) " +
            "WHERE l.owner_character_id=$1 AND l.location_kind='inventory' ORDER BY l.slot FOR UPDATE OF l,i), " +
            "moved AS (UPDATE game.item_locations l SET location_kind='gm-discarded',placed_at=clock_timestamp()," +
            "row_version=l.row_version+1 FROM selected s WHERE l.item_instance_id=s.item_instance_id " +
            "RETURNING s.slot,s.legacy_item_uid) SELECT legacy_item_uid FROM moved ORDER BY slot",
            connection, transaction))
        {
            discard.Parameters.AddWithValue(characterId);
            var values = new List<int>();
            await using NpgsqlDataReader reader = await discard.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) values.Add(reader.GetInt32(0));
            removed = values.ToArray();
        }
        string category = removed.Length == 0 ? "empty-inventory" : "committed";
        await using (var receipt = new NpgsqlCommand(
            "INSERT INTO game.gm_inventory_clear_receipts(operation_id,account_id,character_id,legacy_character_uid," +
            "removed_item_uids,removed_count,safe_category) VALUES($1,$2,$3,$4,$5,$6,$7)", connection, transaction))
        {
            receipt.Parameters.AddWithValue(operationId); receipt.Parameters.AddWithValue(accountId.Value);
            receipt.Parameters.AddWithValue(characterId); receipt.Parameters.AddWithValue(checked((int)characterUid.Value));
            receipt.Parameters.AddWithValue(removed); receipt.Parameters.AddWithValue(removed.Length);
            receipt.Parameters.AddWithValue(category); await receipt.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(true, false, category, removed.Select(value => checked((uint)value)).ToArray());
    }

    private static async Task<string?> ValidateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ProgressionCharacterSnapshot character, ProgressionPurchaseKind kind, ushort? targetProfession,
        CancellationToken token)
    {
        switch (kind)
        {
            case ProgressionPurchaseKind.MaximumLevel:
                return character.Level >= MaximumLevel ? "already-maximum-level" : null;
            case ProgressionPurchaseKind.FirstRebirth:
                if (character.RebirthCount != 0) return "invalid-rebirth-order";
                if (character.Level < 120 && !(character.Profession == 135 && character.Level >= 110))
                    return "rebirth-level-required";
                break;
            case ProgressionPurchaseKind.SecondRebirth:
                if (character.RebirthCount != 1) return "invalid-rebirth-order";
                if (character.Level < 120 && !(character.Profession == 135 && character.Level >= 110))
                    return "rebirth-level-required";
                break;
            case ProgressionPurchaseKind.ClassLoadout:
                if (targetProfession is null) return "unsupported-class";
                await using (var count = new NpgsqlCommand(
                    "SELECT count(*) FROM gameplay_reference.class_loadout_items i " +
                    "JOIN gameplay_reference.class_loadouts l USING(loadout_key) " +
                    "WHERE l.target_profession=$1 AND l.enabled", connection, transaction))
                {
                    count.Parameters.AddWithValue(checked((short)targetProfession.Value));
                    int itemCount = Convert.ToInt32(await count.ExecuteScalarAsync(token));
                    if (itemCount is < 6 or > 8) return "unsupported-loadout";
                    if (await FreeInventorySlotsAsync(connection, transaction, character.CharacterId, token) < itemCount)
                        return "inventory-full";
                }
                break;
        }
        if (kind is ProgressionPurchaseKind.FirstRebirth or ProgressionPurchaseKind.SecondRebirth)
        {
            if (targetProfession is null || !SupportedRebirthProfession(targetProfession.Value)) return "unsupported-class";
            int incompatible = await CountIncompatibleEquipmentAsync(connection, transaction, character.CharacterId,
                targetProfession.Value, token);
            if (await FreeInventorySlotsAsync(connection, transaction, character.CharacterId, token) < incompatible)
                return "inventory-full";
        }
        return null;
    }

    private static async Task ApplyMaximumLevelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ProgressionCharacterSnapshot before, CancellationToken token)
    {
        (int strength, int agility, int vitality, int spirit) = await BaseStatsAsync(connection, transaction,
            Family(before.Profession), 120, token);
        int earned = Math.Max(0, MaximumLevel - 120) * 3;
        ushort? resultingProfession = MaximumRankTarget(before.Profession);
        await UpdateProgressionValuesAsync(connection, transaction, before.CharacterId, MaximumLevel, 0,
            strength, agility, vitality, spirit, checked(before.AvailableAttributePoints + earned), token,
            resultingProfession);
        if (resultingProfession == 55 && before.Profession < 55)
            await InsertNinjaPromotionReceiptsAsync(connection, transaction, before, token);
        if (resultingProfession is not null)
            await MasterLoadoutWeaponsAsync(connection, transaction, before.CharacterId,
                resultingProfession.Value, token);
    }

    private static async Task<bool> NeedsMaximumRankReconciliationAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ProgressionCharacterSnapshot before, CancellationToken token)
    {
        ushort? target = MaximumRankTarget(before.Profession);
        if (target is null) return false;
        if (before.Profession != target.Value) return true;
        ushort[] expected = LoadoutWeaponProficiencies(target.Value).ToArray();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM game.character_proficiencies WHERE character_id=$1 " +
            "AND proficiency_type=ANY($2) AND level=20 AND experience=0", connection, transaction);
        command.Parameters.AddWithValue(before.CharacterId);
        command.Parameters.AddWithValue(expected.Select(value => checked((int)value)).ToArray());
        return Convert.ToInt32(await command.ExecuteScalarAsync(token)) != expected.Length;
    }

    private static async Task PromoteNinjaToMasterAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ProgressionCharacterSnapshot before, CancellationToken token)
    {
        await InsertNinjaPromotionReceiptsAsync(connection, transaction, before, token);
        await using var command = new NpgsqlCommand(
            "UPDATE game.characters SET profession=55,updated_at=clock_timestamp(),row_version=row_version+1 " +
            "WHERE character_id=$1 AND profession=$2", connection, transaction);
        command.Parameters.AddWithValue(before.CharacterId);
        command.Parameters.AddWithValue(checked((short)before.Profession));
        if (await command.ExecuteNonQueryAsync(token) != 1)
            throw new InvalidDataException("The Ninja promotion reconciliation failed.");
    }

    private static async Task InsertNinjaPromotionReceiptsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ProgressionCharacterSnapshot before, CancellationToken token)
    {
        for (short profession = checked((short)(before.Profession + 1)); profession <= 55; profession++)
        {
            await using var receipt = new NpgsqlCommand(
                "INSERT INTO game.ninja_promotion_receipts(operation_id,character_id,account_id," +
                "legacy_character_uid,previous_profession,resulting_profession,character_level) " +
                "VALUES($1,$2,$3,$4,$5,$6,$7)", connection, transaction);
            receipt.Parameters.AddWithValue(Guid.NewGuid());
            receipt.Parameters.AddWithValue(before.CharacterId);
            receipt.Parameters.AddWithValue(before.AccountId);
            receipt.Parameters.AddWithValue(checked((int)before.LegacyUid));
            receipt.Parameters.AddWithValue(checked((short)(profession - 1)));
            receipt.Parameters.AddWithValue(profession);
            receipt.Parameters.AddWithValue(checked((short)MaximumLevel));
            await receipt.ExecuteNonQueryAsync(token);
        }
    }

    private static async Task ApplyRebirthAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ProgressionCharacterSnapshot before, ProgressionPurchaseKind kind, ushort targetProfession,
        CancellationToken token)
    {
        await MoveIncompatibleEquipmentAsync(connection, transaction, before.CharacterId, targetProfession, token);
        int rebirth = kind == ProgressionPurchaseKind.FirstRebirth ? 1 : 2;
        int extra = before.Level >= 120
            ? checked((before.Level - 120) * 3 + rebirth * 10 + 45)
            : rebirth * 10;
        (int strength, int agility, int vitality, int spirit) = await BaseStatsAsync(connection, transaction,
            Family(targetProfession), 15, token);
        int points = kind == ProgressionPurchaseKind.FirstRebirth
            ? extra
            : checked(before.AvailableAttributePoints + extra);
        await UpdateProgressionValuesAsync(connection, transaction, before.CharacterId, 15, 0,
            strength, agility, vitality, spirit, points, token, targetProfession);
        await using var progression = new NpgsqlCommand(
            "UPDATE game.character_progression SET rebirth_count=$2,original_profession=CASE WHEN $2=1 THEN $3 ELSE original_profession END," +
            "first_rebirth_profession=CASE WHEN $2=1 THEN $4 ELSE first_rebirth_profession END," +
            "second_rebirth_profession=CASE WHEN $2=2 THEN $4 ELSE second_rebirth_profession END," +
            "previous_profession=$3,updated_at=clock_timestamp(),row_version=row_version+1 WHERE character_id=$1",
            connection, transaction);
        progression.Parameters.AddWithValue(before.CharacterId);
        progression.Parameters.AddWithValue((short)rebirth);
        progression.Parameters.AddWithValue(checked((short)before.Profession));
        progression.Parameters.AddWithValue(checked((short)targetProfession));
        if (await progression.ExecuteNonQueryAsync(token) != 1)
            throw new InvalidDataException("The rebirth lineage update failed.");
    }

    private static async Task<IReadOnlyList<uint>> ApplyLoadoutAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ProgressionCharacterSnapshot before, ushort targetProfession,
        CancellationToken token)
    {
        var rows = new List<(Guid Source, long Type, int Durability, short Plus, short Bless, short Enchant,
            short SocketOne, short SocketTwo, short Color)>();
        await using (var command = new NpgsqlCommand(
            "SELECT e.source_id,i.item_type,t.maximum_durability,i.plus,i.bless,i.enchant," +
            "i.socket_one,i.socket_two,i.color FROM gameplay_reference.class_loadout_items i " +
            "JOIN gameplay_reference.class_loadouts l USING(loadout_key) " +
            "JOIN gameplay_reference.catalog_releases r ON r.release_state='active' " +
            "JOIN gameplay_reference.item_type_release_entries e ON e.release_id=r.release_id AND e.item_type=i.item_type " +
            "JOIN gameplay_reference.item_types t ON t.source_id=e.source_id AND t.item_type=e.item_type " +
            "WHERE l.target_profession=$1 AND l.enabled ORDER BY i.ordinal", connection, transaction))
        {
            command.Parameters.AddWithValue(checked((short)targetProfession));
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                rows.Add((reader.GetGuid(0), reader.GetInt64(1), reader.GetInt32(2), reader.GetInt16(3),
                    reader.GetInt16(4), reader.GetInt16(5), reader.GetInt16(6), reader.GetInt16(7), reader.GetInt16(8)));
        }
        int[] slots = await ReserveInventorySlotsAsync(connection, transaction, before.CharacterId, rows.Count, token);
        var result = new List<uint>(rows.Count);
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            Guid itemId = Guid.NewGuid();
            await using var item = new NpgsqlCommand(
                "INSERT INTO game.item_instances(item_instance_id,owner_character_id,item_source_id,item_type,stack_count," +
                "durability,maximum_durability,plus,bless,enchant,socket_one,socket_two,color,bound) " +
                "VALUES($1,$2,$3,$4,1,$5,$5,$6,$7,$8,$9,$10,$11,true) RETURNING legacy_item_uid",
                connection, transaction);
            item.Parameters.AddWithValue(itemId);
            item.Parameters.AddWithValue(before.CharacterId);
            item.Parameters.AddWithValue(row.Source);
            item.Parameters.AddWithValue(row.Type);
            item.Parameters.AddWithValue(row.Durability);
            item.Parameters.AddWithValue(row.Plus);
            item.Parameters.AddWithValue(row.Bless);
            item.Parameters.AddWithValue(row.Enchant);
            item.Parameters.AddWithValue(row.SocketOne);
            item.Parameters.AddWithValue(row.SocketTwo);
            item.Parameters.AddWithValue(row.Color);
            uint legacyUid = checked((uint)Convert.ToInt32(await item.ExecuteScalarAsync(token)));
            await using var location = new NpgsqlCommand(
                "INSERT INTO game.item_locations(item_instance_id,owner_character_id,location_kind,slot) " +
                "VALUES($1,$2,'inventory',$3)", connection, transaction);
            location.Parameters.AddWithValue(itemId);
            location.Parameters.AddWithValue(before.CharacterId);
            location.Parameters.AddWithValue(checked((short)slots[index]));
            await location.ExecuteNonQueryAsync(token);
            result.Add(legacyUid);
        }
        await PromoteAndMasterLoadoutWeaponsAsync(connection, transaction, before.CharacterId,
            targetProfession, token);
        return result;
    }

    private static async Task PromoteAndMasterLoadoutWeaponsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid characterId, ushort targetProfession, CancellationToken token)
    {
        ushort highestProfession = targetProfession switch
        {
            11 or 15 => 15,
            21 or 25 => 25,
            41 or 45 => 45,
            55 => 55,
            132 or 135 => 135,
            142 or 145 => 145,
            _ => throw new InvalidDataException("The selected loadout has no reviewed highest-rank profession."),
        };
        await using (var promotion = new NpgsqlCommand(
            "UPDATE game.characters SET profession=$2,updated_at=clock_timestamp(),row_version=row_version+1 " +
            "WHERE character_id=$1", connection, transaction))
        {
            promotion.Parameters.AddWithValue(characterId);
            promotion.Parameters.AddWithValue(checked((short)highestProfession));
            if (await promotion.ExecuteNonQueryAsync(token) != 1)
                throw new InvalidDataException("The class-loadout promotion failed.");
        }

        await MasterLoadoutWeaponsAsync(connection, transaction, characterId, targetProfession, token);
    }

    private static async Task MasterLoadoutWeaponsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid characterId, ushort targetProfession, CancellationToken token)
    {
        foreach (ushort proficiencyType in LoadoutWeaponProficiencies(targetProfession))
        {
            await using var proficiency = new NpgsqlCommand(
                "INSERT INTO game.character_proficiencies(character_id,proficiency_type,level,experience) " +
                "VALUES($1,$2,20,0) ON CONFLICT(character_id,proficiency_type) DO UPDATE SET " +
                "level=20,experience=0,row_version=game.character_proficiencies.row_version+1",
                connection, transaction);
            proficiency.Parameters.AddWithValue(characterId);
            proficiency.Parameters.AddWithValue(checked((int)proficiencyType));
            await proficiency.ExecuteNonQueryAsync(token);
        }
    }

    public static IReadOnlyList<ushort> LoadoutWeaponProficiencies(ushort targetProfession) => targetProfession switch
    {
        11 or 15 => [410, 420],
        21 or 25 => [480],
        41 or 45 => [500],
        55 => [601],
        132 or 135 or 142 or 145 => [421],
        _ => throw new ArgumentOutOfRangeException(nameof(targetProfession)),
    };

    public static ushort? MaximumRankTarget(ushort profession) => profession switch
    {
        >= 10 and <= 15 => 15,
        >= 20 and <= 25 => 25,
        >= 40 and <= 45 => 45,
        >= 50 and <= 55 => 55,
        >= 130 and <= 135 => 135,
        >= 140 and <= 145 => 145,
        _ => null,
    };

    private static async Task EnsureOwnedRowsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        AccountId accountId, CharacterUid characterUid, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "WITH owner AS (SELECT character_id FROM game.characters WHERE account_id=$1 AND legacy_uid=$2), " +
            "attributes AS (INSERT INTO game.character_attributes(character_id) SELECT character_id FROM owner ON CONFLICT DO NOTHING), " +
            "vitals AS (INSERT INTO game.character_vitals(character_id) SELECT character_id FROM owner ON CONFLICT DO NOTHING), " +
            "progression AS (INSERT INTO game.character_progression(character_id) SELECT character_id FROM owner ON CONFLICT DO NOTHING) " +
            "INSERT INTO game.character_wallets(character_id,currency_type,balance) " +
            "SELECT character_id,v.currency,0 FROM owner CROSS JOIN (VALUES('silver'),('conquer-points')) v(currency) " +
            "ON CONFLICT DO NOTHING", connection, transaction);
        command.Parameters.AddWithValue(accountId.Value);
        command.Parameters.AddWithValue(checked((int)characterUid.Value));
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<ProgressionCharacterSnapshot> ReadLockedAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, AccountId accountId, CharacterUid characterUid, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(SnapshotSql + " FOR UPDATE OF c,a,p", connection, transaction);
        command.Parameters.AddWithValue(accountId.Value);
        command.Parameters.AddWithValue(checked((int)characterUid.Value));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new InvalidDataException("The authenticated character was not found.");
        return ReadSnapshot(reader);
    }

    private static ProgressionCharacterSnapshot ReadSnapshot(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), checked((uint)reader.GetInt32(2)),
        checked((ushort)reader.GetInt32(3)), checked((ushort)reader.GetInt16(4)),
        checked((byte)reader.GetInt16(5)), reader.GetInt64(6), checked((ushort)reader.GetInt32(7)),
        checked((ushort)reader.GetInt32(8)), checked((ushort)reader.GetInt32(9)), checked((ushort)reader.GetInt32(10)),
        reader.GetInt32(11), checked((byte)reader.GetInt16(12)), checked((ushort)reader.GetInt16(13)),
        checked((ushort)reader.GetInt16(14)), checked((ushort)reader.GetInt16(15)), reader.GetInt64(16),
        reader.GetInt64(17), reader.GetInt32(18), reader.GetInt32(19), reader.GetInt32(20),
        reader.GetInt32(21), reader.GetInt64(22));

    private static async Task<bool> IsEnabledAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        AccountId accountId, CharacterUid characterUid, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM gameplay.feature_gates g JOIN gameplay.feature_rollouts r USING(feature_key) " +
            "WHERE g.feature_key='progression.services' AND g.gate_state='enabled' AND (r.rollout_scope='global' OR " +
            "(r.rollout_scope='canary' AND EXISTS(SELECT 1 FROM gameplay.feature_canaries f " +
            "WHERE f.feature_key=g.feature_key AND f.account_id=$1 AND f.legacy_character_uid=$2)) OR EXISTS(" +
            "SELECT 1 FROM gameplay.account_feature_canaries f JOIN game.characters c ON c.account_id=f.account_id " +
            "WHERE f.feature_key=g.feature_key AND f.account_id=$1 AND c.account_id=$1 AND c.legacy_uid=$2)))",
            connection, transaction);
        command.Parameters.AddWithValue(accountId.Value);
        command.Parameters.AddWithValue(checked((int)characterUid.Value));
        return Convert.ToBoolean(await command.ExecuteScalarAsync(token));
    }

    private static async Task<ProgressionMutationResult> RejectAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid operationId, ProgressionCharacterSnapshot character,
        ProgressionPurchaseKind kind, ushort? targetProfession, string category, CancellationToken token)
    {
        await InsertReceiptAsync(connection, transaction, operationId, character, ServiceKey(kind), targetProfession,
            category, 0, 0, token);
        await transaction.CommitAsync(token);
        return new(false, false, category, character, [], 0);
    }

    private static async Task InsertReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid operationId, ProgressionCharacterSnapshot character, string serviceKey, ushort? targetProfession,
        string category, long charged, int created, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO game.progression_service_receipts(operation_id,account_id,character_id,service_key," +
            "target_profession,result_category,charged_conquer_points,created_item_count) VALUES($1,$2,$3,$4,$5,$6,$7,$8)",
            connection, transaction);
        command.Parameters.AddWithValue(operationId);
        command.Parameters.AddWithValue(character.AccountId);
        command.Parameters.AddWithValue(character.CharacterId);
        command.Parameters.AddWithValue(serviceKey);
        command.Parameters.AddWithValue(targetProfession is null ? DBNull.Value : checked((short)targetProfession.Value));
        command.Parameters.AddWithValue(category);
        command.Parameters.AddWithValue(charged);
        command.Parameters.AddWithValue(checked((short)created));
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task InsertAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid operationId, ProgressionCharacterSnapshot character, string eventType, string category,
        CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO operations.progression_service_audit(operation_id,account_id,character_id," +
            "legacy_character_uid,event_type,safe_category) VALUES($1,$2,$3,$4,$5,$6)", connection, transaction);
        command.Parameters.AddWithValue(operationId);
        command.Parameters.AddWithValue(character.AccountId);
        command.Parameters.AddWithValue(character.CharacterId);
        command.Parameters.AddWithValue(checked((int)character.LegacyUid));
        command.Parameters.AddWithValue(eventType);
        command.Parameters.AddWithValue(category);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task SetBalanceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid characterId, string currency, long balance, Guid operationId, long delta, string reason,
        CancellationToken token)
    {
        await using (var wallet = new NpgsqlCommand(
            "UPDATE game.character_wallets SET balance=$3,updated_at=clock_timestamp(),row_version=row_version+1 " +
            "WHERE character_id=$1 AND currency_type=$2", connection, transaction))
        {
            wallet.Parameters.AddWithValue(characterId);
            wallet.Parameters.AddWithValue(currency);
            wallet.Parameters.AddWithValue(balance);
            if (await wallet.ExecuteNonQueryAsync(token) != 1) throw new InvalidDataException("The wallet update failed.");
        }
        if (delta == 0) return;
        await using var ledger = new NpgsqlCommand(
            "INSERT INTO game.currency_ledger(operation_id,character_id,currency_type,delta,balance_after,reason,correlation_id) " +
            "VALUES($1,$2,$3,$4,$5,$6,$1)", connection, transaction);
        ledger.Parameters.AddWithValue(operationId);
        ledger.Parameters.AddWithValue(characterId);
        ledger.Parameters.AddWithValue(currency);
        ledger.Parameters.AddWithValue(delta);
        ledger.Parameters.AddWithValue(balance);
        ledger.Parameters.AddWithValue(reason);
        await ledger.ExecuteNonQueryAsync(token);
    }

    private static async Task UpdateProgressionValuesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid characterId, int level, long experience, int strength, int agility, int vitality, int spirit,
        int availablePoints, CancellationToken token, ushort? profession = null)
    {
        await using (var character = new NpgsqlCommand(
            "UPDATE game.characters SET level=$2,profession=coalesce($3,profession),updated_at=clock_timestamp()," +
            "row_version=row_version+1 WHERE character_id=$1", connection, transaction))
        {
            character.Parameters.AddWithValue(characterId);
            character.Parameters.AddWithValue(checked((short)level));
            character.Parameters.AddWithValue(profession is null ? DBNull.Value : checked((short)profession.Value));
            await character.ExecuteNonQueryAsync(token);
        }
        await using (var attributes = new NpgsqlCommand(
            "UPDATE game.character_attributes SET strength=$2,agility=$3,vitality=$4,spirit=$5," +
            "available_attribute_points=$6,experience=$7,updated_at=clock_timestamp(),row_version=row_version+1 " +
            "WHERE character_id=$1", connection, transaction))
        {
            attributes.Parameters.AddWithValue(characterId);
            attributes.Parameters.AddWithValue(strength);
            attributes.Parameters.AddWithValue(agility);
            attributes.Parameters.AddWithValue(vitality);
            attributes.Parameters.AddWithValue(spirit);
            attributes.Parameters.AddWithValue(availablePoints);
            attributes.Parameters.AddWithValue(experience);
            await attributes.ExecuteNonQueryAsync(token);
        }
        int maximumHealth = checked(vitality * 24 + strength * 3 + agility * 3 + spirit * 3);
        int maximumMana = checked(spirit * ((profession ?? 0) is >= 130 and <= 145 ? 15 : 5));
        await using var vitals = new NpgsqlCommand(
            "UPDATE game.character_vitals SET current_health=$2,current_mana=$3,updated_at=clock_timestamp()," +
            "row_version=row_version+1 WHERE character_id=$1", connection, transaction);
        vitals.Parameters.AddWithValue(characterId);
        vitals.Parameters.AddWithValue(Math.Max(1, maximumHealth));
        vitals.Parameters.AddWithValue(maximumMana);
        await vitals.ExecuteNonQueryAsync(token);
    }

    private static async Task<(int Strength, int Agility, int Vitality, int Spirit)> BaseStatsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string family, int level, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT b.strength,b.agility,b.vitality,b.spirit FROM gameplay_reference.base_character_stats b " +
            "JOIN gameplay_reference.import_sources s USING(source_id) WHERE b.profession_family=$1 AND b.level=$2 " +
            "ORDER BY s.imported_at DESC,b.source_id DESC LIMIT 1", connection, transaction);
        command.Parameters.AddWithValue(family);
        command.Parameters.AddWithValue(checked((short)level));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new InvalidDataException("The canonical base stat row is missing.");
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
    }

    private static async Task<int> CountIncompatibleEquipmentAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid characterId, ushort targetProfession, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM game.item_locations l JOIN game.item_instances i USING(item_instance_id,owner_character_id) " +
            "JOIN gameplay_reference.item_types t ON t.source_id=i.item_source_id AND t.item_type=i.item_type " +
            "WHERE l.owner_character_id=$1 AND l.location_kind='equipment' AND t.item_class<>0 " +
            "AND t.item_class<>ALL($2)", connection, transaction);
        command.Parameters.AddWithValue(characterId);
        command.Parameters.AddWithValue(CompatibleItemClasses(targetProfession));
        return Convert.ToInt32(await command.ExecuteScalarAsync(token));
    }

    private static async Task MoveIncompatibleEquipmentAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid characterId, ushort targetProfession, CancellationToken token)
    {
        var itemIds = new List<Guid>();
        await using (var command = new NpgsqlCommand(
            "SELECT l.item_instance_id FROM game.item_locations l JOIN game.item_instances i USING(item_instance_id,owner_character_id) " +
            "JOIN gameplay_reference.item_types t ON t.source_id=i.item_source_id AND t.item_type=i.item_type " +
            "WHERE l.owner_character_id=$1 AND l.location_kind='equipment' AND t.item_class<>0 " +
            "AND t.item_class<>ALL($2) ORDER BY l.slot FOR UPDATE OF l", connection, transaction))
        {
            command.Parameters.AddWithValue(characterId);
            command.Parameters.AddWithValue(CompatibleItemClasses(targetProfession));
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) itemIds.Add(reader.GetGuid(0));
        }
        int[] slots = await ReserveInventorySlotsAsync(connection, transaction, characterId, itemIds.Count, token);
        for (int index = 0; index < itemIds.Count; index++)
        {
            await using var update = new NpgsqlCommand(
                "UPDATE game.item_locations SET location_kind='inventory',slot=$2,placed_at=clock_timestamp()," +
                "row_version=row_version+1 WHERE item_instance_id=$1 AND owner_character_id=$3",
                connection, transaction);
            update.Parameters.AddWithValue(itemIds[index]);
            update.Parameters.AddWithValue(checked((short)slots[index]));
            update.Parameters.AddWithValue(characterId);
            await update.ExecuteNonQueryAsync(token);
        }
    }

    private static async Task<int> FreeInventorySlotsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid characterId, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT p.slot_capacity-count(l.item_instance_id) FROM game.inventory_profiles p " +
            "LEFT JOIN game.item_locations l ON l.owner_character_id=p.character_id AND l.location_kind='inventory' " +
            "WHERE p.character_id=$1 GROUP BY p.slot_capacity", connection, transaction);
        command.Parameters.AddWithValue(characterId);
        object? value = await command.ExecuteScalarAsync(token);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static async Task<int[]> ReserveInventorySlotsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid characterId, int count, CancellationToken token)
    {
        if (count == 0) return [];
        await using var command = new NpgsqlCommand(
            "WITH capacity AS (SELECT slot_capacity FROM game.inventory_profiles WHERE character_id=$1 FOR UPDATE), " +
            "used AS (SELECT slot FROM game.item_locations WHERE owner_character_id=$1 AND location_kind='inventory') " +
            "SELECT candidate.slot FROM capacity CROSS JOIN LATERAL " +
            "generate_series(0,capacity.slot_capacity-1) AS candidate(slot) " +
            "WHERE NOT EXISTS(SELECT 1 FROM used WHERE used.slot=candidate.slot) " +
            "ORDER BY candidate.slot LIMIT $2",
            connection, transaction);
        command.Parameters.AddWithValue(characterId);
        command.Parameters.AddWithValue(count);
        var slots = new List<int>(count);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) slots.Add(reader.GetInt32(0));
        if (slots.Count != count) throw new InvalidDataException("The inventory reservation was incomplete.");
        return slots.ToArray();
    }

    private static bool SupportedProfession(ProgressionPurchaseKind kind, ushort profession) =>
        kind == ProgressionPurchaseKind.ClassLoadout
            ? profession is 11 or 21 or 41 or 55 or 132 or 142
            : SupportedRebirthProfession(profession);
    private static bool SupportedRebirthProfession(ushort profession) => profession is 11 or 21 or 41 or 132 or 142;
    private static string ServiceKey(ProgressionPurchaseKind kind) => kind switch
    {
        ProgressionPurchaseKind.MaximumLevel => "maximum-level",
        ProgressionPurchaseKind.FirstRebirth => "first-rebirth",
        ProgressionPurchaseKind.SecondRebirth => "second-rebirth",
        ProgressionPurchaseKind.ClassLoadout => "class-loadout",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    private static string Family(ushort profession) => profession switch
    {
        >= 10 and <= 19 => "trojan",
        >= 20 and <= 29 => "warrior",
        >= 40 and <= 49 => "archer",
        >= 50 and <= 59 => "ninja",
        >= 100 and <= 145 => "taoist",
        _ => throw new InvalidDataException("The profession has no reviewed progression family."),
    };
    private static short[] CompatibleItemClasses(ushort profession) => profession switch
    {
        11 => [11, 15], 21 => [21, 25], 41 => [40, 45], 55 => [50], 132 or 142 => [190],
        _ => throw new InvalidDataException("The selected profession has no reviewed item-class mapping."),
    };

    private const string SnapshotSql =
        "SELECT c.account_id,c.character_id,c.legacy_uid,c.mesh,c.profession,c.level," +
        "a.experience,a.strength,a.agility,a.vitality,a.spirit,a.available_attribute_points," +
        "p.rebirth_count,p.original_profession,p.first_rebirth_profession,p.second_rebirth_profession," +
        "coalesce((SELECT balance FROM game.character_wallets WHERE character_id=c.character_id AND currency_type='silver'),0)," +
        "coalesce((SELECT balance FROM game.character_wallets WHERE character_id=c.character_id AND currency_type='conquer-points'),0)," +
        "v.current_health,greatest(0,v.current_mana),s.maximum_health,s.maximum_mana,c.row_version FROM game.characters c " +
        "JOIN game.character_attributes a USING(character_id) JOIN game.character_progression p USING(character_id) " +
        "JOIN game.character_vitals v USING(character_id) JOIN game.character_stat_snapshots s USING(character_id) " +
        "WHERE c.account_id=$1 AND c.legacy_uid=$2";

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
