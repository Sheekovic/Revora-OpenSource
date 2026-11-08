# DX9 Bridge Owner-Test proof of concept

## Scope and status

This milestone proves the Revora-owned loader and bridge boundary against a Revora-owned synthetic 32-bit process. It does not claim that Patch 7630 accepts the bridge. The proprietary client was not executed during development. Real-client acceptance and lifetime comparison remain a private owner test.

The production launcher configuration defaults `dx9BridgeOwnerTestEnabled` to `false`. The private Owner-Test package alone enables it and trusts the existing Owner-Test signing root. Phase 2 endpoint redirection and Phase 3 Launch Grants are excluded.

The PoC package uses a dedicated six-entry signed manifest, not the 82,000-file Repair Catalog. An intermediate packaging attempt incorrectly retained the earlier full-client manifest and appended the Bridge entries. Runtime launch readiness would still have selected only entries marked `critical`, but the operator command `verify-tree` is deliberately a complete-manifest verifier and therefore began hashing all 82,178 root entries plus profile files. That process was stopped and the retained catalog was removed from the package. Package validation now covers exactly the six CriticalLaunchSet entries listed below. The optional Repair Catalog is neither packaged nor scanned for this PoC.

## Target and selection boundary

The helper targets only a process already selected by the official signed chain:

```text
PlayRevora -> play.exe -> AutoP.exe -> Env_DX9/Conquer.exe
```

Before loading the bridge, the launcher and x86 helper require all of the following:

- the observed process is a descendant of the launcher-started `play.exe` chain;
- the normalized process path is exactly the selected client root plus `Env_DX9/Conquer.exe`;
- the file size is `18,594,824` bytes;
- SHA-256 is `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE`;
- the helper and bridge are signed CriticalLaunchSet entries in the Owner-Test manifest.

The production helper contains no hash-bypass option. The separately named synthetic helper accepts only an explicitly supplied SHA-256 through a test-only environment variable and is never packaged.

## Injection and Bridge ABI

`RevoraBridgeLoader.exe` is a 32-bit, narrow loader. It opens only the validated target PID, allocates the absolute bridge path, and invokes `LoadLibraryW` with `CreateRemoteThread`. It then invokes two exported functions by RVA:

```cpp
DWORD WINAPI RevoraBridgeInitialize(const wchar_t* pipeName);
DWORD WINAPI RevoraBridgeCleanup(void* reserved);
```

Version 1 performs no hooks, network changes, packet inspection, password processing, persistence, or global injection. `DllMain` only disables thread notifications. Initialization opens the per-launch local pipe and writes exactly:

```json
{"event":"bridge_ready","version":"1.0.0-owner-test","profile":"dx9"}
```

The launcher creates a random per-launch named pipe under `PlayRevora.Bridge.*` with `CurrentUserOnly` access, accepts one connection, validates the exact bounded marker, and records only a redacted structured result. Cleanup performs deterministic Bridge cleanup, after which the helper calls `FreeLibrary`. This unload is intentional for Phase 1; a future approved persistent feature would require a separately reviewed lifetime contract.

Expected diagnostic on success:

```text
bridge_ready profile=Dx9 result=success
```

No pipe contents, credentials, tokens, packet payloads, or hardware identifiers are logged.

## Synthetic validation

The native build produces a Revora-owned x86 sleep target and a separately compiled test-only helper. The test loads and unloads the exact Bridge, receives the marker, verifies a zero helper exit, and confirms that the synthetic target remains alive. Managed fake-process tests prove that DX9 invokes the Bridge only after an approved descendant appears, DX8 never invokes it, and Bridge failure prevents launch readiness. No proprietary executable is used.

## Private Owner-Test procedure

1. Back up the client root and ensure no game process is running.
2. Extract the private package into the Patch-7630 client root, outside `Env_DX8` and `Env_DX9`.
3. Do not move or copy `RevoraBridge.dll` into either profile directory.
4. Start `PlayRevora.Launcher.exe` normally, without elevation.
5. Choose Automatic or DX9 and click Start Game. PlayRevora starts root `play.exe`; the signed official bootstrap retains ownership of profile selection and its opaque startup state.
6. Confirm that diagnostics contain the exact redacted `bridge_ready` event and that the client lifetime is not shorter than the owner's current no-Bridge baseline.
7. Exit normally and retain only redacted timestamps, exit classification, and the marker result.

If the official bootstrap selects DX8, the bridge is not loaded. PlayRevora does not force or patch the official selector.

## Stop condition and rollback

Stop immediately if TQSafe/licensing reports a Bridge-specific rejection, the launcher reports an unexpected path/hash, the marker is absent, or the client exits earlier than the established baseline in a repeatable Bridge-correlated way. Preserve the exact redacted timestamp, UI message, approved process path category, and exit classification. Do not add stealth, evasion, protection bypasses, runtime hooks, or broader injection.

Rollback is bounded: close the launcher/game, restore the prior private Owner-Test launcher package or set `dx9BridgeOwnerTestEnabled` to `false`, and remove only `LauncherAssets/RevoraBridge.dll` and `LauncherAssets/RevoraBridgeLoader.exe`. No profile DLL is replaced and no persistent system state is created.
