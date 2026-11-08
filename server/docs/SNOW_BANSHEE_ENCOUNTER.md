# Owned public Snow Banshee encounter

## Retained fight checkpoint and reward process recovery (2026-10-01)

The retained ordinary character4294907001 returns from floor6 through the
Guardian's Desert City exit, then takes the Grotto Envoy's ordinary floor2 route.
The public Snow Banshee starts at100M HP. Five normal ranged attacks leave
99,180,239 HP; the production five-second checkpoint saves that value before
an abrupt process exit. No HP amount, damage amount or victory is injected.

The next game/database process restores the same run and exact saved HP. Another
626 normal ranged hits earn the registered victory and10527. Exactly100 Study
Points and three saved uncollected drops are present before the next abrupt
exit. After restart, ordinary floor-pickup packets collect exactly three items,
including at least the guaranteed bound P7; repeated packets grant nothing.
A further cold process preserves the exact item UIDs, study, prior career and
completed reward records. Polling again reconstructs no claimed loot.

Passing phases: `career-snow-checkpoint`, `career-snow-win`, `career-snow-loot`,
`career-snow-reload`. Checkpoint: `revora_career_snow_probe` stage3 /
`CareerSnowSnapshot`. Candidate SHA-256:
`E7DCA61B62DBA49412DEE383A633D7AB4447C1B7C52AB5370F6B7006E20682E9`.
No production source or reward rules changed in this batch.

Snapshot comparison includes the run's identity/state/HP/outcome, not its
replacement lease, checkpoint sequence or occupancy-adjusted actor coordinates.
Those are intentionally reconstructed on startup. NPC/combat proximity and
attack cooldowns are controlled. Phase clocks use the initial stamp plus0/6,
10,20 and30 seconds; this keeps loot within its original saved deadline and does
not prove natural expiry or the thirty-minute respawn boundary. Damage and reward
draws use production calculations. Native presentation, fair combat and live
deployment remain unverified.

The receipt inventory rises to220 stored/117 absent identities by adding10527.
No Dragon Ball was collected in this run, so10523 is not claimed. Older receipts
remain mixed-provenance evidence, not220 independently earned achievements or an
overall completion percentage. Existing historical validation below retains its
original scope.

The source candidate now connects the reviewed Grotto site, persistent run,
combat and rewards into one ordinary public earning route. Startup initializes
the controller; the world tick maintains it. This supersedes the earlier
storage/reward documents' inactive-integration status. **It is not deployed or
genuine-client validated.** The separate Purple Banshee house rite remains dormant.

## Public lifecycle and ownership

The boss uses reserved family 9916805, name SnowBanshee and loaded IceQueen 4171's
mesh 951. It starts at 100,000,000 HP in Frozen Grotto 2, near (370,365), the
reviewed reachable clearing. An occupied recovery tile causes a bounded search
nearby, with the chosen location checkpointed before the actor appears. A spawn
announcement identifies the actual location.

One controller owns its actor and damage lifecycle. Canonical public map, actor
reference, ordinary player session and proximity are required. Registered
companions may fight and their nearby owner receives the kill; stale actors,
unregistered companions, replaced sessions and distant owners cannot claim it.
Coordinate comparisons use widened arithmetic. The legacy per-player monster AI
and movement do not drive this stationary encounter.

Health and position checkpoint every five seconds. Reconstructing the actor
preserves the run and last durable HP while replacing its lease. At most the
damage since the last successful checkpoint is lost after interruption. A failed
checkpoint retires the unsafe actor; a later tick reconstructs from durable
state. Old callbacks cannot complete or mutate its successor. An undefeated
boss persists rather than accumulating half-hour copies.

Victory records the exact killer and nearby current teammates, three loot
entitlements, each teammate's 100 Study Points, and the killer's eligible owned
1,000-spirit credit in one transaction. Any failure leaves the encounter alive
and unpaid. Only after commit is its actor retired and the client updated.
The persisted thirty-minute post-defeat timer is polled every five seconds.

Legacy Grotto NPC 899990 now directs hunters to this location; it cannot summon
another Ice Queen through its old hourly continuation. Golden War's separate
4171 summon and the dormant Purple Banshee item contract are not altered.

## Combat contract and evidence limits

The [official guide](https://co.99.com/guide/monsters/snow_banshee.shtml) supports
the named behaviors, clockwise hail rotation and freeze durations. Local spell
names are checked at level zero. Exact original spell variant mapping, numerical
formulas and client effects remain unverified. These are explicit Revora rules:

| Skill | ID | Candidate behavior |
| --- | --- | --- |
| IceBolt | 30010 | Single target within 19 tiles, calculated magic damage |
| FrostClaw | 30014 | Single target within 3 tiles, calculated melee damage |
| ArrowHail | 30013 | Six one-second pulses; 60-degree fan, 18 tiles; rotates 10 degrees clockwise per pulse |
| IceBurst | 10373 | Six-tile circle around the boss; calculated magic damage and five-second freeze |
| IceShock | 30011 | Four-tile circle at the warned target location; calculated magic damage and three-second freeze |
| Avalanche | 30012 | 60-degree fan, 18 tiles; calculated melee damage |
| Metaplasia | 10372 | Heal 2% of maximum HP, capped at maximum |

Each skill has a two-second warning and four-second recovery; players can escape
the locked target area or fan. Raw level 138, attack 10,000 and defense 40,000
follow the local IceQueen values. Magic attack 7,500 and magic defense 200 are
candidate Revora tuning. Existing damage calculations and Warrior counters apply;
a counter that kills the boss cancels its remaining damage.

Encounter-owned freezes gate normal movement, jump handlers, attack admission and
damage dispatch. Their timers, player eligibility and actor generation determine
validity; release clears owned effects without removing another feature's Freeze
flag. Existing Freeze and SpellUse serializers provide presentation, not proof
of native acceptance. All seven visual effects and full long HP display still
need genuine 6711 review.

## Rewards and validation

The [persisted reward contract](SNOW_BANSHEE_REWARDS.md) remains two weighted drops
plus an extra bound P7. All weighted P7 drops are also bound; +2 Stones retain
Plus=2. This public event grants a registered 1,000-spirit bonus only to the killer
with an eligible active daily bead. It does not alias arbitrary legacy
SnowBansheeSoul/PurpleBanshee deaths or add P7 to daily quest completion.

The isolated build and **60 encounter checks**, **79 Sword Master checks**,
**617 Soul Ball checks** and **15 packet checks** pass. The encounter fixture
uses ordinary sessions, actual Daily Envoy bead issuance, production damage/death
dispatch and pickup, and production character/item reload. It covers all seven
skills, freeze/jump/attack enforcement and expiry, saved HP recovery, old callbacks,
out-of-range arithmetic, concurrent kills, full reward/spirit rollback and retry,
nearby team rewards, companion identity/range, occupied spawning and failed
checkpoint recovery. Earlier storage (33), reward (28) and travel (106) fixtures
cover their separate contracts; those counts are not new runs in this batch.

Fixture-only setup supplies player positioning, team formation and lethal damage
values. Recovery is controller reconstruction, not a live Host restart. No test
claims a genuine-client fight. The existing ordinary Envoy-to-Grotto walk was
validated separately. The 1,000-spirit source is checked alongside the shared
normal bead grade progression, and daily completions still have no P7 reward.

Ignored logs under `obj/chi-refill-validation/`: `snow-banshee-encounter-tests.log`,
`snow-banshee-integrated-sword-tests.log`, `snow-banshee-integrated-soul-tests.log`
and `snow-banshee-integrated-packet-tests.log`. Server build SHA-256:
`D18D3F94C2943F4F9B65F25DFA85A8D759EB57D0AD069B1823015B87595DFDC1`.
Only disposable fixture state was written. No live server or installed client
was changed. Native combat/HP/effects/loot and live Host restart/rollback remain
open; the combined Snow/Purple Banshee milestone is not complete.


## Retained DragonBall pickup and cold recovery (2026-10-01)

The same retained ordinary character continues from the equipment-upgrade
checkpoint, buys a Desert City scroll for its catalog 200 silver, uses it and
enters floor 2 through the Grotto Envoy. Its existing earned bow defeats the
100M-HP Snow Banshee with 1,684 normal ranged attacks. No damage, HP, weapon,
victory or loot item is supplied. The controlled loot draw selects the two
DragonBall outcomes; the third drop is the guaranteed bound P7. The victory
awards exactly 100 Study Points.

`career-retained-snowloot-win`, `career-retained-snowloot-loot` and
`career-retained-snowloot-reload` pass against unchanged private game SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Separate game/database restarts precede pickup and final verification. Ordinary
floor-pickup packets earn 10523 and preserve exact item identities, ownership,
binding, Study Points and the prior career. Duplicate pickups leave the complete
snapshot unchanged. Each collected item joins its saved loot row on UID, type
and owner; another reward poll grants nothing. Final checkpoint:
`revora_retained_snowloot_probe`, step 2, `RetainedSnowLootSnapshot`.

Fixture corrections: the first attempt incorrectly addressed the floor-6
Guardian from Twin City; it failed before travel payment. The corrected route
paid once, but the initial combat request used melee while a bow was equipped.
That attempt exhausted its bound without a victory. A read-only comparison
proved only the 200-silver fare and selected active run differed from the prior
checkpoint; recovery preserved those changes and did not buy another scroll.
A temporary diagnostic path also failed before further player mutation and was
removed. Successful combat uses the equipped weapon's ranged attack.

Time, approach, attack cooldown and loot outcomes are controlled. The retained
character's earlier eligibility/funding retains its documented provenance.
This is a specific earned-drop and persistence check, not a claim about natural
loot odds, combat balance, full socket authentication or native client behavior.
The earlier fight checkpoint test remains separate; these successful phases use
graceful process stops. No production gameplay change or deployment occurred.


## Unclaimed loot deadline across processes (2026-10-03)

`career-snow-expiry-earn` and `career-snow-expiry-reload` pass against unchanged
private game SHA256 `943AA1FBE2BF19433045F4CAF5CDA0FE4BCBEF2EC1EFAE9CD703995346FC6435`.
The dedicated ordinary, non-GM profile4294918601 receives a production victory
contract containing two +2 stones and one bound P7. The fixture supplies the
lethal callback and controls the UTC clock and loot draw; it does not establish
an earned combat route, natural timing or another completed quest.

All three drops are unexpired one tick before the two-minute deadline and
expired exactly at it. Normal floor-pickup packets and direct managed pickup
at that boundary leave inventory, purse, Study Points and all loot receipts
unchanged. Reward polling removes the three ground projections; stale-object
pickup still grants nothing. The three records remain uncollected with null
item UIDs, while the legitimately recorded100 Study Points remain paid once.

After a separate game/database restart, the fixture reloads the ordinary
profile, sets the controlled deadline, clears startup's real-clock ground
projection and invokes production reconstruction twice. No expired drop
returns; inventory stays empty and the exact saved item/currency/Study/loot
snapshot is unchanged. This explicitly tests reconstruction at the expired
clock, not clock injection before Host startup. The earlier retained earned
combat/pickup evidence remains separate and unchanged.

Evidence: `tools/CheckCandidateSnowExpiry.cs`, probe
`revora_snow_expiry_probe`, and private logs
`C:/Revora/DisposablePostgres/moon-box-candidate/arena-process-career-snow-expiry-{earn,reload}.log`.
No production source, client or live deployment changed. Genuine-client
presentation, natural clock observation and live release gates remain open.


## Fresh career checkpoint and paid equipment recovery (2026-10-03)

The ordinary level131 Warrior1096322 continues the earned Dragon Slayer/Terato
career with all three warehouses loaded. The Grotto Envoy route admits it to
map1927 without GM status. Five calculated melee attacks leave99,994,885 of
100M HP; the encounter persists that partial HP and restores it after a separate
game/database process. No boss HP, player statistics or victory is seeded.

The first finishing attempt hit its15,000-attack fixture bound. A subsequent
inspection proves exact equality with the saved career/run snapshot, stage1,
99,994,885 saved HP and no Snow loot or Study receipts. Four equipped items were
already broken: blade410009, shield900009, ring150009 and fan201009. The character
still carried its legitimately earned low-level starter gear; the level140 epic
fists remained ineligible. The interrupted attempt is not a completed fight.

The repair prerequisite uses ordinary warehouse deposits, shop2888 Meteor
purchases as needed, unequip/repair/equip requests, and the saved earned purse.
The four broken items consume five Meteors apiece. Replaying each repair at full
durability changes neither the snapshot nor the purse. Each successful repair
has a saved checkpoint, and a separate cold process verifies all four equipped
items at full durability, their repair receipts and the full prior career.
A fixture return-route error after the fourth repair was corrected to the
Desert City Envoy; it resumed step4 without repeating any paid repair.

Evidence: `tools/CheckCandidateFreshSnow.cs`,
`revora_fresh_snow_probe`, `revora_fresh_snow_repair_probe`, and private
`arena-process-career-fresh-snow-{checkpoint,inspect,repair,repair-reload}.log`.
The initial failed combat log is retained separately as
`arena-process-career-fresh-snow-win-initial-failure.log`.
This section proves partial-HP recovery and the repair prerequisite, not the
subsequent victory or its rewards. NPC/combat approach, cooldown and phase clocks
are controlled; natural duration, retaliation/balance and native6711 acceptance
remain separate gates. Production source and the live deployment are unchanged.


The repaired15,000-attack attempt dealt75,363,370 damage, leaving24,631,515 HP,
but still hit the old bound. Its frozen phase clock prevented periodic HP saves
while attacking. The follow-up JSON audit found only four durability decreases
(repeated in three nested item inventories) and controlled X/Y changes; there
were no currency, reward or saved boss-HP differences. The recovery accepts only
these exact reviewed snapshot hashes, then preserves the real saved boss HP:

- Before: `46dddb2892057e053342a3182b323676d7c5aec8bba0b82c4a80c787993b24a9`.
- After: `037807defbf0a45405d1ecc1a06cb4f898b9184d4e932144aa3e9e2f78bb6b32`.

The corrected `career-fresh-snow-combat` fixture replaces the single-process
finishing phase. It limits each process to5,000 attacks, advances the
private encounter clock and calls the production checkpoint every1,000 attacks,
and saves the character/run snapshot at the segment boundary. Maintenance uses
ordinary repairs before gear breaks, with silver or purchased Meteors actually
consumed. It does not restore discarded combat damage, alter statistics, weaken
the encounter or inject achievement/reward records. Only a real defeat advances
the reward stage. The two interrupted runs remain failed evidence.


## Fresh maintained victory and cold reward recovery (2026-10-03)

All four bounded combat segments pass, restoring the exact preceding HP and
career before continuing. Their saved HP is74,873,604,49,752,488,24,631,482 and0.
They use5,000/5,000/5,000/4,904 calculated melee attacks:19,904 total on the
successful segmented path, additional to the five initial checkpoint attacks.
The two earlier interrupted15,000-attack runs are not included in that total.
The character stays alive, uses no GM access and pays for equipment maintenance.

The real defeat records10527 and100 Study Points once, plus three uncollected
loot records. After a process/database restart, ordinary sealed floor-pickup
requests collect all three saved rewards. Each request is repeated; inventory
grows by exactly three items. At least one drop is P7, and every P7 drawn is bound.
The next cold process preserves exact reward UIDs, binding, Study Points, boss
outcome and the nested prior career. Repeated reward polling creates no claimed
loot, and all three collector records belong to the ordinary character.

Passing phases: `career-fresh-snow-checkpoint`, paid `repair`/`repair-reload`,
reviewed `recover`, four `combat` segments, `loot` and `reload` (all phase names
use the `career-fresh-snow-` prefix). Inspection phases diagnose the failed runs;
they are not victory evidence. Final `revora_fresh_snow_probe` stage3 and
`revora_fresh_snow_combat_probe` attacks19904/segments4 preserve the checkpoint.
Private combat logs are retained individually as
`arena-process-career-fresh-snow-combat-{1,2,3,4}.log`.

The candidate game SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
Only validation tooling and documentation changed. This closes the fresh
server-side earning/recovery milestone, not natural fight duration, retaliation,
class balance, native client presentation or live release. The post-Flame receipt
audit predates this event and must not be treated as the current receipt count.

## Healing and attack-rate feasibility (2026-10-04)

The combat timing values are now named constants used by the existing production
rotation; their values and gameplay behavior are unchanged. With continuous
eligible targets and callbacks exactly on time, seven two-second warnings,
seven four-second recoveries and ArrowHail's five additional one-second gaps
give47 seconds between heals. Phase6 restores up to2,000,000 HP. Delayed world
callbacks can lengthen this interval;47 seconds models the fastest healing.

At one ordinary non-magic hit every500ms without Infinity, these conditional
per-player damage floors produce positive progress against that healing:

| Simultaneous attackers | Attacking continuously | Attacking half the time |
| --- | ---: | ---: |
| 1 | 21,277 per hit | 42,554 per hit |
| 2 | 10,639 per hit | 21,277 per hit |
| 5 | 4,256 per hit | 8,511 per hit |

The floor is strictly above break-even. Five fixed5,000-damage attackers at
full uptime gain only350,000 damage per47-second cycle after healing; at half
uptime they lose progress. This does not model a particular character's damage
rolls, criticals, buffs, movement, freezes, healing supplies or repair stops.
It establishes why a reward-only accelerated victory cannot prove natural
combat feasibility. It does not establish that the public boss is impossible
or authorize reducing its HP/healing. Public participation is not limited to
one five-player team; the nearby winner's reward party is capped separately.

Run `tests/CombatBalance/Verify-Combat.ps1 -ArtifactRoot
obj/counter-validation/bin -SnowHealingOnly`. The isolated build and nine focused
arithmetic checks pass against assembly SHA-256
`8849AC59E9C39FD13BDEFA49F33FA8A0C916E70CA9606A8ACB1E57B754A7F4C2`.
The default combat suite includes the checks. These fixtures read production
timing constants without opening a database or awarding anything. Actual combat
acceptance still needs an ordinarily equipped profile or team that sustains the
required damage while surviving the attack cycle; prior reward/reload evidence
remains valid. No live build was deployed.

### Saved ordinary loadout check

The private `career-fresh-arena-classes-mentor-snow-readiness` phase loads the
existing earned character1096322 and verifies its complete current career
snapshot remains unchanged. It is level133, Archer41, with19,664 maximum HP,
358 BP,5,876 minimum attack,6,633 physical fan contribution and180% Dragon gems.
However, its equipped weapon is blade410009. A separate read-only query of all
owned `items` rows in the500000..500999 bow range returns no rows, including
warehouses. No equipment or balance was altered.

The35,025 ranged arithmetic estimate from those current stats is hypothetical:
the ordinary ranged dispatcher requires a bow (or another supported ranged
weapon), and equipping one changes the profile. It must not be used to certify
this character's fight. The next prerequisite is an ordinary purchased/equipped
bow and suitable arrows, followed by recalculation and sustained combat checks.
Preserve the prior loot victory; do not repeat it to address this loadout gap.
The [readiness result](research/FRESH_SNOW_READINESS.json) records source and log
hashes, the empty owned-bow result and the explicit incomplete combat status.

### Ordinary funded Archer loadout and cold recovery

The loadout gap above is now closed for fresh UID1096322. Normal warehouse
deposits made space; quoted RevoraMaster peak training and Archer vestments cost
65,000 earned CPs in total. Normal attribute allocation and equipment handlers
equipped all seven purchased items. Repeated confirmations created no extra
charge or items. No character level, attributes, equipment or purse was seeded.
The peak service promotes directly; this is not proof of intermediate trainer
achievements, which remain covered by their separate retained route.

The private earn/reload phases preserve the complete prior career and all earlier
achievement identities. Both confirm level140/Archer45, rebirth2 unchanged,
30,163 maximum HP, minimum/maximum attack25,571/26,563, physical fan7,733 and
180% Dragon gems. The same bound bow62312163 (type500429) and arrows62312164
(type1050002) survive restart. All six non-arrow set pieces retain+12, -7,
two Super Dragon Gems and permanent P7 purification.

The first attempt's bag-space guard stopped before payment. A later preliminary
cold report preserved all durable state but understated stats because the test
loader calculated equipment before loading JiangHu. The scoped harness now
refreshes stats after that load and asserts the earned values exactly. Earlier
logs are retained; no saved checkpoint or gameplay stat was overwritten to make
this comparison pass. The final reload also verifies every purchased item's
binding and enhancements.

The resulting unbuffed ranged arithmetic estimate is93,251 per hit, above the
42,554 half-uptime solo healing threshold. This is still not a measured attack
or survival proof. Continue with actual timed damage, retaliation, ammunition
and maintenance on this saved loadout; do not buy the set again.
Evidence: [paid loadout](research/FRESH_SNOW_LOADOUT.json), phases
`career-fresh-arena-classes-mentor-snow-loadout-{earn,reload}` and private
`revora_fresh_snow_loadout_probe` stage3. No live deployment or native acceptance.

### Timed earned solo victory and arrow-wear correction

The paid Archer loadout now defeats a fresh100M-HP boss while its production
healing/attack rotation advances every500ms. Normal ranged requests occupy at
most one slot each; freezes suppress outgoing attacks, and medicine would replace
an attack slot. Approach coordinates and clock/cooldown translation are controlled;
this is server-side simulated timing, not a native-client stopwatch result.

The run lasts324 steps (162 simulated seconds), with306 attack requests. The
boss heals6M HP during the fight. The character receives3 HP damage and remains
alive at23,960 HP; its stronger maximum HP is30,163, but the run does not refill
the initial23,963 HP. Normal pharmacy purchases added77 medicines for77,000
silver; none were needed. All non-arrow equipment retained positive durability,
so no repair or resupply stop was necessary. The saved victory names this UID,
pays100 Study Points and creates three loot entitlements. A separate cold process
preserves the entire nested career, outcome, reward records and paid study.
Prior floor-pickup/replay validation is reused; this run does not claim a new
pickup test or a new achievement identity for an already completed kill.

The first combat process stopped after victory on an overly narrow final damage
assertion. It counted105,999,995 HP during player-attack windows and6M healing,
leaving5 HP of additional loss unmeasured. The owned Reflect3060 skill can damage
the boss in enemy callbacks; this attribution is an inference, not a per-hit
trace. The exact measured totals and failed log remain intact. Cold validation
accepted the existing durable victory and paid reward rather than replaying it
or replacing its damage counters. The harness now counts enemy-callback boss
damage for future runs. The5-HP historical residual remains explicit.

Cold inspection also found the arrows worn from1 durability to0. Defensive
durability incorrectly excluded arrows only if their slot was not5, although
arrows occupy slot5. The corrected guard excludes arrows from armour wear,
matching the existing outgoing-attack exclusion. An offline regression invokes
the real durability routine100 times for each of three arrow types. The old
assembly attempts an arrow write and fails the deliberately invalid connection
string guard before any database/network access; the corrected build passes all
nine checks. Run `tests/CombatBalance/Verify-Combat.ps1 -ArtifactRoot
obj/counter-validation/bin -ArrowWearOnly`.

The isolated corrected server SHA-256 is
`BF6B3C12681CCCB1C0C48823D03882F6AFE8996C9745AE214788A6B0551DE44D`.
It has not replaced the private or live server. The private character's depleted
arrows remain preserved as evidence; no free replacement was supplied.
See [timed combat evidence](research/FRESH_SNOW_TIMED_COMBAT.json).
This closes representative earned Archer feasibility with the disclosed
accounting limit; native timing, other classes and coordinated release remain open.
