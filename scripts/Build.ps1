param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Launcher', 'Website', 'Admin', 'Server')]
    [string]$Component,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    if ($Component -eq 'Server') {
        if (-not (Test-Path -LiteralPath 'server/app.config')) {
            throw 'Copy server/app.config.example to server/app.config and configure it locally first. See docs/SERVER_BUILD.md.'
        }
        $msbuild = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
        if ($msbuild) {
            $msbuildPath = $msbuild.Source
        } else {
            $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
            if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio MSBuild is required. See docs/SERVER_BUILD.md.' }
            $msbuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
            if (-not $msbuildPath) { throw 'MSBuild was not found.' }
        }
        & $msbuildPath 'server/Revora.csproj' "/p:Configuration=$Configuration" /p:Platform=x86 /p:ImportDirectoryBuildProps=false /p:ImportDirectoryBuildTargets=false /p:TreatWarningsAsErrors=false /p:Nullable=disable /nologo /verbosity:minimal
        if ($LASTEXITCODE -ne 0) { throw 'Game-server build failed. Check the external dependencies in docs/SERVER_BUILD.md.' }
    } else {
        $project = switch ($Component) {
            'Launcher' { 'launcher/PlayRevora.Launcher.sln' }
            'Website' { 'website/tests/Revora.WebTests/Revora.WebTests.csproj' }
            'Admin' { 'server/Admin/Revora.Admin.csproj' }
        }
        $restoreArgs = @('restore', $project, '--verbosity', 'minimal')
        if ($Component -eq 'Launcher') { $restoreArgs += '--locked-mode' }
        & dotnet @restoreArgs
        if ($LASTEXITCODE -ne 0) { throw "$Component restore failed." }
        & dotnet build $project -c $Configuration --no-restore --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "$Component build failed." }
    }
} finally {
    Pop-Location
}
