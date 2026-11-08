using System.IO.Compression;
using System.Security.Cryptography;
using PlayRevora.Launcher.Models;
using PlayRevora.Launcher.Services;

public static class UpdateFoundationTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("strict RevoraVersion.ini round trip", VersionRoundTrip);
        yield return ("version duplicate key rejected", () => VersionReject("[Revora]\nSchemaVersion=1\nSchemaVersion=1\nClientPatch=7630\nContentVersion=1.0.0\nChannel=owner-test\nLastSuccessfulPatch=m1"));
        yield return ("version duplicate section rejected", () => VersionReject("[Revora]\n[Revora]\nSchemaVersion=1\nClientPatch=7630\nContentVersion=1.0.0\nChannel=owner-test\nLastSuccessfulPatch=m1"));
        yield return ("version malformed value rejected", () => VersionReject("[Revora]\nSchemaVersion=1\nClientPatch=x\nContentVersion=1.0.0\nChannel=owner-test\nLastSuccessfulPatch=m1"));
        yield return ("multi patch chain", MultiPatchChain);
        yield return ("missing intermediate patch rejected", MissingIntermediate);
        yield return ("wrong source version rejected", WrongSource);
        yield return ("wrong patch channel rejected", WrongChannel);
        yield return ("manifest receipt change invalidates cache", ReceiptManifestChange);
        yield return ("safe archive extraction", SafeArchive);
        yield return ("archive traversal rejected", ArchiveTraversal);
        yield return ("archive duplicate case rejected", ArchiveDuplicateCase);
        yield return ("transaction commit", TransactionCommit);
        yield return ("post-install verification failure rolls back", TransactionRollback);
        yield return ("write failure rolls back", TransactionWriteFailure);
        yield return ("interrupted installation recovery", InterruptedRecovery);
        yield return ("signed file modification fails closed", SignedModification);
        yield return ("legacy receipt uses critical verification", CachedLifecycle);
        yield return ("manual command runs repair catalog", ManualLifecycle);
        yield return ("patch install uses critical verification", PatchLifecycle);
        yield return ("lightweight failure uses critical verification", LightweightLifecycle);
        yield return ("pending install requires recovery", PendingLifecycle);
        yield return ("unknown version schema rejected", () => VersionReject("[Revora]\nSchemaVersion=2\nClientPatch=7630\nContentVersion=1.0.0\nChannel=owner-test\nLastSuccessfulPatch=m1"));
        yield return ("unknown version key rejected", () => VersionReject("[Revora]\nSchemaVersion=1\nClientPatch=7630\nContentVersion=1.0.0\nChannel=owner-test\nLastSuccessfulPatch=m1\nExtra=x"));
        yield return ("corrupted package hash rejected", CorruptPackage);
        yield return ("wrong package source rejected", WrongPackageSource);
        yield return ("revoked package rejected", RevokedPackage);
        yield return ("reserved archive path rejected", ReservedArchivePath);
        yield return ("stale local version invalidates receipt", StaleVersion);
        yield return ("offline uses valid cached signed index", OfflineCachedIndex);
        yield return ("first run offline fails closed explicitly", FirstRunOffline);
    }

    private static Task VersionRoundTrip()
    {
        RevoraClientVersion result = RevoraVersionStore.Parse("[Revora]\nSchemaVersion=1\nClientPatch=7630\nContentVersion=1.0.0\nChannel=owner-test\nLastSuccessfulPatch=owner-test-1\n");
        Equal(7630, result.ClientPatch); Equal("1.0.0", result.ContentVersion); return Task.CompletedTask;
    }

    private static Task VersionReject(string value) { Throws<InvalidDataException>(() => RevoraVersionStore.Parse(value)); return Task.CompletedTask; }

    private static Task MultiPatchChain()
    {
        SignedPatchIndex index = Index(("p1", "1.0.0", "1.1.0"), ("p2", "1.1.0", "2.0.0"));
        IReadOnlyList<SignedPatchReference> chain = new PatchChainResolver().Resolve(index, "1.0.0");
        Equal(2, chain.Count); Equal("p2", chain[1].PatchId); return Task.CompletedTask;
    }

    private static Task MissingIntermediate()
    {
        Throws<InvalidDataException>(() => new PatchChainResolver().Resolve(Index(("p2", "1.1.0", "2.0.0")), "1.0.0")); return Task.CompletedTask;
    }

    private static Task WrongSource()
    {
        SignedPatchIndex index = Index(("p1", "2.0.0", "3.0.0"));
        Throws<InvalidDataException>(() => new PatchChainResolver().Resolve(index, "1.0.0")); return Task.CompletedTask;
    }

    private static Task WrongChannel()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256); SignedPatchIndex index = Index(("p1", "1.0.0", "1.1.0"));
        index.Channel = "production"; index.Signature.KeyId = "test";
        index.Signature.ValueBase64 = Convert.ToBase64String(key.SignData(UpdateCanonicalizer.Index(index), HashAlgorithmName.SHA256));
        Throws<InvalidDataException>(() => new SignedUpdateMetadataVerifier(new TestUpdateClock()).VerifyIndex(index,
            new("test", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())), "owner-test")); return Task.CompletedTask;
    }

    private static Task ReceiptManifestChange()
    {
        RevoraClientVersion version = new(1, 7630, "1.0.0", "owner-test", "m1");
        VerificationReceipt receipt = new() { SchemaVersion = 1, ClientRootIdentity = VerificationReceiptStore.ClientRootIdentity(@"C:\client"), ManifestSha256 = new string('A', 64), BuildChannel = "owner-test", ClientPatch = 7630, ContentVersion = "1.0.0", ManifestId = "m1", CompletedAtUtc = DateTimeOffset.UtcNow };
        True(VerificationReceiptStore.Matches(receipt, @"C:\client", new string('A', 64), "owner-test", version));
        True(!VerificationReceiptStore.Matches(receipt, @"C:\client", new string('B', 64), "owner-test", version)); return Task.CompletedTask;
    }

    private static async Task SafeArchive()
    {
        using TempTree tree = new(); string archive = tree.Path("patch.zip"); byte[] bytes = "new-content"u8.ToArray(); CreateZip(archive, ("payload/a.dat", bytes));
        string hash = Convert.ToHexString(SHA256.HashData(bytes)); await new SafePatchExtractor().ExtractAsync(archive, tree.Path("staging"), [new() { ArchivePath = "payload/a.dat", TargetPath = "data/a.dat", Size = bytes.Length, Sha256 = hash }], null, default);
        True(File.Exists(tree.Path("staging/payload/a.dat")));
    }

    private static async Task ArchiveTraversal()
    {
        using TempTree tree = new(); string archive = tree.Path("patch.zip"); CreateZip(archive, ("../escape.dat", [1]));
        await ThrowsAsync<InvalidDataException>(() => new SafePatchExtractor().ExtractAsync(archive, tree.Path("staging"), [new() { ArchivePath = "../escape.dat", TargetPath = "data/a", Size = 1, Sha256 = Convert.ToHexString(SHA256.HashData([1])) }], null, default));
    }

    private static Task ArchiveDuplicateCase()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256); SignedPatchReference reference = Ref("p1", "1.0.0", "1.1.0");
        SignedPatchPackage package = Package(reference); package.Files = [FileMeta("A.dat", "data/A.dat"), FileMeta("a.dat", "data/a.dat")];
        package.Signature.KeyId = "test"; package.Signature.ValueBase64 = Convert.ToBase64String(key.SignData(UpdateCanonicalizer.Package(package), HashAlgorithmName.SHA256));
        Throws<InvalidDataException>(() => new SignedUpdateMetadataVerifier(new TestUpdateClock()).VerifyPackage(package,
            new("test", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())), "owner-test", reference)); return Task.CompletedTask;
    }

    private static Task TransactionCommit() => Transaction(false, false);
    private static Task TransactionRollback() => Transaction(true, false);
    private static Task TransactionWriteFailure() => Transaction(false, true);

    private static async Task Transaction(bool verificationFails, bool commitFails)
    {
        using TempTree tree = new(); Directory.CreateDirectory(tree.Path("client/data")); Directory.CreateDirectory(tree.Path("staged/payload"));
        await File.WriteAllTextAsync(tree.Path("client/data/a.dat"), "old"); await File.WriteAllTextAsync(tree.Path("staged/payload/a.dat"), "new");
        SignedPatchPackage package = new() { PatchId = "p1", Files = [new() { ArchivePath = "payload/a.dat", TargetPath = "data/a.dat" }] };
        var installer = new TransactionalPatchInstaller(tree.Path("client"), "RevoraUpdates", new NoOpInstallationGuard(), new TestUpdateClock());
        if (verificationFails || commitFails)
        {
            await ThrowsAsync<IOException>(() => installer.InstallAsync(package, tree.Path("staged"),
                _ => verificationFails ? Task.FromException(new IOException("verify")) : Task.CompletedTask,
                _ => commitFails ? Task.FromException(new IOException("disk")) : Task.CompletedTask, default));
            Equal("old", await File.ReadAllTextAsync(tree.Path("client/data/a.dat"))); True(!installer.RecoveryRequired);
        }
        else
        {
            await installer.InstallAsync(package, tree.Path("staged"), _ => Task.CompletedTask, _ => Task.CompletedTask, default);
            Equal("new", await File.ReadAllTextAsync(tree.Path("client/data/a.dat"))); True(!installer.RecoveryRequired);
        }
    }

    private static async Task InterruptedRecovery()
    {
        using TempTree tree = new(); string client = tree.Path("client"); Directory.CreateDirectory(tree.Path("client/data")); Directory.CreateDirectory(tree.Path("client/RevoraUpdates/Rollback/t/data")); Directory.CreateDirectory(tree.Path("client/RevoraUpdates/State"));
        await File.WriteAllTextAsync(tree.Path("client/data/a.dat"), "new"); await File.WriteAllTextAsync(tree.Path("client/RevoraUpdates/Rollback/t/data/a.dat"), "old");
        PendingInstallState pending = new() { TransactionId = "t", PatchId = "p1", Phase = "Installing", Entries = [new() { TargetPath = "data/a.dat", BackupPath = "Rollback/t/data/a.dat", PreviouslyExisted = true, Installed = true }] };
        await JsonFiles.WriteAtomicAsync(tree.Path("client/RevoraUpdates/State/pending-install.json"), pending, default);
        var installer = new TransactionalPatchInstaller(client, "RevoraUpdates", new NoOpInstallationGuard(), new TestUpdateClock()); True(installer.RecoveryRequired); await installer.RecoverAsync(default);
        Equal("old", await File.ReadAllTextAsync(tree.Path("client/data/a.dat"))); True(!installer.RecoveryRequired);
    }

    private static async Task SignedModification()
    {
        using TempTree tree = new(); Directory.CreateDirectory(tree.Path("client/data")); await File.WriteAllTextAsync(tree.Path("client/data/a"), "changed");
        var root = new ValidatedClientRoot(tree.Path("client"), new PhysicalProfileFileSystem());
        await ThrowsAsync<ClientFileVerificationException>(() => new ClientFileIntegrityVerifier(root, new PhysicalProfileFileSystem()).VerifyAsync([new("data/a", 3, Convert.ToHexString(SHA256.HashData("old"u8.ToArray())))], default));
    }

    private static Task CachedLifecycle() { Equal(VerificationAction.VerifyCriticalLaunchSet, VerificationLifecycle.Decide(Context())); return Task.CompletedTask; }
    private static Task ManualLifecycle() { Equal(VerificationAction.RunRepairCatalog, VerificationLifecycle.Decide(Context() with { ExplicitRequest = true })); return Task.CompletedTask; }
    private static Task PatchLifecycle() { Equal(VerificationAction.VerifyCriticalLaunchSet, VerificationLifecycle.Decide(Context() with { PatchInstalled = true })); return Task.CompletedTask; }
    private static Task LightweightLifecycle() { Equal(VerificationAction.VerifyCriticalLaunchSet, VerificationLifecycle.Decide(Context() with { LightweightReady = false })); return Task.CompletedTask; }
    private static Task PendingLifecycle() { Equal(VerificationAction.RequireRecovery, VerificationLifecycle.Decide(Context() with { PendingInstall = true })); return Task.CompletedTask; }
    private static VerificationContext Context() => new(false, false, true, true, true, false, false, false);
    private static async Task CorruptPackage(){using TempTree tree=new();await File.WriteAllTextAsync(tree.Path("p.zip.partial"),"corrupt");True(!await FileIntegrity.MatchesAsync(tree.Path("p.zip.partial"),7,new string('A',64),default));}
    private static Task WrongPackageSource(){SignedPatchReference reference=Ref("p1","1.0.0","1.1.0");SignedPatchPackage package=Package(reference);package.SourceVersion="0.9.0";Throws<InvalidDataException>(()=>new SignedUpdateMetadataVerifier(new TestUpdateClock()).VerifyPackage(package,new("x",Convert.ToBase64String(ECDsa.Create().ExportSubjectPublicKeyInfo())),"owner-test",reference));return Task.CompletedTask;}
    private static Task RevokedPackage(){SignedPatchReference reference=Ref("p1","1.0.0","1.1.0");SignedPatchPackage package=Package(reference);package.Revoked=true;Throws<InvalidDataException>(()=>new SignedUpdateMetadataVerifier(new TestUpdateClock()).VerifyPackage(package,new("x",Convert.ToBase64String(ECDsa.Create().ExportSubjectPublicKeyInfo())),"owner-test",reference));return Task.CompletedTask;}
    private static Task ReservedArchivePath(){Throws<InvalidDataException>(()=>SignedUpdateMetadataVerifier.ValidateArchivePath("data/CON.txt"));return Task.CompletedTask;}
    private static Task StaleVersion(){RevoraClientVersion current=new(1,7630,"1.1.0","owner-test","m2");VerificationReceipt receipt=new(){SchemaVersion=1,ClientRootIdentity=VerificationReceiptStore.ClientRootIdentity(@"C:\client"),ManifestSha256=new string('A',64),BuildChannel="owner-test",ClientPatch=7630,ContentVersion="1.0.0",ManifestId="m1",CompletedAtUtc=DateTimeOffset.UtcNow};True(!VerificationReceiptStore.Matches(receipt,@"C:\client",new string('A',64),"owner-test",current));return Task.CompletedTask;}

    private static async Task OfflineCachedIndex()
    {
        using TempTree tree = new(); using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await new RevoraVersionStore(tree.Path("client")).WriteAtomicAsync(new(1, 7630, "1.0.0", "owner-test", "baseline"), default);
        SignedPatchIndex index = SignedEmptyIndex(key); var download = new FakeDownload(index);
        LauncherConfig config = OfflineConfig(tree, key); var service = new UpdateService(config, download, new TestUpdateClock());
        Equal(0, (await service.DiscoverAsync(default)).Count); download.Fail = true;
        Equal(0, (await service.DiscoverAsync(default)).Count);
        True(File.Exists(tree.Path("client/RevoraUpdates/State/patch-index.cached.signed.json")));
    }

    private static async Task FirstRunOffline()
    {
        using TempTree tree = new(); using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await new RevoraVersionStore(tree.Path("client")).WriteAtomicAsync(new(1, 7630, "1.0.0", "owner-test", "baseline"), default);
        var download = new FakeDownload(SignedEmptyIndex(key)) { Fail = true };
        await ThrowsAsync<InvalidOperationException>(() => new UpdateService(OfflineConfig(tree, key), download, new TestUpdateClock()).DiscoverAsync(default));
    }

    private static LauncherConfig OfflineConfig(TempTree tree, ECDsa key) => new()
    {
        GameRoot = tree.Path("client"), BuildChannel = "owner-test", ManifestKeyId = "owner-test-key",
        ManifestPublicKeySpkiBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), UpdateBaseUrl = "https://invalid.example/updates/"
    };

    private static SignedPatchIndex SignedEmptyIndex(ECDsa key)
    {
        SignedPatchIndex index = new() { IndexId = "owner-test-current", Channel = "owner-test", IssuedAtUtc = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), ExpiresAtUtc = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), Signature = new() { KeyId = "owner-test-key" } };
        index.Signature.ValueBase64 = Convert.ToBase64String(key.SignData(UpdateCanonicalizer.Index(index), HashAlgorithmName.SHA256)); return index;
    }

    private static SignedPatchIndex Index(params (string Id, string Source, string Target)[] patches) => new() { Patches = patches.Select(item => Ref(item.Id, item.Source, item.Target)).ToList() };
    private static SignedPatchReference Ref(string id, string source, string target) => new() { PatchId = id, SourceVersion = source, TargetVersion = target, MetadataPath = $"metadata/{id}.json" };
    private static SignedPatchPackage Package(SignedPatchReference reference) => new() { PatchId = reference.PatchId, Channel = "owner-test", SourceVersion = reference.SourceVersion, TargetVersion = reference.TargetVersion, IssuedAtUtc = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), ExpiresAtUtc = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), ArchivePath = "packages/p.zip", ArchiveSha256 = new string('A', 64), TargetManifestId = "m2", TargetManifestSha256 = new string('B', 64) };
    private static PatchArchiveFile FileMeta(string archive, string target) => new() { ArchivePath = archive, TargetPath = target, Size = 1, Sha256 = new string('C', 64) };
    private static void CreateZip(string path, params (string Name, byte[] Content)[] files) { using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create); foreach (var file in files) { ZipArchiveEntry entry = zip.CreateEntry(file.Name); using Stream stream = entry.Open(); stream.Write(file.Content); } }
    private static void True(bool value) { if (!value) throw new Exception("assertion"); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}"); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("expected " + typeof(T).Name); }
    private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception("expected " + typeof(T).Name); }
}

file sealed class FakeDownload(SignedPatchIndex index) : IDownloadService
{
    public bool Fail { get; set; }
    public Task<T> GetJsonAsync<T>(Uri uri, CancellationToken cancellationToken)
    {
        if (Fail) throw new HttpRequestException("offline");
        return Task.FromResult((T)(object)index);
    }
    public Task DownloadAsync(Uri uri, string destination, long maximumBytes, CancellationToken cancellationToken, IProgress<OperationProgress>? progress = null) => throw new NotSupportedException();
    public Task DownloadResumableAsync(Uri uri, string temporaryDestination, long expectedBytes, CancellationToken cancellationToken, IProgress<OperationProgress>? progress = null) => throw new NotSupportedException();
}

file sealed class TestUpdateClock : IClock { public DateTimeOffset UtcNow => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); public Task Delay(TimeSpan duration, CancellationToken cancellationToken) => Task.CompletedTask; }
sealed class TempTree : IDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "revora-update-tests", Guid.NewGuid().ToString("N"));
    public TempTree() => Directory.CreateDirectory(root);
    public string Path(string relative) => System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
