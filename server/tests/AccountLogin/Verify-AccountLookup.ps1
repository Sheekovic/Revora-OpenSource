$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$testExe = Join-Path $output 'AccountLookup.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:exe /platform:x86 /warnaserror /main:AccountLookupTests "/out:$testExe" "/reference:$output\Revora.exe" "/reference:$output\Npgsql.dll" /reference:System.Data.dll /reference:System.Numerics.dll (Join-Path $PSScriptRoot 'AccountLookupTests.cs') (Join-Path $PSScriptRoot 'SrpVerifierTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Account lookup tests did not compile.' }
Copy-Item -LiteralPath (Join-Path $project 'bin\Debug\Revora.exe.config') -Destination "$testExe.config"
$logDir = Join-Path $project 'obj\chi-refill-validation'
if (-not (Test-Path -LiteralPath $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
$log = Join-Path $logDir 'homecoming-lease-tests.log'
Push-Location (Join-Path $project 'bin\Debug')
try {
    & $testExe *>&1 | Tee-Object -FilePath $log
    if ($LASTEXITCODE -ne 0) { throw 'Account lookup checks failed.' }
}
finally { Pop-Location }
