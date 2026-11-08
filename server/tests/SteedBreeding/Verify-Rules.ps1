$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = Join-Path $project 'obj/chi-refill-validation/SteedRulesTests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /warnaserror /target:exe "/out:$output" (Join-Path $project 'Game/Features/SteedBreedingRules.cs') (Join-Path $PSScriptRoot 'RulesTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Steed rules compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Steed rules checks failed.' }
