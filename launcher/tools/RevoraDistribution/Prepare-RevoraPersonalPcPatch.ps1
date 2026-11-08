param(
    [Parameter(Mandatory=$true)][string]$CanonicalClient,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$Version='revora-7630-personal-pc-1',
    [string]$CngKeyName='PlayRevoraOwnerTest20260831V2'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$root = (Resolve-Path $CanonicalClient).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Output directory must be new.' }
New-Item -ItemType Directory -Path $output | Out-Null
$scratch = Join-Path (Split-Path $output) ('.revora-personal-build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
try {
    dotnet build (Join-Path $repo 'tools\Revora.BinaryDelta\Revora.BinaryDelta.csproj') -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Delta tool build failed.' }
    dotnet build (Join-Path $repo 'tools\PlayRevora.ClientManifestSigner\PlayRevora.ClientManifestSigner.csproj') -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Manifest signer build failed.' }
    $deltaTool = Join-Path $repo 'tools\Revora.BinaryDelta\bin\Release\net8.0'
    New-Item -ItemType Directory -Path (Join-Path $output 'tools'),(Join-Path $output 'delta'),(Join-Path $output 'payload') | Out-Null
    foreach ($name in @('Revora.BinaryDelta.dll','Revora.BinaryDelta.deps.json','Revora.BinaryDelta.runtimeconfig.json')) { Copy-Item -LiteralPath (Join-Path $deltaTool $name) -Destination (Join-Path $output 'tools') }

    $sourceExe = Join-Path $root 'Env_DX9\Conquer.exe'
    $targetExe = Join-Path $root 'Env_DX9\Revora-Conquer.exe'
    $deltaPath = Join-Path $output 'delta\Revora-Conquer.original-to-current.rvdelta'
    & dotnet (Join-Path $output 'tools\Revora.BinaryDelta.dll') create $sourceExe $targetExe $deltaPath
    if ($LASTEXITCODE -ne 0) { throw 'Delta creation failed.' }
    $secondDelta = Join-Path $scratch 'determinism.rvdelta'
    & dotnet (Join-Path $output 'tools\Revora.BinaryDelta.dll') create $sourceExe $targetExe $secondDelta
    if ((Get-FileHash $deltaPath -Algorithm SHA256).Hash -ne (Get-FileHash $secondDelta -Algorithm SHA256).Hash) { throw 'Delta determinism check failed.' }
    $applied = Join-Path $scratch 'applied.exe'; $reversed = Join-Path $scratch 'reversed.exe'
    & dotnet (Join-Path $output 'tools\Revora.BinaryDelta.dll') apply $deltaPath $sourceExe $applied
    & dotnet (Join-Path $output 'tools\Revora.BinaryDelta.dll') reverse $deltaPath $targetExe $reversed
    if ((Get-FileHash $applied -Algorithm SHA256).Hash -ne (Get-FileHash $targetExe -Algorithm SHA256).Hash -or
        (Get-FileHash $reversed -Algorithm SHA256).Hash -ne (Get-FileHash $sourceExe -Algorithm SHA256).Hash) { throw 'Delta round-trip verification failed.' }

    $trust = Join-Path $root 'owner-test-public-trust.json'
    $unsigned = Join-Path $scratch 'client-manifest.unsigned.json'
    $signed = Join-Path $output 'payload\client-manifest.signed.json'
    $signer = Join-Path $repo 'tools\PlayRevora.ClientManifestSigner\bin\Release\net8.0-windows\win-x64\PlayRevora.ClientManifestSigner.dll'
    if (-not (Test-Path -LiteralPath $signer)) { $signer = Join-Path $repo 'tools\PlayRevora.ClientManifestSigner\bin\Release\net8.0-windows\PlayRevora.ClientManifestSigner.dll' }
    & dotnet $signer refresh-revora-dx9 (Join-Path $root 'client-manifest.signed.json') $trust $root $unsigned "$Version-client" '2027-09-05T23:59:59Z'
    if ($LASTEXITCODE -ne 0) { throw 'Client manifest refresh failed.' }
    & dotnet $signer sign-windows $unsigned $CngKeyName $signed
    if ($LASTEXITCODE -ne 0) { throw 'Client manifest signing failed.' }
    & dotnet $signer verify $signed $trust
    if ($LASTEXITCODE -ne 0) { throw 'Client manifest verification failed.' }

    $payloadFiles = @(
        '1.url','2.url','conquer.ico','readme.txt','version.dat','Revora-Server.dat','ini\oem.ini','ini\UStrRes.ini',
        'Env_DX9\RevoraAuthClient.dll','PlayRevora.Launcher.exe','PlayRevora.Launcher.dll','PlayRevora.Launcher.deps.json',
        'PlayRevora.Launcher.runtimeconfig.json','launcher.json','owner-test-public-trust.json','RevoraVersion.ini'
    )
    $operations = @()
    foreach ($relative in $payloadFiles) {
        $from = Join-Path $root $relative; if (-not (Test-Path -LiteralPath $from -PathType Leaf)) { throw "Missing payload input: $relative" }
        $to = Join-Path $output ('payload\' + $relative); New-Item -ItemType Directory -Force -Path (Split-Path $to) | Out-Null; Copy-Item -LiteralPath $from -Destination $to
        $item = Get-Item -LiteralPath $to
        $operations += [ordered]@{packagePath=('payload/' + $relative.Replace('\','/'));destination=$relative.Replace('\','/');size=$item.Length;sha256=(Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash}
    }
    $manifestOperation = [ordered]@{packagePath='payload/client-manifest.signed.json';destination='client-manifest.signed.json';size=(Get-Item $signed).Length;sha256=(Get-FileHash $signed -Algorithm SHA256).Hash}
    $operations += $manifestOperation
    $contract = [ordered]@{
        schemaVersion=1; packageVersion=$Version;
        binaryDelta=[ordered]@{sourcePath='Env_DX9/Conquer.exe';sourceSize=(Get-Item $sourceExe).Length;sourceSha256=(Get-FileHash $sourceExe -Algorithm SHA256).Hash;destinationPath='Env_DX9/Revora-Conquer.exe';destinationSize=(Get-Item $targetExe).Length;destinationSha256=(Get-FileHash $targetExe -Algorithm SHA256).Hash;deltaPath='delta/Revora-Conquer.original-to-current.rvdelta'};
        files=$operations
    }
    [IO.File]::WriteAllText((Join-Path $output 'install-contract.json'),($contract|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $repo 'tools\RevoraDistribution\Install-RevoraPersonalPcPatch.ps1') -Destination $output
    Copy-Item -LiteralPath (Join-Path $repo 'tools\RevoraDistribution\Rollback-RevoraPersonalPcPatch.ps1') -Destination $output
    $instructions = @"
# Revora Patch-7630 Personal-PC Conversion

Prerequisite: the .NET 8 Desktop Runtime and an intact Patch-7630 client whose `Env_DX9\\Conquer.exe` matches the approved SHA-256. This package does not contain or alter licensing state.

Install from PowerShell:

````powershell
Set-ExecutionPolicy -Scope Process Bypass
& '.\\Install-RevoraPersonalPcPatch.ps1' -ClientRoot 'C:\\Path\\To\\Conquer-7630'
````

Then run `PlayRevora.Launcher.exe`. The original `Conquer.exe` is retained unchanged. The package creates `Env_DX9\\Revora-Conquer.exe` by a hash-gated deterministic delta.
"@
    [IO.File]::WriteAllText((Join-Path $output 'README_FIRST.md'),$instructions,[Text.UTF8Encoding]::new($false))
    & (Join-Path $repo 'tools\RevoraDistribution\Sign-IncrementalPackage.ps1') -PackageDirectory $output -TrustFile $trust -Version $Version -CngKeyName $CngKeyName
    & dotnet (Join-Path $output 'tools\Revora.BinaryDelta.dll') verify-package $output 'revora-owner-test-windows-2026-08-31' 'MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAECPc2HbKjroApJlEEkpQV/Bvt7iobTQLsyyY4BY6qaKJ6Cr9GT5VKjBUzgSyopdmMXav9q24q/DRTRlrdLdQ05Q=='
    if ($LASTEXITCODE -ne 0) { throw 'Final signed package verification failed.' }
    Write-Output "Prepared $output"
}
finally { if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force } }
