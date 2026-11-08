$ErrorActionPreference='Stop'
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path $project 'obj\pirate-rules'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$exe=Join-Path $output 'PirateRules.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /warnaserror /target:exe ("/out:"+$exe) (Join-Path $project 'Game\Features\PirateEpicRules.cs') (Join-Path $PSScriptRoot 'RulesTests.cs')
if($LASTEXITCODE -ne 0){throw 'Pirate rule checks did not compile.'}
& $exe
if($LASTEXITCODE -ne 0){throw 'Pirate rule checks failed.'}
