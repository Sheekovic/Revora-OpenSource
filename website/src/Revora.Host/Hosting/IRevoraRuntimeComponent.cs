namespace Revora.Host.Hosting;

public interface IRevoraRuntimeComponent
{
    string Name { get; }

    Task Completion { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    ComponentHealth GetHealth();
}
