# Equipment quest aggregate 10931

Status: the approved cross-rebirth Revora evaluator is implemented. All 19 final
reward transactions call it before commit; progression recovery checks older
receipts. Predicate and rollback validation passed in a private game process.
The complete 19-chain journey and restart recovery pass through server-side
handlers on one non-GM character. Native/authenticated wire acceptance and deployment remain pending.

The installed achievement catalog identifies10931 as completion of all equipment
bonus quests. The [official achievement guide](https://co.99.com/guide/guides/achievement.shtml),
rechecked2026-09-26, identifies equipment bonus quests as a quest achievement
category but does not enumerate its required missions or rebirth semantics.
Do not equate a catalog category with verified original completion rules.
A bounded official-site search did not establish the missing rule; unrelated
player-authored tips and iPad editions were not adopted as PC achievement evidence.

The hash-bound6711 Questinfo catalog has132 named Type4 missions. Nineteen reviewed
Revora chains cover every named row exactly once. Eighteen unnamed950-967 rows
are excluded from this proposal. Fan400 is linked to Tower527 in the original
journal, while the reviewed Revora edition requires the final Tower528 receipt.
That difference is recorded, not silently treated as verified retail behavior.

## Approved Revora rule

Require final completion of all19 chains below by the same character. For the16
rebirth-scoped chains, any valid saved life0-2 can satisfy that chain; different
chains may have been completed in different lives. The three once-per-character
chains remain once-per-character. The owner approved this cross-rebirth policy on 2026-09-30; it is not an
original-rule claim.

Each chain must have its final stage and complete committed reward receipt.
Reward UID lists must have the exact expected number of distinct positive UIDs;
Power Book instead joins its separate reward row to the same character/life.
Do not require the reward items to remain in inventory: spending an EXP Ball,
opening a reward pack or losing equipment cannot erase a completed quest.
Journal packets, old achievement bits, owning a pack, buying an item, reaching
a level and another character's receipts cannot establish completion.

| Chain | Missions | Final stage | Reward receipt count | Scope |
| --- | --- | --- | --- | --- |
| connector | 1718-1722 | 6 | 3 | per rebirth |
| spun gold | 1723-1726 | 8 | 2 | per rebirth |
| azure pill | 1727-1732 | 7 | 2 | per rebirth |
| artisan feather | 1733-1737 | 6 | 2 | per rebirth |
| mystic ring | 1738-1744 | 10 | 2 | per rebirth |
| frantic monkeys | 1745-1750 | 10 | 2 | per rebirth |
| soul general | 1751-1757 | 12 | 2 | per rebirth |
| mad man | 1781-1786 | 9 | 3 | per rebirth |
| ghost city | 1787-1794 | 11 | 3 | per rebirth |
| tomboy | 1795-1799 | 6 | 3 | per rebirth |
| desert blood | 1800-1807 | 11 | 3 | per rebirth |
| hero wings | 1808-1818 | 12 | 3 | per rebirth |
| family feud | 1819-1827 | 10 | 3 | per rebirth |
| desert treasure | 1828-1834 | 8 | 3 | per rebirth |
| desert expedition | 1835-1839 | 6 | 3 | per rebirth |
| power book | 1840-1852 | 14 | 2 | per rebirth |
| defense talisman | 520-528 | 10 | 2 | per character |
| attack talisman | 400-409 | 11 | 2 | per character |
| tempest wings | 200 | 4 | 2 | per character |

The [numeric mapping](research/EQUIPMENT_QUEST_AGGREGATE.json) records each table,
final transaction source and current source hash. This is a manually reviewed
mapping plus coverage checks, not an automated proof of SQL semantics or a live
player having completed all chains.

## Implementation and acceptance

- [x] Review the official category description and exact installed mission coverage.
- [x] Map19 final transactions, final stages and distinct saved reward shapes.
- [x] Six audit checks pass: reviewed coverage, changed source hash, missing mission,
  duplicate mission, altered prerequisite and duplicate field. Reproduce with
  `python tests/test_equipment_aggregate_audit.py`. No server build needed.
- [x] Owner approved the documented Revora19-chain rule across rebirth lives.
- [x] Add the transaction-bound aggregate evaluator and replay-safe 10931 evidence
  to all 19 final reward transactions and progression recovery.
- [x] Verify missing schemas, 149 invalid receipt cases, mixed saved lives,
  consumed-reward independence, suppressed evidence insertion, outer transaction
  rollback and repeated evaluation in the actual game assembly.
- [x] Exercise Connector, Spun Gold, Azure Pill, Artisan Feather, Mystic Ring,
  Frantic Monkeys, Soul General, Mad Man, Ghost City, Tomboy, Desert Blood and
  Hero Wings, Family Feud, Desert Treasure, Desert Expedition, Power Book, Tower,
  Fan and Tempest Wings on one non-GM character through quest handlers. Preserve
  all forty-seven bound rewards across restart, refuse duplicate claims and
  keep 10931 absent until the nineteenth final reward claim.
- [x] Verify four competing final Tempest NPC claims produce one reward pair and
  one 10931 evidence row after the earned 19-chain path.
- [x] Resolve map-entity collection invalidation with concurrent snapshots, atomic
  lookup/addition and exact-entity removal. Deterministic reproduction and
  concurrent guard-search tests pass; this is not a claim about all world state.
- [x] Earn the final receipt through quest handlers, reload, invoke normal title
  reconciliation and verify the 10931 achievement bit. No synthetic completion
  or reward insertion is used. This is handler-level evidence, not native display.
- [ ] Native/Host acceptance and deployment stay pending under the existing gates.

Recruit aggregate 10930 is separate and remains an unresolved edition decision.

## Private validation (2026-09-30)

Build passed using the committed-owner-file overlay. Tested executable SHA-256:
`8E6210B23013E7C8BDCED4E9E591A65B68DB7BC1FFA83EF86F4FE06C5DA4D4F0`. Run
`tools/Verify-ArenaProcess.ps1 -EquipmentAggregateOnly` after refreshing the
private candidate. The fixture uses transaction-local temporary receipt tables
and the production evaluator. It never persists synthetic quest completions.
Its 149 refusal cases include every chain's wrong owner and partial stage,
invalid rebirth lives, incomplete/null/duplicate/nonpositive UID arrays,
mismatched Power Book receipts and a missing quest table. A suppressed evidence
insert must throw; rollback removes evidence and retry writes exactly one row.

These checks establish receipt predicates and transaction behavior. They do not
establish that one ordinary character earned all 19 chains, a restart of that
complete journey, or native client display. Existing individual quest evidence
retains its separately documented scope. Live service was not restarted.

## Complete nineteen-chain journey and combat contention (2026-09-30)

`tools/Verify-ArenaProcess.ps1 -EquipmentJourneyOnly` uses one supplied level-140,
life-zero non-GM character. It earns the nineteen chains through NPC handlers,
loaded monster deaths and quest-item use, without inserting quest completions,
materials or rewards. Random quest drops and Azure Pill's wait are controlled;
combat uses supplied lethal damage through `ReceiveAttack`. Mystic Ring consumes
twelve Stanchers and twelve Agrypnotics bought from the loaded silver shop, with
exact payment checked. Frantic Monkeys uses its ordinary poison/vial admission,
five private trial kills and three spawned ape kills; its wait clock and heart
drop are controlled. Soul General earns all 21 scent kills and the owned phantom
fight; its cleanup clock is controlled. Mad Man uses the earned fang and ordered
Blue/Green toxin recipe, visits all seven stones, combines their powders and
hands in the result; the fang drop is controlled. Ghost City uses ordered
fireworks, charm entry, the pillar errand, 100 ordinary encounter kills, two
bosses and five optional child rescues; encounter ticks use controlled time.
Tomboy earns ten marbles and two of each fusion stone, crafts all three jades
through item handlers and answers Carolyn; drop selection and successful fusion
rolls are controlled. Desert Blood earns its Stone Spell, visits the private
crypt, kills 200 BladeGhosts, gathers five fangs and completes the letter/Peach
Bag/note deliveries. Spell and fang drops are controlled. The loaded flocks use
their natural respawn timers; the fixture waits at most 45 seconds for an
existing flock, without spawning replacements or changing kill timestamps.
The journey phase has a bounded three-minute deadline to accommodate this hunt.
Hero Wings funds its Meteor through an earned daily-boss bounty: exact increases
of 1,000,000 CPs, 50,000 RVPs and 10,000,000 silver and the paid boss receipt are
checked. The character purchases one Meteor at the loaded shop price and consumes
it through the doctor dialogue. Bird Blood, Gallbladder/Ginseng gathering and
five timed HawkL93 pairs proceed through normal handlers; drops, gathering-site
selection, boss scheduling and pair timing use controlled randomness/time.
This validates the payment route, not natural boss combat difficulty. Family
Feud earns 50 BanditL97 kills, one BanditL98, ten Banditti and thirteen owned
encounter kills (leader, courier, ten deliverers and senior). The disguise admits
the character to Kerry's chamber. All five ordered jumps pass the production
jump handler, with each durable puzzle step checked before the final hand-in.
Encounter/disguise/puzzle clocks and drop rolls are controlled; positions and
jump timing inputs are supplied by the fixture. The character then deposits all
34 owned items through normal warehouse requests. Desert Treasure earns the key,
uses the vault jump, buys the portrait for 99,999 silver with duplicate payment
refused, gathers five essences and captures the defeated JinxTombBat with its
bottle. Hunt/vault rolls and encounter timing are controlled. The final balance
is exactly 93,499 silver below the chain's opening balance after its payouts. Desert
Expedition earns ten horns from loaded BullMonsters while the pill is active,
refuses an early hand-in and settles all six saved HP payments. Three Mad Bulls
are lured onto their mirror traps through the encounter movement handler, then
killed through combat; the spawned Bull Master earns the final heart. The exact
6,300-silver total and three bound final rewards are checked. The pill/encounter
clock, successful horn rolls, positions and movement timing are controlled.
Power Book purchases its Meteor from the same previously earned boss funds,
consumes it once through the Beggar, and completes all four private floors.
The ten Gold Ghosts follow the player through `Screen.DriveMonsterAt` to a
herding point; the fixture supplies player positions and move timing, without
teleporting the monsters. Stage checks cover the hunts, timed pairs, fang use,
wing cooking/eating and ten corpse burns. Population timing and successful
material rolls are controlled. The badge/book/plume are gone after final hand-in,
and the separate durable armor-pack/soul receipt is included in restart checks.
Tower earns both five-kill house trials, three net purchases at 30,000 silver,
three fish, the timed Shawn encounter, both desert fights, the dungeon generals,
Yang Feng and reconstruction. Its saved completion admits the same character to
Fan. Fan buys wine and cave access, earns five loaded Snakeman kills, cave and
alchemy materials, ambushes, the lake and Courtesan encounters, pillar entry,
a losing garden-box fight and the winning box. Its net silver change is checked
at -19,100. These routes use controlled encounter clocks and supplied positions;
Fan horn drops are forced successful while saved Courtesan/box choices are read.
Tempest buys fifty Meteors using the earlier boss bounty, converts them through
the normal Meteor-scroll NPC, spends all five scrolls, defeats the spawned Roc
and hands in its feather. Exact CP payment and the five-scroll receipt are checked.
Private Tower/Tempest terrain is staged using the existing reviewed hashes and
removed by the runner afterward. This does not publish a client terrain patch.

The exact 49-item restart fingerprint includes 47 final rewards, the unused red
toxin and reusable Reveal Vial. Thirty-four items are in Twin City storage and
fifteen remain in the bag. Loaded memory must match saved UIDs, types, binding,
stack sizes, warehouse IDs and equipment positions, with no duplicate UIDs.
The offline fixture now restores the normal town-warehouse registration before
loading stored items. Earning and restart phases use that initialization order,
without inserting completions, items or rewards. This is server-side handler
evidence, not authenticated wire or native-client acceptance.

The fixture checks final receipts, exact reward UIDs/types/binding/stack counts,
currency and receipt fingerprints after a game/database restart and repeated final
NPC claims. 10931 is absent after eighteen completions and the Roc encounter,
then present exactly once after four competing final NPC claim requests. After
restart, the source/rule version, unchanged receipt fingerprint and projected
achievement bit are verified.

An initial run recorded only 99 feathers after 100 lethal attack attempts. A
controlled follow-up held the character's session lease on another thread: the
old assembly killed the monster but saved zero feathers. `HealthMutationLease`
now acquires the managed player attacker's session lease before item/health locks
for monster combat. A busy lease refuses damage; normal hits hold the lease
through synchronous quest credit. It does not queue stale kill credit or wait
while holding character locks. The fixture requires the contested hit to leave
the monster unharmed, then verifies one saved feather per subsequent kill.

The six-chain combat-contention validation used executable SHA-256:
`BA543847A74B6FF4506539881D01B578CBE302CAFCACDEBEF88BF0346A488DA0`.
The eight-chain extension through sixteen-chain earning/reload phases used
`E9607BE43A45ECAE234D49C2D74C54993B94908BCBF4F423B0023E87A277CCA2`.
It exposed and fixed the missing Phoenix RockMonsterL42 population; see
[the population evidence](SOUL_GENERAL_IMPLEMENTATION.md#phoenix-population-and-earned-process-route-2026-09-30).
The private build uses the committed-owner-file overlay; unrelated owner edits
are excluded. No live deployment or client patch is part of this validation.

## Respawn overflow correction and final run (2026-09-30)

The longer run encountered living Tower targets rejected by their encounter
combat guard. Inspection identified a concrete independent timer defect:
`Time32.AddSeconds` multiplies seconds by 1,000 in 32-bit arithmetic. The
1,000,000,000-second delay used by quest encounters becomes an already-due
value, allowing the map timer to revive a corpse while the quest coordinator
still owns its death. The old arithmetic is reproduced explicitly in the fixture;
the precise cause of each earlier intermittent failed hit was not captured.

`Map.RespawnDue` now compares wrap-safe elapsed milliseconds with a 64-bit delay,
rejecting a death newer than the tick snapshot. Tests cover the old overflow,
both long encounter delays, the strict 30-second boundary, signed/unsigned clock
wrap boundaries, zero delay and a future death. Long encounter delays remain
inactive; their coordinators own removal/replacement. Ordinary short respawns
remain due on schedule. Desert Expedition's fixture lure also retries its normal
nonblocking movement handler for a bounded two seconds instead of twenty rapid
calls that can all lose a transient lock.

The full nineteen-chain earning phase passed with executable SHA-256
`E686BB2935D8BADF4172BD07DC251F557132686A0DC98A529DC36EF68790CDE5`.
An added future-death regression rejected that initial timer fix during reload.
After the correction, the same already-earned database passed restart recovery,
all timer cases, repeated claims and the achievement-bit check with executable
`99F451251F4159F44DEF380C8D92FC0FB9A1C36CB4C9C9DE5392898C1A22BCAE`.
No quest progress or rewards were seeded to recover that run. The build uses the
committed-owner-file overlay, excluding unrelated worktree changes. Six aggregate
mapping checks also pass. Live deployment and native acceptance remain open.

## Map entity concurrency correction (2026-09-30)

One extension run logged `InvalidOperationException: Collection was modified`
in `CityGuardSystem.FindNearbyMonster` while `LivingWorldSystem.Drive` enumerated
`map.Entities.Values`. `SafeDictionary` derives from a mutable dictionary and
does not provide a concurrent snapshot. In that run, the Soul General phantom
also remained alive after the fixture's bounded attack retry. A causal link
between the two observations has not been established. The redacted local log
was retained at `obj/equipment-journey-concurrency-observation.log`.

`Map.Entities` now uses the existing `SafeConcurrentDictionary`. Its values
are snapshots, so adding/removing actors cannot invalidate a reader's enumeration.
The shared concurrent indexer uses one `TryGetValue`, preserving missing-key
behavior without a check/read race. Map insertion uses `TryAdd`; removal compares
the exact stored entity so an old reference cannot remove a replacement. Guard
summon lookup and three fixture map constructors were adapted to the new type.
Other map collections, floor flags and mutable entity fields are outside this
specific collection-safety claim.

The private fixture deterministically reproduces the old dictionary failure,
checks snapshot retention after mutation/clear, duplicate insertion and stale
removal, then runs two writers and two readers on an isolated map: 4,000 actual
map add/remove cycles and 4,000 `CityGuardSystem.FindNearbyMonster` searches,
plus concurrent value enumeration and missing-key reads. It repeats at restart.
The 57 companion navigation checks also pass; Team PK and quarantine ledger
fixtures compile against the new type, but their database suites were not rerun.

A full nineteen-chain earning/reload run now passes on the same final executable:
`0F73F292FEB583E0DC46549AEA54933FF3EA38A8C49630DAD86F1C9AB2B542E1`.
It preserves all 47 rewards and the 10931 evidence/achievement bit. No collection
invalidation appears in either run log. The fixture chooses the Gold Ghost
nearest the herding point, independent of dictionary iteration order, and waits
through bounded normal quest ticks for the Bull Master spawn. It still moves
monsters through their movement handler and grants no synthetic quest progress.
The private database clone is removed and its PostgreSQL instance stopped by
the runner. No live deployment or native acceptance is claimed.

## Fresh Connector and Spun Gold progression (2026-10-03)

The production-created ordinary career now completes Connector and Spun Gold
in life2 using its earned qualification and gear. Eight calculated kills with
unforced invitation loot earn both unpaid final stages. Separate unclaimed/paid
processes preserve exact materials, all prior assets, five bound rewards and
repeat refusal. The five saved UIDs match728236/723911/728237 and two P7 souls.
This is2/19 fresh chains; the other17, pack opening and fresh10931 remain open.
It extends the earlier supplied-profile journey, not native/live acceptance.
[Evidence and limitations](CAREER_STARTING_BOUNDARY.md#fresh-connector-and-spun-gold-equipment-chains-2026-10-03).

## Fresh Azure Pill and Artisan Feather (2026-10-03)

The fresh career now also earns Azure Pill and Artisan Feather in life2, using
calculated kills and unforced drops. Azure's saved brewing deadline is accelerated
only after immediate refusal is checked. A first-swing fixture assertion stopped
an earlier process; exact reviewed snapshot hashes preserve its completed Azure
errand and79 Artisan feathers before continuing the remaining hunt. No character
or quest state is reset. Separate unpaid/paid processes verify four exact bound
rewards728238/728239/two P7 souls, saved final stages and duplicate refusal.
Fresh coverage is4/19; the other15, pack openings and fresh10931 remain open.
[Interruption, evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-azure-pill-and-artisan-feather-with-preserved-interruption-2026-10-03).

## Fresh Mystic Ring and Frantic Monkeys (2026-10-03)

The fresh career now earns both life2 chains through24 shop-purchased medicines,
20 Ratlings, one unforced Macaque drop, five trial monkeys and three ape encounters.
Calculated combat records29 defeats/30 attacks. Separate preparation, quest,
unpaid-claim and paid-reload processes preserve Phoenix warehouse storage and
verify four exact bound rewards:728240,728243 and two P7 souls. Duplicate final
claims preserve the complete snapshot. Only trial/cleanup clocks, proximity and
attack cooldowns are controlled; no quest/drop/damage override supplies progress.
Fresh coverage is6/19; thirteen chains, pack opening and fresh10931 remain open.
[Evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-mystic-ring-and-frantic-monkeys-2026-10-03).

## Fresh Soul of the General and The Mad Man (2026-10-03)

The fresh life2 career earns21 scent targets and the phantom through calculated
combat, followed by an unforced fang drop and the ordinary toxin/seven-powder
route. Both quests persist unpaid before a separate process grants five exact
bound rewards:728244,728245,723911 and two P7 souls. Final cold replay preserves
those items, prior belongings and all new quest receipts. Repeated stone pickup
and final claims are no-ops. The23 defeats use23 attacks; quest clocks and drop
rolls stay unmodified. Fresh coverage is8/19; eleven chains, pack opening and
fresh10931 remain open.
[Evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-soul-of-the-general-and-the-mad-man-2026-10-03).

## Fresh Ghost City and Tomboy (2026-10-03)

The fresh life2 career earns Ghost City's102 calculated defeats and five rescued
children, then Tomboy's ten Marbles and three jades through unforced drops/fusions.
Tomboy records28 defeats/28 attacks and four fusions, including one properly
consumed failed pair. Ghost's multi-visit snapshot query was corrected after an
interruption; exact reviewed hashes preserve its earned progress for ordinary
charm re-entry. Separate unpaid/paid processes verify six exact bound rewards,
saved receipts and duplicate refusal. The character earns level123. Fresh coverage
is10/19; nine chains, pack opening and fresh10931 remain open.
[Interruption, evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-ghost-city-and-tomboy-2026-10-03).

## Fresh Desert Blood and Hero's Wings (2026-10-03)

The fresh life2 career completes Desert Blood's200-kill hunt across a100-kill cold
checkpoint, normal respawns, unforced spell/fang drops and four Crypt visits.
Hero's Wings pays3 earned CPs for a Meteor, gathers unforced materials at its
randomly assigned site and earns five Hawk pairs under real deadlines. Both
unpaid quests reload before six exact bound rewards and cold duplicate refusal.
An exhausted-population fixture interruption preserves69 earned kills through
reviewed snapshot hashes; later counters cover continuation only. Fresh coverage
is12/19; seven chains, pack opening and fresh10931 remain open.
[Interruption, evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-desert-blood-and-heros-wings-2026-10-03).

## Fresh Family Feud and Desert Treasure (2026-10-03)

The fresh career earns Family Feud's hunt/token/encounter route and five accepted
puzzle jumps, then Desert Treasure's unforced vault/material route,99,999-silver
portrait, Jinx capture and replay-safe quiz. Five separate phases preserve41
Phoenix warehouse items and verify six exact bound rewards after unpaid and paid
restarts. Measured combat is89 defeats/95 attacks. Quest clocks and movement are
controlled; damage and random drops are not overridden. Fresh coverage is14/19;
five chains, pack opening and fresh10931 remain open.
[Evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-family-feud-and-desert-treasure-2026-10-03).


## Fresh Desert Expedition (2026-10-03)

The ordinary fresh career earns ten unforced Bullhorn drops, pays all six pill
health debits, lures three bulls into their traps and defeats the Bull Master
through calculated melee. An early hand-in is refused. Three separate processes
verify the unpaid state, exact three bound rewards (728250,723911,P7) and cold
claim replay with prior possessions intact. Combat totals34 defeats/68 attacks.
Fresh coverage is15/19; four chains, pack opening and fresh10931 remain open.
Quest time, travel and cooldowns are controlled; damage/drop RNG are unchanged.
[Evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-desert-expedition-2026-10-03).


## Fresh Power Book (2026-10-03)

The fresh character buys its Meteor for3 earned CPs, completes four owned floors
through107 calculated defeats/240 attacks, earns unforced materials, lures ten
monsters and burns ten saved final corpses. Purchase and report replay preserve
the snapshot. Unpaid restart, exact bound armor pack728251/P7 payout and cold
claim replay pass; earlier possessions persist. The route earns level124.
Fresh coverage is16/19; three chains, pack opening and fresh10931 remain open.
Travel, cooldowns and quest time are controlled; damage/drop RNG are unchanged.
[Evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-power-book-2026-10-03).


## Fresh Defense Talisman (2026-10-03)

The fresh career pays90000 silver for three nets with duplicate purchases refused,
then earns fishing, house, scheduled Shawn, desert and dungeon encounters through
21 calculated defeats/34 attacks. Four smelts reconstruct the tower. Unpaid
restart, exact bound202008/P7 payout and cold claim replay preserve prior property.
The runner stages hash-checked private house terrain and cleans up its additions.
Fresh coverage is17/19; two chains, pack opening and fresh10931 remain open.
Travel, cooldowns and quest time are controlled; native/live gates remain pending.
[Evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-defense-talisman-2026-10-03).


## Fresh Attack Talisman (2026-10-03)

The fresh career buys wine and a cave permit for10000 silver each, earns unforced
meat drops and completes cave, ambush, lake, Courtesan, pillar and garden stages.
Boxes are tried in order without reading the hidden winner. Calculated combat
is16 defeats/25 attacks. Unpaid restart, exact bound201009/P7 payout and cold claim
replay preserve earlier possessions and receipts. Final silver matches both fees
and900 stage rewards. Reviewed private terrain is staged for the prerequisite.
Fresh coverage is18/19; Tempest Wings, pack opening and fresh10931 remain open.
[Evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-attack-talisman-2026-10-03).


## Fresh Tempest Wings and aggregate (2026-10-03)

The fresh character buys50 Meteors for150 earned CPs, exchanges them for five
scrolls, pays the permit and defeats the owned Roc through four calculated hits.
Unpaid restart, exact bound204008/P7 payout and cold claim replay pass, preserving
earlier possessions (52 Phoenix warehouse items). Achievement10931 is absent
before the final claim, then has one correctly sourced/versioned evidence row and
its login achievement bit after restart. Fresh coverage is19/19 plus aggregate;
pack opening and native/live gates remain pending. Private terrain is hash-staged.
[Evidence and limits](CAREER_STARTING_BOUNDARY.md#fresh-tempest-wings-and-nineteen-chain-achievement-2026-10-03).


## Fresh earned reward-pack opening (2026-10-03)

All sixteen earned packs728236..728251 are withdrawn where needed and opened
through sealed ordinary item-use dispatch. For the actual Warrior25 character,
the sixteen exact bound equipment items plus Power Book's EXP item match their
saved output UIDs. Cold reload and replay of every consumed pack preserve all
seventeen outputs, prior possessions and aggregate10931 evidence. Thus the fresh
nineteen-chain batch includes pack consumption, not merely owning unopened packs.
Other-class outputs and native/live acceptance are not newly established here.
[Evidence and exact outputs](CAREER_STARTING_BOUNDARY.md#fresh-earned-equipment-pack-opening-2026-10-03).
