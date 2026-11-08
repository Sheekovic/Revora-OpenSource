param([switch]$SkipNative, [switch]$ReleaseRuntime)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts/tests'
$runtimeArgs = @()
if ($ReleaseRuntime) { $runtimeArgs = @('-p:RevoraRelease=true') }
dotnet run --project "$root/Tests/Revora.ClientEndpoint.Tests.csproj" --configuration Release @runtimeArgs -- $artifacts
if ($LASTEXITCODE -ne 0) { throw 'Managed endpoint tests failed' }
dotnet build "$root/Gui/Revora.ClientEndpoint.csproj" --configuration Release @runtimeArgs
if ($LASTEXITCODE -ne 0) { throw 'GUI build failed' }
dotnet build "$root/Cli/Revora.ClientEndpoint.Cli.csproj" --configuration Release @runtimeArgs
if ($LASTEXITCODE -ne 0) { throw 'CLI build failed' }
if ($SkipNative) { return }
$vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio C++ x86 tools are required for native ABI tests' }
$vcvars = Join-Path $vs 'VC/Auxiliary/Build/vcvars32.bat'
$source = Join-Path $root 'Tests/native_abi.c'
$native = Join-Path $artifacts 'native-abi.exe'
$object = Join-Path $artifacts 'native-abi.obj'
# cmd is used only for the compiler environment, never filesystem cleanup.
$buildCommand = Join-Path $artifacts 'build-native.cmd'
@"
@echo off
call "$vcvars" >nul
if errorlevel 1 exit /b 1
cl /nologo /W4 /WX /MT /O2 "$source" /Fo"$object" /Fe"$native" /link /MACHINE:X86
exit /b %errorlevel%
"@ | Set-Content -LiteralPath $buildCommand -Encoding ASCII
& $buildCommand
if ($LASTEXITCODE -ne 0) { throw 'Native ABI harness build failed' }
foreach ($case in (Get-Content (Join-Path $artifacts 'native-cases.json') -Raw | ConvertFrom-Json)) {
    & $native (Join-Path $artifacts $case.file) $case.ip $case.port
    if ($LASTEXITCODE -ne 0) { throw "Native ABI test failed: $($case.file)" }
}
