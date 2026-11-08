$ErrorActionPreference = 'Stop'
$root = Join-Path $env:RUNNER_TEMP 'revora-trace-x86'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$fixture = Join-Path $root 'trace_fixture_x86.exe'
$marker = Join-Path $root 'marker.bin'
$record = Join-Path $root 'event.bin'
& cl.exe /nologo /W4 /WX /O2 /MT /D_CRT_SECURE_NO_WARNINGS /Fe:$fixture "$PSScriptRoot\trace_fixture_x86.c"
if ($LASTEXITCODE -ne 0) { throw 'x86 fixture compilation failed' }
if (Test-Path $marker) { Remove-Item -LiteralPath $marker -Force }
if (Test-Path $record) { Remove-Item -LiteralPath $record -Force }
$process = Start-Process -FilePath $fixture -ArgumentList @($marker, $record) -Wait -PassThru -NoNewWindow
if ($process.ExitCode -ne 0) { throw "x86 fixture exited $($process.ExitCode)" }
$bytes = [IO.File]::ReadAllBytes($record)
$markerBytes = [IO.File]::ReadAllBytes($marker)
if ($bytes.Length -ne 28) { throw 'record length mismatch' }
if ($markerBytes.Length -ne 28 -or [BitConverter]::ToUInt32($markerBytes, 0) -ne 0x484d5652) { throw 'entry marker mismatch' }
$magic = [BitConverter]::ToUInt32($bytes, 0)
$stage = [BitConverter]::ToUInt32($bytes, 4)
$recordProcessId = [BitConverter]::ToUInt32($bytes, 12)
$thread = [BitConverter]::ToUInt32($bytes, 16)
$stamp = [BitConverter]::ToInt64($bytes, 20)
if ($magic -ne 0x46345652 -or $stage -ne 1 -or $recordProcessId -ne $process.Id -or $thread -eq 0 -or $stamp -le 0) {
    throw 'x86 transport metadata mismatch'
}
$stream = [IO.File]::Open($record, 'Open', 'ReadWrite', 'None'); $stream.Dispose()
Write-Host "x86 startup transport passed for PID $recordProcessId"
