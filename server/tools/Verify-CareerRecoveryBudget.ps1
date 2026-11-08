param(
    [string]$ArtifactRoot = 'obj\counter-validation\bin',
    [string]$CatalogPath = 'C:\Revora\DisposablePostgres\moon-box-candidate\game\database\itemtype-new.txt'
)
$ErrorActionPreference = 'Stop'
$budgetRoot = Split-Path $PSScriptRoot -Parent
if (![IO.Path]::IsPathRooted($ArtifactRoot)) { $ArtifactRoot = Join-Path $budgetRoot $ArtifactRoot }
$budgetAssembly = Join-Path $ArtifactRoot 'Revora.exe'
$budgetOutput = Join-Path $ArtifactRoot 'CareerRecoveryBudget.exe'
$budgetSource = Join-Path $PSScriptRoot 'CheckCareerRecoveryBudget.cs'
$ledger = Get-Content -Raw -LiteralPath (Join-Path $budgetRoot 'docs\research\CAREER_RECORDED_EXPENSES.json') | ConvertFrom-Json
$cycles = @($ledger.ledgers | Where-Object source -eq 'reincarnation')
if ($cycles.Count -ne 1) { throw 'Expected one reviewed reincarnation ledger.' }
$row = [IO.File]::ReadLines($CatalogPath) | Where-Object { $_.StartsWith('723700@@') } | Select-Object -First 1
if (!$row) { throw 'EXP Ball catalog row missing.' }
$parts = $row.Split(@('@@'), [StringSplitOptions]::RemoveEmptyEntries)
$price = [uint32]$parts[36]
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:exe /platform:x86 /warnaserror "/out:$budgetOutput" "/reference:$budgetAssembly" $budgetSource
if ($LASTEXITCODE -ne 0) { throw 'Recovery budget fixture compilation failed.' }
Copy-Item -LiteralPath "$budgetAssembly.config" -Destination "$budgetOutput.config"
& $budgetOutput $price $cycles[0].receipts
if ($LASTEXITCODE -ne 0) { throw 'Recovery budget fixture failed.' }
