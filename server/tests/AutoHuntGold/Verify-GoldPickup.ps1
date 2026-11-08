$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $projectRoot 'obj\chi-refill-validation\bin'
$assembly = Join-Path $output 'Revora.exe'
$testExe = Join-Path $output 'GoldPickup.Tests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x86 /unsafe /warnaserror /main:GoldPickupTests "/out:$testExe" "/reference:$assembly" /reference:System.Drawing.dll /reference:System.Data.dll /reference:System.Core.dll (Join-Path $projectRoot 'Game\Features\CompanionNavigation.cs') (Join-Path $projectRoot 'tests\CompanionAI\CompanionTests.cs') (Join-Path $PSScriptRoot 'GoldPickupTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Gold pickup fixtures did not compile.' }
Write-Output ('Testing isolated server SHA256: ' + (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash)
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Gold pickup fixtures failed.' }
