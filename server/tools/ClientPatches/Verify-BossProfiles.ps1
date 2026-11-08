param([ValidateSet('boss-hud-space','abyss-boss-hud','warrior','ancient-boss-hud')][string]$Profile='boss-hud-space')
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\Administrator\Desktop\Revora'
$fixture = if ($Profile -eq 'warrior') { 'abyss-boss-hud' } else { $Profile }
$work = Join-Path $root ("Source\Revora-6711\obj\chi-refill-validation\" + $fixture)
$native = if ($Profile -eq 'ancient-boss-hud') { Join-Path $root 'Source\Revora-6711\obj\chi-refill-validation\abyss-boss-hud\native' } else { Join-Path $work 'native' }
$source = Join-Path $PSScriptRoot 'VerifyBossProfiles.cs'
New-Item -ItemType Directory -Path (Join-Path $native 'ini') -Force | Out-Null
# Only the fixed, hash-gated dependency set may enter this disposable fixture.
foreach ($entry in [regex]::Matches((Get-Content $source -Raw), '\{"([^"]+)","([a-f0-9]{64})"\}')) {
    $relative = $entry.Groups[1].Value
    $inputFile = Join-Path "$root\6711\Env_DX9" $relative
    if ((Get-FileHash -LiteralPath $inputFile).Hash -ne $entry.Groups[2].Value) { throw "Unexpected approved dependency: $relative" }
    $destination = Join-Path $native $relative
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    if (-not (Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $destination).Hash -ne $entry.Groups[2].Value) { Copy-Item -LiteralPath $inputFile -Destination $destination -Force }
}
$probe = Join-Path $native 'VerifyBossProfiles.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /platform:x86 /warnaserror "/out:$probe" $source
if ($LASTEXITCODE -ne 0) { throw 'Native fixture compilation failed.' }
Write-Output ('Probe SHA256: ' + (Get-FileHash -LiteralPath $probe).Hash)
foreach ($variant in @('before', 'after')) {
    $folder = if ($variant -eq 'before') { 'originals' } else { 'staged' }
    Copy-Item -LiteralPath (Join-Path $work "$folder\ini\monster.dat") -Destination (Join-Path $native 'ini\monster.dat') -Force
    $mode = if ($Profile -eq 'ancient-boss-hud') { 'ancient-' + $variant } elseif ($Profile -eq 'warrior') { 'warrior-' + $variant } elseif ($Profile -eq 'abyss-boss-hud') { 'abyss-' + $variant } else { $variant }
    $outputLog = Join-Path $work ("native-" + $mode + '.txt')
    $errorLog = Join-Path $work ("native-" + $mode + '.stderr.txt')
    $process = Start-Process -FilePath $probe -ArgumentList $mode -WorkingDirectory $native -WindowStyle Hidden -PassThru -RedirectStandardOutput $outputLog -RedirectStandardError $errorLog
    # Retain the process handle before waiting so Windows PowerShell can read its exit code.
    $probeHandle = $process.Handle
    if (-not $process.WaitForExit(20000)) { $process.Kill(); $process.WaitForExit(); throw "Native fixture timed out: $mode" }
    $process.WaitForExit()
    Get-Content -LiteralPath $outputLog
    if ($process.ExitCode -ne 0) { throw "Native boss profile check failed: $mode (exit $($process.ExitCode)); inspect $errorLog" }
}
