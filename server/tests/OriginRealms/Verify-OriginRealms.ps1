param([string]$ArtifactRoot, [string]$RuntimeRoot = 'C:\Revora\DisposablePostgres\moon-box-candidate\game')
$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $ArtifactRoot) { $ArtifactRoot = Join-Path $project 'obj\counter-validation\bin' }
$output = (Resolve-Path -LiteralPath $ArtifactRoot).Path
$testExe = Join-Path $output 'OriginRealm.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:exe /platform:x86 /warnaserror /main:OriginRealmTests "/out:$testExe" "/reference:$output\Revora.exe" "/reference:$output\Npgsql.dll" /reference:System.Data.dll /reference:System.Core.dll /reference:System.Numerics.dll (Join-Path $PSScriptRoot 'OriginRealmTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Origin realm tests did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'Revora.exe.config') -Destination "$testExe.config"
Write-Output ('Test harness SHA256: ' + (Get-FileHash -LiteralPath $testExe).Hash)
Push-Location (Resolve-Path -LiteralPath $RuntimeRoot).Path
$savedPassword = $env:REVORA_DATABASE_PASSWORD
$secure = $null
try {
    if (-not $env:REVORA_DATABASE_PASSWORD) {
        $secure = Import-Clixml -LiteralPath (Join-Path (Split-Path $RuntimeRoot -Parent) 'runtime-password.dpapi.xml')
        $credential = New-Object System.Management.Automation.PSCredential('revora_candidate_runtime', $secure)
        $env:REVORA_DATABASE_PASSWORD = $credential.GetNetworkCredential().Password
    }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Origin realm checks failed.' }
}
finally {
    $env:REVORA_DATABASE_PASSWORD = $savedPassword
    $credential = $null
    if ($secure) { $secure.Dispose() }
    Pop-Location
}
