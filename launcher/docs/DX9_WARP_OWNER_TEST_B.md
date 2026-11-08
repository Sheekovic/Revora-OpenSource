# DX9 Microsoft WARP Owner-Test B

This private package binds the launcher payload and DX9 critical profile to a
single forced-WARP proxy hash. The UI and redacted diagnostics identify
`Microsoft WARP OWNER-TEST`. Applying settings preserves that exact payload;
it neither deploys the native proxy nor permits an unapproved proxy.

The proxy requires the Windows WARP adapter, creates a D3D12 WARP device and
direct queue, and passes them to the operating-system D3D9On12 API. It has no
hardware or native-D3D9 fallback and ships no Microsoft graphics binaries.

The completed owner run proved WARP device creation but not compatibility:
multiple index-buffer creations returned `D3DERR_INVALIDCALL`, no Present was
attempted, and DX9 exited. The official bootstrap then selected DX8, which
reached a stable login window. WARP is retained only as Owner-Test evidence and
is removed from production plans.

Install only after restoring Variant A to the native proxy. Close PlayRevora,
play.exe, AutoP.exe, and Conquer.exe, then run:

```powershell
.\Install-OwnerTestB.ps1 -ClientRoot 'E:\Conquer\Revora7630'
```

Run exactly one normal official-bootstrap test. Collect the existing v5
load-image evidence plus `Env_DX9/revora_d3d9.jsonl`. From the separately
verified v5 collector directory, use Windows PowerShell 5.1:

```powershell
Set-ExecutionPolicy -Scope Process RemoteSigned
.\Collect-RevoraOwnerEnvironment.ps1 -ClientRoot 'E:\Conquer\Revora7630' -Profile DX9 -OutputDirectory '.\OwnerPcWarpBReport'
```

Wait for its readiness prompt, launch PlayRevora normally, and allow the one
run to finish. The compatible v5 observer must report `graphicsModuleStatus`
as `Observed` and distinguish the profile-local proxy, `d3d9on12.dll`,
`d3d12.dll`, and `dxgi.dll`. Stop on protection
rejection, a backend other than WARP, fallback evidence, or an earlier
repeatable exit. Restore exactly with:

```powershell
.\Restore-OwnerTestB.ps1 -ClientRoot 'E:\Conquer\Revora7630'
```

The signed critical manifest covers the official bootstrap, independently
approved DX8/DX9 executables, unchanged RevoraBridge components, and the exact
WARP proxy in both launcher payload and `Env_DX9`. The optional full Repair
Catalog is not included and is not a package gate.
