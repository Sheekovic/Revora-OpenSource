param(
    [Parameter(Mandatory=$true)][string]$AssemblyDirectory,
    [Parameter(Mandatory=$true)][string]$CatalogDirectory
)
$ErrorActionPreference = 'Stop'
$AssemblyDirectory = (Resolve-Path -LiteralPath $AssemblyDirectory).Path
$CatalogDirectory = (Resolve-Path -LiteralPath $CatalogDirectory).Path
$testExe = Join-Path $AssemblyDirectory 'CollectionPrestige.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:exe /platform:x86 /warnaserror "/out:$testExe" "/reference:$AssemblyDirectory\Revora.exe" (Join-Path $PSScriptRoot 'CollectionPrestigeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Collection prestige fixture compilation failed.' }
Copy-Item -LiteralPath "$AssemblyDirectory\Revora.exe.config" -Destination "$testExe.config"
Push-Location $CatalogDirectory
try {
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Collection prestige fixture failed.' }
} finally { Pop-Location }
