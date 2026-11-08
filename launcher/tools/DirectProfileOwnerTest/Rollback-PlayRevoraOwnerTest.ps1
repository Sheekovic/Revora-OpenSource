[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ClientRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Safe([string]$root,[string]$relative){if([IO.Path]::IsPathRooted($relative)-or$relative.Contains('..')-or$relative.StartsWith('\')){throw 'Unsafe receipt path.'};$p=[IO.Path]::GetFullPath((Join-Path $root $relative));if(-not$p.StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Receipt path escaped client root.'};$p}
& (Join-Path $PSScriptRoot 'Verify-OwnerTestIntegrity.ps1') -PackageDirectory $PSScriptRoot
$root=[IO.Path]::GetFullPath($ClientRoot).TrimEnd('\');$state=Safe $root 'RevoraUpdates\State';$receiptPath=Join-Path $state 'direct-profile-installed.json';$receipt=Get-Content $receiptPath -Raw -Encoding UTF8|ConvertFrom-Json;$rollback=Safe $root ('RevoraUpdates\Rollback\'+$receipt.transactionId)
if(@(Get-Process PlayRevora.Launcher,play,AutoP,Conquer -ErrorAction SilentlyContinue).Count){throw 'Launcher and game processes must be closed.'}
$reverse=@($receipt.files);[array]::Reverse($reverse);foreach($e in $reverse){$target=Safe $root $e.relativePath;if((Get-FileHash $target -Algorithm SHA256).Hash-ne$e.sha256){throw "Installed file changed: $($e.relativePath)"};if($e.existed){$backup=Safe $rollback $e.relativePath;Copy-Item $backup ($target+'.restore') -Force;[IO.File]::Replace(($target+'.restore'),$target,($target+'.owner-test'));Remove-Item ($target+'.owner-test') -Force}else{Remove-Item $target -Force}}
Remove-Item $receiptPath -Force;Write-Host 'PlayRevora OWNER-TEST rollback completed.' -ForegroundColor Green
