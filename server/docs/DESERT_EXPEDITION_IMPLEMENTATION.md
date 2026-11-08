# Desert Expedition implementation record

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence

Evidence baseline: 6740e8b3. The initial audit did not implement an earning route;
the completed source integration and its validation are recorded below.
The hash-gated `tools/audit_desert_expedition.py` verifies five client stages
1835-1839, required materials, Master Mo Mo's location, pack728250 and equipment
candidates150219/152229. [Contract](research/DESERT_EXPEDITION_CONTRACT.json).
Client minimum105 is consistent throughout, maximum125. Revora will retain
minimum105 without an upper cap, once per rebirth, as in neighboring quests.
Root Prequest=0: do not add a Desert Treasure dependency based only on old tips.

The [official guide](https://co.99.com/guide/quests/desertexpedition.shtml)
describes a one-minute pill, losing10% of current HP every ten seconds, during
which Bull Monster kills can produce horns. Ten horns precede the Ghost Mirror.
Lure Mad Bulls into placed traps and kill them for three ghosts, then bring the
Bull Master's heart. Rewards distinguish level110 Super ring from level115
Super Taoist bracelet, plus silver, experience and an EXP Ball. Rebirth resets
eligibility. Original drop probabilities and trap duration are not published.

The installed Fervent Pill721920 tooltip instead says five minutes. Follow the
official one-minute mechanic and correct the private tooltip. HP rounding and
missed-tick/reconnect behavior require explicit documented implementation;
never let logout remove the disadvantage while retaining its earning benefit.
The lure condition must check the bull's position at death, not merely mirror
ownership or the player's location. Unverified visual effects stay disabled.

## Runtime and terrain

The loaded read-only catalog probe identifies BullMonster57 (level112,mesh284)
and BullMonsterL11382 (level113,mesh282), with272 configured bodies on map1001.
RedBull2231 and BlueBull2232 use mesh317. BlueBull has a spawn at the journal's
Bull Master coordinate186,359, but this alone does not prove its original role.
No row exists for NPC4702. The client confirms MasterMoMo model475; source
registration at1000(520,589) remains necessary. No MadBull/BullMaster-named
template was found in the bull-name probe. [Runtime](research/DESERT_EXPEDITION_RUNTIME.json).

Shared server/native terrain connects the horn hint173,329 with all three trap
hints172,399;102,292;109,369 and the Bull Master point186,359. Mo Mo's north
approach520,587 is blocked; west approach518,589 is verified with NPC footprint.
These are static paths, not native lure/combat or actual loaded population tests.
[Terrain](research/DESERT_EXPEDITION_TERRAIN.json), reproducible using
`tools/audit_desert_expedition_terrain.py`. All probes leave live state unchanged.

## Implementation sequence

- [x] Client contract, runtime catalog and shared-terrain prerequisites.
- [x] Class-aware pack728250: ring150219 or Taoist bracelet152229; preserve binding.
- [x] Durable per-rebirth progression, tracked materials and lost-item recovery.
- [x] Persisted pill duration, scheduled current-HP drain and gated horn drops.
- [x] Owned trap placement and Mad Bull lure/death capture (seeded-stage tests).
- [x] Owned Bull Master earning path and tracked heart (seeded-stage tests).
- [x] Atomic report/final bound rewards and duplicate protection (store tests).
- [x] Ordinary NPC/item/journal/combat integration and full route tests.
- [x] Private composed journal/tooltip overlay.
- [ ] Native/Host acceptance and deployment.

Owner policy adds a frozen bound P7 soul to this one-time quest's final bundle.
Keep the bound EXP Ball separate from the base equipment pack as in adjacent
quests and correct the tooltip accordingly. Preserve original tier equipment;
explicit Revora probabilities/tuning must be documented. No live schema,
client installation, production build or deployment was performed for this audit.

## Equipment pack

Pack728250 now resolves to Super DragonRing150219 (level110) for supported
physical classes or Super DiamondBracelet152229 (level115) for supported Taoists.
Catalog identity, class and level are checked before transaction construction.
Unknown classes retain their unopened pack. Outputs are +0 and preserve source
binding. The existing fixed-pack inventory transaction and receipt prevent
replay and retain the pack on failed inserts. The capacity guard explicitly
covers this pack independently of unrelated owner work in the handler.

The one-time quest will award its bound EXP Ball separately; the pending private
overlay will correct the old pack tooltip to avoid promising a second EXP Ball.
Quest acquisition is not implemented by this pack change. No live schema
initialization, client installation or deployment was performed.

Build and435 checks passed (406 shared prerequisites +29 pack checks): ordinary
use across ten promoted classes, bound/unbound output, exact persisted UIDs and
receipts, concurrent stacked use, capacity, rollback/reconnect retry and unknown
class rejection. Logs: `obj/expedition-pack-build.log`,
`obj/expedition-pack-tests.log`. Next: persistent progression and pill timing.

## Persistent referrals and materials

`DesertExpeditionStore` now records one run per character/rebirth (0-2), level105+
eligibility, frozen bound P7 identity and tracked material UIDs. State transitions
are acceptance0->1, Mo Mo's bound pill referral1->2, ten earned horns plus pill
exchanged for a bound mirror2->3, and three earned ghosts plus mirror handed
in3->4. Each referral pays100 silver atomically. The heart report and final claim
remain separate pending operations; the generic advance method cannot skip them.

Untracked same-type items cannot satisfy hand-ins. Owner/session, saved level,
rebirth and inventory checks plus database/owner locks protect transactions.
Insufficient space or missing materials preserve the whole state; item insertion
failure rolls back consumed items and payments. Lost-item recovery is limited to
stage-relevant previously earned UIDs absent from the entire item table. Stored
or transferred items cannot be copied, and receipts retire replaced entitlements.
No NPC/Host initialization is connected yet; pill timing and trap earning remain
pending. Tests seed materials explicitly and do not claim an ordinary route.

Build and467 checks passed (406 shared +29 pack +32 store), across all three
rebirth states. Coverage includes underlevel/order/replay rejection, full bag,
stored/lost pill recovery, tracked material counts, failed mirror insertion,
frozen soul persistence, concurrent ghost hand-in and saved silver. Logs:
`obj/expedition-store-build.log`, `obj/expedition-store-tests.log`.
Next: persisted one-minute pill, HP drain and eligible bull drops. Nothing deployed.

## Final report and rewards

The tracked BullHeart710260 now advances stage4->5 in Desert City, consuming
that earned UID and committing6000 silver plus one direct EXP Ball worth of
experience in the same transaction. Existing progression, mentor and rebirth
recovery paths handle experience and its cap. Together with the three referrals,
the quest pays6300 silver. An untracked same-type heart cannot qualify.

Stage5->6 in Twin City grants bound728250 equipment pack, bound723911 EXP Ball
and the bound P7 soul frozen at acceptance. Three free inventory slots are
required. The terminal stage and exact output UIDs commit with the entire bundle;
failed inserts preserve eligibility and cannot leave partial rewards. Ordinary
session/ownership checks apply; there is no GM or owner-name requirement.

Validation explicitly seeds the heart and report stage to isolate reward
transactions. It does not prove Bull Master earning or a playable NPC route.
Build and500 checks passed (406 shared +29 pack +32 store +33 rewards).
The reward checks cover each rebirth, progression caps, rollback, capacity,
concurrent claims, exact bound receipt UIDs and reload. Logs:
`obj/expedition-rewards-build.log`, `obj/expedition-rewards-tests.log`.
Nothing was deployed or initialized in the production schema.

### Pill synchronization finding

Entity.Hitpoints sends HP updates but does not itself persist player HP.
ReceiveAttack takes a TeamArenaCombatLease only for arena participants; ordinary
wilderness combat does not universally take ItemSyncRoot. A pill implementation
that only holds that inventory lock across a database write could overwrite
concurrent combat HP. Before connecting the effect, establish a consistent HP
mutation boundary, persist timer/drain progress together, and validate concurrent
damage, missed ticks, failed writes and reconnect. The pill milestone remains
unchecked; this prerequisite is not a reason to alter the official60-second rule.

## Persisted pill schedule (not yet connected)

The store now starts a reusable tracked Fervent Pill only during stage2 on
map1001. One UTC deadline survives repeated clicks, concurrent activation,
reconnect and replacement of a genuinely lost item. The item remains available
for its later horn hand-in. A dropped item that still exists in the item table
cannot also be recovered.

Six debits are scheduled at10,20,30,40,50,60 seconds. The persisted paid count
will be advanced only by the pending HP settlement transaction. Expiry does not
forgive unpaid ticks; neither reactivation nor the horn hand-in can clear them.
A new minute is permitted only after expiry and all six debits are paid. The
schedule rejects clocks before activation or the last paid tick and normalizes
stored timestamps to PostgreSQL microsecond precision.

Explicit Revora arithmetic uses floor10% of current HP for each debit, minimum
one damage while leaving at least one HP; zero HP remains zero. Missed debits
compound from current HP at settlement, capped at six. These rules are tested
as calculations only. Actual synchronized HP persistence/projection, periodic
settlement, login settlement and gated horn drops are still unimplemented.
There is no NPC/item/tick hook for ActivatePill, so this batch cannot expose a
benefit without its cost. The combined pill/hunting milestone stays unchecked.

Build and541 checks passed (500 prior checks +41 pill schedule checks), including
all three rebirth states, failed activation, concurrent use, reload, recovery,
expiry and unpaid hand-in rejection. Settlement is seeded only to verify
reactivation; no HP payment is claimed. Logs: `obj/expedition-pill-build.log`
and `obj/expedition-pill-tests.log`. No live schema or client was changed and
no deployment occurred.

## Owned bulls and trap captures

The mirror can now place one persisted five-minute trap at one of the three
journal sites:172,399;102,292;109,369 on map1001. Placement requires the tracked
mirror, stage3 and distance at most3. Repeated placement cannot extend an active
trap. Each site can yield one of the three required ghosts per rebirth; this
site uniqueness, five-minute duration and guaranteed successful capture reward
are explicit Revora rules, not claimed original odds.

An owned MadBull spawns four or more cells east of the trap. Only its owner's
screen callback drives its normal walking/attack AI; unrelated viewers cannot
steer it. Owner/current-companion combat leases and checks reject strangers.
Capture tests the bull's death location within one cell of the same unexpired
trap UUID. The owner standing on the trap does not qualify a distant kill.
A successful capture commits the used trap, unique death receipt and bound ghost
together. A missed lure leaves the active trap available for another attempt.

The three earned ghosts plus mirror advance to the owned BullMaster near
186,359. Its death grants one tracked bound heart. Full inventory grants nothing
and permits a new encounter. Lost earned materials remain recoverable through
the existing missing-UID path. Encounters are capped at32 globally/one per owner,
expire after five minutes and clean up on owner replacement, departure or death.
MadBull HP35535, BullMaster HP100000, attack1000-1500 and defence500 are Revora
values using the verified RedBull mesh317; no original combat-value claim.

The terrain audit now covers every horizontal approach/spawn cell from x-3
through x+8 at each site, shared by the hash-verified server and native grids.
Build and574 checks passed (541 prior +33 encounter checks), including forced
ghost-insert rollback/reconnect retry, ownership, real AI lure, distant-death
rejection, exact bound material persistence, bag capacity and cleanup. Logs:
`obj/expedition-encounters-build.log`, `obj/expedition-encounters-tests.log`.
Fixtures seed only the mirror stage;
they then exercise actual movement, combat, three captures and heart earning.
No public NPC/item activation or trap visual has been connected, and the full
route still depends on the pill/horn step. No production initialization,
deployment or native-client acceptance occurred.

## Horn award gate

BullMonster57 and BullMonsterL11382 deaths on map1001 now have an internal horn
award operation. It requires an ordinary current owner, registered actual corpse,
matching killer, screen proximity, stage2, a tracked owned pill and a deadline
strictly after now. Every HP tick due at both transaction entry and commit must
already be paid; a timer alone is insufficient. At exact expiry no horn qualifies.

Explicit Revora odds are50% per eligible death. The dead body is attempted only
once, including a losing roll, full bag or failed insert. Map respawn resets the
attempt marker only after the monster is alive. Tracked bound horns cap at ten,
including lost/stored entitlements, so recovery cannot be combined with repeat
farming to duplicate them. Inventory insertion and material tracking commit
atomically; a failed insert leaves neither item nor entitlement.

Build and590 checks passed (574 prior +16 horn checks). Coverage includes both
families, actual combat deaths, concurrent credit, odds boundary/replay, unpaid
tick and expiry rejection, failed inserts, capacity, cap and reload. Logs:
`obj/expedition-hunt-build.log`, `obj/expedition-hunt-tests.log`. Paid ticks are
seeded to isolate this gate: actual HP settlement is still pending. Horn awards
and pill use remain disconnected from public gameplay until that prerequisite
and ordinary route integration are complete. Nothing deployed.

## HP payment transaction (live synchronization gate remains)

SettlePill now commits current HP reduction, paid-tick progress and a receipt in
one transaction. The receipt identifies character/rebirth, activation deadline,
paid range and before/after HP. Concurrent settlement calls cannot pay twice;
expired sessions catch up unpaid ticks instead of resetting them. A failed
receipt rolls back both HP and progress. Committed HP is then projected through
the existing client/team update path. Dead characters cannot invoke settlement
through the current ordinary-player eligibility guard.

Entity now exposes a health mutation boundary and serializes individual player
HP setters through it. Settlement holds the inventory lock before this boundary.
This is NOT yet sufficient for live activation: legacy expressions that read HP
and later assign it must hold the same boundary across the whole calculation,
including lethal-damage checks. Do not wire periodic/login/item hooks until
combat and healing callers have been covered and interleaving tests pass.

Build and611 checks passed (590 prior +21 actual-payment checks), across all
three rebirth states.1000 HP becomes900 at10 seconds,729 after the30-second
catch-up,657 at40 seconds and533 after all six ticks. Tests verify concurrent
settlement, injected receipt failure, rollback, reconnect retry, expiry and
saved HP/receipts. Logs: `obj/expedition-payment-build.log` and
`obj/expedition-payment-tests.log`. This does not establish safety against all
legacy combat/HP writers. Full route/native acceptance and deployment remain
pending; nothing was deployed.

## Scoped combat/buff/medicine synchronization

Unpaid pill state now enables HealthSynchronizationRequired on activation/load,
and settlement clears it after all six ticks are paid. HealthMutationLease takes
participating owners' inventory gates in UID order, then their health gates;
failed nonblocking acquisition releases all acquired gates. Unaffected actors
acquire no additional lease gates. This avoids waiting through opposing-owner
lock orders. A contested combat attempt returns zero damage; contested medicine
use retains its item and creates no receipt.

The normal attack constructor, ReceiveAttack, per-character buff callback and
HealingMedicine use this lease for the complete calculation/update operation.
Build and618 checks passed (611 prior +7 interleaving checks), including competing
player damage and medicine during a settlement transaction, subsequent successful
damage/healing, reconnect flag restoration and release after full payment. Logs:
`obj/expedition-health-build.log`, `obj/expedition-health-tests.log`.

Live wiring is still gated: review per-recipient area healing and direct player
health writers in guard/adventurer retaliation and character callbacks. Some event
combat already holds the target ItemSyncRoot (for example Exorcism/SnowBanshee),
which serializes against payment; do not add redundant broad locks blindly.
Verify actual incoming monster attack construction and the remaining direct paths
before claiming complete combat coverage. The initial fixture incorrectly sent a
monster through the player ReceiveAttack entry; the corrected interleaving test
uses a real player attacker. No runtime feature activation or deployment occurred.

## Direct retaliation and healing paths

Guard strikes and adventurer retaliation now acquire the scoped lease before
range/cost/damage/death handling. Companion healing takes it before choosing a
recipient or spending spell resources. Three player-heal branches use a shared
per-recipient operation that computes missing HP and applies healing inside the
lease. The character flame callback protects its target's nonlethal HP update.

Build and620 quest checks passed (618 prior +2 area-recipient interleavings).
The existing in-memory guard37, retaliation28 and companion57 checks each passed
both normally and with all ordinary fixture players marked as having unpaid pill
health costs. Enable that fixture mode with REVORA_TEST_HEALTH_LEASE=1; it changes
no production configuration. These separate totals must not be added to the
quest suite as unique checks. Logs: `obj/expedition-direct-health-build.log`,
`obj/expedition-direct-health-tests.log` and `obj/expedition-*-health-tests.log`.

This covers the identified direct guard/adventurer/companion and area-heal gaps.
Complete the remaining writer audit and ordinary periodic/login/NPC/item route
before enabling the effect. No runtime activation, deployment or native-client
acceptance occurred.

## Ordinary source route and private overlay

DesertExpeditionQuest now connects Taskmaster Chang options180/181, MasterMoMo4702
at1000(520,589), five protected journal stages1835-1839 and ordinary item handling.
Three SpiritStone markers9918352-9918354 stand two cells south of the trap centers;
they explain the mirror placement rather than claiming an unverified ground glow.
These static markers are a documented Revora presentation adaptation. The terrain
audit includes their footprints and all approach/lure corridors.

Pill use, eligible bull deaths, Mo Mo hand-ins and login settle due HP costs before
benefits. A one-second quest pulse settles due watched costs and starts eligible
owned bulls near their hunting sites. Expired costs survive reconnect. Horn credit
checks the clock again before commit; crossing a deadline/tick rolls back the drop
without treating a normal timing boundary as a session failure. Weapon HP procs
also use the same per-recipient healing operation. Full ordinary source activation
is wired through existing initialization, NPC, item, death, login and journal paths.
This source wiring has not been deployed to the live Host.

Build and485 ordinary-route checks passed (406 shared +79 route) across all three
rebirth states, including a Taoist equipment result. No quest material, paid tick,
trap capture or completion stage is seeded. Tests exercise actual incoming monster
attack construction with an active pill, actual kill hooks, timed and login HP
payments, the ten-horn hand-in, three mirror/lure/combat captures, automatic owned
BullMaster, heart report, final bound bundle, repeat rejection and full reload.
Remote/underlevel acceptance and forged journal requests are denied. The configured
RNG is deterministic in the fixture; population density and real client rendering
are not proven by this route test. Logs: `obj/expedition-quest-build.log`,
`obj/expedition-quest-tests.log`, `obj/expedition-quest-terrain.log`.

`tools/ClientPatches/prepare_desert_expedition_journal.py` composes over the private
Desert Treasure overlay. It updates five missions and three tooltips, including
the one-minute pill and the class-aware pack with its separately awarded EXP Ball.
Hash gates, unrelated-journal preservation, idempotence and itemtype encryption
round-trip pass; installed client bytes remain unchanged. Private outputs:
`obj/chi-refill-validation/desert-expedition-journal/`.

- Questinfo SHA256: a9bb11d3b44be6208b3e01abb011ef42704f756a7e5703f36bc862fd8266b4c2
- Itemtype TXT:945d0c61f987f1bde6f051f832dc61a59f3127799dd5a454bf88e08f95d73f58
- Itemtype DAT:d6298cf31f3d2b4ce75d2f53c9ca9dd2a7ba3df3f0efc76fe9dd5824756f1a19

The earlier620-check store/health suite and separate guard/companion suites cover
additional failure/interleaving cases; overlapping totals are not additive. Native
rendering, timing under live load, staged Host acceptance and release remain pending.
No production schema initialization, client install, signing or deployment occurred.


## Configured bull population and respawn verification

The disposable-schema fixture copies only public map1001 spawn rows for families
57 and82 using SELECT. Production Map.LoadMonsters places all272 configured bulls
alive inside their configured rectangles; no placement change was needed. Actual
respawn callbacks restore each family to full template HP. An ordinary level105
character accepts through Chang/Mo Mo, uses the earned pill, kills a loaded bull,
then kills that same bull after revival. Each death earns a tracked horn, and both
horns survive reload. No quest stage, material or paid tick is seeded.

421 checks pass (406 shared +15 population checks), with deterministic successful
horn rolls and a controlled quest clock. This proves the tested loader and revival
paths, not statistical drop odds, every random placement sequence or live timing.
Test: VerifyDesertExpeditionPopulation in ConquerPointBagTests.cs; selectable with
Verify-PotencyHunt.ps1 -ExpeditionPopulationOnly. Executed using the existing private
harness against the current validation binary; log: obj/expedition-population-tests.log.
Native/Host acceptance, live-load timing, installation and deployment remain pending.


### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001, life2, deposits spare belongings
  through its house box, accepts the quest and earns ten materials with the pill
  active through calculated BullMonster attacks. Premature hand-in consumes
  nothing; at sixty controlled seconds all six health deductions are paid,
  leaving the character alive with less HP.
- [x] Each of three trap sites uses the mirror and actual owned AI movement
  to lure the bull into range before calculated combat. All three materials
  are earned, followed by the final owned encounter and its tracked heart.
- [x] A separate process reloads the heart, reports and claims three bound
  rewards (728250, 723911 and one P7 soul), with exactly 6,300 silver across
  the quest. Another process preserves exact career, pill, trap, encounter and
  reward state; repeat final claim leaves the snapshot unchanged.

Evidence: `career-retained-expedition-hunt`, `career-retained-expedition-claim`,
and `career-retained-expedition-reload` all passed on private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
Clock, drops, approach positions and return from home are controlled; public
quest travel uses portals. No injected kill damage or quest receipts. Native
rendering/human playability and deployment remain pending. Aggregate15/19;
achievement10931 is incomplete with four chains outstanding.
