using Revora.Persistence.PostgreSql;

namespace Revora.Host.Hosting;

public sealed class PostgreSqlPersistenceRuntime(PostgreSqlGameDataStore store, params IAsyncDisposable[] adjuncts) : IPersistenceRuntime
{
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int state = (int)HealthState.Stopped;
    private int migrationVersion;
    private string operationalStatus = "unavailable";

    public string Name => "Persistence";
    public Task Completion => completion.Task;
    public PostgreSqlGameDataStore Store { get; } = store;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref state, (int)HealthState.Starting);
        if (!await Store.CanConnectAsync(cancellationToken)) throw new InvalidOperationException("PostgreSQL readiness failed.");
        migrationVersion = await Store.MigrationVersionAsync(cancellationToken);
        if (migrationVersion < 2) throw new InvalidOperationException("PostgreSQL migration level is below the reviewed minimum.");
        operationalStatus = await Store.OperationalStatusAsync(cancellationToken);
        Volatile.Write(ref state, (int)HealthState.Ready);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (IAsyncDisposable adjunct in adjuncts.Reverse()) await adjunct.DisposeAsync();
        await Store.DisposeAsync();
        Volatile.Write(ref state, (int)HealthState.Stopped);
        completion.TrySetResult();
    }

    public ComponentHealth GetHealth() => new(Name, (HealthState)Volatile.Read(ref state), $"PostgreSQL reachable; {operationalStatus}.");
}
