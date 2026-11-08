using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Revora.Host.Hosting;

public sealed class LoopbackTcpRuntimeComponent : IRevoraRuntimeComponent, IPlayerCountProvider
{
    private readonly IPAddress _address;
    private readonly int _port;
    private readonly int _maximumConnections;
    private readonly ConcurrentDictionary<long, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<long, Task> _clientTasks = new();
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TcpListener? _listener;
    private CancellationTokenSource? _lifetime;
    private Task? _acceptTask;
    private long _nextConnectionId;
    private int _state = (int)HealthState.Stopped;
    private int _intentionalStop;

    public LoopbackTcpRuntimeComponent(
        string name,
        IPAddress address,
        int port,
        int maximumConnections)
    {
        if (!IPAddress.IsLoopback(address))
        {
            throw new ArgumentException("Development listeners must bind to loopback.", nameof(address));
        }

        Name = name;
        _address = address;
        _port = port;
        _maximumConnections = maximumConnections;
    }

    public string Name { get; }
    public Task Completion => _completion.Task;
    public int PlayerCount => _clients.Count;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref _state, (int)HealthState.Starting);
        _lifetime = new CancellationTokenSource();
        _listener = new TcpListener(_address, _port);
        try
        {
            _listener.Start(_maximumConnections);
        }
        catch
        {
            Volatile.Write(ref _state, (int)HealthState.Failed);
            _lifetime.Dispose();
            throw;
        }
        Volatile.Write(ref _state, (int)HealthState.Ready);
        _acceptTask = AcceptLoopAsync(_listener, _lifetime.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if ((HealthState)Volatile.Read(ref _state) == HealthState.Stopped)
        {
            return;
        }

        Interlocked.Exchange(ref _intentionalStop, 1);
        Volatile.Write(ref _state, (int)HealthState.Draining);
        _listener?.Stop();

        if (_acceptTask is not null)
        {
            await IgnoreExpectedStopAsync(_acceptTask).ConfigureAwait(false);
        }

        // This protocol-neutral harness has no session state or outbound work to drain.
        // Closing the raw sockets is therefore its complete bounded drain operation.
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }

        await Task.WhenAll(_clientTasks.Values).WaitAsync(cancellationToken).ConfigureAwait(false);

        _lifetime?.Cancel();
        _lifetime?.Dispose();
        Volatile.Write(ref _state, (int)HealthState.Stopped);
        _completion.TrySetResult();
    }

    public ComponentHealth GetHealth()
    {
        var state = (HealthState)Volatile.Read(ref _state);
        return new ComponentHealth(Name, state, $"Loopback TCP listener; activeConnections={PlayerCount}.");
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                var id = Interlocked.Increment(ref _nextConnectionId);
                if (_clients.Count >= _maximumConnections || !_clients.TryAdd(id, client))
                {
                    client.Dispose();
                    continue;
                }

                var task = ManageClientAsync(id, client, cancellationToken);
                _clientTasks[id] = task;
                _ = task.ContinueWith(
                    static (completedTask, state) =>
                    {
                        _ = completedTask;
                        var tuple = ((ConcurrentDictionary<long, Task>, long))state!;
                        tuple.Item1.TryRemove(tuple.Item2, out _);
                    },
                    (_clientTasks, id),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (Volatile.Read(ref _intentionalStop) != 0)
        {
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _intentionalStop) != 0)
        {
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _state, (int)HealthState.Failed);
            _completion.TrySetException(new InvalidOperationException("Listener failed.", exception));
            return;
        }

        if (Volatile.Read(ref _intentionalStop) == 0)
        {
            Volatile.Write(ref _state, (int)HealthState.Failed);
            _completion.TrySetException(new InvalidOperationException("Listener stopped unexpectedly."));
        }
    }

    private async Task ManageClientAsync(long id, TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[4096];
            var stream = client.GetStream();
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                // No packet layout is assumed. Development traffic is deliberately discarded.
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        finally
        {
            _clients.TryRemove(id, out _);
            client.Dispose();
        }
    }

    private static async Task IgnoreExpectedStopAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Completion is separately observed and sanitized by the coordinator.
        }
    }
}
