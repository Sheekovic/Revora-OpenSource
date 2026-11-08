param([string]$BuildDirectory='obj\mrms2017-validation\bin')
$ErrorActionPreference='Stop'
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $project $BuildDirectory
$assembly=Join-Path $bin 'Revora.exe'
$test=Join-Path $bin 'KoTimerTests.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:exe /platform:x86 /warnaserror /main:KoTimerTests ("/out:"+$test) ("/reference:"+$assembly) ("/reference:"+(Join-Path $bin 'CO2_CORE_DLL.dll')) /reference:System.Core.dll (Join-Path $PSScriptRoot 'KoTimerTests.cs')
if($LASTEXITCODE -ne 0){throw 'KO timer fixture compilation failed.'}
& $test
if($LASTEXITCODE -ne 0){throw 'KO timer regression failed.'}
