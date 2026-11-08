$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$testExe = Join-Path $output 'ConnectionConfig.Tests.exe'
foreach ($required in @($compiler, (Join-Path $output 'Revora.exe'), (Join-Path $output 'Npgsql.dll'))) {
    if (!(Test-Path -LiteralPath $required -PathType Leaf)) {
        throw 'Build the isolated x86 server output before running connection configuration checks.'
    }
}
& $compiler /nologo /platform:x86 ("/out:" + $testExe) `
    ("/reference:" + (Join-Path $output 'Revora.exe')) `
    ("/reference:" + (Join-Path $output 'Npgsql.dll')) `
    (Join-Path $PSScriptRoot 'ConnectionConfigTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Connection configuration fixtures did not compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Connection configuration checks failed.' }
