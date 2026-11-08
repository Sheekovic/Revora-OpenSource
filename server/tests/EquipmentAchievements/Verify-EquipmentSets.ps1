param([switch]$Earning)
# Uses only the existing disposable candidate; never starts the game process.
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validation = Join-Path $repoRoot 'obj/mrms2017-validation/bin'
$fixtureName = if ($Earning) {'EquipmentSetEarning.Tests.exe'} else {'EquipmentSetTests.exe'}
$logName = if ($Earning) {'equipment-earning-tests.log'} else {'equipment-set-tests.log'}
if ($Earning) {
    & python (Join-Path $PSScriptRoot 'compile_earning.py')
} else {
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 /warnaserror /main:EquipmentSetTests "/out:$validation/EquipmentSetTests.exe" "/reference:$validation/Revora.exe" "/reference:$validation/Npgsql.dll" "/reference:$validation/CO2_CORE_DLL.dll" /reference:System.Core.dll /reference:System.Data.dll (Join-Path $PSScriptRoot 'SetTests.cs')
}
if ($LASTEXITCODE -ne 0) { throw 'Equipment fixture compilation failed.' }
Copy-Item -LiteralPath (Join-Path $validation 'Revora.exe.config') -Destination (Join-Path $validation ($fixtureName+'.config'))
$candidateRoot = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$data = Join-Path $candidateRoot 'data'
$toolRoot = 'C:\Revora\DisposablePostgres\tools-17.11-4\pgsql\bin'
$hashes = @{
    'postgres.exe' = 'F1B4E561FF3C6BE6FC7DB26B916BA756508AA0E02C4B4047E5B2DA9CA8C77396'
    'pg_ctl.exe' = 'EED85E36AABC7CDF99A63BDA0A2E33FA3BCE8D3C59D6BC12E9E19469706CC012'
    'psql.exe' = 'E2B971FFE755010DA9F9A3699F62A5F3A5D6000B054B5D80D0E973245F44DEDE'
    'pg_restore.exe' = '95D4165D034CDE580C4514036A2305267043F34B801F0B4C9C2656CA242371EB'
}
foreach ($name in $hashes.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $toolRoot $name)).Hash -ne $hashes[$name]) {
        throw ('Candidate tool identity mismatch: ' + $name)
    }
}
if (Test-Path -LiteralPath (Join-Path $data 'postmaster.pid')) {
    throw 'Candidate has a PID file; inspect its state before running the owned start/stop check.'
}
if (@(Get-NetTCPConnection -State Listen -LocalPort 25439 -ErrorAction SilentlyContinue).Count) {
    throw 'Candidate port is already in use.'
}
$hba = Get-Content -LiteralPath (Join-Path $data 'pg_hba.conf') | Where-Object { $_ -match '^\s*(host|local)\s' }
if (!$hba -or @($hba | Where-Object { $_ -notmatch '\s+scram-sha-256\s*$' }).Count) {
    throw 'Candidate authentication must use SCRAM on every configured connection rule.'
}
$secure = Import-Clixml -LiteralPath (Join-Path $candidateRoot 'bootstrap-password.dpapi.xml')
$credential = New-Object System.Management.Automation.PSCredential('revora_candidate_admin', $secure)
$pgctl = Join-Path $toolRoot 'pg_ctl.exe'
function Invoke-CandidateControl([string]$Action) {
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $pgctl
    $start.Arguments = '-D "' + $data + '" -l "' + (Join-Path $candidateRoot 'postgres.log') + '" -w -t 15 -m fast ' + $Action
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $control = New-Object System.Diagnostics.Process
    $control.StartInfo = $start
    try {
        if (!$control.Start()) { throw 'Candidate lifecycle tool did not start.' }
        # Wait for pg_ctl itself, not inherited stream handles held by postgres.
        if (!$control.WaitForExit(20000)) { $control.Kill(); throw 'Candidate lifecycle command timed out.' }
        if ($control.ExitCode -ne 0) { throw ('Candidate lifecycle command failed: ' + $Action) }
    } finally { $control.Dispose() }
}
function Invoke-CandidateProbe([bool]$WrongPassword) {
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $toolRoot 'psql.exe'
    $start.Arguments = '-X -w -A -t -h 127.0.0.1 -p 25439 -U revora_candidate_admin -d postgres -c "SELECT current_setting(''data_directory'') = ''C:/Revora/DisposablePostgres/moon-box-candidate/data'' AND inet_server_addr() = ''127.0.0.1''::inet AND inet_server_port() = 25439;"'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.EnvironmentVariables['PGCONNECT_TIMEOUT'] = '5'
    $start.EnvironmentVariables['PGPASSWORD'] = if ($WrongPassword) { [Guid]::NewGuid().ToString('N') } else { $credential.GetNetworkCredential().Password }
    $probe = New-Object System.Diagnostics.Process
    $probe.StartInfo = $start
    try {
        if (!$probe.Start()) { throw 'Candidate probe did not start.' }
        $start.EnvironmentVariables.Remove('PGPASSWORD')
        $stdout = $probe.StandardOutput.ReadToEndAsync()
        $stderr = $probe.StandardError.ReadToEndAsync()
        if (!$probe.WaitForExit(10000)) { $probe.Kill(); throw 'Candidate probe timed out.' }
        $result = $stdout.GetAwaiter().GetResult().Trim()
        $errorText = $stderr.GetAwaiter().GetResult()
        if ($WrongPassword) {
            if ($probe.ExitCode -eq 0 -or $errorText -notmatch 'password authentication failed') {
                throw 'Candidate did not demonstrate password rejection.'
            }
        } elseif ($probe.ExitCode -ne 0 -or $result -ne 't') {
            throw 'Candidate database identity probe failed.'
        }
    } finally {
        $start.EnvironmentVariables.Remove('PGPASSWORD')
        $probe.Dispose()
    }
}
try {
    Invoke-CandidateControl 'start'
    Invoke-CandidateProbe $false
    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder['Host']='127.0.0.1'; $builder['Port']=25439; $builder['Database']='revora_candidate_game'
    $builder['Username']=$credential.UserName; $builder['Password']=$credential.GetNetworkCredential().Password
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $validation $fixtureName
    $start.WorkingDirectory=$validation; $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $start.EnvironmentVariables['REVORA_QUARANTINE_TEST_CONNECTION']=$builder.ConnectionString
    $child=New-Object System.Diagnostics.Process; $child.StartInfo=$start
    try {
        if (!$child.Start()) {throw 'Equipment fixture did not start.'}
        $stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
        if (!$child.WaitForExit(60000)) {$child.Kill();$child.WaitForExit();throw 'Equipment fixture timed out.'}
        $out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path (Join-Path $repoRoot 'obj') $logName),$out+$err)
        if ($child.ExitCode -ne 0) {throw 'Equipment fixture failed; inspect its local log.'}
        Write-Output $out
    } finally {$start.EnvironmentVariables.Remove('REVORA_QUARANTINE_TEST_CONNECTION');$builder['Password']='';$child.Dispose()}
} finally {
    if (Test-Path -LiteralPath (Join-Path $data 'postmaster.pid')) { Invoke-CandidateControl 'stop' }
    $credential = $null; $secure.Dispose()
}
