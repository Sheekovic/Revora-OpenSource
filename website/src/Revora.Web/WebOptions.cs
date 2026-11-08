using System.Net;

namespace Revora.Web;

public sealed class WebOptions
{
    public const string SectionName = "RevoraWeb";
    public string PublicBaseUrl { get; init; } = "https://playrevora.com";
    public bool IndexingEnabled { get; init; }
    public bool RegistrationEnabled { get; init; }
    public string GameIdentityRegistrationUrl { get; init; } = string.Empty;
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
        bool validOrigin = Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out Uri? origin) &&
            origin.UserInfo.Length == 0 && origin.AbsolutePath == "/" && origin.Query.Length == 0 && origin.Fragment.Length == 0 &&
            origin.Host.Length > 0 && options.PublicBaseUrl == origin.GetLeftPart(UriPartial.Authority);
        bool localPreview = validOrigin && origin is not null && origin.Scheme == Uri.UriSchemeHttp && environment.IsDevelopment() &&
            IPAddress.TryParse(origin.Host, out IPAddress? previewAddress) && IPAddress.IsLoopback(previewAddress) &&
            !options.RegistrationEnabled && !options.RecoveryEnabled;
        if (!validOrigin || origin is null || (origin.Scheme != Uri.UriSchemeHttps && !localPreview))
        {
            throw new InvalidOperationException("RevoraWeb:PublicBaseUrl must be an explicit HTTPS origin, or a loopback-only development preview with registration/recovery disabled.");
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

        if (options.RegistrationEnabled) _ = RegistrationEndpoint(options);

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

    public static Uri RegistrationEndpoint(WebOptions options)
    {
        if (!Uri.TryCreate(options.GameIdentityRegistrationUrl, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps || endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.AbsolutePath != "/api/v1/identity/register")
            throw new InvalidOperationException("An explicit operator-owned HTTPS game identity registration endpoint is required.");
        return endpoint;
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
