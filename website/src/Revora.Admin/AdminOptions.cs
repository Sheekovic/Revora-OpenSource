using System.Net;
using System.Text.Json;

namespace Revora.Admin;

public sealed record AdminOptions
{
    public string Url { get; init; } = "http://127.0.0.1:5081";
    public string PublicBaseUrl { get; init; } = "https://admin.playrevora.com";
    public string RuntimeMode { get; init; } = "Disabled";
    public string SecretFile { get; init; } = "config/revora-admin.secrets.json";
    public string DataProtectionKeysPath { get; init; } = "../data/keys";

    public void Validate()
    {
        if (RuntimeMode != "Production") throw new InvalidDataException("Revora.Admin is fail-closed unless RuntimeMode is Production.");
        if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri? listener) || listener.Scheme != "http" ||
            !IPAddress.TryParse(listener.Host, out IPAddress? address) || !IPAddress.IsLoopback(address) || listener.Port != 5081)
        {
            throw new InvalidDataException("Revora.Admin must listen exactly on loopback port 5081.");
        }

        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out Uri? publicBase) || publicBase.Scheme != "https" || publicBase.Host != "admin.playrevora.com")
        {
            throw new InvalidDataException("The Admin public origin must be https://admin.playrevora.com.");
        }
    }

    public static AdminSecrets LoadSecrets(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Protected Admin secret file is missing.", path);
        if (!OperatingSystem.IsWindows())
        {
            UnixFileMode forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((File.GetUnixFileMode(path) & forbidden) != 0) throw new UnauthorizedAccessException("Admin secret file must be mode 0600 or stricter.");
        }

        return JsonSerializer.Deserialize<AdminSecrets>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Admin secret file is empty.");
    }
}

public sealed record AdminSecrets
{
    public string DatabaseMode { get; init; } = "Disabled";
}
