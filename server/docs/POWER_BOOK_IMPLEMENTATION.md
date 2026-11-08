# Power Book implementation contract

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence resolved 2026-09-26

The installed, hash-bound 6711 journal identifies a linear thirteen-stage chain,
missions 1840-1852. `tools/audit_power_book.py` validates predecessor links,
level/class gates, NPC coordinates, counters, materials, timed objectives and
reward identity. Derived numerical output is in
[POWER_BOOK_CONTRACT.json](research/POWER_BOOK_CONTRACT.json). No client text
or binary content is committed. Installed audit plus five altered-input rejection
checks pass (predecessor, kill count, plume quantity, missing mission and book identity).

The [official PC guide](https://co.99.com/guide/quests/powerbook.shtml) corroborates
the trial sequence and adds spatial conditions: Gold Ghosts must be led to one
of three locations, wings are cooked at a fire, and ten Minotaur corpses must be
ignited within three seconds of death. It lists armor, experience, silver and
an EXP Ball as rewards. Drop and item-use success odds are not published there.
Original Trial Cave identity and native behavior remain unverified.

## Concrete route from the installed journal

| Mission | Required action |
| --- | --- |
| 1840 | Chang acceptance, Old Beggar, one Meteor |
| 1841 | 20 Slingers |
| 1842 | 10 Gold Ghosts |
| 1843 | 15 Slingers and 15 Gold Ghosts |
| 1844 | Two Bladelings within ten seconds |
| 1845 | Fang interaction succeeds |
| 1846 | Ten Bladelings and ten Agile Rats |
| 1847 | Two Blue Birds within twenty seconds |
| 1848 | Consume a roasted wing |
| 1849 | Five Blue Birds and five Fiend Bats |
| 1850 | Blue Bird Plume and ten burned corpses |
| 1851 | Return to the Beggar; 6,000 silver and one hour EXP |
| 1852 | Chang grants pack 728251 |

Earlier stage rewards declare 100 silver each. Item identities are Training
Badge 721715, Power Book 721924, Fang 721926, raw Wing 721928, roasted Wing
721929 and Plume 721931. The journal range is 115-135; use the same documented
Revora recovery approach as Azure (minimum preserved, no upper cutoff) when
implementing, without claiming that this was the original maximum rule.

## Installed-world conflicts: do not reuse blindly

Read-only loaded catalog and parameterized spawn queries are summarized in
[POWER_BOOK_SERVER_CATALOG.json](research/POWER_BOOK_SERVER_CATALOG.json).
NPCs 4704 and 8796 are absent from the installed table. All seven selected item
IDs load, but the armor pack has no located source item-use handler. Its class
outputs and EXP Ball identity must be verified before producing it as a reward.

The matching monsters spawn on existing Labyrinth maps 1351-1354, already owned
by `LabyrinthQuest`. Family 3147 is **AzureBird, level 140, 5,000,000 HP**, and
also spawns on map 1000. It is not verified as this trial's ordinary Blue Bird.
Do not rename or rebalance that shared family, award Power Book progress from
unrelated Labyrinth kills, or infer original Trial Cave IDs from those rows.
The next implementation must provision distinct owned trial encounters and
validate map geometry/portals independently. No live catalog was modified.

## Remaining implementation sequence

1. Establish four owned trial floors and explicit quest monster definitions;
   verify walkable combat regions, herding points, fire and safe return.
2. Save per-character/per-rebirth acceptance, exact material UIDs and stage.
   Charge the Meteor once for book purchase; distinguish admission cost from
   duplicate interaction retries and document any Revora departure.
3. Process only accepted owned deaths, with corpse deduplication, durable
   counters and bounded UTC windows for timed kills and burning.
4. Implement owned book/fang/wing/plume interactions, explicit Revora odds,
   capacity refusal, atomic exchanges and reconnect recovery.
5. Resolve the original class armor pack and add the owner-authorized valuable
   one-time bound P7 bonus; freeze the promised reward and prevent replay.
6. Project all thirteen saved journal stages, compose private navigation over
   Azure's overlay, then validate ordinary players through earning and reload.
7. Keep native DX9, Host interruption and deployment gates separate and pending.

This evidence batch activates no NPC, item interaction or quest reward. Aggregate
achievement 10931 remains disabled. Source inspection has not established all
Equipment Bonus chains, so completion of this chain alone cannot award it.


## Prepared owned caves and explicit monster profiles

`PowerBookScenes` now allocates empty dynamic copies of base maps 1500-1503.
These are an explicit Revora cave adaptation, not a claim to have recovered the
original Trial Cave IDs. The public class-PK maps are not moved or edited. The
existing Labyrinth layouts are unsuitable for retaining the published herding
and fire coordinates: those cells are blocked in the relevant Labyrinth floors.

`tools/audit_power_book_terrain.py` verifies both map-index bindings, exact native
and server terrain hashes, byte-for-byte client/server cell agreement, and
four-direction walking paths to six landmarks on each floor. All 24 routes pass;
see [derived terrain evidence](research/POWER_BOOK_TERRAIN.json). Entry is (77,25),
return point (77,27), forward gate (140,140), fire (100,66), and the three original
herding coordinates remain walkable. No path requires diagonal corner cutting.

Seven private profile IDs 9918400-9918406 preserve loaded appearances while
explicitly setting trial combat stats. Blue Bird is level 115 with 45,000 HP;
other profiles retain the inspected ordinary-family level/HP values. Attack
900-1,300, defense 200 and movement timings are Revora tuning. No boss flags,
legacy Labyrinth loot, money, potion or special-item drops are inherited.
The shared family 3147 remains untouched. Profiles are prepared definitions;
no actors are spawned or globally registered by this batch.

Allocation requires a locked canonical eligible session, reserves one floor per
character with a capacity of sixteen, checks terrain and leaves NPCs, portals
and monsters empty. Release refuses occupied maps, surviving actors and replaced
map/metadata ownership. This prevents premature disposal or deletion of another
component's registry entry. Terrain preparation itself grants no progress.

The coordinator, durable acceptance/admission, teleport/combat guards, scheduled
cleanup/recovery, actual quest monster spawns and progression are still required.
Do not connect this preparation class to a live admission path until those are
implemented and validated together. Native DX9 and deployment stay pending.


Validation: staged build and 441 checks pass (406 prerequisite checks and 35
scene/profile assertions). The cases cover all four real terrain allocations,
private profile isolation, duplicate ownership refusal, occupied-map rejection,
changed-metadata preservation, idempotent release, invalid floors, fake sessions
and registry cleanup. These are prepared-scene checks, not admitted-player
quest progression or actual combat. Logs: `obj/power-scenes-build.log` and
`obj/power-scenes-tests.log`; tracked tests run in the purse-review suite.


## Saved acceptance and book purchase

`PowerBookStore` now records one run per character/rebirth (0-2), the exact bound
Training Badge UID, a frozen bound-P7 reward type, stage and purchased book UID.
Acceptance preserves the minimum level 115 with no upper cutoff. It requires a
canonical managed character, owner lock and validated caller context both before
work and immediately before commit. A full bag cannot accept the badge.

Book purchase requires that run's owned badge and an unlocked, permanent Meteor
in the inventory. Bound or unbound Meteors are accepted; the quest book is bound.
The transaction consumes exactly one Meteor, issues the book, awards the first
stage's 100 silver and saves a payment receipt plus stage 2. A singleton can be
replaced in a full bag; a remaining stack needs one free slot. Repeated requests
and reconnect cannot charge again. Database inventory and rebirth/level are
checked against the canonical session before mutation.

The staged build and 428 checks pass (406 shared prerequisites and 22 new store
assertions). Coverage includes eight concurrent acceptance/purchase requests per
rebirth, exact UID/reward reload, missing/locked payment, both bag-capacity cases,
failed book insertion rolling back all payment/progress/silver, retry after
reload, and loss of final world context rolling back acceptance. The maintained
purse-review suite includes these cases. Logs: `obj/power-store-build.log` and
`obj/power-store-tests.log`.

Tests supply a controlled world-context predicate: this is durable store
validation, not NPC admission or a complete earning route. The coordinator must
supply registered proximity/scene ownership, hold the session's installation
lease and fence post-commit installation failures. It is not wired to startup
or NPCs yet. No trial-admission fee is charged here: this receipt is specifically
the book purchase, and the separate cave-admission rule remains implementation
work. Stages after the book purchase, final rewards and native gates remain open.


## Durable owned combat objectives

The store now persists two objective counters, a timed-kill deadline, the last
accepted kill time and registered monster-life receipts. Registration requires
an eligible locked owner inside its exact registered private scene, a live
canonical map entity, an isolated trial family and the floor corresponding to
saved progress. The coordinator will register each spawned life before combat.

Death credit requires that same scene/session/entity and its exact player killer.
Each life is consumed once, in the same transaction as counters, stage and the
stage's 100 silver. A monster registered for an earlier stage cannot be reused
for a later objective. Nonmatching deaths grant no progress. Living, foreign or
stale-session actors cannot provide a receipt. Failed stage writes roll back the
receipt and counters so a retained death can be retried after the database fault.
This is not a transaction spanning the upstream combat damage or loot operation.

Implemented hunt stages are 20 Slingers; 10 herded Gold Ghosts; 15 of each;
two Bladelings in ten seconds; ten Bladelings and ten Agile Rats; two Blue Birds
in twenty seconds; and five Blue Birds plus five Fiend Bats. Herding uses a
Revora radius of three cells around the published points. The timer starts on
the first accepted kill; a kill exactly at expiry starts a new one-kill attempt.
Both the counter and deadline survive reconnect. Backward clock movement fails
closed instead of stretching a saved timed attempt.

Staged build and 423 checks pass (406 shared prerequisites, 17 hunt assertions).
The fixture traverses the initial combat stages from normal store acceptance,
uses actual private scenes and registered entities, checks corpse replay, herding,
mixed counters, both exact deadlines, production reload, stale sessions and an
injected stage-write failure followed by one successful retry. Later stages 7
and 10 are explicitly seeded fixtures to isolate their combat contracts; the
missing fang/wing steps are not claimed as implemented or exercised.

Logs: `obj/power-hunts-build.log`, `obj/power-hunts-tests.log`. The tracked
purse-review suite includes these tests. Death state is controlled in the fixture;
native attacks, packet damage, companion credit, scheduling/admission, actor
respawn/cleanup and Host interruption are not proven. Fang, cooking/eating,
plume/corpse burning, final claims and journal integration remain implementation
work. No NPC or death-pipeline hook has enabled this unfinished quest.


## Earned materials and corpse burning

Owned Agile Rat, Fiend Bat and Blue Bird deaths now produce bound fang, raw wing
and plume respectively at the matching stage. The current material UID/type is
saved with the run in the same transaction as the consumed monster-life receipt.
Only one current material is issued; an old corpse cannot reroll its drop. All
three drops use an explicit Revora 50% chance. Fang marking and wing roasting
also use 50% success, since original odds remain unpublished. These are custom
rules, not recovered original probabilities.

Only that run's exact saved, permanent, unlocked bound singleton can be used.
A failed fang attempt consumes the fang without advancing; a successful attempt
finishes the page. Raw wings require proximity within three cells of the fire.
Failed cooking consumes the raw wing; success replaces it with an exact saved
roasted wing, whose consumption advances the quest. Duplicate item requests
cannot use a replaced or consumed UID. Item exchanges, stage and completion
silver commit together and immediately install into the loaded inventory.

A plume can be obtained on floor three during the mixed hunt or afterward by
returning there during stage 11. It is reusable for ten burns, then consumed on
the tenth: this is the explicit Revora interpretation of the journal's single
plume requirement. Floor four deaths record exact corpse location and UTC death
time. Burning requires the registered owner/killer, proximity within three cells,
an unburned receipt, and an age strictly below three seconds. The receipt,
count, final plume consumption and stage-12 silver commit atomically.

Build and 446 checks pass (406 prerequisites and 40 Power Book assertions).
The former seeded later stages were replaced with one continuous store journey
from acceptance/book purchase through all hunt and item objectives to stage 12.
It includes actual private terrain and registered monster entities, failed and
successful fang/cooking rolls, unrelated item refusal, cooked-item and timed
progress reload, repeated use, exact corpse expiry, distance refusal and ten
unique burns. No direct stage updates skip the fang or wing steps anymore.

Logs: `obj/power-materials-build.log`, `obj/power-materials-tests.log`. Deaths,
clock/rolls and scene travel remain controlled fixture operations. This does
not prove live packet combat, NPC admissions, automatic corpse retention/cleanup
or the final NPC hand-in and rewards. Those coordinator responsibilities, journal
projection, Host recovery and native acceptance remain unfinished; no live route
is enabled by this batch. AcceptDeath returns whether counters/items changed;
a stage-11 Minotaur death still saves its corpse receipt when that return is false.


## Final hand-in, saved claims and usable armor pack

The store now completes stage 12 at the Beggar context with 6,000 silver and
one EXP Ball's worth of direct EXP (the existing Revora one-hour conversion).
Normal level caps, attribute/mentor effects, reincarnation recovery and auto-hunt
accounting use the shared atomic progression service. Stage 13 prevents repeat
EXP or silver. A final Chang-context claim consumes the exact quest badge and
book, issues pack 728251 and the P7 type frozen at acceptance, and saves both
output UIDs with stage 14 in one transaction. Both prizes are bound. A failed
soul insertion restores the quest items and rolls back the pack and completion.

The normal fixed-pack item route now opens 728251 into the opener's level-120
Super armor and one bound EXP Ball 723911. Armor preserves pack binding; EXP Ball
is always bound. Explicit Revora armor mappings are Trojan 130109, Warrior 131109,
Archer 133109, Taoists 134109, Ninja 135109, Monk 136109, Dragon Warrior 138109,
Pirate 139109 and Wind Walker 101109. Unsupported professions keep their pack.
This is a documented Revora output contract, not a recovered original pack
algorithm. The first six identity fields for all eleven selected pack/armor/EXP
items match the encrypted client and server catalogs:
[reward catalog evidence](research/POWER_BOOK_REWARD_CATALOG.json).

Build and 474 checks pass (406 prerequisites and 68 quest/reward assertions).
The continuous ordinary-character store journey now starts at level 115 and
finishes earned EXP, silver, rollback/retry, concurrent final claims, exact
production reload and real item-use dispatch of its earned pack. Ten profession
cases cover all class families and both Taoist paths, binding preservation and
reload. Additional cases cover full-bag refusal and failure of the EXP Ball
insert rolling armor and pack consumption back. Logs: `obj/power-rewards-build.log`
and `obj/power-rewards-tests.log`.

This closes reward implementation, not the playable quest gate. Context predicates,
scene transfers and deaths in this test remain controlled. Actual NPC placement,
admission, owned combat lifecycle, item dispatch for trial materials, journal
projection, Host recovery and native acceptance remain unfinished. No live schema,
server or client was deployed.

The shared fixed-pack regression also passes all 556 checks (406 prerequisites,
142 existing fixed-pack assertions and eight Azure ring cases), with results in
`obj/power-fixed-regression.log`. No unrelated pack behavior was intentionally changed.


## Private travel and saved return lifecycle

The scene layer now has an explicit thirty-minute UTC lifetime and a durable
visit intent written before the server-authorized entry teleport. Saved quest
stage must permit the selected floor; an unresolved active visit cannot be
replaced. All four Entity teleport paths consult the scene guard: outsiders and
owners without a server transfer are rejected, while admitted owners can make
short valid relocations inside their own scene. Progression, contest and current
world-context checks precede admission. No new Meteor fee is charged by this
travel primitive; the coordinator's final admission policy remains separate.

Expiry, death and leaving the scene return a live owner to the verified Twin
City landing (466,333), save arrival and retire the visit before disposing of
owned terrain. Reconnect recovery preserves current quest progress and clears
an old visit after saving a safe location. A replaced session can then release
its former scene through Tick. Offline recovery only updates characters whose
saved map matches the recorded private map, preserving unrelated locations.
It refuses a currently loaded session. A suppressed return write aborts before
retiring the active intent, permitting recovery to retry.

Build and 414 checks pass (406 prerequisites plus eight lifecycle assertions).
The focused fixture exercises actual Entity.Teleport entry/exit, outsider and
unapproved-owner refusal, duplicate entry, exact thirty-minute expiry, production
reload into a replacement session, disconnected cleanup, lost-registry recovery,
suppressed return writes and an idempotent second recovery. Results are in
`obj/power-travel-build.log` and `obj/power-travel-tests.log`.

The interrupted-startup case removes the owned in-process registry before
calling recovery; it is not an actual Host termination/restart. These tests
contain no live trial combat actors or companion lifecycle. Startup, Tick and
login recovery still require the final quest coordinator's wiring, as do NPC
entry, spawning, damage ownership and client journal integration. No live route
or native-acceptance claim is enabled by this batch.


## Registered trial combat and corpse lifecycle

`PowerBookCombat` now spawns real monsters from the isolated profiles, registers
their life before sending the spawn and caps a scene at 24 actors. Spawn requests
require the canonical locked owner, matching registered scene, floor-compatible
family and an unoccupied walkable cell. Shared family 3147 is not changed.

The real attack and receive-damage paths take the owner's combat lease and reject
unregistered identities, outsiders and private-scene PvP. Owner companions must
be the currently registered companion, not just carry the same owner UID. Private
family deaths are intercepted before generic death handling: the accepted owner
receives the saved objective credit, and legacy floor loot is suppressed. The
existing normal monster EXP path remains in use. Stage persistence and upstream
combat are separate operations; this is not a single transaction spanning damage,
death presentation and quest rewards.

Recorded ordinary corpses retire after one second. Minotaur corpses remain for
four seconds, covering the entire strict three-second burn opportunity. The map's
legacy respawn timer skips private trial families; new lives must be explicitly
spawned and registered. Pruning and release remove owned actors, and a stale actor
from a released scene cannot keep attacking. No automatic population loop or NPC
admission route is enabled yet.

Build and 416 checks pass (406 prerequisites plus ten combat assertions). Actual
server damage handles a nonlethal hit and twenty lethal Slingers, verifies the
saved stage, refuses outsider damage and death replay, checks floor-loot absence,
validates corpse deadlines and rejects a forged family-only actor. The Minotaur
case is an explicitly seeded stage-11 fixture for retention/cleanup, not another
full-route claim. Logs: `obj/power-combat-build.log`, `obj/power-combat-tests.log`.

Native packet decoding/UI, companion combat under real disconnect races and the
full scheduled quest lifecycle remain pending. The next coordinator must connect
NPCs, item interactions, population refresh, lifecycle hooks and journal projection
before the complete quest can be enabled and accepted by ordinary clients.


### 2026-09-26 - Bounded objective population

`PowerBookCombat.Populate` now supplies stage-specific waves to an entered,
registered owner scene. It maintains eight living monsters (four of each family
for mixed objectives), polls saved progress at most once per three seconds, and
uses a bounded cell search around 90,50. Players, companions and existing bodies
are excluded from spawn cells. The existing 24-entity hard cap includes corpses.
These population sizes and placement are documented Revora rules, not claimed
original mechanics. The coordinator must still schedule this method.

A stage change removes old actors instead of reusing their prior-stage receipts.
New actors receive fresh persisted lives. Material stages stop replenishing once
the saved material exists. Stage 11 supports Blue Birds on floor 2 for the plume
and Minotaurs on floor 3. Wrong-floor and expired visits cannot receive new waves.
Corpse pruning runs between population polls, preserving the four-second Minotaur
retention while replacing its living slot after three seconds. This code does not
recover discarded quest items or enable NPC admission by itself.

Build and 432 checks passed: 406 shared prerequisites, ten previous combat checks,
and sixteen population checks. The first stage uses real damage and automatic
refills for all twenty kills, then verifies fresh Gold Ghost lives and rejects
stale actors. Later stage family tests deliberately seed saved stages; they do
not establish a complete player journey. Minotaur retention, between-poll cleanup
and expiry are checked. Logs: `obj/power-population-build.log` and
`obj/power-population-tests.log`. Tests are registered in the tracked purse review
suite. Full NPC/item routes, lifecycle scheduling, journal projection, ordinary
native-client acceptance and deployment remain pending.


### 2026-09-26 - Connected ordinary-player quest route

`PowerBookQuest` connects the services to the existing Chang (900071727) and a
new Old Beggar (900071841, map 1002 at 466,331, client NPC mesh 4570). Chang's
Azure menu offers Power Book at level 115; no owner name, GM flag or allowlist is
required. The NPC coordinator checks registered proximity and request identity,
uses the session-install lease and owner lock, and repeats context checks through
the transactional store. A remote request cannot accept the quest. Final rewards
remain a bound level-120 Super class armor pack plus the frozen bound P7 soul.
This is a once-per-rebirth quest, not a daily P7 reward.

Revora admission policy: one Meteor buys the book once; retries cost nothing.
This explicitly resolves the original guide's entry-fee ambiguity in favor of
recoverable attempts. Each visit lasts thirty minutes. Beggar enters the current
saved floor, with floor 2 chosen for a missing stage-11 plume. Reading the owned
book near 140,140 advances to the next saved floor; near 77,27 it returns the
player to Twin City. Floor changes first save/retire the old visit before reserving
the new visit. The book also reports the current objective. Raw wings require
100,66; plumes burn one eligible nearby corpse per use. All material/book requests
require the exact owned, bound, permanent, unlocked item. Existing saved-UID
checks protect their effects.

Startup initializes schemas, validates terrain and appearances, recovers interrupted
visits, and creates the Beggar. Login invokes saved-return recovery. World Tick
runs travel cleanup and bounded population under the owner/session locks. These
are source hooks; no live database migration, Host restart, client patch or live
NPC deployment was performed in this batch. Discarded quest-item recovery remains
unimplemented. Journal projection and native map/item/dialog acceptance remain
pending; those limitations prevent claiming the whole feature accepted.

Build and 423 checks passed (406 fixture prerequisites and seventeen new route
assertions). An ordinary level-115 character uses the real NPC dispatcher to
accept/buy/enter, actual damage to complete every hunt, real item dispatch for
fangs, cooking, eating, floor transitions, corpse burning and return, then NPC
hand-in/claim and reload. No quest stages are seeded for this route. RNG is forced
to successful rolls; time is controlled and the herding fixture positions monsters
at the target rather than proving native luring AI. Remote acceptance, payment and
claim replays, off-fire cooking, persisted reward receipt and single reward output
are checked. Companion and real Host interruption/native acceptance remain pending.
Logs: `obj/power-quest-build.log`, `obj/power-quest-tests.log`; the route is also
registered in the tracked purse review suite.


### 2026-09-26 - Saved journal and private directions overlay

PowerBookJournal registers missions 1840-1852, projects the durable current-life
stage, and suppresses conflicting legacy entries. Stages 1-13 show their matching
accepted mission and finished predecessors; stage 14 shows all finished. Begin
requests cannot accept/finish these missions. The NPC quest-abandon path is also
routed to refresh instead of deleting state; that branch has source review but
no new native packet acceptance claim. NPC and material changes refresh the GUI;
hunts refresh only on stage advancement, avoiding a full journal send per kill.
The journal exposes stage status, not live per-monster counters or countdowns.

Build and 452 server checks pass: the previous 423-route fixture plus thirteen
forged-Begin rejections and sixteen journal projections covering unaccepted state,
every stage and completed reload. No progress stages are seeded in the route.
Logs: `obj/power-journal-build.log`, `obj/power-journal-tests.log`.

`tools/ClientPatches/prepare_power_journal.py` composes a private overlay over the
Azure journal. It checks the approved DX9 executable, active journal and prior
staged source hashes. Only thirteen Power Book sections change; unrelated bytes
are verified identical and the rewrite is idempotent. Correct NPC aliases,
level ceiling, bound rewards, free retries, private trial directions and book
travel replace stale Labyrinth/static-objective links. The source mission 1851
contains `Intention1Talk to` without `=`; the exact known typo is repaired. Four
Python tests cover preservation/idempotence, missing/duplicate missions, malformed
fields and the specific hand-in typo.

Private output: `obj/chi-refill-validation/power-book-journal/`.
SHA-256: `65e897ecdd50bcbfa69bb0f72cf81c4f858f7dfaa4384f0a46876118fe87335c`.
No proprietary journal bytes are committed. The overlay is unsigned, uninstalled
and unpublished. Native journal presentation, route buttons, actual herding,
companions, Host interruption and discarded-item recovery remain pending. The
active client and live server were not changed.


### 2026-09-26 - Lost-item recovery without progress reset

Chang and Old Beggar offer recovery during an unfinished quest. The server locks
and verifies the saved owner/inventory/current-life quest, then checks each tracked
UID in the entire items table. Any surviving row, regardless of owner or storage,
prevents replacement. Truly absent badge/book UIDs receive fresh bound singleton
items in one transaction with updated tracking and unique recovery receipts. The
original Meteor purchase receipt is retained; no additional payment or reward is
issued. Full inventory leaves the request retryable without partial replacement.

A truly absent tracked fang, wing or plume clears only the material reference,
allowing the existing hunt/drop path to earn another. Stage, counters, timed-hunt
state, reward selection and completed burns are preserved. A lost cooked wing must
be hunted and cooked again. A missing stage-11 plume reopens floor 2 on admission.
Completed and unaccepted quests cannot generate replacement items. Rebirth lookup
continues to use the current life. Database/installation failures retain the
existing persistence fence; the new recovery receipt is committed with the items.

Build and 462 checks passed (the prior 452 plus ten recovery assertions). Tests
exercise the real recovery NPC route, lost badge/book replacement, replay,
reload, stored-book refusal, full-bag retry, lost-fang re-earning, stored-plume
refusal, preserved burn count and completed-quest refusal. Loss and warehouse
conditions are deliberately arranged in persistence fixtures; material stages
are seeded for these focused checks. The separate full quest route still runs
without seeded quest progress. Logs: `obj/power-recovery-build.log` and
`obj/power-recovery-tests.log`.

Source review confirms bound item disposal deletes its row; ordinary ground drops
retain an items row with entityid=0, which recovery refuses to copy. Native disposal
and concurrent warehouse/trade UI acceptance are not claimed by the fixture.
Companion/native herding/real Host interruption checks and live deployment remain
pending. No live schema, Host or client was changed in this batch.


### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001, life2, buys one Meteor for the
  catalog price (3 CPs), pays it to the NPC and completes all four owned floors.
  Calculated ranged attacks earn the hunts; ordinary monster movement earns
  the herding requirement. Item-use gates, cooking and burning reach stage12.
- [x] A separate process claims after the completed hunt is saved. Final rewards
  are bound 728251 and one P7 soul; the complete silver gain is 7,100. Another
  process preserves exact career, hunt-life, purchase and reward state. Repeated
  report/claim changes nothing, and consumed quest items remain absent.

The first hunt run completed and returned to Twin City but its final fixture
assertion expected only +100 silver, overlooking +1,000 in stage rewards. It
failed; it is not recorded as a fully green hunt phase. A read-only comparison
showed stage12, +1,100 silver, -3 CPs, the tracked badge/book and no changes to
existing items. A one-use exact-hash guard adopted this already committed state
as checkpoint1 without replaying payment/combat or editing quest data. The guard
and diagnostic branch were removed; clean `career-retained-power-reload` passed.
Two fixture compilation errors (local name collision and qualified BitConverter)
were corrected before their respective runs; neither executed quest mutations.
Prior snapshot SHA-256:
`7e1f62078ea657a2134ec862bac4386aac62baa14e090a141657869b647827dc`.
Completed-hunt snapshot SHA-256:
`b185d2002b2d786946bbed6888ba944bd210fe4987eaa0999e6a9d3b1bae59e4`.

Passing final phases: `career-retained-power-claim` and
`career-retained-power-reload`. Private game SHA-256:
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
Clock, rolls and approach positions are controlled. Native rendering/navigation
and deployment remain pending. Retained aggregate16/19; the defense talisman,
attack talisman and Tempest Wings chains remain outstanding on this character.
