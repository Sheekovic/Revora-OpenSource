using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PlayRevora.Launcher.Models;

namespace PlayRevora.Launcher.Services;

public static class VerificationLifecycle
{
    public static VerificationAction Decide(VerificationContext context)
    {
        if (context.PendingInstall) return VerificationAction.RequireRecovery;
        if (context.ExplicitRequest) return VerificationAction.RunRepairCatalog;
        return VerificationAction.VerifyCriticalLaunchSet;
    }
}

public sealed class RevoraVersionStore(string clientRoot)
{
    public const int MaximumBytes = 16 * 1024;
    private readonly string path = SafePaths.UnderRoot(clientRoot, "RevoraVersion.ini");
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RequiredKeys = ["SchemaVersion", "ClientPatch", "ContentVersion", "Channel", "LastSuccessfulPatch"];

    public async Task<RevoraClientVersion> ReadRequiredAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new InvalidDataException("Local client version state is missing.");
        FileInfo info = new(path);
        if (info.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Local client version state has an invalid size.");
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        string text;
        try { text = StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException exception) { throw new InvalidDataException("Local client version state is not strict UTF-8.", exception); }
        return Parse(text);
    }

    public async Task WriteAtomicAsync(RevoraClientVersion version, CancellationToken cancellationToken)
    {
        Validate(version);
        string content = $"[Revora]\nSchemaVersion={version.SchemaVersion}\nClientPatch={version.ClientPatch}\nContentVersion={version.ContentVersion}\nChannel={version.Channel}\nLastSuccessfulPatch={version.LastSuccessfulPatch}\n";
        byte[] bytes = StrictUtf8.GetBytes(content);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static RevoraClientVersion Parse(string text)
    {
        if (StrictUtf8.GetByteCount(text) > MaximumBytes) throw new InvalidDataException("Local client version state is oversized.");
        string? section = null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                string candidate = line[1..^1];
                if (section is not null || candidate != "Revora") throw new InvalidDataException("Duplicate or unknown version section.");
                section = candidate; continue;
            }
            if (section != "Revora") throw new InvalidDataException("Version key is outside the Revora section.");
            int separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1) throw new InvalidDataException("Malformed version key.");
            string key = line[..separator].Trim(); string value = line[(separator + 1)..].Trim();
            if (!RequiredKeys.Contains(key, StringComparer.Ordinal) || !values.TryAdd(key, value))
                throw new InvalidDataException("Duplicate or unknown version key.");
        }
        if (section != "Revora" || RequiredKeys.Any(key => !values.ContainsKey(key)))
            throw new InvalidDataException("Required version metadata is missing.");
        if (!int.TryParse(values["SchemaVersion"], out int schema) || !int.TryParse(values["ClientPatch"], out int patch))
            throw new InvalidDataException("Invalid numeric version metadata.");
        var result = new RevoraClientVersion(schema, patch, SemanticVersion.Parse(values["ContentVersion"]).ToString(),
            values["Channel"], values["LastSuccessfulPatch"]);
        Validate(result); return result;
    }

    private static void Validate(RevoraClientVersion version)
    {
        if (version.SchemaVersion != 1 || version.ClientPatch <= 0
            || !string.Equals(SemanticVersion.Parse(version.ContentVersion).ToString(), version.ContentVersion, StringComparison.Ordinal)
            || !IsToken(version.Channel) || !IsToken(version.LastSuccessfulPatch))
            throw new InvalidDataException("Local client version metadata is invalid or unsupported.");
    }

    private static bool IsToken(string value) => value.Length is > 0 and <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
}

public sealed class VerificationReceiptStore(string clientRoot, string relativePath, IClock clock)
{
    private readonly string path = SafePaths.UnderRoot(clientRoot, relativePath);

    public async Task<VerificationReceipt?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            if (bytes.Length is <= 0 or > 64 * 1024) return null;
            return JsonSerializer.Deserialize<VerificationReceipt>(bytes, new JsonSerializerOptions
            { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow });
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    public Task WriteAsync(string clientRoot, string manifestDigest, string channel, RevoraClientVersion version,
        CancellationToken cancellationToken) => JsonFiles.WriteAtomicAsync(path, new VerificationReceipt
        {
            SchemaVersion = 1,
            ClientRootIdentity = ClientRootIdentity(clientRoot),
            ManifestSha256 = manifestDigest.ToUpperInvariant(),
            BuildChannel = channel,
            ClientPatch = version.ClientPatch,
            ContentVersion = version.ContentVersion,
            ManifestId = version.LastSuccessfulPatch,
            CompletedAtUtc = clock.UtcNow
        }, cancellationToken);

    public static bool Matches(VerificationReceipt? receipt, string clientRoot, string manifestDigest,
        string channel, RevoraClientVersion version) => receipt is
        {
            SchemaVersion: 1,
            CompletedAtUtc: var completed
        } && completed != default
        && FixedEquals(receipt.ClientRootIdentity, ClientRootIdentity(clientRoot))
        && FixedEquals(receipt.ManifestSha256.ToUpperInvariant(), manifestDigest.ToUpperInvariant())
        && string.Equals(receipt.BuildChannel, channel, StringComparison.Ordinal)
        && receipt.ClientPatch == version.ClientPatch
        && string.Equals(receipt.ContentVersion, version.ContentVersion, StringComparison.Ordinal)
        && string.Equals(receipt.ManifestId, version.LastSuccessfulPatch, StringComparison.Ordinal);

    public void Invalidate()
    {
        if (File.Exists(path)) File.Delete(path);
    }

    public static string ClientRootIdentity(string clientRoot)
    {
        string normalized = Path.GetFullPath(clientRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static bool FixedEquals(string left, string right)
    {
        byte[] a = Encoding.UTF8.GetBytes(left); byte[] b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
