[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not [IO.File]::Exists($compiler)) { throw 'Windows .NET Framework C# compiler is unavailable.' }
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($OutputDirectory)) | Out-Null
$output = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'Revora.GraphicsModuleObserver.exe'
& $compiler /nologo /target:exe /platform:x86 /optimize+ /warnaserror+ /out:$output (Join-Path $PSScriptRoot 'Revora.GraphicsModuleObserver.cs')
if ($LASTEXITCODE -ne 0) { throw 'Graphics module observer compilation failed.' }
Write-Output $output
