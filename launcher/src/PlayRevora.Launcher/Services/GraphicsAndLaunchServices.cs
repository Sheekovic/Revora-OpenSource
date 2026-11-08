using System.Text;
using PlayRevora.Launcher.Models;

namespace PlayRevora.Launcher.Services;

public sealed class GraphicsSettingsService(LauncherConfig config)
{
    private string SettingsPath => SafePaths.UnderRoot(config.GameRoot, @"Env_DX9\revora_render.ini");

    public GraphicsSettings Load()
    {
        var settings = new GraphicsSettings { EnhancedGraphics = false };
        if (!File.Exists(SettingsPath)) return settings;
        var values = File.ReadAllLines(SettingsPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith(";") && !line.StartsWith("[") && line.Contains('='))
            .Select(line => line.Split(['='], 2))
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        settings.DisplayMode = values.TryGetValue("Borderless", out string? borderless) && borderless == "0"
            ? "Standard Windowed" : "Borderless Windowed";
        int.TryParse(values.GetValueOrDefault("Width"), out int width);
        int.TryParse(values.GetValueOrDefault("Height"), out int height);
        settings.Resolution = (width, height) switch
        {
            (1920, 1080) => "1920 × 1080",
            (2560, 1440) => "2560 × 1440 (2K)",
            (3840, 2160) => "3840 × 2160 (4K)",
            _ => "Auto / Native"
        };
        return settings;
    }

    public async Task SaveAsync(GraphicsSettings settings, CancellationToken cancellationToken)
    {
        (int width, int height) = settings.Resolution switch
        {
            "1920 × 1080" => (1920, 1080),
            "2560 × 1440 (2K)" => (2560, 1440),
            "3840 × 2160 (4K)" => (3840, 2160),
            _ => (0, 0)
        };
        string ini = $"[Render]\r\nWidth={width}\r\nHeight={height}\r\nForceWindowed=1\r\nBorderless={(settings.DisplayMode == "Borderless Windowed" ? 1 : 0)}\r\nMonitor=-1\r\nLogPresentEvery=300\r\n";
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        string temporary = SettingsPath + ".tmp";
        await File.WriteAllTextAsync(temporary, ini, new UTF8Encoding(false), cancellationToken);
        File.Move(temporary, SettingsPath, true);
    }
}
