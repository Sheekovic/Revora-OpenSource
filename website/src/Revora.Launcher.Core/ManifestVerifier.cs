using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Revora.Launcher.Core;

public sealed partial class ManifestVerifier
{
    private readonly byte[] publicKeySubjectPublicKeyInfo;
    private readonly string trustedKeyId;

    public ManifestVerifier(byte[] publicKeySubjectPublicKeyInfo, string trustedKeyId = LauncherPolicy.SigningKeyId)
    {
        this.publicKeySubjectPublicKeyInfo = publicKeySubjectPublicKeyInfo.ToArray();
        this.trustedKeyId = trustedKeyId;
    }

    public ClientProfile VerifyAndRead(ReadOnlySpan<byte> envelopeJson)
    {
        if (envelopeJson.Length is 0 or > LauncherPolicy.MaximumManifestBytes)
        {
            throw new InvalidDataException("The signed profile size is invalid.");
        }

        SignedEnvelope envelope = JsonSerializer.Deserialize<SignedEnvelope>(envelopeJson)
            ?? throw new InvalidDataException("The signed profile is empty.");
        if (envelope.KeyId != trustedKeyId || envelope.Algorithm != "ECDSA-P256-SHA256")
        {
            throw new CryptographicException("The signed profile uses an untrusted signing policy.");
        }

        byte[] payload;
        byte[] signature;
        try
        {
            payload = Convert.FromBase64String(envelope.Payload);
            signature = Convert.FromBase64String(envelope.Signature);
        }
        catch (FormatException error)
        {
            throw new InvalidDataException("The signed profile encoding is invalid.", error);
        }

        using ECDsa verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(publicKeySubjectPublicKeyInfo, out int consumed);
        if (consumed != publicKeySubjectPublicKeyInfo.Length ||
            !verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
        {
            throw new CryptographicException("The signed profile signature is invalid.");
        }

        ClientProfile profile = JsonSerializer.Deserialize<ClientProfile>(payload)
            ?? throw new InvalidDataException("The signed profile payload is empty.");
        ValidatePolicy(profile);
        return profile;
    }

    public static void ValidatePolicy(ClientProfile profile)
    {
        if (profile.SchemaVersion != 1 || profile.ClientPatch != 5165 ||
            profile.ServerHost != LauncherPolicy.ServerHost ||
            profile.AccountPort != LauncherPolicy.AccountPort ||
            profile.GamePort != LauncherPolicy.GamePort ||
            profile.Executable != LauncherPolicy.ClientExecutable)
        {
            throw new InvalidDataException("The client profile does not match the fixed Revora policy.");
        }

        if (profile.Channel is not ("testing" or "production") ||
            !BuildIdPattern().IsMatch(profile.BuildId) ||
            profile.MinimumWindowsBuild < 19045 ||
            profile.LauncherArguments is null || profile.LauncherArguments.Count != 1 ||
            profile.LauncherArguments[0] != "blacknull" ||
            profile.CompatibilityVersion.Length is 0 or > 64)
        {
            throw new InvalidDataException("The client profile metadata is invalid.");
        }

        if (profile.Files.Count is 0 or > 50_000 ||
            profile.Files.Count(file => file.Path == LauncherPolicy.ClientExecutable) != 1)
        {
            throw new InvalidDataException("The client profile file inventory is incomplete.");
        }

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (ClientFile file in profile.Files)
        {
            if (!SafePath.IsNormalizedRelative(file.Path) || !unique.Add(file.Path) ||
                file.Size < 0 || file.Size > LauncherPolicy.MaximumFileBytes || !Sha256Pattern().IsMatch(file.Sha256) ||
                file.Role is not ("passive" or "active" or "configuration") || file.FileType.Length is 0 or > 100)
            {
                throw new InvalidDataException("The client profile contains an invalid file entry.");
            }
            totalBytes = checked(totalBytes + file.Size);
            if (totalBytes > LauncherPolicy.MaximumClientBytes)
                throw new InvalidDataException("The client profile exceeds the bounded client size.");

            if (file.DownloadUrl is not null &&
                (!Uri.TryCreate(file.DownloadUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
                 (!string.Equals(uri.Host, LauncherPolicy.ServerHost, StringComparison.OrdinalIgnoreCase) &&
                  !(profile.Channel == "testing" && string.Equals(uri.Host, LauncherPolicy.TestingDownloadHost, StringComparison.OrdinalIgnoreCase)))))
            {
                throw new InvalidDataException("Client files may only use absolute HTTPS download URLs.");
            }
        }
    }

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex BuildIdPattern();
}
