param([Parameter(Mandatory=$true)][string]$ArtifactRoot)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=(Resolve-Path -LiteralPath $ArtifactRoot).Path
$exe=Join-Path $output 'DisCity.Budget.Tests.exe'
if (!$env:REVORA_QUARANTINE_TEST_CONNECTION) { throw 'Explicit disposable test connection required.' }
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x86 /unsafe /warnaserror /main:DisCityCombatBudgetTests "/out:$exe" "/reference:$output\Revora.exe" "/reference:$output\Npgsql.dll" /reference:System.Drawing.dll /reference:System.Data.dll /reference:System.Core.dll (Join-Path $projectRoot 'Game\Features\CompanionNavigation.cs') (Join-Path $projectRoot 'tests\CompanionAI\CompanionTests.cs') (Join-Path $PSScriptRoot 'DisCityCombatBudgetTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Catalog combat fixtures did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'Revora.exe.config') -Destination "$exe.config"
Push-Location (Join-Path $projectRoot 'bin\Debug')
try { & $exe; if ($LASTEXITCODE -ne 0) { throw 'Catalog combat fixtures failed.' } }
finally { Pop-Location }
