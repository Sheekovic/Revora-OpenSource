# Couples vs. Singles: recurring Revora edition

Owner decision, 2026-09-25: design a recurring Revora event rather than retain
Heart wings as locked pending original rules. This document specifies the local
design. The server earning path and actual-process recovery are validated below;
native acceptance and deployment remain pending. This is not a retail-parity claim.

## Evidence and scope

The installed wardrobe maps Wings of Heart to **60186018**, worth zero wardrobe
points (`Sistemas/WardRobe/StorageManager.cs`). The historical research associates
the March 2018 campaign with faction support but does not establish its threshold
or duration ([research](research/WARDROBE_QUEST_EVENT_RESEARCH.md#poker-fashion-euro-and-couples-vs-singles)).
The existing Couple PK arena is a different competition and cannot attest this
campaign. All numeric rules below are explicit Revora design choices.

## Player experience

Each Pacific weekend, Saturday midnight through Monday midnight, a city herald
opens the campaign. Every eligible ordinary authenticated character can support
either Couples or Singles, regardless of their own marriage status. The names
describe the supported faction; they do not restrict access. Faction selection
is free, confirmed and fixed for that character for the weekend.

The herald offers a short introduction: “Love or liberty? Choose your banner,
gather supplies, and help your side win the weekend.” Details belong in optional
rules pages. A standings page shows both faction totals and personal progress.

Complete up to three supply sorties per weekend. Each sortie requires ten
campaign raiders defeated, one guarded supply cache secured, and delivery at the
chosen faction's camp. Raider and cache encounters must be owned by that run;
normal monsters and another player's cache cannot substitute. The hunt is PvE;
player kills give no progress. Actual map, spawn actors, terrain and combat
balance must be checked before connecting an NPC or death hook.

Each delivered sortie gives 100 support. Three deliveries (300 support) qualify
the character for Wings of Heart for 30 days from that weekend's close. Both
factions can earn wings. Higher total committed support wins; on an exact tie
both factions win. Qualifying members of the winning faction additionally receive
5,000 CPs and 1,000 RVPs once. There are no entry fees, paid score boosts or P7
rewards. The currency bonus uses the existing custom-pack reward scale; it is a
proposed campaign policy, not a recovered retail reward.

## Persistence and ownership contract

- Save the Pacific window as UTC start/end at entry; handle 47/49-hour DST
  weekends explicitly. Reject admissions and progress at or after the end.
- Bind faction, sortie ordinal and rule version durably to the character and
  weekend. Reconnect resumes that run; it cannot change faction or reset the cap.
- Persist accepted death/cache receipts with run identity and unique objective
  identity. Duplicate callbacks, parties, stale actors and foreign encounters
  must not duplicate or transfer progress. Establish a bounded retention policy.
- Commit delivery, personal support and faction support together. At close,
  freeze totals and eligible winners under the same campaign lock. Never award
  from mutable live counters or a client-supplied score.
- Recover offline prizes through durable receipts. Currency balances, delivery
  acknowledgement and a unique award identity share one transaction. Project
  inventory currency only after commit; fence/reload after installation failure.
- Wing expiry is close plus 30 days, even for late claims. Later awards may extend
  eligibility to their own fixed expiry; they cannot restart an old term or
  downgrade any independently earned permanent entitlement.
- Disconnects and restart must clean up owned world actors and restore unfinished
  objectives without spawning duplicate caches. No GM/owner allowlist is allowed.

## Implementation sequence and acceptance

1. Verify a suitable installed map, NPC meshes and combat templates; inspect
   existing owned-encounter and reward infrastructure for reusable behavior.
2. Implement versioned calendar/admission and durable objective/delivery rules.
3. Connect owned actors, brief NPC menus and standings for ordinary players.
4. Implement close snapshots, wing grants and atomic winning-faction currency.
5. Validate both factions, all three earned sorties, ties, concurrent delivery,
   failures, expiry, reconnect and separate-process recovery. Test starting with
   no supplied campaign progress, then complete native-client acceptance.

Native acceptance remains pending under the owner's standing decision. Source
work may progress independently; deployment is a separate gate. Heart remains
`unimplemented` in the wardrobe matrix until a production earning path exists.

## Admission and objective store checkpoint (2026-09-25)

`Game/Features/HeartCampaign.cs` now stores the weekend window, immutable faction,
up to three numbered sorties, individual objective receipts and final delivery.
It reuses the established Pacific weekend calculation. A shared weekend advisory
lock serializes mutations for the later close snapshot; enrollment then locks the
character row. Repeated enrollment resumes an unfinished sortie. A completed third
sortie cannot be replaced until the next weekend.

Ten raider slots precede the guard slot, which precedes cache collection. Each
objective requires a unique actor UUID and unique run/slot receipt; receipt plus
progress update share one transaction. Timestamps must follow the current sortie's
admission and precede close. Delivery requires all twelve objectives and commits
once. Support will be derived as 100 per committed delivery; this checkpoint does
not add a second mutable score counter.

The store accepts only canonical ordinary loaded players outside trade/booth.
`Record` is a disconnected server-only seam: the future encounter wrapper must
prove the supplied actor belongs to the run before invoking it. Neither arbitrary
monster deaths nor packets are connected. Startup initialization, actual NPC
proximity, encounter ownership, cleanup/recovery, standings and reward producers
remain unimplemented. No Heart wing earning source is enabled by this store.

Validation: isolated legacy x86 build passes; title suite passes **398 checks**,
including 19 new store checks. Coverage includes faction immutability, repeated
admission, foreign ownership, objective ordering, pre-admission timestamps,
concurrent/replayed objectives, suppressed-update rollback, three complete stored
sorties, concurrent delivery, exclusive close across spring DST and fresh next-week
admission. Objectives are synthetic UUID receipts here, not earned combat.
Logs: `obj/heart-build.log` and `obj/heart-tests.log`. Existing workspace changes
remain included in the test binary and unstaged outside this feature.

Map research: `PlanetEncounter` already clones installed map 700 into owned
private scenes, removes portals, places a cache at (48,48), enters at (48,41), and
copies monster template 1413. These are inspected reuse candidates, not verified
Heart placement or balance. Heart must validate its own ten-raider/guard layout,
safe return, reservations and cross-event isolation before activating actors.

## Encounter ownership checkpoint (2026-09-25)

`HeartCampaign.Encounters.cs` adds durable arena reservations and actor UUIDs.
Each owner and arena can have only one active reservation. Attempts last at most
15 minutes and never outlive the weekend. Actor identities belong to a reserved
attempt and objective slot; repeated preparation returns the same identity.
Completed slots cannot be prepared again. `Record` now rejects fabricated IDs,
wrong slots/runs/owners, expired attempts and abandoned actors before progress.

Ending an encounter shares the campaign lock with objective credit. It releases
the arena and marks a fully collected cache complete, otherwise abandoned.
Startup recovery can abandon interrupted attempts; the future world coordinator
must call it only before owned scenes exist. Resuming keeps the sortie's durable
objective mask and creates fresh identities for remaining actors. World entities,
actual disconnect callbacks, teleport restrictions and safe-return receipts are
not yet connected: this checkpoint is ownership storage, not playable combat.

Isolated build and **408 title checks** pass, including ten added ownership checks
for duplicate admission, fabricated IDs, stable actor preparation, release,
interrupted recovery, stale actors, fresh resumption, foreign release and timeout.
Earlier ordering/timestamp/foreign-owner checks now use registered actors so they
exercise their intended conditions rather than failing merely for absent IDs.

Terrain inspection of `bin/Debug/database/maps/700.map` found a 96x96 map with
SHA-256 `575ec41dc5ac6523278e029dd11fdb0bce5a121b3a4c04fab2e4b327860d9b31`.
Using the fixture loader's walkability rule (cell byte != 1), four-neighbor flood
fill from (48,41), with cache tile (48,48) blocked, reached every proposed raider
tile: (40,44), (44,40), (52,40), (56,44), (40,50), (56,50), (42,56), (46,58),
(50,58), (54,56), and the guard tile (48,53). The cache tile itself is walkable.
These are static placement candidates; actual actor movement, footprint, collision,
damage and native rendering still require the encounter implementation and tests.

## Safe-return checkpoint (2026-09-25)

`HeartCampaign.Return.cs` adds ordinary-character return from an ended encounter.
It resolves a walkable landing near Twin City (321,299), invokes server teleport,
verifies the resulting map/coordinates, then commits the durable safe position
and return acknowledgement together. Failure fences the session for reload and
leaves the receipt pending. Repeating an acknowledged return is harmless; an
active encounter cannot be acknowledged as returned.

Admission now refuses any outstanding return for that character, even if the old
attempt is abandoned or complete. A unique pending-return index backs this check.
The table initializer adds the return column idempotently. No existing encounter
history is silently marked returned to make the index succeed.

Isolated build and **415 title checks** pass, with seven added return checks:
active-attempt refusal, safe position persistence for each of three sorties,
pending-return admission refusal, interrupted/idempotent return, and suppressed
acknowledgement retention/fencing. The retry fixture explicitly rearms its player
after verifying the failure; it is not an actual network reconnect. Teleport is
tested from an ordinary fixture map, not a live Heart arena. Actual owned entity
creation, combat hooks, disconnect callbacks, login/startup return invocation and
scene disposal are still pending. Nothing is deployed or initialized in live
startup by this checkpoint.

## Owned world component (2026-09-25)

`HeartEncounter.cs` creates a private copy of map 700 for an enrolled character,
reserves its durable encounter and actor identities, and spawns only objectives
missing from the saved mask. Ten Banner Raiders precede a Banner Guardian. The
cache at (48,48) requires the guardian's saved defeat; collecting it ends the
encounter, calls safe return and disposes the map once no players remain inside.
The component's sweep abandons encounters whose owner leaves, dies, loses its
canonical session or exceeds the time limit. It preserves pending return work.

Entity death, damage ownership/leases, monster target selection and all four
teleport overloads now consult Heart ownership. Copied/foreign actors, foreign
players and foreign arena teleports are rejected. The store is credited before
the live monster is removed. Admission is capped at 16 component scenes.

Explicit provisional Revora tuning: raiders copy template 1413 with 35,050 HP
and 900 attack; the guardian has 210,300 HP and 1,800 attack. The shared catalog
template is unchanged. These are catalog attack values, not promised player
damage or validated class balance. The guardian uses the existing boss HUD helper.

Validation: isolated build and **426 title checks** pass. Eleven added world
checks exercise ordinary component entry, actual map/entities, premature cache
refusal, foreign death/attack/teleport refusal, ten saved deaths before guardian
spawn, cache return, map disposal, delivery qualification and abandoned-scene
cleanup. Fixtures position the character next to actors and invoke `Entity.Die`;
they do not prove real attack execution, movement, AI balance or native rendering.

The existing Planet regression passes **122 parent plus 9 child checks** with the
new shared hooks. Its first run failed because the restart fixture kept the
parent's AccountActivity lease alive while launching a fresh child. The fixture
now relinquishes the lease before the child and reacquires after exit, matching
its declared stopped-parent transition. Production lease rules are unchanged.
Logs: `obj/heart-tests.log` and `obj/heart-planet-regression.log`.

No public Heart entry NPC, cache dialogue, startup/login recovery invocation or
automatic Heart sweep has been activated. The test calls component entry, cache
collection and sweep directly. Those lifecycle/NPC connections, real combat
validation, faction standings, prize grants and native acceptance remain open.

## Lifecycle hooks and calculated combat (2026-09-25)

The source startup path now initializes Heart tables and closes interrupted
attempts before serving the event. Recovery verifies no active rows remain and
refuses to run while component scenes exist. The world timer invokes a one-second
cleanup sweep. Login recovery closes an old session's scene/receipt, returns the
new canonical player, and removes the old map without losing the sortie mask.
An already ended receipt may be closed idempotently; a suppressed close still
fails. Login stops if return recovery fences the character.

The isolated build and **444 title checks** pass. The world fixture now defeats
all ten raiders and the guardian through normal melee calculation and damage
application using the existing controlled 20,000-attack helper. A foreign direct
damage call is rejected before HP changes. Replacement-session recovery preserves
one earned raider, returns safely, and resumes with nine raiders; repeated recovery
is harmless. A live-scene initialization attempt is refused.

The 18 additional checks cover these combat and recovery paths. This remains a
synthetic ordinary-session replacement, not wire login or real process restart.
Attack values, adjacent placement and admission levels are controlled fixtures;
AI pressure, representative equipped classes and native acceptance remain open.
There is still no public Heart entry or cache dialogue, faction results or reward
producer. No deployment or running-server restart was performed.

## Frozen faction results (2026-09-25)

`HeartCampaign.Results.cs` now freezes both faction totals and qualifying prize
entitlements in the same transaction as the closed marker. It uses the same
weekend lock as objective/delivery writes and validates the saved Pacific window.
Every delivered sortie contributes 100 faction support, including partial
participants. Only characters with three delivered sorties receive a result.
Both qualifying factions receive the wing entitlement; the higher-scoring faction
also receives the fixed 5,000 CP/1,000 RVP entitlement. Ties qualify both factions
for that bonus. Wing expiry remains 720 hours after the original close.

The world timer polls overdue saved weekends every 30 seconds, at most twelve
per pulse. Repeated/concurrent closes do not replace frozen totals or extend
expiry. Failed result insertion rolls back scores and the closed marker, leaving
the weekend eligible for retry. Closing does not alter live inventory or balances.

Isolated build and **460 title checks** pass. Sixteen additional checks cover
open-weekend refusal, suppressed-result rollback, concurrent closing, wins/losses,
ties, partial/empty eligibility, fixed wing expiry and delayed replay. These
result fixtures seed completed sortie rows to isolate ranking/entitlement logic;
the separate world suite provides combat/delivery evidence. They do not prove a
whole earned three-sortie campaign or actual prize delivery. The award consumer,
ordinary NPC/standings flow and full earning/native acceptance remain open.

## Atomic prize delivery (2026-09-25)

`HeartCampaign.Prizes.cs` consumes a closed result for its canonical ordinary
character. The entity balance lock precedes the result lock. Durable CP/RVP
preimages are checked through `PurseBalance`; overflow leaves the entire claim
pending. A single transaction creates the 30-day Heart wing source receipt,
updates balances and acknowledges the frozen result. Duplicate claims return
without granting again. An existing wing receipt without its corresponding claim
acknowledgement is treated as inconsistent state, not permission to pay twice.

Only after commit are CP/RVP projected to the client and wardrobe eligibility
reconciled. Any failure, including interrupted projection after commit, fences the
session for reload. Login attempts up to twelve pending weekend claims; excess
history can be recovered by later requests. A late claim preserves the original
wing deadline. The losing faction's qualifying players receive the wing with no
currency bonus. Public NPC claim access is still pending.

Isolated build and **476 title checks** pass, including sixteen added prize checks.
They cover balance overflow, acknowledgement rollback across both currencies and
wing receipt, concurrent claims, live/persisted currency, losing-faction wings,
postcommit interruption/replay refusal, ordinary equip and exact expiry/unequip.
The minimal title fixture now includes currency columns and initializes wardrobe
login before equip; these were fixture gaps, not changes to production admission.
The rollback retry explicitly rearms its test session after verifying saved state;
full socket reload is not established by that test. Results remain seeded ranking
fixtures here. A full three-sortie earned claim, player-facing NPC and native/
process acceptance remain open. No deployment or restart occurred.

## Ordinary NPC earning route (2026-09-25)

The existing City Commander (380) now offers **Heart campaign**. Ordinary players
confirm a free, weekend-fixed faction choice; either banner is available regardless
of marriage. They enter or resume up to three sorties, collect the guarded cache
through its own dialogue, and return to the commander to deliver it. Standings
show both faction totals and personal deliveries. Closed prizes can be claimed
there, including outside the weekend. Displayed-action checks consume faction
confirmation before mutation; proximity and canonical-session checks remain required.
The cache additionally enforces owned-scene identity and three-tile proximity.

Isolated build and **536 title checks** pass. The new ordinary-player fixture
defeats all thirty raiders and three guardians through normal calculated melee,
collects and delivers three caches through NPC pages, closes that earned weekend,
claims 5,000 CPs/1,000 RVPs plus Heart wings, reloads wardrobe ownership, equips,
and verifies exact expiry. It also checks forged confirmation/cache requests,
farewell invalidation, remote requests, replayed delivery/claims and a fourth sortie.
No completed-run or prize rows are seeded for this earning fixture. Time, adjacent
combat placement and attack strength are controlled, so this does not establish
representative class balance, wire login, a real host restart or native presentation.

The wardrobe inventory now classifies Heart as code-validated: 17 such rewards,
49 predicate-only and six unimplemented, with 66 eligibility expressions across
72 unique catalog keys. The isolated build includes the owner's existing WIP;
this is source validation, not clean release acceptance. No deployment, client
patch publication or live restart was performed.

## Fresh-process persistence evidence (2026-09-25)

The title suite now launches its own bounded `--heart-restart-check` child against
the disposable validation schema. Parent fixtures enter through the commander,
earn one combat objective for an interrupted run, and earn another complete cache
whose return acknowledgement is deliberately suppressed. Fixture sessions/scenes
are discarded without graceful abandonment before the child starts. The parent
relinquishes its account-activity lease and reacquires it after the child exits.
Database configuration is transmitted through redirected stdin, never command-line
arguments, persisted diagnostics or source. The child refuses another search path.

The child starts with no sessions/scenes, invokes production startup recovery and
login recovery methods, safely returns both ordinary fixture characters, retains
the earned objective, and delivers the recovered cache through the commander.
It restores the previously earned three-sortie winner's committed currency values
and reloads/equips its wing. Repeated recovery pays nothing extra. It checks the
exact saved expiry, expiry-driven unequip, and absence of an early wing or invented
delivery for incomplete participants. Parent checks confirm child writes survive exit.

**554 parent title checks and 10 child checks pass.** The child uses canonical
fixture sessions and explicitly loads currency projections; it does not run full
EntityTable loading, wire authentication or the production Host composition root.
This is fresh-process component persistence evidence, not full Host restart/native
acceptance. Those gates, representative class balance and deployment remain open.
No production code, running service or live character data changed in this batch.

## Cache committed before encounter close (2026-09-25)

Startup recovery previously classified every still-active encounter as abandoned.
That was incorrect when all twelve objectives, including the cache, had committed
but the separate encounter-close update had failed. The saved run mask still
retained delivery eligibility, so this was a recovery-state inconsistency rather
than proof that earned rewards were lost.

Recovery now uses the same saved-mask rule as normal closure: mask 4095 becomes
complete; incomplete masks become abandoned. It does not acknowledge a return,
deliver a cache, alter objective receipts or grant a wing during startup.

The fresh-process fixture adds a third ordinary entrant, defeats its ten raiders
and guardian through normal calculated combat, and suppresses the encounter-close
write after cache credit commits. The player fences and the database retains an
active encounter attached to a complete run. After discarding fixture memory,
the child verifies complete classification, safe return and exactly one NPC
delivery with no early wing. The parent confirms the saved complete/returned
state after child exit. Existing interrupted combat and interrupted return cases
continue to pass.

The added scenario failed against the previous executable and passes after the
fix. Isolated build, **568 parent title checks and 12 child checks** pass. Logs:
`obj/heart-close-before.log`, `obj/heart-close-build.log` and
`obj/heart-close-after.log`. This remains component recovery in a fresh test
process, not an actual game Host with logged-in players. The earlier composed
startup report retains its original executable hash; it does not certify this
new build. No private runtime refresh or live deployment occurred for this fix.

## Actual game-process earning and immediate wing availability (2026-09-27)

- [x] Start the real private game composition with an eligible ordinary profile.
  Join singles through City Commander380 and earn one raider objective through
  the owned encounter's normal damage/death hook. Interrupt the actual process.
- [x] Restart PostgreSQL and the game; load the character through EntityTable and
  invoke the normal Heart login recovery hook. The abandoned encounter returns
  safely, preserving exactly one objective. NPC re-entry spawns only the remaining
  nine raiders and guardian. Complete that sortie and two additional sorties,
  collecting each cache and delivering it through the commander.
- [x] No early result exists. Advance the private campaign clock to the canonical
  weekend end, run normal judging, and claim once through the NPC. Three runs,
  36 objective receipts,300 support, one award and the winning5000 CPs/1000 RVPs
  persist. Repeated delivery and prize requests do not duplicate rewards.
- [x] Open the wardrobe before claiming, then equip the wing immediately after
  claiming. This failed before the fix: `HeartCampaign.Claim` reconciled against
  the cached pre-claim award list for up to30 seconds. Invalidate that list only
  after the award transaction commits, before reconciliation. No reward rules,
  payment transaction or authority checks are changed.
- [x] A third actual game process restores the equipped wing before another equip
  request. Repeated claim/equip preserves the result fingerprint and currency.
  Both the result and wardrobe receipt retain exactly720 hours of duration.
- [ ] Authenticated/native client UX, representative natural combat balance,
  naturally elapsed expiry and deployment remain pending. Losing/tied factions
  and failure-injection scenarios retain their separate component-test evidence.

Reproduce: `tools/Verify-ArenaProcess.ps1 -HeartOnly`. Three launches pass in
`obj/heart-process-run.log`, against executable SHA256
`F702C021C627E79513165F367F795F554B517985A731034A40BAD3646710ACB8`.
Build succeeds with existing legacy warnings; the process helper compiles with
warnings as errors. The pre-fix run reached prize delivery but failed the immediate
wing equip assertion. This is an offline-profile process test: qualification,
proximity, lethal damage and event time are supplied; earning receipts, materials,
standings and prizes are not seeded. Authentication and native packet/UI behavior
are not covered. The marked database clone is removed and private processes stop
on success. No live database, game service or installed client is changed.
## Earned wing expiry across actual game processes (2026-09-27)

- [x] Extend the earned three-sortie process suite to five game launches. A fresh
  process restores the saved equipped wing one .NET tick before its original
 720-hour deadline. At the exact deadline, an ordinary wardrobe request removes
  the expired wing and refuses re-equipping it.
- [x] Another game process starts at that same deadline. Login, repeated equip
  and repeated commander prize requests cannot restore or renew the wing.
  The original result fingerprint and single historical award remain unchanged;
  the earned5000 CPs/1000 RVPs are retained without another payment.
- [x] Use explicit DateTimeOffset parameters for the helper's saved clock metadata,
  avoiding implicit PostgreSQL session-timezone conversions. Production quest
  timestamp handling is unchanged.
- [ ] Native rendering/packet acceptance, representative combat and deployment
  remain pending. Deadline behavior is verified with a controlled UTC clock,
  not a claim that thirty wall-clock days elapsed during testing.

`tools/Verify-ArenaProcess.ps1 -HeartOnly` passes all five launches; log
`obj/heart-expiry-process-run.log`. Existing executable SHA256
`7D86AB5305F15DFE9C77E9849B27A166597C3D497B71A4BF1E49CB43A10F8055`.
The suite earns its progress and rewards again before expiry checks; no award
or expiry receipt is supplied. No production code change or rebuild was needed.
Private database cleanup and process shutdown complete; live state is unchanged.