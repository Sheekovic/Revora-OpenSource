using System.Data;
using Npgsql;

namespace Revora.Persistence.PostgreSql;

public sealed record HonorMutationResult(bool Applied, bool Replay, int Balance, string Reason);

/// <summary>Serializable, database-idempotent Honor mutation boundary.</summary>
public sealed class PostgreSqlCityGuardStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;

    public PostgreSqlCityGuardStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConnectionStringBuilder.ApplicationName = "Revora.CityGuard";
        builder.ConnectionStringBuilder.Timeout = 5;
        builder.ConnectionStringBuilder.CommandTimeout = 10;
        builder.ConnectionStringBuilder.MaxPoolSize = 10;
        dataSource = builder.Build();
    }

    public async Task<HonorMutationResult> AwardBountyAsync(Guid transactionId, string correlationKey,
        Guid bountyCycleId, Guid killerAccountId, Guid killerCharacterId, Guid wantedAccountId,
        Guid wantedCharacterId, int points, int pkPoints, int mapId, int instanceId,
        string decisionReason, CancellationToken cancellationToken)
    {
        if (transactionId == Guid.Empty || bountyCycleId == Guid.Empty || points is < 1 or > 5 ||
            pkPoints < 100 || mapId <= 0 || instanceId < 0 || correlationKey.Length is < 16 or > 200)
            throw new ArgumentOutOfRangeException(nameof(points));
        return await SerializableRetry(async (connection, transaction) =>
        {
            await using (var replay = new NpgsqlCommand(
                "SELECT balance_after FROM city_guard.honor_ledger WHERE correlation_key=$1", connection, transaction))
            {
                replay.Parameters.AddWithValue(correlationKey);
                object? prior = await replay.ExecuteScalarAsync(cancellationToken);
                if (prior is int priorBalance) return new HonorMutationResult(false, true, priorBalance, "replay");
            }
            await using (var claim = new NpgsqlCommand(
                "UPDATE city_guard.bounty_cycles SET claimed_at=clock_timestamp(),claimed_by_account_id=$2," +
                "claimed_by_character_id=$3,ended_at=clock_timestamp(),end_reason='eligible-capture' " +
                "WHERE bounty_cycle_id=$1 AND ended_at IS NULL AND claimed_at IS NULL AND wanted_account_id=$4 " +
                "AND wanted_character_id=$5", connection, transaction))
            {
                claim.Parameters.AddWithValue(bountyCycleId); claim.Parameters.AddWithValue(killerAccountId);
                claim.Parameters.AddWithValue(killerCharacterId); claim.Parameters.AddWithValue(wantedAccountId);
                claim.Parameters.AddWithValue(wantedCharacterId);
                if (await claim.ExecuteNonQueryAsync(cancellationToken) != 1)
                    return new HonorMutationResult(false, false, 0, "cycle-already-resolved");
            }
            int balance;
            await using (var update = new NpgsqlCommand(
                "INSERT INTO city_guard.account_honor_balances(account_id,balance) VALUES($1,$2) " +
                "ON CONFLICT(account_id) DO UPDATE SET balance=city_guard.account_honor_balances.balance+EXCLUDED.balance," +
                "row_version=city_guard.account_honor_balances.row_version+1,updated_at=clock_timestamp() RETURNING balance",
                connection, transaction))
            {
                update.Parameters.AddWithValue(killerAccountId); update.Parameters.AddWithValue(points);
                balance = Convert.ToInt32(await update.ExecuteScalarAsync(cancellationToken));
            }
            await using (var ledger = new NpgsqlCommand(
                "INSERT INTO city_guard.honor_ledger(transaction_id,account_id,character_id,balance_delta,balance_after," +
                "reason_type,bounty_cycle_id,wanted_account_id,wanted_character_id,killer_account_id,killer_character_id," +
                "pk_points_at_death,map_id,instance_id,eligibility_decision,decision_reason,correlation_key) " +
                "VALUES($1,$2,$3,$4,$5,'bounty-award',$6,$7,$8,$2,$3,$9,$10,$11,'award',$12,$13)", connection, transaction))
            {
                ledger.Parameters.AddWithValue(transactionId); ledger.Parameters.AddWithValue(killerAccountId);
                ledger.Parameters.AddWithValue(killerCharacterId); ledger.Parameters.AddWithValue(points);
                ledger.Parameters.AddWithValue(balance); ledger.Parameters.AddWithValue(bountyCycleId);
                ledger.Parameters.AddWithValue(wantedAccountId); ledger.Parameters.AddWithValue(wantedCharacterId);
                ledger.Parameters.AddWithValue(pkPoints); ledger.Parameters.AddWithValue(mapId);
                ledger.Parameters.AddWithValue(instanceId); ledger.Parameters.AddWithValue(decisionReason);
                ledger.Parameters.AddWithValue(correlationKey);
                await ledger.ExecuteNonQueryAsync(cancellationToken);
            }
            return new HonorMutationResult(true, false, balance, "awarded");
        }, cancellationToken);
    }

    public async Task<HonorMutationResult> RedeemStatusRewardAsync(Guid redemptionId, Guid ledgerTransactionId,
        string correlationKey, Guid accountId, Guid characterId, Guid rewardId,
        CancellationToken cancellationToken)
    {
        if (redemptionId == Guid.Empty || ledgerTransactionId == Guid.Empty || correlationKey.Length is < 16 or > 200)
            throw new ArgumentOutOfRangeException(nameof(redemptionId));
        return await SerializableRetry(async (connection, transaction) =>
        {
            await using (var replay = new NpgsqlCommand(
                "SELECT b.balance FROM city_guard.reward_redemptions r JOIN city_guard.account_honor_balances b " +
                "ON b.account_id=r.account_id WHERE r.correlation_key=$1", connection, transaction))
            {
                replay.Parameters.AddWithValue(correlationKey);
                object? prior = await replay.ExecuteScalarAsync(cancellationToken);
                if (prior is int priorBalance) return new HonorMutationResult(false, true, priorBalance, "replay");
            }
            int cost;
            string rewardKey;
            await using (var catalog = new NpgsqlCommand(
                "SELECT honor_cost,reward_key FROM city_guard.reward_catalog WHERE reward_id=$1 AND enabled AND grant_kind='status' FOR SHARE",
                connection, transaction))
            {
                catalog.Parameters.AddWithValue(rewardId);
                await using NpgsqlDataReader reader = await catalog.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) return new HonorMutationResult(false, false, 0, "reward-unavailable");
                cost = reader.GetInt32(0); rewardKey = reader.GetString(1);
            }
            int balance;
            await using (var debit = new NpgsqlCommand(
                "UPDATE city_guard.account_honor_balances SET balance=balance-$2,row_version=row_version+1," +
                "updated_at=clock_timestamp() WHERE account_id=$1 AND balance >= $2 RETURNING balance", connection, transaction))
            {
                debit.Parameters.AddWithValue(accountId); debit.Parameters.AddWithValue(cost);
                object? result = await debit.ExecuteScalarAsync(cancellationToken);
                if (result is not int value) return new HonorMutationResult(false, false, 0, "insufficient-honor");
                balance = value;
            }
            await using (var grant = new NpgsqlCommand(
                "INSERT INTO city_guard.achievement_progress(account_id,achievement_key,progress) " +
                "SELECT $1,grant_reference,1 FROM city_guard.reward_catalog WHERE reward_id=$2 " +
                "ON CONFLICT(account_id,achievement_key) DO UPDATE SET progress=GREATEST(city_guard.achievement_progress.progress,1)," +
                "row_version=city_guard.achievement_progress.row_version+1,updated_at=clock_timestamp()", connection, transaction))
            {
                grant.Parameters.AddWithValue(accountId); grant.Parameters.AddWithValue(rewardId);
                if (await grant.ExecuteNonQueryAsync(cancellationToken) != 1)
                    throw new InvalidOperationException($"Status reward {rewardKey} could not be granted.");
            }
            await using (var receipt = new NpgsqlCommand(
                "INSERT INTO city_guard.reward_redemptions(redemption_id,account_id,character_id,reward_id,honor_cost,correlation_key,status) " +
                "VALUES($1,$2,$3,$4,$5,$6,'committed')", connection, transaction))
            {
                receipt.Parameters.AddWithValue(redemptionId); receipt.Parameters.AddWithValue(accountId);
                receipt.Parameters.AddWithValue(characterId); receipt.Parameters.AddWithValue(rewardId);
                receipt.Parameters.AddWithValue(cost); receipt.Parameters.AddWithValue(correlationKey);
                await receipt.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var ledger = new NpgsqlCommand(
                "INSERT INTO city_guard.honor_ledger(transaction_id,account_id,character_id,balance_delta,balance_after," +
                "reason_type,eligibility_decision,decision_reason,correlation_key) VALUES($1,$2,$3,$4,$5," +
                "'reward-redemption','not-applicable','atomic-status-redemption',$6)", connection, transaction))
            {
                ledger.Parameters.AddWithValue(ledgerTransactionId); ledger.Parameters.AddWithValue(accountId);
                ledger.Parameters.AddWithValue(characterId); ledger.Parameters.AddWithValue(-cost);
                ledger.Parameters.AddWithValue(balance); ledger.Parameters.AddWithValue(correlationKey + ":ledger");
                await ledger.ExecuteNonQueryAsync(cancellationToken);
            }
            return new HonorMutationResult(true, false, balance, "redeemed");
        }, cancellationToken);
    }

    private async Task<T> SerializableRetry<T>(Func<NpgsqlConnection,NpgsqlTransaction,Task<T>> operation,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                T result = await operation(connection, transaction);
                await transaction.CommitAsync(cancellationToken); return result;
            }
            catch (PostgresException error) when (attempt < 2 && error.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation)
            { await transaction.RollbackAsync(cancellationToken); await Task.Yield(); }
        }
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
