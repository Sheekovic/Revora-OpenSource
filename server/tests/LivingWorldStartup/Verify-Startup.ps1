param([string]$BuildDirectory='obj/mrms2017-validation/bin')
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$build=(Resolve-Path -LiteralPath (Join-Path $repo $BuildDirectory)).Path
$exe=Join-Path $build 'LivingWorldStartup.Tests.exe'
& "$env:WINDIR/Microsoft.NET/Framework/v4.0.30319/csc.exe" /nologo /platform:x86 /warnaserror ("/out:"+$exe) ("/reference:"+(Join-Path $build 'Revora.exe')) (Join-Path $PSScriptRoot 'StartupTests.cs')
if($LASTEXITCODE -ne 0){throw 'Startup regression did not compile.'}
& $exe
if($LASTEXITCODE -ne 0){throw 'Startup initialization ownership regression failed.'}
