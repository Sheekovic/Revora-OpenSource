[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$bin = Join-Path $env:TEMP ('RevoraGraphicsObserver-'+[Guid]::NewGuid().ToString('N'))
try {
    $observer = & (Join-Path $PSScriptRoot 'Build-GraphicsModuleObserver.ps1') -OutputDirectory $bin
    $targetSource = Join-Path $bin 'SyntheticGraphicsTarget.cs'
    [IO.File]::WriteAllText($targetSource, 'using System; using System.Runtime.InteropServices; using System.Threading; class T { [DllImport("d3d9.dll")] static extern IntPtr Direct3DCreate9(uint v); static int Main(){ Direct3DCreate9(32); Thread.Sleep(5000); return 0; } }',[Text.UTF8Encoding]::new($false))
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
    $target = Join-Path $bin 'SyntheticGraphicsTarget.exe'
    & $compiler /nologo /target:exe /platform:x86 /optimize+ /warnaserror+ /out:$target $targetSource
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic target compilation failed.' }
    $process = Start-Process -FilePath $target -PassThru
    try {
        Start-Sleep -Milliseconds 500
        $result = (& $observer --pid $process.Id) | ConvertFrom-Json
        if ($result.status -ne 'Observed') { throw "Observer status: $($result.status)" }
        if (@($result.modules | Where-Object { [IO.Path]::GetFileName($_.path) -ieq 'd3d9.dll' }).Count -ne 1) { throw 'Synthetic d3d9 module was not observed.' }
    } finally { if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force } }
    $missing = (& $observer --pid 4294967294) | ConvertFrom-Json
    if ($missing.status -notin @('ProcessUnavailable','SnapshotFailed')) { throw "Unexpected missing-process status: $($missing.status)" }
    'PASS x86 Toolhelp graphics module observation'
    'PASS explicit unavailable-process classification'
} finally { Remove-Item -LiteralPath $bin -Recurse -Force -ErrorAction SilentlyContinue }
