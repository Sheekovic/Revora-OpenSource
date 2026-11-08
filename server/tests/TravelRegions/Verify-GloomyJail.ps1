param([Parameter(Mandatory=$true)][string]$StagedFloor,[Parameter(Mandatory=$true)][string]$ExtractedDMap)
$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$testExe = Join-Path $output 'GloomyJail.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /warnaserror "/out:$testExe" "/reference:$output\Revora.exe" /reference:System.Core.dll (Join-Path $PSScriptRoot 'GloomyJailTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Gloomy Jail floor fixtures did not compile.' }
Copy-Item -LiteralPath (Join-Path $project 'bin\Debug\Revora.exe.config') -Destination "$testExe.config"
Write-Output ('Test harness SHA256: ' + (Get-FileHash -LiteralPath $testExe).Hash)
& $testExe (Resolve-Path -LiteralPath $StagedFloor).Path (Resolve-Path -LiteralPath $ExtractedDMap).Path
if ($LASTEXITCODE -ne 0) { throw 'Gloomy Jail floor fixtures failed.' }
