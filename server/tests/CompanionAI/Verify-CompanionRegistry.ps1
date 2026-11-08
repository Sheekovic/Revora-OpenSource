param([string]$BuildPath='obj\mrms2017-validation\bin')
$ErrorActionPreference='Stop'
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path $project $BuildPath
$assembly=Join-Path $output 'Revora.exe'
$test=Join-Path $output 'CompanionRegistry.Tests.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe') /nologo /target:exe /platform:x86 /unsafe /warnaserror "/out:$test" "/reference:$assembly" /reference:System.Core.dll (Join-Path $PSScriptRoot 'CompanionRegistryTests.cs')
if($LASTEXITCODE -ne 0){throw 'Companion registry fixture failed to compile.'}
# In-memory map only: no configuration, database, listeners or server startup.
& $test
if($LASTEXITCODE -ne 0){throw 'Companion registry checks failed.'}
