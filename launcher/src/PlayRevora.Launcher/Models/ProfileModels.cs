using System.Text.Json.Serialization;
using System.Runtime.InteropServices;

namespace PlayRevora.Launcher.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClientProfileId { Dx8, Dx9 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProfileSelectionMode { Automatic, DirectX9, DirectX8 }

public sealed record ProfileRequirements(
    Version MinimumWindowsVersion,
    Architecture[] SupportedArchitectures,
    bool RequiresDirectX8,
    bool RequiresDirectX9);

public sealed record RequiredProfileFile(string RelativePath, long Size, string Sha256, bool Critical = true);

public sealed record ClientProfile(
    ClientProfileId Id,
    string DisplayName,
    string ExecutableRelativePath,
    string WorkingDirectoryRelativePath,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<RequiredProfileFile> RequiredFiles,
    ProfileRequirements Requirements,
    int Priority,
    bool AutomaticFallbackAllowed)
{
    public IReadOnlyList<RequiredProfileFile> CriticalLaunchFiles { get; init; } = [];
}

public sealed record HardwareCapabilities(
    bool IsWindows,
    Version WindowsVersion,
    Architecture Architecture,
    bool DirectX8Available,
    bool DirectX9Available,
    string GpuVendorCategory,
    bool IsConclusive,
    string Summary);

public enum ProfileVerificationState { NotVerified, Verified, Unavailable }

public enum ClientFileMismatchCategory
{
    Missing,
    SizeMismatch,
    HashMismatch,
    SizeAndHashMismatch,
    AccessFailure
}

public sealed record ClientFileVerificationFailure(string RelativePath, ClientFileMismatchCategory Category);

public enum ProfileReadinessReason
{
    ManifestNotProvisioned,
    InvalidSignature,
    ExpiredManifest,
    SelectedProfileAbsent,
    RequiredFileMissing,
    HashMismatch,
    VerificationRequired,
    RecoveryRequired,
    UpdateCheckPending,
    MandatoryUpdateRequired,
    UnsupportedHardware,
    ProfileReady
}

public sealed record ProfileVerification(ClientProfile Profile, ProfileVerificationState State, string UserMessage,
    ClientFileVerificationFailure? Failure = null);

public sealed record ProfileSelectionResult(
    ClientProfile? Profile,
    string Reason,
    bool RequiresUserSelection,
    ClientProfile? ConfirmableFallback = null,
    ProfileReadinessReason ReasonCode = ProfileReadinessReason.SelectedProfileAbsent);

public sealed record ProfileReadiness(
    ProfileReadinessReason ReasonCode,
    string Heading,
    string Message,
    bool LaunchAuthorized);

public sealed class LauncherPreferences
{
    public ProfileSelectionMode ProfileMode { get; set; } = ProfileSelectionMode.Automatic;
}

public sealed class SignedClientManifest
{
    public int SchemaVersion { get; set; }
    public string ManifestVersion { get; set; } = "";
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string MinimumLauncherVersion { get; set; } = "";
    public RollbackMetadata Rollback { get; set; } = new();
    public List<RequiredProfileFile> RootRequiredFiles { get; set; } = [];
    public List<ManifestProfile> Profiles { get; set; } = [];
    public ManifestSignature Signature { get; set; } = new();
}

public sealed class RollbackMetadata
{
    public string PreviousManifestVersion { get; set; } = "";
    public string PreviousManifestSha256 { get; set; } = "";
}

public sealed class ManifestProfile
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public List<string> Arguments { get; set; } = [];
    public List<RequiredProfileFile> RequiredFiles { get; set; } = [];
    public List<RequiredProfileFile> CriticalLaunchFiles { get; set; } = [];
    public string MinimumWindowsVersion { get; set; } = "10.0";
    public List<string> SupportedArchitectures { get; set; } = [];
    public bool RequiresDirectX8 { get; set; }
    public bool RequiresDirectX9 { get; set; }
    public int Priority { get; set; }
    public bool AutomaticFallbackAllowed { get; set; }
}

public sealed class ManifestSignature
{
    public string Algorithm { get; set; } = "ECDSA_P256_SHA256";
    public string KeyId { get; set; } = "";
    public string ValueBase64 { get; set; } = "";
}

public sealed record ManifestTrust(string KeyId, string PublicKeySpkiBase64);
public sealed record VerifiedClientManifest(IReadOnlyList<RequiredProfileFile> RootRequiredFiles,
    IReadOnlyList<ClientProfile> Profiles);

public sealed record LaunchRequest(string ExecutablePath, string WorkingDirectory, IReadOnlyList<string> Arguments);
public sealed record LaunchResult(bool Started, bool ImmediateExit, int? ExitCode, string UserMessage);
public sealed record ObservedProcess(int ProcessId, int ParentProcessId, string ExecutablePath, bool HasExited, int? ExitCode);
