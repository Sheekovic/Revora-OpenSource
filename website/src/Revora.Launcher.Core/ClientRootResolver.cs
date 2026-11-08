using System.Security.Cryptography;

namespace Revora.Launcher.Core;

public enum ClientRootKind { ExistingClient, RepairableClient, NewInstallation }

public sealed record ClientRootResult(
    string Root,
    bool IsWritable,
    IReadOnlyList<string> FingerprintFiles,
    ClientRootKind Kind);

public sealed class ClientRootResolver
{
    private static readonly string[] Fingerprint = ["Conquer.exe", "version.dat", "ini", "map"];

    public ClientRootResult ResolvePortable(string executablePath, string? selectedRoot = null)
    {
        string launcherDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath))
            ?? throw new InvalidDataException("The launcher directory is unavailable.");
        string root = Path.GetFullPath(selectedRoot ?? launcherDirectory).TrimEnd(Path.DirectorySeparatorChar);
        RejectDangerousRoot(root, launcherDirectory);
        SafePath.RejectExistingReparsePoints(Path.GetPathRoot(root)!, root);

        var found = new List<string>();
        foreach (string entry in Fingerprint)
        {
            string path = Path.Combine(root, entry);
            if (File.Exists(path) || Directory.Exists(path)) found.Add(entry);
        }
        bool hasExecutable = File.Exists(Path.Combine(root, LauncherPolicy.ClientExecutable));
        bool hasVersion = File.Exists(Path.Combine(root, "version.dat"));
        bool hasDataDirectory = Directory.Exists(Path.Combine(root, "ini")) ||
            Directory.Exists(Path.Combine(root, "map"));
        ClientRootKind kind = hasExecutable && hasVersion && hasDataDirectory
            ? ClientRootKind.ExistingClient
            : found.Count > 0
                ? ClientRootKind.RepairableClient
                : ResolveNewInstallationKind(root, launcherDirectory, selectedRoot);

        return new(root, ProbeWrite(root), found, kind);
    }

    private static ClientRootKind ResolveNewInstallationKind(string root, string launcherDirectory,
        string? selectedRoot)
    {
        // A blank client is accepted only where this launcher is running. This prevents a
        // selected arbitrary directory from silently becoming a managed client installation.
        if (selectedRoot is not null || !Same(root, launcherDirectory) ||
            !File.Exists(Path.Combine(root, "Revora.Launcher.exe")))
            throw new InvalidDataException("The selected folder is not a supported Revora client or a new launcher installation folder.");

        return ClientRootKind.NewInstallation;
    }

    public static void RejectDangerousRoot(string root, string launcherDirectory)
    {
        string normalized = NormalizeRootCandidate(root);
        string? driveRoot = Path.GetPathRoot(normalized) is { } value ? NormalizeRootCandidate(value) : null;
        string windows = NormalizeRootCandidate(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        string programFiles = NormalizeRootCandidate(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        string programFilesX86 = NormalizeRootCandidate(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        string profile = NormalizeRootCandidate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (string.Equals(normalized, driveRoot, StringComparison.OrdinalIgnoreCase) ||
            Same(normalized, windows) || Same(normalized, programFiles) || Same(normalized, programFilesX86) || Same(normalized, profile) ||
            normalized.EndsWith(Path.DirectorySeparatorChar + ".revora-launcher", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(Path.Combine(normalized, ".git")) ||
            IsWithin(normalized, Path.Combine(launcherDirectory, ".revora-launcher", "staging")))
            throw new InvalidDataException("The selected folder is unsafe for managed client repair.");
    }

    private static bool ProbeWrite(string root)
    {
        string name = ".revora-write-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        string path = Path.Combine(root, name);
        try
        {
            using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1,
                FileOptions.WriteThrough)) { }
            File.Delete(path);
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
    }

    private static bool Same(string left, string right) => !string.IsNullOrWhiteSpace(right) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static string NormalizeRootCandidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        string full = Path.GetFullPath(path);
        string? volumeRoot = Path.GetPathRoot(full);
        return volumeRoot is not null && string.Equals(full, volumeRoot, StringComparison.OrdinalIgnoreCase)
            ? volumeRoot
            : full.TrimEnd(Path.DirectorySeparatorChar);
    }
    private static bool IsWithin(string root, string candidate)
    {
        string normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar);
        return root.StartsWith(normalizedCandidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Same(root, normalizedCandidate);
    }
}

public sealed class ClientPatchLock : IDisposable
{
    private readonly FileStream stream;
    private ClientPatchLock(FileStream stream) => this.stream = stream;

    public static ClientPatchLock Acquire(string clientRoot)
    {
        string control = Path.Combine(clientRoot, ".revora-launcher");
        Directory.CreateDirectory(control);
        string lockPath = Path.Combine(control, "patch.lock");
        SafePath.RejectExistingReparsePoints(clientRoot, lockPath);
        try { return new(new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.WriteThrough)); }
        catch (IOException error) { throw new IOException("Another Revora Launcher is already managing this client folder.", error); }
    }

    public void Dispose() => stream.Dispose();
}
