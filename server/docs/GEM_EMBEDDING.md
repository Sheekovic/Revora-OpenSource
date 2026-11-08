# Gem embedding persistence repair

2026-09-27. Prerequisite for reliable ordinary achievement10428 earning.

The existing1027 handler previously saved socket state before material payment.
It also accepted insertion into socket2 without requiring an empty hole and
could erase the first gem while removing the second. The new handler keeps the
existing insertion/removal operations and commits payment and result together.

- Ordinary canonical, loaded players only; no GM or account allowlist.
- Reject trading, booths, Poker, fenced sessions, rented/locked/suspicious items,
  invalid positions, unknown types, arrows, non-gem materials and bound-to-unbound use.
- Both insertion slots must already contain an empty hole. Core equipment accepts
  the eight ordinary gem families; Fan accepts Thunder, Tower Glory, Wing either.
  Talisman gems cannot count toward the core twelve-Super-gem achievement.
- Lock the owner and owned rows in UID order, validate socket preimages, then commit
  the one-unit gem debit, socket, receipt and achievement evidence in one transaction.
- Update the client and equipped stats after commit. A failed projection fences
  the session; reloading recovers the saved socket and remaining inventory.
- A permanent target/gem-UID/socket receipt rejects an old insert even if its
  material stack has units left and the socket was subsequently emptied. A new
  deliberate insertion into that same hole needs a different material UID.
- Removal destroys the selected gem, creates no refund and retains both holes
  and the other socket. No verified removal nonce exists; a delayed removal
  request is indistinguishable from a new request for that socket.

Validation uses the isolated existing PostgreSQL candidate and direct ordinary
server handlers. Native frame acceptance, Host acceptance and live deployment
remain open. Successful-COMMIT reply loss is covered by the follow-up below. No price or reward changed.

## Validation

The isolated build and449 combined equipment earning checks pass. The ordinary
route buys and embeds12 Super Tortoise Gems with four concurrent delivery attempts
per insertion; exactly12 receipts persist and10428 survives production reload.
Seeded boundary cases separately cover nine malformed frames, a missing second
hole, a non-gem numeric alias, Thunder on core gear, bound-to-unbound rejection,
receipt-trigger rollback, stacked payment, delayed insertion replay after removal,
preservation of socket1 when removing socket2, and postcommit projection failure
with full reload. Existing +12, blessing, enchantment and alternate-set checks
remain in the same passing fixture. These tests do not prove native wire parity,
all talisman combinations, or every transport-failure boundary.

## Successful COMMIT response loss (2026-09-27)

The combined fixture now passes453 checks. A bounded loopback relay, reused from
the existing Skill PK test technique, targets only PostgreSQL127.0.0.1:25439 and
the equipment_earning_fixture schema. It observes the backend's successful COMMIT
completion and closes the client connection before forwarding that response.
It logs no database payloads or credentials and stops with the fixture.

The interruption occurs on the ordinary player's twelfth purchased gem insertion,
not a manually granted achievement. The stale cache retains the old gem and no
achievement while the session is fenced. Direct persisted checks find the twelfth
socket, removed material and exactly one10428 receipt together. Production
character/item/achievement reload recovers the earned result; four subsequent
insertion attempts do not charge again, and all twelve embedding receipts remain.
This is real database transport interruption at successful commit, distinct from
the previously tested after-commit projection exception. It is not a Host-process
crash, native interaction test, or proof of every transaction failure boundary.
