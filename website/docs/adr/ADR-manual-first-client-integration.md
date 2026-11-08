# ADR: Manual-first client integration

- Status: Accepted for the current milestone
- Date: 2026-08-29

## Context

Owner Tests 4 through 4j attempted to start the exact patch-5165 Account state automatically with
deterministic fixed-address executable changes. The work established valuable exact-client
control-flow and diagnostic evidence, but it did not produce an automatic socket connection. Test
4j reproducibly reached its bounded wrapper rejection stage, froze at the loading surface, and
closed without consuming command `0x480` or reaching Account state.

Further fixed-address changes would increase coupling to incidental client lifecycle, register,
stack, window, and address assumptions without advancing a genuine protocol session.

## Decision

Freeze the PE-entry/native UI-bootstrap path. Do not publish Test 4k/4l variants, retry external
window messages, add inline hooks, or automate legacy credentials.

Make manual playability the next gate. A throwaway development account may be entered manually in
the verified legacy client while all listeners remain private. Revora will first prove genuine
276/1086 parsing and a controlled 1055 response, then determine successful Account handoff, Game
1052, character entry, and map entry from exact evidence.

## Reusable completed work

- deterministic client reconstruction and signed full-file profiles;
- exact client hashing, transactional controlled-file updates, rollback, and sanitized reports;
- initial Account cipher and genuine manual 276/1086 header evidence;
- bounded fragmented/coalesced stream handling and redacted reports;
- loopback-only PID ownership checks, ticket issue/consume/revoke primitives, rate limiting, and
  PostgreSQL identity foundations;
- controlled encrypted 1055 error response implementation and tests;
- Test 4j evidence and the rejected-path documentation.

None of these proves a successful 1055 response, Game handoff, character selection, or playability.

## Dragon decision

Dragon demonstrates a practical x86 suspended-process and Winsock-redirection architecture, but
its wildcard module discovery, arbitrary DLL loading, absent signature/hash enforcement, editable
endpoints, RWX buffer, unbounded polling, Flash module, website hook, and elevation/antivirus
guidance conflict with Revora's security contract. It remains a BSD-3-Clause reference, not a
drop-in dependency. It was neither built nor executed.

## Conditions for a future hardened runtime bridge

Approval requires a fixed Revora-owned allowlist, exact client/module hashes and signatures,
signed-profile binding, fixed endpoints, no Flash or website hooks, no plugins, bounded cleanup,
same-user non-elevated Windows evidence, PID/session-authenticated IPC, memory-only single-use
tickets, complete source review, security-product testing, and BSD attribution for reused Dragon
code. The bridge must be necessary after manual interoperability is proven; convenience alone is
insufficient.

## Consequences

The next owner run must advance real protocol behavior rather than instrumentation. It will not be
requested until the unchanged-client endpoint/signing constraint described in
`MANUAL_PLAYABLE_BASELINE.md` is resolved through an explicitly reviewed choice. Production
launcher-only authentication remains a later goal and is not weakened by the temporary manual
development account.
