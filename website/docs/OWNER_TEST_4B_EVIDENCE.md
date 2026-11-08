# Owner Test 4 and 4b Evidence

## Owner Test 4 — genuine result

Classification: **partial failure at the automatic Account-transition boundary**.

Owner observation and sanitized report summary:

- HTTPS authentication: passed.
- Launch ticket issuance: passed.
- Loopback bridge readiness: passed.
- Exact patched client launch: passed; PID 33120.
- Executable SHA-256: `6d03cd53edde97c4354eea42b07e7fa79f7132d65f4710ba45f7c6b5da59e11d`.
- Original 5165 login/server interface visible and Revora appeared selected.
- No legacy credentials entered.
- No connection reached `127.0.0.1:9958`; no packet or response existed.
- Ticket revoked; final failure `timeout`.

The raw returned report was not uploaded to a server-side project path, so this independently written summary preserves its stated fields without inventing an artifact. The patch bytes were present, but callback execution, stub execution, message `0x480`, Account builder execution and socket creation are **not confirmed**.

## Owner Test 4b — genuine result

Classification: **partial failure at the internal Account-start boundary**.

- Exact RSA-intermediate client SHA-256: `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
- Client PID 35416; exact window observed.
- One external top-level-window message `0x480` was accepted by Windows.
- No Account connection, packet, controlled response or ticket consumption occurred.
- The ticket was revoked after timeout.

This proves only that Windows queued the external message. Static disassembly now shows the
client's real helper posts Win32 message `0x464` to its internal HWND with internal command
`0x480` as an argument. Consequently, external top-level `0x480` was the wrong message layer.

The following remain hypotheses, not conclusions:

- the patched callback at file offset `0x4a3f4` may not execute on the observed Windows path;
- it may execute before group/server/Account/message-receiver state is committed;
- later callback code may overwrite the state;
- posting from inside the callback may be re-entrant or ignored;
- the receiver/thread may differ from the static model;
- the transition may require a later UI message-loop iteration.

Those hypotheses are superseded by the corrected Test 4c call-path evidence.

## Archived Owner Test 4b design

Test 4b returns to the reviewed RSA-testing intermediate client SHA-256 `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`. After authentication, ticket issuance, bridge readiness and exact client launch, it:

1. verifies launcher and client have equal non-high Windows integrity;
2. obtains the exact launcher-created process's top-level HWND;
3. requires that HWND to remain PID-owned, visible and responsive continuously for two seconds;
4. allows a further 250 ms message-loop interval and revalidates all state;
5. queues custom internal message `0x480` exactly once;
6. waits for the PID-bound encrypted 276/1086 request and sends controlled 1055 only after atomic ticket consumption.

It sends no character, key, mouse, clipboard, username, password or Enter input. It uses no hook, injection, remote thread, APC, DLL, elevation, service, persistence or antivirus exclusion.

Test 4b success requires all of: `clientWindowObserved`, `bootstrapMessageAttempted`, `bootstrapMessagePosted`, `accountConnectionObserved`, matching connected PID, decrypted length 276/type 1086, consumed ticket, controlled response sent, and an owner-recorded visible response.

Test 4b did not succeed. External PostMessage is rejected as the final architecture.

Private package evidence:

- ZIP: 152,843,602 bytes; SHA-256 `0230412524077253c4f4dccb166518021e2d65ea40bb0db1d9cab2635fa5844b`.
- Launcher: 139,858,059 bytes; SHA-256 `66126f3a1a434dcdc621e9a1105f9345a210701228ca3ab10f677ad4e109d2ed`.
- Signed Test 4b profile: SHA-256 `e065f244458dc8fac45af791c227912f0bd16c2af97b5d30b663ef78c80e0c08`.
- RSA patch metadata: SHA-256 `036069e65095ee0d884aae5f974609e9273ea82a17436d39b904a9bd89d02e0a`.
