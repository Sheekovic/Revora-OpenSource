param(
    [Parameter(Mandatory = $true)] [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"
$archiveUrl = "https://playrevora.com/downloads/Revora-Full-Client-5165-1.0.7.zip"
$archiveSha256 = "eb45995b817a153e2664f80f871c54ae24bf6993456887c498098d56decfcb0e"
$versionDatSha256 = "44898b43f6d5ea1c2b39daad4f4b043830318ecd3b54a348063f9c48cf99f0c4"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$archive = Join-Path $OutputDirectory "Revora-Full-Client-5165-1.0.7.zip"
$client = Join-Path $OutputDirectory "client"

for ($attempt = 1; $attempt -le 3; $attempt++) {
    try {
        Invoke-WebRequest -Uri $archiveUrl -MaximumRedirection 0 -OutFile $archive -TimeoutSec 900
        break
    }
    catch {
        if ($attempt -eq 3) { throw }
        Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
    }
}
if ((Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant() -ne $archiveSha256) { throw "Full-client archive hash mismatch." }
Expand-Archive -LiteralPath $archive -DestinationPath $client

function Wait-ForSelectedGame([int] $TimeoutSeconds) {
    $expected = [IO.Path]::GetFullPath((Join-Path $client "Conquer.exe"))
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        foreach ($process in @(Get-Process -Name Conquer -ErrorAction SilentlyContinue)) {
            try {
                if ([IO.Path]::GetFullPath($process.Path) -eq $expected) { return $process }
            }
            catch { }
        }
        Start-Sleep -Milliseconds 500
    }
    throw "The stable launcher did not start the selected genuine client within the bounded interval."
}

function Stop-DisposableProcesses($launcher, $game) {
    if ($null -ne $game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force }
    Start-Sleep -Seconds 2
    if ($null -ne $launcher -and -not $launcher.HasExited) {
        $launcher.CloseMainWindow() | Out-Null
        if (-not $launcher.WaitForExit(5000)) { Stop-Process -Id $launcher.Id -Force }
    }
}

function Start-And-Observe([string] $Label) {
    $started = [DateTimeOffset]::UtcNow
    $launcher = Start-Process -FilePath (Join-Path $client "Revora.Launcher.exe") -WorkingDirectory $client -PassThru
    $game = $null
    try {
        $game = Wait-ForSelectedGame 600
        $stagedFiles = @(Get-ChildItem (Join-Path $client ".revora-launcher/staging") -File -Recurse -ErrorAction SilentlyContinue)
        return [ordered]@{
            label = $Label
            launcherPid = $launcher.Id
            gameStarted = $true
            elapsedMilliseconds = [long]([DateTimeOffset]::UtcNow - $started).TotalMilliseconds
            stagedFiles = $stagedFiles.Count
            stagedBytes = [long](($stagedFiles | Measure-Object Length -Sum).Sum)
        }
    }
    finally { Stop-DisposableProcesses $launcher $game }
}

$first = Start-And-Observe "included-client-first-verification"
$cache = Join-Path $client ".revora-launcher/verified-state.json"
if (-not (Test-Path $cache)) { throw "The first verification did not persist the verified-state cache." }
$firstCacheHash = (Get-FileHash -Algorithm SHA256 $cache).Hash.ToLowerInvariant()
$second = Start-And-Observe "verified-cache-reuse"
if ($second.stagedBytes -ne 0) { throw "The second launch downloaded client repair content unexpectedly." }

$versionDat = Join-Path $client "version.dat"
[IO.File]::WriteAllBytes($versionDat, [byte[]](0x00, 0x01, 0x02, 0x03))
$repair = Start-And-Observe "single-file-repair"
if ((Get-FileHash -Algorithm SHA256 $versionDat).Hash.ToLowerInvariant() -ne $versionDatSha256) { throw "The disposable corruption was not repaired." }

$evidence = [ordered]@{
    schemaVersion = 1
    timestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
    runner = [Environment]::OSVersion.VersionString
    archiveUrl = $archiveUrl
    archiveSha256 = $archiveSha256
    archiveBytes = (Get-Item $archive).Length
    firstVerification = $first
    verifiedStateSha256 = $firstCacheHash
    cacheReuse = $second
    singleFileRepair = $repair
    repairedVersionDatSha256 = $versionDatSha256
    automaticLaunchesPerCycle = 1
    credentialsUsed = $false
    genuineClientAuthenticationChanged = $false
}
$evidence | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "stable-full-client-windows-evidence.json")

Remove-Item -LiteralPath $archive -Force
Remove-Item -LiteralPath $client -Recurse -Force
