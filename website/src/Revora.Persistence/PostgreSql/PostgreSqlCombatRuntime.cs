using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Revora.Domain.Accounts;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;
using Revora.Gameplay.Services;

namespace Revora.Persistence.PostgreSql;

/// <summary>
/// PostgreSQL exactly-once boundary for the disabled live-combat runtime.
/// Player HP and its receipt commit together; monster HP is session-owned and
/// only its replay receipt is durable.
/// </summary>
public sealed class PostgreSqlCombatRuntime : ILiveCombatPersistence, INinjaCombatPersistence,
    IMonsterCombatPersistence, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;

    public PostgreSqlCombatRuntime(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConnectionStringBuilder.ApplicationName = "Revora.CombatRuntime";
        builder.ConnectionStringBuilder.Timeout = 5;
        builder.ConnectionStringBuilder.CommandTimeout = 10;
        builder.ConnectionStringBuilder.MaxPoolSize = 20;
        dataSource = builder.Build();
    }

    public async Task<bool> IsEnabledAsync(AccountId accountId, CharacterUid characterUid,
        string featureKey, CancellationToken cancellationToken)
    {
        if (featureKey is not ("combat.runtime" or "ninja.native"))
            throw new ArgumentOutOfRangeException(nameof(featureKey));
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "WITH RECURSIVE required(feature_key) AS (VALUES($3::text) UNION " +
            "SELECT d.depends_on_feature_key FROM gameplay.feature_gate_dependencies d JOIN required q ON q.feature_key=d.feature_key), " +
            "evaluated AS (SELECT q.feature_key,g.gate_state,r.rollout_scope,g.required_migration,g.required_release_id, " +
            "(EXISTS(SELECT 1 FROM gameplay.feature_canaries f JOIN game.characters c ON c.character_id=f.character_id " +
            "WHERE f.feature_key=q.feature_key AND f.account_id=$1 AND f.legacy_character_uid=$2 " +
            "AND c.account_id=$1 AND c.legacy_uid=$2) OR " +
            "EXISTS(SELECT 1 FROM gameplay.account_feature_canaries f JOIN game.characters c ON c.account_id=f.account_id " +
            "WHERE f.feature_key=q.feature_key AND f.account_id=$1 AND c.account_id=$1 AND c.legacy_uid=$2)) AS is_canary, " +
            "EXISTS(SELECT 1 FROM gameplay_reference.catalog_releases cr WHERE cr.release_id=g.required_release_id " +
            "AND cr.release_state='active') AS release_active FROM required q " +
            "LEFT JOIN gameplay.feature_gates g USING(feature_key) LEFT JOIN gameplay.feature_rollouts r USING(feature_key)) " +
            "SELECT count(*)>0 AND bool_and(gate_state='enabled' AND required_migration<=" +
            "(SELECT coalesce(max(version),0) FROM operations.schema_migrations) AND " +
            "(required_release_id IS NULL OR release_active) AND " +
            "(rollout_scope='global' OR rollout_scope='canary' AND is_canary)) FROM evaluated");
        command.Parameters.AddWithValue(accountId.Value);
        command.Parameters.AddWithValue(checked((int)characterUid.Value));
        command.Parameters.AddWithValue(featureKey);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<ushort?> LoadStaminaAsync(GameplayActor actor, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT a.stamina FROM game.character_attributes a JOIN game.characters c USING(character_id) " +
            "WHERE c.account_id=$1 AND c.legacy_uid=$2");
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is short stamina ? checked((ushort)stamina) : null;
    }

    public async Task<bool> TryUpdateStaminaAsync(GameplayActor actor, ushort expected, ushort current,
        CancellationToken cancellationToken)
    {
        if (expected > 100 || current > 100) throw new ArgumentOutOfRangeException(nameof(current));
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "UPDATE game.character_attributes a SET stamina=$3,row_version=a.row_version+1," +
            "updated_at=clock_timestamp() FROM game.characters c WHERE c.character_id=a.character_id " +
            "AND c.account_id=$1 AND c.legacy_uid=$2 AND a.stamina=$4");
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        command.Parameters.AddWithValue(checked((short)current));
        command.Parameters.AddWithValue(checked((short)expected));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async ValueTask<RecoveryItemPersistenceResult> ConsumeRecoveryItemAsync(
        RecoveryItemMutation mutation, CancellationToken cancellationToken)
    {
        if (mutation.OperationId == Guid.Empty || mutation.LegacyItemUid == 0 || mutation.ItemType == 0 ||
            mutation.HealthRecovery < 0 || mutation.ManaRecovery < 0 ||
            mutation.HealthRecovery == 0 && mutation.ManaRecovery == 0)
            throw new ArgumentOutOfRangeException(nameof(mutation));
        string fingerprint = FormattableString.Invariant(
            $"recovery-item:{mutation.Actor.CharacterUid.Value}:{mutation.LegacyItemUid}:{mutation.ItemType}");

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
                await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);

                await using (var receipt = new NpgsqlCommand(
                    "SELECT fingerprint FROM game.gameplay_command_receipts WHERE command_id=$1",
                    connection, transaction))
                {
                    receipt.Parameters.AddWithValue(mutation.OperationId);
                    object? existing = await receipt.ExecuteScalarAsync(cancellationToken);
                    if (existing is string saved)
                    {
                        RecoveryItemPersistenceResult replay = await ReadRecoveryVitalsAsync(connection, transaction,
                            mutation.Actor, saved == fingerprint ? RecoveryItemPersistenceCode.Replay :
                                RecoveryItemPersistenceCode.Conflict, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        return replay;
                    }
                }

                Guid characterId;
                int currentHealth;
                int currentMana;
                long vitalsVersion;
                int maximumHealth;
                int maximumMana;
                await using (var character = new NpgsqlCommand(
                    "SELECT c.character_id,v.current_health,v.current_mana,v.row_version," +
                    "s.maximum_health,s.maximum_mana FROM game.characters c " +
                    "JOIN game.character_vitals v USING(character_id) " +
                    "JOIN game.character_stat_snapshots s USING(character_id) " +
                    "WHERE c.account_id=$1 AND c.legacy_uid=$2 FOR UPDATE OF c,v",
                    connection, transaction))
                {
                    character.Parameters.AddWithValue(mutation.Actor.AccountId.Value);
                    character.Parameters.AddWithValue(checked((int)mutation.Actor.CharacterUid.Value));
                    await using NpgsqlDataReader reader = await character.ExecuteReaderAsync(cancellationToken);
                    if (!await reader.ReadAsync(cancellationToken))
                    {
                        await reader.DisposeAsync();
                        await transaction.RollbackAsync(cancellationToken);
                        return new(RecoveryItemPersistenceCode.Conflict, 0, 0, 0);
                    }
                    characterId = reader.GetGuid(0);
                    currentHealth = reader.GetInt32(1);
                    currentMana = reader.GetInt32(2);
                    vitalsVersion = reader.GetInt64(3);
                    maximumHealth = reader.GetInt32(4);
                    maximumMana = reader.GetInt32(5);
                }
                if (currentHealth == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new(RecoveryItemPersistenceCode.CharacterDead, currentHealth, currentMana, vitalsVersion);
                }
                if (currentHealth != mutation.PreviousHealth || vitalsVersion != mutation.ExpectedPersistenceVersion)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new(RecoveryItemPersistenceCode.Conflict, currentHealth, currentMana, vitalsVersion);
                }

                Guid itemInstanceId;
                await using (var item = new NpgsqlCommand(
                    "SELECT i.item_instance_id,i.item_type,l.location_kind " +
                    "FROM game.item_instances i JOIN game.item_locations l USING(item_instance_id) " +
                    "WHERE i.owner_character_id=$1 AND i.legacy_item_uid=$2 FOR UPDATE OF i,l",
                    connection, transaction))
                {
                    item.Parameters.AddWithValue(characterId);
                    item.Parameters.AddWithValue(checked((int)mutation.LegacyItemUid));
                    await using NpgsqlDataReader reader = await item.ExecuteReaderAsync(cancellationToken);
                    if (!await reader.ReadAsync(cancellationToken))
                    {
                        await reader.DisposeAsync();
                        await transaction.RollbackAsync(cancellationToken);
                        return new(RecoveryItemPersistenceCode.ItemNotFound, currentHealth, currentMana, vitalsVersion);
                    }
                    itemInstanceId = reader.GetGuid(0);
                    long itemType = reader.GetInt64(1);
                    if (itemType != mutation.ItemType || !string.Equals(reader.GetString(2), "inventory",
                            StringComparison.Ordinal))
                    {
                        await reader.DisposeAsync();
                        await transaction.RollbackAsync(cancellationToken);
                        return new(RecoveryItemPersistenceCode.InvalidItem, currentHealth, currentMana, vitalsVersion);
                    }
                }

                int resultingHealth = Math.Min(maximumHealth, checked(currentHealth + mutation.HealthRecovery));
                int resultingMana = Math.Min(maximumMana, checked(currentMana + mutation.ManaRecovery));
                await using (var update = new NpgsqlCommand(
                    "UPDATE game.character_vitals SET current_health=$2,current_mana=$3," +
                    "updated_at=clock_timestamp(),row_version=row_version+1 " +
                    "WHERE character_id=$1 AND row_version=$4 RETURNING row_version", connection, transaction))
                {
                    update.Parameters.AddWithValue(characterId);
                    update.Parameters.AddWithValue(resultingHealth);
                    update.Parameters.AddWithValue(resultingMana);
                    update.Parameters.AddWithValue(vitalsVersion);
                    object? version = await update.ExecuteScalarAsync(cancellationToken);
                    if (version is not long committedVersion)
                        throw new InvalidOperationException("The recovery-item vitals update lost its optimistic version.");
                    vitalsVersion = committedVersion;
                }
                await using (var remove = new NpgsqlCommand(
                    "DELETE FROM game.item_locations WHERE item_instance_id=$1 AND owner_character_id=$2",
                    connection, transaction))
                {
                    remove.Parameters.AddWithValue(itemInstanceId);
                    remove.Parameters.AddWithValue(characterId);
                    if (await remove.ExecuteNonQueryAsync(cancellationToken) != 1)
                        throw new InvalidOperationException("The recovery item location changed during consumption.");
                }
                await using (var save = new NpgsqlCommand(
                    "INSERT INTO game.gameplay_command_receipts(command_id,fingerprint,result_code) VALUES($1,$2,$3)",
                    connection, transaction))
                {
                    save.Parameters.AddWithValue(mutation.OperationId);
                    save.Parameters.AddWithValue(fingerprint);
                    save.Parameters.AddWithValue((int)GameplayResultCode.Success);
                    if (await save.ExecuteNonQueryAsync(cancellationToken) != 1)
                        throw new InvalidOperationException("The recovery-item receipt was not saved.");
                }
                await using (var consumed = new NpgsqlCommand(
                    "INSERT INTO game.consumed_item_instances(item_instance_id,owner_character_id,operation_id) " +
                    "VALUES($1,$2,$3)", connection, transaction))
                {
                    consumed.Parameters.AddWithValue(itemInstanceId);
                    consumed.Parameters.AddWithValue(characterId);
                    consumed.Parameters.AddWithValue(mutation.OperationId);
                    if (await consumed.ExecuteNonQueryAsync(cancellationToken) != 1)
                        throw new InvalidOperationException("The recovery-item tombstone was not saved.");
                }
                await transaction.CommitAsync(cancellationToken);
                return new(RecoveryItemPersistenceCode.Applied, resultingHealth, resultingMana, vitalsVersion);
            }
            catch (PostgresException exception) when (attempt < 3 &&
                exception.SqlState is "40001" or "40P01" or "23505")
            {
                // Re-open a new serializable transaction; a duplicate command resolves through its receipt.
            }
        }
    }

    private static async Task<RecoveryItemPersistenceResult> ReadRecoveryVitalsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, GameplayActor actor, RecoveryItemPersistenceCode code,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT v.current_health,v.current_mana,v.row_version FROM game.characters c " +
            "JOIN game.character_vitals v USING(character_id) WHERE c.account_id=$1 AND c.legacy_uid=$2",
            connection, transaction);
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(code, reader.GetInt32(0), reader.GetInt32(1), reader.GetInt64(2))
            : new(RecoveryItemPersistenceCode.Conflict, 0, 0, 0);
    }

    public async Task<(int MaximumHealth, ushort Stamina, long PersistenceVersion)?> RevivePlayerAsync(GameplayActor actor,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await using var select = new NpgsqlCommand(
            "SELECT c.character_id,s.maximum_health FROM game.characters c JOIN game.character_vitals v USING(character_id) " +
            "JOIN game.character_stat_snapshots s USING(character_id) " +
            "WHERE c.account_id=$1 AND c.legacy_uid=$2 AND v.current_health=0 FOR UPDATE OF c,v", connection, transaction);
        select.Parameters.AddWithValue(actor.AccountId.Value);
        select.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        Guid characterId = reader.GetGuid(0);
        int maximumHealth = reader.GetInt32(1);
        await reader.DisposeAsync();
        long persistenceVersion;
        await using (var vitals = new NpgsqlCommand(
            "UPDATE game.character_vitals v SET current_health=s.maximum_health,updated_at=clock_timestamp(),row_version=v.row_version+1 " +
            "FROM game.character_stat_snapshots s WHERE v.character_id=$1 AND s.character_id=v.character_id " +
            "RETURNING v.row_version",
            connection, transaction))
        {
            vitals.Parameters.AddWithValue(characterId);
            persistenceVersion = Convert.ToInt64(await vitals.ExecuteScalarAsync(cancellationToken));
        }
        await using (var stamina = new NpgsqlCommand(
            "UPDATE game.character_attributes SET stamina=100,updated_at=clock_timestamp(),row_version=row_version+1 WHERE character_id=$1",
            connection, transaction))
        {
            stamina.Parameters.AddWithValue(characterId);
            await stamina.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return (maximumHealth, 100, persistenceVersion);
    }

    public async ValueTask<NinjaPromotionResult> PromoteNinjaAsync(Guid operationId,
        GameplayActor actor, byte requestedProfession, CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(operationId));
        for (int attempt = 0; ; attempt++)
        {
            try { return await PromoteNinjaOnceAsync(operationId, actor, requestedProfession, cancellationToken); }
            catch (PostgresException error) when (attempt < 2 && error.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or
                PostgresErrorCodes.UniqueViolation)
            { await Task.Yield(); }
        }
    }

    private async Task<NinjaPromotionResult> PromoteNinjaOnceAsync(Guid operationId,
        GameplayActor actor, byte requestedProfession, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await using (var replay = new NpgsqlCommand(
            "SELECT previous_profession,resulting_profession,character_level FROM game.ninja_promotion_receipts r " +
            "JOIN game.characters c USING(character_id) WHERE r.operation_id=$1 AND r.account_id=$2 " +
            "AND r.legacy_character_uid=$3", connection, transaction))
        {
            replay.Parameters.AddWithValue(operationId); replay.Parameters.AddWithValue(actor.AccountId.Value);
            replay.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
            await using NpgsqlDataReader reader = await replay.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                byte before = checked((byte)reader.GetInt16(0));
                byte after = checked((byte)reader.GetInt16(1));
                byte level = checked((byte)reader.GetInt16(2));
                await reader.DisposeAsync(); await transaction.CommitAsync(cancellationToken);
                return new(after == requestedProfession ? NinjaPromotionResultCode.Replay :
                    NinjaPromotionResultCode.Conflict, before, after, level);
            }
        }

        Guid characterId;
        byte current, levelValue;
        await using (var character = new NpgsqlCommand(
            "SELECT character_id,profession,level FROM game.characters WHERE account_id=$1 AND legacy_uid=$2 FOR UPDATE",
            connection, transaction))
        {
            character.Parameters.AddWithValue(actor.AccountId.Value);
            character.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
            await using NpgsqlDataReader reader = await character.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(NinjaPromotionResultCode.OwnershipMismatch, 0, requestedProfession, 0);
            }
            characterId = reader.GetGuid(0); current = checked((byte)reader.GetInt16(1));
            levelValue = checked((byte)reader.GetInt16(2));
        }
        if (!NinjaProfessionRules.IsNinja(current))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(NinjaPromotionResultCode.WrongProfession, current, requestedProfession, levelValue);
        }
        if (!NinjaProfessionRules.IsNinja(requestedProfession) || requestedProfession != current + 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(NinjaPromotionResultCode.InvalidTransition, current, requestedProfession, levelValue);
        }
        if (!NinjaProfessionRules.CanPromote(current, requestedProfession, levelValue))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(NinjaPromotionResultCode.InsufficientLevel, current, requestedProfession, levelValue);
        }
        await using (var update = new NpgsqlCommand(
            "UPDATE game.characters SET profession=$2,row_version=row_version+1,updated_at=clock_timestamp() " +
            "WHERE character_id=$1 AND profession=$3", connection, transaction))
        {
            update.Parameters.AddWithValue(characterId); update.Parameters.AddWithValue((short)requestedProfession);
            update.Parameters.AddWithValue((short)current);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(NinjaPromotionResultCode.Conflict, current, requestedProfession, levelValue);
            }
        }
        await using (var receipt = new NpgsqlCommand(
            "INSERT INTO game.ninja_promotion_receipts(operation_id,character_id,account_id,legacy_character_uid," +
            "previous_profession,resulting_profession,character_level) VALUES($1,$2,$3,$4,$5,$6,$7)",
            connection, transaction))
        {
            receipt.Parameters.AddWithValue(operationId); receipt.Parameters.AddWithValue(characterId);
            receipt.Parameters.AddWithValue(actor.AccountId.Value);
            receipt.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
            receipt.Parameters.AddWithValue((short)current); receipt.Parameters.AddWithValue((short)requestedProfession);
            receipt.Parameters.AddWithValue((short)levelValue);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(NinjaPromotionResultCode.Applied, current, requestedProfession, levelValue);
    }

    public async Task<LiveCombatEntitySnapshot?> LoadPlayerAsync(GameplayActor actor, int instanceId,
        long monotonicMilliseconds, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT c.legacy_uid,c.map_id,c.x,c.y,v.current_health," +
            "greatest(v.current_health,coalesce(s.maximum_health,v.current_health),1),v.row_version " +
            "FROM game.characters c JOIN game.character_vitals v USING(character_id) " +
            "LEFT JOIN game.character_stat_snapshots s USING(character_id) " +
            "WHERE c.account_id=$1 AND c.legacy_uid=$2");
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        long current = reader.GetInt32(4);
        return new LiveCombatEntitySnapshot(
            checked((uint)reader.GetInt32(0)), LiveEntityKind.Player, actor,
            new CombatMapKey(reader.GetInt32(1), instanceId),
            checked((ushort)reader.GetInt32(2)), checked((ushort)reader.GetInt32(3)),
            current, reader.GetInt32(5), current > 0, reader.GetInt64(6), monotonicMilliseconds,
            PersistenceVersion: reader.GetInt64(6)).Validate();
    }

    public async Task<IReadOnlyList<CombatMonsterSpawn>> LoadEnabledMonsterSpawnsAsync(
        long monotonicMilliseconds, CancellationToken cancellationToken)
    {
        var result = new List<CombatMonsterSpawn>();
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT p.legacy_uid,t.mesh,t.safe_name,t.level,t.maximum_health,p.map_id,p.x,p.y,p.instance_id,p.direction " +
            "FROM gameplay_reference.live_monster_spawn_points p JOIN gameplay_reference.monster_types t " +
            "ON t.source_id=p.monster_source_id AND t.monster_type=p.monster_type WHERE p.enabled " +
            "ORDER BY p.legacy_uid");
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            long health = reader.GetInt64(4);
            var snapshot = new LiveCombatEntitySnapshot(checked((uint)reader.GetInt64(0)),
                LiveEntityKind.Monster, null, new CombatMapKey(reader.GetInt32(5), reader.GetInt32(8)),
                checked((ushort)reader.GetInt32(6)), checked((ushort)reader.GetInt32(7)), health, health,
                true, 1, monotonicMilliseconds).Validate();
            result.Add(new(snapshot, checked((uint)reader.GetInt32(1)), reader.GetString(2),
                checked((ushort)reader.GetInt16(3)), checked((byte)reader.GetInt16(9))));
        }
        return result;
    }

    public async Task<OwnerMonsterCatalog> LoadEnabledOwnerMonsterCatalogAsync(
        CancellationToken cancellationToken)
    {
        var templates = new Dictionary<int, OwnerMonsterTemplate>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT monster_type,safe_name,behavior,mesh,level,maximum_health,defense,magic_defense," +
            "magic_attack,minimum_attack,maximum_attack,damage_reduction,dodge,attack_type,magic_skill," +
            "magic_level,gives_rewards,attack_distance,minimum_silver,maximum_silver,move_delay_ms," +
            "respawn_delay_ms,use_level_difference_damage,classification FROM gameplay_reference.monster_types"))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new OwnerMonsterTemplate(reader.GetInt32(0), reader.GetString(1),
                    (OwnerMonsterBehavior)reader.GetInt16(2), checked((uint)reader.GetInt32(3)),
                    checked((ushort)reader.GetInt16(4)), reader.GetInt64(5), reader.GetInt32(6),
                    reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10),
                    checked((byte)reader.GetInt16(11)), checked((byte)reader.GetInt16(12)),
                    (OwnerMonsterAttackType)reader.GetInt16(13), checked((ushort)reader.GetInt32(14)),
                    checked((byte)reader.GetInt16(15)), reader.GetBoolean(16),
                    checked((byte)reader.GetInt16(17)), reader.GetInt32(18), reader.GetInt32(19),
                    reader.GetInt32(20), reader.GetInt64(21), reader.GetBoolean(22),
                    reader.GetString(23) switch
                    {
                        "normal" => OwnerMonsterClassification.Normal,
                        "guard" => OwnerMonsterClassification.Guard,
                        "boss" => OwnerMonsterClassification.Boss,
                        "event-special" => OwnerMonsterClassification.EventSpecial,
                        "passive" => OwnerMonsterClassification.Passive,
                        _ => throw new InvalidDataException("An owner monster classification is invalid.")
                    }).Validate();
                if (!templates.TryAdd(row.MonsterType, row))
                    throw new InvalidDataException("The owner monster catalog contains a duplicate type.");
            }
        }
        var generators = new List<OwnerMonsterGenerator>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT spawn_region_id,monster_type,map_id,minimum_x,minimum_y,maximum_x,maximum_y," +
            "requested_population,enabled,activation_category FROM gameplay_reference.monster_spawn_regions " +
            "WHERE enabled AND activation_category='normal' ORDER BY spawn_region_id"))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new OwnerMonsterGenerator(reader.GetInt64(0), reader.GetInt32(1),
                    reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5),
                    reader.GetInt32(6), reader.GetInt32(7), reader.GetBoolean(8), reader.GetString(9)).Validate();
                if (!templates.ContainsKey(row.MonsterType))
                    throw new InvalidDataException("An enabled owner monster generator has no template.");
                generators.Add(row);
            }
        }
        var assignments = new List<OwnerMonsterUidAssignment>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT a.spawn_region_id,a.ordinal,a.legacy_uid FROM " +
            "gameplay_reference.monster_runtime_uid_assignments a JOIN " +
            "gameplay_reference.monster_spawn_regions r ON r.source_id=a.source_id AND " +
            "r.spawn_region_id=a.spawn_region_id WHERE r.enabled AND r.activation_category='normal' " +
            "ORDER BY a.spawn_region_id,a.ordinal"))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                assignments.Add(new OwnerMonsterUidAssignment(reader.GetInt64(0), reader.GetInt32(1),
                    checked((uint)reader.GetInt32(2))).Validate());
        }
        if (assignments.Count != generators.Sum(value => value.Population) ||
            assignments.Select(value => value.LegacyUid).Distinct().Count() != assignments.Count)
            throw new InvalidDataException("The exact-client monster UID assignment catalog is incomplete or duplicated.");
        var drops = new List<OwnerMonsterDropRule>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT monster_type,ordinal,item_source_id,item_type,chance_basis_points,plus,sockets,bless " +
            "FROM gameplay_reference.monster_drop_rules ORDER BY monster_type,ordinal"))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            drops.Add(new(reader.GetInt32(0), reader.GetInt16(1), reader.GetGuid(2),
                checked((uint)reader.GetInt64(3)), reader.GetInt32(4), checked((byte)reader.GetInt16(5)),
                checked((byte)reader.GetInt16(6)), checked((byte)reader.GetInt16(7))));
        }
        var equipment = new List<OwnerEquipmentDropCandidate>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT monster_level,item_source_id,item_type,ordinal FROM gameplay_reference.equipment_drop_candidates " +
            "ORDER BY monster_level,ordinal"))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                equipment.Add(new(checked((ushort)reader.GetInt16(0)), reader.GetGuid(1),
                    checked((uint)reader.GetInt64(2)), reader.GetInt16(3)));
        }
        var common = new List<OwnerCommonDropItem>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT source_id,item_type FROM gameplay_reference.item_types WHERE item_type IN (1088000,1088001) " +
            "ORDER BY item_type,source_id"))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                common.Add(new(reader.GetGuid(0), checked((uint)reader.GetInt64(1))));
        }
        if (common.Select(value => value.ItemType).Distinct().Count() != 2)
            throw new InvalidDataException("The owner common-drop catalog is incomplete.");
        return new(templates, generators, assignments, drops, equipment, common);
    }

    public async Task<IReadOnlyDictionary<uint, long>> LoadNextMonsterSpawnGenerationsAsync(
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<uint, long>();
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT monster_legacy_uid,max(spawn_generation) FROM game.monster_death_resolutions " +
            "GROUP BY monster_legacy_uid");
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            uint uid = checked((uint)reader.GetInt64(0));
            long next = checked(reader.GetInt64(1) + 1);
            if (uid == 0 || next <= 1 || !result.TryAdd(uid, next))
                throw new InvalidDataException("The durable monster generation catalog is invalid.");
        }
        return result;
    }

    public async Task<PlayerCombatStatistics?> LoadPlayerCombatStatisticsAsync(GameplayActor actor,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT c.level,a.strength,a.agility,a.spirit,s.minimum_attack,s.maximum_attack,s.defense," +
            "s.magic_attack,s.magic_defense,s.accuracy,s.dodge,coalesce(pk.status,'normal') FROM game.characters c " +
            "JOIN game.character_attributes a USING(character_id) JOIN game.character_stat_snapshots s USING(character_id) " +
            "LEFT JOIN game.character_pk_state pk USING(character_id) " +
            "WHERE c.account_id=$1 AND c.legacy_uid=$2");
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PlayerCombatStatistics(checked((byte)reader.GetInt16(0)), reader.GetInt32(1), reader.GetInt32(2),
                reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7),
                reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10), 0,
                reader.GetString(11) switch
                {
                    "flashing" => PlayerPkStatus.Flashing,
                    "red" => PlayerPkStatus.Red,
                    "black" => PlayerPkStatus.Black,
                    _ => PlayerPkStatus.Normal
                }).Validate()
            : null;
    }

    public async ValueTask<CombatPersistenceResult> CommitMonsterAttackAsync(
        MonsterCombatMutation mutation, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await using (var replay = new NpgsqlCommand(
            "SELECT monster_legacy_uid,damage,resulting_stamina,sitting_interrupted " +
            "FROM game.monster_attack_receipts WHERE operation_id=$1",
            connection, transaction))
        {
            replay.Parameters.AddWithValue(mutation.OperationId);
            await using NpgsqlDataReader reader = await replay.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                bool same = reader.GetInt64(0) == mutation.MonsterUid && reader.GetInt32(1) == mutation.Damage;
                ushort? stamina = reader.IsDBNull(2) ? null : checked((ushort)reader.GetInt16(2));
                bool interrupted = reader.GetBoolean(3);
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);
                return new(same ? CombatPersistenceCode.Replay : CombatPersistenceCode.Conflict,
                    null, same ? stamina : null, same && interrupted);
            }
        }
        Guid characterId;
        await using (var target = new NpgsqlCommand(
            "SELECT c.character_id,v.current_health,v.row_version,c.map_id,a.stamina FROM game.characters c " +
            "JOIN game.character_vitals v USING(character_id) JOIN game.character_attributes a USING(character_id) " +
            "WHERE c.account_id=$1 AND c.legacy_uid=$2 FOR UPDATE OF v,a", connection, transaction))
        {
            target.Parameters.AddWithValue(mutation.Target.AccountId.Value);
            target.Parameters.AddWithValue(checked((int)mutation.Target.CharacterUid.Value));
            await using NpgsqlDataReader reader = await target.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetInt32(1) != mutation.PreviousHealth ||
                reader.GetInt64(2) != mutation.ExpectedTargetVersion || reader.GetInt32(3) != mutation.Map.MapId ||
                mutation.PreviousStamina is ushort expectedStamina && reader.GetInt16(4) != expectedStamina)
            {
                await reader.DisposeAsync();
                await transaction.RollbackAsync(cancellationToken);
                return new(CombatPersistenceCode.Conflict);
            }
            characterId = reader.GetGuid(0);
        }
        if (mutation.PreviousStamina is ushort previousStamina &&
            mutation.RemainingStamina is ushort remainingStamina)
        {
            await using var updateStamina = new NpgsqlCommand(
                "UPDATE game.character_attributes SET stamina=$2,row_version=row_version+1," +
                "updated_at=clock_timestamp() WHERE character_id=$1 AND stamina=$3", connection, transaction);
            updateStamina.Parameters.AddWithValue(characterId);
            updateStamina.Parameters.AddWithValue(checked((short)remainingStamina));
            updateStamina.Parameters.AddWithValue(checked((short)previousStamina));
            if (await updateStamina.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(CombatPersistenceCode.Conflict);
            }
        }
        await using (var update = new NpgsqlCommand(
            "UPDATE game.character_vitals SET current_health=$2,row_version=row_version+1," +
            "updated_at=clock_timestamp() WHERE character_id=$1 AND current_health=$3 AND row_version=$4",
            connection, transaction))
        {
            update.Parameters.AddWithValue(characterId);
            update.Parameters.AddWithValue(checked((int)mutation.RemainingHealth));
            update.Parameters.AddWithValue(checked((int)mutation.PreviousHealth));
            update.Parameters.AddWithValue(mutation.ExpectedTargetVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(CombatPersistenceCode.Conflict);
            }
        }
        await using (var receipt = new NpgsqlCommand(
            "INSERT INTO game.monster_attack_receipts(operation_id,target_character_id,monster_legacy_uid," +
            "monster_type,previous_health,resulting_health,damage,killed,previous_stamina,resulting_stamina," +
            "sitting_interrupted) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)",
            connection, transaction))
        {
            receipt.Parameters.AddWithValue(mutation.OperationId);
            receipt.Parameters.AddWithValue(characterId);
            receipt.Parameters.AddWithValue(checked((long)mutation.MonsterUid));
            receipt.Parameters.AddWithValue(mutation.MonsterType);
            receipt.Parameters.AddWithValue(checked((int)mutation.PreviousHealth));
            receipt.Parameters.AddWithValue(checked((int)mutation.RemainingHealth));
            receipt.Parameters.AddWithValue(checked((int)mutation.Damage));
            receipt.Parameters.AddWithValue(mutation.Killed);
            receipt.Parameters.AddWithValue((object?)mutation.PreviousStamina is ushort previous
                ? checked((short)previous) : DBNull.Value);
            receipt.Parameters.AddWithValue((object?)mutation.RemainingStamina is ushort after
                ? checked((short)after) : DBNull.Value);
            receipt.Parameters.AddWithValue(mutation.SittingInterrupted);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(CombatPersistenceCode.Applied, null, mutation.RemainingStamina,
            mutation.SittingInterrupted);
    }

    public async Task<NinjaCombatant?> LoadNinjaCombatantAsync(GameplayActor actor,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT c.profession,c.level,c.map_id,c.x,c.y,v.current_health,a.stamina," +
            "(SELECT i.item_type FROM game.equipped_items i WHERE i.character_id=c.character_id AND i.slot=4)," +
            "(SELECT i.item_type FROM game.equipped_items i WHERE i.character_id=c.character_id AND i.slot=5) " +
            "FROM game.characters c JOIN game.character_vitals v USING(character_id) " +
            "JOIN game.character_attributes a USING(character_id) WHERE c.account_id=$1 AND c.legacy_uid=$2",
            connection);
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        ushort profession = checked((ushort)reader.GetInt16(0));
        byte level = checked((byte)reader.GetInt16(1));
        var point = new WorldPoint(reader.GetInt32(2), checked((ushort)reader.GetInt32(3)),
            checked((ushort)reader.GetInt32(4)));
        bool alive = reader.GetInt32(5) > 0;
        ushort stamina = checked((ushort)reader.GetInt16(6));
        uint? right = reader.IsDBNull(7) ? null : checked((uint)reader.GetInt64(7));
        uint? left = reader.IsDBNull(8) ? null : checked((uint)reader.GetInt64(8));
        await reader.DisposeAsync();
        var skills = new Dictionary<NinjaSkillId, byte>();
        await using var skillCommand = new NpgsqlCommand(
            "SELECT magic_type,level FROM game.character_skills WHERE character_id=(SELECT character_id " +
            "FROM game.characters WHERE account_id=$1 AND legacy_uid=$2) AND magic_type=ANY($3)", connection);
        skillCommand.Parameters.AddWithValue(actor.AccountId.Value);
        skillCommand.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        skillCommand.Parameters.AddWithValue(NativeNinjaSkillCatalog5165.All.Select(value => (int)value.Id).ToArray());
        await using NpgsqlDataReader skillReader = await skillCommand.ExecuteReaderAsync(cancellationToken);
        while (await skillReader.ReadAsync(cancellationToken))
            skills.Add((NinjaSkillId)skillReader.GetInt32(0), checked((byte)skillReader.GetInt16(1)));
        return new NinjaCombatant(actor.CharacterUid, checked((byte)profession), level, point, alive,
            stamina, right, left, skills);
    }

    public async Task<PersistedSkillState?> ReadSkillAsync(GameplayActor actor, ushort skillId,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT s.experience,s.magic_type,s.level FROM game.character_skills s JOIN game.characters c " +
            "USING(character_id) WHERE c.account_id=$1 AND c.legacy_uid=$2 AND s.magic_type=$3");
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        command.Parameters.AddWithValue((int)skillId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PersistedSkillState(checked((uint)reader.GetInt64(0)), checked((ushort)reader.GetInt32(1)),
                checked((ushort)reader.GetInt16(2)))
            : null;
    }

    public async Task<IReadOnlyList<WeaponProficiencyCommitResult>> LoadProficienciesAsync(
        GameplayActor actor, CancellationToken cancellationToken)
    {
        var values = new List<WeaponProficiencyCommitResult>();
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT p.proficiency_type,p.level,p.experience FROM game.character_proficiencies p " +
            "JOIN game.characters c USING(character_id) WHERE c.account_id=$1 AND c.legacy_uid=$2 " +
            "ORDER BY p.proficiency_type");
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            values.Add(new(checked((ushort)reader.GetInt32(0)), 0,
                checked((byte)reader.GetInt16(1)), checked((uint)reader.GetInt64(2))));
        return values;
    }

    public async ValueTask<CombatPersistenceResult> CommitSkillAsync(NinjaCombatMutation mutation,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return await CommitSkillOnceAsync(mutation, cancellationToken); }
            catch (PostgresException error) when (attempt < 2 && error.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or
                PostgresErrorCodes.UniqueViolation)
            { await Task.Yield(); }
        }
    }

    private async Task<CombatPersistenceResult> CommitSkillOnceAsync(NinjaCombatMutation mutation,
        CancellationToken cancellationToken)
    {
        string fingerprint = SkillFingerprint(mutation);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await using (var replay = new NpgsqlCommand(
            "SELECT request_fingerprint,character_experience_awarded,resulting_character_experience," +
            "resulting_character_level FROM game.combat_operation_receipts WHERE operation_id=$1",
            connection, transaction))
        {
            replay.Parameters.AddWithValue(mutation.OperationId);
            await using NpgsqlDataReader replayReader = await replay.ExecuteReaderAsync(cancellationToken);
            if (await replayReader.ReadAsync(cancellationToken))
            {
                string value = replayReader.GetString(0);
                long progressionAward = replayReader.GetInt64(1);
                long? resultingExperience = replayReader.IsDBNull(2) ? null : replayReader.GetInt64(2);
                byte? resultingLevel = replayReader.IsDBNull(3) ? null :
                    checked((byte)replayReader.GetInt16(3));
                await replayReader.DisposeAsync();
                CombatProgressionCommitResult? replayProgression = resultingExperience.HasValue ?
                    await ReadProgressionResultAsync(connection, transaction, mutation.OperationId,
                        progressionAward, resultingExperience.Value, resultingLevel!.Value,
                        cancellationToken) : null;
                MonsterRewardCommitResult? replayReward = null;
                if (value == fingerprint && mutation.Reward is not null)
                    replayReward = await PostgreSqlGroundDropRuntime.CommitRewardInTransactionAsync(
                        connection, transaction, mutation.Reward, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(value == fingerprint ? CombatPersistenceCode.Replay : CombatPersistenceCode.Conflict,
                    replayReward, Progression: value == fingerprint ? replayProgression : null);
            }
        }
        Guid characterId;
        await using (var actor = new NpgsqlCommand(
            "SELECT c.character_id,c.profession,c.level,c.map_id,c.x,c.y,v.current_health,a.stamina,s.level,s.experience,s.magic_source_id," +
            "(SELECT i.item_type FROM game.equipped_items i WHERE i.character_id=c.character_id AND i.slot=4)," +
            "(SELECT i.item_type FROM game.equipped_items i WHERE i.character_id=c.character_id AND i.slot=5) " +
            "FROM game.characters c JOIN game.character_vitals v USING(character_id) " +
            "JOIN game.character_attributes a USING(character_id) JOIN game.character_skills s ON " +
            "s.character_id=c.character_id AND s.magic_type=$3 WHERE c.account_id=$1 AND c.legacy_uid=$2 " +
            "FOR UPDATE OF c,v,a,s", connection, transaction))
        {
            actor.Parameters.AddWithValue(mutation.Attacker.AccountId.Value);
            actor.Parameters.AddWithValue(checked((int)mutation.Attacker.CharacterUid.Value));
            actor.Parameters.AddWithValue((int)mutation.SkillId);
            await using NpgsqlDataReader reader = await actor.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) ||
                !NinjaProfessionRules.IsNinja(checked((byte)reader.GetInt16(1))) ||
                reader.GetInt32(3) != mutation.Map.MapId || reader.GetInt32(4) != mutation.AttackerX ||
                reader.GetInt32(5) != mutation.AttackerY ||
                reader.GetInt32(6) <= 0 || reader.GetInt16(7) != mutation.PreviousStamina ||
                reader.GetInt16(8) != mutation.SkillLevel)
            {
                await reader.DisposeAsync(); await transaction.RollbackAsync(cancellationToken);
                return new(CombatPersistenceCode.Conflict);
            }
            characterId = reader.GetGuid(0);
            if (NativeNinjaSkillCatalog5165.TryGet(mutation.SkillId, out NinjaSkillDefinition definition) &&
                definition.RequiresDualKatana)
            {
                if (reader.IsDBNull(11) || reader.IsDBNull(12) ||
                    !NinjaItemRules.IsKatana(checked((uint)reader.GetInt64(11))) ||
                    !NinjaItemRules.IsKatana(checked((uint)reader.GetInt64(12))))
                {
                    await reader.DisposeAsync(); await transaction.RollbackAsync(cancellationToken);
                    return new(CombatPersistenceCode.Conflict);
                }
            }
        }
        if (mutation.TargetKind == LiveEntityKind.Player && mutation.Damage > 0)
        {
            await using var target = new NpgsqlCommand(
                "UPDATE game.character_vitals v SET current_health=$2,updated_at=clock_timestamp(),row_version=row_version+1 " +
                "WHERE v.character_id=(SELECT character_id FROM game.characters WHERE legacy_uid=$1 AND map_id=$4 AND x=$5 AND y=$6) " +
                "AND v.current_health=$3 AND v.row_version=$7", connection, transaction);
            target.Parameters.AddWithValue(checked((int)mutation.TargetUid));
            target.Parameters.AddWithValue(checked((int)mutation.RemainingHealth));
            target.Parameters.AddWithValue(checked((int)mutation.PreviousHealth));
            target.Parameters.AddWithValue(mutation.Map.MapId);
            target.Parameters.AddWithValue((int)mutation.TargetX);
            target.Parameters.AddWithValue((int)mutation.TargetY);
            target.Parameters.AddWithValue(mutation.ExpectedTargetVersion);
            if (await target.ExecuteNonQueryAsync(cancellationToken) != 1)
            { await transaction.RollbackAsync(cancellationToken); return new(CombatPersistenceCode.Conflict); }
        }
        await using (var stamina = new NpgsqlCommand(
            "UPDATE game.character_attributes SET stamina=$2,updated_at=clock_timestamp(),row_version=row_version+1 " +
            "WHERE character_id=$1 AND stamina=$3", connection, transaction))
        {
            stamina.Parameters.AddWithValue(characterId);
            stamina.Parameters.AddWithValue(checked((short)mutation.RemainingStamina));
            stamina.Parameters.AddWithValue(checked((short)mutation.PreviousStamina));
            if (await stamina.ExecuteNonQueryAsync(cancellationToken) != 1)
            { await transaction.RollbackAsync(cancellationToken); return new(CombatPersistenceCode.Conflict); }
        }
        await using (var skill = new NpgsqlCommand(
            "UPDATE game.character_skills s SET " +
            "level=CASE WHEN m.required_experience>0 AND s.experience+$3>=m.required_experience " +
            "AND c.level>=m.required_level AND EXISTS(SELECT 1 FROM gameplay_reference.magic_types n " +
            "WHERE n.source_id=s.magic_source_id AND n.magic_type=s.magic_type AND n.level=s.level+1) " +
            "THEN s.level+1 ELSE s.level END, " +
            "experience=CASE WHEN m.required_experience>0 AND s.experience+$3>=m.required_experience " +
            "AND c.level>=m.required_level AND EXISTS(SELECT 1 FROM gameplay_reference.magic_types n " +
            "WHERE n.source_id=s.magic_source_id AND n.magic_type=s.magic_type AND n.level=s.level+1) " +
            "THEN 0 ELSE s.experience+$3 END,row_version=s.row_version+1 " +
            "FROM gameplay_reference.magic_types m,game.characters c WHERE s.character_id=$1 " +
            "AND s.magic_type=$2 AND s.level=$4 AND c.character_id=s.character_id " +
            "AND m.source_id=s.magic_source_id AND m.magic_type=s.magic_type AND m.level=s.level", connection, transaction))
        {
            skill.Parameters.AddWithValue(characterId); skill.Parameters.AddWithValue((int)mutation.SkillId);
            skill.Parameters.AddWithValue(mutation.SkillExperienceAward);
            skill.Parameters.AddWithValue(checked((short)mutation.SkillLevel));
            if (await skill.ExecuteNonQueryAsync(cancellationToken) != 1)
            { await transaction.RollbackAsync(cancellationToken); return new(CombatPersistenceCode.Conflict); }
        }
        MonsterRewardCommitResult? reward = null;
        if (mutation.Reward is MonsterRewardPlan plan)
        {
            if (!mutation.Killed || mutation.TargetKind != LiveEntityKind.Monster ||
                plan.Killer != mutation.Attacker || plan.MonsterUid != mutation.TargetUid ||
                plan.Map != mutation.Map || plan.X != mutation.TargetX || plan.Y != mutation.TargetY)
                throw new InvalidOperationException("A Ninja reward plan is not bound to its authoritative death mutation.");
            reward = await PostgreSqlGroundDropRuntime.CommitRewardInTransactionAsync(
                connection, transaction, plan, cancellationToken);
            if (!reward.Applied)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(CombatPersistenceCode.Conflict);
            }
        }
        CombatProgressionCommitResult? progression = mutation.Progression is null ? null :
            await ApplyCombatProgressionAsync(connection, transaction, characterId,
                mutation.Progression.Validate(), cancellationToken);
        if (reward is not null && progression is not null)
            reward = reward with { ExperienceAfter = progression.ExperienceAfter,
                LevelAfter = progression.LevelAfter };
        await using (var receipt = new NpgsqlCommand(
            "INSERT INTO game.combat_operation_receipts(operation_id,attacker_character_id,target_kind,target_legacy_uid," +
            "map_id,result,safe_result_category,request_fingerprint,target_version_before,health_before,damage,health_after," +
            "killed,skill_type,skill_level,stamina_before,stamina_after,skill_experience_award," +
            "character_experience_awarded,resulting_character_level,resulting_character_experience) " +
            "VALUES($1,$2,$3,$4,$5,'applied',$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)", connection, transaction))
        {
            receipt.Parameters.AddWithValue(mutation.OperationId); receipt.Parameters.AddWithValue(characterId);
            receipt.Parameters.AddWithValue(mutation.TargetKind == LiveEntityKind.Monster ? "monster" : "character");
            receipt.Parameters.AddWithValue((long)mutation.TargetUid); receipt.Parameters.AddWithValue(mutation.Map.MapId);
            receipt.Parameters.AddWithValue($"ninja-skill-{mutation.SkillId}"); receipt.Parameters.AddWithValue(fingerprint);
            receipt.Parameters.AddWithValue(mutation.ExpectedTargetVersion); receipt.Parameters.AddWithValue(mutation.PreviousHealth);
            receipt.Parameters.AddWithValue(mutation.Damage); receipt.Parameters.AddWithValue(mutation.RemainingHealth);
            receipt.Parameters.AddWithValue(mutation.Killed); receipt.Parameters.AddWithValue((int)mutation.SkillId);
            receipt.Parameters.AddWithValue(checked((short)mutation.SkillLevel));
            receipt.Parameters.AddWithValue(checked((short)mutation.PreviousStamina));
            receipt.Parameters.AddWithValue(checked((short)mutation.RemainingStamina));
            receipt.Parameters.AddWithValue(mutation.SkillExperienceAward);
            receipt.Parameters.AddWithValue(mutation.Progression?.CharacterExperienceAward ?? 0);
            receipt.Parameters.AddWithValue((object?)progression?.LevelAfter ?? DBNull.Value);
            receipt.Parameters.AddWithValue((object?)progression?.ExperienceAfter ?? DBNull.Value);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }
        if (progression is not null)
            await InsertProficiencyResultsAsync(connection, transaction, mutation.OperationId,
                progression.Proficiencies, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(CombatPersistenceCode.Applied, reward, Progression: progression);
    }

    public async ValueTask<CombatPersistenceResult> CommitAsync(CombatMutation mutation,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return await CommitOnceAsync(mutation, cancellationToken); }
            catch (PostgresException error) when (attempt < 2 &&
                error.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or
                    PostgresErrorCodes.UniqueViolation)
            {
                await Task.Yield();
            }
        }
    }

    private async Task<CombatPersistenceResult> CommitOnceAsync(CombatMutation mutation,
        CancellationToken cancellationToken)
    {
        string fingerprint = Fingerprint(mutation);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        Guid attackerCharacterId;
        await using (var attacker = new NpgsqlCommand(
            "SELECT character_id FROM game.characters WHERE account_id=$1 AND legacy_uid=$2 FOR SHARE", connection, transaction))
        {
            attacker.Parameters.AddWithValue(mutation.Attacker.AccountId.Value);
            attacker.Parameters.AddWithValue(checked((int)mutation.Attacker.CharacterUid.Value));
            if (await attacker.ExecuteScalarAsync(cancellationToken) is not Guid characterId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(CombatPersistenceCode.Conflict);
            }
            attackerCharacterId = characterId;
        }

        await using (var replay = new NpgsqlCommand(
            "SELECT request_fingerprint,resulting_target_stamina,sitting_interrupted," +
            "character_experience_awarded,resulting_character_experience,resulting_character_level " +
            "FROM game.combat_operation_receipts WHERE operation_id=$1", connection, transaction))
        {
            replay.Parameters.AddWithValue(mutation.OperationId);
            await using NpgsqlDataReader replayReader = await replay.ExecuteReaderAsync(cancellationToken);
            if (await replayReader.ReadAsync(cancellationToken))
            {
                string value = replayReader.GetString(0);
                ushort? targetStamina = replayReader.IsDBNull(1) ? null :
                    checked((ushort)replayReader.GetInt16(1));
                bool sittingInterrupted = replayReader.GetBoolean(2);
                long progressionAward = replayReader.GetInt64(3);
                long? resultingExperience = replayReader.IsDBNull(4) ? null : replayReader.GetInt64(4);
                byte? resultingLevel = replayReader.IsDBNull(5) ? null :
                    checked((byte)replayReader.GetInt16(5));
                await replayReader.DisposeAsync();
                CombatProgressionCommitResult? replayProgression = resultingExperience.HasValue ?
                    await ReadProgressionResultAsync(connection, transaction, mutation.OperationId,
                        progressionAward, resultingExperience.Value, resultingLevel!.Value,
                        cancellationToken) : null;
                MonsterRewardCommitResult? replayReward = null;
                if (value == fingerprint && mutation.Reward is not null)
                    replayReward = await PostgreSqlGroundDropRuntime.CommitRewardInTransactionAsync(
                        connection, transaction, mutation.Reward, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                bool same = value == fingerprint;
                return new(same ? CombatPersistenceCode.Replay : CombatPersistenceCode.Conflict,
                    replayReward, same ? targetStamina : null, same && sittingInterrupted,
                    same ? replayProgression : null);
            }
        }

        MonsterRewardCommitResult? reward = null;
        if (mutation.Reward is MonsterRewardPlan plan)
        {
            if (!mutation.Killed || mutation.TargetKind != LiveEntityKind.Monster ||
                plan.Killer != mutation.Attacker || plan.MonsterUid != mutation.TargetUid ||
                plan.Map != mutation.Map || plan.X != mutation.TargetX || plan.Y != mutation.TargetY)
                throw new InvalidOperationException("A combat reward plan is not bound to its authoritative death mutation.");
            reward = await PostgreSqlGroundDropRuntime.CommitRewardInTransactionAsync(
                connection, transaction, plan, cancellationToken);
            if (!reward.Applied)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(CombatPersistenceCode.Conflict);
            }
        }

        CombatProgressionCommitResult? progression = mutation.Progression is null ? null :
            await ApplyCombatProgressionAsync(connection, transaction, attackerCharacterId,
                mutation.Progression.Validate(), cancellationToken);
        if (reward is not null && progression is not null)
            reward = reward with { ExperienceAfter = progression.ExperienceAfter,
                LevelAfter = progression.LevelAfter };

        if (mutation.TargetKind == LiveEntityKind.Player)
        {
            await using var target = new NpgsqlCommand(
                "SELECT c.character_id,c.map_id,c.x,c.y,v.current_health,v.row_version,a.stamina FROM game.characters c " +
                "JOIN game.character_vitals v USING(character_id) JOIN game.character_attributes a USING(character_id) " +
                "WHERE c.legacy_uid=$1 FOR UPDATE OF c,v,a",
                connection, transaction);
            target.Parameters.AddWithValue(checked((int)mutation.TargetUid));
            await using NpgsqlDataReader reader = await target.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetInt32(1) != mutation.Map.MapId ||
                mutation.Map.InstanceId != 0 || reader.GetInt32(2) != mutation.TargetX ||
                reader.GetInt32(3) != mutation.TargetY || reader.GetInt32(4) != mutation.PreviousHealth ||
                reader.GetInt64(5) != mutation.ExpectedTargetVersion ||
                mutation.PreviousTargetStamina is ushort expectedStamina && reader.GetInt16(6) != expectedStamina)
            {
                await reader.DisposeAsync();
                await transaction.RollbackAsync(cancellationToken);
                return new(CombatPersistenceCode.Conflict);
            }
            Guid targetCharacterId = reader.GetGuid(0);
            await reader.DisposeAsync();
            if (mutation.PreviousTargetStamina is ushort previousStamina &&
                mutation.RemainingTargetStamina is ushort resultingStamina)
            {
                await using var stamina = new NpgsqlCommand(
                    "UPDATE game.character_attributes SET stamina=$2,row_version=row_version+1," +
                    "updated_at=clock_timestamp() WHERE character_id=$1 AND stamina=$3", connection, transaction);
                stamina.Parameters.AddWithValue(targetCharacterId);
                stamina.Parameters.AddWithValue(checked((short)resultingStamina));
                stamina.Parameters.AddWithValue(checked((short)previousStamina));
                if (await stamina.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new(CombatPersistenceCode.Conflict);
                }
            }
            await using var update = new NpgsqlCommand(
                "UPDATE game.character_vitals v SET current_health=$2,updated_at=clock_timestamp(),row_version=row_version+1 " +
                "WHERE v.character_id=(SELECT character_id FROM game.characters WHERE legacy_uid=$1) AND v.row_version=$3",
                connection, transaction);
            update.Parameters.AddWithValue(checked((int)mutation.TargetUid));
            update.Parameters.AddWithValue(checked((int)mutation.RemainingHealth));
            update.Parameters.AddWithValue(mutation.ExpectedTargetVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(CombatPersistenceCode.Conflict);
            }
        }

        await using (var receipt = new NpgsqlCommand(
            "INSERT INTO game.combat_operation_receipts(operation_id,attacker_character_id,target_kind," +
            "target_legacy_uid,map_id,result,safe_result_category,request_fingerprint,target_version_before," +
            "health_before,damage,health_after,killed,previous_target_stamina,resulting_target_stamina,sitting_interrupted," +
            "character_experience_awarded,resulting_character_level,resulting_character_experience) " +
            "VALUES($1,(SELECT character_id FROM game.characters WHERE account_id=$2 AND legacy_uid=$3)," +
            "$4,$5,$6,'applied',$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19)",
            connection, transaction))
        {
            receipt.Parameters.AddWithValue(mutation.OperationId);
            receipt.Parameters.AddWithValue(mutation.Attacker.AccountId.Value);
            receipt.Parameters.AddWithValue(checked((int)mutation.Attacker.CharacterUid.Value));
            receipt.Parameters.AddWithValue(mutation.TargetKind == LiveEntityKind.Player ? "character" : "monster");
            receipt.Parameters.AddWithValue((long)mutation.TargetUid);
            receipt.Parameters.AddWithValue(mutation.Map.MapId);
            receipt.Parameters.AddWithValue(mutation.SafeCategory);
            receipt.Parameters.AddWithValue(fingerprint);
            receipt.Parameters.AddWithValue(mutation.ExpectedTargetVersion);
            receipt.Parameters.AddWithValue(mutation.PreviousHealth);
            receipt.Parameters.AddWithValue(mutation.Damage);
            receipt.Parameters.AddWithValue(mutation.RemainingHealth);
            receipt.Parameters.AddWithValue(mutation.Killed);
            receipt.Parameters.AddWithValue((object?)mutation.PreviousTargetStamina is ushort previous
                ? checked((short)previous) : DBNull.Value);
            receipt.Parameters.AddWithValue((object?)mutation.RemainingTargetStamina is ushort after
                ? checked((short)after) : DBNull.Value);
            receipt.Parameters.AddWithValue(mutation.SittingInterrupted);
            receipt.Parameters.AddWithValue(mutation.Progression?.CharacterExperienceAward ?? 0);
            receipt.Parameters.AddWithValue((object?)progression?.LevelAfter ?? DBNull.Value);
            receipt.Parameters.AddWithValue((object?)progression?.ExperienceAfter ?? DBNull.Value);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }
        if (progression is not null)
            await InsertProficiencyResultsAsync(connection, transaction, mutation.OperationId,
                progression.Proficiencies, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(CombatPersistenceCode.Applied, reward, mutation.RemainingTargetStamina,
            mutation.SittingInterrupted, progression);
    }

    private static async Task<CombatProgressionCommitResult> ApplyCombatProgressionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid characterId,
        CombatProgressionPlan plan, CancellationToken cancellationToken)
    {
        byte proficiencyEligibilityLevel;
        await using (var currentLevel = new NpgsqlCommand(
            "SELECT level FROM game.characters WHERE character_id=$1 FOR UPDATE",
            connection, transaction))
        {
            currentLevel.Parameters.AddWithValue(characterId);
            proficiencyEligibilityLevel = checked((byte)Convert.ToInt16(
                await currentLevel.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture));
        }
        (long experience, byte level) = await PostgreSqlGroundDropRuntime.ApplyExperienceAsync(
            connection, transaction, characterId, plan.CharacterExperienceAward, cancellationToken);
        if (plan.WeaponProficiencyBaseAward == 0)
            return new(plan.CharacterExperienceAward, experience, level, []);

        var equipment = new List<(short Slot, uint ItemType, byte SocketOne, byte SocketTwo)>();
        await using (var equipped = new NpgsqlCommand(
            "SELECT slot,item_type,socket_one,socket_two FROM game.equipped_items " +
            "WHERE character_id=$1 ORDER BY slot", connection, transaction))
        {
            equipped.Parameters.AddWithValue(characterId);
            await using NpgsqlDataReader reader = await equipped.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                equipment.Add((reader.GetInt16(0), checked((uint)reader.GetInt64(1)),
                    checked((byte)reader.GetInt32(2)), checked((byte)reader.GetInt32(3))));
        }
        double gemMultiplier = 1d;
        foreach (byte socket in equipment.SelectMany(item => new[] { item.SocketOne, item.SocketTwo }))
            gemMultiplier += socket switch { 51 => .3d, 52 => .5d, 53 => 1d, _ => 0d };
        uint? right = equipment.FirstOrDefault(item => item.Slot == 4).ItemType;
        uint? left = equipment.FirstOrDefault(item => item.Slot == 5).ItemType;
        if (right == 0) right = null;
        if (left == 0) left = null;
        IReadOnlyDictionary<ushort, long> raw = OwnerSourceProficiencyFormula.ResolveWeaponAwards(
            right, left, plan.WeaponProficiencyBaseAward);
        var results = new List<WeaponProficiencyCommitResult>(raw.Count);
        foreach ((ushort proficiencyType, long rawAward) in raw.OrderBy(value => value.Key))
        {
            if (rawAward <= 0) continue;
            short proficiencyLevel;
            long proficiencyExperience;
            bool existed;
            await using (var current = new NpgsqlCommand(
                "SELECT level,experience FROM game.character_proficiencies " +
                "WHERE character_id=$1 AND proficiency_type=$2 FOR UPDATE", connection, transaction))
            {
                current.Parameters.AddWithValue(characterId);
                current.Parameters.AddWithValue((int)proficiencyType);
                await using NpgsqlDataReader reader = await current.ExecuteReaderAsync(cancellationToken);
                existed = await reader.ReadAsync(cancellationToken);
                proficiencyLevel = existed ? reader.GetInt16(0) : (short)0;
                proficiencyExperience = existed ? reader.GetInt64(1) : 0;
            }
            if (!existed)
            {
                await using var insert = new NpgsqlCommand(
                    "INSERT INTO game.character_proficiencies(character_id,proficiency_type,level,experience) " +
                    "VALUES($1,$2,0,0)", connection, transaction);
                insert.Parameters.AddWithValue(characterId); insert.Parameters.AddWithValue((int)proficiencyType);
                await insert.ExecuteNonQueryAsync(cancellationToken);
                results.Add(new(proficiencyType, 0, 0, 0));
                continue;
            }
            long applied = 0;
            if (proficiencyLevel < 20)
            {
                applied = OwnerSourceProficiencyFormula.ScaleAward(rawAward,
                    checked((byte)proficiencyLevel), proficiencyEligibilityLevel, gemMultiplier);
                proficiencyExperience = Math.Min(uint.MaxValue, checked(proficiencyExperience + applied));
                if (proficiencyLevel < 20)
                {
                    await using var requirement = new NpgsqlCommand(
                        "SELECT required_experience FROM gameplay_reference.proficiency_level_requirements " +
                        "WHERE level=$1", connection, transaction);
                    requirement.Parameters.AddWithValue(proficiencyLevel);
                    long required = Convert.ToInt64(await requirement.ExecuteScalarAsync(cancellationToken),
                        CultureInfo.InvariantCulture);
                    if (proficiencyExperience >= required)
                    {
                        proficiencyLevel++;
                        proficiencyExperience = 0;
                    }
                }
                await using var update = new NpgsqlCommand(
                    "UPDATE game.character_proficiencies SET level=$3,experience=$4,row_version=row_version+1 " +
                    "WHERE character_id=$1 AND proficiency_type=$2", connection, transaction);
                update.Parameters.AddWithValue(characterId); update.Parameters.AddWithValue((int)proficiencyType);
                update.Parameters.AddWithValue(proficiencyLevel); update.Parameters.AddWithValue(proficiencyExperience);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
            results.Add(new(proficiencyType, applied, checked((byte)proficiencyLevel),
                checked((uint)proficiencyExperience)));
        }
        return new(plan.CharacterExperienceAward, experience, level, results);
    }

    private static async Task InsertProficiencyResultsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid operationId,
        IReadOnlyList<WeaponProficiencyCommitResult> values, CancellationToken cancellationToken)
    {
        foreach (WeaponProficiencyCommitResult value in values)
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO game.combat_proficiency_results(operation_id,proficiency_type,experience_awarded," +
                "resulting_level,resulting_experience) VALUES($1,$2,$3,$4,$5)", connection, transaction);
            insert.Parameters.AddWithValue(operationId); insert.Parameters.AddWithValue((int)value.ProficiencyType);
            insert.Parameters.AddWithValue(value.ExperienceAwarded); insert.Parameters.AddWithValue((short)value.LevelAfter);
            insert.Parameters.AddWithValue((long)value.ExperienceAfter);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<CombatProgressionCommitResult> ReadProgressionResultAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId,
        long experienceAward, long experienceAfter, byte levelAfter, CancellationToken cancellationToken)
    {
        var values = new List<WeaponProficiencyCommitResult>();
        await using var command = new NpgsqlCommand(
            "SELECT proficiency_type,experience_awarded,resulting_level,resulting_experience " +
            "FROM game.combat_proficiency_results WHERE operation_id=$1 ORDER BY proficiency_type",
            connection, transaction);
        command.Parameters.AddWithValue(operationId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            values.Add(new(checked((ushort)reader.GetInt32(0)), reader.GetInt64(1),
                checked((byte)reader.GetInt16(2)), checked((uint)reader.GetInt64(3))));
        return new(experienceAward, experienceAfter, levelAfter, values);
    }

    private static string Fingerprint(CombatMutation mutation)
    {
        string canonical = string.Join('|',
            mutation.Attacker.AccountId.Value.ToString("N", CultureInfo.InvariantCulture),
            mutation.Attacker.CharacterUid.Value.ToString(CultureInfo.InvariantCulture),
            ((byte)mutation.TargetKind).ToString(CultureInfo.InvariantCulture),
            mutation.TargetUid.ToString(CultureInfo.InvariantCulture),
            mutation.Map.MapId.ToString(CultureInfo.InvariantCulture),
            mutation.Map.InstanceId.ToString(CultureInfo.InvariantCulture),
            mutation.TargetX.ToString(CultureInfo.InvariantCulture),
            mutation.TargetY.ToString(CultureInfo.InvariantCulture),
            mutation.ExpectedTargetVersion.ToString(CultureInfo.InvariantCulture),
            mutation.PreviousHealth.ToString(CultureInfo.InvariantCulture),
            mutation.Damage.ToString(CultureInfo.InvariantCulture),
            mutation.RemainingHealth.ToString(CultureInfo.InvariantCulture),
            mutation.Killed ? "1" : "0",
            mutation.Progression?.CharacterExperienceAward.ToString(CultureInfo.InvariantCulture) ?? "0",
            mutation.Progression?.WeaponProficiencyBaseAward.ToString(CultureInfo.InvariantCulture) ?? "0",
            mutation.Reward?.DeathId.ToString("N", CultureInfo.InvariantCulture) ?? "none",
            mutation.Reward?.SpawnGeneration.ToString(CultureInfo.InvariantCulture) ?? "0");
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string SkillFingerprint(NinjaCombatMutation mutation)
    {
        string canonical = string.Join('|',
            mutation.Attacker.AccountId.Value.ToString("N", CultureInfo.InvariantCulture),
            mutation.Attacker.CharacterUid.Value.ToString(CultureInfo.InvariantCulture),
            mutation.SkillId.ToString(CultureInfo.InvariantCulture),
            mutation.SkillLevel.ToString(CultureInfo.InvariantCulture),
            mutation.TargetUid.ToString(CultureInfo.InvariantCulture),
            mutation.Map.MapId.ToString(CultureInfo.InvariantCulture),
            mutation.Map.InstanceId.ToString(CultureInfo.InvariantCulture),
            mutation.AttackerX.ToString(CultureInfo.InvariantCulture),
            mutation.AttackerY.ToString(CultureInfo.InvariantCulture),
            mutation.TargetX.ToString(CultureInfo.InvariantCulture),
            mutation.TargetY.ToString(CultureInfo.InvariantCulture),
            mutation.PreviousStamina.ToString(CultureInfo.InvariantCulture),
            mutation.RemainingStamina.ToString(CultureInfo.InvariantCulture),
            mutation.ExpectedTargetVersion.ToString(CultureInfo.InvariantCulture),
            mutation.PreviousHealth.ToString(CultureInfo.InvariantCulture),
            mutation.Damage.ToString(CultureInfo.InvariantCulture),
            mutation.RemainingHealth.ToString(CultureInfo.InvariantCulture),
            mutation.Progression?.CharacterExperienceAward.ToString(CultureInfo.InvariantCulture) ?? "0",
            mutation.Reward?.DeathId.ToString("N", CultureInfo.InvariantCulture) ?? "none",
            mutation.Reward?.SpawnGeneration.ToString(CultureInfo.InvariantCulture) ?? "0");
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(canonical))).ToLowerInvariant();
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}

public sealed record CombatMonsterSpawn(LiveCombatEntitySnapshot Entity, uint Mesh, string Name,
    ushort Level, byte Direction);
public readonly record struct PersistedSkillState(uint Experience, ushort SkillId, ushort Level);
public sealed record OwnerMonsterCatalog(
    IReadOnlyDictionary<int, OwnerMonsterTemplate> Templates,
    IReadOnlyList<OwnerMonsterGenerator> Generators,
    IReadOnlyList<OwnerMonsterUidAssignment> UidAssignments,
    IReadOnlyList<OwnerMonsterDropRule> DropRules,
    IReadOnlyList<OwnerEquipmentDropCandidate> EquipmentDropCandidates,
    IReadOnlyList<OwnerCommonDropItem> CommonDropItems);
