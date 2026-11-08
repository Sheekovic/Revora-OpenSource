$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$directory = Join-Path $project 'obj/chi-refill-validation'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$output = Join-Path $directory 'EliminationRulesTests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /warnaserror /target:exe "/out:$output" (Join-Path $project 'Game/Features/EliminationRules.cs') (Join-Path $PSScriptRoot 'RulesTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Elimination rules compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Elimination rules checks failed.' }
