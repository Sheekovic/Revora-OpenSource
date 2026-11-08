param(
    [Parameter(Mandatory = $true)] [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"
$launcherUrl = "https://playrevora.com/downloads/Revora-Launcher-1.0.8.zip"
$launcherSha256 = "3c7db8a4cf077b05c456bf485fec13a362ebf7b5e3a5e5bc04a81e31308e0720"
$fullClientUrl = "https://playrevora.com/downloads/Revora-Full-Client-5165-1.0.7.zip"
$fullClientSha256 = "eb45995b817a153e2664f80f871c54ae24bf6993456887c498098d56decfcb0e"
$expectedClientFiles = 12831
$expectedArchiveEntries = 12842
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function Download-Exact([string] $Uri, [string] $Path, [string] $ExpectedHash) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -MaximumRedirection 0 -PassThru -OutFile $Path -TimeoutSec 900
            if ($response.StatusCode -ne 200) { throw "Unexpected HTTP status." }
            $actual = (Get-FileHash -Algorithm SHA256 $Path).Hash.ToLowerInvariant()
            if ($actual -ne $ExpectedHash) { throw "Downloaded package hash mismatch." }
            return [ordered]@{ url = $Uri; status = $response.StatusCode; bytes = (Get-Item $Path).Length; sha256 = $actual }
        }
        catch {
            if ($attempt -eq 3) { throw }
            Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
        }
    }
}

$launcherZip = Join-Path $OutputDirectory "Revora-Launcher-1.0.8.zip"
$fullClientZip = Join-Path $OutputDirectory "Revora-Full-Client-5165-1.0.7.zip"
$launcherTree = Join-Path $OutputDirectory "launcher-extracted"
$fullClientTree = Join-Path $OutputDirectory "full-client-extracted"
$launcher = Download-Exact $launcherUrl $launcherZip $launcherSha256
$fullClient = Download-Exact $fullClientUrl $fullClientZip $fullClientSha256
Expand-Archive -LiteralPath $launcherZip -DestinationPath $launcherTree
Expand-Archive -LiteralPath $fullClientZip -DestinationPath $fullClientTree

if (Test-Path (Join-Path $launcherTree "Revora.TestChannel")) { throw "The stable package contains the test marker." }
$channel = Get-Content (Join-Path $launcherTree "Revora.Channel.json") -Raw | ConvertFrom-Json
if ($channel.channel -ne "stable" -or $channel.launcherVersion -ne "1.0.7") { throw "Stable channel identity is invalid." }
$fullEntries = @(Get-ChildItem $fullClientTree -File -Recurse)
if ($fullEntries.Count -ne $expectedArchiveEntries) { throw "The full-client entry count changed." }
$managedFiles = $fullEntries.Count - 11
if ($managedFiles -ne $expectedClientFiles) { throw "The full-client managed file count changed." }

$status = Get-MpComputerStatus
$before = @(Get-MpThreatDetection | Where-Object { $_.InitialDetectionTime -ge (Get-Date).AddMinutes(-10) })
$scanner = Join-Path $env:ProgramFiles "Windows Defender/MpCmdRun.exe"
if (-not (Test-Path $scanner)) { throw "Microsoft Defender command-line scanner is unavailable." }
$targets = @($launcherZip, $launcherTree, $fullClientZip, $fullClientTree)
foreach ($target in $targets) {
    & $scanner -Scan -ScanType 3 -File $target -DisableRemediation
    if ($LASTEXITCODE -ne 0) { throw "Microsoft Defender rejected an exact stable target with exit code $LASTEXITCODE." }
}
$after = @(Get-MpThreatDetection | Where-Object { $_.InitialDetectionTime -ge (Get-Date).AddMinutes(-10) })
if ($after.Count -gt $before.Count) { throw "Microsoft Defender reported a new detection." }

$evidence = [ordered]@{
    schemaVersion = 1
    timestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
    runner = [Environment]::OSVersion.VersionString
    launcher = $launcher
    fullClient = $fullClient
    fullClientEntries = $fullEntries.Count
    managedClientFiles = $managedFiles
    stableChannelMarkerValidated = $true
    testMarkerPresent = $false
    engineVersion = $status.AMEngineVersion
    productVersion = $status.AMProductVersion
    antivirusSignatureVersion = $status.AntivirusSignatureVersion
    antivirusSignatureUpdatedUtc = ([DateTimeOffset]$status.AntivirusSignatureLastUpdated).ToUniversalTime().ToString("O")
    realTimeProtectionEnabled = $status.RealTimeProtectionEnabled
    scannedTargets = @("launcher-zip", "launcher-extracted", "full-client-zip", "full-client-extracted")
    detections = 0
    exclusionsAdded = $false
}
$evidence | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "stable-defender-evidence.json")

Remove-Item -LiteralPath $launcherZip, $fullClientZip -Force
Remove-Item -LiteralPath $launcherTree, $fullClientTree -Recurse -Force
