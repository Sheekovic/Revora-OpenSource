param(
    [Parameter(Mandatory = $true)] [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"
$packageUrl = "https://playrevora.com/downloads/Revora-Launcher-1.0.7-test.zip"
$expectedHash = "9f68f07b68f3fc47d45cdc596131ad9d4561df8e5aa9a0939456c7d8dc72dd62"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$zipPath = Join-Path $OutputDirectory "Revora-Launcher-1.0.7-test.zip"
$extractPath = Join-Path $OutputDirectory "extracted"

$response = Invoke-WebRequest -Uri $packageUrl -MaximumRedirection 0 -PassThru -OutFile $zipPath
if ($response.StatusCode -ne 200) { throw "The exact launcher package was unavailable." }
$actualHash = (Get-FileHash -Algorithm SHA256 -Path $zipPath).Hash.ToLowerInvariant()
if ($actualHash -ne $expectedHash) { throw "The downloaded launcher package hash changed." }
Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath

$status = Get-MpComputerStatus
$before = @(Get-MpThreatDetection | Where-Object { $_.InitialDetectionTime -ge (Get-Date).AddMinutes(-5) })
$scanner = Join-Path $env:ProgramFiles "Windows Defender/MpCmdRun.exe"
if (-not (Test-Path $scanner)) { throw "Microsoft Defender command-line scanner is unavailable." }
& $scanner -Scan -ScanType 3 -File $zipPath -DisableRemediation
if ($LASTEXITCODE -ne 0) { throw "Microsoft Defender rejected the launcher ZIP with exit code $LASTEXITCODE." }
& $scanner -Scan -ScanType 3 -File $extractPath -DisableRemediation
if ($LASTEXITCODE -ne 0) { throw "Microsoft Defender rejected extracted launcher files with exit code $LASTEXITCODE." }
$after = @(Get-MpThreatDetection | Where-Object { $_.InitialDetectionTime -ge (Get-Date).AddMinutes(-5) })
if ($after.Count -gt $before.Count) { throw "Microsoft Defender reported a new detection." }

$files = Get-ChildItem $extractPath -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = $_.FullName.Substring($extractPath.Length + 1).Replace('\', '/')
        bytes = $_.Length
        sha256 = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant()
    }
}
$evidence = [ordered]@{
    schemaVersion = 1
    timestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
    packageUrl = $packageUrl
    packageSha256 = $actualHash
    packageBytes = (Get-Item $zipPath).Length
    engineVersion = $status.AMEngineVersion
    productVersion = $status.AMProductVersion
    antivirusSignatureVersion = $status.AntivirusSignatureVersion
    antivirusSignatureUpdatedUtc = ([DateTimeOffset]$status.AntivirusSignatureLastUpdated).ToUniversalTime().ToString("O")
    realTimeProtectionEnabled = $status.RealTimeProtectionEnabled
    zipScanExitCode = 0
    extractedScanExitCode = 0
    detections = 0
    exclusionsAdded = $false
    files = $files
}
$evidence | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "defender-scan-evidence.json")

# Leave only sanitized evidence in the uploaded directory.
Remove-Item -LiteralPath $extractPath -Recurse -Force
Remove-Item -LiteralPath $zipPath -Force
