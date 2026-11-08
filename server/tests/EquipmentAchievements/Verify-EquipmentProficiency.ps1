param()
$ErrorActionPreference='Stop'
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $project 'obj\mrms2017-validation\bin'
$assembly=Join-Path $bin 'Revora.exe'
if(!(Test-Path -LiteralPath $assembly)){throw 'Build the private validation assembly first.'}
$test=Join-Path $bin 'EquipmentProficiencyTests.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:exe /platform:x86 /warnaserror /main:EquipmentProficiencyTests ("/out:"+$test) ("/reference:"+$assembly) ("/reference:"+(Join-Path $bin 'CO2_CORE_DLL.dll')) /reference:System.Core.dll (Join-Path $PSScriptRoot 'EquipmentProficiencyTests.cs')
if($LASTEXITCODE -ne 0){throw 'Equipment proficiency fixture compilation failed.'}
& $test
if($LASTEXITCODE -ne 0){throw 'Equipment proficiency checks failed.'}
