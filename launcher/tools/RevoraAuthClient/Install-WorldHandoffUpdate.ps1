param([Parameter(Mandatory=$true)][string]$ClientRoot,[Parameter(Mandatory=$true)][string]$TrustFile)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Verify-WorldHandoffUpdate.ps1') -TrustFile $TrustFile
$root=(Resolve-Path -LiteralPath $ClientRoot).Path
$targets=@('Env_DX9\Revora-Conquer.exe','Env_DX9\RevoraAuthClient.dll','client-manifest.signed.json')
foreach($rel in $targets){$cursor=Join-Path $root $rel;while($cursor.Length-ge$root.Length){if((Get-Item -LiteralPath $cursor).Attributes-band[IO.FileAttributes]::ReparsePoint){throw 'Reparse client path'};$cursor=Split-Path $cursor}}
if(Get-CimInstance Win32_Process|Where-Object {$_.ExecutablePath-and($_.ExecutablePath.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase))}){throw 'Close the target client and launcher before installation'}
$backup=Join-Path (Split-Path $root) ('world-handoff-backup-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssffffZ'))
New-Item -ItemType Directory -Path $backup|Out-Null
$entries=@(foreach($rel in $targets){
 $source=Join-Path $root $rel;$saved=Join-Path $backup $rel
 $hash=(Get-FileHash -LiteralPath $source).Hash;$size=(Get-Item -LiteralPath $source).Length
 New-Item -ItemType Directory -Force -Path (Split-Path $saved)|Out-Null;Copy-Item -LiteralPath $source -Destination $saved
 if((Get-FileHash -LiteralPath $saved).Hash-ne$hash-or(Get-FileHash -LiteralPath $source).Hash-ne$hash){throw 'Backup verification failed'}
 [ordered]@{relativePath=$rel;sha256=$hash;size=$size}
})
[IO.File]::WriteAllText((Join-Path $backup 'backup-manifest.json'),($entries|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Restore-WorldHandoffUpdate.ps1') -Destination $backup
try{
 foreach($rel in $targets){
  $source=Join-Path (Join-Path $PSScriptRoot 'payload') $rel;$destination=Join-Path $root $rel;$temporary=$destination+'.world-handoff-new'
  if(Test-Path -LiteralPath $temporary){throw 'Staging collision'}
  Copy-Item -LiteralPath $source -Destination $temporary
  if((Get-FileHash -LiteralPath $source).Hash-ne(Get-FileHash -LiteralPath $temporary).Hash){throw 'Staging mismatch'}
  [IO.File]::Replace($temporary,$destination,[NullString]::Value)
  if((Get-FileHash -LiteralPath $destination).Hash-ne(Get-FileHash -LiteralPath $source).Hash){throw 'Installed mismatch'}
 }
}catch{& (Join-Path $backup 'Restore-WorldHandoffUpdate.ps1') -ClientRoot $root -BackupRoot $backup;throw}
"Installed exactly three files. Backup: $backup"
"Rollback: & '$backup\Restore-WorldHandoffUpdate.ps1' -ClientRoot '$root' -BackupRoot '$backup'"
