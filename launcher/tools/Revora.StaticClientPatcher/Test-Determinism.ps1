param(
    [Parameter(Mandatory=$true)][string]$ApprovedConquer,
    [string]$Configuration = 'Release'
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$manifest=Join-Path $PSScriptRoot 'revora-conquer-patch.json'
$work=Join-Path ([IO.Path]::GetTempPath()) ('revora-static-patch-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
  $a=Join-Path $work 'a.exe';$b=Join-Path $work 'b.exe';$restored=Join-Path $work 'restored.exe'
  dotnet run --project (Join-Path $PSScriptRoot 'Revora.StaticClientPatcher.csproj') -c $Configuration -- apply $manifest $ApprovedConquer $a
  if($LASTEXITCODE){throw 'first patch failed'}
  dotnet run --project (Join-Path $PSScriptRoot 'Revora.StaticClientPatcher.csproj') -c $Configuration -- apply $manifest $ApprovedConquer $b
  if($LASTEXITCODE){throw 'second patch failed'}
  $ha=(Get-FileHash -Algorithm SHA256 -LiteralPath $a).Hash;$hb=(Get-FileHash -Algorithm SHA256 -LiteralPath $b).Hash
  if($ha -ne $hb){throw 'patch is nondeterministic'}
  dotnet run --project (Join-Path $PSScriptRoot 'Revora.StaticClientPatcher.csproj') -c $Configuration -- reverse $manifest $a $restored
  if($LASTEXITCODE){throw 'reverse failed'}
  if((Get-FileHash -Algorithm SHA256 -LiteralPath $ApprovedConquer).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $restored).Hash){throw 'reverse mismatch'}
  $tampered=[IO.File]::ReadAllBytes($ApprovedConquer);$tampered[1024]=$tampered[1024]-bxor 1;[IO.File]::WriteAllBytes((Join-Path $work 'tampered.exe'),$tampered)
  $prior=$ErrorActionPreference;$ErrorActionPreference='Continue'
  dotnet run --project (Join-Path $PSScriptRoot 'Revora.StaticClientPatcher.csproj') -c $Configuration -- apply $manifest (Join-Path $work 'tampered.exe') (Join-Path $work 'rejected.exe') 2>$null
  $tamperedExit=$LASTEXITCODE;$ErrorActionPreference=$prior
  if($tamperedExit -eq 0){throw 'tampered source accepted'}
  "PASS deterministic=$ha reverse=approved unknown-hash=rejected"
} finally { if(Test-Path -LiteralPath $work){Remove-Item -LiteralPath $work -Recurse -Force} }
