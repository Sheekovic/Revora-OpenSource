using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace Revora.Launcher.Core;

public sealed record VerifiedFileState(string Path, long Size, long LastWriteUtcTicks, string Sha256);
public sealed record VerifiedStateDocument(int SchemaVersion, Guid ReleaseId, DateTimeOffset PublishedAtUtc,
    IReadOnlyList<VerifiedFileState> Files);

public sealed class VerifiedStateCache
{
    private const string CacheRelativePath = ".revora-launcher/verified-state.json";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WriteGates =
        new(StringComparer.OrdinalIgnoreCase);

    public static string PathFor(string clientRoot) => Path.Combine(clientRoot, ".revora-launcher", "verified-state.json");

    public async Task<VerifiedStateDocument?> ReadAsync(string clientRoot, CancellationToken token)
    {
        string path = PathFor(clientRoot);
        if (!File.Exists(path)) return null;
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path, token);
            if (bytes.Length is 0 or > 16 * 1024 * 1024) return null;
            VerifiedStateDocument? document = JsonSerializer.Deserialize<VerifiedStateDocument>(bytes);
            return document is { SchemaVersion: 1 } && document.Files.Count <= 50_000 ? document : null;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    public async Task WriteAsync(string clientRoot, DesiredStateManifest manifest,
        IReadOnlyCollection<VerifiedFileState> states, CancellationToken token)
    {
        string path = PathFor(clientRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".new";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new VerifiedStateDocument(1, manifest.ReleaseId,
            manifest.PublishedAtUtc, states.OrderBy(state => state.Path, StringComparer.Ordinal).ToArray()));
        SemaphoreSlim writeGate = WriteGates.GetOrAdd(Path.GetFullPath(path), static _ => new(1, 1));
        bool entered = false;
        try
        {
            await writeGate.WaitAsync(token);
            entered = true;
            try
            {
                await using (FileStream output = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                    64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await output.WriteAsync(bytes, token);
                    await output.FlushAsync(token);
                }

                File.Move(temporary, path, true);
            }
            catch (IOException error)
            {
                throw new LauncherFileOperationException("verified-state-cache-write", CacheRelativePath, 1,
                    transient: false, exhausted: true, error);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (entered)
            {
                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch (IOException error)
                {
                    throw new LauncherFileOperationException("verified-state-cache-write", CacheRelativePath, 1,
                        transient: false, exhausted: true, error);
                }
                finally { writeGate.Release(); }
            }
        }
    }
}

public sealed record ClientScanFailure(string Path, string Category, bool Critical);
public sealed record ClientScanResult(bool IsValid, IReadOnlyList<ClientScanFailure> Failures,
    IReadOnlyList<VerifiedFileState> Verified, long HashedBytes, int CachedFiles);

public class LauncherFileOperationException : IOException
{
    public LauncherFileOperationException(string operation, string relativePath, int attemptCount, bool transient,
        bool exhausted, IOException innerException)
        : base($"A launcher file operation failed after {attemptCount} attempt(s).", innerException)
    {
        Operation = operation;
        RelativePath = relativePath;
        AttemptCount = attemptCount;
        Transient = transient;
        Exhausted = exhausted;
        NativeErrorCode = WindowsFileErrors.NativeCode(innerException);
        HResultCode = innerException.HResult;
        HResult = innerException.HResult;
    }

    public string Operation { get; }
    public string RelativePath { get; }
    public int AttemptCount { get; }
    public bool Transient { get; }
    public bool Exhausted { get; }
    public int NativeErrorCode { get; }
    public int HResultCode { get; }
}

public sealed class ClientFileAccessException : LauncherFileOperationException
{
    public ClientFileAccessException(string operation, string relativePath, int attemptCount, bool transient,
        bool exhausted, IOException innerException)
        : base(operation, relativePath, attemptCount, transient, exhausted, innerException) { }
}

public static class WindowsFileErrors
{
    public const int SharingViolation = 32;
    public const int LockViolation = 33;
    public static int NativeCode(IOException error) => error.HResult & 0xffff;
    public static bool IsSharingViolation(IOException error) => NativeCode(error) is SharingViolation or LockViolation;
}

public sealed class DesiredStateScanner
{
    private readonly int maximumReadAttempts;
    private readonly TimeSpan initialRetryDelay;

    public DesiredStateScanner(int maximumReadAttempts = 5, TimeSpan? initialRetryDelay = null)
    {
        if (maximumReadAttempts is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(maximumReadAttempts));
        this.maximumReadAttempts = maximumReadAttempts;
        this.initialRetryDelay = initialRetryDelay ?? TimeSpan.FromMilliseconds(125);
    }

    public async Task<ClientScanResult> ScanAsync(string clientRoot, DesiredStateManifest manifest,
        VerifiedStateDocument? cache, bool fullRepair, IProgress<UpdateProgress>? progress, CancellationToken token)
    {
        var failures = new List<ClientScanFailure>();
        var verified = new List<VerifiedFileState>(manifest.Files.Count);
        var cached = cache is not null && cache.ReleaseId == manifest.ReleaseId
            ? cache.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, VerifiedFileState>(StringComparer.OrdinalIgnoreCase);
        long total = manifest.Files.Sum(file => file.Size), completed = 0, hashed = 0;
        int cachedCount = 0;
        foreach (DesiredStateFile expected in manifest.Files)
        {
            token.ThrowIfCancellationRequested();
            string path = SafePath.ResolveContained(clientRoot, expected.Path);
            SafePath.RejectExistingReparsePoints(clientRoot, path);
            if (!File.Exists(path)) { failures.Add(new(expected.Path, "missing", expected.Critical)); completed += expected.Size; continue; }
            var info = new FileInfo(path);
            if (info.Length != expected.Size) { failures.Add(new(expected.Path, "wrong-size", expected.Critical)); completed += expected.Size; continue; }

            cached.TryGetValue(expected.Path, out VerifiedFileState? previous);
            bool mustHash = fullRepair || expected.Critical || previous is null || previous.Size != info.Length ||
                previous.LastWriteUtcTicks != info.LastWriteTimeUtc.Ticks || previous.Sha256 != expected.Sha256;
            string hash;
            if (mustHash)
            {
                progress?.Report(new("scan", expected.Path, verified.Count, manifest.Files.Count, completed, total));
                hash = await HashWithSharingRetryAsync(path, expected.Path, progress, token);
                hashed += info.Length;
            }
            else { hash = previous!.Sha256; cachedCount++; }
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hash), Convert.FromHexString(expected.Sha256)))
                failures.Add(new(expected.Path, "hash-mismatch", expected.Critical));
            else verified.Add(new(expected.Path, info.Length, info.LastWriteTimeUtc.Ticks, hash));
            completed += expected.Size;
        }
        return new(failures.Count == 0, failures, verified, hashed, cachedCount);
    }

    private async Task<string> HashWithSharingRetryAsync(string path, string relativePath,
        IProgress<UpdateProgress>? progress, CancellationToken token)
    {
        IOException? last = null;
        for (int attempt = 1; attempt <= maximumReadAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await using FileStream stream = new(path, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 128 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
            }
            catch (IOException error) when (WindowsFileErrors.IsSharingViolation(error))
            {
                last = error;
                bool exhausted = attempt == maximumReadAttempts;
                progress?.Report(new("scan-retry", relativePath, attempt, maximumReadAttempts, 0, 0,
                    error.HResult, WindowsFileErrors.NativeCode(error), "read-only-hash", attempt,
                    LockTransient: !exhausted, LockExhausted: exhausted));
                if (exhausted)
                    throw new ClientFileAccessException("read-only-hash", relativePath, attempt,
                        transient: false, exhausted: true, error);
                double exponential = initialRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
                int jitter = Random.Shared.Next(0, Math.Max(2, (int)(exponential / 4) + 1));
                await Task.Delay(TimeSpan.FromMilliseconds(exponential + jitter), token);
            }
        }
        throw new ClientFileAccessException("read-only-hash", relativePath, maximumReadAttempts,
            transient: false, exhausted: true, last!);
    }
}
