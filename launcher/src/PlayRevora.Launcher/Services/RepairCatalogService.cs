using PlayRevora.Launcher.Models;

namespace PlayRevora.Launcher.Services;

public sealed record RepairCatalogFinding(string RelativePath, ClientFileMismatchCategory Category);
public sealed record RepairCatalogScanResult(IReadOnlyList<RepairCatalogFinding> Findings)
{
    public bool IsClean => Findings.Count == 0;
}

/// <summary>Explicit maintenance scan. This service is never called by normal startup readiness.</summary>
public sealed class RepairCatalogService(ValidatedClientRoot root, IProfileFileSystem files)
{
    public async Task<RepairCatalogScanResult> ScanAsync(VerifiedClientManifest catalog,
        IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        RequiredProfileFile[] declared = catalog.RootRequiredFiles.Concat(
            catalog.Profiles.SelectMany(profile => profile.RequiredFiles)).ToArray();
        var findings = new List<RepairCatalogFinding>();
        var verifier = new ClientFileIntegrityVerifier(root, files);
        for (int index = 0; index < declared.Length; index++)
        {
            try { await verifier.VerifyAsync([declared[index]], cancellationToken); }
            catch (ClientFileVerificationException exception)
            { findings.Add(new(exception.RelativePath, exception.Category)); }
            progress?.Report(new("Repairing client", declared.Length == 0 ? 100 : (index + 1) * 100d / declared.Length,
                100, $"{index + 1}/{declared.Length} files"));
        }
        return new(findings.AsReadOnly());
    }

    public static IReadOnlyList<RequiredProfileFile> PlanDownloads(VerifiedClientManifest catalog,
        RepairCatalogScanResult scan)
    {
        var declared = catalog.RootRequiredFiles.Concat(catalog.Profiles.SelectMany(profile => profile.RequiredFiles))
            .ToDictionary(file => file.RelativePath.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
        return scan.Findings.Select(finding => declared[finding.RelativePath.Replace('\\', '/')]).ToArray();
    }
}
