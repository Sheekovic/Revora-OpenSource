using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Revora.Persistence.PostgreSql;

namespace Revora.Web;

public sealed class IdentityTokenService : IDisposable
{
    private readonly byte[] key;
    public IdentityTokenService(string base64Key) => key = Convert.FromBase64String(base64Key);
    public IdentityTokenService(byte[] rawKey) { ArgumentNullException.ThrowIfNull(rawKey); if (rawKey.Length < 32) throw new ArgumentException("The security key must contain at least 256 bits.", nameof(rawKey)); key = rawKey.ToArray(); }
    public byte[] Digest(string token) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(token.Trim()));
    public string NewToken(int bytes = 32) => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(bytes));
    public IReadOnlyList<RecoveryCode> NewRecoveryCodes()
    {
        var codes = new List<RecoveryCode>(10);
        for (int index = 0; index < 10; index++)
        {
            string raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            string display = string.Join('-', Enumerable.Range(0, 4).Select(part => raw.Substring(part * 8, 8)));
            codes.Add(new RecoveryCode(display, Digest(raw)));
        }
        return codes;
    }
    public byte[] NetworkDigest(IPAddress? address)
    {
        bool ipv4 = address?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        byte[] source = ipv4 ? address!.GetAddressBytes() : (address?.MapToIPv6().GetAddressBytes() ?? new byte[16]);
        int retained = ipv4 ? 3 : 7;
        Span<byte> prefix = stackalloc byte[2 + retained]; prefix[0] = 2; prefix[1] = ipv4 ? (byte)4 : (byte)6; source.AsSpan(0, retained).CopyTo(prefix[2..]);
        return HMACSHA256.HashData(key, prefix);
    }
    public void Dispose() => CryptographicOperations.ZeroMemory(key);
}

public sealed record RecoveryCode(string Plaintext, byte[] Digest);

public sealed class RecoveryDisplayCache
{
    private readonly ConcurrentDictionary<string, DisplayReceipt> receipts = new(StringComparer.Ordinal);
    public string Add(IReadOnlyList<string> codes)
    {
        string handle = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        receipts[handle] = new DisplayReceipt(codes, DateTimeOffset.UtcNow.AddMinutes(10));
        return handle;
    }
    public IReadOnlyList<string>? Get(string handle) => receipts.TryGetValue(handle, out DisplayReceipt? receipt) && receipt.ExpiresAt > DateTimeOffset.UtcNow ? receipt.Codes : null;
    public void Acknowledge(string handle) => receipts.TryRemove(handle, out _);
    private sealed record DisplayReceipt(IReadOnlyList<string> Codes, DateTimeOffset ExpiresAt);
}

public sealed class FormProofService(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("Revora.Web.FormProof.v1");
    public string Create() => protector.Protect(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
    public bool IsMature(string? proof, int minimumSeconds)
    {
        if (string.IsNullOrWhiteSpace(proof)) return false;
        try
        {
            long issued = long.Parse(protector.Unprotect(proof), System.Globalization.CultureInfo.InvariantCulture);
            long age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issued;
            return age >= minimumSeconds && age <= 3600;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or OverflowException) { return false; }
    }
}

public static class PublicPasswordPolicy
{
    public static bool IsValid(string? password) => password is { Length: >= 8 and <= 15 } && password.All(static value => value is >= '!' and <= '~');
}

public static class AccountCookies
{
    public const string RecoveryDisplay = "__Host-Revora.RecoveryDisplay";
    public const string Reset = "__Host-Revora.Reset";
    public const string Pending = "__Host-Revora.PendingRecovery";
    public const string Device = "__Host-Revora.Device";
    public static CookieOptions ShortLived() => new() { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromMinutes(15), IsEssential = true };
    public static CookieOptions DeviceCookie() => new() { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromDays(180), IsEssential = true };
}

public sealed class IdentityRetentionService(WebAccountStore store, ILogger<IdentityRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { await store.CleanupExpiredAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Npgsql.NpgsqlException) { logger.LogError("Identity retention cleanup failed without exposing record data."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
