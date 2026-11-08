# Desert Treasure implementation record

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Evidence and current status

Source baseline: 53693bbc. No earning route is implemented by this batch.
`tools/audit_desert_treasure.py` validates the installed client hashes, seven
missions (1828-1834), nine materials, reward pack 728249 and boots 160199.
Results: [contract](research/DESERT_TREASURE_CONTRACT.json). Client data is
read only; no proprietary assets are included in Git.

The [official guide](https://co.99.com/guide/quests/deserttreasure.shtml)
supports a level-98 quest repeated after rebirth: obtain a key from Tomb Bats,
reveal a compartment, open the Scripture Box, deliver its scripture, purchase
and inspect a portrait, collect five Bat Essences, reveal and deliver the
portrait within one minute, then use a Pure Bottle on the killed Jinx Tomb Bat
before its corpse disappears. Rewards include level-100 Super boots, silver,
experience and an EXP Ball. A side quiz offers half an EXP Ball of experience.
Drop, compartment and bargaining probabilities are not supplied.

A [2009 player article hosted by the publisher](https://co.99.com/content/2009-03-31/20090331173636642.shtml)
mentions a maximum bid of 99,999 silver and a three-second corpse window.
These are lower-confidence historical observations, not verified current rules.
It also names a preceding Family Feud requirement, while installed mission 1828
has Prequest=0. Do not silently introduce this extra dependency.

## Client inconsistencies

All seven sections repeat ActivityType=0. The auditor accepts only this known
identical duplication. Final mission 1834 says level 45-65, incompatible with
the preceding level 98-118 stages. Revora should use minimum 98 throughout and
no upper cap, matching its other equipment quests. Louis has destination
(466,658), while prose also says (466,649); actual catalog and terrain checks
must settle the usable position before integration. The final NPC label also
contains a stray color suffix. Correct these in a composed private overlay.

## Implementation sequence and acceptance requirements

- [x] Hash-gated client contract and explicit unknowns recorded.
- [x] Audit runtime catalog and static terrain; absent NPCs/Jinx remain implementation work.
- [x] Implement boots pack safely, preserving binding and inventory capacity.
- [x] Add durable per-character/per-rebirth progression and tracked materials.
- [x] Add earned key/essence drops, compartment interaction and recoverable items.
- [x] Implement an atomic paid portrait purchase and persisted 60-second reveal.
- [x] Implement owned Jinx encounter and corpse-bound bottle eligibility.
- [x] Integrate ordinary NPC/item/journal paths and optional one-time quiz.
- [x] Verify concurrency, rollback, replay, reconnect and ordinary completion in isolated fixtures.
- [ ] Stage composed client text; native/Host acceptance, signing and deployment.

Use explicit documented Revora probabilities when original odds remain unknown.
Freeze the final bound P7 entitlement once per run, alongside the original pack
and bound EXP Ball, under the owner's one-time-quest reward policy. Keep quiz
experience separate and one-time; unknown original questions require documented
Revora questions. Paid failures must never accidentally debit twice. Expiry must
preserve a recovery path. Do not grant rewards through client journal mutations.
No server build or deployment is justified for this evidence-only batch.

## Runtime and terrain evidence

The read-only loaded catalog probe found no public NPC rows for IDs 4651/4652
and no Jinx-named bat template. Client npc.ini identifies Louis as model 408
and Shark as 409; future source registration must handle ID conflicts explicitly.
Ordinary families are TombBat 20 (level102, mesh206), TombBatL103 80,
BloodyBat 56 (level107, mesh148), BloodyBatL108 81 and VampireBat 86.
Their map1001 spawn definitions total 704 configured monsters; every rectangle
contains server-walkable cells. This is catalog/terrain evidence, not a real
loader population or kill-credit test. Unrelated bat bosses must not earn
ordinary material credit. [Runtime snapshot](research/DESERT_TREASURE_RUNTIME.json).

The hash-gated native/server map audit confirms connected walking routes from
Shark's approach (310,629) to the compartment (91,283), hunting hints and Jinx
region (728,676). Louis's journal position (466,658) is walkable with approach
(466,656). Initial west-side approach guesses were blocked and were replaced
with these verified north-side approaches. Listed NPC footprints are reserved
by the audit, but other live objects and map transitions remain untested.
[Terrain evidence](research/DESERT_TREASURE_TERRAIN.json), reproducible through
`tools/audit_desert_treasure_terrain.py`. No client/world mutation performed.

Next implementation batch: bound-aware boots pack, then persistent earning
state and ordinary interactions. The absent Jinx needs an owned encounter using
a verified bat model; it must preserve a corpse interaction window rather than
automatically award the relic on death.

## Boots pack implementation

Pack 728249 now yields one catalog-verified level-100 Super KylinBoots (160199),
+0, preserving source binding. It uses the existing durable fixed-pack receipt
and inventory transaction. The final quest will separately award the bound EXP
Ball; its old client pack tooltip must be corrected in the composed overlay.
No EXP Ball is duplicated inside this pack. Acquisition remains unimplemented.

Build passed with existing repository warnings. The isolated suite passed 414
checks (406 shared prerequisites + 8 pack checks): concurrent stacked use,
bound/unbound outputs, exact reload persistence, full-bag replacement/rejection,
failed database insert retaining the source, and retry after reconnect.
Logs: `obj/treasure-pack-build.log`, `obj/treasure-pack-tests.log`.
The capacity guard is explicit for this pack and does not depend on unrelated
owner work in the same handler. No live schema initialization or deployment.

## Persistent referrals and portrait state

`DesertTreasureStore` now stores a run per character/rebirth (0-2), freezes its
bound P7 choice on acceptance, tracks material UIDs and consumption, and stores
portrait inspection plus a UTC reveal deadline. Eligibility is level 98+,
ordinary managed characters, with canonical session/owner and inventory checks.
This foundation is not wired into NPC/item dispatch or Host initialization yet.

Implemented transitions: acceptance; Louis's bound Reveal Vial referral;
tracked Scripture hand-in; inspected portrait referral; timely real portrait
hand-in granting a bound Pure Bottle. Each implemented referral pays 100 silver
atomically. Purchase (stage3), relic report (stage6) and final claim are separate
pending operations and cannot be skipped through the generic advance method.

Inspection requires the tracked blank portrait. Reveal requires five distinct
tracked essences and changes the blank into a bound real portrait. An active
reveal cannot be extended; expiration retains the real portrait and permits a
new five-essence reveal. Hand-in accepts the exact deadline, rejects clock
rollback before the reveal start, and consumes only the tracked real item.
Inventory changes, stage, silver and deadline commit together before projection.

Build and 456 checks passed (406 shared + 8 pack + 42 store checks), across all
three rebirth states. Coverage includes full inventory, untracked item denial,
replay, frozen reward persistence, inspection, insufficient essence, injected
item-insert failure rollback, deadline reload, expiry, renewal and exact-boundary
hand-in. Tests seed purchased/earned materials explicitly; they do not prove
purchase, hunting, corpse interaction or ordinary NPC completion.
Logs: `obj/treasure-store-build.log`, `obj/treasure-store-tests.log`.
No live schema initialization, publication or deployment.

## Shark purchase transaction

The source now offers 30,000 / 60,000 / 99,999 silver bids with Revora-defined
30% / 60% / 100% acceptance. These probabilities are custom, not original odds.
Rejected bids cost nothing and persist a five-second retry interval, including
across reconnect. Successful purchase grants one tracked bound blank portrait,
pays the 100-silver journal stage reward, advances to stage4 and stores the bid
receipt in the same transaction as the price deduction. Inventory/owner locks
serialize concurrent attempts. Unlisted prices, remote requests, full bags and
insufficient funds cannot create a bid or charge. The service requires proximity
to Shark's verified map1001 location and rechecks context before commit.

Build and 474 checks passed (previous 456 + 18 purchase checks): refusal/winning
probability boundaries, maximum bid, persisted cooldown, remote/invalid offers,
full-bag rejection, injected output failure, concurrent use and durable replay
protection. Fixture progression reaches the purchase stage directly; the NPC
route and earned materials still require integration. Logs:
`obj/treasure-purchase-build.log`, `obj/treasure-purchase-tests.log`.
No live initialization or deployment. Next: public hunting and compartment use.

## Earned materials and compartment transactions

The source now accepts registered, ordinary map1001 TombBat/TombBatL103 deaths
for Box Keys and BloodyBat/BloodyBatL108/VampireBat deaths for Bat Essence.
Catalog names/levels/flags are checked at initialization. Actual killer, map,
proximity, body ownership and world registration are checked for each credit.
Revora drop probability is 50%, once per death, including failed rolls. A living
respawn may reset that guard. Active tracked keys cap at one; essences at five.
Boxes/Scripture stop further keys. Live death/respawn hooks remain pending.

Reveal Vial use within three cells of (91,283) saves a 60-second compartment
window. A separate accepted-step operation at the exact cell precedes clicking
open. An active reveal cannot extend. Opening uses a tracked key and retires the
window, with a Revora 50% success probability; failure consumes the key. The
resulting tracked bound box converts once into tracked bound Scripture. Item
insertion failure rolls back the key and window. These custom timing/probability
rules will be disclosed in the private overlay. No new packet layout is assumed.

Build and 492 checks passed (previous 474 + 18 hunting/vault checks), including
actual combat-created corpses, wrong families, probability boundaries, corpse
replay, material caps, location/order checks, persisted step, failure/retry,
box-insert rollback, exact deadline and bound essence reload. Tests invoke the
store directly; accepted movement/click dispatch and real spawned population
acceptance are still pending. Logs: `obj/treasure-hunt-build.log`,
`obj/treasure-hunt-tests.log`. Next: lost-material recovery and owned Jinx capture,
then final rewards and complete ordinary NPC/item/combat integration.

## Lost-material recovery

Recovery is restricted to material types still relevant to the saved stage.
It selects an active tracked UID only when no item with that UID exists anywhere
in the items table, so warehouse, equipment and other ownership do not create
copies. A new bound UID, retirement of the old entitlement and a recovery receipt
commit together under the player lock and saved owner/inventory checks. A full
bag leaves the entitlement untouched. Recovery does not grant progress, silver,
new reward choices or extra portrait time. Live NPC dispatch remains pending.

Build and 501 checks passed (previous 492 + 9 recovery checks), covering
unearned/owned/stored/transferred items, failed receipt rollback, full capacity,
concurrent replacement, reconnect, preserved expiry and obsolete-stage denial.
Logs: `obj/treasure-recovery-build.log`, `obj/treasure-recovery-tests.log`.
The initial fixture compile rejected a local/lambda name collision; this was
corrected before the passing run. No production build repeat was necessary.

## Relic report and final claim

The report transaction at map1000 requires and consumes a tracked Buddha Relic,
then saves stage7, 6,000 silver and one direct EXP Ball worth of capped experience,
including progression/mentor/reincarnation effects through the existing services.
An arbitrary same-type item cannot qualify. Chang's later map1002 claim requires
three free inventory slots and atomically grants bound boots pack 728249, bound
EXP Ball 723911 and the frozen bound P7 soul, records their exact UIDs and advances
to terminal stage8. Source retries cannot duplicate either payout. This is a
one-time quest per rebirth, not a daily P7 source. The optional quiz is separate.

Build and 531 checks passed (previous 501 + 30 reward checks). Tests cover all
three rebirth states, unearned/untracked denial, report rollback retaining relic
and progression, capped EXP, required map, capacity, failure of the final soul
insert rolling back the entire bundle, exact bound UID reload and claim replay.
Tests seed the captured relic to isolate rewards; Jinx earning and complete
ordinary dispatch remain pending. Logs: `obj/treasure-rewards-build.log`,
`obj/treasure-rewards-tests.log`. No live initialization or deployment.

## Owned Jinx encounter

`DesertTreasureEncounter` adds a source-owned JinxTombBat using verified TombBat
mesh206. Revora combat tuning: level105, 100,000 HP, 1,000-1,500 attack, 500
defense. It spawns near (728,676) only for stage6 with a tracked Pure Bottle and
no active relic entitlement. Three candidate cells (728/730/732,676) pass the
shared terrain audit; the runtime checks exact terrain hash and occupancy.
One encounter per owner, at most32 globally, with a five-minute lifetime.

The combat pipeline limits damage to the owner/current registered companion.
Actual death opens a three-second bottle window; repeated death callbacks do
not extend it. Capture requires the current owner, registered corpse, proximity
and tracked bottle. It atomically consumes the bottle, creates a bound tracked
Buddha Relic and stores the encounter receipt. It can replace the bottle in a
full inventory. Expired attempts preserve the bottle for a fresh encounter.
Timeout, capture and replaced-session cleanup remove only the owned actor.
Automatic spawning/tick and ordinary item dispatch remain integration work.

Build and 546 checks passed (previous 531 + 15 encounter checks): real combat,
outsider denial, living/dead distinction, repeated death, expiry/retry, range,
failed relic insert retaining bottle/receipt absence, exact deadline with full
inventory, persisted bound capture, lifetime and replacement-session cleanup.
Logs: `obj/treasure-jinx-build.log`, `obj/treasure-jinx-tests.log`.
Native rendering and actual Host acceptance remain pending. No deployment.

## Optional Louis quiz

The source now implements three documented Revora recall questions: five Bat
Essences, one-minute portrait duration, and bottle use while Jinx's corpse
remains. Original question wording is unverified. A wrong answer resets this
short sequence; correct answers advance persisted steps. Requests must supply
the current question and a listed answer, preventing out-of-order completion.

Eligibility begins after the relic report (stage7) and remains after the main
claim (stage8). Ordinary eligible players must stand near Louis on map1000.
Completing the sequence grants half of the current EXP Ball value (capped at
the server level limit) through the existing progression transaction. Completion
step3 and experience commit atomically under owner/progression locks; retries
cannot restart a paid quiz. This optional reward never blocks the main claim.
NPC options remain part of the pending ordinary-route integration.

Build and 571 checks passed (previous 546 + 25 quiz checks), across all three
rebirth states including after the final claim. Coverage includes proximity,
prerequisites, skipped/invalid/repeated answers, reconnect/reset, injected receipt
failure with experience rollback, concurrent completion and durable paid state.
Logs: `obj/treasure-quiz-build.log`, `obj/treasure-quiz-tests.log`.
Next: ordinary NPC/item/journal, accepted movement and automatic combat lifecycle
integration; these store checks do not establish a playable client route.

## Ordinary earning route integration

`DesertTreasureQuest` now initializes the persistent services and Louis/Shark
at their audited positions/models. Unexpected existing NPC IDs/positions/models
fail closed. Chang options170-172 enter the quest; ordinary NPC dispatch covers
referrals, bargaining, lost-item recovery, quiz and claims. Item dispatch handles
vial, box, portrait inspection/reveal and bottle capture. Both journal mutation
paths reject client-authored progress. Seven mission rows reflect saved stages.

Revora adapts the original ground-compartment click to a visible ScriptureVault
stone (model383) at (94,283), beside the original target (91,283). The static
shared-terrain audit now reserves its footprint. A production accepted walk/jump
arrival at the target sets the step; teleport/login cannot do so. No unverified
ground-effect packet is enabled or claimed. The private client overlay must
explain this interaction. Native appearance/click acceptance is still pending.

Actual death and revival hooks deliver earned materials and reset per-life
corpse guards. A bounded three-second scan starts owned Jinx encounters near
the region for eligible stage6 participants. Login refreshes saved journal state
and cleans replaced-session encounters. Tick handles corpse/lifetime cleanup.
Normal gameplay has no GM, owner-name or character allowlist requirement.

Build and 495 ordinary-route checks passed (406 shared + 89 route checks), across
all three rebirth states. The route uses real NPC dispatch, production item use,
combat deaths, accepted jump, automatic Jinx spawn/capture and persisted final
rewards. It checks remote/underlevel denial, forged journal writes, ordinary lost
vial recovery, teleport bypass rejection, one-time purchase, timed portrait,
6,500 total silver less the agreed 99,999 purchase price, optional quiz after the
main claim and reconnect. Monster fixtures are registered combat bodies; real
configured population/respawn acceptance remains a separate check. The prior
571-check store suite covers other failure cases; totals overlap and must not
be added. Logs: `obj/treasure-quest-build.log`, `obj/treasure-quest-tests.log`.

An initial compile resolved the project-local BitConverter name instead of
System.BitConverter; the hash check now explicitly uses System.BitConverter.
Next: real population checks and composed private journal/item overlay. Nothing
installed, deployed, signed or published; actual Host/native acceptance remains
pending and aggregate achievement10931 is not established by these tests.

## Private client overlay and configured population

`tools/ClientPatches/prepare_desert_treasure_journal.py` stages all seven journal
entries over the private Family Feud overlay, correcting level98 eligibility,
NPC labels, vault interaction, bid prices/probabilities, corpse timing and
separate EXP Ball reward. Five item tooltips match the implemented behavior.
Unrelated journal sections are byte-preserved; rewrites are idempotent and the
encrypted item catalog passes a round trip. Installed client/DX9/source hashes
are verified. Output: `obj/chi-refill-validation/desert-treasure-journal/`.
Nothing installed, signed or published.

SHA-256 outputs:

- Questinfo: `ecffa24751eaaa6b13e6f756c1738e7dc7e9880ea474be0983b3942dbcc7e8fc`
- itemtype.txt: `59c431acd932266d53f8a318768b6339be01849e573b145f211a0e21040bee51`
- itemtype.dat: `3c3d3a5f597990a3121121ad63ea15f552aa11165a5dfa2f1624f5ddb02d0c3d`

The real-loader population test initially failed its combined count/placement
assertion. A diagnostic repeat showed 704/704 loaded, none dead or out of bounds,
and passed kill credit/respawn for all five families. Inspection identified the
loader's fixed 50 random placement attempts, which can miss sparse walkable
terrain (one TombBat rectangle is only about13% walkable). The first failure did
not log individual counts, so its exact missing body cannot be reconstructed.
A scoped deterministic fallback now scans the existing configured rectangle
when random placement fails, limited to the five quest families on map1001.
It neither increases configured counts nor moves spawn definitions. Occupied or
fully blocked rectangles remain unfilled rather than placing invalid monsters.

The final build and population suite passed 424 checks (406 shared + 18
population checks). It validates deterministic fallback/occupied-cell refusal,
704 actual loaded bodies inside configured bounds, production combat credit
from each family, normal respawn and fresh essence credit after revival.
Public spawn rows are read-only inputs copied into the explicitly guarded
`revora_potency_validation` schema; fixture rows are restored afterward.
Logs: `obj/treasure-population-build.log`, `obj/treasure-population-tests.log`,
`obj/treasure-journal-stage.log`. These are not native/client or Host acceptance.

Next independent root: Desert Expedition (1835). Desert Treasure's native
rendering/clicks, actual Host acceptance, signing, installation and deployment
remain pending. No aggregate achievement completion is claimed.


### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001, life2, earns the key through
  calculated combat, reveals the vault, triggers its movement condition through
  the jump handler, opens the box and follows the scripture/portrait route.
- [x] Portrait purchase and repeated purchase dialogue leave the exact expected
  silver balance. Calculated material hunts and the owned JinxTombBat encounter
  lead to item-use capture and a tracked relic at stage6.
- [x] A separate process reloads the relic and completes report/claim. The net
  silver change is -93,499 after costs and rewards; final items are bound
  728249, 723911 and one P7 soul. The reusable vial is not a final reward.
- [x] A third process preserves exact saved career, vault, purchase, capture
  and reward state. Repeating the final claim leaves the snapshot unchanged.

Evidence: `career-retained-treasure-hunt`, `career-retained-treasure-claim`, and
`career-retained-treasure-reload` all passed on private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
Clock, drop/vault rolls and approach positions are controlled; attacks use
calculated damage and public map transitions use portals. Natural odds, native
rendering/navigation and deployment remain unproven. Retained aggregate14/19;
achievement10931 remains incomplete with five chains outstanding.
