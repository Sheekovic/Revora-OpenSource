Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Revora.OwnerDiff.psm1') -Force
function Assert($condition,[string]$message){if(-not$condition){throw $message}}
$root=Join-Path $env:TEMP ('RevoraOwnerDiff-'+[Guid]::NewGuid().ToString('N'));New-Item $root -ItemType Directory|Out-Null
try{
  $file=Join-Path $root 'synthetic.bin';[IO.File]::WriteAllText($file,'synthetic')
  $evidence=Get-RevoraFileEvidence $file $root;Assert $evidence.exists 'file evidence';Assert ($evidence.path -like '<CLIENT_ROOT>*') 'root redaction'
  $process=Start-Process $env:ComSpec -ArgumentList '/d','/c','ping 127.0.0.1 -n 3 >nul' -PassThru -WindowStyle Hidden
  try{$record=Get-RevoraProcessRecord $process.Id $root;Assert ($null-ne$record) 'synthetic process observation'}finally{if(-not$process.HasExited){Stop-Process $process.Id -Force}}
  $base=[pscustomobject]@{schemaVersion=1;system=[pscustomobject]@{remoteSession=$false;osBuild='1';edition='Synthetic'};display=@{};runtimes=@{};files=@([pscustomobject]@{relativePath='play.exe';exists=$true;sha256='A';zoneIdentifier=$false;aclFingerprintSha256='X'});compatibility=@{};securityProducts=@();run=[pscustomobject]@{events=@();modules=@()}}
  $owner=$base|ConvertTo-Json -Depth 10|ConvertFrom-Json;$server=$base|ConvertTo-Json -Depth 10|ConvertFrom-Json;$owner.files[0].sha256='B'
  $comparison=Get-RevoraRankedDifferences $owner $server;Assert ($comparison.mostLikelyCause-eq'Critical/startup file differs') 'deterministic ranking';Assert (@($comparison.rankedDifferences).Count-ge 1) 'ranked evidence'
  $ownerPath=Join-Path $root 'owner.json';$serverPath=Join-Path $root 'server.json';$output=Join-Path $root 'comparison';$owner|ConvertTo-Json -Depth 10|Set-Content $ownerPath -Encoding UTF8;$server|ConvertTo-Json -Depth 10|Set-Content $serverPath -Encoding UTF8
  & (Join-Path $PSScriptRoot 'Compare-RevoraOwnerEnvironments.ps1') -OwnerPcJson $ownerPath -WindowsServerJson $serverPath -OutputDirectory $output|Out-Null;Assert (Test-Path (Join-Path $output 'revora-environment-comparison.json')) 'comparison JSON';Assert (Test-Path (Join-Path $output 'revora-environment-comparison.txt')) 'comparison text'
  $client=Join-Path $root 'Synthetic Client';New-Item (Join-Path $client 'ini') -ItemType Directory|Out-Null;[IO.File]::WriteAllText((Join-Path $client 'ini\CommonTip.lua'),'')
  $oldLocation=Get-Location;Set-Location $root;$env:REVORA_OWNERDIFF_SYNTHETIC='TEST_ONLY'
  try{
    $cases=@(
      @{Name='absent relative output directory';Path='.\Absent Output';Adapters=0;Expected=(Join-Path $root 'Absent Output')},
      @{Name='existing relative output directory';Path='.\Existing';Adapters=1;Expected=(Join-Path $root 'Existing')},
      @{Name='absolute output directory';Path=(Join-Path $root 'Absolute');Adapters=3;Expected=(Join-Path $root 'Absolute')},
      @{Name='path containing spaces';Path='.\Output With Spaces';Adapters=1;Expected=(Join-Path $root 'Output With Spaces')}
    );New-Item (Join-Path $root 'Existing') -ItemType Directory|Out-Null
    foreach($case in $cases){& (Join-Path $PSScriptRoot 'Collect-RevoraOwnerEnvironment.ps1') -ClientRoot $client -Profile DX9 -OutputDirectory $case.Path -SyntheticDisplayAdapterCount $case.Adapters;Assert (Test-Path (Join-Path $case.Expected 'revora-owner-environment.json')) $case.Name;Assert (Test-Path (Join-Path $case.Expected 'revora-owner-environment-summary.txt')) $case.Name;Assert (@(Get-ChildItem $case.Expected -Filter '*.tmp' -Force).Count-eq 0) 'temporary cleanup';$written=Get-Content (Join-Path $case.Expected 'revora-owner-environment.json') -Raw|ConvertFrom-Json;Assert (@($written.run.graphicsModules).Count-eq 0) 'synthetic graphics module boundary';Assert ($written.run.graphicsModuleStatus-eq'NotObserved') 'unavailable observation classification';$summaryText=Get-Content (Join-Path $case.Expected 'revora-owner-environment-summary.txt') -Raw;Assert ($summaryText-match'Profile-local d3d9 observation: Unavailable') 'unavailable summary';Assert ($summaryText-notmatch'Profile-local d3d9 loaded: False') 'no invalid negative evidence';"PASS $($case.Name)"}
    $blockedParent=Join-Path $root 'not-a-directory';[IO.File]::WriteAllText($blockedParent,'x');try{& (Join-Path $PSScriptRoot 'Collect-RevoraOwnerEnvironment.ps1') -ClientRoot $client -Profile DX9 -OutputDirectory (Join-Path $blockedParent 'Report') -SyntheticDisplayAdapterCount 1;throw 'expected preflight failure'}catch{Assert ($_.Exception.Message -match "Collector stage 'OutputPreflight' failed") 'preflight stage';Assert ($_.Exception.Message -notmatch [regex]::Escape($root)) 'preflight path redaction'}
  }finally{Remove-Item Env:REVORA_OWNERDIFF_SYNTHETIC -ErrorAction SilentlyContinue;Set-Location $oldLocation}
  'PASS zero/one/multiple collection normalization';'PASS output preflight failure stage and redaction';'PASS file evidence and redaction';'PASS synthetic process observation';'PASS deterministic comparison and single cause';'PASS comparison output contract';'PASS Windows PowerShell 5.1 report writing'
  $collectorSource=Get-Content (Join-Path $PSScriptRoot 'Collect-RevoraOwnerEnvironment.ps1') -Raw
  Assert ($collectorSource -match 'Env_DX8\\Conquer\.exe' -and $collectorSource -match 'Env_DX9\\Conquer\.exe') 'dual-profile successor paths'
  Assert ($collectorSource -match 'conquerProcesses' -and $collectorSource -match 'successorDeadline') 'successor lifetime model'
  'PASS authorized DX9-to-DX8 successor lifetime model'
  $windowStart=[DateTime]'2026-09-01T10:00:00Z';$windowEnd=$windowStart.AddMinutes(2);$clean='E:\Conquer\7630-original\Env_DX9\Conquer.exe';$old='E:\Conquer\Revora7630\Env_dx9\Conquer.exe'
  $exact=[pscustomobject]@{TimeCreated=$windowStart.AddSeconds(30);Message="Faulting application path: $clean";EvidenceValues=@($clean);TargetProcessId=1234}
  $historical=[pscustomobject]@{TimeCreated=$windowStart.AddSeconds(30);Message="Faulting application path: $old";EvidenceValues=@($old);TargetProcessId=1234}
  $wrongPid=[pscustomobject]@{TimeCreated=$windowStart.AddSeconds(30);Message="Faulting application path: $clean";EvidenceValues=@($clean);TargetProcessId=9999}
  $stale=[pscustomobject]@{TimeCreated=$windowStart.AddMinutes(-5);Message="Faulting application path: $clean";EvidenceValues=@($clean);TargetProcessId=1234}
  Assert (Test-RevoraEventCorrelation $exact @($clean) @(1234) $windowStart $windowEnd) 'exact event correlation'
  Assert (-not(Test-RevoraEventCorrelation $historical @($clean) @(1234) $windowStart $windowEnd)) 'historical root rejection'
  Assert (-not(Test-RevoraEventCorrelation $wrongPid @($clean) @(1234) $windowStart $windowEnd)) 'wrong PID rejection'
  Assert (-not(Test-RevoraEventCorrelation $stale @($clean) @(1234) $windowStart $windowEnd)) 'stale event rejection'
  'PASS exact-root path PID and observation-window event correlation'
}finally{Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue}
