using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PlayRevora.Launcher.Models;

namespace PlayRevora.Launcher.Services;

public sealed record BridgeLoadResult(bool Loaded, string UserMessage);

public interface IDx9BridgeLoader
{
    BridgeLoadResult Prepare();
    Task<BridgeLoadResult> LoadAsync(ObservedProcess target, string expectedPath, CancellationToken cancellationToken);
}

public sealed class DisabledDx9BridgeLoader : IDx9BridgeLoader
{
    public BridgeLoadResult Prepare() => new(true, "DX9 Bridge is disabled for this build.");
    public Task<BridgeLoadResult> LoadAsync(ObservedProcess target, string expectedPath, CancellationToken cancellationToken) =>
        Task.FromResult(new BridgeLoadResult(true, "DX9 Bridge is disabled for this build."));
}

public sealed class SingleEntryDx9BridgeLoader(ValidatedClientRoot root,
    string loaderRelativePath, IDiagnosticSink diagnostics) : IDx9BridgeLoader
{
    private const int ReportSchema = 4;
    private const int MaximumReportBytes = 64 * 1024;
    private const int MaximumEvents = 128;
    private static readonly string[] RequiredInitializationEvents =
    [
        "login_bridge_loaded", "executable_verified", "target_resolved", "target_preimage_verified",
        "hook_install_started", "hook_install_succeeded", "hook_postimage_verified", "login_bridge_ready"
    ];
    private readonly string diagnosticsDirectory = root.Resolve("RevoraDiagnostics");
    public BridgeLoadResult Prepare()
    {
        try
        {
            Directory.CreateDirectory(diagnosticsDirectory);
            if ((File.GetAttributes(diagnosticsDirectory) & FileAttributes.ReparsePoint) != 0)
                return new(false,"Native login diagnostic path is unavailable.");
            return new(true,"Native login integration ready to initialize.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { return new(false,"Native login diagnostic path is unavailable."); }
    }
    public async Task<BridgeLoadResult> LoadAsync(ObservedProcess target,string expectedPath,CancellationToken cancellationToken)
    {
        if(target.HasExited||!Path.GetFullPath(target.ExecutablePath).Equals(expectedPath,StringComparison.OrdinalIgnoreCase)) return new(false,"Approved DX9 process unavailable.");
        string loader=root.Resolve(loaderRelativePath,true);
        string correlation=Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        string reportPath=Path.Combine(diagnosticsDirectory,$"native-login-single-entry-{correlation}.jsonl");
        DateTimeOffset attemptStarted=DateTimeOffset.UtcNow;
        if (!PerAttemptReadinessProtocol.IsApprovedNewReportPath(diagnosticsDirectory,reportPath,correlation))
            return Fail("invalid_report_path","Native login readiness path was rejected.");
        var info=new ProcessStartInfo(loader){WorkingDirectory=Path.GetDirectoryName(loader)!,UseShellExecute=false,CreateNoWindow=true};
        foreach(string value in new[]{"--pid",target.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),"--target",expectedPath,"--report",reportPath,"--correlation",correlation}) info.ArgumentList.Add(value);
        using Process helper=Process.Start(info)??throw new InvalidOperationException("Native login loader did not start.");
        diagnostics.Record("native_login_loader_started",new Dictionary<string,string>{{"correlationId",correlation},{"launcherPid",Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"loaderPid",helper.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"targetPid",target.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try{await helper.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){return Fail("loader_timeout","Native login initialization timed out.");}
        if(helper.ExitCode!=0)return Fail($"loader_exit_{helper.ExitCode}","Native login integration failed closed.");
        var validation=await PerAttemptReadinessProtocol.ValidateAsync(diagnosticsDirectory,reportPath,correlation,target.ProcessId,attemptStarted,DateTimeOffset.UtcNow,cancellationToken);
        if(!validation.Valid)return Fail(validation.Category,"Native login integration did not report valid readiness.");
        diagnostics.Record("native_login_ready",new Dictionary<string,string>{{"result","success"},{"correlationId",correlation},{"launcherPid",Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"loaderPid",helper.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"targetPid",target.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
        return new(true,"Native login integration ready.");

        BridgeLoadResult Fail(string category,string message)
        {
            diagnostics.Record("native_login_init_failed",new Dictionary<string,string>{{"category",category},{"correlationId",correlation},{"launcherPid",Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"targetPid",target.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
            return new(false,message);
        }
    }
}

public static class PerAttemptReadinessProtocol
{
    public static bool IsApprovedNewReportPath(string approvedDirectory,string reportPath,string correlation)
    {
        if(correlation.Length!=32 || correlation.Any(c=>!Uri.IsHexDigit(c) || char.IsLower(c)))return false;
        string directory=Path.GetFullPath(approvedDirectory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(reportPath);
        if(!full.StartsWith(directory,StringComparison.OrdinalIgnoreCase) ||
           !Path.GetFileName(full).Equals($"native-login-single-entry-{correlation}.jsonl",StringComparison.Ordinal))return false;
        if(File.Exists(full))return false;
        return Directory.Exists(approvedDirectory) && (File.GetAttributes(approvedDirectory)&FileAttributes.ReparsePoint)==0;
    }

    public static async Task<(bool Valid,string Category)> ValidateAsync(string approvedDirectory,string reportPath,
        string correlation,int targetPid,DateTimeOffset started,DateTimeOffset completed,CancellationToken cancellationToken)
    {
        string directory=Path.GetFullPath(approvedDirectory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(reportPath);
        if(!full.StartsWith(directory,StringComparison.OrdinalIgnoreCase) ||
           !Path.GetFileName(full).Equals($"native-login-single-entry-{correlation}.jsonl",StringComparison.Ordinal) ||
           (File.GetAttributes(approvedDirectory)&FileAttributes.ReparsePoint)!=0)return(false,"report_outside_approved_directory");
        if(!File.Exists(full))return(false,"missing_report");
        FileInfo file=new(full);
        if((file.Attributes&FileAttributes.ReparsePoint)!=0)return(false,"report_reparse_point");
        if(file.Length<=0 || file.Length>64*1024)return(false,"report_size_invalid");
        string text;
        try{await using var stream=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.Read,4096,FileOptions.Asynchronous|FileOptions.SequentialScan);using var reader=new StreamReader(stream,new UTF8Encoding(false,true));text=await reader.ReadToEndAsync(cancellationToken);}
        catch(Exception e)when(e is IOException or DecoderFallbackException){return(false,"report_read_failed");}
        string[] lines=text.Split('\n',StringSplitOptions.RemoveEmptyEntries);
        if(lines.Length==0 || lines.Length>128)return(false,"report_event_count_invalid");
        int expected=0,readyCount=0;
        foreach(string raw in lines)
        {
            string line=raw.EndsWith('\r')?raw[..^1]:raw;
            if(line.Length==0 || line.Length>2048)return(false,"report_event_size_invalid");
            try
            {
                using JsonDocument document=JsonDocument.Parse(line);
                JsonElement root=document.RootElement;
                if(root.ValueKind!=JsonValueKind.Object || root.GetProperty("schema").GetInt32()!=4 ||
                   root.GetProperty("version").GetString()!="2.0.0-owner-test" ||
                   root.GetProperty("correlationId").GetString()!=correlation ||
                   root.GetProperty("targetPid").GetInt32()!=targetPid)return(false,"report_identity_mismatch");
                if(!DateTimeOffset.TryParse(root.GetProperty("timestampUtc").GetString(),System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeUniversal,out var timestamp) ||
                   timestamp<started.AddSeconds(-1) || timestamp>completed.AddSeconds(1))return(false,"report_timestamp_invalid");
                string? eventName=root.GetProperty("event").GetString();
                if(eventName=="login_bridge_ready")readyCount++;
                if(expected<8 && eventName==new[]{"login_bridge_loaded","executable_verified","target_resolved","target_preimage_verified","hook_install_started","hook_install_succeeded","hook_postimage_verified","login_bridge_ready"}[expected])expected++;
                else if(eventName=="login_bridge_ready")return(false,"readiness_out_of_order");
            }
            catch(Exception e)when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){return(false,"malformed_report");}
        }
        if(readyCount!=1)return(false,readyCount==0?"missing_current_readiness":"duplicate_readiness");
        return expected==8?(true,"success"):(false,"required_initialization_event_missing");
    }
}

#if LEGACY_TWO_ENTRY_CANARY
public sealed class OwnerTestDx9BridgeLoader(
    ValidatedClientRoot root,
    string loaderRelativePath,
    IDiagnosticSink diagnostics,
    NativeLoginCanaryService nativeLogin) : IDx9BridgeLoader
{
    private const string ExpectedMarker = "{\"event\":\"account_redirect_ready\",\"version\":\"2.0.0-owner-test\",\"profile\":\"dx9\"}";
    private readonly string reportPath = root.Resolve("RevoraDiagnostics/account-redirect-owner-test.jsonl");
    private NamedPipeServerStream? activePipe;

    public BridgeLoadResult Prepare()
    {
        if (!nativeLogin.TicketReady)
            return new(false, "Authenticate the Native Login Canary before starting DirectX 9.");
        try
        {
            string directory = Path.GetDirectoryName(reportPath)!;
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $".observer-write-test-{Guid.NewGuid():N}.tmp");
            using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       1, FileOptions.WriteThrough)) { stream.WriteByte(0); stream.Flush(true); }
            File.Delete(probe);
            diagnostics.Record("runtime_report_output_ready", new Dictionary<string, string> { ["profile"] = "Dx9" });
            return new(true, "Runtime report output is ready.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Record("observer_init_failed", new Dictionary<string, string>
                { ["profile"] = "Dx9", ["category"] = "report_path_unwritable" });
            return new(false, "The diagnostic report location is not writable.");
        }
    }

    public async Task<BridgeLoadResult> LoadAsync(ObservedProcess target, string expectedPath,
        CancellationToken cancellationToken)
    {
        if (target.HasExited || !Path.GetFullPath(target.ExecutablePath).Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
            return FailBeforePipe("target_unavailable", "The approved DX9 process is no longer available.");

        string loaderPath = root.Resolve(loaderRelativePath, true);
        long reportStart = File.Exists(reportPath) ? new FileInfo(reportPath).Length : 0;
        string channel = $"PlayRevora.Bridge.{target.ProcessId}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";
        var pipe = new NamedPipeServerStream(channel, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        diagnostics.Record("pipe_server_ready", new Dictionary<string, string> { ["profile"] = "Dx9" });
        var start = new ProcessStartInfo
        {
            FileName = loaderPath,
            WorkingDirectory = Path.GetDirectoryName(loaderPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            Verb = ""
        };
        start.ArgumentList.Add("--pid");
        start.ArgumentList.Add(target.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--target");
        start.ArgumentList.Add(expectedPath);
        start.ArgumentList.Add("--pipe");
        start.ArgumentList.Add($@"\\.\pipe\{channel}");
        start.ArgumentList.Add("--report");
        start.ArgumentList.Add(reportPath);

        Process? startedHelper;
        try { startedHelper = Process.Start(start); }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            pipe.Dispose();
            return FailBeforePipe("loader_start_failed", "The DX9 Account redirect loader could not be started.");
        }
        if (startedHelper is null)
        {
            pipe.Dispose();
            return FailBeforePipe("loader_start_failed", "The DX9 Account redirect loader could not be started.");
        }
        using Process helper = startedHelper;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        bool retainPipe = false;
        try
        {
            Task pipeReady = pipe.WaitForConnectionAsync(timeout.Token);
            Task helperExited = helper.WaitForExitAsync(timeout.Token);
            Task first = await Task.WhenAny(pipeReady, helperExited);
            if (first == helperExited && !pipe.IsConnected)
            {
                await helperExited;
                diagnostics.Record("bridge_init_failed", new Dictionary<string, string>
                {
                    ["profile"] = "Dx9", ["category"] = $"loader_exit_{helper.ExitCode}"
                });
                return FailHandshake("loader_exit", "The DX9 Account redirect loader failed before hook readiness.");
            }
            await pipeReady;
            diagnostics.Record("pipe_connected", new Dictionary<string, string> { ["profile"] = "Dx9" });
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out uint clientPid)
                || clientPid != (uint)target.ProcessId)
                return FailHandshake("pid_rejected", "The native authorization channel process identity was rejected.");
            diagnostics.Record("pipe_pid_verified", new Dictionary<string, string> { ["profile"] = "Dx9" });
            var reader = new StreamReader(pipe, new UTF8Encoding(false, true), false, 256, leaveOpen: true);
            var writer = new StreamWriter(pipe, new UTF8Encoding(false), 256, leaveOpen: true)
                { AutoFlush = true, NewLine = "\n" };
            string? marker = await reader.ReadLineAsync(timeout.Token);
            if (!string.Equals(marker, ExpectedMarker, StringComparison.Ordinal))
                return FailHandshake("marker_invalid", "The native authorization channel marker was rejected.");
            string challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await writer.WriteLineAsync($"CHALLENGE {challenge}");
            string? proof = await reader.ReadLineAsync(timeout.Token);
            string expectedProof = "PROOF " + Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(challenge)));
            if (!string.Equals(proof, expectedProof, StringComparison.Ordinal))
                return FailHandshake("proof_invalid", "The native authorization channel proof was rejected.");
            diagnostics.Record("proof_verified", new Dictionary<string, string> { ["profile"] = "Dx9" });
            await helperExited;
            string[] requiredEvents = ["account_redirect_hook_installed", "account_redirect_ready"];
            string report = await ReadActiveReportAsync(reportPath, reportStart, timeout.Token);
            foreach (string eventName in requiredEvents.Where(name => report.Contains($"\"event\":\"{name}\"", StringComparison.Ordinal)))
                diagnostics.Record(eventName, new Dictionary<string, string> { ["profile"] = "Dx9" });
            bool reportReady = requiredEvents.All(name => report.Contains($"\"event\":\"{name}\"", StringComparison.Ordinal));
            bool valid = helper.ExitCode == 0 && reportReady
                && string.Equals(marker, ExpectedMarker, StringComparison.Ordinal)
                && string.Equals(proof, expectedProof, StringComparison.Ordinal);
            string category = marker?.Contains("observer_init_failed", StringComparison.Ordinal) == true
                ? "observer_init_failed" : "invalid_acknowledgement";
            diagnostics.Record(valid ? "account_redirect_ready" : "bridge_init_failed", new Dictionary<string, string>
            {
                ["profile"] = "Dx9", ["result"] = valid ? "success" : category
            });
            if (!valid) return FailHandshake(category, "The DX9 Account redirect did not complete its authenticated readiness check.");
            diagnostics.Record("ready_acknowledged", new Dictionary<string, string> { ["profile"] = "Dx9" });
            activePipe?.Dispose(); activePipe = pipe;
            retainPipe = true;
            _ = ServeAuthorizationAsync(pipe, reader, writer, nativeLogin.LaunchSessionId, cancellationToken);
            return new(true, "Revora Account redirect and native authorization channel are ready for DirectX 9.");
        }
        catch (OperationCanceledException)
        {
            diagnostics.Record("bridge_rejected", new Dictionary<string, string>
                { ["profile"] = "Dx9", ["reason"] = "timeout" });
            return FailHandshake("timeout", "The DX9 Account redirect initialization timed out.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return FailHandshake("ipc_failure", "The DX9 Account redirect initialization failed securely.");
        }
        finally { if (!retainPipe) pipe.Dispose(); }

        BridgeLoadResult FailHandshake(string category, string message)
        {
            diagnostics.Record("handshake_failed", new Dictionary<string, string>
                { ["profile"] = "Dx9", ["category"] = category });
            nativeLogin.InvalidateTicket();
            diagnostics.Record("ticket_invalidated", new Dictionary<string, string>
                { ["profile"] = "Dx9", ["reason"] = "bridge_initialization_failed" });
            return new(false, message);
        }

        BridgeLoadResult FailBeforePipe(string category, string message)
        {
            diagnostics.Record("handshake_failed", new Dictionary<string, string>
                { ["profile"] = "Dx9", ["category"] = category });
            nativeLogin.InvalidateTicket();
            diagnostics.Record("ticket_invalidated", new Dictionary<string, string>
                { ["profile"] = "Dx9", ["reason"] = "bridge_initialization_failed" });
            return new(false, message);
        }
    }

    private async Task ServeAuthorizationAsync(NamedPipeServerStream pipe, StreamReader reader,
        StreamWriter writer, Guid launchSessionId, CancellationToken cancellationToken)
    {
        try
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (!TryParseAuthorization(line, out string digest, out Guid correlation))
                throw new InvalidDataException("invalid_ipc_message");
            diagnostics.Record("native_authorization_requested", new Dictionary<string, string>
                { ["profile"] = "Dx9", ["result"] = "digest_received" });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            bool ready = await nativeLogin.RedeemAsync(digest, correlation, launchSessionId, timeout.Token);
            await writer.WriteLineAsync(ready ? $"READY {correlation:D}" : $"FAILED {correlation:D}");
            diagnostics.Record("native_authorization_result", new Dictionary<string, string>
                { ["profile"] = "Dx9", ["result"] = ready ? "ready" : "failed" });
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException)
        {
            diagnostics.Record("native_authorization_result", new Dictionary<string, string>
                { ["profile"] = "Dx9", ["result"] = "secure_authorization_failed" });
        }
        finally { nativeLogin.ClearTicket(); pipe.Dispose(); if (ReferenceEquals(activePipe, pipe)) activePipe = null; }
    }

    private static bool TryParseAuthorization(string? line, out string digest, out Guid correlation)
    {
        digest = "";
        correlation = default;
        if (line is null || line.Length != 111 || line.IndexOfAny(['\r', '\n', '\t']) >= 0) return false;
        if (!line.StartsWith("AUTHORIZE ", StringComparison.Ordinal) || line[74] != ' ') return false;
        string candidateDigest = line.Substring(10, 64);
        if (candidateDigest.Any(character => !((character >= '0' && character <= '9')
            || (character >= 'A' && character <= 'F')))) return false;
        if (!Guid.TryParseExact(line.AsSpan(75), "D", out correlation)) return false;
        digest = candidateDigest;
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(nint pipe, out uint clientProcessId);

    private static async Task<string> ReadActiveReportAsync(string path, long startOffset,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt != 5; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return "";
                await using FileStream stream = new(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (startOffset < 0 || startOffset > stream.Length) return "";
                stream.Seek(startOffset, SeekOrigin.Begin);
                using StreamReader reader = new(stream, new UTF8Encoding(false, true));
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch (IOException) when (attempt != 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken);
            }
        }
        return "";
    }
}
#endif
