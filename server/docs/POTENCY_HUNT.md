# Potency Hunt on map 1217

Owner request: add 1,000 replenishing monsters at MrKaelos's saved map, make
them slightly harder than AlienSerpent, and drop Outstanding/Excellent/Supreme
Exploit Packs with final owner-selected probabilities 10% / 5% / 1% (84% no pack).

## Player route

PotencyGuide in Twin City (432,347), next to InnerPowerShop, briefly introduces the
packs and offers free travel to map 1217 near (561,549). A second guide at
(559,547) returns players to Twin City. Both require a living, authenticated
ordinary player physically within six tiles, with no trade or booth active.
No GM, owner-name, class or account allowlist is required. Entry is free.

Pick up dropped packs and open them from inventory. Potency upgrades unlocked
Inner Power techniques; books from InnerPowerShop unlock the techniques.

| Pack | Item ID | Potency | Chance per kill |
| --- | --- | --- | --- |
| Outstanding Exploit Pack | 3007108 | 400 | 10% |
| Excellent Exploit Pack | 3007109 | 600 | 5% |
| Supreme Exploit Pack | 3007110 | 1,000 | 1% |
| No pack | — | — | 84% |

One mutually exclusive roll per death, at most one pack. These dedicated
monsters use this loot table instead of the legacy general drop table;
normal kill experience still applies. Existing map monsters retain their
own spawns and loot. There are 1,000 additional PotencySerpents, not a removal
or replacement of the existing population.

## Difficulty and population

Runtime AlienSerpent ID 1413, mesh 204, level 112, life 35050, attack 900,
defense 250, attack interval 2000ms is the baseline. PotencySerpent family
9901217 uses the same appearance, level, movement, attack style and interval:
43,813 HP (+25%, rounded up), 1,035 attack (+15%), 275 defense (+10%).
The client Monsters.txt contains a separate display level; server template
level is authoritative for this comparison.

The map owns a population object and synchronization gate. It places 1,000
entities on distinct walkable tiles in the reachable area within 100 tiles
of (561,549), leaving space immediately around arrival. Repeated or concurrent
ticks cannot create more than 1,000 managed entities. Death processing and loot
are guarded once per life. Respawn reuses the entity after one second, on the
next existing 500ms map tick; real kill-to-return latency is about 1–1.5s.
Short dead intervals are expected: this is a maintained target, not a promise
that the live count never dips during a kill. Unexpectedly missing members
are replaced. Blocked tiles postpone placement rather than spawning in walls.

The existing NPC meshes and monster model are used; no client patch is needed.
Runtime-only guides use UIDs 900000018/900000019 and are recreated on startup.
The runtime emits `database/potency-hunt-status.json` approximately every ten
seconds with map, target, tracked and alive counts for deployment checks.

## Pack ownership and persistence

The old use handler could grant points before discovering a consumed item
was absent. The new handler validates the exact inventory instance, ownership,
stack count, ordinary eligibility, overflow and trade state under the player's
item lock. Pickup also serializes the shared floor object and verifies that
the exact object still exists on the map, with existing owner/team protection.

A new additive PostgreSQL table `revora_potency_awards(entityid bigint primary
key, points bigint)` records cumulative pack awards. Item consumption, the
cumulative award increment and existing War Exploit balance update commit in
one transaction. Client notifications follow commit. Ambiguous failures fence
the player until reconnect. Existing Union Fund bonuses retain their legacy
post-commit update behavior; they are not part of the new Potency transaction.

InnerPower.txt appends an applied cumulative-pack total to each existing row.
Old rows read as zero. Startup and login reconcile committed awards beyond
that checkpoint exactly once, so a consumed pack cannot be lost between the
SQL commit and the next file save. The file now saves by atomic replacement,
and snapshots synchronize with pack use and technique changes. Existing
technique progression still follows its established checkpoint timing.

Do not drop/reset the cumulative-awards table independently of checkpoints.
For rollback after players start using packs: stop gracefully, retain the
current award table and InnerPower.txt together, and preserve the backup
assembly. The old reader ignores trailing checkpoint fields, but an old
writer discards them; do not run old pack handlers against the new ledger and
then upgrade without reconciling/resetting markers and totals together.

## Validation

`tests/PotencyHunt/Verify-PotencyHunt.ps1` uses a disposable PostgreSQL schema
and private checkpoint directory, the real cached 1002/1217 terrain, the
built server assembly and ordinary non-GM player fixtures. 36 checks cover
population, difficulty, exact drop boundaries, duplicate deaths, replenishment,
guide entry/return and remote rejection, item pickup/ownership, all pack awards,
concurrent replay, stacks, trade, overflow, commit recovery, persistence and
injected database failure. Existing 39 progression-shop checks also pass.

No real player inventory or balance is edited by tests. In-game visual review
and the owner's combat-balance playtest remain separate from these fixtures.

## Dialogue and Pheasant tuning

Owner preference: NPC dialogue must be brief and sound like a person in the game world. Keep drop tables, spawn counts, and technical mechanics in the website guide. PotencyGuide offers one short greeting and Take me there./Take me home. choices.

Pheasant flocks double their configured max_per_gen once during spawn loading (exact family name Pheasant). Database baselines remain 100/40/100 at Twin City (552,235), (665,313), and (444,480); runtime targets are 200/80/200, total 480. Bounds, strength, loot, and respawn timing remain as configured. Repeated server restarts do not multiply the database values. Real-terrain fixtures check the total, each flock, and distinct placement.
