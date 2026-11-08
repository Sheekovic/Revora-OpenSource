param(
    [ValidateSet('Check', 'Prepare', 'Start', 'Stop')][string]$Mode = 'Start',
    [ValidateRange(1024, 65535)][int]$DatabasePort = 25432,
    [ValidateRange(1024, 65535)][int]$WebPort = 5080,
    [switch]$WithDatabase,
    [string]$GameRuntimeDirectory,
    [ValidatePattern('^$|^[A-Fa-f0-9]{64}$')][string]$GameExecutableSha256,
    [string]$GameSchemaSql,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repoRoot '.local'
$isWindowsHost = $env:OS -eq 'Windows_NT'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$workspaceIdentityPath = if ($isWindowsHost) { $repoRoot.ToLowerInvariant() } else { $repoRoot }
$workspaceId = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($utf8.GetBytes($workspaceIdentityPath)))).Replace('-', '').Substring(0, 12).ToLowerInvariant()
$container = "revora-dev-$workspaceId"
$volume = "$container-data"
$script:redactions = @()

function Quote-Argument([string]$Value) {
    '"' + [regex]::Replace([regex]::Replace($Value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}

function Invoke-Tool([string]$File, [string[]]$Arguments, [string]$InputText = '', [switch]$AllowFailure, [int]$TimeoutSeconds = 180) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $File
    $info.Arguments = (($Arguments | ForEach-Object { Quote-Argument $_ }) -join ' ')
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    $null = $process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if ($InputText) { $process.StandardInput.Write($InputText) }
    $process.StandardInput.Close()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { $process.Kill(); throw "$File timed out." }
    $text = $stdout.Result + $stderr.Result
    foreach ($secret in $script:redactions) { if ($secret) { $text = $text.Replace($secret, '[REDACTED]') } }
    $result = [PSCustomObject]@{ Code = $process.ExitCode; Text = $text.Trim() }
    $process.Dispose()
    if ($result.Code -ne 0 -and -not $AllowFailure) { throw "$File failed (exit $($result.Code)). $($result.Text)" }
    return $result
}

function Write-PrivateText([string]$Path, [string]$Text) { [IO.File]::WriteAllText($Path, $Text, $utf8) }
function Write-PrivateJson([string]$Path, $Value) { Write-PrivateText $Path ($Value | ConvertTo-Json -Depth 12) }
function New-Secret {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes); [Convert]::ToBase64String($bytes) } finally { $rng.Dispose() }
}

function Assert-NoReparseTree([string]$Path) {
    $pending = New-Object 'Collections.Generic.Queue[string]'
    $pending.Enqueue($Path)
    while ($pending.Count -gt 0) {
        $current = Get-Item -LiteralPath $pending.Dequeue() -Force
        if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse points are not accepted in local setup data: $($current.Name)" }
        if ($current.PSIsContainer) { foreach ($child in Get-ChildItem -LiteralPath $current.FullName -Force) { $pending.Enqueue($child.FullName) } }
    }
}

function Assert-OwnedContainer {
    $existing = Invoke-Tool docker @('container', 'inspect', $container) -AllowFailure
    if ($existing.Code -ne 0) { return $null }
    $details = @($existing.Text | ConvertFrom-Json)[0]
    if ($details.Config.Labels.'org.revora.workspace' -ne $workspaceId) { throw 'A container with this name belongs to another setup. It will not be changed.' }
    return $details
}

function Invoke-Sql([string]$Database, [string]$Sql) {
    Invoke-Tool docker @('exec', '-i', $container, 'psql', '-X', '-U', 'postgres', '-d', $Database, '-v', 'ON_ERROR_STOP=1', '-q', '-t', '-A') ($Sql + "`n")
}

function Stop-OwnedProcess($Entry) {
    if (-not $Entry) { return }
    $process = Get-Process -Id $Entry.Id -ErrorAction SilentlyContinue
    if ($process -and [string]$process.StartTime.ToUniversalTime().Ticks -eq [string]$Entry.StartTicks) {
        if ($process.Path -ne $Entry.Executable) { throw 'Process identity changed; it will not be stopped.' }
        Stop-Process -Id $process.Id
    }
}

function Start-LocalProcess([string]$Executable, [string]$Arguments, [string]$Directory, [string]$Name, $EnvironmentValues) {
    $saved = @{}
    try {
        foreach ($key in $EnvironmentValues.Keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key, 'Process'); [Environment]::SetEnvironmentVariable($key, $EnvironmentValues[$key], 'Process') }
        $parameters = @{ FilePath = $Executable; WorkingDirectory = $Directory; PassThru = $true; RedirectStandardOutput = (Join-Path $localRoot "logs/$Name.stdout.log"); RedirectStandardError = (Join-Path $localRoot "logs/$Name.stderr.log") }
        if ($Arguments) { $parameters.ArgumentList = $Arguments }
        if ($isWindowsHost) { $parameters.WindowStyle = 'Hidden' }
        $process = Start-Process @parameters
        Start-Sleep -Milliseconds 300
        if ($process.HasExited) { throw "$Name exited during startup. See the private .local/logs directory." }
        return [PSCustomObject]@{ Id = $process.Id; StartTicks = [string]$process.StartTime.ToUniversalTime().Ticks; Executable = $process.Path }
    } finally {
        foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key], 'Process') }
    }
}

function Test-Tcp([int]$Port) {
    $client = New-Object Net.Sockets.TcpClient
    try { $task = $client.ConnectAsync('127.0.0.1', $Port); return $task.Wait(500) -and $client.Connected } catch { return $false } finally { $client.Dispose() }
}

function Read-LocalState {
    if (Test-Path -LiteralPath $stateFile) {
        $savedState = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
        if ($savedState.WorkspaceId -ne $workspaceId -or $savedState.DatabasePort -ne $DatabasePort -or $savedState.WebPort -ne $WebPort) { throw 'Saved workspace/ports differ. Existing data will not be reconfigured.' }
        return $savedState
    }
    return [PSCustomObject]@{ WorkspaceId = $workspaceId; Container = $container; Volume = $volume; DatabasePort = $DatabasePort; WebPort = $WebPort; WebProcess = $null; GameProcess = $null }
}

function Prepare-StaticWebsite($State) {
    $null = Invoke-Tool node @('--check', (Join-Path $repoRoot 'website/site/app.js'))
    if ($Mode -ne 'Start') { return $false }
    $existingProcess = if ($State.WebProcess) { Get-Process -Id $State.WebProcess.Id -ErrorAction SilentlyContinue } else { $null }
    $running = $existingProcess -and [string]$existingProcess.StartTime.ToUniversalTime().Ticks -eq [string]$State.WebProcess.StartTicks
    if (-not $running) {
        if (Test-Tcp $WebPort) { throw 'Website port is occupied. No existing process will be stopped.' }
        $arguments = (Quote-Argument (Join-Path $PSScriptRoot 'serve-site.mjs')) + " --port $WebPort"
        $State.WebProcess = Start-LocalProcess (Get-Command node).Source $arguments $repoRoot 'website' @{}
        Write-PrivateJson $stateFile $State
    }
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { $health = Invoke-RestMethod -Uri "http://127.0.0.1:$WebPort/__health" -TimeoutSec 2; if ($health.state -eq 'ready' -and $health.component -eq 'Revora.Static') { return $true } } catch { }
        Start-Sleep -Seconds 1
    }
    throw 'The static website did not become ready. See .local/logs.'
}

try {
    if (Test-Path -LiteralPath $localRoot) { Assert-NoReparseTree $localRoot }
    $stateFile = Join-Path $localRoot 'state.json'
    if ($Mode -eq 'Stop') {
        if (-not (Test-Path -LiteralPath $stateFile)) { Write-Host 'No managed local services have been recorded.'; exit 0 }
        $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
        if ($state.WorkspaceId -ne $workspaceId) { throw 'Workspace identity mismatch.' }
        Stop-OwnedProcess $state.GameProcess
        Stop-OwnedProcess $state.WebProcess
        if (Get-Command docker -ErrorAction SilentlyContinue) {
            $details = Assert-OwnedContainer
            if ($details) { $null = Invoke-Tool docker @('stop', $container) }
        }
        $state.GameProcess = $null; $state.WebProcess = $null
        Write-PrivateJson $stateFile $state
        Write-Host 'Stopped this setup only. Database volume, configuration and keys are preserved.'
        exit 0
    }

    if (($GameRuntimeDirectory -or $GameSchemaSql) -and -not $WithDatabase) { throw 'Use -WithDatabase when preparing or starting the game runtime.' }
    $missing = New-Object 'Collections.Generic.List[string]'
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        if ($Mode -eq 'Start' -and -not $WithDatabase) {
            Write-Host 'Node is optional for the website. Opening the buildless HTML files directly.'
            if ($isWindowsHost -and -not $NoBrowser) { Start-Process (Join-Path $repoRoot 'website/site/index.html') }
            exit 0
        }
        $missing.Add('Node.js 22 or newer for the local HTTP preview: https://nodejs.org/')
    }
    else {
        $nodeVersion = Invoke-Tool node @('--version')
        if ([int]($nodeVersion.Text.TrimStart('v').Split('.')[0]) -lt 22) { $missing.Add('Node.js 22 or newer: https://nodejs.org/') }
    }
    if ($WithDatabase) {
        if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { $missing.Add('Docker Desktop with Linux containers: https://docs.docker.com/desktop/setup/install/windows-install/') }
        else {
            $docker = Invoke-Tool docker @('info', '--format', '{{.OSType}}') -AllowFailure
            if ($docker.Code -ne 0 -or $docker.Text -ne 'linux') { $missing.Add('Start Docker with its Linux-container engine.') }
        }
    }
    if ($Mode -eq 'Check' -or $missing.Count -gt 0) {
        [PSCustomObject]@{ Mode = 'Check'; WithDatabase = [bool]$WithDatabase; MissingPrerequisites = @($missing.ToArray()); GameRuntimeProvided = [bool]$GameRuntimeDirectory; GameRequires = 'Reviewed .NET Framework 4.8 build/dependencies, clean game schema/data, runtime tables and a matching 6711 client.' } | ConvertTo-Json -Depth 4 | Write-Host
        if ($missing.Count -gt 0) { exit 1 }; exit 0
    }

    if ($DatabasePort -eq $WebPort) { throw 'Database and website ports must differ.' }
    $null = New-Item -ItemType Directory -Path $localRoot -Force
    if ($isWindowsHost) {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
        $acl = Get-Acl -LiteralPath $localRoot
        $rules = @($acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]))
        $allowed = @($identity.Value, 'S-1-5-18')
        $private = $acl.AreAccessRulesProtected -and $rules.Count -eq 2
        foreach ($rule in $rules) { if ($rule.IdentityReference.Value -notin $allowed -or $rule.AccessControlType -ne 'Allow' -or $rule.FileSystemRights -ne 'FullControl') { $private = $false } }
        if (-not $private) {
            $acl.SetAccessRuleProtection($true, $false)
            foreach ($rule in @($acl.GetAccessRules($true, $false, [Security.Principal.SecurityIdentifier]))) { $acl.RemoveAccessRuleSpecific($rule) }
            $inherit = [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
            $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', $inherit, 'None', 'Allow')))
            $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier('S-1-5-18')), 'FullControl', $inherit, 'None', 'Allow')))
            Set-Acl -LiteralPath $localRoot -AclObject $acl
        }
    } else { $null = Invoke-Tool chmod @('700', $localRoot) }
    foreach ($relative in @('logs', 'secrets', 'game/runtime')) { $null = New-Item -ItemType Directory -Path (Join-Path $localRoot $relative) -Force }
    $state = Read-LocalState
    if (-not $WithDatabase) {
        $webReady = Prepare-StaticWebsite $state
        Write-PrivateJson $stateFile $state
        $report = [ordered]@{ Mode = $Mode; WebsiteKind = 'HTML/CSS/JavaScript'; WebsiteReady = $webReady; WebsiteUrl = "http://127.0.0.1:$WebPort"; DatabasePrepared = $false; RegistrationEnabled = $false; Game = 'not started; use -WithDatabase and the documented runtime prerequisites' }
        Write-PrivateJson (Join-Path $localRoot 'setup-report.json') $report
        $report | ConvertTo-Json | Write-Host
        if ($Mode -eq 'Start' -and -not $NoBrowser -and $isWindowsHost) { Start-Process "http://127.0.0.1:$WebPort" }
        exit 0
    }
    $credentialFile = Join-Path $localRoot 'secrets/credentials.secrets.json'
    $existingVolume = Invoke-Tool docker @('volume', 'inspect', $volume) -AllowFailure
    if ($existingVolume.Code -eq 0) {
        $volumeDetails = @($existingVolume.Text | ConvertFrom-Json)[0]
        if ($volumeDetails.Labels.'org.revora.workspace' -ne $workspaceId -or -not (Test-Path -LiteralPath $credentialFile)) { throw 'Existing database volume has no matching owned credentials. It will not be reset.' }
    }
    if (-not (Test-Path -LiteralPath $credentialFile)) { Write-PrivateJson $credentialFile ([ordered]@{ Postgres = (New-Secret); Web = (New-Secret); Game = (New-Secret) }) }
    $credentials = Get-Content -LiteralPath $credentialFile -Raw | ConvertFrom-Json
    $script:redactions = @($credentials.Postgres, $credentials.Web, $credentials.Game)
    foreach ($secret in $script:redactions) { if ($secret -notmatch '^[A-Za-z0-9+/]{43}=$') { throw 'Local credentials are malformed. They will not be replaced automatically.' } }
    $passwordFile = Join-Path $localRoot 'secrets/postgres-password.txt'
    Write-PrivateText $passwordFile $credentials.Postgres
    if (-not $isWindowsHost) { $null = Invoke-Tool chmod @('-R', 'go-rwx', $localRoot) }

    $details = Assert-OwnedContainer
    if (-not $details) {
        if (Test-Tcp $DatabasePort) { throw 'Database port is occupied. No existing service will be stopped.' }
        if ($existingVolume.Code -ne 0) { $null = Invoke-Tool docker @('volume', 'create', '--label', "org.revora.workspace=$workspaceId", $volume) }
        Write-Host 'Preparing the official PostgreSQL 17 image and an isolated local database...'
        $null = Invoke-Tool docker @('run', '-d', '--name', $container, '--label', "org.revora.workspace=$workspaceId", '--publish', "127.0.0.1:${DatabasePort}:5432", '--volume', "${volume}:/var/lib/postgresql/data", '--mount', "type=bind,source=$passwordFile,target=/run/secrets/postgres-password,readonly", '--env', 'POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password', 'postgres:17') -TimeoutSeconds 600
    } elseif (-not $details.State.Running) { $null = Invoke-Tool docker @('start', $container) }
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        $probe = Invoke-Tool docker @('exec', $container, 'pg_isready', '-U', 'postgres') -AllowFailure
        if ($probe.Code -eq 0 -and (Test-Tcp $DatabasePort)) { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'The managed PostgreSQL instance did not become ready.' }
    Write-PrivateJson $stateFile $state
    $roles = [IO.File]::ReadAllText((Join-Path $repoRoot 'website/db/roles.sql'))
    $null = Invoke-Sql 'postgres' $roles
    $roleSql = @'
DO $setup$ BEGIN
IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='revora_web_local') THEN CREATE ROLE revora_web_local LOGIN; END IF;
IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='revora_game_local') THEN CREATE ROLE revora_game_local LOGIN; END IF;
END $setup$;
'@
    $roleSql += "`nALTER ROLE revora_web_local PASSWORD '$($credentials.Web)';`nALTER ROLE revora_game_local PASSWORD '$($credentials.Game)';`nGRANT revora_web TO revora_web_local;"
    $null = Invoke-Sql 'postgres' $roleSql
    foreach ($database in @(@('revora_web_local', 'revora_migrator', 'revora_web_local'), @('revora6711_3d', 'revora_game_local', 'revora_game_local'))) {
        $exists = Invoke-Sql 'postgres' ("SELECT 1 FROM pg_database WHERE datname='$($database[0])';")
        if ($exists.Text -ne '1') { $null = Invoke-Sql 'postgres' ("CREATE DATABASE $($database[0]) OWNER $($database[1]);") }
        $null = Invoke-Sql 'postgres' ("REVOKE ALL ON DATABASE $($database[0]) FROM PUBLIC; GRANT CONNECT ON DATABASE $($database[0]) TO $($database[2]);")
    }
    $ledgerExists = (Invoke-Sql 'revora_web_local' "SELECT to_regclass('operations.schema_migrations') IS NOT NULL;").Text -eq 't'
    $applied = 0
    foreach ($migration in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'website/db/migrations') -Filter 'V*.sql' | Sort-Object Name) {
        if ($migration.Name -notmatch '^V(\d+)__[a-z0-9_]+\.sql$') { throw 'Unexpected migration filename.' }
        $version = [int]$Matches[1]
        $sql = [IO.File]::ReadAllText($migration.FullName).Replace("`r`n", "`n")
        $checksum = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($utf8.GetBytes($sql)))).Replace('-', '').ToLowerInvariant()
        if ($ledgerExists) {
            $existing = (Invoke-Sql 'revora_web_local' "SELECT checksum_sha256 FROM operations.schema_migrations WHERE version=$version;").Text
            if ($existing) { if ($existing -ne $checksum) { throw "Applied migration changed: $($migration.Name)." }; continue }
        }
        if ([regex]::Matches($sql, '(?im)^COMMIT;\s*$').Count -ne 1) { throw 'Migration has an unsupported transaction boundary.' }
        $record = "INSERT INTO operations.schema_migrations(version,name,checksum_sha256) VALUES($version,'$($migration.Name)','$checksum');`nCOMMIT;"
        $sql = [regex]::Replace($sql, '(?im)^COMMIT;\s*$', $record)
        try { $null = Invoke-Sql 'revora_web_local' $sql } catch { throw "Website migration failed: $($migration.Name). $($_.Exception.Message)" }
        $ledgerExists = $true; $applied++
    }
    Write-Host "Database ready; $applied website migrations applied. Existing credentials and data were preserved."

    if ($GameSchemaSql) {
        $schema = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $GameSchemaSql).Path)
        if ($schema -match '(?im)^\s*\\(?!restrict\b|unrestrict\b)' -or $schema -match '(?i)\b(?:CREATE|DROP)\s+DATABASE\b|\bALTER\s+ROLE\b|\bCOPY\b|\bINSERT\s+INTO\s+(?:public\.)?(?:accounts|entities)\b') { throw 'Supply a reviewed schema-only game export, without accounts, player rows or psql commands.' }
        $tableCount = (Invoke-Sql 'revora6711_3d' "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';").Text
        if ($tableCount -ne '0') { throw 'The game database is not empty; an existing schema will not be overwritten.' }
        $null = Invoke-Sql 'revora6711_3d' ("BEGIN; SET LOCAL ROLE revora_game_local;`n" + $schema + "`nCOMMIT;")
    }

    $permissions = (Invoke-Sql 'revora_web_local' "SET ROLE revora_web_local; SELECT current_user='revora_web_local' AND has_table_privilege(current_user,'identity.game_accounts','SELECT,INSERT,UPDATE') AND NOT has_table_privilege(current_user,'identity.game_accounts','DELETE');").Text
    if ($permissions -ne 't') { throw 'Restricted website database permissions were not established.' }
    Write-Host 'Verified restricted database role permissions. The static website contains no database credentials.'
    $webReady = Prepare-StaticWebsite $state

    $gameStatus = 'blocked: provide missing build dependencies, a reviewed runtime/hash and clean game schema/data; see docs/QUICKSTART.md'
    if ($GameRuntimeDirectory) {
        if (-not $isWindowsHost) { throw 'The .NET Framework 4.8 game runtime requires Windows.' }
        $sourceRuntime = (Resolve-Path -LiteralPath $GameRuntimeDirectory).Path
        Assert-NoReparseTree $sourceRuntime
        $sourceExe = Join-Path $sourceRuntime 'Revora.exe'
        if (-not $GameExecutableSha256 -or (Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash -ne $GameExecutableSha256) { throw 'An independently reviewed, matching -GameExecutableSha256 is required.' }
        $runtime = Join-Path $localRoot 'game/runtime'
        if ($sourceRuntime.StartsWith($localRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a separate operator runtime as the staging source.' }
        if (-not (Test-Path -LiteralPath (Join-Path $runtime 'Revora.exe'))) {
            foreach ($entry in Get-ChildItem -LiteralPath $sourceRuntime -Force) { if ($entry.Name -eq '.git') { throw 'Supply a runtime directory, not a Git checkout.' }; Copy-Item -LiteralPath $entry.FullName -Destination $runtime -Recurse }
        }
        if ((Get-FileHash -LiteralPath (Join-Path $runtime 'Revora.exe') -Algorithm SHA256).Hash -ne $GameExecutableSha256) { throw 'The staged runtime differs; it will not be overwritten or executed.' }
        if (-not (Test-Path -LiteralPath (Join-Path $runtime 'database') -PathType Container)) { throw 'The game runtime needs its lawfully supplied database tables.' }
        $schemaReady = (Invoke-Sql 'revora6711_3d' "SELECT to_regclass('public.accounts') IS NOT NULL AND to_regclass('public.entities') IS NOT NULL AND to_regclass('public.configuration') IS NOT NULL;").Text -eq 't'
        if (-not $schemaReady) { throw 'The 6711 game schema is missing. Website migrations do not create the 6711 schema.' }
        $ini = "[configuracion]`nIP=127.0.0.1`nGamePort=9959`nAuthPort=9958`nProtectionPort=6319`nServidor=Revora`n`n[MySql]`nHost=127.0.0.1`nPort=$DatabasePort`nUsername=revora_game_local`nPassword=`nDatabase=revora6711_3d`n"
        Write-PrivateText (Join-Path $runtime 'Configuracion.ini') $ini
        $gameStatus = 'prepared; runtime hash verified, schema presence checked; gameplay not tested'
        if ($Mode -eq 'Start') {
            if ($state.GameProcess) { throw 'A game process is already recorded. Stop this setup before starting another game process.' }
            foreach ($port in @(9958, 9959, 6319)) { if (Test-Tcp $port) { throw "Game port $port is occupied; no existing service will be stopped." } }
            $state.GameProcess = Start-LocalProcess (Join-Path $runtime 'Revora.exe') '' $runtime 'game' @{ REVORA_DATABASE_PASSWORD = $credentials.Game }
            Write-PrivateJson $stateFile $state
            $listeners = $false
            for ($attempt = 0; $attempt -lt 60; $attempt++) { if ((Test-Tcp 9958) -and (Test-Tcp 9959)) { $listeners = $true; break }; Start-Sleep -Seconds 2 }
            if (-not $listeners) { Stop-OwnedProcess $state.GameProcess; $state.GameProcess = $null; Write-PrivateJson $stateFile $state; throw 'Game listeners did not become ready; the failed managed process was stopped.' }
            $gameStatus = 'listeners-ready on loopback; real 6711 login and gameplay still require physical validation'
        }
    }
    $report = [ordered]@{ Mode = $Mode; WebsiteKind = 'HTML/CSS/JavaScript'; WebsitePrepared = $true; WebsiteReady = $webReady; WebsiteUrl = "http://127.0.0.1:$WebPort"; DatabasePrepared = $true; DatabaseContainer = $container; DatabasePort = $DatabasePort; RegistrationEnabled = $false; Game = $gameStatus }
    Write-PrivateJson (Join-Path $localRoot 'setup-report.json') $report
    $report | ConvertTo-Json | Write-Host
    Write-Host 'Next steps: docs/QUICKSTART.md, docs/CONNECT_COMPONENTS.md and docs/AI_AGENT_SETUP.md.'
    if ($Mode -eq 'Start' -and -not $NoBrowser -and $isWindowsHost) { Start-Process "http://127.0.0.1:$WebPort" }
    exit 0
} catch {
    $message = $_.Exception.Message
    foreach ($secret in $script:redactions) { if ($secret) { $message = $message.Replace($secret, '[REDACTED]') } }
    Write-Error $message -ErrorAction Continue
    exit 1
}
