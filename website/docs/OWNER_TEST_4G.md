# Owner Test 4g PE-entry startup control

## Test 4f evidence classification

The two genuine Test 4f reports are one reproducible evidence set. Both exact clients used
SHA-256 `b4d4977ef50980b656397d0e339bd0d34f350917663b6c9af393586a49cdd6df`,
launched visibly as PID 29356 and PID 44572, and verified all 12,829 immutable files. Both
contained zero structured events, no diagnostic PID, no Account connection, and a safely
revoked ticket. The launcher-only parser self-test passed, but it did not exercise client code.

Consequently no Test 4f client hook, corrected `0x44b8b4` call, wrapper, Account transition,
or socket operation is classified as executed. The two runs failed at the earliest client-side
diagnostic boundary.

## Static reconstruction

The launched file matches the intended Test 4f output byte-for-byte. It is PE32/i386 with entry
point VA `0x59d668`, fixed image base `0x400000`, stripped relocations, and no ASLR flag. The
diagnostic body at VA `0x60c600` lies in mapped executable/readable `.text`; the expanded
virtual size and every patched offset match the signed patch metadata. Relative calls and
returns disassemble to their intended targets, and no later patch overlaps the trace body.

The root placement error is earlier: Test 4f labelled VA `0x4b9bc2` as a startup control, but
the actual PE entry point is `0x59d668`. A visible window proves the PE entry ran; it does not
prove the unrelated `0x4b9bc2` site ran. This makes the old stage-1 location non-guaranteed.
The existing transport is not IPC or a named endpoint: the 32-bit client uses `fopen`/`fwrite`
to a relative file below the prevalidated workspace. There is therefore no listener, namespace,
security descriptor, inheritance, or cross-integrity IPC boundary to validate. The launcher
waits only by reading the closed file after the bounded run.

## Test 4g design

Test 4g is credentials-free and does not start the Account bridge. It accepts only the exact
RSA-testing input and routes the exact PE entry through a deterministic wrapper. The wrapper
first writes a fixed 28-byte startup marker, then an independently named 28-byte structured
event, closes both handles, restores the displaced five entry bytes and jumps to entry+5.

Each record contains only magic/version-or-stage, a fixed counter, PID, thread ID, and QPC
timestamp. Files live under the existing non-reparse, tool-owned `.revora-owner-test`
directory. Stale controls are removed before launch. Classification is strict:

- absent/invalid PID-bound marker: `patch-or-entry-stub-execution-failed`;
- valid marker but absent/invalid structured event: `client-to-launcher-transport-failed`;
- both valid and attributed to the launched PID: `startup-controls-proven`.

The deterministic output SHA-256 is
`206bcee90388633930dc4171a4cbc23e2c5b140ae55d25c7220af0f35a0c89a9`.
Test 4g does not instrument or interpret stages 2–19 and cannot prove login compatibility.
The diagnostic patch must not become production client code.
