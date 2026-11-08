using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Revora.NativeLogin.Core;

public sealed record ManualPlayableProbeResult(
    string State, int? ConnectedClientProcessId, DateTimeOffset? ConnectionTimestampUtc,
    int EncryptedRequestLength, int? DecryptedPacketLength, int? PacketId,
    string HeaderValidation, int? ResponseLength, int? ResponsePacketId,
    bool ResponseSent, string? FailureCategory);

/// <summary>One-connection loopback probe for genuine manual patch-5165 Account input.</summary>
public sealed class ManualPlayableAccountProbe : IAsyncDisposable
{
    public const int AccountPort = 9958;
    private const int RequestLength = 276;
    private const int RequestPacketId = 1086;
    private const int ResponseLength = 12;
    private const int ResponsePacketId = 1055;
    private readonly TcpListener listener;
    private readonly ITcpOwnerResolver ownerResolver;
    private readonly TimeSpan timeout;
    private readonly TaskCompletionSource<int> expectedProcessId = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource stop = new();
    private Task<ManualPlayableProbeResult>? worker;

    public ManualPlayableAccountProbe(ITcpOwnerResolver ownerResolver, TimeSpan? timeout = null, int port = AccountPort)
    {
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        listener = new TcpListener(IPAddress.Loopback, port);
        this.ownerResolver = ownerResolver;
        this.timeout = timeout ?? TimeSpan.FromSeconds(90);
        if (this.timeout <= TimeSpan.Zero || this.timeout > TimeSpan.FromSeconds(90))
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public void Start()
    {
        listener.Start(1);
        worker = RunAsync(stop.Token);
    }

    public void BindExpectedProcess(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processId, 1);
        if (!expectedProcessId.TrySetResult(processId)) throw new InvalidOperationException("The expected process was already bound.");
    }

    public Task<ManualPlayableProbeResult> Completion => worker ?? throw new InvalidOperationException("The probe has not started.");

    private async Task<ManualPlayableProbeResult> RunAsync(CancellationToken token)
    {
        byte[] encrypted = new byte[RequestLength];
        byte[] decrypted = new byte[RequestLength];
        byte[] response = new byte[ResponseLength];
        byte[] responseEncrypted = new byte[ResponseLength];
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(timeout);
            Task<TcpClient> pendingClient = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
            int expected = await expectedProcessId.Task.WaitAsync(deadline.Token);
            using TcpClient client = await pendingClient;
            DateTimeOffset connectedAt = DateTimeOffset.UtcNow;
            if (client.Client.RemoteEndPoint is not IPEndPoint remote || client.Client.LocalEndPoint is not IPEndPoint local)
                return Failure(null, connectedAt, 0, null, null, "endpoint-unavailable");
            int? owner = ownerResolver.Resolve(remote, local);
            if (owner != expected) return Failure(owner, connectedAt, 0, null, null, "unexpected-client-process");

            await using NetworkStream stream = client.GetStream();
            int received = await ReadExactlyAsync(stream, encrypted, deadline.Token);
            if (received != RequestLength) return Failure(owner, connectedAt, received, null, null, "truncated-request");

            var cipher = new AccountCipher5165();
            cipher.DecryptClient(encrypted, decrypted);
            int length = BinaryPrimitives.ReadUInt16LittleEndian(decrypted);
            int packetId = BinaryPrimitives.ReadUInt16LittleEndian(decrypted.AsSpan(2));
            if (length != RequestLength || packetId != RequestPacketId)
                return Failure(owner, connectedAt, received, length, packetId, "invalid-decrypted-header");

            BinaryPrimitives.WriteUInt16LittleEndian(response, ResponseLength);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(2), ResponsePacketId);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(8), 1);
            cipher.EncryptServer(response, responseEncrypted);
            await stream.WriteAsync(responseEncrypted, deadline.Token);
            await stream.FlushAsync(deadline.Token);
            return new("controlled-response-sent", owner, connectedAt, received, length, packetId,
                "validated-276-1086", ResponseLength, ResponsePacketId, true, null);
        }
        catch (OperationCanceledException)
        {
            return Failure(null, null, 0, null, null, token.IsCancellationRequested ? "owner-stopped" : "bounded-timeout");
        }
        catch (IOException) { return Failure(null, null, 0, null, null, "network-io"); }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
            CryptographicOperations.ZeroMemory(decrypted);
            CryptographicOperations.ZeroMemory(response);
            CryptographicOperations.ZeroMemory(responseEncrypted);
            listener.Stop();
        }
    }

    private static ManualPlayableProbeResult Failure(int? owner, DateTimeOffset? timestamp, int bytes,
        int? length, int? packetId, string category) =>
        new("failed", owner, timestamp, bytes, length, packetId,
            length is null ? "not-validated" : "rejected", null, null, false, category);

    private static async Task<int> ReadExactlyAsync(NetworkStream stream, Memory<byte> target, CancellationToken token)
    {
        int offset = 0;
        while (offset < target.Length)
        {
            int read = await stream.ReadAsync(target[offset..], token);
            if (read == 0) break;
            offset += read;
        }
        return offset;
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop();
        if (worker is not null) await worker;
        stop.Dispose();
    }
}
