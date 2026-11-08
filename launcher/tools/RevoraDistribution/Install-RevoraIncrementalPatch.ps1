param([Parameter(Mandatory=$true)][string]$ClientRoot)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($ClientRoot);$package=[IO.Path]::GetFullPath($PSScriptRoot)
if((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Client root cannot be a reparse point.'}
$backup=Join-Path (Split-Path $root) ('revora-patch-backup-'+(Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))
New-Item -ItemType Directory -Path $backup | Out-Null
$files=@('PlayRevora.Launcher.exe','PlayRevora.Launcher.dll','PlayRevora.Launcher.deps.json','PlayRevora.Launcher.runtimeconfig.json','launcher.json','RevoraVersion.ini','client-manifest.signed.json','owner-test-public-trust.json','conquer.ico','1.url','2.url','readme.txt','ini\UStrRes.ini','ini\oem.ini','RevoraUpdates\State\patch-index.cached.signed.json','Revora-Server.dat','Env_DX9\Revora-Server.dat','Env_DX9\Revora-Conquer.exe','LauncherAssets\RevoraBridge.dll','LauncherAssets\RevoraBridgeLoader.exe','LauncherAssets\RevoraNativeLogin.dll','LauncherAssets\RevoraNativeLoginLoader.exe','Env_DX9\d3d9.dll')
$new=@();foreach($rel in $files){$dst=Join-Path $root $rel;if(Test-Path -LiteralPath $dst){$save=Join-Path $backup $rel;New-Item -ItemType Directory -Force -Path (Split-Path $save)|Out-Null;Copy-Item -LiteralPath $dst -Destination $save -Force}else{$new+=$rel}}
$new|Set-Content -LiteralPath (Join-Path $backup 'NEW_FILES.txt') -Encoding utf8
dotnet (Join-Path $package 'patcher\Revora.StaticClientPatcher.dll') apply (Join-Path $package 'patcher\revora-conquer-patch.json') (Join-Path $root 'Env_DX9\Conquer.exe') (Join-Path $root 'Env_DX9\Revora-Conquer.exe')
if($LASTEXITCODE){throw 'Static client patch failed.'}
Get-ChildItem -LiteralPath (Join-Path $package 'payload') -Force | Copy-Item -Destination $root -Recurse -Force
foreach($rel in @('LauncherAssets\RevoraBridge.dll','LauncherAssets\RevoraBridgeLoader.exe','LauncherAssets\RevoraNativeLogin.dll','LauncherAssets\RevoraNativeLoginLoader.exe','Env_DX9\d3d9.dll','Env_DX9\Revora-Server.dat')){$path=Join-Path $root $rel;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}
Set-Content -LiteralPath (Join-Path $root '.revora-last-backup') -Value $backup -Encoding ascii
"Installed. Rollback: & '$package\Rollback-RevoraIncrementalPatch.ps1' -ClientRoot '$root' -Backup '$backup'"
