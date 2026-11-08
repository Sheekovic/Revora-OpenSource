param(
    [string]$BuildDirectory = 'C:\Revora\DisposablePostgres\moon-box-candidate\game',
    [string]$ExpectedSha256 = '34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3'
)
$ErrorActionPreference = 'Stop'
$binary = Join-Path $BuildDirectory 'Revora.exe'
if ((Get-FileHash -LiteralPath $binary).Hash -ne $ExpectedSha256) { throw 'Reviewed candidate binary changed.' }
$exe = Join-Path $BuildDirectory 'RestoredCallbackTests.exe'
& "$env:WINDIR/Microsoft.NET/Framework/v4.0.30319/csc.exe" /nologo /target:exe /platform:x86 /warnaserror "/out:$exe" "/reference:$binary" /reference:System.Core.dll (Join-Path $PSScriptRoot 'RestoredCallbackTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Callback fixture compilation failed.' }
# No server Main or configuration loading. The pulse uses unopened services,
# an off-calendar clock and throttled war callbacks; no database is required.
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Callback failure-containment fixture failed.' }
