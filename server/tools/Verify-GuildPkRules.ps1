$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root 'obj\guild-pk-rules'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$exe=Join-Path $output 'CheckGuildPkRules.exe'
& $compiler /nologo /warnaserror+ /target:exe /out:$exe (Join-Path $root 'Game\Features\Tournaments\GuildPkRules.cs') (Join-Path $PSScriptRoot 'CheckGuildPkRules.cs')
if($LASTEXITCODE -ne 0){throw 'Guild PK rules compilation failed.'}
& $exe
if($LASTEXITCODE -ne 0){throw 'Guild PK rules validation failed.'}
