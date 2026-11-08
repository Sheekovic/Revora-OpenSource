param([switch]$Catalogs)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$exe = Join-Path $output 'PrepareMoonBoxSchema.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
& $compiler /nologo /platform:x86 ("/out:" + $exe) /reference:System.Data.dll `
    ("/reference:" + (Join-Path $output 'Revora.exe')) `
    ("/reference:" + (Join-Path $output 'Npgsql.dll')) `
    (Join-Path $PSScriptRoot 'PrepareMoonBoxSchema.cs')
if ($LASTEXITCODE -ne 0) { throw 'Schema preparation tool did not compile.' }
Copy-Item -LiteralPath (Join-Path $project 'bin\Debug\Revora.exe.config') -Destination ($exe + '.config')
Push-Location (Join-Path $project 'bin\Debug')
try {
    if ($Catalogs) { & $exe --catalogs } else { & $exe }
    if ($LASTEXITCODE -ne 0) { throw 'Schema preparation did not complete.' }
}
finally { Pop-Location }
