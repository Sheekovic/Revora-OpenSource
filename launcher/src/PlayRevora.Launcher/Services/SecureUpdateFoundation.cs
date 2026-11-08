using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PlayRevora.Launcher.Models;

namespace PlayRevora.Launcher.Services;

public sealed class PatchChainResolver
{
    public IReadOnlyList<SignedPatchReference> Resolve(SignedPatchIndex index, string localVersion)
    {
        string current = SemanticVersion.Parse(localVersion).ToString();
        var result = new List<SignedPatchReference>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            List<SignedPatchReference> candidates = index.Patches.Where(patch =>
                SemanticVersion.Parse(patch.SourceVersion).ToString() == current).ToList();
            if (candidates.Count == 0) break;
            if (candidates.Count != 1) throw new InvalidDataException("The signed patch chain is ambiguous.");
            SignedPatchReference next = candidates[0];
            if (!used.Add(next.PatchId)) throw new InvalidDataException("The signed patch chain contains a cycle.");
            if (SemanticVersion.Parse(next.TargetVersion).CompareTo(SemanticVersion.Parse(current)) <= 0)
                throw new InvalidDataException("The signed patch chain does not advance the version.");
            ValidatedClientRoot.ValidateRelative(next.MetadataPath);
            result.Add(next); current = SemanticVersion.Parse(next.TargetVersion).ToString();
        }
        string latest = index.Patches.Count == 0 ? current : index.Patches
            .Select(item => SemanticVersion.Parse(item.TargetVersion)).Max()!.ToString();
        if (SemanticVersion.Parse(current).CompareTo(SemanticVersion.Parse(latest)) < 0)
            throw new InvalidDataException("A required intermediate patch is missing.");
        return result;
    }
}

public sealed class SignedUpdateMetadataVerifier(IClock clock)
{
    public void VerifyIndex(SignedPatchIndex index, ManifestTrust trust, string expectedChannel)
    {
        ValidateWindow(index.SchemaVersion, index.IssuedAtUtc, index.ExpiresAtUtc);
        if (!string.Equals(index.Channel, expectedChannel, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(index.IndexId))
            throw new InvalidDataException("Patch index channel or identity is invalid.");
        VerifySignature(UpdateCanonicalizer.Index(index), index.Signature, trust);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SignedPatchReference patch in index.Patches)
        {
            if (!ids.Add(patch.PatchId) || string.IsNullOrWhiteSpace(patch.PatchId)) throw new InvalidDataException("Duplicate patch identity.");
            _ = SemanticVersion.Parse(patch.SourceVersion); _ = SemanticVersion.Parse(patch.TargetVersion);
            ValidatedClientRoot.ValidateRelative(patch.MetadataPath);
        }
    }

    public void VerifyPackage(SignedPatchPackage package, ManifestTrust trust, string expectedChannel,
        SignedPatchReference reference)
    {
        ValidateWindow(package.SchemaVersion, package.IssuedAtUtc, package.ExpiresAtUtc);
        if (package.Revoked || package.PatchId != reference.PatchId || package.Channel != expectedChannel
            || SemanticVersion.Parse(package.SourceVersion).ToString() != SemanticVersion.Parse(reference.SourceVersion).ToString()
            || SemanticVersion.Parse(package.TargetVersion).ToString() != SemanticVersion.Parse(reference.TargetVersion).ToString())
            throw new InvalidDataException("Patch package binding is invalid or revoked.");
        ValidateHash(package.ArchiveSize, package.ArchiveSha256); ValidateHash(0, package.TargetManifestSha256, false);
        ValidatedClientRoot.ValidateRelative(package.ArchivePath);
        ValidatedClientRoot.ValidateRelative(package.TargetManifestPath);
        var archive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var target = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PatchArchiveFile file in package.Files)
        {
            ValidateHash(file.Size, file.Sha256);
            ValidateArchivePath(file.ArchivePath); ValidatedClientRoot.ValidateRelative(file.TargetPath);
            if (ClientMutablePathPolicy.IsExcluded(file.TargetPath)
                && !Normalize(file.TargetPath).Equals(Normalize(package.TargetManifestPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Patch content may not overwrite launcher-owned mutable state.");
            if (!archive.Add(Normalize(file.ArchivePath)) || !target.Add(Normalize(file.TargetPath)))
                throw new InvalidDataException("Patch package contains duplicate or case-colliding paths.");
        }
        foreach (string deletedPath in package.DeletedFiles)
        {
            ValidateArchivePath(deletedPath);
            string normalized = Normalize(deletedPath);
            if (!target.Add(normalized) || ClientMutablePathPolicy.IsExcluded(deletedPath))
                throw new InvalidDataException("Patch deletion contains a duplicate, collision, or protected mutable path.");
        }
        if (!target.Contains(Normalize(package.TargetManifestPath)))
            throw new InvalidDataException("Patch package does not install its declared target manifest.");
        VerifySignature(UpdateCanonicalizer.Package(package), package.Signature, trust);
    }

    private void ValidateWindow(int schema, DateTimeOffset issued, DateTimeOffset expires)
    {
        if (schema != 1 || issued > clock.UtcNow || expires <= clock.UtcNow || expires <= issued)
            throw new InvalidDataException("Signed update metadata is unsupported or outside its validity window.");
    }

    private static void VerifySignature(byte[] payload, ManifestSignature signature, ManifestTrust trust)
    {
        if (signature.Algorithm != "ECDSA_P256_SHA256" || signature.KeyId != trust.KeyId)
            throw new CryptographicException("Update signature trust does not match.");
        using ECDsa key = ECDsa.Create(); key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(trust.PublicKeySpkiBase64), out _);
        if (!key.VerifyData(payload, Convert.FromBase64String(signature.ValueBase64), HashAlgorithmName.SHA256))
            throw new CryptographicException("Update signature is invalid.");
    }

    private static void ValidateHash(long size, string hash, bool validateSize = true)
    {
        if ((validateSize && size < 0) || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new InvalidDataException("Patch integrity metadata is invalid.");
    }

    internal static string Normalize(string path) => path.Replace('\\', '/');
    public static void ValidateArchivePath(string path)
    {
        ValidatedClientRoot.ValidateRelative(path);
        foreach (string segment in Normalize(path).Split('/'))
        {
            string stem = segment.Split('.')[0];
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
                || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9')
                || segment.EndsWith(' ') || segment.EndsWith('.'))
                throw new InvalidDataException("Archive path uses a reserved Windows name.");
        }
    }
}

public static class UpdateCanonicalizer
{
    public static byte[] Index(SignedPatchIndex value) => Write(writer =>
    {
        writer.WriteNumber("schemaVersion", value.SchemaVersion); writer.WriteString("indexId", value.IndexId);
        writer.WriteString("channel", value.Channel); writer.WriteString("issuedAtUtc", value.IssuedAtUtc.ToUniversalTime());
        writer.WriteString("expiresAtUtc", value.ExpiresAtUtc.ToUniversalTime()); writer.WritePropertyName("patches"); writer.WriteStartArray();
        foreach (SignedPatchReference patch in value.Patches.OrderBy(item => item.PatchId, StringComparer.Ordinal))
        { writer.WriteStartObject(); writer.WriteString("patchId", patch.PatchId); writer.WriteString("sourceVersion", patch.SourceVersion); writer.WriteString("targetVersion", patch.TargetVersion); writer.WriteString("metadataPath", patch.MetadataPath); writer.WriteBoolean("mandatory", patch.Mandatory); writer.WriteEndObject(); }
        writer.WriteEndArray();
    });

    public static byte[] Package(SignedPatchPackage value) => Write(writer =>
    {
        writer.WriteNumber("schemaVersion", value.SchemaVersion); writer.WriteString("patchId", value.PatchId); writer.WriteString("channel", value.Channel);
        writer.WriteString("sourceVersion", value.SourceVersion); writer.WriteString("targetVersion", value.TargetVersion);
        writer.WriteString("issuedAtUtc", value.IssuedAtUtc.ToUniversalTime()); writer.WriteString("expiresAtUtc", value.ExpiresAtUtc.ToUniversalTime());
        writer.WriteBoolean("revoked", value.Revoked); writer.WriteString("archivePath", value.ArchivePath); writer.WriteNumber("archiveSize", value.ArchiveSize);
        writer.WriteString("archiveSha256", value.ArchiveSha256.ToUpperInvariant()); writer.WriteString("targetManifestId", value.TargetManifestId);
        writer.WriteString("targetManifestPath", value.TargetManifestPath);
        writer.WriteString("targetManifestSha256", value.TargetManifestSha256.ToUpperInvariant()); writer.WritePropertyName("files"); writer.WriteStartArray();
        foreach (PatchArchiveFile file in value.Files.OrderBy(item => item.ArchivePath, StringComparer.Ordinal))
        { writer.WriteStartObject(); writer.WriteString("archivePath", file.ArchivePath); writer.WriteString("targetPath", file.TargetPath); writer.WriteNumber("size", file.Size); writer.WriteString("sha256", file.Sha256.ToUpperInvariant()); writer.WriteEndObject(); }
        writer.WriteEndArray(); writer.WritePropertyName("deletedFiles"); writer.WriteStartArray();
        foreach (string path in value.DeletedFiles.OrderBy(item => item, StringComparer.Ordinal)) writer.WriteStringValue(path.Replace('\\', '/'));
        writer.WriteEndArray();
    });

    private static byte[] Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) { writer.WriteStartObject(); body(writer); writer.WriteEndObject(); }
        return stream.ToArray();
    }
}

public sealed class SafePatchExtractor
{
    public async Task ExtractAsync(string archivePath, string stagingRoot, IReadOnlyList<PatchArchiveFile> declared,
        IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stagingRoot);
        if ((File.GetAttributes(stagingRoot) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Patch staging root is a reparse point.");
        var expected = declared.ToDictionary(item => SignedUpdateMetadataVerifier.Normalize(item.ArchivePath), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        long totalBytes = declared.Sum(item => item.Size); long completed = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/') || string.IsNullOrEmpty(entry.Name)) throw new InvalidDataException("Directory archive entries are not supported.");
            SignedUpdateMetadataVerifier.ValidateArchivePath(entry.FullName);
            string normalized = SignedUpdateMetadataVerifier.Normalize(entry.FullName);
            if (!seen.Add(normalized) || !expected.TryGetValue(normalized, out PatchArchiveFile? metadata))
                throw new InvalidDataException("Archive contains an unexpected or duplicate entry.");
            int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixType is 0xA000 or 0x6000) throw new InvalidDataException("Archive links and special files are forbidden.");
            string target = SafePaths.UnderRoot(stagingRoot, normalized);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (Stream input = entry.Open())
            await using (FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output, cancellationToken);
            if (!await FileIntegrity.MatchesAsync(target, metadata.Size, metadata.Sha256, cancellationToken))
                throw new InvalidDataException("A staged patch file failed integrity verification.");
            if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("A staged patch file is a reparse point.");
            completed += metadata.Size;
            progress?.Report(new("Preparing update", totalBytes == 0 ? 100 : completed * 100d / totalBytes, 100, $"{seen.Count}/{declared.Count} files"));
        }
        if (seen.Count != expected.Count) throw new InvalidDataException("Archive is missing a declared file.");
    }
}

public interface IInstallationGuard { Task EnsureExclusiveAsync(CancellationToken cancellationToken); }
public sealed class NoOpInstallationGuard : IInstallationGuard { public Task EnsureExclusiveAsync(CancellationToken cancellationToken) => Task.CompletedTask; }

public sealed class TransactionalPatchInstaller(string clientRoot, string updatesRelativeRoot, IInstallationGuard guard, IClock clock)
{
    private readonly string updatesRoot = SafePaths.UnderRoot(clientRoot, updatesRelativeRoot);
    private string PendingPath => SafePaths.UnderRoot(updatesRoot, "State/pending-install.json");

    public bool RecoveryRequired => File.Exists(PendingPath);

    public async Task InstallAsync(SignedPatchPackage package, string stagedRoot, Func<CancellationToken, Task> verifyTarget,
        Func<CancellationToken, Task> commitVersionAndReceipt, CancellationToken cancellationToken,
        IReadOnlyList<string>? managedStatePaths = null)
    {
        await guard.EnsureExclusiveAsync(cancellationToken);
        string transactionId = Guid.NewGuid().ToString("N");
        string rollbackRoot = SafePaths.UnderRoot(updatesRoot, $"Rollback/{transactionId}");
        var pending = new PendingInstallState { TransactionId = transactionId, PatchId = package.PatchId, Phase = "Installing", StartedAtUtc = clock.UtcNow };
        foreach (PatchArchiveFile file in package.Files)
        {
            string target = SafePaths.UnderRoot(clientRoot, file.TargetPath); string backup = SafePaths.UnderRoot(rollbackRoot, file.TargetPath);
            pending.Entries.Add(new() { TargetPath = file.TargetPath.Replace('\\', '/'), BackupPath = Path.GetRelativePath(updatesRoot, backup).Replace('\\', '/'), PreviouslyExisted = File.Exists(target) });
        }
        foreach (string deletedPath in package.DeletedFiles)
        {
            SignedUpdateMetadataVerifier.ValidateArchivePath(deletedPath);
            string target = SafePaths.UnderRoot(clientRoot, deletedPath); string backup = SafePaths.UnderRoot(rollbackRoot, deletedPath);
            pending.Entries.Add(new() { TargetPath = deletedPath.Replace('\\', '/'), BackupPath = Path.GetRelativePath(updatesRoot, backup).Replace('\\', '/'), PreviouslyExisted = File.Exists(target) });
        }
        foreach (string statePath in managedStatePaths ?? [])
        {
            ValidatedClientRoot.ValidateRelative(statePath); string target = SafePaths.UnderRoot(clientRoot, statePath);
            string backup = SafePaths.UnderRoot(rollbackRoot, statePath);
            pending.Entries.Add(new() { TargetPath = statePath.Replace('\\', '/'), BackupPath = Path.GetRelativePath(updatesRoot, backup).Replace('\\', '/'), PreviouslyExisted = File.Exists(target), ManagedState = true });
        }
        await JsonFiles.WriteAtomicAsync(PendingPath, pending, cancellationToken);
        try
        {
            foreach (RollbackJournalEntry entry in pending.Entries)
            {
                if (entry.ManagedState) continue;
                string target = SafePaths.UnderRoot(clientRoot, entry.TargetPath);
                string backup = SafePaths.UnderRoot(updatesRoot, entry.BackupPath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (entry.PreviouslyExisted) { Directory.CreateDirectory(Path.GetDirectoryName(backup)!); File.Move(target, backup); }
                PatchArchiveFile? replacement = package.Files.SingleOrDefault(file => file.TargetPath.Replace('\\', '/').Equals(entry.TargetPath, StringComparison.OrdinalIgnoreCase));
                if (replacement is not null)
                {
                    string staged = SafePaths.UnderRoot(stagedRoot, replacement.ArchivePath);
                    File.Move(staged, target);
                }
                entry.Installed = true; await JsonFiles.WriteAtomicAsync(PendingPath, pending, cancellationToken);
            }
            pending.Phase = "Verifying"; await JsonFiles.WriteAtomicAsync(PendingPath, pending, cancellationToken);
            await verifyTarget(cancellationToken);
            foreach (RollbackJournalEntry entry in pending.Entries.Where(item => item.ManagedState))
            {
                string target = SafePaths.UnderRoot(clientRoot, entry.TargetPath); string backup = SafePaths.UnderRoot(updatesRoot, entry.BackupPath);
                if (entry.PreviouslyExisted) { Directory.CreateDirectory(Path.GetDirectoryName(backup)!); File.Move(target, backup); }
                entry.Installed = true;
            }
            await JsonFiles.WriteAtomicAsync(PendingPath, pending, cancellationToken);
            await commitVersionAndReceipt(cancellationToken);
            File.Delete(PendingPath);
            PruneRollbackDirectories(2);
        }
        catch
        {
            await RecoverAsync(CancellationToken.None); throw;
        }
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        PendingInstallState pending = await JsonFiles.ReadAsync<PendingInstallState>(PendingPath, cancellationToken);
        if (pending.SchemaVersion != 1 || string.IsNullOrWhiteSpace(pending.TransactionId)) throw new InvalidDataException("Rollback journal is malformed.");
        foreach (RollbackJournalEntry entry in pending.Entries.AsEnumerable().Reverse())
        {
            string target = SafePaths.UnderRoot(clientRoot, entry.TargetPath); string backup = SafePaths.UnderRoot(updatesRoot, entry.BackupPath);
            if (entry.Installed && File.Exists(target)) File.Delete(target);
            if (entry.PreviouslyExisted)
            {
                if (!File.Exists(backup)) throw new InvalidDataException("Rollback backup is missing.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Move(backup, target);
            }
        }
        File.Delete(PendingPath);
    }

    private void PruneRollbackDirectories(int retain)
    {
        string root = SafePaths.UnderRoot(updatesRoot, "Rollback"); if (!Directory.Exists(root)) return;
        foreach (DirectoryInfo directory in new DirectoryInfo(root).EnumerateDirectories()
                     .OrderByDescending(item => item.CreationTimeUtc).Skip(retain)) directory.Delete(true);
    }
}
