param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Launcher', 'Website')]
    [string]$Component,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$directory = if ($Component -eq 'Launcher') { 'launcher' } else { 'website' }
$project = if ($Component -eq 'Launcher') { 'tests/PlayRevora.Launcher.FoundationTests' } else { 'tests/Revora.WebTests' }
Push-Location (Join-Path $repoRoot $directory)
try {
    & dotnet run --project $project -c $Configuration --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw "$Component tests failed." }
} finally {
    Pop-Location
}
