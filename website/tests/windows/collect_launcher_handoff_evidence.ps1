param(
    [Parameter(Mandatory = $true)] [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$clientManifestUrl = "https://playrevora.com/patch/v2/test/manifest.json"
$launcherManifestUrl = "https://playrevora.com/launcher/v1/test/manifest.json"
$expectedClientManifest = "b0237870474713c9f911fd891919f56f4ed458c28aa627041541969f44cf346c"
$expectedLauncherManifest = "bf78a81735832cdabc253babf8313b3c14512107fe96849e8fd03c9247febb16"
$expectedClient = "902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f"

function Download-Exact([string] $Uri, [string] $Path, [string] $ExpectedHash) {
    $response = $null
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -MaximumRedirection 0 -PassThru -OutFile $Path -TimeoutSec 60
            break
        }
        catch {
            if ($attempt -eq 3) { throw }
            Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
        }
    }
    if ($null -eq $response) { throw "The exact publication could not be downloaded." }
    if ($response.StatusCode -ne 200) { throw "Unexpected HTTP status $($response.StatusCode) for $Uri" }
    $actual = (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
    if ($actual -ne $ExpectedHash) { throw "Hash mismatch for $Uri" }
    return [ordered]@{ url = $Uri; status = $response.StatusCode; bytes = (Get-Item $Path).Length; sha256 = $actual }
}

$clientManifestPath = Join-Path $OutputDirectory "client-manifest.signed.json"
$launcherManifestPath = Join-Path $OutputDirectory "launcher-manifest.signed.json"
$clientEndpoint = Download-Exact $clientManifestUrl $clientManifestPath $expectedClientManifest
$launcherEndpoint = Download-Exact $launcherManifestUrl $launcherManifestPath $expectedLauncherManifest

$envelope = Get-Content $clientManifestPath -Raw | ConvertFrom-Json
if ($envelope.keyId -ne "revora-client-production-2026-01" -or $envelope.algorithm -ne "ECDSA-P256-SHA256") {
    throw "The public client manifest uses an unexpected trust identity."
}
$payloadBytes = [Convert]::FromBase64String($envelope.payload)
$payload = [Text.Encoding]::UTF8.GetString($payloadBytes) | ConvertFrom-Json
$clientEntry = $payload.files | Where-Object path -eq "Conquer.exe"
if ($clientEntry.sha256 -ne $expectedClient -or $clientEntry.size -ne 2841656) {
    throw "The signed desired state does not identify the approved executable."
}
$objectUrl = "$($payload.manifestBaseUri)/$($clientEntry.objectPath)"
$clientPath = Join-Path $OutputDirectory "Conquer.exe"
$clientObject = Download-Exact $objectUrl $clientPath $expectedClient

$bytes = [IO.File]::ReadAllBytes($clientPath)
$ascii = [Text.Encoding]::ASCII.GetString($bytes)
foreach ($required in @("blacknull", "%s %s %s", "LOGINUSER", "LOGINPASS")) {
    if (-not $ascii.Contains($required)) { throw "Required exact-client command-line evidence is absent: $required" }
}
$forbiddenIpc = @("CreateNamedPipe", "ConnectNamedPipe", "CallNamedPipe", "WaitNamedPipe", "CreateFileMapping", "OpenFileMapping", "MapViewOfFile")
$presentIpc = @($forbiddenIpc | Where-Object { $ascii.Contains($_) })

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class SuspendedEvidenceProcess {
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  public struct STARTUPINFO { public int cb; public string lpReserved; public string lpDesktop; public string lpTitle; public int dwX; public int dwY; public int dwXSize; public int dwYSize; public int dwXCountChars; public int dwYCountChars; public int dwFillAttribute; public int dwFlags; public short wShowWindow; public short cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError; }
  [StructLayout(LayoutKind.Sequential)] public struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public int processId; public int threadId; }
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  static extern bool CreateProcessW(string app, System.Text.StringBuilder command, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr environment, string cwd, ref STARTUPINFO startup, out PROCESS_INFORMATION process);
  [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr process, uint exitCode);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
  public static int Create(string executable) {
    var startup = new STARTUPINFO(); startup.cb = Marshal.SizeOf(startup);
    var command = new System.Text.StringBuilder("\"" + executable + "\" blacknull REVORA_EVIDENCE_USER NON_SECRET_EVIDENCE_MARKER");
    PROCESS_INFORMATION process;
    if (!CreateProcessW(executable, command, IntPtr.Zero, IntPtr.Zero, false, 0x00000004, IntPtr.Zero, System.IO.Path.GetDirectoryName(executable), ref startup, out process)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    try { return process.processId; }
    finally { TerminateProcess(process.hProcess, 0); CloseHandle(process.hThread); CloseHandle(process.hProcess); }
  }
}
"@

$pidValue = [SuspendedEvidenceProcess]::Create($clientPath)
# The process was deliberately never resumed. The exact command-line parser was established by
# static control flow; this Windows observation establishes that putting a bearer value there is
# not secret. Record no command line or sensitive value in the result.

$result = [ordered]@{
    schemaVersion = 1
    timestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
    runner = [Environment]::OSVersion.VersionString
    clientManifest = $clientEndpoint
    launcherManifest = $launcherEndpoint
    clientObject = $clientObject
    signedReleaseId = $payload.releaseId
    clientBuild = $payload.clientBuild
    approvedExecutableSha256 = $expectedClient
    exactCommandLineSurfacePresent = $true
    commandLineMarkerVisibleToLauncher = $true
    approvedClientCodeExecuted = $false
    processCreatedSuspendedAndTerminated = ($pidValue -gt 0)
    namedPipeOrSharedMemoryImportsObserved = ($presentIpc.Count -gt 0)
    observedIpcNames = $presentIpc
    classification = "command-line-supported-but-secret-bearing; no-safe-ticket-handoff-proven"
}
$result | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "launcher-handoff-evidence.json")

Remove-Item $clientPath, $clientManifestPath, $launcherManifestPath -Force
