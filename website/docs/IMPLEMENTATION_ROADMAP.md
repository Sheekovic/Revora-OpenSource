# Verified Interoperability Roadmap

Each vertical milestone is independently written. “Fixture” means bytes from a lawful original 5165 client/capture with provenance and sensitive fields redacted or deterministically replaced. A legacy source alone cannot promote evidence to Confirmed.

## 1. Verified Account login framing

- **Evidence:** hash-pinned vanilla 5165 client; controlled loopback observation establishing initial cipher state, declared/wire length, packet ID, footer, field boundaries, encoding, fragmentation behavior.
- **Tests:** exact known-good frame; one-byte fragmentation at every boundary; coalescing; truncated/oversized/invalid ID/footer; fuzzed lengths; no payload logging.
- **Security:** maximum packet size, timeout, bounded queue, source rate limit, redacted capture process.
- **Non-goals:** password decryption, account lookup, response, Game Server.
- **Reject/rollback:** reject if observations vary without an explained state transition or if provenance cannot be retained.

## 2. Verified login cryptography

- **Evidence:** multiple known input/output pairs from the same client build; seed/no-seed transcript; key/version provenance; independent agreement with licensed/reference evidence.
- **Tests:** deterministic vectors, incorrect key/seed, non-ASCII username/password boundaries, constant-time comparison boundary, zeroization where practical.
- **Security:** transport transform is isolated from stored-password verification; no plaintext/hash logs; reviewed cryptographic implementation/dependency.
- **Non-goals:** inventing stronger wire crypto the client cannot speak; storing client plaintext.
- **Reject/rollback:** keep adapter disabled if any byte transformation is inferred from one legacy source only.

## 3. Server selection response

- **Evidence:** verified 1055 success/failure captures, IP/port widths, token fields, rejection codes, and client transition behavior.
- **Tests:** byte fixtures for success and generic failure; invalid endpoint; exact declared/wire lengths; no account enumeration differences where possible.
- **Security:** loopback default, registration disabled, auth throttling, no raw token logging.
- **Non-goals:** durable character data or public binding.
- **Reject/rollback:** fail closed if the client expects a token representation not safely mapped yet.

## 4. Account-to-Game handoff mapping

- **Evidence:** paired 1055 and 1052 authentic transcript showing which wire fields correlate and when Game encryption begins.
- **Tests:** single use, expiry, realm/account binding, replay, wrong realm, concurrent consume, duplicate login, malformed 1052.
- **Security:** retain Revora's internal random 256-bit token. A narrow wire adapter may expose only the verified compatible representation; collisions and replay must remain bounded.
- **Non-goals:** claiming the internal 256-bit token itself appears on the legacy wire.
- **Reject/rollback:** reject mappings that require predictable global counters or non-atomic shared tables.

## 5. Character list/create

- **Evidence:** no-character and existing-character transcripts; verified 1001/1006 fields, validation ranges, strings, response ordering.
- **Tests:** create fixture, invalid name/body/class, duplicate-name race, replayed token, atomic persistence, existing-character entry.
- **Security:** parameterized persistence, Unicode/legacy encoding policy, no account password accepted as authority after handoff.
- **Non-goals:** inventory, equipment, NPCs, combat.
- **Reject/rollback:** reject any design requiring plaintext password persistence or trusting client account/UID fields.

## 6. Initial map entry

- **Evidence:** verified action/map-info/user-info sequence with IDs, lengths, map/document identifiers, coordinates, and encryption state.
- **Tests:** deterministic entry transcript, invalid map/coordinate fallback, cancellation mid-entry, repeated entry, partial/coalesced encrypted frames.
- **Security:** server-owned map choice and coordinates; bounded snapshot; no arbitrary client map transition.
- **Non-goals:** portals, monsters, items, dynamic maps.
- **Reject/rollback:** remain connected-but-not-ready or disconnect safely if required ordering is uncertain; never send mixed-generation packets.

## 7. Movement

- **Evidence:** walk/run/jump captures establishing whether 5165 uses 1005 or 10005, declared length/footer, direction/mode and coordinate semantics.
- **Tests:** all directions, walk/run, bounds/collision, rate limit, duplicate/reordered input, malformed mode, two clients, fragmented/coalesced transport.
- **Security:** server-authoritative coordinates, movement budget, map ownership, no double application.
- **Non-goals:** combat movement effects, mounts, interactions.
- **Reject/rollback:** reject on client/server coordinate divergence or if only Heroes' double-walk behavior appears to justify the result.

## 8. Nearby-player visibility

- **Evidence:** two-client captures for enter range, move within range, leave range, disconnect, and map change; verified spawn/despawn layouts (1014/10014 and action variant).
- **Tests:** deterministic visibility transitions, simultaneous movement, abrupt disconnect, duplicate suppression, ghost cleanup, bounded slow-consumer behavior.
- **Security:** zone-owned spatial state, limited disclosure, bounded broadcast queues, cancellation cleanup.
- **Non-goals:** monster/item visibility, teams, chat, combat.
- **Reject/rollback:** fail the milestone on duplicate/ghost entities, unbounded fan-out, or any layout inferred solely from a legacy builder.

## Post-M4 order

Only after authentic two-client M4 acceptance: inventory/equipment, NPC/shops/warehouses, monsters/spawns, combat/skills/drops, social systems, trade/vending, quests/events, and finally separately approved client modernization. Each follows the same evidence-first vertical rule.
