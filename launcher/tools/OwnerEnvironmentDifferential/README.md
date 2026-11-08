# Revora Owner Environment Differential Collector

Read-only Windows diagnostics for comparing one owner-PC run with one Windows Server run. It never starts Conquer, reads process memory, captures packet contents, changes security configuration, or modifies client files.

Run from a normal, non-elevated PowerShell window:

```powershell
Set-ExecutionPolicy -Scope Process RemoteSigned
.\Collect-RevoraOwnerEnvironment.ps1 -ClientRoot 'S:\R E V O R A' -Profile DX9 -OutputDirectory '.\OwnerPcReport'
```

When prompted, start PlayRevora normally. The collector observes only `play.exe`, `AutoP.exe`, and the selected `Conquer.exe` under the supplied client root, then stops after Conquer exits or the bounded timeout expires. Run the same command on the server with its local client root and a different output directory.

Compare:

```powershell
.\Compare-RevoraOwnerEnvironments.ps1 -OwnerPcJson '.\OwnerPcReport\revora-owner-environment.json' -WindowsServerJson '.\ServerReport\revora-owner-environment.json' -OutputDirectory '.\Comparison'
```

Expected collection outputs:

- `revora-owner-environment.json`
- `revora-owner-environment-summary.txt`

Expected comparison outputs:

- `revora-environment-comparison.json`
- `revora-environment-comparison.txt`

The comparison ranks only differences present in the two reports and emits one most-likely category. A high rank is a prioritization aid, not proof of causation; reproduce any candidate before changing the client or operating system.

## Privacy and integrity

User names, user-profile paths, the client-root path, and the official bootstrap's opaque eight-hex session argument are redacted. Collection is restricted to the specified client tree/process chain and relevant event windows. No credentials, packet bodies, memory, private keys, browser data, or unrelated process inventory are collected.

The scripts are deliberately readable. The private package includes `owner-test-integrity.signed.json`, a detached ECDSA P-256/SHA-256 signature made with the existing Windows-protected Owner-Test key. This is an Owner-Test integrity marker, not Authenticode and not production trust.

## Windows PowerShell compatibility and output

Version 5 explicitly supports Windows PowerShell 5.1 with StrictMode enabled. Relative output paths are resolved once against the caller's current working directory; absolute paths remain absolute. The final directory is created and write-tested before the observation prompt. Reports are written to private temporary files in that directory and atomically renamed. Failed writes remove temporary/partial output and report only the failing stage and exception type.

The collector invokes the packaged x86 `Revora.GraphicsModuleObserver.exe`, which uses the documented Toolhelp module snapshot API rather than PowerShell's `Get-Process.Modules`. It polls the selected x86 Conquer process throughout its lifetime for `d3d9.dll`, `d3d9on12.dll`, `d3d11.dll`, `d3d12.dll`, and `dxgi.dll`. `run.graphicsModules` records the normalized path category, size, SHA-256, Authenticode state, and first-seen timestamp without reading process memory. `run.graphicsModuleStatus` distinguishes `Observed`, `AccessDenied`, `CrossArchitecturePartialCopy`, `ProcessUnavailable`, timeout, and other snapshot failures. The summary never reports `loaded=false` unless enumeration completed successfully.
