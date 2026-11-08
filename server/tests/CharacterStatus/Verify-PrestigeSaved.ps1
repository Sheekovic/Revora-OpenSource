param([Parameter(Mandatory=$true)][string]$ArtifactRoot)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=(Resolve-Path -LiteralPath $ArtifactRoot).Path
$exe=Join-Path $output 'Prestige.Saved.Tests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (!$env:REVORA_QUARANTINE_TEST_CONNECTION) { throw 'Explicit disposable test connection required.' }
& $compiler /nologo /target:exe /platform:x86 /warnaserror "/out:$exe" "/reference:$output\Revora.exe" "/reference:$output\Npgsql.dll" "/reference:$output\protobuf-net.dll" /reference:System.Data.dll /reference:System.Core.dll (Join-Path $PSScriptRoot 'PrestigeSavedWingTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Saved wing fixture compilation failed.' }
Copy-Item -LiteralPath (Join-Path $output 'Revora.exe.config') -Destination "$exe.config"
Push-Location (Join-Path $projectRoot 'bin\Debug')
try {
    & $exe
    if ($LASTEXITCODE -ne 0) { throw 'Saved wing fixture failed.' }
} finally { Pop-Location }
