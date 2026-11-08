# Attack Talisman / Heaven Fan implementation contract

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

Current private package (2026-09-27): `attack-talisman-composed-v3`, inheriting Tower v2 Net Peddler at325,371. Earlier versions are superseded; native acceptance remains pending.

Status: opening/cave, recipes, Crimson Claw, Swan Lake and Courtesan earning are
implemented through garden treasure/ghost encounters, the Poem Book exchange and
final bound Super Fan/P7 rewards. Ordinary NPC/item/journal/tick/login wiring is
implemented in source. Composed client directions are privately staged and tested;
native acceptance and deployment remain pending.

## Verified sources

The installed6711 journal has ten Type4 missions400-409, minimum100, maximum120.
Mission400 names527 as its prerequisite, while528 is the Tower final hand-in.
The [official PC guide](https://co.99.com/guide/quests/heavenfan.shtml), reviewed
2026-09-26, requires completing Defense Talisman. Revora will therefore require
saved Tower stage10 with its final claim, not possession of a purchased tower,
reconstruction alone or a client-reported journal flag. The first mission has an
empty Profession field; the other nine use0. This is recorded, not silently
normalized during evidence extraction.

The guide confirms separate10,000-silver wine and cave-entry payments. It does
not specify kill odds, exact enemy statistics or replacement fees. Its route
includes cave hunting, ingredient mixing, a lake encounter, four pillar objects
and a treasure-box finale. Those mechanics need actual earning handlers before
completion can be claimed; payment is not a20,000-silver acceptance charge.

[Contract](research/ATTACK_TALISMAN_CONTRACT.json) records all ten mission links,
sixteen exact item identities, checked-material counts, ingredient recipes and
pillar identities. Final equipment201009 is the bound Super HeavenFan;
710221 is a separate quest fan exchanged for PoemBook710222. Never award the quest
fan as the equippable reward. The owner-approved one-time reward policy permits
an additional bound P7 frozen on acceptance. No daily P7 source is introduced.

`tools/audit_attack_talisman.py` verifies exact installed journal/catalog hashes.
`python tests/test_attack_talisman_contract.py` passes7 tests: original evidence,
missing/duplicate stages, changed prerequisite, wrong reward identity, renamed
item and missing item. Test log: obj/fan-contract-tests.log.

## Runtime and spatial findings

[Runtime probe](research/ATTACK_TALISMAN_RUNTIME.json) records read-only loaded
monster data and public NPC/spawn SELECT results. Ordinary WingedSnake6 and
WingedSnakeL28 family65 exist in Phoenix Castle. No Viper, Brigand, Crimson,
Courtesan, Poem or Lyra name match supplies the later quest encounters. Messenger,
Aide and King variants must not accidentally become the normal meat source.
No proprietary implementation was copied and no runtime schema was initialized.

Villager Chou4582 is present at1011(788,456). Only two pillar NPCs and the platform
appear in the selected runtime range, at older positions. NPCs4592-4595 are
clustered on1791, but their names are absent. Candidate scene IDs1788-1791 are
missing from both installed GameMap indexes and have no server floor. The exact
original roles of1788-1790 are not verified. Do not invent original map bindings.

[Terrain inventory](research/ATTACK_TALISMAN_TERRAIN.json) compares all blocked
cells between native and server floors for1011,1000 and1015. Journal/guide public
NPC targets, snake hunting/entrance points, all four Courtesan candidates and
pillar/platform targets are walkable. Connectivity, NPC footprints, cave/lake/
garden interiors, portal behavior and real client rendering remain separate gates.
The only filename match from a bounded scene-name search was skygarden.7z; a
filename is not proof of its suitability or original role.

The four pillar item tooltips all retain653,700, while journal objectives name
several different targets, including733,776 also used for Jade Courtesan. Runtime
pillar placements differ again. Resolve explicit Revora pillar positions from
terrain/footprint evidence and correct the private client overlay together.
Swan Lake entrance471,844 and incense site55,109 are separate locations; its
interior map identity is not established by that text alone.

## Implementation sequence

- [x] Record exact journal/item contract and verify malformed evidence rejection.
- [x] Inspect required runtime catalogs, candidate scene gaps and public terrain.
- [x] Implement once-per-character admission requiring completed Tower rewards;
  issue tracked bound Jadeite and freeze the bound P7 reward. Support every
  eligible ordinary player at level100+, including all rebirth states.
- [x] Implement Villager Chou referral, paid wine, five earned snake meats and
  tracked Tang Silk exchange. Preserve payments/materials on failed writes.
- [x] Implement Sugar Tang/Kunlun referrals and10,000-silver owned cave entry;
  earn Poison Fang and Bee Kettle through combat, with safe durable return.
- [x] Implement Kunlun Liquid, Sacred Water and Poem Silk transactions; no
  client-side material checks may grant completion or consume another owner's item.
- [x] Implement owned Crimson Claw ambush and earned incense/hairpin.
- [x] Implement the lake encounter and Purple Eye with audited native/server
  terrain and an explicit private1011 Revora scene adaptation.
- [x] Implement owned Courtesan encounter and tracked bound Jade Scepter.
- [x] Implement four tracked pillar offerings and replay-safe garden admission,
  preserving progress on interrupted sessions.
- [x] Implement treasure-box encounters, quest fan/book exchange and final bound
  Super Fan/P7 reward with atomic consumption, receipt and immediate updates.
- [x] Wire shared Sugar Tang options without hiding the existing Tower service;
  normal NPC/item/journal/tick/login paths must expose the entire earning route.
- [x] Validate the full ordinary route, failures, recovery and repeated claims;
  compose corrected journal/tooltips and scene profiles over the Tower bundle.
- [ ] Native DX9/Host acceptance, signing, transactional installation and deployment.

Unpublished odds, encounter tuning, durable checkpoints and alternative scene
artwork will be documented as Revora choices. No original probability, map role
or monster appearance is considered verified merely because a fixture can use it.
Pending native acceptance does not block independent transaction/earning work.


## Opening admission, meal and Kunlun referral

AttackTalismanStore now accepts an ordinary level100+ owner only near Sugar Tang
with saved Tower stage10 and both final reward UIDs. Reconstruction alone fails.
Admission creates the per-character Fan row, freezes its eventual bound P7 and
issues one tracked bound Jadeite atomically, without charging silver. Repeating
admission cannot issue another item; rebirth does not reset this chain.

The Villager Chou referral advances stage1 to2 and pays100 silver once. Wine
purchase requires proximity to182,280, room in the bag and10,000 silver. The
bound wine, exact deduction and purchase receipt commit together. An outstanding
wine entitlement prevents another purchase even if that item was lost; recovery
replaces only a globally absent old UID and cannot charge or duplicate it.

Qualifying public-map1011 WingedSnake6 and WingedSnakeL28 deaths can earn bound
tracked SnakeMeat, capped at five active entitlements including stored/lost ones.
Chance is an explicit Revora50% per actual death, not a verified original rate.
The actual registered monster, matching killer, map and screen range are checked.
Messenger/Aide/King variants do not qualify. Repeated corpse callbacks cannot
reroll failure or duplicate success; a real respawn clears the per-life marker.
Full bags and failed inserts create no hidden entitlement. A failed death award
requires another kill; it does not make the corpse reusable.

Near Villager Chou, five usable owned meats and the wine are consumed atomically
for tracked bound TangSilk,100 silver and stage3. The replacement succeeds in a
full bag. Sugar Tang and Kunlun referrals preserve the owned Silk/Jadeite, each
pay100 silver once and advance to the cave-ready stage5. Cave entry itself is
not implemented yet, so no cave-entry charge is made in this batch.

Build and528 checks pass (466 Tower/shared route checks +62 Fan opening checks).
Three characters actually complete the Tower route at all rebirth states before
Fan acceptance. No Tower completion or Fan earning material is seeded. Fan store
methods are called directly after actual snake combat; this is not a public Fan
NPC/combat-hook acceptance claim. Checks cover early/remote admission, exact
silver balances, full bags, failed wine/meat/Silk writes, paid-item recovery,
missed/repeated death rolls, wrong monster families, five-meat cap, frozen reward,
material persistence and repeated referrals/exchanges. Test logs:
obj/fan-opening-build.log and obj/fan-opening-tests.log.
Reproduce with tests/PotencyHunt/Verify-PotencyHunt.ps1 -FanOpeningOnly after the
private Tower scenes are staged. The test uses only the disposable schema.

Next: evidence-backed owned cave terrain, paid admission, Fang/Kettle combat and
safe recovery, then ingredient conversion and later encounters. No public Fan
admission, production schema initialization, live restart or deployment occurred.


## Owned cave, saved entry permit and Fang/Kettle combat

The missing original cave is replaced by an explicitly documented Revora reuse
of installed mine-one map1025, not a claimed original1788 restoration. The
[terrain audit](research/ATTACK_TALISMAN_CAVE_TERRAIN.json) verifies matching native/
server184x184 blocked cells, entrance52,50 and six walking routes to candidate
combat cells. No additional client map binding is needed. Each owned scene loads
its own exact-hash floor with no inherited catalog actors or portals. Up to eight
owners may have one ten-minute scene each; a foreign player cannot teleport in.
Native rendering and portal behavior still require client acceptance.

Admission at Kunlun1000(490,618) requires the saved stage5 and usable tracked
Jadeite/TangSilk. The first10,000-silver payment, durable cave permit and active
visit receipt commit together. Failed visit insertion leaves no charge or permit.
The permit allows subsequent interrupted/expired retries without charging again,
a Revora recovery choice. If transport fails after payment commits, the saved
permit remains available for reentry. Departure/death/expiry returns safely to
1000(492,618), retires the visit and removes owned actors. Login/startup recovery
has matching durable paths; public lifecycle hooks are still pending.

CaveViper at56,50 then CaveBrigand at60,50 award tracked bound PoisonFang721803 and
BeeKettle721806. Each actual owned kill has a durable per-character/kind receipt.
The first encounter uses verified WingedSnake6 mesh203; the second uses reviewed
BanditL97 family55 mesh201. Explicit Revora tuning is level105,30,000/40,000HP,
800-1,200 attack and350 defense. Each successful qualifying encounter guarantees
its one quest material; original odds, random Brigand timing and an inventory
poison damage rule are not established. No undocumented poison damage is applied.

Only the owner or its registered companion can fight these actors. Full bags
leave no kill receipt, permitting a fresh encounter after space is made. Saved
kill receipts prevent re-farming; globally missing tracked items may recover.
The earned Kettle consumes atomically, marks the scene completed and returns the
owner safely with the Fang. A failed consumption preserves the item and scene.
Combat/teleport/AI guards are wired; no public Fan admission or tick is active.

Build and572 checks pass (528 prior +44 cave checks). The same three characters
earn the entire Tower and Fan opening chain, then enter/fight/return from the
cave at all rebirth states. No permit, Fang, Kettle or cave kill is supplied.
Checks cover exact payment, duplicate entry, failed visit rollback, outsider
teleport/damage, actual owned deaths, full-bag retries, expired/replaced sessions,
free reentry, failed Kettle consumption and persisted Fang/permit/balance.
Logs: obj/fan-cave-build.log and obj/fan-cave-tests.log. Reproduce with the existing
-FanOpeningOnly fixture after private Tower staging; only isolated runtime copies
and the disposable database schema are used.

Next: Kunlun Liquid, Sacred Water and Poem Silk transformations, then the Crimson
Claw/lake/pillar/garden route. No production initialization, restart, installation
or deployment occurred.


## Ingredient transformations and Crimson Claw ambush

Kunlun grants tracked bound Liquid721802 only near490,618 with the earned Fang
and TangSilk. A saved alchemy step prevents duplicate claims even if the liquid
is lost; ordinary lost-material recovery uses the existing globally-absent UID
rule. No extra price is charged. Right-clicking either tracked ingredient can
consume Fang721803 and Liquid721802 for SacredWater721804. Using that water
consumes it with TangSilk721798 for PoemSilk721801,100 silver and stage6.
Inputs, bound output, payment and progress commit together. Consumed raw materials
cannot recover. Each two-to-one replacement also works in a full bag.

The PoemSilk report near Villager Chou preserves the poem and Jadeite, pays100
silver once and advances to stage7. Five owned CrimsonClaw encounters then award
saved death credits near788,460 in public Phoenix Castle. Explicit Revora tuning
uses reviewed Birdman18 mesh205, level105,25,000HP,800-1,200 attack and350 defense.
Five sequential enemies and persistent partial progress are Revora choices, not
verified original population/model rules. Outsiders cannot attack these actors;
repeated death callbacks cannot increase the saved count. Existing cave ownership
and combat rules remain separate from the registered public-map check.

After five actual kills, Villager Chou consumes the poem for bound Incense721799,
bound Hairpin710219,100 silver and stage8 atomically. One additional free bag slot
is required. Jadeite is retained for the later pillar sequence. Failed claims
preserve earned credit and materials; repeated claims cannot reissue rewards.

The [ambush terrain audit](research/ATTACK_TALISMAN_AMBUSH_TERRAIN.json) verifies
native/server cells and four walking routes after reserving Villager Chou's
three-by-three footprint. Source spawning checks the exact reviewed public floor
hash and excludes occupied cells. No original model or unverified portal is used.

Build and665 checks pass (572 prior +93 ingredient/ambush checks). All three
characters earn the prerequisites, mix/dye the actual materials, kill their own
ambush enemies and reload the bound lake-ready items. Tests cover remote requests,
full bags, lost-liquid recovery, locked ingredients, failed output/stage writes,
repeated item UIDs, consumed-item recovery rejection, exact silver totals,
outsider protection, duplicate deaths, partial-progress reconnect and claim
rollback. Logs: obj/fan-alchemy-build.log and obj/fan-alchemy-tests.log.
The existing -FanOpeningOnly fixture now includes this checkpoint; methods remain
internal until the full public Fan route is wired. No earning inputs are seeded.

Next: Swan Lake terrain and owned incense/Herbalist/Crimson Viper route, then
Courtesan, pillars, garden and final rewards. Native/Host acceptance, client patch
composition and deployment remain pending; no live changes occurred.


## Swan Lake earned checkpoint (2026-09-26)

Implemented internal earning transactions through stage9: enter the owned lake,
burn earned incense, speak to Herbalist Chou, defeat the owned Crimson Viper,
receive bound Purple Eye710216, return and report to Villager Chou for100 silver
once. Jadeite and Hairpin remain owned for the later pillars. No additional
entry charge, replacement incense after burning, or duplicate kill reward.
Incense consumption, kill reward and stage payments each use atomic transactions.
Full bags leave the viper reward available through another owned encounter.

Explicit Revora layout: a private copy of map1011 around Swan Lake471,844;
incense475,844, Herbalist475,840 (approach477,840), Viper479,844 with481/483
fallbacks, safe public return473,844. The original55,109 interior is not restored.
Missing1788-1791 maps and unsuitable inspected interiors do not justify an
unverified native map. The [lake terrain audit](research/ATTACK_TALISMAN_LAKE_TERRAIN.json)
checks native/server cells and seven paths, reserving the spirit footprint.
No client map alias is required; the future client overlay must replace original
interior directions with this documented layout. Native visual acceptance remains
pending. Herbalist custom NPC9910401 reuses mesh3694. CrimsonViper9905403 reuses
verified WingedSnake6 mesh203, with Revora tuning: level110,50,000HP,900-1,400
attack,400 defense. These are documented custom choices, not original statistics.

Saved lake steps distinguish unburned incense, summoned spirit, conversation and
completed kill. Reentry restores the spirit without reissuing incense. Visits
save their base map so expiry/offline recovery returns to the correct shore;
legacy cave visits retain the1025 return profile.

Build and712 checks pass (665 prior +47 lake checks). All three rebirth states
actually earn prerequisites and defeat their owned enemy; cases cover remote
requests, failed incense writes, duplicate actions/deaths, outsider damage,
full-bag retry, replacement-session and expiry recovery, exact silver and reloaded
bound gems. Logs: obj/fan-lake-build.log and obj/fan-lake-tests.log; reproduce with
-FanOpeningOnly. No production initialization, restart or deployment occurred.

Next: Courtesan, four pillars, garden, final rewards, ordinary public Fan wiring
and composed client directions. The full quest remains incomplete.


## Courtesan and Jade Scepter (2026-09-26)

The official guide confirms a Courtesan kill on Bird Island at one of four
candidate locations, yielding Jade Scepter. Chou's successful lake report now
chooses and saves one site:733,776 /803,884 /797,513 /703,653. Equal random
selection once per character and persistence through reentry are explicit Revora
rules; original scheduling/odds are not documented. Failed referral writes cannot
advance the stage or charge/pay silver independently of this choice.

At that saved location the eligible owner can encounter one JadeCourtesan9905404.
Actual owned death atomically records a unique receipt and awards bound tracked
JadeScepter710217; a full bag leaves another encounter available. Outsiders cannot
steal the kill, repeated callbacks cannot duplicate the item, and globally missing
tracked Scepters use the existing recovery rules. The three previously earned
gems must remain usable. No new silver payment or P7 reward occurs at this step.

Explicit temporary Revora appearance uses reviewed BanditL97 family55 mesh201;
this does not claim the original Courtesan model. Revora tuning: level110,
60,000HP,900-1,400 attack,400 defense. Replace appearance only with verified client
model evidence and native acceptance. The [terrain audit](research/ATTACK_TALISMAN_COURTESAN_TERRAIN.json)
verifies all four native/server sites, nearby approaches and two fallback spawn
cells each. Live occupied cells are rejected by the existing encounter allocator.
Source pins the exact reviewed1015 floor hash.

Public Fan NPC/journal/tick hooks remain pending until the entire earning route
is complete. No production schema initialization, restart or deployment occurred.

Build and736 checks pass (712 prior +24 Courtesan checks), including actual earned
prerequisites and owned kills across all rebirths, saved random selection,
wrong-site rejection, duplicate spawn/death prevention, outsider protection,
full-bag retry, reconnect cleanup, lost-Scepter recovery, exact silver and
reloaded four bound gems. These tests call internal Fan methods; public route
and native acceptance are not claimed. Reproduce with -FanOpeningOnly; logs:
obj/fan-courtesan-build.log and obj/fan-courtesan-tests.log.


## Pillar offerings and garden admission (2026-09-26)

Four ordinary-owned tracked gems must be offered after the saved Courtesan kill.
Each successful offering consumes exactly its matching gem and saves one bit in
pillars atomically; the consumed material ledger prevents replacement recovery.
The order is unrestricted. Remote requests, incorrect active NPCs, invalid indices,
missing/locked gems and repeated offerings are rejected. No new charge occurs.
Partial offerings survive reload so a failed/interrupted visit never requires
re-earning consumed gems.

Explicit Revora layout on1015 resolves conflicting legacy coordinates:
Sparrow9910410 at680,720 consumes PurpleEye; Dragon9910411 at694,720 consumes
JadeScepter; Tiger9910412 at680,734 consumes Jadeite; Leopard9910413 at694,734
consumes Hairpin. All reuse reviewed pillar mesh3500. GardenPlatform9910414
at686,723 reuses platform mesh3540. Installation is internal/staged and pins the
reviewed1015 floor hash; public initialization and old-object reconciliation
remain part of public Fan wiring. Future journal/tooltips must match this layout.

A valid interaction with the platform after all four offerings permits an owned
private3937 skygarden instance. This is an explicit Revora Rose Garden adaptation,
not a claim that the missing original1791 scene has been restored. Existing client
and server indexes agree on map/map/skygarden.7z; no new map alias is needed.
The [garden audit](research/ATTACK_TALISMAN_GARDEN_TERRAIN.json) checks every blocked
cell against native data and verifies NPC footprints and approach/combat routes
for the pillars and planned interior. Entry48,60; public return1015(688,723).
Source pins garden floor SHA256d76d15a0bf69c2e22e75b782138ae94673e68da916debb052106b184cce307e8.
Native visuals and portal behavior remain pending.

Visit creation, stage9-to10 and100 silver commit together. Reentry at stage10
uses the saved four offerings and pays nothing again. Existing ten-minute lifetime,
owner/capacity/teleport guards and persisted return profiles cover the garden.
Expiry returns to the platform approach; reconnect preserves the unlock and
retires the abandoned visit. No treasure boxes or final rewards are yet wired.
No live initialization, installation, restart or deployment occurred.

Build and810 checks pass (736 prior +74 pillar/admission checks), with actual
Tower-to-Fan earning across all rebirths and different offering orders. Coverage
includes remote/wrong-NPC/index rejection, failed consumption/progress write,
partial reload, consumed-token recovery rejection, failed visit rollback,
outsider entry rejection, reconnect/expiry return, reentry and exact silver.
Reproduce with -FanOpeningOnly; logs: obj/fan-pillars-build.log and
obj/fan-pillars-tests.log. Internal-method coverage is not public NPC acceptance.


## Garden treasure, ghosts and final claim (2026-09-26)

First successful garden entry saves one uniformly chosen winning box out of three;
reentry never rerolls it. Explicit Revora rules: each other box can be opened once
and creates a saved pending ghost, blocking other boxes until actual owned combat
clears it. The two wrong boxes map cyclically to PoemGhost and LyraGhost. Choosing
the winning box first needs no ghost kill. Original odds, box assignment and
checkpoint behavior are not published; these choices are documented adaptations.
No click-count reward or client completion flag substitutes for a ghost death.

Boxes9910420-9910422 at52/58/64,50 reuse catalog mesh3550; MrLoneliness9910423
at60,64 reuses3580. Both are scoped to the owned scene. PoemGhost9905405 and
LyraGhost9905406 use verified Skeleton100 mesh163, explicit Revora level110,
55,000HP,900-1,400 attack and400 defense. Their audited spawn is54,58 with56/58
fallback cells. The18-cell trigger reaches all box approaches. Native visuals
remain pending; none of these reused models claims original appearance.

The correct box atomically records its opening and grants tracked bound quest
HeavenFan710221. Full bags or failed writes preserve that entitlement. MrLoneliness
consumes the tracked fan for bound PoemBook710222, saving book_given and completing
the scene; the existing return coordinator evacuates the owner safely. A full bag
supports this one-for-one exchange. Lost quest fan/book recovery requires its old
tracked UID to be absent globally; consumed items cannot recover. Book owners
cannot reopen the garden or obtain another fan.

At Sugar Tang, the saved book exchange plus the actual usable tracked Poem Book
are required. One transaction consumes that book, inserts bound SuperFan201009
and the bound P7 frozen on original admission, and stores both reward UIDs with
stage11. At most39 occupied slots permits the net extra output. Failure preserves
the book; repeated claims, final-book recovery and restarting the completed
once-per-character quest are rejected. No daily P7 source is introduced.

The full earned transaction route is implemented internally; ordinary NPC/item/
journal/tick/login wiring and composed client directions remain the next step.
No production initialization, client installation, restart or deployment occurred.

Build and879 checks pass (810 prior +69 garden/final checks). Actual Tower-to-Fan
prerequisites are earned across all rebirths; fixtures exercise every winning-box
position, both owned ghost types from box approaches, outsider protection,
duplicate deaths/openings, pending-ghost reconnect, full bags, treasure/book/final
write failures, exact balances and final production-loader persistence. No quest
progress or reward input is seeded. Tests still invoke internal Fan methods;
public earning availability and genuine-client acceptance are not claimed.
Reproduce with -FanOpeningOnly; logs: obj/fan-garden-build.log and
obj/fan-garden-tests.log.


## Ordinary Fan coordinator (2026-09-26)

AttackTalismanQuest now owns the ordinary NPC, item-use, natural snake-death,
periodic encounter, journal and login seams. Admission remains level100+ and
requires the actual completed Tower receipt; all rebirth states are supported.
No GM/name/owner allowlist exists. The ordinary Tower dialogue at Sugar Tang
retains its original options and offers Heaven Fan separately (80 menu,81 action).
Fan recovery uses90/91 menus and100-114 item choices without stealing Tower options.

Chou4582 keeps1011(788,456). WineKiddo4581 at182,280 and KunlunWanderer4583 at
1000(490,618) use documented Revora mesh reuse3600/3694 where original loaded
NPCs were absent. LakeKeeper9910430 is a Revora travel helper at1011(469,840),
with approach471,840 and entry471,844. The first proposed469,844 tile was blocked
and was rejected before publication. The [public terrain audit](research/ATTACK_TALISMAN_PUBLIC_TERRAIN.json)
checks native/server cells and connected approaches around reserved footprints.

The coordinator recognizes actual owned scene NPCs, verifies active-NPC identity
and proximity, locks inventory/session ownership, and routes each action through
the saved earning transactions. Cave entry/reentry is an explicit Kunlun option.
Incense, recipes and Kettle use the ordinary ItemHandler path. Scene completion
returns the owner immediately or during the next tick. Short dialogue hints give
current destinations and prices; lost-item menus preserve the existing global
absence check. Fan NPC/item requests are intercepted while disabled rather than
falling through to old reward branches.

IDs400-409 are authoritative journal projections. Existing Begin/status packet
handlers only refresh the saved state and cannot manufacture completion. Completed
kills refresh journal state; periodic spawning uses saved stage/site watches and
existing owned encounter guards. Login retires persisted visits before refreshing.
These reuse existing packet families; no inferred wire layout was introduced.

Initialization waits for the Tower coordinator and reviewed1025/1011/3937 scene
files, validates exact terrain hashes, initializes durable stores/travel and
checks public NPC conflicts. The three reviewed older pillar/platform objects
4587/4588/4591 are retired only at their recorded coordinates/meshes, avoiding
stacked or misleading public objects. New pillars use the documented layout.
This source initialization is not a live deployment or native-client claim.

Build and541 public-route checks pass:406 shared rebirth checks,60 ordinary Tower
checks and75 ordinary Fan checks. These totals overlap prior879 internal-route
checks and must not be summed. The three ordinary characters earn Tower first,
then complete Fan through Npcs.GetDialog, ItemHandler.UseItem, real attack/death
hooks, coordinator ticks, journal packets and production item/entity reloads.
Tests verify exact20,000 silver costs and900 stage silver, duplicate wine/final
requests, remote admission, forged journal rejection, owned scene travel, every
major earned material and final bound rewards. The test setup was corrected to
preserve initialized maps between players and use monotonic fixture time.
Reproduce with tests/PotencyHunt/Verify-PotencyHunt.ps1 -FanPublicOnly after private
Tower scene staging. Logs: obj/fan-public-build.log and obj/fan-public-tests.log.
Native6711 rendering, automatic pathfinding and live Host acceptance remain open.


## Composed private client directions (2026-09-26)

`tools/ClientPatches/prepare_attack_talisman.py` composes ten missions400-409 and
fifteen quest-item tooltip fields over the exact reviewed Tower package. Other
journal sections, item statistics and both pending map repairs remain byte-for-byte
unchanged. Mission400 now requires completed Tower528; all Fan missions use
level100-255 and Profession0, matching the ordinary all-class server route.
Directions name Lake Keeper469,840, interior incense475,844, the four corrected
pillars, GardenPlatform9910414 and actual garden boxes/MrLoneliness. Obsolete
55,109,653,700 and legacy platform4591 targets are removed from Fan directions.

Descriptions distinguish quest fan710221 from equippable SuperFan201009, describe
both10,000-silver costs, free cave/garden reentry, persisted offerings/box choices,
and bound final Super Fan plus frozen bound P7. Obsolete client material-completion
checks are removed because consumed offerings must not make the journal regress;
server projection and request guards remain authoritative. Actual6711 UI and
pathfinding acceptance remain pending.

Private output: `obj/chi-refill-validation/attack-talisman-composed-v2`.
The initial draft had an incorrect reward label and is marked SUPERSEDED.txt;
automatic approval review rejected its deletion as blocked by policy. It remains
unused; only v2 is eligible for later acceptance. No output was installed, signed,
published or added to Git. Exact output hashes are recorded in the secret-free
[patch report](research/ATTACK_TALISMAN_PATCH.json).

Ten tests pass: idempotence, missing/duplicate missions and fields, prerequisite/
level/class/reward consistency, corrected destinations, unchanged unrelated
journal bytes, tooltip-only edits, malformed tooltip rejection, encrypted catalog
round trip, repeated private composition/readback and unchanged source hashes.
Command: `python tests/test_attack_talisman_patch.py`; log: obj/fan-patch-tests.log.
Prior541 public server checks remain the separate server evidence. Next gates are
native DX9/Host acceptance, signing and transactional installation/deployment.

## Earned Tower-to-Fan journey across actual processes (2026-09-27)

- [x] Complete the actual Tower prerequisite through the prior NPC/combat route,
  including house interruption/recovery. Continue that saved ordinary character;
  do not insert a completed prerequisite or supply quest materials.
- [x] Defeat a normally scheduled boss for the purchase funds. Accept Sugar Tang's
  Fan request, pay Wine Kiddo once despite a repeated request, and hunt loaded
  Winged Snakes through ReceiveAttack. This run earns five tracked meat drops
  from12 snakes with the unchanged50% random odds, without overriding HuntRoll.
- [x] Exchange the meal, follow the referrals, pay the cave permit and interrupt
  the actual game process. Restart game/database, return safely and re-enter
  through Kunlun without another charge.
- [x] Earn Fang/Kettle, make liquid/water/poem, defeat the five Crimson Claws,
  enter the lake, burn incense, speak to the spirit, defeat the Viper and selected
  Courtesan, offer all four pillars, defeat a wrong-box ghost, recover the quest
  fan and exchange the Poem Book. Sugar Tang grants the bound Super Fan201009
  and frozen bound P7 soul once; final silver is starting silver minus20,000
  plus900 stage silver. The recorded wine purchase is exactly10,000 silver.
- [x] Final process reload and repeated hand-in preserve stage11, exact saved
  reward UIDs and remaining silver. The earlier Tower item remains owned.
- [ ] Authenticated/native quest navigation, representative natural combat,
  lost final-COMMIT response, signing and live deployment remain pending.

Run `tools/Verify-ArenaProcess.ps1 -FanOnly`. All five game launches pass in
`obj/fan-process-run.log`; executable SHA256
`7D86AB5305F15DFE9C77E9849B27A166597C3D497B71A4BF1E49CB43A10F8055`.
No production source change or rebuild was required. Qualification, proximity,
lethal damage and quest/boss clocks are controlled. Saved random Courtesan/box
choices are read to exercise the intended encounter branches; progress, materials,
permits and rewards are earned only through the normal handlers.

The shared process combat helper now retries normal attacks for at most two
seconds when a real world tick temporarily prevents the nonblocking encounter
lease. It never forces death or writes quest credit, and retains diagnostics on
failure. The earlier one-shot assumption could fail during the Tower prerequisite.
This is offline-profile process evidence, not wire/native client acceptance.
The private terrain/configuration is restored, marked clone removed and test
processes stopped. No live service, database, public index or client changed.

### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001 uses its earned Defense Talisman
  completion to start the Fan route. Wine/cave costs and interim payments net
  exactly -19,100 silver. Calculated attacks earn snake materials, cave drops,
  ambush, lake, courtesan and wrong-box ghost encounters before the winning box.
- [x] The exchanged book and stage10 survive restart. Normal final dialogue
  consumes the book and grants bound Super Heaven Fan201009 and one P7 soul,
  leaving silver unchanged. Another restart preserves exact career, material,
  purchase, encounter, visit and reward state; repeat claim changes nothing.

All three phases passed: `career-retained-fan-hunt`,
`career-retained-fan-claim`, `career-retained-fan-reload`. Private binary:
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The runner temporarily stages the existing reviewed Tower prerequisite floors,
then removes only the unchanged files it created. The fixture controls clock,
rolls, public-map placement and approach positions; combat damage is calculated.
Native rendering/navigation, natural balance and deployment remain pending.
The full receipt audit verifies18/19 chains; only Tempest Wings remains on this
character, and achievement10931 remains incomplete.
