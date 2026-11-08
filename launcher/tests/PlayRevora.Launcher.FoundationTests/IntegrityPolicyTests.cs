using System.Runtime.InteropServices;
using PlayRevora.Launcher.Models;
using PlayRevora.Launcher.Services;

public static class IntegrityPolicyTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("normal startup hashes critical set only", CriticalOnly);
        yield return ("modified login mp3 does not block launch", LoginMusicNonCritical);
        yield return ("missing noncritical asset does not block launch", MissingAssetNonCritical);
        yield return ("altered critical executable blocks launch", AlteredExecutable);
        yield return ("DX8 and DX9 critical sets are independent", IndependentProfiles);
        yield return ("mandatory update blocks launch policy", MandatoryUpdate);
        yield return ("optional update permits launch policy", OptionalUpdate);
        yield return ("manual repair detects noncritical mismatch", RepairDetectsNonCritical);
        yield return ("repair plan contains damaged declared files only", RepairPlanIsBounded);
        yield return ("legacy receipt never requests automatic repair scan", LegacyReceiptMigration);
    }

    private static async Task CriticalOnly()
    {
        CountingFs fs = Files(); ClientProfile profile = Profile(ClientProfileId.Dx9);
        ProfileVerification result = await new ProfileVerifier(new(@"C:\client", fs), fs).VerifyAsync(profile, default);
        Equal(ProfileVerificationState.Verified, result.State); Equal(1, fs.HashReads);
    }

    private static async Task LoginMusicNonCritical()
    {
        CountingFs fs = Files(); fs.Hash[@"C:\client\sound\login.mp3"] = new string('F', 64);
        Equal(ProfileVerificationState.Verified, (await new ProfileVerifier(new(@"C:\client", fs), fs)
            .VerifyAsync(Profile(ClientProfileId.Dx9), default)).State);
    }

    private static async Task MissingAssetNonCritical()
    {
        CountingFs fs = Files(); fs.Data.Remove(@"C:\client\sound\login.mp3"); fs.Hash.Remove(@"C:\client\sound\login.mp3");
        Equal(ProfileVerificationState.Verified, (await new ProfileVerifier(new(@"C:\client", fs), fs)
            .VerifyAsync(Profile(ClientProfileId.Dx9), default)).State);
    }

    private static async Task AlteredExecutable()
    {
        CountingFs fs = Files(); fs.Hash[@"C:\client\Env_DX9\Revora-Conquer.exe"] = new string('F', 64);
        Equal(ProfileVerificationState.Unavailable, (await new ProfileVerifier(new(@"C:\client", fs), fs)
            .VerifyAsync(Profile(ClientProfileId.Dx9), default)).State);
    }

    private static async Task IndependentProfiles()
    {
        CountingFs fs = Files(); fs.Hash[@"C:\client\Env_DX9\Revora-Conquer.exe"] = new string('F', 64);
        var verifier = new ProfileVerifier(new(@"C:\client", fs), fs);
        Equal(ProfileVerificationState.Verified, (await verifier.VerifyAsync(Profile(ClientProfileId.Dx8), default)).State);
        Equal(ProfileVerificationState.Unavailable, (await verifier.VerifyAsync(Profile(ClientProfileId.Dx9), default)).State);
    }

    private static Task MandatoryUpdate()
    {
        SignedPatchReference patch = new() { Mandatory = true };
        True(!LaunchGatePolicy.UpdatePermitsLaunch([patch])); return Task.CompletedTask;
    }

    private static Task OptionalUpdate()
    {
        SignedPatchReference patch = new() { Mandatory = false };
        True(LaunchGatePolicy.UpdatePermitsLaunch([patch])); return Task.CompletedTask;
    }

    private static async Task RepairDetectsNonCritical()
    {
        CountingFs fs = Files(); fs.Hash[@"C:\client\sound\login.mp3"] = new string('F', 64);
        RepairCatalogScanResult result = await new RepairCatalogService(new(@"C:\client", fs), fs)
            .ScanAsync(Catalog(), null, default);
        True(result.Findings.Any(item => item.RelativePath.Equals("sound/login.mp3", StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task RepairPlanIsBounded()
    {
        CountingFs fs = Files(); fs.Data.Remove(@"C:\client\sound\login.mp3"); fs.Hash.Remove(@"C:\client\sound\login.mp3");
        fs.Add(@"C:\client\screenshots\owner.png", 99, new string('A', 64));
        RepairCatalogScanResult scan = await new RepairCatalogService(new(@"C:\client", fs), fs).ScanAsync(Catalog(), null, default);
        IReadOnlyList<RequiredProfileFile> plan = RepairCatalogService.PlanDownloads(Catalog(), scan);
        Equal(1, plan.Count); Equal("sound/login.mp3", plan[0].RelativePath.Replace('\\', '/'));
    }

    private static Task LegacyReceiptMigration()
    {
        VerificationContext oldReceipt = new(true, false, false, false, true, false, false, false);
        Equal(VerificationAction.VerifyCriticalLaunchSet, VerificationLifecycle.Decide(oldReceipt)); return Task.CompletedTask;
    }

    private static VerifiedClientManifest Catalog() => new([new("sound/login.mp3", 10, new string('2', 64))],
        [Profile(ClientProfileId.Dx8), Profile(ClientProfileId.Dx9)]);
    private static ClientProfile Profile(ClientProfileId id)
    {
        string env = id == ClientProfileId.Dx8 ? "Env_DX8" : "Env_DX9";
        string name = id == ClientProfileId.Dx8 ? "Conquer.exe" : "Revora-Conquer.exe";
        var executable = new RequiredProfileFile($"{env}/{name}", 1, new string('1', 64));
        return new(id, id.ToString(), $"{env}/{name}", env, ["blacknull"],
            [executable, new($"{env}/optional.dat", 10, new string('2', 64))],
            new(new(10, 0), [Architecture.X64], id == ClientProfileId.Dx8, id == ClientProfileId.Dx9), 1, true)
        { CriticalLaunchFiles = [executable] };
    }

    private static CountingFs Files()
    {
        var fs = new CountingFs();
        foreach (string directory in new[] { @"C:\client", @"C:\client\Env_DX8", @"C:\client\Env_DX9", @"C:\client\sound" }) fs.Directories.Add(directory);
        fs.Add(@"C:\client\Env_DX8\Conquer.exe", 1, new string('1', 64)); fs.Add(@"C:\client\Env_DX9\Revora-Conquer.exe", 1, new string('1', 64));
        fs.Add(@"C:\client\Env_DX8\optional.dat", 10, new string('2', 64)); fs.Add(@"C:\client\Env_DX9\optional.dat", 10, new string('2', 64));
        fs.Add(@"C:\client\sound\login.mp3", 10, new string('2', 64)); return fs;
    }

    private static void True(bool value) { if (!value) throw new Exception("assertion"); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}"); }
}

sealed class CountingFs : IProfileFileSystem
{
    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, long> Data { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Hash { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int HashReads { get; private set; }
    public void Add(string path, long size, string hash) { Data[path] = size; Hash[path] = hash; }
    public string GetFullPath(string path) => Path.GetFullPath(path);
    public bool FileExists(string path) => Data.ContainsKey(GetFullPath(path));
    public bool DirectoryExists(string path) => Directories.Contains(GetFullPath(path));
    public bool IsReparsePoint(string path) => false;
    public long FileLength(string path) => Data[GetFullPath(path)];
    public Task<string> Sha256Async(string path, CancellationToken cancellationToken) { HashReads++; return Task.FromResult(Hash[GetFullPath(path)]); }
}
