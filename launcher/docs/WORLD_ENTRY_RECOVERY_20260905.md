# World-entry recovery — September 5

This change preserves the 993624a transparent send boundary: only the first valid
2429 gets a four-byte identifier substitution; other opcodes pass unchanged.
The worker handoff and authentication are unchanged. Added bounded opcode/category
evidence, post-creation deadline observation and visible disconnect recovery.
`game_entry_watchdog.h` only reads bounded 2099 protobuf action metadata; it does
not mutate packets or authorize a session. Action 74 is not itself visible-world
proof. Synthetic malformed/truncated/action tests and transparency tests pass.

Installed package revora-7630-world-handoff-v2.4:

- EXE: 0F71CC853767099E025F804F423FE05028EB03465FBF4414B33332C0081667C2
- DLL: 318CDE7CF355EF7073DEF6F418E242C6DB4D0AF149FD2BC78DD901964211D98D
- client manifest: 16F6C7D3F8807AC54039FA15D0F6E06F52DFF12187E3C1A26682F1E1E4353A66
- ZIP: E3D219612386008DD58AFE8E91D89C06A62A20FEE5DBECE902F8A6C3CFE39180
  (11,715,972 bytes; full client not rebuilt).
- Root: C:\Users\Administrator\Desktop\launcher-tests\Revora-World-Entry-Recovery-20260905-v4
- Client backup: canonical Conquer-7630/world-handoff-backup-20260905T1329328198Z.
  Run its Restore-WorldHandoffUpdate.ps1 with -ClientRoot pointing to assembled
  and -BackupRoot pointing to that backup for exact three-file rollback.

Fresh signature verification, transactional apply, injected staging failure,
automatic exact rollback and one-byte DLL tamper rejection all passed. Approved
original Conquer/TQPlat were not replaced. Native Release /W4 /WX passed.

## Genuine validation — NOT visibly complete

The owner's creation persisted exactly once. Server fixes for missing local realm
identity and AcceptLogin-to-LoginFull packet admission let the existing character
reach MaTriX SetLocation at 13:38:51.6790470 UTC. Client forwarded 2099 and subsequent
Game packets unchanged. However the Login window remained visible and stage 5
expired at 13:39:19.470. A server map-entry event is not genuine visible world entry.
The remaining blocker is native login-to-world UI completion; no owner retry is
requested and no speculative native function call was introduced.

The exact release-equivalent research client was PID 6304, approved EXE/DLL hashes
above. Screenshots are private ignored evidence, not package content. The initial
capture helper selected/restored the login window and therefore cannot prove what
the Game renderer displayed. Subsequent observation still does not prove world
rendering; this limitation must remain explicit.
