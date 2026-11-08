param([switch]$RestoreCatalogs, [switch]$VerifyCatalogs, [switch]$ResumeEmptyRestore, [switch]$ProvisionRuntime, [switch]$VerifyRuntime, [switch]$ProvisionAccount, [switch]$VerifyAccount, [switch]$CheckGameStartup, [switch]$CheckLmsRecovery, [switch]$CheckQuarantineRecovery, [switch]$CheckArenaStartup, [switch]$CheckRelicRecovery, [switch]$CheckWaveRecovery, [switch]$CheckRoadmapStartup, [switch]$CheckLocalTeamPkRecovery, [switch]$CheckLocalTeamPkReturn, [switch]$CheckSuperGuildRecovery, [switch]$CheckClanPurseRecovery, [switch]$CheckEliteGuildRecovery, [switch]$PrepareQuestCatalog, [switch]$CheckWireAuth, [switch]$CheckCharacterEntry, [switch]$CheckCourierCrash, [switch]$CheckSoulTrialAccess, [ValidateRange(0,3)][int]$SoulCombat = 0, [ValidateRange(1,50)][int]$SoulBatch = 1, [switch]$CheckPathHunt, [ValidateRange(0,4)][int]$PathCombat = 0, [switch]$CheckPromotion, [switch]$CheckMoonAccess, [switch]$CheckMoonCombat, [switch]$CheckMoonFinish, [switch]$CheckFirstRebirth, [switch]$CheckSecondLife, [switch]$CheckSecondPromotion, [switch]$CheckSecondRebirth, [switch]$CheckHomecomingPresence, [switch]$CheckHomecomingReturn, [switch]$CheckHomecomingExpired, [switch]$CheckTournamentAccess, [switch]$CheckSkillTeamPk)
$ErrorActionPreference = 'Stop'
if ($CheckSkillTeamPk -and !$CheckTournamentAccess) { throw 'Skill PK mode requires tournament access checks.' }
if ($CheckTournamentAccess -and !$CheckSecondRebirth) { throw 'Tournament access uses the completed ordinary career fixture.' }
if ($CheckHomecomingExpired -and !$CheckHomecomingReturn) { throw 'Expiry check requires Homecoming return mode.' }
if ($CheckHomecomingReturn -and (!$CheckSecondRebirth -or $CheckHomecomingPresence -or $CheckCourierCrash)) { throw 'Qualifying return requires the completed ordinary career without presence/crash mode.' }
if ($CheckHomecomingPresence -and (!$CheckCharacterEntry -or ($CheckCourierCrash -and !$CheckSecondRebirth))) { throw 'Homecoming presence requires ordinary entry; crash mode requires the completed career.' }
if ($CheckSecondRebirth -and !$CheckSecondPromotion) { throw 'Second rebirth requires completed second-life promotions.' }
if ($CheckSecondPromotion -and !$CheckSecondLife) { throw 'Second-life promotions require the earned second-life level.' }
if ($CheckSecondLife -and !$CheckFirstRebirth) { throw 'Second-life earning requires the first-rebirth career checks.' }
if ($CheckFirstRebirth -and !$CheckMoonFinish) { throw 'First rebirth requires the completed first-life checks.' }
if ($CheckMoonFinish -and !$CheckMoonCombat) { throw 'Moon completion requires the earned token checks.' }
if ($CheckMoonCombat -and !$CheckMoonAccess) { throw 'Moon combat requires Moon access checks.' }
if ($CheckMoonAccess -and !$CheckPromotion) { throw 'Moon Box access requires the earned promotion career checks.' }
if ($CheckPromotion -and !$CheckPathHunt) { throw 'Promotion requires the existing career checks.' }
if ($CheckPathHunt -and (!$CheckSoulTrialAccess -or !$CheckCharacterEntry)) { throw 'Path Hunt requires the existing character and Soul Trial checks.' }
if ($PathCombat -and !$CheckPathHunt) { throw 'Path combat requires the Path Hunt check.' }
if ($SoulCombat -and !$CheckSoulTrialAccess) { throw 'Soul combat requires the Soul Trial check.' }
if ($ResumeEmptyRestore -and !$RestoreCatalogs) { throw 'Empty-restore recovery requires -RestoreCatalogs.' }
$candidateRoot = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$data = Join-Path $candidateRoot 'data'
$toolRoot = 'C:\Revora\DisposablePostgres\tools-17.11-4\pgsql\bin'
$hashes = @{
    'postgres.exe' = 'F1B4E561FF3C6BE6FC7DB26B916BA756508AA0E02C4B4047E5B2DA9CA8C77396'
    'pg_ctl.exe' = 'EED85E36AABC7CDF99A63BDA0A2E33FA3BCE8D3C59D6BC12E9E19469706CC012'
    'psql.exe' = 'E2B971FFE755010DA9F9A3699F62A5F3A5D6000B054B5D80D0E973245F44DEDE'
    'pg_restore.exe' = '95D4165D034CDE580C4514036A2305267043F34B801F0B4C9C2656CA242371EB'
}
if ($RestoreCatalogs) {
    $archives = @{
        'game-schema.dump' = '2C8C7BD52A173FB52B9150D77CC7272CEA012E847283183C5B4B828A879F5967'
        'game-catalogs.dump' = '4C58A4D1F7D24F275E220D93062F6ECDC670CFADAABDAF24FC670FB6D0900BEE'
    }
    foreach ($name in $archives.Keys) {
        if ((Get-FileHash -LiteralPath (Join-Path $candidateRoot $name)).Hash -ne $archives[$name]) {
            throw ('Candidate archive does not match the reviewed snapshot: ' + $name)
        }
    }
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
function Invoke-CandidateTool([string]$Tool, [string]$Arguments) {
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $toolRoot $Tool
    $start.Arguments = $Arguments
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.EnvironmentVariables['PGHOST'] = '127.0.0.1'
    $start.EnvironmentVariables['PGPORT'] = '25439'
    $start.EnvironmentVariables['PGUSER'] = 'revora_candidate_admin'
    $start.EnvironmentVariables['PGCONNECT_TIMEOUT'] = '5'
    $start.EnvironmentVariables['PGPASSWORD'] = $credential.GetNetworkCredential().Password
    $start.EnvironmentVariables['PGOPTIONS'] = '-c statement_timeout=30000 -c lock_timeout=5000'
    $child = New-Object System.Diagnostics.Process
    $child.StartInfo = $start
    try {
        if (!$child.Start()) { throw 'Candidate database tool did not start.' }
        $start.EnvironmentVariables.Remove('PGPASSWORD')
        $stdout = $child.StandardOutput.ReadToEndAsync()
        $stderr = $child.StandardError.ReadToEndAsync()
        if (!$child.WaitForExit(60000)) { $child.Kill(); throw 'Candidate database tool timed out.' }
        $result = $stdout.GetAwaiter().GetResult().Trim()
        $stderr.GetAwaiter().GetResult() | Out-Null
        if ($child.ExitCode -ne 0) { throw ('Candidate database tool failed: ' + $Tool + '; inspect candidate before retrying.') }
        return $result
    } finally {
        $start.EnvironmentVariables.Remove('PGPASSWORD')
        $child.Dispose()
    }
}
function Restore-CandidateCatalogs {
    $absent = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d postgres -c "SELECT NOT EXISTS(SELECT 1 FROM pg_database WHERE datname=''revora_candidate_game'') AND NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=''revora_candidate_game'');"'
    if ($absent -eq 't') {
        Invoke-CandidateTool 'psql.exe' '-X -w -v ON_ERROR_STOP=1 -d postgres -c "CREATE ROLE revora_candidate_game NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;"' | Out-Null
        Invoke-CandidateTool 'psql.exe' '-X -w -v ON_ERROR_STOP=1 -d postgres -c "CREATE DATABASE revora_candidate_game OWNER revora_candidate_game;"' | Out-Null
    } elseif (!$ResumeEmptyRestore) { throw 'Candidate game database or role already exists; refusing to overwrite it.' }
    # Recovery is limited to our own empty database. No CASCADE or populated-table deletion.
    $empty = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT EXISTS(SELECT 1 FROM pg_database d JOIN pg_roles r ON r.oid=d.datdba WHERE d.datname=current_database() AND r.rolname=''revora_candidate_game'' AND NOT r.rolsuper AND NOT r.rolcanlogin) AND NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=''public'') AND NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname NOT LIKE ''pg_%'' AND nspname NOT IN (''information_schema'',''public''));"'
    if ($empty -ne 't') { throw 'Candidate database is not owned and empty; refusing restore.' }
    Invoke-CandidateTool 'psql.exe' '-X -w -v ON_ERROR_STOP=1 -d revora_candidate_game -c "DROP SCHEMA public;"' | Out-Null
    foreach ($archive in @('game-schema.dump', 'game-catalogs.dump')) {
        Invoke-CandidateTool 'pg_restore.exe' ('--no-password --exit-on-error --single-transaction --no-owner --no-privileges --role=revora_candidate_game --dbname=revora_candidate_game "' + (Join-Path $candidateRoot $archive) + '"') | Out-Null
    }
}
function Test-CandidateCatalogs {
    $valid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT current_database()=''revora_candidate_game'' AND (SELECT count(*) FROM pg_tables WHERE schemaname=''public'' AND tablename<>''revora_account_srp'')=100 AND NOT EXISTS(SELECT 1 FROM pg_tables WHERE schemaname=''public'' AND tableowner<>''revora_candidate_game'' AND NOT (tablename=''revora_account_srp'' AND tableowner=''revora_candidate_runtime'')) AND NOT EXISTS(SELECT 1 FROM entities) AND NOT EXISTS(SELECT 1 FROM items) AND NOT EXISTS(SELECT 1 FROM skills) AND EXISTS(SELECT 1 FROM maps WHERE id=1002) AND EXISTS(SELECT 1 FROM monsterinfos) AND EXISTS(SELECT 1 FROM npcs) AND EXISTS(SELECT 1 FROM configuration) AND EXISTS(SELECT 1 FROM pg_roles WHERE rolname=''revora_candidate_game'' AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolcanlogin);"'
    if ($valid -ne 't') { throw 'Candidate catalog, empty-player-table or ownership validation failed.' }
    if ($ProvisionAccount -or $VerifyAccount) {
        $accountState = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT (SELECT count(*) FROM accounts)=1 AND EXISTS(SELECT 1 FROM accounts WHERE username=''MoonBoxTester'' AND state=2 AND entityid=0);"'
    } else {
        $accountState = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT NOT EXISTS(SELECT 1 FROM accounts);"'
    }
    if ($accountState -ne 't') { throw 'Unexpected candidate account state; use the explicit account verification mode for the prepared account.' }
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
function Read-CandidatePresence([bool]$AllowOpen = $false) {
    $json = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT json_build_object(''auth'',h.auth_count,''recent'',h.last_auth>now()-interval ''1 day'',''sessions'',(SELECT count(*) FROM revora_account_sessions s WHERE s.account_name=h.account_name),''valid'',NOT EXISTS(SELECT 1 FROM revora_account_sessions s WHERE s.account_name=h.account_name AND (s.character_uid<>a.entityid OR s.owner IS NULL OR s.closed_at<s.opened_at)),''open'',(SELECT count(*) FROM revora_account_sessions s WHERE s.account_name=h.account_name AND s.closed_at IS NULL),''returns'',(SELECT count(*) FROM revora_account_returns r WHERE r.account_name=h.account_name),''history'',(SELECT md5(string_agg(row_to_json(s)::text,'','' ORDER BY s.session_id)) FROM revora_account_sessions s WHERE s.account_name=h.account_name)) FROM revora_account_activity h JOIN accounts a ON lower(a.username)=h.account_name WHERE a.username=''MoonBoxTester'' AND a.state=2;"'
    $result = $json | ConvertFrom-Json
    if (!$result -or ($result.open -ne $(if ($AllowOpen) { 1 } else { 0 })) -or !$result.recent -or !$result.valid -or $result.returns -ne 0 -or $result.sessions -lt 1) { throw 'Homecoming active-player presence prerequisites or saved state failed.' }
    return $result
}
try {
    if ($CheckLocalTeamPkReturn -and (!$CheckTournamentAccess -or !$CheckCharacterEntry -or $CheckCourierCrash)) { throw 'Local Team PK return requires ordinary tournament access without a new crash.' }
    if ($CheckLocalTeamPkRecovery -and !$CheckRoadmapStartup) { throw 'Local Team PK recovery requires roadmap startup.' }
    if ($CheckQuarantineRecovery -and !$CheckRoadmapStartup) { throw 'Team Tournament recovery requires the roadmap startup check.' }
    if ($CheckSuperGuildRecovery -and !$CheckRoadmapStartup) { throw 'Super Guild recovery requires the roadmap startup check.' }
    if ($CheckCharacterEntry -and (!$CheckWireAuth -or $PrepareQuestCatalog -or $RestoreCatalogs -or $VerifyCatalogs -or $ProvisionAccount -or $VerifyAccount)) { throw 'Character entry requires -CheckWireAuth and an already prepared catalog; empty-player/account checks cannot be combined.' }
    if ($CheckCourierCrash -and !$CheckCharacterEntry) { throw 'Courier interruption requires character entry.' }
    if ($CheckSoulTrialAccess -and (!$CheckCharacterEntry -or ($CheckCourierCrash -and !$CheckSecondRebirth))) { throw 'Soul Trial access requires normal character entry.' }
    $characterUid = 0
    if ($CheckWireAuth -and !$CheckRoadmapStartup) { throw 'Wire authentication requires the roadmap startup check.' }
    if ($CheckEliteGuildRecovery -and !$CheckRoadmapStartup) { throw 'Elite Guild recovery requires the roadmap startup check.' }
    if ($CheckClanPurseRecovery -and !$CheckRoadmapStartup) { throw 'Clan purse recovery requires the roadmap startup check.' }
    $cycles = if ($CheckGameStartup -and !$CheckLmsRecovery -and !$CheckArenaStartup -and !$CheckRelicRecovery -and !$CheckWaveRecovery -and !$CheckRoadmapStartup) { 1 } else { 2 }
    for ($cycle = 1; $cycle -le $cycles; $cycle++) {
        try {
            Invoke-CandidateControl 'start'
            $listeners = @(Get-NetTCPConnection -State Listen -LocalPort 25439)
            if ($listeners.Count -ne 1 -or $listeners[0].LocalAddress -ne '127.0.0.1') {
                throw 'Candidate listener is not exclusively IPv4 loopback.'
            }
            Invoke-CandidateProbe $true
            Invoke-CandidateProbe $false
            if ($PrepareQuestCatalog) { & (Join-Path $PSScriptRoot 'Prepare-CandidateQuestCatalog.ps1') -Credential $credential }
            if ($RestoreCatalogs -and $cycle -eq 1) { Restore-CandidateCatalogs }
            if ($ProvisionRuntime -and $cycle -eq 1) { & (Join-Path $PSScriptRoot 'Invoke-CandidateRuntimeCheck.ps1') -Create }
            elseif ($ProvisionRuntime -or $VerifyRuntime) { & (Join-Path $PSScriptRoot 'Invoke-CandidateRuntimeCheck.ps1') }
            if ($ProvisionAccount -and $cycle -eq 1) { & (Join-Path $PSScriptRoot 'Invoke-CandidateAccountCheck.ps1') -Create }
            elseif ($ProvisionAccount -or $VerifyAccount) { & (Join-Path $PSScriptRoot 'Invoke-CandidateAccountCheck.ps1') }
            if ($RestoreCatalogs -or $VerifyCatalogs) { Test-CandidateCatalogs }
            if ($CheckArenaStartup) { & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaStartup.ps1') }
            if ($CheckRoadmapStartup) {
                $arenaBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)::text||'':''||COALESCE(md5(string_agg(c.oid::text,'','' ORDER BY c.relname)),'''') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=''public'' AND c.relkind=''r'' AND c.relname IN(''revora_arena_daily_rewards'',''revora_team_arena_daily_rewards'');"'
                $arenaColumnsBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM information_schema.columns WHERE table_schema=''public'' AND table_name IN(''revora_arena_daily_rewards'',''revora_team_arena_daily_rewards'') AND column_name IN(''rune_type'',''rune_uid'');"'
                Write-Output ('Arena rune columns before game cycle ' + $cycle + ': ' + $arenaColumnsBefore)
            }
            if ($CheckClanPurseRecovery -and $cycle -eq 1) {
                # Storage-only legacy receipts, staged before the actual game performs its migration.
                $clanFixture = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_clan_wars WHERE run=''a6a56380-219f-4678-99c9-000000000004'';"'
                if ($clanFixture -ne '0') { throw 'Clan purse fixture already exists; inspect before retrying.' }
                $clanColumnsBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM information_schema.columns WHERE table_schema=''public'' AND table_name=''revora_clan_war_awards'' AND column_name IN(''soul_type'',''soul_uid'');"'
                Write-Output ('Clan soul columns before first Host: ' + $clanColumnsBefore)
                Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; INSERT INTO revora_clan_wars(run,arena,day,starts_at,state,winner,cp,rvp,ended_at) VALUES(''a6a56380-219f-4678-99c9-000000000004'',0,20990101,now()-interval ''1 hour'',''complete'',4294900070,100,20,now()); INSERT INTO revora_clan_war_awards(run,uid,cp,rvp,paid_at) VALUES(''a6a56380-219f-4678-99c9-000000000004'',4294900071,100,20,NULL),(''a6a56380-219f-4678-99c9-000000000004'',4294900072,100,20,now()); COMMIT;"' | Out-Null
                $clanLegacyBaseline = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(p)::text,'','' ORDER BY uid)) FROM (SELECT run,uid,cp,rvp,paid_at FROM revora_clan_war_awards WHERE run=''a6a56380-219f-4678-99c9-000000000004'' AND uid IN(4294900071,4294900072)) p;"'
                if ($clanLegacyBaseline -notmatch '^[0-9a-f]{32}$') { throw 'Clan legacy fingerprint is invalid.' }
            }
            if ($CheckHomecomingReturn -and $cycle -eq 1) {
                $returnCount = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_account_returns WHERE account_name=''moonboxtester'';"'
                if ($returnCount -eq '0') {
                    $eligible = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM accounts a JOIN entities e ON e.uid=a.entityid JOIN revora_account_activity h ON h.account_name=lower(a.username) WHERE a.username=''MoonBoxTester'' AND a.state=2 AND e.reborn=2 AND NOT EXISTS(SELECT 1 FROM revora_account_sessions s WHERE s.account_name=h.account_name AND s.closed_at IS NULL);"'
                    if ($eligible -ne 't') { throw 'Private inactive-return fixture requires the ordinary reborn character with no open session.' }
                    $activityPreimage = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT json_build_object(''activity'',row_to_json(h),''sessions'',(SELECT json_agg(s) FROM revora_account_sessions s WHERE s.account_name=h.account_name)) FROM revora_account_activity h WHERE h.account_name=''moonboxtester'';"'
                    $preimagePath = Join-Path $candidateRoot 'homecoming-activity-preimage.json'
                    if (Test-Path -LiteralPath $preimagePath) { throw 'Existing activity preimage requires inspection before another simulated absence.' }
                    [IO.File]::WriteAllText($preimagePath,$activityPreimage)
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; UPDATE revora_account_activity SET first_auth=first_auth-interval ''31 days'',last_auth=last_auth-interval ''31 days'' WHERE account_name=''moonboxtester''; UPDATE revora_account_sessions SET opened_at=opened_at-interval ''31 days'',closed_at=closed_at-interval ''31 days'' WHERE account_name=''moonboxtester''; COMMIT;"' | Out-Null
                    Write-Output 'Private Homecoming fixture: simulated 31-day absence by aging actual activity; preimage retained outside Git.'
                } elseif ($returnCount -ne '1') { throw 'Unexpected private Homecoming return history.' }
            }
            if ($CheckHomecomingExpired) {
                if ($cycle -eq 1) {
                    $alreadyExpired = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 AND bool_and(expires_at<now()) FROM revora_account_returns WHERE account_name=''moonboxtester'';"'
                    if ($alreadyExpired -ne 't') {
                        $expiryPreimage = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT json_build_object(''receipt'',row_to_json(r),''award'',row_to_json(w)) FROM revora_account_returns r JOIN revora_event_awards w ON w.uid=r.character_uid AND w.source=''homecoming-revora-v1'' AND w.title_key=r.title_key WHERE r.account_name=''moonboxtester'' AND r.award_recorded AND w.expires_at=r.expires_at;"'
                        $expiryPath = Join-Path $candidateRoot 'homecoming-expiry-preimage.json'
                        if (Test-Path -LiteralPath $expiryPath) { throw 'Existing expiry preimage requires inspection before another fixture shift.' }
                        if (!$expiryPreimage -or @($expiryPreimage | ConvertFrom-Json).Count -ne 1) { throw 'Expiry fixture requires one matched return/award.' }
                        [IO.File]::WriteAllText($expiryPath,$expiryPreimage)
                        Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; UPDATE revora_account_returns SET inactive_since=inactive_since-interval ''31 days'',returned_at=returned_at-interval ''31 days'',expires_at=expires_at-interval ''31 days'' WHERE account_name=''moonboxtester''; UPDATE revora_event_awards SET earned_at=earned_at-interval ''31 days'',expires_at=expires_at-interval ''31 days'' WHERE source=''homecoming-revora-v1'' AND uid=(SELECT entityid FROM accounts WHERE username=''MoonBoxTester''); COMMIT;"' | Out-Null
                        Write-Output 'Private Homecoming expiry fixture aged matching return/award timestamps; original preimage retained outside Git.'
                    }
                }
                $expiredReceiptBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(r)::text,'','' ORDER BY auth_number)) FROM revora_account_returns r WHERE account_name=''moonboxtester'';"'
            }
            if ($CheckHomecomingPresence) {
                $presenceBefore = Read-CandidatePresence ($CheckCourierCrash -and $cycle -eq 2)
                if ($cycle -gt 1 -and ($presenceBefore | ConvertTo-Json -Compress) -ne ($presenceAfter | ConvertTo-Json -Compress)) { throw 'Homecoming activity/session history changed across stopped database restart.' }
            }
            if ($CheckSecondLife) {
                $secondLifeBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_courier_claims r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND r.life=1;"'
            }
            if ($CheckSecondRebirth) {
                $secondRebirthBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_second_rebirths r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
            }
            if ($CheckFirstRebirth) {
                $rebirthBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_first_rebirths r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
            }
            if ($CheckMoonFinish) {
                $moonResultBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.run)),''none'') FROM revora_moon_results r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
            }
            if ($CheckMoonAccess) {
                $moonRunBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_moon_runs r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
            }
            if ($CheckCharacterEntry) {
                $courierAwardBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_courier_claims r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND r.life=0;"'
                if ($CheckSoulTrialAccess) {
                    $soulAwardBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_soul_trial_claims r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND r.life=0;"'
                }
                if ($CheckPathHunt) {
                    $pathAwardBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_path_hunt_claims r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND r.life=0;"'
                }
                $characterState = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT entityid FROM accounts WHERE username=''MoonBoxTester'' AND state=2 AND (entityid=0 AND NOT EXISTS(SELECT 1 FROM entities) OR entityid<>0 AND (SELECT count(*) FROM entities)=1 AND EXISTS(SELECT 1 FROM entities WHERE uid=accounts.entityid AND owner=''MoonBoxTester'' AND name=''RoadmapProbe'' AND class BETWEEN 10 AND 15));"'
                if ($CheckCourierCrash -and $courierAwardBefore -eq 'none') { throw 'Courier crash recovery requires a previously earned receipt.' }
                if ($characterState -notmatch '^\d+$') { throw 'Private ordinary character preimage failed.' }
                if ($cycle -gt 1 -and [uint32]$characterState -ne $characterUid) { throw 'Private character identity changed across restart.' }
                $characterUid = [uint32]$characterState
            }
            if ($CheckCourierCrash -and $CheckTournamentAccess -and $cycle -eq 1) {
                $recentTeamRuns = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_local_team_pk_runs WHERE kind=0 AND started_at>now()-interval ''24 hours'';"'
                if ($CheckSkillTeamPk) {
                $recentTeamRuns = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_local_team_pk_runs WHERE kind=1 AND started_at>now()-interval ''24 hours'';"'
                }
                if ($recentTeamRuns -ne '0') { throw 'Private Team PK opening was already used recently; inspect retained runs or use the return-only check instead of resetting them.' }
            }
            if ($CheckLocalTeamPkReturn -and $cycle -eq 1) {
                $returnBefore = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT ad.run::text||'':''||ad.pkmode::text FROM revora_local_team_pk_admissions ad JOIN accounts a ON a.entityid=ad.uid WHERE a.username=''MoonBoxTester'' AND NOT ad.returned;"'
                if ($returnBefore -notmatch '^[0-9a-f-]{36}:[0-9]+$') { throw 'Expected one saved pending Team PK return.' }
            }
            if ($CheckGameStartup -or $CheckLmsRecovery -or $CheckRelicRecovery -or $CheckWaveRecovery -or $CheckRoadmapStartup) { & (Join-Path $PSScriptRoot 'Check-CandidateGameStartup.ps1') -CheckWireAuth:$CheckWireAuth -CheckCharacterEntry:$CheckCharacterEntry -CharacterUid $characterUid -RequireCourierResume:($CheckCharacterEntry -and $cycle -gt 1) -CheckCourierCrash:($CheckCourierCrash -and $cycle -eq 1) -CheckSoulTrialAccess:$CheckSoulTrialAccess -SoulCombat $SoulCombat -SoulBatch $SoulBatch -CheckPathHunt:$CheckPathHunt -PathCombat $PathCombat -CheckPromotion:$CheckPromotion -CheckMoonAccess:$CheckMoonAccess -CheckMoonCombat:$CheckMoonCombat -CheckMoonFinish:$CheckMoonFinish -CheckFirstRebirth:$CheckFirstRebirth -CheckSecondLife:$CheckSecondLife -CheckSecondPromotion:$CheckSecondPromotion -CheckSecondRebirth:$CheckSecondRebirth -CheckHomecomingReturn:$CheckHomecomingReturn -CheckHomecomingExpired:$CheckHomecomingExpired -CheckTournamentAccess:$CheckTournamentAccess -CheckSkillTeamPk:$CheckSkillTeamPk }
            if ($CheckHomecomingReturn) {
                $returnValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM revora_account_returns r JOIN accounts a ON a.entityid=r.character_uid AND lower(a.username)=r.account_name JOIN revora_event_awards w ON w.uid=r.character_uid AND w.source=''homecoming-revora-v1'' AND w.title_key=r.title_key WHERE a.username=''MoonBoxTester'' AND a.state=2 AND r.title_key=20312031 AND r.award_recorded AND r.returned_at-r.inactive_since>=interval ''30 days'' AND r.expires_at=r.returned_at+interval ''720 hours'' AND w.earned_at=r.returned_at AND w.expires_at=r.expires_at AND NOT w.revoked AND r.character_snapshot->>''reborn''=''2'' AND r.character_snapshot->>''vip_level''=''7'';"'
                $returnAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(r)::text,'','' ORDER BY auth_number)) FROM revora_account_returns r WHERE account_name=''moonboxtester'';"'
                if ($returnValid -ne 't' -or $returnAfter -notmatch '^[0-9a-f]{32}$' -or ($cycle -gt 1 -and $returnAfter -ne $returnBaseline)) { throw 'Homecoming frozen return/award or restart nonrenewal failed.' }
                if ($CheckHomecomingExpired) {
                    $expiredValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 AND bool_and(r.expires_at<now()) AND NOT EXISTS(SELECT 1 FROM titles t JOIN accounts a ON a.entityid=t.id CROSS JOIN generate_series(1,octet_length(t.data)-5,5) n WHERE a.username=''MoonBoxTester'' AND get_byte(t.data,n)=239 AND get_byte(t.data,n+1)=7 AND get_byte(t.data,n+2)=239 AND get_byte(t.data,n+3)=7) FROM revora_account_returns r WHERE r.account_name=''moonboxtester'';"'
                    if ($expiredValid -ne 't' -or $returnAfter -ne $expiredReceiptBefore) { throw 'Expired return renewed or remained in saved wardrobe.' }
                    Write-Output ('Expired Homecoming receipt retained without renewal; saved title removed; cycle=' + $cycle)
                }
                $returnBaseline=$returnAfter
                Write-Output ('Private qualifying Homecoming receipt, matching timed award and nonrenewal passed; cycle=' + $cycle)
            }
            if ($CheckHomecomingPresence) {
                $presenceAfter = Read-CandidatePresence ($CheckCourierCrash -and $cycle -eq 1)
                if ($presenceAfter.auth -le $presenceBefore.auth -or $presenceAfter.sessions -ne $presenceBefore.sessions + 1 -or $presenceAfter.history -eq $presenceBefore.history) { throw 'Real socket login did not persist exactly one game session and authentication activity.' }
                Write-Output ('Homecoming actual active-player authentication/session lifecycle and no-return checks passed; cycle=' + $cycle)
            }
            if ($CheckCharacterEntry) {
                $savedArgs = '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT a.entityid FROM accounts a JOIN entities e ON e.uid=a.entityid JOIN revora_character_creation r ON r.character_uid=e.uid AND r.account_name=lower(a.username) WHERE a.username=''MoonBoxTester'' AND a.state=2 AND e.owner=a.username AND e.name=''RoadmapProbe'' AND r.character_name=e.name AND e.class BETWEEN 10 AND 15 AND e.mapid=1002 AND e.x=327 AND e.y=303 AND (SELECT count(*) FROM entities)=1;"'
                if ($CheckCourierCrash -and $CheckTournamentAccess -and $cycle -eq 1) {
                    $savedArgs = $savedArgs.Replace('e.mapid=1002 AND e.x=327 AND e.y=303','EXISTS(SELECT 1 FROM revora_local_team_pk_admissions ad JOIN revora_local_team_pk_runs tr ON tr.run=ad.run WHERE ad.uid=e.uid AND NOT ad.returned AND tr.state=''active'')')
                }
                $saved = Invoke-CandidateTool 'psql.exe' $savedArgs
                if ($saved -notmatch '^[1-9]\d*$' -or ($characterUid -ne 0 -and [uint32]$saved -ne $characterUid)) { throw 'Private character persistence check failed.' }
                $characterUid = [uint32]$saved
                Write-Output ('Private ordinary character and creation receipt retained; uid=' + $characterUid + '; cycle=' + $cycle)
                $courierAwardAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_courier_claims r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND r.life=0;"'
                if ($courierAwardBefore -ne 'none' -and $courierAwardAfter -ne $courierAwardBefore) { throw 'Courier earned receipt changed during replay/restart.' }
                if ($courierAwardAfter -ne 'none') {
                    $courierAwardValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM revora_courier_claims r JOIN accounts a ON a.entityid=r.uid JOIN entities e ON e.uid=r.uid JOIN items i ON i.uid=r.soul_uid AND i.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND r.life=0 AND r.cp=8000 AND r.rvp=200 AND r.silver=80000 AND r.target_level=120 AND r.vip_level=6 AND r.vip_days=7 AND r.vip_level_days=7 AND (e.level=120 OR (e.level=15 AND e.reborn IN(1,2) AND EXISTS(SELECT 1 FROM revora_first_rebirths f WHERE f.uid=e.uid AND f.old_class=15 AND f.old_level=120 AND f.new_class=11))) AND i.bound=1 AND i.position=0 AND i.warehouse=0 AND i.id BETWEEN 800000 AND 899999 AND COALESCE(NULLIF(i.days,''''),''0'')=''0'';"'
                    if ($courierAwardValid -ne 't') { throw 'Courier earned receipt/item/profile failed validation.' }
                    Write-Output ('Private Courier exact earned receipt and bound bag soul verified; existing receipt preserved=' + ($courierAwardBefore -ne 'none'))
                }
            }
            if ($CheckSoulTrialAccess) {
                $soulAwardAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_soul_trial_claims r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND r.life=0;"'
                if ($soulAwardBefore -ne 'none' -and $soulAwardAfter -ne $soulAwardBefore) { throw 'Soul Trial earned receipt changed across replay/restart.' }
                if ($soulAwardAfter -ne 'none') {
                    $soulValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM revora_soul_trial_claims r JOIN accounts a ON a.entityid=r.uid JOIN entities e ON e.uid=r.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND r.life=0 AND r.rvp=500 AND r.silver=50000 AND r.target_level=120 AND (e.level=120 OR (e.level=15 AND e.reborn IN(1,2) AND EXISTS(SELECT 1 FROM revora_first_rebirths f WHERE f.uid=e.uid AND f.old_class=15 AND f.old_level=120 AND f.new_class=11))) AND cardinality(r.rewards)=16 AND (SELECT count(*)=16 AND count(*) FILTER(WHERE i.id=723017 AND i.bound=0)=15 AND count(*) FILTER(WHERE i.uid=r.soul_uid AND i.id BETWEEN 800000 AND 899999 AND i.bound=1)=1 FROM items i WHERE i.uid=ANY(r.rewards) AND i.entityid=r.uid AND i.position=0 AND i.warehouse=0 AND COALESCE(NULLIF(i.days,''''),''0'')=''0'');"'
                    if ($soulValid -ne 't') { throw 'Soul Trial earned receipt and reward items failed validation.' }
                    Write-Output ('Private Soul Trial exact earned receipt and all 16 permanent reward items verified; existing receipt preserved=' + ($soulAwardBefore -ne 'none'))
                }
            }
            if ($CheckPathHunt) {
                $pathAwardAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_path_hunt_claims r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND r.life=0;"'
                if ($pathAwardBefore -ne 'none' -and $pathAwardAfter -ne $pathAwardBefore) { throw 'Path Hunt earned receipt changed across replay/restart.' }
                if ($pathAwardAfter -ne 'none') {
                    $pathValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM revora_path_hunt_claims r JOIN accounts a ON a.entityid=r.uid JOIN entities e ON e.uid=r.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND r.life=0 AND r.valuable=true AND r.rvp=100 AND r.silver=25000 AND r.target_level=120 AND (e.level=120 OR (e.level=15 AND e.reborn IN(1,2) AND EXISTS(SELECT 1 FROM revora_first_rebirths f WHERE f.uid=e.uid AND f.old_class=15 AND f.old_level=120 AND f.new_class=11))) AND cardinality(r.rewards)=12 AND (SELECT count(*)=12 AND count(*) FILTER(WHERE i.id=723017 AND i.bound=0)=11 AND count(*) FILTER(WHERE i.uid=r.soul_uid AND i.id BETWEEN 800000 AND 899999 AND i.bound=1)=1 FROM items i WHERE i.uid=ANY(r.rewards) AND i.entityid=r.uid AND i.position=0 AND i.warehouse=0 AND COALESCE(NULLIF(i.days,''''),''0'')=''0'');"'
                    if ($pathValid -ne 't') { throw 'Path Hunt earned receipt and reward items failed validation.' }
                    Write-Output ('Private Path Hunt exact earned receipt and all 12 permanent reward items verified; existing receipt preserved=' + ($pathAwardBefore -ne 'none'))
                }
            }
            if ($CheckPromotion) {
                $promotionValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM entities e JOIN accounts a ON a.entityid=e.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND ((e.reborn=0 AND e.class IN(14,15)) OR (e.reborn IN(1,2) AND e.class BETWEEN 11 AND 15 AND e.firstrebornclass=15)) AND (SELECT count(DISTINCT achievement_id) FROM revora_achievement_evidence WHERE uid=e.uid AND achievement_id IN(10819,10820,10821,10822,10823))=(CASE WHEN e.reborn IN(1,2) THEN e.firstrebornclass ELSE e.class END)-10 AND NOT EXISTS(SELECT 1 FROM items WHERE entityid=e.uid AND id IN(1080001,1088001)) AND (SELECT count(*) FROM items WHERE entityid=e.uid AND id=700031 AND bound=0 AND position=0 AND warehouse=0 AND COALESCE(NULLIF(days,''''),''0'')=''0'')=(CASE WHEN e.reborn=2 OR (e.reborn=1 AND e.class>=14) THEN 2 ELSE 1 END);"'
                if ($promotionValid -ne 't') { throw 'Earned Trojan promotion class/evidence did not persist.' }
                Write-Output 'Private ordinary promotion class and matching earned achievement receipts retained.'
            }
            if ($CheckMoonAccess) {
                $moonState = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 AND bool_and(r.phase=''portal'' AND r.area=1042 AND r.tokens=ARRAY[0,0,0,0,0,0]::bigint[] AND r.kills=ARRAY[0,0,0,0,0,0] AND r.jade_uid=0 AND r.valuable_reward AND NOT EXISTS(SELECT 1 FROM revora_moon_results x WHERE x.uid=r.uid)) FROM revora_moon_runs r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
                $moonRunAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_moon_runs r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
                if (!$CheckMoonCombat -and ($moonState -ne 't' -or $moonRunAfter -notmatch '^[0-9a-f]{32}$' -or ($moonRunBefore -ne 'none' -and $moonRunBefore -ne $moonRunAfter))) { throw 'Moon Box entry changed or lost the durable portal run.' }
                if (!$CheckMoonCombat) { Write-Output 'Private Moon Box portal run retained without counters, tokens, payment or rewards.' }
                if ($CheckMoonCombat -and !$CheckMoonFinish) {
                    $moonEarned = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 AND bool_and(r.phase=''portal'' AND r.area=1042 AND r.jade_uid=0 AND r.valuable_reward AND NOT EXISTS(SELECT 1 FROM revora_moon_results x WHERE x.uid=r.uid) AND NOT EXISTS(SELECT 1 FROM generate_subscripts(r.tokens,1) n WHERE r.kills[n]<1 OR r.kills[n]>20 OR NOT EXISTS(SELECT 1 FROM items i WHERE i.uid=r.tokens[n] AND i.entityid=r.uid AND i.id=721009+n AND i.position=0 AND i.warehouse=0 AND i.bound=1 AND i.days=''0''))) FROM revora_moon_runs r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
                    if ($moonEarned -ne 't' -or ($cycle -gt 1 -and $moonRunBefore -ne $moonRunAfter)) { throw 'Moon Box six earned token identities or ownership did not persist.' }
                    Write-Output 'Private Moon Box six earned bound token UIDs and kill counters persisted in the ordinary inventory.'
                }
            }
            if ($CheckMoonFinish) {
                $moonComplete = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM revora_moon_runs r JOIN revora_moon_results z ON z.run=r.run AND z.uid=r.uid JOIN accounts a ON a.entityid=r.uid JOIN entities e ON e.uid=r.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND ((e.class=15 AND e.reborn=0) OR (e.class BETWEEN 11 AND 15 AND e.reborn IN(1,2) AND e.firstrebornclass=15)) AND r.phase=''complete'' AND r.area=1002 AND r.tokens=ARRAY[0,0,0,0,0,0]::bigint[] AND r.jade_uid=0 AND z.choice=''moon-box'' AND cardinality(z.rewards)=2 AND NOT EXISTS(SELECT 1 FROM items WHERE uid=z.rewards[1]) AND EXISTS(SELECT 1 FROM items WHERE uid=z.rewards[2] AND entityid=r.uid AND id BETWEEN 800000 AND 899999 AND bound=1 AND position=0 AND warehouse=0 AND COALESCE(NULLIF(days,''''),''0'')=''0'') AND NOT EXISTS(SELECT 1 FROM items WHERE entityid=r.uid AND (id BETWEEN 721010 AND 721015 OR id IN(721072,721090))) AND (SELECT count(*) FROM items WHERE entityid=r.uid AND id=1088000 AND bound=0 AND position=0 AND warehouse=0 AND COALESCE(NULLIF(days,''''),''0'')=''0'')=(CASE WHEN e.reborn=2 OR (e.reborn=1 AND e.class=15) THEN 2 ELSE 1 END) AND (SELECT count(DISTINCT achievement_id) FROM revora_achievement_evidence WHERE uid=r.uid AND achievement_id IN(10819,10820,10821,10822,10823,11022))=6;"'
                $moonResultAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.run)),''none'') FROM revora_moon_results r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
                if ($moonComplete -ne 't' -or $moonResultAfter -notmatch '^[0-9a-f]{32}$' -or ($moonResultBefore -ne 'none' -and $moonResultBefore -ne $moonResultAfter)) { throw 'Earned Moon Box completion, consumption or reward persistence failed.' }
                $moonSoul = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT i.id FROM revora_moon_results r JOIN accounts a ON a.entityid=r.uid JOIN items i ON i.uid=r.rewards[2] WHERE a.username=''MoonBoxTester'' AND r.choice=''moon-box'';"'
                if ($moonSoul -notmatch '^[0-9]{6}$') { throw 'Moon Box soul identity is invalid.' }
                $catalogRows = @([IO.File]::ReadLines((Join-Path $candidateRoot 'game/database/itemtype-new.txt')) | Where-Object { $_.StartsWith($moonSoul + '@@') })
                if ($catalogRows.Count -ne 1 -or ($catalogRows[0].Split(@('@@'),[StringSplitOptions]::RemoveEmptyEntries))[55] -ne '7') { throw 'Moon Box reward is not a catalog P7 soul.' }
                Write-Output 'Private Moon Box completion receipt, bound P7, consumed box, first-life final rank, DragonBall and six achievement receipts persisted.'
            }
            if ($CheckFirstRebirth) {
                $rebirthValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM revora_first_rebirths r JOIN accounts a ON a.entityid=r.uid JOIN entities e ON e.uid=r.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND r.old_class=15 AND r.old_level=120 AND r.new_class=11 AND r.body=1003 AND NOT r.bound AND r.blessed_item=0 AND e.class BETWEEN 11 AND 15 AND (e.level=15 OR (e.level=120 AND EXISTS(SELECT 1 FROM revora_courier_claims q WHERE q.uid=e.uid AND q.life=1 AND q.target_level=120))) AND e.experience=0 AND e.reborn IN(1,2) AND e.firstrebornclass=15 AND e.firstrebornlevel=120 AND (e.secondrebornclass=0 OR (e.reborn=2 AND e.secondrebornclass=15 AND e.secondrebornlevel=120)) AND e.strength+e.agility+e.vitality+e.spirit+e.atributes=(CASE WHEN e.reborn=2 THEN 82 ELSE 72+(e.level-15)*3 END) AND NOT EXISTS(SELECT 1 FROM items WHERE uid=r.stone_uid) AND EXISTS(SELECT 1 FROM items WHERE uid=r.reward_item AND entityid=r.uid AND id IN(700003,700013,700023,700033,700043,700053,700063) AND bound=0 AND position=0 AND warehouse=0 AND COALESCE(NULLIF(days,''''),''0'')=''0'') AND EXISTS(SELECT 1 FROM items WHERE uid=r.garment_uid AND entityid=r.uid AND id=187405 AND bound=0 AND position=0 AND warehouse=0 AND COALESCE(NULLIF(days,''''),''0'')=''0'') AND (SELECT count(*) FROM items WHERE entityid=r.uid AND id=410077 AND bound=0 AND position=0 AND warehouse=0 AND COALESCE(NULLIF(days,''''),''0'')=''0'')=(CASE WHEN e.reborn=2 THEN 2 ELSE 1 END) AND (SELECT count(DISTINCT achievement_id) FROM revora_achievement_evidence WHERE uid=r.uid AND achievement_id IN(11030,10315))=2;"'
                $rebirthAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_first_rebirths r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
                if ($rebirthValid -ne 't' -or $rebirthAfter -notmatch '^[0-9a-f]{32}$' -or ($rebirthBefore -ne 'none' -and $rebirthBefore -ne $rebirthAfter)) { throw 'First rebirth profile, material consumption or reward persistence failed.' }
                Write-Output 'Private first rebirth receipt, current career profile and attribute budget, super gem, weapon and permanent unbound garment persisted.'
            }
            if ($CheckSecondLife) {
                $secondLifeValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM revora_courier_claims r JOIN accounts a ON a.entityid=r.uid JOIN entities e ON e.uid=r.uid JOIN items i ON i.uid=r.soul_uid AND i.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND r.life=1 AND r.cp=8000 AND r.rvp=200 AND r.silver=80000 AND r.target_level=120 AND (e.level=120 OR (e.reborn=2 AND e.level=15 AND e.secondrebornclass=15 AND e.secondrebornlevel=120)) AND e.reborn IN(1,2) AND e.class BETWEEN 11 AND 15 AND e.strength+e.agility+e.vitality+e.spirit+e.atributes=(CASE WHEN e.reborn=2 THEN 82 ELSE 387 END) AND i.bound=1 AND i.position=0 AND i.warehouse=0 AND i.id BETWEEN 800000 AND 899999 AND COALESCE(NULLIF(i.days,''''),''0'')=''0'';"'
                $secondLifeAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_courier_claims r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'' AND r.life=1;"'
                if ($secondLifeValid -ne 't' -or $secondLifeAfter -notmatch '^[0-9a-f]{32}$' -or ($secondLifeBefore -ne 'none' -and $secondLifeBefore -ne $secondLifeAfter)) { throw 'Second-life Courier reward/profile was not earned or retained.' }
                Write-Output 'Private second-life Courier receipt, bound permanent soul and earned leveling history persisted; first-life receipts unchanged.'
            }
            if ($CheckSecondPromotion) {
                $secondPromotion = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM entities e JOIN accounts a ON a.entityid=e.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND ((e.reborn=1 AND e.class=15 AND e.level=120) OR (e.reborn=2 AND e.class=11 AND e.level=15 AND e.secondrebornclass=15 AND e.secondrebornlevel=120)) AND e.firstrebornclass=15 AND e.firstrebornlevel=120 AND NOT EXISTS(SELECT 1 FROM items WHERE entityid=e.uid AND id IN(1080001,1088001,3001405,721020));"'
                if ($secondPromotion -ne 't') { throw 'Second-life final promotion or material consumption did not persist.' }
                Write-Output 'Private second-life final-rank history persisted; purchased Emerald, Meteor and boxed Moon Box consumed.'
            }
            if ($CheckSecondRebirth) {
                $secondRebirthValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM revora_second_rebirths r JOIN accounts a ON a.entityid=r.uid JOIN entities e ON e.uid=r.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND r.first_class=15 AND r.first_level=120 AND r.old_class=15 AND r.old_level=120 AND r.new_class=11 AND NOT r.bound AND r.payment_kind=''exemption-token'' AND r.quest_run IS NULL AND e.class=11 AND e.level=15 AND e.experience=0 AND e.reborn=2 AND e.firstrebornclass=15 AND e.firstrebornlevel=120 AND e.secondrebornclass=15 AND e.secondrebornlevel=120 AND e.strength+e.agility+e.vitality+e.spirit+e.atributes=82 AND NOT EXISTS(SELECT 1 FROM items WHERE uid=r.token_uid) AND EXISTS(SELECT 1 FROM revora_exemption_purchases p WHERE p.uid=r.uid AND p.shop=2888 AND p.item_type=723701 AND p.quantity=1 AND p.unit_price=888 AND p.cost=888 AND p.balance_before-p.balance_after=888 AND p.item_uids=ARRAY[r.token_uid] AND NOT p.bound) AND EXISTS(SELECT 1 FROM items WHERE uid=r.reward_item AND entityid=r.uid AND id=410077 AND bound=0 AND position=0 AND warehouse=0 AND COALESCE(NULLIF(days,''''),''0'')=''0'') AND EXISTS(SELECT 1 FROM items WHERE uid=r.soul_uid AND entityid=r.uid AND id BETWEEN 800000 AND 899999 AND bound=1 AND position=0 AND warehouse=0 AND COALESCE(NULLIF(days,''''),''0'')=''0'') AND EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=r.uid AND achievement_id=10316);"'
                $secondRebirthAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT COALESCE(md5(string_agg(row_to_json(r)::text,'','' ORDER BY r.uid)),''none'') FROM revora_second_rebirths r JOIN accounts a ON a.entityid=r.uid WHERE a.username=''MoonBoxTester'';"'
                if ($secondRebirthValid -ne 't' -or $secondRebirthAfter -notmatch '^[0-9a-f]{32}$' -or ($secondRebirthBefore -ne 'none' -and $secondRebirthBefore -ne $secondRebirthAfter)) { throw 'Second rebirth earned payment, profile or reward persistence failed.' }
                $heroValid = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=1 FROM revora_event_awards w JOIN accounts a ON a.entityid=w.uid JOIN titles t ON t.id=w.uid WHERE a.username=''MoonBoxTester'' AND a.state=2 AND w.source=''honored-hero-revora-v1'' AND w.source_key=''career-v1'' AND w.title_key=20572057 AND w.expires_at IS NULL AND NOT w.revoked AND t.points>=30 AND EXISTS(SELECT 1 FROM generate_series(1,octet_length(t.data)-5,5) n WHERE get_byte(t.data,n)=9 AND get_byte(t.data,n+1)=8 AND get_byte(t.data,n+2)=9 AND get_byte(t.data,n+3)=8);"'
                if ($heroValid -ne 't') { throw 'Earned permanent Honored Hero entitlement/ownership did not persist.' }
                $selectedTitleValid = if ($CheckHomecomingReturn -and !$CheckHomecomingExpired) { Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT EXISTS(SELECT 1 FROM titles t JOIN accounts a ON a.entityid=t.id CROSS JOIN generate_series(1,octet_length(t.data)-5,5) n WHERE a.username=''MoonBoxTester'' AND get_byte(t.data,n)=239 AND get_byte(t.data,n+1)=7 AND get_byte(t.data,n+2)=239 AND get_byte(t.data,n+3)=7 AND get_byte(t.data,n+4)=1);"' } else { Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT EXISTS(SELECT 1 FROM titles t JOIN accounts a ON a.entityid=t.id CROSS JOIN generate_series(1,octet_length(t.data)-5,5) n WHERE a.username=''MoonBoxTester'' AND get_byte(t.data,n)=9 AND get_byte(t.data,n+1)=8 AND get_byte(t.data,n+2)=9 AND get_byte(t.data,n+3)=8 AND get_byte(t.data,n+4)=1);"' }
                if ($selectedTitleValid -ne 't') { throw 'Expected earned title selection did not persist.' }
                $secondSoul = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT i.id FROM revora_second_rebirths r JOIN accounts a ON a.entityid=r.uid JOIN items i ON i.uid=r.soul_uid WHERE a.username=''MoonBoxTester'';"'
                if ($secondSoul -notmatch '^[0-9]{6}$') { throw 'Second rebirth soul identity is invalid.' }
                $secondSoulRows = @([IO.File]::ReadLines((Join-Path $candidateRoot 'game/database/itemtype-new.txt')) | Where-Object { $_.StartsWith($secondSoul + '@@') })
                if ($secondSoulRows.Count -ne 1 -or ($secondSoulRows[0].Split(@('@@'),[StringSplitOptions]::RemoveEmptyEntries))[55] -ne '7') { throw 'Second rebirth reward is not a catalog P7 soul.' }
                Write-Output 'Private second rebirth, receipted 888-CP purchase, token consumption, 82 attributes, rewards, permanent Honored Hero ownership and expected title selection persisted.'
            }
            if ($CheckLocalTeamPkReturn) {
                $returnAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT ad.run::text||'':''||ad.pkmode::text FROM revora_local_team_pk_admissions ad JOIN revora_local_team_pk_runs r ON r.run=ad.run JOIN accounts a ON a.entityid=ad.uid WHERE a.username=''MoonBoxTester'' AND ad.returned AND r.state=''cancelled'' AND NOT ad.entry_earned AND ad.entry_item IS NULL AND cardinality(r.leaders)=0 ORDER BY r.started_at DESC LIMIT 1;"'
                if ($returnAfter -ne $returnBefore) { throw 'Saved Team PK return identity/mode or recovery state changed.' }
                Write-Output ('Private previously interrupted Team PK admission returned on ordinary reconnect, with no invented reward; cycle=' + $cycle)
            }
            if ($CheckCourierCrash -and $CheckTournamentAccess) {
                $teamReturn = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT r.run::text||'':''||r.state||'':''||ad.returned::text||'':''||ad.pkmode::text FROM revora_local_team_pk_admissions ad JOIN revora_local_team_pk_runs r ON r.run=ad.run JOIN accounts a ON a.entityid=ad.uid WHERE a.username=''MoonBoxTester'' ORDER BY r.started_at DESC LIMIT 1;"'
                if ($cycle -eq 1) {
                    if ($teamReturn -notmatch '^([0-9a-f-]{36}):active:false:([0-9]+)$') { throw 'Killed Team PK session has no pending active admission.' }
                    $teamCrashRun=$Matches[1];$teamCrashMode=$Matches[2]
                } elseif ($teamReturn -ne ($teamCrashRun + ':cancelled:true:' + $teamCrashMode)) { throw 'Interrupted Team PK admission did not recover on actual reconnect.' }
                $savedKind = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT r.kind FROM revora_local_team_pk_admissions ad JOIN revora_local_team_pk_runs r ON r.run=ad.run JOIN accounts a ON a.entityid=ad.uid WHERE a.username=''MoonBoxTester'' ORDER BY r.started_at DESC LIMIT 1;"'
                if ($savedKind -ne $(if ($CheckSkillTeamPk) { '1' } else { '0' })) { throw 'Interrupted admission belongs to the wrong tournament series.' }
                $noReward = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT NOT ad.entry_earned AND ad.entry_item IS NULL AND cardinality(r.leaders)=0 AND NOT EXISTS(SELECT 1 FROM revora_skill_pk_round_rewards sr WHERE sr.run=r.run) AND NOT EXISTS(SELECT 1 FROM revora_local_team_pk_claims cl WHERE cl.run=r.run) FROM revora_local_team_pk_admissions ad JOIN revora_local_team_pk_runs r ON r.run=ad.run JOIN accounts a ON a.entityid=ad.uid WHERE a.username=''MoonBoxTester'' ORDER BY r.started_at DESC LIMIT 1;"'
                if ($noReward -ne 't') { throw 'Interrupted registration invented a tournament reward.' }
                Write-Output ('Private tournament live-session admission/recovery verified; skill=' + $CheckSkillTeamPk + '; no invented reward; cycle=' + $cycle)
            }
            if ($CheckRoadmapStartup) {
                # Read-only schema evidence after the actual game has initialized and exited.
                # No synthetic awards or characters are inserted by this check.
                $roadmapIdentity = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT CASE WHEN count(*)=33 AND bool_and(pg_get_userbyid(c.relowner)=''revora_candidate_runtime'') THEN md5(string_agg(c.relname||'':''||c.oid::text,'','' ORDER BY c.relname)) ELSE ''invalid'' END FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=''public'' AND c.relkind=''r'' AND c.relname IN (''revora_mrms2017_seasons'',''revora_mrms2017_entrants'',''revora_mrms2017_donations'',''revora_mrms2017_results'',''revora_heart_weekends'',''revora_heart_entries'',''revora_heart_runs'',''revora_heart_objectives'',''revora_heart_encounters'',''revora_heart_actors'',''revora_heart_scores'',''revora_heart_results'',''revora_euro_jersey_purchases'',''revora_achievement_evidence'',''revora_event_awards'',''revora_plus_compositions'',''revora_exemption_purchases'',''revora_guild_managers'',''revora_perfection_boosts'',''revora_arena_daily_rewards'',''revora_team_arena_daily_rewards'',''revora_guild_purses'',''revora_super_guild_battles'',''revora_super_guild_captures'',''revora_super_guild_purses'',''revora_clan_war_awards'',''revora_elite_guild_battles'',''revora_elite_guild_captures'',''revora_local_team_pk_runs'',''revora_local_team_pk_claims'',''revora_local_team_pk_admissions'',''revora_skill_pk_rounds'',''revora_skill_pk_round_rewards'');"'
                if ($roadmapIdentity -notmatch '^[0-9a-f]{32}$') { throw 'Roadmap startup tables are missing or have unexpected ownership.' }
                if ($cycle -eq 1) { $roadmapBaseline = $roadmapIdentity }
                elseif ($roadmapIdentity -ne $roadmapBaseline) { throw 'Roadmap tables were replaced across the game/database restart.' }
                $guildDeadlines = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=2 FROM pg_attribute WHERE attrelid IN(''public.revora_super_guild_battles''::regclass,''public.revora_elite_guild_battles''::regclass) AND attname=''closes_at'' AND atttypid=''timestamp with time zone''::regtype AND NOT attnotnull AND NOT attisdropped;"'
                if ($guildDeadlines -ne 't') { throw 'Guild War scheduled deadlines were not initialized by the game.' }
                $arsenalSchema = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT EXISTS(SELECT 1 FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum WHERE n.nspname=''public'' AND c.relname=''pt_arsenal'' AND a.attname=''enhancement_expires_utc_ticks'' AND a.atttypid=''bigint''::regtype AND a.attnotnull AND NOT a.attisdropped AND pg_get_expr(d.adbin,d.adrelid)=''0'');"'
                if ($arsenalSchema -ne 't') { throw 'Arsenal UTC deadline schema was not initialized correctly by the game.' }
                $boostSchema = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=10 AND bool_and(a.attnotnull AND a.atttypid=e.typ::regtype) FROM (VALUES(''id'',''uuid''),(''uid'',''bigint''),(''item'',''bigint''),(''cost'',''bigint''),(''bound_cost'',''bigint''),(''level_before'',''integer''),(''progress_before'',''bigint''),(''level_after'',''integer''),(''progress_after'',''bigint''),(''purchased_at'',''timestamp with time zone'')) e(name,typ) JOIN pg_attribute a ON a.attrelid=''public.revora_perfection_boosts''::regclass AND a.attname=e.name AND NOT a.attisdropped;"'
                if ($boostSchema -ne 't') { throw 'Boost receipt columns were not initialized correctly by the game.' }
                $arenaAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)::text||'':''||COALESCE(md5(string_agg(c.oid::text,'','' ORDER BY c.relname)),'''') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=''public'' AND c.relkind=''r'' AND c.relname IN(''revora_arena_daily_rewards'',''revora_team_arena_daily_rewards'');"'
                if ($arenaBefore -match '^2:' -and $arenaAfter -ne $arenaBefore) { throw 'Arena daily tables were replaced during startup migration.' }
                $arenaRuneSchema = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=4 AND bool_and(CASE WHEN column_name=''rune_type'' THEN data_type=''integer'' AND is_nullable=''NO'' AND COALESCE(column_default,'''')=''0'' ELSE data_type=''bigint'' AND is_nullable=''YES'' END) FROM information_schema.columns WHERE table_schema=''public'' AND table_name IN(''revora_arena_daily_rewards'',''revora_team_arena_daily_rewards'') AND column_name IN(''rune_type'',''rune_uid'');"'
                if ($arenaRuneSchema -ne 't') { throw 'Arena rune reward schema is missing or incompatible.' }
                $guildSchema = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=27 AND bool_and(a.attnotnull=e.required AND a.atttypid=e.typ::regtype) FROM (VALUES(''revora_super_guild_battles'',''id'',''uuid'',true),(''revora_super_guild_battles'',''started_at'',''timestamp with time zone'',true),(''revora_super_guild_battles'',''ended_at'',''timestamp with time zone'',false),(''revora_super_guild_battles'',''state'',''text'',true),(''revora_super_guild_battles'',''keeper'',''bigint'',true),(''revora_super_guild_battles'',''captures'',''integer'',true),(''revora_super_guild_battles'',''purses_frozen'',''boolean'',true),(''revora_super_guild_captures'',''battle'',''uuid'',true),(''revora_super_guild_captures'',''number'',''integer'',true),(''revora_super_guild_captures'',''guild_id'',''bigint'',true),(''revora_super_guild_captures'',''damage'',''bigint'',true),(''revora_super_guild_captures'',''captured_at'',''timestamp with time zone'',true),(''revora_super_guild_purses'',''battle'',''uuid'',true),(''revora_super_guild_purses'',''uid'',''bigint'',true),(''revora_super_guild_purses'',''guild_id'',''bigint'',true),(''revora_super_guild_purses'',''rank'',''integer'',true),(''revora_super_guild_purses'',''cp'',''bigint'',true),(''revora_super_guild_purses'',''rvp'',''bigint'',true),(''revora_super_guild_purses'',''gold'',''bigint'',true),(''revora_super_guild_purses'',''tokens'',''integer'',true),(''revora_super_guild_purses'',''honor'',''integer'',true),(''revora_super_guild_purses'',''earned_at'',''timestamp with time zone'',true),(''revora_super_guild_purses'',''honor_until'',''timestamp with time zone'',true),(''revora_super_guild_purses'',''claimed_at'',''timestamp with time zone'',false),(''revora_super_guild_purses'',''rewards'',''bigint[]'',false),(''revora_super_guild_purses'',''soul_type'',''integer'',true),(''revora_guild_purses'',''soul_type'',''integer'',true)) e(tab,name,typ,required) JOIN pg_class c ON c.relname=e.tab JOIN pg_namespace n ON n.oid=c.relnamespace AND n.nspname=''public'' JOIN pg_attribute a ON a.attrelid=c.oid AND a.attname=e.name AND NOT a.attisdropped;"'
                if ($guildSchema -ne 't') { throw 'Guild purse/capture schema is missing or incompatible.' }
                $clanSoulSchema = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=2 AND bool_and(CASE WHEN column_name=''soul_type'' THEN data_type=''integer'' AND is_nullable=''NO'' AND COALESCE(column_default,'''')=''0'' ELSE data_type=''bigint'' AND is_nullable=''YES'' END) FROM information_schema.columns WHERE table_schema=''public'' AND table_name=''revora_clan_war_awards'' AND column_name IN(''soul_type'',''soul_uid'');"'
                if ($clanSoulSchema -ne 't') { throw 'Clan soul reward columns are incompatible.' }
                $eliteSchema = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=18 AND bool_and(a.attnotnull=e.required AND a.atttypid=e.typ::regtype) FROM (VALUES(''revora_elite_guild_battles'',''id'',''uuid'',true),(''revora_elite_guild_battles'',''started_at'',''timestamp with time zone'',true),(''revora_elite_guild_battles'',''ended_at'',''timestamp with time zone'',false),(''revora_elite_guild_battles'',''state'',''text'',true),(''revora_elite_guild_battles'',''keeper'',''bigint'',true),(''revora_elite_guild_battles'',''captures'',''integer'',true),(''revora_elite_guild_battles'',''gold'',''bigint'',true),(''revora_elite_guild_battles'',''rvp'',''bigint'',true),(''revora_elite_guild_battles'',''soul_type'',''integer'',true),(''revora_elite_guild_battles'',''claimed_at'',''timestamp with time zone'',false),(''revora_elite_guild_battles'',''claimed_by'',''bigint'',false),(''revora_elite_guild_battles'',''soul_uid'',''bigint'',false),(''revora_elite_guild_battles'',''honor_until'',''timestamp with time zone'',false),(''revora_elite_guild_captures'',''battle'',''uuid'',true),(''revora_elite_guild_captures'',''number'',''integer'',true),(''revora_elite_guild_captures'',''guild_id'',''bigint'',true),(''revora_elite_guild_captures'',''damage'',''bigint'',true),(''revora_elite_guild_captures'',''captured_at'',''timestamp with time zone'',true)) e(tab,name,typ,required) JOIN pg_class c ON c.relname=e.tab JOIN pg_namespace n ON n.oid=c.relnamespace AND n.nspname=''public'' JOIN pg_attribute a ON a.attrelid=c.oid AND a.attname=e.name AND NOT a.attisdropped;"'
                if ($eliteSchema -ne 't') { throw 'Elite Guild battle/capture columns are incompatible.' }
                $entrySchema = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=2 AND bool_and(CASE WHEN column_name=''entry_earned'' THEN data_type=''boolean'' AND is_nullable=''NO'' AND column_default=''false'' ELSE data_type=''bigint'' AND is_nullable=''YES'' END) FROM information_schema.columns WHERE table_schema=''public'' AND table_name=''revora_local_team_pk_admissions'' AND column_name IN(''entry_earned'',''entry_item'');"'
                if ($entrySchema -ne 't') { throw 'Local Team PK entry reward columns are incompatible.' }
                $entryIndex = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT EXISTS(SELECT 1 FROM pg_index i WHERE i.indexrelid=''public.revora_local_team_pk_entry_item''::regclass AND i.indrelid=''public.revora_local_team_pk_admissions''::regclass AND i.indisunique AND i.indisvalid AND pg_get_indexdef(i.indexrelid,1,true)=''entry_item'' AND pg_get_expr(i.indpred,i.indrelid)=''(entry_item IS NOT NULL)'');"'
                if ($entryIndex -ne 't') { throw 'Local Team PK entry receipt uniqueness is missing.' }
                $skillRoundSchema = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=11 AND bool_and(format_type(a.atttypid,a.atttypmod)=e.typ AND a.attnotnull=e.required) FROM (VALUES (''revora_skill_pk_rounds'',''run'',''uuid'',true),(''revora_skill_pk_rounds'',''match'',''bigint'',true),(''revora_skill_pk_rounds'',''round'',''integer'',true),(''revora_skill_pk_rounds'',''winner'',''bigint'',true),(''revora_skill_pk_rounds'',''loser'',''bigint'',true),(''revora_skill_pk_rounds'',''owners'',''bigint[]'',true),(''revora_skill_pk_round_rewards'',''run'',''uuid'',true),(''revora_skill_pk_round_rewards'',''match'',''bigint'',true),(''revora_skill_pk_round_rewards'',''round'',''integer'',true),(''revora_skill_pk_round_rewards'',''uid'',''bigint'',true),(''revora_skill_pk_round_rewards'',''item_uid'',''bigint'',false)) e(tab,name,typ,required) JOIN pg_class c ON c.relname=e.tab JOIN pg_namespace n ON n.oid=c.relnamespace AND n.nspname=''public'' JOIN pg_attribute a ON a.attrelid=c.oid AND a.attname=e.name AND NOT a.attisdropped;"'
                if ($skillRoundSchema -ne 't') { throw 'Skill round outcome/reward columns are incompatible.' }


                Write-Output ('Roadmap startup schema passed in game/database cycle ' + $cycle + ': 33 owned tables retained; arsenal deadline, Boost, Arena rune, guild purse/capture/calendar and Skill round reward columns verified. Schema evidence only.')
            }
            if ($CheckLocalTeamPkRecovery) {
                if ($cycle -eq 1) {
                    # Fixed private storage fixtures: no accounts, characters, items or playable rewards.
                    $empty = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_local_team_pk_runs WHERE run IN(''a6a56380-219f-4678-99c9-000000000085'',''a6a56380-219f-4678-99c9-000000000086'');"'
                    if ($empty -ne '0') { throw 'Local Team PK fixtures already exist; inspect before retrying.' }
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; INSERT INTO revora_local_team_pk_runs(run,kind,bracket,started_at,ended_at,state) VALUES(''a6a56380-219f-4678-99c9-000000000085'',0,3,now()-interval ''1 hour'',NULL,''active''),(''a6a56380-219f-4678-99c9-000000000086'',0,3,now()-interval ''2 hours'',now()-interval ''1 hour'',''cancelled''); INSERT INTO revora_local_team_pk_admissions(run,uid,pkmode,returned,entry_earned,entry_item) VALUES(''a6a56380-219f-4678-99c9-000000000085'',4294900091,1,false,true,NULL),(''a6a56380-219f-4678-99c9-000000000086'',4294900092,3,true,true,4294900094),(''a6a56380-219f-4678-99c9-000000000086'',4294900093,1,true,true,NULL); COMMIT;"' | Out-Null
                    $localEntryBaseline = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(a)::text,'','' ORDER BY uid)) FROM revora_local_team_pk_admissions a WHERE run IN(''a6a56380-219f-4678-99c9-000000000085'',''a6a56380-219f-4678-99c9-000000000086'');"'
                    if ($localEntryBaseline -notmatch '^[0-9a-f]{32}$') { throw 'Invalid local Team PK recovery fingerprint.' }
                } else {
                    $localEntryAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(a)::text,'','' ORDER BY uid)) FROM revora_local_team_pk_admissions a WHERE run IN(''a6a56380-219f-4678-99c9-000000000085'',''a6a56380-219f-4678-99c9-000000000086'');"'
                    $localRuns = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=2 AND bool_and(state=''cancelled'' AND ended_at IS NOT NULL AND cardinality(leaders)=0) FROM revora_local_team_pk_runs WHERE run IN(''a6a56380-219f-4678-99c9-000000000085'',''a6a56380-219f-4678-99c9-000000000086'');"'
                    if ($localEntryAfter -ne $localEntryBaseline -or $localRuns -ne 't') { throw 'Local Team PK startup lost pending/paid admission state or invented results.' }
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; DELETE FROM revora_local_team_pk_admissions WHERE run IN(''a6a56380-219f-4678-99c9-000000000085'',''a6a56380-219f-4678-99c9-000000000086''); DELETE FROM revora_local_team_pk_runs WHERE run IN(''a6a56380-219f-4678-99c9-000000000085'',''a6a56380-219f-4678-99c9-000000000086''); COMMIT;"' | Out-Null
                    $left = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_local_team_pk_runs WHERE run IN(''a6a56380-219f-4678-99c9-000000000085'',''a6a56380-219f-4678-99c9-000000000086'');"'
                    if ($left -ne '0') { throw 'Local Team PK fixture cleanup failed.' }
                    Write-Output 'Local Team PK actual startup cancelled the interrupted run and retained exact offline, deferred and paid entry receipts. Storage fixtures removed; this is not an in-flight crash or earned-player test.'
                }
            }
            if ($CheckClanPurseRecovery) {
                $clanOldAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(p)::text,'','' ORDER BY uid)) FROM (SELECT run,uid,cp,rvp,paid_at FROM revora_clan_war_awards WHERE run=''a6a56380-219f-4678-99c9-000000000004'' AND uid IN(4294900071,4294900072)) p;"'
                $clanOldTerms = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=2 AND bool_and(soul_type=0 AND soul_uid IS NULL) FROM revora_clan_war_awards WHERE run=''a6a56380-219f-4678-99c9-000000000004'' AND uid IN(4294900071,4294900072);"'
                if ($clanOldAfter -ne $clanLegacyBaseline -or $clanOldTerms -ne 't') { throw 'Clan migration changed an old pending/paid currency-only purse.' }
                if ($cycle -eq 1) {
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "INSERT INTO revora_clan_war_awards(run,uid,cp,rvp,paid_at,soul_type,soul_uid) VALUES(''a6a56380-219f-4678-99c9-000000000004'',4294900073,100,20,NULL,800000,NULL),(''a6a56380-219f-4678-99c9-000000000004'',4294900074,100,20,now(),800000,4294900075);"' | Out-Null
                    $clanAllBaseline = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(p)::text,'','' ORDER BY uid)) FROM revora_clan_war_awards p WHERE run=''a6a56380-219f-4678-99c9-000000000004'';"'
                    if ($clanAllBaseline -notmatch '^[0-9a-f]{32}$') { throw 'Clan mixed-purse fingerprint is invalid.' }
                } else {
                    $clanAllAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(p)::text,'','' ORDER BY uid)) FROM revora_clan_war_awards p WHERE run=''a6a56380-219f-4678-99c9-000000000004'';"'
                    if ($clanAllAfter -ne $clanAllBaseline) { throw 'Clan pending/paid purse terms changed across the Host/database restart.' }
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; DELETE FROM revora_clan_war_awards WHERE run=''a6a56380-219f-4678-99c9-000000000004''; DELETE FROM revora_clan_wars WHERE run=''a6a56380-219f-4678-99c9-000000000004''; COMMIT;"' | Out-Null
                    $clanRemaining = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_clan_wars WHERE run=''a6a56380-219f-4678-99c9-000000000004'';"'
                    if ($clanRemaining -ne '0') { throw 'Clan purse fixture cleanup failed.' }
                    Write-Output 'Clan migration and real Host restart preserve old currency-only terms and exact synthetic pending/paid soul receipts. Fixtures removed; earned-player/native acceptance remains pending.'
                }
            }
            if ($CheckEliteGuildRecovery) {
                if ($cycle -eq 1) {
                    # Private storage receipts only: no accounts, characters or reward items.
                    $eliteExisting = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_elite_guild_battles WHERE id IN (''a6a56380-219f-4678-99c9-000000000005'',''a6a56380-219f-4678-99c9-000000000006'',''a6a56380-219f-4678-99c9-000000000007'',''a6a56380-219f-4678-99c9-000000000008'');"'
                    if ($eliteExisting -ne '0') { throw 'Elite Guild fixtures already exist; inspect before retrying.' }
                    $eliteInserted = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; INSERT INTO revora_elite_guild_battles(id,started_at,ended_at,state,keeper,captures,gold,rvp,soul_type,claimed_at,claimed_by,soul_uid,honor_until) VALUES(''a6a56380-219f-4678-99c9-000000000005'',now()-interval ''2 hours'',NULL,''active'',4294900080,1,0,0,0,NULL,NULL,NULL,NULL),(''a6a56380-219f-4678-99c9-000000000006'',now()-interval ''2 hours'',now()-interval ''1 hour'',''won'',4294900080,1,500000000000,200000,800000,NULL,NULL,NULL,NULL),(''a6a56380-219f-4678-99c9-000000000007'',now()-interval ''2 hours'',now()-interval ''1 hour'',''won'',4294900080,1,500000000000,200000,800000,now(),4294900081,4294900082,now()+interval ''1 day''),(''a6a56380-219f-4678-99c9-000000000008'',now()-interval ''2 hours'',now()-interval ''1 hour'',''empty'',0,0,0,0,0,NULL,NULL,NULL,NULL); INSERT INTO revora_elite_guild_captures(battle,number,guild_id,damage,captured_at) VALUES(''a6a56380-219f-4678-99c9-000000000005'',1,4294900080,1000,now()-interval ''30 minutes''); COMMIT;"'
                    $eliteBaseline = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(p)::text,'','' ORDER BY id)) FROM revora_elite_guild_battles p WHERE id IN (''a6a56380-219f-4678-99c9-000000000006'',''a6a56380-219f-4678-99c9-000000000007'',''a6a56380-219f-4678-99c9-000000000008'');"'
                    if ($eliteBaseline -notmatch '^[0-9a-f]{32}$') { throw 'Elite Guild receipt fingerprint is invalid.' }
                    Write-Output 'Private Elite Guild active, pending, paid and empty fixtures staged after first Host exit.'
                } else {
                    $eliteRecovered = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT (SELECT count(*) FROM revora_elite_guild_battles WHERE id=''a6a56380-219f-4678-99c9-000000000005'' AND state=''cancelled'' AND ended_at IS NOT NULL AND captures=1 AND keeper=4294900080 AND gold=0 AND rvp=0 AND soul_type=0 AND claimed_at IS NULL)=1 AND (SELECT count(*) FROM revora_elite_guild_captures WHERE battle=''a6a56380-219f-4678-99c9-000000000005'' AND number=1 AND guild_id=4294900080 AND damage=1000)=1;"'
                    $eliteAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(p)::text,'','' ORDER BY id)) FROM revora_elite_guild_battles p WHERE id IN (''a6a56380-219f-4678-99c9-000000000006'',''a6a56380-219f-4678-99c9-000000000007'',''a6a56380-219f-4678-99c9-000000000008'');"'
                    if ($eliteRecovered -ne 't' -or $eliteAfter -ne $eliteBaseline) { throw 'Elite Guild startup changed saved prizes or failed to cancel the interrupted battle.' }
                    $eliteCleaned = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; DELETE FROM revora_elite_guild_captures WHERE battle IN (''a6a56380-219f-4678-99c9-000000000005'',''a6a56380-219f-4678-99c9-000000000006'',''a6a56380-219f-4678-99c9-000000000007'',''a6a56380-219f-4678-99c9-000000000008''); DELETE FROM revora_elite_guild_battles WHERE id IN (''a6a56380-219f-4678-99c9-000000000005'',''a6a56380-219f-4678-99c9-000000000006'',''a6a56380-219f-4678-99c9-000000000007'',''a6a56380-219f-4678-99c9-000000000008''); COMMIT;"'
                    $eliteRemaining = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT (SELECT count(*) FROM revora_elite_guild_battles WHERE id IN (''a6a56380-219f-4678-99c9-000000000005'',''a6a56380-219f-4678-99c9-000000000006'',''a6a56380-219f-4678-99c9-000000000007'',''a6a56380-219f-4678-99c9-000000000008''))+(SELECT count(*) FROM revora_elite_guild_captures WHERE battle IN (''a6a56380-219f-4678-99c9-000000000005'',''a6a56380-219f-4678-99c9-000000000006'',''a6a56380-219f-4678-99c9-000000000007'',''a6a56380-219f-4678-99c9-000000000008''));"'
                    if ($eliteRemaining -ne '0') { throw 'Elite Guild fixture cleanup failed.' }
                    Write-Output 'Elite Guild real Host restart cancelled the interrupted battle, retained its capture and preserved exact pending/paid/empty results. Fixtures removed; earned-player/native acceptance remains pending.'
                }
            }
            if ($CheckSuperGuildRecovery) {
                if ($cycle -eq 1) {
                    # Private storage-only fixtures. No character, account or actual reward item is created.
                    $empty = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_super_guild_battles WHERE id IN (''a6a56380-219f-4678-99c9-000000000001'',''a6a56380-219f-4678-99c9-000000000002'',''a6a56380-219f-4678-99c9-000000000003'');"'
                    if ($empty -ne '0') { throw 'Super Guild recovery fixture already exists; inspect before rerunning.' }
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; INSERT INTO revora_super_guild_battles(id,started_at,ended_at,state,keeper,captures,purses_frozen) VALUES(''a6a56380-219f-4678-99c9-000000000001'',now()-interval ''1 hour'',NULL,''active'',4294900050,1,false),(''a6a56380-219f-4678-99c9-000000000002'',now()-interval ''2 hours'',now()-interval ''1 hour'',''won'',4294900050,1,true),(''a6a56380-219f-4678-99c9-000000000003'',now()-interval ''2 hours'',now()-interval ''1 hour'',''empty'',0,0,false); INSERT INTO revora_super_guild_captures(battle,number,guild_id,damage,captured_at) VALUES(''a6a56380-219f-4678-99c9-000000000001'',1,4294900050,1000,now()-interval ''30 minutes''); INSERT INTO revora_super_guild_purses(battle,uid,guild_id,rank,cp,rvp,gold,tokens,honor,earned_at,honor_until,claimed_at,rewards,soul_type) VALUES(''a6a56380-219f-4678-99c9-000000000002'',4294900051,4294900050,200,100,20,300,4,32,now()-interval ''1 hour'',now()+interval ''6 days'',NULL,NULL,800000),(''a6a56380-219f-4678-99c9-000000000002'',4294900052,4294900050,200,100,20,300,4,32,now()-interval ''1 hour'',now()+interval ''6 days'',now(),ARRAY[4294900060::bigint],800000); COMMIT;"' | Out-Null
                    $superPurseBaseline = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(p)::text,'','' ORDER BY uid)) FROM revora_super_guild_purses p WHERE battle=''a6a56380-219f-4678-99c9-000000000002'';"'
                    if ($superPurseBaseline -notmatch '^[0-9a-f]{32}$') { throw 'Super Guild fixture fingerprint is invalid.' }
                    Write-Output 'Private Super Guild recovery fixtures staged after first Host exit.'
                } else {
                    $superRecovery = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT (SELECT count(*) FROM revora_super_guild_battles WHERE id=''a6a56380-219f-4678-99c9-000000000001'' AND state=''cancelled'' AND ended_at IS NOT NULL AND purses_frozen AND captures=1)=1 AND (SELECT count(*) FROM revora_super_guild_captures WHERE battle=''a6a56380-219f-4678-99c9-000000000001'' AND number=1 AND damage=1000 AND guild_id=4294900050)=1 AND (SELECT count(*) FROM revora_super_guild_battles WHERE id=''a6a56380-219f-4678-99c9-000000000002'' AND state=''won'' AND purses_frozen)=1 AND (SELECT count(*) FROM revora_super_guild_battles WHERE id=''a6a56380-219f-4678-99c9-000000000003'' AND state=''empty'' AND purses_frozen)=1 AND (SELECT count(*) FROM revora_super_guild_purses WHERE battle IN (''a6a56380-219f-4678-99c9-000000000001'',''a6a56380-219f-4678-99c9-000000000002'',''a6a56380-219f-4678-99c9-000000000003''))=2;"'
                    $superPurseAfter = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT md5(string_agg(row_to_json(p)::text,'','' ORDER BY uid)) FROM revora_super_guild_purses p WHERE battle=''a6a56380-219f-4678-99c9-000000000002'';"'
                    if ($superRecovery -ne 't' -or $superPurseAfter -ne $superPurseBaseline) { throw 'Host did not cancel the interrupted battle while preserving pending/paid purses.' }
                    Write-Output 'Real Host restart preserved exact synthetic pending/paid purses, cancelled the interrupted battle, retained its capture, and sealed old results without retroactive grants. Earned-player/native acceptance remains pending.'
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN; DELETE FROM revora_super_guild_purses WHERE battle IN (''a6a56380-219f-4678-99c9-000000000001'',''a6a56380-219f-4678-99c9-000000000002'',''a6a56380-219f-4678-99c9-000000000003''); DELETE FROM revora_super_guild_captures WHERE battle IN (''a6a56380-219f-4678-99c9-000000000001'',''a6a56380-219f-4678-99c9-000000000002'',''a6a56380-219f-4678-99c9-000000000003''); DELETE FROM revora_super_guild_battles WHERE id IN (''a6a56380-219f-4678-99c9-000000000001'',''a6a56380-219f-4678-99c9-000000000002'',''a6a56380-219f-4678-99c9-000000000003''); COMMIT;"' | Out-Null
                    $remaining = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_super_guild_battles WHERE id IN (''a6a56380-219f-4678-99c9-000000000001'',''a6a56380-219f-4678-99c9-000000000002'',''a6a56380-219f-4678-99c9-000000000003'');"'
                    if ($remaining -ne '0') { throw 'Super Guild recovery fixture cleanup failed.' }
                }
            }
            if ($CheckQuarantineRecovery) {
                if ($cycle -eq 1) {
                    $empty = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_quarantine_rounds WHERE id IN(''00000000-0000-0000-0000-000000000081'',''00000000-0000-0000-0000-000000000082'');"'
                    if ($empty -ne '0') { throw 'Team Tournament restart probe already exists; inspect before rerunning.' }
                    # Synthetic rounds/receipts only; no player, currency or item is fabricated.
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN;INSERT INTO revora_quarantine_rounds(id,started_at,state,white_score,black_score) VALUES(''00000000-0000-0000-0000-000000000081'',clock_timestamp(),''active'',20,10);INSERT INTO revora_quarantine_admissions(round_id,uid,white_team,pkmode,uniform_uid,original_garment,entered) VALUES(''00000000-0000-0000-0000-000000000081'',4294900081,true,1,4294900081,0,true),(''00000000-0000-0000-0000-000000000081'',4294900083,false,1,4294900083,0,true);INSERT INTO revora_quarantine_hits(round_id,sequence,attacker,target,spell,white_team,occurred_at) VALUES(''00000000-0000-0000-0000-000000000081'',1,4294900081,4294900083,1045,true,clock_timestamp());INSERT INTO revora_quarantine_rounds(id,started_at,ended_at,state,white_score,black_score,winners,purses_frozen) VALUES(''00000000-0000-0000-0000-000000000082'',clock_timestamp()-interval ''1 hour'',clock_timestamp(),''won'',100,90,ARRAY[4294900082::bigint],true);INSERT INTO revora_quarantine_purses(round_id,uid,cp,rvp,gold,soul_type,earned_at) VALUES(''00000000-0000-0000-0000-000000000082'',4294900082,300000000,50000,100000000000,800725,clock_timestamp());COMMIT;"' | Out-Null
                    & (Join-Path $PSScriptRoot 'Check-CandidateGameStartup.ps1')
                }
                $result = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT (SELECT count(*)=1 FROM revora_quarantine_rounds WHERE id=''00000000-0000-0000-0000-000000000081'' AND state=''cancelled'' AND ended_at>=started_at AND white_score=20 AND black_score=10 AND cardinality(winners)=0) AND (SELECT count(*)=1 FROM revora_quarantine_admissions WHERE round_id=''00000000-0000-0000-0000-000000000081'' AND uid=4294900081 AND uniform_uid=4294900081 AND original_garment=0 AND white_team AND pkmode=1 AND entered AND ended AND NOT returned) AND (SELECT count(*)=1 FROM revora_quarantine_hits WHERE round_id=''00000000-0000-0000-0000-000000000081'' AND sequence=1 AND attacker=4294900081 AND target=4294900083 AND spell=1045 AND white_team) AND NOT EXISTS(SELECT 1 FROM revora_quarantine_purses WHERE round_id=''00000000-0000-0000-0000-000000000081'') AND (SELECT count(*)=1 FROM revora_quarantine_rounds WHERE id=''00000000-0000-0000-0000-000000000082'' AND state=''won'' AND white_score=100 AND black_score=90 AND winners=ARRAY[4294900082::bigint] AND purses_frozen) AND (SELECT count(*)=1 FROM revora_quarantine_purses WHERE round_id=''00000000-0000-0000-0000-000000000082'' AND uid=4294900082 AND cp=300000000 AND rvp=50000 AND gold=100000000000 AND soul_type=800725 AND claimed_at IS NULL AND soul_uid IS NULL);"'
                if ($result -ne 't') { throw 'Team Tournament actual startup recovery changed a pending return or frozen purse.' }
                Write-Output ('Team Tournament actual game/database recovery passed cycle ' + $cycle + '; synthetic reservations, no ordinary earning claim.')
                if ($cycle -eq 2) {
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "BEGIN;DELETE FROM revora_quarantine_hits WHERE round_id=''00000000-0000-0000-0000-000000000081'';DELETE FROM revora_quarantine_admissions WHERE round_id=''00000000-0000-0000-0000-000000000081'';DELETE FROM revora_quarantine_purses WHERE round_id=''00000000-0000-0000-0000-000000000082'';DELETE FROM revora_quarantine_rounds WHERE id IN(''00000000-0000-0000-0000-000000000081'',''00000000-0000-0000-0000-000000000082'');COMMIT;"' | Out-Null
                }
            }
            if ($CheckLmsRecovery) {
                if ($cycle -eq 1) {
                    $empty = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_lms_admissions WHERE cycle=''probe-lms-restart'';"'
                    if ($empty -ne '0') { throw 'LMS restart probe already exists; inspect it before rerunning.' }
                    # Synthetic reservations only: no account, character, reward or live data is copied.
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "INSERT INTO revora_lms_admissions(cycle,uid,arena,pkmode) VALUES(''probe-lms-restart'',4294900001,700,1),(''probe-lms-restart'',4294900002,700,3);"' | Out-Null
                    & (Join-Path $PSScriptRoot 'Check-CandidateGameStartup.ps1')
                }
                $result = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=2 AND bool_and(ended AND NOT returned) AND NOT EXISTS(SELECT 1 FROM revora_lms_victories WHERE cycle=''probe-lms-restart'') FROM revora_lms_admissions WHERE cycle=''probe-lms-restart'';"'
                if ($result -ne 't') { throw 'Actual game startup did not preserve pending returns without a fabricated winner.' }
                Write-Output ('LMS reservation recovery passed in game/database process cycle ' + $cycle + '. Native player return remains untested here.')
                if ($cycle -eq 2) {
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "DELETE FROM revora_lms_admissions WHERE cycle=''probe-lms-restart'' AND uid IN (4294900001,4294900002);"' | Out-Null
                }
            }
            if ($CheckRelicRecovery) {
                if ($cycle -eq 1) {
                    $empty = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_relic_admissions WHERE cycle=''probe-relic-restart'';"'
                    if ($empty -ne '0') { throw 'Relic Rush restart probe already exists; inspect it before rerunning.' }
                    # Synthetic reservations only: no account, character, reward or live data is copied.
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "INSERT INTO revora_relic_admissions(cycle,uid,arena,pkmode) VALUES(''probe-relic-restart'',4294900001,700,1),(''probe-relic-restart'',4294900002,700,3);"' | Out-Null
                    & (Join-Path $PSScriptRoot 'Check-CandidateGameStartup.ps1')
                }
                $result = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=2 AND bool_and(ended AND NOT returned) AND NOT EXISTS(SELECT 1 FROM revora_relic_placements WHERE cycle=''probe-relic-restart'') FROM revora_relic_admissions WHERE cycle=''probe-relic-restart'';"'
                if ($result -ne 't') { throw 'Actual game startup did not preserve pending returns without a fabricated winner.' }
                Write-Output ('Relic Rush reservation recovery passed in game/database process cycle ' + $cycle + '. Native player return remains untested here.')
                if ($cycle -eq 2) {
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "DELETE FROM revora_relic_admissions WHERE cycle=''probe-relic-restart'' AND uid IN (4294900001,4294900002);"' | Out-Null
                }
            }
            if ($CheckWaveRecovery) {
                if ($cycle -eq 1) {
                    $empty = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*) FROM revora_wave_admissions WHERE cycle=''00000000000000000000000000000071'';"'
                    if ($empty -ne '0') { throw 'Wave Defense restart probe already exists; inspect it before rerunning.' }
                    # Synthetic reservations only: no account, character, reward or live data is copied.
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "INSERT INTO revora_wave_admissions(cycle,uid,arena,pkmode) VALUES(''00000000000000000000000000000071'',4294900001,700,1),(''00000000000000000000000000000071'',4294900002,700,3);"' | Out-Null
                    & (Join-Path $PSScriptRoot 'Check-CandidateGameStartup.ps1')
                }
                $result = Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "SELECT count(*)=2 AND bool_and(ended AND NOT returned) AND NOT EXISTS(SELECT 1 FROM revora_wave_results WHERE run=''00000000-0000-0000-0000-000000000071''::uuid) FROM revora_wave_admissions WHERE cycle=''00000000000000000000000000000071'';"'
                if ($result -ne 't') { throw 'Actual game startup did not preserve pending returns without a fabricated winner.' }
                Write-Output ('Wave Defense reservation recovery passed in game/database process cycle ' + $cycle + '. Native player return remains untested here.')
                if ($cycle -eq 2) {
                    Invoke-CandidateTool 'psql.exe' '-X -w -A -t -v ON_ERROR_STOP=1 -d revora_candidate_game -c "DELETE FROM revora_wave_admissions WHERE cycle=''00000000000000000000000000000071'' AND uid IN (4294900001,4294900002);"' | Out-Null
                }
            }
        } finally {
            if (Test-Path -LiteralPath (Join-Path $data 'postmaster.pid')) {
                Invoke-CandidateControl 'stop'
            }
        }
    }
    if (Test-Path -LiteralPath (Join-Path $data 'postmaster.pid')) { throw 'Candidate PID file remains after shutdown.' }
    if (@(Get-NetTCPConnection -State Listen -LocalPort 25439 -ErrorAction SilentlyContinue).Count) {
        throw 'Candidate port remains occupied after shutdown.'
    }
    Write-Output ('Passed ' + $cycles + ' candidate start/authentication/identity/stop cycles; database remains stopped.')
    if ($RestoreCatalogs -or $VerifyCatalogs) {
        Write-Output 'Verified 100 base game tables, candidate ownership, base world catalogs and expected account/character state before and after restart.'
    }
} finally {
    $credential = $null
    $secure.Dispose()
}
