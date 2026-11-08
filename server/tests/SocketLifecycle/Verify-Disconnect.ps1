param([string]$BuildPath='obj/mrms2017-validation/bin')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path $root $BuildPath
$assembly=Join-Path $output 'Revora.exe'
$testExe=Join-Path $output 'SocketDisconnect.Tests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /platform:x86 /warnaserror "/out:$testExe" "/reference:$assembly" /reference:System.Core.dll (Join-Path $PSScriptRoot 'DisconnectTests.cs')
if($LASTEXITCODE -ne 0){throw 'Socket lifecycle fixture compilation failed.'}
Write-Output ('Testing private server SHA256: '+(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash)
& $testExe
if($LASTEXITCODE -ne 0){throw 'Socket lifecycle fixture failed.'}
