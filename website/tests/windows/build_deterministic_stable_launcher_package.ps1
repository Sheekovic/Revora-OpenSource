param(
    [Parameter(Mandatory = $true)] [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"
$version = "1.0.8"
$releaseId = "51650108-0000-4000-9000-000000000001"
$acceptedTestUrl = "https://playrevora.com/downloads/Revora-Launcher-1.0.8-test.zip"
$acceptedTestSha256 = "f7406da78ef055d08b940a7819836a8bb8e58e177cc5ec0a51398d656193f412"
$repository = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path $output) { Remove-Item $output -Recurse -Force }
New-Item -ItemType Directory -Path $output | Out-Null

function New-DeterministicZip([string] $source, [string] $destination) {
    Add-Type -AssemblyName System.IO.Compression
    if (Test-Path $destination) { Remove-Item $destination -Force }
    $archive = [IO.Compression.ZipFile]::Open($destination, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = Get-ChildItem -LiteralPath $source -File -Recurse | Sort-Object {
            $_.FullName.Substring($source.Length + 1).Replace('\', '/')
        }
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($source.Length + 1).Replace('\', '/')
            $entry = $archive.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $input = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
            $target = $entry.Open()
            try { $input.CopyTo($target) }
            finally { $target.Dispose(); $input.Dispose() }
        }
    }
    finally { $archive.Dispose() }
}

function Build-Package([string] $suffix, [string] $acceptedZip) {
    $package = Join-Path $output "package-$suffix"
    Expand-Archive -LiteralPath $acceptedZip -DestinationPath $package
    Remove-Item -LiteralPath (Join-Path $package "Revora.TestChannel") -Force
    Copy-Item (Join-Path $repository "docs/REVORA_LAUNCHER_STABLE_INSTALLATION.md") (Join-Path $package "INSTALLATION.md")
    [IO.File]::WriteAllText((Join-Path $package "Revora.Channel.json"),
        "{`"schemaVersion`":1,`"channel`":`"stable`",`"launcherVersion`":`"$version`",`"releaseId`":`"$releaseId`"}`n",
        [Text.UTF8Encoding]::new($false))
    if (Test-Path (Join-Path $package "Revora.TestChannel")) { throw "A test-channel marker entered the stable package." }
    $zip = Join-Path $output "Revora-Launcher-$version-$suffix.zip"
    New-DeterministicZip $package $zip
    return $zip
}

$acceptedZip = Join-Path $output "accepted-test.zip"
for ($attempt = 1; $attempt -le 3; $attempt++) {
    try {
        Invoke-WebRequest -Uri $acceptedTestUrl -MaximumRedirection 0 -OutFile $acceptedZip -TimeoutSec 120
        break
    }
    catch {
        if ($attempt -eq 3) { throw }
        Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
    }
}
$downloadedHash = (Get-FileHash -Algorithm SHA256 $acceptedZip).Hash.ToLowerInvariant()
if ($downloadedHash -ne $acceptedTestSha256) { throw "The accepted immutable test package changed." }
$first = Build-Package "a" $acceptedZip
$second = Build-Package "b" $acceptedZip
$firstHash = (Get-FileHash -Algorithm SHA256 $first).Hash.ToLowerInvariant()
$secondHash = (Get-FileHash -Algorithm SHA256 $second).Hash.ToLowerInvariant()
if ($firstHash -ne $secondHash) { throw "The stable launcher package is not deterministic." }

$final = Join-Path $output "Revora-Launcher-$version.zip"
Copy-Item $first $final
$files = Get-ChildItem (Join-Path $output "package-a") -File | Sort-Object Name | ForEach-Object {
    [ordered]@{ path = $_.Name; bytes = $_.Length; sha256 = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant() }
}
$evidence = [ordered]@{
    schemaVersion = 1
    channel = "stable"
    releaseId = $releaseId
    launcherVersion = $version
    package = [ordered]@{ path = [IO.Path]::GetFileName($final); bytes = (Get-Item $final).Length; sha256 = $firstHash }
    deterministicComparisonSha256 = $secondHash
    deterministic = $true
    testMarkerPresent = $false
    acceptedTestPackageSha256 = $acceptedTestSha256
    files = $files
}
$evidence | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $output "stable-package-evidence.json")
Write-Host "Deterministic stable launcher package: $final ($firstHash)"
