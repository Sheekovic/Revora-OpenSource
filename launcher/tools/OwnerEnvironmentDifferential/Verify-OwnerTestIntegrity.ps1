[CmdletBinding()]
param([string]$PackageDirectory=$PSScriptRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$manifest=Get-Content (Join-Path $PackageDirectory 'owner-test-integrity.signed.json') -Raw -Encoding UTF8|ConvertFrom-Json
$trust=Get-Content (Join-Path $PackageDirectory 'owner-test-public-trust.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($manifest.schemaVersion-ne 1-or$manifest.signature.algorithm-ne'ECDSA_P256_SHA256'-or$manifest.signature.keyId-ne$trust.keyId){throw 'Unsupported or mismatched Owner-Test integrity metadata.'}
$canonical=[ordered]@{schemaVersion=$manifest.schemaVersion;label=$manifest.label;files=@($manifest.files|Sort-Object relativePath|ForEach-Object{[ordered]@{relativePath=$_.relativePath;size=[long]$_.size;sha256=$_.sha256}})}|ConvertTo-Json -Depth 8 -Compress
$spki=[Convert]::FromBase64String($trust.publicKeySpkiBase64)
if($spki.Length-lt 65-or$spki[$spki.Length-65]-ne 4){throw 'Unsupported Owner-Test P-256 public key encoding.'}
$blob=New-Object byte[] 72
[BitConverter]::GetBytes([uint32]0x31534345).CopyTo($blob,0);[BitConverter]::GetBytes([uint32]32).CopyTo($blob,4)
[Array]::Copy($spki,$spki.Length-64,$blob,8,64)
$cng=[Security.Cryptography.CngKey]::Import($blob,[Security.Cryptography.CngKeyBlobFormat]::EccPublicBlob)
try{$key=New-Object Security.Cryptography.ECDsaCng($cng);try{if(-not$key.VerifyData([Text.Encoding]::UTF8.GetBytes($canonical),[Convert]::FromBase64String($manifest.signature.valueBase64),[Security.Cryptography.HashAlgorithmName]::SHA256)){throw 'Owner-Test integrity signature is invalid.'}}finally{$key.Dispose()}}finally{$cng.Dispose()}
foreach($entry in $manifest.files){$path=[IO.Path]::GetFullPath((Join-Path $PackageDirectory $entry.relativePath));if(-not$path.StartsWith(([IO.Path]::GetFullPath($PackageDirectory)).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe package path.'};$item=Get-Item -LiteralPath $path;if($item.Length-ne$entry.size-or(Get-FileHash $path -Algorithm SHA256).Hash-ne$entry.sha256){throw "Integrity mismatch: $($entry.relativePath)"}}
Write-Host "Verified Owner-Test diagnostic package: $(@($manifest.files).Count) readable files." -ForegroundColor Green
