# Elimination daily herb persistence

The quest route is staged in source behind the global
`EliminationLifecycle.ReleaseReady=false` gate. Startup, tick and item-use hooks
are connected; ordinary-player fixtures earn the rescue, den and achievement
11010. Live activation and native-client/real-Host acceptance remain unfinished.
The sections below retain the sequence and evidence of the implementation.

`EliminationStore` admits ordinary reborn characters at level 70 or higher beside
Georgia in Maple Forest. The saved level, rebirth count and map must match the
canonical connected character; trading, vending and poker exclude writes.
All writes require the inventory lock, transaction and locked character row.
The actor ID, mesh, coordinates, active NPC and interaction distance are checked.

One run per character per Pacific calendar day is a Revora adaptation using the
existing daily calendar. The guide establishes a daily limit, not a timezone.
Admission is idempotent. A final day check rejects a transaction that crosses
the reset boundary before the store operation finishes.

Each of twelve distinct plants inserts a bound, single Beauty Herb plus a receipt
of its exact item UID, and advances the run mask in the same transaction. Georgia
accepts only the twelve items recorded for that run, with matching saved owner,
type and restrictions. All must be in the bag, unlocked, permanent and unmodified.
Their consumption, one bound Fancy Spice and stage transition commit together.
Inventory packets/projection occur only after commit, while retaining the lock.
Concurrent retries and reloads cannot generate another herb or spice.

Build and **195 store/interaction/lifecycle/combat checks** pass through
`tests/PotencyHunt/Verify-PotencyHunt.ps1 -EliminationStoreOnly`.
Coverage includes ordinary admission boundaries, range/trade refusal, concurrent
admission and gathering, reconnect replay, twelve distinct plants, runtime and
saved lock restrictions, saved foreign ownership, full inventory, late receipt
and stage-update rollback, concurrent exchange, spice reload and next-day
admission, lost-item recovery, storage/foreign-owner refusal, normal bound-item
discard, replacement receipt rollback and reset-boundary rollback. The disposable
test schema is removed by the fixture harness. No
production schema changes occur in this validation.

The fixture supplies character progression and places the character beside each
test actor; it proves store behavior, not earning the required level/rebirth,
packet-driven travel or native clicks. Terrain evidence is recorded separately.

## Lost materials

Georgia can replace one explicitly selected missing item from the current day's
owned receipt. Only herbs during collection or spice before summoning qualify.
Any saved instance of that UID, including storage or another owner, refuses
replacement. A stale in-memory copy requires reload. The normal bound-item drop
handler destroys these items rather than creating a floor item; the fixture
exercises that actual path for herbs and spice.

The replacement item, updated current receipt and immutable old/new UID history
commit together. The gathered-plant mask does not change. A request naming the
previous UID cannot create another copy, including after reconnect or concurrent
requests. Repeated genuine loss can replace the new current UID. Consumed herbs
cannot return after the spice exchange, and earlier-day materials cannot be
recovered through today's run. Recovery is a documented Revora policy; the
archived guide does not specify replacement behavior.

## Earlier-day materials

The staged cleanup operation runs beside Georgia under the same player and
inventory ownership checks. It selects exact herb/spice UIDs from that character's
earlier-day receipts, joins their saved item ownership and locks matching bag
items. It never deletes by item type alone. Current-day receipts, unrelated
copies, other owners, stored items and locked/restricted items remain intact.
Stored items become eligible after they return to the owner's bag; unlocking a
restricted item likewise allows a later cleanup attempt.

Deletion and a unique cleanup receipt commit together. Post-commit `Apply`
projects removals under the inventory lock. Concurrent repeats become no-ops;
late receipt failures roll back deletion. The fixture verifies both expired herb
and spice cleanup, storage/lock deferral, exact owner selection, preservation of
today's herb and an unrelated same-type item, and reload. Stored/locked setup
uses fixture mutations; it is not a packet-level warehouse journey.

Cleanup does not erase daily runs or earning/replacement receipts, and never
reopens expired recovery. This is Revora's material housekeeping policy, not a
claim about the archived game's unpublished reset behavior.

## Staged NPC interaction

`EliminationQuest` is connected to the ordinary NPC dispatcher. Its initializer
is intentionally absent from normal startup while the complete route remains
unfinished. The fixture explicitly initializes it; an uninitialized request
cannot create a run. Initialization validates all thirteen actor approaches and
rejects conflicting IDs before adding missing actors. Repeating initialization
preserves the existing intended actors. No NPC database rows or client assets
are modified.

Georgia offers admission, exact herb exchange, the first genuinely missing item
from the owned run, and previous-day cleanup. Each herb offers gathering only
while its plant bit is unset in the current collection run. Short dialogue keeps
the rescue story in the foreground. Offers expire after 60 seconds and bind the
NPC, character level/rebirth, calendar day, allowed choices and missing item UID.
Every mutation rechecks the store's independent authorization. Full bags return
a brief message before gathering/recovery rather than attempting a write.

The added fixture path uses ordinary accounts, `Npcs.GetDialog` for opening a
menu and sealed 2032 packets for choices. It covers disabled startup, conflicting
actor profiles, idempotent initialization, forged/expired/changed-eligibility
offers, range/NPC mismatches, all twelve plant choices, recovery, exchange,
reload and cleanup across a day boundary. Shared packet dispatch consumes option
255 before feature handlers; the farewell check asserts no quest side effects,
not a server-side active-NPC reset. No packet-layout change was introduced.

The fixture places the character beside each plant directly. Actual walking,
native menu rendering and the rest of the rescue are not proven by these checks.

## Saved rescue admission

The staged `Summon` transaction accepts only the exact Fancy Spice UID recorded
for the current owned daily run, beside (311,256) in public Maple Forest. It
rechecks eligibility, bag state, binding, saved owner and item restrictions.
Consumption, one rescue receipt and stage 1→2 commit together. The receipt keeps
the original spice identity after consumption; the recovery service cannot
recreate it once the run has advanced.

`ResumeRescue` returns the same owned pending run and deadline without another
item debit. It is restricted to the clearing and current daily run. Admission
expires at the next Pacific daily reset; this is a Revora adaptation, not an
officially published encounter timer. Reconnect and repeated resume do not reset
that deadline. The intended runtime coordinator must reserve its map/actors
before committing initial admission, then use the saved receipt for recovery if
publishing or transport fails after commit.

Fifteen additional checks cover location, unrelated spice, locked/stale saved
flags, late receipt/stage failures, reset-boundary rollback, concurrent admission,
fixed deadline, reload/resume, foreign-owner refusal and consumed-item recovery
refusal. This proves the transaction contract only. There is still no registered
spice item-use handler, live actor binding, monster death credit or native rescue
journey. A saved admission does not itself constitute a completed encounter.

## Private actor binding foundation

`EliminationRescueBindings` now binds a paid pending run to an exact registered
private Maple Forest map, its owner metadata and the canonical monster object.
The map must contain exactly one live full-health monster, no NPCs, companions
or floor items. The binding stores a fresh generation, map ID and actor UID.
Repeated binding of the same known runtime object returns its existing generation;
a second registered scene cannot replace it. After scene disposal, rebinding
gets a new generation without another spice payment. Runtime object identity is
tracked weakly, so numeric map/actor reuse alone cannot authenticate the old
incarnation. The future kill handler must validate that generation independently.

The staged profile uses native display name RockBehemoth, level 42 and 3,656 HP.
Mesh 105 is the installed ordinary RockMonster model; applying it to Behemoth is
an explicit Revora appearance adaptation, not recovered retail identity. Attack
300–450, defense 100, two-cell melee range and movement/attack speeds are also
Revora choices. The profile has no configured item drops, boss flag or spells.
Native rendering and gameplay tuning remain unverified.

Nine added fixture checks instantiate real private maps and registered monster
objects, then test foreign map ownership, counterfeit same-UID objects, rejected
binding writes, stable repeated binding, refusal of a competing live scene,
fresh generation after disposal and retained admission. The owner stays in the
public forest; there is no tested travel, combat or rescued-NPC path yet.
The registered monster's occupied tile is checked for walkable terrain without
requiring it to be free of itself. Private-map creation remains fixture-driven;
normal startup integration remains disabled. The staged lifecycle below now
uses these bindings.

## Staged private-map lifecycle

`EliminationRescueWorld` verifies the installed forest terrain hash, reserves a
private forest copy, constructs its Behemoth and binds the saved generation.
For a first entry, spice consumption and actor binding share a transaction;
the item projection and ordinary teleport occur after commit. The landing is
(309,256), beside the monster at (311,256). At most sixteen scenes and one scene
per owner are admitted. A resumed run retains its existing daily deadline.

The travel guard rejects another session's entry through the shared teleport
paths and restricts ordinary in-scene teleport displacement. Explicit leave
returns the owner to public Maple Forest and saves that return before disposing
the map. Tick cleanup retires scenes whose owner has left, disconnected, died or
passed the deadline. It preserves an already saved external destination rather
than overwriting it with the forest return. Session replacement takes its own
inventory lock without waiting while holding the retiring scene's lock.

Ten additional tests cover persisted entry, duplicate-scene refusal, foreign
teleport, foreign combat/death refusal, durable return, reconnect/resume with a
fresh generation, external departure and first-entry binding failure followed
by a successful retry with the exact earned spice. The existing Ancient Devil
world fixture also passes after the shared teleport/combat guard additions.
Disconnect/death cleanup, restart recovery and uncertain post-commit publication
still need dedicated lifecycle tests. These are not real Host restart results.

The initializer and tick are not registered in normal startup, and
Fancy Spice is not yet connected to item-use. This remains an incomplete staged
quest, not an available player service.

## Behemoth combat and defeat receipt

The staged scene now permits combat with its canonical owner or registered
owned companion. Direct damage takes a nonblocking owner inventory lease and
revalidates the active map, actor object, eligibility, deadline and trade state.
Foreign, replaced and retired actors cannot award victory. Behemoth's target is
the owner, and its attack authorization preserves ordinary player attackability
protection. Companion identity checks exist in source but do not yet have a
dedicated companion-earned victory fixture.

Before death presentation, the transaction locks the saved character and checks
the exact pending run, current day, map, actor and saved generation. One kill
receipt and rescue state `rescued` commit together. That state currently means
Behemoth defeated; it does not mean the player has spoken to Landy or completed
the quest. Normal monster loot is suppressed, repeated corpse callbacks are
inert, and achievement 11010 remains unearned. A failed receipt rolls back and
requires reconnect; the paid pending rescue can then create a fresh scene.

The current suite covers partial and lethal damage through `ReceiveAttack`,
foreign/counterfeit/retired callbacks, busy owner locks, trade exclusion, saved
generation mismatch, late defeat-write failure and retry, duplicate deaths,
floor-loot suppression and no premature achievement. It controls damage inputs;
it does not prove time-to-kill, full attack calculation balance or native combat.
The Ancient Devil world regression passes 42 checks after the shared hooks.

Family 9916701 was absent in a targeted read-only `monsterinfos` query.
Initialization also rejects any future loaded-catalog collision, with a fixture
check. Post-victory re-entry now uses the rescued-NPC path below rather than
recreating a defeated boss.

## Landy and saved-victory recovery

After the defeat commit, the world removes Behemoth and publishes Landy at
(311,256), including a spawn to the owner's screen. NPC 300033 uses the installed
Cherry profile 469 (mesh 4690), with the server name Landy. This is an explicit
Revora appearance adaptation; the retail Landy appearance was not recovered and
native rendering remains untested.

The ordinary NPC dispatcher serves a brief request to tell Georgia about the
poison. A sixty-second offer is tied to the owned live scene and its saved
generation. Confirmation verifies range, character identity/eligibility, the
canonical NPC, the saved defeat receipt and the unexpired daily run. The Landy
receipt and stage 2→3 commit together, then the owner returns to public Maple
Forest. Stage 3 means Landy was spoken to and Georgia's cure stage is next;
it is not final quest completion.

Leaving or reconnecting after victory retains the earned defeat. Resume creates
a Landy-only scene, verifies the kill receipt, and assigns a fresh binding.
It cannot respawn Behemoth or debit another spice. A failed Landy receipt fences
the stale session and leaves stage 2 recoverable; after successful dialogue,
stage 3 prevents another rescue entry. Seven added checks cover Landy publication,
unoffered/expired choices, saved-victory reload, late-write rollback/retry,
once-only acknowledgement/return and no premature achievement. These exercise
normal NPC option packets, but not a real client render or Host restart.

At the Landy checkpoint, remaining work included the den encounter, startup
recovery and final rewards; later sections below record their implementation.
Do not enable this partial service before the complete route exists.


## Georgia report and Fire Pill cure

After Landy's acknowledgement, Georgia advances the owned daily run from stage 3
to stage 4. The saved Landy receipt and ordinary NPC range/choice checks are
required. Ten exact bound Fire Pills (711041) earned for that run are consumed
atomically with a cure receipt and stage 5. This does not award achievement 11010
or the final quest rewards.

The staged death hook accepts canonical FireSpiritL43 bodies (family 68, mesh
202, level 43) in public Maple Forest 1011, killed by the nearby canonical player.
Read-only catalog inspection found two spawn rows for family 68 in map 1011
(and one in 1523, which does not qualify). This is catalog evidence, not live
spawn or native-client acceptance.

The recovered guide names the monster and ten pills but publishes no odds.
Revora explicitly delivers one bound pill to the bag per eligible kill, capped at
ten earned pills per run. A corpse callback is attempted once per death stamp;
full bags and failed persistence cannot reroll that corpse. The normal quest
initializer remains unregistered, so this hook is disabled in ordinary startup.

Twenty-one additional checks cover report replay, wrong/counterfeit monsters,
pre-admission corpses, failed writes and session fencing, ten actual Entity.Die
callbacks with concurrent duplicate callbacks, the ten-pill cap, reload, locked
materials, late cure-write rollback and once-only ordinary Georgia turn-in.
Death is invoked with controlled fixture state; this is not full combat-formula,
travel or real-client proof. Discarded-pill recovery and expired-pill cleanup are covered by the following
source milestone.

The shared daily-hunt loot regression also passes 477 checks after this hook
addition (`-HuntLootOnly`). No live Host or active client was changed.


## Fire Pill loss and expiry recovery

Georgia's existing lost-item choice now includes stage-4 pill receipts. A pill
must be absent from all saved item locations and the runtime bag before it can
be replaced. The exact earned slot retains its kill evidence and atomically
changes to the replacement UID, with a durable replacement receipt. The ten-kill
cap is unchanged. Stored items, obsolete requests and pills consumed by the cure
cannot be recovered. Georgia briefly reminds stage-4 players where to find the
pills; stage 5 no longer asks for a cure already delivered.

Expired-supplies cleanup now includes exact pill UIDs from earlier owned runs,
including replacements, under the existing bag/lock/ownership restrictions.
Initialization expands the replacement and cleanup item constraints inside the
schema transaction. This migration has only run in the disposable fixture.

Eight added checks exercise stored-item refusal, normal bound-pill discard,
late replacement-write rollback, ordinary NPC replacement with concurrent replay,
reload/current-day preservation, expired recovery refusal, selection of all ten
expired pills including the replacement, and cleanup rollback. The existing cure
reload check additionally refuses recovery of the consumed replacement. The
expiry test rolls back intentionally so the same earned set can exercise cure;
it does not claim a committed expired-pill client update or real-client proof.
Build and 152 Elimination checks pass. Normal activation remains disabled while
the den, final rewards and lifecycle completion are unfinished.


## Den admission prerequisite

`AdmitDen` stages an owned reservation after the exact ten-pill cure. It requires
the current eligible public-forest player, saved cure receipt, stage 5 and actual
Georgia interaction. A foreign-keyed row records admission and the next Pacific
midnight; concurrent calls and reconnects reuse the same row and deadline. It
neither charges another item nor advances past the den combat or awards rewards.
The future coordinator must reserve a verified scene before calling it. No NPC
choice or world handler currently calls this store method.

Build and 158 Elimination checks pass. Six added checks cover pre-cure refusal,
late-insert rollback, Georgia interaction, concurrent once-only admission,
reload with the same deadline and reset expiry. The full Elimination fixture
remains ordinary-player based, but this staged method has no live travel proof.


## Den encounter and final reward route

The staged `EliminationDenWorld` now reserves an owned private copy of base 1021
and binds one current actor to a fresh generation. Georgia's offered option 7
enters it after the cure. Six RockLackeys appear sequentially at the reviewed
sites; each committed distinct-slot death reveals the next. The seventh actor is
RockLeader. Re-entry reads the saved kills and recreates only the next undefeated
actor, with a new generation. There is no public monster spawn or normal startup
registration. The mine layout, six sequential fights, rock body 105 and combat
values are explicit Revora adaptations; leader level 42 and HP 3,656 match the
installed display catalog. Lackeys have 1,219 HP, attacks 300?450, defence 100.

The shared attack/death/teleport paths enforce canonical private-map ownership,
actual actor identity and the owner or their canonical companion. Damage holds a
nonblocking inventory lease. The store also checks the saved character profile,
current daily run, generation, slot and deadline. Each kill commits before scene
advancement. Duplicates, outsiders and retired bodies cannot earn another slot;
failed persistence fences the session. Normal loot is suppressed for this family.

Georgia at the private exit (300034, 30,40) returns the player through an offered,
time-limited dialogue choice. Saved victories remain when leaving. Leader defeat
atomically advances stage 6 and returns the player to Maple Forest. Final Georgia
option 8 commits one current-level EXP-ball unit, 50 Study Points, progression
and mentor/reincarnation effects, achievement 11010 and a unique claim receipt
in one transaction. Projection and Study/achievement updates follow commit.
Concurrent claims and reloads cannot duplicate payment. A full Study balance or
curse leaves the reward unclaimed; the current daily run still expires at reset.

Build and **185 Elimination checks** pass, including the earned path from herbs
through rescue, Landy, cure, six lackeys, leader and ordinary final claim. The 27
new checks cover foreign entry/damage, normal partial/lethal damage, late kill
failure and retry, saved progress after three lackeys, exit dialogue, final return,
late reward rollback, concurrent claims, exact EXP-unit receipt and saved EXP,
Study and achievement reload. The shared Ancient World regression passes 42 checks.
Fixture movement into combat range is controlled; this is not full pathfinding,
skill-formula balance, native rendering, or real Host restart acceptance.

At the den/reward checkpoint, startup/tick/item integration, saved-location
recovery and lifecycle edge cases were still open. The next section records
the implemented integration and simulated lifecycle validation; real-client
and real-Host acceptance remain open.
Do not deploy or enable the partial integration on the strength of these tests.


## Runtime integration and interrupted-location recovery

`Thread` now calls the lifecycle initializer and quest tick; normal item use
routes Fancy Spice to the quest. Initialization remains a no-op while the global
release gate is false. This is a release gate for every player, not a GM or owner
allowlist. No live configuration, active client or Host was changed.

Once the gate can be enabled, initialization prepares both worlds, recovers saved
private locations before admission, then publishes the public quest actors.
Spice use requires the exact canonical bag item at the forest clearing; it commits
the paid rescue before entering. Georgia's brief stage-2 return choice resumes
that same rescue, including saved Landy victory, without another spice charge.

Rescue and den bindings now have a durable active marker. Leaving retires the
matching generation in the same transaction as the saved return. New bindings
reactivate it. Combat requires an active binding. Startup recovery matches the
saved character's exact map and owner to an active quest binding, returns them
to public Maple Forest, then retires bindings while preserving all material,
kill, cure and reward evidence. It refuses registered scenes or admitted affected
players; a failed return rolls back both location and binding retirement.

Build and **193 Elimination checks** pass. Eight added checks cover death cleanup,
active-scene/admitted-player recovery refusal, failed startup return rollback,
idempotent simulated process-memory-loss recovery with three earned kills,
disconnection, replacement sessions and daily expiry without leader victory.
Existing route checks now use the normal item-use handler and Georgia's offered
rescue-return option. The fixture disables unrelated random equipment drops only
while testing Fire Spirit deaths, restoring the setting afterward.

The generic character loader already redirects a replacement session from a
private map to a public map; the retirement check preserves a valid public
location rather than forcing an unrelated public destination. Startup recovery
is simulated by removing runtime scenes and the admitted session, then reloading
from persistence. It is not a real process-restart or native-client acceptance
claim. Those external acceptance gates remain open before flipping ReleaseReady.

Use [Elimination acceptance](ELIMINATION_ACCEPTANCE.md) for the remaining
consolidated external checks. Historical checkpoint lists above are not the
current release status.


## Current daily reward: bound rune at full completion

The 2026-09-21 follow-up found that this once-per-day quest had been missed by
the no-daily-P7 correction. New admissions now freeze `rune_reward=true` and
award one bound level-1 Tortoise Breaker rune (4030101) at full completion,
alongside existing EXP and Study Points. Migration defaults existing contracts
to false. The obsolete `valuable_reward` flag is ignored, including on pending
contracts; it cannot restore daily P7. Completed historical receipts and issued
items remain untouched. The exact rune UID is saved in `rune_uid` with all
progression effects in the same transaction, then projected after commit.

Latest validation: isolated build and 197 Elimination checks passed. The earned
full route grants a bound rune with its exact receipt, reload and replay
protection; the final-claim failure rolls back the item and progression. A
rolled-back transaction evaluates an old pending contract with its obsolete P7
flag set and proves neither a P7 nor retroactive rune is inserted. The global
release gate remains false. Log: `obj/chi-refill-validation/elimination-rune-tests.log`.

New Elimination contracts need one free bag slot and defer the entire claim when full.
Flame Lit exchanges its consumed Dragon Rune for the soul, so no extra slot is
needed; partial claims do not grant a full-completion soul. There is no extra
CP/RVP bonus on these quests: the specified 5,000 CPs/1,000 RVPs belong to the
separately approved Magnolia pack contract.

Historical P7-batch validation: isolated build, 195 Elimination checks and 93 Flame checks passed.
Coverage includes normal full-route claims, soul binding/catalog class, receipt
ownership, full-bag deferral, late-write rollback, concurrent/replayed claims,
saved item reload, old-contract preservation and partial-claim exclusion.
No live Host, database or active client was changed. The wider all-quest reward
upgrade remains incomplete; see [owner standard](QUEST_REWARD_STANDARD.md).


## Actual-process full-route follow-up (2026-09-27)

The ordinary NPC/material route now runs in the actual private game through
Behemoth, Landy, ten loaded FireSpiritL43 kills, cure, six lackeys, RockLeader and
Georgia's final reward. Deliberate process interruptions after spice consumption
and after three lackey kills preserve progress across database/game restarts.
Final reward replay and another process reload retain one bound rune, 50 Study
Points, the EXP-ball reward and achievement11010 without another claim.
[Exact process scope and remaining gates](ELIMINATION_ACCEPTANCE.md#full-route-and-interrupted-den-in-actual-game-processes-2026-09-27).
These tests privately enable the gate only inside the helper process. The normal
release gate and native/deployment requirements remain unchanged.
