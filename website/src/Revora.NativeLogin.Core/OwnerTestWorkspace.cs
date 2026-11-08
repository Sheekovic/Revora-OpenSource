using System.Security.Cryptography;
using System.Text.Json;
using Revora.Launcher.Core;

namespace Revora.NativeLogin.Core;

public sealed record OwnerWorkspaceResult(string Workspace, bool Created, int VerifiedFiles, string ExecutableSha256,
    string PreviousControlledBuildId = "unknown", Guid WorkspaceId = default, bool RollbackAttempted = false, string RollbackResult = "not-required",
    OwnerRootEvidence? RootEvidence = null);

public sealed record OwnerRootEvidence(string SourceKind, Guid? WorkspaceId, string BaselineBuildId,
    string SignedProfileMatch, string SafeRootFingerprint, bool DonorWorkspaceOverlap, bool ExpectedClientRoot);

public sealed class WorkspaceUpgradeException : Exception
{
    public WorkspaceUpgradeException(string phase, string category, string component, string? expected = null, string? actual = null,
        string? baseline = null, string? previous = null, bool rollbackAttempted = false, string rollbackResult = "not-required",
        OwnerRootEvidence? rootEvidence = null, Exception? inner = null)
        : base("The reusable workspace upgrade failed safely.", inner)
    { Phase = phase; Category = category; Component = component; ExpectedSha256 = expected; ActualSha256 = actual;
      MarkerBaselineBuildId = baseline; PreviousControlledBuildId = previous; RollbackAttempted = rollbackAttempted; RollbackResult = rollbackResult; RootEvidence = rootEvidence; }
    public string Phase { get; }
    public string Category { get; }
    public string Component { get; }
    public string? ExpectedSha256 { get; }
    public string? ActualSha256 { get; }
    public string? MarkerBaselineBuildId { get; }
    public string? PreviousControlledBuildId { get; }
    public bool RollbackAttempted { get; }
    public string RollbackResult { get; }
    public OwnerRootEvidence? RootEvidence { get; }
}

public sealed record OwnerWorkspaceUpgradeHooks(Action<string>? BeforeStageComplete = null, Action<string>? BeforeAtomicReplace = null);

/// <summary>Creates a full workspace once, then transactionally replaces only two controlled files.</summary>
public sealed class OwnerTestWorkspace(OwnerWorkspaceUpgradeHooks? hooks = null)
{
    public const string DirectoryName = "Revora Owner Test Workspace";
    private const string ControlDirectory = ".revora-owner-test";
    private const string MarkerName = "workspace-marker.json";
    private const string CanonicalExe = "e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c";
    private const string RsaExe = "902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f";
    private const string Test4cExe = "8040023d599116c5db3d91eb3eb97065ec15521ed8b6a3a7ac6c3a28247ed310";
    private const string Test4dExe = "842204b71208b88646797177f0a3a685bac9868d2e28ed8b1d85f7ce6dea0a83";
    private const string Test4eExe = "7aa96e49cb30ded6db3944b5fd4a7f36282cc26036e1152e7b1f1802c8891df4";
    private const string Test4fExe = "b4d4977ef50980b656397d0e339bd0d34f350917663b6c9af393586a49cdd6df";
    private const string Test4gExe = "206bcee90388633930dc4171a4cbc23e2c5b140ae55d25c7220af0f35a0c89a9";
    private const string Test4hExe = "cf4525da3f43f73bfb1eecc6252f3b9c3a570db1963bbe7b2a7a1ec0a6abfc68";
    private const string Test4iExe = "6bb9c92f285cada26208fc625a12c45c7185e99482032a51c5c1de5a019618dc";
    private const string Test4jExe = "8f2d5d837cf894bfb362c900370f065d4b3c916dba7724af2a3184f3d8b8d4f2";
    private static readonly string[] ControlledFiles = ["Conquer.exe", "Server.dat"];
    private static readonly Dictionary<string, string> KnownExecutableStates = new(StringComparer.OrdinalIgnoreCase)
    {
        [CanonicalExe] = "doctor-reconstruction-5165-test-2c", [RsaExe] = "native-login-5165-rsa-testing-intermediate",
        [Test4cExe] = "native-login-5165-owner-test-4c", [Test4dExe] = "native-login-5165-owner-test-4d",
        [Test4eExe] = "native-login-5165-owner-test-4e",
        [Test4fExe] = "native-login-5165-owner-test-4f",
        [Test4gExe] = "native-login-5165-owner-test-4g",
        [Test4hExe] = "native-login-5165-owner-test-4h",
        [Test4iExe] = "native-login-5165-owner-test-4i",
        [Test4jExe] = "native-login-5165-owner-test-4j",
    };

    public async Task<OwnerWorkspaceResult> PrepareAsync(string verifiedCleanClient, ClientProfile baseline, ClientProfile target,
        string patchMetadataPath, string serverDatPath, CancellationToken token, IProgress<PreparationProgress>? progress = null)
        => await PrepareChainedAsync(verifiedCleanClient, baseline, target, [patchMetadataPath], serverDatPath, token, progress);

    public async Task<OwnerWorkspaceResult> PrepareChainedAsync(string verifiedCleanClient, ClientProfile baseline, ClientProfile target,
        IReadOnlyList<string> patchMetadataPaths, string serverDatPath, CancellationToken token, IProgress<PreparationProgress>? progress = null)
    {
        if (patchMetadataPaths.Count == 0) throw Fail("patch-chain-validation", "patch-chain-empty", "Conquer.exe");
        string source = Path.GetFullPath(verifiedCleanClient);
        string parent = Path.GetDirectoryName(source) ?? throw Fail("path-validation", "source-parent-unavailable", "client-root");
        string workspace = Path.Combine(parent, DirectoryName);
        OwnerRootEvidence rootEvidence = await InspectRootAsync(source, workspace, baseline, token);
        if (rootEvidence.SourceKind == "owned-workspace")
            throw Fail("path-validation", "owned-workspace-selected-as-donor", "client-root", baseline: rootEvidence.BaselineBuildId, rootEvidence: rootEvidence);
        ValidateDistinctSafeRoots(source, workspace, rootEvidence);
        bool created = false;
        Marker marker;
        if (!Directory.Exists(workspace))
        {
            VerificationResult donor = await new ClientVerifier().VerifyAsync(source, baseline, token);
            if (!donor.IsValid) throw Fail("donor-validation", "donor-profile-mismatch", "signed-baseline", baseline: baseline.BuildId, rootEvidence: rootEvidence);
            Directory.CreateDirectory(workspace);
            try
            {
                int complete = 0;
                foreach (ClientFile file in baseline.Files)
                {
                    string input = SafePath.ResolveContained(source, file.Path), output = SafePath.ResolveContained(workspace, file.Path);
                    SafePath.RejectExistingReparsePoints(source, input); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    File.Copy(input, output, false); progress?.Report(new("initial-workspace-copy", ++complete, baseline.Files.Count, file.Path));
                }
                marker = CreateMarker(workspace, baseline); created = true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            { throw Fail("workspace-creation", "initial-copy-failed", "workspace", inner: error); }
        }
        else marker = ValidateMarker(workspace, baseline);

        int verified = await VerifyImmutableAsync(workspace, baseline, marker, token);
        (string previousBuild, Dictionary<string, string> previousHashes) = InspectControlled(workspace, baseline, target, marker);
        VerificationResult sourceResult = await new ClientVerifier().VerifyAsync(source, baseline, token);
        if (!sourceResult.IsValid) throw Fail("donor-validation", "donor-profile-mismatch", "signed-baseline", baseline: marker.BaselineBuildId, previous: previousBuild, rootEvidence: rootEvidence);
        return Upgrade(source, workspace, baseline, target, patchMetadataPaths, serverDatPath, marker, previousBuild, previousHashes, created, verified, rootEvidence);
    }

    private OwnerWorkspaceResult Upgrade(string source, string workspace, ClientProfile baseline, ClientProfile target,
        IReadOnlyList<string> patchMetadataPaths, string serverDatPath, Marker marker, string previousBuild,
        Dictionary<string, string> previousHashes, bool created, int verified, OwnerRootEvidence rootEvidence)
    {
        string control = Path.Combine(workspace, ControlDirectory), staging = Path.Combine(control, "upgrade-staging");
        PrepareOwnedDirectory(staging, marker.WorkspaceId);
        try
        {
            ClientFile canonical = baseline.Files.Single(file => file.Path == "Conquer.exe");
            string donorExe = SafePath.ResolveContained(source, "Conquer.exe"), stagedExe = Path.Combine(staging, "Conquer.exe");
            VerifyFile(donorExe, canonical, "donor-validation", marker, previousBuild); File.Copy(donorExe, stagedExe, true);
            foreach (string patchMetadataPath in patchMetadataPaths)
                ApplyPatch(stagedExe, patchMetadataPath, marker, previousBuild);
            string stagedServer = Path.Combine(staging, "Server.dat"); File.Copy(serverDatPath, stagedServer, true);
            VerifyFile(stagedExe, target.Files.Single(file => file.Path == "Conquer.exe"), "staging-validation", marker, previousBuild);
            VerifyFile(stagedServer, target.Files.Single(file => file.Path == "Server.dat"), "staging-validation", marker, previousBuild);
            hooks?.BeforeStageComplete?.Invoke("controlled-files");
        }
        catch (WorkspaceUpgradeException) { throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or CryptographicException)
        { throw Fail("staging", "staging-failed", "controlled-files", baseline: marker.BaselineBuildId, previous: previousBuild, inner: error); }

        string previous = Path.Combine(control, "previous-controlled"); PrepareOwnedDirectory(previous, marker.WorkspaceId);
        foreach (string relative in ControlledFiles) File.Copy(SafePath.ResolveContained(workspace, relative), Path.Combine(previous, relative), true);
        bool started = false;
        try
        {
            foreach (string relative in ControlledFiles)
            {
                hooks?.BeforeAtomicReplace?.Invoke(relative); started = true;
                AtomicWrite(File.ReadAllBytes(Path.Combine(staging, relative)), SafePath.ResolveContained(workspace, relative));
            }
            foreach (ClientFile expected in target.Files.Where(file => ControlledFiles.Contains(file.Path, StringComparer.Ordinal)))
                VerifyFile(SafePath.ResolveContained(workspace, expected.Path), expected, "final-validation", marker, previousBuild);
            Marker updated = marker with { SchemaVersion = 2, ControlledBuildId = target.BuildId,
                ControlledHashes = target.Files.Where(file => ControlledFiles.Contains(file.Path, StringComparer.Ordinal)).ToDictionary(file => file.Path, file => file.Sha256), Checksum = "" };
            WriteMarker(control, updated);
            return new(workspace, created, verified, target.Files.Single(file => file.Path == target.Executable).Sha256, previousBuild, marker.WorkspaceId, RootEvidence: rootEvidence);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or CryptographicException)
        {
            string rollback = "not-required";
            if (started) try
            {
                foreach (string relative in ControlledFiles) AtomicWrite(File.ReadAllBytes(Path.Combine(previous, relative)), SafePath.ResolveContained(workspace, relative));
                foreach ((string relative, string hash) in previousHashes) RequireHash(File.ReadAllBytes(SafePath.ResolveContained(workspace, relative)), hash, "rollback", relative, marker, previousBuild);
                rollback = "succeeded";
            }
            catch { rollback = "failed"; }
            throw Fail("atomic-replacement", "controlled-replacement-failed", "controlled-files", baseline: marker.BaselineBuildId,
                previous: previousBuild, rollbackAttempted: started, rollbackResult: rollback, inner: error);
        }
    }

    private static Marker CreateMarker(string workspace, ClientProfile baseline)
    {
        string control = Path.Combine(workspace, ControlDirectory), rollback = Path.Combine(control, "rollback"); Directory.CreateDirectory(rollback);
        foreach (string relative in ControlledFiles) File.Copy(SafePath.ResolveContained(workspace, relative), Path.Combine(rollback, relative), false);
        Marker marker = new(2, baseline.BuildId, Guid.NewGuid(), DateTimeOffset.UtcNow, baseline.BuildId,
            baseline.Files.Where(file => ControlledFiles.Contains(file.Path, StringComparer.Ordinal)).ToDictionary(file => file.Path, file => file.Sha256), "");
        WriteMarker(control, marker); return marker;
    }

    private static Marker ValidateMarker(string workspace, ClientProfile baseline)
    {
        string control = Path.Combine(workspace, ControlDirectory); RejectReparseTree(workspace, control);
        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(control, MarkerName)));
            int schema = doc.RootElement.GetProperty("SchemaVersion").GetInt32();
            if (schema == 1)
            {
                MarkerV1 old = doc.RootElement.Deserialize<MarkerV1>() ?? throw new JsonException();
                string expected = Checksum(old with { Checksum = "" });
                if (old.BaselineBuildId != baseline.BuildId || !HexEqual(old.Checksum, expected))
                    throw Fail("marker-validation", "marker-checksum-mismatch", MarkerName, expected, SafeHash(old.Checksum), old.BaselineBuildId);
                return new(1, old.BaselineBuildId, old.WorkspaceId, old.CreatedUtc, null, null, old.Checksum);
            }
            Marker marker = doc.RootElement.Deserialize<Marker>() ?? throw new JsonException(); string current = Checksum(marker with { Checksum = "" });
            if (schema != 2 || marker.BaselineBuildId != baseline.BuildId || !HexEqual(marker.Checksum, current))
                throw Fail("marker-validation", "marker-checksum-mismatch", MarkerName, current, SafeHash(marker.Checksum), marker.BaselineBuildId, marker.ControlledBuildId);
            return marker;
        }
        catch (WorkspaceUpgradeException) { throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException)
        { throw Fail("marker-validation", "marker-unreadable", MarkerName, baseline: baseline.BuildId, inner: error); }
    }

    private static async Task<int> VerifyImmutableAsync(string workspace, ClientProfile baseline, Marker marker, CancellationToken token)
    {
        int count = 0;
        foreach (ClientFile file in baseline.Files.Where(file => !ControlledFiles.Contains(file.Path, StringComparer.Ordinal)))
        {
            token.ThrowIfCancellationRequested(); string path = SafePath.ResolveContained(workspace, file.Path); SafePath.RejectExistingReparsePoints(workspace, path);
            if (!File.Exists(path)) throw Fail("immutable-validation", "immutable-file-missing", file.Path, file.Sha256, null, marker.BaselineBuildId, marker.ControlledBuildId);
            string actual = Hash(path);
            if (new FileInfo(path).Length != file.Size || !HexEqual(actual, file.Sha256)) throw Fail("immutable-validation", "immutable-hash-mismatch", file.Path, file.Sha256, actual, marker.BaselineBuildId, marker.ControlledBuildId);
            count++;
        }
        return count;
    }

    private static (string, Dictionary<string, string>) InspectControlled(string workspace, ClientProfile baseline, ClientProfile target, Marker marker)
    {
        Dictionary<string, string> hashes = ControlledFiles.ToDictionary(relative => relative, relative => Hash(SafePath.ResolveContained(workspace, relative)), StringComparer.Ordinal);
        string? build = ClassifyKnownExecutableHash(hashes["Conquer.exe"]);
        if (build is null && HexEqual(hashes["Conquer.exe"], baseline.Files.Single(file => file.Path == "Conquer.exe").Sha256)) build = baseline.BuildId;
        if (build is null && HexEqual(hashes["Conquer.exe"], target.Files.Single(file => file.Path == "Conquer.exe").Sha256)) build = target.BuildId;
        if (build is null) throw Fail("controlled-state", "unknown-active-hash", "Conquer.exe", string.Join(",", KnownExecutableStates.Keys), hashes["Conquer.exe"], marker.BaselineBuildId, marker.ControlledBuildId);
        string baselineServer = baseline.Files.Single(file => file.Path == "Server.dat").Sha256, targetServer = target.Files.Single(file => file.Path == "Server.dat").Sha256;
        if (!HexEqual(hashes["Server.dat"], baselineServer) && !HexEqual(hashes["Server.dat"], targetServer)) throw Fail("controlled-state", "unknown-active-hash", "Server.dat", $"{baselineServer},{targetServer}", hashes["Server.dat"], marker.BaselineBuildId, build);
        return (build, hashes);
    }

    public static string? ClassifyKnownExecutableHash(string sha256) => KnownExecutableStates.GetValueOrDefault(sha256);

    public static async Task<OwnerRootEvidence> InspectRootAsync(string sourcePath, string expectedWorkspace, ClientProfile baseline, CancellationToken token)
    {
        string source = Path.GetFullPath(sourcePath), workspace = Path.GetFullPath(expectedWorkspace);
        bool overlap = IsSameOrContained(source, workspace) || IsSameOrContained(workspace, source);
        Guid? workspaceId = null; string markerBaseline = baseline.BuildId;
        string markerPath = Path.Combine(source, ControlDirectory, MarkerName);
        bool owned = Path.GetFileName(source).Equals(DirectoryName, StringComparison.OrdinalIgnoreCase) && File.Exists(markerPath);
        if (owned)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(markerPath, token));
                workspaceId = document.RootElement.GetProperty("WorkspaceId").GetGuid();
                markerBaseline = document.RootElement.GetProperty("BaselineBuildId").GetString() ?? "unknown";
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException)
            { throw Fail("marker-validation", "marker-unreadable", MarkerName, baseline: baseline.BuildId, inner: error); }
        }
        VerificationResult? verification = owned ? null : await new ClientVerifier().VerifyAsync(source, baseline, token);
        bool matches = verification?.IsValid == true;
        string kind = owned ? "owned-workspace" : matches ? "verified-donor" : "unknown";
        string versionHash = SafeFileFingerprint(source, "version.dat"), executableHash = SafeFileFingerprint(source, baseline.Executable);
        string material = $"{kind}|{markerBaseline}|{workspaceId?.ToString("D") ?? "none"}|{versionHash}|{executableHash}";
        string fingerprint = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
        return new(kind, workspaceId, markerBaseline, matches ? "matched" : owned ? "not-applicable-owned-workspace" : "not-matched",
            fingerprint, overlap, matches && !owned);
    }

    private static void ApplyPatch(string path, string metadataPath, Marker marker, string previous)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(metadataPath)); JsonElement root = doc.RootElement; byte[] bytes = File.ReadAllBytes(path);
        RequireHash(bytes, root.GetProperty("inputSha256").GetString()!, "patch-input-validation", "Conquer.exe", marker, previous);
        foreach (JsonElement change in root.GetProperty("changes").EnumerateArray())
        {
            int offset = change.GetProperty("Offset").GetInt32(); byte[] original = Convert.FromHexString(change.GetProperty("OriginalHex").GetString()!); byte[] replacement = Convert.FromHexString(change.GetProperty("ReplacementHex").GetString()!);
            if (offset < 0 || original.Length != replacement.Length || offset > bytes.Length - original.Length || !bytes.AsSpan(offset, original.Length).SequenceEqual(original)) throw Fail("patch-byte-validation", "patch-original-byte-mismatch", "Conquer.exe", baseline: marker.BaselineBuildId, previous: previous);
            replacement.CopyTo(bytes, offset);
        }
        RequireHash(bytes, root.GetProperty("outputSha256").GetString()!, "patch-output-validation", "Conquer.exe", marker, previous); File.WriteAllBytes(path, bytes);
    }

    private static void PrepareOwnedDirectory(string directory, Guid workspaceId)
    {
        Directory.CreateDirectory(directory); if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw Fail("staging", "staging-reparse-point", Path.GetFileName(directory));
        string owner = Path.Combine(directory, "owner.json");
        if (Directory.EnumerateFileSystemEntries(directory).Any())
        {
            if (!File.Exists(owner) || File.ReadAllText(owner) != workspaceId.ToString("D")) throw Fail("staging", "unowned-staging-preserved", Path.GetFileName(directory));
            foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
            if (Directory.EnumerateDirectories(directory).Any()) throw Fail("staging", "unexpected-staging-directory", Path.GetFileName(directory));
        }
        File.WriteAllText(owner, workspaceId.ToString("D"));
    }

    private static void VerifyFile(string path, ClientFile expected, string phase, Marker marker, string? previous)
    { if (!File.Exists(path)) throw Fail(phase, "file-missing", expected.Path, expected.Sha256, null, marker.BaselineBuildId, previous); string actual = Hash(path); if (new FileInfo(path).Length != expected.Size || !HexEqual(actual, expected.Sha256)) throw Fail(phase, "file-hash-mismatch", expected.Path, expected.Sha256, actual, marker.BaselineBuildId, previous); }
    private static void RequireHash(byte[] bytes, string expected, string phase, string component, Marker marker, string? previous)
    { string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); if (!HexEqual(actual, expected)) throw Fail(phase, "file-hash-mismatch", component, expected, actual, marker.BaselineBuildId, previous); }
    private static void WriteMarker(string control, Marker marker) { Marker signed = marker with { Checksum = Checksum(marker with { Checksum = "" }) }; string path = Path.Combine(control, MarkerName), partial = path + ".revora-partial"; File.WriteAllText(partial, JsonSerializer.Serialize(signed)); File.Move(partial, path, true); }
    private static void AtomicWrite(byte[] bytes, string destination) { string partial = destination + ".revora-partial"; if (File.Exists(partial)) File.Delete(partial); File.WriteAllBytes(partial, bytes); File.Move(partial, destination, true); }
    private static string Hash(string path) { using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static string Checksum<T>(T marker) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(marker))).ToLowerInvariant();
    private static bool HexEqual(string left, string right) { try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right)); } catch (FormatException) { return false; } }
    private static string? SafeHash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit) ? value.ToLowerInvariant() : null;
    private static string SafeFileFingerprint(string root, string relative)
    {
        try
        {
            string path = SafePath.ResolveContained(root, relative);
            return File.Exists(path) ? Hash(path) : "missing";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return "unavailable";
        }
    }
    private static bool IsSameOrContained(string parent, string candidate)
    { string relation = Path.GetRelativePath(parent, candidate); return relation == "." || (!relation.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && relation != ".."); }
    private static void ValidateDistinctSafeRoots(string source, string workspace, OwnerRootEvidence rootEvidence) { if (!Directory.Exists(source) || Path.GetFileName(workspace) != DirectoryName || workspace.StartsWith("\\\\", StringComparison.Ordinal)) throw Fail("path-validation", "unsupported-workspace-path", "workspace", baseline: rootEvidence.BaselineBuildId, rootEvidence: rootEvidence); string relation = Path.GetRelativePath(source, workspace); if (relation == "." || (!relation.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && relation != "..")) throw Fail("path-validation", "workspace-inside-donor", "workspace", baseline: rootEvidence.BaselineBuildId, rootEvidence: rootEvidence); if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0 || (Directory.Exists(workspace) && (File.GetAttributes(workspace) & FileAttributes.ReparsePoint) != 0)) throw Fail("path-validation", "reparse-point-root", "workspace", baseline: rootEvidence.BaselineBuildId, rootEvidence: rootEvidence); }
    private static void RejectReparseTree(string root, string control) { SafePath.RejectExistingReparsePoints(root, control); if (Directory.EnumerateFileSystemEntries(control, "*", SearchOption.AllDirectories).Any(entry => (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)) throw Fail("marker-validation", "control-reparse-point", "workspace-control"); }
    private static WorkspaceUpgradeException Fail(string phase, string category, string component, string? expected = null, string? actual = null, string? baseline = null, string? previous = null, bool rollbackAttempted = false, string rollbackResult = "not-required", OwnerRootEvidence? rootEvidence = null, Exception? inner = null) => new(phase, category, component, expected, actual, baseline, previous, rollbackAttempted, rollbackResult, rootEvidence, inner);
    private sealed record MarkerV1(int SchemaVersion, string BaselineBuildId, Guid WorkspaceId, DateTimeOffset CreatedUtc, string Checksum);
    private sealed record Marker(int SchemaVersion, string BaselineBuildId, Guid WorkspaceId, DateTimeOffset CreatedUtc, string? ControlledBuildId, Dictionary<string, string>? ControlledHashes, string Checksum);
}
