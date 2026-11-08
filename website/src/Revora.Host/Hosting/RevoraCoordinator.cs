using Revora.Host.Logging;

namespace Revora.Host.Hosting;

public sealed class RevoraCoordinator
{
    private readonly IPersistenceRuntime _persistence;
    private readonly IRevoraRuntimeComponent _accountServer;
    private readonly IRevoraRuntimeComponent _gameServer;
    private readonly IPlayerCountProvider _players;
    private readonly SafeConsoleLog _log;
    private readonly SemaphoreSlim _shutdownRequested = new(0, 1);
    private int _shutdownSignalAccepted;
    private int _state = (int)HealthState.Stopped;
    private int _readyMarkerWritten;
    private int _unexpectedFailure;

    public RevoraCoordinator(
        IPersistenceRuntime persistence,
        IRevoraRuntimeComponent accountServer,
        IRevoraRuntimeComponent gameServer,
        IPlayerCountProvider players,
        SafeConsoleLog log)
    {
        _persistence = persistence;
        _accountServer = accountServer;
        _gameServer = gameServer;
        _players = players;
        _log = log;
    }

    public HealthState State => (HealthState)Volatile.Read(ref _state);

    public int PlayerCount => _players.PlayerCount;

    public bool UnexpectedFailure => Volatile.Read(ref _unexpectedFailure) != 0;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SetState(HealthState.Starting);
        _log.Info("Host", "Startup", "Valid configuration loaded; starting runtime components.");

        try
        {
            await _persistence.StartAsync(cancellationToken).ConfigureAwait(false);
            RequireReady(_persistence);
            _log.Info("Persistence", "DatabaseConnected", "Persistence dependency is healthy.");

            await _accountServer.StartAsync(cancellationToken).ConfigureAwait(false);
            RequireReady(_accountServer);
            _log.Info("AccountServer", "AccountListenerReady", "Account listener is active.");

            await _gameServer.StartAsync(cancellationToken).ConfigureAwait(false);
            RequireReady(_gameServer);
            _log.Info("GameServer", "GameListenerReady", "Game listener is active.");

            SetState(HealthState.Ready);
            if (Interlocked.Exchange(ref _readyMarkerWritten, 1) == 0)
            {
                _log.ReadyMarker();
            }


            _ = MonitorUnexpectedCompletionAsync(_accountServer);
            _ = MonitorUnexpectedCompletionAsync(_gameServer);
        }
        catch
        {
            SetState(HealthState.Failed);
            await StopStartedComponentsAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public Task WaitForShutdownRequestAsync(CancellationToken cancellationToken) =>
        _shutdownRequested.WaitAsync(cancellationToken);

    public void RequestShutdown(string source)
    {
        if (Interlocked.Exchange(ref _shutdownSignalAccepted, 1) == 0)
        {
            _shutdownRequested.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        _log.Audit("SaveRequested", "Persistence save requested through the operator console.");
        await _persistence.SaveAsync(cancellationToken).ConfigureAwait(false);
        _log.Info("Persistence", "PersistenceFlushComplete", "Pending persistence work was flushed.");
    }

    public IReadOnlyList<ComponentHealth> GetHealth() =>
        new[]
        {
            new ComponentHealth("Process", State, "Revora.Host lifecycle"),
            _persistence.GetHealth(),
            _accountServer.GetHealth(),
            _gameServer.GetHealth(),
        };

    public async Task StopAsync(TimeSpan timeout)
    {
        if (State == HealthState.Stopped)
        {
            return;
        }

        SetState(HealthState.Draining);
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            _log.Info("Host", "ListenerDrainStarted", "Stopping Account and Game listeners and draining active connections.");
            await Task.WhenAll(
                _accountServer.StopAsync(cancellation.Token),
                _gameServer.StopAsync(cancellation.Token)).ConfigureAwait(false);
            _log.Info("Host", "ListenerDrainComplete", "Account and Game listeners stopped and active connections drained.");
            await SaveAsync(cancellation.Token).ConfigureAwait(false);
            _log.Info("Host", "PersistenceStopStarted", "Closing the persistence dependency.");
            await _persistence.StopAsync(cancellation.Token).ConfigureAwait(false);
            SetState(HealthState.Stopped);
            _log.StoppedMarker();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            SetState(HealthState.Failed);
            _log.Error("Host", "ShutdownTimeout", "Graceful shutdown exceeded its configured timeout.");
            throw;
        }
    }

    private static void RequireReady(IRevoraRuntimeComponent component)
    {
        var health = component.GetHealth();
        if (health.State != HealthState.Ready)
        {
            throw new InvalidOperationException($"{component.Name} did not reach Ready state: {health.Detail}");
        }
    }

    private async Task StopStartedComponentsAsync(CancellationToken cancellationToken)
    {
        foreach (var component in new IRevoraRuntimeComponent[] { _gameServer, _accountServer, _persistence })
        {
            try
            {
                await component.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _log.Error(component.Name, "StartupRollbackFailed", exception.GetType().Name);
            }
        }
    }

    private void SetState(HealthState state) => Volatile.Write(ref _state, (int)state);

    private async Task MonitorUnexpectedCompletionAsync(IRevoraRuntimeComponent component)
    {
        try
        {
            await component.Completion.ConfigureAwait(false);
        }
        catch
        {
            // The public event intentionally omits raw exception details.
        }

        if (State is HealthState.Draining or HealthState.Stopped)
        {
            return;
        }

        Interlocked.Exchange(ref _unexpectedFailure, 1);
        SetState(HealthState.Failed);
        _log.Error(component.Name, "UnexpectedComponentFailure", "A runtime component stopped unexpectedly.");
        RequestShutdown($"{component.Name} failure");
    }
}
