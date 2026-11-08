# SHEEKOVIC genuine jump disconnect — 2026-08-29

## Confirmed production evidence

The genuine session entered map 1002 and accepted/persisted walking through
`17:29:49.1298316Z`, ending at authoritative `(442,386)`. At
`17:29:49.4454161Z` Revora classified the next decrypted frame as
`protocol-validation`, ended its connection handler, and disposed the TCP
connection. The last packet sent before that failure was the authoritative
declared `20/10005` movement response for `(442,386)`. The client did not author
this close. A later genuine session restored the persisted position and proved
that walking/running and reconnect persistence remained healthy.

The rejected jump was not logged after parsing, so its owner-selected target
coordinates were intentionally not retained and cannot be reconstructed. DMap
accessibility, elevation, and jump distance were therefore **not evaluated** in
that run. The received frame is classified by the exact-client construction
path as declared `32/10010`, action `137`; the old runtime threw before emitting
the action-stage audit event.

## Exact root cause

The approved client (`902954c9...93c2f`) constructs jump at `0x5171b7–0x517202`
through constructor `0x54da8c`. The constructor writes:

| Offset | Exact field |
|---:|---|
| 0 | declared length `32` |
| 2 | packet ID `10010` |
| 4 | numeric Character UID |
| 8, 10 | packed target X/Y |
| 12 | monotonic client timestamp |
| 16 | action `137` |
| 18 | direction |
| 20, 22 | source X/Y |
| 24 | zero |
| 28 | `0xffffffff` sentinel |

Revora's synthetic jump encoder left offsets 24–31 all zero, and `ParseAction`
required the entire tail to be zero. The genuine sentinel therefore caused the
parser to throw before decoding action 137 or reaching UID, rate, distance,
DMap-accessibility, or elevation validation.

The owner-provided 5165 `PacketHandling/Jump.cs` independently agrees that
target X/Y are the two 16-bit values at offsets 8/10 and that invalid semantic
jumps receive an authoritative position correction. Its older packet builder
does not contain the exact client's final sentinel, so it is corroborating
gameplay evidence rather than wire authority. Licensed COPS v7 commit
`84919e25e79eded39314ea50bfd787427eadd59f` agrees on authenticated UID,
source-position matching, packed target coordinates, maximum distance 17, map
validation, echo/broadcast, and a non-disconnecting kick-back correction.

## Correction

Jump parsing and encoding now require the exact `0 / 0xffffffff` tail only for
action 137; generic 32/10010 actions retain their all-zero tail. Foreign UID,
malformed framing, and invalid tail still fail closed. Rate, origin, distance,
collision, and elevation rules remain enforced. A semantically rejected jump
returns exact-build action 108 with the authoritative position and keeps the
authenticated Game session alive.

Reports contain fixed packet metadata and non-secret coordinates only. No raw
packet, credential, handoff, cipher key, or owner address was retained.

## Validation and deployment

- Release solution build: zero warnings and zero errors.
- Disposable PostgreSQL Playable suite: 17/17 passed, including 20 consecutive
  jumps, interleaved two-minute movement/jump soak, two-client broadcast,
  correction, persistence, reconnect, and UID invariants.
- Windows regression run: `33266652861`, successful.
- Deterministic Linux Host SHA-256:
  `0de17c2d397a29a7880f08b3959f61a12ef8c2e7b6714c3faed212af166a638c`.
- ClamAV: zero detections.
- Production service: READY, enabled, active, and zero restarts after the
  atomic Host-only replacement.
- Rollback copy: protected server path
  `/root/revora-backups/genuine-jump-fix-20260829T1753Z/Revora.Host.previous`
  (SHA-256 `9f5a32e2c5e237e8314cc718faa86d5fd3cefbc17254a3318030a66f86287055`).
