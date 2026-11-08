# Frantic Monkeys implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence and scope (2026-09-26)

The [official guide](https://co.99.com/guide/quests/franticmonkeys.shtml)
describes a Macaque heart investigation, a monkey disguise and poison trial,
a 60-second recovery, and a summoned ape encounter at Ape City 795,471.
The vial has a one-minute use interval. Bao receives the resulting sword;
Chang grants the equipment reward. The quest resets after rebirth.
The guide does not give the heart drop odds or exact trial/encounter population.
Do not substitute the older Facebook badge/bonus-EXP-ball version.

The installed 6711 journal confirms six missions (1745?1750), levels 50?70,
all classes, sequential prerequisites, five trial kills, and final pack **728243**.
It identifies Chang 8796 at 1002 473,300, Bao 4485 at 1020 553,584, and Li 4486
at 1002 440,343. The Revora Chang alias is 900071727. The heart hunt points to
1020 668,673. Four stage payments of 100 silver and a later 3,000 silver plus
half an EXP Ball total 3,400 silver and thirty minutes of experience.

Items: Decayed Heart 721699, Poison 721700, Metamorphic Vial 721701, Devilish
Sword 721702. Poison and vial are distinct items and stages; neither can be
accepted as the other. Pack 728241 is an unrelated necklace reward.
`tools/audit_frantic_monkeys.py` checks the hash-bound journal, exact stage links,
NPC coordinates, kill/material objectives and item identities. Derived metadata
is in `research/FRANTIC_MONKEYS_CONTRACT.json`; three rejection/contract tests pass.
No original packet layout is newly inferred or enabled.

## Implemented reward prerequisite

Ordinary fixed-pack dispatch now handles 728243. It gives one permanent Elite
level-55 weapon with +0, copies pack binding, snapshots the player's class,
and atomically consumes the pack, persists the weapon and records its receipt.
Unsupported classes keep the pack. The quest's eventual pack will be bound.
No extra EXP Ball is added: the installed pack description names only a weapon.

| Class | Weapon ID | Name |
| --- | --- | --- |
| Trojan | 410108 | ColdBlade |
| Warrior | 510108 | SharpGlaive |
| Archer | 500098 | LongBow |
| Ninja | 601108 | NightKatana |
| Monk | 610108 | BeadsOfTruth |
| Pirate | 611108 | MelodyRapier |
| Dragon Warrior | 617108 | FlameNunchaku |
| Water / Fire Taoist | 421108 | CopperBacksword |
| Windwalker | 626108 | PrideFan |

These class choices are documented Revora mappings, not claimed original
class-selection code. Client and server catalog metadata agree for all nine
outputs; hashes are in `research/FRANTIC_MONKEYS_WEAPONS.json`.
Build and **435 checks pass** (406 shared prerequisites + 29 pack checks):
ordinary dispatch for ten final classes, binding, full bags, receipt/reward reload,
replays and unsupported classes. Logs: `obj/frantic-pack-build.log` and
`obj/frantic-pack-tests.log`. They do not prove quest earning.

## Remaining implementation sequence

- [x] Installed quest contract and class reward catalog.
- [x] Transactional ordinary reward-pack opening.
- [x] Persisted per-character/per-rebirth investigation and tracked items (store component).
- [x] Genuine Macaque kill validation, item recovery and atomic heart/poison exchange component.
- [ ] Ordinary NPC/death dispatch integration and full earning route.
- [ ] Bounded poison trial: monkey appearance, 60-second wait, five owned kills,
  automatic return, disconnect/restart recovery and no remote/foreign credit.
- [ ] Owned vial encounter at 795,471: one-minute cooldown, ape progression,
  exact killer credit, sword delivery and restart/retry safety.
- [ ] Final one-time bound pack plus frozen bound P7, silver/EXP, journal and
  ordinary-player end-to-end persistence tests.
- [ ] Private client directions, terrain/asset validation and native acceptance.
- [ ] Actual Host restart, deployment and signed client publication.

Use minimum level 50 without the legacy upper cap, once per rebirth 0/1/2,
consistent with the preceding Revora equipment quests. P7 is permitted because
this is a one-time quest, and must remain bound. Do not claim quest completion
from the pack implementation. Achievement 10931 stays disabled.

Before enabling the trial, establish its map/monster/disguise evidence; do not
replace the timed encounter with dialogue completion. Any custom drop odds or
population/recovery rules must be explicitly documented as Revora behavior.


## Investigation store checkpoint (2026-09-26)

`FranticMonkeysStore` now persists acceptance, Bao referral/inspection and Li's
heart-to-poison exchange. It freezes the bound-P7 selection at acceptance, pays
100 silver for each of the first three completed steps, and tracks the exact
bound item UID. Level/rebirth, canonical session, trade state, owner/item locks,
saved inventory and final context are checked around each transaction.

Heart earning requires a genuine nearby kill by the owner on map 1020, family
10 (Macaque) or 69 (MacaqueL48). **50% is an explicit Revora chance**, not an
original published rate. A weak per-body death marker prevents roll/credit
replays and is cleared only for a living revived body. Wrong-map/family,
pre-referral, repeated, already-earned and full-bag deaths cannot issue hearts.
A lost heart must be hunted again; Bao's completed inspection and payment stay
saved. Poison replacement is one-for-one. Recovery refuses any surviving item
row, including storage or changed ownership, and records durable receipts.

Build and **447 checks pass** (406 shared + 41 investigation checks), covering
rebirth 0/1/2, genuine deaths, failed-roll replay, bag limits, reload, stored
heart rejection, re-hunting after inspection, poison-insert rollback, full-bag
exchange, exact silver, frozen soul choice, and durable poison recovery.
Logs: `obj/frantic-investigation-build.log` and
`obj/frantic-investigation-tests.log`. These invoke the persistence component
under its required locks; they do not prove NPC or timed encounter integration.
Production startup does not initialize this new store yet.

### Scene evidence for the next batch

The read-only loaded catalog is in `research/FRANTIC_MONKEYS_SERVER_CATALOG.json`.
Bao 4485 and Li 4486 have no loaded public NPC rows. Installed `npc.ini` contains
OfficerBao model 687 (simple object 4) and RoyalDoctorLi model 688 (object 265).
These are asset identities, not native interaction acceptance.

No loaded monster names start with Venomous or ApeLeader. Macaque 10 is level
47, HP 1,467, mesh 156; MacaqueL48 69 is level 48, HP 4,400, mesh 156.
Legacy spawn 1504 references map 1523 with Macaques, but **1523 is absent from
both loaded GameMap indexes and has no server 1523.map**. It is not a usable
trial scene. This does not identify the original trial map. The next step must
validate a real compatible scene and document any Revora population/encounter
choices; it must preserve the 60-second wait and five actual kills.


## Validated private trial terrain (2026-09-26)

`tools/audit_frantic_terrain.py` validates exact installed client/server hashes
and map bindings, then traverses only cells accepted by both grids. All quest
landmarks pass: Chang to Li is 88 cardinal steps; Bao to the heart-hunt landmark
is 204, and Bao to 795,471 is 433. The four surrounding planned spawn cells
also pass. Results are in `research/FRANTIC_MONKEYS_TERRAIN.json`. Twin City
still differs in dimensions/flags, and both maps contain terrain differences;
this is shared-path evidence, not terrain parity or native navigation acceptance.

**Revora scene choice:** the poison trial uses a private dynamic copy of map
1020, entering at 795,471, instead of the unavailable legacy 1523 reference.
`FranticMonkeysScenes` validates the server terrain hash before allocation,
creates no inherited NPCs/monsters/portals, records owner metadata and permits
at most eight reservations, one per character. A ten-minute deadline is stored
for the upcoming coordinator; automatic expiry is not implemented here. Release
checks exact scene/map/metadata ownership and requires evacuation and actor
cleanup. This component alone does not admit players or spawn monsters.

Explicit Revora level-50 profiles use the verified Macaque mesh 156 without
copying catalog loot/boss properties:

| Profile | Family | HP | Attack range of values |
| --- | --- | --- | --- |
| PoisonTrialMonkey | 9917450 | 1,467 | 120?200 |
| VenomousApe | 9917451 | 4,400 | 260?380 |
| ApeLeader | 9917452 | 8,800 | 400?600 |

All use defence 100, melee distance 2 and attack interval 1,500 ms. These are
custom combat values, not recovered original stats. Player monkey rendering
remains a separate unverified requirement; a monster mesh alone does not prove
native player disguise compatibility.

Build and **432 checks pass** (406 shared + 26 scene checks). Evidence covers
clean profiles, unchanged shared Macaque data, empty private terrain, owner
checks, duplicate reservation, occupied cleanup rejection, replaced metadata,
repeated allocation/release without registry leaks, invalid profiles, fake
sessions and non-UTC clock rejection. Logs: `obj/frantic-scene-build.log` and
`obj/frantic-scene-tests.log`. Native scene entry and concurrent live combat are
not covered. No production caller enables the scene component yet.

- [x] Compatible shared terrain routes and owned scene allocation/release.
- [ ] Durable timed trial, appearance, owned combat, automatic return and recovery.


## Durable poison trial component (2026-09-26)

`FranticMonkeysStore.Trial` reserves a saved attempt using the owned scene's
visit UUID, consumes the exact bound poison, records its dynamic map ID and
UTC unlock/deadline, and moves stage 4 to stage 5 atomically. The unlock is
60 seconds after reservation. The caller must reserve the appearance/scene and
supply its in-memory context before consumption; automatic travel and appearance
are not implemented by this store.

Trial kills require the same registered owner/scene, canonical map entity,
private trial family 9917450, actual owner kill and nearby position. The saved
attempt must be active, unlocked and unexpired. Weak body markers reject early
corpse replay; database receipts uniquely identify each attempt/body pair.
The fifth committed kill completes the attempt and saves stage 6. Extra deaths
cannot increase the count. A later Doctor Li coordinator can deliver the bound
vial and fourth 100-silver payment atomically, advancing to stage 7. Full bags
and insertion failure leave the won trial available to retry.

Interrupted attempts can be recovered only after the runtime scene reservation
is released. Recovery atomically marks the attempt abandoned, restores one
bound poison, clears partial kills and returns to stage 4. An old attempt
cannot issue another replacement. Completed trials cannot be abandoned into
extra poison. The coordinator still needs to evacuate/clean up expired or
interrupted scenes and call this recovery path; no production dispatch is added.

Build and **491 checks pass**: 406 shared prerequisites, 41 investigation checks
and 44 trial checks across rebirth 0/1/2. Trial evidence covers final-context
rollback, saved reservation times, live-scene recovery rejection, real death
before unlock, the 59/60-second boundary, early/repeated corpse rejection,
expiry, released-attempt recovery, new-attempt count reset, four/five kills,
full-bag completion, vial-insert rollback, payment/reward replay and reload.
Logs: `obj/frantic-trial-build.log` and `obj/frantic-trial-tests.log`.
The fixtures place actors into the private scene and invoke its store credit;
they do not establish automatic population, travel, appearance or attack isolation.

- [x] Durable 60-second gate, per-attempt five-kill receipts and completion.
- [x] Abandoned-attempt poison recovery and one-time vial/payment persistence.
- [ ] Appearance, automatic travel/population/return, combat guards and scene cleanup.
- [ ] Ordinary NPC/item/death routes, vial encounter and final quest rewards.


## Owned trial combat integration (2026-09-26)

`FranticMonkeysCombat` is now connected to the ordinary attack validation,
damage lease and entity death handlers. Families 9917450?9917452 reject
unregistered actors; only trial family 9917450 can currently be spawned by this
component. Registered trial fights permit only their owner or that owner's
registered companion. Other combat involving the reserved trial map is blocked.
Companion native combat acceptance remains unverified.

Population reads the saved trial at most once per three-second pulse, waits
until its unlock time and requires the exact visit/map pair. It uses only the
five audited landing points, avoids the owner, companions and existing bodies,
and caps all bodies at ten. At the central entry position four initial cells
are free; after a kill, pruning/replenishment supplies the fifth required life.
Successful deaths invoke the durable trial store through the normal death path.
Corpses expire after one second, recorded deaths cannot pay twice, and completed
trials stop population. Attack eligibility also respects the saved unlock time
if the UTC clock moves backward after spawn.

The runtime coordinator must still invoke population for entered scenes and
handle admission, appearance, completion return and interrupted cleanup. No
production startup or ordinary quest NPC/item route enables this runtime yet.
Tests that separately inspect store behavior now call the death core explicitly;
this avoids bypassing the newly installed public rejection of unregistered
actors while keeping their persistence-only scope explicit.

- [x] Registered trial attack/damage/death isolation and ordinary kill credit.
- [x] Timed bounded population, corpse cleanup and stop-on-completion behavior.
- [ ] Automatic admission/appearance/return and disconnect/expiry cleanup.
- [ ] Vial encounter, final earning route and native acceptance.

Build and **493 combat checks pass** (406 shared + 41 investigation + 46 trial/
combat checks), including rebirth 0/1/2, empty wait-period population, direct
spawn rejection before unlock, occupied landing avoidance, repeat pulses,
foreign damage/direct-death rejection, unregistered clone rejection, real
owner attacks, death replay, replacement life, five-kill completion and corpse
cleanup. The **491 persistence/trial checks also pass** after the new guards;
these suites overlap and must not be added. Logs:
`obj/frantic-combat-build.log`, `obj/frantic-combat-tests.log`,
`obj/frantic-trial-regression.log`. Fixtures enter scenes directly; automatic
travel and native play remain unproven. No live deployment.


## Trial travel and recovery component (2026-09-26)

`FranticMonkeysTravel` now admits only the canonical owner with the exact saved
active trial UUID/map and stage 5. It persists a visit intent before the actual
teleport, verifies arrival and saves the character position. A thread-local
server transfer permits entry; normal teleport overloads reject outsiders and
unadmitted owners. Already admitted owners can move locally within their scene.

The scene tick returns completed stage-6 trials near Doctor Li at 1002 440,345,
retains reward eligibility, retires the visit and clears only owned trial actors
before terrain release. Expiry, death or departure also trigger return/cleanup;
failed stage-5 attempts restore one poison only after the reservation is gone.
Offline cleanup saves a safe durable position before releasing terrain. Login
recovery returns a replacement session, and poison recovery waits until the old
runtime reservation has been released. Startup location recovery is implemented,
but actual Host interruption/restart acceptance remains pending.

Build and **512 checks pass** (406 shared prerequisites + 106 investigation,
trial, combat and travel checks). The runtime branch now uses actual entry
teleports. Evidence includes unadmitted/foreign teleport rejection, durable
visit/arrival, idempotent entry, exact expiry, offline return and reload,
replacement-session recovery, fresh retry, normal owned combat and automatic
completion return preserving stage 6. Logs: `obj/frantic-travel-build.log` and
`obj/frantic-travel-tests.log`. Native rendering, networked client acceptance,
actual process restart and ordinary NPC/item-driven admission are not covered.

The code has no production quest initializer/tick/login caller yet. The next
coordinator must initialize/recover travel, call its tick/login hooks and reserve
the disguise before poison consumption. Appearance must last only until the
saved unlock time and must not mutate identity/HP. Do not mark the ordinary
quest flow complete until those routes and the vial encounter/final claim exist.

- [x] Saved trial admission, actual teleport guards and automatic completion return.
- [x] Expiry/offline/replacement-session cleanup and poison retry after release.
- [ ] Monkey disguise and ordinary NPC/item/tick/login integration.
- [ ] Vial encounter, final claim/journal, native/actual Host acceptance and deployment.


## Monkey appearance lease (2026-09-26)

The shared `QuestAppearance` manager now supports a dedicated monkey lease,
reserved through `ApplyMonkey` with the exact owned trial scene and a UTC expiry
no more than sixty seconds away. Its scope is that private map or Doctor Li's
six-tile vicinity in Twin City. The generic five-minute disguise API rejects
monkey requests. Repeated reservation returns the original token without
extending expiry; different scene IDs cannot reuse the lease. The canonical
session, normal transformation state and level 50 eligibility are checked.

Projection changes only the existing spawn/update mesh. Body, Face, class,
transformation identity and HP remain unchanged. Wrong-kind/stale-token cleanup
cannot remove the lease. It survives the intended map transition and clears on
expiry or departure from its scope. No scene-registry locks are taken while
checking a lease's current map during projection.

Installed asset metadata maps simple object 156 to model 156000000
(`c3/monster/156/100.c3`) and texture 356000000. File hashes/mappings are recorded
in `research/FRANTIC_MONKEYS_APPEARANCE.json`. This supports the monkey candidate,
but does not prove native player rendering, animation or color.

Build and **430 checks pass** (406 shared, twelve existing Spun Gold appearance
checks and twelve monkey appearance checks). Tests cover required scene API,
maximum duration, rejected context, unchanged identity/HP, stale cleanup,
ordinary appearance recomputation, map transition, repeat/no-extension, second
59/60 expiry, out-of-scope departure and explicit failed-admission cleanup.
Logs: `obj/frantic-appearance-build.log` and `obj/frantic-appearance-tests.log`.

The caller still needs to bind this lease's expiry to the durable trial unlock
and clear provisional appearance on failed poison consumption or travel. No NPC
or item route calls this lease yet. Cosmetic native acceptance remains pending.

- [x] Scoped monkey projection, bounded expiry and shared appearance cleanup.
- [ ] Coordinator reservation/rollback tied to saved trial time and ordinary routes.

The existing Mystic Ring reward/appearance suite also passes **484 overlapping
checks** after this shared change, including mouse leases, rollback and
Spun/Mystic disguise interaction. Log:
`obj/frantic-appearance-mystic-regression.log`. Do not add overlapping totals.


## Coordinated poison admission (2026-09-26)

`FranticMonkeysAdmission.Start` connects owned poison use, scene reservation,
monkey appearance, durable trial reservation and actual travel. It requires the
real Doctor Li NPC (4486, model 688) at 1002 440,343, within six tiles, and the
exact inventory object. It owns the session-install lease and inventory lock.
The full quest dispatcher is still pending, so this coordinator is invoked by
fixtures rather than ordinary item routing in production.

The disguised store reservation now uses one captured UTC timestamp for both
appearance expiry and saved trial unlock. Appearance is provisional until the
poison/trial transaction commits. Failure before commit clears the disguise
and preserves poison; failure after consumption leaves a recoverable saved
attempt, clears provisional runtime state where safe, and fences the session
for reload. Login recovery issues one replacement poison after terrain release.
Successful admission retains its appearance token until expiry or scene cleanup.
The scene tick clears expired appearance before allowing trial population.

The combined fixture exposed a return-location bug: spawning the real Doctor Li
blocks his own tile for player landing. Return and offline recovery now use
**1002 440,345**, two tiles south, still within conversation distance.
The terrain audit includes this landing and passes against both hash-bound grids.

Build and **479 checks pass** (406 shared + 41 investigation + 32 coordinated
admission checks) across rebirth 0/1/2. Evidence includes remote-use rejection,
pre-commit attempt insertion failure, post-commit travel insertion failure,
one-time reload recovery, actual disguise/consumption/entry, identity/HP
preservation, replay rejection, expiry/retry, matching saved/disguise deadline,
59/60-second scene ticks, five normal combat kills, safe return beside the real
NPC, and bound vial claim. Logs: `obj/frantic-admission-build.log` and
`obj/frantic-admission-tests.log`. The actual NPC exists in this fixture; dialogue
and public item dispatch are not yet routed to the coordinator.

- [x] Disguise, saved timer, exact poison consumption and actual admission joined.
- [x] Reservation rollback, post-commit reload recovery and occupied-NPC return fix.
- [ ] Full NPC/item/tick/login initialization, vial encounter and final claim/journal.
- [ ] Native rendering, actual Host restart, deployment and signed client publication.


## Vial encounter ledger (2026-09-26)

`FranticMonkeysStore.Apes` now reserves an owner/life-specific attempt only with
the exact tracked vial at map 1020 within three tiles of 795,471. It preserves
a one-minute reuse timestamp across abandonment/reload, a five-minute encounter
deadline, and a unique open attempt. The vial remains owned until sword delivery.

**Revora population rule:** defeat two Venomous Apes, then one fresh Ape Leader.
The guide does not establish the original population; this explicit sequence
uses the already documented level-50 profiles. Durable body receipts enforce
order and replay protection. Runtime callbacks must additionally prove each
actor belongs to the registered owner/attempt; the runtime encounter coordinator
is not implemented in this batch.

The leader's credited death records victory before item delivery. Sword delivery
atomically consumes the exact vial, inserts one bound Devilish Sword, closes the
attempt and saves stage 8. This one-for-one exchange works with a full bag. A
stored vial cannot be consumed or copied, but victory remains saved until the
owner retrieves it. An absent vial can be replaced after abandonment or victory,
never during an active attempt. A genuinely lost earned sword can be replaced
once per missing UID without replaying combat. Existing item rows in any storage
or ownership still prevent replacement.

Build and **528 checks pass** (479 existing investigation/admission checks + 49
ape-ledger checks, including the same 406 shared prerequisites). New coverage
across rebirth 0/1/2 includes wrong location, one active reservation, early leader,
lost vial during active combat, abandonment/recovery, second 59/60 cooldown after
reload, stale attempts, duplicate/extra kills, fresh leader requirement, saved
victory, stored-vial protection, sword insertion rollback, full bag, reward replay,
closed-attempt reload and lost-sword recovery. Logs:
`obj/frantic-apes-build.log`, `obj/frantic-apes-tests.log`.

These ledger fixtures register bodies and invoke the death core explicitly;
they do not establish automatic spawning or public attack isolation for apes.
The existing trial combat guard still rejects unregistered ape-family actors.
The next runtime coordinator must own spawning, kill context, cleanup/retries,
and promptly deliver a won sword or retain delivery eligibility if unavailable.
No production route calls the new ledger and nothing was deployed.

- [x] Saved vial cooldown, ordered ape/leader receipts and durable victory.
- [x] Atomic vial/sword exchange and exact-item recovery across reload.
- [ ] Owned ape runtime population/attack guards and ordinary vial use.
- [ ] Bao hand-in/final claim, full NPC/journal routes and acceptance gates.


## Owned outdoor ape runtime (2026-09-26)

`FranticMonkeysApes` connects vial reservation to the ordinary attack, damage and
death handlers. It spawns **one actor at a time**: Venomous Ape, Venomous Ape,
then Ape Leader. Only the owner or their registered companion may fight the
actor; unregistered/stale actors cannot enter combat. Registered owner identity,
map, deadline, actor membership and nearby kill location are checked before
credit. Companion native combat acceptance is still pending.

Spawns use the audited five-cell area at 795,471, reject occupied cells, preserve
the bound vial until a won exchange, and never inherit ordinary monster loot.
There are at most 32 owner reservations and one actor per reservation. Cell
selection/publication is serialized between these encounters. If no landing is
free, population retries on a bounded three-second pulse until the five-minute
deadline. Successful corpses are removed after one second before the next actor.
The trial guard now owns only family 9917450; the outdoor guard owns 9917451/2.

The leader's normal death records victory and attempts the atomic vial/sword
exchange immediately. Cleanup retains any undelivered victory for recovery.
Departure, expiry or a replaced/disconnected owner removes runtime actors;
canonical owners can abandon interrupted saved attempts after cleanup, keeping
the original reuse cooldown. The future login/NPC coordinator must invoke
recovery for attempts left by disconnected sessions or a process restart.

Build and **522 checks pass** (479 prior investigation/admission checks + 43
outdoor runtime checks; totals overlap earlier suites). Across rebirth 0/1/2,
checks cover wrong map, duplicate use, actual first spawn, departure and cooldown,
replacement-session recovery, exact five-minute expiry, stale actors, foreign
normal damage/direct death, the ape/ape/leader sequence, normal kill handlers,
full-bag automatic bound sword delivery, cleanup and saved progress after reload.
Logs: `obj/frantic-ape-runtime-build.log`, `obj/frantic-ape-runtime-tests.log`.

These fixtures call the vial coordinator and pulse directly. Global ordinary
item dispatch and scheduled/login integration are still pending, as are native
AI/rendering and actual Host restart. Nothing was deployed.

- [x] Owned outdoor spawning, attack/death guards and ordered normal combat.
- [x] Automatic sword exchange, bounded retry/cleanup and cooldown-preserving recovery.
- [ ] Bao hand-in, Chang reward, full NPC/item/journal/scheduled/login routes.
- [ ] Native/Host acceptance, deployment and signed client publication.

### 2026-09-26 Frantic Monkeys ordinary route checkpoint

- [x] Ordinary Chang/Bao/Doctor dialogue, poison/vial use, heart death credit,
  scheduled encounters, login recovery and saved journal missions 1745?1750.
- [x] Atomic Bao sword hand-in (3,000 silver and half an EXP ball) and Chang
  final claim (bound class weapon pack 728243 and one frozen bound P7 soul).
  Earlier steps bring the full silver reward to 3,400. Once per rebirth 0/1/2.
- [x] Build and 502 checks pass: 406 shared prerequisites plus 96 route checks,
  including ordinary handlers, saved progress, replay, full bags, failed reward
  transaction rollback, recovery and opening the earned class weapon pack.
- [x] Private journal overlay staged; idempotence and unrelated-section byte
  preservation verified. Source client and executable hashes remain unchanged.
- [ ] Native NPC/monkey rendering, AI/companion/journal acceptance, actual Host
  restart, deployment and signed client publication. Achievement 10931 stays disabled.

Evidence: `obj/frantic-quest-build.log`, `obj/frantic-quest-tests.log` and
`obj/chi-refill-validation/frantic-monkeys-journal/manifest.json`.
Overlay SHA-256: `c69b98b3f114b3cd6d011edd2eb9e771cf3980761c7c59517a7d652f08b8c208`.
Nothing deployed. Next independent roadmap root: Soul of the General (1751).


## Interrupted admission and earned encounters through full restart (2026-10-01)

Four private phases pass: `career-frantic-interrupt`, `career-frantic-win`,
`career-frantic-claim`, `career-frantic-reload`. Ordinary actor4294908401 accepts
at Chang, travels through installed Phoenix/Ape City portals and visits Bao.
Three loaded Macaque deaths produce the owned Decayed Heart with unchanged rolls.
Bao and Doctor Li advance the errand; ordinary poison item-use beside the Doctor
creates and enters the registered private trial. The actor saves inside and exits
abruptly at stage5, before any trial kills or reward.

Cold game/database startup returns the saved character to1002(440,345), with
prior belongings/rewards unchanged. The normal quest OnLogin recovery issues
one replacement poison and returns the quest to stage4. Repeating OnLogin makes
no further change. Ordinary poison use admits a new owned trial; an early Tick
spawns no fighters. Advancing the controlled clock past the60-second delay and
pulsing the actual coordinator produces five registered trial monkeys. Their
normal ReceiveAttack death path completes the trial and returns the actor.
Doctor Li grants the earned vial.

The actor travels through installed portals and uses that vial at795,471. The
normal outdoor coordinator produces the ape/ape/leader sequence; all three owned
deaths earn the bound Devilish Sword and consume the vial. The actor saves and
exits abruptly at stage8. Another cold startup preserves the exact sword and
quest/visit/trial/ape records. Bao consumes that sword; Chang grants exactly two
new bound items, class weapon pack728243 and the frozen P7 soul. Total quest
silver is3,400. Normal EXP effects are retained in the profile snapshot.
Completion and recovery replay change nothing. A final cold reload preserves
stage10, both reward UIDs and the entire snapshot; repeats still grant nothing.
Checkpoint: `revora_frantic_process_probe`, step3 and FranticProcessSnapshot.

No stages, materials, reservations, encounters or rewards are fixture-seeded.
NPC approaches, outdoor vial location, encounter pulses, damage and clock are
controlled; the initial heart roll is unchanged. This proves server earning and
recovery, not natural fight difficulty, native disguise/AI/rendering, companion
combat or signed journal acceptance. The class pack remains owned/unopened.
No production code/build changed; candidate SHA256:
`05F7A80A0EC5F74ED699076019CE7FFD9F2B5B2646F7C69562FEC1A4E43BD9A6`.
The main retained career is unchanged. Native acceptance, live deployment and
signed client publication remain pending; no live server or client changed.

## Retained Frantic Monkeys journey (2026-10-02)

The retained Water Taoist follows Chang option91 and Ape City's NPC4485, defeats
a Macaque through calculated ranged combat and delivers its quest material.
NPC4486's potion enters the owned trial with monkey appearance156. The fixture
advances the trial clock63 seconds, defeats five PoisonTrialMonkeys using normal
calculated attacks, and ticks the event through ordinary return. The next vial
starts the Ape City encounter at795,471; three calculated ape defeats complete it
and deliver sword721702. Normal portals connect the public maps. A full game and
database restart preserves the completed encounter records and carried sword
before hand-in and final claim.

The final NPC route consumes that exact sword and grants bound728243 and bound
P7 soul820073. The claim committed correctly, but the fixture asserted3,300 total
silver instead of3,400: it omitted the100-silver trial-vial payment. Full nested
snapshot comparison found only stage8-to10, the consumed sword60888444, new reward
UIDs60889738/60889739 and the final3,000-silver hand-in credit. No existing item was
modified. A one-time exact-SHA guard adopted that already-completed checkpoint,
without replaying payment or injecting rewards/evidence. Diagnostic and recovery
branches were removed; the cleaned full-reload phase passes again. The corrected
claim assertion was not exercised as a fresh second earning run.

`career-retained-frantic-hunt` and cleaned `career-retained-frantic-reload` pass
on candidate SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The first claim phase is recorded as a fixture assertion failure after successful
production commit, not as an all-green run. Final probe
`revora_retained_frantic_probe` is step2. Both exact reward identities survive
restart and repeat claim changes nothing. A fresh read-only aggregate check now
finds6/19 final receipts for this UID; thirteen remain.

Proximity, attack cooldowns, trial timing and the initial material roll are
controlled. No lethal damage, material, quest stage or completion receipt is
assigned by the fixture. Earlier qualification/funding provenance and pending
native acceptance remain unchanged. No production code/build or live deployment
changed in this batch.
