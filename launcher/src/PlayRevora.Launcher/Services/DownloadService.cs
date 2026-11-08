using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using PlayRevora.Launcher.Models;

namespace PlayRevora.Launcher.Services;

public interface IDownloadService
{
    Task<T> GetJsonAsync<T>(Uri uri, CancellationToken cancellationToken);
    Task DownloadAsync(Uri uri, string destination, long maximumBytes, CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null);
    Task DownloadResumableAsync(Uri uri, string temporaryDestination, long expectedBytes,
        CancellationToken cancellationToken, IProgress<OperationProgress>? progress = null);
}

public sealed class DownloadService : IDisposable, IDownloadService
{
    private readonly HttpClient client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DownloadService()
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = System.Net.DecompressionMethods.All };
        client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlayRevora-Launcher", "1.0"));
    }

    public async Task<T> GetJsonAsync<T>(Uri uri, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"Empty JSON response from {uri}.");
    }

    public async Task DownloadAsync(Uri uri, string destination, long maximumBytes,
        CancellationToken cancellationToken, IProgress<OperationProgress>? progress = null)
    {
        using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > maximumBytes)
            throw new InvalidDataException("Download exceeded manifest size.");
        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[128 * 1024];
        long total = 0;
        var timer = Stopwatch.StartNew();
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maximumBytes) throw new InvalidDataException("Download exceeded manifest size.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            double percent = maximumBytes == 0 ? 100 : total * 100d / maximumBytes;
            double bytesPerSecond = total / Math.Max(0.001, timer.Elapsed.TotalSeconds);
            progress?.Report(new OperationProgress($"Downloading {Path.GetFileName(destination)}", 0,
                percent, FormatRate(bytesPerSecond)));
        }
        if (total != maximumBytes) throw new InvalidDataException("Downloaded file size does not match its manifest.");
        await output.FlushAsync(cancellationToken);
    }

    public async Task DownloadResumableAsync(Uri uri, string temporaryDestination, long expectedBytes,
        CancellationToken cancellationToken, IProgress<OperationProgress>? progress = null)
    {
        long existing = File.Exists(temporaryDestination) ? new FileInfo(temporaryDestination).Length : 0;
        if (existing < 0 || existing > expectedBytes) { File.Delete(temporaryDestination); existing = 0; }
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (existing > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            existing = 0;
            response.Dispose();
            string restart = temporaryDestination + ".restart";
            if (File.Exists(restart)) File.Delete(restart);
            await DownloadAsync(uri, restart, expectedBytes, cancellationToken, progress);
            File.Move(restart, temporaryDestination, true);
            return;
        }
        response.EnsureSuccessStatusCode();
        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream output = new(temporaryDestination, existing == 0 ? FileMode.Create : FileMode.Append,
            FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[128 * 1024]; long total = existing; var timer = Stopwatch.StartNew(); int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read; if (total > expectedBytes) throw new InvalidDataException("Download exceeded signed package size.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            progress?.Report(new("Downloading", expectedBytes == 0 ? 100 : total * 100d / expectedBytes,
                expectedBytes == 0 ? 100 : total * 100d / expectedBytes, FormatRate((total - existing) / Math.Max(.001, timer.Elapsed.TotalSeconds))));
        }
        await output.FlushAsync(cancellationToken);
        if (total != expectedBytes) throw new InvalidDataException("Downloaded package size does not match signed metadata.");
    }

    private static string FormatRate(double bytesPerSecond) => bytesPerSecond >= 1024 * 1024
        ? $"{bytesPerSecond / (1024 * 1024):0.0} MiB/s"
        : $"{bytesPerSecond / 1024:0} KiB/s";

    public void Dispose() => client.Dispose();
}
