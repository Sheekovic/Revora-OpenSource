using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Net.Http;
using PlayRevora.Launcher.Models;
using PlayRevora.Launcher.Services;

namespace PlayRevora.Launcher.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly DownloadService downloads;
    private readonly UpdateService updates;
    private readonly GraphicsSettingsService graphicsService;
    private readonly DualProfileLauncherService profileLauncher;
    private readonly ServerStatusService serverStatus;
    private readonly OfficialRegistrationLinkService registrationLink;
    private readonly DispatcherTimer announcementTimer;
    private readonly DispatcherTimer statusTimer;
    private readonly IReadOnlyList<Announcement> announcements;
    private string status = "Ready to Conquer";
    private string versionLabel = "Local version: checking…";
    private double progress;
    private double currentProgress;
    private string transferLabel = "Awaiting orders";
    private bool busy;
    private bool settingsOpen;
    private Announcement activeAnnouncement;
    private int announcementIndex;
    private string serverStatusText = "CHECKING";
    private string serverDetail = "REVORA • contacting realm";
    private Brush serverStatusBrush = Brushes.Goldenrod;
    private string latencyText = "— ms";
    private string selectedGateway = "REVORA PRIME";
    private ProfileSelectionMode selectedProfileMode;
    private string resolvedProfile = "Not resolved";
    private string profileSelectionReason = "Verifying signed client profiles...";
    private string profileVerificationState = "NOT VERIFIED";
    private bool profileReady;
    private string launchDisabledExplanation = "Awaiting trusted client verification.";

    private MainWindowViewModel(LauncherConfig config)
    {
        IsOwnerTestBuild = string.Equals(config.BuildChannel, "owner-test", StringComparison.OrdinalIgnoreCase);
        RenderBackendDisplay = config.RenderBackendLabel;
        downloads = new DownloadService();
        updates = new UpdateService(config, downloads);
        graphicsService = new GraphicsSettingsService(config);
        Graphics = graphicsService.Load();
        profileLauncher = new DualProfileLauncherService(config);
        profileLauncher.LaunchStatusChanged += message => Application.Current.Dispatcher.Invoke(() => Status = message);
        selectedProfileMode = profileLauncher.Preferences.ProfileMode;
        serverStatus = new ServerStatusService(config.StatusEndpoint);
        registrationLink = new OfficialRegistrationLinkService(new WindowsExternalUriOpener());
        announcements =
        [
            new("DRAGON REALM AWAKENS", "New dungeons. Epic rewards. The legend continues.", "MAJOR UPDATE", "pack://application:,,,/Assets/news-dragon-realm.png"),
            new("CELESTIAL FESTIVAL", "Earn exclusive mounts and rare treasures!", "LIMITED EVENT", "pack://application:,,,/Assets/news-celestial-festival.png"),
            new("SHADOWFALL TRIALS", "Test your might in the ultimate challenge.", "NEW CONTENT", "pack://application:,,,/Assets/news-shadowfall-trials.png")
        ];
        activeAnnouncement = announcements[0];
        announcementTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        announcementTimer.Tick += (_, _) => ActiveAnnouncement = announcements[++announcementIndex % announcements.Count];
        announcementTimer.Start();
        statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        statusTimer.Tick += async (_, _) => await RefreshServerStatusAsync();
        statusTimer.Start();
        UpdateCommand = new AsyncCommand(() => RunAsync(async (report, token) =>
        {
            try
            {
                LocalVersion version = await updates.UpdateAsync(report, token);
                VersionLabel = $"Local version: {version.Version}";
                await profileLauncher.InitializeAsync(token);
                ApplyProfileSelection();
            }
            catch
            {
                profileLauncher.RequireRecovery(); ApplyProfileSelection(); throw;
            }
        }), () => !Busy);
        RepairCommand = new AsyncCommand(() => RunAsync(async (report, token) =>
        {
            report.Report(new("Verifying client", 0, 0, "Signed manifest"));
            await profileLauncher.FullVerifyAsync(token);
            report.Report(new("Ready", 100, 100, "Verification complete"));
            ApplyProfileSelection();
        }), () => !Busy);
        SaveGraphicsCommand = new AsyncCommand(() => RunAsync(async (_, token) =>
        {
            await graphicsService.SaveAsync(Graphics, token);
            Status = "Graphics settings saved";
        }), () => !Busy);
        PlayCommand = new AsyncCommand(() => RunAsync(async (_, token) =>
        {
            await graphicsService.SaveAsync(Graphics, token);
            LaunchResult result = await profileLauncher.LaunchAsync(token);
            Status = result.UserMessage;
        }), () => !Busy && ProfileReady);
        ToggleSettingsCommand = new AsyncCommand(() =>
        {
            SettingsOpen = !SettingsOpen;
            return Task.CompletedTask;
        }, () => true);
        ToggleProxyCommand = new AsyncCommand(() => RunAsync(async (_, token) =>
        {
            Graphics.EnhancedGraphics = !Graphics.EnhancedGraphics;
            OnPropertyChanged(nameof(Graphics));
            await graphicsService.SaveAsync(Graphics, token);
            Status = Graphics.EnhancedGraphics ? "Display Proxy enabled" : "Display Proxy disabled";
        }), () => !Busy);
        ResetProfileCommand = new AsyncCommand(async () =>
        {
            await profileLauncher.ResetAutomaticAsync(CancellationToken.None);
            selectedProfileMode = ProfileSelectionMode.Automatic;
            OnPropertyChanged(nameof(SelectedProfileMode));
            ApplyProfileSelection();
        }, () => !Busy);
        RegisterCommand = new AsyncCommand(() =>
        {
            try
            {
                bool opened = registrationLink.TryOpen(new Uri(OfficialRegistrationLinkService.ApprovedRegistrationUrl));
                Status = opened ? "Registration opened in your browser" : "Registration page is already opening";
            }
            catch
            {
                Status = "Could not open the registration page. Please try again.";
            }
            return Task.CompletedTask;
        }, () => !Busy);
        _ = InitializeLauncherStateAsync();
        _ = RefreshServerStatusAsync();
    }

    public static MainWindowViewModel Create(string baseDirectory)
    {
        LauncherConfig config = JsonFiles.Read<LauncherConfig>(Path.Combine(baseDirectory, "launcher.json"));
        config.GameRoot = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(config.GameRoot), baseDirectory);
        return new MainWindowViewModel(config);
    }

    public GraphicsSettings Graphics { get; }
    public bool IsOwnerTestBuild { get; }
    public string RenderBackendDisplay { get; }
    public Visibility OwnerTestVisibility => IsOwnerTestBuild ? Visibility.Visible : Visibility.Collapsed;
    public IReadOnlyList<string> DisplayModes { get; } = ["Borderless Windowed", "Standard Windowed"];
    public IReadOnlyList<string> Resolutions { get; } = ["Auto / Native", "1920 × 1080", "2560 × 1440 (2K)", "3840 × 2160 (4K)"];
    // The branded release currently has one approved runtime: the static DX9
    // Revora derivative. Do not expose or silently select an original client.
    public IReadOnlyList<ProfileSelectionMode> ProfileModes { get; } = [ProfileSelectionMode.DirectX9];
    public ProfileSelectionMode SelectedProfileMode
    {
        get => selectedProfileMode;
        set { if (Set(ref selectedProfileMode, value)) _ = ChangeProfileModeAsync(value); }
    }
    public string ResolvedProfile { get => resolvedProfile; private set => Set(ref resolvedProfile, value); }
    public string ProfileSelectionReason { get => profileSelectionReason; private set => Set(ref profileSelectionReason, value); }
    public string ProfileVerificationState { get => profileVerificationState; private set => Set(ref profileVerificationState, value); }
    public bool ProfileReady { get => profileReady; private set { if (Set(ref profileReady, value)) CommandManager.InvalidateRequerySuggested(); } }
    public string LaunchDisabledExplanation { get => launchDisabledExplanation; private set => Set(ref launchDisabledExplanation, value); }
    public ICommand UpdateCommand { get; }
    public ICommand RepairCommand { get; }
    public ICommand SaveGraphicsCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand ToggleSettingsCommand { get; }
    public ICommand ToggleProxyCommand { get; }
    public ICommand ResetProfileCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand LaunchGameCommand => PlayCommand;
    public string Status { get => status; private set => Set(ref status, value); }
    public string VersionLabel { get => versionLabel; private set => Set(ref versionLabel, value); }
    public double Progress { get => progress; private set { if (Set(ref progress, value)) OnPropertyChanged(nameof(ProgressText)); } }
    public double CurrentProgress { get => currentProgress; private set { if (Set(ref currentProgress, value)) { OnPropertyChanged(nameof(CurrentProgressText)); OnPropertyChanged(nameof(CurrentTelemetryText)); } } }
    public string TransferLabel { get => transferLabel; private set { if (Set(ref transferLabel, value)) OnPropertyChanged(nameof(CurrentTelemetryText)); } }
    public string ProgressText => $"{Progress:0}%";
    public string CurrentProgressText => $"{CurrentProgress:0}%";
    public string CurrentTelemetryText => $"{CurrentProgressText}  •  {TransferLabel}";
    public Announcement ActiveAnnouncement { get => activeAnnouncement; private set => Set(ref activeAnnouncement, value); }
    public bool SettingsOpen { get => settingsOpen; private set { if (Set(ref settingsOpen, value)) OnPropertyChanged(nameof(SettingsVisibility)); } }
    public Visibility SettingsVisibility => SettingsOpen ? Visibility.Visible : Visibility.Collapsed;
    public string ServerStatusText { get => serverStatusText; private set => Set(ref serverStatusText, value); }
    public string ServerDetail { get => serverDetail; private set => Set(ref serverDetail, value); }
    public Brush ServerStatusBrush { get => serverStatusBrush; private set => Set(ref serverStatusBrush, value); }
    public string LatencyText { get => latencyText; private set => Set(ref latencyText, value); }
    public IReadOnlyList<string> Gateways { get; } = ["REVORA PRIME", "REVORA BACKUP"];
    public string SelectedGateway { get => selectedGateway; set => Set(ref selectedGateway, value); }
    public bool Busy { get => busy; private set { if (Set(ref busy, value)) CommandManager.InvalidateRequerySuggested(); } }

    private async Task LoadVersionAsync()
    {
        try { VersionLabel = $"Local version: {(await updates.ReadLocalVersionAsync(CancellationToken.None)).Version}"; }
        catch (Exception exception) { Status = exception.Message; }
    }

    private async Task InitializeLauncherStateAsync()
    {
        await InitializeProfilesAsync();
        await LoadVersionAsync();
        if (profileLauncher.Selection.Profile is null) return;
        try
        {
            Status = "Checking for updates";
            IReadOnlyList<SignedPatchReference> patches = await updates.DiscoverAsync(CancellationToken.None);
            profileLauncher.ApplyUpdatePolicy(patches);
            ApplyProfileSelection();
            Status = patches.Any(patch => patch.Mandatory) ? "Mandatory update required"
                : patches.Count == 0 ? "Ready" : "Update available";
        }
        catch (Exception) { profileLauncher.MarkUpdateCheckFailed(); ApplyProfileSelection(); Status = "Update check unavailable"; }
    }

    private async Task InitializeProfilesAsync()
    {
        try
        {
            await profileLauncher.InitializeAsync(CancellationToken.None);
            selectedProfileMode = profileLauncher.Preferences.ProfileMode;
            OnPropertyChanged(nameof(SelectedProfileMode));
            ApplyProfileSelection();
        }
        catch (Exception)
        {
            ProfileReady = false;
            ResolvedProfile = "Unavailable";
            ProfileVerificationState = "VERIFICATION FAILED";
            ProfileSelectionReason = "Signed client verification failed. See launcher diagnostics.";
        }
    }

    private async Task ChangeProfileModeAsync(ProfileSelectionMode mode)
    {
        try { await profileLauncher.SetModeAsync(mode, CancellationToken.None); ApplyProfileSelection(); }
        catch (Exception)
        {
            ProfileReady = false;
            ProfileVerificationState = "VERIFICATION FAILED";
            ProfileSelectionReason = "The client profile preference could not be applied.";
        }
    }

    private void ApplyProfileSelection()
    {
        ProfileSelectionResult selection = profileLauncher.Selection;
        ProfileReadiness readiness = profileLauncher.Readiness;
        ProfileReady = readiness.LaunchAuthorized && selection.Profile is not null;
        ResolvedProfile = selection.Profile?.DisplayName ?? SelectedProfileMode switch
        {
            ProfileSelectionMode.DirectX8 => "DirectX 8 preference",
            ProfileSelectionMode.DirectX9 => "DirectX 9 preference",
            _ => "Automatic preference"
        };
        ProfileSelectionReason = readiness.Message;
        ProfileVerificationState = readiness.Heading.ToUpperInvariant();
        LaunchDisabledExplanation = ProfileReady ? "Critical launch checks passed. Repair scan is optional."
            : "START GAME is disabled — " + readiness.Heading.ToLowerInvariant() + ".";
    }

    private async Task RunAsync(Func<IProgress<OperationProgress>, CancellationToken, Task> operation)
    {
        if (Busy) return;
        Busy = true;
        Progress = 0;
        CurrentProgress = 0;
        try
        {
            var reporter = new Progress<OperationProgress>(item =>
            {
                Status = item.Message;
                if (item.Percent > 0) Progress = item.Percent;
                CurrentProgress = item.CurrentPercent;
                TransferLabel = item.RateText;
            });
            await operation(reporter, CancellationToken.None);
        }
        catch (Exception exception) { Status = "Failed: " + exception.Message; }
        finally { Busy = false; }
    }

    private async Task RefreshServerStatusAsync()
    {
        ServerStatusSnapshot snapshot = await serverStatus.CheckAsync(CancellationToken.None);
        ServerStatusText = snapshot.Online ? "ONLINE"
            : snapshot.HealthCategory == "realm_reported_offline" ? "OFFLINE" : "STATUS UNAVAILABLE";
        ServerStatusBrush = snapshot.Online ? new SolidColorBrush(Color.FromRgb(53, 215, 209))
            : new SolidColorBrush(Color.FromRgb(159, 43, 43));
        string population = snapshot.PlayersOnline is int players ? $" • {players:N0} adventurers" : "";
        ServerDetail = snapshot.HealthCategory == "healthy" || snapshot.HealthCategory == "realm_reported_offline"
            ? $"{snapshot.ServerName} • {snapshot.PingMilliseconds} ms{population}"
            : "REVORA • HTTPS status unavailable";
        LatencyText = $"{snapshot.PingMilliseconds} ms";
    }

    public void Dispose()
    {
        announcementTimer.Stop();
        statusTimer.Stop();
        serverStatus.Dispose();
        downloads.Dispose();
    }
}

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => canExecute();
    public async void Execute(object? parameter) => await execute();
}
