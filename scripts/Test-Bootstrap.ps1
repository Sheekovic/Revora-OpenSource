$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$engine = (Get-Process -Id $PID).Path
$entry = Join-Path $PSScriptRoot 'Start-Revora.ps1'
function Run-Setup([string[]]$Options) {
    & $engine -NoProfile -ExecutionPolicy RemoteSigned -File $entry @Options
    if ($LASTEXITCODE -ne 0) { throw 'Bootstrap command failed.' }
}
try {
    Run-Setup @('-Mode','Check','-WithDatabase','-NoBrowser')
    Run-Setup @('-WithDatabase','-NoBrowser')
    $privateRoot = Join-Path $repoRoot '.local'
    $credentialsPath = Join-Path $privateRoot 'secrets/credentials.secrets.json'
    $firstHash = (Get-FileHash -LiteralPath $credentialsPath -Algorithm SHA256).Hash
    $first = Get-Content -LiteralPath (Join-Path $privateRoot 'setup-report.json') -Raw | ConvertFrom-Json
    if (-not $first.WebsiteReady -or -not $first.DatabasePrepared -or $first.RegistrationEnabled -or $first.Game -notlike 'blocked:*') { throw 'Bootstrap readiness did not match the partial-runtime contract.' }
    $page = Invoke-WebRequest -Uri $first.WebsiteUrl -UseBasicParsing
    if ($page.StatusCode -ne 200 -or $page.Content -notmatch 'Legends aren') { throw 'Static website did not load.' }
    $credentials = Get-Content -LiteralPath $credentialsPath -Raw | ConvertFrom-Json
    $containerEnvironment = & docker inspect --format '{{json .Config.Env}}' $first.DatabaseContainer
    foreach ($secret in @($credentials.Postgres,$credentials.Web,$credentials.Game)) { if ($containerEnvironment.Contains($secret) -or $page.Content.Contains($secret)) { throw 'A private credential was exposed.' } }
    $bindings = (& docker inspect --format '{{json .HostConfig.PortBindings}}' $first.DatabaseContainer | ConvertFrom-Json).'5432/tcp'
    if (@($bindings).Count -ne 1 -or $bindings[0].HostIp -ne '127.0.0.1') { throw 'Database was not restricted to loopback.' }
    Run-Setup @('-WithDatabase','-NoBrowser')
    if ((Get-FileHash -LiteralPath $credentialsPath -Algorithm SHA256).Hash -ne $firstHash) { throw 'Re-running setup changed credentials.' }
    $migrationCount = & docker exec $first.DatabaseContainer psql -X -U postgres -d revora_web_local -t -A -c 'SELECT count(*) FROM operations.schema_migrations;'
    if ($LASTEXITCODE -ne 0 -or $migrationCount.Trim() -ne '47') { throw 'The complete website migration ledger was not preserved.' }
    Write-Host 'PASS real PostgreSQL provisioning, 47 migrations, restricted loopback/roles, static website, secret isolation and idempotent rerun.'
} finally { Run-Setup @('-Mode','Stop','-NoBrowser') }
