namespace Revora.Host.Hosting;

public interface IPersistenceRuntime : IRevoraRuntimeComponent
{
    Task SaveAsync(CancellationToken cancellationToken);
}

