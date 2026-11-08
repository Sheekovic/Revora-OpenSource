using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PlayRevora.Launcher.Models;

namespace PlayRevora.Launcher.Services;

public static class ClientMutablePathPolicy
{
    private static readonly HashSet<string> ExactPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "launcher.json", "launcher.preferences.json", "client-manifest.signed.json", "RevoraVersion.ini",
        "PlayRevora.Launcher.exe", "PlayRevora.Launcher.dll", "PlayRevora.Launcher.pdb",
        "PlayRevora.Launcher.deps.json", "PlayRevora.Launcher.runtimeconfig.json",
        "regkey.dat", "Env_DX9/regkey.dat",
        "Env_DX9/revora_render.ini",
        "Env_DX9/revora_d3d9.jsonl",
        "ini/GameSetup.Ini", "ini/PlaySetUP.ini"
    };

    private static readonly string[] DirectoryPrefixes = ["logs/", "debug/", "RevoraUpdates/", "Env_DX8/debug/", "Env_DX9/debug/"];

    public static bool IsExcluded(string relativePath)
    {
        ValidatedClientRoot.ValidateRelative(relativePath);
        string normalized = relativePath.Replace('\\', '/');
        return ExactPaths.Contains(normalized)
            || normalized.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
            || DirectoryPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static List<RequiredProfileFile> KeepImmutable(IEnumerable<RequiredProfileFile> files) =>
        files.Where(file => !IsExcluded(file.RelativePath)).ToList();

    public static void RemoveExcluded(SignedClientManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.RootRequiredFiles = KeepImmutable(manifest.RootRequiredFiles);
        foreach (ManifestProfile profile in manifest.Profiles)
        {
            profile.RequiredFiles = KeepImmutable(profile.RequiredFiles);
            profile.CriticalLaunchFiles = KeepImmutable(profile.CriticalLaunchFiles);
        }
    }
}

public interface IProfileFileSystem
{
    string GetFullPath(string path);
    bool FileExists(string path);
    bool DirectoryExists(string path);
    bool IsReparsePoint(string path);
    long FileLength(string path);
    Task<string> Sha256Async(string path, CancellationToken cancellationToken);
}

public sealed class PhysicalProfileFileSystem : IProfileFileSystem
{
    public string GetFullPath(string path) => Path.GetFullPath(path);
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool IsReparsePoint(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    public long FileLength(string path) => new FileInfo(path).Length;
    public async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}

public sealed class ValidatedClientRoot
{
    private readonly IProfileFileSystem files;
    public ValidatedClientRoot(string root, IProfileFileSystem files)
    {
        this.files = files;
        Root = files.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!files.DirectoryExists(Root)) throw new DirectoryNotFoundException("The client root is unavailable.");
        RejectReparseChain(Root);
    }

    public string Root { get; }

    public string Resolve(string relativePath, bool requireExisting = false)
    {
        ValidateRelative(relativePath);
        string candidate = files.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = Root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A client path escapes the validated client root.");
        RejectReparseChain(candidate);
        if (requireExisting && !files.FileExists(candidate) && !files.DirectoryExists(candidate))
            throw new FileNotFoundException("A required client file is missing.");
        return candidate;
    }

    public static void ValidateRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.StartsWith("\\\\", StringComparison.Ordinal)
            || path.Contains(':'))
            throw new InvalidDataException("Client paths must be non-empty relative paths.");
        string[] segments = path.Replace('\\', '/').Split('/');
        if (segments.Any(segment => segment is "" or "." or ".."))
            throw new InvalidDataException("Client paths may not contain traversal segments.");
    }

    private void RejectReparseChain(string candidate)
    {
        string current = Root;
        if (files.DirectoryExists(current) && files.IsReparsePoint(current))
            throw new InvalidDataException("The client root may not be a reparse point.");
        string relative = Path.GetRelativePath(Root, candidate);
        if (relative == ".") return;
        foreach (string part in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            if ((files.FileExists(current) || files.DirectoryExists(current)) && files.IsReparsePoint(current))
                throw new InvalidDataException("A client path crosses a reparse point.");
        }
    }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    Task Delay(TimeSpan duration, CancellationToken cancellationToken);
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public Task Delay(TimeSpan duration, CancellationToken cancellationToken) => Task.Delay(duration, cancellationToken);
}

public interface IHardwareCapabilityProvider { HardwareCapabilities Detect(); }

public sealed class WindowsHardwareCapabilityProvider : IHardwareCapabilityProvider
{
    public HardwareCapabilities Detect()
    {
        bool windows = OperatingSystem.IsWindows();
        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        bool dx8 = windows && File.Exists(Path.Combine(system, "ddraw.dll"));
        bool dx9 = windows && File.Exists(Path.Combine(system, "d3d9.dll"));
        bool conclusive = windows && (dx8 || dx9);
        return new(windows, Environment.OSVersion.Version, RuntimeInformation.OSArchitecture,
            dx8, dx9, "Not collected", conclusive,
            conclusive ? "Windows and DirectX runtime availability were detected." : "Automatic capability detection is inconclusive.");
    }
}

public static class ProfileContracts
{
    public static void Enforce(ClientProfile profile)
    {
        string exe = profile.ExecutableRelativePath.Replace('\\', '/');
        string cwd = profile.WorkingDirectoryRelativePath.Replace('\\', '/');
        if (profile.Id == ClientProfileId.Dx8)
        {
            if (!exe.Equals("Env_DX8/Conquer.exe", StringComparison.OrdinalIgnoreCase)
                || !cwd.Equals("Env_DX8", StringComparison.OrdinalIgnoreCase)
                || profile.Arguments.Count != 1
                || profile.Arguments[0] != "blacknull")
                throw new InvalidDataException("The signed DX8 launch contract is not approved.");
        }
        else if (!exe.Equals("Env_DX9/Revora-Conquer.exe", StringComparison.OrdinalIgnoreCase)
                 || !cwd.Equals("Env_DX9", StringComparison.OrdinalIgnoreCase)
                 || profile.Arguments.Count != 1
                 || profile.Arguments[0] != "blacknull")
            throw new InvalidDataException("The signed DX9 launch contract is not approved.");

        string environment = profile.Id == ClientProfileId.Dx8 ? "Env_DX8/" : "Env_DX9/";
        if (!profile.RequiredFiles.Any(file => file.Critical && file.RelativePath.Replace('\\', '/')
                .Equals(exe, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The executable must be a critical signed profile file.");
        foreach (RequiredProfileFile file in profile.RequiredFiles)
            if (!file.RelativePath.Replace('\\', '/').StartsWith(environment, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Profile files must remain inside their immutable environment tree.");
        if (profile.CriticalLaunchFiles.Count > 0 && !profile.CriticalLaunchFiles.Any(file =>
                file.RelativePath.Replace('\\', '/').Equals(exe, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The approved executable must be in the critical launch set.");
    }
}

public sealed class ProfileVerifier(ValidatedClientRoot root, IProfileFileSystem files)
{
    public async Task<ProfileVerification> VerifyAsync(ClientProfile profile, CancellationToken cancellationToken)
    {
        try
        {
            ProfileContracts.Enforce(profile);
            string workingDirectory = root.Resolve(profile.WorkingDirectoryRelativePath);
            if (!files.DirectoryExists(workingDirectory))
                return Unavailable(profile, "The profile working directory is missing.",
                    profile.WorkingDirectoryRelativePath, ClientFileMismatchCategory.Missing);
            IReadOnlyList<RequiredProfileFile> critical = profile.CriticalLaunchFiles.Count == 0
                ? profile.RequiredFiles.Where(file => file.RelativePath.Equals(profile.ExecutableRelativePath,
                    StringComparison.OrdinalIgnoreCase)).ToArray()
                : profile.CriticalLaunchFiles;
            await new ClientFileIntegrityVerifier(root, files).VerifyAsync(critical, cancellationToken);
            return new(profile, ProfileVerificationState.Verified, "Critical launch files verified and ready.");
        }
        catch (ClientFileVerificationException exception)
        {
            return Unavailable(profile, exception.Category == ClientFileMismatchCategory.Missing
                ? "A required profile dependency is missing." : "A critical profile file failed verification.",
                exception.RelativePath, exception.Category);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unavailable(profile, exception.Message);
        }
    }

    private static ProfileVerification Unavailable(ClientProfile profile, string message) =>
        new(profile, ProfileVerificationState.Unavailable, message);

    private static ProfileVerification Unavailable(ClientProfile profile, string message, string relativePath,
        ClientFileMismatchCategory category) => new(profile, ProfileVerificationState.Unavailable, message,
            new(relativePath.Replace('\\', '/'), category));
}

public sealed class ClientFileIntegrityVerifier(ValidatedClientRoot root, IProfileFileSystem files)
{
    public async Task VerifyAsync(IEnumerable<RequiredProfileFile> requiredFiles, CancellationToken cancellationToken)
    {
        foreach (RequiredProfileFile required in requiredFiles)
        {
            string path = root.Resolve(required.RelativePath);
            if (!files.FileExists(path))
                throw new ClientFileVerificationException(required.RelativePath, ClientFileMismatchCategory.Missing);
            if (required.Size < 0 || required.Sha256.Length != 64 || !required.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("Client integrity metadata is invalid.");
            try
            {
                long actualSize = files.FileLength(path);
                string actualHash = await files.Sha256Async(path, cancellationToken);
                bool sizeMismatch = actualSize != required.Size;
                bool hashMismatch = !string.Equals(actualHash, required.Sha256, StringComparison.OrdinalIgnoreCase);
                if (sizeMismatch || hashMismatch)
                    throw new ClientFileVerificationException(required.RelativePath,
                        sizeMismatch && hashMismatch ? ClientFileMismatchCategory.SizeAndHashMismatch
                        : sizeMismatch ? ClientFileMismatchCategory.SizeMismatch : ClientFileMismatchCategory.HashMismatch);
            }
            catch (ClientFileVerificationException) { throw; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new ClientFileVerificationException(required.RelativePath,
                    ClientFileMismatchCategory.AccessFailure, exception);
            }
        }
    }
}

public sealed class ProfileSelectionService
{
    public ProfileSelectionResult Select(ProfileSelectionMode mode, HardwareCapabilities hardware,
        IReadOnlyDictionary<ClientProfileId, ProfileVerification> profiles)
    {
        if (mode != ProfileSelectionMode.Automatic)
        {
            ClientProfileId requested = mode == ProfileSelectionMode.DirectX9 ? ClientProfileId.Dx9 : ClientProfileId.Dx8;
            if (!profiles.TryGetValue(requested, out ProfileVerification? verification)
                || verification.State != ProfileVerificationState.Verified)
                return new(null, $"The selected {Display(requested)} profile is unavailable.", true);
            if (!Supports(verification.Profile, hardware))
                return new(null, $"The selected {Display(requested)} profile is not supported on this system.", true);
            return new(verification.Profile, $"Using your explicit {Display(requested)} preference.", false);
        }

        if (!hardware.IsConclusive)
            return new(null, "Hardware detection was inconclusive. Choose DirectX 8 or DirectX 9 explicitly.", true);
        ClientProfile? dx9 = Valid(ClientProfileId.Dx9, hardware, profiles);
        if (dx9 is not null) return new(dx9, "DirectX 9 is verified and supported; it is preferred automatically.", false);
        ClientProfile? dx8 = Valid(ClientProfileId.Dx8, hardware, profiles);
        if (dx8 is not null) return new(dx8, "DirectX 9 is unavailable; DirectX 8 is eligible.", false);
        return new(null, "No verified client profile is available.", true);
    }

    private static ClientProfile? Valid(ClientProfileId id, HardwareCapabilities hardware,
        IReadOnlyDictionary<ClientProfileId, ProfileVerification> profiles) =>
        profiles.TryGetValue(id, out ProfileVerification? item) && item.State == ProfileVerificationState.Verified
        && Supports(item.Profile, hardware) ? item.Profile : null;

    private static bool Supports(ClientProfile profile, HardwareCapabilities hardware) => hardware.IsWindows
        && hardware.WindowsVersion >= profile.Requirements.MinimumWindowsVersion
        && profile.Requirements.SupportedArchitectures.Contains(hardware.Architecture)
        && (!profile.Requirements.RequiresDirectX8 || hardware.DirectX8Available)
        && (!profile.Requirements.RequiresDirectX9 || hardware.DirectX9Available);

    private static string Display(ClientProfileId id) => id == ClientProfileId.Dx9 ? "DirectX 9" : "DirectX 8";
}

public interface ILaunchedProcess : IDisposable
{
    int ProcessId { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task WaitForExitAsync(CancellationToken cancellationToken);
}

public interface IProcessLauncher { ILaunchedProcess Start(LaunchRequest request); }
public interface ITerminableLaunchedProcess : ILaunchedProcess { void Terminate(); }
public interface ISuspendedLaunchedProcess : ILaunchedProcess
{
    void Resume();
    void Terminate();
}
public interface ISuspendedProcessLauncher : IProcessLauncher
{
    ISuspendedLaunchedProcess StartSuspended(LaunchRequest request);
}

public interface IProcessTreeObserver
{
    IReadOnlyList<ObservedProcess> Snapshot();
}

public interface IDiagnosticSink { void Record(string eventName, IReadOnlyDictionary<string, string> fields); }

public sealed class RedactedFileDiagnosticSink(string path) : IDiagnosticSink
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
        { "profile", "result", "exitCode", "reason", "category", "relativePath", "lifetimeMs", "classification", "stage",
          "official_updater_bypassed", "play_exe_observed", "autop_exe_observed" };
    private readonly object gate = new();
    public void Record(string eventName, IReadOnlyDictionary<string, string> fields)
    {
        if (!eventName.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-')) return;
        string detail = string.Join(' ', fields.Where(item => AllowedFields.Contains(item.Key))
            .Select(item => $"{item.Key}={(item.Key == "relativePath" ? SanitizeRelativePath(item.Value) : Sanitize(item.Value))}"));
        lock (gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {eventName} {detail}{Environment.NewLine}", Encoding.UTF8);
        }
    }
    private static string Sanitize(string value) => new(value.Take(80)
        .Select(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' ? character : '_').ToArray());
    private static string SanitizeRelativePath(string value) => new(value.Replace('\\', '/').Take(240)
        .Select(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' or '/' ? character : '_').ToArray());
}

public sealed class ClientFileVerificationException(string relativePath, ClientFileMismatchCategory category,
    Exception? innerException = null) : IOException("A required client file failed verification.", innerException)
{
    public string RelativePath { get; } = relativePath.Replace('\\', '/');
    public ClientFileMismatchCategory Category { get; } = category;
}

public interface ILauncherPreferencesStore
{
    Task<LauncherPreferences> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(LauncherPreferences preferences, CancellationToken cancellationToken);
}

public sealed class JsonLauncherPreferencesStore(string path) : ILauncherPreferencesStore
{
    public Task<LauncherPreferences> LoadAsync(CancellationToken cancellationToken) =>
        JsonFiles.ReadAsync<LauncherPreferences>(path, cancellationToken);
    public Task SaveAsync(LauncherPreferences preferences, CancellationToken cancellationToken) =>
        JsonFiles.WriteAtomicAsync(path, preferences, cancellationToken);
}

public sealed class WindowsProcessLauncher : ISuspendedProcessLauncher
{
    public ILaunchedProcess Start(LaunchRequest request)
    {
        var info = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = false,
            Verb = ""
        };
        foreach (string argument in request.Arguments) info.ArgumentList.Add(argument);
        info.Environment["REVORA_LAUNCHER"] = "1";
        return new WindowsLaunchedProcess(Process.Start(info)
            ?? throw new InvalidOperationException("Windows did not create the game process."));
    }

    public ISuspendedLaunchedProcess StartSuspended(LaunchRequest request)
    {
        string commandLine = string.Join(" ", new[] { Quote(request.ExecutablePath) }
            .Concat(request.Arguments.Select(Quote)));
        STARTUPINFO startup = new() { Size = (uint)Marshal.SizeOf<STARTUPINFO>() };
        if (!CreateProcessW(request.ExecutablePath, commandLine, 0, 0, false, 0x00000004,
                0, request.WorkingDirectory, ref startup, out PROCESS_INFORMATION created))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        CloseHandle(created.ProcessHandle);
        return new WindowsSuspendedProcess(Process.GetProcessById((int)created.ProcessId), created.ThreadHandle);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private sealed class WindowsLaunchedProcess(Process process) : ITerminableLaunchedProcess
    {
        public int ProcessId => process.Id;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);
        public void Terminate() { if (!process.HasExited) process.Kill(entireProcessTree: false); }
        public void Dispose() => process.Dispose();
    }

    private sealed class WindowsSuspendedProcess(Process process, nint primaryThread) : ISuspendedLaunchedProcess
    {
        private bool resumed;
        public int ProcessId => process.Id;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);
        public void Resume()
        {
            if (resumed || ResumeThread(primaryThread) == uint.MaxValue)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            resumed = true;
        }
        public void Terminate() { if (!process.HasExited) TerminateProcess(process.Handle, 1); }
        public void Dispose() { CloseHandle(primaryThread); process.Dispose(); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO { public uint Size; public nint Reserved, Desktop, Title; public uint X,Y,XSize,YSize,XCountChars,YCountChars,FillAttribute,Flags; public ushort ShowWindow,Reserved2; public nint Reserved2Pointer,StdInput,StdOutput,StdError; }
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION { public nint ProcessHandle, ThreadHandle; public uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(string applicationName, string commandLine, nint processAttributes,
        nint threadAttributes, bool inheritHandles, uint creationFlags, nint environment, string currentDirectory,
        ref STARTUPINFO startupInfo, out PROCESS_INFORMATION processInformation);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(nint thread);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(nint process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(nint handle);
}

public sealed class WindowsProcessTreeObserver : IProcessTreeObserver
{
    public IReadOnlyList<ObservedProcess> Snapshot()
    {
        var items = new List<ObservedProcess>();
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                string path = process.MainModule?.FileName ?? "";
                if (path.Length == 0) continue;
                items.Add(new(process.Id, ParentProcess(process.Id), Path.GetFullPath(path), process.HasExited,
                    process.HasExited ? process.ExitCode : null));
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            { }
            finally { process.Dispose(); }
        }
        return items;
    }

    private static int ParentProcess(int processId)
    {
        nint snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == -1) return 0;
        try
        {
            PROCESSENTRY32 entry = new() { Size = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Process32First(snapshot, ref entry)) return 0;
            do { if (entry.ProcessId == processId) return (int)entry.ParentProcessId; }
            while (Process32Next(snapshot, ref entry));
            return 0;
        }
        finally { CloseHandle(snapshot); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint Size, Usage, ProcessId; public nint DefaultHeapId; public uint ModuleId, Threads, ParentProcessId;
        public int BasePriority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeFile;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32First(nint snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32Next(nint snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
}

public sealed class GameProcessCoordinator(ValidatedClientRoot root, IProfileFileSystem files,
    IProcessLauncher launcher, IClock clock, IDiagnosticSink? diagnostics = null, IProcessTreeObserver? processTree = null,
    Action<string>? status = null, IDx9BridgeLoader? bridge = null)
{
    private readonly object gate = new();
    private ILaunchedProcess? active;
    private readonly IProcessTreeObserver processTree = processTree ?? new WindowsProcessTreeObserver();

    public async Task<LaunchResult> LaunchAsync(ClientProfile profile, CancellationToken cancellationToken,
        bool allowOfficialFallback = false, ClientProfile? approvedFallback = null)
    {
        ProfileContracts.Enforce(profile);
        if (profile.Id == ClientProfileId.Dx9 && bridge is not null)
        {
            BridgeLoadResult preparation = bridge.Prepare();
            if (!preparation.Loaded) return new(false, false, null, preparation.UserMessage);
        }
        string expectedExecutable = root.Resolve(profile.ExecutableRelativePath, true);
        string dx8Executable = root.Resolve("Env_DX8/Conquer.exe");
        string dx9Executable = root.Resolve("Env_DX9/Revora-Conquer.exe");
        string executable = expectedExecutable;
        string workingDirectory = root.Resolve(profile.WorkingDirectoryRelativePath, true);
        if (!files.FileExists(executable) || !files.DirectoryExists(workingDirectory))
            return new(false, false, null, "A critical startup component is missing.");
        if (profile.Id == ClientProfileId.Dx9 && bridge is not null)
        {
            const long approvedSize = 18594824;
            const string approvedHash = "4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE";
            string actualHash = await files.Sha256Async(executable, cancellationToken);
            if (files.FileLength(executable) != approvedSize || !actualHash.Equals(approvedHash, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics?.Record("launch_failed", new Dictionary<string, string>
                    { ["profile"] = "Dx9", ["reason"] = "approved_hash_mismatch" });
                return new(false, false, null, "The selected DX9 executable is not the approved Patch-7630 build.");
            }
        }
        lock (gate)
        {
            if (active is not null && !active.HasExited)
            {
                diagnostics?.Record("launch_rejected", new Dictionary<string, string> { ["profile"] = profile.Id.ToString(), ["reason"] = "duplicate" });
                return new(false, false, null, "The game is already running.");
            }
            if (processTree.Snapshot().Any(item => !item.HasExited &&
                (Path.GetFullPath(item.ExecutablePath).Equals(dx8Executable, StringComparison.OrdinalIgnoreCase)
                 || Path.GetFullPath(item.ExecutablePath).Equals(dx9Executable, StringComparison.OrdinalIgnoreCase))))
                return new(false, false, null, "An approved game client is already running.");
            active?.Dispose();
            try
            {
                // Start the hash-verified static Revora profile directly. No runtime bridge,
                // loader, proxy, injection, or socket redirection participates in launch.
                active = launcher.Start(new(executable, workingDirectory, profile.Arguments));
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                diagnostics?.Record("launch_failed", new Dictionary<string, string>
                    { ["profile"] = profile.Id.ToString(), ["reason"] = exception.GetType().Name });
                return new(false, false, null, "Windows could not start the selected game profile.");
            }
        }
        diagnostics?.Record("direct_profile_launch_requested", new Dictionary<string, string>
        {
            ["profile"] = profile.Id.ToString(), ["stage"] = "direct",
            ["official_updater_bypassed"] = "true", ["play_exe_observed"] = "false",
            ["autop_exe_observed"] = "false"
        });
        diagnostics?.Record("direct_profile_process_started", new Dictionary<string, string>
            { ["profile"] = profile.Id.ToString(), ["stage"] = "not_required" });
        if (profile.Id == ClientProfileId.Dx9 && bridge is not null)
        {
            var observed = new ObservedProcess(active.ProcessId, Environment.ProcessId, executable, active.HasExited,
                active.HasExited ? active.ExitCode : null);
            BridgeLoadResult bridgeResult = await bridge.LoadAsync(observed, executable, cancellationToken);
            if (!bridgeResult.Loaded)
            {
                if (!active.HasExited && active is ITerminableLaunchedProcess terminable) terminable.Terminate();
                return new(false, false, null, bridgeResult.UserMessage);
            }
        }
        status?.Invoke($"Direct profile started: {profile.DisplayName}");
        _ = MonitorDirectProcessAsync(active, profile, allowOfficialFallback ? approvedFallback : null);
        await clock.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        if (active.HasExited)
            return new(false, true, active.ExitCode, "The selected game profile exited during startup.");
        return new(true, false, null, "Direct profile process started; awaiting owner runtime confirmation.");
    }

    private async Task MonitorDirectProcessAsync(ILaunchedProcess process, ClientProfile profile,
        ClientProfile? approvedFallback)
    {
        var lifetime = Stopwatch.StartNew();
        await process.WaitForExitAsync(CancellationToken.None);
        lifetime.Stop();
        diagnostics?.Record("direct_profile_exit", new Dictionary<string, string>
        {
            ["profile"] = profile.Id.ToString(), ["exitCode"] = process.ExitCode.ToString(),
            ["lifetimeMs"] = ((long)lifetime.Elapsed.TotalMilliseconds).ToString(),
            ["classification"] = lifetime.Elapsed < TimeSpan.FromSeconds(15) ? "early" : "normal"
        });
        if (lifetime.Elapsed >= TimeSpan.FromSeconds(15) || approvedFallback is null) return;
        try
        {
            ProfileContracts.Enforce(approvedFallback);
            string executable = root.Resolve(approvedFallback.ExecutableRelativePath, true);
            string workingDirectory = root.Resolve(approvedFallback.WorkingDirectoryRelativePath, true);
            ILaunchedProcess successor;
            lock (gate)
            {
                if (active != process) return;
                process.Dispose();
                successor = launcher.Start(new(executable, workingDirectory, approvedFallback.Arguments));
                active = successor;
            }
            diagnostics?.Record("automatic_profile_fallback", new Dictionary<string, string>
                { ["profile"] = approvedFallback.Id.ToString(), ["reason"] = "approved_early_exit" });
            status?.Invoke($"{profile.DisplayName} exited early; continued once with {approvedFallback.DisplayName}.");
            await MonitorDirectProcessAsync(successor, approvedFallback, null);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            diagnostics?.Record("launch_failed", new Dictionary<string, string>
                { ["profile"] = approvedFallback.Id.ToString(), ["reason"] = exception.GetType().Name });
        }
    }

    private async Task MonitorGameAsync(int processId, string expectedPath, ClientProfileId profile, Stopwatch lifetime,
        HashSet<int> knownDescendants, string dx8Executable, bool allowOfficialFallback)
    {
        while (true)
        {
            await clock.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            ObservedProcess? process = processTree.Snapshot().SingleOrDefault(item => item.ProcessId == processId);
            if (process is not null && !Path.GetFullPath(process.ExecutablePath).Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics?.Record("unexpected_executable_path", new Dictionary<string, string> { ["profile"] = profile.ToString(), ["reason"] = "path_changed" });
                return;
            }
            if (process is not null && !process.HasExited) continue;
            if (allowOfficialFallback && profile == ClientProfileId.Dx9)
            {
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    IReadOnlyList<ObservedProcess> snapshot = processTree.Snapshot();
                    foreach (ObservedProcess candidate in snapshot)
                        if (knownDescendants.Contains(candidate.ParentProcessId)) knownDescendants.Add(candidate.ProcessId);
                    ObservedProcess? successor = snapshot.FirstOrDefault(item => !item.HasExited
                        && knownDescendants.Contains(item.ProcessId)
                        && Path.GetFullPath(item.ExecutablePath).Equals(dx8Executable, StringComparison.OrdinalIgnoreCase));
                    if (successor is not null)
                    {
                        diagnostics?.Record("official_profile_fallback", new Dictionary<string, string>
                            { ["from"] = "Dx9", ["to"] = "Dx8", ["stage"] = "successor" });
                        status?.Invoke("DirectX 9 was unavailable; continued with DirectX 8.");
                        await MonitorGameAsync(successor.ProcessId, dx8Executable, ClientProfileId.Dx8,
                            Stopwatch.StartNew(), knownDescendants, dx8Executable, false);
                        return;
                    }
                    await clock.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None);
                }
            }
            lifetime.Stop();
            string classification = lifetime.Elapsed < TimeSpan.FromSeconds(15) ? "clean_early_client_exit" : "normal_game_exit";
            status?.Invoke(classification == "clean_early_client_exit" ? "Game exited early" : "Normal game exit");
            diagnostics?.Record("game_process_exit", new Dictionary<string, string>
            {
                ["profile"] = profile.ToString(), ["exitCode"] = process?.ExitCode?.ToString() ?? "unknown",
                ["lifetimeMs"] = ((long)lifetime.Elapsed.TotalMilliseconds).ToString(), ["classification"] = classification
            });
            return;
        }
    }
}

public static class SignedManifestCanonicalizer
{
    public static byte[] Canonicalize(SignedClientManifest manifest)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            writer.WriteString("manifestVersion", manifest.ManifestVersion);
            writer.WriteString("issuedAtUtc", manifest.IssuedAtUtc.ToUniversalTime());
            writer.WriteString("expiresAtUtc", manifest.ExpiresAtUtc.ToUniversalTime());
            writer.WriteString("minimumLauncherVersion", manifest.MinimumLauncherVersion);
            writer.WritePropertyName("rollback"); writer.WriteStartObject();
            writer.WriteString("previousManifestVersion", manifest.Rollback.PreviousManifestVersion);
            writer.WriteString("previousManifestSha256", manifest.Rollback.PreviousManifestSha256);
            writer.WriteEndObject();
            writer.WritePropertyName("rootRequiredFiles"); writer.WriteStartArray();
            foreach (RequiredProfileFile file in manifest.RootRequiredFiles.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
            { writer.WriteStartObject(); writer.WriteString("relativePath", file.RelativePath); writer.WriteNumber("size", file.Size); writer.WriteString("sha256", file.Sha256.ToUpperInvariant()); writer.WriteBoolean("critical", file.Critical); writer.WriteEndObject(); }
            writer.WriteEndArray();
            writer.WritePropertyName("profiles"); writer.WriteStartArray();
            foreach (ManifestProfile profile in manifest.Profiles.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", profile.Id); writer.WriteString("displayName", profile.DisplayName);
                writer.WriteString("executablePath", profile.ExecutablePath); writer.WriteString("workingDirectory", profile.WorkingDirectory);
                writer.WritePropertyName("arguments"); writer.WriteStartArray(); foreach (string arg in profile.Arguments) writer.WriteStringValue(arg); writer.WriteEndArray();
                writer.WritePropertyName("requiredFiles"); writer.WriteStartArray();
                foreach (RequiredProfileFile file in profile.RequiredFiles.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
                { writer.WriteStartObject(); writer.WriteString("relativePath", file.RelativePath); writer.WriteNumber("size", file.Size); writer.WriteString("sha256", file.Sha256.ToUpperInvariant()); writer.WriteBoolean("critical", file.Critical); writer.WriteEndObject(); }
                writer.WriteEndArray();
                if (manifest.SchemaVersion >= 2)
                {
                    writer.WritePropertyName("criticalLaunchFiles"); writer.WriteStartArray();
                    foreach (RequiredProfileFile file in profile.CriticalLaunchFiles.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
                    { writer.WriteStartObject(); writer.WriteString("relativePath", file.RelativePath); writer.WriteNumber("size", file.Size); writer.WriteString("sha256", file.Sha256.ToUpperInvariant()); writer.WriteBoolean("critical", file.Critical); writer.WriteEndObject(); }
                    writer.WriteEndArray();
                }
                writer.WriteString("minimumWindowsVersion", profile.MinimumWindowsVersion);
                writer.WritePropertyName("supportedArchitectures"); writer.WriteStartArray(); foreach (string arch in profile.SupportedArchitectures.Order(StringComparer.Ordinal)) writer.WriteStringValue(arch); writer.WriteEndArray();
                writer.WriteBoolean("requiresDirectX8", profile.RequiresDirectX8); writer.WriteBoolean("requiresDirectX9", profile.RequiresDirectX9);
                writer.WriteNumber("priority", profile.Priority); writer.WriteBoolean("automaticFallbackAllowed", profile.AutomaticFallbackAllowed);
                writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}

public sealed class SignedManifestVerifier(IClock clock, Version launcherVersion)
{
    public VerifiedClientManifest Verify(SignedClientManifest manifest, ManifestTrust trust)
    {
        if (manifest.SchemaVersion is not 1 and not 2) throw new InvalidDataException("Unsupported signed manifest schema version.");
        if (clock.UtcNow < manifest.IssuedAtUtc || clock.UtcNow >= manifest.ExpiresAtUtc)
            throw new InvalidDataException("The signed client manifest is not currently valid.");
        if (SemanticVersion.Parse(manifest.MinimumLauncherVersion).CompareTo(
                SemanticVersion.Parse(launcherVersion.ToString(3))) > 0)
            throw new InvalidDataException("A newer launcher is required.");
        if (manifest.Signature.Algorithm != "ECDSA_P256_SHA256" || manifest.Signature.KeyId != trust.KeyId)
            throw new CryptographicException("The client manifest signature policy is unsupported.");
        byte[] signature;
        byte[] publicKey;
        try { signature = Convert.FromBase64String(manifest.Signature.ValueBase64); publicKey = Convert.FromBase64String(trust.PublicKeySpkiBase64); }
        catch (FormatException exception) { throw new CryptographicException("The manifest signature encoding is invalid.", exception); }
        using ECDsa verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(publicKey, out int read);
        if (read != publicKey.Length || !verifier.VerifyData(SignedManifestCanonicalizer.Canonicalize(manifest), signature, HashAlgorithmName.SHA256))
            throw new CryptographicException("The client manifest signature is invalid.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RequiredProfileFile file in manifest.RootRequiredFiles)
        {
            ValidatedClientRoot.ValidateRelative(file.RelativePath);
            string normalized = file.RelativePath.Replace('\\', '/');
            if (normalized.StartsWith("Env_DX8/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Env_DX9/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Root manifest files may not enter a profile tree.");
            if (!seen.Add(normalized)) throw new InvalidDataException("The manifest contains duplicate paths.");
        }
        var profiles = new List<ClientProfile>();
        foreach (ManifestProfile source in manifest.Profiles)
        {
            ClientProfileId id = source.Id switch { "dx8" => ClientProfileId.Dx8, "dx9" => ClientProfileId.Dx9, _ => throw new InvalidDataException("The manifest contains an unsupported profile.") };
            ValidatedClientRoot.ValidateRelative(source.ExecutablePath); ValidatedClientRoot.ValidateRelative(source.WorkingDirectory);
            foreach (RequiredProfileFile file in source.RequiredFiles)
            {
                ValidatedClientRoot.ValidateRelative(file.RelativePath);
                if (!seen.Add(file.RelativePath)) throw new InvalidDataException("The manifest contains duplicate paths.");
            }
            IReadOnlyList<RequiredProfileFile> critical = manifest.SchemaVersion == 1
                ? source.RequiredFiles.Where(file => file.RelativePath.Replace('\\', '/').Equals(
                    source.ExecutablePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)).ToArray()
                : source.CriticalLaunchFiles.AsReadOnly();
            if (critical.Count == 0) throw new InvalidDataException("The profile has no signed critical launch files.");
            string environment = id == ClientProfileId.Dx8 ? "Env_DX8/" : "Env_DX9/";
            foreach (RequiredProfileFile file in critical)
            {
                ValidatedClientRoot.ValidateRelative(file.RelativePath);
                if (!file.RelativePath.Replace('\\', '/').StartsWith(environment, StringComparison.OrdinalIgnoreCase)
                    || !source.RequiredFiles.Any(item => item.RelativePath.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase)
                        && item.Size == file.Size && item.Sha256.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Critical launch files must be an exact profile-local repair-catalog subset.");
            }
            var architectures = source.SupportedArchitectures.Select(value => value switch
            { "x86" => Architecture.X86, "x64" => Architecture.X64, "arm64" => Architecture.Arm64, _ => throw new InvalidDataException("Unsupported architecture in manifest.") }).ToArray();
            var profile = new ClientProfile(id, source.DisplayName, source.ExecutablePath, source.WorkingDirectory,
                source.Arguments.AsReadOnly(), source.RequiredFiles.AsReadOnly(),
                new(Version.Parse(source.MinimumWindowsVersion), architectures, source.RequiresDirectX8, source.RequiresDirectX9),
                source.Priority, source.AutomaticFallbackAllowed) { CriticalLaunchFiles = critical };
            ProfileContracts.Enforce(profile); profiles.Add(profile);
        }
        if (profiles.Select(item => item.Id).Distinct().Count() != profiles.Count)
            throw new InvalidDataException("The manifest contains duplicate profiles.");
        return new(manifest.RootRequiredFiles.AsReadOnly(), profiles.AsReadOnly());
    }
}

public sealed class DualProfileLauncherService
{
    private readonly LauncherConfig config;
    private readonly IProfileFileSystem files;
    private readonly IHardwareCapabilityProvider hardware;
    private readonly IClock clock;
    private readonly IDiagnosticSink diagnostics;
    private readonly ProfileSelectionService selector = new();
    private readonly ILauncherPreferencesStore preferencesStore;
    private IReadOnlyDictionary<ClientProfileId, ProfileVerification> verified =
        new Dictionary<ClientProfileId, ProfileVerification>();
    private GameProcessCoordinator? processCoordinator;
    private VerifiedClientManifest? trustedManifest;
    private readonly VerificationReceiptStore receiptStore;

    public DualProfileLauncherService(LauncherConfig config, IProfileFileSystem? files = null,
        IHardwareCapabilityProvider? hardware = null, IProcessLauncher? processes = null, IClock? clock = null,
        ILauncherPreferencesStore? preferencesStore = null, IDiagnosticSink? diagnostics = null)
    {
        this.config = config;
        this.files = files ?? new PhysicalProfileFileSystem();
        this.hardware = hardware ?? new WindowsHardwareCapabilityProvider();
        this.clock = clock ?? new SystemClock();
        this.diagnostics = diagnostics ?? new RedactedFileDiagnosticSink(
            Path.Combine(AppContext.BaseDirectory, "logs", "launcher-diagnostics.log"));
        this.preferencesStore = preferencesStore ?? new JsonLauncherPreferencesStore(
            Path.Combine(AppContext.BaseDirectory, config.PreferencesFile));
        receiptStore = new(config.GameRoot, config.VerificationReceiptFile, this.clock);
        ProcessLauncher = processes ?? new WindowsProcessLauncher();
    }

    public LauncherPreferences Preferences { get; private set; } = new();
    private IProcessLauncher ProcessLauncher { get; }
    public ProfileSelectionResult Selection { get; private set; } =
        new(null, "Client profiles have not been verified.", true);
    public ProfileReadiness Readiness { get; private set; } = NotProvisioned();
    public event Action<string>? LaunchStatusChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Preferences = await preferencesStore.LoadAsync(cancellationToken);
        diagnostics.Record("render_backend_contract", new Dictionary<string, string>
        {
            ["channel"] = config.BuildChannel,
            ["backend"] = config.RenderBackendLabel,
            ["profile"] = "dx9"
        });
        // This branded release has exactly one approved process image. Normalize
        // stale Automatic/DX8 preferences instead of permitting an original-client fallback.
        Preferences.ProfileMode = ProfileSelectionMode.DirectX9;
        if (string.IsNullOrWhiteSpace(config.ManifestKeyId) || string.IsNullOrWhiteSpace(config.ManifestPublicKeySpkiBase64))
        {
            SetNotProvisioned();
            return;
        }
        string manifestPath = SafePaths.UnderRoot(AppContext.BaseDirectory, config.SignedClientManifest);
        if (!File.Exists(manifestPath))
        {
            SetNotProvisioned();
            return;
        }
        IReadOnlyList<ClientProfile> profiles;
        try
        {
            byte[] manifestBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
            SignedClientManifest manifest = JsonSerializer.Deserialize<SignedClientManifest>(manifestBytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Signed manifest JSON is invalid.");
            VerifiedClientManifest verifiedManifest = new SignedManifestVerifier(clock,
                typeof(DualProfileLauncherService).Assembly.GetName().Version ?? new Version(1, 0, 0))
                .Verify(manifest, new(config.ManifestKeyId, config.ManifestPublicKeySpkiBase64));
            profiles = verifiedManifest.Profiles; trustedManifest = verifiedManifest;
            if (File.Exists(SafePaths.UnderRoot(config.GameRoot, config.PendingInstallFile))
                || Directory.Exists(SafePaths.UnderRoot(config.GameRoot, "RevoraUpdates/Staging/active")))
            {
                SetFailure(ProfileReadinessReason.RecoveryRequired, "Rollback/recovery required",
                    "An interrupted update must be recovered before game launch.");
                return;
            }

            diagnostics.Record("legacy_full_verification_state", new Dictionary<string, string>
            {
                ["launchGate"] = "ignored",
                ["repairCatalog"] = "not_requested_or_stale"
            });
        }
        catch (CryptographicException)
        {
            SetFailure(ProfileReadinessReason.InvalidSignature, "Client verification failed",
                "The trusted client manifest could not be verified. Game launch remains disabled.");
            return;
        }
        catch (InvalidDataException exception) when (exception.Message.Contains("not currently valid", StringComparison.Ordinal))
        {
            SetFailure(ProfileReadinessReason.ExpiredManifest, "Client verification expired",
                "The trusted client manifest is outside its validity period. Game launch remains disabled.");
            return;
        }
        catch (ClientFileVerificationException exception)
        {
            RecordVerificationFailure(exception.RelativePath, exception.Category, "root");
            if (exception.Category == ClientFileMismatchCategory.Missing)
                SetFailure(ProfileReadinessReason.RequiredFileMissing, "Required file missing",
                    "A required client file is missing. Game launch remains disabled.");
            else
                SetFailure(ProfileReadinessReason.HashMismatch, "Client verification mismatch",
                    "A required client file did not match the trusted manifest. Game launch remains disabled.");
            return;
        }
        catch (InvalidDataException)
        {
            SetFailure(ProfileReadinessReason.InvalidSignature, "Client verification failed",
                "The trusted client manifest is invalid or unsupported. Game launch remains disabled.");
            return;
        }
        await ResolveProfilesAsync(profiles, cancellationToken);
    }

    public async Task SetModeAsync(ProfileSelectionMode mode, CancellationToken cancellationToken)
    {
        ProfileReadiness previousGate = Readiness;
        Preferences.ProfileMode = mode;
        await preferencesStore.SaveAsync(Preferences, cancellationToken);
        if (Readiness.ReasonCode == ProfileReadinessReason.ManifestNotProvisioned)
        {
            SetNotProvisioned();
            return;
        }
        Selection = selector.Select(mode, hardware.Detect(), verified);
        Readiness = ResolveReadiness(Selection, verified);
        if (Selection.Profile is not null && previousGate.ReasonCode is ProfileReadinessReason.UpdateCheckPending
            or ProfileReadinessReason.MandatoryUpdateRequired)
            Readiness = previousGate;
    }

    public Task ResetAutomaticAsync(CancellationToken cancellationToken) =>
        SetModeAsync(ProfileSelectionMode.Automatic, cancellationToken);

    public void ApplyUpdatePolicy(IReadOnlyList<SignedPatchReference> patches)
    {
        if (Selection.Profile is null) return;
        if (!LaunchGatePolicy.UpdatePermitsLaunch(patches))
            Readiness = new(ProfileReadinessReason.MandatoryUpdateRequired, "Mandatory update required",
                "A required signed update must be installed before game launch.", false);
        else
            Readiness = new(ProfileReadinessReason.ProfileReady, patches.Count == 0 ? "Ready" : "Update available",
                patches.Count == 0 ? "Critical launch readiness passed. Full repair was not requested."
                    : "An optional update is available. You may still start the game.", true);
    }

    public void MarkUpdateCheckFailed() =>
        Readiness = new(ProfileReadinessReason.UpdateCheckPending, "Checking for updates",
            "The signed update state could not be established. Try again before launch.", false);

    public void RequireRecovery(string reason = "update_incomplete")
    {
        receiptStore.Invalidate();
        SetFailure(ProfileReadinessReason.RecoveryRequired, "Recovery required",
            "Client update or rollback did not complete. Recovery is required before launch.");
        diagnostics.Record("verification_required", new Dictionary<string, string> { ["reason"] = reason });
    }

    public async Task FullVerifyAsync(CancellationToken cancellationToken)
    {
        if (trustedManifest is null) throw new InvalidOperationException("A trusted signed client manifest is not loaded.");
        await VerifyRepairCatalogAsync(cancellationToken);
        await ResolveProfilesAsync(trustedManifest.Profiles, cancellationToken);
    }

    public Task<LaunchResult> LaunchAsync(CancellationToken cancellationToken)
    {
        if (!Readiness.LaunchAuthorized || Selection.Profile is null || processCoordinator is null)
            return Task.FromResult(new LaunchResult(false, false, null, "Game launch is awaiting trusted client verification."));
        if (Selection.Profile.Id != ClientProfileId.Dx9)
            return Task.FromResult(new LaunchResult(false, false, null,
                "Only the verified Revora DX9 profile may be started by this release."));
        return processCoordinator.LaunchAsync(Selection.Profile, cancellationToken, false, null);
    }

    private void SetNotProvisioned()
    {
        verified = new Dictionary<ClientProfileId, ProfileVerification>();
        Selection = new(null, "Profile preference is available; trusted verification is not provisioned.", false,
            null, ProfileReadinessReason.ManifestNotProvisioned);
        Readiness = NotProvisioned();
        diagnostics.Record("profile_readiness", new Dictionary<string, string> { ["reason"] = ProfileReadinessReason.ManifestNotProvisioned.ToString() });
    }

    private void SetFailure(ProfileReadinessReason reason, string heading, string message)
    {
        verified = new Dictionary<ClientProfileId, ProfileVerification>();
        Selection = new(null, message, false, null, reason);
        Readiness = new(reason, heading, message, false);
        diagnostics.Record("profile_readiness", new Dictionary<string, string> { ["reason"] = reason.ToString() });
    }

    private void RecordVerificationFailure(string relativePath, ClientFileMismatchCategory category, string profile) =>
        diagnostics.Record("client_file_verification_failed", new Dictionary<string, string>
        {
            ["profile"] = profile,
            ["relativePath"] = relativePath,
            ["category"] = category.ToString()
        });

    private static ProfileReadiness ResolveReadiness(ProfileSelectionResult selection,
        IReadOnlyDictionary<ClientProfileId, ProfileVerification> profiles)
    {
        if (selection.Profile is not null)
            return new(ProfileReadinessReason.ProfileReady, "Profile ready",
                "Trusted client verification succeeded. The selected profile is ready.", true);
        if (selection.Reason.Contains("not supported", StringComparison.OrdinalIgnoreCase))
            return new(ProfileReadinessReason.UnsupportedHardware, "Profile unsupported",
                "The selected profile is not supported by the detected system capabilities.", false);
        ProfileVerification? failure = profiles.Values.FirstOrDefault(item => item.State == ProfileVerificationState.Unavailable);
        if (failure?.UserMessage.Contains("missing", StringComparison.OrdinalIgnoreCase) == true)
            return new(ProfileReadinessReason.RequiredFileMissing, "Required file missing",
                $"Critical file {failure.Failure?.RelativePath ?? "unknown"} is missing. Use Verify & Repair Client.", false);
        if (failure?.UserMessage.Contains("failed verification", StringComparison.OrdinalIgnoreCase) == true)
            return new(ProfileReadinessReason.HashMismatch, "Client verification mismatch",
                $"Critical file {failure.Failure?.RelativePath ?? "unknown"} failed {failure.Failure?.Category}. Use Verify & Repair Client.", false);
        return new(ProfileReadinessReason.SelectedProfileAbsent, "Selected profile unavailable",
            "The selected profile is not present in the trusted client manifest.", false);
    }

    private static ProfileReadiness NotProvisioned() => new(ProfileReadinessReason.ManifestNotProvisioned,
        "Verification not provisioned",
        "This owner-review build has no trusted client manifest. Profile selection can be reviewed, but game launch remains disabled.", false);

    private async Task VerifyRepairCatalogAsync(CancellationToken cancellationToken)
    {
        if (trustedManifest is null) throw new InvalidOperationException("Trusted manifest is unavailable.");
        diagnostics.Record("repair_catalog_scan_started", new Dictionary<string, string> { ["reason"] = "explicit_request" });
        var root = new ValidatedClientRoot(config.GameRoot, files);
        var integrity = new ClientFileIntegrityVerifier(root, files);
        await integrity.VerifyAsync(trustedManifest.RootRequiredFiles, cancellationToken);
        foreach (ClientProfile profile in trustedManifest.Profiles)
            await integrity.VerifyAsync(profile.RequiredFiles, cancellationToken);
        diagnostics.Record("repair_catalog_scan_completed", new Dictionary<string, string> { ["result"] = "success" });
    }

    private async Task ResolveProfilesAsync(IReadOnlyList<ClientProfile> profiles, CancellationToken cancellationToken)
    {
        var root = new ValidatedClientRoot(config.GameRoot, files);
        RequiredProfileFile[] rootCritical = [];
        if (trustedManifest is not null)
        {
            rootCritical = trustedManifest.RootRequiredFiles.Where(file => file.Critical).ToArray();
            await new ClientFileIntegrityVerifier(root, files).VerifyAsync(rootCritical, cancellationToken);
        }
        processCoordinator = new(root, files, ProcessLauncher, clock, diagnostics, null,
            message => LaunchStatusChanged?.Invoke(message));
        var verifier = new ProfileVerifier(root, files);
        var results = new Dictionary<ClientProfileId, ProfileVerification>();
        foreach (ClientProfile profile in profiles)
            results[profile.Id] = await verifier.VerifyAsync(profile, cancellationToken);
        verified = results; Selection = selector.Select(Preferences.ProfileMode, hardware.Detect(), verified); Readiness = ResolveReadiness(Selection, verified);
        bool criticalLaunchPassed = Readiness.LaunchAuthorized;
        if (criticalLaunchPassed)
            Readiness = new(ProfileReadinessReason.UpdateCheckPending, "Checking for updates",
                "Critical launch readiness passed. Checking signed update requirements.", false);
        diagnostics.Record("critical_readiness", new Dictionary<string, string>
        {
            ["result"] = criticalLaunchPassed ? "passed" : "failed",
            ["repairCatalog"] = "not_requested_or_stale"
        });
    }
}

public static class LaunchGatePolicy
{
    public static bool UpdatePermitsLaunch(IReadOnlyList<SignedPatchReference> patches) =>
        !patches.Any(patch => patch.Mandatory);
}
