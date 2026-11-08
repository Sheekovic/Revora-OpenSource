param([string]$ArtifactRoot, [switch]$PveNobilityOnly, [switch]$TeratoGeometryOnly, [switch]$SnowHealingOnly, [switch]$ArrowWearOnly, [switch]$FrostGazeOnly, [switch]$StaminaRecoveryOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $ArtifactRoot) { $ArtifactRoot = Join-Path $projectRoot 'obj\chi-refill-validation\bin' }
$output = (Resolve-Path -LiteralPath $ArtifactRoot).Path
$assembly = Join-Path $output 'Revora.exe'
$testExe = Join-Path $output 'Combat.Tests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x86 /unsafe /warnaserror /main:CombatTests "/out:$testExe" "/reference:$assembly" /reference:System.Drawing.dll /reference:System.Data.dll /reference:System.Core.dll (Join-Path $projectRoot 'Game\Features\CompanionNavigation.cs') (Join-Path $projectRoot 'tests\CompanionAI\CompanionTests.cs') (Join-Path $PSScriptRoot 'CombatTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Combat fixtures did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'Revora.exe.config') -Destination "$testExe.config"
Write-Output ('Testing isolated server SHA256: ' + (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash)
if ((@($PveNobilityOnly,$TeratoGeometryOnly,$SnowHealingOnly,$ArrowWearOnly,$FrostGazeOnly,$StaminaRecoveryOnly) | Where-Object { $_ }).Count -gt 1) { throw 'Choose one focused combat suite.' }
if ($StaminaRecoveryOnly) { & $testExe --stamina-recovery-only } elseif ($FrostGazeOnly) { & $testExe --frost-gaze-only } elseif ($ArrowWearOnly) { & $testExe --arrow-wear-only } elseif ($SnowHealingOnly) { & $testExe --snow-healing-only } elseif ($TeratoGeometryOnly) { & $testExe --terato-geometry-only } elseif ($PveNobilityOnly) { & $testExe --pve-nobility-only } else { & $testExe }
if ($LASTEXITCODE -ne 0) { throw 'Combat fixtures failed.' }
