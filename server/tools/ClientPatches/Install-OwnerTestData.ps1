[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$ClientPath,
 [Parameter(Mandatory=$true)][string]$PayloadPath,
 [ValidateSet('Check','Apply','Rollback')][string]$Mode='Check'
)
$ErrorActionPreference='Stop'
$expected=ConvertFrom-Json '{"ani/ItemMinIcon.ani":"b28e42bc4e50012b4a2ce48bd0000b930888b6f2365a3bea2b26fdcc5d9e135a","ani/MapItemIcon.ani":"d50e9fb49627d5b26c20f87493964401f13fc50733286e77bdc55283279dd682","ini/GameMap.dat":"86a54928a9682617371542cdb5a72707c799b92a5c820e3581183adaa94f32b0","ini/Questinfo.ini":"f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14","ini/emoneyshopv2.ini":"5663b8b102d779a5be7bdef9fe5e18c3b4e30058c643a910813adc969d89c941","ini/itemtype.dat":"ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753","ini/itemtype.txt":"52889c537ddd00a6f4d349fa9243268dfedc81ab3899523f1e434218735387cb","ini/monster.dat":"99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585"}'
$root=[IO.Path]::GetFullPath($ClientPath).TrimEnd('\')
$payload=[IO.Path]::GetFullPath($PayloadPath).TrimEnd('\')
if([IO.Path]::GetFileName($root) -ne 'Revora6711-OwnerTest'){throw 'Use a separate client copy named Revora6711-OwnerTest.'}
function Check-Path([string]$path){
 $full=[IO.Path]::GetFullPath($path)
 if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path escaped test client.'}
 $cursor=$full
 while($cursor){
  if(Test-Path -LiteralPath $cursor){
   if((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse points are not allowed in the client path.'}
  }
  $parent=[IO.Path]::GetDirectoryName($cursor)
  if($parent -eq $cursor){break};$cursor=$parent
 }
 return $full
}
function Hash([string]$path){if(!(Test-Path -LiteralPath $path -PathType Leaf)){return $null};return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
$exe=Check-Path (Join-Path $root 'Env_DX9\Conquer.exe')
if((Hash $exe) -ne '3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60'){throw 'Approved genuine6711 DX9 executable required.'}
foreach($process in Get-Process -Name Conquer -ErrorAction SilentlyContinue){
 if(!$process.Path -or $process.Path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Close the test client before patching.'}
}
$manifestPath=Join-Path $payload 'manifest.json'
if((Hash $manifestPath) -ne '511eb3a9fb659e17030f8972e9e3aa5feecc591b8a045d79794800c48731cd93'){throw 'Wrong reviewed data manifest.'}
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$entries=@($manifest.files | Where-Object { $_.path.StartsWith('client/') })
if($entries.Count -ne 8){throw 'Incomplete client payload.'}
$backup=Check-Path (Join-Path $root '.revora-owner-backup-v1')
foreach($entry in $entries){
 $relative=$entry.path.Substring(7)
 if(!$expected.PSObject.Properties[$relative]){throw 'Unreviewed client destination.'}
 $target=Check-Path (Join-Path $root $relative)
 $entry | Add-Member NoteProperty target $target
 $entry | Add-Member NoteProperty backup (Check-Path (Join-Path $backup $relative))
 $entry | Add-Member NoteProperty before $expected.$relative
 if((Hash (Join-Path $payload $entry.path)) -ne $entry.sha256){throw "Payload hash mismatch: $relative"}
 $current=Hash $target
 if($Mode -eq 'Rollback'){
  if($current -ne $entry.before -and $current -ne $entry.sha256){throw "Client changed after patch: $relative"}
  if((Hash $entry.backup) -ne $entry.before){throw "Backup mismatch: $relative"}
 }elseif($current -ne $entry.before){throw "Client preimage mismatch: $relative"}
 if(Test-Path -LiteralPath ($target+'.revora-new')){throw 'An interrupted temporary file needs review; no overwrite performed.'}
}
if($Mode -eq 'Check'){Write-Output 'PASS: approved test copy and all8 payload/preimage hashes; no files changed.';return}
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$lock=$null
$temporaries=New-Object 'System.Collections.Generic.List[string]'
try{
 $lock=[IO.File]::Open((Join-Path $backup 'operation.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
 if($Mode -eq 'Apply'){
  foreach($entry in $entries){
   if(Test-Path -LiteralPath $entry.backup){if((Hash $entry.backup) -ne $entry.before){throw 'Existing backup differs.'}}
   else{New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($entry.backup)) -Force | Out-Null;Copy-Item -LiteralPath $entry.target -Destination $entry.backup}
   if((Hash $entry.backup) -ne $entry.before){throw 'Backup readback failed.'}
  }
 }
 foreach($entry in $entries){
  $wanted=if($Mode -eq 'Rollback'){$entry.before}else{$entry.sha256}
  $source=if($Mode -eq 'Rollback'){$entry.backup}else{Join-Path $payload $entry.path}
  $current=Hash $entry.target
  if($current -eq $wanted){continue}
  if($current -ne $entry.before -and $current -ne $entry.sha256){throw 'Client changed during operation.'}
  $temporary=$entry.target+'.revora-new'
  $temporaries.Add($temporary)
  Copy-Item -LiteralPath $source -Destination $temporary
  if((Hash $temporary) -ne $wanted){throw 'Temporary copy verification failed.'}
  [IO.File]::Replace($temporary,$entry.target,[NullString]::Value)
  if((Hash $entry.target) -ne $wanted){throw 'Installed readback failed.'}
 }
 Write-Output "$Mode verified for all8 data files. Backups retained. No client launched."
}catch{
 $reason=$_.Exception.Message
 if($lock -and $Mode -eq 'Apply'){
  foreach($entry in $entries){
   if((Hash $entry.target) -eq $entry.sha256 -and (Hash $entry.backup) -eq $entry.before){
    Copy-Item -LiteralPath $entry.backup -Destination $entry.target
    if((Hash $entry.target) -ne $entry.before){throw 'Rollback readback failed; preserve backups and do not launch the test client.'}
   }
  }
 }
 throw "Operation failed: $reason Backups retained; do not launch until Check succeeds or Rollback is verified."
}finally{
 foreach($temporary in $temporaries){
  if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath (Check-Path $temporary)}
 }
 if($lock){$lock.Dispose()}
}
