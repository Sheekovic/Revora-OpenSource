param([switch]$Create)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'obj\chi-refill-validation\bin'
$candidateRoot = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$passwordFile = Join-Path $candidateRoot 'account-password.dpapi.xml'
$exe = Join-Path $output 'ProvisionCandidateAccount.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /platform:x86 /warnaserror /main:ProvisionCandidateAccount `
    ("/out:" + $exe) ("/reference:" + (Join-Path $output 'Revora.exe')) ("/reference:" + (Join-Path $output 'Npgsql.dll')) /reference:System.Data.dll /reference:System.Numerics.dll `
    (Join-Path $PSScriptRoot 'ProvisionCandidateAccount.cs') (Join-Path $project 'tests\AccountLogin\SrpVerifierTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Candidate account helper did not compile.' }
Copy-Item -LiteralPath (Join-Path $project 'bin\Debug\Revora.exe.config') -Destination ($exe + '.config')
if ($Create) {
    if (Test-Path -LiteralPath $passwordFile) { throw 'Candidate account credential already exists; inspect before retrying.' }
    $random = New-Object byte[] 8
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($random)
    $secureAccount = ConvertTo-SecureString ([BitConverter]::ToString($random).Replace('-', '')) -AsPlainText -Force
    [Array]::Clear($random, 0, $random.Length); $rng.Dispose()
    $secureAccount | Export-Clixml -LiteralPath $passwordFile
} else { $secureAccount = Import-Clixml -LiteralPath $passwordFile }
$secureRuntime = Import-Clixml -LiteralPath (Join-Path $candidateRoot 'runtime-password.dpapi.xml')
$account = New-Object System.Management.Automation.PSCredential('MoonBoxTester', $secureAccount)
$runtime = New-Object System.Management.Automation.PSCredential('revora_candidate_runtime', $secureRuntime)
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $exe
$start.Arguments = if ($Create) { '--create' } else { '--verify' }
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['REVORA_DATABASE_PASSWORD'] = $runtime.GetNetworkCredential().Password
$start.EnvironmentVariables['REVORA_CANDIDATE_ACCOUNT_PASSWORD'] = $account.GetNetworkCredential().Password
$child = New-Object System.Diagnostics.Process
$child.StartInfo = $start
try {
    if (!$child.Start()) { throw 'Candidate account helper did not start.' }
    $start.EnvironmentVariables.Remove('REVORA_DATABASE_PASSWORD')
    $start.EnvironmentVariables.Remove('REVORA_CANDIDATE_ACCOUNT_PASSWORD')
    $stdout = $child.StandardOutput.ReadToEndAsync(); $stderr = $child.StandardError.ReadToEndAsync()
    if (!$child.WaitForExit(60000)) { $child.Kill(); throw 'Candidate account helper timed out.' }
    $result = $stdout.GetAwaiter().GetResult(); $stderr.GetAwaiter().GetResult() | Out-Null
    if ($child.ExitCode -ne 0) { throw 'Candidate account helper failed; inspect before retrying.' }
    Write-Output $result.Trim()
} finally {
    $start.EnvironmentVariables.Remove('REVORA_DATABASE_PASSWORD')
    $start.EnvironmentVariables.Remove('REVORA_CANDIDATE_ACCOUNT_PASSWORD')
    $account = $runtime = $null
    $secureAccount.Dispose(); $secureRuntime.Dispose(); $child.Dispose()
}
