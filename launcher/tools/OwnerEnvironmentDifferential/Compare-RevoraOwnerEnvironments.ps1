[CmdletBinding()]
param([Parameter(Mandatory)][string]$OwnerPcJson,[Parameter(Mandatory)][string]$WindowsServerJson,[string]$OutputDirectory=(Join-Path $PWD 'RevoraOwnerComparison'))
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Revora.OwnerDiff.psm1') -Force
$owner=Get-Content -LiteralPath $OwnerPcJson -Raw -Encoding UTF8|ConvertFrom-Json
$server=Get-Content -LiteralPath $WindowsServerJson -Raw -Encoding UTF8|ConvertFrom-Json
if($owner.schemaVersion-ne 1-or$server.schemaVersion-ne 1){throw 'Unsupported report schema.'}
$result=Get-RevoraRankedDifferences $owner $server
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$jsonPath=Join-Path $OutputDirectory 'revora-environment-comparison.json';$textPath=Join-Path $OutputDirectory 'revora-environment-comparison.txt'
[IO.File]::WriteAllText($jsonPath,($result|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
$lines=@("Most likely cause: $($result.mostLikelyCause)","Confidence: $($result.confidence)","",'Ranked evidenced differences:')+@($result.rankedDifferences|ForEach-Object{"[$($_.score)] $($_.category): $($_.evidence)"})
[IO.File]::WriteAllLines($textPath,$lines,(New-Object Text.UTF8Encoding($false)))
Write-Host $lines[0];Write-Host "Saved: $jsonPath";Write-Host "Saved: $textPath"
