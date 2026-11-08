param([Parameter(Mandatory=$true)][System.Management.Automation.PSCredential]$Credential)
$ErrorActionPreference = 'Stop'
$root = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'obj\mrms2017-validation\bin'
$manifest = Get-Content -LiteralPath (Join-Path $root 'game-runtime-manifest.json') -Raw | ConvertFrom-Json
if (@(Get-NetTCPConnection -State Listen -LocalPort 19558,19559,19619 -ErrorAction SilentlyContinue).Count) { throw 'Candidate game must be stopped for catalog preparation.' }
foreach ($entry in @($manifest.files | Where-Object { $_.path -match '^[^/\\]+\.dll$' })) {
    if ((Get-FileHash -LiteralPath (Join-Path $output $entry.path)).Hash -ne $entry.sha256) { throw 'Candidate helper dependency identity mismatch.' }
}
$exe = Join-Path $output 'PrepareCandidateQuestCatalog.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /platform:x86 /warnaserror "/out:$exe" "/reference:$output\Npgsql.dll" /reference:System.Data.dll (Join-Path $PSScriptRoot 'PrepareCandidateQuestCatalog.cs')
if ($LASTEXITCODE -ne 0) { throw 'Candidate quest helper compilation failed.' }
Copy-Item -LiteralPath (Join-Path $root 'game\Revora.exe.config') -Destination ($exe+'.config') -Force
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $exe; $start.WorkingDirectory = $output
$start.UseShellExecute = $false; $start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
$start.EnvironmentVariables['REVORA_CANDIDATE_PASSWORD'] = $Credential.GetNetworkCredential().Password
$child = New-Object System.Diagnostics.Process
$child.StartInfo = $start
try {
    if (!$child.Start()) { throw 'Candidate quest helper did not start.' }
    $start.EnvironmentVariables.Remove('REVORA_CANDIDATE_PASSWORD')
    $stdout = $child.StandardOutput.ReadToEndAsync(); $stderr = $child.StandardError.ReadToEndAsync()
    if (!$child.WaitForExit(15000)) { $child.Kill(); $child.WaitForExit(); throw 'Candidate quest helper timed out.' }
    $text = $stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
    if ($child.ExitCode -ne 0) { throw $text }
    Write-Output $text.Trim()
} finally { $start.EnvironmentVariables.Remove('REVORA_CANDIDATE_PASSWORD'); $child.Dispose() }
