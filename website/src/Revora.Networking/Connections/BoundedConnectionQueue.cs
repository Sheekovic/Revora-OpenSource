using System.Threading.Channels;

namespace Revora.Networking.Connections;

/// <summary>A bounded, cancellation-aware queue with explicit backpressure.</summary>
public sealed class BoundedConnectionQueue<T>
{
    private readonly Channel<T> _channel;

    public BoundedConnectionQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    }

    public ValueTask WriteAsync(T item, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(item, cancellationToken);

    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public bool TryComplete(Exception? error = null) => _channel.Writer.TryComplete(error);
}

