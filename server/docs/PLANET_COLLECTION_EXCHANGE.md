# Planet collection exchange — Revora rules

Owner approved 2026-10-01. Rowan at Twin City (319,299) exchanges expedition
credits for permanent, bound cosmetics. Each completed Planet collection
delivery earns 20 credits; each design costs one. Thirteen different weekend
deliveries earn 260 credits. This supplements the existing expedition gifts.
There is no CP purchase, rental counting or daily P7 reward.

The fixed stock contains108 garments and149 mounts. The original156 designs
retain their indices;101 additional mounts fill the remaining reviewed supply
gaps, ordered highest star first then numeric ID. Garments span three through
five stars; mounts now include one through five stars. Ratings stay unchanged.
Each character may claim each design once. Already owned permanent
designs are refused before spending. Unused credits remain available.

Credits derive from matching completed collection runs and completion receipts,
including their frozen weekend bounds, 100 kills and collection step 1–13.
Legacy step-zero runs do not qualify. A character row lock serializes spending;
the gift and unique design/item receipt commit together. Exact inventory
ownership, proximity, canonical session, trade and bag capacity are checked.
Failure after uncertain persistence fences the session for reload.

## Thirteen-delivery extension (2026-10-03)

The first eight reward recipes and the one-time30-day wing award are unchanged.
Deliveries9-13 each pay one bound universal rune4030101 alongside20 credits.
The maximum260 credits covers all257 exchange designs, with three left over.
The schema migration expands the collection-step constraint without renumbering
old runs, purchases or item receipts; completed-step uniqueness remains enforced.

The original156 stock entries keep their dialogue options. A second collection
section holds the additional101 mounts, with navigation and purchase options
remaining within one byte. Both sections use the same serialized transaction,
per-design uniqueness, saved ownership and exact-item installation checks.

Private validation passed552 main checks,518 collection-restart checks and11
other restart checks. It exercises migration of an existing eight-step database,
idempotent initialization, all13 earned deliveries, all257 purchases/deposits,
three unspent credits, closure after step13 and exact276-cosmetic recovery in a
fresh process. All257 repeat claims preserve the full saved snapshot. Prior
rollback, full-bag, concurrency, timing and wing-expiry checks still pass.

[Exact source/build/log evidence](research/PLANET_COLLECTION_EXTENSION.json)
records the scope and controls. The game builds with existing warnings; test
helpers compile with warnings as errors. The old fixture required previous-map
schema fields and its restart totals were updated from175/156 to276/257.
Qualification, calendar and hunt deaths are controlled; this is not natural
combat balance or thirteen real weekends. Process restart does not claim a
PostgreSQL restart. The compiled supplier audit supports200 garments/203 mounts,
but this fixture's276 combined cosmetics do not prove200 owned mounts.
Native display, a combined200-mount achievement check and live release remain open.

## Original eight-delivery validation (historical)


The private Planet suite passes **413 checks**, plus **11 checks in its fresh
child process**, against build SHA-256
`6AF5DECBF893313E1B0644DEE0BFC5140FFB6A0DA990CD7ADE61F0FF7C8DB123`.
The fixture completes eight weekend hunts, defeats their owned sentries and
delivers their caches. Calendar, eligibility and combat damage are controlled;
completion receipts and credits are not inserted as shortcuts.

Through the ordinary Rowan dispatcher it purchases all 156 designs, deposits
them using the wardrobe handler and verifies exact item/receipt ownership,
permanence and binding. Combined Planet gifts unlock achievement11231 for 100
permanent garments. Earlier checks exhaust the first 80 earned credits and
refuse the next purchase. Full bag, distance, invalid selection, injected
rollback, concurrent duplicate purchase and replay are covered.

The child process reloads the first 80 purchases and confirms their exact saved
wardrobe identities plus replay refusal. This is a reconstructed ordinary
character fixture using production item loading, not authenticated full-server
entry or a PostgreSQL restart. A full Server.Run/database restart remains unverified for the final set; the
fresh-process follow-up below now covers its item loading and achievement view. Native menus, live deployment and
client acceptance remain pending.

Potential supplier capacity is now **200 garments / 100 mounts**. The complete
combined 200/100 earning journeys remain unverified. The compatible catalog
still has only 199 mounts, so the 200-mount achievement remains unavailable.


## Complete collection recovery follow-up (2026-10-01)

The focused suite now passes415parent checks,11earlier child checks and316checks
in a second child after all eight deliveries and156exchange purchases. This
child reconstructs an ordinary character and invokes production item/wardrobe
loading. It recovers all175designs (156exchange plus19Planet), checking every
exchange receipt against its exact item UID, type, binding, permanence and
wardrobe membership. The normal achievement Send path reads the saved100garment
receipt and projects11231 into the outgoing achievement view.

All156 repeat purchases return the already-claimed refusal without fencing the
session. Exact before/after snapshots match all owned item metadata, exchange
claims, eight collection runs/completions and collection achievement receipts.
No reward, credit or completion is seeded for recovery. The parent relinquishes
its database-wide session lease before the child starts. The first fixture run
omitted the normal achievement refresh; adding Send fixes that test setup rather
than altering production achievement behavior.

Production assembly SHA-256:
`102604CE650FAD38027D5FA07EEFAB57D1A0CFB887217AFF9F5D912F6748627D`.
No production code change or rebuild was needed for this follow-up. The helper
compiles with warnings as errors. PostgreSQL is private and shared between these
processes; authenticated login, a full server/database restart, native UI and
live deployment remain separate gates. The combined200garment/100mount earning
journeys also remain open.


## Combined earning follow-up (2026-10-01)

The [one-character200garment/100mount journey](COLLECTION_TWO_HUNDRED_AND_HUNDRED.md) now passes ordinary earning, exact game/database restart recovery and replay. This supersedes the earlier unverified-combination notes, without claiming native acceptance or a compatible320-achievement career. The200-mount tier remains unavailable with199compatible designs.
