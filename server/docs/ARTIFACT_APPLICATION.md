# Dragon Soul application transaction

Reviewed 2026-09-25. Hash-bound installed Achievement.dat identifies 10501 as
applying a Dragon Soul (SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`).

ArtifactApplication replaces the legacy split-write handler. It validates the
canonical player, bag ownership, target level, known compatibility and binding;
locks the owner and affected items; checks saved inventory/effect preimages; and
commits effect replacement, soul/Meteor debits, scroll change and achievement
10501 together. Effect deletion/insertion, all debits and change writes are
checked. Client projection and achievement refresh follow commit. Failure fences
the session for reload. The saved effect suppresses repeats for one second,
including stacked soul requests. An expired or existing permanent soul can still
be deliberately replaced, as in the legacy handler.

Payment uses loose Meteors first, then the minimum needed Meteor Scroll units.
Stacks count by quantity. A final scroll returns its exact excess as loose
Meteors with that scroll's binding. The old floor/byte arithmetic could return
incorrect change. Capacity is checked before spending; the complete saved bag
must match the session. Bound materials cannot enhance unbound equipment.
Locked, wardrobe, rental, suspicious and foreign materials cannot pay. The existing
catalog Meteor costs and VIP lifetime schedule remain unchanged. These are
installed Revora rules, not newly verified original-server economic rules.

SoulProtection.Load now falls back to the installed
`database/Slayer/souls_protection.txt` when its preferred root file is absent.
The installed fallback has 405 mappings, SHA-256
`b06c415252ea26d72f5229b77fdb4b7896636d1813c0a0a70bcca863fa26ffd9`.
Unknown mappings reject application before payment; the old handler silently
skipped compatibility when a mapping was missing. The catalog audit found 33
unmapped entries with a nonzero purification level: eight non-soul packs and 25
souls in the 801000/801100/801200/801300 families. These require explicit reviewed
compatibility for Nunchaku, Taoist weapons, Fists and Handfans. Their descriptions
provide a follow-up research seam; this batch does not claim all-family support.

Validation: isolated Debug/x86 build, 225 refinery/artifact checks and five audits
pass. Added tests cover concurrent payment once, loose/scroll/mixed payment,
bound change, missing money, full bags, stale ownership/locks/wardrobe, bound
soul denial, wrong targets, missing mappings, malformed frames, suppressed
payment/effect/change/achievement writes, failed replacement preserving its prior
effect, and production item loading. Fixtures supply targets/souls/payment; full
earned acquisition and character/achievement reload remain open. Native UI,
all-family compatibility and live deployment are not certified. Unrelated owner
edits were present in the isolated build. No live data or client patch was changed.

## Modern-family follow-up

The 25 soul identities missing from the legacy file now have explicit compatibility
rules and 553 passing refinery/artifact checks; see ARTIFACT_MODERN_COMPATIBILITY.md.
Its Lotus mapping is documented Revora interpretation, not an original-server
parity claim. The earlier missing-family paragraph records the initial audit.

## Earned-route follow-up

ARTIFACT_EARNED_ROUTE.md now records the earned Terato soul, boss-funded equipment,
Meteors and stabilization stone through both handlers and full character/effect/
achievement/purse reload. All 320 Grotto checks pass. Earlier fixture limitations
above describe the initial batch; native/live acceptance remains pending.
