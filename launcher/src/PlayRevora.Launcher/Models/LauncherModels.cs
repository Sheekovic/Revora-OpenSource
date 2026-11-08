using System.Text.Json.Serialization;

namespace PlayRevora.Launcher.Models;

public sealed class LauncherConfig
{
    public string UpdateBaseUrl { get; set; } = "https://playrevora.com/updates/";
    public string GameRoot { get; set; } = @"C:\Games\PlayRevora";
    public string GameExecutable { get; set; } = "play.exe";
    public string VersionFile { get; set; } = "RevoraVersion.ini";
    public string ChannelManifest { get; set; } = "channel.json";
    public string MasterManifest { get; set; } = "master_manifest.json";
    public string BaselinePath { get; set; } = "baseline/";
    public string ProxyPayloadPath { get; set; } = @"LauncherAssets\d3d9.dll";
    public string? ProxySha256 { get; set; }
    public string RenderBackendLabel { get; set; } = "Native D3D9";
    public string StatusEndpoint { get; set; } = "https://playrevora.com/api/status";
    public string SignedClientManifest { get; set; } = "client-manifest.signed.json";
    public string ManifestKeyId { get; set; } = "";
    public string ManifestPublicKeySpkiBase64 { get; set; } = "";
    public string PreferencesFile { get; set; } = "launcher.preferences.json";
    public string BuildChannel { get; set; } = "production";
    public string VerificationReceiptFile { get; set; } = @"RevoraUpdates\State\verification-receipt.json";
    public string PendingInstallFile { get; set; } = @"RevoraUpdates\State\pending-install.json";
    public string SignedPatchIndex { get; set; } = "patch-index.signed.json";
    public string CachedPatchIndex { get; set; } = @"RevoraUpdates\State\patch-index.cached.signed.json";
    public int UpdateIndexTimeoutSeconds { get; set; } = 10;
    public int BaselineClientPatch { get; set; } = 7630;
    public string BaselineContentVersion { get; set; } = "1.0.0";
    public bool Dx9BridgeOwnerTestEnabled { get; set; }
    public string Dx9BridgeLoaderPath { get; set; } = @"LauncherAssets\RevoraNativeLoginLoader.exe";
    public string Dx9BridgeDllPath { get; set; } = @"LauncherAssets\RevoraNativeLogin.dll";
}

public sealed class LocalVersion
{
    public string Version { get; set; } = "0.0.0";
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UnixEpoch;
}

public sealed record RevoraClientVersion(int SchemaVersion, int ClientPatch, string ContentVersion,
    string Channel, string LastSuccessfulPatch);

public sealed class VerificationReceipt
{
    public int SchemaVersion { get; set; } = 1;
    public string ClientRootIdentity { get; set; } = "";
    public string ManifestSha256 { get; set; } = "";
    public string BuildChannel { get; set; } = "";
    public int ClientPatch { get; set; }
    public string ContentVersion { get; set; } = "";
    public string ManifestId { get; set; } = "";
    public DateTimeOffset CompletedAtUtc { get; set; }
}

public enum LauncherLifecycleState
{
    CheckingForUpdates, UpdateAvailable, MandatoryUpdateRequired, Downloading, VerifyingPackage, PreparingUpdate,
    Installing, VerifyingClient, Ready, CriticalFileProblem, RepairRecommended, RepairingClient, RecoveryRequired
}

public enum VerificationAction { VerifyCriticalLaunchSet, RunRepairCatalog, RequireRecovery }
public sealed record VerificationContext(bool FirstUse, bool PatchInstalled, bool PreviousSucceeded,
    bool ReceiptCompatible, bool LightweightReady, bool PendingInstall, bool ExplicitRequest, bool IntegrityProblem);

public sealed class SignedPatchIndex
{
    public int SchemaVersion { get; set; } = 1;
    public string IndexId { get; set; } = "";
    public string Channel { get; set; } = "";
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public List<SignedPatchReference> Patches { get; set; } = [];
    public ManifestSignature Signature { get; set; } = new();
}

public sealed class SignedPatchReference
{
    public string PatchId { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string TargetVersion { get; set; } = "";
    public string MetadataPath { get; set; } = "";
    public bool Mandatory { get; set; }
}

public sealed class SignedPatchPackage
{
    public int SchemaVersion { get; set; } = 1;
    public string PatchId { get; set; } = "";
    public string Channel { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public string TargetVersion { get; set; } = "";
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool Revoked { get; set; }
    public string ArchivePath { get; set; } = "";
    public long ArchiveSize { get; set; }
    public string ArchiveSha256 { get; set; } = "";
    public string TargetManifestId { get; set; } = "";
    public string TargetManifestPath { get; set; } = "client-manifest.signed.json";
    public string TargetManifestSha256 { get; set; } = "";
    public List<PatchArchiveFile> Files { get; set; } = [];
    public List<string> DeletedFiles { get; set; } = [];
    public ManifestSignature Signature { get; set; } = new();
}

public sealed class PatchArchiveFile
{
    public string ArchivePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class PendingInstallState
{
    public int SchemaVersion { get; set; } = 1;
    public string TransactionId { get; set; } = "";
    public string PatchId { get; set; } = "";
    public string Phase { get; set; } = "";
    public DateTimeOffset StartedAtUtc { get; set; }
    public List<RollbackJournalEntry> Entries { get; set; } = [];
}

public sealed class RollbackJournalEntry
{
    public string TargetPath { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public bool PreviouslyExisted { get; set; }
    public bool Installed { get; set; }
    public bool ManagedState { get; set; }
}

public sealed class UpdateChannelManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string LatestVersion { get; set; } = "0.0.0";
    public List<ReleaseReference> Releases { get; set; } = [];
}

public sealed class ReleaseReference
{
    public string Version { get; set; } = "";
    public string Manifest { get; set; } = "";
}

public sealed class PatchManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "";
    public string? RequiresVersion { get; set; }
    public List<ManifestFile> Files { get; set; } = [];
}

public sealed class MasterManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "";
    public List<ManifestFile> Files { get; set; } = [];
}

public sealed class ManifestFile
{
    public string Path { get; set; } = "";
    public string Source { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class GraphicsSettings
{
    public bool EnhancedGraphics { get; set; } = true;
    public string DisplayMode { get; set; } = "Borderless Windowed";
    public string Resolution { get; set; } = "Auto / Native";
}

public sealed record Announcement(string Title, string Body, string Tag, string Image);
public readonly record struct OperationProgress(string Message, double Percent,
    double CurrentPercent = 0, string RateText = "SHA-256 verification");
