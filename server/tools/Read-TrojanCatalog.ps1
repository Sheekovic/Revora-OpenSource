$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'obj/chi-refill-validation/bin'
$auditExe = Join-Path $output 'TrojanCatalogEvidence.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 /warnaserror "/out:$auditExe" "/reference:$output/Revora.exe" "/reference:$output/Npgsql.dll" /reference:System.Data.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'TrojanCatalogEvidence.cs')
if ($LASTEXITCODE -ne 0) { throw 'Catalog audit did not compile.' }
Copy-Item -LiteralPath (Join-Path $project 'bin/Debug/Revora.exe.config') -Destination "$auditExe.config"
Write-Host ('Read-only audit SHA256: ' + (Get-FileHash -LiteralPath $auditExe).Hash)
Push-Location (Join-Path $project 'bin/Debug')
try { & $auditExe; if ($LASTEXITCODE -ne 0) { throw 'Catalog audit failed.' } }
finally { Pop-Location }
