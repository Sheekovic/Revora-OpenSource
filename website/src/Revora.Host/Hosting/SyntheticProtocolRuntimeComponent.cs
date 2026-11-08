using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Revora.Application.Authentication;
using Revora.Persistence.PostgreSql;

namespace Revora.Host.Hosting;

/// <summary>A deterministic test-only protocol. It is not Conquer 5165 and can run only in Development/Test.</summary>
public sealed class SyntheticProtocolRuntimeComponent : IRevoraRuntimeComponent, IPlayerCountProvider
{
    private const int MaximumPacketSize = 4096;
    private readonly IPAddress address;
    private readonly int port;
    private readonly int maximumConnections;
    private readonly bool accountMode;
    private readonly PostgreSqlGameDataStore store;
    private readonly AuthenticationRateLimiter loginLimiter = new(5, TimeSpan.FromMinutes(1), 10_000);
    private readonly ConcurrentDictionary<Guid, Connection> connections = new();
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TcpListener? listener;
    private CancellationTokenSource? lifetime;
    private Task? acceptTask;
    private int state = (int)HealthState.Stopped;
    private int intentionalStop;
    private int authenticatedPlayers;

    public SyntheticProtocolRuntimeComponent(string name, IPAddress address, int port, int maximumConnections, bool accountMode, PostgreSqlGameDataStore store)
    {
        if (!IPAddress.IsLoopback(address)) throw new ArgumentException("The synthetic test protocol is loopback-only.", nameof(address));
        Name = name;
        this.address = address;
        this.port = port;
        this.maximumConnections = maximumConnections;
        this.accountMode = accountMode;
        this.store = store;
    }

    public string Name { get; }
    public Task Completion => completion.Task;
    public int PlayerCount => accountMode ? 0 : Volatile.Read(ref authenticatedPlayers);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref state, (int)HealthState.Starting);
        lifetime = new CancellationTokenSource();
        listener = new TcpListener(address, port);
        listener.Start(maximumConnections);
        acceptTask = AcceptAsync(listener, lifetime.Token);
        Volatile.Write(ref state, (int)HealthState.Ready);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref intentionalStop, 1);
        Volatile.Write(ref state, (int)HealthState.Draining);
        listener?.Stop();
        foreach (Connection connection in connections.Values) connection.Client.Dispose();
        if (lifetime is not null) await lifetime.CancelAsync();
        if (acceptTask is not null) await IgnoreExpectedAsync(acceptTask);
        await Task.WhenAll(connections.Values.Select(item => item.Completion)).WaitAsync(cancellationToken);
        Volatile.Write(ref state, (int)HealthState.Stopped);
        completion.TrySetResult();
    }

    public ComponentHealth GetHealth() => new(Name, (HealthState)Volatile.Read(ref state), $"Synthetic test protocol; connections={connections.Count}; authenticatedPlayers={PlayerCount}.");

    private async Task AcceptAsync(TcpListener activeListener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await activeListener.AcceptTcpClientAsync(cancellationToken);
                client.NoDelay = true;
                if (connections.Count >= maximumConnections)
                {
                    client.Dispose();
                    continue;
                }

                var connection = new Connection(client);
                if (!connections.TryAdd(connection.Id, connection)) { client.Dispose(); continue; }
                connection.Completion = RunConnectionAsync(connection, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (SocketException) when (Volatile.Read(ref intentionalStop) != 0) { }
        catch (ObjectDisposedException) when (Volatile.Read(ref intentionalStop) != 0) { }
        catch (Exception exception)
        {
            Volatile.Write(ref state, (int)HealthState.Failed);
            completion.TrySetException(new InvalidOperationException("Synthetic listener failed.", exception));
        }
    }

    private async Task RunConnectionAsync(Connection connection, CancellationToken cancellationToken)
    {
        Task writer = WriteLoopAsync(connection, cancellationToken);
        string disconnectReason = "connection_closed";
        try
        {
            NetworkStream stream = connection.Client.GetStream();
            while (!cancellationToken.IsCancellationRequested)
            {
                SyntheticFrame? frame = await ReadFrameAsync(stream, cancellationToken);
                if (frame is null) break;
                if (accountMode) await HandleAccountAsync(connection, frame.Value, cancellationToken);
                else await HandleGameAsync(connection, frame.Value, cancellationToken);
            }
        }
        catch (InvalidDataException) { disconnectReason = "malformed_packet"; }
        catch (JsonException) { disconnectReason = "malformed_payload"; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { disconnectReason = "shutdown"; }
        catch (IOException) { disconnectReason = "transport_closed"; }
        catch (SocketException) { disconnectReason = "transport_closed"; }
        finally
        {
            connection.Outbound.Writer.TryComplete();
            connection.Client.Dispose();
            await IgnoreExpectedAsync(writer);
            connections.TryRemove(connection.Id, out _);
            if (connection.SessionId is Guid session)
            {
                Interlocked.Decrement(ref authenticatedPlayers);
                try { await store.CloseSessionAsync(session, disconnectReason, CancellationToken.None); } catch { }
                if (connection.Character is not null) await BroadcastAsync(103, new DespawnMessage(connection.Character.Id), connection.Id, CancellationToken.None);
            }
        }
    }

    private async Task HandleAccountAsync(Connection connection, SyntheticFrame frame, CancellationToken cancellationToken)
    {
        if (frame.Type != 1) throw new InvalidDataException("Unexpected Account test message.");
        LoginMessage request = Deserialize<LoginMessage>(frame.Body);
        string key = (connection.Client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "unknown";
        if (!loginLimiter.TryAcquire(key) || request.Username.Length is < 3 or > 32 || request.Password.Length is < 12 or > 256 || request.Realm != "Revora Development")
        {
            await SendAsync(connection, 2, new LoginResult(false, null, "Authentication failed."), cancellationToken);
            return;
        }

        Guid? accountId = await store.AuthenticateAsync(request.Username, request.Password, cancellationToken);
        if (accountId is null)
        {
            await SendAsync(connection, 2, new LoginResult(false, null, "Authentication failed."), cancellationToken);
            return;
        }

        byte[] token = await store.IssueHandoffAsync(accountId.Value, TimeSpan.FromSeconds(30), cancellationToken);
        await SendAsync(connection, 2, new LoginResult(true, Convert.ToBase64String(token), "Proceed to the synthetic Game test endpoint."), cancellationToken);
        CryptographicOperations.ZeroMemory(token);
    }

    private async Task HandleGameAsync(Connection connection, SyntheticFrame frame, CancellationToken cancellationToken)
    {
        switch (frame.Type)
        {
            case 10:
            {
                if (connection.AccountId is not null) throw new InvalidDataException("Handoff was already accepted.");
                HandoffMessage request = Deserialize<HandoffMessage>(frame.Body);
                byte[] token;
                try { token = Convert.FromBase64String(request.Token); } catch (FormatException) { token = []; }
                Guid? accountId = request.Realm == "Revora Development" ? await store.ConsumeHandoffAsync(token, cancellationToken) : null;
                CryptographicOperations.ZeroMemory(token);
                if (accountId is null) { await SendAsync(connection, 11, new HandoffResult(false, null), cancellationToken); return; }
                connection.AccountId = accountId;
                connection.SessionId = Guid.NewGuid();
                await store.OpenSessionAsync(connection.SessionId.Value, accountId.Value, connection.Id, cancellationToken);
                Interlocked.Increment(ref authenticatedPlayers);
                connection.Character = await store.GetCharacterAsync(accountId.Value, cancellationToken);
                await SendAsync(connection, 11, new HandoffResult(true, connection.Character), cancellationToken);
                break;
            }
            case 12:
            {
                RequireAuthenticated(connection);
                if (connection.Character is not null) throw new InvalidDataException("Character already exists.");
                CharacterCreateMessage request = Deserialize<CharacterCreateMessage>(frame.Body);
                if (request.Name.Length is < 3 or > 16 || !request.Name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-')) throw new InvalidDataException("Character name is invalid.");
                connection.Character = await store.CreateCharacterAsync(connection.AccountId!.Value, request.Name, cancellationToken);
                await SendAsync(connection, 13, connection.Character, cancellationToken);
                break;
            }
            case 14:
                RequireCharacter(connection);
                connection.EnteredWorld = true;
                await SendAsync(connection, 15, connection.Character!, cancellationToken);
                await BroadcastAsync(100, new SpawnMessage(connection.Character!), connection.Id, cancellationToken);
                foreach (Connection nearby in Nearby(connection)) await SendAsync(connection, 100, new SpawnMessage(nearby.Character!), cancellationToken);
                break;
            case 16:
            {
                RequireCharacter(connection);
                MovementMessage move = Deserialize<MovementMessage>(frame.Body);
                CharacterState current = connection.Character!;
                if (move.Direction is < 0 or > 7 || Math.Abs(move.X - current.X) > 1 || Math.Abs(move.Y - current.Y) > 1) throw new InvalidDataException("Movement validation failed.");
                CharacterState? updated = await store.MoveAsync(current.Id, move.X, move.Y, move.Direction, current.Version, cancellationToken);
                if (updated is null) throw new InvalidDataException("Concurrent movement conflict.");
                connection.Character = updated;
                await SendAsync(connection, 17, updated, cancellationToken);
                await BroadcastAsync(101, updated, connection.Id, cancellationToken);
                break;
            }
            case 18:
            {
                RequireCharacter(connection);
                ChatMessage chat = Deserialize<ChatMessage>(frame.Body);
                if (string.IsNullOrWhiteSpace(chat.Text) || chat.Text.Length > 120 || chat.Text.Any(char.IsControl)) throw new InvalidDataException("Chat validation failed.");
                await BroadcastAsync(102, new ChatBroadcast(connection.Character!.Id, connection.Character.Name, chat.Text), null, cancellationToken);
                break;
            }
            default: throw new InvalidDataException("Unknown synthetic test message.");
        }
    }

    private IEnumerable<Connection> Nearby(Connection source) => connections.Values.Where(other => other.Id != source.Id && other.EnteredWorld && other.Character?.MapId == source.Character?.MapId && Math.Abs(other.Character!.X - source.Character!.X) <= 18 && Math.Abs(other.Character.Y - source.Character.Y) <= 18);

    private async Task BroadcastAsync<T>(ushort type, T payload, Guid? except, CancellationToken cancellationToken)
    {
        byte[] packet = Encode(type, payload);
        foreach (Connection target in connections.Values)
        {
            if (target.Id != except && target.EnteredWorld && !target.Outbound.Writer.TryWrite(packet)) target.Client.Dispose();
        }
        await Task.CompletedTask;
    }

    private static void RequireAuthenticated(Connection connection) { if (connection.AccountId is null) throw new InvalidDataException("Authentication required."); }
    private static void RequireCharacter(Connection connection) { RequireAuthenticated(connection); if (connection.Character is null) throw new InvalidDataException("Character required."); }
    private static T Deserialize<T>(byte[] body) => JsonSerializer.Deserialize<T>(body) ?? throw new JsonException("Payload was empty.");

    private static async Task<SyntheticFrame?> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[4];
        int first = await stream.ReadAsync(header.AsMemory(0, 1), cancellationToken);
        if (first == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(1), cancellationToken);
        int length = BinaryPrimitives.ReadUInt16LittleEndian(header);
        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2));
        if (length is < 4 or > MaximumPacketSize) throw new InvalidDataException("Invalid synthetic frame length.");
        byte[] body = new byte[length - 4];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return new SyntheticFrame(type, body);
    }

    private static Task SendAsync<T>(Connection connection, ushort type, T payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!connection.Outbound.Writer.TryWrite(Encode(type, payload))) throw new IOException("Bounded outbound queue is full.");
        return Task.CompletedTask;
    }

    private static byte[] Encode<T>(ushort type, T payload)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (body.Length + 4 > MaximumPacketSize) throw new InvalidDataException("Synthetic payload exceeds the packet limit.");
        byte[] packet = new byte[body.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), type);
        body.CopyTo(packet, 4);
        return packet;
    }

    private static async Task WriteLoopAsync(Connection connection, CancellationToken cancellationToken)
    {
        NetworkStream stream = connection.Client.GetStream();
        await foreach (byte[] packet in connection.Outbound.Reader.ReadAllAsync(cancellationToken)) await stream.WriteAsync(packet, cancellationToken);
    }

    private static async Task IgnoreExpectedAsync(Task task) { try { await task; } catch { } }

    private sealed class Connection(TcpClient client)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public TcpClient Client { get; } = client;
        public Channel<byte[]> Outbound { get; } = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
        public Task Completion { get; set; } = Task.CompletedTask;
        public Guid? AccountId { get; set; }
        public Guid? SessionId { get; set; }
        public CharacterState? Character { get; set; }
        public bool EnteredWorld { get; set; }
    }

    private readonly record struct SyntheticFrame(ushort Type, byte[] Body);
    private sealed record LoginMessage(string Username, string Password, string Realm);
    private sealed record LoginResult(bool Success, string? Token, string Message);
    private sealed record HandoffMessage(string Token, string Realm);
    private sealed record HandoffResult(bool Success, CharacterState? Character);
    private sealed record CharacterCreateMessage(string Name);
    private sealed record MovementMessage(int X, int Y, short Direction);
    private sealed record ChatMessage(string Text);
    private sealed record SpawnMessage(CharacterState Character);
    private sealed record DespawnMessage(Guid CharacterId);
    private sealed record ChatBroadcast(Guid CharacterId, string Name, string Text);
}
