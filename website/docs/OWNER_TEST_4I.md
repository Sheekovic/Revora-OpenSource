# Owner Test 4i corrected PE-entry Account trace

Owner Test 4i corrects only the confirmed Test 4h transport defect. The structured-event
counter moves from read-only `.text` address `0x60c880` to zero-filled writable, non-executable
`.data` virtual storage at `0x695300`. The marker and event files are uniquely named
`test4i-startup.bin` and `test4i-events.bin`. All supported current and legacy diagnostic
filenames are removed before launch and reported afterward using presence, size, launch-window,
record-validity, and launched-PID-match metadata only.

The deterministic patch accepts RSA-testing input SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f` and produces exact
diagnostic output SHA-256
`6bb9c92f285cada26208fc625a12c45c7185e99482032a51c5c1de5a019618dc`.
The build audit disassembles the reviewed entry-wrapper byte sequence, verifies marker then
Stage-1 call ordering, validates the writable/non-executable counter section, verifies the
embedded filenames, and rejects superseded Test 4h event targets.

The launcher interprets no stage unless both the PID-bound independent marker and PID-bound
Stage 1 are valid. Stages 2–19 and the corrected full `0x44b8b4` transition are otherwise
unchanged and remain unproven until genuine Windows evidence is returned. Authentication is
HTTPS launcher-only, the bridge binds only to `127.0.0.1:9958`, tickets are single-use or
revoked on failure, and reports contain no credentials, ticket, packet payload, key, or private
path.

This is one-run private evidence tooling. It does not establish playable login, Account
success, Game handoff, or 5165 compatibility unless a genuine PID-bound encrypted 276/1086
request is observed. Public downloads and public game listeners remain disabled.
