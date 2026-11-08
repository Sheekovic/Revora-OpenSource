using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace PlayRevora.Launcher.Services;

public sealed record ServerStatusSnapshot(bool Online, string ServerName, int? PlayersOnline,
    long PingMilliseconds, string HealthCategory = "healthy");

public sealed class ServerStatusService : IDisposable
{
    private readonly HttpClient client;
    private readonly Uri endpoint;

    public ServerStatusService(string endpoint)
    {
        endpoint = endpoint?.Trim() ?? "";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? parsed) || parsed.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Server status endpoint must be an absolute HTTPS URL.");
        this.endpoint = parsed;
        client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task<ServerStatusSnapshot> CheckAsync(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            using HttpResponseMessage response = await client.GetAsync(endpoint,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(false, "REVORA", null, timer.ElapsedMilliseconds, "status_endpoint_http_failure");
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            JsonElement root = json.RootElement;
            bool online = ReadBoolean(root, "online") ?? true;
            string server = ReadString(root, "serverName") ?? ReadString(root, "name") ?? "REVORA";
            int? players = ReadInteger(root, "playersOnline") ?? ReadInteger(root, "onlinePlayers");
            return new(online, server, players, timer.ElapsedMilliseconds,
                online ? "healthy" : "realm_reported_offline");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(false, "REVORA", null, timer.ElapsedMilliseconds, "status_endpoint_unavailable");
        }
    }

    private static bool? ReadBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : null;
    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
    private static int? ReadInteger(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int count) ? count : null;

    public void Dispose() => client.Dispose();
}
