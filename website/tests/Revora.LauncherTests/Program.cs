using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Revora.Launcher.Core;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Fixed endpoint policy", FixedEndpointPolicy),
    ("Valid signed profile", ValidSignedProfile),
    ("Tampered profile rejected", TamperedProfileRejected),
    ("Wrong endpoint rejected", WrongEndpointRejected),
    ("Traversal rejected", TraversalRejected),
    ("Windows special paths rejected", WindowsSpecialPathsRejected),
    ("HTTP update rejected", HttpRejected),
    ("Missing client file rejected", MissingFileRejected),
    ("Exact client hash accepted", ExactHashAccepted),
    ("Unexpected client hash rejected", UnexpectedHashRejected),
    ("Client launch unavailable off Windows", LaunchRejectedOffWindows),
    ("Deterministic launcher state transitions", StateTransitions),
    ("Primary action mapping", PrimaryActionMapping),
    ("Test package channel marker", TestPackageChannelMarker),
    ("Valid desired-state signature", DesiredStateSignature),
    ("Desired-state altered entry rejected", DesiredStateAltered),
    ("Desired-state duplicate path rejected", DesiredStateDuplicate),
    ("Desired-state excludes launcher-owned files", DesiredStateExcludesLauncherFiles),
    ("Desired-state downgrade rejected", DesiredStateDowngrade),
    ("Client root fingerprint and write probe", ClientRootFingerprint),
    ("Blank portable folder is a bounded first installation", BlankPortableRoot),
    ("Arbitrary selected blank folder is rejected", SelectedBlankRootRejected),
    ("Partial client fingerprint is repairable", PartialClientRoot),
    ("Dangerous client roots rejected", DangerousClientRoots),
    ("Verified-state cache avoids passive rehash", VerifiedCacheScan),
    ("Corrupt cache triggers revalidation", CorruptCache),
    ("Verified-state cache real Windows lifecycle", VerifiedCacheWindowsLifecycle),
    ("Temporary Windows sharing violation retries", TemporarySharingViolation),
    ("Permanent Windows sharing violation fails closed", PermanentSharingViolation),
    ("Cancelled scan releases every file handle", CancelledScanReleasesHandles),
    ("Range download resumes and verifies", RangeResume),
    ("No-range server restarts safely", NoRangeRestart),
    ("Download hash mismatch rejected", DownloadHashMismatch),
    ("Atomic apply preserves unmanaged files", AtomicApplyPreservesUnmanaged),
    ("Atomic apply failure rolls back", AtomicApplyRollback),
    ("Patch lock excludes a second launcher", PatchLockExclusion),
    ("Sanitized diagnostics export", DiagnosticsExport),
    ("Sharing diagnostics are bounded and actionable", SharingDiagnostics),
    ("Cache-write diagnostics use safe operation identity", CacheWriteDiagnostics),
    ("Launcher release policy", LauncherReleasePolicy),
    ("Launcher self-update preserves client files", LauncherSelfUpdatePreservesClient),
    ("Launcher self-update readiness failure rolls back", LauncherSelfUpdateRollback),
    ("Launcher self-update hash failure leaves live files", LauncherSelfUpdateHashFailure),
};

int failures = 0;
foreach ((string name, Func<Task> run) in tests)
{
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
}
Console.WriteLine($"Total: {tests.Length}, Passed: {tests.Length - failures}, Failed: {failures}");
return failures == 0 ? 0 : 1;

static Task FixedEndpointPolicy()
{
    Assert(LauncherPolicy.ServerHost == "playrevora.com" && LauncherPolicy.AccountPort == 9958 && LauncherPolicy.GamePort == 9959, "Endpoint policy changed.");
    return Task.CompletedTask;
}

static Task ValidSignedProfile()
{
    using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    ClientProfile actual = new ManifestVerifier(key.ExportSubjectPublicKeyInfo()).VerifyAndRead(Sign(Profile(), key));
    Assert(actual.ClientPatch == 5165, "Profile was not decoded.");
    return Task.CompletedTask;
}

static Task TamperedProfileRejected()
{
    using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    byte[] envelope = Sign(Profile(), key);
    envelope[^10] ^= 1;
    AssertThrows<Exception>(() => new ManifestVerifier(key.ExportSubjectPublicKeyInfo()).VerifyAndRead(envelope));
    return Task.CompletedTask;
}

static Task WrongEndpointRejected()
{
    ClientProfile profile = Profile() with { ServerHost = "example.invalid" };
    AssertThrows<InvalidDataException>(() => ManifestVerifier.ValidatePolicy(profile));
    return Task.CompletedTask;
}

static Task TraversalRejected()
{
    Assert(!SafePath.IsNormalizedRelative("../Conquer.exe"), "Traversal was accepted.");
    Assert(!SafePath.IsNormalizedRelative("client\\Conquer.exe"), "Backslash path was accepted.");
    AssertThrows<InvalidDataException>(() => SafePath.ResolveContained(Path.GetTempPath(), "../../escape"));
    return Task.CompletedTask;
}

static Task WindowsSpecialPathsRejected()
{
    Assert(!SafePath.IsNormalizedRelative("C:evil.exe"), "Drive-relative path was accepted.");
    Assert(!SafePath.IsNormalizedRelative("client/NUL.txt"), "Windows device path was accepted.");
    Assert(!SafePath.IsNormalizedRelative("client/file. "), "Trailing dot/space was accepted.");
    return Task.CompletedTask;
}

static Task HttpRejected()
{
    ClientProfile profile = Profile() with { Files = [new("Conquer.exe", 3, new string('a', 64), "http://example.invalid/Conquer.exe")] };
    AssertThrows<InvalidDataException>(() => ManifestVerifier.ValidatePolicy(profile));
    return Task.CompletedTask;
}

static async Task MissingFileRejected()
{
    string root = MakeTemp();
    VerificationResult result = await new ClientVerifier().VerifyAsync(root, Profile(), CancellationToken.None);
    Assert(!result.IsValid && result.Failures.Single().Reason == "Missing", "Missing file was accepted.");
}

static async Task ExactHashAccepted()
{
    string root = MakeTemp();
    byte[] content = "abc"u8.ToArray();
    await File.WriteAllBytesAsync(Path.Combine(root, "Conquer.exe"), content);
    ClientProfile profile = Profile() with { Files = [new("Conquer.exe", content.Length, Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), null)] };
    Assert((await new ClientVerifier().VerifyAsync(root, profile, CancellationToken.None)).IsValid, "Exact file failed verification.");
}

static async Task UnexpectedHashRejected()
{
    string root = MakeTemp();
    await File.WriteAllTextAsync(Path.Combine(root, "Conquer.exe"), "tampered");
    VerificationResult result = await new ClientVerifier().VerifyAsync(root, Profile(), CancellationToken.None);
    Assert(!result.IsValid, "Unexpected file was accepted.");
}

static Task LaunchRejectedOffWindows()
{
    if (!OperatingSystem.IsWindows())
    {
        AssertThrows<PlatformNotSupportedException>(() => new ClientProcessLauncher().LaunchVerified(MakeTemp(), Profile(), new(true, [])));
    }
    return Task.CompletedTask;
}

static Task StateTransitions()
{
    var machine = new LauncherStateMachine();
    machine.Transition(LauncherState.ResolvingClient, "resolve");
    machine.Transition(LauncherState.Recovering, "recover");
    machine.Transition(LauncherState.FetchingManifest, "fetch");
    machine.Transition(LauncherState.VerifyingManifest, "verify");
    machine.Transition(LauncherState.Scanning, "scan");
    machine.Transition(LauncherState.RepairRequired, "repair");
    machine.Transition(LauncherState.Downloading, "download", totalBytes: 10);
    machine.Transition(LauncherState.Paused, "pause", totalBytes: 10);
    machine.Transition(LauncherState.Downloading, "resume", totalBytes: 10);
    machine.Transition(LauncherState.VerifyingDownloads, "verify download");
    machine.Transition(LauncherState.Applying, "apply");
    machine.Transition(LauncherState.Scanning, "rescan");
    machine.Transition(LauncherState.Ready, "ready");
    machine.Transition(LauncherState.Launching, "launch");
    machine.Transition(LauncherState.GameRunning, "game");
    machine.Transition(LauncherState.Ready, "ready again");
    AssertThrows<InvalidOperationException>(() => machine.Transition(LauncherState.Applying, "illegal"));
    return Task.CompletedTask;
}

static Task PrimaryActionMapping()
{
    Assert(LauncherStateMachine.PrimaryAction(LauncherState.ResolvingClient, false) == "Locate Client", "Locate action changed.");
    Assert(LauncherStateMachine.PrimaryAction(LauncherState.RepairRequired) == "Repair Client", "Repair action changed.");
    Assert(LauncherStateMachine.PrimaryAction(LauncherState.Ready) == "Play Revora", "Ready action changed.");
    Assert(LauncherStateMachine.PrimaryAction(LauncherState.GameRunning) == "Open Another Account", "Multi-account action is unavailable while a game is running.");
    return Task.CompletedTask;
}

static Task TestPackageChannelMarker()
{
    string root = MakeTemp();
    Assert(LauncherChannelResolver.Resolve(root, []) == LauncherChannel.Stable, "Stable was not the safe default.");
    Assert(LauncherChannelResolver.Resolve(root, ["launcher", "--test-channel"]) == LauncherChannel.Test,
        "Explicit test-channel switch was not accepted.");
    File.WriteAllText(Path.Combine(root, LauncherChannelResolver.TestMarkerFileName),
        LauncherChannelResolver.TestMarkerContent + Environment.NewLine);
    Assert(LauncherChannelResolver.Resolve(root, []) == LauncherChannel.Test,
        "Exact test-package marker was not accepted.");
    Assert(LauncherChannelResolver.ClientManifest(LauncherChannel.Test) == LauncherPolicy.TestManifest,
        "Test client endpoint changed.");
    Assert(LauncherChannelResolver.LauncherManifest(LauncherChannel.Test) == LauncherPolicy.TestLauncherReleaseManifest,
        "Test launcher endpoint changed.");
    File.WriteAllText(Path.Combine(root, LauncherChannelResolver.TestMarkerFileName), "test");
    AssertThrows<InvalidDataException>(() => LauncherChannelResolver.Resolve(root, []));
    return Task.CompletedTask;
}

static Task DesiredStateSignature()
{
    using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    DesiredStateManifest expected = Desired();
    DesiredStateManifest actual = new DesiredStateManifestVerifier(key.ExportSubjectPublicKeyInfo(), LauncherPolicy.SigningKeyId)
        .Verify(SignDesired(expected, key), Version.Parse("1.0.0"));
    Assert(actual.ReleaseId == expected.ReleaseId && actual.Files.Single().Critical, "Desired-state manifest changed.");
    return Task.CompletedTask;
}

static Task DesiredStateAltered()
{
    using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    byte[] envelope = SignDesired(Desired(), key); envelope[^8] ^= 1;
    AssertThrows<Exception>(() => new DesiredStateManifestVerifier(key.ExportSubjectPublicKeyInfo(), LauncherPolicy.SigningKeyId)
        .Verify(envelope, Version.Parse("1.0.0")));
    return Task.CompletedTask;
}

static Task DesiredStateDuplicate()
{
    DesiredStateManifest manifest = Desired();
    manifest = manifest with { Files = [manifest.Files[0], manifest.Files[0] with { Sha256 = new string('b', 64), ObjectPath = $"objects/sha256/bb/{new string('b', 64)}" }] };
    AssertThrows<InvalidDataException>(() => DesiredStateManifestVerifier.Validate(manifest, Version.Parse("1.0.0")));
    return Task.CompletedTask;
}

static Task DesiredStateExcludesLauncherFiles()
{
    DesiredStateManifest manifest = Desired();
    DesiredStateFile invalid = manifest.Files[0] with { Path = "Revora.Launcher.exe" };
    AssertThrows<InvalidDataException>(() => DesiredStateManifestVerifier.Validate(
        manifest with { Files = [manifest.Files[0], invalid] }, Version.Parse(LauncherPolicy.ProductVersion)));
    invalid = invalid with { Path = ".revora-launcher/verified-state.json" };
    AssertThrows<InvalidDataException>(() => DesiredStateManifestVerifier.Validate(
        manifest with { Files = [manifest.Files[0], invalid] }, Version.Parse(LauncherPolicy.ProductVersion)));
    return Task.CompletedTask;
}

static Task DesiredStateDowngrade()
{
    DesiredStateManifest manifest = Desired() with { PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-2) };
    AssertThrows<InvalidDataException>(() => DesiredStateManifestVerifier.Validate(manifest, Version.Parse("1.0.0"), Guid.NewGuid(), DateTimeOffset.UtcNow));
    return Task.CompletedTask;
}

static Task ClientRootFingerprint()
{
    string root = MakeTemp(); File.WriteAllText(Path.Combine(root, "Conquer.exe"), "client"); File.WriteAllText(Path.Combine(root, "version.dat"), "5165"); Directory.CreateDirectory(Path.Combine(root, "ini"));
    string launcher = Path.Combine(root, "Revora.Launcher.exe"); File.WriteAllText(launcher, "launcher");
    ClientRootResult result = new ClientRootResolver().ResolvePortable(launcher);
    Assert(result.Root == root && result.IsWritable && result.Kind == ClientRootKind.ExistingClient &&
        result.FingerprintFiles.Contains("Conquer.exe"), "Portable client root was not resolved.");
    return Task.CompletedTask;
}

static Task BlankPortableRoot()
{
    string root = MakeTemp();
    string launcher = Path.Combine(root, "Revora.Launcher.exe");
    File.WriteAllText(launcher, "launcher");
    ClientRootResult result = new ClientRootResolver().ResolvePortable(launcher);
    Assert(result.Root == root && result.IsWritable && result.Kind == ClientRootKind.NewInstallation &&
        result.FingerprintFiles.Count == 0, "A blank portable launcher folder was not accepted as a bounded first installation.");
    return Task.CompletedTask;
}

static Task SelectedBlankRootRejected()
{
    string launcherRoot = MakeTemp(), selected = MakeTemp();
    string launcher = Path.Combine(launcherRoot, "Revora.Launcher.exe");
    File.WriteAllText(launcher, "launcher");
    AssertThrows<InvalidDataException>(() => new ClientRootResolver().ResolvePortable(launcher, selected));
    return Task.CompletedTask;
}

static Task PartialClientRoot()
{
    string root = MakeTemp();
    string launcher = Path.Combine(root, "Revora.Launcher.exe");
    File.WriteAllText(launcher, "launcher");
    File.WriteAllText(Path.Combine(root, "version.dat"), "5165");
    ClientRootResult result = new ClientRootResolver().ResolvePortable(launcher);
    Assert(result.Kind == ClientRootKind.RepairableClient && result.FingerprintFiles.SequenceEqual(["version.dat"]),
        "A partial client was not classified for signed repair.");
    return Task.CompletedTask;
}

static Task DangerousClientRoots()
{
    AssertThrows<InvalidDataException>(() => ClientRootResolver.RejectDangerousRoot(Path.GetPathRoot(Path.GetTempPath())!, MakeTemp()));
    string repo = MakeTemp(); Directory.CreateDirectory(Path.Combine(repo, ".git"));
    AssertThrows<InvalidDataException>(() => ClientRootResolver.RejectDangerousRoot(repo, repo));
    return Task.CompletedTask;
}

static async Task VerifiedCacheScan()
{
    string root = MakeTemp(); byte[] data = "abc"u8.ToArray(); string path = Path.Combine(root, "Conquer.exe"); await File.WriteAllBytesAsync(path, data);
    string hash = Convert.ToHexStringLower(SHA256.HashData(data)); DesiredStateManifest manifest = Desired(hash, data.Length);
    var state = new VerifiedFileState("Conquer.exe", data.Length, File.GetLastWriteTimeUtc(path).Ticks, hash);
    var cache = new VerifiedStateDocument(1, manifest.ReleaseId, manifest.PublishedAtUtc, [state]);
    ClientScanResult scan = await new DesiredStateScanner().ScanAsync(root, manifest, cache, false, null, default);
    Assert(scan.IsValid && scan.HashedBytes == data.Length && scan.CachedFiles == 0, "Critical executable incorrectly trusted the cache.");
    DesiredStateFile passive = manifest.Files[0] with { Path = "version.dat", Critical = false };
    await File.WriteAllBytesAsync(Path.Combine(root, "version.dat"), data); passive = passive with { Sha256 = hash, ObjectPath = $"objects/sha256/{hash[..2]}/{hash}" };
    manifest = manifest with { Files = [manifest.Files[0], passive] };
    cache = cache with { Files = [state, new("version.dat", data.Length, File.GetLastWriteTimeUtc(Path.Combine(root, "version.dat")).Ticks, hash)] };
    scan = await new DesiredStateScanner().ScanAsync(root, manifest, cache, false, null, default);
    Assert(scan.IsValid && scan.CachedFiles == 1, "Unchanged passive content was needlessly rehashed.");
}

static async Task CorruptCache()
{
    string root = MakeTemp(); Directory.CreateDirectory(Path.Combine(root, ".revora-launcher"));
    await File.WriteAllTextAsync(VerifiedStateCache.PathFor(root), "{not-json");
    Assert(await new VerifiedStateCache().ReadAsync(root, default) is null, "A corrupt verified-state cache was accepted.");
}

static async Task VerifiedCacheWindowsLifecycle()
{
    if (!OperatingSystem.IsWindows()) return;
    string root = MakeTemp();
    byte[] critical = "critical-client"u8.ToArray(), passive = "5165"u8.ToArray();
    string criticalPath = Path.Combine(root, "Conquer.exe"), passivePath = Path.Combine(root, "version.dat");
    await File.WriteAllBytesAsync(criticalPath, critical);
    await File.WriteAllBytesAsync(passivePath, passive);
    string criticalHash = Convert.ToHexStringLower(SHA256.HashData(critical));
    string passiveHash = Convert.ToHexStringLower(SHA256.HashData(passive));
    DesiredStateManifest manifest = Desired(criticalHash, critical.Length);
    manifest = manifest with
    {
        Files =
        [
            manifest.Files[0],
            manifest.Files[0] with
            {
                Path = "version.dat", Size = passive.Length, Sha256 = passiveHash,
                ObjectPath = $"objects/sha256/{passiveHash[..2]}/{passiveHash}", Critical = false,
                Executable = false,
            },
        ],
    };
    VerifiedFileState[] states =
    [
        new("Conquer.exe", critical.Length, File.GetLastWriteTimeUtc(criticalPath).Ticks, criticalHash),
        new("version.dat", passive.Length, File.GetLastWriteTimeUtc(passivePath).Ticks, passiveHash),
    ];
    var cache = new VerifiedStateCache();
    string path = VerifiedStateCache.PathFor(root), temporary = path + ".new";

    await cache.WriteAsync(root, manifest, states, default);
    Assert(!File.Exists(temporary), "The first cache write left its temporary file behind.");
    VerifiedStateDocument first = await cache.ReadAsync(root, default)
        ?? throw new InvalidOperationException("The first cache document could not be read.");
    Assert(first.ReleaseId == manifest.ReleaseId && first.Files.Count == 2 &&
        first.Files.Any(file => file.Path == "Conquer.exe" && file.Sha256 == criticalHash) &&
        first.Files.Any(file => file.Path == "version.dat" && file.Sha256 == passiveHash),
        "The complete first cache document was not preserved.");

    await cache.WriteAsync(root, manifest, states.Reverse().ToArray(), default);
    for (int repeat = 0; repeat < 5; repeat++)
        await cache.WriteAsync(root, manifest, states, default);
    await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
        new VerifiedStateCache().WriteAsync(root, manifest, states, default)));
    VerifiedStateDocument concurrent = await cache.ReadAsync(root, default)
        ?? throw new InvalidOperationException("Serialized concurrent writes produced no cache.");
    Assert(concurrent.Files.Count == 2 && !File.Exists(temporary),
        "Repeated or serialized writes produced an incomplete cache or stale temporary file.");

    string moved = path + ".moved";
    File.Move(path, moved, false);
    File.Delete(moved);
    Assert(!File.Exists(path), "The cache remained locked after WriteAsync returned.");

    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    await AssertThrowsAsync<OperationCanceledException>(() => cache.WriteAsync(root, manifest, states, cancelled.Token));
    Assert(!File.Exists(temporary), "Cancellation left verified-state.json.new behind.");
    await cache.WriteAsync(root, manifest, states, default);
    File.Delete(path);
    Assert(!File.Exists(path) && !File.Exists(temporary), "The final cache handle was not released immediately.");
}

static async Task TemporarySharingViolation()
{
    if (!OperatingSystem.IsWindows()) return;
    string root = MakeTemp(); byte[] data = new byte[256 * 1024]; RandomNumberGenerator.Fill(data);
    string path = Path.Combine(root, "Conquer.exe"); await File.WriteAllBytesAsync(path, data);
    string hash = Convert.ToHexStringLower(SHA256.HashData(data)); DesiredStateManifest manifest = Desired(hash, data.Length);
    using FileStream owner = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    Task<ClientScanResult> pending = new DesiredStateScanner(6, TimeSpan.FromMilliseconds(25))
        .ScanAsync(root, manifest, null, false, null, default);
    await Task.Delay(120); owner.Dispose();
    ClientScanResult result = await pending;
    Assert(result.IsValid && result.HashedBytes == data.Length,
        "A temporary sharing violation did not recover by reopening the file.");
}

static async Task PermanentSharingViolation()
{
    if (!OperatingSystem.IsWindows()) return;
    string root = MakeTemp(); byte[] data = "locked-critical"u8.ToArray();
    string path = Path.Combine(root, "Conquer.exe"); await File.WriteAllBytesAsync(path, data);
    string hash = Convert.ToHexStringLower(SHA256.HashData(data)); DesiredStateManifest manifest = Desired(hash, data.Length);
    using FileStream owner = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    ClientFileAccessException error = await CaptureAsync<ClientFileAccessException>(() =>
        new DesiredStateScanner(3, TimeSpan.FromMilliseconds(5)).ScanAsync(root, manifest, null, false, null, default));
    Assert(error.RelativePath == "Conquer.exe" && error.Operation == "read-only-hash" &&
        error.AttemptCount == 3 && error.Exhausted && error.NativeErrorCode == WindowsFileErrors.SharingViolation,
        "The exhausted sharing violation lost its safe Windows evidence.");
}

static async Task CancelledScanReleasesHandles()
{
    if (!OperatingSystem.IsWindows()) return;
    string root = MakeTemp(); byte[] data = "cancelled-lock"u8.ToArray();
    string path = Path.Combine(root, "Conquer.exe"); await File.WriteAllBytesAsync(path, data);
    string hash = Convert.ToHexStringLower(SHA256.HashData(data)); DesiredStateManifest manifest = Desired(hash, data.Length);
    using FileStream owner = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
    await AssertThrowsAsync<OperationCanceledException>(() => new DesiredStateScanner(8, TimeSpan.FromMilliseconds(50))
        .ScanAsync(root, manifest, null, false, null, cancellation.Token));
    owner.Dispose();
    using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    Assert(exclusive.Length == data.Length, "Cancellation left a background scan handle open.");
}

static async Task RangeResume()
{
    byte[] data = "abcdef"u8.ToArray(); string hash = Convert.ToHexStringLower(SHA256.HashData(data)); DesiredStateManifest manifest = Desired(hash, data.Length);
    string stage = MakeTemp(); string objectPath = SafePath.ResolveContained(stage, manifest.Files[0].ObjectPath); Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!); await File.WriteAllBytesAsync(objectPath + ".partial", data[..2]);
    using var http = new HttpClient(new TestHandler(request =>
    {
        Assert(request.Headers.Range?.Ranges.Single().From == 2, "Resume range was not requested.");
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(data[2..]) };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2, 5, 6); return response;
    }));
    await new PatchDownloadEngine(http, new(1, 1)).DownloadAsync(stage, manifest, manifest.Files, new PauseController(), null, default);
    Assert((await File.ReadAllBytesAsync(objectPath)).SequenceEqual(data), "The resumed object differs.");
}

static async Task NoRangeRestart()
{
    byte[] data = "abcdef"u8.ToArray(); string hash = Convert.ToHexStringLower(SHA256.HashData(data)); DesiredStateManifest manifest = Desired(hash, data.Length);
    string stage = MakeTemp(); string objectPath = SafePath.ResolveContained(stage, manifest.Files[0].ObjectPath); Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!); await File.WriteAllBytesAsync(objectPath + ".partial", data[..2]);
    using var http = new HttpClient(new TestHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) }));
    await new PatchDownloadEngine(http, new(1, 1)).DownloadAsync(stage, manifest, manifest.Files, new PauseController(), null, default);
    Assert((await File.ReadAllBytesAsync(objectPath)).SequenceEqual(data), "No-range restart failed.");
}

static async Task DownloadHashMismatch()
{
    byte[] data = "abcdef"u8.ToArray(); DesiredStateManifest manifest = Desired(new string('a', 64), data.Length);
    using var http = new HttpClient(new TestHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) }));
    await AssertThrowsAsync<CryptographicException>(() => new PatchDownloadEngine(http, new(1, 1)).DownloadAsync(MakeTemp(), manifest, manifest.Files, new PauseController(), null, default));
}

static async Task AtomicApplyPreservesUnmanaged()
{
    byte[] replacement = "new"u8.ToArray(); string hash = Convert.ToHexStringLower(SHA256.HashData(replacement)); DesiredStateManifest manifest = Desired(hash, replacement.Length);
    string root = MakeTemp(), stage = MakeTemp(); await File.WriteAllTextAsync(Path.Combine(root, "Conquer.exe"), "old"); await File.WriteAllTextAsync(Path.Combine(root, "screenshot.png"), "owned");
    string source = SafePath.ResolveContained(stage, manifest.Files[0].ObjectPath); Directory.CreateDirectory(Path.GetDirectoryName(source)!); await File.WriteAllBytesAsync(source, replacement);
    await new TransactionalPatchApplier().ApplyAsync(root, stage, manifest, manifest.Files, null, default);
    Assert(await File.ReadAllTextAsync(Path.Combine(root, "Conquer.exe")) == "new" && await File.ReadAllTextAsync(Path.Combine(root, "screenshot.png")) == "owned", "Atomic apply changed unmanaged content.");
}

static async Task AtomicApplyRollback()
{
    byte[] first = "new-a"u8.ToArray(), second = "new-b"u8.ToArray(); string hashA = Convert.ToHexStringLower(SHA256.HashData(first)), hashB = Convert.ToHexStringLower(SHA256.HashData(second));
    DesiredStateManifest manifest = Desired(hashA, first.Length); DesiredStateFile b = manifest.Files[0] with { Path = "z.dat", Size = second.Length, Sha256 = hashB, ObjectPath = $"objects/sha256/{hashB[..2]}/{hashB}", Critical = false };
    manifest = manifest with { Files = [manifest.Files[0], b] };
    string root = MakeTemp(), stage = MakeTemp(); await File.WriteAllTextAsync(Path.Combine(root, "Conquer.exe"), "old-a");
    string source = SafePath.ResolveContained(stage, manifest.Files[0].ObjectPath); Directory.CreateDirectory(Path.GetDirectoryName(source)!); await File.WriteAllBytesAsync(source, first);
    await AssertThrowsAsync<IOException>(() => new TransactionalPatchApplier().ApplyAsync(root, stage, manifest, manifest.Files, null, default));
    Assert(await File.ReadAllTextAsync(Path.Combine(root, "Conquer.exe")) == "old-a", "Failed apply did not restore the previous file.");
}

static Task PatchLockExclusion()
{
    string root = MakeTemp(); using ClientPatchLock first = ClientPatchLock.Acquire(root);
    AssertThrows<IOException>(() => ClientPatchLock.Acquire(root)); return Task.CompletedTask;
}

static async Task DiagnosticsExport()
{
    var diagnostics = new LauncherDiagnostics(); diagnostics.Record(LauncherState.Error, "test", "safe", "ini/test.dat");
    string path = await diagnostics.ExportAsync(MakeTemp(), "1.0.0", Guid.NewGuid(), "test-build", default);
    using var archive = System.IO.Compression.ZipFile.OpenRead(path);
    Assert(archive.Entries.Single().FullName == "launcher-report.json", "Diagnostics package is not bounded.");
    string json; using (var reader = new StreamReader(archive.Entries.Single().Open())) json = await reader.ReadToEndAsync();
    Assert(!json.Contains(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase), "Diagnostics exposed the user profile path.");
}

static async Task SharingDiagnostics()
{
    var diagnostics = new LauncherDiagnostics();
    var native = new IOException("sharing", unchecked((int)0x80070020));
    diagnostics.Record(LauncherState.Scanning, "state", "Scanning", "Conquer.exe");
    diagnostics.RecordFileAccess(LauncherState.Scanning, "sharing-violation-retry", "Conquer.exe",
        "attempt-1-of-5", unchecked((int)0x80070020), 32, "read-only-hash", 1, true, false);
    diagnostics.RecordException(LauncherState.Scanning, "initialization",
        new ClientFileAccessException("read-only-hash", "Conquer.exe", 5, false, true, native),
        "initialization", "filesystem-or-lock");
    string path = await diagnostics.ExportAsync(MakeTemp(), LauncherPolicy.ProductVersion, Guid.NewGuid(), "test-build", default);
    using var archive = System.IO.Compression.ZipFile.OpenRead(path);
    string json; using (var reader = new StreamReader(archive.Entries.Single().Open())) json = await reader.ReadToEndAsync();
    Assert(json.Contains("Conquer.exe", StringComparison.Ordinal) && json.Contains("read-only-hash", StringComparison.Ordinal) &&
        json.Contains("\"NativeError\": 32", StringComparison.Ordinal) && json.Contains("\"AttemptCount\": 5", StringComparison.Ordinal) &&
        json.Contains("\"LockExhausted\": true", StringComparison.Ordinal), "Sharing diagnostics omitted required evidence.");
    Assert(json.Contains("\"LockTransient\": true", StringComparison.Ordinal),
        "Transient sharing diagnostics omitted their classification.");
    Assert(!json.Contains(Path.GetFullPath("Conquer.exe"), StringComparison.Ordinal), "Diagnostics exposed an absolute path.");
}

static async Task CacheWriteDiagnostics()
{
    var diagnostics = new LauncherDiagnostics();
    var native = new IOException("sharing", unchecked((int)0x80070020));
    diagnostics.RecordException(LauncherState.Scanning, "initialization",
        new LauncherFileOperationException("verified-state-cache-write",
            ".revora-launcher/verified-state.json", 1, false, true, native),
        "initialization", "filesystem-or-lock");
    string path = await diagnostics.ExportAsync(MakeTemp(), LauncherPolicy.ProductVersion, Guid.NewGuid(),
        "test-build", default);
    using var archive = System.IO.Compression.ZipFile.OpenRead(path);
    string json;
    using (var reader = new StreamReader(archive.Entries.Single().Open())) json = await reader.ReadToEndAsync();
    Assert(json.Contains("verified-state-cache-write", StringComparison.Ordinal) &&
        json.Contains(".revora-launcher/verified-state.json", StringComparison.Ordinal) &&
        json.Contains("\"NativeError\": 32", StringComparison.Ordinal),
        "Cache-write diagnostics omitted their safe operation evidence.");
    Assert(!json.Contains(Path.GetFullPath(VerifiedStateCache.PathFor(MakeTemp())), StringComparison.Ordinal),
        "Cache-write diagnostics exposed an absolute path.");
}

static Task LauncherReleasePolicy()
{
    var manifest = new LauncherReleaseManifest(1, Guid.NewGuid(), "stable", "1.0.0", DateTimeOffset.UtcNow,
        "Revora.Launcher.exe", [new("Revora.Launcher.exe", 10, new string('a', 64), "https://playrevora.com/launcher/v1/objects/a", true)]);
    LauncherReleaseVerifier.Validate(manifest);
    AssertThrows<InvalidDataException>(() => LauncherReleaseVerifier.Validate(manifest with { EntryPoint = "evil.exe" }));
    return Task.CompletedTask;
}

static async Task LauncherSelfUpdatePreservesClient()
{
    byte[] launcher = "new-launcher"u8.ToArray(), core = "new-core"u8.ToArray();
    LauncherReleaseManifest manifest = LauncherRelease(launcher, core);
    string root = MakeTemp(); string staging = Path.Combine(root, ".revora-launcher", "self-update", manifest.ReleaseId.ToString("N"));
    await File.WriteAllTextAsync(Path.Combine(root, "Revora.Launcher.exe"), "old-launcher");
    await File.WriteAllTextAsync(Path.Combine(root, "Conquer.exe"), "preserve-client");
    using var http = LauncherReleaseHttp(launcher, core);
    var runner = new TestReadinessRunner((entry, working, ready) =>
    {
        Assert(File.ReadAllText(entry) == "new-launcher", "The new launcher was not active at readiness.");
        Assert(File.ReadAllText(Path.Combine(working, "Conquer.exe")) == "preserve-client", "Self-update changed an unmanaged client file.");
        File.WriteAllText(ready, "1.0.0");
    });
    await new BootstrapperEngine(http, runner).ApplyAsync(manifest, root, staging, null, TimeSpan.FromSeconds(1), default);
    Assert(await File.ReadAllTextAsync(Path.Combine(root, "Revora.Launcher.exe")) == "new-launcher", "The launcher update was not retained.");
    Assert(await File.ReadAllTextAsync(Path.Combine(root, "Conquer.exe")) == "preserve-client", "The client file was not preserved.");
}

static async Task LauncherSelfUpdateRollback()
{
    byte[] launcher = "new-launcher"u8.ToArray(), core = "new-core"u8.ToArray();
    LauncherReleaseManifest manifest = LauncherRelease(launcher, core);
    string root = MakeTemp(); string staging = Path.Combine(root, ".revora-launcher", "self-update", manifest.ReleaseId.ToString("N"));
    await File.WriteAllTextAsync(Path.Combine(root, "Revora.Launcher.exe"), "old-launcher");
    await File.WriteAllTextAsync(Path.Combine(root, "Conquer.exe"), "preserve-client");
    using var http = LauncherReleaseHttp(launcher, core);
    var runner = new TestReadinessRunner((_, _, _) => throw new InvalidOperationException("readiness-failed"));
    await AssertThrowsAsync<InvalidOperationException>(() => new BootstrapperEngine(http, runner).ApplyAsync(
        manifest, root, staging, null, TimeSpan.FromMilliseconds(10), default));
    Assert(await File.ReadAllTextAsync(Path.Combine(root, "Revora.Launcher.exe")) == "old-launcher", "Readiness failure did not restore the prior launcher.");
    Assert(!File.Exists(Path.Combine(root, "Revora.Launcher.Core.dll")), "Readiness failure left a new-only launcher file.");
    Assert(await File.ReadAllTextAsync(Path.Combine(root, "Conquer.exe")) == "preserve-client", "Rollback changed an unmanaged client file.");
}

static async Task LauncherSelfUpdateHashFailure()
{
    byte[] launcher = "new-launcher"u8.ToArray(), core = "new-core"u8.ToArray();
    LauncherReleaseManifest manifest = LauncherRelease(launcher, core);
    manifest = manifest with { Files = [manifest.Files[0] with { Sha256 = new string('a', 64) }, manifest.Files[1]] };
    string root = MakeTemp(); string staging = Path.Combine(root, ".revora-launcher", "self-update", manifest.ReleaseId.ToString("N"));
    await File.WriteAllTextAsync(Path.Combine(root, "Revora.Launcher.exe"), "old-launcher");
    using var http = LauncherReleaseHttp(launcher, core);
    await AssertThrowsAsync<CryptographicException>(() => new BootstrapperEngine(http, new TestReadinessRunner((_, _, _) => { })).ApplyAsync(
        manifest, root, staging, null, TimeSpan.FromMilliseconds(10), default));
    Assert(await File.ReadAllTextAsync(Path.Combine(root, "Revora.Launcher.exe")) == "old-launcher", "Hash failure changed the live launcher.");
}

static LauncherReleaseManifest LauncherRelease(byte[] launcher, byte[] core)
{
    string launcherHash = Convert.ToHexStringLower(SHA256.HashData(launcher));
    string coreHash = Convert.ToHexStringLower(SHA256.HashData(core));
    return new(1, Guid.NewGuid(), "test", "1.0.0", DateTimeOffset.UtcNow, "Revora.Launcher.exe",
    [
        new("Revora.Launcher.exe", launcher.Length, launcherHash, "https://playrevora.com/launcher/tests/launcher", true),
        new("Revora.Launcher.Core.dll", core.Length, coreHash, "https://playrevora.com/launcher/tests/core", true),
    ]);
}

static HttpClient LauncherReleaseHttp(byte[] launcher, byte[] core) => new(new TestHandler(request =>
    new(HttpStatusCode.OK) { Content = new ByteArrayContent(request.RequestUri!.AbsolutePath.EndsWith("/launcher", StringComparison.Ordinal) ? launcher : core) }));

static ClientProfile Profile() => new(1, 5165, "playrevora.com", 9958, 9959, "Conquer.exe", [new("Conquer.exe", 3, new string('a', 64), null)], "testing", "test-1", ["blacknull"], 19045, "disabled");

static DesiredStateManifest Desired(string? hash = null, long size = 3)
{
    hash ??= new string('a', 64);
    return new(2, Guid.NewGuid(), "revora-5165-1", "test", DateTimeOffset.UtcNow, "1.0.0", hash,
        "https://playrevora.com/patch/v2/test/release", "https://playrevora.com/patch/v2/origin/release",
        "playrevora.com", 9958, 9959, null, null,
        [new("Conquer.exe", size, hash, "1", $"objects/sha256/{hash[..2]}/{hash}", "none", null, true, true, "replace", true)]);
}

static byte[] SignDesired(DesiredStateManifest manifest, ECDsa key)
{
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(manifest);
    byte[] signature = key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    return JsonSerializer.SerializeToUtf8Bytes(new SignedEnvelope(LauncherPolicy.SigningKeyId, "ECDSA-P256-SHA256", Convert.ToBase64String(payload), Convert.ToBase64String(signature)));
}

static byte[] Sign(ClientProfile profile, ECDsa key)
{
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(profile);
    byte[] signature = key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    return JsonSerializer.SerializeToUtf8Bytes(new SignedEnvelope(LauncherPolicy.SigningKeyId, "ECDSA-P256-SHA256", Convert.ToBase64String(payload), Convert.ToBase64String(signature)));
}

static string MakeTemp()
{
    string path = Path.Combine(Path.GetTempPath(), "revora-launcher-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void AssertThrows<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
static async Task AssertThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
static async Task<T> CaptureAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T error) { return error; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
sealed class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
}
sealed class TestReadinessRunner(Action<string, string, string> run) : ILauncherReadinessRunner
{
    public Task RunAsync(string entryPoint, string workingDirectory, string readinessPath, TimeSpan timeout, CancellationToken token)
    {
        run(entryPoint, workingDirectory, readinessPath);
        return Task.CompletedTask;
    }
}
