using System.Net.Http.Json;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Buffers;

namespace PlayRevora.Launcher.Services;

public sealed class NativeLoginCanaryService : IDisposable
{
    internal static readonly Uri TicketEndpoint = new("https://auth.playrevora.com/api/v1/native-login/tickets");
    internal static readonly Uri RedeemEndpoint = new("https://auth.playrevora.com/api/v1/native-login/tickets/redeem");
    private readonly HttpClient http;
    private readonly object gate = new();
    private byte[]? ticket;
    private DateTimeOffset expiresUtc;
    private Guid launchSessionId;
    public event Action<string>? StateChanged;

    public NativeLoginCanaryService(HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler, true);
        http.Timeout = TimeSpan.FromSeconds(8);
    }

    public bool TicketReady { get { lock (gate) return ticket is not null && expiresUtc > DateTimeOffset.UtcNow; } }
    public Guid LaunchSessionId { get { lock (gate) return launchSessionId; } }

    public async Task<bool> AuthenticateAsync(string username, char[] password,
        CancellationToken cancellationToken)
    {
        ClearTicket();
        byte[]? requestBody = null;
        try
        {
            Guid session = Guid.NewGuid();
            var payload = new ArrayBufferWriter<byte>();
            using (var jsonWriter = new Utf8JsonWriter(payload))
            {
                jsonWriter.WriteStartObject(); jsonWriter.WriteString("username", username);
                jsonWriter.WriteString("password", password);
                jsonWriter.WriteString("realm", "Revora"); jsonWriter.WriteString("profile", "dx9");
                jsonWriter.WriteString("clientContract", "patch-7630-168.06");
                jsonWriter.WriteString("launchSessionId", session.ToString("D")); jsonWriter.WriteEndObject();
            }
            requestBody = payload.WrittenSpan.ToArray();
            Array.Clear(password);
            using var content = new ByteArrayContent(requestBody);
            content.Headers.ContentType = new("application/json");
            using HttpResponseMessage response = await http.PostAsync(TicketEndpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            byte[] json = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                byte[] issued = document.RootElement.GetProperty("ticket").GetBytesFromBase64();
                DateTimeOffset expiry = document.RootElement.GetProperty("expiresUtc").GetDateTimeOffset();
                if (issued.Length != 32 || expiry <= DateTimeOffset.UtcNow) { CryptographicOperations.ZeroMemory(issued); return false; }
                lock (gate) { ticket = issued; expiresUtc = expiry; launchSessionId = session; }
                return true;
            }
            finally { CryptographicOperations.ZeroMemory(json); }
        }
        finally { Array.Clear(password); if (requestBody is not null) CryptographicOperations.ZeroMemory(requestBody); }
    }

    public async Task<bool> RedeemAsync(string requestSha256, Guid correlationId,
        Guid expectedLaunchSessionId, CancellationToken cancellationToken)
    {
        byte[]? localTicket;
        lock (gate)
        {
            if (ticket is null || expiresUtc <= DateTimeOffset.UtcNow || launchSessionId != expectedLaunchSessionId)
                return false;
            localTicket = ticket;
            ticket = null;
        }
        try
        {
            var payload = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(payload))
            {
                writer.WriteStartObject(); writer.WriteBase64String("ticket", localTicket);
                writer.WriteString("requestSha256", requestSha256); writer.WriteString("realm", "Revora");
                writer.WriteString("profile", "dx9"); writer.WriteString("clientContract", "patch-7630-168.06");
                writer.WriteString("correlationId", correlationId.ToString("D"));
                writer.WriteString("launchSessionId", expectedLaunchSessionId.ToString("D")); writer.WriteEndObject();
            }
            byte[] requestBody = payload.WrittenSpan.ToArray();
            try
            {
                using var content = new ByteArrayContent(requestBody); content.Headers.ContentType = new("application/json");
            using HttpResponseMessage response = await http.PostAsync(RedeemEndpoint, content, cancellationToken);
                bool ready = response.IsSuccessStatusCode;
                StateChanged?.Invoke(ready ? "Authorization ready" : "Secure authorization failed");
                return ready;
            }
            finally { CryptographicOperations.ZeroMemory(requestBody); }
        }
        finally { CryptographicOperations.ZeroMemory(localTicket); }
    }

    public void ClearTicket()
    {
        lock (gate)
        {
            if (ticket is not null) CryptographicOperations.ZeroMemory(ticket);
            ticket = null; expiresUtc = default; launchSessionId = default;
        }
    }

    public void InvalidateTicket()
    {
        ClearTicket();
        StateChanged?.Invoke("Authenticate again before starting the Native Login Canary");
    }
    public DateTimeOffset ExpiresUtc { get { lock (gate) return expiresUtc; } }

    public void Dispose() { ClearTicket(); http.Dispose(); }
}
