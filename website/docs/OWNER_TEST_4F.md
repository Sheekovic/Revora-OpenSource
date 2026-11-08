# Owner Test 4f structured transition trace

## Purpose

Owner Test 4f is one bounded Windows diagnostic. It replaces Test 4e's ambiguous one-byte
append stream with fixed 28-byte records containing only a magic marker, fixed stage ID,
process-wide monotonic sequence number, process ID, thread ID, and
`QueryPerformanceCounter` timestamp.

It records no credential, ticket, compatibility value, packet, key, address, path, or memory.
The launcher accepts records only from the exact PID it created after signed-profile and
executable verification. It separately reports `diagnosticChannelProcessId`, startup-control
attribution, window-control attribution, duplicates, missing or unexpected stages, raw file
order, monotonic event order, and the first invalid dependency.

## Partial order

The client can execute login-surface initialization re-entrantly while `ShowWindow` is still
active. Consequently stage 3 need not precede stages 4–8. The reviewed dependencies are:

- `1 < 2`;
- `2 < 3` and independently `2 < 4 < 5 < 6 < 7 < 8`;
- `8 < 9 < 10 < 11 < 12 < 13 < 14 < 15 < 16 < 17`;
- `17 < 18 < 19` when the Account path proceeds;
- guard-failure stage 20 or 21 requires stage 9.

File-write order is not used as causal order. Records are ordered by an interlocked process-wide
sequence. Duplicate sequences, duplicate stage IDs, unknown stages, malformed records, PID
mismatch, or a dependency violation fail the transport.

Before client validation, a launcher-side self-test verifies structured parsing, two-thread
metadata, PID attribution, and the valid re-entrant sequence `1,2,4,3`. This does not replace
the genuine controls: stages 1 and 3 must still arrive from the launched PID.

## Proven transition defect

The original call at file offset `307182` is `e8 c1 08 00 00`, targeting VA `0x44b8b4` with
`ECX=this` and one stack argument. Test 4c/4d used a trampoline that called `0x44b8b5`,
skipping the function's initial `b8` opcode. That one-byte target error is a confirmed static
defect.

Test 4f replaces the call-site bytes with `e8 2d 17 1c 00`, routing to a bounded trampoline.
The trampoline preserves `ECX`, pushes the original zero argument, calls exactly `0x44b8b4`,
lets the original callee clean its argument, restores `ECX`, and returns with `ret 4` to preserve
the original caller ABI. The manifest asserts every original byte. Input SHA-256 is
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`; deterministic
output SHA-256 is
`b4d4977ef50980b656397d0e339bd0d34f350917663b6c9af393586a49cdd6df`.

## Boundary events

| ID | Fixed event |
|---:|---|
| 7 | immediately before genuine UI-completion call |
| 8 | immediately after genuine UI-completion return |
| 9 | before compatibility state preparation |
| 10 | group/server state verified |
| 11/12 | before/after compatibility username setter |
| 13/14 | before/after compatibility password setter |
| 15 | immediately before internal wrapper call |
| 16 | first instruction inside genuine wrapper |
| 17 | wrapper returned |
| 18 | Account transition entered |
| 19 | socket connect attempted |
| 20/21 | group/server guard failed |

The fixed compatibility strings are not authentication authority. The launcher-held ticket is
the only authority and is consumed once by the PID-bound loopback bridge. No real credential
enters the client.

## Workspace and non-goals

The marked workspace is transactionally upgraded from an explicitly known active state. The
Test 2c donor remains unchanged, no second full client is made, and only controlled active
files are atomically replaced with rollback. Test 4f is not playable-login proof. Public
downloads and TCP 9958/9959 remain disabled.

## Genuine two-run result

The genuine runs under PID 29356 and PID 44572 both emitted zero records. The exact executable
and immutable baseline matched, but the launcher-side self-test did not prove client-side
execution. No corrected transition or Account behavior was tested. Subsequent PE reconstruction
showed that the stage labelled “startup” was at VA `0x4b9bc2`, not the actual PE entry point
`0x59d668`; it was therefore not a guaranteed startup control. See `OWNER_TEST_4G.md`.
