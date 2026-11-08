# Critical launch integrity and optional repair

> Owner-Test bridge note: when the explicitly non-production DX9 Bridge proof is enabled, `LauncherAssets/RevoraBridge.dll` and `LauncherAssets/RevoraBridgeLoader.exe` are additional signed CriticalLaunchSet entries. They are root launcher assets, never profile DLL substitutions. Production configuration keeps the bridge disabled. See `DX9_BRIDGE_OWNER_TEST_POC.md`.

## Launch gate

Normal startup never enumerates or hashes the full repair catalog. Launch requires a valid signed client manifest and approved launch contract, a successful profile-specific `CriticalLaunchSet` check, a valid signed update-index check with no mandatory patch, no pending installation/rollback, and no already-running owned game process. Optional updates do not block.

A missing or malformed `RevoraVersion.ini` does not trigger a repair scan. Update discovery may establish the baseline from the signed installed manifest. If signed update status cannot be established, launch fails closed without scanning the catalog.

## CriticalLaunchSet

| Profile | File | Justification |
|---|---|---|
The direct-profile L1.1 Owner-Test manifest no longer classifies `play.exe` or `AutoP.exe` as launch-critical because normal launch explicitly bypasses both. They remain repair-catalog evidence only.
| DX8 | `Env_DX8/Conquer.exe` (18,534,160 bytes; `F417A25AB8B037D37619E304618BE615F7645CAF39F760EFF98D988796EE4E1E`) | Approved DX8 profile executable, launched directly with structured `blacknull` from `Env_DX8` in L1.1 Owner-Test. |
| DX9 | `Env_DX9/Conquer.exe` (18,594,824 bytes; `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE`) | Approved DX9 profile executable, launched directly with structured `blacknull` from `Env_DX9` in L1.1 Owner-Test. |

Read-only measurement against the local finalized staging tree on 2026-08-31 hashed both files (37,128,984 bytes total) in 248.596 ms on this host. No client executable was started. Actual launcher time varies with storage and cache state.

No legacy DLL is classified as critical without evidence. The two root bootstrap files are shared critical entries; the profile executables remain independent and cannot be substituted across environments. A future Revora-owned authentication/bootstrap module must be explicitly added when it exists.

## Repair Catalog

All other declared files, including `sound/login.mp3`, are repair content. `Verify & Repair Client` is explicit optional maintenance. It reports normalized paths and mismatch categories and plans only damaged or missing declared entries. Unknown files are neither selected nor deleted. Repair downloads require signed package metadata and use transactional installation.

Legacy full-verification receipts are repair history only and never a launch gate. Migration records that critical readiness passed while the full repair scan is stale or not requested; it never claims the full tree is verified.

## Incremental updates

The signed index includes `mandatory`. Package metadata binds channel, source/target versions, archive size/hash, added/replaced file hashes, deliberate deletions, target manifest, expiry/revocation and signature. Extraction rejects absolute, traversal, UNC, reserved, duplicate/case-colliding, link and undeclared entries.

Installation uses `RevoraUpdates/Downloads`, `Staging` and `Rollback`. Post-install verification hashes only affected files plus both profiles' CriticalLaunchSets and confirms declared deletions. `RevoraVersion.ini` advances atomically only after success. Failure restores the rollback journal and disables launch. Completed old downloads do not block.

## RevoraVersion.ini

Clean launcher publication includes a strict channel-appropriate
`RevoraVersion.ini` template. Private Owner-Test packaging replaces it with the
Owner-Test channel value before signing, and the transactional installer
journals it like every other installed file. It is never first created after
the rest of installation has already completed. Incremental updates continue
to advance it atomically only after successful affected-file and critical-set
verification.

```ini
[Revora]
SchemaVersion=1
ClientPatch=7630
ContentVersion=1.0.0
Channel=owner-test
LastSuccessfulPatch=baseline-owner-test
```

The strict UTF-8 parser is bounded to 16 KiB, rejects duplicate/unknown sections and keys, validates semantic versions and tokens, and writes atomically. This file is discovery state, not cryptographic proof.

## Security and UI

Trust-root separation, signed metadata, path/reparse defenses, rollback, profile separation, mandatory updates and duplicate-process prevention remain fail-closed. UI states distinguish update checking, optional/mandatory updates, download/install, critical problems, optional repair and recovery. A critical failure shows its normalized relative path and category. A non-critical repair finding never disables Start Game.
