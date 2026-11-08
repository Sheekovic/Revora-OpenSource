# Direct profile launch L1.1

Owner validation on the clean Patch-7630 baseline proved both direct contracts reach a stable login screen. No `play.exe`, `AutoP.exe`, second argument, named-pipe emulation, Bridge, WARP, or D3D9On12 is required. Direct launch is authoritative.

## Contracts

| Profile | Executable | Working directory | Structured arguments |
|---|---|---|---|
| DX8 | `Env_DX8/Conquer.exe` | `Env_DX8` | `blacknull` |
| DX9 | `Env_DX9/Conquer.exe` | `Env_DX9` | `blacknull` |

`ProcessStartInfo.ArgumentList` supplies the single argument. Shell execution and elevation remain disabled. Root `Conquer.exe`, `play.exe`, and `AutoP.exe` are never selected. The launcher validates the selected executable through its profile-specific signed CriticalLaunchSet and rejects duplicate approved DX8/DX9 processes.

Stage 1 deliberately supplies no second argument, named pipe, credential, grant, injection, or Bridge operation. The Owner-Test configuration disables the existing Bridge proof. Native D3D9 remains selected. The launcher records `direct_profile_launch_requested` and `direct_profile_process_started`; the latter reports the local bootstrap as `not_required`. Runtime confirmation must establish that neither official updater process appears and that the selected process reaches a stable login window.

## Gate and rollback

If `blacknull` alone exits before stable readiness, Identity and Grants L2 remains blocked. Stage 2 may then inspect only the already-authorized local correlation/pipe boundary described by the owner; it may not alter protection, networking, authentication, or proprietary code.

Rollback is the previous signed Owner-Test launcher package and manifest. The package contains no proprietary binaries and never mutates either profile during packaging. Installation is into the approved client root; the included `RevoraVersion.ini` is Owner-Test state and is replaced atomically by the existing transaction mechanism.

## Owner test

1. Extract the private package into `E:\Conquer\7630-original`, outside both environment directories.
2. Select DX9 explicitly and start once. Confirm `Conquer.exe blacknull`, working directory `Env_DX9`, no `play.exe`/`AutoP.exe`, stable login screen, and duplicate rejection.
3. Close DX9, select DX8 explicitly, and repeat for `Env_DX8`.
4. Return redacted launcher diagnostics and process/path/module observations. Do not provide credentials or packet contents.

No proprietary client was executed while producing this milestone.
