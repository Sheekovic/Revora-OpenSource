$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$exe = Join-Path $output 'SrpVerifier.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /platform:x86 /warnaserror `
    ("/out:" + $exe) ("/reference:" + (Join-Path $output 'Revora.exe')) /reference:System.Numerics.dll `
    (Join-Path $PSScriptRoot 'SrpVerifierTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'SRP verifier fixtures did not compile.' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'SRP verifier fixtures failed.' }
