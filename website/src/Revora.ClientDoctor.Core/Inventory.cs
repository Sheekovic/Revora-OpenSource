using System.Security.Cryptography;
using Revora.Launcher.Core;

namespace Revora.ClientDoctor.Core;

public enum DonorFileState { ReusablePassive, ActiveUntrusted, Mismatch, Unexpected, SelfExcluded, DisguisedExecutable, UnsafePath }
public sealed record DonorFile(string RelativePath, long Size, string Sha256, DonorFileState State, string Reason);
public sealed record DoctorInventory(string PatchEvidence, int? DetectedPatch, bool RecognizedDonor, IReadOnlyList<DonorFile> Files, long TotalBytes, bool SourceUnsafe);

public sealed class SourceInventory
{
    public async Task<DoctorInventory> InspectAsync(string sourceRoot, ClientProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot)) throw new InvalidDataException("Select a donor directory before scanning.");
        string root = Path.GetFullPath(sourceRoot);
        RejectUnsafeRoot(root);
        var expected = profile.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        var result = new List<DonorFile>();
        long total = 0;

        var foldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Count >= DoctorPolicy.MaximumFiles) throw new InvalidDataException("The source contains too many files.");
            SafePath.RejectExistingReparsePoints(root, path);
            string relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            if (!SafePath.IsNormalizedRelative(relative))
            {
                result.Add(new(relative, 0, string.Empty, DonorFileState.UnsafePath, "Unsafe relative path"));
                continue;
            }
            if (!foldedPaths.Add(relative))
                throw new InvalidDataException("The donor contains a case-colliding path.");

            var info = new FileInfo(path);
            if (info.Length > DoctorPolicy.MaximumSingleFileBytes || checked(total += info.Length) > DoctorPolicy.MaximumTotalBytes)
                throw new InvalidDataException("The source exceeds the bounded inventory size.");
            string hash = await HashAsync(path, cancellationToken);
            bool mz = await HasMzHeaderAsync(path, cancellationToken);
            DonorFileState state;
            string reason;
            if (DoctorPolicy.IsDoctorBundleFile(relative)) { state = DonorFileState.SelfExcluded; reason = "Client Doctor bundle or unrelated archive is excluded"; }
            else if (DoctorPolicy.IsAlwaysActive(relative)) { state = DonorFileState.ActiveUntrusted; reason = "Active content is never reused"; }
            else if (mz) { state = DonorFileState.DisguisedExecutable; reason = "Executable header under a passive extension"; }
            else if (!expected.TryGetValue(relative, out ClientFile? file)) { state = DonorFileState.Unexpected; reason = "Not present in the signed profile"; }
            else if (file.Role != "passive") { state = DonorFileState.ActiveUntrusted; reason = "Profile requires a repository copy"; }
            else if (!DoctorPolicy.IsApprovedPassiveExtension(relative)) { state = DonorFileState.Unexpected; reason = "Extension is not an approved passive type"; }
            else if (file.Size != info.Length || !hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) { state = DonorFileState.Mismatch; reason = "Size or SHA-256 mismatch"; }
            else { state = DonorFileState.ReusablePassive; reason = "Exact signed passive asset"; }
            result.Add(new(relative, info.Length, hash, state, reason));
        }

        int? detectedPatch = await ReadVersionAsync(root, cancellationToken);
        var inventoried = result.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
        ClientFile[] recognizableFiles = profile.Files.Where(file => file.Role == "passive").ToArray();
        int exactProfileMatches = recognizableFiles.Count(file => inventoried.TryGetValue(file.Path, out DonorFile? donor) &&
            donor.Size == file.Size && donor.Sha256.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase));
        int requiredMatches = Math.Max(1, (int)Math.Ceiling(recognizableFiles.Length * 0.95));
        bool recognized = detectedPatch == profile.ClientPatch && exactProfileMatches >= requiredMatches &&
            profile.BuildId.StartsWith("doctor-reconstruction-5165-", StringComparison.Ordinal);
        string version = detectedPatch is int patch ? $"version.dat={patch}" : "version.dat missing or invalid";
        return new(version, detectedPatch, recognized, result, total,
            result.Any(file => file.State is DonorFileState.ActiveUntrusted or DonorFileState.DisguisedExecutable or DonorFileState.UnsafePath));
    }

    private static async Task<int?> ReadVersionAsync(string root, CancellationToken token)
    {
        string path = Path.Combine(root, "version.dat");
        if (!File.Exists(path)) return null;
        SafePath.RejectExistingReparsePoints(root, path);
        string value = (await File.ReadAllTextAsync(path, token)).Trim();
        return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int patch) ? patch : null;
    }

    public static void ValidateDestination(string sourceRoot, string destination)
    {
        string source = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        RejectUnsafeRoot(target);
        if (target.StartsWith(source, StringComparison.OrdinalIgnoreCase) || source.StartsWith(target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The destination and source must be separate trees.");
        SafePath.RejectExistingReparsePoints(Path.GetPathRoot(target)!, target);
    }

    private static void RejectUnsafeRoot(string root)
    {
        if (!Path.IsPathFullyQualified(root) || root.StartsWith("\\\\", StringComparison.Ordinal) ||
            root.Equals(Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A local non-root directory is required.");
        if (OperatingSystem.IsWindows())
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (root.StartsWith(windows, StringComparison.OrdinalIgnoreCase) || root.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("System and Program Files locations are forbidden.");
        }
    }

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
    }

    private static async Task<bool> HasMzHeaderAsync(string path, CancellationToken token)
    {
        byte[] header = new byte[2];
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 2, FileOptions.Asynchronous);
        return await stream.ReadAsync(header, token) == 2 && header[0] == (byte)'M' && header[1] == (byte)'Z';
    }
}
