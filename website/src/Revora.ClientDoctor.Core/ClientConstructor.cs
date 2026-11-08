using System.Security.Cryptography;
using System.Text.Json;
using Revora.Launcher.Core;

namespace Revora.ClientDoctor.Core;

public sealed record BuildProgress(string Phase, string RelativePath, int FilesCompleted, int TotalFiles, long BytesDownloaded, long TotalDownloadBytes);
public sealed record BuildResult(bool Success, bool DestinationVerified, int CopiedFiles, int DownloadedFiles, int ExcludedFiles, int FinalFiles, long FinalBytes, bool FinalProfileVerified);

public sealed class ClientBuildException(string phase, string? relativePath, Exception cause, bool cleanupSucceeded,
    int copiedFiles = 0, int downloadedFiles = 0, int excludedFiles = 0)
    : Exception("Client construction failed safely.", cause)
{
    public string Phase { get; } = phase;
    public string? RelativePath { get; } = relativePath;
    public string Category { get; } = cause switch
    {
        OperationCanceledException => "Cancelled", CryptographicException => "Integrity", HttpRequestException => "Network",
        UnauthorizedAccessException => "Access", IOException => "FileAccess", InvalidDataException => "Validation", _ => "Unexpected",
    };
    public bool CleanupSucceeded { get; } = cleanupSucceeded;
    public int CopiedFiles { get; } = copiedFiles;
    public int DownloadedFiles { get; } = downloadedFiles;
    public int ExcludedFiles { get; } = excludedFiles;
}

public sealed class ClientConstructor(HttpClient httpClient)
{
    private const string MarkerName = ".revora-client-doctor-staging.json";
    private const string Test2bBuildId = "doctor-reconstruction-5165-test-2b";
    private const string Test2cBuildId = "doctor-reconstruction-5165-test-2c";

    public async Task<BuildResult> BuildAsync(string sourceRoot, string destination, ClientProfile profile, DoctorInventory inventory,
        CancellationToken token, IProgress<BuildProgress>? progress = null)
    {
        SourceInventory.ValidateDestination(sourceRoot, destination);
        if (!inventory.RecognizedDonor || inventory.DetectedPatch != 5165)
            throw new InvalidDataException("Only a recognized patch-5165 donor can be reconstructed by this testing profile.");
        if (Directory.Exists(destination)) throw new IOException("The destination must not already exist.");
        string stage = destination + ".staging";
        string rollback = destination + ".rollback";
        if (Directory.Exists(rollback)) throw new IOException("A rollback directory already exists and requires review.");
        RecoverOwnedStage(sourceRoot, destination, stage, profile.BuildId);
        Directory.CreateDirectory(stage);
        WriteMarker(stage, profile.BuildId);

        int copied = 0, downloaded = 0;
        string phase = "copy";
        string? relativePath = null;
        try
        {
            var donor = inventory.Files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
            ClientFile[] passiveFiles = profile.Files.Where(file => file.Role == "passive").ToArray();
            foreach (ClientFile file in passiveFiles)
            {
                token.ThrowIfCancellationRequested();
                relativePath = file.Path;
                if (!donor.TryGetValue(file.Path, out DonorFile? source) || source.State != DonorFileState.ReusablePassive) continue;
                string sourcePath = SafePath.ResolveContained(sourceRoot, file.Path);
                string target = SafePath.ResolveContained(stage, file.Path);
                SafePath.RejectExistingReparsePoints(sourceRoot, sourcePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(sourcePath, target, overwrite: false);
                copied++;
                progress?.Report(new("copy", file.Path, copied, passiveFiles.Length, 0, 0));
            }

            ClientFile[] missing = profile.Files.Where(file => !File.Exists(SafePath.ResolveContained(stage, file.Path))).ToArray();
            phase = "download";
            var updateProgress = new InlineProgress<UpdateProgress>(item =>
            {
                phase = item.Phase; relativePath = item.RelativePath; downloaded = item.FilesCompleted;
                progress?.Report(new(item.Phase, item.RelativePath, item.FilesCompleted, item.TotalFiles, item.BytesDownloaded, item.TotalBytes));
            });
            await new AtomicUpdate(httpClient).StageAsync(stage, missing, token, updateProgress);

            phase = "final-verify"; relativePath = null;
            progress?.Report(new("final-verify", string.Empty, 0, profile.Files.Count, 0, 0));
            VerificationResult verification = await new ClientVerifier().VerifyAsync(stage, profile, token);
            if (!verification.IsValid)
            {
                relativePath = verification.Failures.FirstOrDefault()?.Path;
                throw new InvalidDataException("The constructed client failed final signed-profile verification.");
            }

            File.Delete(SafePath.ResolveContained(stage, MarkerName));
            phase = "move";
            Directory.Move(stage, destination);
            return new(true, true, copied, downloaded, inventory.Files.Count(file => file.State != DonorFileState.ReusablePassive),
                profile.Files.Count, profile.Files.Sum(file => file.Size), true);
        }
        catch (Exception error) when (error is not ClientBuildException)
        {
            if (phase == "move" && Directory.Exists(stage) && !File.Exists(SafePath.ResolveContained(stage, MarkerName)))
            {
                try { WriteMarker(stage, profile.BuildId); } catch (Exception markerError) when (markerError is IOException or UnauthorizedAccessException) { }
            }
            bool cleanup = TryDeleteOwnedStage(sourceRoot, destination, stage, profile.BuildId);
            throw new ClientBuildException(phase, relativePath, error, cleanup, copied, downloaded,
                inventory.Files.Count(file => file.State != DonorFileState.ReusablePassive));
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private static void RecoverOwnedStage(string sourceRoot, string destination, string stage, string buildId)
    {
        if (!Directory.Exists(stage)) return;
        SourceInventory.ValidateDestination(sourceRoot, destination);
        if (!HasRecoverableMarker(stage, buildId))
            throw new IOException("An unmarked staging directory named 'Revora Client Clean.staging' exists beside the selected destination. Confirm it is the failed Test 2 staging directory and remove only that exact directory manually before retrying.");
        if (!TryDeleteOwnedStage(sourceRoot, destination, stage, buildId, allowPredecessor: true))
            throw new IOException("The marked incomplete staging directory could not be removed safely.");
    }

    private static void WriteMarker(string stage, string buildId)
    {
        string marker = SafePath.ResolveContained(stage, MarkerName);
        File.WriteAllText(marker, JsonSerializer.Serialize(new { schemaVersion = 1, owner = "Revora.ClientDoctor", buildId }));
    }

    private static bool HasValidMarker(string stage, string buildId)
        => ReadMarkerBuildId(stage) == buildId;

    private static bool HasRecoverableMarker(string stage, string buildId)
    {
        string? markerBuildId = ReadMarkerBuildId(stage);
        return markerBuildId == buildId ||
            (buildId == Test2cBuildId && markerBuildId == Test2bBuildId);
    }

    private static string? ReadMarkerBuildId(string stage)
    {
        try
        {
            SafePath.RejectExistingReparsePoints(Path.GetDirectoryName(stage)!, stage);
            string marker = SafePath.ResolveContained(stage, MarkerName);
            SafePath.RejectExistingReparsePoints(stage, marker);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(marker));
            JsonElement root = document.RootElement;
            return root.GetProperty("schemaVersion").GetInt32() == 1 && root.GetProperty("owner").GetString() == "Revora.ClientDoctor"
                ? root.GetProperty("buildId").GetString()
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or KeyNotFoundException)
        {
            return null;
        }
    }

    private static bool TryDeleteOwnedStage(string sourceRoot, string destination, string stage, string buildId, bool allowPredecessor = false)
    {
        try
        {
            SourceInventory.ValidateDestination(sourceRoot, destination);
            if (!Directory.Exists(stage) || !(allowPredecessor ? HasRecoverableMarker(stage, buildId) : HasValidMarker(stage, buildId)))
                return !Directory.Exists(stage);
            foreach (string entry in Directory.EnumerateFileSystemEntries(stage, "*", SearchOption.AllDirectories))
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) return false;
            Directory.Delete(stage, recursive: true);
            return !Directory.Exists(stage);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
