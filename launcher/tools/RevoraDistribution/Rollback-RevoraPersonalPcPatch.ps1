param([Parameter(Mandatory=$true)][string]$ClientRoot,[Parameter(Mandatory=$true)][string]$Backup)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$forbidden = @('regkey.dat','Env_DX9/regkey.dat')
$root = [IO.Path]::GetFullPath($ClientRoot).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
$save = [IO.Path]::GetFullPath($Backup).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
if (-not $save.StartsWith((Split-Path $root) + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Backup is outside the client parent.' }
$state = Get-Content -LiteralPath (Join-Path $save 'rollback-state.json') -Raw | ConvertFrom-Json
if ($state.schemaVersion -ne 1 -or $state.clientRoot -ne $root) { throw 'Rollback state does not match this client.' }
foreach ($entry in $state.replaced) {
    $relative = $entry.path.Replace('\','/')
    if ($forbidden -contains $relative -or $relative.Contains('..') -or [IO.Path]::IsPathRooted($relative)) { throw 'Unsafe rollback path.' }
    $source = Join-Path $save ('files\' + $relative.Replace('/',[IO.Path]::DirectorySeparatorChar))
    if ((Get-Item $source).Length -ne [long]$entry.size -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Backup mismatch: $relative" }
    $destination = Join-Path $root $relative.Replace('/',[IO.Path]::DirectorySeparatorChar)
    New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}
foreach ($relativeValue in $state.created) {
    $relative = ([string]$relativeValue).Replace('\','/')
    if ($forbidden -contains $relative -or $relative.Contains('..') -or [IO.Path]::IsPathRooted($relative)) { throw 'Unsafe rollback path.' }
    $destination = Join-Path $root $relative.Replace('/',[IO.Path]::DirectorySeparatorChar)
    if (Test-Path -LiteralPath $destination -PathType Leaf) { Remove-Item -LiteralPath $destination -Force }
}
Write-Output "REVORA_PERSONAL_PATCH_ROLLED_BACK"
