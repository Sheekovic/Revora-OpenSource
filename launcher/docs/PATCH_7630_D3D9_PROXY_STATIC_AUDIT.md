# Patch 7630 D3D9 proxy static audit

## Decision gate — passed

The owner-PC v4 report did not prove the module path. It recorded `EnumerationUnavailable:ProcessCommandException`; therefore its empty `graphicsModules` array and `Profile-local d3d9 loaded: False` summary were invalid negative evidence. Version 5 replaced `Get-Process.Modules` with a packaged x86 Toolhelp snapshot observer.

The v5 owner report conclusively observed the profile-local proxy at
`2026-09-01T14:01:07.4851218Z`, size `248,320`, SHA-256
`1F2400BFE993079477C9C4879BFFD32F40D5D46C2DF6CCBE55E70719EABD9442`,
category `ProfileLocalD3D9`. Windows native `d3d9.dll` and `dxgi.dll` loaded
later; `d3d9on12.dll`, `d3d11.dll`, and `d3d12.dll` were not observed. The
status was `Observed`, and the process lifetime was 12.889 seconds. This closes
the load-path evidence gate and confirms that the baseline proxy used native
D3D9 rather than a translation backend.

Owner-Test Variant A is now built separately in `Revora-DisplayProxy`: it
requests the operating-system D3D9On12 backend without native fallback. A
Revora-owned synthetic x86 test observed `d3d9on12.dll`, `d3d12.dll`, and
`dxgi.dll`, then created a device and presented successfully. Patch-7630
compatibility remains an owner-test question. Variant B remains gated on a
repeatable Variant-A failure or incompatibility report.

Historical server evidence is stronger but environment-specific. `Env_DX9/revora_d3d9.jsonl` beside the matching proxy records `direct3d_create9`, `create_device_succeeded`, and successful sampled `Present` calls through frame 2400. The reviewed source derives that log directory from the loaded proxy module itself. This proves the proxy executed and rendered successfully in that server run; it does not prove the owner-PC load path.

## Exact current binary

- Path class: `Env_DX9/d3d9.dll`
- Size: `248,320` bytes
- SHA-256: `1F2400BFE993079477C9C4879BFFD32F40D5D46C2DF6CCBE55E70719EABD9442`
- PE: x86 / PE32 (`Machine 0x014C`)
- Authenticode: not signed
- Export surface: 23 functions at ordinals 16 through 38, including `Direct3DCreate9`, `Direct3DCreate9Ex`, `Direct3DCreate9On12`, and `Direct3DCreate9On12Ex`

The staged binary is byte-identical to the reviewed `Revora-DisplayProxy` Release output.

## What it implements

The proxy explicitly resolves the Windows system-directory `d3d9.dll`. Its `Direct3DCreate9` export calls the system runtime's ordinary `Direct3DCreate9`, wraps `IDirect3D9`, and intercepts `CreateDevice`. Its device facade intercepts `Reset` and `Present`; all other `IDirect3DDevice9` vtable slots forward to the native D3D9 device. Configuration can override windowed presentation, backbuffer dimensions, borderless window placement, and logging.

The `Direct3DCreate9On12*` exports are only optional forwarders. The proxy's own `Direct3DCreate9` path does not call them. It imports or creates no D3D11 device, D3D12 device/queue, DXGI adapter, or WARP adapter. Therefore it is a native-D3D9 observation/presentation wrapper, not a D3D9-to-D3D11/D3D12 translation layer and not a WARP implementation.

## Owner-Test alternatives after the load gate

Each future variant must occupy only `Env_DX9/d3d9.dll`, be mutually exclusive, be covered by a dedicated signed Owner-Test manifest, and retain the current DLL as the rollback artifact. DX8 and RevoraBridge remain untouched.

1. **D3D9-to-D3D11:** no reviewed production-suitable implementation is selected. The open `GabrielMajeri/d3d9-to-11` project explicitly says it cannot run real games. dgVoodoo2 implements D3D11/12 translation but requires a separate redistribution/license and provenance decision. Neither is packaged.
2. **OS D3D9On12:** technically viable as a Revora-owned x86 proxy variant whose `Direct3DCreate9` calls the operating system's `Direct3DCreate9On12`. Microsoft documents D3D9On12 as the D3D9-to-D3D12 mapping layer and advises applications not to ship a private replacement for the OS `d3d9on12.dll`. A future variant must use the OS component and fail closed when unavailable.
3. **WARP diagnostic:** technically viable through a D3D12 WARP adapter (`IDXGIFactory4::EnumWarpAdapter`) supplied to the D3D9On12 creation path. It is a diagnostic baseline, not an automatic fallback. It must report adapter/device creation without logging render payloads.

No alternative binary has been built or packaged because the owner-PC load gate remains open.

## Sources

- Microsoft D3D9On12 repository and architecture: https://github.com/microsoft/D3D9On12
- Microsoft WARP guide: https://learn.microsoft.com/windows/win32/direct3darticles/directx-warp
- Microsoft `IDXGIFactory4::EnumWarpAdapter`: https://learn.microsoft.com/windows/win32/api/dxgi1_4/nf-dxgi1_4-idxgifactory4-enumwarpadapter
- Microsoft DLL search-order documentation: https://learn.microsoft.com/windows/win32/dlls/dynamic-link-library-search-order
- Incomplete D3D9-to-D3D11 research implementation: https://github.com/GabrielMajeri/d3d9-to-11
