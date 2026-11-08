param([string]$BuildPath = 'obj\chi-refill-validation\bin\Revora.exe')
$ErrorActionPreference = 'Stop'
$root = 'C:\Revora\DisposablePostgres\moon-box-candidate'
$project = Split-Path $PSScriptRoot -Parent
$source = [IO.Path]::GetFullPath([IO.Path]::Combine($project,$BuildPath))
$buildRoot = [IO.Path]::GetFullPath((Join-Path $project 'obj')) + '\'
if (!$source.StartsWith($buildRoot,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($source) -ne 'Revora.exe') { throw 'Select a private Revora validation build under this project obj directory.' }
$target = Join-Path $root 'game\Revora.exe'
$manifestPath = Join-Path $root 'game-runtime-manifest.json'
if (@(Get-NetTCPConnection -State Listen -LocalPort 19558,19559,19619 -ErrorAction SilentlyContinue).Count) { throw 'Stop the isolated candidate before refreshing.' }
foreach ($path in @($root,(Join-Path $root 'game'),$target,$manifestPath,$source)) {
    if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected candidate/build path.' }
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$entry = @($manifest.files | Where-Object path -eq 'Revora.exe')
if ($entry.Count -ne 1 -or (Get-FileHash -LiteralPath $target).Hash -ne $entry[0].sha256) { throw 'Candidate executable does not match its manifest.' }
foreach ($dll in @($manifest.files | Where-Object { $_.path -match '^[^/\\]+\.dll$' })) {
    if ((Get-FileHash -LiteralPath (Join-Path (Split-Path $source -Parent) $dll.path)).Hash -ne $dll.sha256) { throw 'Dependency changed; review the complete candidate first.' }
}
$hash = (Get-FileHash -LiteralPath $source).Hash
if ($hash -eq $entry[0].sha256) { Write-Output 'Candidate already uses this build.'; return }
$bytes = [IO.File]::ReadAllBytes($source)
$pe = [BitConverter]::ToInt32($bytes,60)
if ($pe -lt 64 -or $pe + 6 -gt $bytes.Length -or [BitConverter]::ToUInt32($bytes,$pe) -ne 0x4550 -or [BitConverter]::ToUInt16($bytes,$pe+4) -ne 0x14c) { throw 'Expected x86 PE candidate.' }
$backup = Join-Path $root ('Revora-before-' + $entry[0].sha256 + '.exe')
$manifestBackup = $backup + '.manifest.json'
$staged = Join-Path $root 'game\Revora.refresh.exe'
$manifestStaged = Join-Path $root 'game-runtime-manifest.refresh.json'
# Reuse an identical retained executable; never duplicate or overwrite a different preimage.
$exeBackupTarget = $backup
if (Test-Path -LiteralPath $backup) {
    if ((Get-Item -LiteralPath $backup).Attributes -band [IO.FileAttributes]::ReparsePoint -or (Get-FileHash -LiteralPath $backup).Hash -ne $entry[0].sha256) { throw 'Retained executable backup does not match the current candidate.' }
    $exeBackupTarget = [NullString]::Value
}
$currentManifestHash = (Get-FileHash -LiteralPath $manifestPath).Hash
if (Test-Path -LiteralPath $manifestBackup) {
    if ((Get-Item -LiteralPath $manifestBackup).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected manifest backup.' }
    if ((Get-FileHash -LiteralPath $manifestBackup).Hash -ne $currentManifestHash) { $manifestBackup += '.' + $currentManifestHash + '.json' }
}
$manifestBackupTarget = $manifestBackup
if (Test-Path -LiteralPath $manifestBackup) {
    if ((Get-Item -LiteralPath $manifestBackup).Attributes -band [IO.FileAttributes]::ReparsePoint -or (Get-FileHash -LiteralPath $manifestBackup).Hash -ne $currentManifestHash) { throw 'Retained manifest backup differs from the current preimage.' }
    $manifestBackupTarget = [NullString]::Value
}
foreach ($path in @($staged,$manifestStaged)) { if (Test-Path -LiteralPath $path) { throw 'Refresh staging artifact already exists; inspect before retrying.' } }
$commit = (& git -C $project rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
$changes = @{}
foreach ($path in @(& git -C $project diff --name-only HEAD -- '*.cs' '*.csproj')) {
    $changes[$path] = (Get-FileHash -LiteralPath (Join-Path $project $path)).Hash
}
$manifest.source_commit = $commit
$manifest.source_changes = $changes
$manifest.clean_build_claimed = $false
$entry[0].sha256 = $hash
$entry[0].bytes = $bytes.Length
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestStaged -Encoding UTF8
Copy-Item -LiteralPath $source -Destination $staged
if ((Get-FileHash -LiteralPath $staged).Hash -ne $hash) { throw 'Staged copy mismatch.' }
[IO.File]::Replace($staged,$target,$exeBackupTarget)
try { [IO.File]::Replace($manifestStaged,$manifestPath,$manifestBackupTarget) }
catch { Copy-Item -LiteralPath $backup -Destination $target -Force; throw }
Write-Output ('Private candidate refreshed: ' + $commit + '; executable SHA256 ' + $hash + '; previous executable and manifest preserved.')
