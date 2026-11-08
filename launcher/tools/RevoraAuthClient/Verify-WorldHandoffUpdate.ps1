param([string]$PackageDirectory=$PSScriptRoot,[Parameter(Mandatory=$true)][string]$TrustFile)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\')
$trust=Get-Content -LiteralPath $TrustFile -Raw|ConvertFrom-Json
$spki=[Convert]::FromBase64String($trust.publicKeySpkiBase64)
$sha=[Security.Cryptography.SHA256]::Create()
try{$fingerprint=[BitConverter]::ToString($sha.ComputeHash($spki)).Replace('-','')}finally{$sha.Dispose()}
if($fingerprint-ne'EF7595AB459EB08776532A66D96C2A6ED8C224615E6CD561FC0A6E066B892908'){throw 'Unapproved signing identity'}
$path=Join-Path $root 'incremental-manifest.signed.json'
if((Get-Item -LiteralPath $path).Length-gt 65536){throw 'Oversized integrity contract'}
$manifest=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
if($manifest.schemaVersion-ne 1-or$manifest.signature.algorithm-ne'ECDSA_P256_SHA256'-or$manifest.signature.keyId-ne$trust.keyId){throw 'Invalid signing contract'}
$entries=@($manifest.files|ForEach-Object{[ordered]@{relativePath=$_.relativePath;size=[long]$_.size;sha256=$_.sha256}})
$canonical=[ordered]@{schemaVersion=1;version=$manifest.version;files=$entries}|ConvertTo-Json -Depth 8 -Compress
# Exact DER SPKI prefix for the already-pinned P-256 public identity.
if($spki.Length-ne 91-or[BitConverter]::ToString($spki,0,27).Replace('-','')-ne'3059301306072A8648CE3D020106082A8648CE3D03010703420004'){throw 'Invalid P-256 encoding'}
$blob=New-Object byte[] 72
[BitConverter]::GetBytes([uint32]0x31534345).CopyTo($blob,0);[BitConverter]::GetBytes([uint32]32).CopyTo($blob,4)
[Array]::Copy($spki,27,$blob,8,64)
$cng=[Security.Cryptography.CngKey]::Import($blob,[Security.Cryptography.CngKeyBlobFormat]::EccPublicBlob)
try{$key=[Security.Cryptography.ECDsaCng]::new($cng);try{if(!$key.VerifyData([Text.Encoding]::UTF8.GetBytes($canonical),[Convert]::FromBase64String($manifest.signature.valueBase64),[Security.Cryptography.HashAlgorithmName]::SHA256)){throw 'Invalid package signature'}}finally{$key.Dispose()}}finally{$cng.Dispose()}
$seen=@{}
foreach($entry in $entries){
 $rel=$entry.relativePath
 if($rel-notmatch '^[A-Za-z0-9_.-]+(/[A-Za-z0-9_.-]+)*$'-or$rel.Split('/')-contains'..'-or$seen.ContainsKey($rel)){throw 'Unsafe/duplicate package path'}
 $seen[$rel]=$true;$file=Join-Path $root $rel
 $cursor=$file;while($cursor.Length-ge$root.Length){if((Get-Item -LiteralPath $cursor).Attributes-band[IO.FileAttributes]::ReparsePoint){throw 'Reparse package path'};$cursor=Split-Path $cursor}
 $item=Get-Item -LiteralPath $file
 if($item.Length-ne$entry.size-or(Get-FileHash -LiteralPath $file).Hash-ne$entry.sha256){throw "Package hash mismatch: $rel"}
}
foreach($rel in @('payload/Env_DX9/Revora-Conquer.exe','payload/Env_DX9/RevoraAuthClient.dll','payload/client-manifest.signed.json')){if(!$seen.ContainsKey($rel)){throw 'Missing required payload'}}
"Verified pinned signed update: $($manifest.version), $($entries.Count) entries"
