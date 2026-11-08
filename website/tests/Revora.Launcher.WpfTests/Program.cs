using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Revora.Launcher;
using Revora.Launcher.Core;

namespace Revora.Launcher.WpfTests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["blacknull"])
        {
            Thread.Sleep(TimeSpan.FromSeconds(30));
            return 0;
        }
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        Environment.SetEnvironmentVariable("REVORA_LAUNCHER_VISUAL_TEST", "1");
        string output = args.Length == 1 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine("artifacts", "launcher-screenshots"));
        Directory.CreateDirectory(output);
        var cases = new[]
        {
            new VisualCase("ready-en-1920x1080-100", 1920, 1080, 1.0, false, LauncherState.Ready, 0L, 0L),
            new VisualCase("downloading-en-2560x1440-125", 2560, 1440, 1.25, false, LauncherState.Downloading, 638_000_000L, 1_450_174_454L),
            new VisualCase("repair-en-3840x2160-200", 3840, 2160, 2.0, false, LauncherState.Error, 0L, 0L),
            new VisualCase("ready-ar-1920x1080-100", 1920, 1080, 1.0, true, LauncherState.Ready, 0L, 0L),
        };

        var application = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.InitializeComponent();
        foreach (VisualCase item in cases)
        {
            double width = item.PixelWidth / item.Scale, height = item.PixelHeight / item.Scale;
            var window = new MainWindow { Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.Manual };
            window.ConfigureVisualTest(item.State, item.Arabic, item.Completed, item.Total);
            window.RootGrid.Width = width; window.RootGrid.Height = height;
            window.RootGrid.Measure(new Size(width, height)); window.RootGrid.Arrange(new Rect(0, 0, width, height)); window.RootGrid.UpdateLayout();
            var bitmap = new RenderTargetBitmap(item.PixelWidth, item.PixelHeight, 96 * item.Scale, 96 * item.Scale, PixelFormats.Pbgra32);
            bitmap.Render(window.RootGrid);
            string path = Path.Combine(output, item.Name + ".png");
            using (FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None)) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream); }
            if (new FileInfo(path).Length < 100_000) throw new InvalidOperationException($"Rendered screenshot is unexpectedly small: {item.Name}");
            Console.WriteLine($"PASS {item.Name}: {path}");
        }

        var consistency = new MainWindow();
        consistency.ConfigureVisualTest(LauncherState.Downloading, false, 1_450_174_454L, 1_450_174_454L);
        typeof(MainWindow).GetMethod("ResetProgress", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(consistency, ["Starting integrity scan"]);
        if (consistency.CurrentFileText.Text != "Starting integrity scan" || consistency.ProgressPercent.Text != "0%" ||
            consistency.TransferText.Text != "0 B / 0 B" || consistency.SpeedText.Text != "—" || consistency.TotalProgress.Value != 0)
            throw new InvalidOperationException("Initialization retained stale download progress.");
        string visibleVersion = $"Launcher {LauncherPolicy.ProductVersion}";
        string? packageVersion = FileVersionInfo.GetVersionInfo(typeof(MainWindow).Assembly.Location).ProductVersion;
        if (consistency.LauncherVersionText.Text != visibleVersion || packageVersion is null ||
            !packageVersion.StartsWith(LauncherPolicy.ProductVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Package metadata, diagnostics policy, and visible version diverged.");
        Console.WriteLine($"PASS progress reset and visible/package version {LauncherPolicy.ProductVersion}");

        byte[] icon = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "revora.ico"));
        ushort count = BitConverter.ToUInt16(icon, 4); var sizes = new HashSet<int>();
        for (int index = 0; index < count; index++) { int width = icon[6 + index * 16]; sizes.Add(width == 0 ? 256 : width); }
        foreach (int required in new[] { 16, 24, 32, 48, 64, 128, 256 }) if (!sizes.Contains(required)) throw new InvalidOperationException($"Icon size {required} is absent.");
        Console.WriteLine("PASS seven-resolution Revora icon");
        VerifyCompleteInitializationUsesCache();
        VerifyAutomaticLaunchLifecycle();
        VerifyAutomaticRepairAndLaunch(missing: true);
        VerifyAutomaticRepairAndLaunch(missing: false);
        VerifyMultiAccountAndRepairSafety();
        application.Shutdown();
        return 0;
    }

    private static void VerifyCompleteInitializationUsesCache()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This regression requires Windows.");
        string root = Path.Combine(Path.GetTempPath(), "revora-launcher-wpf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        byte[] critical = "approved-client-fixture"u8.ToArray(), passive = "5165"u8.ToArray();
        string criticalPath = Path.Combine(root, "Conquer.exe"), passivePath = Path.Combine(root, "version.dat");
        File.WriteAllBytes(criticalPath, critical);
        File.WriteAllBytes(passivePath, passive);
        string criticalHash = Convert.ToHexStringLower(SHA256.HashData(critical));
        string passiveHash = Convert.ToHexStringLower(SHA256.HashData(passive));
        var manifest = new DesiredStateManifest(2, Guid.NewGuid(), "complete-valid-client-fixture", "test",
            DateTimeOffset.UtcNow, LauncherPolicy.ProductVersion, criticalHash,
            "https://playrevora.com/patch/v2/test/release", "https://playrevora.com/patch/v2/origin/release",
            LauncherPolicy.ServerHost, LauncherPolicy.AccountPort, LauncherPolicy.GamePort, null, null,
            [
                new("Conquer.exe", critical.Length, criticalHash, "fixture",
                    $"objects/sha256/{criticalHash[..2]}/{criticalHash}", "none", null, true, true, "replace", true),
                new("version.dat", passive.Length, passiveHash, "fixture",
                    $"objects/sha256/{passiveHash[..2]}/{passiveHash}", "none", null, true, false, "replace", false),
            ]);
        var window = new MainWindow();
        SetField(window, "clientRoot", root);
        SetField(window, "manifest", manifest);
        var machine = (LauncherStateMachine)GetField(window, "state");
        machine.Transition(LauncherState.ResolvingClient, "test");
        machine.Transition(LauncherState.FetchingManifest, "test");
        machine.Transition(LauncherState.VerifyingManifest, "test");

        InvokeTask(window, "ScanAsync", false, CancellationToken.None);
        if (machine.Current.State != LauncherState.Ready || !File.Exists(VerifiedStateCache.PathFor(root)) ||
            File.Exists(VerifiedStateCache.PathFor(root) + ".new"))
            throw new InvalidOperationException("A complete valid client did not persist its cache and reach Ready.");

        InvokeTask(window, "ScanAsync", false, CancellationToken.None);
        var second = (ClientScanResult)GetField(window, "scan");
        if (machine.Current.State != LauncherState.Ready || second.CachedFiles != 1 ||
            second.HashedBytes != critical.Length || second.Failures.Count != 0)
            throw new InvalidOperationException("Second initialization did not accept the cache without a client download.");
        string cachePath = VerifiedStateCache.PathFor(root);
        string moved = cachePath + ".moved";
        File.Move(cachePath, moved, false);
        File.Delete(moved);
        window.Close();
        Directory.Delete(root, true);
        Console.WriteLine("PASS complete Windows initialization persists cache, reaches Ready, and reuses cache");
    }

    private static void VerifyAutomaticLaunchLifecycle()
    {
        string root = CreateLaunchableClient(out DesiredStateManifest manifest, writeApprovedExecutable: true);
        using var http = new HttpClient(new ObjectHandler([]));
        var launcher = new TrackingProcessLauncher();
        var window = PrepareWindow(root, manifest, http, launcher, static () => false);
        InvokeTask(window, "PrepareClientAsync", false, 1L, CancellationToken.None);
        var machine = (LauncherStateMachine)GetField(window, "state");
        if (launcher.Count != 1 || machine.Current.State != LauncherState.GameRunning)
            throw new InvalidOperationException("A valid client did not launch exactly once.");
        if (window.FindName("UsernameBox") is not null || window.FindName("PasswordBox") is not null ||
            window.FindName("LoginPanel") is not null)
            throw new InvalidOperationException("Launcher credential controls remain in the WPF tree.");
        InvokeTask(window, "LaunchVerifiedClientAsync", 1L, true);
        if (launcher.Count != 1) throw new InvalidOperationException("An initialization cycle launched twice.");
        InvokeTask(window, "LaunchVerifiedClientAsync", new object?[] { null, false });
        if (launcher.Count != 2 || machine.Current.State != LauncherState.GameRunning ||
            !Equals(window.PrimaryButton.Content, "Open Another Account") || window.FullRepairButton.IsEnabled)
            throw new InvalidOperationException("A deliberate second account did not launch safely while repair remained disabled.");
        StopFixture(launcher.Processes[1], machine, LauncherState.GameRunning);
        if (machine.Current.State != LauncherState.GameRunning)
            throw new InvalidOperationException("Closing one game incorrectly marked all game clients closed.");
        StopFixture(launcher.Processes[0], machine, LauncherState.Ready);
        Thread.Sleep(250);
        PumpDispatcher();
        if (machine.Current.State != LauncherState.Ready || launcher.Count != 2)
            throw new InvalidOperationException("Game exit did not return to Ready without relaunching.");
        InvokeTask(window, "LaunchVerifiedClientAsync", new object?[] { null, false });
        if (launcher.Count != 3 || machine.Current.State != LauncherState.GameRunning)
            throw new InvalidOperationException("Manual Play did not launch exactly once after game exit.");
        StopFixture(launcher.LastProcess!, machine);
        window.Close();
        Directory.Delete(root, true);
        Console.WriteLine("PASS valid client auto-launches once, opens a deliberate second account, tracks independent exits, and returns to Ready");
    }

    private static void VerifyAutomaticRepairAndLaunch(bool missing)
    {
        string root = CreateLaunchableClient(out DesiredStateManifest manifest, writeApprovedExecutable: !missing);
        string executable = Path.Combine(root, LauncherPolicy.ClientExecutable);
        if (!missing) File.WriteAllBytes(executable, "corrupt-managed-client"u8.ToArray());
        byte[] approved = File.ReadAllBytes(Environment.ProcessPath!);
        var handler = new ObjectHandler(approved);
        using var http = new HttpClient(handler);
        var launcher = new TrackingProcessLauncher();
        var window = PrepareWindow(root, manifest, http, launcher, static () => false);
        InvokeTask(window, "PrepareClientAsync", false, missing ? 2L : 3L, CancellationToken.None);
        var machine = (LauncherStateMachine)GetField(window, "state");
        if (launcher.Count != 1 || machine.Current.State != LauncherState.GameRunning || handler.RequestCount != 1 ||
            !File.ReadAllBytes(executable).SequenceEqual(approved))
            throw new InvalidOperationException($"Automatic {(missing ? "missing" : "corrupt")} file repair did not verify and launch exactly once.");
        StopFixture(launcher.LastProcess!, machine);
        window.Close();
        Directory.Delete(root, true);
        Console.WriteLine($"PASS {(missing ? "missing" : "corrupt")} managed file auto-repairs and launches once");
    }

    private static void VerifyMultiAccountAndRepairSafety()
    {
        string root = CreateLaunchableClient(out DesiredStateManifest manifest, writeApprovedExecutable: true);
        using var http = new HttpClient(new ObjectHandler([]));
        var multiLauncher = new TrackingProcessLauncher();
        var multi = PrepareWindow(root, manifest, http, multiLauncher, static () => true);
        InvokeTask(multi, "PrepareClientAsync", false, 4L, CancellationToken.None);
        var multiState = (LauncherStateMachine)GetField(multi, "state");
        if (multiLauncher.Count != 1 || multiState.Current.State != LauncherState.GameRunning)
            throw new InvalidOperationException("A verified additional account was blocked by another running game.");
        StopFixture(multiLauncher.LastProcess!, multiState);
        multi.Close();

        using var fullHttp = new HttpClient(new ObjectHandler([]));
        var fullLauncher = new TrackingProcessLauncher();
        var full = PrepareWindow(root, manifest, fullHttp, fullLauncher, static () => false);
        InvokeTask(full, "PrepareClientAsync", true, 5L, CancellationToken.None);
        if (fullLauncher.Count != 0 || ((LauncherStateMachine)GetField(full, "state")).Current.State != LauncherState.Ready)
            throw new InvalidOperationException("Manual Full Repair launched the game automatically.");
        full.Close();

        string repairRoot = CreateLaunchableClient(out DesiredStateManifest repairManifest, writeApprovedExecutable: false);
        byte[] approved = File.ReadAllBytes(Environment.ProcessPath!);
        using var repairHttp = new HttpClient(new ObjectHandler(approved));
        var repairLauncher = new TrackingProcessLauncher();
        var repair = PrepareWindow(repairRoot, repairManifest, repairHttp, repairLauncher, static () => true);
        try
        {
            InvokeTask(repair, "PrepareClientAsync", false, 6L, CancellationToken.None);
            throw new InvalidOperationException("Repair was allowed while a game process was running.");
        }
        catch (IOException error) when (error.Message.Contains("Close the game", StringComparison.Ordinal)) { }
        if (repairLauncher.Count != 0 || File.Exists(Path.Combine(repairRoot, LauncherPolicy.ClientExecutable)))
            throw new InvalidOperationException("Blocked repair changed the live executable or launched a game.");
        repair.Close();
        Directory.Delete(repairRoot, true);
        Directory.Delete(root, true);
        Console.WriteLine("PASS another account may launch while repair remains blocked by a running game and Full Repair remains manual");
    }

    private static MainWindow PrepareWindow(string root, DesiredStateManifest manifest, HttpClient http,
        IVerifiedClientProcessLauncher launcher, Func<bool> gameRunning)
    {
        var window = new MainWindow(http, launcher, gameRunning);
        SetField(window, "clientRoot", root);
        SetField(window, "manifest", manifest);
        var machine = (LauncherStateMachine)GetField(window, "state");
        machine.Transition(LauncherState.ResolvingClient, "test");
        machine.Transition(LauncherState.FetchingManifest, "test");
        machine.Transition(LauncherState.VerifyingManifest, "test");
        return window;
    }

    private static string CreateLaunchableClient(out DesiredStateManifest manifest, bool writeApprovedExecutable)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This regression requires Windows.");
        string root = Path.Combine(Path.GetTempPath(), "revora-launcher-process-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        foreach (string source in Directory.EnumerateFiles(AppContext.BaseDirectory))
        {
            string target = Path.Combine(root, Path.GetFileName(source));
            if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(Environment.ProcessPath!), StringComparison.OrdinalIgnoreCase))
                File.Copy(source, target, true);
        }
        byte[] executable = File.ReadAllBytes(Environment.ProcessPath!);
        if (writeApprovedExecutable) File.WriteAllBytes(Path.Combine(root, LauncherPolicy.ClientExecutable), executable);
        string hash = Convert.ToHexStringLower(SHA256.HashData(executable));
        manifest = new DesiredStateManifest(2, Guid.NewGuid(), "automatic-launch-windows-fixture", "test",
            DateTimeOffset.UtcNow, LauncherPolicy.ProductVersion, hash,
            "https://playrevora.com/patch/v2/test/release", "https://playrevora.com/patch/v2/origin/release",
            LauncherPolicy.ServerHost, LauncherPolicy.AccountPort, LauncherPolicy.GamePort, null, null,
            [new(LauncherPolicy.ClientExecutable, executable.Length, hash, "fixture",
                $"objects/sha256/{hash[..2]}/{hash}", "none", null, true, true, "replace", true)]);
        return root;
    }

    private static void StopFixture(Process process, LauncherStateMachine machine, LauncherState expectedState = LauncherState.Ready)
    {
        if (!process.HasExited) process.Kill(true);
        process.WaitForExit(5000);
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (machine.Current.State != expectedState && DateTime.UtcNow < deadline) PumpDispatcher();
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static object GetField(object instance, string name) => instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void SetField(object instance, string name, object value) => instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    private static void InvokeTask(object instance, string name, params object?[] arguments)
    {
        var task = (Task)instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments)!;
        var frame = new DispatcherFrame();
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }
}

internal sealed record VisualCase(string Name, int PixelWidth, int PixelHeight, double Scale, bool Arabic, LauncherState State, long Completed, long Total);

internal sealed class TrackingProcessLauncher : IVerifiedClientProcessLauncher
{
    private readonly ClientProcessLauncher inner = new();
    private readonly List<Process> processes = [];
    public int Count { get; private set; }
    public Process? LastProcess { get; private set; }
    public IReadOnlyList<Process> Processes => processes;
    public Process LaunchVerified(string clientRoot, ClientProfile profile, VerificationResult verification)
    {
        Count++;
        LastProcess = inner.LaunchVerified(clientRoot, profile, verification);
        processes.Add(LastProcess);
        return LastProcess;
    }
}

internal sealed class ObjectHandler(byte[] content) : HttpMessageHandler
{
    public int RequestCount { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content),
            RequestMessage = request,
        });
    }
}
