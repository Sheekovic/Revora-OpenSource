param([Parameter(Mandatory=$true)][string]$PackageDirectory,[Parameter(Mandatory=$true)][string]$ClientRoot,[Parameter(Mandatory=$true)][string]$TrustFile,[Parameter(Mandatory=$true)][string]$TestRoot)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $TestRoot){throw 'Fresh test root required'}
$mock=Join-Path $TestRoot 'mock-client';New-Item -ItemType Directory -Path (Join-Path $mock 'Env_DX9') -Force|Out-Null
$targets=@('Env_DX9\Revora-Conquer.exe','Env_DX9\RevoraAuthClient.dll','client-manifest.signed.json')
$before=@{};foreach($rel in $targets){Copy-Item -LiteralPath (Join-Path $ClientRoot $rel) -Destination (Join-Path $mock $rel);$before[$rel]=(Get-FileHash -LiteralPath (Join-Path $mock $rel)).Hash}
& (Join-Path $PackageDirectory 'Install-WorldHandoffUpdate.ps1') -ClientRoot $mock -TrustFile $TrustFile
foreach($rel in $targets){if((Get-FileHash -LiteralPath (Join-Path $mock $rel)).Hash-ne(Get-FileHash -LiteralPath (Join-Path $PackageDirectory "payload\$rel")).Hash){throw 'Test install mismatch'}}
$backup=@(Get-ChildItem -LiteralPath $TestRoot -Directory -Filter 'world-handoff-backup-*')
if($backup.Count-ne 1){throw 'Unexpected backup count'}
& (Join-Path $backup[0].FullName 'Restore-WorldHandoffUpdate.ps1') -ClientRoot $mock -BackupRoot $backup[0].FullName
foreach($rel in $targets){if((Get-FileHash -LiteralPath (Join-Path $mock $rel)).Hash-ne$before[$rel]){throw 'Rollback failed'}}
# Force a second-file staging collision after the EXE replacement; the installer
# must restore all three original hashes rather than leave a mixed ABI pair.
[IO.File]::WriteAllBytes((Join-Path $mock 'Env_DX9\RevoraAuthClient.dll.world-handoff-new'),[byte[]]@(0))
$failed=$false;try{& (Join-Path $PackageDirectory 'Install-WorldHandoffUpdate.ps1') -ClientRoot $mock -TrustFile $TrustFile}catch{$failed=$true}
if(!$failed){throw 'Injected transaction failure was not rejected'}
foreach($rel in $targets){if((Get-FileHash -LiteralPath (Join-Path $mock $rel)).Hash-ne$before[$rel]){throw 'Automatic rollback failed'}}
# Mutate a disposable package, never an installed file.
$tamper=Join-Path $TestRoot 'tampered';Copy-Item -LiteralPath $PackageDirectory -Destination $tamper -Recurse
$file=Join-Path $tamper 'payload\Env_DX9\RevoraAuthClient.dll'
$stream=[IO.File]::Open($file,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite)
try{$stream.Position=512;$value=$stream.ReadByte();$stream.Position=512;$stream.WriteByte([byte]($value-bxor 1))}finally{$stream.Dispose()}
$rejected=$false;try{& (Join-Path $PackageDirectory 'Verify-WorldHandoffUpdate.ps1') -PackageDirectory $tamper -TrustFile $TrustFile}catch{$rejected=$true}
if(!$rejected){throw 'Tamper accepted'}
'PASS verified three-file apply, independently verified backup, exact rollback, tampered DLL rejected; canonical client not changed by this test'
