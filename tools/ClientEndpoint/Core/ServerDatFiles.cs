using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Revora.ClientEndpoint;

public sealed record ServerListState(ClientState Client, string DatSha256, IReadOnlyList<ServerEntry> Servers);
public sealed record PairChange(string Executable, string ServerDat, string Journal, string KeyFile, string ExecutableSha256, string DatSha256);

internal sealed record PairJournal(int Format, string Executable, string Dat, string ExeBefore, string ExeAfter, string DatBefore, string DatAfter, string State);

public static class ServerDatFiles
{
    public static ServerListState Inspect(string executable, string dat)
    {
        var paths = Paths(executable, dat);
        using var held = new PathLocks([paths.Exe, paths.Dat]);
        return InspectBytes(Read(paths.Exe, EndpointPatch.ClientSize), Read(paths.Dat, ServerDatCodec.MaximumCiphertext), EndpointPatch.Inspect);
    }

    public static PairChange Apply(string executable, string dat, uint serverId, string name, string address, int port, ServerListState? expected = null) =>
        ApplyCore(executable, dat, serverId, name, address, port, true, EndpointPatch.Inspect, expected: expected);

    internal static PairChange ApplyCore(string executable, string dat, uint serverId, string name, string address, int port,
        bool useDatRouting, Func<byte[], ClientState> inspect, Action<int>? checkpoint = null, bool rollbackOnError = true, ServerListState? expected = null)
    {
        var paths = Paths(executable, dat);
        string home = Home(paths.Exe), keyFile = ClientFiles.CheckedPath(Path.Combine(home, "keys", "server-list-private.pem"));
        using var held = new PathLocks([paths.Exe, paths.Dat, keyFile]);
        byte[] beforeExe = Read(paths.Exe, EndpointPatch.ClientSize), beforeDat = Read(paths.Dat, ServerDatCodec.MaximumCiphertext);
        if (expected is not null && (EndpointPatch.Hash(beforeExe) != expected.Client.CurrentSha256 || EndpointPatch.Hash(beforeDat) != expected.DatSha256))
            throw new IOException("The executable or server list changed after loading. Reload the pair before saving.");
        ClientState state = inspect(beforeExe);
        var list = new ServerListDocument(ServerDatCodec.Decode(beforeDat, ServerDatCodec.ReadModulus(beforeExe)));
        byte[] xml = list.Edit(serverId, name, address, port); // Validate every requested edit before creating a key or backups.
        using RSA key = LoadOrCreateKey(keyFile);
        byte[] afterDat = ServerDatCodec.Encode(xml, key);
        byte[] afterExe = ServerDatCodec.ReplaceModulus(useDatRouting && state.Patched ? EndpointPatch.RestoreSpans(beforeExe) : beforeExe, key.ExportParameters(false).Modulus!);
        _ = inspect(afterExe);
        _ = InspectBytes(afterExe, afterDat, inspect);
        return SavePair(paths.Exe, paths.Dat, beforeExe, beforeDat, afterExe, afterDat, keyFile, checkpoint, rollbackOnError);
    }

    public static PairChange Restore(string executable, string dat) => RestoreCore(executable, dat, EndpointPatch.Inspect);

    internal static PairChange RestoreCore(string executable, string dat, Func<byte[], ClientState> inspect)
    {
        var paths = Paths(executable, dat);
        using var held = new PathLocks([paths.Exe, paths.Dat]);
        byte[] currentExe = Read(paths.Exe, EndpointPatch.ClientSize), currentDat = Read(paths.Dat, ServerDatCodec.MaximumCiphertext);
        _ = inspect(currentExe); // A mixed pair after interrupted replacement need not decode yet.
        string exeHash = EndpointPatch.Hash(currentExe), datHash = EndpointPatch.Hash(currentDat), home = Home(paths.Exe);
        string journalDirectory = ClientFiles.CheckedPath(Path.Combine(home, "transactions"));
        if (!Directory.Exists(journalDirectory)) throw new InvalidOperationException("No paired backup journal exists for this client.");
        string[] journals = Directory.EnumerateFiles(journalDirectory, "*.json").Take(1025).ToArray();
        if (journals.Length > 1024) throw new InvalidDataException("Too many journals. Preserve/archive older transactions before restoring.");
        foreach (string journalPath in journals.OrderByDescending(File.GetLastWriteTimeUtc))
        {
            PairJournal journal;
            try { journal = JsonSerializer.Deserialize<PairJournal>(Read(journalPath, 64 * 1024)) ?? throw new InvalidDataException(); }
            catch (JsonException) { continue; }
            if (journal.Format != 1 || !SamePath(journal.Executable, paths.Exe) || !SamePath(journal.Dat, paths.Dat) ||
                journal.State is not ("Prepared" or "Committed") || !HexHash(journal.ExeBefore) || !HexHash(journal.ExeAfter) || !HexHash(journal.DatBefore) || !HexHash(journal.DatAfter)) continue;
            if ((exeHash != journal.ExeBefore && exeHash != journal.ExeAfter) || (datHash != journal.DatBefore && datHash != journal.DatAfter) ||
                (exeHash == journal.ExeBefore && datHash == journal.DatBefore)) continue;
            byte[] originalExe = BackupRead(home, journal.ExeBefore, EndpointPatch.ClientSize);
            byte[] originalDat = BackupRead(home, journal.DatBefore, ServerDatCodec.MaximumCiphertext);
            _ = InspectBytes(originalExe, originalDat, inspect);
            PairChange restored = SavePair(paths.Exe, paths.Dat, currentExe, currentDat, originalExe, originalDat, "", null, true, isRestore: true);
            // This journal must not be considered again, including after a recovery from a partial write.
            TrySetJournal(journalPath, journal with { State = "Restored" });
            return restored;
        }
        throw new InvalidDataException("No matching paired restore point was found, or the files have unrelated changes. Preserve the backups; no files were replaced.");
    }

    private static ServerListState InspectBytes(byte[] executable, byte[] dat, Func<byte[], ClientState> inspect)
    {
        ClientState state = inspect(executable);
        var list = new ServerListDocument(ServerDatCodec.Decode(dat, ServerDatCodec.ReadModulus(executable)));
        return new(state, EndpointPatch.Hash(dat), list.Entries);
    }

    private static PairChange SavePair(string exePath, string datPath, byte[] beforeExe, byte[] beforeDat, byte[] afterExe, byte[] afterDat,
        string keyFile, Action<int>? checkpoint, bool rollbackOnError, bool isRestore = false)
    {
        string exeBefore = EndpointPatch.Hash(beforeExe), datBefore = EndpointPatch.Hash(beforeDat);
        string exeAfter = EndpointPatch.Hash(afterExe), datAfter = EndpointPatch.Hash(afterDat);
        if (exeBefore == exeAfter && datBefore == datAfter) throw new InvalidOperationException("The requested server list is already configured.");
        string home = Home(exePath);
        Backup(home, beforeExe); Backup(home, beforeDat);
        string directory = ClientFiles.CheckedPath(Path.Combine(home, "transactions")); Directory.CreateDirectory(directory);
        string journalPath = ClientFiles.CheckedPath(Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}.json"));
        var journal = new PairJournal(1, exePath, datPath, exeBefore, exeAfter, datBefore, datAfter, isRestore ? "Restoring" : "Prepared");
        WriteNew(journalPath, JsonSerializer.SerializeToUtf8Bytes(journal));
        bool exeWritten = false, datWritten = false;
        try
        {
            Replace(exePath, beforeExe, afterExe); exeWritten = true; checkpoint?.Invoke(1);
            Replace(datPath, beforeDat, afterDat); datWritten = true; checkpoint?.Invoke(2);
            TrySetJournal(journalPath, journal with { State = isRestore ? "RestoreComplete" : "Committed" });
            return new(exePath, datPath, journalPath, keyFile, exeAfter, datAfter);
        }
        catch (Exception operationError)
        {
            if (!rollbackOnError) throw;
            try
            {
                if (datWritten) Replace(datPath, afterDat, beforeDat);
                if (exeWritten) Replace(exePath, afterExe, beforeExe);
                TrySetJournal(journalPath, journal with { State = "RolledBack" });
            }
            catch (Exception recoveryError)
            {
                throw new IOException("Pair replacement was interrupted and automatic rollback failed. Keep the game closed and use Restore pair with the same paths. Journal: " + journalPath,
                    new AggregateException(operationError, recoveryError));
            }
            throw new IOException("Pair replacement failed; replaced files were rolled back. Keep the backups. " + operationError.Message, operationError);
        }
    }

    private static RSA LoadOrCreateKey(string path)
    {
        ClientFiles.CheckedPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        RSA key = RSA.Create(2048);
        try
        {
            if (!File.Exists(path))
            {
                byte[] pem = Encoding.ASCII.GetBytes(key.ExportPkcs8PrivateKeyPem());
                try
                {
                    using FileStream file = CreatePrivate(path);
                    file.Write(pem); file.Flush(true);
                }
                finally { CryptographicOperations.ZeroMemory(pem); }
            }
            else
            {
                byte[] pem = Read(path, 32 * 1024);
                try { key.ImportFromPem(Encoding.ASCII.GetString(pem)); }
                finally { CryptographicOperations.ZeroMemory(pem); }
            }
            RSAParameters material = key.ExportParameters(false);
            ServerDatCodec.ValidateModulus(material.Modulus!);
            if (!material.Exponent!.SequenceEqual(new byte[] { 1, 0, 1 })) throw new CryptographicException("Unsupported signing exponent.");
            // A public-only PEM must never be accepted as a signing key.
            byte[] privateProbe = key.ExportPkcs8PrivateKey(); CryptographicOperations.ZeroMemory(privateProbe);
            return key;
        }
        catch { key.Dispose(); throw; }
    }

    private static FileStream CreatePrivate(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            SecurityIdentifier owner = WindowsIdentity.GetCurrent().User ?? throw new IOException("Cannot identify the key owner.");
            var permissions = new FileSecurity(); permissions.SetOwner(owner); permissions.SetAccessRuleProtection(true, false);
            permissions.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
            return new FileInfo(ClientFiles.CheckedPath(path)).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.None, permissions);
        }
        return new FileStream(ClientFiles.CheckedPath(path), new FileStreamOptions
        { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
    }

    private static void Replace(string path, byte[] expected, byte[] replacement)
    {
        path = ClientFiles.CheckedPath(path);
        string temp = ClientFiles.CheckedPath(Path.Combine(Path.GetDirectoryName(path)!, ".revora-endpoint-" + Guid.NewGuid().ToString("N") + ".tmp"));
        try
        {
            WriteNew(temp, replacement);
            if (EndpointPatch.Hash(Read(path, Math.Max(EndpointPatch.ClientSize, ServerDatCodec.MaximumCiphertext))) != EndpointPatch.Hash(expected))
                throw new IOException("The selected file changed during the operation: " + path);
            ClientFiles.CheckedPath(path); File.Replace(temp, path, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void Backup(string home, byte[] bytes)
    {
        string hash = EndpointPatch.Hash(bytes), directory = ClientFiles.CheckedPath(Path.Combine(home, "backups")); Directory.CreateDirectory(directory);
        string path = ClientFiles.CheckedPath(Path.Combine(directory, hash + ".bin"));
        if (File.Exists(path))
        {
            if (EndpointPatch.Hash(Read(path, EndpointPatch.ClientSize)) != hash) throw new InvalidDataException("An existing backup is corrupt; no files were replaced.");
        }
        else WriteNew(path, bytes);
    }

    private static byte[] BackupRead(string home, string hash, int maximum)
    {
        if (!HexHash(hash)) throw new InvalidDataException("Invalid backup hash.");
        byte[] bytes = Read(Path.Combine(home, "backups", hash + ".bin"), maximum);
        if (EndpointPatch.Hash(bytes) != hash) throw new InvalidDataException("Backup hash mismatch; restore refused.");
        return bytes;
    }

    private static void TrySetJournal(string path, PairJournal journal)
    {
        try { Replace(path, Read(path, 64 * 1024), JsonSerializer.SerializeToUtf8Bytes(journal)); }
        catch (IOException) { /* A Prepared journal retains the same recovery hashes after a completed write. */ }
        catch (UnauthorizedAccessException) { }
    }

    private static string Home(string executable) => ClientFiles.CheckedPath(Path.Combine(Path.GetDirectoryName(executable)!, ".revora-endpoint"));
    private static bool HexHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static bool SamePath(string? left, string right) => left is not null && string.Equals(left, right, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static (string Exe, string Dat) Paths(string executable, string dat)
    {
        string exePath = ClientFiles.CheckedPath(executable), datPath = ClientFiles.CheckedPath(dat);
        if (SamePath(exePath, datPath)) throw new ArgumentException("Select two different files: the executable and its matching Server.dat.");
        return (exePath, datPath);
    }

    internal static byte[] Read(string path, int maximum)
    {
        using var file = new FileStream(ClientFiles.CheckedPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is < 1 || file.Length > maximum) throw new InvalidDataException("File size is out of range: " + Path.GetFileName(path));
        byte[] bytes = new byte[(int)file.Length]; file.ReadExactly(bytes); return bytes;
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using var file = new FileStream(ClientFiles.CheckedPath(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes); file.Flush(true);
    }
}

internal sealed class PathLocks : IDisposable
{
    private readonly List<Mutex> held = [];
    internal PathLocks(IEnumerable<string> paths)
    {
        try
        {
            foreach (string path in paths.Select(p => p.ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal))
            {
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)));
                var mutex = new Mutex(false, "RevoraEndpoint_" + hash);
                bool acquired;
                try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(10)); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) { mutex.Dispose(); throw new IOException("Another operation is using the selected client files."); }
                held.Add(mutex);
            }
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        for (int i = held.Count - 1; i >= 0; i--) { held[i].ReleaseMutex(); held[i].Dispose(); }
        held.Clear();
    }
}
