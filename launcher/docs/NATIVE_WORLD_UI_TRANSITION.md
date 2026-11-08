# Native world UI transition (2026-09-05)

Scope: restore only the missing UI lifecycle after map admission. No change to
credentials, HTTPS, 0x7F11, 1637, cross-thread handoff, DH/CAST5, 2429, grants,
character persistence, packet transparency or MaTriX.

## Reference comparison

The supplied `Source 7630 By WKConquer V308/Login/Patch` is a binary/configuration
patch, not an available C++ source project. Its DX9 executable SHA-256 is
`86F19DECE9E455D644E121EA7F95F3334059C3229374AA9DD128D61B893F6520`;
it is not the approved executable and its offsets cannot be transplanted.
LoginLoader SHA-256 is
`4E0F9E2634C257A9070C0CEA113E726AB2B873E44244C35D358BAD295FD1B885`.
The frozen private LoginLoader report describes identifier capture at DLL RVA
0x34A0 and substitution at 0x32A0, returning to the client's original paths.
It does not establish an independent world-renderer initialization to import.

| Original behavior | Current equivalent | Missing behavior / decision |
|---|---|---|
| Loader identifier capture/substitution | Proven synchronized Revora handoff | Retain Revora unchanged |
| Legacy authentication, redirects and TQPlat patches | Proven Revora authentication | Obsolete; do not restore |
| Original native Login loading-dialog setup | Direct submit bypassed that branch | Restore three UI-only native calls |
| Original map-state callback, rendering and dialog shutdown | Still present in approved client | Let them run normally; no fake window hiding |

## Exact approved-client evidence

Approved original SHA-256:
`4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE`.
All VAs below assume image base 0x00400000; runtime calls use module base + RVA.
The unchanged derivative EXE SHA-256 is
`0F71CC853767099E025F804F423FE05028EB03465FBF4414B33332C0081667C2`.

Original Login function VA 0x0065969C performs these UI operations separately
from its now-obsolete credential submission:

| Original call sites | Runtime target RVA | Checked preimage | UI operation |
|---|---|---|---|
| 0x006599D3..0x006599E6 | 0x00255046 | 55 8B EC 8B 45 08 | thiscall loading setup (300,30) |
| 0x006599EB..0x006599F9 | 0x000CEAC6 | 55 8B EC 8A 45 08 | thiscall loading flag (true) |
| 0x00659B46..0x00659B54 | 0x0057FBDB | 55 8B EC 83 7D 08 00 | native dialog Show(5) |

Main shell pointer: RVA 0x010B3080. Loading object: main + 0x260D70,
expected vtable RVA 0x00D6B198. Native window handle is object + 0x20.
The calls execute only on the original Login UI timer thread, after verifying
the shell window belongs to that thread and current process. There is no
worker-thread UI invocation or new runtime hook.

Map reply VA 0x00C0974C..0x00C09758 selects state 1013. State-manager pointer
is RVA 0x010AFBF4; current state object is manager + 4. State-1013 vtable
RVA 0x00E1EFFC dispatches through VA 0x0093D25D to VA 0x009E390D.
At VA 0x009E3925 that callback checks loading-dialog visibility. Without the
original setup/show, its loading-completion work is skipped. With it, native
VA 0x00654ABC / 0x00654DB1 complete loading while normal rendering continues.

Real world state is **1010**, not an inferred 1100: its runtime vtable is
RVA 0x00E1F014; VA 0x0093D18F returns 0x3F2, and VA 0x0093D222 dispatches
the world update to VA 0x009E289A. Completion requires this state plus both
native Login and loading dialogs no longer visible. Hiding Login alone cannot
complete this gate. Owned child controls disappear with genuine parent shutdown;
password clearing is repeated on confirmed completion. No unrelated UI is hidden.

## Runtime evidence and safety

First bounded TestPlayer run (existing owner-created character) reached map
admission at 14:07:59.5506686Z, native loading setup at 14:08:00.363Z, and
native dialog shutdown at 14:08:01.800Z. Read-only state inspection confirmed
state-1010 vtable, hidden loading dialog, and native loading completion flag.
Private screenshot `research/isolated/direct-native-transport-20260905/
world-ui-candidate-visible.png` shows the player/map, minimap, chat, action bar
and HUD. It was captured without manually hiding the Login window.

### Final release recheck boundary

The first visible-world candidate DLL was
`59C3D49C55E1CD8F227B306A8141C7A7D353F05BE8DF7FDB48AF2780B13F91B2`.
The final build additionally corrects confirmation to the observed state 1010,
prevents the old Login-hidden shortcut from bypassing that confirmation, and
repeats password clearing. Final DLL SHA-256:
`BC4D3CA292B92DF323B0333416D668E95ED1B1680297C1349CD0C4B2B1CD5E67`.
Do not conflate the first candidate's runtime proof with final-build validation.

Final-build startup was blocked by TQSafe's `Your license key is corrupted`
message before Login. The disposable profile was checked in both launch
contexts, and its owner-local DX9 license prerequisite was copied byte-for-byte
from the canonical profile (never read as text or packaged). Startup remained
blocked. The canonical profile's exact DX9 launch context also stopped at TQSafe.
No further authentication attempt occurred. No protection was disabled or patched.

The native startup rewrote `Env_DX9/regkey.dat` from 445 bytes to 1 byte
(canonical timestamp 2026-09-05T14:22:16Z). The root regkey and original client
binaries remain untouched. The three-file installer backup does not include
owner-local licensing state; exact license restoration therefore is NOT claimed.
No regkey backup was found in the sibling transactional backup directories.
An owner-valid licensing prerequisite must be restored through its legitimate
workflow before another final-build runtime check. Do not guess license contents.

The canonical three-file update was rolled back and all previous hashes verified.
Prepared private package (not currently installed):
`C:/Users/Administrator/Desktop/launcher-tests/Revora-World-UI-Transition-20260905-v5/revora-7630-world-ui-v2.5.zip`,
11,716,552 bytes, SHA-256
`FCE3A2D3C93E60BD66C07C60DF3EFCBCE2A9E0FBA2233E8DEFA35CF610D877E5`.
Signed client manifest SHA-256:
`B20614FD357142ACB6098E478B3ACAC7AEC5EAE8B17D509B7617132FABFB5DA2`.
Fresh extraction, signature verification, synthetic apply/exact rollback,
automatic rollback on a staging collision, and tampered-DLL rejection passed.
GameHost PID 5508 and its binary were not changed/restarted.

The existing 30-second stage-5 bound remains; it is not increased. Successful
world confirmation cancels the entry deadline only after the state/dialog gate.
New-character selection retains its separate existing path. No character was
created/reset by this change. Full new-character re-creation is not authorized.

Tests cover UI-thread ownership, wrong state, premature/duplicate callback,
visible-dialog rejection, failed-attempt reset and single completion. Existing
real-export packet transparency, worker handoff and owned-control tests remain
required. Native build is x86 Release /W4 /WX.

## Source ownership and rollback

Implementation: `tools/RevoraAuthClient/src/direct_native_runtime.cpp` and
`native_world_ui.h`; regressions: `tests/game_send_transparency_tests.cpp`.
No executable instruction is changed in this update; the static import remains.
Generate the small signed update with `Prepare-WorldHandoffUpdate.ps1`, verify
its fresh extraction with the existing trusted key, then use the transactional
installer. It backs up EXE/DLL/client manifest and copies a rollback script into
the backup directory. Run that `Restore-WorldHandoffUpdate.ps1` with the exact
ClientRoot and BackupRoot recorded by installation. Never replace originals,
TQPlat, owner-local license state, or server files for this UI correction.

This run's canonical backup: `C:/Users/Administrator/Downloads/Revora-Client-Staging/Conquer-7630/world-handoff-backup-20260905T1422149862Z`.
