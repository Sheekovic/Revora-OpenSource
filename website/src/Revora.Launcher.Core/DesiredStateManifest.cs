using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Revora.Launcher.Core;

public sealed record DesiredStateFile(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("contentVersion")] string ContentVersion,
    [property: JsonPropertyName("objectPath")] string ObjectPath,
    [property: JsonPropertyName("compression")] string Compression,
    [property: JsonPropertyName("compressedSize")] long? CompressedSize,
    [property: JsonPropertyName("required")] bool Required,
    [property: JsonPropertyName("critical")] bool Critical,
    [property: JsonPropertyName("replacementPolicy")] string ReplacementPolicy,
    [property: JsonPropertyName("executable")] bool Executable = false);

public sealed record DesiredStateManifest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("releaseId")] Guid ReleaseId,
    [property: JsonPropertyName("clientBuild")] string ClientBuild,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("publishedAtUtc")] DateTimeOffset PublishedAtUtc,
    [property: JsonPropertyName("minimumLauncherVersion")] string MinimumLauncherVersion,
    [property: JsonPropertyName("supportedExecutableSha256")] string SupportedExecutableSha256,
    [property: JsonPropertyName("manifestBaseUri")] string ManifestBaseUri,
    [property: JsonPropertyName("originBaseUri")] string OriginBaseUri,
    [property: JsonPropertyName("serverHost")] string ServerHost,
    [property: JsonPropertyName("accountPort")] int AccountPort,
    [property: JsonPropertyName("gamePort")] int GamePort,
    [property: JsonPropertyName("rollbackOfReleaseId")] Guid? RollbackOfReleaseId,
    [property: JsonPropertyName("maintenanceMessage")] string? MaintenanceMessage,
    [property: JsonPropertyName("files")] IReadOnlyList<DesiredStateFile> Files);

public sealed partial class DesiredStateManifestVerifier
{
    private readonly byte[] publicKey;
    private readonly string keyId;

    public DesiredStateManifestVerifier(ReadOnlySpan<byte> publicKey, string keyId)
    {
        this.publicKey = publicKey.ToArray();
        this.keyId = keyId;
    }

    public DesiredStateManifest Verify(ReadOnlySpan<byte> envelopeBytes, Version launcherVersion,
        Guid? previouslyAcceptedRelease = null, DateTimeOffset? previouslyAcceptedPublishedAt = null)
    {
        if (envelopeBytes.Length is 0 or > LauncherPolicy.MaximumManifestBytes)
            throw new InvalidDataException("The desired-state manifest size is invalid.");
        SignedEnvelope envelope = JsonSerializer.Deserialize<SignedEnvelope>(envelopeBytes)
            ?? throw new InvalidDataException("The desired-state envelope is empty.");
        if (!string.Equals(envelope.KeyId, keyId, StringComparison.Ordinal) || envelope.Algorithm != "ECDSA-P256-SHA256")
            throw new CryptographicException("The desired-state manifest uses an untrusted signing policy.");
        byte[] payload;
        byte[] signature;
        try { payload = Convert.FromBase64String(envelope.Payload); signature = Convert.FromBase64String(envelope.Signature); }
        catch (FormatException error) { throw new InvalidDataException("The desired-state envelope encoding is invalid.", error); }
        try
        {
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKey, out int consumed);
            if (consumed != publicKey.Length ||
                !verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
                throw new CryptographicException("The desired-state manifest signature is invalid.");
            DesiredStateManifest manifest = JsonSerializer.Deserialize<DesiredStateManifest>(payload)
                ?? throw new InvalidDataException("The desired-state manifest is empty.");
            Validate(manifest, launcherVersion, previouslyAcceptedRelease, previouslyAcceptedPublishedAt);
            return manifest;
        }
        finally { CryptographicOperations.ZeroMemory(payload); CryptographicOperations.ZeroMemory(signature); }
    }

    public static void Validate(DesiredStateManifest manifest, Version launcherVersion,
        Guid? previouslyAcceptedRelease = null, DateTimeOffset? previouslyAcceptedPublishedAt = null)
    {
        if (manifest.SchemaVersion != 2 || manifest.ReleaseId == Guid.Empty ||
            manifest.Channel is not ("stable" or "test") ||
            !Version.TryParse(manifest.MinimumLauncherVersion, out Version? minimum) || launcherVersion < minimum ||
            !BuildPattern().IsMatch(manifest.ClientBuild) || !ShaPattern().IsMatch(manifest.SupportedExecutableSha256) ||
            manifest.ServerHost != LauncherPolicy.ServerHost || manifest.AccountPort != LauncherPolicy.AccountPort ||
            manifest.GamePort != LauncherPolicy.GamePort || manifest.Files.Count is 0 or > 50_000)
            throw new InvalidDataException("The desired-state manifest metadata is invalid.");
        ValidateBaseUri(manifest.ManifestBaseUri);
        ValidateBaseUri(manifest.OriginBaseUri);
        if (previouslyAcceptedPublishedAt is not null && manifest.PublishedAtUtc < previouslyAcceptedPublishedAt &&
            manifest.RollbackOfReleaseId != previouslyAcceptedRelease)
            throw new InvalidDataException("The desired-state manifest is an unauthorized downgrade.");

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (DesiredStateFile file in manifest.Files)
        {
            if (!SafePath.IsNormalizedRelative(file.Path) || IsLauncherOwnedPath(file.Path) ||
                !paths.Add(file.Path) || file.Size < 0 ||
                file.Size > LauncherPolicy.MaximumFileBytes || !ShaPattern().IsMatch(file.Sha256) ||
                !VersionTextPattern().IsMatch(file.ContentVersion) || !SafeObjectPath(file.ObjectPath, file.Sha256) ||
                file.Compression is not ("none" or "gzip") || file.CompressedSize is < 0 ||
                file.ReplacementPolicy is not ("replace" or "preserve-if-present"))
                throw new InvalidDataException("The desired-state manifest contains an invalid file.");
            total = checked(total + file.Size);
            if (total > LauncherPolicy.MaximumClientBytes) throw new InvalidDataException("The desired-state client exceeds policy bounds.");
        }
        DesiredStateFile[] executable = manifest.Files.Where(file => string.Equals(file.Path, LauncherPolicy.ClientExecutable, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (executable.Length != 1 || !executable[0].Critical || executable[0].Sha256 != manifest.SupportedExecutableSha256)
            throw new InvalidDataException("The desired-state executable identity is incomplete.");
    }

    private static void ValidateBaseUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !string.Equals(uri.Host, LauncherPolicy.ServerHost, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A patch base URI violates fixed HTTPS policy.");
    }

    private static bool SafeObjectPath(string objectPath, string sha256) =>
        SafePath.IsNormalizedRelative(objectPath) &&
        string.Equals(objectPath, $"objects/sha256/{sha256[..2]}/{sha256}", StringComparison.Ordinal);

    private static bool IsLauncherOwnedPath(string path) =>
        path.StartsWith(".revora-launcher/", StringComparison.OrdinalIgnoreCase) ||
        path.Equals(".revora-launcher", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("Revora.Launcher", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("Revora.Bootstrapper", StringComparison.OrdinalIgnoreCase) ||
        path.Equals(LauncherChannelResolver.TestMarkerFileName, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{2,95}$", RegexOptions.CultureInvariant)] private static partial Regex BuildPattern();
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)] private static partial Regex ShaPattern();
    [GeneratedRegex("^[0-9A-Za-z][0-9A-Za-z._-]{0,63}$", RegexOptions.CultureInvariant)] private static partial Regex VersionTextPattern();
}
