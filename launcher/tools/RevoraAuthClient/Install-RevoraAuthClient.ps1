param([Parameter(Mandatory=$true)][string]$ClientRoot)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($ClientRoot);$payload=Join-Path $PSScriptRoot 'payload'
$targets=@('Env_DX9\Revora-Conquer.exe','Env_DX9\RevoraAuthClient.dll','client-manifest.signed.json')
foreach($relative in $targets){$destination=Join-Path $root $relative;$source=Join-Path $payload $relative;if(!(Test-Path -LiteralPath $source)){throw "Missing payload: $relative"};$parent=Split-Path $destination;New-Item -ItemType Directory -Force -Path $parent|Out-Null;$temporary=$destination+'.revora-new';Copy-Item -LiteralPath $source -Destination $temporary -Force;if(Test-Path -LiteralPath $destination){$swap=$destination+'.revora-swap';[IO.File]::Replace($temporary,$destination,$swap);Remove-Item -LiteralPath $swap -Force}else{[IO.File]::Move($temporary,$destination)}}
'Revora Auth Client v1 installed.'
