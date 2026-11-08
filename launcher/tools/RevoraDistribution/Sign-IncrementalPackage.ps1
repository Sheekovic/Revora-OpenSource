param(
 [Parameter(Mandatory=$true)][string]$PackageDirectory,
 [Parameter(Mandatory=$true)][string]$TrustFile,
 [string]$Version='revora-7630-rc1',
 [string]$CngKeyName='PlayRevoraOwnerTest20260831V2'
)
$ErrorActionPreference='Stop';$package=(Resolve-Path $PackageDirectory).Path;$trust=Get-Content $TrustFile -Raw|ConvertFrom-Json
$entries=@(Get-ChildItem $package -File -Recurse|Where-Object Name -ne 'incremental-manifest.signed.json'|Sort-Object FullName|ForEach-Object{[ordered]@{relativePath=$_.FullName.Substring($package.Length+1).Replace('\','/');size=[long]$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}})
$payload=[ordered]@{schemaVersion=1;version=$Version;files=$entries};$canonical=$payload|ConvertTo-Json -Depth 8 -Compress
$cng=[Security.Cryptography.CngKey]::Open($CngKeyName)
try{$key=[Security.Cryptography.ECDsaCng]::new($cng);try{$signature=$key.SignData([Text.Encoding]::UTF8.GetBytes($canonical),[Security.Cryptography.HashAlgorithmName]::SHA256)}finally{$key.Dispose()}}finally{$cng.Dispose()}
$signed=[ordered]@{schemaVersion=1;version=$Version;files=$entries;signature=[ordered]@{algorithm='ECDSA_P256_SHA256';keyId=$trust.keyId;valueBase64=[Convert]::ToBase64String($signature)}}
[IO.File]::WriteAllText((Join-Path $package 'incremental-manifest.signed.json'),($signed|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
