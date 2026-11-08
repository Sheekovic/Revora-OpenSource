using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Revora.NativeLogin.Core;

public sealed record ProbeFrame(DateTimeOffset ObservedAt, int Length, int? Type);
public sealed record ProbeReport(string State, string Binding, IReadOnlyList<ProbeFrame> Frames, string? DisconnectCategory);

public sealed class LoopbackProtocolProbe : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource stop = new();
    private readonly List<ProbeFrame> frames = [];
    private Task? worker;

    public LoopbackProtocolProbe(int port = 9958)
    {
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        listener = new TcpListener(IPAddress.Loopback, port);
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public void Start()
    {
        listener.Start(1);
        worker = RunAsync(stop.Token);
    }

    public ProbeReport Snapshot()
    {
        lock (frames)
            return new(worker?.IsCompleted == true ? "completed" : "listening", $"127.0.0.1:{Port}", frames.ToArray(), null);
    }

    public async Task WriteSanitizedAsync(string path, CancellationToken token) =>
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Snapshot(), new JsonSerializerOptions { WriteIndented = true }), token);

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(token);
            await using NetworkStream stream = client.GetStream();
            byte[] header = new byte[4];
            while (!token.IsCancellationRequested)
            {
                if (!await ReadExactlyOrEofAsync(stream, header, token)) break;
                int length = BinaryPrimitives.ReadUInt16LittleEndian(header);
                int? type = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2));
                lock (frames) frames.Add(new(DateTimeOffset.UtcNow, length, type));
                if (length is < 4 or > 4096) break;
                int remaining = length - header.Length;
                byte[] discard = new byte[Math.Min(remaining, 4096)];
                while (remaining > 0)
                {
                    int chunk = await stream.ReadAsync(discard.AsMemory(0, Math.Min(discard.Length, remaining)), token);
                    if (chunk == 0) return;
                    remaining -= chunk;
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    private static async Task<bool> ReadExactlyOrEofAsync(NetworkStream stream, Memory<byte> destination, CancellationToken token)
    {
        int offset = 0;
        while (offset < destination.Length)
        {
            int read = await stream.ReadAsync(destination[offset..], token);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop();
        if (worker is not null) try { await worker; } catch (SocketException) when (stop.IsCancellationRequested) { }
        stop.Dispose();
    }
}
