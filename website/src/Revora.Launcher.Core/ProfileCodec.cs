using System.Text.Json;

namespace Revora.Launcher.Core;

public static class ProfileCodec
{
    public static byte[] SerializeCanonical(ClientProfile profile)
    {
        ClientProfile ordered = profile with { Files = profile.Files.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray() };
        ManifestVerifier.ValidatePolicy(ordered);
        return JsonSerializer.SerializeToUtf8Bytes(ordered);
    }
}
