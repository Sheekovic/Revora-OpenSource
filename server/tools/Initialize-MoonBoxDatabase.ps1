$ErrorActionPreference = 'Stop'
# Local candidate only. This script does not read the live game configuration.
$candidateRoot = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$toolRoot = 'C:\Revora\DisposablePostgres\tools-17.11-4\pgsql\bin'
$initdb = Join-Path $toolRoot 'initdb.exe'
$expectedHash = '17A208D1F8C5671E8EAF74FC80E3300326CBE15862DBEFBC7490C882BA395FE0'
if ((Get-FileHash -LiteralPath $initdb -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Candidate initdb identity does not match the reviewed tool package.'
}
if (Test-Path -LiteralPath $candidateRoot) {
    throw 'Candidate directory already exists; inspect it instead of overwriting or reinitializing it.'
}
New-Item -ItemType Directory -Path $candidateRoot | Out-Null
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
$acl = New-Object System.Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true, $false)
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$acl.AddAccessRule($rule)
Set-Acl -LiteralPath $candidateRoot -AclObject $acl

$random = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($random)
$password = [Convert]::ToBase64String($random)
[Array]::Clear($random, 0, $random.Length)
$rng.Dispose()
$secure = ConvertTo-SecureString $password -AsPlainText -Force
# Windows DPAPI protects this value for the current user. No plaintext file.
$secure | Export-Clixml -LiteralPath (Join-Path $candidateRoot 'bootstrap-password.dpapi.xml')
$data = Join-Path $candidateRoot 'data'
$pipeName = 'revora-moon-init-' + [Guid]::NewGuid().ToString('N')
$pipeSecurity = New-Object System.IO.Pipes.PipeSecurity
$pipeSecurity.AddAccessRule((New-Object System.IO.Pipes.PipeAccessRule($identity, 'FullControl', 'Allow')))
$pipe = New-Object System.IO.Pipes.NamedPipeServerStream($pipeName, 'Out', 1, 'Byte', 'Asynchronous', 4096, 4096, $pipeSecurity)
$waiting = $pipe.BeginWaitForConnection($null, $null)
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $initdb
$start.Arguments = '--pgdata="' + $data + '" --username=revora_candidate_admin --auth=scram-sha-256 --pwfile="\\.\pipe\' + $pipeName + '" --encoding=UTF8 --locale=C --data-checksums --no-instructions'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$process = New-Object System.Diagnostics.Process
$process.StartInfo = $start
try {
    if (!$process.Start()) { throw 'Candidate initialization could not start.' }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $process.StandardInput.Close()
    if (!$waiting.AsyncWaitHandle.WaitOne(15000)) {
        if (!$process.HasExited) { $process.Kill() }
        throw 'Candidate did not open its private password pipe.'
    }
    $pipe.EndWaitForConnection($waiting)
    $writer = New-Object System.IO.StreamWriter($pipe)
    $writer.WriteLine($password)
    $writer.Flush()
    $writer.Dispose()
    $password = $null
    if (!$process.WaitForExit(60000)) {
        $process.Kill()
        throw 'Candidate initialization timed out; inspect the candidate before retrying.'
    }
    $stdout.GetAwaiter().GetResult() | Out-Null
    $stderr.GetAwaiter().GetResult() | Out-Null
    if ($process.ExitCode -ne 0) {
        throw ('Candidate initialization failed with exit code ' + $process.ExitCode + '; no raw credential-adjacent output was persisted.')
    }
    @"

# Isolated Moon Box acceptance candidate; no public listener.
listen_addresses = '127.0.0.1'
port = 25439
max_connections = 20
shared_buffers = '32MB'
password_encryption = 'scram-sha-256'
log_statement = 'none'
log_min_error_statement = 'panic'
"@ | Add-Content -LiteralPath (Join-Path $data 'postgresql.conf') -Encoding ASCII
    if ((Get-Content -LiteralPath (Join-Path $data 'PG_VERSION') -Raw).Trim() -ne '17') {
        throw 'Unexpected candidate database version.'
    }
    Write-Output 'Initialized PostgreSQL 17 candidate with SCRAM authentication; cluster remains stopped.'
    Write-Output 'Candidate: C:\Revora\DisposablePostgres\moon-box-candidate; loopback port: 25439.'
} catch {
    # Windows initdb may relaunch itself with a restricted token. Clean up only
    # initializers from this tool path targeting this exact candidate directory.
    $initializers = @(Get-CimInstance Win32_Process -Filter "Name='initdb.exe'" |
        Where-Object { $_.ExecutablePath -eq $initdb -and $_.CommandLine -and $_.CommandLine.Contains($data) })
    foreach ($initializer in $initializers) {
        Stop-Process -Id $initializer.ProcessId -ErrorAction Stop
    }
    throw
} finally {
    $password = $null
    $secure.Dispose()
    $pipe.Dispose()
    $process.Dispose()
}
