using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Revora.Domain.Accounts;
using Revora.Gameplay.Models;

namespace Revora.Persistence.PostgreSql;

/// <summary>Serializable death-reward and ground-pickup persistence.</summary>
public sealed class PostgreSqlGroundDropRuntime(string connectionString) : IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);

    public async Task<IReadOnlyList<GroundDropSnapshot>> LoadActiveAsync(CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using var command = new NpgsqlCommand(DropSelect +
            " WHERE d.state='active' AND d.expires_at>clock_timestamp() ORDER BY d.ground_uid", connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        var rows = new List<GroundDropSnapshot>();
        while (await reader.ReadAsync(token)) rows.Add(ReadDrop(reader));
        return rows;
    }

    public async Task<IReadOnlyList<GroundDropSnapshot>> ExpireDueAsync(CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return await ExpireDueOnceAsync(token); }
            catch (PostgresException error) when (attempt < 2 && error.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {
                await Task.Yield();
            }
        }
    }

    private async Task<IReadOnlyList<GroundDropSnapshot>> ExpireDueOnceAsync(CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await using var select = new NpgsqlCommand(DropSelect +
            " WHERE d.state='active' AND d.expires_at<=clock_timestamp() FOR UPDATE OF d", connection, tx);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(token);
        var rows = new List<GroundDropSnapshot>();
        while (await reader.ReadAsync(token)) rows.Add(ReadDrop(reader));
        await reader.DisposeAsync();
        if (rows.Count > 0)
        {
            await using var expire = new NpgsqlCommand(
                "UPDATE game.world_ground_drops SET state='expired',row_version=row_version+1 " +
                "WHERE ground_uid=ANY($1) AND state='active'", connection, tx);
            expire.Parameters.AddWithValue(rows.Select(value => (long)value.GroundUid).ToArray());
            if (await expire.ExecuteNonQueryAsync(token) != rows.Count)
                throw new DBConcurrencyException("Ground expiry lost a serialized row.");
        }
        await tx.CommitAsync(token); return rows;
    }

    public async Task<MonsterRewardCommitResult> CommitRewardAsync(MonsterRewardPlan plan,
        CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        MonsterRewardCommitResult result = await CommitRewardInTransactionAsync(connection, tx, plan, token);
        await tx.CommitAsync(token);
        return result;
    }

    internal static async Task<MonsterRewardCommitResult> CommitRewardInTransactionAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, MonsterRewardPlan plan, CancellationToken token)
    {
        Guid characterId = await ResolveCharacterAsync(connection, tx, plan.Killer, token);
        await using (var existing = new NpgsqlCommand(
            "SELECT death_id FROM game.monster_death_resolutions WHERE monster_legacy_uid=$1 AND spawn_generation=$2",
            connection, tx))
        {
            existing.Parameters.AddWithValue((long)plan.MonsterUid);
            existing.Parameters.AddWithValue(plan.SpawnGeneration);
            object? prior = await existing.ExecuteScalarAsync(token);
            if (prior is Guid death)
            {
                if (death != plan.DeathId) throw new InvalidOperationException("A monster generation has conflicting death identities.");
                IReadOnlyList<GroundDropSnapshot> replayDrops = await ReadDeathDropsAsync(connection, tx, death, token);
                (long exp, byte level) = await ReadProgressAsync(connection, tx, characterId, token);
                return new(false, true, exp, level, replayDrops);
            }
        }
        await using (var death = new NpgsqlCommand(
            "INSERT INTO game.monster_death_resolutions(death_id,monster_legacy_uid,spawn_generation,monster_type," +
            "killer_character_id,map_id,instance_id,x,y,experience_awarded,skill_experience_awarded) " +
            "VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", connection, tx))
        {
            death.Parameters.AddWithValue(plan.DeathId); death.Parameters.AddWithValue((long)plan.MonsterUid);
            death.Parameters.AddWithValue(plan.SpawnGeneration); death.Parameters.AddWithValue(plan.MonsterType);
            death.Parameters.AddWithValue(characterId); death.Parameters.AddWithValue(plan.Map.MapId);
            death.Parameters.AddWithValue(plan.Map.InstanceId); death.Parameters.AddWithValue((int)plan.X);
            death.Parameters.AddWithValue((int)plan.Y); death.Parameters.AddWithValue(plan.Experience);
            death.Parameters.AddWithValue(plan.SkillExperience);
            await death.ExecuteNonQueryAsync(token);
        }
        (long after, byte levelAfter) = await ApplyExperienceAsync(connection, tx, characterId,
            plan.Experience, token);
        var created = new List<GroundDropSnapshot>(plan.Drops.Count);
        foreach (PlannedGroundDrop drop in plan.Drops)
            created.Add(await CreateDropAsync(connection, tx, characterId, plan, drop, token));
        return new(true, false, after, levelAfter, created);
    }

    internal static async Task<(long Experience, byte Level)> ApplyExperienceAsync(NpgsqlConnection c,
        NpgsqlTransaction tx, Guid characterId, long award, CancellationToken token)
    {
        if (award < 0) throw new ArgumentOutOfRangeException(nameof(award));
        short level, profession, rebirth;
        long experience;
        int strength, agility, vitality, spirit, points;
        await using (var current = new NpgsqlCommand(
            "SELECT ch.level,ch.profession,coalesce(p.rebirth_count,0),a.experience,a.strength,a.agility," +
            "a.vitality,a.spirit,a.available_attribute_points FROM game.characters ch " +
            "JOIN game.character_attributes a USING(character_id) LEFT JOIN game.character_progression p USING(character_id) " +
            "WHERE ch.character_id=$1 FOR UPDATE OF ch,a", c, tx))
        {
            current.Parameters.AddWithValue(characterId);
            await using NpgsqlDataReader reader = await current.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new InvalidOperationException("Reward progression state is missing.");
            level = reader.GetInt16(0); profession = reader.GetInt16(1); rebirth = reader.GetInt16(2);
            experience = reader.GetInt64(3); strength = reader.GetInt32(4); agility = reader.GetInt32(5);
            vitality = reader.GetInt32(6); spirit = reader.GetInt32(7); points = reader.GetInt32(8);
        }
        short beforeLevel = level;
        experience = Math.Min(2_000_000_000L, checked(experience + award));
        while (level < 130)
        {
            await using var requirement = new NpgsqlCommand(
                "SELECT required_experience FROM gameplay_reference.character_level_requirements " +
                "WHERE level=$1 ORDER BY source_id LIMIT 1", c, tx);
            requirement.Parameters.AddWithValue(level);
            object? raw = await requirement.ExecuteScalarAsync(token);
            if (raw is null) break;
            long required = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            if (experience <= required) break; // Owner Source uses a strict greater-than transition.
            experience -= required;
            level++;
        }
        if (level != beforeLevel)
        {
            string family = profession switch
            {
                >= 10 and <= 15 => "trojan", >= 20 and <= 25 => "warrior",
                >= 40 and <= 45 => "archer", >= 50 and <= 55 => "ninja", _ => "taoist"
            };
            if (rebirth == 0 && beforeLevel < 120)
            {
                await using var baseline = new NpgsqlCommand(
                    "SELECT strength,agility,vitality,spirit FROM gameplay_reference.base_character_stats " +
                    "WHERE profession_family=$1 AND level=$2 ORDER BY source_id LIMIT 1", c, tx);
                baseline.Parameters.AddWithValue(family); baseline.Parameters.AddWithValue((short)Math.Min(level, (short)120));
                await using NpgsqlDataReader reader = await baseline.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InvalidDataException("A level reward has no reviewed base-stat row.");
                strength = reader.GetInt32(0); agility = reader.GetInt32(1);
                vitality = reader.GetInt32(2); spirit = reader.GetInt32(3);
            }
            int pointLevels = rebirth > 0 ? level - beforeLevel : Math.Max(0, level - Math.Max(beforeLevel, (short)120));
            points = checked(points + pointLevels * 3);
            await using var character = new NpgsqlCommand(
                "UPDATE game.characters SET level=$2,row_version=row_version+1,updated_at=clock_timestamp() " +
                "WHERE character_id=$1 AND level=$3", c, tx);
            character.Parameters.AddWithValue(characterId); character.Parameters.AddWithValue(level);
            character.Parameters.AddWithValue(beforeLevel);
            if (await character.ExecuteNonQueryAsync(token) != 1) throw new DBConcurrencyException("Level reward lost its character row.");
        }
        await using (var attributes = new NpgsqlCommand(
            "UPDATE game.character_attributes SET experience=$2,strength=$3,agility=$4,vitality=$5,spirit=$6," +
            "available_attribute_points=$7,updated_at=clock_timestamp(),row_version=row_version+1 WHERE character_id=$1", c, tx))
        {
            attributes.Parameters.AddWithValue(characterId); attributes.Parameters.AddWithValue(experience);
            attributes.Parameters.AddWithValue(strength); attributes.Parameters.AddWithValue(agility);
            attributes.Parameters.AddWithValue(vitality); attributes.Parameters.AddWithValue(spirit);
            attributes.Parameters.AddWithValue(points);
            if (await attributes.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("Reward attributes disappeared.");
        }
        if (level != beforeLevel)
        {
            int maxHealth = Math.Max(1, checked(vitality * 24 + strength * 3 + agility * 3 + spirit * 3));
            int maxMana = checked(spirit * (profession is >= 130 and <= 145 ? 15 : 5));
            await using var stats = new NpgsqlCommand(
                "UPDATE game.character_stat_snapshots SET maximum_health=greatest(maximum_health,$2)," +
                "maximum_mana=greatest(maximum_mana,$3),calculated_at=clock_timestamp() WHERE character_id=$1; " +
                "UPDATE game.character_vitals SET current_health=greatest(current_health,$2),current_mana=greatest(current_mana,$3)," +
                "row_version=row_version+1,updated_at=clock_timestamp() WHERE character_id=$1", c, tx);
            stats.Parameters.AddWithValue(characterId); stats.Parameters.AddWithValue(maxHealth);
            stats.Parameters.AddWithValue(maxMana); await stats.ExecuteNonQueryAsync(token);
        }
        return (experience, checked((byte)level));
    }

    public async Task<GroundPickupResult> PickupAsync(GroundPickupRequest request, CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return await PickupOnceAsync(request, token); }
            catch (PostgresException error) when (attempt < 2 && error.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or
                PostgresErrorCodes.UniqueViolation)
            {
                await Task.Yield();
            }
        }
    }

    private async Task<GroundPickupResult> PickupOnceAsync(GroundPickupRequest request,
        CancellationToken token)
    {
        string fingerprint = Fingerprint(request);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        (Guid characterId, CombatMapKey actorMap, ushort actorX, ushort actorY) =
            await ResolvePickupCharacterAsync(connection, tx, request.Actor, token);
        if (actorMap != request.Map || actorX != request.X || actorY != request.Y)
        {
            await tx.RollbackAsync(token);
            return new(GroundPickupCode.OwnershipMismatch, null, null, null, null);
        }
        await using (var replay = new NpgsqlCommand(
            "SELECT request_fingerprint,result,resulting_item_uid,resulting_silver_balance FROM game.ground_pickup_receipts WHERE operation_id=$1",
            connection, tx))
        {
            replay.Parameters.AddWithValue(request.OperationId);
            await using NpgsqlDataReader reader = await replay.ExecuteReaderAsync(token);
            if (await reader.ReadAsync(token))
            {
                if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(fingerprint), Convert.FromHexString(reader.GetString(0))))
                    throw new InvalidOperationException("A pickup operation identity was reused with different semantics.");
                uint? item = reader.IsDBNull(2) ? null : checked((uint)reader.GetInt32(2));
                long? silver = reader.IsDBNull(3) ? null : reader.GetInt64(3);
                await reader.DisposeAsync(); await tx.CommitAsync(token);
                return new(GroundPickupCode.Replay, null, item, silver, null);
            }
        }
        GroundDropSnapshot? drop = await LockDropAsync(connection, tx, request.GroundUid, token);
        GroundPickupCode rejection = ValidatePickup(drop, request);
        if (rejection != GroundPickupCode.Applied)
        {
            await tx.RollbackAsync(token); return new(rejection, drop, null, null, null);
        }
        uint? itemUid = null; long? balance = null; byte? slot = null;
        if (drop!.Kind == GroundDropKind.Item)
        {
            slot = await ReserveSlotAsync(connection, tx, characterId, token);
            if (!slot.HasValue)
            {
                await tx.RollbackAsync(token);
                return new(GroundPickupCode.InventoryFull, drop, null, null, null);
            }
            await MarkPickedAsync(connection, tx, request.GroundUid, characterId, drop.Version, token);
            await using (var transfer = new NpgsqlCommand(
                "UPDATE game.item_instances i SET owner_character_id=$2,row_version=i.row_version+1 " +
                "FROM game.world_ground_drops d WHERE d.ground_uid=$1 AND d.item_instance_id=i.item_instance_id " +
                "AND i.owner_character_id=d.owner_character_id", connection, tx))
            {
                transfer.Parameters.AddWithValue((long)request.GroundUid);
                transfer.Parameters.AddWithValue(characterId);
                if (await transfer.ExecuteNonQueryAsync(token) != 1)
                    throw new DBConcurrencyException("A ground item lost its authoritative owner transition.");
            }
            await using var locate = new NpgsqlCommand(
                "INSERT INTO game.item_locations(item_instance_id,owner_character_id,location_kind,slot) " +
                "SELECT item_instance_id,$2,'inventory',$3 FROM game.world_ground_drops WHERE ground_uid=$1",
                connection, tx);
            locate.Parameters.AddWithValue((long)request.GroundUid); locate.Parameters.AddWithValue(characterId);
            locate.Parameters.AddWithValue((short)slot.Value); await locate.ExecuteNonQueryAsync(token);
            itemUid = drop.LegacyItemUid;
        }
        else
        {
            long before;
            await using (var walletLock = new NpgsqlCommand(
                "SELECT balance FROM game.character_wallets WHERE character_id=$1 AND currency_type='silver' FOR UPDATE",
                connection, tx))
            {
                walletLock.Parameters.AddWithValue(characterId);
                before = Convert.ToInt64(await walletLock.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            }
            long credited = Math.Min(drop.Silver, 2_000_000_000L - before);
            if (credited <= 0)
            {
                await tx.RollbackAsync(token);
                return new(GroundPickupCode.WalletFull, drop, null, before, null);
            }
            await MarkPickedAsync(connection, tx, request.GroundUid, characterId, drop.Version, token);
            await using var wallet = new NpgsqlCommand(
                "UPDATE game.character_wallets SET balance=balance+$2,row_version=row_version+1," +
                "updated_at=clock_timestamp() WHERE character_id=$1 AND currency_type='silver' RETURNING balance",
                connection, tx);
            wallet.Parameters.AddWithValue(characterId); wallet.Parameters.AddWithValue(credited);
            balance = Convert.ToInt64(await wallet.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            await using var ledger = new NpgsqlCommand(
                "INSERT INTO game.currency_ledger(operation_id,character_id,currency_type,delta,balance_after,reason,correlation_id) " +
                "VALUES($1,$2,'silver',$3,$4,'monster-drop',$1)", connection, tx);
            ledger.Parameters.AddWithValue(request.OperationId); ledger.Parameters.AddWithValue(characterId);
            ledger.Parameters.AddWithValue(credited); ledger.Parameters.AddWithValue(balance.Value);
            await ledger.ExecuteNonQueryAsync(token);
        }
        await InsertPickupReceiptAsync(connection, tx, request, characterId, fingerprint,
            GroundPickupCode.Applied, itemUid, balance, token);
        await tx.CommitAsync(token);
        return new(GroundPickupCode.Applied, drop, itemUid, balance, slot);
    }

    private static GroundPickupCode ValidatePickup(GroundDropSnapshot? drop, GroundPickupRequest request)
    {
        if (drop is null) return GroundPickupCode.Missing;
        if (drop.Map != request.Map) return GroundPickupCode.WrongMap;
        if (Math.Max(Math.Abs(drop.X-request.X), Math.Abs(drop.Y-request.Y)) > 3) return GroundPickupCode.OutOfRange;
        if (request.UtcNow >= drop.ExpiresAt) return GroundPickupCode.Expired;
        if (request.UtcNow < drop.ProtectedUntil && drop.Owner.CharacterUid != request.Actor.CharacterUid)
            return GroundPickupCode.Protected;
        return GroundPickupCode.Applied;
    }

    private static async Task<Guid> ResolveCharacterAsync(NpgsqlConnection c, NpgsqlTransaction tx,
        GameplayActor actor, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT character_id FROM game.characters WHERE account_id=$1 AND legacy_uid=$2 FOR SHARE", c, tx);
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        return await command.ExecuteScalarAsync(token) is Guid id ? id :
            throw new InvalidOperationException("The authenticated reward character is missing.");
    }

    private static async Task<(Guid CharacterId, CombatMapKey Map, ushort X, ushort Y)>
        ResolvePickupCharacterAsync(NpgsqlConnection c, NpgsqlTransaction tx, GameplayActor actor,
            CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT character_id,map_id,x,y FROM game.characters WHERE account_id=$1 AND legacy_uid=$2 FOR SHARE", c, tx);
        command.Parameters.AddWithValue(actor.AccountId.Value);
        command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token))
            throw new InvalidOperationException("The authenticated pickup character is missing.");
        return (reader.GetGuid(0), new CombatMapKey(reader.GetInt32(1), 0),
            checked((ushort)reader.GetInt32(2)), checked((ushort)reader.GetInt32(3)));
    }

    private static async Task<(long Experience, byte Level)> ReadProgressAsync(NpgsqlConnection c,
        NpgsqlTransaction tx, Guid characterId, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT a.experience,c.level FROM game.character_attributes a JOIN game.characters c USING(character_id) WHERE character_id=$1", c, tx);
        command.Parameters.AddWithValue(characterId); await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new InvalidOperationException("Reward progression state is missing.");
        return (reader.GetInt64(0), checked((byte)reader.GetInt16(1)));
    }

    private static async Task<GroundDropSnapshot> CreateDropAsync(NpgsqlConnection c, NpgsqlTransaction tx,
        Guid characterId, MonsterRewardPlan plan, PlannedGroundDrop drop, CancellationToken token)
    {
        Guid? itemId = null; uint? legacyItemUid = null;
        if (drop.Kind == GroundDropKind.Item)
        {
            itemId = Guid.NewGuid();
            await using var item = new NpgsqlCommand(
                "INSERT INTO game.item_instances(item_instance_id,owner_character_id,item_source_id,item_type,durability,maximum_durability,plus,bless,socket_one,socket_two) " +
                "SELECT $1,$2,$3,$4,maximum_durability,maximum_durability,$5,$6,CASE WHEN $7>0 THEN 255 ELSE 0 END,CASE WHEN $7>1 THEN 255 ELSE 0 END " +
                "FROM gameplay_reference.item_types WHERE source_id=$3 AND item_type=$4 RETURNING legacy_item_uid", c, tx);
            item.Parameters.AddWithValue(itemId.Value); item.Parameters.AddWithValue(characterId);
            item.Parameters.AddWithValue(drop.ItemSourceId!.Value); item.Parameters.AddWithValue((long)drop.ItemType);
            item.Parameters.AddWithValue((short)drop.Plus); item.Parameters.AddWithValue((short)drop.Bless);
            item.Parameters.AddWithValue((short)drop.Sockets);
            legacyItemUid = checked((uint)Convert.ToInt32(await item.ExecuteScalarAsync(token), CultureInfo.InvariantCulture));
        }
        await using var insert = new NpgsqlCommand(
            "INSERT INTO game.world_ground_drops(death_id,item_instance_id,silver_amount,display_item_type,owner_character_id,map_id,instance_id,x,y,protected_until,expires_at) " +
            "VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,clock_timestamp()+interval '20 seconds',clock_timestamp()+interval '2 minutes') RETURNING ground_uid,protected_until,expires_at,row_version", c, tx);
        insert.Parameters.AddWithValue(plan.DeathId); insert.Parameters.AddWithValue((object?)itemId ?? DBNull.Value);
        insert.Parameters.AddWithValue(drop.Kind == GroundDropKind.Silver ? drop.Silver : DBNull.Value);
        insert.Parameters.AddWithValue((long)drop.ItemType); insert.Parameters.AddWithValue(characterId);
        insert.Parameters.AddWithValue(plan.Map.MapId); insert.Parameters.AddWithValue(plan.Map.InstanceId);
        insert.Parameters.AddWithValue((int)drop.X); insert.Parameters.AddWithValue((int)drop.Y);
        await using NpgsqlDataReader reader = await insert.ExecuteReaderAsync(token); await reader.ReadAsync(token);
        return new GroundDropSnapshot(checked((uint)reader.GetInt64(0)), drop.Kind, drop.ItemType,
            drop.Silver, plan.Killer, plan.Map, drop.X, drop.Y, reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetFieldValue<DateTimeOffset>(2), reader.GetInt64(3), legacyItemUid).Validate();
    }

    private static async Task<IReadOnlyList<GroundDropSnapshot>> ReadDeathDropsAsync(NpgsqlConnection c,
        NpgsqlTransaction tx, Guid deathId, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT d.ground_uid,CASE WHEN d.item_instance_id IS NULL THEN 2 ELSE 1 END,d.display_item_type," +
            "coalesce(d.silver_amount,0),a.account_id,c.legacy_uid,d.map_id,d.instance_id,d.x,d.y,d.protected_until," +
            "d.expires_at,d.row_version,i.legacy_item_uid FROM game.world_ground_drops d " +
            "JOIN game.characters c ON c.character_id=d.owner_character_id JOIN identity.game_accounts a ON a.account_id=c.account_id " +
            "LEFT JOIN game.item_instances i ON i.item_instance_id=d.item_instance_id WHERE d.death_id=$1 ORDER BY d.ground_uid",
            c, tx);
        command.Parameters.AddWithValue(deathId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        var rows = new List<GroundDropSnapshot>();
        while (await reader.ReadAsync(token)) rows.Add(ReadDrop(reader));
        return rows;
    }

    private static async Task<GroundDropSnapshot?> LockDropAsync(NpgsqlConnection c, NpgsqlTransaction tx,
        uint uid, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT d.ground_uid,CASE WHEN d.item_instance_id IS NULL THEN 2 ELSE 1 END,d.display_item_type," +
            "coalesce(d.silver_amount,0),a.account_id,ch.legacy_uid,d.map_id,d.instance_id,d.x,d.y,d.protected_until," +
            "d.expires_at,d.row_version,i.legacy_item_uid FROM game.world_ground_drops d " +
            "JOIN game.characters ch ON ch.character_id=d.owner_character_id JOIN identity.game_accounts a ON a.account_id=ch.account_id " +
            "LEFT JOIN game.item_instances i ON i.item_instance_id=d.item_instance_id " +
            "WHERE d.ground_uid=$1 AND d.state='active' FOR UPDATE OF d", c, tx);
        command.Parameters.AddWithValue((long)uid);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? ReadDrop(reader) : null;
    }

    private static GroundDropSnapshot ReadDrop(NpgsqlDataReader reader) => new GroundDropSnapshot(
        checked((uint)reader.GetInt64(0)), (GroundDropKind)reader.GetInt32(1),
        checked((uint)reader.GetInt64(2)), reader.GetInt64(3),
        new GameplayActor(new AccountId(reader.GetGuid(4)),
            new CharacterUid(checked((uint)reader.GetInt32(5)))),
        new CombatMapKey(reader.GetInt32(6), reader.GetInt32(7)), checked((ushort)reader.GetInt32(8)),
        checked((ushort)reader.GetInt32(9)), reader.GetFieldValue<DateTimeOffset>(10),
        reader.GetFieldValue<DateTimeOffset>(11), reader.GetInt64(12),
        reader.IsDBNull(13) ? null : checked((uint)reader.GetInt32(13))).Validate();

    private const string DropSelect =
        "SELECT d.ground_uid,CASE WHEN d.item_instance_id IS NULL THEN 2 ELSE 1 END,d.display_item_type," +
        "coalesce(d.silver_amount,0),a.account_id,ch.legacy_uid,d.map_id,d.instance_id,d.x,d.y,d.protected_until," +
        "d.expires_at,d.row_version,i.legacy_item_uid FROM game.world_ground_drops d " +
        "JOIN game.characters ch ON ch.character_id=d.owner_character_id JOIN identity.game_accounts a ON a.account_id=ch.account_id " +
        "LEFT JOIN game.item_instances i ON i.item_instance_id=d.item_instance_id";

    private static async Task<byte?> ReserveSlotAsync(NpgsqlConnection c, NpgsqlTransaction tx,
        Guid characterId, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT s FROM generate_series(0,(SELECT slot_capacity-1 FROM game.inventory_profiles WHERE character_id=$1)) s " +
            "WHERE NOT EXISTS(SELECT 1 FROM game.item_locations WHERE owner_character_id=$1 AND location_kind='inventory' AND slot=s) ORDER BY s LIMIT 1", c, tx);
        command.Parameters.AddWithValue(characterId); object? value = await command.ExecuteScalarAsync(token);
        return value is int slot ? checked((byte)slot) : null;
    }

    private static async Task MarkPickedAsync(NpgsqlConnection c, NpgsqlTransaction tx, uint uid,
        Guid characterId, long version, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "UPDATE game.world_ground_drops SET state='picked-up',picked_up_by=$2,picked_up_at=clock_timestamp(),row_version=row_version+1 " +
            "WHERE ground_uid=$1 AND state='active' AND row_version=$3", c, tx);
        command.Parameters.AddWithValue((long)uid); command.Parameters.AddWithValue(characterId);
        command.Parameters.AddWithValue(version);
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new DBConcurrencyException("Ground pickup lost its serialized row.");
    }

    private static async Task InsertPickupReceiptAsync(NpgsqlConnection c, NpgsqlTransaction tx,
        GroundPickupRequest request, Guid characterId, string fingerprint, GroundPickupCode result,
        uint? itemUid, long? silver, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO game.ground_pickup_receipts(operation_id,ground_uid,character_id,request_fingerprint,result,resulting_item_uid,resulting_silver_balance) " +
            "VALUES($1,$2,$3,$4,$5,$6,$7)", c, tx);
        command.Parameters.AddWithValue(request.OperationId); command.Parameters.AddWithValue((long)request.GroundUid);
        command.Parameters.AddWithValue(characterId); command.Parameters.AddWithValue(fingerprint);
        command.Parameters.AddWithValue(result switch
        {
            GroundPickupCode.Applied => "picked-up",
            GroundPickupCode.Replay => "replay",
            GroundPickupCode.Protected => "protected",
            GroundPickupCode.InventoryFull => "full",
            GroundPickupCode.Expired => "expired",
            _ => "invalid"
        });
        command.Parameters.AddWithValue((object?)itemUid is uint item ? checked((int)item) : DBNull.Value);
        command.Parameters.AddWithValue((object?)silver ?? DBNull.Value); await command.ExecuteNonQueryAsync(token);
    }

    private static string Fingerprint(GroundPickupRequest request)
    {
        string canonical = string.Join('|', request.Actor.AccountId.Value.ToString("N"),
            request.Actor.CharacterUid.Value, request.GroundUid, request.Map.MapId, request.Map.InstanceId,
            request.X, request.Y);
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(canonical))).ToLowerInvariant();
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
