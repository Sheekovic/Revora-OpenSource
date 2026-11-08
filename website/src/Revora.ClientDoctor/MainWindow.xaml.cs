using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using Microsoft.Win32;
using Revora.ClientDoctor.Core;
using Revora.Launcher.Core;

namespace Revora.ClientDoctor;

internal sealed record CapturedBuildRequest(string SourceRoot, string Destination, ClientProfile Profile, DoctorInventory Inventory, CancellationToken Token);

public partial class MainWindow : Window
{
    private ClientProfile? profile;
    private DoctorInventory? inventory;
    private CancellationTokenSource? buildCancellation;
    internal string? LastBuildReportPath { get; private set; }
    public MainWindow() => InitializeComponent();

    private void SelectFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select the untrusted legacy client folder" };
        if (dialog.ShowDialog() == true) SourcePath.Text = dialog.FolderName;
    }

    private async void Scan(object sender, RoutedEventArgs e)
    {
        ScanButton.IsEnabled = false;
        StateText.Text = "Scanning / جارٍ الفحص";
        try
        {
            string profilePath = Path.Combine(AppContext.BaseDirectory, "revora-testing-profile.json");
            byte[] envelope = await File.ReadAllBytesAsync(profilePath);
            profile = new ManifestVerifier(TrustedProfileKeys.Testing.ToArray(), LauncherPolicy.TestingSigningKeyId).VerifyAndRead(envelope);
            inventory = await new SourceInventory().InspectAsync(SourcePath.Text, profile, CancellationToken.None);
            string report = SanitizedReport.Create(inventory, profile.BuildId, ToolVersion());
            string reportPath = Path.Combine(ReportDirectory(), $"doctor-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            await File.WriteAllTextAsync(reportPath, report);
            Results.Text = report;
            BuildButton.IsEnabled = inventory.RecognizedDonor;
            StateText.Text = inventory.RecognizedDonor ? $"Recognized {inventory.PatchEvidence}; reusable passive assets found" : $"Unsafe or unsupported donor: {inventory.PatchEvidence}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or CryptographicException)
        {
            Results.Text = error.Message;
            StateText.Text = "Blocked with exact reason / متوقف بسبب واضح";
        }
        finally { ScanButton.IsEnabled = true; }
    }

    private async void Build(object sender, RoutedEventArgs e)
    {
        if (profile is null || inventory is null || !inventory.RecognizedDonor) return;
        var dialog = new OpenFolderDialog { Title = "Select a safe parent folder for Revora Client Clean" };
        if (dialog.ShowDialog() != true) return;

        buildCancellation = new CancellationTokenSource();
        CapturedBuildRequest request = CaptureBuildRequest(Path.Combine(dialog.FolderName, "Revora Client Clean"), buildCancellation.Token);
        await ExecuteCapturedBuildAsync(request, static async (captured, progress) =>
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            return await new ClientConstructor(http).BuildAsync(captured.SourceRoot, captured.Destination, captured.Profile,
                captured.Inventory, captured.Token, progress);
        });
    }

    internal CapturedBuildRequest CaptureBuildRequest(string destination, CancellationToken token)
    {
        Dispatcher.VerifyAccess();
        ClientProfile selectedProfile = profile ?? throw new InvalidOperationException("A signed profile must be scanned before building.");
        DoctorInventory selectedInventory = inventory ?? throw new InvalidOperationException("A donor inventory must be completed before building.");
        string sourceRoot = SourcePath.Text;
        string selectedDestination = destination;
        return new(sourceRoot, selectedDestination, selectedProfile, selectedInventory, token);
    }

    internal async Task ExecuteCapturedBuildAsync(CapturedBuildRequest request,
        Func<CapturedBuildRequest, IProgress<BuildProgress>, Task<BuildResult>> worker)
    {
        Dispatcher.VerifyAccess();
        BuildButton.IsEnabled = false; ScanButton.IsEnabled = false; CancelButton.IsEnabled = true;
        StateText.Text = "Constructing and verifying / جارٍ الإنشاء والتحقق";
        SynchronizationContext uiContext = SynchronizationContext.Current ?? new System.Windows.Threading.DispatcherSynchronizationContext(Dispatcher);
        using var progress = new ThrottledUiProgress(uiContext, UpdateProgressUi);
        try
        {
            BuildResult result = await Task.Run(() => worker(request, progress));
            progress.Flush();
            string report = SanitizedReport.CreateBuildSuccess(result, request.Profile.BuildId, ToolVersion());
            LastBuildReportPath = await SaveBuildReportAsync(report);
            Results.Text = report;
            StateText.Text = "Reconstruction verified; launch remains disabled pending security review.";
        }
        catch (ClientBuildException error)
        {
            string report = SanitizedReport.CreateBuildFailure(error, request.Profile.BuildId, ToolVersion());
            LastBuildReportPath = await SaveBuildReportAsync(report);
            Results.Text = report;
            StateText.Text = error.Category == "Cancelled" ? "Build cancelled safely / تم الإلغاء بأمان" : "Blocked with exact reason / متوقف بسبب واضح";
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            var failure = new ClientBuildException("unexpected", null, error, cleanupSucceeded: false);
            string report = SanitizedReport.CreateBuildFailure(failure, request.Profile.BuildId, ToolVersion());
            LastBuildReportPath = await SaveBuildReportAsync(report);
            Results.Text = report;
            StateText.Text = "Unexpected failure handled safely / تمت معالجة فشل غير متوقع بأمان";
        }
        finally
        {
            buildCancellation?.Dispose(); buildCancellation = null;
            CancelButton.IsEnabled = false; ScanButton.IsEnabled = true; BuildButton.IsEnabled = request.Inventory.RecognizedDonor;
        }
    }

    private void UpdateProgressUi(BuildProgress item)
    {
        Dispatcher.VerifyAccess();
        ProgressText.Text = $"{item.Phase}: {item.FilesCompleted:N0}/{item.TotalFiles:N0} files · {item.BytesDownloaded:N0}/{item.TotalDownloadBytes:N0} bytes · {item.RelativePath}";
        BuildProgress.Maximum = Math.Max(1, item.TotalFiles);
        BuildProgress.Value = Math.Min(item.FilesCompleted, BuildProgress.Maximum);
    }

    private void Cancel(object sender, RoutedEventArgs e) => buildCancellation?.Cancel();
    internal void CancelForTest() => buildCancellation?.Cancel();
    internal void SetCapturedStateForTest(string sourceRoot, ClientProfile selectedProfile, DoctorInventory selectedInventory)
    {
        Dispatcher.VerifyAccess(); SourcePath.Text = sourceRoot; profile = selectedProfile; inventory = selectedInventory;
    }
    internal (bool Scan, bool Build, bool Cancel) ButtonStateForTest() => (ScanButton.IsEnabled, BuildButton.IsEnabled, CancelButton.IsEnabled);

    private static string ToolVersion() => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    internal static string ReportDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Revora", "ClientDoctor", "reports");
    private static async Task<string> SaveBuildReportAsync(string report)
    {
        string directory = ReportDirectory(); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"doctor-build-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.json");
        await File.WriteAllTextAsync(path, report); return path;
    }

    private sealed class ThrottledUiProgress(SynchronizationContext context, Action<BuildProgress> render) : IProgress<BuildProgress>, IDisposable
    {
        private readonly object gate = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private BuildProgress? latest;
        private bool pending;
        private long lastRenderedMilliseconds = -100;

        public void Report(BuildProgress value)
        {
            lock (gate)
            {
                latest = value;
                if (pending || clock.ElapsedMilliseconds - lastRenderedMilliseconds < 100) return;
                pending = true;
            }
            context.Post(_ => RenderLatest(), null);
        }

        public void Flush()
        {
            lock (gate) { if (pending || latest is null) return; pending = true; }
            context.Post(_ => RenderLatest(), null);
        }

        private void RenderLatest()
        {
            BuildProgress? value;
            lock (gate) { value = latest; pending = false; lastRenderedMilliseconds = clock.ElapsedMilliseconds; }
            if (value is not null) render(value);
        }
        public void Dispose() { }
    }
}
