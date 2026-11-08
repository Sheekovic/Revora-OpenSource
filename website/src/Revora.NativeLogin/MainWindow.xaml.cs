using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net.Sockets;
using System.Windows;
using Microsoft.Win32;
using Revora.Launcher.Core;
using Revora.NativeLogin.Core;

namespace Revora.NativeLogin;

public partial class MainWindow : Window
{
    private CancellationTokenSource? cancellation;
    private string? preparedRoot;
    private Process? client;
    private LoopbackProtocolProbe? probe;

    public MainWindow() { InitializeComponent(); Closed += async (_, _) => await StopAsync(); }

    private void Browse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select verified Revora Client Clean" };
        if (dialog.ShowDialog() == true) ClientPath.Text = dialog.FolderName;
    }

    private async void Prepare(object sender, RoutedEventArgs e)
    {
        string source = ClientPath.Text;
        if (string.IsNullOrWhiteSpace(source)) { Status.Text = "Select the verified Test 2c client first."; return; }
        string destination = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source))!, "Revora Client Test 3");
        PrepareButton.IsEnabled = false; CancelButton.IsEnabled = true; cancellation = new();
        try
        {
            ClientProfile input = await ReadProfileAsync("revora-test2c-profile.json");
            ClientProfile output = await ReadProfileAsync("revora-test3-profile.json");
            var progress = new Progress<PreparationProgress>(item => { Progress.Maximum = item.Total; Progress.Value = item.Completed; Status.Text = $"{item.Phase}: {item.Completed:N0}/{item.Total:N0}"; });
            PreparationResult result = await Task.Run(() => new Test3Preparation().PrepareAsync(source, destination, input, output,
                Asset("conquer-test3-patch.json"), Asset("Server.test3.dat"), cancellation.Token, progress));
            preparedRoot = result.Destination;
            Status.Text = $"Prepared and signed-profile verified: {result.Files:N0} files, {result.Bytes:N0} bytes. Client launch remains owner-controlled.";
            StartButton.IsEnabled = true;
        }
        catch (OperationCanceledException) { Status.Text = "Preparation cancelled; marked staging cleanup was attempted safely."; }
        catch (Exception error) when (error is IOException or InvalidDataException or CryptographicException or UnauthorizedAccessException)
        {
            Status.Text = "Preparation blocked safely. A sanitized report was written.";
            WriteReport("preparation-failed", error.GetType().Name);
        }
        catch (Exception error) when (error is not (OutOfMemoryException or StackOverflowException))
        {
            Status.Text = "Preparation failed unexpectedly and was stopped safely. A sanitized report was written.";
            TryWriteReport("preparation-failed", "Unexpected");
        }
        finally { cancellation.Dispose(); cancellation = null; PrepareButton.IsEnabled = true; CancelButton.IsEnabled = false; }
    }

    private void Cancel(object sender, RoutedEventArgs e) => cancellation?.Cancel();

    private async void StartClient(object sender, RoutedEventArgs e)
    {
        if (preparedRoot is null || client is not null) return;
        try
        {
            probe = new(); probe.Start();
            string executable = Path.Combine(preparedRoot, "Conquer.exe");
            client = Process.Start(new ProcessStartInfo(executable, "blacknull") { WorkingDirectory = preparedRoot, UseShellExecute = false }) ?? throw new InvalidOperationException();
            await client.WaitForInputIdleAsync(TimeSpan.FromSeconds(20));
            StartButton.IsEnabled = false; SendButton.IsEnabled = true; StopButton.IsEnabled = true;
            Status.Text = "Exact patched Test 3 client started. Confirm its window is visible, then click Send native login once.";
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or SocketException)
        {
            Status.Text = "Client start was blocked safely."; WriteReport("start-failed", error.GetType().Name); await StopAsync();
        }
        catch (Exception error) when (error is not (OutOfMemoryException or StackOverflowException))
        {
            Status.Text = "Client start failed unexpectedly and was stopped safely.";
            TryWriteReport("start-failed", "Unexpected");
            await StopAsync();
        }
    }

    private async void SendLogin(object sender, RoutedEventArgs e)
    {
        if (client is null || client.HasExited || Username.Text.Length is < 3 or > 16 || Password.SecurePassword.Length is < 1 or > 16)
        { Status.Text = "Enter a 3–16 character account and a 1–16 character password."; return; }
        SendButton.IsEnabled = false;
        try
        {
            IntPtr window = client.MainWindowHandle;
            if (window == IntPtr.Zero) throw new InvalidOperationException();
            PostText(window, Username.Text.AsSpan()); PostKey(window, 0x09);
            PostSecure(window, Password.SecurePassword); Password.Clear(); PostKey(window, 0x0d);
            Status.Text = "Native input delivered only to the exact child window. Waiting for loopback protocol evidence; no credential bytes are recorded.";
            await Task.Delay(5000);
            await WriteProbeAsync();
        }
        catch (Exception error) when (error is InvalidOperationException or IOException)
        { Password.Clear(); Status.Text = "Native boundary delivery failed safely."; WriteReport("native-boundary-failed", error.GetType().Name); }
        catch (Exception error) when (error is not (OutOfMemoryException or StackOverflowException))
        { Password.Clear(); Status.Text = "Native boundary delivery failed unexpectedly and was stopped safely."; TryWriteReport("native-boundary-failed", "Unexpected"); await StopAsync(); }
    }

    private async void EmergencyStop(object sender, RoutedEventArgs e) => await StopAsync();

    private async Task StopAsync()
    {
        SendButton.IsEnabled = false; StopButton.IsEnabled = false;
        if (client is { HasExited: false })
        {
            client.CloseMainWindow();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await client.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { client.Kill(entireProcessTree: true); await client.WaitForExitAsync(); }
        }
        client?.Dispose(); client = null;
        await WriteProbeAsync();
        if (probe is not null) await probe.DisposeAsync();
        probe = null; StartButton.IsEnabled = preparedRoot is not null;
    }

    private async Task<ClientProfile> ReadProfileAsync(string name) => new ManifestVerifier(TrustedProfileKeys.Testing.ToArray(), LauncherPolicy.TestingSigningKeyId)
        .VerifyAndRead(await File.ReadAllBytesAsync(Asset(name)));
    private static string Asset(string name) => Path.Combine(AppContext.BaseDirectory, name);
    private async Task WriteProbeAsync() { if (probe is not null) await probe.WriteSanitizedAsync(Path.Combine(AppContext.BaseDirectory, $"native-protocol-{DateTime.UtcNow:yyyyMMddHHmmss}.json"), default); }
    private static void WriteReport(string state, string category) => File.WriteAllText(Path.Combine(AppContext.BaseDirectory, $"native-compatibility-{DateTime.UtcNow:yyyyMMddHHmmss}.json"), JsonSerializer.Serialize(new { schemaVersion = 1, buildId = "native-login-5165-test-3", state, category }));
    internal static void TryWriteReport(string state, string category)
    {
        try { WriteReport(state, category); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    private static void PostText(IntPtr window, ReadOnlySpan<char> text) { foreach (char value in text) PostMessage(window, 0x0102, value, 0); }
    private static void PostSecure(IntPtr window, SecureString secure) { IntPtr value = Marshal.SecureStringToGlobalAllocUnicode(secure); try { for (int i = 0; i < secure.Length; i++) PostMessage(window, 0x0102, Marshal.ReadInt16(value, i * 2), 0); } finally { Marshal.ZeroFreeGlobalAllocUnicode(value); } }
    private static void PostKey(IntPtr window, int key) { PostMessage(window, 0x0100, key, 0); PostMessage(window, 0x0101, key, 0); }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr window, uint message, nint wParam, nint lParam);
}

internal static class ProcessWaitExtensions
{
    public static async Task WaitForInputIdleAsync(this Process process, TimeSpan timeout) => await Task.Run(() => { if (!process.WaitForInputIdle((int)timeout.TotalMilliseconds)) throw new InvalidOperationException(); });
}
