param([Parameter(Mandatory=$true)][string]$ArtifactRoot)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=(Resolve-Path -LiteralPath $ArtifactRoot).Path
$exe=Join-Path $output 'Unarmed.Budget.Tests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x86 /unsafe /warnaserror /main:UnarmedExtractionBudgetTests "/out:$exe" "/reference:$output\Revora.exe" /reference:System.Drawing.dll /reference:System.Core.dll (Join-Path $projectRoot 'Game\Features\CompanionNavigation.cs') (Join-Path $projectRoot 'tests\CompanionAI\CompanionTests.cs') (Join-Path $PSScriptRoot 'UnarmedExtractionBudgetTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Unarmed fixture did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'Revora.exe.config') -Destination "$exe.config"
Push-Location (Join-Path $projectRoot 'bin\Debug')
try { & $exe; if ($LASTEXITCODE -ne 0) { throw 'Unarmed budget failed.' } }
finally { Pop-Location }
