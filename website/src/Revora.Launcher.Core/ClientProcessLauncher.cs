using System.Diagnostics;

namespace Revora.Launcher.Core;

public interface IVerifiedClientProcessLauncher
{
    Process LaunchVerified(string clientRoot, ClientProfile profile, VerificationResult verification);
}

public sealed class ClientProcessLauncher : IVerifiedClientProcessLauncher
{
    public Process LaunchVerified(string clientRoot, ClientProfile profile, VerificationResult verification)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The reviewed game client can only be launched on supported Windows systems.");
        }

        if (!verification.IsValid)
        {
            throw new InvalidOperationException("Client verification must succeed before launch.");
        }

        string executable = SafePath.ResolveContained(clientRoot, profile.Executable);
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetFullPath(clientRoot),
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        start.ArgumentList.Add("blacknull");
        return Process.Start(start) ?? throw new InvalidOperationException("Windows did not start the reviewed client executable.");
    }
}
