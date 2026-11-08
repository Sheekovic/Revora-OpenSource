using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Revora.Launcher.Core;

public sealed record DownloadEngineOptions(int Parallelism = 3, int MaximumAttempts = 4,
    long? BytesPerSecondLimit = null, TimeSpan? RequestTimeout = null);

public sealed class PauseController
{
    private volatile TaskCompletionSource resume = Completed();
    public bool IsPaused { get; private set; }
    public void Pause() { if (IsPaused) return; IsPaused = true; resume = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    public void Resume() { if (!IsPaused) return; IsPaused = false; resume.TrySetResult(); }
    public Task WaitAsync(CancellationToken token) => IsPaused ? resume.Task.WaitAsync(token) : Task.CompletedTask;
    private static TaskCompletionSource Completed() { var value = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); value.SetResult(); return value; }
}

public sealed class PatchDownloadEngine(HttpClient httpClient, DownloadEngineOptions? options = null)
{
    private readonly DownloadEngineOptions settings = options ?? new();

    public async Task DownloadAsync(string stagingRoot, DesiredStateManifest manifest,
        IReadOnlyCollection<DesiredStateFile> files, PauseController pause,
        IProgress<UpdateProgress>? progress, CancellationToken token)
    {
        if (settings.Parallelism is < 1 or > 8 || settings.MaximumAttempts is < 1 or > 8)
            throw new InvalidOperationException("Download limits are invalid.");
        Directory.CreateDirectory(stagingRoot);
        long required = checked(files.Sum(file => file.CompressedSize ?? file.Size) + Math.Min(files.Sum(file => file.Size), 2L * 1024 * 1024 * 1024));
        DriveInfo drive = new(Path.GetPathRoot(Path.GetFullPath(stagingRoot))!);
        if (drive.AvailableFreeSpace < required) throw new IOException("Insufficient free disk space for this repair.");

        long total = files.Sum(file => file.CompressedSize ?? file.Size);
        long downloaded = 0;
        int completed = 0;
        var gate = new SemaphoreSlim(settings.Parallelism);
        var progressGate = new object();
        await Task.WhenAll(files.Select(async file =>
        {
            await gate.WaitAsync(token);
            try
            {
                await DownloadOneAsync(stagingRoot, manifest, file, pause, read =>
                {
                    lock (progressGate)
                    {
                        downloaded = checked(downloaded + read);
                        progress?.Report(new("download", file.Path, completed, files.Count, downloaded, total));
                    }
                }, token);
                lock (progressGate)
                {
                    completed++;
                    progress?.Report(new("download", file.Path, completed, files.Count, downloaded, total));
                }
            }
            finally { gate.Release(); }
        }));
    }

    private async Task DownloadOneAsync(string stagingRoot, DesiredStateManifest manifest, DesiredStateFile file,
        PauseController pause, Action<int> report, CancellationToken token)
    {
        string objectFile = SafePath.ResolveContained(stagingRoot, file.ObjectPath);
        SafePath.RejectExistingReparsePoints(stagingRoot, objectFile);
        Directory.CreateDirectory(Path.GetDirectoryName(objectFile)!);
        string partial = objectFile + ".partial";
        long transferSize = file.CompressedSize ?? file.Size;
        if (File.Exists(objectFile) && await VerifyFileAsync(objectFile, file, token)) return;
        if (File.Exists(objectFile)) File.Delete(objectFile);

        Exception? last = null;
        Uri[] endpoints = [BuildUri(manifest.ManifestBaseUri, file.ObjectPath), BuildUri(manifest.OriginBaseUri, file.ObjectPath)];
        for (int attempt = 0; attempt < settings.MaximumAttempts; attempt++)
        {
            await pause.WaitAsync(token);
            Uri endpoint = endpoints[Math.Min(attempt, endpoints.Length - 1)];
            try
            {
                long existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;
                if (existing < 0 || existing > transferSize) { File.Delete(partial); existing = 0; }
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(settings.RequestTimeout ?? TimeSpan.FromMinutes(10));
                using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                bool resumed = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
                if (existing > 0 && response.StatusCode == HttpStatusCode.OK) { File.Delete(partial); existing = 0; }
                else if (existing > 0 && !resumed) throw new InvalidDataException("The patch endpoint returned an invalid range response.");
                else if (resumed && (response.Content.Headers.ContentRange is not ContentRangeHeaderValue range ||
                    range.From != existing || range.To != transferSize - 1 || range.Length != transferSize))
                    throw new InvalidDataException("The patch endpoint returned an inconsistent content range.");
                if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.PartialContent)) response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long declared && declared != transferSize - existing)
                    throw new InvalidDataException("The patch object response size is invalid.");

                await using (Stream input = await response.Content.ReadAsStreamAsync(timeout.Token))
                await using (FileStream output = new(partial, existing == 0 ? FileMode.Create : FileMode.Append,
                    FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await CopyRateLimitedAsync(input, output, transferSize - existing, pause, report, timeout.Token);
                    await output.FlushAsync(timeout.Token);
                }
                if (new FileInfo(partial).Length != transferSize) throw new IOException("The patch object download is incomplete.");
                File.Move(partial, objectFile, true);
                if (!await VerifyFileAsync(objectFile, file, token))
                {
                    File.Delete(objectFile);
                    throw new CryptographicException("The patch object hash does not match the signed manifest.");
                }
                return;
            }
            catch (Exception error) when (error is HttpRequestException or IOException or TaskCanceledException && !token.IsCancellationRequested)
            {
                last = error;
                int delay = Math.Min(8_000, 250 * (1 << attempt)) + RandomNumberGenerator.GetInt32(0, 251);
                await Task.Delay(delay, token);
            }
        }
        throw new IOException("The patch object could not be downloaded within the retry policy.", last);
    }

    private async Task CopyRateLimitedAsync(Stream input, Stream output, long expected, PauseController pause,
        Action<int> report, CancellationToken token)
    {
        byte[] buffer = new byte[128 * 1024];
        long total = 0;
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            await pause.WaitAsync(token);
            int read = await input.ReadAsync(buffer, token);
            if (read == 0) break;
            total = checked(total + read);
            if (total > expected) throw new InvalidDataException("A patch object exceeded its signed transfer size.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            report(read);
            if (settings.BytesPerSecondLimit is > 0)
            {
                TimeSpan expectedElapsed = TimeSpan.FromSeconds((double)total / settings.BytesPerSecondLimit.Value);
                TimeSpan delay = expectedElapsed - stopwatch.Elapsed;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
            }
        }
        CryptographicOperations.ZeroMemory(buffer);
        if (total != expected) throw new IOException("A patch object ended before its signed transfer size.");
    }

    private static async Task<bool> VerifyFileAsync(string objectPath, DesiredStateFile file, CancellationToken token)
    {
        string candidate = objectPath;
        string? decompressed = null;
        try
        {
            if (file.Compression == "gzip")
            {
                decompressed = objectPath + ".decompressed";
                await using FileStream input = new(objectPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: false);
                await using FileStream output = new(decompressed, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024,
                    FileOptions.Asynchronous | FileOptions.WriteThrough);
                await CopyDecompressedBoundedAsync(gzip, output, file.Size, token);
                await output.FlushAsync(token);
                candidate = decompressed;
            }
            var info = new FileInfo(candidate);
            if (info.Length != file.Size) return false;
            await using FileStream verify = new(candidate, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(verify, token), Convert.FromHexString(file.Sha256));
        }
        finally { if (decompressed is not null && File.Exists(decompressed)) File.Delete(decompressed); }
    }

    private static async Task CopyDecompressedBoundedAsync(Stream input, Stream output, long expected, CancellationToken token)
    {
        byte[] buffer = new byte[128 * 1024]; long total = 0;
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer, token)) != 0)
            {
                total = checked(total + read);
                if (total > expected) throw new InvalidDataException("A compressed patch object exceeded its signed expanded size.");
                await output.WriteAsync(buffer.AsMemory(0, read), token);
            }
            if (total != expected) throw new InvalidDataException("A compressed patch object has an invalid expanded size.");
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    private static Uri BuildUri(string baseUri, string objectPath)
    {
        var root = new Uri(baseUri.EndsWith('/') ? baseUri : baseUri + '/', UriKind.Absolute);
        var result = new Uri(root, objectPath);
        if (!string.Equals(result.Host, root.Host, StringComparison.OrdinalIgnoreCase) || result.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("The patch object URI escaped its signed base endpoint.");
        return result;
    }
}
