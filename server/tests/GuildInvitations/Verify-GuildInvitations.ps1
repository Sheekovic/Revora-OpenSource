param([string]$ArtifactRoot)
$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $ArtifactRoot) { $ArtifactRoot = Join-Path $project 'obj\counter-validation\bin' }
$output = (Resolve-Path -LiteralPath $ArtifactRoot).Path
$exe = Join-Path $output 'GuildInvitation.Tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:exe /platform:x86 /warnaserror "/out:$exe" "/reference:$output\Revora.exe" "/reference:$output\Npgsql.dll" /reference:System.Data.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Numerics.dll (Join-Path $PSScriptRoot 'GuildInvitationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Guild invitation tests did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'Revora.exe.config') -Destination "$exe.config"
Push-Location $output
try {
    & $exe
    if ($LASTEXITCODE -ne 0) { throw 'Guild invitation checks failed.' }
} finally { Pop-Location }
