namespace Revora.Host.Hosting;

/// <summary>
/// Safe fail-closed adapter used until real persistence and listener composition exists.
/// It prevents the production executable from emitting READY prematurely.
/// </summary>
public sealed class UnavailableRuntimeComponent : IPersistenceRuntime, IPlayerCountProvider
{
    private readonly string _name;
    private HealthState _state = HealthState.Stopped;

    public UnavailableRuntimeComponent(string name)
    {
        _name = name;
    }

    public string Name => _name;

    public Task Completion => Task.CompletedTask;

    public int PlayerCount => 0;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _state = HealthState.Failed;
        throw new InvalidOperationException($"{_name} has not been implemented; refusing to report readiness.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _state = HealthState.Stopped;
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException("Persistence is not available."));

    public ComponentHealth GetHealth() =>
        new(_name, _state, "Implementation pending; fail-closed.");
}
