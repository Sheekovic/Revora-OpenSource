$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $root 'obj/chi-refill-validation/bin'
$test = Join-Path $output 'Cosmetics.Tests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 /warnaserror "/out:$test" "/reference:$output/Revora.exe" /reference:System.Core.dll (Join-Path $PSScriptRoot 'CosmeticsTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Cosmetics fixtures failed to compile' }
Write-Output ('Harness SHA256: ' + (Get-FileHash -LiteralPath $test).Hash)
& $test (Join-Path $root 'bin/Debug/database/hairface_storage_type.txt') (Join-Path $root 'obj/chi-refill-validation/cosmetic-catalog')
if ($LASTEXITCODE -ne 0) { throw 'Cosmetics fixtures failed' }
