$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $project 'obj/chi-refill-validation/bin'
$testExe = Join-Path $output 'DynamicMapAllocation.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /warnaserror "/out:$testExe" "/reference:$output\Revora.exe" /reference:System.Core.dll (Join-Path $PSScriptRoot 'DynamicMapAllocationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Dynamic map fixture compilation failed.' }
Push-Location (Join-Path $project 'bin/Debug')
try {
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Dynamic map checks failed.' }
} finally { Pop-Location }
