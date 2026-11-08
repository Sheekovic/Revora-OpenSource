# Revora Login Seam Owner-Test 1 v2

## V1 tooling failure and correction

V1 incorrectly equated successful component loading/initializer return with diagnostic readiness. Its report was opened lazily only after a seam event, so the component could emit `bridge_ready` while no report existed. Report and writer failures were not acknowledged to the launcher.

V2 resolves the report path once from the validated client root, preflights directory writeability before launching the game, and passes the absolute path in a versioned initialization contract. The observer creates the report and immediately flushes initialization, report-open, preimage, hook, worker, and readiness records. The launcher accepts only the exact `observer_ready` acknowledgement. Bounded failures use `observer_init_failed` categories without paths or secrets, and every seam event is flushed immediately.

The exact production DLL is validated using the Revora-owned x86 synthetic target. Synthetic mode is accepted only for that exact target filename and exercises the entry/return event pipeline without proprietary artifacts.

This private DX9-only observer consumes Owner Test 1. It targets only `Env_DX9/Conquer.exe` SHA-256 `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE` and verifies the exact call preimages before installing two transparent call wrappers.

- Ordinal-55 call: preferred VA `0x00DB0342`, preimage `E8 B7 CC 17 00`.
- Ordinal-63 call: preferred VA `0x00DFCC00`, preimage `E8 19 04 13 00`.

Each wrapper records a monotonic timestamp, thread ID, active-window ownership, GUI-thread status, and queue-status flags before and after invoking the original thunk with the original arguments. It never dereferences an argument, changes a packet, suppresses a call, performs network activity, or observes unrelated memory. A bounded in-memory ring is flushed immediately by a dedicated writer thread to `RevoraDiagnostics/login-seam-owner-test-1.jsonl`.

Initialization fails open: any target/preimage/patch failure restores any first patch already applied and returns an initialization error. The launcher then reports that the observer was not ready. The DLL remains loaded only for the game-process lifetime; no persistence is installed.

Owner action after extracting the private package:

```powershell
./Install-PlayRevoraOwnerTest.ps1 -ClientRoot 'E:\Conquer\7630-original'
```

Select explicit DX9, enter one disposable test account, click Login once, wait for the native result, close the game, and return only `E:\Conquer\7630-original\RevoraDiagnostics\login-seam-owner-test-1.jsonl`.

Rollback:

```powershell
./Rollback-PlayRevoraOwnerTest.ps1 -ClientRoot 'E:\Conquer\7630-original'
```
