using System.Net;
using System.Security.Cryptography;

namespace Revora.Launcher.Core;

public sealed record UpdateProgress(string Phase, string RelativePath, int FilesCompleted, int TotalFiles,
    long BytesDownloaded, long TotalBytes, int? HResult = null, int? NativeError = null,
    string? Operation = null, int? AttemptCount = null, bool? LockTransient = null, bool? LockExhausted = null);

public sealed class AtomicUpdate(HttpClient httpClient)
{
    public async Task StageAsync(string stagingRoot, IReadOnlyList<ClientFile> files, CancellationToken cancellationToken, IProgress<UpdateProgress>? progress = null)
    {
        Directory.CreateDirectory(stagingRoot);
        ClientFile[] downloads = files.Where(file => file.DownloadUrl is not null).ToArray();
        long totalBytes = downloads.Sum(file => file.Size);
        long bytesDownloaded = 0;
        int completed = 0;
        foreach (ClientFile file in downloads)
        {
            string destination = SafePath.ResolveContained(stagingRoot, file.Path);
            SafePath.RejectExistingReparsePoints(stagingRoot, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            string partial = destination + ".partial";
            if (File.Exists(partial)) File.Delete(partial);
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength is > LauncherPolicy.MaximumFileBytes)
                {
                    throw new InvalidDataException("The update server returned an invalid file response.");
                }

                await using (Stream input = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (FileStream output = new(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await CopyBoundedAsync(input, output, file.Size, read =>
                    {
                        bytesDownloaded = checked(bytesDownloaded + read);
                        progress?.Report(new("download", file.Path, completed, downloads.Length, bytesDownloaded, totalBytes));
                    }, cancellationToken);
                    await output.FlushAsync(cancellationToken);
                }

                var info = new FileInfo(partial);
                if (info.Length != file.Size) throw new InvalidDataException("A staged update file has an unexpected size.");

                progress?.Report(new("hash", file.Path, completed, downloads.Length, bytesDownloaded, totalBytes));
                byte[] digest;
                await using (FileStream verify = new(partial, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    digest = await SHA256.HashDataAsync(verify, cancellationToken);
                }
                if (!CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(file.Sha256)))
                    throw new CryptographicException("A staged update file failed verification.");
                progress?.Report(new("move", file.Path, completed, downloads.Length, bytesDownloaded, totalBytes));
                File.Move(partial, destination, overwrite: false);
                completed++;
                progress?.Report(new("move", file.Path, completed, downloads.Length, bytesDownloaded, totalBytes));
            }
            catch
            {
                if (File.Exists(partial)) File.Delete(partial);
                throw;
            }
        }
    }

    private static async Task CopyBoundedAsync(Stream input, Stream output, long expectedSize, Action<int> reportBytes, CancellationToken token)
    {
        byte[] buffer = new byte[128 * 1024];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, token)) != 0)
        {
            total = checked(total + read);
            if (total > expectedSize || total > LauncherPolicy.MaximumFileBytes) throw new InvalidDataException("A download exceeded its signed size.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            reportBytes(read);
        }
    }

    public static void Activate(string currentDirectory, string stagedDirectory, string rollbackDirectory)
    {
        if (Directory.Exists(rollbackDirectory))
        {
            throw new IOException("The rollback directory must be empty before activation.");
        }

        Directory.Move(currentDirectory, rollbackDirectory);
        try
        {
            Directory.Move(stagedDirectory, currentDirectory);
        }
        catch
        {
            Directory.Move(rollbackDirectory, currentDirectory);
            throw;
        }
    }
}
