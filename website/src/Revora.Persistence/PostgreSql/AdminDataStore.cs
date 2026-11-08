using Npgsql;
using Revora.Application.Authentication;
using System.Security.Cryptography;

namespace Revora.Persistence.PostgreSql;

public sealed class AdminDataStore : IAsyncDisposable
{
    private static readonly Guid DevelopmentRealmId = Guid.Parse("4d1e7aac-e046-48bd-88b9-642f7eafef83");
    private readonly NpgsqlDataSource dataSource;

    public AdminDataStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConnectionStringBuilder.ApplicationName = "Revora.Admin";
        builder.ConnectionStringBuilder.Timeout = 5;
        builder.ConnectionStringBuilder.CommandTimeout = 10;
        builder.ConnectionStringBuilder.Pooling = true;
        builder.ConnectionStringBuilder.MaxPoolSize = 10;
        dataSource = builder.Build();
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT 1");
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<AdminIdentity?> FindAdministratorAsync(string username, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT admin_id, username, password_hash, status, totp_secret_ciphertext, totp_enabled, must_change_password, must_enroll_mfa, locked_until " +
            "FROM admin.administrators WHERE lower(username)=lower($1)");
        command.Parameters.AddWithValue(username);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new AdminIdentity(
            reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.IsDBNull(4) ? null : (byte[])reader[4], reader.GetBoolean(5), reader.GetBoolean(6),
            reader.GetBoolean(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8));
    }

    public async Task<Guid> CreateBootstrapAdministratorAsync(string username, string passwordHash, CancellationToken cancellationToken)
    {
        Guid id = Guid.NewGuid();
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO admin.administrators(admin_id,username,password_hash,password_algorithm,password_version,must_change_password,must_enroll_mfa) " +
            "VALUES($1,$2,$3,'pbkdf2-sha512',1,true,true)");
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(username);
        command.Parameters.AddWithValue(passwordHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await AppendAuditAsync(null, "admin.bootstrap_created", "administrator", id.ToString(), "One-time local bootstrap", cancellationToken);
        return id;
    }

    public async Task RecordFailedLoginAsync(Guid? adminId, CancellationToken cancellationToken)
    {
        if (adminId is null) return;
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "UPDATE admin.administrators SET failed_login_count=failed_login_count+1, " +
            "locked_until=CASE WHEN failed_login_count+1>=5 THEN clock_timestamp()+interval '15 minutes' ELSE locked_until END, " +
            "updated_at=clock_timestamp(), row_version=row_version+1 WHERE admin_id=$1");
        command.Parameters.AddWithValue(adminId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CompleteEnrollmentAsync(Guid adminId, string passwordHash, byte[] protectedTotpSecret, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            "UPDATE admin.administrators SET password_hash=$2,password_algorithm='pbkdf2-sha512',password_version=1," +
            "totp_secret_ciphertext=$3,totp_enabled=true,must_change_password=false,must_enroll_mfa=false," +
            "failed_login_count=0,locked_until=NULL,updated_at=clock_timestamp(),row_version=row_version+1 WHERE admin_id=$1";
        command.Transaction = transaction;
        command.Parameters.AddWithValue(adminId);
        command.Parameters.AddWithValue(passwordHash);
        command.Parameters.AddWithValue(protectedTotpSecret);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Administrator enrollment target was not found.");
        await using NpgsqlCommand audit = connection.CreateCommand();
        audit.Transaction = transaction;
        audit.CommandText = "INSERT INTO operations.audit_events(actor_type,actor_id,action,target_type,target_id,reason,correlation_id) VALUES('administrator',$1,$2,$3,$4,$5,$6)";
        audit.Parameters.AddWithValue(adminId);
        audit.Parameters.AddWithValue("admin.mfa_enrolled");
        audit.Parameters.AddWithValue("administrator");
        audit.Parameters.AddWithValue(adminId.ToString());
        audit.Parameters.AddWithValue("Bootstrap enrollment completed");
        audit.Parameters.AddWithValue(Guid.NewGuid());
        await audit.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Guid> CreateSessionAsync(Guid adminId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        Guid sessionId = Guid.NewGuid();
        byte[] digest = System.Security.Cryptography.SHA256.HashData(sessionId.ToByteArray());
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO admin.admin_sessions(session_digest,admin_id,absolute_expires_at) VALUES($1,$2,clock_timestamp()+$3::interval)");
        command.Parameters.AddWithValue(digest);
        command.Parameters.AddWithValue(adminId);
        command.Parameters.AddWithValue($"{(int)lifetime.TotalSeconds} seconds");
        await command.ExecuteNonQueryAsync(cancellationToken);
        return sessionId;
    }

    public async Task<bool> ValidateSessionAsync(Guid sessionId, Guid adminId, CancellationToken cancellationToken)
    {
        byte[] digest = System.Security.Cryptography.SHA256.HashData(sessionId.ToByteArray());
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "UPDATE admin.admin_sessions SET last_seen_at=clock_timestamp() WHERE session_digest=$1 AND admin_id=$2 AND revoked_at IS NULL " +
            "AND absolute_expires_at>clock_timestamp() RETURNING 1");
        command.Parameters.AddWithValue(digest);
        command.Parameters.AddWithValue(adminId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        byte[] digest = System.Security.Cryptography.SHA256.HashData(sessionId.ToByteArray());
        await using NpgsqlCommand command = dataSource.CreateCommand("UPDATE admin.admin_sessions SET revoked_at=clock_timestamp() WHERE session_digest=$1");
        command.Parameters.AddWithValue(digest);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameAccountSummary>> SearchAccountsAsync(string query, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT account_id,username,status,created_at FROM identity.game_accounts WHERE lower(username) LIKE lower($1) ORDER BY username LIMIT 50");
        command.Parameters.AddWithValue($"%{query}%");
        var results = new List<GameAccountSummary>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GameAccountSummary(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return results;
    }

    public async Task<Guid?> AuthenticateGameAccountAsync(string username, string password, AdaptivePasswordHasher hasher, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT account_id,password_hash,status,locked_until,force_password_reset," +
            "EXISTS(SELECT 1 FROM identity.account_bans b WHERE b.account_id=a.account_id AND b.revoked_at IS NULL AND (b.expires_at IS NULL OR b.expires_at>clock_timestamp())) " +
            "FROM identity.game_accounts a WHERE lower(username)=lower($1)");
        command.Parameters.AddWithValue(username);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            _ = hasher.Verify(password, "invalid");
            return null;
        }

        bool allowed = reader.GetString(2) == "active" &&
            (reader.IsDBNull(3) || reader.GetFieldValue<DateTimeOffset>(3) <= DateTimeOffset.UtcNow) &&
            !reader.GetBoolean(4) && !reader.GetBoolean(5);
        return allowed && hasher.Verify(password, reader.GetString(1)) != PasswordVerification.Failed ? reader.GetGuid(0) : null;
    }

    public async Task<(byte[] Ticket, Guid LaunchId)> IssueLaunchTicketAsync(Guid accountId, Guid launcherSessionId, string clientBuildId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        if (clientBuildId.Length is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(clientBuildId));
        byte[] ticket = RandomNumberGenerator.GetBytes(LaunchTicket.Size);
        byte[] digest = SHA256.HashData(ticket);
        Guid launchId = Guid.NewGuid();
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO identity.launch_tickets(ticket_digest,account_id,realm_id,launcher_session_id,client_build_id,intended_launch_id,expires_at) " +
            "VALUES($1,$2,$3,$4,$5,$6,clock_timestamp()+$7::interval)");
        command.Parameters.AddWithValue(digest);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(DevelopmentRealmId);
        command.Parameters.AddWithValue(launcherSessionId);
        command.Parameters.AddWithValue(clientBuildId);
        command.Parameters.AddWithValue(launchId);
        command.Parameters.AddWithValue($"{(int)lifetime.TotalSeconds} seconds");
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return (ticket, launchId);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(ticket);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    public async Task<bool> ConsumeLaunchTicketAsync(byte[] ticket, Guid launcherSessionId, Guid launchId, string clientBuildId, CancellationToken cancellationToken)
    {
        if (ticket.Length != LaunchTicket.Size || launcherSessionId == Guid.Empty || launchId == Guid.Empty || clientBuildId.Length is < 1 or > 128) return false;
        byte[] digest = SHA256.HashData(ticket);
        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand(
                "UPDATE identity.launch_tickets SET consumed_at=clock_timestamp() WHERE ticket_digest=$1 AND launcher_session_id=$2 " +
                "AND intended_launch_id=$3 AND client_build_id=$4 AND consumed_at IS NULL AND expires_at>clock_timestamp() RETURNING 1");
            command.Parameters.AddWithValue(digest);
            command.Parameters.AddWithValue(launcherSessionId);
            command.Parameters.AddWithValue(launchId);
            command.Parameters.AddWithValue(clientBuildId);
            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    public async Task<Guid> CreateGameAccountAsync(Guid actorId, string username, string passwordHash, CancellationToken cancellationToken)
    {
        Guid accountId = Guid.NewGuid();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO identity.game_accounts(account_id,username,password_hash,password_algorithm,password_version,force_password_reset) VALUES($1,$2,$3,'pbkdf2-sha512',1,true)";
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(username);
        command.Parameters.AddWithValue(passwordHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await InsertAuditAsync(connection, transaction, actorId, "game_account.created", "game_account", accountId.ToString(), "Administrative account creation", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return accountId;
    }

    public Task DisableGameAccountAsync(Guid actorId, Guid accountId, string reason, CancellationToken cancellationToken) =>
        MutateAccountAsync(actorId, accountId, "disabled", false, "game_account.disabled", reason, cancellationToken);

    public Task UnlockGameAccountAsync(Guid actorId, Guid accountId, string reason, CancellationToken cancellationToken) =>
        MutateAccountAsync(actorId, accountId, "active", true, "game_account.unlocked", reason, cancellationToken);

    public async Task ForcePasswordResetAsync(Guid actorId, Guid accountId, string passwordHash, string reason, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE identity.game_accounts SET password_hash=$2,password_algorithm='pbkdf2-sha512',password_version=1,force_password_reset=true,updated_at=clock_timestamp(),row_version=row_version+1 WHERE account_id=$1";
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(passwordHash);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Game account was not found.");
        await InsertAuditAsync(connection, transaction, actorId, "game_account.password_reset", "game_account", accountId.ToString(), reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task BanGameAccountAsync(Guid actorId, Guid accountId, string reason, DateTimeOffset? expiresAt, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE identity.game_accounts SET status='banned',updated_at=clock_timestamp(),row_version=row_version+1 WHERE account_id=$1";
        update.Parameters.AddWithValue(accountId);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Game account was not found.");
        await using NpgsqlCommand ban = connection.CreateCommand();
        ban.Transaction = transaction;
        ban.CommandText = "INSERT INTO identity.account_bans(ban_id,account_id,reason,actor_admin_id,expires_at) VALUES($1,$2,$3,$4,$5)";
        ban.Parameters.AddWithValue(Guid.NewGuid());
        ban.Parameters.AddWithValue(accountId);
        ban.Parameters.AddWithValue(reason);
        ban.Parameters.AddWithValue(actorId);
        ban.Parameters.AddWithValue((object?)expiresAt ?? DBNull.Value);
        await ban.ExecuteNonQueryAsync(cancellationToken);
        await InsertAuditAsync(connection, transaction, actorId, "game_account.banned", "game_account", accountId.ToString(), reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditSummary>> RecentAuditAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT occurred_at,action,target_type,target_id,correlation_id FROM operations.audit_events ORDER BY audit_id DESC LIMIT 100");
        var results = new List<AuditSummary>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AuditSummary(reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetGuid(4)));
        }

        return results;
    }

    public async Task<IReadOnlyList<PendingRecoverySummary>> PendingRecoveriesAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand("SELECT request_id,requested_at,support_category FROM identity.pending_recoveries WHERE status='pending' AND expires_at>clock_timestamp() ORDER BY requested_at LIMIT 50");
        var results = new List<PendingRecoverySummary>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) results.Add(new PendingRecoverySummary(reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2)));
        return results;
    }

    public async Task ReviewRecoveryAsync(Guid requestId, Guid administratorId, bool approve, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE identity.pending_recoveries SET status=$2,reviewed_at=clock_timestamp(),reviewed_by=$3 WHERE request_id=$1 AND status='pending' AND expires_at>clock_timestamp()";
        command.Parameters.AddWithValue(requestId); command.Parameters.AddWithValue(approve ? "approved" : "rejected"); command.Parameters.AddWithValue(administratorId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("The pending recovery request is unavailable.");
        await InsertAuditAsync(connection, transaction, administratorId, approve ? "recovery.approved" : "recovery.rejected", "pending_recovery", requestId.ToString(), "Sanitized administrator recovery review", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<string> GetOperationalStatusAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT 'migration='||(SELECT max(version) FROM operations.schema_migrations)||';backup='||" +
            "coalesce((SELECT status FROM operations.backup_status WHERE backup_name='postgresql'),'unknown')||';realm='||" +
            "(SELECT CASE WHEN maintenance THEN 'maintenance' ELSE 'online' END FROM game.realms LIMIT 1)");
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable";
    }

    public async Task AppendAuditAsync(Guid? actorId, string action, string targetType, string targetId, string reason, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO operations.audit_events(actor_type,actor_id,action,target_type,target_id,reason,correlation_id) VALUES('administrator',$1,$2,$3,$4,$5,$6)");
        command.Parameters.AddWithValue((object?)actorId ?? DBNull.Value);
        command.Parameters.AddWithValue(action);
        command.Parameters.AddWithValue(targetType);
        command.Parameters.AddWithValue(targetId);
        command.Parameters.AddWithValue(reason);
        command.Parameters.AddWithValue(Guid.NewGuid());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();

    private async Task MutateAccountAsync(Guid actorId, Guid accountId, string status, bool clearLock, string action, string reason, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = clearLock
            ? "UPDATE identity.game_accounts SET status=$2,failed_login_count=0,locked_until=NULL,updated_at=clock_timestamp(),row_version=row_version+1 WHERE account_id=$1"
            : "UPDATE identity.game_accounts SET status=$2,updated_at=clock_timestamp(),row_version=row_version+1 WHERE account_id=$1";
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(status);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Game account was not found.");
        await InsertAuditAsync(connection, transaction, actorId, action, "game_account", accountId.ToString(), reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid actorId, string action, string targetType, string targetId, string reason, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand audit = connection.CreateCommand();
        audit.Transaction = transaction;
        audit.CommandText = "INSERT INTO operations.audit_events(actor_type,actor_id,action,target_type,target_id,reason,correlation_id) VALUES('administrator',$1,$2,$3,$4,$5,$6)";
        audit.Parameters.AddWithValue(actorId);
        audit.Parameters.AddWithValue(action);
        audit.Parameters.AddWithValue(targetType);
        audit.Parameters.AddWithValue(targetId);
        audit.Parameters.AddWithValue(reason);
        audit.Parameters.AddWithValue(Guid.NewGuid());
        await audit.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record AdminIdentity(Guid Id, string Username, string PasswordHash, string Status, byte[]? ProtectedTotpSecret, bool TotpEnabled, bool MustChangePassword, bool MustEnrollMfa, DateTimeOffset? LockedUntil);
public sealed record GameAccountSummary(Guid Id, string Username, string Status, DateTimeOffset CreatedAt);
public sealed record AuditSummary(DateTimeOffset OccurredAt, string Action, string TargetType, string TargetId, Guid CorrelationId);
public sealed record PendingRecoverySummary(Guid RequestId, DateTimeOffset RequestedAt, string SupportCategory);
