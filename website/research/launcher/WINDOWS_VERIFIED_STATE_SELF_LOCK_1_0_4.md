# Windows verified-state self-lock in Launcher 1.0.4

## Genuine evidence and root cause

The schema-v2 owner report for Launcher `1.0.4` records a complete scan of all `12,831` managed
files, followed by `System.IO.IOException`, HRESULT `0x80070020`, native error
`ERROR_SHARING_VIOLATION` (32). The sanitized stack terminates at
`Revora.Launcher.Core.VerifiedStateCache.WriteAsync`, called by `MainWindow.ScanAsync` and
`MainWindow.InitializeAsync`.

The failing file was launcher-owned `.revora-launcher/verified-state.json.new`; no managed client
file or external process caused this failure. `VerifiedStateCache.WriteAsync` used an await-using
declaration whose scope included `File.Move`. The temporary output remained open with
`FileShare.None` while Windows was asked to rename it. Windows therefore rejected Revora's own
move. This is deterministic Windows handle-lifetime behavior.

## Correction

Launcher `1.0.5` uses an explicit nested await-using scope. The writer retains `FileShare.None`,
asynchronous I/O, `WriteThrough`, explicit flush, byte zeroing, cleanup, cancellation, and
fail-closed replacement. Only after the nested scope has disposed the output does the atomic
`File.Move(..., overwrite: true)` execute. A process-wide per-cache-path semaphore serializes
overlapping writes to the same `.new` file.

Cache-write exceptions now identify only operation `verified-state-cache-write` and safe path
`.revora-launcher/verified-state.json`; absolute paths remain excluded.

## Write-before-move lifetime audit

| Component | Write/verify lifetime before mutation | Result |
|---|---|---|
| `VerifiedStateCache.WriteAsync` | Explicit nested output scope now ends before `File.Move` | Corrected |
| `MainWindow.FetchManifestAsync` | `File.WriteAllBytesAsync` completes and releases its internal handle before `File.Move` | Safe |
| `TransactionalPatchApplier.WriteJournalAsync` | `File.WriteAllBytesAsync` completes before `File.Move` | Safe |
| `TransactionalPatchApplier.MaterializeAsync` | Input/content/output scopes end before verification; verification ends when the method returns; caller moves afterward | Safe |
| `TransactionalPatchApplier.ApplyAsync` | Previous-file hash stream is scoped inside its `if` block and ends before backup/replacement moves | Safe |
| `PatchDownloadEngine.DownloadOneAsync` | Response and partial output nested scopes end before partial-object move | Safe |
| `PatchDownloadEngine.VerifyFileAsync` | Decompression writers end before verification and cleanup | Safe |
| `AtomicUpdate.StageAsync` | Download and verification scopes both end before partial-file move | Safe |
| `BootstrapperEngine.ApplyAsync` | Download writers and verification readers complete before live/rollback moves | Safe |
| `LauncherDiagnostics.ExportAsync` | Archive is returned as its final file; no rename, replacement, rollback, or deletion occurs while open | Not a write-then-move path |

## Closed CI gap

The earlier Windows workflow exercised sharing violations while hashing managed client files, but
its verified-cache test supplied an in-memory cache directly to `DesiredStateScanner`. It never
called `VerifiedStateCache.WriteAsync`. The WPF regression validated progress reset and version
presentation but did not run a successful `MainWindow.ScanAsync` through cache persistence and the
`Ready` transition. Therefore the one Windows-only rename boundary that failed in production was
absent from CI.

The corrected gate directly performs first write, overwrite, full read/validation, immediate
rename/delete, cancellation cleanup, repeated and concurrent serialized writes on a real Windows
filesystem. A separate WPF integration starts with a complete valid client fixture, invokes the
real `MainWindow.ScanAsync`, persists the cache, reaches `Ready`, runs a second scan, accepts the
passive-file cache, and proves no repair/download is required.
