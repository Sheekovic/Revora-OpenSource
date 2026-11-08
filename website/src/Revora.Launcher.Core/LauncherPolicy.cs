namespace Revora.Launcher.Core;

public static class LauncherPolicy
{
    public const string ProductName = "Revora Launcher";
    public const string ProductVersion = "1.0.8";
    public const string AppUserModelId = "Revora.Launcher.Stable";
    public const string ServerHost = "playrevora.com";
    public const string TestingDownloadHost = "admin.playrevora.com";
    public const int AccountPort = 9958;
    public const int GamePort = 9959;
    public const string ClientExecutable = "Conquer.exe";
    public const string SigningKeyId = "revora-launcher-production-v1";
    public const string TestingSigningKeyId = "revora-client-testing-2026-01";
    public const string ProductionSigningKeyId = "revora-client-production-2026-01";
    public const int MaximumManifestBytes = 8_388_608;
    public const long MaximumFileBytes = 4L * 1024 * 1024 * 1024;
    public const long MaximumClientBytes = 32L * 1024 * 1024 * 1024;
    public static readonly Uri StableManifest = new("https://playrevora.com/patch/v2/stable/manifest.json");
    public static readonly Uri TestManifest = new("https://playrevora.com/patch/v2/test/manifest.json");
    public static readonly Uri LauncherReleaseManifest = new("https://playrevora.com/launcher/v1/stable/manifest.json");
    public static readonly Uri TestLauncherReleaseManifest = new("https://playrevora.com/launcher/v1/test/manifest.json");
}
