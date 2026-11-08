$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repository = (Resolve-Path (Join-Path $root '../..')).Path
$destination = Join-Path $root ('artifacts/package-' + [Guid]::NewGuid().ToString('N'))
$publish = Join-Path $destination 'publish'
$package = Join-Path $destination 'Revora-ClientEndpoint-win-x64'
New-Item -ItemType Directory -Path $package -Force | Out-Null
# The portable release embeds a supported .NET 10 runtime; .NET 8 is only a development fallback.
dotnet publish "$root/Gui/Revora.ClientEndpoint.csproj" -c Release -r win-x64 --self-contained true `
    -p:RevoraRelease=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Portable GUI publication failed' }
Copy-Item -LiteralPath (Join-Path $publish 'Revora.ClientEndpoint.exe') -Destination $package
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'RUNTIME-NOTICES.txt') -Destination $package
$runtimePacks = @(Get-Content (Join-Path $publish 'runtime-pack-paths.txt') |
    Where-Object { $_ -match 'microsoft\.(netcore|windowsdesktop)\.app\.runtime\.win-x64[\\/]' } | Select-Object -Unique)
if ($runtimePacks.Count -ne 2) { throw 'Expected exact core and desktop runtime packs for license collection' }
foreach ($packPath in $runtimePacks) {
    if (-not (Test-Path -LiteralPath $packPath)) { throw "Runtime pack unavailable: $packPath" }
    $packName = (Split-Path (Split-Path $packPath -Parent) -Leaf) + '-' + (Split-Path $packPath -Leaf)
    $notices = @(Get-ChildItem -LiteralPath $packPath -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)' })
    if (-not ($notices | Where-Object Name -match '^LICENSE')) { throw "Runtime license missing: $packName" }
    if ($packName -match 'microsoft.netcore' -and -not ($notices | Where-Object Name -match '^THIRD-PARTY-NOTICES')) {
        throw "Core runtime third-party notice missing: $packName"
    }
    $licenseDirectory = Join-Path $package ('runtime-licenses/' + $packName)
    New-Item -ItemType Directory -Path $licenseDirectory -Force | Out-Null
    foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination $licenseDirectory }
}
# The desktop NuGet pack omits the WinForms BSD notice; include its reviewed upstream text.
Copy-Item -LiteralPath (Join-Path $repository 'licenses/dotnet-winforms-10.0.14-THIRD-PARTY-NOTICES.txt') `
    -Destination (Join-Path $package 'runtime-licenses')
@{
    sourceCommit = (& git -C $repository rev-parse HEAD)
    runtime = '.NET 10, self-contained Windows x64'
    version = '1.0.0'
    executableSha256 = (Get-FileHash (Join-Path $package 'Revora.ClientEndpoint.exe') -Algorithm SHA256).Hash
    includesProprietaryClient = $false
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'BUILD-INFO.json') -Encoding utf8
$zip = Join-Path $destination 'Revora-ClientEndpoint-v1.0.0-win-x64.zip'
Compress-Archive -Path $package -DestinationPath $zip
$checksums = Join-Path $destination 'SHA256SUMS.txt'
"$((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath $checksums -Encoding ascii
if ($env:GITHUB_OUTPUT) {
    "zip=$zip" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
    "checksums=$checksums" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
}
Write-Output "Package: $zip"
Write-Output "Checksums: $checksums"
