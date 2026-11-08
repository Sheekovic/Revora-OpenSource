using System.Net;

namespace Revora.Web;

public sealed class WebOptions
{
    public const string SectionName = "RevoraWeb";
    public string PublicBaseUrl { get; init; } = "https://playrevora.com";
    public bool IndexingEnabled { get; init; }
    public bool RegistrationEnabled { get; init; }
    public bool RecoveryEnabled { get; init; }
    public bool MaintenanceMode { get; init; }
    public string RuntimeMode { get; init; } = "Unconfigured";
    public string DataProtectionKeysPath { get; init; } = string.Empty;
    public string DatabaseConnectionString { get; init; } = string.Empty;
    public string SecurityHmacKeyBase64 { get; init; } = string.Empty;
    public string SecurityHmacKeyPath { get; init; } = string.Empty;
    public bool InternalGameIntegrationEnabled { get; init; }
    public uint IdentityServerId { get; init; }
    public string IdentityVerificationKeyBase64 { get; init; } = string.Empty;
    public string IdentityVerificationKeyPath { get; init; } = string.Empty;
    public string IdentityClientCertificateSha256 { get; init; } = string.Empty;
    public int MinimumFormSeconds { get; init; } = 2;
    public int NetworkRetentionDays { get; init; } = 30;

    public static void Validate(WebOptions options, IHostEnvironment environment, string[] urls)
    {
        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out Uri? origin) ||
            origin.Scheme != Uri.UriSchemeHttps || origin.Host != "playrevora.com" || origin.PathAndQuery != "/")
        {
            throw new InvalidOperationException("RevoraWeb:PublicBaseUrl must be the canonical HTTPS origin.");
        }

        if (string.Equals(options.RuntimeMode, "Unconfigured", StringComparison.Ordinal) ||
            (environment.IsProduction() && !string.Equals(options.RuntimeMode, "Production", StringComparison.Ordinal) && !string.Equals(options.RuntimeMode, "Development", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Runtime configuration is incomplete or invalid.");
        }

        foreach (string configuredUrl in urls)
        {
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out Uri? endpoint) ||
                !IPAddress.TryParse(endpoint.Host, out IPAddress? address) || !IPAddress.IsLoopback(address))
            {
                throw new InvalidOperationException("Revora.Web may bind only to an explicit loopback IP endpoint.");
            }
        }

        if (options.RegistrationEnabled || options.RecoveryEnabled)
        {
            if (options.RecoveryEnabled && !environment.IsDevelopment()) throw new InvalidOperationException("Public recovery remains disabled until its release gates pass.");
            if (string.IsNullOrWhiteSpace(options.DatabaseConnectionString)) throw new InvalidOperationException("A restricted Revora Web database connection is required.");
            if (string.IsNullOrWhiteSpace(options.SecurityHmacKeyPath))
            {
                byte[] key;
                try { key = Convert.FromBase64String(options.SecurityHmacKeyBase64); }
                catch (FormatException exception) { throw new InvalidOperationException("The Web security HMAC key is invalid.", exception); }
                if (key.Length < 32) throw new InvalidOperationException("The Web security HMAC key must contain at least 256 bits.");
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
            }
        }

        if (options.InternalGameIntegrationEnabled)
        {
            if (options.IdentityServerId == 0) throw new InvalidOperationException("A non-zero identity server ID is required.");
            if (options.IdentityClientCertificateSha256.Length != 64 ||
                !options.IdentityClientCertificateSha256.All(Uri.IsHexDigit))
                throw new InvalidOperationException("The game-server client certificate SHA-256 fingerprint is invalid.");
            byte[] key = LoadIdentityKey(options);
            if (key.Length < 32) throw new InvalidOperationException("The identity verification key must contain at least 256 bits.");
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    public static byte[] LoadIdentityKey(WebOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.IdentityVerificationKeyPath))
        {
            if (!Path.IsPathRooted(options.IdentityVerificationKeyPath))
                throw new InvalidOperationException("The identity verification key path must be absolute.");
            return File.ReadAllBytes(options.IdentityVerificationKeyPath);
        }
        try { return Convert.FromBase64String(options.IdentityVerificationKeyBase64); }
        catch (FormatException exception) { throw new InvalidOperationException("The identity verification key is invalid.", exception); }
    }
}
