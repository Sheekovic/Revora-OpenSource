$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = [IO.File]::ReadAllText((Join-Path $projectRoot 'Network\GamePackets\JiangHu\JiangHuCalculate.cs'))
$start = $source.IndexOf('public byte GetStatusLevel()')
$end = $source.IndexOf('public byte GetValueLevel(', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Production quality method not found.' }
$method = $source.Substring($start, $end - $start)
Add-Type -TypeDefinition (@'
using System;
public sealed class JiangQualityDraw : Random {
    readonly int upper, value;
    int calls;
    public JiangQualityDraw(int upper, int value) { this.upper=upper; this.value=value; }
    public override int Next(int min, int max) {
        calls++;
        if(calls==1 && min==1 && max==15) return upper;
        if(calls==2 && min==1 && max==upper) return value;
        throw new InvalidOperationException("Production random call contract changed.");
    }
    public void Verify() { if(calls!=2) throw new InvalidOperationException("Missing quality draw."); }
}
public sealed class JiangQualityMeasurement {
    public Random Random;
'@ + $method + '}')
$weights = New-Object 'double[]' 7
$branches = 0
foreach ($upper in 1..14) {
    # System.Random.Next(1,1) returns 1; otherwise the upper bound is exclusive.
    $count = [Math]::Max(1, $upper - 1)
    foreach ($value in 1..$count) {
        $draw = New-Object JiangQualityDraw($upper, $value)
        $measurement = New-Object JiangQualityMeasurement
        $measurement.Random = $draw
        $level = $measurement.GetStatusLevel()
        $draw.Verify()
        if ($level -lt 1 -or $level -gt 6) { throw 'Invalid quality.' }
        $weights[$level] += 1.0 / 14 / $count
        $branches++
    }
}
$sum = ($weights | Measure-Object -Sum).Sum
if ([Math]::Abs($sum - 1) -gt 1e-12) { throw 'Incomplete probability coverage.' }
if ($weights[6] -le 0.10) { throw 'Reassess proposed pill policy: baseline changed.' }
"Production branches evaluated: $branches"
foreach ($level in 1..6) { 'Quality {0}: {1:F9}%' -f $level, (100 * $weights[$level]) }
'Normal Ultra probability: {0:F9}%' -f (100 * $weights[6])
'Additional 10% rescue of non-Ultra outcomes would yield: {0:F9}%' -f (100 * ($weights[6] + (1 - $weights[6]) * 0.10))
