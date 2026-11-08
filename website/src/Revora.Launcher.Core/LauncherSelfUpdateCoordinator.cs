using System.Diagnostics;

namespace Revora.Launcher.Core;

public sealed class LauncherSelfUpdateCoordinator(HttpClient httpClient)
{
    public async Task<bool> StartIfRequiredAsync(Version currentVersion, string installRoot,
        LauncherChannel channel, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return false;
        Uri releaseEndpoint = LauncherChannelResolver.LauncherManifest(channel);
        byte[] envelope = await httpClient.GetByteArrayAsync(releaseEndpoint, token);
        LauncherReleaseManifest release = new LauncherReleaseVerifier(TrustedProfileKeys.Production,
            LauncherPolicy.ProductionSigningKeyId).Verify(envelope);
        if (!Version.TryParse(release.Version, out Version? available))
            throw new InvalidDataException("The launcher release version is invalid.");
        if (available <= currentVersion) return false;

        string[] bootstrapperFiles =
        [
            "Revora.Bootstrapper.exe",
            "Revora.Bootstrapper.dll",
            "Revora.Bootstrapper.deps.json",
            "Revora.Bootstrapper.runtimeconfig.json",
            "Revora.Launcher.Core.dll",
        ];
        foreach (string file in bootstrapperFiles)
        {
            string source = Path.Combine(installRoot, file);
            if (!File.Exists(source)) throw new FileNotFoundException("The trusted Revora Bootstrapper package is incomplete.", source);
        }
        string temporary = Path.Combine(Path.GetTempPath(), "Revora", "bootstrap", release.ReleaseId.ToString("N"));
        Directory.CreateDirectory(temporary);
        foreach (string file in bootstrapperFiles)
            File.Copy(Path.Combine(installRoot, file), Path.Combine(temporary, file), true);
        string copied = Path.Combine(temporary, "Revora.Bootstrapper.exe");
        string staging = Path.Combine(installRoot, ".revora-launcher", "self-update", release.ReleaseId.ToString("N"));
        var start = new ProcessStartInfo
        {
            FileName = copied,
            WorkingDirectory = temporary,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("apply"); start.ArgumentList.Add(releaseEndpoint.AbsoluteUri);
        start.ArgumentList.Add(Path.GetFullPath(installRoot)); start.ArgumentList.Add(staging);
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _ = Process.Start(start) ?? throw new InvalidOperationException("The Revora Bootstrapper could not start.");
        return true;
    }
}
