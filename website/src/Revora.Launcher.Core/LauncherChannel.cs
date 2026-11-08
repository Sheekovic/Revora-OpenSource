namespace Revora.Launcher.Core;

public enum LauncherChannel
{
    Stable,
    Test,
}

public static class LauncherChannelResolver
{
    public const string TestMarkerFileName = "Revora.TestChannel";
    public const string TestMarkerContent = "revora-launcher-test-channel-v1";

    public static LauncherChannel Resolve(string installRoot, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Any(value => string.Equals(value, "--test-channel", StringComparison.Ordinal)))
            return LauncherChannel.Test;

        string marker = Path.Combine(Path.GetFullPath(installRoot), TestMarkerFileName);
        if (!File.Exists(marker)) return LauncherChannel.Stable;

        string value;
        try { value = File.ReadAllText(marker); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The test-channel package marker could not be validated.", error);
        }

        if (!string.Equals(value.Trim(), TestMarkerContent, StringComparison.Ordinal))
            throw new InvalidDataException("The test-channel package marker is invalid.");
        return LauncherChannel.Test;
    }

    public static Uri ClientManifest(LauncherChannel channel) => channel switch
    {
        LauncherChannel.Stable => LauncherPolicy.StableManifest,
        LauncherChannel.Test => LauncherPolicy.TestManifest,
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };

    public static Uri LauncherManifest(LauncherChannel channel) => channel switch
    {
        LauncherChannel.Stable => LauncherPolicy.LauncherReleaseManifest,
        LauncherChannel.Test => LauncherPolicy.TestLauncherReleaseManifest,
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };
}
