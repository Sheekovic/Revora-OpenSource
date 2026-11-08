namespace Revora.Networking.Connections;

/// <summary>Owns per-connection cancellation and idempotent asynchronous cleanup.</summary>
public sealed class ConnectionLifetime : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Func<ValueTask> _cleanup;
    private int _disposed;

    public ConnectionLifetime(Func<ValueTask> cleanup)
    {
        _cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
    }

    public CancellationToken CancellationToken => _cancellation.Token;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _cancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            await _cleanup().ConfigureAwait(false);
        }
        finally
        {
            _cancellation.Dispose();
        }
    }
}

