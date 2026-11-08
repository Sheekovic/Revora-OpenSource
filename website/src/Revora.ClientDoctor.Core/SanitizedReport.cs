using System.Text.Json;

namespace Revora.ClientDoctor.Core;

public static class SanitizedReport
{
    public static string Create(DoctorInventory inventory, string buildId, string toolVersion) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        buildId,
        toolVersion,
        os = Environment.OSVersion.VersionString,
        patchEvidence = inventory.PatchEvidence,
        detectedPatch = inventory.DetectedPatch,
        recognizedDonor = inventory.RecognizedDonor,
        totalFiles = inventory.Files.Count,
        totalBytes = inventory.TotalBytes,
        states = inventory.Files.GroupBy(file => file.State.ToString()).ToDictionary(group => group.Key, group => group.Count()),
        files = inventory.Files.Select(file => new { file.RelativePath, file.Size, file.Sha256, state = file.State.ToString(), file.Reason })
    }, new JsonSerializerOptions { WriteIndented = true });

    public static string CreateBuildSuccess(BuildResult result, string buildId, string toolVersion) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, reportType = "build", buildId, toolVersion, os = Environment.OSVersion.VersionString,
        buildStatus = "Succeeded", destinationVerificationStatus = result.DestinationVerified ? "Verified" : "NotVerified",
        result.CopiedFiles, result.DownloadedFiles, result.ExcludedFiles, result.FinalFiles, result.FinalBytes,
        finalSignedProfileVerification = result.FinalProfileVerified ? "Passed" : "Failed",
    }, new JsonSerializerOptions { WriteIndented = true });

    public static string CreateBuildFailure(ClientBuildException error, string buildId, string toolVersion) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, reportType = "build", buildId, toolVersion, os = Environment.OSVersion.VersionString,
        buildStatus = "Failed", destinationVerificationStatus = "NotVerified", failedPhase = error.Phase,
        failedRelativeFile = error.RelativePath, exceptionCategory = error.Category, stagingCleanupSucceeded = error.CleanupSucceeded,
        error.CopiedFiles, error.DownloadedFiles, error.ExcludedFiles, finalFiles = 0, finalBytes = 0,
        finalSignedProfileVerification = "Failed",
    }, new JsonSerializerOptions { WriteIndented = true });

    public static string CreateUnhandled(string toolVersion) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, reportType = "crash", toolVersion, os = Environment.OSVersion.VersionString,
        buildStatus = "Failed", failedPhase = "dispatcher-unhandled", exceptionCategory = "Unexpected",
        destinationVerificationStatus = "NotVerified", finalSignedProfileVerification = "Failed",
    }, new JsonSerializerOptions { WriteIndented = true });
}
