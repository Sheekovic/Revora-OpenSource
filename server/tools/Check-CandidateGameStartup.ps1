param([switch]$CheckWireAuth, [switch]$CheckCharacterEntry, [uint32]$CharacterUid = 0, [switch]$RequireCourierResume, [switch]$CheckCourierCrash, [switch]$CheckSoulTrialAccess, [ValidateRange(0,3)][int]$SoulCombat = 0, [ValidateRange(1,50)][int]$SoulBatch = 1, [switch]$CheckPathHunt, [ValidateRange(0,4)][int]$PathCombat = 0, [switch]$CheckPromotion, [switch]$CheckMoonAccess, [switch]$CheckMoonCombat, [switch]$CheckMoonFinish, [switch]$CheckFirstRebirth, [switch]$CheckSecondLife, [switch]$CheckSecondPromotion, [switch]$CheckSecondRebirth, [switch]$CheckHomecomingReturn, [switch]$CheckHomecomingExpired, [switch]$CheckTournamentAccess, [switch]$CheckSkillTeamPk, [switch]$CheckKingdomStartup)
$ErrorActionPreference = 'Stop'
if ($CheckSkillTeamPk -and !$CheckTournamentAccess) { throw 'Skill PK mode requires tournament access checks.' }
if ($CheckTournamentAccess -and !$CheckSecondRebirth) { throw 'Tournament access uses the completed ordinary career fixture.' }
if ($CheckHomecomingExpired -and !$CheckHomecomingReturn) { throw 'Expiry check requires Homecoming return mode.' }
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
if ($CheckCourierCrash -and (!$CheckWireAuth -or !$CheckCharacterEntry)) { throw 'Courier crash check requires ordinary character wire acceptance.' }
$candidateRoot = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$game = Join-Path $candidateRoot 'game'
$manifest = Get-Content -LiteralPath (Join-Path $candidateRoot 'game-runtime-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest.files) {
    $path = [IO.Path]::GetFullPath((Join-Path $game $entry.path))
    if (!$path.StartsWith($game + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Candidate path escaped runtime.' }
    # These are fresh state files written by the game, not immutable catalog/code.
    if (@('database/InnerPower.txt','database/broadcasts.txt','database/Slayer/broadcasts.txt','database/Slayer/PoleEmulator.txt') -contains $entry.path) { continue }
    if ((Get-FileHash -LiteralPath $path).Hash -ne $entry.sha256) {
        throw 'Candidate game file identity mismatch; review before launch.'
    }
}
if (@(Get-NetTCPConnection -State Listen -LocalPort 19558,19559,19619 -ErrorAction SilentlyContinue).Count) { throw 'Candidate game ports are occupied.' }
$secure = Import-Clixml -LiteralPath (Join-Path $candidateRoot 'runtime-password.dpapi.xml')
$credential = New-Object System.Management.Automation.PSCredential('revora_candidate_runtime', $secure)
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = Join-Path $game 'Revora.exe'
$start.WorkingDirectory = $game
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['REVORA_DATABASE_PASSWORD'] = $credential.GetNetworkCredential().Password
$start.EnvironmentVariables['REVORA_KINGDOM_COMPETITION_ENABLED'] = if($CheckKingdomStartup) { 'true' } else { 'false' }
$process = New-Object System.Diagnostics.Process
$process.StartInfo = $start
$ready = $false
$arenaPrepared = $false
$teamClerkReady = $false
$kingdomReady = $false
$restoredEventsReady = $false
$forced = $false
$started = $false
$exitCode = $null
try {
    if (!$process.Start()) { throw 'Candidate game did not start.' }
    $started = $true
    $start.EnvironmentVariables.Remove('REVORA_DATABASE_PASSWORD')
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while (!$process.WaitForExit(1000) -and [DateTime]::UtcNow -lt $deadline) {
        $listeners = @(Get-NetTCPConnection -State Listen -OwningProcess $process.Id -ErrorAction SilentlyContinue)
        if (@($listeners | Where-Object { $_.LocalAddress -ne '127.0.0.1' }).Count) {
            throw 'Candidate opened an unexpected listener.'
        }
        if (@($listeners | Where-Object { $_.LocalPort -eq 19558 }).Count -eq 1 -and @($listeners | Where-Object { $_.LocalPort -eq 19559 }).Count -eq 1 -and @($listeners | Where-Object { $_.LocalPort -eq 19619 }).Count -eq 1) {
            $ready = $true;
            if ($CheckWireAuth) { & (Join-Path $PSScriptRoot 'Invoke-CandidateAccountWireCheck.ps1') -CheckCharacterEntry:$CheckCharacterEntry -CharacterUid $CharacterUid -RequireCourierResume:$RequireCourierResume -CheckSoulTrialAccess:$CheckSoulTrialAccess -SoulCombat $SoulCombat -SoulBatch $SoulBatch -CheckPathHunt:$CheckPathHunt -PathCombat $PathCombat -CheckPromotion:$CheckPromotion -CheckMoonAccess:$CheckMoonAccess -CheckMoonCombat:$CheckMoonCombat -CheckMoonFinish:$CheckMoonFinish -CheckFirstRebirth:$CheckFirstRebirth -CheckSecondLife:$CheckSecondLife -CheckSecondPromotion:$CheckSecondPromotion -CheckSecondRebirth:$CheckSecondRebirth -CheckHomecomingReturn:$CheckHomecomingReturn -CheckHomecomingExpired:$CheckHomecomingExpired -CheckTournamentAccess:$CheckTournamentAccess -CheckSkillTeamPk:$CheckSkillTeamPk -CrashCandidate:$(if ($CheckCourierCrash) { $process } else { $null }) }
            break
        }
    }
} finally {
    $start.EnvironmentVariables.Remove('REVORA_DATABASE_PASSWORD')
    if ($started -and !$process.HasExited) {
        $process.StandardInput.WriteLine($(if ($ready) { '@close' } else { '' }))
        $process.StandardInput.Flush()
        if (!$process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit(); $forced = $true }
    }
    if ($started -and $process.HasExited) { $exitCode = $process.ExitCode }
    if ($stdout -and $stderr) {
        $text = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        $teamClerkReady = [regex]::Matches($text,[regex]::Escape('[local-team-pk] clerk_ready map=1002 x=291 y=146')).Count -eq 1 -and !$text.Contains('NPC 417171 parked on map 6000')
        $teamClerkReady = $teamClerkReady -and [regex]::Matches($text,[regex]::Escape('[skill-team-pk] clerk_ready map=1002 x=300 y=146')).Count -eq 1 -and !$text.Contains('NPC 801717 parked on map 6000')
        $restoredEventsReady = $text.Contains('[roadmap] restored-event-pulse complete')
        $kingdomReady = if($CheckKingdomStartup) { [regex]::Matches($text,[regex]::Escape('[kingdom] startup-ready map=3998 actors=5 crown=')).Count -eq 1 } else { [regex]::Matches($text,[regex]::Escape('[kingdom] startup-disabled')).Count -eq 1 }
        $preparedMarker = '[arena] startup-prepared'
        $cacheMarker = '[arena] caches-loaded prepared=True'
        $arenaPrepared = ([regex]::Matches($text,[regex]::Escape($preparedMarker)).Count -eq 1 -and
            [regex]::Matches($text,[regex]::Escape($cacheMarker)).Count -eq 1 -and
            $text.IndexOf($preparedMarker,[StringComparison]::Ordinal) -lt $text.IndexOf($cacheMarker,[StringComparison]::Ordinal))
        $text = $text.Replace($credential.GetNetworkCredential().Password, '[REDACTED]')
        $text = [regex]::Replace($text, '(?i)(password\s*=)[^;\r\n]*', '$1[REDACTED]')
        $text = [regex]::Replace($text, '(?i)(token\s*=)\S+', '$1[REDACTED]')
        $text | Set-Content -LiteralPath (Join-Path $candidateRoot 'game-startup.log') -Encoding UTF8
    }
    $secure.Dispose(); $credential = $null; $process.Dispose()
}
Write-Output ('Candidate game listeners ready: ' + $ready + '; Arena prepared before caches: ' + $arenaPrepared + '; restored events ticked: ' + $restoredEventsReady + '; Team/Skill PK clerks ready: ' + $teamClerkReady + '; Kingdom startup mode verified: ' + $kingdomReady + '; emergency cleanup kill: ' + $forced + '; exit code: ' + $exitCode)
if (!$kingdomReady -or !$ready -or !$arenaPrepared -or !$restoredEventsReady -or !$teamClerkReady -or $forced -or (!$CheckCourierCrash -and $exitCode -ne 0) -or ($CheckCourierCrash -and $exitCode -eq 0)) { throw 'Candidate did not pass the requested startup/termination inspection; review its redacted log.' }
if (@(Get-NetTCPConnection -State Listen -LocalPort 19558,19559,19619 -ErrorAction SilentlyContinue).Count) { throw 'Candidate game listener remains after close.' }

if ($CheckCourierCrash) { Write-Output "Private candidate intentionally terminated with a verified live game session; normal shutdown was not used." }
