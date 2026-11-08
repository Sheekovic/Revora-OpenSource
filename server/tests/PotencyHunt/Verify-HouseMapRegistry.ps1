$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $project 'obj/chi-refill-validation/bin'
$testExe = Join-Path $output 'HouseMapRegistry.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /warnaserror "/out:$testExe" "/reference:$output\Revora.exe" "/reference:$output\Npgsql.dll" /reference:System.Data.dll /reference:System.Core.dll (Join-Path $PSScriptRoot 'HouseMapRegistryTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Housing fixture compilation failed.' }
Copy-Item -LiteralPath (Join-Path $output 'Revora.exe.config') -Destination ($testExe + '.config')
Push-Location (Join-Path $project 'bin/Debug')
try {
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Housing registry checks failed.' }
} finally { Pop-Location }
