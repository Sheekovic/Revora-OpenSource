using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Revora.Launcher.Core;

namespace Revora.Launcher;

public partial class MainWindow : Window
{
    private readonly LauncherStateMachine state = new();
    private readonly LauncherDiagnostics diagnostics = new();
    private readonly PauseController pause = new();
    private readonly HttpClient http;
    private readonly IVerifiedClientProcessLauncher processLauncher;
    private readonly Func<bool> isGameProcessRunning;
    private CancellationTokenSource operation = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private string? clientRoot;
    private DesiredStateManifest? manifest;
    private ClientScanResult? scan;
    private readonly Dictionary<int, Process> games = [];
    private readonly SemaphoreSlim launchGate = new(1, 1);
    private long initializationCycle;
    private long claimedAutomaticLaunchCycle;
    private bool automaticRepairActive;
    private bool arabic;
    private bool reducedMotion;
    private Storyboard? motion;
    private DateTimeOffset progressStarted;
    private string? diagnosticsDirectory;
    private readonly LauncherChannel channel = LauncherChannelResolver.Resolve(
        AppContext.BaseDirectory, Environment.GetCommandLineArgs());

    public MainWindow() : this(CreateHttpClient(), new ClientProcessLauncher(), IsApprovedGameRunning) { }

    internal MainWindow(HttpClient httpClient, IVerifiedClientProcessLauncher processLauncher,
        Func<bool> isGameProcessRunning)
    {
        http = httpClient;
        this.processLauncher = processLauncher;
        this.isGameProcessRunning = isGameProcessRunning;
        InitializeComponent();
        LauncherVersionText.Text = $"Launcher {LauncherPolicy.ProductVersion}";
        state.Changed += (_, snapshot) => Dispatcher.Invoke(() => Render(snapshot));
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureMotion();
        if (string.Equals(Environment.GetEnvironmentVariable("REVORA_LAUNCHER_VISUAL_TEST"), "1", StringComparison.Ordinal)) return;
        try
        {
            if (await new LauncherSelfUpdateCoordinator(http).StartIfRequiredAsync(Version.Parse(LauncherPolicy.ProductVersion),
                AppContext.BaseDirectory, channel, operation.Token))
            {
                Application.Current.Shutdown();
                return;
            }
        }
        catch (HttpRequestException) { diagnostics.Record(LauncherState.Offline, "self-update", "endpoint-unavailable"); }
        catch (TaskCanceledException) { diagnostics.Record(LauncherState.Offline, "self-update", "endpoint-timeout"); }
        catch (Exception error)
        {
            diagnostics.RecordException(LauncherState.Error, "self-update", error, "self-update", Classify(error));
            Transition(LauncherState.Error, "Launcher update validation failed safely.");
            return;
        }
        await InitializeAsync(null, false);
    }

    internal void ConfigureVisualTest(LauncherState visualState, bool isArabic, long completedBytes = 0, long totalBytes = 0)
    {
        if (arabic != isArabic) ToggleLanguage(this, new RoutedEventArgs());
        manifest = DesiredVisualManifest(); clientRoot = "visual-test-client";
        Render(new(visualState, visualState switch
        {
            LauncherState.Ready => isArabic ? "الملفات موثقة وجاهزة" : "All managed files are verified",
            LauncherState.Downloading => isArabic ? "تنزيل تحديث ريفورا" : "Downloading Revora update",
            LauncherState.Error => isArabic ? "تعذر التحقق من ملف مهم" : "A critical file needs repair",
            _ => "Visual validation",
        }, visualState == LauncherState.Downloading ? "data/map/map1002.dmap" : null, completedBytes, totalBytes));
        if (visualState == LauncherState.Downloading)
            RenderProgress(new("download", "data/map/map1002.dmap", 42, 12831, completedBytes, totalBytes));
        RemoteVersionText.Text = "revora-5165-stable"; LocalVersionText.Text = visualState == LauncherState.Error ? "Repair required" : "revora-5165-stable";
        ServerStatusText.Text = "Online"; PingText.Text = "Ping 42 ms";
    }

    private static DesiredStateManifest DesiredVisualManifest()
    {
        const string hash = "902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f";
        return new(2, Guid.Parse("49f67f25-86da-4f39-8ecb-df1829c15db3"), "revora-5165-stable", "stable",
            new DateTimeOffset(2026, 8, 30, 0, 0, 0, TimeSpan.Zero), LauncherPolicy.ProductVersion, hash,
            "https://playrevora.com/patch/v2/stable/release", "https://playrevora.com/patch/v2/origin/release",
            LauncherPolicy.ServerHost, LauncherPolicy.AccountPort, LauncherPolicy.GamePort, null, null,
            [new(LauncherPolicy.ClientExecutable, 2841656, hash, "revora-5165-stable", $"objects/sha256/{hash[..2]}/{hash}", "none", null, true, true, "replace", true)]);
    }

    private async Task InitializeAsync(string? selectedRoot, bool fullRepair)
    {
        CancellationTokenSource currentOperation = ReplaceOperation();
        long cycle = Interlocked.Increment(ref initializationCycle);
        bool entered = false;
        try
        {
            await operationGate.WaitAsync(currentOperation.Token); entered = true;
            Transition(LauncherState.ResolvingClient, "Resolving client folder");
            string processPath = Environment.ProcessPath ?? throw new InvalidDataException("Launcher executable path is unavailable.");
            ClientRootResult resolved = new ClientRootResolver().ResolvePortable(processPath, selectedRoot);
            if (!resolved.IsWritable) throw new UnauthorizedAccessException("The client folder is not writable. Move it to a user-owned folder; elevation is not requested.");
            clientRoot = resolved.Root;
            diagnostics.Record(LauncherState.ResolvingClient, "client-root",
                resolved.Kind switch
                {
                    ClientRootKind.NewInstallation => "new-installation",
                    ClientRootKind.RepairableClient => "repairable-client",
                    _ => "existing-client",
                });
            string control = Path.Combine(clientRoot, ".revora-launcher"); Directory.CreateDirectory(control);
            Transition(LauncherState.Recovering, "Checking interrupted updates");
            await new TransactionalPatchApplier().RecoverAsync(clientRoot, currentOperation.Token);
            Transition(LauncherState.FetchingManifest, "Fetching signed release manifest");
            byte[] envelope = await FetchManifestAsync(control, currentOperation.Token);
            try
            {
                Transition(LauncherState.VerifyingManifest, "Verifying release signature");
                VerifiedStateDocument? previous = await new VerifiedStateCache().ReadAsync(clientRoot, currentOperation.Token);
                manifest = new DesiredStateManifestVerifier(TrustedProfileKeys.Production.Span,
                    LauncherPolicy.ProductionSigningKeyId).Verify(envelope, Version.Parse(LauncherPolicy.ProductVersion),
                    previous?.ReleaseId, previous?.PublishedAtUtc);
            }
            finally { CryptographicOperations.ZeroMemory(envelope); }
            RemoteVersionText.Text = manifest.ClientBuild;
            await PrepareClientAsync(fullRepair, cycle, currentOperation.Token);
            ServerStatusText.Text = "Online";
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            PauseButton.IsEnabled = false;
            diagnostics.RecordException(state.Current.State, "initialization", error, "initialization", Classify(error));
            Transition(LauncherState.Error, Friendly(error));
        }
        finally { if (entered) operationGate.Release(); }
    }

    private async Task PrepareClientAsync(bool fullRepair, long cycle, CancellationToken token)
    {
        await ScanAsync(fullRepair, token);
        if (scan is not { IsValid: true } && !fullRepair)
        {
            automaticRepairActive = true;
            Render(state.Current);
            try { await RepairCoreAsync(token); }
            finally { automaticRepairActive = false; PauseButton.IsEnabled = false; }
        }
        if (scan is { IsValid: true } && !fullRepair)
            await LaunchVerifiedClientAsync(cycle, automatic: true);
    }

    private async Task<byte[]> FetchManifestAsync(string control, CancellationToken token)
    {
        bool testChannel = channel == LauncherChannel.Test;
        Uri endpoint = LauncherChannelResolver.ClientManifest(channel);
        string cached = Path.Combine(control, testChannel ? "test-manifest.signed.json" : "stable-manifest.signed.json");
        try
        {
            using HttpResponseMessage response = await http.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(token);
            if (bytes.Length is 0 or > LauncherPolicy.MaximumManifestBytes) throw new InvalidDataException("The manifest response is invalid.");
            await File.WriteAllBytesAsync(cached + ".new", bytes, token); File.Move(cached + ".new", cached, true);
            return bytes;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !token.IsCancellationRequested && File.Exists(cached))
        {
            diagnostics.Record(LauncherState.Offline, "manifest", "using-previously-signed-cache");
            Transition(LauncherState.Offline, "Network unavailable; validating cached signed release");
            return await File.ReadAllBytesAsync(cached, token);
        }
    }

    private async Task ScanAsync(bool fullRepair, CancellationToken token)
    {
        if (clientRoot is null || manifest is null) throw new InvalidOperationException("Client scan prerequisites are unavailable.");
        Transition(LauncherState.Scanning, fullRepair ? "Running full repair scan" : "Checking critical files");
        VerifiedStateDocument? cache = await new VerifiedStateCache().ReadAsync(clientRoot, token);
        scan = await new DesiredStateScanner().ScanAsync(clientRoot, manifest, cache, fullRepair,
            new Progress<UpdateProgress>(RenderProgress), token);
        if (scan.IsValid)
        {
            await new VerifiedStateCache().WriteAsync(clientRoot, manifest, scan.Verified, token);
            LocalVersionText.Text = manifest.ClientBuild;
            Transition(LauncherState.Ready, fullRepair ? "Full verification complete" : "Client ready");
        }
        else
        {
            LocalVersionText.Text = $"{scan.Failures.Count} file(s) need repair";
            Transition(LauncherState.RepairRequired, "Client repair required");
        }
    }

    private async Task RepairAsync()
    {
        if (clientRoot is null || manifest is null || scan is null) return;
        CancellationTokenSource currentOperation = ReplaceOperation(); progressStarted = DateTimeOffset.UtcNow;
        bool entered = false;
        try
        {
            await operationGate.WaitAsync(currentOperation.Token); entered = true;
            await RepairCoreAsync(currentOperation.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            PauseButton.IsEnabled = false;
            diagnostics.RecordException(state.Current.State, "repair", error, "repair", Classify(error));
            Transition(LauncherState.Error, Friendly(error));
        }
        finally { if (entered) operationGate.Release(); }
    }

    private async Task RepairCoreAsync(CancellationToken token)
    {
        if (clientRoot is null || manifest is null || scan is null) throw new InvalidOperationException("Client repair prerequisites are unavailable.");
        DesiredStateFile[] required = manifest.Files.Where(file => scan.Failures.Any(failure =>
            string.Equals(failure.Path, file.Path, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (required.Length == 0) { await ScanAsync(false, token); return; }
        string staging = Path.Combine(clientRoot, ".revora-launcher", "staging", manifest.ReleaseId.ToString("N"));
        Transition(LauncherState.Downloading, "Downloading verified client files",
            totalBytes: required.Sum(file => file.CompressedSize ?? file.Size));
        PauseButton.IsEnabled = true;
        await new PatchDownloadEngine(http).DownloadAsync(staging, manifest, required, pause,
            new Progress<UpdateProgress>(RenderProgress), token);
        Transition(LauncherState.VerifyingDownloads, "Verifying downloaded content");
        using (ClientPatchLock.Acquire(clientRoot))
        {
            if (isGameProcessRunning()) throw new IOException("Close the game before applying client repairs.");
            Transition(LauncherState.Applying, "Applying recoverable update");
            await new TransactionalPatchApplier().ApplyAsync(clientRoot, staging, manifest, required,
                new Progress<UpdateProgress>(RenderProgress), token);
        }
        PauseButton.IsEnabled = false;
        await ScanAsync(false, token);
    }

    private async void PrimaryAction(object sender, RoutedEventArgs e)
    {
        switch (state.Current.State)
        {
            case LauncherState.RepairRequired: await RepairAsync(); break;
            case LauncherState.Paused: pause.Resume(); Transition(LauncherState.Downloading, "Resuming verified download"); break;
            case LauncherState.Ready: await LaunchVerifiedClientAsync(null, automatic: false); break;
            case LauncherState.GameRunning: await LaunchVerifiedClientAsync(null, automatic: false); break;
            case LauncherState.Error or LauncherState.Offline: await InitializeAsync(clientRoot, false); break;
            case LauncherState.ResolvingClient: LocateClient(); break;
        }
    }

    private async Task LaunchVerifiedClientAsync(long? cycle, bool automatic)
    {
        if (clientRoot is null || manifest is null || scan is not { IsValid: true }) return;
        if (automatic && (cycle is null || !TryClaimAutomaticLaunch(cycle.Value))) return;
        await launchGate.WaitAsync();
        try
        {
            PruneExitedGames();
            Transition(LauncherState.Launching, arabic ? "تم التحقق من الملفات — تشغيل ريفورا" : "Client verified — launching Revora");
            Process launched = processLauncher.LaunchVerified(clientRoot, ToClientProfile(manifest), new(true, []));
            int launchedId = launched.Id;
            launched.Exited += (_, _) => GameExited(launchedId);
            launched.EnableRaisingEvents = true;
            games.Add(launchedId, launched);
            diagnostics.Record(LauncherState.Launching, "verified-game-launch", $"active-instances-{games.Count}");
            Transition(LauncherState.GameRunning, GameRunningText());
        }
        catch (Exception error)
        {
            diagnostics.RecordException(state.Current.State, "verified-game-launch", error,
                "verified-game-launch", Classify(error));
            if (games.Count > 0)
                Transition(LauncherState.GameRunning, arabic
                    ? $"تعذر فتح حساب إضافي؛ ما زالت {games.Count} من نوافذ اللعبة تعمل"
                    : $"An additional client could not start; {games.Count} game client(s) remain running");
            else
                Transition(LauncherState.Error, Friendly(error));
        }
        finally { launchGate.Release(); }
    }

    private bool TryClaimAutomaticLaunch(long cycle)
    {
        while (true)
        {
            long claimed = Volatile.Read(ref claimedAutomaticLaunchCycle);
            if (claimed >= cycle) return false;
            if (Interlocked.CompareExchange(ref claimedAutomaticLaunchCycle, cycle, claimed) == claimed) return true;
        }
    }

    private void GameExited(int processId) => Dispatcher.BeginInvoke(() =>
    {
        if (games.Remove(processId, out Process? tracked)) tracked.Dispose();
        PruneExitedGames();
        if (games.Count == 0)
        {
            Show(); Activate();
            if (state.Current.State is LauncherState.GameRunning or LauncherState.Launching)
                Transition(LauncherState.Ready, "All game clients closed; files remain verified");
        }
        else if (state.Current.State == LauncherState.GameRunning)
            Transition(LauncherState.GameRunning, GameRunningText());
    });

    private void PruneExitedGames()
    {
        foreach ((int id, Process process) in games.ToArray())
        {
            if (!process.HasExited) continue;
            games.Remove(id);
            process.Dispose();
        }
    }

    private string GameRunningText() => arabic
        ? $"تعمل {games.Count} من نوافذ اللعبة — يمكنك فتح حساب آخر"
        : $"{games.Count} game client(s) running — you can open another account";

    private static ClientProfile ToClientProfile(DesiredStateManifest value) => new(1, 5165, LauncherPolicy.ServerHost,
        LauncherPolicy.AccountPort, LauncherPolicy.GamePort, LauncherPolicy.ClientExecutable,
        value.Files.Select(file => new ClientFile(file.Path, file.Size, file.Sha256, null, file.Critical ? "active" : "passive", file.Required)).ToArray(),
        "production", value.ClientBuild, ["blacknull"], 19045, "rsa-testing-config-only-m1");

    private async void FullRepair(object sender, RoutedEventArgs e)
    {
        if (clientRoot is null || manifest is null) { LocateClient(); return; }
        CancellationTokenSource currentOperation = ReplaceOperation(); bool entered = false;
        try
        {
            await operationGate.WaitAsync(currentOperation.Token); entered = true;
            await ScanAsync(true, currentOperation.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            diagnostics.RecordException(state.Current.State, "full-repair", error, "full-repair", Classify(error));
            Transition(LauncherState.Error, Friendly(error));
        }
        finally { if (entered) operationGate.Release(); }
    }
    private void PauseResume(object sender, RoutedEventArgs e)
    {
        if (pause.IsPaused) { pause.Resume(); Transition(LauncherState.Downloading, "Resuming verified download"); }
        else { pause.Pause(); Transition(LauncherState.Paused, "Download paused safely"); }
    }
    private void Settings(object sender, RoutedEventArgs e)
    {
        reducedMotion = !reducedMotion; MotionLayer.Visibility = reducedMotion ? Visibility.Collapsed : Visibility.Visible;
        if (reducedMotion) motion?.Pause(this); else motion?.Resume(this);
        OperationText.Text = reducedMotion ? "Reduced motion enabled" : "Reduced motion disabled";
    }
    private void LocateClient()
    {
        var dialog = new OpenFolderDialog { Title = "Select a Revora 5165 client folder", Multiselect = false };
        if (dialog.ShowDialog(this) == true) _ = InitializeAsync(dialog.FolderName, false);
    }
    private void OpenFolder(object sender, RoutedEventArgs e)
    {
        if (clientRoot is null) { LocateClient(); return; }
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { clientRoot } });
    }
    private async void ExportDiagnostics(object sender, RoutedEventArgs e)
    {
        diagnosticsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Revora Diagnostics");
        string path = await diagnostics.ExportAsync(diagnosticsDirectory, LauncherPolicy.ProductVersion,
            manifest?.ReleaseId, manifest?.ClientBuild, CancellationToken.None);
        OperationText.Text = $"Sanitized diagnostics exported: {path}";
        OpenDiagnosticsButton.IsEnabled = true;
        try { Clipboard.SetText(path); } catch { }
    }
    private void OpenDiagnosticsFolder(object sender, RoutedEventArgs e)
    {
        if (diagnosticsDirectory is null || !Directory.Exists(diagnosticsDirectory)) return;
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true,
            ArgumentList = { diagnosticsDirectory } });
    }
    private void ToggleLanguage(object sender, RoutedEventArgs e)
    {
        arabic = !arabic; RootGrid.FlowDirection = arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        LanguageButton.Content = arabic ? "English" : "العربية"; PanelHeading.Text = arabic ? "حالة ملفات اللعبة" : "Client status";
        FullRepairButton.Content = arabic ? "إصلاح شامل" : "Full Repair";
        SettingsButton.Content = arabic ? "تقليل الحركة" : "Settings"; FolderButton.Content = arabic ? "فتح المجلد" : "Open Folder";
        DiagnosticsButton.Content = arabic ? "تصدير التشخيص" : "Diagnostics";
        OpenDiagnosticsButton.Content = arabic ? "فتح مجلد التشخيص" : "Open Diagnostics Folder";
        Render(state.Current);
    }

    private void Render(LauncherStateSnapshot snapshot)
    {
        string action = LauncherStateMachine.PrimaryAction(snapshot.State, clientRoot is not null);
        PrimaryButton.Content = arabic ? TranslateAction(action) : action;
        PrimaryButton.IsEnabled = snapshot.State is LauncherState.RepairRequired or LauncherState.Paused or LauncherState.Ready or LauncherState.GameRunning or LauncherState.Error or LauncherState.Offline or LauncherState.ResolvingClient;
        FullRepairButton.IsEnabled = snapshot.State != LauncherState.GameRunning;
        if (automaticRepairActive && snapshot.State == LauncherState.RepairRequired) PrimaryButton.IsEnabled = false;
        OperationText.Text = snapshot.State == LauncherState.GameRunning ? GameRunningText() : snapshot.Operation;
        HeroStatus.Text = snapshot.State switch { LauncherState.Ready => arabic ? "جاهز لبدء المغامرة" : "Ready for your journey", LauncherState.GameRunning => arabic ? "العالم ينتظرك" : "Your world is waiting", LauncherState.Error => arabic ? "توقف آمن" : "Stopped safely", _ => arabic ? "تجهيز رحلتك" : "Preparing your journey" };
        PauseButton.Content = pause.IsPaused ? (arabic ? "استئناف" : "Resume") : (arabic ? "إيقاف مؤقت" : "Pause");
        diagnostics.Record(snapshot.State, "state", snapshot.Operation, snapshot.RelativePath);
    }

    private void RenderProgress(UpdateProgress value)
    {
        CurrentFileText.Text = value.RelativePath;
        if (value.Phase == "scan-retry")
        {
            TotalProgress.Maximum = Math.Max(1, value.TotalFiles);
            TotalProgress.Value = Math.Min(value.FilesCompleted, value.TotalFiles);
            ProgressPercent.Text = $"Read retry {value.FilesCompleted}/{value.TotalFiles}";
            TransferText.Text = "Waiting for a temporary file lock";
            SpeedText.Text = "—";
            if (value.HResult is { } hResult && value.NativeError is { } nativeError &&
                value.Operation is { } fileOperation && value.AttemptCount is { } attemptCount)
                diagnostics.RecordFileAccess(LauncherState.Scanning, "sharing-violation-retry", value.RelativePath,
                    $"attempt-{value.FilesCompleted}-of-{value.TotalFiles}", hResult, nativeError, fileOperation,
                    attemptCount, value.LockTransient == true, value.LockExhausted == true);
            return;
        }
        TotalProgress.Maximum = Math.Max(1, value.TotalBytes);
        TotalProgress.Value = Math.Min(value.BytesDownloaded, TotalProgress.Maximum);
        double fraction = value.TotalBytes <= 0 ? 0 : (double)value.BytesDownloaded / value.TotalBytes;
        ProgressPercent.Text = $"{fraction:P0}";
        TransferText.Text = value.Phase == "scan"
            ? $"Scanning file {Math.Min(value.FilesCompleted + 1, value.TotalFiles)} of {value.TotalFiles}"
            : $"{FormatBytes(value.BytesDownloaded)} / {FormatBytes(value.TotalBytes)}";
        double seconds = Math.Max(0.1, (DateTimeOffset.UtcNow - progressStarted).TotalSeconds); double speed = value.BytesDownloaded / seconds;
        SpeedText.Text = value.Phase == "scan" ? "Integrity scan" : speed > 0 ? $"{FormatBytes((long)speed)}/s" : "—";
    }

    private void Transition(LauncherState next, string text, string? relativePath = null, long completedBytes = 0, long totalBytes = 0)
    {
        if (next is LauncherState.ResolvingClient or LauncherState.Recovering or LauncherState.FetchingManifest or
            LauncherState.VerifyingManifest or LauncherState.Scanning)
            ResetProgress(next == LauncherState.Scanning ? "Starting integrity scan" : "No file operation");
        if (state.Current.State == LauncherState.Starting && next != LauncherState.ResolvingClient) state.Transition(LauncherState.ResolvingClient, "Resolving client folder");
        state.Transition(next, text, relativePath, completedBytes, totalBytes);
    }
    private void ResetProgress(string current)
    {
        progressStarted = DateTimeOffset.UtcNow;
        CurrentFileText.Text = current;
        TotalProgress.Minimum = 0;
        TotalProgress.Maximum = 1;
        TotalProgress.Value = 0;
        ProgressPercent.Text = "0%";
        TransferText.Text = "0 B / 0 B";
        SpeedText.Text = "—";
    }
    private CancellationTokenSource ReplaceOperation()
    {
        CancellationTokenSource previous = operation;
        operation = new();
        previous.Cancel();
        previous.Dispose();
        return operation;
    }
    private void CancelOperation() { operation.Cancel(); operation.Dispose(); }
    private static string Classify(Exception error) => error switch { CryptographicException => "signature-or-hash-validation", UnauthorizedAccessException => "access-denied", HttpRequestException => "network", InvalidDataException => "policy-validation", IOException => "filesystem-or-lock", _ => "unexpected" };
    private static string Friendly(Exception error) => error switch { UnauthorizedAccessException => "Move the client to a writable user-owned folder. Administrator access is not requested.", HttpRequestException => "The signed patch service is temporarily unavailable.", CryptographicException => "Signed content validation failed. No client file was changed.", InvalidDataException => error.Message, IOException => error.Message, _ => "The launcher stopped safely. Export diagnostics for support." };
    private static string FormatBytes(long value) => value switch { >= 1L << 30 => $"{value / (double)(1L << 30):0.00} GB", >= 1L << 20 => $"{value / (double)(1L << 20):0.0} MB", >= 1L << 10 => $"{value / (double)(1L << 10):0.0} KB", _ => $"{value} B" };
    private static string TranslateAction(string action) => action switch { "Locate Client" => "تحديد ملفات اللعبة", "Install & Repair" => "تثبيت وإصلاح", "Check Files" => "فحص الملفات", "Repair Client" => "إصلاح اللعبة", "Update Revora" => "تحديث ريفورا", "Resume" => "استئناف", "Play Revora" => "ابدأ ريفورا", "Open Another Account" => "فتح حساب آخر", "Maintenance" => "صيانة", "Retry" => "إعادة المحاولة", _ => "يرجى الانتظار" };

    private void ConfigureMotion()
    {
        motion = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
        var one = new DoubleAnimation(-45, 90, TimeSpan.FromSeconds(18)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        Storyboard.SetTarget(one, FogOne); Storyboard.SetTargetProperty(one, new PropertyPath("X")); motion.Children.Add(one);
        var two = new DoubleAnimation(55, -70, TimeSpan.FromSeconds(24)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        Storyboard.SetTarget(two, FogTwo); Storyboard.SetTargetProperty(two, new PropertyPath("X")); motion.Children.Add(two); motion.Begin(this, true);
    }
    private void WindowStateChanged(object? sender, EventArgs e) { if (motion is null || reducedMotion) return; if (WindowState == WindowState.Minimized || !IsVisible) motion.Pause(this); else motion.Resume(this); }
    private void TitleBarDrag(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private static HttpClient CreateHttpClient() => new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    }) { Timeout = TimeSpan.FromMinutes(15) };

    private static bool IsApprovedGameRunning()
    {
        Process[] candidates = Process.GetProcessesByName(
            Path.GetFileNameWithoutExtension(LauncherPolicy.ClientExecutable));
        try { return candidates.Any(candidate => !candidate.HasExited); }
        finally { foreach (Process candidate in candidates) candidate.Dispose(); }
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelOperation();
        foreach (Process game in games.Values) game.Dispose();
        games.Clear();
        launchGate.Dispose();
        http.Dispose();
        base.OnClosed(e);
    }
}
