# Defense Talisman / Star Tower — implementation contract

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

Status: ordinary route and interrupted visits tested; composed private client
patch staged. Native/Host acceptance and deployment remain pending.

## Evidence and conflicts

The installed 6711 journal confirms missions520–528, Type4, minimum100,
maximum120 and no predecessor for520. Each later mission names the preceding
stage. Reward202008 is the bound Elite Star Tower;721785 is a separate quest
material, never the equippable reward. Seventeen relevant catalog identities
and four weapon-to-clinker pairs are recorded in
[the hash-gated contract](research/DEFENSE_TALISMAN_CONTRACT.json).

The [official PC guide](https://co.99.com/guide/quests/startower.shtml), reviewed
2026-09-26, corroborates the tower reconstruction route. It places net use at
Bird Island310,380 and Bloody Shawn at400,650, describes a ten-minute Shawn
schedule and warns that late-stage failure requires restarting from the second
Treasure House visit. It does not establish net price, precise drop odds,
monster statistics or modern persistence behavior.

The installed net/fish tooltips retain older coordinates170,190 and192,212.
The journal places Ye Sheng at324,400. Its Shawn prose disagrees with its own
400,650 item link, and TimeDoor appears at340,404 in one stage but220,220 in
the next. Use the PC guide/journal agreement as the route candidate; validate
terrain and NPC footprints before fixing client directions. Do not substitute
Facebook/iPad coordinates or treat these conflicts as verified runtime travel.

Source search found no existing quest handlers for the tower materials or
Sugar Tang/Yang Yun IDs in Npcs.cs or Game/Features. Existing talisman purchases
and equipment achievements do not implement this chain.

## Implementation sequence

- [x] Extract all nine missions, seventeen item identities and contradictions
  from exact-hash installed catalogs with tools/audit_defense_talisman.py.
- [x] Verify extraction rejects a missing stage, duplicated stage, changed item
  identity and missing item; original contract succeeds.
- [x] Inspect runtime NPC/monster catalogs and maps1782–1787; validate terrain,
  scene entrances/exits, doors and both outdoor hunting sites.
- [x] Implement durable admission/referrals and material ownership. Use ordinary
  authenticated players level100+, without a maximum-level exclusion. Freeze
  one bound P7 bonus per character's one-time claim, alongside the original
  bound Elite tower, following the owner's valuable one-time reward policy.
  Treat these as documented Revora rules, not original eligibility/rewards.
- [x] Implement the first Treasure House encounter, earned fishing, three-fish
  exchange, door puzzle, Hut visit and earned Shawn spear/splinter exchange.
- [x] Implement desert encounters, splinter theft, handkerchief referral,
  second Treasure House and earned invocation. Separate progress from journal
  display; prohibit client-forged stage completion and transferred materials.
- [x] Implement owned dungeon encounters on all three floors, four earned
  weapons, their exact clinker conversions, Yang Feng and three splinters.
  Cauldron reconstruction must consume all required tracked materials atomically.
- [x] Implement replay-safe final hand-in, immediate inventory updates, persisted
  completion and recovery. Define explicit durable checkpoint/re-entry rules;
  no recovery may duplicate a stored, traded, dropped or already consumed item.
- [x] Validate the ordinary NPC/item/combat route without supplied progression
  across all rebirth states, including final rewards after login.
- [x] Verify replacement-session and expired-visit handling through the ordinary
  route. Earlier focused fixtures cover bag capacity, failed writes and replay.
  Real Host concurrency/load acceptance remains pending.
- [x] Compose private corrected journal/tooltips with both pending map repairs.
- [ ] Native rendering, actual Host acceptance, signing, installation and release.

Open design details are bounded encounter timing, net pricing, door order and
checkpoint reset behavior. Resolve using runtime evidence and documented Revora
rules before implementation. Do not award a tower from fixture-only progression
or infer achievement IDs from item identity. Attack Talisman is a separate chain
and its admission relationship must be verified before integration.


## Runtime prerequisites found

Read-only catalog evidence is captured in
[the runtime inventory](research/DEFENSE_TALISMAN_RUNTIME.json). Ten NPC rows
exist in4565?4579, but Sugar Tang4565, exterior Yang Yun4566, Plum4572 and
Ghoul Kong4573 are absent. Ye Sheng4569, Net Peddler4579 and doors4575/4576
use older coordinates; DreamDoor4577 is on map5000 instead of Bird Island.
These rows require explicit corrections during source initialization. No
required quest monster was established by the bounded name search; ViciousBat
is an unrelated substring match and must not be silently used as Vicious Rat.

The [scene audit](research/DEFENSE_TALISMAN_TERRAIN.json) confirms both installed
GameMap indexes lack1782,1783,1784 and their server floor files are absent.
Treasure House, its second encounter scene and the Hut therefore need a private
map-binding/floor prerequisite or documented Revora scene adaptation. Do not
allow an NPC to teleport players into these absent maps.

Maps1785/1786 are164x164;1787 is108x108. All65,456 blocked/walkable cells agree
between their native DMaps and server floors. All six journal targets are
walkable. This does not prove routes between them, portal placement, spawn
footprints or client rendering. Hashes record the exact examined inputs.

Next: select and validate scene assets for the three absent maps, then implement
owned encounters against that explicit profile. Existing dungeon floors need
connectivity/footprint checks before admission. No public catalog, map index,
server floor, client or Host was modified by these read-only probes.


## Private scene adaptation

`tools/stage_defense_talisman_scenes.py` now stages1782?1784 as independent
aliases of the installed house03 interior (existing map1099). This is an explicit
Revora presentation adaptation, not evidence of the original Treasure House or
Hut appearance. The smaller house01 cannot contain the required coordinates;
Nhouse04 blocks two required scene targets. The reviewed house03 supports every
required NPC center without changing its terrain.

The tool binds the exact DX9 executable, both installed map indexes, house03
archive/native DMap and server1099 floor by SHA256. All8,464 native/server cells
agree. It preserves existing index records and adds only the three missing IDs.
Each scene uses entrance45,60. Static four-direction routes reach every listed
NPC approach and five proposed encounter cells after applying the relevant3x3
NPC footprints. The native inherited portal at53,86/index0 is recorded and must
receive explicit owned-scene exit handling before admission is enabled.

Private outputs: `obj/chi-refill-validation/defense-talisman-scenes/`.
Twenty checks passed: five output hashes, five unchanged input hashes, two
repeat-output comparisons, duplicate/truncated-index rejection for both indexes,
three scene route checks and blocked-entrance rejection. No client assets or
stage outputs are tracked in Git. The tool publishes a new staged directory
only after input/readback verification; it refuses installed destinations.

- Client index:12263b5432399b657378c0af2d1504ac85a578e6be5c9dabd439823dc6296142
- Server index:7fa425051b4ba4b4a68849915bf052cee05a89aa5fdf0dfbb1c57ad8ded32e78
- Each floor:9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4

These indexes are a private branch from installed inputs. They must be composed
with other pending map repairs at release preparation, not installed over them.
Native rendering, owned lifecycle, actual Host acceptance, signing and deployment
remain pending. Installed maps1782?1784 are still absent; the earlier inventory
continues to describe installed state correctly.


## Saved admission and fishing transaction foundation

DefenseTalismanStore now saves a single run per character UID, with a frozen P7
choice. Rebirth does not reset eligibility or reroll that choice. Admission is
level100+ for ordinary authenticated, fully loaded characters with matching
session/inventory ownership; trade/booth/poker activity is excluded. No NPC,
item-use, initialization or combat hook enables this foundation publicly yet.

Stage1 means the first Treasure House encounter is pending; stage3 is fishing.
The encounter/referral transitions between them are not implemented in this
batch. At stage3, a bound net costs30,000 silver as an explicit Revora economy
choice, not a verified original price. Item insertion, payment and receipt commit
together. One active entitlement prevents repeated charges even when its item is
stored or lost. Three tracked, usable Fresh Fish exchange atomically for one
bound Iron Poise. Untracked or other-owner items do not qualify. Lost stage3
materials recover only when the old UID is absent globally; the old entitlement
is retired and a replacement receipt is persisted.

Build and438 checks pass (406 shared +32 foundation checks), across admission
at each rebirth state, including level140. Tests cover frozen choice/reload,
rebirth replay prevention, concurrent purchase, full bag, changed context,
stored/lost net, untracked fish rejection, failed poise insertion rollback and
saved payment/poise. These are ledger tests: stage3 and fish entitlements are
explicit fixtures, not proof of completed encounters or ordinary fishing.

Run with Verify-PotencyHunt.ps1 -TowerStoreOnly; this batch used the existing
private harness and validation binary. Logs: obj/tower-store-build.log and
obj/tower-store-tests.log. Four new tables were initialized only in the disposable
validation schema. No live schema, server restart or deployment occurred.

Next: implement first-house completion and referrals, then owned trout earning
with net consumption and scene cleanup. Do not mark source earning complete
until the fixture stages/materials are replaced by actual NPC/item/combat tests.


## Owned trout combat and earned fish

DefenseTalismanEncounters now owns a maximum of one trout per current player
session and32 globally. Stage3 plus an owned usable net is required, within eight
cells of1015(310,380). Three candidate positions312/314/316,380 pass shared native
and server walking checks with the planned fishing-NPC footprints. NPC approach
routes are not included in that claim; the earlier319,371 candidate was blocked.
[Terrain evidence](research/DEFENSE_TALISMAN_FISHING_TERRAIN.json).

Appearance uses installed FishB3579 mesh311, verified in the loaded runtime
catalog. Level100,18,000HP,600?900 attack,250 defense and a five-minute lifetime
are explicit Revora tuning; original Trout Elf stats/model are not claimed.
Attack/damage/death/AI hooks enforce current ownership, ordinary companion
ownership, range and expiry. Successful combat atomically consumes the paid net,
tracks one bound fish and records an attempt/net-unique receipt. Failed or expired
hunts preserve the net. Three active fish entitlements or a poise prevent another
summon/purchase. Cleanup removes corpses, timed-out and replaced-session actors.

Build and462 checks pass (438 prior +24 combat checks). Actual Handle.ReceiveAttack
kills all three trout and the earned fish exchange into a persisted poise, with
three net payments/receipts. A full bag works because consuming the net frees the
fish slot. Other-player theft, remote summons, repeated death, timeout and session
replacement are covered. Stage3 alone is seeded; no net, fish, poise or combat
receipt is supplied. First-house/referral progression is still unimplemented.

Logs: obj/tower-fishing-build.log and obj/tower-fishing-tests.log. The same
-TowerStoreOnly selector now includes combat coverage. Public NPC/item entry and
runtime scheduling remain disabled; no production schema or Host deployment.
Next: owned first-house admission/clearance/referral and scene portal handling.


## First Treasure House earning and travel

DefenseTalismanScenes/Travel now allocate a private1782 scene per eligible owner,
maximum8 scenes, with a ten-minute visit. The staged floor is exact-hash checked.
The dynamic map loads no catalog actors or portals; the inherited house03 portal
has no destination route. All teleport overloads deny unrelated entry and scene
shortcuts; controlled admission saves its visit before saving arrival. Ordinary
NPC entry and client portal handling are still not wired publicly.

Five sequential TowerBandits must be killed through guarded combat before the
house report advances1?2 and grants100 silver. Five is an explicit Revora count;
the installed BanditL97 mesh201 is reused with level100,18,000HP,600?900 attack
and250 defense. Each kill is bound to the owner, visit, body and unique attempt.
Repeated death cannot count twice. The report checks all five saved kills and
commits stage/payment atomically. Exit retires the visit and scene. The exterior
referral advances2?3 with another100 silver exactly once, unlocking fishing.
Incomplete visits time out without progress; a new visit starts its own kill count.

Build and489 checks pass (462 prior +27 house/travel checks). Tests accept an
ordinary player, allocate the private staged floor, enter, kill five actual
bandits, report, exit, take the exterior referral and reload stage3/payment.
No house kills or referral stage are supplied. Outsider admission/damage, early
report, repeat report/referral, timeout and replacement-session cleanup are covered.
The production entity loader can already return the reconnecting character to
Twin City; quest recovery preserves an already safe outside location, retires
the old receipt and releases the scene instead of forcing a second teleport.

The fixture changes only its process working directory while constructing the
scene from the private staging tree; installed1782.map remains absent. Logs:
obj/tower-house-build.log and obj/tower-house-tests.log. Store tables, visit/kill
tables and all saved test characters remain confined to the disposable schema.

Remaining: NPC presentation/interaction guards and periodic/login wiring, fishing
item use, door puzzle/Hut/spear, desert/second house/dungeon, reconstruction and
final payout. This is tested source machinery, not a deployed player-facing quest.


## Three doors and owned Hut admission

The stored route now requires Time?Season?Dream, an explicit Revora puzzle order
because original selection/ordering rules remain unverified. Door centers are
1015(340,404),(354,402),(362,397). Shared native/server terrain connects the
verified approaches342,404;352,402;360,397 around all three3x3 NPC footprints.
The earlier338,404 approach is blocked and is not used.
[Door terrain evidence](research/DEFENSE_TALISMAN_DOOR_TERRAIN.json).

Each click requires the current eligible owner, matching stage/step, a usable
tracked Iron Poise and distance at most3. Partial progress survives reconnect.
The third door consumes the poise, saves step3/stage4 and grants100 silver in one
transaction. Wrong order, distance, material ownership, repeated clicks or failed
writes cannot advance or duplicate payment. The poise remains available until
that final transaction commits.

CreateHut uses the privately staged1784 alias and the same bounded owned-scene
lifecycle. Admission requires saved stage4 plus all three completed doors. A
stage3 player cannot enter, and unrelated players cannot teleport into the Hut.
No first-house bandits spawn there; exit retires the visit and releases the map
while preserving earned progress. NPC presentation and spear hand-in are next.

Build and503 checks pass (489 prior +14 door/Hut checks). The route consumes the
poise obtained from three actual trout kills, without seeding poise or door
progress. Tests cover partial reload, remote/invalid/order rejection, changed
context, database failure after planned consumption, concurrent final clicks,
repeat rejection and owned Hut admission/exit. Fishing referral remains seeded
in this focused fishing/door test; the separate first-house test earns that
referral through combat. A single ordinary NPC/item route is still pending.
Logs: obj/tower-doors-build.log and obj/tower-doors-tests.log.

No live table initialization, map installation, NPC activation, restart or release.


## Ruan request and earned Shawn spear

An owned Hut visit now records Ruan's request only within six cells of55,55.
The request persists across reconnects. Before it, stage4 cannot summon Shawn.
After it, the owner can encounter BloodyShawn near1015(400,650); candidate cells
402/404/406,650 match shared native/server walking terrain.
[Shawn terrain evidence](research/DEFENSE_TALISMAN_SHAWN_TERRAIN.json).

Revora adapts the guide's ten-minute cadence to owned admission during UTC
minutes00/10/20/30/40/50. An admitted encounter lives for up to five minutes, so
it does not vanish when that admission minute ends. Other players' ambient
monsters do not hide this owned encounter. This isolation and admission-window
interpretation are Revora rules. Appearance uses the installed BanditL97 mesh201;
level105,50,000HP,800?1,200 attack and350 defense are explicit custom tuning.
Original Shawn model/statistics and ambient concealment behavior are not claimed.

Only a registered owner/companion kill can award the bound tracked721781 spear.
A unique character kill receipt and material entitlement prevent repeat credit.
A full bag commits no receipt or reward, allowing another attempt. Stored spears
cannot be copied; a genuinely absent earned UID can be recovered once into a
replacement entitlement. Ruan consumes that spear, grants one bound721777
splinter, pays100 silver and advances4?5 atomically, then retires the Hut visit.
A failed item insertion preserves spear, payment and stage. Consumed spears cannot
be recovered after the report.

Build and518 checks pass (503 prior +15 request/Shawn/report checks). The existing
combat-earned fishing/doors route now requests Ruan, exercises schedule/full-bag/
outsider denial, kills Shawn, recovers an earned lost spear, exchanges it and
reloads stage5/splinter/payment. No spear, request or completion receipt is seeded.
The first fishing referral remains a focused-test fixture; the separate house
suite earns it normally. Logs: obj/tower-spear-build.log and obj/tower-spear-tests.log.

Next: Plum's referral, desert encounters and stolen-splinter/handkerchief route.
Public NPC/item/scheduling wiring, later dungeon/final rewards and native/Host
acceptance remain pending. No live schema, client install or deployment changed.


## Plum, desert investigation and handkerchief report

Plum's stage5?6 referral requires a usable owned splinter and proximity to
1000(472,578), retains the splinter and pays100 silver once. The correct shared
terrain approach is474,578;470,578 is blocked. A static route connects it to
166,226 and all three candidate encounter cells168/170/172,226.
[Desert terrain](research/DEFENSE_TALISMAN_DESERT_TERRAIN.json).

The owner fights DesertCondor then CapriceLeader, one of each, with separate
saved attempt/body credits. The fixed sequence/count is Revora's interpretation
of the investigation. Condor uses the verified Birdman18 mesh205; Caprice uses
BanditL97 mesh201. Level105,35,000/60,000HP,800?1,200/1,200?1,600 attack and
350/500 defense are custom tuning, not verified original monster statistics.
Public-map identity and exact terrain hashes prevent spawning these fights in
the fishing map or a foreign dynamic scene.

The first kill saves progress only. The second atomically consumes the tracked
splinter, awards bound BudHandkerchief721782 and completes the two-kill ledger.
This works in a full bag because the consumed material frees its replacement
slot. The stolen splinter cannot subsequently recover. Yang Yun at1015(732,516)
consumes the tracked handkerchief, advances6?7 and pays100 silver once. Failed
stage/payment writes preserve the handkerchief and progress. Stage7 is the
pending second Treasure House encounter, not completed dungeon access.

Yang Yun's western approaches730,516 and728,516 are blocked in both profiles;
734,516 and732,518 connect around his proposed NPC footprint. Tests now use734,516.
[Yang terrain](research/DEFENSE_TALISMAN_YANG_TERRAIN.json).

Build and533 checks pass (518 prior +15 desert checks), including remote/wrong-map
and outsider denial, actual Condor/Caprice kills, reload between kills, full-bag
replacement, repeated death/receipt rejection, obsolete recovery denial, report
rollback and saved stage7/payment. No desert kill, splinter, handkerchief or report
stage is supplied in this extension of the combat-earned route. The initial
fishing referral remains a fixture, with its separate house earning test intact.
Logs: obj/tower-desert-build.log and obj/tower-desert-tests.log.

Next: second house ambush and earned invocation, Ghoul Kong/dungeon progression,
reconstruction/final rewards and public ordinary NPC/item/lifecycle wiring.
Nothing initialized in production, installed, restarted or deployed.


## Second house ambush and Weird Invocation

CreateAmbush uses the private1783 alias and admits only a saved stage7 owner.
It shares the bounded scene lifecycle, exact floor hash, private teleport guards,
visit receipts and retirement/recovery with the first house. Five sequential
ViciousRats must be killed before the invocation can be collected within three
cells of52,50. Their appearance reuses loaded Ratling8 mesh202; five opponents,
level105,25,000HP,800?1,200 attack and350 defense are explicit Revora tuning.
The original Vicious Rat model/statistics and population are not claimed.

Kill credit is per current visit/body/attempt. Claims check the five saved deaths,
current owned scene, location and free inventory slot. Bound WeirdInvocation721786,
100 silver and stage7?8 commit together; only then is the scene retired. A full
bag or failed item write leaves the earned claim available during that visit.
Stage8 permits recovery only for a tracked invocation whose old UID is absent
globally; stored or existing replacements cannot duplicate. Recovery preserves
Revora's durable checkpoint rather than the original guide's late-stage reset.

Build and558 checks pass (533 prior +25 ambush checks): earned stage7 admission,
actual five rat deaths, outsider and repeated-death rejection, early/remote claim,
full-bag retry, database failure rollback, saved invocation/payment, scene cleanup
and stored/lost item recovery. No ambush kills, invocation or stage8 is supplied.
Logs: obj/tower-ambush-build.log and obj/tower-ambush-tests.log. The existing focused
route still supplies the initial fishing referral; the house suite separately
proves how it is earned. Ordinary public NPC/item wiring remains pending.

Next: Ghoul Kong admission, three dungeon floors and earned weapon/clinker/splinter
reconstruction, final reward and full ordinary route. No production schema,
installed client/map, live process or release changed.


## Ghoul Kong and owned dungeon admission

PrepareDungeon requires stage8, a usable tracked invocation and proximity to
Ghoul Kong1000(85,313). Approach87,313 is verified against both terrain profiles.
The saved dungeon floor becomes1 once; repeated preparation preserves it and
charges nothing. Invocation remains the owned entrance key until reconstruction.
[Ghoul approach](research/DEFENSE_TALISMAN_GHOUL_TERRAIN.json).

CreateDungeon selects only the saved entrance floor. Scene admission rechecks
that floor, stage and actual owned invocation inside the visit transaction. A
stored key cannot enter; no caller-supplied floor can bypass this gate. Dungeon
scenes use exact reviewed native/server floors1785?1787, load no catalog monsters
or portals and retain owner-only teleport/visit recovery. Entry points are
96,124;113,126;56,68 respectively. Only floor1 can currently be unlocked by play;
floor advancement and combat objectives are the next implementation step.

`tools/audit_defense_talisman_dungeon.py` checks exact reviewed terrain hashes,
full native/server blocked-cell agreement and eight walking routes to journal
objectives/proposed transition targets. The third-floor Cauldron3x3 footprint is
included. These are spatial candidates, not implemented inter-floor transfers.
[Dungeon routes](research/DEFENSE_TALISMAN_DUNGEON_ROUTES.json).

Build and567 checks pass (558 prior +9 admission checks). The combat-earned
invocation unlocks Ghoul's gate, survives changed-context rollback, reload and
repeat preparation, admits its owner at the exact first-floor entrance and
survives exit without advancing floors. Missing unlock, remote preparation,
stored key and outsider admission are rejected. No dungeon floor is supplied
by the test. Logs: obj/tower-dungeon-entry-build.log and
obj/tower-dungeon-entry-tests.log. All three spatial profiles pass the separate
read-only terrain audit.

Next: four earned general weapons, guarded floor transitions, final-floor
encounters and reconstruction. Public NPC/item lifecycle wiring and native/Host
acceptance remain pending. No production initialization, install or deployment.


## Four generals and saved floor progression

The owned encounter route now awards GlitterSword, AnnattoBlade, WaspSpear and
InfernalAxe from four actual general deaths on floors1785 and1786. Durable
character/slot receipts prevent repeated death rewards. Each weapon is bound
and tracked; only a globally missing tracked item can be recovered. Full bags
leave no kill receipt, allowing a fresh encounter after freeing space.

Models and combat tuning are explicit Revora choices: catalog Skeleton100,
mesh163, level110,60,000HP,1,000-1,500 attack and500 defense. The journal's general
order and coordinates remain: General2 at96,120; General3 at70,60; General1 at
113,122; General4 at62,81. These are not verified original monster statistics.

The first gate at70,56 checks both first-floor weapons; the second at62,77 checks
all four. Both require the tracked invocation, owned scene and proximity. Saved
floor progression commits before retiring the old visit. Travel uses the existing
safe-return hop before allocating/admitting the next owned floor; a failed new
admission retains the earned checkpoint for re-entry. Native transition rendering
has not been accepted. The third-floor entrance is56,68.

Build and591 checks pass (567 prior +24 general/progression checks), including
outsider damage rejection, actual combat rewards, repeated death, full-bag retry,
locked material rejection, remote/early gates, database-failure rollback, old-map
cleanup, exact next-floor entrances and saved weapons/floor after reload.
Logs: obj/tower-generals-build.log and obj/tower-generals-tests.log.

Next: final-floor encounters, splinters/clinkers and reconstruction, final reward,
then ordinary NPC/item/journal/lifecycle wiring and composed client staging.
No production initialization, install, deployment or native acceptance occurred.


## Yang Feng, Cauldron and final hand-in

The final owned floor now summons YangFeng at56,64 only when the owner has all
four usable tracked weapons and the invocation. One actual owned kill awards
three separately tracked bound TowerSplinters. A durable character/attempt
receipt prevents replay; fewer than three free slots leaves the reward unclaimed
and permits a fresh encounter. Outsiders cannot damage this encounter. The
appearance is an explicit Revora reuse of verified Skeleton100 mesh163, with
level110,100,000HP,1,000-1,500 attack and500 defense; no original model/statistics
are claimed. The previously audited56,64 target is reachable from56,68.

After that kill, Cauldron interactions within six cells of48,37 convert each
weapon into its exact catalog clinker. Each exchange consumes the tracked input
and inserts its bound replacement atomically, including in a full bag. Lost
clinkers recover only if their old UID is globally absent; consumed weapons
cannot recover. Reconstruction consumes three splinters, four clinkers and the
invocation in one transaction, awards quest StarTower721785 and saves stage9.
The owned scene then becomes complete and uses the existing safe exit. This
retains Revora's documented durable checkpoints instead of the original reset.

At Sugar Tang1015(685,599), the final transaction consumes that quest item,
inserts bound Elite StarTower202008 and the bound P7 selected at acceptance,
and saves stage10 with both reward UIDs. One additional free bag slot is needed.
Replay, reacceptance and recovery after completion cannot reissue rewards.
Lost quest StarTower recovery is available before the hand-in. This remains a
one-time quest reward, not a daily P7 source.

Build and615 checks pass (591 prior +24 final-path checks). Tests use actual
combat-earned weapons/splinters and conversions, not seeded final materials.
They cover outsider denial, replay, capacity, exact conversions, failed item and
stage writes, input rollback, material recovery, immediate inventory changes,
frozen bound reward IDs and persisted completion. The initial fishing referral
remains supplied in this focused fixture; its separate house suite proves that
referral's earning. This is not yet a full public NPC/client acceptance claim.
Logs: obj/tower-final-build.log and obj/tower-final-tests.log.

Next: ordinary NPC/item/journal/lifecycle integration and an end-to-end route
from acceptance without supplied progression, followed by composed private
client staging. Production initialization, native/Host acceptance, installation
and deployment remain pending.


## Ordinary interaction route

DefenseTalismanQuest now connects normal NPC requests, item use, owned encounter
ticks, login recovery and journal display. No owner name, GM status or account
allowlist is required. Forged quest-status requests cannot grant progress; IDs
520-528 are protected even when this quest is not activated.

Initialization deliberately leaves the feature disabled if staged house floors
1782-1784 are absent. With those files present, all six private floor hashes and
both outdoor floor hashes are checked before enabling. The three private aliases
remain a deployment prerequisite; nothing installs them automatically. Startup
initializes quest/visit tables only when that prerequisite is present, then
recovers interrupted visits. This batch did not initialize production tables.

The runtime places Sugar Tang, Yang Yun, Plum and Ghoul Kong at the reviewed
public coordinates. Ye Sheng, Net Peddler and the three doors move from the exact
recorded legacy placements in memory; a changed unexpected placement/model is
rejected. No production NPC rows are rewritten. Owned scenes receive their own
Yang Yun, Ruan Brother, Invocation Stone or Cauldron. Missing original NPC
appearances use explicit Revora choices from the reviewed existing quest models:
Sugar Tang/Yang Yun3654, Plum3676, Ghoul Kong3694 and Invocation Stone3700.
Original artwork for these missing NPCs is not claimed.

Nets start fishing through item use. Outdoor Shawn/desert and owned house/dungeon
encounters appear when the accepted owner approaches. The invocation advances
at gates70,56 and62,77; elsewhere inside a dungeon it returns the owner safely,
retaining the saved floor. Scene NPCs also offer an exit. The dialogue explains
this Revora adaptation; automatic original glowing-spot transitions are not
claimed. Completed house/reconstruction reports return their owner immediately.

Build and459 route checks pass (406 shared +53 focused): underlevel and remote
admission, disabled activation with missing installed scenes, forged journal
requests, every encounter/exchange through actual handlers, all nine journal
entries, both dungeon transitions and bound final rewards after login. Three
ordinary characters complete the entire chain at zero, one and two rebirths;
no quest stage, kill receipt or material is supplied. Movement between targets
is fixture positioning, not native-client pathfinding acceptance. The separate
615-check store suite covers rollback/recovery; totals overlap and are not summed.
Logs: obj/tower-route-build.log and obj/tower-route-tests.log.
Reproduce with tests/PotencyHunt/Verify-PotencyHunt.ps1 -TowerRouteOnly after private
scene staging. The fixture copies only required maps into an ignored test folder,
uses the disposable validation schema and never modifies installed floors.

Next: compose corrected client journal/tooltips with the pending scene overlay,
check interrupted sessions through the ordinary route, and retain native/Host,
installation and deployment gates.


## Interrupted visits and composed private client package

The ordinary route suite now passes466 checks (406 shared +60 focused). It
reconnects during a partially cleared first house, rejects the replaced session's
report and requires fresh kills after reentry. It expires a partially cleared
rat ambush and verifies safe return without an invocation or reused visit credit.
It reconnects after earning the first second-floor weapon, retires the old scene,
and resumes through Ghoul Kong on the saved floor with that weapon retained.
All three characters then finish the complete quest and reload their rewards.
Log: obj/tower-route-recovery-tests.log. No production process was restarted.

`tools/ClientPatches/prepare_defense_talisman.py` stages nine corrected journal
missions over the Desert Expedition bundle, preserving all other quest sections.
Five tooltips correct fishing/Ye Sheng coordinates, door order, invocation travel
and the distinction between quest tower and equippable reward. Eligibility is
level100+; text states the once-per-character bound rewards and durable checkpoints.
The package composes both pending GameMap changes: Gloomy Jail1363 and Tower
aliases1782-1784. Earlier installed map records retain their exact order and
values. The four corresponding server floors are included from hash-verified
private inputs; existing client archives supply the native scenes.

Private output: `obj/chi-refill-validation/defense-talisman-composed/`.
Its manifest records every input/output hash and explicitly remains unsigned,
uninstalled and unpublished. Creation uses a new temporary directory followed
by a rename; reruns accept only identical complete output. Installed input
hashes are checked again after staging. No proprietary content is committed.

`python tests/test_defense_talisman_patch.py` passes10 tests covering unrelated
journal/catalog preservation, missing/duplicate missions, missing required fields,
changed tooltips, eligibility/reward wording, both map repairs, duplicate-map
rejection, idempotence and complete bundle/input readback. Logs:
obj/tower-composed-tests.log and obj/tower-composed-stage.log.

Staged outputs:
- `client/ini/Questinfo.ini`: `490b473662549c79f3b650f70c87b229a1fbebd0b876eeb755f769ab9502fb8a`
- `client/ini/itemtype.txt`: `215d2f071ffd7caefbfd13331e5f2c99952953dbf74ce373d9ca807c8c28c5a1`
- `client/ini/itemtype.dat`: `b034bafd53b51db365476c1e66073d17a6a3e61e1949d7f53b550f73cbd95b55`
- `client/ini/GameMap.dat`: `48f46669af68189f30359ebd56d6db6665e608e4941d0a006b4495ebdcfd5a15`
- `server/database/GameMap.dat`: `ae0a92063c4f82ca1face0386cc288b2f89d78b04b635f29ce745f5fafe743fd`
- `server/database/maps/1782.map`: `9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4`
- `server/database/maps/1783.map`: `9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4`
- `server/database/maps/1784.map`: `9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4`
- `server/database/maps/1363.map`: `a9a555cf6b1b407f75b5ce19aaaec2109d3d1397cf8b0675b67acfdf7b5a54dd`

Native client navigation/rendering, live Host load/concurrency, signing and
transactional installation remain unaccepted. The installed server still lacks
the house floors, so the ordinary feature remains disabled there. No live
schema initialization, client installation or release occurred.

## Actual startup, earned route and corrected Net Peddler (2026-09-27)

- [x] Actual startup with reviewed house floors found NPC900180 already occupying
  Net Peddler's proposed321,371 tile. Preserve that legacy protector and relocate
  Net Peddler to325,371. Its nine footprint cells are clear on the reviewed1015
  floor hash. Terrain checks remain enabled. Both Tower and Fan now initialize.
- [x] Enter Sugar Tang's house through normal NPC dispatch, defeat one bandit and
  interrupt the game. Restart game/database and invoke normal login recovery.
  The incomplete visit returns safely and cannot grant the house report; a fresh
  visit requires all five bandits again, as designed.
- [x] Complete the ordinary route: house/report, three paid nets and caught fish,
  Ye Sheng, three doors, timed Shawn, Ruan, Condor/Caprice, second house, invocation,
  four generals, Yang Feng, Cauldron and final Sugar Tang claim. No quest progress,
  materials or rewards are seeded. Three net receipts each record30,000 silver.
- [x] Third process reload and repeated claim preserve stage10, the frozen P7
  choice and exact UIDs of one bound Elite Star Tower202008 and one bound P7 soul.
- [ ] Authenticated/native travel, natural combat balance, Fan's full actual-process
  earning route, signing and live installation/deployment remain pending.

Run `tools/Verify-ArenaProcess.ps1 -TowerOnly`. All three launches pass in
`obj/tower-process-run.log`; executable SHA256
`7D86AB5305F15DFE9C77E9849B27A166597C3D497B71A4BF1E49CB43A10F8055`.
The private helper supplies eligibility, proximity, lethal damage and the Tower
clock. It uses existing initial silver for net purchases and never inserts quest
receipts. This is an offline-profile process test, not authenticated native play.
The wrapper temporarily stages only the three exact reviewed house floors and
removes only files it created. Private configuration is restored, marked clone
removed and test processes stopped. Public indexes and live services are untouched.

The corrected client composition is `defense-talisman-composed-v2`; its journal
SHA256 is `a69ac0d6be78134bdd82fa2a43d0fc81dc9079436cd99b6d2919a10429be0a95`.
The inherited correction is recomposed into `attack-talisman-composed-v3` and
`tempest-wings-composed-v2`. Their exact outputs are in the existing numeric patch
reports. Tower/Fan/Tempest composition suites pass10/10/6 checks. Earlier package
versions are superseded and retained only for provenance; none are installed,
signed or published. Native review must use the new versions.

### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001 completes both houses, buys three
  nets at 30,000 silver each, earns fish through owned encounters, completes
  doors/Shawn/spear/Condor/Caprice, invocation, generals, Yang Feng and cauldron.
  Combat uses calculated ranged attacks; no lethal-damage shortcut is used.
- [x] Reconstructed tower progress at stage9 survives restart. Normal final
  dialogue consumes it and grants bound Elite Star Tower202008 plus one P7 soul.
  The claim leaves silver unchanged. Another restart preserves exact career,
  material, purchase, encounter and reward state; repeat claim changes nothing.

All three phases passed: `career-retained-tower-hunt`,
`career-retained-tower-claim`, `career-retained-tower-reload`. Private binary:
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The runner reuses three reviewed house floors, hash
`9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4`,
through temporary staging and hash-checked cleanup. No installed terrain changes.
The fixture controls clock, public-map placement and approach positions. It does
not establish native travel/rendering or natural combat balance. Nothing deployed.

Staging initializes Defense and Attack schemas. The retained audit now evaluates
both full final-receipt predicates, rather than interpreting schema existence as
completion: Defense complete, Attack incomplete, Tempest schema still absent.
Retained aggregate17/19; achievement10931 remains incomplete.
