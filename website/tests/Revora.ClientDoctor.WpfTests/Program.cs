using System.IO;
using System.Security.Cryptography;
using System.Windows.Threading;
using Revora.ClientDoctor;
using Revora.ClientDoctor.Core;
using Revora.Launcher.Core;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("This WPF regression must run on Windows.");
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("The regression must start on an STA thread.");
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

var window = new MainWindow();
string source = Path.Combine(Path.GetTempPath(), "revora-wpf-source");
ClientProfile profile = Profile();
var inventory = new DoctorInventory("version.dat=5165", 5165, true, [], 0, false);
window.SetCapturedStateForTest(source, profile, inventory);
using var cancellation = new CancellationTokenSource();
CapturedBuildRequest request = window.CaptureBuildRequest(Path.Combine(Path.GetTempPath(), "Revora Client Clean"), cancellation.Token);
int uiThread = Environment.CurrentManagedThreadId;
int workerThread = uiThread;

Task handledFailure = window.ExecuteCapturedBuildAsync(request, (captured, _) =>
{
    workerThread = Environment.CurrentManagedThreadId;
    Assert(captured.SourceRoot == source && captured.Destination.EndsWith("Revora Client Clean", StringComparison.Ordinal), "Captured values changed.");
    Assert(captured.GetType().GetProperties().All(property => !typeof(System.Windows.Threading.DispatcherObject).IsAssignableFrom(property.PropertyType)), "Worker request contains a DispatcherObject.");
    throw new InvalidOperationException("Simulated normal unexpected worker failure.");
});
Pump(handledFailure);
Assert(workerThread != uiThread, "Worker executed on the UI thread.");
Assert(window.LastBuildReportPath is not null && File.Exists(window.LastBuildReportPath), "Handled failure report was not written.");
string failureReport = File.ReadAllText(window.LastBuildReportPath!);
Assert(failureReport.Contains("\"exceptionCategory\": \"Unexpected\"", StringComparison.Ordinal) && !failureReport.Contains("Simulated normal", StringComparison.Ordinal), "Failure report leaked or misclassified details.");
Assert(window.ButtonStateForTest() == (true, true, false), "Buttons were not restored after handled failure.");

using var cancellation2 = new CancellationTokenSource();
CapturedBuildRequest request2 = window.CaptureBuildRequest(Path.Combine(Path.GetTempPath(), "Revora Client Clean 2"), cancellation2.Token);
Task cancelled = window.ExecuteCapturedBuildAsync(request2, async (captured, _) =>
{
    try { await Task.Delay(Timeout.InfiniteTimeSpan, captured.Token); }
    catch (OperationCanceledException error) { throw new ClientBuildException("copy", null, error, true); }
    throw new InvalidOperationException("Unreachable");
});
cancellation2.Cancel();
Pump(cancelled);
Assert(window.ButtonStateForTest() == (true, true, false), "Buttons were not restored after cancellation.");
Assert(window.LastBuildReportPath is not null && File.ReadAllText(window.LastBuildReportPath!).Contains("\"exceptionCategory\": \"Cancelled\"", StringComparison.Ordinal), "Cancellation was not reported safely.");

        Console.WriteLine("PASS WPF Build captures UI state before worker execution");
        Console.WriteLine("PASS WPF handled failure reports without terminating application");
        Console.WriteLine("PASS WPF cancellation restores controls");
        return 0;
    }

static void Pump(Task task)
{
    var frame = new DispatcherFrame();
    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
    Dispatcher.PushFrame(frame);
    task.GetAwaiter().GetResult();
}

static ClientProfile Profile()
{
    byte[] bytes = "MZtest"u8.ToArray();
    return new(1, 5165, "playrevora.com", 9958, 9959, "Conquer.exe",
        [new("Conquer.exe", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), "https://playrevora.com/object", "active")],
        "testing", "doctor-reconstruction-5165-test-2c", ["blacknull"], 19045, "disabled");
}

static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
