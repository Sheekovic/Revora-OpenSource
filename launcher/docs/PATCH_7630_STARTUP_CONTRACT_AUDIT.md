# Patch 7630 startup-contract audit

## Scope and provenance

This was a bounded static audit. No proprietary executable or DLL was run, loaded, patched, hooked or bypassed. The owner's `S:\R E V O R A` volume was not mounted on the audit host, so that installation was not directly inspected. Static findings below come from the locally assembled original Patch-7630 tree at `C:\Users\Administrator\Downloads\Revora-Client-Staging\Conquer-7630\assembled`, constructed from the signed original 7589 distribution plus signed patches through 7630. Its approved profile executable hashes exactly match the owner's launcher contract.

Owner runtime evidence is recorded separately: direct DX8 and DX9 starts with `blacknull`, correct profile working directories, splash display, roughly 1013-second lifetime, and exit code 0. A zero exit code proves orderly process termination only; it does not prove a standalone launch contract.

## Distribution launch route

The root `Conquer.exe` is not a PE executable. It is a 41-byte text sentinel containing: `Click on Player.exe to log into the game.` No `Player.exe` is present in the assembled 7630 tree. No `.lnk` files exist, so there are no shortcut targets, arguments or working directories to report.

The root contains these plausible launch/bootstrap programs:

| File | SHA-256 | Authenticode / static identity | Evidence |
|---|---|---|---|
| `AutoP.exe` | `DDA4062DCC385734DC82267D6E363B345CC48492DB9D8536CD68B1878B01E14D` | Valid NetDragon signature; original name `DxCheck.exe` | Contains `Env_dx8`, `Env_dx9`, `blacknull`, `\\.\Pipe\zfDxCheck-%08x`, `CreateNamedPipeA`, `ConnectNamedPipe`, `DisconnectNamedPipe`, `CreateProcessAsUserA`, and command format strings containing an additional `%08x` value. Reads `Autopatch/config.ini` and `ini/DxInfo.ini`. |
| `play.exe` | `95940F4666A8B29B4C1DA32C32E27D2346C0A78BE09871B574102809F24D806A` | Valid NetDragon signature; description `99 Security Logger` | Contains `Env_dx8`, `Env_dx9`, `NdSafeLogin.exe`, `CreatePipe`, `WaitNamedPipeW`, `SetNamedPipeHandleState`, and process-creation imports. This is evidence of a security/login bootstrap role, not a proven standalone command. |
| `nd_lanucher.exe` | `A64C9CDF8FBFBC39173C70944CB11A78F506A87AF3516A19E86898BE9EF99282` | Embedded signer exists but current Authenticode status is not valid; product `ConquerPlatform` | Contains `\\.\pipe\P2PDownPipe`, `WaitNamedPipeA`, and process-creation imports. Its exact role in the 7630 launch chain is unproven. |
| `gw/play.exe` | `1D081F3476628E2AE0E8CFE7F93183AAAD58D3BC44EE2B7C8F03B4E907F47085` | Valid NetDragon signature; description `play` | Contains `CreatePipe` and process-creation imports. No configuration or shortcut proves it is the active 7630 entry point. |
| `AutoPatch.exe` | `27568CF465C61C7DC86E6EC89664408D2D4FA61926F0F133A4032D9691A9A3E9` | Unsigned legacy patcher | `AutoPatch/config.ini` names `patch=AutoPatch.exe` and `main=Conquer.exe`, but root `Conquer.exe` is the text sentinel. This configuration alone does not establish the final launch command. |

`readme.txt` explicitly identifies `play v1.1.7.6` and `autop v1.0.0.7` and mentions SDK telemetry plus play/autop startup-mode reporting. `AutoPatch/eventValue.ini` defines DX events for DX9 startup, DX9-to-DX8 fallback, and failure of both. These distribution artifacts establish an intended root bootstrap that selects a profile; they do not prove which executable the user should invoke or the complete argument/IPC contract.

## Profile and protection evidence

| Artifact | DX8 SHA-256 | DX9 SHA-256 |
|---|---|---|
| `Conquer.exe` | `F417A25AB8B037D37619E304618BE615F7645CAF39F760EFF98D988796EE4E1E` | `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE` |
| `license.dll` | `12CCA47D01E343890E85B6FDEB834948818FE59645A2D78041F4B64672A2DBD6` | same |
| `TQPlat.dll` | `E46149B7ED0A3BC142FD22CADF1C30FF6A42204B08F6B5920221A2DF0E69815A` | same |
| `TQAnp.dll` | `7D50029DA473CAFC19DB8A2F776E7989B78E8F39379521A1F137213F09307E32` | same |
| `TqNDProtect.dll` | `C78CF9CD91E47429AD12F5CE639497762D485FC6D6B38E601871DE17B7DA66F2` | same |

All listed profile DLLs have valid NetDragon Authenticode signatures. They were not loaded.

The prior bounded debugger experiment observed `license.dll` privileged-instruction exceptions and the TQSafe message `Sorry, this game cannot be run without a license key`, then `STATUS_DLL_INIT_FAILED`, before authentication ordinals were reached. Independently, the existing DX9 startup log states `Failed to open the appointed named pipe! GetLastError: 2` at `gamemain.cpp` line 554. Static `AutoP.exe` evidence supplies a matching named-pipe family and shows that its constructed client command contains `blacknull` plus an additional eight-hex-digit value.

Together these facts prove that `Conquer.exe blacknull` is incomplete for the audited distribution. They do **not** prove whether the extra value is itself a license, a pipe instance identifier, a process/session correlation value, or another bootstrap parameter. No environment-variable requirement was found. Registry API imports and generic Windows policy/COM registry strings exist in `AutoP.exe`, but no specific required game registry value was evidenced. Account, version and patch endpoints exist in configuration, but no evidence shows that network unavailability alone caused the observed orderly exits.

## Configuration and network observations

- `version.dat` is exactly `7630`.
- `AutoPatch.dat` and `AutoPatch/SocketConfig.ini` point to historical public patch infrastructure. These are update dependencies and must not be contacted during Revora testing.
- `ini/DxInfo.ini` configures an official telemetry/DX endpoint and a static application value. It is not evidence of the client-login contract.
- `AutoPatch/config.ini` declares the patcher and root `Conquer.exe` sentinel, a 20-second startup timeout and process-name checks.
- Startup logs show Lua/resource warnings and the missing named pipe; they do not contain a proven alternative argument, environment variable or registry requirement.

## Launcher impact

The current CriticalLaunchSet remains unchanged, as required. However, its DX8/DX9 argument contracts must now be treated as unproven for launch authorization. The launcher must not claim standalone compatibility merely because either signed executable exists and returns exit code 0.

The only non-invasive diagnostic change made by this audit is to record observed process lifetime in milliseconds and classify exit status as `clean_exit` or `nonzero_exit`. `clean_exit` means only that Windows returned zero; it is not labeled startup success.

## Safest next step

Acquire the missing official launch-chain evidence without executing or bypassing protected code: inspect the owner's original installer-created desktop/start-menu shortcuts and any original `Player.exe`/bootstrap file that is absent from the assembled tree, or obtain a vendor-documented command/installation layout. Record hashes and shortcut target/arguments/working directory. If the official bootstrap is available and lawful to distribute, perform a separately authorized, network-contained black-box process/IPC observation without inspecting protected internals. Do not add the extra `%08x` argument or emulate the named pipe based on the current evidence.

## Owner-captured authentic startup contract

The owner subsequently captured the genuine process tree from `S:\R E V O R A`. This resolves the earlier entry-point uncertainty:

```text
play.exe
  "S:\R E V O R A\play.exe"
  -> AutoP.exe
       "S:\R E V O R A\AutoP.exe" Conquer.exe blacknull
       -> Env_dx9\Conquer.exe
            "S:\R E V O R A\Env_dx9\Conquer.exe" blacknull 00002c49
```

`AutoP.exe` recorded `Open Dx9`, `Open Client`, `Success`, `Recv Success`, and `AutoP Close`. The client did not record the earlier missing-named-pipe error. The final eight-hex-digit argument is generated dynamically by the signed bootstrap and is opaque to Revora. It is never hard-coded, guessed, persisted, intercepted, or produced by PlayRevora.

## Proven official DX9-to-DX8 fallback

The Owner-Test B run established a second authorized process route. The signed
bootstrap first created the approved `Env_DX9/Conquer.exe`. That process
initialized forced WARP but exited before Present after repeated
`D3DERR_INVALIDCALL` index-buffer creation failures. The same official AutoP
chain then created the independently approved `Env_DX8/Conquer.exe`, which
remained alive and reached the visible login window. The window title and
handle belonged to DX8, not WARP/DX9.

Accordingly, Automatic mode follows the complete approved descendant chain and
treats the stable DX8 successor as an official fallback. Explicit DX9 remains
strict unless the owner separately enables fallback. Neither launcher nor
collector manufactures the profile transition or its private bootstrap state.

## Clean native DX9 owner validation

The later clean-root validation supersedes WARP as the preferred owner path.
At `E:\Conquer\7630-original`, Automatic selected the approved native DX9
executable and the approved profile-local proxy hash `1F2400BF...9442`. DX9
reached the visible login screen and remained alive beyond 84 seconds. No DX8
successor and no `official_profile_fallback` event occurred; duplicate launch
was rejected while DX9 remained open. Classification:
`CLEAN_NATIVE_DX9_SUCCESS`, `DUPLICATE_PROTECTION_SUCCESS`,
`REAL_FALLBACK_NOT_EXERCISED`, `WARP_NOT_REQUIRED`, and
`D3D9ON12_NOT_REQUIRED`.

Fallback remains an authorized safety path, but its message is emitted only
after an actual approved DX8 descendant is observed. A WER record naming the
different historical `Revora7630` root is unrelated to this clean-root run and
must not be used as evidence against it.

The proven launcher contract is root `play.exe`, no arguments, client-root working directory, no shell execution, and no elevation. PlayRevora observes—but does not recreate—the signed `play.exe -> AutoP.exe -> Conquer.exe` chain. Direct launch of either profile executable is rejected by design.

## Profile-selection limitation

Static distribution evidence proves that `AutoP.exe` reads `ini/DxInfo.ini`, reports DX9, DX9-to-DX8 fallback, and total failure through `AutoPatch/eventValue.ini`, and selected DX9 in the captured run. No reviewed configuration field proves a supported external DX8/DX9 override. PlayRevora may retain a preference for inspection and readiness, but cannot claim to force the official bootstrap's selection. A launch succeeds only when the expected approved profile descendant appears.

## Revised verdict

`PROVEN_STANDALONE_CONTRACT`
