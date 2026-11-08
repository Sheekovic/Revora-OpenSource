# Patch 7630 ConquerLoader comparative audit

## Scope and provenance

This was a bounded source-level audit. No downloaded executable or DLL was loaded, built, or run. No client, hook, injector, installer, or release asset was executed. The checkout remains outside PlayRevora under ignored research storage.

| Field | Value |
|---|---|
| Repository | `https://github.com/OpenConquerOrg/ConquerLoader.git` |
| Local research path | `C:/Users/Administrator/Desktop/Revora/research/isolated/ConquerLoader` |
| Branch | `master` |
| Commit | `068898b70722a61dd3539028f458e5379b3fe9ad` |
| Commit date | 2026-03-25 |
| Submodules | None (`git submodule status` returned no entries) |
| Working tree | Clean |

There is no repository-root license file. `ConquerHook/LICENSE` and `ConquerCipherHook/LICENSE` are GPL-3.0 license texts, so reuse of those covered components would require GPL-3.0 compliance, including corresponding-source and derivative-work obligations when conveyed. The audit found no explicit license grant covering the remaining managed launcher tree or embedded third-party binaries. Those portions must therefore not be copied merely because the repository is public. This report records ideas and behavior, not source adoption.

## Tracked binary inventory

These are Git-tracked binary payloads. They were hashed as inert files only.

| Path | Bytes | SHA-256 |
|---|---:|---|
| `ConquerLoader/ConquerLoader/Resources/CLHook_Legacy.dll` | 140,800 | `7887EB0D81B96A40F1583E7613DA2BF0DEC9160780713AB1893234530E573355` |
| `ConquerLoader/ConquerLoader/Resources/COFlashFixer_DLL.dll` | 952,832 | `B3971D0C5EE3B6A86A0A1ABCB4C3A0EFEE17AB8CF3599AAEC7F450C2C6EDCD94` |
| `ConquerLoader/ConquerLoader/Resources/COServerDat.dll` | 44,544 | `779829563CF7286E800DAD6619221E27F3F792DF5A0BBD84210FF3D1847BDDB9` |
| `ConquerLoader/ConquerLoader/Resources/ConquerCipherHook.dll` | 119,808 | `607A13F113CEDA3D7C8FC78A3F6B8E2008F4EB34176786F75D9DB2BF1D186750` |
| `ConquerLoader/ConquerLoader/Resources/TQAnp.dll` | 5,092,112 | `5D8C33D05AC7A1A37BCCAB58E65F093694463BD02ACE35ED527E5830959628DE` |
| `ConquerLoader/ConquerLoader/Resources/VC_redist.x64.exe` | 14,876,264 | `015EDD4E5D36E053B23A01ADB77A2B12444D3FB6ECCEFE23E3A8CD6388616A16` |
| `ConquerLoader/ConquerLoader/Resources/VC_redist.x86.exe` | 14,319,952 | `E830C313AA99656748F9D2ED582C28101EAAF75F5377E3FB104C761BF3F808B2` |

The solution also embeds images, icons, XML templates, and language resources, but no other tracked files with executable/library extensions. `COHook.dll` is not tracked by that name: `MainLite.xaml.cs` writes embedded `COServerDat` bytes to the selected working directory under the name `COHook.dll`. None of these payloads is suitable for Revora distribution or profile mixing.

## A. Process startup

The active WPF implementation is `ConquerLoader/Forms/WPF/MainLite.xaml.cs`.

1. It starts from a configured `ServerConfiguration.ExecutableName`, defaulted by the wizard to `Conquer.exe` (`WizardWindow.xaml.cs:68-92`).
2. It initially combines that name with the loader startup path (`MainLite.xaml.cs:467-469`).
3. If `Env_DX8/<ExecutableName>` exists, it changes the executable and working directory to DX8 (`495-516`).
4. If `UseDirectX9`, its coarse DirectX check, and `Env_DX9/<ExecutableName>` all succeed, DX9 overwrites that choice (`518-535`).
5. It calls managed `Process.Start(ProcessStartInfo)` with the selected `Conquer.exe`, its containing directory, and the single concatenated argument string `blacknull` (`574-579`). It does not launch `play.exe` or `AutoP.exe`.
6. It does not request suspended creation. The managed launcher has no `CreateProcess`, `CreateProcessAsUser`, or `ShellExecute` call for the game. Its injector subsequently uses `OpenProcess`, `VirtualAllocEx`, `WriteProcessMemory`, and `CreateRemoteThread(LoadLibraryA)` in `CLCore/Helpers/Injector.cs`.
7. A non-null `Process.Start` return is logged as launched. No descendant, window, named pipe, stable-lifetime, or readiness signal is required. There is no distinct early-exit classification.
8. It enables `Process.Exited`; when no process with the configured executable name remains, `ConquerProc_Exited` terminates the loader (`MainLite.xaml.cs:610-613,711-717`). This is name-based monitoring, not normalized-path or parent/child validation.

This startup model conflicts with the owner-verified Patch-7630 route. PlayRevora's signed root-bootstrap and descendant-monitoring design is materially safer and better evidenced.

## B. Patch/version routing

The version is not reliably detected from the selected PE. A new server wizard reads root `Version.dat`; if it contains one line, it rewrites the file to `99999` and preserves the prior number as a `#`-prefixed line, then treats that comment as the real version (`WizardWindow.xaml.cs:68-91`). Thereafter `ServerConfiguration.ServerVersion` is user/configuration data.

Documented routing in the root README is:

| Range | Documented route | Source-level assessment |
|---|---|---|
| `<5717` | no decrypted `server.dat`; inject `CLHook.dll` | Legacy interception, offsets/configuration, unsuitable for Revora. |
| `5717-6186` | decrypted data plus `COFlashFixer`, `ConquerCipherHook`, `COHook` | Injection and authentication compatibility behavior. Unsuitable. |
| `6187-6736` | decrypted data plus `ConquerCipherHook`, `COHook` | Injection and authentication compatibility behavior. Unsuitable. |
| `>6736` | legacy `CLHook.dll`; described as limited/incomplete | Documentation only; not tested Patch-7630 support. |

The current WPF code is internally less precise than the README: local `useDecryptedServerDat` is initialized to `true`; raw `COServer.dat` generation is bounded to 5095-6736, but later cipher/hook injection conditions are broad. `LegacyHookEnabled` defaults to false and no assignment enabling it was found. Therefore the README's `>6736` route should not be treated as a proven implementation contract at this commit.

An all-history search found no `7630` reference. No 7630 branch, tag, filename, configuration, offset, issue reference in the checkout, or commit subject was found. The only explicit ceiling is `MaxVersionUseRAWServerDat = 6736` (`CLCore/Constants.cs`). The hook README claims compatibility only through 6660, while the root README claims tested loader compatibility through 6736. Patch 7630 is **not evidenced**.

## C. Rendering profile selection

ConquerLoader recognizes `Env_DX8` and `Env_DX9`, but it bypasses the authentic selector rather than coordinating it:

- DX8 is selected whenever its configured executable exists.
- DX9 replaces DX8 when `ServerConfiguration.UseDirectX9` is true, `Core.DirectXVersion() >= 9`, and the DX9 executable exists.
- `Core.DirectXVersion` largely equates Windows 7+ with DirectX 11; it does not inspect an adapter or profile-specific startup outcome (`Core.cs:89-121`).
- It copies/writes hook configuration and, in some ranges, a replacement `TQAnp.dll` into the selected environment.
- It writes screen options and server selection to root-relative `ini/GameSetup.ini` (`MainLite.xaml.cs:427-450`; `ServersDatGenerator.cs:188-196`). It does not evidence `PlaySetUP.ini` or a supported `play.exe`/`AutoP.exe` DX selector.

The high-level lesson—keep DX8/DX9 paths explicit and validate availability—is safe. Its actual selection and file-copy mechanism is not applicable to the proven `play.exe -> AutoP.exe -> Env_DX9/Conquer.exe` chain.

## D. Named pipe and bootstrap

No source or documentation reference to named pipes, `\\.\Pipe\zfDxCheck-%08x`, `play.exe`, `AutoP.exe`, or the final eight-hex-digit argument was found. ConquerLoader passes only literal `blacknull` directly. It neither creates nor observes the authentic bootstrap state.

Accordingly, it provides no explanation or reusable implementation for the captured commands:

```text
AutoP.exe Conquer.exe blacknull
Env_dx9/Conquer.exe blacknull 00002c49
```

Revora must continue to leave generation and ownership of that dynamic argument and named pipe entirely to the signed official bootstrap.

## E. Server routing

ConquerLoader's routing is interception-based:

- It writes `CLHook.ini` containing login/game hosts, ports, server name, and a version-dependent server-name memory address (`MainLite.xaml.cs:393-421`).
- It generates `COServer.dat` for the 5095-6736 range or a gzip `Servers.dat` outside that range (`ServersDatGenerator.cs:160-180`).
- It writes embedded hook binaries into the client/profile directory and injects them after direct client startup.
- The legacy hook source contains version branching and memory modification/scanning. These are precisely the version-specific mechanisms excluded from Revora.
- It may rewrite `Version.dat` to suppress normal version behavior. That is not a safe configuration contract.

The only reusable high-level concept is modeling a server entry (name, account/game host and ports) separately from process launch. Its concrete transport redirection is rejected.

`OpenConquerOrg/ConquerDatCipher` describes support only for DAT files through 6711, below 7630. `OpenConquerOrg/ConquerServerPatcher` is MIT-labeled and explicitly patches a client for custom `Server.dat`. They may merit a separate static provenance/data-format audit only if lawful Patch-7630 signed-configuration evidence appears. Neither should be adopted now: one has an explicit pre-7630 ceiling and the other is a client patcher, contrary to the current official-bootstrap boundary.

## F. Security and licensing classification

| Component/behavior | Classification | Reason |
|---|---|---|
| Explicit profile model and pre-launch plugin boundary | Safe architectural lesson | Separation of selection/preflight from launch is useful if independently implemented with signed inputs. |
| Managed process exit event | Potentially reusable as an idea | Standard .NET concept, but its name-only implementation is weaker than PlayRevora's normalized-path descendant observation. |
| `CLHook.dll` / `CLHook_Legacy.dll` | Injection/hooking dependent; version-specific and unsuitable; not evidenced for 7630 | Host/server interception plus memory/version assumptions; >6736 is documented as limited/incomplete. |
| `COHook.dll` (embedded `COServerDat.dll`) | Injection/hooking dependent; version-specific and unsuitable | Loads a custom raw server-data path through injection; supported range ends at 6736. |
| `ConquerCipherHook.dll` | Authentication-bypass dependent; injection dependent; unsuitable | README explicitly describes password-transmission compatibility; no 7630 evidence. |
| `COFlashFixer.dll` | Injection dependent; version-specific | Documented only for 5717-6186. |
| `TQAnp.dll` | Proprietary binary dependent; authentication-bypass dependent | Binary-only payload written into profiles; README calls it modified for authentication/password bypass compatibility. Never reuse or substitute it. |
| Hard-coded server-name addresses and memory scanning | Version-specific and unsuitable | Highest listed addresses target 66xx-era logic, not 7630; prohibited in Revora. |
| Raw/decrypted `COServer.dat` generation | Potential format research only; not evidenced for 7630 | Bounded in code to <=6736 and coupled to hooks. |
| AutoPatch plugin package/state concept | Safe architectural lesson only | Ordered prelaunch work and persisted state are useful ideas, but its external archive-extractor execution and unsigned trust model do not meet Revora's signed transactional policy. |
| Tracked hook/redistributable binaries | Proprietary/opaque binary dependent | No execution or redistribution basis established; excluded from Revora. |

## Comparison with PlayRevora

| Concern | ConquerLoader at pinned commit | PlayRevora current evidence/model |
|---|---|---|
| Executable launched | Direct selected `Conquer.exe` | Signed root `play.exe` |
| Arguments | Literal concatenated `blacknull` | None; official bootstrap owns descendant arguments |
| Working directory | Selected executable's directory | Client root for `play.exe` |
| DX selection | Loader chooses DX8 then optionally DX9 | Signed `AutoP.exe` chooses; PlayRevora observes expected approved descendant |
| Server endpoint | Hook INI, generated DAT, injected/replacement DLLs | Fixed endpoint architecture; no hook adoption; signed configuration decision remains separate |
| Named pipe/bootstrap | No support found | Owned opaquely by signed `play.exe`/`AutoP.exe` chain |
| Authentication | Cipher hook and replacement `TQAnp` behavior | No legacy bypass; Identity/Grant work remains paused |
| Monitoring | Started-process event and process-name count | Parent/descendant observation, normalized approved path, bootstrap timeout, early/normal exit states |
| Integrity/update | Writes embedded DLLs; plugin archives; no comparable signed critical gate | Signed CriticalLaunchSet, signed index/packages, transactional staging and rollback, optional repair catalog |
| Patch 7630 evidence | None; documented tested ceiling 6736 | Owner-captured authentic 7630 process chain and exact signed hashes |
| Maintenance/security risk | High: injection, offsets, binary replacement, ambiguous >6736 path | Lower: official signed bootstrap, no protected-state emulation, fail-closed critical checks |

## Reusable lessons and rejected mechanisms

Safe lessons are limited to explicit server/profile models, pre-launch validation boundaries, user-visible selection, and process lifetime observation. PlayRevora already implements stronger forms of those concepts. No source code needs to be copied, avoiding both provenance ambiguity and GPL coupling.

Rejected mechanisms are direct `Conquer.exe blacknull` launch, remote-thread injection, custom hook DLLs, modified `TQAnp`, password/cipher hooks, hard-coded addresses, client-version rewriting, raw server-data interception, external redistributable execution, and copying any binary between root/DX profiles.

## Recommendation

**A. USE_OFFICIAL_BOOTSTRAP**

Keep PlayRevora's current signed `play.exe` entry point and descendant monitoring. ConquerLoader supplies no authentic Patch-7630 startup, pipe, dynamic-argument, authentication, or server-routing evidence that changes the decision. The safest next action is an Owner-Test validation of the already implemented official-bootstrap launcher behavior, without beginning Identity/Grants until that startup path is confirmed by the owner.
