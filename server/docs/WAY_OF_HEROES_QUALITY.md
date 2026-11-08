# Way of Heroes: Refined, Unique and Elite equipment sets

## Earning contract (2026-10-02)

Private `database/Slayer/stagegoal.txt` defines these six-piece quality goals:

| Stage | Quality | Opening level | Bound reward |
| --- | --- | --- | --- |
| 102 | Refined or higher (6) | 1 | One Meteor1088001 |
| 202 | Unique or higher (7) | 31 | Three Meteors1088001 |
| 302 | Elite or higher (8) | 61 | FashionSuitPack3001264 |

The owner's approved full-set definition supplies the slot interpretation:
headgear, necklace/bag, armor, main weapon, ring/bracelet and boots, plus any
non-arrow offhand equipped in that bank. Cosmetics, talismans, mount/crop/wing
are not substitutes for missing core pieces. This is the documented Revora
interpretation of the installed six-piece wording, not a claim that original
Conquer used the same predicate.

The claim checks saved items in the selected equipment bank with valid catalog
quality/category, single quantity and matching runtime owner/item/slot identity.
An occupied non-arrow offhand must meet the same quality threshold. Duplicate
slots fail closed; wardrobe/suspicious rows, incompatible slots and expired or
malformed rentals cannot complete the set. Valid unexpired temporary equipment
is allowed. Bag/warehouse items do not qualify. Once paid, the historical unique
claim persists independently of later equipment changes.

Existing installed opening gates, inventory capacity, session guards and atomic
bound item/progress/claim writes remain. Stage302 uses the now-owned seven-day
five-star garment consumer. Super-set stage402 remains disabled pending separate
Moon Box reward verification; no main-stage prize is enabled.

## Ordinary progression through three tiers

`tools/CheckCandidateQualityClaims.cs` adds `career-quality-claims-earn/reload`.
One ordinary non-GM Warrior starts with supplied level140, no rebirth, attributes,
proficiency, 100,000 CPs and six low-level normal-quality items. All qualifying
quality changes are earned through normal paid upgrades: the actual CP shop
sells eighteen DragonBalls at loaded prices, and sealed item-use handlers consume
them to advance all six pieces through Refined, Unique and Elite. Rolls are
controlled successes, with controlled two-second spacing between upgrades.

Each next-tier claim refuses before qualification. With the sixth upgraded item
still in the bag, it also refuses. Normal equipment of that last piece permits
the claim; eight concurrent requests award exactly one milestone prize batch.
The one plus three bound Meteors are consumed through normal level upgrades,
including use replay checks. FashionSuitPack opens through the atomic consumer,
producing one bound five-star Days7 garment which is normally equipped. No
qualifying quality, claim, upgrade receipt or milestone reward is supplied by SQL.

After normal EntitySave and game/PostgreSQL stop/start, exact scoped purse,
inventory/equipment, Chi/Way, upgrade and fashion-consumption snapshots match.
There are exactly three claims, eighteen successful quality upgrades and four
successful Meteor level upgrades. The six equipped core pieces remain Elite;
the bag is empty and claim replay changes nothing.

The isolated committed-source-overlay build passed with existing legacy warnings;
the harness compiled with warnings as errors. Both phases passed first run.
Private executable SHA-256:
`04F8C29C33DE3063AEDFE5BA1AF1E17190C53188BEF5DC7C3DDDD5861006A202`.
Logs: `arena-process-career-quality-claims-{earn,reload}.log`.
Both game closes completed and PostgreSQL stopped. Old executable/manifest
preserved; unrelated owner WIP excluded from the build.

Native/authenticated presentation, natural starting qualification/funding/gear,
other classes, alternate-bank/offhand/rental process variants and interrupted
claims remain unverified. Supplied inputs and controlled rolls/clock are explicit
limits; this does not measure natural odds or progression time. No live deployment
occurred and the retained main-career checkpoint is unchanged.


The subsequent [Super-set batch](WAY_OF_HEROES_SUPER_SET.md) enables stage402 and
verifies ordinary earned-Moon-Box promotion use and the bound DragonBall reward.
Random opening and native/release gates remain independent unfinished work.
