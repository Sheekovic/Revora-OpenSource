param([Parameter(Mandatory=$true)][string]$ClientRoot)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ExpectedKeyId = 'revora-owner-test-windows-2026-08-31'
$ExpectedPublicKey = 'MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAECPc2HbKjroApJlEEkpQV/Bvt7iobTQLsyyY4BY6qaKJ6Cr9GT5VKjBUzgSyopdmMXav9q24q/DRTRlrdLdQ05Q=='
$Forbidden = @('regkey.dat', 'Env_DX9/regkey.dat')

function Normalize-Relative([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path) -or $Path.Contains(':')) { throw 'Invalid relative path.' }
    $normalized = $Path.Replace('\','/')
    if (@($normalized.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -ne 0) { throw 'Invalid relative path.' }
    if ($Forbidden -contains $normalized) { throw 'Mutable licensing state is outside this package contract.' }
    return $normalized
}

function Resolve-Under([string]$Root, [string]$Relative) {
    $normalized = Normalize-Relative $Relative
    $candidate = [IO.Path]::GetFullPath((Join-Path $Root $normalized.Replace('/',[IO.Path]::DirectorySeparatorChar)))
    if (-not $candidate.StartsWith($Root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escapes the client root.' }
    return $candidate
}

function Write-Atomic([string]$Source, [string]$Destination) {
    $parent = Split-Path $Destination
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $temporary = Join-Path $parent ('.revora-install-' + [guid]::NewGuid().ToString('N') + '.tmp')
    Copy-Item -LiteralPath $Source -Destination $temporary
    Move-Item -LiteralPath $temporary -Destination $Destination -Force
}

$package = [IO.Path]::GetFullPath($PSScriptRoot)
$root = [IO.Path]::GetFullPath($ClientRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Client root is unavailable.' }
if ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Client root cannot be a reparse point.' }

$tool = Join-Path $package 'tools\Revora.BinaryDelta.dll'
& dotnet $tool verify-package $package $ExpectedKeyId $ExpectedPublicKey
if ($LASTEXITCODE -ne 0) { throw 'Signed package verification failed.' }

$contract = Get-Content -LiteralPath (Join-Path $package 'install-contract.json') -Raw | ConvertFrom-Json
if ($contract.schemaVersion -ne 1 -or $contract.packageVersion -notmatch '^revora-7630-personal-pc-[1-9][0-9]*$') { throw 'Unsupported installation contract.' }
foreach ($processName in @('PlayRevora.Launcher','Revora-Conquer','Conquer','play','AutoP')) {
    if (Get-Process -Name $processName -ErrorAction SilentlyContinue) { throw "Close $processName before installing." }
}

$source = Resolve-Under $root $contract.binaryDelta.sourcePath
$sourceInfo = Get-Item -LiteralPath $source
if ($sourceInfo.Length -ne [long]$contract.binaryDelta.sourceSize -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $contract.binaryDelta.sourceSha256) {
    throw 'The original DX9 executable does not match the approved Patch-7630 source identity.'
}

$backup = Join-Path (Split-Path $root) ('Revora-Personal-Patch-Backup-' + (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))
$staging = Join-Path $backup 'staging'
New-Item -ItemType Directory -Path $staging | Out-Null
$state = [ordered]@{ schemaVersion=1; clientRoot=$root; packageVersion=$contract.packageVersion; replaced=@(); created=@() }

try {
    $destinations = @($contract.files | ForEach-Object { Normalize-Relative $_.destination }) + @(Normalize-Relative $contract.binaryDelta.destinationPath)
    if (($destinations | Sort-Object -Unique).Count -ne $destinations.Count) { throw 'Duplicate installation destination.' }
    foreach ($relative in $destinations) {
        $destination = Resolve-Under $root $relative
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            $saved = Join-Path $backup ('files\' + $relative.Replace('/',[IO.Path]::DirectorySeparatorChar))
            New-Item -ItemType Directory -Force -Path (Split-Path $saved) | Out-Null
            Copy-Item -LiteralPath $destination -Destination $saved
            $state.replaced += [ordered]@{ path=$relative; size=(Get-Item $destination).Length; sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash }
        } else { $state.created += $relative }
    }

    $deltaOutput = Join-Path $staging 'Env_DX9\Revora-Conquer.exe'
    New-Item -ItemType Directory -Force -Path (Split-Path $deltaOutput) | Out-Null
    & dotnet $tool apply (Join-Path $package $contract.binaryDelta.deltaPath) $source $deltaOutput
    if ($LASTEXITCODE -ne 0) { throw 'Executable delta application failed.' }
    if ((Get-FileHash -LiteralPath $deltaOutput -Algorithm SHA256).Hash -ne $contract.binaryDelta.destinationSha256) { throw 'Executable output identity mismatch.' }

    foreach ($file in $contract.files) {
        $relative = Normalize-Relative $file.destination
        $from = Join-Path $package (Normalize-Relative $file.packagePath).Replace('/',[IO.Path]::DirectorySeparatorChar)
        $to = Join-Path $staging $relative.Replace('/',[IO.Path]::DirectorySeparatorChar)
        New-Item -ItemType Directory -Force -Path (Split-Path $to) | Out-Null
        Copy-Item -LiteralPath $from -Destination $to
        if ((Get-Item $to).Length -ne [long]$file.size -or (Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash -ne $file.sha256) { throw "Staged file mismatch: $relative" }
    }

    $statePath = Join-Path $backup 'rollback-state.json'
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    $backupEntries = @(Get-ChildItem -LiteralPath (Join-Path $backup 'files') -File -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path=$_.FullName.Substring((Join-Path $backup 'files').Length + 1).Replace('\','/'); size=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    [IO.File]::WriteAllText((Join-Path $backup 'backup-manifest.json'), ([ordered]@{schemaVersion=1;files=$backupEntries}|ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))

    Write-Atomic $deltaOutput (Resolve-Under $root $contract.binaryDelta.destinationPath)
    foreach ($file in $contract.files) {
        $relative = Normalize-Relative $file.destination
        Write-Atomic (Join-Path $staging $relative.Replace('/',[IO.Path]::DirectorySeparatorChar)) (Resolve-Under $root $relative)
    }
    foreach ($file in $contract.files) {
        $relative = Normalize-Relative $file.destination
        $destination = Resolve-Under $root $relative
        if ((Get-Item $destination).Length -ne [long]$file.size -or (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $file.sha256) { throw "Installed file mismatch: $relative" }
    }
    if ((Get-FileHash -LiteralPath (Resolve-Under $root $contract.binaryDelta.destinationPath) -Algorithm SHA256).Hash -ne $contract.binaryDelta.destinationSha256) { throw 'Installed executable mismatch.' }
    Remove-Item -LiteralPath $staging -Recurse -Force
    [IO.File]::WriteAllText((Join-Path $root '.revora-personal-patch-backup'), $backup, [Text.Encoding]::ASCII)
    Write-Output "REVORA_PERSONAL_PATCH_INSTALLED"
    Write-Output "Backup: $backup"
    Write-Output "Rollback: & '$package\Rollback-RevoraPersonalPcPatch.ps1' -ClientRoot '$root' -Backup '$backup'"
}
catch {
    if (Test-Path -LiteralPath (Join-Path $backup 'rollback-state.json')) {
        & (Join-Path $package 'Rollback-RevoraPersonalPcPatch.ps1') -ClientRoot $root -Backup $backup
    }
    throw
}
