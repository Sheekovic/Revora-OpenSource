# Daily Envoy hunting materials

Implemented from baseline `abb36bc` on 2026-09-16. This completes the material
earning and pickup prerequisite for Everything Has a Price. The quest's
assignment, list travel, payment and final reward transaction were subsequently
completed in [Daily Price Transactions](DAILY_PRICE_TRANSACTIONS.md).
Nothing in this batch grants an achievement or marks the complete quest done.

## Evidence and retained economy

The [official 2012 guide](https://co.99.com/guide/quests/2012/Everything_Has_A_Price.shtml)
confirms that materials drop before accepting the quest and are tradable. The
old server required the killer's current `PriceItemId` to match the drop. The
new source removes that restriction. Completed players can also gather stock
for later use or trade. The chance remains `DailyPriceHuntDropPercent` (40 by
default); this is Revora's economy, not an asserted official probability.

The installed item and monster catalogs, existing server mappings and matching
`public.monsterspawns` rows were checked read-only. All eight variants have
spawns in their corresponding physical map:

| Monsters | Installed family IDs | Physical map | Material |
| --- | --- | --- | --- |
| BanditL97 / BanditL98 | 55 / 79 | Bird Island 1015 | Bandit's Blade 729096 |
| Macaque / MacaqueL48 | 10 / 69 | Ape City 1020 | Bloody Teeth 729101 |
| SandMonster / SandMonsterL68 | 14 / 73 | Desert City 1000 | Bat Blood 729099 |
| Bandit / BanditL33 | 7 / 66 | Phoenix City 1011 | Blade Scrap 729093 |

Initialization resolves these names to installed family IDs and snapshots the
configured item types. It rejects a missing monster or material catalog entry.
Runtime sources require both the resolved family and expected name/map. Dynamic
map aliases, living or unregistered monsters, guards, companions and bosses
cannot produce these materials. Existing exclusions for event monsters remain.

## Delivery guarantees

`DailyHuntDrops` runs from the actual monster death/drop dispatcher. Each life
gets at most one roll, including failed rolls and concurrent duplicate death
notifications. A successful roll creates one unbound item with a unique item
UID, catalog durability and a protected killer-owned floor identity.

Both sealed floor pickup and the direct auto-hunt dispatcher use the same
transaction. Pickup checks the current session, real location, lifetime, team
or killer ownership window, bag capacity and immutable drop origin. It locks
the character's database row, verifies the inventory count and inserts both
the complete item and its unique `revora_daily_hunt_pickups` receipt. Ground
removal and client inventory updates happen after commit. A failed write rolls
back both inserts, retains the floor drop and requires reconnect before retry.

The new table is additive and contains only material provenance. Existing
materials remain usable; a pickup receipt is not a new turn-in requirement.
The server does not persist ground objects across restart, so an uncollected
drop follows the existing server floor lifetime/restart policy. New items are
unbound; full general player-to-player trade persistence is outside this batch.

## Validation

The isolated server build passes. The focused ordinary-player fixture checks
all eight real-family death paths, native pickup, production item/character
reload, the 40-percent boundary, zero/over-100 configuration, duplicate death
and pickup, malformed frames, origin tampering, lifetime and exact position,
full bags, competing collectors, auto-hunt dispatch and suppressed item or
receipt inserts followed by production reconnect/retry.

Final validation: 475 focused checks (392 shared first-rebirth prerequisites
plus 83 hunting checks), 47 monster-equipment, 262 Grotto, 466 Magnolia and
216 Potency/daily-boss/Extraction checks: **1,466 gameplay checks**. The three
achievement-audit tests pass; the source upper bound stays 219/337 catalog
entries, not proof of a per-character 320-achievement route. Harness SHA-256:
`CF91436457D0495C8D88B7A26C108F9FAAF284841C656DF33346FFC206584D06`.
Logs are under ignored `obj/chi-refill-validation/hunt-*.log`. Database writes
use only the disposable `revora_potency_validation` schema. These tests call
the server's death and packet handlers; they are not a genuine-client combat
capture.

## Follow-up status

Assignment, reissued lists, paid changes, guarded list travel and the complete
reward transaction are now covered by [Daily Price Transactions](DAILY_PRICE_TRANSACTIONS.md).
That work also fixes the default exclusive bound so Phoenix City's fourth
assignment is reachable. Official numerical parity and genuine 6711 visual
acceptance remain open. Neither batch has changed the live server.
