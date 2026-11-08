# Owner Test 4e control trace

## Ordered Test 4d Revision 1 evidence

Five returned reports are one evidence set. Only one successful-preflight timestamp was
supplied, so no timestamp is invented for the others.

1. The first attempt stopped during `immutable-validation` because the computed reusable
   workspace did not contain `ini/3DEffect.dbc` (expected SHA-256
   `ca7470a6d8d471016b2159c7617593516286978ddaa6873a327f380af02e3cbd`). The
   phase identifies the checked tree as the reusable workspace, not the Test 2c donor.
   No file was restored, downloaded, or modified.
2. A second attempt stopped at the same `immutable-validation` boundary with the same
   `immutable-file-missing` result for `ini/3DEffect.dbc`. It likewise made no repair.
3. At `2026-08-28T22:10:37.5696365Z`, the exact Test 4d R1 client
   (`842204b71208b88646797177f0a3a685bac9868d2e28ed8b1d85f7ce6dea0a83`) launched
   as PID 38480 after 12,829 immutable files were verified. Every internal breadcrumb was
   false; there was no Account connection, the ticket was revoked, and the run timed out.
   Because that build had no independently proven client-side control breadcrumb, this
   does **not** prove that the candidate hook was skipped. It proves only that no breadcrumb
   reached the launcher.
4. A second complete timeout launched PID 45048. Like PID 38480, its all-false trace is
   inconclusive because Test 4d R1 lacked independently proven client-side transport controls.
5. One attempt stopped during `path-validation` with `workspace-inside-donor`. This means
   the selected root was the owned workspace itself, or a parent/child root that would make
   donor and workspace overlap. It was not accepted as a donor.

The differing results are consistent with materially different selected roots: selecting the
unchanged Test 2c root computes its sibling workspace, selecting the marked workspace is now
classified as `owned-workspace`, and selecting a similarly named or wrong-level folder is
classified as `unknown`. Reports include a path-independent SHA-256 root fingerprint,
workspace ID, baseline build ID, signed-profile match, overlap result, and expected-root flag.
Absolute paths are never reported.

## Control-trace design

Test 4e is diagnostic instrumentation, not another login transition. It accepts only the exact
RSA-testing input SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f` and produces
the deterministic diagnostic output SHA-256
`7aa96e49cb30ded6db3944b5fd4a7f36282cc26036e1152e7b1f1802c8891df4`.

The exact client appends fixed one-byte stage identifiers to the tool-owned
`.revora-owner-test/test4e-stage.bin` channel. The launcher removes a prior trace before
launch, launches the exact verified output itself, owns that process handle/PID, and accepts
only the strict sequence `01` through `0b`, once each. Gaps, duplicates, reparse points, excess
bytes, unknown executables, and profile mismatches fail closed. The channel contains no
credentials, ticket, compatibility identity, packet, key, address, path, or memory content.

| Stage | Evidence location | Exact original behavior preserved |
|---:|---|---|
| 1 | `CWinApp::InitInstance` entry after CRT startup | original initial register value |
| 2 | confirmed main-client initialization return | original local-state write |
| 3 | top-level `ShowWindow` return | original `ShowWindow(5)` call |
| 4 | login/server surface initializer | original object load |
| 5 | server-list completion | original comparison flags restored |
| 6 | Revora selection render/commit call | tail-call to original target |
| 7 | prior Test 4c/4d candidate call | tail-call to original target |
| 8 | genuine UI completion return | original cleanup instructions |
| 9 | internal command wrapper | original wrapper load |
| 10 | Account transition entry | original state load |
| 11 | socket-connect call | tail-call to original socket target |

The patch manifest records each file offset, original bytes, replacement bytes, and reason.
Its SHA-256 is `be59bdedcabaa262c11b2f48799392e9ab30d54f9804febe9d2c3eab35cb3f98`.
The diagnostic section is temporary and must not enter a production client.

The first interpretive gate is strict: if stages 1 and 3 are not both received, diagnostic
transport is failed and no conclusion may be drawn about the login path. If they are received
but stage 7 is absent, the previous Test 4c/4d hook is formally rejected on the genuine
Windows path. Later stages locate the first missing boundary without credential entry.

## Root handling

The folder picker accepts an unchanged signed Test 2c donor only. Selecting a marked owner
workspace produces a specific explanation to select the unchanged donor; it never asks the
owner to delete either tree. The workspace remains a single sibling copy. Only `Conquer.exe`
and `Server.dat` are controlled active files; the other 12,829 profile files remain immutable.
No missing passive file is silently repaired by this diagnostic.

## Acceptance boundary

Test 4e succeeds as a diagnostic when startup and window controls arrive in strict order and
the report identifies the first later missing stage. It is not playable login. If the trace
unexpectedly reaches a PID-bound encrypted 276/1086 connection, the ticket is consumed once
and only the controlled encrypted 1055 response is sent. Ports 9958 and 9959 remain private.

## Genuine Test 4e result

The sole genuine Windows Test 4e run launched exact output
`7aa96e49cb30ded6db3944b5fd4a7f36282cc26036e1152e7b1f1802c8891df4` as PID
44152. The donor was verified, 12,829 immutable files matched, and stages 1 through 8 were
present while stages 9 through 11 were absent. No Account connection occurred and the ticket
was revoked after timeout.

The report marked the trace sequence invalid while also marking stages 1 and 3 present. This
is not a PID-attribution result: Test 4e stored only one-byte stage IDs and did not store a PID,
thread, sequence number, or timestamp in each record. It therefore could not attribute either
control independently. The aggregate attribution field is withdrawn.

The exact disk order is irrecoverable from the JSON because that schema serialized only stage
membership, not the `Stages` array. Static nesting strongly supports `1,2,4,5,6,7,8,3`: stage
3 is written only after `ShowWindow` returns, while stages 4–8 can run synchronously during
window creation/show processing. Duplicates cannot be excluded from the old JSON. This is a
**Strongly Supported explanation**, not a reconstructed genuine byte sequence.

Static review also shows Test 4e contained no automatic transition after stage 8; it only
instrumented the natural path. Therefore absence of stage 9 in Test 4e does not prove a failed
guard. Separately, the Test 4c/4d transition called `0x44b8b5`, one byte after the genuine
UI-completion entry at `0x44b8b4`. Test 4f corrects that exact call target and records a
structured, PID-bearing partial-order trace.
