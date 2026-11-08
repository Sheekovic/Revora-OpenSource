using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Revora.Persistence.PostgreSql;

namespace Revora.Web;

public static class InternalGameEndpoints
{
    public static void MapInternalGameEndpoints(this WebApplication app, WebOptions options)
    {
        app.MapPost("/api/v1/game-handoffs", IssueHandoff).RequireAuthorization("DashboardUser");
        RouteGroupBuilder internalApi = app.MapGroup("/internal/v1");
        internalApi.AddEndpointFilter(new InternalGameClientFilter(options));
        internalApi.MapPost("/game-handoffs/consume", ConsumeHandoff);
        internalApi.MapPost("/game-servers/status", RecordStatus);
        app.MapGet("/api/status", PublicStatus);
    }

    private static async Task<IResult> IssueHandoff(HttpContext context, WebAccountStore store,
        WebOptions options, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        if (!options.InternalGameIntegrationEnabled || options.IdentityServerId == 0)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        await antiforgery.ValidateRequestAsync(context);
        if (!Guid.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out Guid accountId) || context.Connection.RemoteIpAddress is not IPAddress address)
            return Results.Unauthorized();

        IssuedGameHandoff? issued = await store.IssueGameHandoffAsync(accountId,
            address.MapToIPv6(), options.IdentityServerId, TimeSpan.FromSeconds(60),
            context.RequestAborted);
        return issued is null
            ? Results.Conflict(new { error = "character_identity_unavailable" })
            : Results.Json(new { handle = issued.Handle, expiresAtUnixSeconds = issued.ExpiresAt.ToUnixTimeSeconds() });
    }

    private static async Task<IResult> ConsumeHandoff([FromBody] GameHandoffConsumeRequest request,
        WebAccountStore store, IdentityHandoffSigner signer, WebOptions options,
        HttpContext context)
    {
        if (request.Handle == 0 || !IPAddress.TryParse(request.RemoteAddress, out IPAddress? address))
            return Results.BadRequest();
        ConsumedGameHandoff? consumed = await store.ConsumeGameHandoffAsync(request.Handle,
            address.MapToIPv6(), options.IdentityServerId, context.RequestAborted);
        if (consumed is null) return Results.NotFound();
        SignedGameHandoffResponse response = signer.Sign(request.Handle, consumed.AccountId,
            consumed.CharacterId, options.IdentityServerId, address.MapToIPv6().ToString(),
            consumed.ExpiresAt);
        return Results.Json(response);
    }

    private static IResult RecordStatus([FromBody] GameServerStatusRequest request,
        GameServerStatusRegistry registry, WebOptions options)
    {
        if (request.ServerId != options.IdentityServerId || request.ServerId == 0 ||
            request.OnlinePlayers < 0 || request.OnlinePlayers > 100_000 ||
            request.StartedAtUnixSeconds <= 0 || request.ObservedAtUnixSeconds <= 0 ||
            Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - request.ObservedAtUnixSeconds) > 60)
            return Results.BadRequest();
        registry.Record(request);
        return Results.NoContent();
    }

    private static IResult PublicStatus(GameServerStatusRegistry registry, WebOptions options)
    {
        GameServerStatusSnapshot snapshot = registry.Get(options.IdentityServerId, DateTimeOffset.UtcNow);
        return Results.Json(new
        {
            online = snapshot.Online,
            serverName = snapshot.ServerName,
            playersOnline = snapshot.OnlinePlayers,
            uptimeSeconds = snapshot.UptimeSeconds
        });
    }
}

public sealed record GameHandoffConsumeRequest(uint Handle, string RemoteAddress);
public sealed record GameServerStatusRequest(uint ServerId, string ServerName, int OnlinePlayers,
    long StartedAtUnixSeconds, long ObservedAtUnixSeconds, bool Healthy);
public sealed record GameServerStatusSnapshot(bool Online, string ServerName, int OnlinePlayers,
    long UptimeSeconds);

public sealed class GameServerStatusRegistry
{
    private readonly ConcurrentDictionary<uint, GameServerStatusRequest> values = new();
    public void Record(GameServerStatusRequest value) => values[value.ServerId] = value;

    public GameServerStatusSnapshot Get(uint serverId, DateTimeOffset now)
    {
        if (!values.TryGetValue(serverId, out GameServerStatusRequest? value))
            return new(false, "Revora", 0, 0);
        long nowSeconds = now.ToUnixTimeSeconds();
        bool online = value.Healthy && nowSeconds - value.ObservedAtUnixSeconds <= 45;
        return new(online, value.ServerName, online ? value.OnlinePlayers : 0,
            online ? Math.Max(0, nowSeconds - value.StartedAtUnixSeconds) : 0);
    }
}

public sealed class InternalGameClientFilter(WebOptions options) : IEndpointFilter
{
    public const string DirectPeerLoopbackKey = "Revora.DirectPeerIsLoopback";
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation,
        EndpointFilterDelegate next)
    {
        HttpContext context = invocation.HttpContext;
        if (!options.InternalGameIntegrationEnabled ||
            context.Items[DirectPeerLoopbackKey] is not true ||
            !context.Request.Headers.TryGetValue("X-Revora-Client-Certificate-Sha256", out var supplied) ||
            !FixedAsciiEquals(options.IdentityClientCertificateSha256, supplied.ToString()))
            return Results.NotFound();
        return await next(invocation);
    }

    public static bool FixedAsciiEquals(string expected, string supplied)
    {
        byte[] left = Encoding.ASCII.GetBytes(expected.Trim().ToUpperInvariant());
        byte[] right = Encoding.ASCII.GetBytes(supplied.Trim().ToUpperInvariant());
        bool equal = left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
        CryptographicOperations.ZeroMemory(left);
        CryptographicOperations.ZeroMemory(right);
        return equal;
    }
}

public sealed class IdentityHandoffSigner : IDisposable
{
    private readonly byte[] key;
    public IdentityHandoffSigner(byte[] key)
    {
        if (key.Length < 32) throw new ArgumentException("A 256-bit key is required.", nameof(key));
        this.key = (byte[])key.Clone();
    }

    public SignedGameHandoffResponse Sign(uint handle, uint accountId, uint characterId,
        uint serverId, string remoteAddress, DateTimeOffset expiresAt)
    {
        if (handle == 0 || accountId == 0 || characterId == 0 || serverId == 0)
            throw new ArgumentOutOfRangeException(nameof(handle));
        byte[] nonce = RandomNumberGenerator.GetBytes(32);
        byte[] signature = ComputeSignature(key, handle, accountId, characterId, serverId,
            remoteAddress, expiresAt.ToUnixTimeSeconds(), nonce);
        return new(handle, accountId, characterId, serverId, remoteAddress,
            expiresAt.ToUnixTimeSeconds(), nonce, signature);
    }

    public static byte[] ComputeSignature(byte[] key, uint handle, uint accountId,
        uint characterId, uint serverId, string remoteAddress, long expiresAtUnixSeconds,
        byte[] nonce)
    {
        byte[] address = Encoding.UTF8.GetBytes(remoteAddress);
        byte[] payload = new byte[4 * 4 + 8 + 4 + address.Length + 4 + nonce.Length];
        Span<byte> destination = payload;
        BinaryPrimitives.WriteUInt32LittleEndian(destination, handle); destination = destination[4..];
        BinaryPrimitives.WriteUInt32LittleEndian(destination, accountId); destination = destination[4..];
        BinaryPrimitives.WriteUInt32LittleEndian(destination, characterId); destination = destination[4..];
        BinaryPrimitives.WriteUInt32LittleEndian(destination, serverId); destination = destination[4..];
        BinaryPrimitives.WriteInt64LittleEndian(destination, expiresAtUnixSeconds); destination = destination[8..];
        BinaryPrimitives.WriteInt32LittleEndian(destination, address.Length); destination = destination[4..];
        address.CopyTo(destination); destination = destination[address.Length..];
        BinaryPrimitives.WriteInt32LittleEndian(destination, nonce.Length); destination = destination[4..];
        nonce.CopyTo(destination);
        try { return HMACSHA256.HashData(key, payload); }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(key);
}

public sealed record SignedGameHandoffResponse(uint Handle, uint AccountId, uint CharacterId,
    uint ServerId, string RemoteAddress, long ExpiresAtUnixSeconds, byte[] Nonce, byte[] Signature);
