param([Parameter(Mandatory=$true)][string]$ClientRoot,[Parameter(Mandatory=$true)][string]$BackupRoot)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ClientRoot).Path;$backup=(Resolve-Path -LiteralPath $BackupRoot).Path
$allowed=@('Env_DX9\Revora-Conquer.exe','Env_DX9\RevoraAuthClient.dll','client-manifest.signed.json')
$entries=Get-Content -LiteralPath (Join-Path $backup 'backup-manifest.json') -Raw|ConvertFrom-Json
if($entries.Count-ne 3-or @($entries.relativePath|Select-Object -Unique).Count-ne 3){throw 'Invalid backup contract'}
if(Get-CimInstance Win32_Process|Where-Object {$_.ExecutablePath-and$_.ExecutablePath.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)}){throw 'Target process is running'}
foreach($entry in $entries){
 if($entry.relativePath-notin$allowed){throw 'Unapproved restore path'}
 foreach($base in @($root,$backup)){$cursor=Join-Path $base $entry.relativePath;while($cursor.Length-ge$base.Length){if((Get-Item -LiteralPath $cursor).Attributes-band[IO.FileAttributes]::ReparsePoint){throw 'Reparse restore path'};$cursor=Split-Path $cursor}}
 if((Get-FileHash -LiteralPath (Join-Path $backup $entry.relativePath)).Hash-ne$entry.sha256){throw 'Backup hash mismatch'}
}
foreach($entry in $entries){
 $target=Join-Path $root $entry.relativePath;$temp=$target+'.world-handoff-restore'
 if(Test-Path -LiteralPath $temp){throw 'Restore staging collision'}
 Copy-Item -LiteralPath (Join-Path $backup $entry.relativePath) -Destination $temp
 [IO.File]::Replace($temp,$target,[NullString]::Value)
 if((Get-FileHash -LiteralPath $target).Hash-ne$entry.sha256){throw 'Restore hash mismatch'}
}
'Verified exact three-file restoration'
