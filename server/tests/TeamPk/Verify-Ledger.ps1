param([string]$BuildDirectory)
$ErrorActionPreference='Stop'
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (!$BuildDirectory) {$BuildDirectory=Join-Path $project 'obj/mrms2017-validation/bin'}
if (!$env:REVORA_QUARANTINE_TEST_CONNECTION) {throw 'Supply the private candidate connection in REVORA_QUARANTINE_TEST_CONNECTION; never as a command argument.'}
$exe=Join-Path $BuildDirectory 'TeamPkLedgerTests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 /warnaserror /main:TeamPkLedgerTests "/out:$exe" "/reference:$BuildDirectory/Revora.exe" "/reference:$BuildDirectory/Npgsql.dll" "/reference:$BuildDirectory/CO2_CORE_DLL.dll" /reference:System.Core.dll /reference:System.Data.dll (Join-Path $PSScriptRoot 'LedgerTests.cs') (Join-Path $PSScriptRoot 'WorldTests.cs')
if ($LASTEXITCODE -ne 0) {throw 'Local Team PK ledger fixture compilation failed.'}
Copy-Item -LiteralPath (Join-Path $project 'bin/Debug/Revora.exe.config') -Destination ($exe+'.config')
Push-Location $BuildDirectory
try { & $exe; if ($LASTEXITCODE -ne 0) {throw 'Local Team PK ledger fixture failed.'} }
finally {Pop-Location}
