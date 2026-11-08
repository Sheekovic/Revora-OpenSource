$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$testExe = Join-Path $output 'SubclassShop.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /unsafe /warnaserror /main:SubclassShopTests "/out:$testExe" "/reference:$output\Revora.exe" "/reference:$output\Npgsql.dll" "/reference:$output\protobuf-net.dll" /reference:System.Drawing.dll /reference:System.Data.dll /reference:System.Core.dll (Join-Path $project 'Game\Features\CompanionNavigation.cs') (Join-Path $project 'tests\CompanionAI\CompanionTests.cs') (Join-Path $PSScriptRoot 'SubclassShopTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Subclass shop fixtures did not compile.' }
Copy-Item -LiteralPath (Join-Path $project 'bin\Debug\Revora.exe.config') -Destination "$testExe.config"
Write-Output ('Test harness SHA256: ' + (Get-FileHash -LiteralPath $testExe).Hash)
Push-Location (Join-Path $project 'bin\Debug')
try { & $testExe; if ($LASTEXITCODE -ne 0) { throw 'Subclass shop checks failed.' } }
finally { Pop-Location }
