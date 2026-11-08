using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Revora.ClientEndpoint;

public sealed record FileChange(string File, string Backup, string BeforeSha256, string AfterSha256);

public static class ClientFiles
{
    public static ClientState Inspect(string path) => EndpointPatch.Inspect(ReadChecked(CheckedPath(path)));

    public static FileChange Apply(string path, string address, int port) =>
        Change(path, bytes => EndpointPatch.Patch(bytes, address, port));

    public static FileChange Restore(string path) => Change(path, EndpointPatch.Restore);

    internal static FileChange Change(string path, Func<byte[], PatchResult> transform)
    {
        path = CheckedPath(path);
        string lockKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())));
        using var mutex = new Mutex(false, "RevoraEndpoint_" + lockKey);
        bool held;
        try { held = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
        catch (AbandonedMutexException) { held = true; }
        if (!held) throw new IOException("Another endpoint operation is in progress for this file.");
        string? temporary = null;
        try
        {
            byte[] before = ReadChecked(path);
            PatchResult patch = transform(before);
            string beforeHash = EndpointPatch.Hash(before);
            if (beforeHash == patch.OutputSha256) throw new InvalidOperationException("The requested endpoint is already configured.");
            string backupDirectory = Path.Combine(Path.GetDirectoryName(path)!, ".revora-endpoint", "backups");
            CheckedPath(backupDirectory);
            Directory.CreateDirectory(backupDirectory);
            CheckedPath(backupDirectory);
            string backup = CheckedPath(Path.Combine(backupDirectory, beforeHash + ".bin"));
            if (File.Exists(backup))
            {
                if (EndpointPatch.Hash(ReadChecked(backup)) != beforeHash)
                    throw new InvalidDataException("An existing backup is damaged. No client change was made; preserve it for investigation.");
            }
            else WriteNew(backup, before);
            temporary = CheckedPath(Path.Combine(Path.GetDirectoryName(path)!, ".revora-endpoint-" + Guid.NewGuid().ToString("N") + ".tmp"));
            WriteNew(temporary, patch.Bytes);
            // Verify paths and contents again immediately before the atomic replacement.
            CheckedPath(path); CheckedPath(temporary);
            if (EndpointPatch.Hash(ReadChecked(path)) != beforeHash)
                throw new IOException("The client changed during the operation. No replacement was made.");
            File.Replace(temporary, path, null);
            temporary = null;
            var result = new FileChange(path, backup, beforeHash, patch.OutputSha256);
            // The backup and inverse patch are sufficient for recovery if a receipt cannot be saved.
            try
            {
                string receipt = CheckedPath(Path.Combine(backupDirectory, $"{DateTime.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}.json"));
                WriteNew(receipt, JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return result;
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary)) File.Delete(temporary);
            mutex.ReleaseMutex();
        }
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes); file.Flush(true);
    }

    private static byte[] ReadChecked(string path)
    {
        CheckedPath(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length != EndpointPatch.ClientSize) throw new InvalidDataException("Unexpected client size.");
        byte[] bytes = new byte[EndpointPatch.ClientSize];
        file.ReadExactly(bytes);
        return bytes;
    }

    private static string CheckedPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\", StringComparison.Ordinal)) throw new IOException("Use a local client folder, not a network/device path.");
        for (string? part = full; part is not null; part = Path.GetDirectoryName(part))
        {
            try
            {
                if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Symbolic links and reparse points are not supported: " + part);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return full;
    }
}
