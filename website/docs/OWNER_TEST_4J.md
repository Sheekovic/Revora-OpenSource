# Owner Test 4j: post-wrapper Account boundary

Owner Test 4i is genuine Windows evidence through Stage 17. Its exact client stayed alive on
the Revora server-selection surface; the empty legacy credential fields confirm launcher-only
credentials. It did not reach Stage 18, Stage 19, or an Account connection. The visible launcher
stop `control-trace-monotonic-sequence-invalid` was diagnostic validation, not a client crash or
Account-server failure.

The duplicate sequence has a confirmed lifecycle cause. Stage 1 ran at PE entry and incremented
a counter in zero-filled writable `.data`; the Microsoft C runtime subsequently initialized that
storage back to zero before Stage 2. Stage 1 and Stage 2 therefore both recorded sequence 1. Test
4j gives Stage 1 the fixed value 1 and numbers post-CRT records from 2.

Static analysis proves that `FUN_0050ee04` is a thin cdecl wrapper: it obtains the client window,
calls `PostMessageA(hwnd, 0x464, command, argument)`, and returns without consuming the command.
For `CMD_GAMESTART`, command is `0x480` and argument is zero. The genuine `0x480` handler sets the
pending field at object offset `0xaccdc`. A later update path checks that field and the readiness
byte at `0xacce8`; only the ready branch proceeds toward the Account path. The Test 4i Stage 16
hook was in the window getter, so it proved wrapper execution but not PostMessage acceptance or
handler consumption. Stage 17 proved wrapper return.

Test 4j preserves stages 1–19 and adds bounded fixed identifiers:

* 20: command `0x480` consumed and pending flag written;
* 21: pending flag polled;
* 22/23: readiness guard passed/failed;
* 24/25: `PostMessageA` accepted/rejected the exact dispatch.

The diagnostic records no credentials, ticket values, packet content, memory, or private paths.
Stages 18–19, the encrypted 276/1086 request, and Account success remain unproven until genuine
owner evidence says otherwise.

## One-run instructions

1. Keep Microsoft Defender and Malwarebytes enabled. Run as a normal Windows user.
2. Extract the package into a new small tools directory. Do not place it inside the client donor
   or the owned workspace.
3. Verify the ZIP SHA-256 shown by the authenticated Admin page and the included instructions.
4. Start `Revora.Launcher.exe`. Select the unchanged verified Test 2c donor if it is not remembered.
5. Enter the throwaway game-account credentials in the launcher only and click **Play** once.
6. Do not type in the legacy client, do not rerun, and do not disable security software.
7. Wait for the launcher to stop at a classified boundary. Use Emergency Stop only if needed.
8. Return the single `Reports/owner-test4j-*.json` plus Defender/Malwarebytes results. The report
   is sanitized; do not send screenshots containing credentials.

Public downloads and public Account/Game listeners remain disabled.

Reviewed component hashes:

* `Revora.Launcher.exe`: `308251aa21bba290c34497f2cfaf18af0a5f63430cdf9bd534853aec6f945110`
* deterministic diagnostic `Conquer.exe` output (not distributed):
  `8f2d5d837cf894bfb362c900370f065d4b3c916dba7724af2a3184f3d8b8d4f2`
* signed Test 4j profile:
  `dd946f7190df5f7f67e01db56391639b603b82a4791adba346e0dc68371ea171`
