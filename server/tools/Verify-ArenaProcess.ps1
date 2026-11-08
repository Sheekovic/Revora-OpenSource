param([switch]$SoloTwentyOnly,[switch]$TeamDailyOnly,[switch]$CollectionOnly,[switch]$SignInOnly,[switch]$EquipmentJourneyOnly,[switch]$TeamWardrobeOnly,[switch]$LocalWardrobeOnly,[switch]$EquipmentAggregateOnly,[switch]$MrMsExtendedOnly,[switch]$SoulInspectionOnly,[switch]$AdventureAliasOnly,[switch]$SummerCleanupOnly,[switch]$SnakeOnly,[switch]$SkyOnly,[switch]$PerfectionWireOnly,[switch]$RegularTeamCrashOnly,[switch]$RegularTeamWireOnly,[switch]$EliminationWireOnly,[switch]$DailySoulsOnly,[switch]$TaoistWireOnly,[switch]$HealthPersistenceOnly,[switch]$PirateFlowerWireOnly,[switch]$PirateFullWireOnly,[switch]$PirateWireOnly,[switch]$ResumePirateTrainingOwnedFixture,[switch]$ClanWireOnly,[switch]$ClanContestOnly,[switch]$TeamWireCrashOnly,[switch]$ResumeTeamWireOwnedFixture,[switch]$TeamWireOnly,[switch]$AnniversaryOnly,[switch]$ClanWarOnly,[switch]$LocalCtfOnly,[switch]$FlowerContestOnly,[switch]$GuildBeastOnly,[switch]$EliteGuildOnly,[switch]$SuperGuildOnly,[switch]$PirateStoreOnly,[switch]$TaoistInputsOnly,[switch]$TaoistJournalOnly,[switch]$TaoistEdgesOnly,[switch]$TaoistTrainingOnly,[switch]$TaoistRouteOnly,[switch]$TaoistStoreOnly,[switch]$MasteryOnly,[switch]$GuildWarOnly,[switch]$MonkOnly,[switch]$NinjaOnly,[switch]$WarriorOnly,[switch]$TrojanOnly,[switch]$MountPacksOnly,[switch]$ResetOwnedFixture,[switch]$RecoverOwnedFixture,[switch]$ResumeSpendOwnedFixture,[switch]$MrMs2016Only,[switch]$EliminationOnly,[switch]$TempestOnly,[switch]$HeartOnly,[switch]$PlanetOnly,[switch]$FashionOnly,[switch]$TowerOnly,[switch]$FanOnly)
if($SoloTwentyOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('SoloTwentyOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($TeamDailyOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('TeamDailyOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($CollectionOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('CollectionOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($SignInOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('SignInOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($EquipmentJourneyOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('EquipmentJourneyOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($TeamWardrobeOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('TeamWardrobeOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($LocalWardrobeOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('LocalWardrobeOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($EquipmentAggregateOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('EquipmentAggregateOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($MrMsExtendedOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('MrMsExtendedOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($AdventureAliasOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('AdventureAliasOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($SoulInspectionOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('SoulInspectionOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($SummerCleanupOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('SummerCleanupOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($SnakeOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('SnakeOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($SkyOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('SkyOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($PerfectionWireOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('PerfectionWireOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($TaoistWireOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('TaoistWireOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($HealthPersistenceOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('HealthPersistenceOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($PirateFlowerWireOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('PirateFlowerWireOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($PirateFullWireOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('PirateFullWireOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($PirateWireOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('PirateWireOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($ResetOwnedFixture -and ($RecoverOwnedFixture -or $ResumeSpendOwnedFixture)){throw 'Choose reset or recovery, not both.'}
if(($MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly) -and ($RecoverOwnedFixture -or $ResumeSpendOwnedFixture)){throw 'Choose the Mr/Ms suite or Arena recovery, not both.'}
if(($MrMs2016Only -and $EliminationOnly) -or ($TempestOnly -and ($MrMs2016Only -or $EliminationOnly))){throw 'Select one event suite.'}
if($HeartOnly -and ($MrMs2016Only -or $EliminationOnly -or $TempestOnly)){throw 'Select one event suite.'}
if($PlanetOnly -and ($MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly)){throw 'Select one event suite.'}
if($FashionOnly -and ($MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly)){throw 'Select one event suite.'}
if($TowerOnly -and ($MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly)){throw 'Select one event suite.'}
if($FanOnly -and ($MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly)){throw 'Select one event suite.'}
if($MountPacksOnly -and ($RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($TrojanOnly -and ($MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($WarriorOnly -and ($TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($NinjaOnly -and ($WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($MonkOnly -and ($NinjaOnly -or $WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($GuildWarOnly -and ($MonkOnly -or $NinjaOnly -or $WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($MasteryOnly -and ($GuildWarOnly -or $MonkOnly -or $NinjaOnly -or $WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($TaoistStoreOnly -and ($MasteryOnly -or $GuildWarOnly -or $MonkOnly -or $NinjaOnly -or $WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($TaoistRouteOnly -and ($TaoistStoreOnly -or $MasteryOnly -or $GuildWarOnly -or $MonkOnly -or $NinjaOnly -or $WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($TaoistTrainingOnly -and ($TaoistRouteOnly -or $TaoistStoreOnly -or $MasteryOnly -or $GuildWarOnly -or $MonkOnly -or $NinjaOnly -or $WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($TaoistEdgesOnly -and ($TaoistTrainingOnly -or $TaoistRouteOnly -or $TaoistStoreOnly -or $MasteryOnly -or $GuildWarOnly -or $MonkOnly -or $NinjaOnly -or $WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($TaoistJournalOnly -and ($TaoistEdgesOnly -or $TaoistTrainingOnly -or $TaoistRouteOnly -or $TaoistStoreOnly -or $MasteryOnly -or $GuildWarOnly -or $MonkOnly -or $NinjaOnly -or $WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($TaoistInputsOnly -and ($TaoistJournalOnly -or $TaoistEdgesOnly -or $TaoistTrainingOnly -or $TaoistRouteOnly -or $TaoistStoreOnly -or $MasteryOnly -or $GuildWarOnly -or $MonkOnly -or $NinjaOnly -or $WarriorOnly -or $TrojanOnly -or $MountPacksOnly -or $RecoverOwnedFixture -or $ResumeSpendOwnedFixture -or $MrMs2016Only -or $EliminationOnly -or $TempestOnly -or $HeartOnly -or $PlanetOnly -or $FashionOnly -or $TowerOnly -or $FanOnly)){throw 'Select one event suite.'}
if($PirateStoreOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('PirateStoreOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($SuperGuildOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('SuperGuildOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($EliteGuildOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('EliteGuildOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($GuildBeastOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('GuildBeastOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($FlowerContestOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('FlowerContestOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($LocalCtfOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('LocalCtfOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($ClanWarOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('ClanWarOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($AnniversaryOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('AnniversaryOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($TeamWireOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('TeamWireOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($ResumeTeamWireOwnedFixture -and $PSBoundParameters.Count -ne 1){throw 'Select only the retained team wire fixture.'}
if($TeamWireCrashOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('TeamWireCrashOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($ClanContestOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('ClanContestOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($ClanWireOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('ClanWireOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
$ErrorActionPreference='Stop'
if($RegularTeamCrashOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('RegularTeamCrashOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($RegularTeamWireOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('RegularTeamWireOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($EliminationWireOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('EliminationWireOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
if($DailySoulsOnly -and @($PSBoundParameters.Keys | Where-Object { $_ -notin @('DailySoulsOnly','ResetOwnedFixture') }).Count){throw 'Select one event suite.'}
$repoRoot=Split-Path $PSScriptRoot -Parent
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

function Invoke-ProbeSql([string]$Database,[string]$Sql) {
    if($Database -notin @('postgres','revora_candidate_arena_process')){throw 'Unexpected probe database.'}
    $start=New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $toolRoot 'psql.exe'
    $start.Arguments='-X -w -q -A -t -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 25439 -U revora_candidate_admin -d '+$Database
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.EnvironmentVariables['PGPASSWORD']=$credential.GetNetworkCredential().Password
    $child=New-Object System.Diagnostics.Process;$child.StartInfo=$start
    try {
        if(!$child.Start()){throw 'Probe SQL did not start.'}
        $start.EnvironmentVariables.Remove('PGPASSWORD')
        $out=$child.StandardOutput.ReadToEndAsync();$err=$child.StandardError.ReadToEndAsync()
        $child.StandardInput.WriteLine($Sql);$child.StandardInput.Close()
        if(!$child.WaitForExit(30000)){$child.Kill();$child.WaitForExit();throw 'Probe SQL timeout.'}
        $text=$out.GetAwaiter().GetResult();$errorText=$err.GetAwaiter().GetResult()
        if($child.ExitCode -ne 0){throw ('Probe SQL failed: '+$errorText.Replace($credential.GetNetworkCredential().Password,'[REDACTED]'))}
        return $text.Trim()
    } finally {$start.EnvironmentVariables.Remove('PGPASSWORD');$child.Dispose()}
}
function Remove-OwnedProbe {
    $mark=Invoke-ProbeSql 'postgres' "SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname='revora_candidate_arena_process';"
    if($mark -ne 'revora-owned-arena-process-fixture-v1'){throw 'Refusing to remove database without the exact probe ownership marker.'}
    Invoke-ProbeSql 'postgres' 'DROP DATABASE revora_candidate_arena_process;' | Out-Null
}
try {
    Invoke-CandidateControl 'start'
    Invoke-CandidateProbe $false
    $exists=Invoke-ProbeSql 'postgres' "SELECT count(*) FROM pg_database WHERE datname='revora_candidate_arena_process';"
    if($ResumePirateTrainingOwnedFixture) {
        if($PSBoundParameters.Count -ne 1){throw 'Select only the retained Pirate training fixture.'}
        $mark=Invoke-ProbeSql 'postgres' "SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname='revora_candidate_arena_process';"
        if($exists -ne '1' -or $mark -ne 'revora-owned-arena-process-fixture-v1'){throw 'Expected the marked retained probe.'}
        $ready=Invoke-ProbeSql 'revora_candidate_arena_process' 'SELECT count(*)=1 FROM revora_pirate_probe;'
        if($ready -ne 't'){throw 'Expected the earned Pirate conversion snapshot.'}
        & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-train
        Invoke-CandidateControl 'stop'
        Invoke-CandidateControl 'start'
        & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-train-reload
        Remove-OwnedProbe
        Write-Output 'Retained Pirate combat training passed.'
        return
    }
    if($ResumeTeamWireOwnedFixture) {
        $mark=Invoke-ProbeSql 'postgres' "SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname='revora_candidate_arena_process';"
        if($exists -ne '1' -or $mark -ne 'revora-owned-arena-process-fixture-v1'){throw 'Expected the marked retained probe.'}
        $ready=Invoke-ProbeSql 'revora_candidate_arena_process' 'SELECT count(*)=16 FROM revora_team_wire_probe;'
        if($ready -ne 't'){throw 'Expected sixteen earned team reward snapshots.'}
        & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-reload
        Remove-OwnedProbe
        Write-Output 'Retained TCP-earned team rewards survived restart and authenticated duplicate claims.'
        return
    }
    if($RecoverOwnedFixture -or $ResumeSpendOwnedFixture) {
        $mark=Invoke-ProbeSql 'postgres' "SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname='revora_candidate_arena_process';"
        if($exists -ne '1' -or $mark -ne 'revora-owned-arena-process-fixture-v1'){throw 'Expected the marked retained probe.'}
        $ready=Invoke-ProbeSql 'revora_candidate_arena_process' "SELECT count(*)=1 AND NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgname='revora_arena_probe_suppress') FROM revora_arena_process_probe;"
        if($ready -ne 't'){throw 'Probe must have completed capture and trigger removal before recovery.'}
    } else {
        if($exists -ne '0') {
            if(!$ResetOwnedFixture){throw 'Arena probe database already exists; inspect the owned failed fixture before reset.'}
            Remove-OwnedProbe
        }
        Invoke-ProbeSql 'postgres' "CREATE DATABASE revora_candidate_arena_process TEMPLATE revora_candidate_game OWNER revora_candidate_game; COMMENT ON DATABASE revora_candidate_arena_process IS 'revora-owned-arena-process-fixture-v1';" | Out-Null
        if($FanOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase tower-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase tower-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase fan-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase fan-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase fan-finish
            Remove-OwnedProbe
            Write-Output 'Earned Tower-to-Fan journey and paid-entry process recovery passed.'
            return
        }
        if($TowerOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase tower-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase tower-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase tower-finish
            Remove-OwnedProbe
            Write-Output 'Tower earned route and process recovery passed.'
            return
        }
        if($TaoistInputsOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-inputs
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-inputs-reload
            Remove-OwnedProbe
            Write-Output 'Taoist legacy exchange interception, item framing and learning eligibility passed.'
            return
        }
        if($TaoistJournalOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-journal
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-journal-reload
            Remove-OwnedProbe
            Write-Output 'Taoist receipt-derived journal resists forged state and survives reward consumption/restart.'
            return
        }
        if($TaoistEdgesOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-edges
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-edges-reload
            Remove-OwnedProbe
            Write-Output 'Taoist catalog, capacity, stale/concurrent offers and Fish-to-Hossu restart checks passed.'
            return
        }
        if($TaoistTrainingOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-training
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-training-reload
            Remove-OwnedProbe
            Write-Output 'Private Taoist Fish exchanges and atomic scroll learning survived restart.'
            return
        }
        if($TaoistRouteOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-hunt
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-awaken
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-earned-reload
            Remove-OwnedProbe
            Write-Output 'Private Taoist hunted flowers, NPC trials and interrupted awakening recovery passed.'
            return
        }
        if($EliminationWireOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elimination-wire
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elimination-wire-reload
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elimination-wire-claimed
            Remove-OwnedProbe
            Write-Output 'Authenticated Elimination admission, combat, final claim and restart replay passed.'
            return
        }
        if($DailySoulsOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase daily-souls
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase daily-souls-reload
            Remove-OwnedProbe
            Write-Output 'Daily boss bound ground souls, duplicate pickup and restart persistence passed.'
            return
        }
        if($AdventureAliasOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase adventure-alias
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase adventure-alias-reload
            Remove-OwnedProbe
            Write-Output 'Four paid guild passages from Twin City and exact restart recovery passed.'
            return
        }
        if($SoloTwentyOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase solo-twenty-capture
            Invoke-ProbeSql 'revora_candidate_arena_process' 'DROP TRIGGER solo_twenty_suppress ON revora_arena_result_statistics; DROP FUNCTION solo_twenty_suppress();' | Out-Null
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase solo-twenty-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase solo-twenty-verify
            Remove-OwnedProbe
            Write-Output 'Solo twentieth result interruption recovers both participation prizes and preserves the earlier ninth-win prize.'
            return
        }
        if($TeamDailyOnly) {
            foreach($phase in @('team-daily-capture','team-daily-continue')) {
                & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase $phase
                Invoke-ProbeSql 'revora_candidate_arena_process' 'DROP TRIGGER team_daily_suppress ON revora_team_arena_results; DROP FUNCTION team_daily_suppress();' | Out-Null
                Invoke-CandidateControl 'stop'
                Invoke-CandidateControl 'start'
            }
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-daily-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-daily-verify
            Remove-OwnedProbe
            Write-Output 'Interrupted ninth-win and twentieth-match Team Arena results recover leader, member and opponent rewards exactly once.'
            return
        }
        if($CollectionOnly) {
            & (Join-Path $PSScriptRoot 'Stage-CandidateSignInCatalog.ps1')
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase planet-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            for($week=0;$week -lt 8;$week++) {
                & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase planet-week
            }
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase planet-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase collection-buy
            for($month=0;$month -lt 3;$month++) {
                & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase collection-month
            }
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase collection-verify
            Remove-OwnedProbe
            Write-Output 'One ordinary character earned and recovered 50 permanent mount designs through Planet, paid shops and sign-in packs.'
            return
        }
        if($SignInOnly) {
            & (Join-Path $PSScriptRoot 'Stage-CandidateSignInCatalog.ps1')
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase sign-earn
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase sign-reload
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase sign-verify
            Remove-OwnedProbe
            Write-Output 'Sign-in payment, earned Easter mount, failure rollback and monthly restart persistence passed.'
            return
        }
        if($EquipmentJourneyOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase equipment-early
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase equipment-early-reload
            Remove-OwnedProbe
            Write-Output 'Nineteen equipment chains earned by one ordinary character, with warehouse storage, exact restart recovery and one aggregate achievement.'
            return
        }
        if($EquipmentAggregateOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase equipment-aggregate
            Remove-OwnedProbe
            Write-Output 'Equipment aggregate receipt predicates, missing schemas and transaction rollback passed.'
            return
        }
        if($MrMsExtendedOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mrms-extended-register
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mrms-extended-verify
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mrms-extended-verify
            Remove-OwnedProbe
            Write-Output 'Mr/Ms places9-30 ordinary ballots, judging, rollback and equipped restart passed.'
            return
        }
        if($SoulInspectionOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase soul-inspection
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase soul-inspection-reload
            Remove-OwnedProbe
            Write-Output 'Release Souls inspection handoff, earning and exact restart recovery passed.'
            return
        }
        if($SummerCleanupOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase summer-cleanup
            Remove-OwnedProbe
            Write-Output 'Summer midnight acceptance cleanup passed.'
            return
        }
        if($SnakeOnly) {
            foreach($phase in @('snake-victory','snake-claim','snake-verify')) {
                & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase $phase
                Invoke-CandidateControl 'stop'
                Invoke-CandidateControl 'start'
            }
            Remove-OwnedProbe
            Write-Output 'Snake earned victory, next-day claim and exact reward restart passed.'
            return
        }
        if($SkyOnly) {
            foreach($phase in @('sky-prize','sky-claim','sky-verify')) {
                & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase $phase
                Invoke-CandidateControl 'stop'
                Invoke-CandidateControl 'start'
            }
            Remove-OwnedProbe
            Write-Output 'Sky earned prize, claim and exact reward restart passed.'
            return
        }
        if($PerfectionWireOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase perfection-wire-rollback
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase perfection-wire
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase perfection-wire-reload
            Remove-OwnedProbe
            Write-Output 'Authenticated Perfection Boost wallets, progress and durable cooldown passed across restart.'
            return
        }
        if($TaoistWireOnly) {
            foreach($phase in @('taoist-wire-water','taoist-wire-fire','taoist-wire-water-reload','taoist-wire-fire-reload')) {
                & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase $phase
                Invoke-CandidateControl 'stop'
                Invoke-CandidateControl 'start'
            }
            Remove-OwnedProbe
            Write-Output 'Authenticated Water/Fire Taoist trials, awakening, exchanges and class-specific scroll learning passed with restart replay checks.'
            return
        }
        if($HealthPersistenceOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase health-persistence
            Remove-OwnedProbe
            Write-Output 'Health persistence pending combat, concurrent medicine, rollback and stale writer checks passed.'
            return
        }
        if($PirateFlowerWireOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-wire-flowers
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-wire-flowers-reload
            Remove-OwnedProbe
            Write-Output 'Pirate authenticated flower hunt, pickup, offering and daily replay passed after restart.'
            return
        }
        if($PirateFullWireOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-wire-full
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-wire-full-reload
            Remove-OwnedProbe
            Write-Output 'Pirate authenticated chart-to-awakening and scroll journeys persisted after restart.'
            return
        }
        if($PirateWireOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-wire
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-wire-reload
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-wire-search-reload
            Remove-OwnedProbe
            Write-Output 'Pirate authenticated admission, chart purchase, travel and search passed with exact reward persistence and replay protection.'
            return
        }
        if($PirateStoreOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-store
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-reload
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-train
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase pirate-train-reload
            Remove-OwnedProbe
            Write-Output 'Normal-startup Pirate earned conversion and restart checks passed; authenticated/native acceptance remains pending.'
            return
        }
        if($TaoistStoreOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-store
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-reload
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-train
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase taoist-train-reload
            Remove-OwnedProbe
            Write-Output 'Taoist transaction, combat-earned mastery and exact proficiency/achievement restart checks passed.'
            return
        }
        if($MasteryOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mastery-awards
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mastery-reload
            Remove-OwnedProbe
            Write-Output 'All three rebirth paths passed rollback/retry and duplicate refusal; 102 mastery records and exact items/CPs/proficiencies survived restart.'
            return
        }
        if($GuildBeastOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase guild-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase guild-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase beast-hunt
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase beast-claim
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase beast-verify
            Remove-OwnedProbe
            Write-Output 'Earned Guild War funding, Guild Beast kill and post-midnight reward restart passed.'
            return
        }
        if($EliteGuildOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elite-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elite-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elite-verify
            Remove-OwnedProbe
            Write-Output 'Elite Guild scheduled earning, full-bag pending prize restart and exact reward persistence passed.'
            return
        }
        if($SuperGuildOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase super-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase super-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase super-verify
            Remove-OwnedProbe
            Write-Output 'Super Guild scheduled earning, pending purse restart and exact reward persistence passed.'
            return
        }
        if($GuildWarOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase guild-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase guild-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase guild-verify
            Remove-OwnedProbe
            Write-Output 'Guild War earned capture, unclaimed purse recovery and exact reward persistence passed.'
            return
        }
        if($MonkOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase monk-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase monk-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase monk-verify
            Remove-OwnedProbe
            Write-Output 'Monk Epic hunted flowers, saved retry Gem and converted weapon persistence passed.'
            return
        }
        if($NinjaOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase ninja-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase ninja-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase ninja-verify
            Remove-OwnedProbe
            Write-Output 'Ninja Epic paid trials, interrupted offer and converted weapon persistence passed.'
            return
        }
        if($WarriorOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase warrior-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase warrior-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase warrior-verify
            Remove-OwnedProbe
            Write-Output 'Warrior Epic earned entry, interrupted combat and paired reward persistence passed.'
            return
        }
        if($TrojanOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase trojan-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase trojan-finish
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase trojan-battle
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase trojan-verify
            Remove-OwnedProbe
            Write-Output 'Trojan Epic earned story, interrupted memory recovery and final saber persistence passed.'
            return
        }
        if($MountPacksOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mount-buy
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mount-open
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mount-verify
            Remove-OwnedProbe
            Write-Output 'Earned mount-pack purchase, atomic opening and process persistence passed.'
            return
        }
        if($TeamWireCrashOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-crash
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-recover
            Remove-OwnedProbe
            Write-Output 'Interrupted TCP combat recovered all sixteen players without losing earned rewards or awarding unfinished rounds.'
            return
        }
        if($RegularTeamCrashOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-regular-crash
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-regular-recover
            Remove-OwnedProbe
            Write-Output 'Interrupted regular Team PK recovered all sixteen players and earned entries without inventing final rewards.'
            return
        }
        if($TeamWardrobeOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-wardrobe
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-wardrobe-reload
            Remove-OwnedProbe
            Write-Output 'Local Team PK wardrobe rewards earned through authenticated combat and retained across restart.'
            return
        }
        if($RegularTeamWireOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-regular
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-regular-reload
            Remove-OwnedProbe
            Write-Output 'Regular Team PK authenticated combat, champion purse and sixteen-character persistence passed.'
            return
        }
        if($TeamWireOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team-wire-reload
            Remove-OwnedProbe
            Write-Output 'Sixteen authenticated ordinary players completed eight-team combat; champion NPC claim and duplicate refusal verified.'
            return
        }
        if($AnniversaryOnly) {
            foreach($phase in @('anniv-wire-start','anniv-hunt','anniv-hunt','anniv-hunt','anniv-hunt','anniv-hunt','anniv-wire-claim','anniv-verify')) {
                & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase $phase
                Invoke-CandidateControl 'stop'
                Invoke-CandidateControl 'start'
            }
            Remove-OwnedProbe
            Write-Output 'Anniversary full earned hunt and claimed rewards survive game/database restarts.'
            return
        }
        if($ClanWireOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase clan-wire
            Remove-OwnedProbe
            Write-Output 'Authenticated contested Clan War combat, loser exclusion and immediate bound soul, CP and RVP delivery passed.'
            return
        }
        if($ClanContestOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase clan-contest
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase clan-contest-reload
            Remove-OwnedProbe
            Write-Output 'Contested Clan War, stalemate, protected targets and earned prize persistence passed.'
            return
        }
        if($ClanWarOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase clan-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase clan-verify
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase clan-paid
            Remove-OwnedProbe
            Write-Output 'Clan War earned territory, purses and bound P7 survive game/database restart.'
            return
        }
        if($LocalWardrobeOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elite-wardrobe-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elite-wardrobe-reload
            Remove-OwnedProbe
            Write-Output 'Local Elite PK placement titles survive game/database restart.'
            return
        }
        if($LocalCtfOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase ctf-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase ctf-claim
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase ctf-verify
            Remove-OwnedProbe
            Write-Output 'Local CTF earned contributor prizes and replay protection survive process restarts.'
            return
        }
        if($FlowerContestOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase flower-grow
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase flower-donate
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase flower-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase flower-verify
            Remove-OwnedProbe
            Write-Output 'Magnolia-earned flower donations and contest prizes survive game/database restarts.'
            return
        }
        if($FashionOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase fashion-buy
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase fashion-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase fashion-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase fashion-expire
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase fashion-expired
            Remove-OwnedProbe
            Write-Output 'Fashion earned placements, equipped recovery and expired titles survive process restarts.'
            return
        }
        if($PlanetOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase planet-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            for($week=0;$week -lt 8;$week++) {
                & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase planet-week
            }
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase planet-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase planet-expire
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase planet-expired
            Remove-OwnedProbe
            Write-Output 'Planet eight-weekend collection earning, four titles, rollback and expired wing recovery passed.'
            return
        }
        if($HeartOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase heart-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase heart-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase heart-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase heart-expire
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase heart-expired
            Remove-OwnedProbe
            Write-Output 'Heart interrupted earning, judging, equipped reward persistence and expiry passed.'
            return
        }
        if($TempestOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase tempest-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase tempest-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase tempest-finish
            Remove-OwnedProbe
            Write-Output 'Tempest earned-scroll admission and process recovery passed.'
            return
        }
        if($EliminationOnly) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elimination-capture
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elimination-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elimination-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elimination-combat
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elimination-finish
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase elimination-finish
            Remove-OwnedProbe
            Write-Output 'Elimination rescue, cure, interrupted den and final reward recovery passed.'
            return
        }
        if($MrMs2016Only) {
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mrms-register
            Invoke-CandidateControl 'stop'
            Invoke-CandidateControl 'start'
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mrms-verify
            & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase mrms-verify
            Remove-OwnedProbe
            Write-Output 'Mr/Ms ordinary ballots, monthly judging and restart rewards passed.'
            return
        }
        & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase capture
        Invoke-ProbeSql 'revora_candidate_arena_process' 'DROP TRIGGER revora_arena_probe_suppress ON revora_arena_result_statistics; DROP TRIGGER revora_arena_probe_suppress ON revora_team_arena_results; DROP FUNCTION revora_arena_probe_suppress();' | Out-Null
        Invoke-CandidateControl 'stop'
        Invoke-CandidateControl 'start'
    }
    if(!$ResumeSpendOwnedFixture) {
        & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase verify
        & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase verify
    } else {
        if((Invoke-ProbeSql 'revora_candidate_arena_process' 'SELECT fingerprint IS NOT NULL FROM revora_arena_process_probe;') -ne 't'){throw 'Spend resume requires a completed recovery verification.'}
    }
    & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase spend
    & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase spend
    & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team
    & (Join-Path $PSScriptRoot 'Invoke-CandidateArenaLifecycle.ps1') -Phase team
    Remove-OwnedProbe
    Write-Output 'Arena process recovery and second-restart replay passed.'
} finally {
    Invoke-CandidateControl 'stop'
    $credential=$null;$secure.Dispose()
}
