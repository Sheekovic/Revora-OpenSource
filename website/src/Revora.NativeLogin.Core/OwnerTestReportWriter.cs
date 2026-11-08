using System.Text.Json;

namespace Revora.NativeLogin.Core;

public sealed record OwnerTestFailureEvidence(string BuildId, string FailurePhase, string FailureCategory,
    string Component, string? ExpectedSha256, string? ActualSha256, string? MarkerBaselineBuildId,
    string? PreviousControlledBuildId, bool RollbackAttempted, string RollbackResult, OwnerRootEvidence? RootEvidence = null);

public sealed class OwnerTestReportWriter
{
    private readonly string directory;
    public OwnerTestReportWriter(string packageRoot)
    {
        directory = Path.Combine(Path.GetFullPath(packageRoot), "Reports");
        Directory.CreateDirectory(directory);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The Reports directory cannot be a reparse point.");
    }

    public async Task<string> WriteFailureAsync(OwnerTestFailureEvidence evidence, CancellationToken token)
    {
        string reportPrefix = evidence.BuildId.EndsWith("test-4j", StringComparison.Ordinal) ? "owner-test4j-failure" :
            evidence.BuildId.EndsWith("test-4i", StringComparison.Ordinal) ? "owner-test4i-failure" :
            evidence.BuildId.EndsWith("test-4h", StringComparison.Ordinal) ? "owner-test4h-failure" :
            evidence.BuildId.EndsWith("test-4g", StringComparison.Ordinal) ? "owner-test4g-failure" :
            evidence.BuildId.EndsWith("test-4f", StringComparison.Ordinal) ? "owner-test4f-failure" :
            evidence.BuildId.EndsWith("test-4e", StringComparison.Ordinal) ? "owner-test4e-failure" : "owner-test4d-r1-failure";
        string path = Path.Combine(directory, $"{reportPrefix}-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, timestampUtc = DateTimeOffset.UtcNow,
            evidence.BuildId, evidence.FailurePhase, evidence.FailureCategory,
            safeRelativeComponentName = SafeComponent(evidence.Component),
            evidence.ExpectedSha256, evidence.ActualSha256, evidence.MarkerBaselineBuildId,
            evidence.PreviousControlledBuildId, evidence.RollbackAttempted, evidence.RollbackResult,
            sourceKind = evidence.RootEvidence?.SourceKind ?? "unknown",
            markerWorkspaceId = evidence.RootEvidence?.WorkspaceId,
            baselineBuildId = evidence.RootEvidence?.BaselineBuildId ?? evidence.MarkerBaselineBuildId,
            signedProfileMatchResult = evidence.RootEvidence?.SignedProfileMatch ?? "unknown",
            safeRootFingerprint = evidence.RootEvidence?.SafeRootFingerprint,
            donorWorkspaceOverlap = evidence.RootEvidence?.DonorWorkspaceOverlap,
            expectedClientRoot = evidence.RootEvidence?.ExpectedClientRoot,
        }, new JsonSerializerOptions { WriteIndented = true }), token);
        return path;
    }

    private static string SafeComponent(string component)
    {
        string normalized = component.Replace('\\', '/');
        return normalized.Length is > 0 and <= 128 && !Path.IsPathRooted(normalized) &&
            !normalized.Split('/').Any(part => part is "" or "." or "..") ? normalized : "unspecified-component";
    }
}
