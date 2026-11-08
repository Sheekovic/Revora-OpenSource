param([switch]$CheckCharacterEntry, [uint32]$CharacterUid = 0, [switch]$RequireCourierResume, [System.Diagnostics.Process]$CrashCandidate, [switch]$CheckSoulTrialAccess, [ValidateRange(0,3)][int]$SoulCombat = 0, [ValidateRange(1,50)][int]$SoulBatch = 1, [switch]$CheckPathHunt, [ValidateRange(0,4)][int]$PathCombat = 0, [switch]$CheckPromotion, [switch]$CheckMoonAccess, [switch]$CheckMoonCombat, [switch]$CheckMoonFinish, [switch]$CheckFirstRebirth, [switch]$CheckSecondLife, [switch]$CheckSecondPromotion, [switch]$CheckSecondRebirth, [switch]$CheckHomecomingReturn, [switch]$CheckHomecomingExpired, [switch]$CheckTournamentAccess, [switch]$CheckSkillTeamPk)
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
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'obj\mrms2017-validation\bin'
$exe = Join-Path $output 'CandidateAccountWireProbe.exe'
$root = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$listener = @(Get-NetTCPConnection -State Listen -LocalPort 19558 -ErrorAction SilentlyContinue)
if ($listener.Count -ne 1 -or $listener[0].LocalAddress -ne '127.0.0.1') { throw 'Private Account listener is unavailable.' }
$processPath = (Get-Process -Id $listener[0].OwningProcess -ErrorAction Stop).Path
if ([IO.Path]::GetFullPath($processPath) -ne (Join-Path $root 'game\Revora.exe')) { throw 'Account listener is not the approved private candidate.' }
if ((Get-FileHash -LiteralPath (Join-Path $output 'Revora.exe')).Hash -ne (Get-FileHash -LiteralPath $processPath).Hash) { throw 'Probe reference assembly differs from the running private candidate.' }
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 /warnaserror /main:CandidateAccountWireProbe "/out:$exe" "/reference:$output/Revora.exe" "/reference:$output/CO2_CORE_DLL.dll" /reference:System.Numerics.dll /reference:System.Core.dll (Join-Path $PSScriptRoot 'CandidateAccountWireProbe.cs') (Join-Path $PSScriptRoot 'CandidateTeamWireProbe.cs') (Join-Path $project 'tests/AccountLogin/SrpVerifierTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Account wire probe did not compile.' }
Copy-Item -LiteralPath (Join-Path $project 'bin/Debug/Revora.exe.config') -Destination ($exe + '.config')
foreach ($dependency in @('CO2_CORE_DLL.dll','BouncyCastle.Crypto.dll','protobuf-net.dll')) {
    if ((Get-FileHash -LiteralPath (Join-Path $output $dependency)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $root ('game\' + $dependency))).Hash) { throw 'Game probe dependency differs from private candidate.' }
}
$secure = Import-Clixml -LiteralPath (Join-Path $root 'account-password.dpapi.xml')
$credential = New-Object System.Management.Automation.PSCredential('MoonBoxTester', $secure)
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $exe
$start.WorkingDirectory = $output
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['REVORA_CANDIDATE_ACCOUNT_PASSWORD'] = $credential.GetNetworkCredential().Password
if ($CheckTournamentAccess) { $start.EnvironmentVariables['REVORA_CANDIDATE_TOURNAMENT_ACCESS'] = '1' }
if ($CheckSkillTeamPk) { $start.EnvironmentVariables['REVORA_CANDIDATE_SKILL_PK'] = '1' }
if ($CheckHomecomingExpired) { $start.EnvironmentVariables['REVORA_CANDIDATE_HOMECOMING_EXPIRED'] = '1' }
if ($CheckHomecomingReturn) { $start.EnvironmentVariables['REVORA_CANDIDATE_HOMECOMING_RETURN'] = '1' }
if ($CheckCharacterEntry) { $start.EnvironmentVariables['REVORA_CANDIDATE_CHARACTER_UID'] = $CharacterUid.ToString() }
if ($RequireCourierResume) { $start.EnvironmentVariables['REVORA_CANDIDATE_COURIER_RESUME'] = '1' }
if ($CrashCandidate) {
    if (!$CheckCharacterEntry -or $CrashCandidate.HasExited -or $CrashCandidate.Id -ne $listener[0].OwningProcess) { throw 'Crash target is not the owned private candidate.' }
    $start.EnvironmentVariables['REVORA_CANDIDATE_CRASH_CHECK'] = '1'
}
if ($CheckSoulTrialAccess) {
    if (!$CheckCharacterEntry -or ($CrashCandidate -and !$CheckSecondRebirth)) { throw 'Soul Trial crash mode requires the completed career.' }
    $start.EnvironmentVariables['REVORA_CANDIDATE_SOUL_TRIAL'] = '1'
    if ($SoulCombat) { $start.EnvironmentVariables['REVORA_CANDIDATE_SOUL_COMBAT'] = $SoulCombat.ToString(); $start.EnvironmentVariables['REVORA_CANDIDATE_SOUL_BATCH'] = $SoulBatch.ToString() }
}
if ($CheckPathHunt) {
    $start.EnvironmentVariables['REVORA_CANDIDATE_PATH_HUNT'] = '1'
    if ($PathCombat) { $start.EnvironmentVariables['REVORA_CANDIDATE_PATH_COMBAT'] = $PathCombat.ToString(); $start.EnvironmentVariables['REVORA_CANDIDATE_SOUL_BATCH'] = $SoulBatch.ToString() }
}
if ($CheckPromotion) { $start.EnvironmentVariables['REVORA_CANDIDATE_PROMOTION'] = '1' }
if ($CheckMoonAccess) { $start.EnvironmentVariables['REVORA_CANDIDATE_MOON_ACCESS'] = '1' }
if ($CheckMoonCombat) { $start.EnvironmentVariables['REVORA_CANDIDATE_MOON_COMBAT'] = '1' }
if ($CheckMoonFinish) { $start.EnvironmentVariables['REVORA_CANDIDATE_MOON_FINISH'] = '1' }
if ($CheckFirstRebirth) { $start.EnvironmentVariables['REVORA_CANDIDATE_FIRST_REBIRTH'] = '1' }
if ($CheckSecondLife) { $start.EnvironmentVariables['REVORA_CANDIDATE_SECOND_LIFE'] = '1' }
if ($CheckSecondPromotion) { $start.EnvironmentVariables['REVORA_CANDIDATE_SECOND_PROMOTION'] = '1' }
if ($CheckSecondRebirth) { $start.EnvironmentVariables['REVORA_CANDIDATE_SECOND_REBIRTH'] = '1' }
$child = New-Object System.Diagnostics.Process
$child.StartInfo = $start
$childStarted = $false
try {
    if (!$child.Start()) { throw 'Account wire probe did not start.' }
    $childStarted = $true
    $start.EnvironmentVariables.Remove('REVORA_CANDIDATE_ACCOUNT_PASSWORD')
    $stderr = $child.StandardError.ReadToEndAsync()
    $prefix = ''
    if ($CrashCandidate) {
        $deadline = [DateTime]::UtcNow.AddSeconds($(if ($CheckTournamentAccess) { 120 } else { 60 }))
        $checkpoint = $false
        while ([DateTime]::UtcNow -lt $deadline) {
            $lineTask = $child.StandardOutput.ReadLineAsync()
            if (!$lineTask.Wait([Math]::Max(1,[int]($deadline-[DateTime]::UtcNow).TotalMilliseconds))) { throw 'Live crash checkpoint timed out.' }
            $line = $lineTask.GetAwaiter().GetResult()
            if ($null -eq $line) { break }
            $prefix += $line + "`n"
            if ($CheckTournamentAccess -and $line -eq 'Private Team PK opening checkpoint ready.') {
                if ($CrashCandidate.HasExited -or $CrashCandidate.MainModule.FileName -ne $processPath) { throw 'Private opening target changed.' }
                $CrashCandidate.StandardInput.WriteLine($(if ($CheckSkillTeamPk) { '@steam' } else { '@team' }))
                $CrashCandidate.StandardInput.Flush()
            }
            if ($line -eq $(if ($CheckTournamentAccess) { 'Private Team PK live crash checkpoint ready.' } else { 'Private Courier live crash checkpoint ready.' })) {
                if ($CrashCandidate.HasExited -or $CrashCandidate.MainModule.FileName -ne $processPath) { throw 'Private crash target changed.' }
                $CrashCandidate.Kill()
                if (!$CrashCandidate.WaitForExit(5000)) { throw 'Owned candidate did not terminate.' }
                $checkpoint = $true
                break
            }
        }
        if (!$checkpoint) { throw 'Requested live crash checkpoint was not reached.' }
    }
    $stdout = $child.StandardOutput.ReadToEndAsync()
    $probeDeadline = [DateTime]::UtcNow.AddSeconds($(if ($CheckMoonCombat) { 600 } elseif ($SoulCombat -or $PathCombat -or $CheckMoonAccess) { 240 } elseif ($CheckCharacterEntry) { 60 } else { 25 }))
    while (!$child.WaitForExit(1000) -and [DateTime]::UtcNow -lt $probeDeadline) {}
    if (!$child.HasExited) { $child.Kill(); $child.WaitForExit(); throw 'Account wire probe timed out.' }
    $result = $prefix + $stdout.GetAwaiter().GetResult(); $failure = $stderr.GetAwaiter().GetResult().Trim()
    if ($child.ExitCode -ne 0) {
        foreach ($line in ($result -split "`r?`n")) { if ($line -match '^Private (ordinary|Courier|Soul Trial|Path Hunt|Trojan|Moon Box|rebirth|world) ') { Write-Output $line } }
        if ($failure -match '^Private Account wire check failed: [A-Za-z0-9]+ stage=[a-z0-9-]+$') { throw $failure }
        throw 'Account wire probe failed; inspect its bounded protocol checks.'
    }
    Write-Output $result.Trim()
} finally {
    $start.EnvironmentVariables.Remove('REVORA_CANDIDATE_ACCOUNT_PASSWORD')
    if ($childStarted -and !$child.HasExited) { $child.Kill(); $child.WaitForExit(5000) | Out-Null }; $credential = $null; $secure.Dispose(); $child.Dispose()
}
