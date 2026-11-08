using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace Revora.NativeLogin.Core;

public sealed record AccountProbeReport(
    string SchemaVersion,
    string BuildId,
    string SessionId,
    int ConnectionSequence,
    string Binding,
    string Provenance,
    IReadOnlyList<string> States,
    int EncryptedBytes,
    string? EncryptedSha256,
    int? DecryptedLength,
    int? DecryptedPacketId,
    string CryptoMode,
    string RsaStructure,
    bool ResponseSent,
    int? ResponsePacketId,
    string ClientVisibleResponse,
    string? FailureCategory);

public sealed class AccountProbe3b : IAsyncDisposable
{
    public const int AccountPort = 9958;
    public const int ExpectedLength = 276;
    public const int ExpectedPacketId = 1086;
    public const int ResponsePacketId = 1055;
    private const int MaximumCapture = 512;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource stop = new();
    private readonly object sync = new();
    private readonly List<string> states = ["probe-starting"];
    private readonly string sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    private Task? worker;
    private string provenance = "not-triggered";
    private int encryptedBytes;
    private string? encryptedSha256;
    private int? decryptedLength;
    private int? decryptedPacketId;
    private bool responseSent;
    private string clientVisibleResponse = "not-observed";
    private string? failureCategory;

    public AccountProbe3b(int port = AccountPort)
    {
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        listener = new TcpListener(IPAddress.Loopback, port);
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public void Start()
    {
        listener.Start(1);
        AddState("listening");
        worker = RunAsync(stop.Token);
    }

    public void SetLoginProvenance(string value)
    {
        if (value is not ("automatic-foreground-sendinput" or "manual-in-game-login")) throw new ArgumentOutOfRangeException(nameof(value));
        lock (sync) provenance = value;
    }

    public void MarkClientVisibleResponse(bool observed)
    {
        lock (sync)
        {
            clientVisibleResponse = observed ? "owner-confirmed-controlled-error" : "owner-confirmed-not-visible";
            states.Add(observed ? "client-visible-response-observed" : "client-visible-response-not-observed");
        }
    }

    public AccountProbeReport Snapshot()
    {
        lock (sync)
            return new("1", "native-login-5165-test-3b", sessionId, 1, $"127.0.0.1:{Port}", provenance,
                states.ToArray(), encryptedBytes, encryptedSha256, decryptedLength, decryptedPacketId,
                "tq-counter-initial-state-no-seed", "not-applicable-no-rsa-envelope-evidenced", responseSent,
                responseSent ? ResponsePacketId : null, clientVisibleResponse, failureCategory);
    }

    public async Task<string> WriteSanitizedAsync(string directory, CancellationToken token)
    {
        string path = Path.Combine(directory, $"native-protocol-3b-{sessionId}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Snapshot(), new JsonSerializerOptions { WriteIndented = true }), token);
        return path;
    }

    private async Task RunAsync(CancellationToken token)
    {
        byte[]? encrypted = null;
        byte[]? decrypted = null;
        byte[]? response = null;
        byte[]? responseCiphertext = null;
        try
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(token);
            AddState("connection-accepted");
            await using NetworkStream stream = client.GetStream();
            encrypted = new byte[MaximumCapture];
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
            idle.CancelAfter(TimeSpan.FromSeconds(5));
            if (!await ReadExactlyOrEofAsync(stream, encrypted.AsMemory(0, 4), idle.Token)) { Fail("truncated-handshake"); return; }
            AddState("raw-pre-framing-bytes-observed");
            var cipher = new AccountCipher5165();
            decrypted = new byte[MaximumCapture];
            cipher.DecryptClient(encrypted.AsSpan(0, 4), decrypted.AsSpan(0, 4));
            int length = BinaryPrimitives.ReadUInt16LittleEndian(decrypted);
            int packetId = BinaryPrimitives.ReadUInt16LittleEndian(decrypted.AsSpan(2));
            lock (sync) { decryptedLength = length; decryptedPacketId = packetId; }
            AddState("cipher-identified");
            if (length != ExpectedLength || packetId != ExpectedPacketId || length > MaximumCapture) { Fail("invalid-decrypted-header"); return; }
            AddState("decrypted-header-validated");
            if (!await ReadExactlyOrEofAsync(stream, encrypted.AsMemory(4, length - 4), idle.Token)) { Fail("truncated-frame"); return; }
            cipher.DecryptClient(encrypted.AsSpan(4, length - 4), decrypted.AsSpan(4, length - 4));
            lock (sync)
            {
                encryptedBytes = length;
                encryptedSha256 = Convert.ToHexString(SHA256.HashData(encrypted.AsSpan(0, length))).ToLowerInvariant();
            }
            AddState("frame-identified");

            response = new byte[12];
            BinaryPrimitives.WriteUInt16LittleEndian(response, 12);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(2), ResponsePacketId);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(8), 1);
            responseCiphertext = new byte[response.Length];
            cipher.EncryptServer(response, responseCiphertext);
            await stream.WriteAsync(responseCiphertext, token);
            await stream.FlushAsync(token);
            lock (sync) responseSent = true;
            AddState("controlled-invalid-account-response-sent");
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { AddState("stopped-by-owner"); }
        catch (OperationCanceledException) { Fail("idle-timeout"); }
        catch (SocketException) when (stop.IsCancellationRequested) { AddState("stopped-by-owner"); }
        catch (IOException) { Fail("network-io"); }
        finally
        {
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            if (decrypted is not null) CryptographicOperations.ZeroMemory(decrypted);
            if (response is not null) CryptographicOperations.ZeroMemory(response);
            if (responseCiphertext is not null) CryptographicOperations.ZeroMemory(responseCiphertext);
        }
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

    private void AddState(string state) { lock (sync) if (!states.Contains(state, StringComparer.Ordinal)) states.Add(state); }
    private void Fail(string category) { lock (sync) { failureCategory = category; states.Add("failed"); } }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop();
        if (worker is not null) try { await worker; } catch (SocketException) when (stop.IsCancellationRequested) { }
        stop.Dispose();
    }
}
