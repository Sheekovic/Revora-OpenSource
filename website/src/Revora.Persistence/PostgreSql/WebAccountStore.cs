using Npgsql;
using Revora.Application.Authentication;
using System.Security.Cryptography;

namespace Revora.Persistence.PostgreSql;

public sealed class WebAccountStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly AdaptivePasswordHasher hasher = new();

    public WebAccountStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConnectionStringBuilder.ApplicationName = "Revora.Web";
        builder.ConnectionStringBuilder.Timeout = 5;
        builder.ConnectionStringBuilder.CommandTimeout = 10;
        builder.ConnectionStringBuilder.MaxPoolSize = 20;
        dataSource = builder.Build();
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT 1");
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }


    public async Task<IssuedGameHandoff?> IssueGameHandoffAsync(Guid accountId,
        System.Net.IPAddress remoteAddress, uint serverId, TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        if (serverId == 0 || lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        uint handle;
        do { handle = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(RandomNumberGenerator.GetBytes(4)); }
        while (handle == 0);
        Span<byte> handleBytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(handleBytes, handle);
        byte[] digest = SHA256.HashData(handleBytes);
        CryptographicOperations.ZeroMemory(handleBytes);
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(
                "INSERT INTO identity.handoff_tokens(token_digest,account_id,realm_id,expires_at,server_id,remote_address) " +
                "SELECT $1,a.account_id,c.realm_id,$2,$3,$4 FROM identity.game_accounts a " +
                "JOIN game.characters c ON c.account_id=a.account_id WHERE a.account_id=$5 AND a.status='active'");
            command.Parameters.AddWithValue(digest);
            command.Parameters.AddWithValue(expiresAt);
            command.Parameters.AddWithValue((long)serverId);
            command.Parameters.AddWithValue(remoteAddress);
            command.Parameters.AddWithValue(accountId);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1
                ? new IssuedGameHandoff(handle, expiresAt) : null;
        }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    public async Task<ConsumedGameHandoff?> ConsumeGameHandoffAsync(uint handle,
        System.Net.IPAddress remoteAddress, uint serverId, CancellationToken cancellationToken)
    {
        if (handle == 0 || serverId == 0) return null;
        Span<byte> handleBytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(handleBytes, handle);
        byte[] digest = SHA256.HashData(handleBytes);
        CryptographicOperations.ZeroMemory(handleBytes);
        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(
                "WITH consumed AS (UPDATE identity.handoff_tokens h SET consumed_at=clock_timestamp() " +
                "FROM identity.game_accounts a WHERE h.token_digest=$1 AND h.account_id=a.account_id " +
                "AND h.server_id=$2 AND h.remote_address=$3 AND h.consumed_at IS NULL " +
                "AND h.expires_at>clock_timestamp() AND a.status='active' " +
                "AND NOT EXISTS(SELECT 1 FROM identity.account_bans b WHERE b.account_id=a.account_id " +
                "AND b.revoked_at IS NULL AND (b.expires_at IS NULL OR b.expires_at>clock_timestamp())) " +
                "RETURNING h.account_id,a.legacy_uid,h.expires_at) " +
                "SELECT consumed.legacy_uid,c.legacy_uid,consumed.expires_at FROM consumed " +
                "JOIN game.characters c ON c.account_id=consumed.account_id");
            command.Parameters.AddWithValue(digest);
            command.Parameters.AddWithValue((long)serverId);
            command.Parameters.AddWithValue(remoteAddress);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            long accountId = reader.GetInt64(0);
            int characterId = reader.GetInt32(1);
            if (accountId is <= 0 or > uint.MaxValue || characterId <= 0) return null;
            return new ConsumedGameHandoff((uint)accountId, (uint)characterId,
                reader.GetFieldValue<DateTimeOffset>(2));
        }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    public async Task<RegistrationResult> RegisterAsync(string username, string password, IReadOnlyList<byte[]> recoveryDigests, CancellationToken cancellationToken)
    {
        string normalized = UsernamePolicy.Normalize(username);
        Guid accountId = Guid.NewGuid();
        Guid securityStamp = Guid.NewGuid();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (NpgsqlCommand account = connection.CreateCommand())
            {
                account.Transaction = transaction;
                account.CommandText = "INSERT INTO identity.game_accounts(account_id,username,normalized_username,password_hash,password_algorithm,password_version,status,security_stamp,public_registration) VALUES($1,$2,$3,$4,'pbkdf2-sha512',1,'active',$5,true)";
                account.Parameters.AddWithValue(accountId);
                account.Parameters.AddWithValue(username);
                account.Parameters.AddWithValue(normalized);
                account.Parameters.AddWithValue(hasher.HashLegacyCompatible(password));
                account.Parameters.AddWithValue(securityStamp);
                await account.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (NpgsqlCommand security = connection.CreateCommand())
            {
                security.Transaction = transaction;
                security.CommandText = "WITH role_insert AS (INSERT INTO identity.account_roles(account_id,role) VALUES($1,'Player') RETURNING account_id) INSERT INTO identity.account_security(account_id) SELECT account_id FROM role_insert";
                security.Parameters.AddWithValue(accountId);
                await security.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (byte[] digest in recoveryDigests)
            {
                await using NpgsqlCommand code = connection.CreateCommand();
                code.Transaction = transaction;
                code.CommandText = "INSERT INTO identity.recovery_codes(code_id,account_id,generation,code_digest) VALUES($1,$2,1,$3)";
                code.Parameters.AddWithValue(Guid.NewGuid());
                code.Parameters.AddWithValue(accountId);
                code.Parameters.AddWithValue(digest);
                await code.ExecuteNonQueryAsync(cancellationToken);
            }
            await AppendAuditAsync(connection, transaction, accountId, "account.registered", "Public self-registration", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RegistrationResult(accountId, username, securityStamp, "active");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RegistrationResult.Duplicate;
        }
    }

    public async Task<WebAccountIdentity?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        string normalized = UsernamePolicy.TryNormalize(username, out string value) ? value : "invalid";
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT account_id,username,password_hash,status,locked_until,security_stamp,created_at FROM identity.game_accounts WHERE normalized_username=$1");
        command.Parameters.AddWithValue(normalized);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            _ = hasher.Verify(password, "invalid");
            return null;
        }
        Guid accountId = reader.GetGuid(0);
        string storedUsername = reader.GetString(1);
        Guid storedStamp = reader.GetGuid(5);
        DateTimeOffset createdAt = reader.GetFieldValue<DateTimeOffset>(6);
        string storedStatus = reader.GetString(3);
        bool allowed = reader.GetString(3) == "active" && (reader.IsDBNull(4) || reader.GetFieldValue<DateTimeOffset>(4) <= DateTimeOffset.UtcNow);
        bool valid = allowed && hasher.Verify(password, reader.GetString(2)) != PasswordVerification.Failed;
        await reader.DisposeAsync();
        await using NpgsqlCommand outcome = dataSource.CreateCommand(valid
            ? "UPDATE identity.game_accounts SET failed_login_count=0,locked_until=NULL,updated_at=clock_timestamp() WHERE account_id=$1"
            : "UPDATE identity.game_accounts SET failed_login_count=failed_login_count+1,locked_until=CASE WHEN failed_login_count+1>=8 THEN clock_timestamp()+interval '15 minutes' ELSE locked_until END,updated_at=clock_timestamp() WHERE account_id=$1");
        outcome.Parameters.AddWithValue(accountId);
        await outcome.ExecuteNonQueryAsync(cancellationToken);
        return valid ? new WebAccountIdentity(accountId, storedUsername, storedStamp, createdAt, storedStatus) : null;
    }

    public async Task CreateSessionAsync(byte[] digest, WebAccountIdentity account, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("INSERT INTO identity.web_sessions(session_digest,account_id,security_stamp,expires_at) VALUES($1,$2,$3,$4)");
        command.Parameters.AddWithValue(digest); command.Parameters.AddWithValue(account.AccountId); command.Parameters.AddWithValue(account.SecurityStamp); command.Parameters.AddWithValue(expiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ValidateSessionAsync(byte[] digest, Guid accountId, Guid securityStamp, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("UPDATE identity.web_sessions s SET last_seen_at=clock_timestamp() FROM identity.game_accounts a WHERE s.session_digest=$1 AND s.account_id=$2 AND s.security_stamp=$3 AND a.account_id=s.account_id AND a.security_stamp=$3 AND a.status='active' AND s.revoked_at IS NULL AND s.expires_at>clock_timestamp() RETURNING 1");
        command.Parameters.AddWithValue(digest); command.Parameters.AddWithValue(accountId); command.Parameters.AddWithValue(securityStamp);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task RevokeSessionAsync(byte[] digest, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("UPDATE identity.web_sessions SET revoked_at=clock_timestamp() WHERE session_digest=$1 AND revoked_at IS NULL");
        command.Parameters.AddWithValue(digest); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AccountDashboard?> GetDashboardAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT a.username,a.status,a.created_at,(SELECT count(*) FROM identity.recovery_codes r JOIN identity.account_security s ON s.account_id=r.account_id AND s.recovery_generation=r.generation WHERE r.account_id=a.account_id AND r.consumed_at IS NULL),(SELECT count(*) FROM identity.security_answers q WHERE q.account_id=a.account_id) FROM identity.game_accounts a WHERE a.account_id=$1");
        command.Parameters.AddWithValue(accountId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var dashboard = new AccountDashboard(reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2), reader.GetInt64(3), reader.GetInt64(4), []);
        await reader.DisposeAsync();
        await using NpgsqlCommand characters = dataSource.CreateCommand("SELECT legacy_uid,name FROM game.characters WHERE account_id=$1 ORDER BY legacy_uid");
        characters.Parameters.AddWithValue(accountId);
        await using NpgsqlDataReader characterReader = await characters.ExecuteReaderAsync(cancellationToken);
        var list = new List<CharacterSummary>();
        while (await characterReader.ReadAsync(cancellationToken)) list.Add(new CharacterSummary(characterReader.GetInt32(0), characterReader.GetString(1)));
        return dashboard with { Characters = list };
    }

    public async Task<bool> ChangePasswordAsync(Guid accountId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand select = connection.CreateCommand(); select.Transaction = transaction;
        select.CommandText = "SELECT password_hash FROM identity.game_accounts WHERE account_id=$1 FOR UPDATE"; select.Parameters.AddWithValue(accountId);
        string? oldHash = (string?)await select.ExecuteScalarAsync(cancellationToken);
        if (oldHash is null || hasher.Verify(currentPassword, oldHash) == PasswordVerification.Failed) { await transaction.RollbackAsync(cancellationToken); return false; }
        await ReplacePasswordAsync(connection, transaction, accountId, hasher.HashLegacyCompatible(newPassword), cancellationToken);
        await AppendAuditAsync(connection, transaction, accountId, "account.password_changed", "Authenticated password change", cancellationToken);
        await transaction.CommitAsync(cancellationToken); return true;
    }

    public async Task<bool> ConsumeRecoveryCodeAsync(string username, byte[] codeDigest, byte[] authorizationDigest, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        string normalized = UsernamePolicy.TryNormalize(username, out string value) ? value : "invalid";
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "WITH consumed AS (UPDATE identity.recovery_codes r SET consumed_at=clock_timestamp() FROM identity.game_accounts a,identity.account_security s WHERE a.normalized_username=$1 AND a.account_id=r.account_id AND s.account_id=a.account_id AND r.generation=s.recovery_generation AND r.code_digest=$2 AND r.consumed_at IS NULL RETURNING r.account_id) INSERT INTO identity.reset_authorizations(token_digest,account_id,reason,expires_at) SELECT $3,account_id,'recovery-code',$4 FROM consumed RETURNING account_id";
        command.Parameters.AddWithValue(normalized); command.Parameters.AddWithValue(codeDigest); command.Parameters.AddWithValue(authorizationDigest); command.Parameters.AddWithValue(expiresAt);
        bool result = await command.ExecuteScalarAsync(cancellationToken) is not null;
        if (result) await transaction.CommitAsync(cancellationToken); else await transaction.RollbackAsync(cancellationToken);
        return result;
    }

    public async Task<bool> ResetPasswordAsync(byte[] authorizationDigest, string password, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand consume = connection.CreateCommand(); consume.Transaction = transaction;
        consume.CommandText = "UPDATE identity.reset_authorizations SET consumed_at=clock_timestamp() WHERE token_digest=$1 AND consumed_at IS NULL AND expires_at>clock_timestamp() RETURNING account_id";
        consume.Parameters.AddWithValue(authorizationDigest);
        object? value = await consume.ExecuteScalarAsync(cancellationToken); if (value is not Guid accountId) { await transaction.RollbackAsync(cancellationToken); return false; }
        await ReplacePasswordAsync(connection, transaction, accountId, hasher.HashLegacyCompatible(password), cancellationToken);
        await AppendAuditAsync(connection, transaction, accountId, "account.password_reset", "User-controlled recovery reset", cancellationToken);
        await transaction.CommitAsync(cancellationToken); return true;
    }

    public async Task RecordNetworkAsync(Guid accountId, byte[] prefixDigest, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("INSERT INTO identity.network_history(account_id,prefix_digest,expires_at) VALUES($1,$2,$3) ON CONFLICT(account_id,prefix_digest) DO UPDATE SET last_seen_at=clock_timestamp(),expires_at=EXCLUDED.expires_at");
        command.Parameters.AddWithValue(accountId); command.Parameters.AddWithValue(prefixDigest); command.Parameters.AddWithValue(expiresAt); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordDeviceAsync(Guid accountId, byte[] digest, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("INSERT INTO identity.recognized_devices(device_id,account_id,token_digest,expires_at) VALUES($1,$2,$3,$4)");
        command.Parameters.AddWithValue(Guid.NewGuid()); command.Parameters.AddWithValue(accountId); command.Parameters.AddWithValue(digest); command.Parameters.AddWithValue(expiresAt); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RevokeOtherSessionsAsync(Guid accountId, byte[] currentDigest, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("UPDATE identity.web_sessions SET revoked_at=clock_timestamp() WHERE account_id=$1 AND session_digest<>$2 AND revoked_at IS NULL");
        command.Parameters.AddWithValue(accountId); command.Parameters.AddWithValue(currentDigest); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("WITH network AS (DELETE FROM identity.network_history WHERE expires_at<=clock_timestamp() RETURNING account_id), devices AS (DELETE FROM identity.recognized_devices WHERE expires_at<=clock_timestamp() OR (revoked_at IS NOT NULL AND revoked_at<clock_timestamp()-interval '30 days') RETURNING account_id), sessions AS (DELETE FROM identity.web_sessions WHERE expires_at<=clock_timestamp() OR (revoked_at IS NOT NULL AND revoked_at<clock_timestamp()-interval '7 days') RETURNING account_id), resets AS (DELETE FROM identity.reset_authorizations WHERE expires_at<=clock_timestamp() OR consumed_at<clock_timestamp()-interval '7 days' RETURNING account_id), pending_update AS (UPDATE identity.pending_recoveries SET status='expired' WHERE status='pending' AND expires_at<=clock_timestamp() RETURNING request_id) DELETE FROM identity.pending_recoveries WHERE status IN ('rejected','expired','consumed') AND requested_at<clock_timestamp()-interval '30 days'");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ConfigureQuestionsAsync(Guid accountId, IReadOnlyList<(short QuestionId, string Answer)> answers, CancellationToken cancellationToken)
    {
        if (answers.Count != 3 || answers.Select(static item => item.QuestionId).Distinct().Count() != 3) throw new ArgumentException("Exactly three distinct questions are required.", nameof(answers));
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (NpgsqlCommand remove = connection.CreateCommand()) { remove.Transaction = transaction; remove.CommandText = "DELETE FROM identity.security_answers WHERE account_id=$1"; remove.Parameters.AddWithValue(accountId); await remove.ExecuteNonQueryAsync(cancellationToken); }
        foreach ((short questionId, string answer) in answers)
        {
            await using NpgsqlCommand insert = connection.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO identity.security_answers(account_id,question_id,answer_hash) VALUES($1,$2,$3)";
            insert.Parameters.AddWithValue(accountId); insert.Parameters.AddWithValue(questionId); insert.Parameters.AddWithValue(hasher.HashSupportingFactor(NormalizeAnswer(answer))); await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<QuestionRecoveryResult> CreateQuestionRecoveryAsync(string username, IReadOnlyDictionary<short, string> answers, byte[]? deviceDigest, byte[] networkDigest, byte[] requestDigest, CancellationToken cancellationToken)
    {
        string normalized = UsernamePolicy.TryNormalize(username, out string value) ? value : "invalid";
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand lookup = connection.CreateCommand(); lookup.Transaction = transaction;
        lookup.CommandText = "SELECT account_id FROM identity.game_accounts WHERE normalized_username=$1 AND status='active'"; lookup.Parameters.AddWithValue(normalized);
        object? accountValue = await lookup.ExecuteScalarAsync(cancellationToken);
        if (accountValue is not Guid accountId) { _ = hasher.Verify("dummy-answer", "invalid"); await transaction.RollbackAsync(cancellationToken); return QuestionRecoveryResult.Generic; }
        await using NpgsqlCommand questions = connection.CreateCommand(); questions.Transaction = transaction;
        questions.CommandText = "SELECT question_id,answer_hash FROM identity.security_answers WHERE account_id=$1 ORDER BY question_id"; questions.Parameters.AddWithValue(accountId);
        await using NpgsqlDataReader reader = await questions.ExecuteReaderAsync(cancellationToken);
        int count = 0; bool allMatch = true;
        while (await reader.ReadAsync(cancellationToken)) { short id = reader.GetInt16(0); count++; allMatch &= answers.TryGetValue(id, out string? answer) && hasher.Verify(NormalizeAnswer(answer), reader.GetString(1)) != PasswordVerification.Failed; }
        await reader.DisposeAsync();
        if (count != 3 || !allMatch) { await transaction.RollbackAsync(cancellationToken); return QuestionRecoveryResult.Generic; }
        bool device = false;
        if (deviceDigest is not null) { await using NpgsqlCommand check = connection.CreateCommand(); check.Transaction = transaction; check.CommandText = "SELECT EXISTS(SELECT 1 FROM identity.recognized_devices WHERE account_id=$1 AND token_digest=$2 AND revoked_at IS NULL AND expires_at>clock_timestamp())"; check.Parameters.AddWithValue(accountId); check.Parameters.AddWithValue(deviceDigest); device = Convert.ToBoolean(await check.ExecuteScalarAsync(cancellationToken)); }
        await using NpgsqlCommand networkCheck = connection.CreateCommand(); networkCheck.Transaction = transaction; networkCheck.CommandText = "SELECT EXISTS(SELECT 1 FROM identity.network_history WHERE account_id=$1 AND prefix_digest=$2 AND expires_at>clock_timestamp())"; networkCheck.Parameters.AddWithValue(accountId); networkCheck.Parameters.AddWithValue(networkDigest); bool network = Convert.ToBoolean(await networkCheck.ExecuteScalarAsync(cancellationToken));
        string support = device ? "questions-device" : network ? "questions-network" : "insufficient";
        Guid requestId = Guid.NewGuid();
        await using NpgsqlCommand pending = connection.CreateCommand(); pending.Transaction = transaction;
        pending.CommandText = "INSERT INTO identity.pending_recoveries(request_id,request_digest,account_id,support_category,available_after,expires_at) VALUES($1,$2,$3,$4,clock_timestamp()+interval '15 minutes',clock_timestamp()+interval '24 hours')";
        pending.Parameters.AddWithValue(requestId); pending.Parameters.AddWithValue(requestDigest); pending.Parameters.AddWithValue(accountId); pending.Parameters.AddWithValue(support); await pending.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken); return new QuestionRecoveryResult(true, support, requestId);
    }

    public async Task<bool> RedeemPendingRecoveryAsync(byte[] requestDigest, byte[] authorizationDigest, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "WITH accepted AS (UPDATE identity.pending_recoveries SET status='consumed' WHERE request_digest=$1 AND status IN ('approved','pending') AND expires_at>clock_timestamp() AND (status='approved' OR (support_category IN ('questions-device','questions-network') AND available_after<=clock_timestamp())) RETURNING account_id) INSERT INTO identity.reset_authorizations(token_digest,account_id,reason,expires_at) SELECT $2,account_id,'questions-device',$3 FROM accepted RETURNING account_id";
        command.Parameters.AddWithValue(requestDigest); command.Parameters.AddWithValue(authorizationDigest); command.Parameters.AddWithValue(expiresAt);
        bool success = await command.ExecuteScalarAsync(cancellationToken) is not null; if (success) await transaction.CommitAsync(cancellationToken); else await transaction.RollbackAsync(cancellationToken); return success;
    }

    public async Task RegenerateRecoveryCodesAsync(Guid accountId, IReadOnlyList<byte[]> digests, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand generation = connection.CreateCommand(); generation.Transaction = transaction;
        generation.CommandText = "UPDATE identity.account_security SET recovery_generation=recovery_generation+1,updated_at=clock_timestamp() WHERE account_id=$1 RETURNING recovery_generation"; generation.Parameters.AddWithValue(accountId);
        int next = Convert.ToInt32(await generation.ExecuteScalarAsync(cancellationToken));
        foreach (byte[] digest in digests) { await using NpgsqlCommand insert = connection.CreateCommand(); insert.Transaction = transaction; insert.CommandText = "INSERT INTO identity.recovery_codes(code_id,account_id,generation,code_digest) VALUES($1,$2,$3,$4)"; insert.Parameters.AddWithValue(Guid.NewGuid()); insert.Parameters.AddWithValue(accountId); insert.Parameters.AddWithValue(next); insert.Parameters.AddWithValue(digest); await insert.ExecuteNonQueryAsync(cancellationToken); }
        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync() => await dataSource.DisposeAsync();

    private static async Task ReplacePasswordAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId, string hash, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "WITH account_update AS (UPDATE identity.game_accounts SET password_hash=$2,password_algorithm='pbkdf2-sha512',password_version=1,security_stamp=gen_random_uuid(),failed_login_count=0,locked_until=NULL,force_password_reset=false,updated_at=clock_timestamp(),row_version=row_version+1 WHERE account_id=$1 RETURNING account_id), sessions AS (UPDATE identity.web_sessions SET revoked_at=clock_timestamp() WHERE account_id=$1 AND revoked_at IS NULL RETURNING account_id), resets AS (UPDATE identity.reset_authorizations SET consumed_at=clock_timestamp() WHERE account_id=$1 AND consumed_at IS NULL RETURNING account_id), handoffs AS (UPDATE identity.handoff_tokens SET consumed_at=clock_timestamp() WHERE account_id=$1 AND consumed_at IS NULL RETURNING account_id), launches AS (UPDATE identity.launch_tickets SET consumed_at=clock_timestamp() WHERE account_id=$1 AND consumed_at IS NULL RETURNING account_id), devices AS (UPDATE identity.recognized_devices SET revoked_at=clock_timestamp() WHERE account_id=$1 AND revoked_at IS NULL RETURNING account_id) SELECT count(*) FROM account_update";
        command.Parameters.AddWithValue(accountId); command.Parameters.AddWithValue(hash); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeAnswer(string answer)
    {
        string normalized = string.Join(' ', answer.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length is < 2 or > 100) throw new ArgumentException("Security answers must contain 2 to 100 characters.", nameof(answer));
        return normalized;
    }

    private static async Task AppendAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId, string action, string reason, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand audit = connection.CreateCommand(); audit.Transaction = transaction;
        audit.CommandText = "INSERT INTO operations.audit_events(actor_type,actor_id,action,target_type,target_id,reason,correlation_id) VALUES('player',$1,$2,'account',$3,$4,$5)";
        audit.Parameters.AddWithValue(accountId); audit.Parameters.AddWithValue(action); audit.Parameters.AddWithValue(accountId.ToString()); audit.Parameters.AddWithValue(reason); audit.Parameters.AddWithValue(Guid.NewGuid()); await audit.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record IssuedGameHandoff(uint Handle, DateTimeOffset ExpiresAt);
public sealed record ConsumedGameHandoff(uint AccountId, uint CharacterId, DateTimeOffset ExpiresAt);

public static class UsernamePolicy
{
    public static bool TryNormalize(string? username, out string normalized)
    {
        normalized = string.Empty;
        if (username is null || username.Length is < 4 or > 15 || username.Any(static value => !char.IsAsciiLetterOrDigit(value))) return false;
        normalized = username.ToLowerInvariant(); return true;
    }
    public static string Normalize(string username) => TryNormalize(username, out string normalized) ? normalized : throw new ArgumentException("Username must contain 4 to 15 ASCII letters or digits.", nameof(username));
}

public sealed record RegistrationResult(Guid AccountId, string Username, Guid SecurityStamp, string Status)
{
    public static readonly RegistrationResult Duplicate = new(Guid.Empty, string.Empty, Guid.Empty, "duplicate");
    public bool Created => AccountId != Guid.Empty;
}
public sealed record WebAccountIdentity(Guid AccountId, string Username, Guid SecurityStamp, DateTimeOffset CreatedAt, string Status);
public sealed record CharacterSummary(int LegacyUid, string Name);
public sealed record AccountDashboard(string Username, string Status, DateTimeOffset CreatedAt, long RecoveryCodesRemaining, long SecurityQuestionCount, IReadOnlyList<CharacterSummary> Characters);
public sealed record QuestionRecoveryResult(bool Created, string SupportCategory, Guid RequestId)
{
    public static readonly QuestionRecoveryResult Generic = new(false, "generic", Guid.Empty);
}
