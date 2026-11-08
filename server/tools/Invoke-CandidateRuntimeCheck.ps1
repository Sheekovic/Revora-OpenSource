param([switch]$Create)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$candidateRoot = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$passwordFile = Join-Path $candidateRoot 'runtime-password.dpapi.xml'
$exe = Join-Path $output 'ProvisionCandidateRuntime.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /platform:x86 /warnaserror `
    ("/out:" + $exe) ("/reference:" + (Join-Path $output 'Npgsql.dll')) /reference:System.Data.dll `
    (Join-Path $PSScriptRoot 'ProvisionCandidateRuntime.cs')
if ($LASTEXITCODE -ne 0) { throw 'Candidate runtime helper did not compile.' }
Copy-Item -LiteralPath (Join-Path $project 'bin\Debug\Revora.exe.config') -Destination ($exe + '.config')
if ($Create) {
    if (Test-Path -LiteralPath $passwordFile) { throw 'Runtime credential already exists; inspect before retrying.' }
    $random = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($random)
    $securePassword = ConvertTo-SecureString ([Convert]::ToBase64String($random)) -AsPlainText -Force
    [Array]::Clear($random, 0, $random.Length); $rng.Dispose()
    $securePassword | Export-Clixml -LiteralPath $passwordFile
} else { $securePassword = Import-Clixml -LiteralPath $passwordFile }
$runtime = New-Object System.Management.Automation.PSCredential('revora_candidate_runtime', $securePassword)
$bootstrapSecure = Import-Clixml -LiteralPath (Join-Path $candidateRoot 'bootstrap-password.dpapi.xml')
$bootstrap = New-Object System.Management.Automation.PSCredential('revora_candidate_admin', $bootstrapSecure)
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $exe
$start.Arguments = if ($Create) { '--create' } else { '--verify' }
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['REVORA_DATABASE_PASSWORD'] = $runtime.GetNetworkCredential().Password
if ($Create) { $start.EnvironmentVariables['REVORA_CANDIDATE_BOOTSTRAP_PASSWORD'] = $bootstrap.GetNetworkCredential().Password }
$child = New-Object System.Diagnostics.Process
$child.StartInfo = $start
try {
    if (!$child.Start()) { throw 'Candidate runtime helper did not start.' }
    $start.EnvironmentVariables.Remove('REVORA_DATABASE_PASSWORD')
    $start.EnvironmentVariables.Remove('REVORA_CANDIDATE_BOOTSTRAP_PASSWORD')
    $stdout = $child.StandardOutput.ReadToEndAsync()
    $stderr = $child.StandardError.ReadToEndAsync()
    if (!$child.WaitForExit(60000)) { $child.Kill(); throw 'Candidate runtime helper timed out.' }
    $result = $stdout.GetAwaiter().GetResult()
    $stderr.GetAwaiter().GetResult() | Out-Null
    if ($child.ExitCode -ne 0) { throw 'Candidate runtime helper failed; inspect before retrying.' }
    Write-Output $result.Trim()
} finally {
    $start.EnvironmentVariables.Remove('REVORA_DATABASE_PASSWORD')
    $start.EnvironmentVariables.Remove('REVORA_CANDIDATE_BOOTSTRAP_PASSWORD')
    $runtime = $bootstrap = $null
    $securePassword.Dispose(); $bootstrapSecure.Dispose(); $child.Dispose()
}
