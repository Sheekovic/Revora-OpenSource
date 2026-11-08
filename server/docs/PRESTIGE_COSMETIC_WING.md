# Wall of Prestige selected wing

Owner report: other appearance fields render correctly, but the wall shows the
default equipped wing instead of the player's selected wardrobe wing.

The showcase formerly serialized only the wing equipment's item ID. Its
PrestigeProfile had no cosmetic-wing identity. The corrected profile captures
Entity.EquippedWing for online characters and reads the equipped, known wing
from the saved titles record in the existing read-only ranking transaction.
It does not run login or wardrobe mutation routines for offline characters.

Showcase member field 16 now prefers that cosmetic identity. Field 17 retains
the actual equipped wing's plus level. With no cosmetic selected, the wall
uses the equipped item ID; with neither selection nor item it sends zero.
Cosmetic changes do not alter equipment, ownership or prestige scores.

Exact-client static consumer evidence is recorded in PROTOCOL_RESEARCH.md,
section "Prestige cosmetic wing selection, 2026-09-25". The existing 3257
format already supports this behavior; no client data or executable patch is
needed. Native rendering after this correction remains unobserved.

Validation: isolated build succeeded with the existing legacy warning baseline;
68 Prestige checks passed, including online selection, serialization of two
different selected wings, unequip fallback, no equipment, score invariance,
saved selection parsing and malformed saved data. The read-only saved-player
harness loaded all three existing characters twice, checked serialization and
deterministic score/selection reload. Logs are under ignored
obj/chi-refill-validation/prestige-wing-{build,tests,saved}.log.

Status: source and isolated build are corrected; **not deployed**. The live
server executable remains unchanged. The current full build contains pending
server changes beyond this fix, so it is not an isolated wing-only release.
The previously published client patches 1.0.17–1.0.19 are unaffected.

## Source closeout and event expiry (2026-10-04)

Review found that the pending offline reader trusted selected title bytes even
after an event wing expired. The reviewed implementation now requires a valid
event award for the expiring Infernal, Planet, Romance and Heart wing editions.
The database read rejects revoked, future and expired awards, and reuses the
event ledger's source/key/lifetime rules. Timestamp parameters use explicit UTC
`timestamptz` binding. No login, grant, reconciliation or cleanup writes run for
offline characters.

Cached appearance checks the deadline when serializing, so an offline selection
expires without waiting for another login or server restart. The permanent
Romance champion award takes precedence over overlapping temporary editions,
both online and offline. Missing or expired authorization falls back to the
equipped item and its actual plus; scores and equipment are unchanged.

The reviewed source subset builds, passes75 Prestige regressions, and passes
six saved-player cases across two database-backed read-only ranking reloads.
The latter uses an explicitly guarded disposable loopback database and removes
only its newly created fixture schema. Logs are
`obj/prestige-reviewed-{build,tests,packs}.log`. The earlier68-check result is
historical. Native rendering and coordinated release remain pending; there was
no live deployment or restart.
