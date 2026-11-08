using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Revora.Web;

public sealed record PatchFile(string Destination, long Size, string Sha256, string DownloadUrl);
public sealed record PatchManifest(
    int SchemaVersion,
    string Channel,
    string Version,
    string MinimumLauncherVersion,
    DateTimeOffset CreatedAt,
    string? MaintenanceMessage,
    string SignatureAlgorithm,
    string SigningKeyId,
    string Signature,
    IReadOnlyCollection<PatchFile> Files);

public static partial class PatchManifestValidator
{
    public const string EmbeddedSigningKeyId = "revora-offline-signing-v1";
    public const string EmbeddedVerificationPublicKey = "j7JPl2hEqvCAyC4VXeJzkvrq7PySPRxhmNKWcEaMCtQ=";
    private static readonly HashSet<string> AllowedChannels = new(StringComparer.Ordinal) { "stable", "beta" };
    private static readonly HashSet<string> AllowedAlgorithms = new(StringComparer.Ordinal) { "Ed25519" };

    public static IReadOnlyCollection<string> Validate(PatchManifest manifest)
    {
        List<string> errors = [];
        if (manifest.SchemaVersion != 1) errors.Add("Unsupported schema version.");
        if (!AllowedChannels.Contains(manifest.Channel)) errors.Add("Unknown update channel.");
        if (!VersionPattern().IsMatch(manifest.Version) || !VersionPattern().IsMatch(manifest.MinimumLauncherVersion)) errors.Add("Versions must use numeric semantic versioning.");
        if (!AllowedAlgorithms.Contains(manifest.SignatureAlgorithm) || manifest.SigningKeyId != EmbeddedSigningKeyId) errors.Add("Unknown signing policy.");
        if (!Base64SignaturePattern().IsMatch(manifest.Signature)) errors.Add("Signature encoding is invalid.");
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (PatchFile file in manifest.Files)
        {
            if (!IsSafeDestination(file.Destination)) errors.Add($"Unsafe destination: {file.Destination}");
            else if (!paths.Add(file.Destination)) errors.Add($"Duplicate destination: {file.Destination}");
            if (file.Size <= 0) errors.Add($"Invalid size: {file.Destination}");
            if (!Sha256Pattern().IsMatch(file.Sha256)) errors.Add($"Invalid SHA-256: {file.Destination}");
            if (!Uri.TryCreate(file.DownloadUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)) errors.Add($"Invalid HTTPS URL: {file.Destination}");
        }
        return new ReadOnlyCollection<string>(errors);
    }

    public static bool IsSafeDestination(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination) || destination.Length > 240 || destination.StartsWith('/') || destination.StartsWith('\\') || destination.Contains('\\') || destination.Any(char.IsControl)) return false;
        string[] parts = destination.Split('/', StringSplitOptions.None);
        return parts.All(part => part.Length > 0 && part is not "." and not ".." && !part.Contains(':'));
    }

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[a-z0-9.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
    [GeneratedRegex("^[A-Za-z0-9+/]{80,120}={0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex Base64SignaturePattern();
}
