using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Revora.Launcher.Core;

public sealed record PatchJournalEntry(string Path, bool PreviousFileExisted, string? PreviousSha256);
public sealed record PatchJournal(Guid ReleaseId, string RollbackId, string State, IReadOnlyList<PatchJournalEntry> Entries);

public sealed class TransactionalPatchApplier
{
    public async Task ApplyAsync(string clientRoot, string stagingRoot, DesiredStateManifest manifest,
        IReadOnlyCollection<DesiredStateFile> files, IProgress<UpdateProgress>? progress, CancellationToken token)
    {
        string control = Path.Combine(clientRoot, ".revora-launcher");
        string rollbackId = manifest.ReleaseId.ToString("N") + "-" + Guid.NewGuid().ToString("N");
        string rollback = Path.Combine(control, "rollback", rollbackId);
        string journalPath = Path.Combine(control, "apply-journal.json");
        Directory.CreateDirectory(rollback);
        var journal = new List<PatchJournalEntry>();
        await WriteJournalAsync(journalPath, new(manifest.ReleaseId, rollbackId, "applying", journal), token);
        int completed = 0;
        try
        {
            foreach (DesiredStateFile file in files.OrderBy(file => file.Path, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                string target = SafePath.ResolveContained(clientRoot, file.Path);
                SafePath.RejectExistingReparsePoints(clientRoot, target);
                string stagedObject = SafePath.ResolveContained(stagingRoot, file.ObjectPath);
                SafePath.RejectExistingReparsePoints(stagingRoot, stagedObject);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                string replacement = target + ".revora-new";
                await MaterializeAsync(stagedObject, replacement, file, token);

                string? previousHash = null;
                bool existed = File.Exists(target);
                if (existed)
                {
                    await using FileStream previous = new(target, FileMode.Open, FileAccess.Read, FileShare.Read,
                        128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    previousHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(previous, token));
                }
                journal.Add(new(file.Path, existed, previousHash));
                await WriteJournalAsync(journalPath, new(manifest.ReleaseId, rollbackId, "applying", journal), token);
                if (existed)
                {
                    string backup = SafePath.ResolveContained(rollback, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Move(target, backup, false);
                }
                try { File.Move(replacement, target, false); }
                catch
                {
                    if (existed) File.Move(SafePath.ResolveContained(rollback, file.Path), target, false);
                    throw;
                }
                completed++;
                progress?.Report(new("apply", file.Path, completed, files.Count, completed, files.Count));
            }
            await WriteJournalAsync(journalPath, new(manifest.ReleaseId, rollbackId, "complete", journal), token);
            File.Delete(journalPath);
        }
        catch
        {
            await RollbackAsync(clientRoot, rollback, journal, CancellationToken.None);
            await WriteJournalAsync(journalPath, new(manifest.ReleaseId, rollbackId, "rolled-back", journal), CancellationToken.None);
            throw;
        }
    }

    public async Task RecoverAsync(string clientRoot, CancellationToken token)
    {
        string journalPath = Path.Combine(clientRoot, ".revora-launcher", "apply-journal.json");
        if (!File.Exists(journalPath)) return;
        PatchJournal journal = JsonSerializer.Deserialize<PatchJournal>(await File.ReadAllBytesAsync(journalPath, token))
            ?? throw new InvalidDataException("The patch recovery journal is invalid.");
        if (journal.State == "complete") { File.Delete(journalPath); return; }
        if (!SafePath.IsNormalizedRelative(journal.RollbackId)) throw new InvalidDataException("The patch recovery journal has an unsafe rollback identity.");
        string rollback = Path.Combine(clientRoot, ".revora-launcher", "rollback", journal.RollbackId);
        await RollbackAsync(clientRoot, rollback, journal.Entries, token);
        File.Delete(journalPath);
    }

    private static async Task MaterializeAsync(string source, string destination, DesiredStateFile file, CancellationToken token)
    {
        if (File.Exists(destination)) File.Delete(destination);
        await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (Stream content = file.Compression == "gzip" ? new GZipStream(input, CompressionMode.Decompress) : input)
        await using (FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            byte[] buffer = new byte[128 * 1024]; long total = 0;
            try
            {
                int read;
                while ((read = await content.ReadAsync(buffer, token)) != 0)
                {
                    total = checked(total + read);
                    if (total > file.Size) throw new InvalidDataException("A staged object exceeded its signed expanded size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), token);
                }
                await output.FlushAsync(token);
                if (total != file.Size) throw new InvalidDataException("A staged object has an invalid expanded size.");
            }
            finally { CryptographicOperations.ZeroMemory(buffer); }
        }
        await using FileStream verify = new(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(verify, token), Convert.FromHexString(file.Sha256)))
            throw new CryptographicException("A materialized patch file failed verification.");
    }

    private static Task RollbackAsync(string clientRoot, string rollback, IReadOnlyCollection<PatchJournalEntry> entries, CancellationToken token)
    {
        foreach (PatchJournalEntry entry in entries.Reverse())
        {
            token.ThrowIfCancellationRequested();
            string target = SafePath.ResolveContained(clientRoot, entry.Path);
            string backup = SafePath.ResolveContained(rollback, entry.Path);
            if (entry.PreviousFileExisted && File.Exists(backup))
            {
                if (File.Exists(target)) File.Delete(target);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(backup, target, false);
            }
            else if (!entry.PreviousFileExisted && File.Exists(target)) File.Delete(target);
        }
        return Task.CompletedTask;
    }

    private static async Task WriteJournalAsync(string path, PatchJournal journal, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".new";
        await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(journal), token);
        File.Move(temporary, path, true);
    }
}
