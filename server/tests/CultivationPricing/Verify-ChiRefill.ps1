$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = [IO.File]::ReadAllText((Join-Path $projectRoot 'Game\Features\ChiWallet.cs'))
$start = $source.IndexOf('internal static uint RefillPrice(')
$end = $source.IndexOf('internal static void Refill(', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Could not locate the production refill price calculation.' }
$method = $source.Substring($start, $end - $start).Replace('internal static', 'public static')
$config = [IO.File]::ReadAllText((Join-Path $projectRoot 'System\Config.cs'))
$price = [regex]::Match($config, 'public static uint ChiFullRefillCp = (\d+);').Groups[1].Value
if ($price -ne '1000') { throw 'Default price is not the owner-requested 1,000 CPs.' }
Add-Type -TypeDefinition ('public static class RefillPriceChecks {' + $method + '}')
$cases = @(@(0,1000),@(2000,500),@(3500,125),@(3996,1),@(3997,1),@(3999,1),@(4000,0),@(4001,0),@([uint32]::MaxValue,0))
foreach ($row in $cases) {
    if ([RefillPriceChecks]::RefillPrice([uint32]$row[0],1000) -ne $row[1]) { throw ('Wrong price at Chi=' + $row[0]) }
}
if ([RefillPriceChecks]::RefillPrice(0,[uint32]::MaxValue) -ne [uint32]::MaxValue) { throw 'Price overflow.' }
'10 production price calculations passed. Durable refill payment and replay checks run in Verify-PotencyHunt.ps1 -JadesOnly.'