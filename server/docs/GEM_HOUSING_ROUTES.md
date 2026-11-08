# Gem-funded housing

2026-09-20, implementation baseline `13d090b`. This connects the gem alternatives
to the same owned construction transaction as the certificate routes.

## Ordinary player service

House Admin 30156 in Twin City now offers “Build with gems” until the player
owns a level-5 house. The next level is quoted and confirmed through the existing
NPC dialog flow. The [official housing guide](https://co.99.com/guide/guides/house_change.shtml)
specifies these alternatives:

| New level | Materials | Silver |
| --- | --- | --- |
| 1 | 6 refined gems | 1,000,000 |
| 2 | 7 refined gems | 2,000,000 |
| 3 | 8 refined gems | 3,000,000 |
| 4 | 2 super gems | 4,000,000 |
| 5 | 40 super gems | 80,000,000 |

The ten installed gem families are explicitly whitelisted. Mixed gem types and
bindings are accepted, and bound gems are selected first with disclosure in the
quote. This binding-selection policy is Revora behavior, not a claim that the
guide specifies it. The separate installment route's exclusions are not applied
to these direct gem payments without evidence. Only eligible inventory singletons
are selected; locked, stored, equipped, temporary, suspicious and stacked objects
are excluded. Legacy zero-stack singletons remain accepted.

The two-minute offer freezes each material UID, type, stack, binding and object
reference. Confirmation consumes the offer once, checks the next house level,
canonical ownership, proximity, saved location, eligibility, silver, furniture
and occupants, then commits all material deletions, silver, house, reservation
and achievement together. Every consumed material also has a durable child row
in `revora_house_build_materials`. The older build receipt's `material_uid`
retains the first selected UID for compatibility; it is not the complete list
for a gem route. Existing historical receipts are not backfilled with guesses.
Certificate construction now writes the same complete material receipt.

An unsuccessful participant rolls back all changes. After commit the service
updates the purse and bag, publishes the owned house, and refreshes achievement
state. Failures require production reload before retry. The existing stable
private-map identity, foreign-entry prevention, and empty-house requirements
remain in force. No CPs are required by a gem-funded build.

## Validation

The isolated build and `Verify-PotencyHunt.ps1 -HousingOnly` pass **217 checks**.
This includes the existing certificate, material-exchange, travel and scroll
regressions plus all five gem-funded levels with exact funds, simultaneous
confirmations, complete material receipts, all five achievements, owned entry
and production character/item/title reload. The level-5 case consumes all forty
slots. A further request cannot construct a sixth level.

The first level starts with an ordinary player buying/equipping a pickaxe and
buying one return scroll. Accepted walking, the installed portal and Forest Mine
assistant lead to Maple Forest. The actual mining handler supplies 90 normal
Phoenix gems; six native-format composition requests consume those gems and
60,000 silver. The purchased scroll returns the player to Twin City, where the
six refined outputs fund construction. No gem is inserted directly for this
first-house route. The test reconnects before construction and after entry.

The later four levels use fixture-provided refined/super gems to exercise all
payment and construction requirements. Starting silver is also fixture-provided:
90,061,200 covers the pickaxe (1,000), scroll (200), composition (60,000) and all
five building payments (90,000,000), leaving exactly zero. Mining rolls are
controlled successes and the clock advances two seconds per yield; this is not
a drop-frequency, elapsed-play-time or gold-hunting test. Full mined quantities
for the later gem levels remain a separate earning-integration gate.

Negative checks cover missing/wrong-grade materials, locks, storage/equipment,
temporary/suspicious/stacked gems, insufficient silver, trade, unoffered confirms,
changed bindings and foreign saved ownership. Suppressed silver, material,
house, parent receipt, material receipt and achievement writes each roll back
and permit exactly one successful retry after production reload.

No production deployment, live schema mutation or client patch was performed.
Genuine-6711 NPC presentation and full rendered playthrough remain unverified.
Furniture, item boxes, guests, later voucher acquisition and installments remain
unfinished housing work.
