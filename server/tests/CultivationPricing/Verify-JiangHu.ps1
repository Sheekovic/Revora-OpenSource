$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = [IO.File]::ReadAllText((Join-Path $projectRoot 'Network\GamePackets\JiangHu\JiangHuCalculate.cs'))
$start = $source.IndexOf('public static uint GetTrainingCost(')
$end = $source.IndexOf('public static void ExtraStudy(', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Price method not found.' }
$method = $source.Substring($start, $end-$start)
$configSource = [IO.File]::ReadAllText((Join-Path $projectRoot 'System\Config.cs'))
$config = [regex]::Matches($configSource, 'public static (?:uint|ushort) JiangHu(?:TrainingStepCp|TrainingMaxCp) = \d+;')
if ($config.Count -ne 2) { throw 'Training price settings missing.' }
Add-Type -TypeDefinition ('using System; public static class Config {' + (($config | ForEach-Object {$_.Value}) -join [Environment]::NewLine) + '} public static class JiangPriceChecks {' + $method + '}')
foreach ($row in @(@(0,10),@(1,20),@(48,490),@(49,500),@(50,500),@([uint16]::MaxValue,500))) {
    if ([JiangPriceChecks]::GetTrainingCost([uint16]$row[0]) -ne $row[1]) { throw ('Wrong price at count ' + $row[0]) }
}
'6 production JiangHu price calculations passed. Database-backed training, restoration admission and recovery checks run in Verify-PotencyHunt.ps1 -JadesOnly.'
