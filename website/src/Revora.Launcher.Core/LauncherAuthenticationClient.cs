using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace Revora.Launcher.Core;

public sealed record LauncherSession(Guid SessionId, Guid LaunchId, string Realm, DateTimeOffset ExpiresAt, byte[] Ticket) : IDisposable
{
    public void Dispose() => CryptographicOperations.ZeroMemory(Ticket);
}

public sealed class LauncherAuthenticationClient(HttpClient httpClient)
{
    public static readonly Uri Endpoint = new("https://admin.playrevora.com/launcher-api/v1/session");

    public async Task<LauncherSession> AuthenticateAsync(string username, string password, string clientBuildId, Guid sessionId, CancellationToken token)
    {
        if (username.Length is < 3 or > 32 || password.Length is < 1 or > 256 || clientBuildId.Length is < 1 or > 128 || sessionId == Guid.Empty)
            throw new ArgumentException("Launcher authentication input is invalid.");
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(Endpoint,
            new LauncherRequest(username, password, sessionId, clientBuildId), token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest or HttpStatusCode.TooManyRequests)
            throw new UnauthorizedAccessException("Authentication was not accepted.");
        response.EnsureSuccessStatusCode();
        LauncherResponse result = await response.Content.ReadFromJsonAsync<LauncherResponse>(cancellationToken: token)
            ?? throw new InvalidDataException("The launcher response was empty.");
        byte[] ticket;
        try { ticket = Convert.FromBase64String(result.LaunchTicket); }
        catch (FormatException error) { throw new InvalidDataException("The launch ticket encoding was invalid.", error); }
        if (ticket.Length != 32 || result.LaunchId == Guid.Empty || result.ExpiresInSeconds is < 1 or > 120 || result.Realm != "Revora")
        {
            CryptographicOperations.ZeroMemory(ticket);
            throw new InvalidDataException("The launch ticket response failed policy validation.");
        }
        return new(sessionId, result.LaunchId, result.Realm, DateTimeOffset.UtcNow.AddSeconds(result.ExpiresInSeconds), ticket);
    }

    public async Task<bool> ConsumeAsync(LauncherSession session, string clientBuildId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(session);
        return await ConsumeAsync(session.Ticket, session.SessionId, session.LaunchId, clientBuildId, token);
    }

    public async Task<bool> ConsumeAsync(ReadOnlyMemory<byte> ticket, Guid sessionId, Guid launchId, string clientBuildId, CancellationToken token)
    {
        if (ticket.Length != 32 || sessionId == Guid.Empty || launchId == Guid.Empty || clientBuildId.Length is < 1 or > 128) return false;
        var uri = new UriBuilder(new Uri(Endpoint, "session/consume"))
        {
            Query = $"launcherSessionId={sessionId:D}&launchId={launchId:D}&clientBuildId={Uri.EscapeDataString(clientBuildId)}",
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, uri.Uri) { Content = new ReadOnlyMemoryContent(ticket) };
        request.Content.Headers.ContentType = new("application/octet-stream");
        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.Conflict) return false;
        response.EnsureSuccessStatusCode();
        ConsumeResponse result = await response.Content.ReadFromJsonAsync<ConsumeResponse>(cancellationToken: token)
            ?? throw new InvalidDataException("The ticket-consumption response was empty.");
        return result.Consumed;
    }

    private sealed record LauncherRequest(string Username, string Password, Guid LauncherSessionId, string ClientBuildId);
    private sealed record LauncherResponse(string LaunchTicket, Guid LaunchId, int ExpiresInSeconds, string Realm);
    private sealed record ConsumeResponse(bool Consumed);
}
