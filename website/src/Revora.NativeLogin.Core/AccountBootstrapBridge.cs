using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Revora.NativeLogin.Core;

public sealed record BootstrapBridgeResult(string State, int? ClientProcessId, int? Length, int? PacketId, string TicketLifecycle, bool ResponseSent, string? FailureCategory);

public sealed class AccountBootstrapBridge : IAsyncDisposable
{
    private readonly TaskCompletionSource<int> expectedProcessId = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly LockedLaunchTicket ticket;
    private readonly ITcpOwnerResolver ownerResolver;
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, Task<bool>> consumeTicket;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource stop = new();
    private Task<BootstrapBridgeResult>? worker;

    public AccountBootstrapBridge(LockedLaunchTicket ticket, ITcpOwnerResolver ownerResolver,
        Func<ReadOnlyMemory<byte>, CancellationToken, Task<bool>> consumeTicket,
        int port = AccountProbe3b.AccountPort)
    {
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        listener = new TcpListener(IPAddress.Loopback, port);
        this.ticket = ticket;
        this.ownerResolver = ownerResolver;
        this.consumeTicket = consumeTicket;
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
        if (!expectedProcessId.TrySetResult(processId)) throw new InvalidOperationException("The bridge process owner was already bound.");
    }

    public Task<BootstrapBridgeResult> Completion => worker ?? throw new InvalidOperationException("Bridge has not started.");

    private async Task<BootstrapBridgeResult> RunAsync(CancellationToken token)
    {
        byte[] encrypted = new byte[AccountProbe3b.ExpectedLength];
        byte[] decrypted = new byte[AccountProbe3b.ExpectedLength];
        byte[] response = new byte[12];
        byte[] responseCiphertext = new byte[12];
        string ticketLifecycle = "issued";
        bool ticketOperationAttempted = false;
        try
        {
            using var absolute = CancellationTokenSource.CreateLinkedTokenSource(token);
            absolute.CancelAfter(TimeSpan.FromSeconds(30));
            Task<TcpClient> accept = listener.AcceptTcpClientAsync(absolute.Token).AsTask();
            int expectedOwner = await expectedProcessId.Task.WaitAsync(absolute.Token);
            using TcpClient client = await accept;
            if (client.Client.RemoteEndPoint is not IPEndPoint clientEndpoint || client.Client.LocalEndPoint is not IPEndPoint serverEndpoint)
                return await FailureAsync(null, null, null, "endpoint-unavailable", ticketOperationAttempted);
            int? owner = ownerResolver.Resolve(clientEndpoint, serverEndpoint);
            if (owner != expectedOwner) return await FailureAsync(owner, null, null, "unexpected-client-process", ticketOperationAttempted);
            await using NetworkStream stream = client.GetStream();
            if (!await ReadExactlyAsync(stream, encrypted, absolute.Token)) return await FailureAsync(owner, null, null, "truncated-frame", ticketOperationAttempted);
            var cipher = new AccountCipher5165();
            cipher.DecryptClient(encrypted, decrypted);
            int length = BinaryPrimitives.ReadUInt16LittleEndian(decrypted);
            int packetId = BinaryPrimitives.ReadUInt16LittleEndian(decrypted.AsSpan(2));
            if (length != AccountProbe3b.ExpectedLength || packetId != AccountProbe3b.ExpectedPacketId)
                return await FailureAsync(owner, length, packetId, "invalid-account-frame", ticketOperationAttempted);
            ticketOperationAttempted = true;
            bool consumed = await consumeTicket(ticket.Value, absolute.Token);
            ticketLifecycle = consumed ? "consumed" : "rejected";
            ticket.Dispose();
            if (!consumed) return new("failed", owner, length, packetId, ticketLifecycle, false, "ticket-consumption-rejected");
            BinaryPrimitives.WriteUInt16LittleEndian(response, 12);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(2), AccountProbe3b.ResponsePacketId);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(8), 1);
            cipher.EncryptServer(response, responseCiphertext);
            await stream.WriteAsync(responseCiphertext, absolute.Token);
            await stream.FlushAsync(absolute.Token);
            return new("completed", owner, length, packetId, ticketLifecycle, true, null);
        }
        catch (OperationCanceledException) { return await FailureAsync(null, null, null, token.IsCancellationRequested ? "stopped" : "timeout", ticketOperationAttempted); }
        catch (IOException) { return await FailureAsync(null, null, null, "network-io", ticketOperationAttempted); }
        finally
        {
            ticket.Dispose();
            CryptographicOperations.ZeroMemory(encrypted); CryptographicOperations.ZeroMemory(decrypted);
            CryptographicOperations.ZeroMemory(response); CryptographicOperations.ZeroMemory(responseCiphertext);
            listener.Stop();
        }

        async Task<BootstrapBridgeResult> FailureAsync(int? owner, int? length, int? packetId, string category, bool operationAttempted)
        {
            string lifecycle = ticketLifecycle;
            if (!operationAttempted)
            {
                try
                {
                    using var revokeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    lifecycle = await consumeTicket(ticket.Value, revokeTimeout.Token) ? "revoked" : "rejected";
                }
                catch (Exception error) when (error is OperationCanceledException or HttpRequestException or IOException)
                {
                    lifecycle = "revocation-unconfirmed";
                }
            }
            return new("failed", owner, length, packetId, lifecycle, false, category);
        }
    }

    private static async Task<bool> ReadExactlyAsync(NetworkStream stream, Memory<byte> target, CancellationToken token)
    {
        int offset = 0;
        while (offset < target.Length) { int read = await stream.ReadAsync(target[offset..], token); if (read == 0) return false; offset += read; }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop();
        if (worker is not null) await worker;
        ticket.Dispose(); stop.Dispose();
    }
}
