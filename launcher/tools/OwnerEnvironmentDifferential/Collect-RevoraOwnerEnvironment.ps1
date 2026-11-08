[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateScript({Test-Path $_ -PathType Container})][string]$ClientRoot,
    [ValidateSet('DX8','DX9')][string]$Profile = 'DX9',
    [string]$OutputDirectory = (Join-Path $PWD 'RevoraOwnerDiagnostics'),
    [ValidateRange(1,60)][int]$TimeoutMinutes = 30,
    [ValidateRange(-1,8)][int]$SyntheticDisplayAdapterCount = -1
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$collectorStage='Initialization'
trap {
    foreach($variableName in @('jsonTemp','textTemp','outputProbe')){ $variable=Get-Variable -Name $variableName -ErrorAction SilentlyContinue;if($variable-and$variable.Value-and[IO.File]::Exists([string]$variable.Value)){[IO.File]::Delete([string]$variable.Value)} }
    throw [InvalidOperationException]::new("Collector stage '$collectorStage' failed: $($_.Exception.GetType().Name)")
}
Import-Module (Join-Path $PSScriptRoot 'Revora.OwnerDiff.psm1') -Force
$root = [IO.Path]::GetFullPath($ClientRoot).TrimEnd('\')
$started = [DateTimeOffset]::UtcNow
$invocationDirectory = (Get-Location).ProviderPath
$resolvedOutputDirectory = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $invocationDirectory $OutputDirectory)) }
$outputProbe = $null
$collectorStage='OutputPreflight'
try {
    [IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null
    $outputProbe = Join-Path $resolvedOutputDirectory ('.revora-write-test-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $probeStream = [IO.File]::Open($outputProbe,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    $probeStream.Dispose(); [IO.File]::Delete($outputProbe); $outputProbe = $null
} catch {
    if ($outputProbe -and [IO.File]::Exists($outputProbe)) { [IO.File]::Delete($outputProbe) }
    throw [InvalidOperationException]::new("Collector stage 'OutputPreflight' failed: $($_.Exception.GetType().Name)")
}

function Safe([scriptblock]$Action, $Fallback=@()) { try { & $Action } catch { $Fallback } }
function Rel([string]$relative) { Join-Path $root $relative }
function Invoke-GraphicsModuleObserver([int]$ProcessId) {
    $observer = Join-Path $PSScriptRoot 'Revora.GraphicsModuleObserver.exe'
    if (-not [IO.File]::Exists($observer)) { return [pscustomobject]@{status='ObserverNotProvisioned';win32Error=0;modules=@()} }
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $observer
    $start.Arguments = '--pid '+$ProcessId.ToString([Globalization.CultureInfo]::InvariantCulture)
    $start.WorkingDirectory = $PSScriptRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit(2000)) { try { $process.Kill() } catch {}; return [pscustomobject]@{status='ObserverTimeout';win32Error=0;modules=@()} }
        $output = $process.StandardOutput.ReadToEnd()
        if ([string]::IsNullOrWhiteSpace($output)) { return [pscustomobject]@{status='ObserverNoOutput';win32Error=0;modules=@()} }
        return $output | ConvertFrom-Json
    } catch { return [pscustomobject]@{status=('ObserverFailure:'+$_.Exception.GetType().Name);win32Error=0;modules=@()} }
    finally { $process.Dispose() }
}

$collectorStage='StaticInventory'
$profileExe = if ($Profile -eq 'DX9') { 'Env_DX9\Conquer.exe' } else { 'Env_DX8\Conquer.exe' }
$staticPaths = @('play.exe','AutoP.exe',$profileExe,'Env_DX9\d3d9.dll','LauncherAssets\RevoraBridge.dll','LauncherAssets\RevoraBridgeLoader.exe','ini\startFail.ini','ini\PlaySetUP.ini','ini\GameSetup.ini','ini\CommonTip.lua')
$files = @($staticPaths | ForEach-Object { Get-RevoraFileEvidence -Path (Rel $_) -ClientRoot $root })
$os = Get-CimInstance Win32_OperatingSystem
$computer = Get-CimInstance Win32_ComputerSystem
$culture = Get-Culture
$system = [ordered]@{
    edition=$os.Caption; osBuild=$os.BuildNumber; version=$os.Version; architecture=$os.OSArchitecture
    updates=@(Get-HotFix | Sort-Object InstalledOn | Select-Object HotFixId,InstalledOn)
    locale=$culture.Name; timezone=(Get-TimeZone).Id; codePage=[Globalization.CultureInfo]::CurrentCulture.TextInfo.ANSICodePage
    remoteSession=([Environment]::UserInteractive -and ($env:SESSIONNAME -like 'RDP-*')); sessionType=if($env:SESSIONNAME -like 'RDP-*'){'Remote'}else{'LocalOrConsole'}
}
$video = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion,VideoProcessor,CurrentHorizontalResolution,CurrentVerticalResolution)
$monitors = @(Safe { Get-CimInstance -Namespace root\wmi WmiMonitorBasicDisplayParams -ErrorAction Stop | Where-Object Active })
if($SyntheticDisplayAdapterCount -ge 0){if($env:REVORA_OWNERDIFF_SYNTHETIC-ne'TEST_ONLY'){throw 'Synthetic display input is test-only.'};$video=@(1..$SyntheticDisplayAdapterCount|ForEach-Object{if($SyntheticDisplayAdapterCount-gt 0){[pscustomobject]@{Name="Synthetic Adapter $_";DriverVersion='1.0';VideoProcessor='Synthetic';CurrentHorizontalResolution=1920;CurrentVerticalResolution=1080}}});$monitors=@($video)}
$display = [ordered]@{ adapters=$video; activeMonitorCount=@($monitors).Count; dpi=(Get-ItemProperty 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name AppliedDPI -ErrorAction SilentlyContinue).AppliedDPI }
$directX = @(@('d3dx9_24.dll','d3dx9_25.dll','d3dx9_26.dll','d3dx9_27.dll','d3dx9_28.dll','d3dx9_29.dll','d3dx9_30.dll','d3dx9_31.dll','d3dx9_32.dll','d3dx9_33.dll','d3dx9_34.dll','d3dx9_35.dll','d3dx9_36.dll','d3dx9_37.dll','d3dx9_38.dll','d3dx9_39.dll','d3dx9_40.dll','d3dx9_41.dll','d3dx9_42.dll','d3dx9_43.dll') | ForEach-Object { $dxPath=Join-Path $env:WINDIR "SysWOW64\$_"; [ordered]@{name=$_;installed=Test-Path $dxPath;version=if(Test-Path $dxPath){(Get-Item $dxPath).VersionInfo.FileVersion}else{$null};sha256=if(Test-Path $dxPath){(Get-FileHash $dxPath -Algorithm SHA256).Hash}else{$null}} })
$vc = @(Safe { Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x86' -ErrorAction Stop | Select-Object Installed,Version,Bld })
$vcInstalled = @(Safe { Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction Stop | Where-Object { $_.DisplayName -match 'Microsoft Visual C\+\+.*(?:x86|Redistributable)' } | Select-Object DisplayName,DisplayVersion | Sort-Object DisplayName,DisplayVersion })
$runtimes = [ordered]@{ directXLegacy=$directX; vcRuntimeX86=$vc; vcRedistributablesX86=$vcInstalled }
$compatTargets=@('play.exe','AutoP.exe',$profileExe)|ForEach-Object{[IO.Path]::GetFullPath((Rel $_))}
$compat = [ordered]@{
    appCompatUser=@($compatTargets|ForEach-Object{[ordered]@{path=ConvertTo-RevoraRedactedText $_ $root;flags=Safe { Get-ItemPropertyValue 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers' -Name $_ } $null}})
    appCompatMachine=@($compatTargets|ForEach-Object{[ordered]@{path=ConvertTo-RevoraRedactedText $_ $root;flags=Safe { Get-ItemPropertyValue 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers' -Name $_ } $null}})
    processMitigations=Safe { Get-ProcessMitigation -Name (Rel $profileExe) | Out-String } 'Unavailable'
}
$securityProducts = @(Safe { Get-CimInstance -Namespace root\SecurityCenter2 AntiVirusProduct | Select-Object displayName,productState })
$disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$([IO.Path]::GetPathRoot($root).TrimEnd('\'))'"
$filesystem = [ordered]@{ drive=$disk.DeviceID; fileSystem=$disk.FileSystem; freeBytes=[long]$disk.FreeSpace; totalBytes=[long]$disk.Size }

$collectorStage='Observation'
Write-Host 'Observation is ready. Launch PlayRevora normally now. No client process will be started by this collector.' -ForegroundColor Cyan
$deadline = [DateTimeOffset]::UtcNow.AddMinutes($TimeoutMinutes)
$seen = @{}; $timeline=[Collections.Generic.List[object]]::new(); $tcp=[Collections.Generic.List[object]]::new(); $modules=@(); $graphicsModules=[ordered]@{}; $graphicsModuleStatus='NotObserved'; $conquerPid=$null; $conquerFirst=$null; $conquerExit=$null; $trackedConquer=$null; $exitCode=$null;$conquerRuns=[Collections.Generic.List[object]]::new();$successorDeadline=$null
$approvedConquerPaths=@([IO.Path]::GetFullPath((Rel 'Env_DX8\Conquer.exe')),[IO.Path]::GetFullPath((Rel 'Env_DX9\Conquer.exe')))
while ($env:REVORA_OWNERDIFF_SYNTHETIC -ne 'TEST_ONLY' -and [DateTimeOffset]::UtcNow -lt $deadline) {
    $candidates = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('play.exe','AutoP.exe','Conquer.exe') -and $_.ExecutablePath -and ([IO.Path]::GetFullPath($_.ExecutablePath)).StartsWith($root,[StringComparison]::OrdinalIgnoreCase) })
    foreach($candidate in $candidates) {
        $pidValue=[int]$candidate.ProcessId
        if(-not $seen.ContainsKey($pidValue)) { $record=Get-RevoraProcessRecord $pidValue $root; if($record){$seen[$pidValue]=$true;$timeline.Add($record)} }
        foreach($row in @(Get-RevoraTcpMetadata $pidValue ([DateTimeOffset]::UtcNow.ToString('O')))){ $tcp.Add($row) }
        $candidatePath=[IO.Path]::GetFullPath($candidate.ExecutablePath)
        if($candidate.Name -eq 'Conquer.exe' -and $approvedConquerPaths -contains $candidatePath) {
            if($null -eq $conquerPid){$conquerPid=$pidValue;$conquerFirst=[DateTimeOffset]::UtcNow;$trackedConquer=Get-Process -Id $pidValue -ErrorAction SilentlyContinue;$modules=Get-RevoraLoadedModules $pidValue $root;$successorDeadline=$null}
            if($pidValue -eq $conquerPid -and $candidatePath.Equals([IO.Path]::GetFullPath((Rel 'Env_DX9\Conquer.exe')),[StringComparison]::OrdinalIgnoreCase)){try {
                $observation=Invoke-GraphicsModuleObserver $pidValue
                $graphicsModuleStatus=[string]$observation.status
                foreach($module in @($observation.modules)) {
                    $modulePath=[IO.Path]::GetFullPath([string]$module.path);$moduleKey=$modulePath.ToLowerInvariant()
                    if(-not $graphicsModules.Contains($moduleKey)) {
                        $evidence=Get-RevoraFileEvidence $modulePath $root
                        $evidence['firstSeenUtc']=[DateTimeOffset]::UtcNow.ToString('O')
                        $expectedLocal=[IO.Path]::GetFullPath((Rel 'Env_DX9\d3d9.dll'))
                        $evidence['loadCategory']=if($modulePath.Equals($expectedLocal,[StringComparison]::OrdinalIgnoreCase)){'ProfileLocalD3D9'}elseif($modulePath.StartsWith(([IO.Path]::GetFullPath($env:WINDIR)).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){'WindowsGraphicsRuntime'}else{'OtherGraphicsModule'}
                        $graphicsModules[$moduleKey]=$evidence
                    }
                }
            } catch { $graphicsModuleStatus='EnumerationUnavailable:'+$_.Exception.GetType().Name }}
        }
    }
    if($null -ne $conquerPid -and -not (Get-Process -Id $conquerPid -ErrorAction SilentlyContinue)){ $conquerExit=[DateTimeOffset]::UtcNow; if($trackedConquer){try{$exitCode=$trackedConquer.ExitCode}catch{$exitCode=$null}};$conquerRuns.Add([ordered]@{processId=$conquerPid;startedUtc=$conquerFirst.ToString('O');endedUtc=$conquerExit.ToString('O');lifetimeSeconds=[math]::Round(($conquerExit-$conquerFirst).TotalSeconds,3);exitCode=$exitCode});$conquerPid=$null;$trackedConquer=$null;$successorDeadline=[DateTimeOffset]::UtcNow.AddSeconds(10) }
    if($null -eq $conquerPid -and $null -ne $successorDeadline -and [DateTimeOffset]::UtcNow -ge $successorDeadline){break}
    Start-Sleep -Milliseconds 500
}
$ended=[DateTimeOffset]::UtcNow
$collectorStage='PostObservationEvidence'
$eventStart=$started.UtcDateTime; $eventEnd=$ended.UtcDateTime.AddSeconds(5)
$eventPaths=@([IO.Path]::GetFullPath((Rel 'play.exe')),[IO.Path]::GetFullPath((Rel 'AutoP.exe')))+$approvedConquerPaths
$observedPids=@($seen.Keys|ForEach-Object{[int]$_})
$events = @(Safe { Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$eventStart;EndTime=$eventEnd} -ErrorAction Stop | Where-Object { Test-RevoraEventCorrelation -Event $_ -ApprovedExecutablePaths $eventPaths -ObservedProcessIds $observedPids -ObservationStart $eventStart -ObservationEnd $eventEnd } | Select-Object TimeCreated,ProviderName,Id,LevelDisplayName,@{n='Correlation';e={'ExactTestedPathAndWindow'}},@{n='Message';e={ConvertTo-RevoraRedactedText $_.Message $root}} })
$defenderEvents = @(Safe { Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-Windows Defender/Operational';StartTime=$eventStart;EndTime=$eventEnd;Id=@(1116,1117,1121,1122,5007)} -ErrorAction Stop | Where-Object { Test-RevoraEventCorrelation -Event $_ -ApprovedExecutablePaths $eventPaths -ObservedProcessIds $observedPids -ObservationStart $eventStart -ObservationEnd $eventEnd } | Select-Object TimeCreated,ProviderName,Id,LevelDisplayName,@{n='Correlation';e={'ExactTestedPathAndWindow'}},@{n='Message';e={ConvertTo-RevoraRedactedText $_.Message $root}} })
$requestedConfig=@('ini\startFail.ini','ini\PlaySetUP.ini','ini\GameSetup.ini')|ForEach-Object{Get-RevoraBoundedTextEvidence (Rel $_) $root}
$generatedLogs=@(Get-ChildItem -LiteralPath $root -Filter '*.log' -File -Recurse -ErrorAction SilentlyContinue|Where-Object{$_.LastWriteTimeUtc-ge$started.UtcDateTime}|Sort-Object FullName|Select-Object -First 100|ForEach-Object{Get-RevoraBoundedTextEvidence $_.FullName $root -RelevantLinesOnly})
$commonTip=Rel 'ini\CommonTip.lua';$startupAssets=@()
if(Test-Path -LiteralPath $commonTip -PathType Leaf){
  $referencePattern='[''"]([^''"]+\.(?:ini|lua|dds|c3|ani|dat|jpg|png|tga|wav|mp3))[''"]'
  $references=@([regex]::Matches(([IO.File]::ReadAllText($commonTip)),$referencePattern,'IgnoreCase')|ForEach-Object{$_.Groups[1].Value.Replace('/','\')}|Sort-Object -Unique|Select-Object -First 200)
  foreach($reference in $references){try{$candidate=[IO.Path]::GetFullPath((Join-Path $root $reference));if($candidate.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)-and(Test-Path -LiteralPath $candidate -PathType Leaf)){$startupAssets+=Get-RevoraFileEvidence $candidate $root}}catch{}}
}
$report=[ordered]@{
    schemaVersion=1; collectorVersion='1.2.0-owner-test'; collectedUtc=$ended.ToString('O'); profile=$Profile
    system=$system; display=$display; runtimes=$runtimes; files=$files; compatibility=$compat; securityProducts=$securityProducts; filesystem=$filesystem
    requestedConfiguration=$requestedConfig;generatedClientLogs=$generatedLogs;referencedStartupAssets=$startupAssets
    run=[ordered]@{ observationStartedUtc=$started.ToString('O'); observationEndedUtc=$ended.ToString('O'); processTimeline=@($timeline); conquerObserved=(@($conquerRuns).Count -gt 0 -or $null -ne $conquerPid); conquerProcesses=@($conquerRuns);conquerLifetimeSeconds=if(@($conquerRuns).Count){$conquerRuns[$conquerRuns.Count-1].lifetimeSeconds}else{$null}; exitCode=$exitCode; modules=@($modules); graphicsModuleStatus=$graphicsModuleStatus; graphicsModules=@($graphicsModules.Values); tcpDestinations=@($tcp|Sort-Object processId,destinationIp,destinationPort,state -Unique); events=@($events)+@($defenderEvents) }
}
$collectorStage='ReportSerialization'
$jsonPath=Join-Path $resolvedOutputDirectory 'revora-owner-environment.json'; $textPath=Join-Path $resolvedOutputDirectory 'revora-owner-environment-summary.txt'
$jsonTemp=Join-Path $resolvedOutputDirectory ('.revora-owner-environment-'+[Guid]::NewGuid().ToString('N')+'.json.tmp');$textTemp=Join-Path $resolvedOutputDirectory ('.revora-owner-environment-'+[Guid]::NewGuid().ToString('N')+'.txt.tmp')
$json=$report|ConvertTo-Json -Depth 20
$loadedD3D9=@($report.run.graphicsModules | Where-Object { $_['relativePath'] -eq 'Env_DX9/d3d9.dll' })
$d3d9Result=if($report.run.graphicsModuleStatus -ne 'Observed'){'Unavailable ('+$report.run.graphicsModuleStatus+')'}elseif(@($loadedD3D9).Count -gt 0){'Loaded'}else{'Not loaded'}
$summary=@("Revora Owner Environment Differential","Profile: $Profile","Conquer observed: $($report.run.conquerObserved)","Lifetime seconds: $($report.run.conquerLifetimeSeconds)","Modules recorded: $(@($report.run.modules).Count)","Graphics modules recorded: $(@($report.run.graphicsModules).Count)","Profile-local d3d9 observation: $d3d9Result","TCP destinations recorded: $(@($report.run.tcpDestinations).Count)","Relevant events: $(@($report.run.events).Count)","JSON: revora-owner-environment.json")
$jsonHadOriginal=[IO.File]::Exists($jsonPath);$textHadOriginal=[IO.File]::Exists($textPath);$jsonBackup=$jsonPath+'.rollback-'+[Guid]::NewGuid().ToString('N');$textBackup=$textPath+'.rollback-'+[Guid]::NewGuid().ToString('N')
$collectorStage='ReportCommit'
try {
    [IO.File]::WriteAllText($jsonTemp,$json,(New-Object Text.UTF8Encoding($false)));[IO.File]::WriteAllLines($textTemp,$summary,(New-Object Text.UTF8Encoding($false)))
    if($jsonHadOriginal){[IO.File]::Replace($jsonTemp,$jsonPath,$jsonBackup)}else{[IO.File]::Move($jsonTemp,$jsonPath)}
    if($textHadOriginal){[IO.File]::Replace($textTemp,$textPath,$textBackup)}else{[IO.File]::Move($textTemp,$textPath)}
    if([IO.File]::Exists($jsonBackup)){[IO.File]::Delete($jsonBackup)};if([IO.File]::Exists($textBackup)){[IO.File]::Delete($textBackup)}
} catch {
    foreach($partial in @($jsonTemp,$textTemp)){if([IO.File]::Exists($partial)){[IO.File]::Delete($partial)}}
    if([IO.File]::Exists($jsonBackup)){if([IO.File]::Exists($jsonPath)){[IO.File]::Replace($jsonBackup,$jsonPath,$null)}else{[IO.File]::Move($jsonBackup,$jsonPath)}}elseif(-not$jsonHadOriginal-and[IO.File]::Exists($jsonPath)){[IO.File]::Delete($jsonPath)}
    if([IO.File]::Exists($textBackup)){if([IO.File]::Exists($textPath)){[IO.File]::Replace($textBackup,$textPath,$null)}else{[IO.File]::Move($textBackup,$textPath)}}elseif(-not$textHadOriginal-and[IO.File]::Exists($textPath)){[IO.File]::Delete($textPath)}
    throw [InvalidOperationException]::new("Collector stage 'ReportCommit' failed: $($_.Exception.GetType().Name)")
}
$collectorStage='Complete'
Write-Host 'Saved: <OUTPUT>\revora-owner-environment.json' -ForegroundColor Green; Write-Host 'Saved: <OUTPUT>\revora-owner-environment-summary.txt' -ForegroundColor Green
