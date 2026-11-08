param(
    [Parameter(Mandatory = $true)] [string] $PackagePath,
    [Parameter(Mandatory = $true)] [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Path $output | Out-Null
$extract = Join-Path $output "extracted"
Expand-Archive -LiteralPath $package -DestinationPath $extract

$status = Get-MpComputerStatus
$before = @(Get-MpThreatDetection | Where-Object { $_.InitialDetectionTime -ge (Get-Date).AddMinutes(-5) })
$scanner = Join-Path $env:ProgramFiles "Windows Defender/MpCmdRun.exe"
if (-not (Test-Path $scanner)) { throw "Microsoft Defender command-line scanner is unavailable." }
& $scanner -Scan -ScanType 3 -File $package -DisableRemediation
if ($LASTEXITCODE -ne 0) { throw "Microsoft Defender rejected the exact launcher ZIP with exit code $LASTEXITCODE." }
& $scanner -Scan -ScanType 3 -File $extract -DisableRemediation
if ($LASTEXITCODE -ne 0) { throw "Microsoft Defender rejected the extracted launcher files with exit code $LASTEXITCODE." }
$after = @(Get-MpThreatDetection | Where-Object { $_.InitialDetectionTime -ge (Get-Date).AddMinutes(-5) })
if ($after.Count -gt $before.Count) { throw "Microsoft Defender reported a new detection." }

$files = Get-ChildItem $extract -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = $_.FullName.Substring($extract.Length + 1).Replace('\', '/')
        bytes = $_.Length
        sha256 = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant()
    }
}
$evidence = [ordered]@{
    schemaVersion = 1
    timestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
    package = [IO.Path]::GetFileName($package)
    packageSha256 = (Get-FileHash -Algorithm SHA256 $package).Hash.ToLowerInvariant()
    packageBytes = (Get-Item $package).Length
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
$evidence | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $output "defender-scan-evidence.json")

Remove-Item -LiteralPath $extract -Recurse -Force
