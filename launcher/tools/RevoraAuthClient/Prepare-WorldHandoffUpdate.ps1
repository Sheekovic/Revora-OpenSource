param([Parameter(Mandatory=$true)][string]$ClientRoot,[Parameter(Mandatory=$true)][string]$OutputRoot,[Parameter(Mandatory=$true)][string]$TrustFile,[string]$Version='revora-7630-world-handoff-v2.1')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputRoot){throw 'Fresh output directory required'}
$repo=Split-Path (Split-Path $PSScriptRoot)
$signer=Join-Path $repo 'tools\PlayRevora.ClientManifestSigner\bin\Release\net8.0-windows\win-x64\PlayRevora.ClientManifestSigner.dll'
$package=Join-Path $OutputRoot 'package';$mirror=Join-Path $OutputRoot 'signing-input'
New-Item -ItemType Directory -Force -Path (Join-Path $mirror 'Env_DX9'),(Join-Path $mirror 'ini'),(Join-Path $package 'payload\Env_DX9')|Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'out\Revora-Worker-Handoff.exe') -Destination (Join-Path $mirror 'Env_DX9\Revora-Conquer.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'out\Release\RevoraAuthClient.dll') -Destination (Join-Path $mirror 'Env_DX9\RevoraAuthClient.dll')
foreach($rel in @('Revora-Server.dat','ini\oem.ini')){Copy-Item -LiteralPath (Join-Path $ClientRoot $rel) -Destination (Join-Path $mirror $rel)}
$unsigned=Join-Path $OutputRoot 'client-manifest.unsigned.json';$signed=Join-Path $package 'payload\client-manifest.signed.json'
& dotnet $signer refresh-revora-dx9 (Join-Path $ClientRoot 'client-manifest.signed.json') $TrustFile $mirror $unsigned $Version '2027-09-05T00:00:00Z'
if($LASTEXITCODE){throw 'Manifest refresh failed'}
& dotnet $signer sign-windows $unsigned 'PlayRevoraOwnerTest20260831V2' $signed
if($LASTEXITCODE){throw 'Manifest signing failed'}
& dotnet $signer verify $signed $TrustFile
if($LASTEXITCODE){throw 'Production manifest verifier failed'}
foreach($name in @('Revora-Conquer.exe','RevoraAuthClient.dll')){Copy-Item -LiteralPath (Join-Path $mirror "Env_DX9\$name") -Destination (Join-Path $package "payload\Env_DX9\$name")}
foreach($name in @('Install-WorldHandoffUpdate.ps1','Restore-WorldHandoffUpdate.ps1','Verify-WorldHandoffUpdate.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $package}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'out\direct-worker-patch.json') -Destination $package
& (Join-Path $repo 'tools\RevoraDistribution\Sign-IncrementalPackage.ps1') -PackageDirectory $package -TrustFile $TrustFile -Version $Version
& (Join-Path $package 'Verify-WorldHandoffUpdate.ps1') -TrustFile $TrustFile
$zip=Join-Path $OutputRoot ($Version+'.zip');Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -CompressionLevel Optimal
$fresh=Join-Path $OutputRoot 'fresh';Expand-Archive -LiteralPath $zip -DestinationPath $fresh
& (Join-Path $fresh 'Verify-WorldHandoffUpdate.ps1') -TrustFile $TrustFile
& dotnet $signer verify (Join-Path $fresh 'payload\client-manifest.signed.json') $TrustFile
if($LASTEXITCODE){throw 'Fresh runtime signature verification failed'}
Get-Item -LiteralPath $zip|Select-Object FullName,Length
Get-FileHash -LiteralPath $zip
