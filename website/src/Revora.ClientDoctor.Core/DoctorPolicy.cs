namespace Revora.ClientDoctor.Core;

public static class DoctorPolicy
{
    public const int MaximumFiles = 50_000;
    public const long MaximumSingleFileBytes = 4L * 1024 * 1024 * 1024;
    public const long MaximumTotalBytes = 32L * 1024 * 1024 * 1024;

    private static readonly HashSet<string> ActiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".ocx", ".scr", ".com", ".msi", ".msp", ".sys", ".drv", ".cpl",
        ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".wsf", ".hta", ".jar", ".lnk",
        ".url", ".reg", ".swf", ".zip", ".7z", ".rar", ".cab"
    };

    public static bool IsAlwaysActive(string path) => ActiveExtensions.Contains(Path.GetExtension(path));

    public static bool IsDoctorBundleFile(string path)
    {
        string name = Path.GetFileName(path);
        return name.StartsWith("Revora.ClientDoctor", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("revora-testing-profile.json", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("WINDOWS-TEST-", StringComparison.OrdinalIgnoreCase) ||
            (IsAlwaysActive(path) && path.Contains("client-doctor", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly HashSet<string> PassiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wdf", ".dmap", ".map", ".dds", ".tga", ".jpg", ".jpeg", ".png", ".bmp", ".wav", ".mp3",
        ".c3", ".ani", ".ini", ".dat", ".txt"
    };

    public static bool IsApprovedPassiveExtension(string path) => PassiveExtensions.Contains(Path.GetExtension(path));
}
