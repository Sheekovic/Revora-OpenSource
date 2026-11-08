using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Revora.Launcher.Core;

namespace Revora.NativeLogin.Core;

public interface IH1AccountAuthenticator
{
    Task<LauncherSession> AuthenticateAsync(string username, ReadOnlyMemory<byte> passwordUtf8, Guid sessionId, CancellationToken token);
    Task<bool> ConsumeAsync(LauncherSession session, CancellationToken token);
}

public sealed class H1HttpsAccountAuthenticator(HttpClient httpClient) : IH1AccountAuthenticator
{
    public const string ClientBuildId = "manual-playable-5165-m2-h1";
    private readonly LauncherAuthenticationClient inner = new(httpClient);
    public async Task<LauncherSession> AuthenticateAsync(string username, ReadOnlyMemory<byte> passwordUtf8, Guid sessionId, CancellationToken token)
    {
        if (username.Length is < 3 or > 32 || passwordUtf8.Length is < 1 or > 16 || sessionId == Guid.Empty)
            throw new ArgumentException("H1 authentication input is invalid.");
        using var json = new MemoryStream(512);
        using (var writer = new Utf8JsonWriter(json))
        {
            writer.WriteStartObject();
            writer.WriteString("username", username);
            writer.WriteString("password", passwordUtf8.Span);
            writer.WriteString("launcherSessionId", sessionId);
            writer.WriteString("clientBuildId", ClientBuildId);
            writer.WriteEndObject(); writer.Flush();
        }
        if (!json.TryGetBuffer(out ArraySegment<byte> body) || body.Array is null) throw new InvalidOperationException("The bounded authentication buffer was unavailable.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, LauncherAuthenticationClient.Endpoint)
            { Content = new ByteArrayContent(body.Array, body.Offset, body.Count) };
            request.Content.Headers.ContentType = new("application/json");
            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.TooManyRequests)
                throw new UnauthorizedAccessException("Authentication was not accepted.");
            response.EnsureSuccessStatusCode();
            H1LauncherResponse result = await response.Content.ReadFromJsonAsync<H1LauncherResponse>(cancellationToken: token)
                ?? throw new InvalidDataException("The launcher response was empty.");
            byte[] ticket;
            try { ticket = Convert.FromBase64String(result.LaunchTicket); }
            catch (FormatException error) { throw new InvalidDataException("The launch ticket encoding was invalid.", error); }
            if (ticket.Length != 32 || result.LaunchId == Guid.Empty || result.ExpiresInSeconds is < 1 or > 120 || result.Realm != "Revora")
            { CryptographicOperations.ZeroMemory(ticket); throw new InvalidDataException("The launch ticket response failed policy validation."); }
            return new(sessionId, result.LaunchId, result.Realm, DateTimeOffset.UtcNow.AddSeconds(result.ExpiresInSeconds), ticket);
        }
        finally { CryptographicOperations.ZeroMemory(body.Array.AsSpan()); }
    }
    public Task<bool> ConsumeAsync(LauncherSession session, CancellationToken token) => inner.ConsumeAsync(session, ClientBuildId, token);
    private sealed record H1LauncherResponse(string LaunchTicket, Guid LaunchId, int ExpiresInSeconds, string Realm);
}

public sealed record ManualPlayableH1Result(
    string Classification,
    int? AccountClientProcessId,
    int? GameClientProcessId,
    int? AccountRequestLength,
    int? AccountPacketId,
    string CredentialValidationCategory,
    int? AccountResponseLength,
    int? AccountResponsePacketId,
    bool GameConnectionObserved,
    string HandshakeCandidate,
    int? ServerHandshakeLength,
    int? ClientDhReplyLength,
    string DhValidationCategory,
    bool SharedSecretDerived,
    bool BlowfishActivated,
    int? FirstGamePacketLength,
    int? FirstGamePacketId,
    string FooterValidationCategory,
    string TokenLifecycle,
    string? FailureCategory);

/// <summary>One bounded, loopback-only M2-H1 Account-to-Game evidence run.</summary>
public sealed class ManualPlayableH1Probe : IAsyncDisposable
{
    public const int AccountPort = 9958;
    public const int GamePort = 9959;
    private readonly TcpListener accountListener;
    private readonly TcpListener gameListener;
    private readonly ITcpOwnerResolver ownerResolver;
    private readonly IH1AccountAuthenticator authenticator;
    private readonly TimeSpan timeout;
    private readonly TaskCompletionSource<int> expectedProcessId = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource stop = new();
    private Task<ManualPlayableH1Result>? worker;

    public ManualPlayableH1Probe(ITcpOwnerResolver ownerResolver, IH1AccountAuthenticator authenticator,
        TimeSpan? timeout = null, int accountPort = AccountPort, int gamePort = GamePort)
    {
        if (accountPort is < 0 or > 65535 || gamePort is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(accountPort));
        accountListener = new TcpListener(IPAddress.Loopback, accountPort);
        gameListener = new TcpListener(IPAddress.Loopback, gamePort);
        this.ownerResolver = ownerResolver;
        this.authenticator = authenticator;
        this.timeout = timeout ?? TimeSpan.FromSeconds(90);
        if (this.timeout <= TimeSpan.Zero || this.timeout > TimeSpan.FromSeconds(90)) throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public int BoundAccountPort => ((IPEndPoint)accountListener.LocalEndpoint).Port;
    public int BoundGamePort => ((IPEndPoint)gameListener.LocalEndpoint).Port;

    public void Start()
    {
        accountListener.Start(1);
        try { gameListener.Start(1); }
        catch { accountListener.Stop(); throw; }
        worker = RunAsync(stop.Token);
    }

    public void BindExpectedProcess(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processId, 1);
        if (!expectedProcessId.TrySetResult(processId)) throw new InvalidOperationException("The expected process was already bound.");
    }

    public Task<ManualPlayableH1Result> Completion => worker ?? throw new InvalidOperationException("The probe has not started.");

    private async Task<ManualPlayableH1Result> RunAsync(CancellationToken token)
    {
        byte[] accountEncrypted = new byte[AccountPackets5165.LoginLength];
        byte[] accountPlaintext = new byte[AccountPackets5165.LoginLength];
        byte[] accountResponse = new byte[AccountPackets5165.ConnectSuccessLength];
        byte[] encryptedResponse = new byte[AccountPackets5165.ConnectSuccessLength];
        LauncherSession? session = null;
        bool ticketFinalized = false;
        string credentialCategory = "not-attempted";
        string tokenLifecycle = "not-issued";
        int? accountOwner = null, gameOwner = null;
        GameHandshakeResult5165? handshakeResult = null;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(timeout);
            Task<TcpClient> pendingAccount = accountListener.AcceptTcpClientAsync(deadline.Token).AsTask();
            Task<TcpClient> pendingGame = gameListener.AcceptTcpClientAsync(deadline.Token).AsTask();
            int expected = await expectedProcessId.Task.WaitAsync(deadline.Token);
            using TcpClient account = await pendingAccount;
            accountOwner = ResolveOwner(account);
            if (accountOwner != expected) return Failure("unexpected-account-process", accountOwner, null, credentialCategory, tokenLifecycle, null);
            await using NetworkStream accountStream = account.GetStream();
            await ReadExactlyAsync(accountStream, accountEncrypted, deadline.Token);
            var accountCipher = new AccountCipher5165();
            accountCipher.DecryptClient(accountEncrypted, accountPlaintext);
            using AccountLogin5165 login = AccountPackets5165.ParseLogin(accountPlaintext);
            try
            {
                session = await authenticator.AuthenticateAsync(login.Username, login.PasswordBytes, Guid.NewGuid(), deadline.Token);
                credentialCategory = "valid-throwaway-account";
                tokenLifecycle = "issued";
            }
            catch (UnauthorizedAccessException)
            {
                credentialCategory = "invalid-generic";
                return Failure("credential-validation-rejected", accountOwner, null, credentialCategory, tokenLifecycle, null);
            }

            ulong wireToken = RandomWireToken();
            AccountPackets5165.WriteSuccess(accountResponse, wireToken, "127.0.0.1", checked((uint)BoundGamePort));
            // Fail before emitting a response the exact client is proven to reject.
            _ = AccountPackets5165.ParseSuccessAsExactClient(accountResponse);
            accountCipher.EncryptServer(accountResponse, encryptedResponse);
            await accountStream.WriteAsync(encryptedResponse, deadline.Token);
            await accountStream.FlushAsync(deadline.Token);

            using TcpClient game = await pendingGame;
            gameOwner = ResolveOwner(game);
            if (gameOwner != expected) return Failure("unexpected-game-process", accountOwner, gameOwner, credentialCategory, tokenLifecycle, null);
            await using NetworkStream gameStream = game.GetStream();
            using var handshake = new GameHandshake5165();
            byte[] hello = handshake.CreateServerHandshake();
            await gameStream.WriteAsync(hello, deadline.Token);
            await gameStream.FlushAsync(deadline.Token);
            CryptographicOperations.ZeroMemory(hello);

            byte[] first = new byte[11];
            await ReadExactlyAsync(gameStream, first, deadline.Token);
            if (handshake.ConsumeClientDh(first)) throw new InvalidDataException("The client DH message completed before its bounded header.");
            int totalDh = handshake.ExpectedClientDhWireLength ?? throw new InvalidDataException("The client DH length was not recovered.");
            byte[] remainder = new byte[totalDh - first.Length];
            try
            {
                await ReadExactlyAsync(gameStream, remainder, deadline.Token);
                if (!handshake.ConsumeClientDh(remainder)) return Failure("client-dh-rejected", accountOwner, gameOwner, credentialCategory, tokenLifecycle, handshake.Snapshot());
            }
            finally { CryptographicOperations.ZeroMemory(first); CryptographicOperations.ZeroMemory(remainder); }

            byte[] gameHeader = new byte[4];
            byte[] gameRemainder = new byte[GamePackets5165.ConnectLength + 8 - gameHeader.Length];
            try
            {
                await ReadExactlyAsync(gameStream, gameHeader, deadline.Token);
                if (handshake.ConsumeEncryptedGame(gameHeader)) throw new InvalidDataException("The Game packet completed before its body.");
                await ReadExactlyAsync(gameStream, gameRemainder, deadline.Token);
                if (!handshake.ConsumeEncryptedGame(gameRemainder)) return Failure("game-auth-frame-rejected", accountOwner, gameOwner, credentialCategory, tokenLifecycle, handshake.Snapshot());
            }
            finally { CryptographicOperations.ZeroMemory(gameHeader); CryptographicOperations.ZeroMemory(gameRemainder); }

            handshakeResult = handshake.Snapshot();
            GameConnect5165 connect = handshake.Connect ?? throw new InvalidDataException("The Game authentication packet was not retained for bounded validation.");
            ulong receivedWireToken = ((ulong)connect.TokenHigh << 32) | connect.TokenLow;
            if (receivedWireToken != wireToken) return Failure("handoff-words-mismatch", accountOwner, gameOwner, credentialCategory, tokenLifecycle, handshakeResult);
            bool consumed = await authenticator.ConsumeAsync(session, deadline.Token);
            ticketFinalized = consumed;
            tokenLifecycle = consumed ? "consumed-once" : "consume-rejected";
            if (!consumed) return Failure("handoff-ticket-consume-rejected", accountOwner, gameOwner, credentialCategory, tokenLifecycle, handshakeResult);
            return Build("genuine-game-1052-classified", accountOwner, gameOwner, credentialCategory, tokenLifecycle, handshakeResult, null);
        }
        catch (OperationCanceledException)
        {
            return Failure(token.IsCancellationRequested ? "owner-stopped" : "bounded-timeout", accountOwner, gameOwner, credentialCategory,
                session is null ? tokenLifecycle : "revocation-attempted", handshakeResult);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or SocketException or HttpRequestException)
        {
            return Failure(error switch { InvalidDataException => "protocol-validation", HttpRequestException => "https-authentication", SocketException => "loopback-socket", _ => "network-io" },
                accountOwner, gameOwner, credentialCategory, session is null ? tokenLifecycle : "revocation-attempted", handshakeResult);
        }
        finally
        {
            if (session is not null && !ticketFinalized)
            {
                try
                {
                    using var revoke = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    _ = await authenticator.ConsumeAsync(session, revoke.Token);
                }
                catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException) { }
            }
            session?.Dispose();
            CryptographicOperations.ZeroMemory(accountEncrypted);
            CryptographicOperations.ZeroMemory(accountPlaintext);
            CryptographicOperations.ZeroMemory(accountResponse);
            CryptographicOperations.ZeroMemory(encryptedResponse);
            accountListener.Stop(); gameListener.Stop();
        }
    }

    private int? ResolveOwner(TcpClient client) =>
        client.Client.RemoteEndPoint is IPEndPoint remote && client.Client.LocalEndPoint is IPEndPoint local
            ? ownerResolver.Resolve(remote, local) : null;

    private static ulong RandomWireToken()
    {
        Span<byte> bytes = stackalloc byte[8]; RandomNumberGenerator.Fill(bytes);
        ulong value = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        CryptographicOperations.ZeroMemory(bytes);
        return value == 0 ? 1UL : value;
    }

    private static ManualPlayableH1Result Failure(string category, int? accountPid, int? gamePid,
        string credentials, string token, GameHandshakeResult5165? handshake) =>
        Build("m2-h1-partial-or-failed", accountPid, gamePid, credentials, token, handshake, category);

    private static ManualPlayableH1Result Build(string classification, int? accountPid, int? gamePid,
        string credentials, string token, GameHandshakeResult5165? handshake, string? category) =>
        new(classification, accountPid, gamePid, AccountPackets5165.LoginLength, AccountPackets5165.LoginPacketId,
            credentials, AccountPackets5165.ConnectSuccessLength, AccountPackets5165.ConnectPacketId, gamePid is not null,
            GameHandshake5165.CandidateId, handshake?.ServerHandshakeLength, handshake?.ClientDhReplyLength,
            handshake?.FailureCategory is null ? (handshake?.ClientDhReplyLength is null ? "not-validated" : "validated") : "rejected",
            handshake?.SharedSecretDerived ?? false, handshake?.BlowfishActivated ?? false,
            handshake?.GamePacketLength, handshake?.GamePacketId, handshake?.FooterValidation ?? "not-validated", token, category ?? handshake?.FailureCategory);

    private static async Task ReadExactlyAsync(NetworkStream stream, Memory<byte> destination, CancellationToken token)
    {
        int offset = 0;
        while (offset < destination.Length)
        {
            int read = await stream.ReadAsync(destination[offset..], token);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); accountListener.Stop(); gameListener.Stop();
        if (worker is not null) await worker;
        stop.Dispose();
    }
}
