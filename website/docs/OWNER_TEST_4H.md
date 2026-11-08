# Owner Test 4h progressive Account trace

Owner Test 4g is accepted genuine Windows evidence. Exact output
`206bcee90388633930dc4171a4cbc23e2c5b140ae55d25c7220af0f35a0c89a9`
emitted both independent PID/thread-bound controls as PID 44448. This confirms the PE-entry
patch, marker, structured file transport, and attribution. It does not confirm Account behavior.

Test 4h preserves that proven PE-entry marker and makes stage 1 originate at the same entry
wrapper. It restores stages 2–19, the partial-order validator, the corrected `0x44b8b4` target,
and the PID-bound loopback Account bridge. The old non-guaranteed `0x4b9bc2` pseudo-startup
patch is restored to its exact original bytes.

The deterministic patch accepts only RSA-testing input
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
Its exact output is
`cf4525da3f43f73bfb1eecc6252f3b9c3a570db1963bbe7b2a7a1ec0a6abfc68`.
The PE entry call targets `0x60c97b`, restores `55 8b ec 6a ff`, discards only its own call
return, and resumes at `0x59d66d`. The transition preserves `ECX`, supplies the original zero
argument, calls the full initial opcode at `0x44b8b4`, preserves callee cleanup, restores
`ECX`, and returns with the original caller cleanup.

Authentication remains launcher-only over fixed HTTPS. The bridge starts before the client,
is bound to the exact launched PID, and accepts one evidenced encrypted length-276/type-1086
request. Only then may the server consume the digest-stored launch ticket once and send the
controlled encrypted type-1055 response. Timeout, cancellation, PID mismatch, malformed trace,
or malformed protocol revokes the ticket. Reports contain no username, password, ticket, raw
packet, decrypted payload, or private path.

Test 4h is a one-run private diagnostic. TCP 9958 is loopback-only within the owner tool; no
public game listener or public download is enabled. Success is genuine automatic Account
request evidence, not playable login or Game handoff.

## Genuine Windows result and exact defect

Two valid runs used exact executable SHA-256
`cf4525da3f43f73bfb1eecc6252f3b9c3a570db1963bbe7b2a7a1ec0a6abfc68`.
PIDs 39784 and 44108 each produced the valid independent PE-entry marker and no structured
event. Both tickets were revoked and neither run opened an Account connection. Consequently,
stages 1–19, `0x44b8b4`, socket creation, and 276/1086 were not tested by Test 4h.

The separate `ini/3DEffect.dbc` missing-file report failed during immutable validation against
an incorrect or unknown selected root. No client ran, no rollback was required, and that root
was not modified or repaired.

Disassembly of the exact Test 4h output proves the root cause. Stage 1 executes
`InterlockedIncrement(0x60c880)` before opening the event file. Address `0x60c880` is inside
the image's `.text` section, whose characteristics are executable and read-only. The attempted
write faults before `fopen`, while the independent marker writer succeeds because it uses the
fixed sequence value 1. The embedded event filename is exactly the null-terminated relative
`.revora-owner-test/test4h-events.bin`; it was not a reader/writer filename mismatch.
