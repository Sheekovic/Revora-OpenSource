using System.Security.Cryptography;
using Npgsql;
using Revora.Application.Authentication;
using Revora.Domain.Accounts;

namespace Revora.Persistence.PostgreSql;

public sealed class PostgreSqlGameDataStore : IAsyncDisposable
{
    private static readonly Guid DevelopmentRealmId = Guid.Parse("4d1e7aac-e046-48bd-88b9-642f7eafef83");
    private readonly NpgsqlDataSource dataSource;
    private readonly AdaptivePasswordHasher passwordHasher = new();

    public PostgreSqlGameDataStore(string socketPath, string username, string databaseName = "revora")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Path.GetFullPath(socketPath),
            Database = databaseName,
            Username = username,
            Pooling = true,
            MaxPoolSize = 20,
            Timeout = 5,
            CommandTimeout = 10,
            ApplicationName = "Revora.Host",
        };
        dataSource = new NpgsqlDataSourceBuilder(builder.ConnectionString).Build();
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT 1");
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<Guid?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT account_id,password_hash,status,locked_until,EXISTS(SELECT 1 FROM identity.account_bans b WHERE b.account_id=a.account_id AND b.revoked_at IS NULL AND (b.expires_at IS NULL OR b.expires_at>clock_timestamp())) " +
            "FROM identity.game_accounts a WHERE lower(username)=lower($1)");
        command.Parameters.AddWithValue(username);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            _ = passwordHasher.Verify(password, "invalid");
            return null;
        }

        Guid id = reader.GetGuid(0);
        bool allowed = reader.GetString(2) == "active" && (reader.IsDBNull(3) || reader.GetFieldValue<DateTimeOffset>(3) <= DateTimeOffset.UtcNow) && !reader.GetBoolean(4);
        return allowed && passwordHasher.Verify(password, reader.GetString(1)) != PasswordVerification.Failed ? id : null;
    }

    public async Task<Guid?> AuthenticateAsync(string username, ReadOnlyMemory<byte> passwordUtf8, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT account_id,password_hash,status,locked_until,EXISTS(SELECT 1 FROM identity.account_bans b WHERE b.account_id=a.account_id AND b.revoked_at IS NULL AND (b.expires_at IS NULL OR b.expires_at>clock_timestamp())) " +
            "FROM identity.game_accounts a WHERE lower(username)=lower($1)");
        command.Parameters.AddWithValue(username);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            _ = passwordHasher.Verify(passwordUtf8.Span, "invalid");
            return null;
        }

        Guid id = reader.GetGuid(0);
        bool allowed = reader.GetString(2) == "active" && (reader.IsDBNull(3) || reader.GetFieldValue<DateTimeOffset>(3) <= DateTimeOffset.UtcNow) && !reader.GetBoolean(4);
        return allowed && passwordHasher.Verify(passwordUtf8.Span, reader.GetString(1)) != PasswordVerification.Failed ? id : null;
    }

    public async Task<byte[]> IssueHandoffAsync(Guid accountId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        byte[] token = RandomNumberGenerator.GetBytes(32);
        byte[] digest = SHA256.HashData(token);
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO identity.handoff_tokens(token_digest,account_id,realm_id,expires_at) VALUES($1,$2,$3,clock_timestamp()+$4::interval)");
        command.Parameters.AddWithValue(digest);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(DevelopmentRealmId);
        command.Parameters.AddWithValue($"{(int)lifetime.TotalSeconds} seconds");
        await command.ExecuteNonQueryAsync(cancellationToken);
        return token;
    }

    public async Task<ulong> IssueLegacyHandoffAsync(Guid accountId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        Span<byte> tokenBytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(tokenBytes);
        ulong token = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(tokenBytes);
        if (token == 0) token = 1;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(tokenBytes, token);
        byte[] digest = SHA256.HashData(tokenBytes);
        CryptographicOperations.ZeroMemory(tokenBytes);
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO identity.handoff_tokens(token_digest,account_id,realm_id,expires_at) VALUES($1,$2,$3,clock_timestamp()+$4::interval)");
        command.Parameters.AddWithValue(digest);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(DevelopmentRealmId);
        command.Parameters.AddWithValue($"{(int)lifetime.TotalSeconds} seconds");
        await command.ExecuteNonQueryAsync(cancellationToken);
        CryptographicOperations.ZeroMemory(digest);
        return token;
    }

    public async Task<Guid?> ConsumeLegacyHandoffAsync(ulong token, CancellationToken cancellationToken)
    {
        Span<byte> tokenBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(tokenBytes, token);
        byte[] digest = SHA256.HashData(tokenBytes);
        CryptographicOperations.ZeroMemory(tokenBytes);
        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(
                "UPDATE identity.handoff_tokens SET consumed_at=clock_timestamp() WHERE token_digest=$1 AND realm_id=$2 AND consumed_at IS NULL AND expires_at>clock_timestamp() RETURNING account_id");
            command.Parameters.AddWithValue(digest);
            command.Parameters.AddWithValue(DevelopmentRealmId);
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result is Guid id ? id : null;
        }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    public async Task<Guid?> ConsumeHandoffAsync(byte[] token, CancellationToken cancellationToken)
    {
        if (token.Length != 32) return null;
        byte[] digest = SHA256.HashData(token);
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "UPDATE identity.handoff_tokens SET consumed_at=clock_timestamp() WHERE token_digest=$1 AND realm_id=$2 AND consumed_at IS NULL AND expires_at>clock_timestamp() RETURNING account_id");
        command.Parameters.AddWithValue(digest);
        command.Parameters.AddWithValue(DevelopmentRealmId);
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : null;
    }

    public async Task<CharacterState?> GetCharacterAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT character_id,name,map_id,x,y,direction,row_version,legacy_uid,mesh,profession,level,hair FROM game.characters WHERE account_id=$1");
        command.Parameters.AddWithValue(accountId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadCharacter(reader)
            : null;
    }

    public async Task<CharacterState> CreateCharacterAsync(Guid accountId, string name, CancellationToken cancellationToken)
    {
        return await CreateCharacterAsync(accountId, name, 1003, 10, cancellationToken);
    }

    public async Task<CharacterState> CreateCharacterAsync(Guid accountId, string name, ushort mesh, ushort profession, CancellationToken cancellationToken)
    {
        return await TryCreateCharacterAsync(accountId, name, mesh, profession, cancellationToken)
            ?? throw new InvalidOperationException("The account or case-insensitive character name already exists.");
    }

    public async Task<CharacterState?> TryCreateCharacterAsync(Guid accountId, string name, ushort mesh, ushort profession,
        CancellationToken cancellationToken)
    {
        if (profession == 50)
            return await TryCreateNinjaCharacterAsync(accountId, name, mesh, cancellationToken);
        Guid id = Guid.NewGuid();
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO game.characters(character_id,account_id,realm_id,name,map_id,x,y,direction,mesh,profession) " +
            "SELECT $1,$2,$3,$4,1002,430,378,0,$5,$6 WHERE NOT EXISTS " +
            "(SELECT 1 FROM game.characters WHERE account_id=$2 OR lower(name)=lower($4)) " +
            "ON CONFLICT DO NOTHING RETURNING row_version,legacy_uid,level,hair");
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(DevelopmentRealmId);
        command.Parameters.AddWithValue(name);
        command.Parameters.AddWithValue((int)mesh);
        command.Parameters.AddWithValue((short)profession);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new CharacterState(id, name, 1002, 430, 378, 0, reader.GetInt64(0), reader.GetInt32(1), mesh, profession,
            reader.GetInt16(2), reader.GetInt32(3));
    }

    private async Task<CharacterState?> TryCreateNinjaCharacterAsync(Guid accountId, string name, ushort mesh,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return await TryCreateNinjaCharacterOnceAsync(accountId, name, mesh, cancellationToken); }
            catch (PostgresException error) when (attempt < 2 &&
                error.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {
                await Task.Yield();
            }
        }
    }

    private async Task<CharacterState?> TryCreateNinjaCharacterOnceAsync(Guid accountId, string name, ushort mesh,
        CancellationToken cancellationToken)
    {
        Guid characterId = Guid.NewGuid();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await using (var serializeAccount = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended($1::text,5165))", connection, transaction))
            {
                serializeAccount.Parameters.AddWithValue(accountId);
                await serializeAccount.ExecuteScalarAsync(cancellationToken);
            }
            await using (var account = new NpgsqlCommand(
                "SELECT 1 FROM identity.game_accounts WHERE account_id=$1 FOR UPDATE", connection, transaction))
            {
                account.Parameters.AddWithValue(accountId);
                if (await account.ExecuteScalarAsync(cancellationToken) is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }
            }
            await using (var existing = new NpgsqlCommand(
                "SELECT 1 FROM game.characters WHERE account_id=$1", connection, transaction))
            {
                existing.Parameters.AddWithValue(accountId);
                if (await existing.ExecuteScalarAsync(cancellationToken) is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }
            }
            bool ninjaAuthorized;
            await using (var authorize = new NpgsqlCommand(
                "SELECT g.gate_state='enabled' AND (r.rollout_scope='global' OR " +
                "(r.rollout_scope='canary' AND EXISTS(SELECT 1 FROM gameplay.account_feature_canaries c " +
                "WHERE c.feature_key='ninja.native' AND c.account_id=$1))) " +
                "FROM gameplay.feature_gates g JOIN gameplay.feature_rollouts r USING(feature_key) " +
                "WHERE g.feature_key='ninja.native'", connection, transaction))
            {
                authorize.Parameters.AddWithValue(accountId);
                ninjaAuthorized = Convert.ToBoolean(await authorize.ExecuteScalarAsync(cancellationToken) ?? false);
            }
            if (!ninjaAuthorized)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            int strength, agility, vitality, spirit;
            await using (var stats = new NpgsqlCommand(
                "SELECT b.strength,b.agility,b.vitality,b.spirit FROM gameplay_reference.base_character_stats b " +
                "JOIN gameplay_reference.import_sources s USING(source_id) WHERE b.profession_family='ninja' " +
                "AND b.level=1 ORDER BY s.imported_at DESC,b.source_id DESC LIMIT 1", connection, transaction))
            await using (NpgsqlDataReader reader = await stats.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidDataException("The reviewed level-one Ninja stat row is missing.");
                strength = reader.GetInt32(0); agility = reader.GetInt32(1);
                vitality = reader.GetInt32(2); spirit = reader.GetInt32(3);
            }

            int legacyUid = 0, level = 0, hair = 0;
            bool inserted;
            await using (var insert = new NpgsqlCommand(
                "INSERT INTO game.characters(character_id,account_id,realm_id,name,map_id,x,y,direction,mesh,profession) " +
                "SELECT $1,$2,$3,$4,1002,430,378,0,$5,50 WHERE NOT EXISTS " +
                "(SELECT 1 FROM game.characters WHERE account_id=$2 OR lower(name)=lower($4)) " +
                "ON CONFLICT DO NOTHING RETURNING legacy_uid,level,hair", connection, transaction))
            {
                insert.Parameters.AddWithValue(characterId); insert.Parameters.AddWithValue(accountId);
                insert.Parameters.AddWithValue(DevelopmentRealmId); insert.Parameters.AddWithValue(name);
                insert.Parameters.AddWithValue((int)mesh);
                await using NpgsqlDataReader reader = await insert.ExecuteReaderAsync(cancellationToken);
                inserted = await reader.ReadAsync(cancellationToken);
                if (inserted)
                {
                    legacyUid = reader.GetInt32(0); level = reader.GetInt16(1); hair = reader.GetInt32(2);
                }
            }
            if (!inserted)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            int maximumHealth = checked(vitality * 24 + strength * 3 + agility * 3 + spirit * 3);
            await using (var initialize = new NpgsqlCommand(
                "WITH attributes AS (INSERT INTO game.character_attributes" +
                "(character_id,strength,agility,vitality,spirit,stamina) VALUES($1,$2,$3,$4,$5,100)), " +
                "vitals AS (INSERT INTO game.character_vitals(character_id,current_health,current_mana) VALUES($1,$6,0)), " +
                "stats AS (INSERT INTO game.character_stat_snapshots(character_id,character_row_version,inventory_revision," +
                "maximum_health,maximum_mana,minimum_attack,maximum_attack,defense,magic_attack,magic_defense,accuracy,dodge) " +
                "VALUES($1,1,0,$6,0,0,0,0,0,0,0,0)), " +
                "inventory AS (INSERT INTO game.inventory_profiles(character_id,slot_capacity) VALUES($1,40)), " +
                "progression AS (INSERT INTO game.character_progression(character_id,original_profession) VALUES($1,50)), " +
                "wallets AS (INSERT INTO game.character_wallets(character_id,currency_type,balance) " +
                "SELECT $1,currency,0 FROM (VALUES('silver'),('conquer-points'),('bound-conquer-points')) v(currency)) " +
                "INSERT INTO game.ninja_character_initialization(character_id,account_id,legacy_character_uid," +
                "stats_content_sha256,initialized_profession) VALUES($1,$7,$8," +
                "'dbc2076b14a98fc5303c916a9b033d347b99a09d3f7e482392c28d3c939e6281',50)",
                connection, transaction))
            {
                initialize.Parameters.AddWithValue(characterId);
                initialize.Parameters.AddWithValue(strength); initialize.Parameters.AddWithValue(agility);
                initialize.Parameters.AddWithValue(vitality); initialize.Parameters.AddWithValue(spirit);
                initialize.Parameters.AddWithValue(maximumHealth); initialize.Parameters.AddWithValue(accountId);
                initialize.Parameters.AddWithValue(legacyUid);
                await initialize.ExecuteNonQueryAsync(cancellationToken);
            }
            if (await IsNinjaAccountCanaryAsync(connection, transaction, accountId, cancellationToken))
            {
                (long ItemType, short Slot)[] requestedItems =
                [
                    (601301, 1), (601301, 2), (112000, 3), (123000, 4), (135000, 5)
                ];
                var grantedItemUids = new int[requestedItems.Length];
                for (int index = 0; index < requestedItems.Length; index++)
                {
                    Guid itemInstanceId = Guid.NewGuid();
                    await using (var item = new NpgsqlCommand(
                        "INSERT INTO game.item_instances(item_instance_id,owner_character_id,item_source_id,item_type," +
                        "durability,maximum_durability,bound) SELECT $1,$2,i.source_id,i.item_type," +
                        "i.maximum_durability,i.maximum_durability,true FROM gameplay_reference.item_types i " +
                        "WHERE i.source_id='51650035-0000-4e56-8000-000000000001' AND i.item_type=$3 " +
                        "RETURNING legacy_item_uid", connection, transaction))
                    {
                        item.Parameters.AddWithValue(itemInstanceId);
                        item.Parameters.AddWithValue(characterId);
                        item.Parameters.AddWithValue(requestedItems[index].ItemType);
                        object? value = await item.ExecuteScalarAsync(cancellationToken);
                        if (value is null)
                            throw new InvalidDataException("A reviewed Ninja canary starter item is missing.");
                        grantedItemUids[index] = Convert.ToInt32(value);
                    }
                    await using (var location = new NpgsqlCommand(
                        "INSERT INTO game.item_locations(item_instance_id,owner_character_id,location_kind,slot) " +
                        "VALUES($1,$2,'inventory',$3)", connection, transaction))
                    {
                        location.Parameters.AddWithValue(itemInstanceId);
                        location.Parameters.AddWithValue(characterId);
                        location.Parameters.AddWithValue(requestedItems[index].Slot);
                        if (await location.ExecuteNonQueryAsync(cancellationToken) != 1)
                            throw new InvalidDataException("A reviewed Ninja canary starter item location is missing.");
                    }
                }
                await using (var skills = new NpgsqlCommand(
                    "INSERT INTO game.character_skills(character_id,magic_source_id,magic_type,level,experience) " +
                    "SELECT $1,m.source_id,m.magic_type,0,0 FROM gameplay_reference.magic_types m WHERE m.level=0 " +
                    "AND m.magic_type IN(6000,6001,6002,6003,6004,6010,6011)", connection, transaction))
                {
                    skills.Parameters.AddWithValue(characterId);
                    if (await skills.ExecuteNonQueryAsync(cancellationToken) != 7)
                        throw new InvalidDataException("The reviewed Ninja canary skill set is incomplete.");
                }
                await using (var wallet = new NpgsqlCommand(
                    "UPDATE game.character_wallets SET balance=1000000,row_version=row_version+1," +
                    "updated_at=clock_timestamp() WHERE character_id=$1 AND currency_type='conquer-points'", connection, transaction))
                {
                    wallet.Parameters.AddWithValue(characterId);
                    if (await wallet.ExecuteNonQueryAsync(cancellationToken) != 1)
                        throw new InvalidDataException("The Ninja canary test wallet is missing.");
                }
                await using (var grant = new NpgsqlCommand(
                    "INSERT INTO game.ninja_canary_test_grants(character_id,account_id,legacy_character_uid," +
                    "legacy_item_uids,conquer_points_granted) VALUES($1,$2,$3,$4,1000000)", connection, transaction))
                {
                    grant.Parameters.AddWithValue(characterId);
                    grant.Parameters.AddWithValue(accountId);
                    grant.Parameters.AddWithValue(legacyUid);
                    grant.Parameters.AddWithValue(grantedItemUids);
                    if (await grant.ExecuteNonQueryAsync(cancellationToken) != 1)
                        throw new InvalidDataException("The reviewed Ninja canary starter grant was incomplete.");
                }
            }
            await transaction.CommitAsync(cancellationToken);
            return new CharacterState(characterId, name, 1002, 430, 378, 0, 1, legacyUid, mesh, 50,
                checked((short)level), hair);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<bool> IsNinjaAccountCanaryAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid accountId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM gameplay.account_feature_canaries " +
            "WHERE feature_key='ninja.native' AND account_id=$1)", connection, transaction);
        command.Parameters.AddWithValue(accountId);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<CharacterState?> MoveAsync(Guid characterId, int x, int y, short direction, long expectedVersion, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "UPDATE game.characters SET x=$2,y=$3,direction=$4,updated_at=clock_timestamp(),row_version=row_version+1 WHERE character_id=$1 AND row_version=$5 RETURNING name,map_id,row_version,legacy_uid,mesh,profession,level,hair");
        command.Parameters.AddWithValue(characterId);
        command.Parameters.AddWithValue(x);
        command.Parameters.AddWithValue(y);
        command.Parameters.AddWithValue(direction);
        command.Parameters.AddWithValue(expectedVersion);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CharacterState(characterId, reader.GetString(0), reader.GetInt32(1), x, y, direction, reader.GetInt64(2),
                reader.GetInt32(3), checked((ushort)reader.GetInt32(4)), checked((ushort)reader.GetInt16(5)), reader.GetInt16(6), reader.GetInt32(7))
            : null;
    }

    public async Task<CharacterState?> TransitionMapAsync(Guid characterId, int mapId, int x, int y,
        short direction, long expectedVersion, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "UPDATE game.characters SET map_id=$2,x=$3,y=$4,direction=$5,updated_at=clock_timestamp(),row_version=row_version+1 " +
            "WHERE character_id=$1 AND row_version=$6 RETURNING name,row_version,legacy_uid,mesh,profession,level,hair");
        command.Parameters.AddWithValue(characterId);
        command.Parameters.AddWithValue(mapId);
        command.Parameters.AddWithValue(x);
        command.Parameters.AddWithValue(y);
        command.Parameters.AddWithValue(direction);
        command.Parameters.AddWithValue(expectedVersion);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CharacterState(characterId, reader.GetString(0), mapId, x, y, direction, reader.GetInt64(1),
                reader.GetInt32(2), checked((ushort)reader.GetInt32(3)), checked((ushort)reader.GetInt16(4)),
                reader.GetInt16(5), reader.GetInt32(6))
            : null;
    }

    public async Task<GameplayReferenceRows> LoadPortalNpcReferencesAsync(
        IReadOnlyCollection<int> reviewedMapIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reviewedMapIds);
        int[] maps = reviewedMapIds.Distinct().Order().ToArray();
        if (maps.Length is < 1 or > 32 || maps.Any(id => id <= 0))
            throw new ArgumentOutOfRangeException(nameof(reviewedMapIds));

        var portals = new List<PortalReferenceRow>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT DISTINCT ON (p.legacy_id) p.legacy_id,p.source_map,p.source_x,p.source_y,p.destination_map,p.destination_x,p.destination_y " +
            "FROM gameplay_reference.portals p JOIN gameplay_reference.import_sources s USING(source_id) " +
            "WHERE s.classification='reviewed-reference-data' AND p.source_map=ANY($1) AND p.destination_map=ANY($1) " +
            "ORDER BY p.legacy_id,s.imported_at DESC,p.source_id DESC"))
        {
            command.Parameters.AddWithValue(maps);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                portals.Add(new(reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
                    reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6)));
        }

        var npcs = new List<NpcReferenceRow>();
        await using (NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT DISTINCT ON (n.legacy_uid) n.legacy_uid,n.npc_type,n.flags,n.face,n.map_id,n.x,n.y " +
            "FROM gameplay_reference.npcs n JOIN gameplay_reference.import_sources s USING(source_id) " +
            "WHERE s.classification='reviewed-reference-data' AND n.map_id=ANY($1) " +
            "AND n.active_month=0 AND n.active_begin=0 AND n.active_end=0 " +
            "ORDER BY n.legacy_uid,s.imported_at DESC,n.source_id DESC"))
        {
            command.Parameters.AddWithValue(maps);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                npcs.Add(new(checked((uint)reader.GetInt64(0)), checked((ushort)reader.GetInt64(1)),
                    checked((ushort)reader.GetInt32(2)), checked((ushort)reader.GetInt32(3)),
                    reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6)));
        }
        return new(portals, npcs);
    }

    public async Task OpenSessionAsync(Guid sessionId, Guid accountId, Guid connectionId, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO identity.game_sessions(session_id,account_id,realm_id,connection_id) VALUES($1,$2,$3,$4)");
        command.Parameters.AddWithValue(sessionId);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(DevelopmentRealmId);
        command.Parameters.AddWithValue(connectionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CloseSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "UPDATE identity.game_sessions SET disconnected_at=clock_timestamp(),disconnect_reason=$2 WHERE session_id=$1 AND disconnected_at IS NULL");
        command.Parameters.AddWithValue(sessionId);
        command.Parameters.AddWithValue(reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> HasGameplayRoleAsync(Guid accountId, Guid characterId, string role,
        CancellationToken cancellationToken)
    {
        if (role is not "gm") throw new ArgumentOutOfRangeException(nameof(role));
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM game.gameplay_role_assignments " +
            "WHERE account_id=$1 AND character_id=$2 AND role=$3 AND revoked_at IS NULL)");
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(characterId);
        command.Parameters.AddWithValue(role);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<int> MigrationVersionAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT coalesce(max(version),0) FROM operations.schema_migrations");
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<bool> IsCharacterFeatureEnabledAsync(AccountId accountId, int legacyCharacterUid,
        string featureKey, CancellationToken cancellationToken)
    {
        if (!string.Equals(featureKey, "world.player.equipment.visibility", StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(featureKey));
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM gameplay.feature_gates g " +
            "JOIN gameplay.feature_rollouts r USING(feature_key) " +
            "JOIN gameplay.feature_canaries f USING(feature_key) " +
            "JOIN game.characters c ON c.character_id=f.character_id " +
            "WHERE g.feature_key=$3 AND g.gate_state='enabled' AND r.rollout_scope='canary' " +
            "AND g.required_migration<=(SELECT coalesce(max(version),0) FROM operations.schema_migrations) " +
            "AND f.account_id=$1 AND f.legacy_character_uid=$2 " +
            "AND c.character_id=f.character_id AND c.account_id=$1 AND c.legacy_uid=$2)");
        command.Parameters.AddWithValue(accountId.Value);
        command.Parameters.AddWithValue(legacyCharacterUid);
        command.Parameters.AddWithValue(featureKey);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<string> OperationalStatusAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT 'migration='||coalesce((SELECT max(version) FROM operations.schema_migrations),0)||';backup='||coalesce((SELECT status FROM operations.backup_status WHERE backup_name='postgresql'),'unknown')");
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable";
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();

    private static CharacterState ReadCharacter(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2),
        reader.GetInt32(3), reader.GetInt32(4), reader.GetInt16(5), reader.GetInt64(6), reader.GetInt32(7),
        checked((ushort)reader.GetInt32(8)), checked((ushort)reader.GetInt16(9)), reader.GetInt16(10), reader.GetInt32(11));
}

public sealed record CharacterState(Guid Id, string Name, int MapId, int X, int Y, short Direction, long Version,
    int LegacyUid, ushort Mesh, ushort Profession, short Level, int Hair);

public sealed record PortalReferenceRow(long LegacyId, int SourceMap, int SourceX, int SourceY,
    int DestinationMap, int DestinationX, int DestinationY);

public sealed record NpcReferenceRow(uint LegacyUid, ushort Type, ushort Flags, ushort Face,
    int MapId, int X, int Y);

public sealed record GameplayReferenceRows(
    IReadOnlyList<PortalReferenceRow> Portals, IReadOnlyList<NpcReferenceRow> Npcs);
