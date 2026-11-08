$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $projectRoot 'obj\chi-refill-validation\bin'
$assembly = Join-Path $output 'Revora.exe'
if (!(Test-Path -LiteralPath $assembly)) { throw 'Build the isolated validation output first; see README.md.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$testExe = Join-Path $output 'CompanionAI.Tests.exe'
$navigation = Join-Path $projectRoot 'Game\Features\CompanionNavigation.cs'
& $compiler /nologo /target:exe /platform:x86 /unsafe /warnaserror "/out:$testExe" "/reference:$assembly" /reference:System.Drawing.dll /reference:System.Data.dll /reference:System.Core.dll $navigation (Join-Path $PSScriptRoot 'CompanionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Companion fixtures did not compile.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\Debug\Revora.exe.config') -Destination "$testExe.config"
Write-Output ('Testing isolated server SHA256: ' + (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash)
# The fixture creates only in-memory maps and characters, with disconnected sockets.
# It never starts the server, loads configuration, or opens a database connection.
& $testExe (Join-Path $projectRoot 'bin\Debug\database\magictype.txt')
if ($LASTEXITCODE -ne 0) { throw 'Companion fixtures failed.' }
