param([Parameter(Mandatory=$true)][string]$ClientRoot,[Parameter(Mandatory=$true)][string]$Backup)
$ErrorActionPreference='Stop';$root=[IO.Path]::GetFullPath($ClientRoot);$save=[IO.Path]::GetFullPath($Backup)
if(-not (Test-Path -LiteralPath $save) -or -not $save.StartsWith((Split-Path $root),[StringComparison]::OrdinalIgnoreCase)){throw 'Backup missing or outside the client parent.'}
if(Test-Path -LiteralPath (Join-Path $save 'NEW_FILES.txt')){Get-Content -LiteralPath (Join-Path $save 'NEW_FILES.txt')|Where-Object{$_}|ForEach-Object{$dst=Join-Path $root $_;if(Test-Path -LiteralPath $dst){Remove-Item -LiteralPath $dst -Force}}}
Get-ChildItem -LiteralPath $save -File -Recurse | Where-Object Name -ne 'NEW_FILES.txt' | ForEach-Object {$rel=$_.FullName.Substring($save.Length+1);$dst=Join-Path $root $rel;New-Item -ItemType Directory -Force -Path (Split-Path $dst)|Out-Null;Copy-Item -LiteralPath $_.FullName -Destination $dst -Force}
"Restored $save"
