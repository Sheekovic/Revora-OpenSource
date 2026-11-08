$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$root = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$exe = Join-Path $output 'CheckCandidateArenaStartup.exe'
$geoSource = Join-Path $root 'game\GeoIP.dat'
$geoTarget = Join-Path $output 'GeoIP.dat'
$manifest = Get-Content -LiteralPath (Join-Path $root 'game-runtime-manifest.json') -Raw | ConvertFrom-Json
$geoEntry = @($manifest.files | Where-Object path -eq 'GeoIP.dat')
if ($geoEntry.Count -ne 1 -or (Get-FileHash -LiteralPath $geoSource).Hash -ne $geoEntry[0].sha256) { throw 'Candidate GeoIP catalog identity mismatch.' }
if (!(Test-Path -LiteralPath $geoTarget)) { Copy-Item -LiteralPath $geoSource -Destination $geoTarget }
if ((Get-FileHash -LiteralPath $geoTarget).Hash -ne $geoEntry[0].sha256) { throw 'Probe GeoIP catalog identity mismatch.' }
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /platform:x86 /warnaserror /unsafe `
    ("/out:" + $exe) ("/reference:" + (Join-Path $output 'Revora.exe')) `
    ("/reference:" + (Join-Path $output 'Npgsql.dll')) /reference:System.Data.dll `
    (Join-Path $PSScriptRoot 'CheckCandidateArenaStartup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Candidate Arena helper did not compile.' }
Copy-Item -LiteralPath (Join-Path $root 'game\Revora.exe.config') -Destination ($exe + '.config')
$secure = Import-Clixml -LiteralPath (Join-Path $root 'runtime-password.dpapi.xml')
$credential = New-Object System.Management.Automation.PSCredential('revora_candidate_runtime', $secure)
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $exe
$start.WorkingDirectory = Join-Path $root 'game'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['REVORA_DATABASE_PASSWORD'] = $credential.GetNetworkCredential().Password
$child = New-Object System.Diagnostics.Process
$child.StartInfo = $start
try {
    if (!$child.Start()) { throw 'Candidate Arena helper did not start.' }
    $start.EnvironmentVariables.Remove('REVORA_DATABASE_PASSWORD')
    $stdout = $child.StandardOutput.ReadToEndAsync(); $stderr = $child.StandardError.ReadToEndAsync()
    if (!$child.WaitForExit(30000)) { $child.Kill(); $child.WaitForExit(); throw 'Candidate Arena helper timed out.' }
    $result = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
    $result = $result.Replace($credential.GetNetworkCredential().Password, '[REDACTED]')
    Write-Output $result.Trim()
    if ($child.ExitCode -ne 0) { throw 'Candidate Arena helper failed.' }
    Write-Output ('Candidate Arena assembly SHA256: ' + (Get-FileHash -LiteralPath (Join-Path $output 'Revora.exe')).Hash)
} finally {
    $start.EnvironmentVariables.Remove('REVORA_DATABASE_PASSWORD')
    $credential = $null; $secure.Dispose(); $child.Dispose()
}
