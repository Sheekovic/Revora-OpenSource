$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$testExe = Join-Path $output 'WarehouseProtocol.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /unsafe /warnaserror "/out:$testExe" "/reference:$output\Revora.exe" (Join-Path $PSScriptRoot 'WarehouseProtocolTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Warehouse protocol fixture compile failed.' }
Copy-Item -LiteralPath (Join-Path $project 'bin\Debug\Revora.exe.config') -Destination "$testExe.config"
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Warehouse protocol fixture failed.' }
