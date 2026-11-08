namespace Revora.Host.Hosting;

public sealed class InMemoryPersistenceRuntime : IPersistenceRuntime
{
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _state = (int)HealthState.Stopped;
    private int _saveCount;

    public string Name => "Persistence";
    public Task Completion => _completion.Task;
    public int SaveCount => Volatile.Read(ref _saveCount);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref _state, (int)HealthState.Ready);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _saveCount);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref _state, (int)HealthState.Stopped);
        _completion.TrySetResult();
        return Task.CompletedTask;
    }

    public ComponentHealth GetHealth() =>
        new(Name, (HealthState)Volatile.Read(ref _state), $"In-memory development store; flushes={SaveCount}.");
}
