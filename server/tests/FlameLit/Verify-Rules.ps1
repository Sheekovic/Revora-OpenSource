$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$directory = Join-Path $project 'obj/chi-refill-validation'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$output = Join-Path $directory 'FlameLitRulesTests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /warnaserror /target:exe "/out:$output" (Join-Path $project 'Game/Features/FlameLitRules.cs') (Join-Path $PSScriptRoot 'RulesTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Flame Lit rules compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Flame Lit rules checks failed.' }
