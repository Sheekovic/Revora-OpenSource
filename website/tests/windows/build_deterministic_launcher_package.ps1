param(
    [Parameter(Mandatory = $true)] [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"
$version = "1.0.8"
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

function Build-Package([string] $suffix) {
    $launcherPublish = Join-Path $output "launcher-$suffix"
    $bootstrapPublish = Join-Path $output "bootstrap-$suffix"
    $package = Join-Path $output "package-$suffix"
    dotnet publish (Join-Path $repository "src/Revora.Launcher/Revora.Launcher.csproj") -c Release --no-restore `
        -r win-x64 --self-contained false -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false `
        -o $launcherPublish | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Launcher publish failed." }
    dotnet publish (Join-Path $repository "src/Revora.Bootstrapper/Revora.Bootstrapper.csproj") -c Release --no-restore `
        -r win-x64 --self-contained false -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false `
        -o $bootstrapPublish | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Bootstrapper publish failed." }
    New-Item -ItemType Directory -Path $package | Out-Null
    foreach ($name in @("Revora.Launcher.exe", "Revora.Launcher.dll", "Revora.Launcher.deps.json",
        "Revora.Launcher.runtimeconfig.json", "Revora.Launcher.Core.dll")) {
        Copy-Item (Join-Path $launcherPublish $name) (Join-Path $package $name)
    }
    foreach ($name in @("Revora.Bootstrapper.exe", "Revora.Bootstrapper.dll",
        "Revora.Bootstrapper.deps.json", "Revora.Bootstrapper.runtimeconfig.json")) {
        Copy-Item (Join-Path $bootstrapPublish $name) (Join-Path $package $name)
    }
    Copy-Item (Join-Path $repository "docs/REVORA_LAUNCHER_INSTALLATION.md") (Join-Path $package "INSTALLATION.md")
    [IO.File]::WriteAllText((Join-Path $package "Revora.TestChannel"), "revora-launcher-test-channel-v1`n",
        [Text.UTF8Encoding]::new($false))
    $zip = Join-Path $output "Revora-Launcher-$version-test-$suffix.zip"
    New-DeterministicZip $package $zip
    return $zip
}

$first = Build-Package "a"
$second = Build-Package "b"
$firstHash = (Get-FileHash -Algorithm SHA256 $first).Hash.ToLowerInvariant()
$secondHash = (Get-FileHash -Algorithm SHA256 $second).Hash.ToLowerInvariant()
if ($firstHash -ne $secondHash) { throw "The launcher package is not deterministic." }

$final = Join-Path $output "Revora-Launcher-$version-test.zip"
Copy-Item $first $final
$files = Get-ChildItem (Join-Path $output "package-a") -File | Sort-Object Name | ForEach-Object {
    [ordered]@{ path = $_.Name; bytes = $_.Length; sha256 = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant() }
}
$evidence = [ordered]@{
    schemaVersion = 1
    launcherVersion = $version
    package = [ordered]@{ path = [IO.Path]::GetFileName($final); bytes = (Get-Item $final).Length; sha256 = $firstHash }
    deterministicComparisonSha256 = $secondHash
    deterministic = $true
    files = $files
}
$evidence | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $output "package-evidence.json")
Write-Host "Deterministic launcher package: $final ($firstHash)"
