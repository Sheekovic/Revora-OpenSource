# Worker-thread world identifier handoff

## Proven defect

The bounded TestPlayer execution on 2026-09-05 at 05:03:13 UTC recorded
`world_handle_captured/nonzero_status_2` on the Login thread, followed by
`world_bridge_boundary/native_send_worker_thread` and
`world_bridge_identifier_comparison/does_not_match_retained`. GameHost recorded
successful DH/cipher establishment, a complete 2429, and `world_grant_rejected`.
The previous same-thread test cleared the retained identifier instead of copying
it into the worker's packet. The identifier's value is never included here.

## Correction and native ABI

`direct_native_runtime.cpp` uses `WorldHandoff` (SRWLOCK) rather than a thread-ID
guard for the Game send. Login/HTTPS/native UI dispatch still stays on its UI
thread. The state contains only the four-byte handle plus non-secret attempt
generation, PID, transport identity, socket identity, deadline and state flags.
Capture is restricted to decoded 52-byte, status-2 opcode 1637 with a nonzero
UInt32LE at +4, during the current successful authentication attempt.

The Game connect callback binds the attempt before starting the native worker.
It requires the exact native manager, Game type 2, no existing connect worker,
and the reviewed endpoint. The 2429 callback additionally checks that the passed
transport is still the manager's current Game transport and that its live peer
is `79.110.49.34:9959`. Under one lock it validates bounds/opcode/declared length,
expiry/PID/generation/transport and single-use state, writes +4..+7 only, then
optimization-resistantly erases the retained value. Other packet types neither
consume nor clear it. Rejected 2429 packets do not reach the original send path.

Normal PE imports, not injection or runtime patching:

| Boundary RVA | Approved preimage | Contract |
|---|---|---|
| 009FD0A2 | 68 4C 06 00 00 | Existing post-decryption 1637 observation, packet `[this+404]` |
| 009FC095 | 55 8B EC 51 53 | Native send, ECX=transport, argument object+4=packet |
| 0075D2F4 | E8 F3 3A 1A 00 | Existing Game connect call, preserves fastcall/thiscall context |
| 00900944 | 6A 0C B8 96 58 0A 01 | Observe manager close before original cleanup |

The new import is `RevoraAuthDirectPatch2429V2`, stdcall, 12 stack bytes:
`(packet, declaredLength, transport)`. A distinct export name makes an old DLL
incompatible at import resolution rather than silently using the old ABI.
The generated stub preserves original prologue/registers/flags and returns the
native failure value without sending on rejection. On acceptance it resumes at
009FC09A. WSA and Win32 last-error values are preserved across the callback.

Native close at VA D00944 stops workers and clears native queues (D00A29..D00AB5)
before subsequent sessions. The new observer clears retained state when that
close belongs to the bound Game attempt; it does not mistake the intentional
Account-to-Game transition for a Game disconnect. New Login, rejection,
exceptions, expiry and process termination also clear state. The worker never
calls KillTimer for a UI-owned timer. UI dispatch continues after injection so
the response leading to character creation can be processed.

Input is the preserved branded pre-auth derivative SHA-256
`93E5EF1946B9FE3161135E206DC5D5E2158E8379534E101A42B4127FA0E9092C`.
The original signed Conquer.exe remains untouched. `patch_direct_native.py`
records every pre/postimage, verifies the exact source hash and supports exact
reversal using that preserved source. Generated derivatives are private only.

## Validation completed

- Native x86 Release `/W4 /WX`: zero warnings/errors.
- Existing native authentication and owned-control tests pass.
- Worker race: 16 threads, exactly one successful injection; bytes outside
  +4..+7 unchanged; replay, expired/missing/zero state, wrong PID/attempt/transport,
  malformed bounds, reconnect and explicit cleanup rejected.
- The exact generated x86 stub executes in a synthetic x86 harness for 1,000
  accepted/rejected calls: ECX, arguments, stack cleanup and return path checked.
- Deterministic patch generation, every recorded pre/postimage, unknown-input
  rejection and byte-exact reversal pass.
- Launcher foundation: 110/110 pass.
- Fresh package extraction: existing pinned integrity key and production client
  manifest verifier both pass. No key rotation or verification bypass.
- Three-file synthetic install, independently verified backup, exact rollback,
  induced failure after first-file replacement, and DLL tamper rejection pass.

## Installed private candidate; runtime gate still open

Installed only EXE/DLL/client manifest into canonical `assembled`:

- EXE: `0F71CC853767099E025F804F423FE05028EB03465FBF4414B33332C0081667C2`
- DLL: `5DAE7B14995AD2C6DDE277979475D9382F0021FD3902483D47DDC1825DA58271`
- Manifest: `2013B46536C3D2BF80F32089D5DA79E791C34ABE7D082736290E937F15293701`
- Version: `revora-7630-world-handoff-v2.2`
- ZIP: `C:\Users\Administrator\Desktop\launcher-tests\Revora-World-Handoff-20260905-v2\revora-7630-world-handoff-v2.2.zip`
- ZIP size: 11,714,984 bytes; SHA-256
  `63F2A3EF36E5ABEA710A08276D2D5B9AFAFAF2A8129A9313A4C933E26F3085F1`
- Backup: `C:\Users\Administrator\Downloads\Revora-Client-Staging\Conquer-7630\world-handoff-backup-20260905T0543383111Z`

Rollback (close only the affected client first):

```powershell
& 'C:\Users\Administrator\Downloads\Revora-Client-Staging\Conquer-7630\world-handoff-backup-20260905T0543383111Z\Restore-WorldHandoffUpdate.ps1' -ClientRoot 'C:\Users\Administrator\Downloads\Revora-Client-Staging\Conquer-7630\assembled' -BackupRoot 'C:\Users\Administrator\Downloads\Revora-Client-Staging\Conquer-7630\world-handoff-backup-20260905T0543383111Z'
```

The genuine runtime gate is NOT passed. Session 2 (Administrator) is disconnected.
Startup-only research executions exited before Login. Their client log records
`Init3DEx: Fail to GetDeviceCaps!` and `CMyBitmap::Init3DEx dx9 fail` at
2026-09-04 22:27:47 and 22:29:26 local (UTC-7). No new credentials were entered
and no TestPlayer authorization occurred in these startup checks. Bounded
debugging recorded startup exception metadata only, no memory dumps or secret
buffers. All validation processes were closed.

Reconnect the desktop, then use the hash-gated OwnedInputValidation fixture on
the exact installed-equivalent research EXE/DLL. Do not ask the owner to log in.
Require actual 2429 handle acceptance and a visible class/appearance selection
screen before reporting OWNER_CHARACTER_CREATION_READY. Never select a class,
enter a character name, or create a character automatically.

GameHost remains PID 6804, SHA-256
`68E44500DA2DE169C2555EBD8DD6A4C5FE53F764785F683BCCED4612A5AEC6AB`;
AccountHost remains PID 8716. Neither server was modified or restarted here.
The proven DH/CAST5 and Account authentication implementations are unchanged.
