using System.Security.Cryptography;
using System.Text.Json;
using System.Net.Http;
using PlayRevora.Launcher.Models;

namespace PlayRevora.Launcher.Services;

public sealed class UpdateService(LauncherConfig config, IDownloadService downloads, IClock? updateClock = null)
{
    private readonly Uri baseUri = new(config.UpdateBaseUrl, UriKind.Absolute);
    private readonly RevoraVersionStore versionStore = new(config.GameRoot);
    private readonly IClock clock = updateClock ?? new SystemClock();
    private string UpdatesRoot => SafePaths.UnderRoot(config.GameRoot, "RevoraUpdates");

    public async Task<LocalVersion> ReadLocalVersionAsync(CancellationToken cancellationToken)
    {
        RevoraClientVersion version = await versionStore.ReadRequiredAsync(cancellationToken);
        return new() { Version = version.ContentVersion, UpdatedAtUtc = DateTimeOffset.UnixEpoch };
    }

    public async Task<IReadOnlyList<SignedPatchReference>> DiscoverAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.ManifestKeyId) || string.IsNullOrWhiteSpace(config.ManifestPublicKeySpkiBase64)) return [];
        RevoraClientVersion local = await ReadVersionOrSignedBaselineAsync(cancellationToken);
        SignedPatchIndex index = await LoadVerifiedIndexAsync(cancellationToken);
        return new PatchChainResolver().Resolve(index, local.ContentVersion);
    }

    private async Task<SignedPatchIndex> LoadVerifiedIndexAsync(CancellationToken cancellationToken)
    {
        var verifier = new SignedUpdateMetadataVerifier(clock);
        var trust = new ManifestTrust(config.ManifestKeyId, config.ManifestPublicKeySpkiBase64);
        string cachePath = SafePaths.UnderRoot(config.GameRoot, config.CachedPatchIndex);
        try
        {
            if (config.UpdateIndexTimeoutSeconds is < 1 or > 60)
                throw new InvalidDataException("Update index timeout is outside the supported range.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(config.UpdateIndexTimeoutSeconds));
            SignedPatchIndex remote = await downloads.GetJsonAsync<SignedPatchIndex>(
                SafePaths.RelativeHttps(baseUri, config.SignedPatchIndex), timeout.Token);
            verifier.VerifyIndex(remote, trust, config.BuildChannel);
            await JsonFiles.WriteAtomicAsync(cachePath, remote, cancellationToken);
            return remote;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException
                                           or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            if (!File.Exists(cachePath))
                throw new InvalidOperationException("The signed update index is unavailable and no trusted cache exists.", exception);
            SignedPatchIndex cached = await JsonFiles.ReadAsync<SignedPatchIndex>(cachePath, cancellationToken);
            verifier.VerifyIndex(cached, trust, config.BuildChannel);
            return cached;
        }
    }

    private async Task<RevoraClientVersion> ReadVersionOrSignedBaselineAsync(CancellationToken cancellationToken)
    {
        try { return await versionStore.ReadRequiredAsync(cancellationToken); }
        catch (InvalidDataException)
        {
            string path = SafePaths.UnderRoot(AppContext.BaseDirectory, config.SignedClientManifest);
            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            SignedClientManifest signed = JsonSerializer.Deserialize<SignedClientManifest>(bytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Signed baseline metadata is invalid.");
            _ = new SignedManifestVerifier(clock, typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0))
                .Verify(signed, new(config.ManifestKeyId, config.ManifestPublicKeySpkiBase64));
            string version = SemanticVersion.Parse(signed.ManifestVersion).ToString();
            return new(1, config.BaselineClientPatch, version, config.BuildChannel, "baseline-not-installed");
        }
    }

    public async Task<LocalVersion> UpdateAsync(IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.ManifestKeyId) || string.IsNullOrWhiteSpace(config.ManifestPublicKeySpkiBase64))
            throw new InvalidOperationException("Trusted update verification is not provisioned.");
        RevoraClientVersion local = await versionStore.ReadRequiredAsync(cancellationToken);
        progress.Report(new("Checking for updates", 0, 0, "Signed index"));
        SignedPatchIndex index = await downloads.GetJsonAsync<SignedPatchIndex>(SafePaths.RelativeHttps(baseUri, config.SignedPatchIndex), cancellationToken);
        var trust = new ManifestTrust(config.ManifestKeyId, config.ManifestPublicKeySpkiBase64);
        var metadataVerifier = new SignedUpdateMetadataVerifier(clock); metadataVerifier.VerifyIndex(index, trust, config.BuildChannel);
        IReadOnlyList<SignedPatchReference> chain = new PatchChainResolver().Resolve(index, local.ContentVersion);
        if (chain.Count == 0) { progress.Report(new("Ready", 100, 100, "Client is current")); return new() { Version = local.ContentVersion }; }

        for (int patchIndex = 0; patchIndex < chain.Count; patchIndex++)
        {
            SignedPatchReference reference = chain[patchIndex]; progress.Report(new("Update available", patchIndex * 100d / chain.Count, 0, reference.TargetVersion));
            SignedPatchPackage package = await downloads.GetJsonAsync<SignedPatchPackage>(SafePaths.RelativeHttps(baseUri, reference.MetadataPath), cancellationToken);
            metadataVerifier.VerifyPackage(package, trust, config.BuildChannel, reference);
            string downloadDirectory = SafePaths.UnderRoot(UpdatesRoot, "Downloads"); Directory.CreateDirectory(downloadDirectory);
            string archive = SafePaths.UnderRoot(downloadDirectory, package.PatchId + ".zip.partial");
            progress.Report(new("Downloading", patchIndex * 100d / chain.Count, 0, "Resumable package"));
            await downloads.DownloadResumableAsync(SafePaths.RelativeHttps(baseUri, package.ArchivePath), archive, package.ArchiveSize, cancellationToken, progress);
            progress.Report(new("Verifying package", patchIndex * 100d / chain.Count, 0, "SHA-256"));
            if (!await FileIntegrity.MatchesAsync(archive, package.ArchiveSize, package.ArchiveSha256, cancellationToken))
                throw new InvalidDataException("Downloaded package failed signed integrity verification.");
            string verifiedArchive = SafePaths.UnderRoot(downloadDirectory, package.PatchId + ".zip");
            File.Move(archive, verifiedArchive, true); archive = verifiedArchive;

            string staging = SafePaths.UnderRoot(UpdatesRoot, $"Staging/{package.PatchId}-{Guid.NewGuid():N}");
            string activeStaging = SafePaths.UnderRoot(UpdatesRoot, "Staging/active");
            Directory.CreateDirectory(activeStaging);
            progress.Report(new("Preparing update", patchIndex * 100d / chain.Count, 0, "Safe extraction"));
            try
            {
                await new SafePatchExtractor().ExtractAsync(archive, staging, package.Files, progress, cancellationToken);
                var installer = new TransactionalPatchInstaller(config.GameRoot, "RevoraUpdates", new ProcessInstallationGuard(), clock);
                progress.Report(new("Installing", patchIndex * 100d / chain.Count, 0, "Transactional install"));
                await installer.InstallAsync(package, staging, token => VerifyInstalledTargetAsync(package, trust, token), async token =>
                {
                    var next = local with { ContentVersion = SemanticVersion.Parse(package.TargetVersion).ToString(), LastSuccessfulPatch = package.PatchId };
                    await versionStore.WriteAtomicAsync(next, token);
                    var receipt = new VerificationReceiptStore(config.GameRoot, config.VerificationReceiptFile, clock);
                    await receipt.WriteAsync(config.GameRoot, package.TargetManifestSha256, config.BuildChannel, next, token); local = next;
                }, cancellationToken, [config.VersionFile, config.VerificationReceiptFile]);
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); if (Directory.Exists(activeStaging)) Directory.Delete(activeStaging, true); }
        }
        progress.Report(new("Ready", 100, 100, "Update verified")); return new() { Version = local.ContentVersion, UpdatedAtUtc = clock.UtcNow };
    }

    private async Task VerifyInstalledTargetAsync(SignedPatchPackage package, ManifestTrust trust, CancellationToken cancellationToken)
    {
        string path = SafePaths.UnderRoot(config.GameRoot, package.TargetManifestPath);
        if (!File.Exists(path)) throw new InvalidDataException("Target client manifest is missing.");
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(package.TargetManifestSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Target client manifest digest does not match patch metadata.");
        SignedClientManifest manifest = JsonSerializer.Deserialize<SignedClientManifest>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Target client manifest is invalid.");
        if (manifest.ManifestVersion != package.TargetManifestId) throw new InvalidDataException("Target client manifest identity does not match patch metadata.");
        VerifiedClientManifest verified = new SignedManifestVerifier(clock, typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0)).Verify(manifest, trust);
        var fileSystem = new PhysicalProfileFileSystem(); var root = new ValidatedClientRoot(config.GameRoot, fileSystem);
        var integrity = new ClientFileIntegrityVerifier(root, fileSystem);
        var declaredTargets = package.Files.ToDictionary(file => file.TargetPath.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
        var catalog = verified.RootRequiredFiles.Concat(verified.Profiles.SelectMany(profile => profile.RequiredFiles))
            .ToDictionary(file => file.RelativePath.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
        var affected = new List<RequiredProfileFile>();
        foreach (string targetPath in declaredTargets.Keys)
        {
            if (targetPath.Equals(package.TargetManifestPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;
            if (!catalog.TryGetValue(targetPath, out RequiredProfileFile? expected))
                throw new InvalidDataException("A patched target is absent from the signed target catalog.");
            affected.Add(expected);
        }
        await integrity.VerifyAsync(affected, cancellationToken);
        foreach (string deleted in package.DeletedFiles)
            if (File.Exists(root.Resolve(deleted))) throw new InvalidDataException("A declared deleted file remains installed.");
        foreach (ClientProfile profile in verified.Profiles)
            await integrity.VerifyAsync(profile.CriticalLaunchFiles, cancellationToken);
    }
}

public sealed class ProcessInstallationGuard : IInstallationGuard
{
    public Task EnsureExclusiveAsync(CancellationToken cancellationToken)
    {
        int currentId = Environment.ProcessId;
        if (System.Diagnostics.Process.GetProcessesByName("Conquer").Any(process => process.Id != currentId)
            || System.Diagnostics.Process.GetProcessesByName("PlayRevora.Launcher").Any(process => process.Id != currentId))
            throw new InvalidOperationException("Close the game and other launcher instances before installing updates.");
        return Task.CompletedTask;
    }
}
