# Unarmed monster victory: transactional boss routes

Installed 6711 achievement 10525 requires killing a level-120-or-higher monster
without a weapon. Achievement.dat SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
Its wording does not restrict the attack skill. This implementation checks the
actual killer and active equipment at the owned victory transaction.

EquipmentAchievements.RecordUnarmedVictory is integrated into Snow Banshee,
Sword Master, Terato, Purple Banshee, Exorcism Spook, Dragon Slayer, Dis City, summoned Abyss and the Ancient Devil
boss battle and Trojan epic combat victory transactions. It requires a canonical ordinary
session, the player's own entity as killer, a monster level of at least 120,
and empty active right/left hand slots in both cache and saved item rows. With
alternate equipment active, slots 24/25 are checked instead of 4/5; weapons in
the inactive set do not block credit. A hand-slot item, including a shield,
blocks the empty-hands qualification. Companion kills do not qualify.

The caller owns encounter identity, range, party membership, live actor and
single-victory proof; the helper is not a general death-packet endpoint. Evidence
10525 is saved in the same transaction as victory, reward entitlements and other
kill achievements. Failed evidence aborts the whole victory and allows a later
reconnect/retry. Terato now acquires the character row lock before victory writes,
matching the equipment/purse ownership order used by the other two encounters.

Validation: isolated Debug/x86 build, 116 Snow Banshee checks, 329 Grotto checks
and 97 Sword Master checks. Snow covers right/left weapons, a saved weapon absent
from cache, alternate/inactive sets, levels 119/120, companion exclusion, failed
evidence rollback/retry and full production reload. Terato's ordinary admitted
killer earns credit while the non-killing summoner does not; Sword Master checks
both kill and unarmed evidence after reload. Combat time/damage and equipment
setup are controlled fixtures, not native balance acceptance. Five source audit
tests pass; source inventory is 281/337 with 56 missing identities.

This is a playable source route, not universal coverage: ordinary wilderness
death handling and other managed encounters still need equivalent authoritative
victory integration. Keep that expansion and native/live acceptance open in the
roadmap. No live deployment or client patch occurred; unrelated owner edits were
present in the isolated build.

## Purple Banshee and Spook expansion

Purple Banshee and Spook use their existing owner locks and victory transactions.
Spook grades are level 90, 120 and 140: only grades two and three can qualify.
Nearby party quest/reward entitlement and companion victory attribution do not
substitute for the player's own unarmed lethal hit. Paid Exorcism completion
cannot grant either kill achievement.

Validation: isolated Debug/x86 build, 103 Purple Banshee encounter checks,
652 Exorcism quest checks and five source audit tests. Both suites exercise
ordinary killer credit, party exclusions, saved evidence rollback and retry;
Purple includes companion exclusion and a full reward/evidence reload. Spook
checks all three level grades before and after reload and a level-120 companion
kill. Suppressing Spook's 10525 insert rolls back its earlier 10529 insert,
victory and daily progress, then a full character reload resumes the same hunt.
Purple rollback retry uses a fixture-only fence reset; it does not claim a
reconnected house owner can adopt an old actor.

Concurrent starter-pack edits initially interrupted compilation. The successful
server build includes unrelated working-tree changes; the Exorcism harness uses
temporary HEAD copies of StarterPackTests.cs and its PotencyHuntTests.cs entry
point to avoid unfinished unrelated fixture calls. No starter-pack files were
modified or included in this batch. Logs are ignored local artifacts:
obj/unarmed-more-build.log, obj/unarmed-purple-tests.log and
obj/unarmed-spook-tests.log. Source inventory remains 281/337; expanding an
existing achievement does not increase the count. Remaining encounters,
wilderness deaths and native/live validation are still open.

## Dragon Slayer expansion

The owned level-128 Dragon Slayer minions and ghouls now record 10525 with their
existing per-run/per-slot kill receipt and phase/material changes. The existing
character row and inventory locks protect the hand-slot check. Achievement
projection refreshes after the transaction commits. No extra reward item,
donation, GM flag or private allowlist is needed.

Validation: isolated Debug/x86 build and 341 Grotto checks (12 added), plus five
source audit tests. Actual lethal attack dispatch checks empty hands and an
active weapon. A suppressed 10525 insert preserves the living monster and rolls
back both kill receipt and objective; production character/item reload resumes
the hunt and permits one successful retry. Corpse replay, companion exclusion,
partial-hunt reconnect and achievement persistence are covered. The existing
Terato assertion now preserves the summoner's earlier unarmed achievement while
still proving the Terato kill identity belongs only to its actual killer.

The Grotto suite used the same unrelated starter-fixture isolation described
above. Logs: obj/unarmed-dragon-build.log and obj/unarmed-dragon-tests.log.
No live deployment occurred. Other managed encounters and ordinary wilderness
kills still need integration; this is not a completed universal kill route.

## Dis City expansion

Dis City now saves unarmed evidence in the owned defeat transaction alongside
its step receipt, progress, collected quest items and final ranking result.
Run upserts and step inserts must each affect one row; a suppressed write now
throws and rolls back the transaction instead of acknowledging unsaved progress.
The character row is locked before the run and item rows. The existing ordinary
player, instance identity, range, expiry and ordered-guardian checks still apply.
Achievement projection refreshes only after a committed victory.

Spawned monster profiles now carry the same level already assigned to their
visible entity: 120 for ordinary enemies/guardians, 140 for Ultimate Pluto.
Previously the copied catalog profile retained a potentially different level.
Combat code reading that profile now sees the event's assigned level; genuine
client combat/balance acceptance remains pending.

Focused assertions cover profile/entity agreement, failed-evidence rollback of
progress/items/step receipt with a living monster, retry through lethal combat,
a fresh achievement projection reading one durable receipt after corpse replay,
and armed kills progressing without unarmed credit. Retry uses a fixture fence
reset; the persistence assertion reloads the achievement object, not a complete
character session. Do not describe it as full character-reconnect acceptance.
The suite also exercises existing stage, ranking, reward and instance recovery.
Logs: obj/unarmed-dis-build.log and obj/unarmed-dis-tests.log. The same ignored
starter-fixture isolation described above is used; no live deployment occurred.

Validation passed: isolated Debug/x86 build, 209 Dis City checks (seven added)
and five source audit tests. Producer inventory remains 281/337.

## Abyss and remaining encounter classification

Summoned Abyss encounters explicitly set level 120 and now save unarmed credit
in the existing defeat transaction. AbyssTrialStore.Require already acquires the
character row lock. The evidence commits with encounter defeat, stage progress,
pending spoils and final quest completion; the existing post-commit refresh
projects the result. Companion attribution for quest rewards still does not
qualify as a direct unarmed player kill. Ordinary Abyss wilderness deaths remain
outside this change.

The world suite covers suppressed unarmed insertion, rollback of paid defeat
and spoils, full production reload and resumption of the original encounter,
full-bag reward recovery, companion exclusion, and full completion reload with
both 11031 and 10525. Death-callback fixtures and production combat admission
are server evidence, not native client balance acceptance.

A proposed Planet integration was rejected by installed-catalog evidence:
all three guards are level **112**, verified from their spawned entities and
profiles. The full Planet suite now asserts that exclusion. No Planet gameplay
code, monster level, or reward was changed. HeartEncounter.Spawn copies the
same 1413 template without overriding its level: its level-112 exclusion is a
source inference from the verified shared template, not a separate Heart run.

| Remaining path | Current evidence and next work |
| --- | --- |
| Sky Pass, Snake Islands, Labyrinth, Moon Box, Power Arena | TryDie invokes DieCore before opening the quest credit transaction. Do not append a pre-death achievement or claim atomic kill recovery; review eligible levels and move eligible kill credit behind a recoverable defeat boundary. |
| Demon Exterminators | Same post-death ordering; shared kill also feeds Summer and Planet hunts. Preserve those listeners when introducing recoverable qualifying kill evidence. |
| Ancient Devil boss | Integrated below: level-100 guards are excluded; the level-120 final boss can qualify. |
| Ordinary Abyss deaths | WildDeath has life identity/receipts, but OnWildDeath runs after death. Completion eligibility and retry semantics still need review. |
| Other owned encounters and generic wilderness | Not certified by this classification; keep the roadmap item open. |

Logs: obj/unarmed-abyss-build.log, obj/unarmed-abyss-tests.log,
obj/unarmed-planet-build.log and obj/unarmed-planet-tests.log. Fixtures use the
previously documented unrelated starter-test isolation. No live deployment.

Validation passed: 1,518 Abyss world checks, 123 Planet checks plus nine child
restart checks, and five source audit tests. Inventory remains 281/337; no new
achievement identity was invented by expanding the existing unarmed route.

## Ancient Devil final boss

AncientDevilBossWorld now records 10525 inside the existing paid battle defeat
transaction. AncientDevilBattles.Defeat already acquires the character row lock
through AncientDevilStore.Owner, checks the saved generation/actor/slot, and
requires all four guards before final victory. The shared helper excludes the
level-100 guards. The level-120 boss grants only a qualifying direct player kill.
No offering price, item reward recipe, monster level or drop rate was changed.

Validation: isolated Debug/x86 build, 286 Ancient boss world checks (five added)
and five source audit tests. The ordinary earned-nine-amulet route checks no
credit from guards, suppresses only the 10525 insert at final lethal combat,
and verifies rollback of the final defeat and reward while retaining guard
progress. Full production reload restores the same paid summon with only its
boss alive; a lethal attack retries successfully, refreshes the achievement,
and subsequent reward reload/duplicate claim retains it.

Logs: obj/unarmed-ancient-build.log and obj/unarmed-ancient-tests.log. Shared
helper weapon/companion cases are covered by the earlier suites; this batch does
not add a separate Ancient companion acceptance claim. Remaining ordinary
wilderness/older post-death paths and native/live gates stay open. The build
includes unrelated owner work; none of that work is part of this commit.

## Trojan epic combat

Ghost Reaver, Evil Monk Misery and Furious Flame Devastator are explicitly level
120 in TrojanEpicCombat.Spawn. Its registered death path now calls a distinct
store Defeat entry point carrying the original monster and killer. The common
advance transaction still checks character state, story revision, active
encounter and context before committing. Unarmed evidence joins the victory,
step and daily-energy writes. NPC-only Advance calls carry no combat identity
and cannot generate unarmed credit. The existing Solar Blade ownership condition
and epic reward route are unchanged.

Focused validation suppresses 10525 at the first combat victory, reads durable
revision/energy while the session is fenced, verifies no saved victory, then
retries actual lethal damage. The full story later reloads the character and
retains the earned achievement. The retry uses a fixture-only fence reset;
full reconnect is tested after committed victory, not during that retry.
Shared helper weapon/companion checks remain covered in earlier encounter
suites; this batch does not claim a separate Trojan companion test.

WarriorEpicCombat explicitly spawns level 70/72 enemies, so those two encounters
are excluded by source evidence. No Warrior levels or rewards were changed.
Trojan initialization is present in Thread.cs through TrojanEpicQuest; the
store's outdated pre-integration comment was corrected. This source check does
not prove the currently running server has the new build.

Logs: obj/unarmed-trojan-build.log and obj/unarmed-trojan-tests.log. The build
includes unrelated owner edits and uses the documented starter-fixture isolation.
Native/live acceptance and remaining kill handlers stay open.

Validation passed: isolated Debug/x86 build and 1,805 Trojan storage/world/NPC
checks, including four added assertions, plus five source audits. Producer
inventory remains 281/337; this extends an existing achievement route.

## Elimination exclusion

EliminationRescueBindings.Profile and EliminationDenBindings.Profile explicitly
assign level 42 to Rock Behemoth, Rock Lackey and Rock Leader. The world spawn
methods use those profile levels. They do not qualify for 10525; no levels,
rewards or encounter code were changed. This is source inspection, not a fresh
Elimination runtime test. Remaining qualifying wilderness paths stay open.

## Four daily world bosses (2026-10-02)

Moonfang King, Dawnfire King, Sunscar Tyrant and Nightveil King now include
10525 in the existing durable victory transaction, alongside the saved killer
and four bound-P7 loot choices. The source key is `daily-boss:YYYY-MM-DD:slot`;
the slash in the scheduling key is normalized to the evidence token alphabet.
The ordinary direct killer must have empty active hand slots in both loaded
and saved equipment. An inactive-bank weapon does not prevent qualification.
The shared helper still excludes companion killers and noncanonical sessions.

The spawn now sets both the monster profile and displayed entity level to140.
Previously the display was140 while the profile retained its template level.
The 100M HP, attack range, schedule, bounty and four soul drops are preserved.
Victory acquires the player's item lock without waiting under the boss gate,
then the saved character row before checking equipment and committing. A failed
credit insert rolls back victory and all four loot choices; the living boss
can be retried. Committed evidence invalidates the loaded achievement cache and
sends an updated packet when the item lock is available. Otherwise a later
normal refresh/login reads the committed receipt. Bounty delivery remains the
existing separately recoverable saved award.

Validation: isolated Debug/x86 build (existing warnings retained), followed by
191 checks covering the new daily-boss cases, existing KO suite and existing
daily-boss schedule/HP/bounty/floor-loot suite. New coverage includes suppressed
10525 rollback and retry, item-lock contention, saved weapon/cache disagreement,
inactive-bank eligibility, exact purses, duplicate death/payment, production
character reload and expiry refusal. Tests use ordinary sessions but controlled
lethal damage, positions and calendar; this is not native combat/balance or a
new full process-restart acceptance test. The four profile levels are checked.

`tests/EquipmentAchievements/compile_daily_unarmed.py` compiles the focused
runner with warnings as errors, using committed shared fixtures so unrelated
owner edits are excluded. `DailyUnarmed.Tests.exe` requires a secret supplied
through `REVORA_QUARANTINE_TEST_CONNECTION`, and rejects anything except
127.0.0.1:25439 / revora_candidate_game. It creates and removes only its named
`daily_unarmed_fixture` schema. The legacy suite's zero purse defaults are
reproduced inside that schema after the new cases. No live data or retained
career checkpoint is modified. PostgreSQL is stopped after validation.

Build SHA256: `E85FAE6CAD61A46AB6B824E47E20F79C03136BA60B10BEDDE8AABAF10E9700B3`.
Ignored logs: `obj/unarmed-daily-build.log`, `obj/unarmed-daily-tests.log`.
The test wrapper's initial stderr handling interrupted cleanup; the exact owned
fixture schema was removed before retry and the wrapper now waits for exit.
The first test also exposed and corrected the receipt-key slash and missing
immediate achievement refresh. Final191 checks pass on the final assembly.

Ordinary wilderness and the other post-death quest listeners listed above still
need recoverable victory integration. This closes only the four daily-boss
routes; it does not close universal coverage, native acceptance or deployment.

## Ordinary wilderness death boundary (2026-10-02)

The fallback in `Entity.Die(Entity)` now routes ordinary registered monsters
through `WildMonsterVictory`. Managed encounter handlers still intercept first
and retain their own `DieCore` behavior. The new route holds the monster-life
and direct killer's item locks without waiting on a competing owner lock. It
checks the canonical ordinary session, map registration, level120+, direct
player killer, companion/guard/reviver exclusions and empty active hands in
both cache and saved items. Inactive-bank weapons do not block qualification.

The credit transaction occurs inside the death core after its cancelling
branches, immediately before terminal death effects and kill counters. It
stores a random monster-life identity, killer and profile with10525 in the same
transaction. A suppressed write rolls back both; HP and kill counters remain
unchanged for a later lethal retry. This is an accepted logical defeat boundary,
not an achievement granted merely for attacking a living monster.

If commit acknowledgement is lost, the same in-memory life identity resolves
the stored decision. A second ordinary killer or an ineligible caller cannot
claim that pending life. The original killer can finish the acknowledged death
without rechecking equipment against a later loadout: the original empty-hand
decision is already committed. Respawn creates a new life identity; repeated
death calls do not repeat kill counters or evidence. A session cache avoids
new database writes/reads for subsequent kills after this achievement is known
complete. The ledger is only needed for a character's first qualifying award.

Committed achievement updates are sent immediately under the existing owner
lock. A refresh failure fences the session for reload, preserving the durable
award. The life lock/receipt do not make generic floor loot or every legacy
quest side effect transactional. A full process restart still uses existing
world repopulation; this does not claim exact corpse/loot recovery across a
crash between the committed logical defeat and in-memory death effects.

Validation: Debug/x86 build with existing warnings, focused fixture compilation
with warnings as errors, and211 private checks (wilderness plus existing daily
boss/KO regressions). Wilderness cases cover nonlethal exclusion, suppressed
credit rollback, lethal retry, immediate projection, character reload, duplicate
death, respawn, two concurrent killers, missing commit acknowledgement, later
weapon changes, busy inventory, saved weapon/cache mismatch, inactive bank and
level119 exclusion. Companion/guard/unregistered exclusions include admission
checks; they are not new end-to-end pet-combat acceptance. Damage, coordinates
and schedules are controlled. No native or full process-restart claim is added.

The fixture's first compile caught two incorrect integer declarations; these
were fixed before the new checks ran. The final211 checks use assembly SHA256
`05A3FD49B8C5E3E64A2B66CD371D0241A4E10F55689EE0A5FD5134A071A37DCD`.
Build log: `obj/unarmed-wild-build.log`; suite log:
`obj/unarmed-daily-tests.log`. The existing bounded runner/schema from the
previous section is reused; the disposable database is stopped afterward.
No production deployment, client release or retained-career mutation occurred.

Remaining implementation work is the managed post-death quest paths in the
classification above (Sky Pass, Snake Islands, Labyrinth, Moon Box, Power Arena,
Demon Exterminators and any other qualifying intercepted encounters). They must
integrate with their own victory/progress recovery before universal coverage
can be checked off. The ordinary fallback is now implemented; native/live gates
remain open separately.

## Power Arena counter and unarmed credit (2026-10-02)

Power Arena's configured guardians on floors5?7 are level120/125/130. Their
accepted-life transaction now saves the hunt counter and eligible10525 together.
A counter failure leaves the guardian alive and grants no achievement; a failed
achievement insertion also rolls back the counter. Retry acknowledges one saved
life, including when the commit succeeded but its acknowledgement was lost.
Already-earned10525 does not skip later counters, and a full floor quota creates
no redundant progress ledger rows. Floors1?4 remain below120 and retain their
existing counter path. Their legacy post-death failure semantics are not claimed
to have changed in this batch.

`WildMonsterVictory.DieWithProgress` accepts an explicit transactional progress
callback. Its existing life ledger gains an `unarmed` flag: prior wilderness rows
migrate as true; managed progress without a new unarmed award records false.
Only a changed managed counter or a first unarmed award needs a new ledger row.
Callbacks are cleared after each attempt and do not survive as captured session
references. Monster death notifications follow the durable decision. The earlier
wilderness section's crash-time corpse/loot limitation still applies.

Private validation passes118 Power Arena checks,214 shared wilderness/daily-boss/
KO checks and30 equipment eligibility checks. The Power suite now uses the actual
configured guardian levels, rather than its old level1 placeholder. It covers
counter and evidence rollback, retry, lost acknowledgement without double count,
full-character10525 reload, and the existing floor rewards, bound packs, permanent
mount, wardrobe and title recovery. The shared suite checks legacy ledger migration.
These remain controlled-damage/calendar fixtures, not native or live acceptance.

The full reward regression exposed a real mount-equipping regression from the
missing-weapon-proficiency repair: earned mount200449 has catalog Proficiency1,
which was incorrectly treated as a weapon-family200 training requirement. Mount
armor now bypasses that weapon-proficiency check while retaining other equipment
eligibility. Ordinary earned-mount equip/reload passes; weapon missing/low/current
proficiency tests still pass. See EQUIPMENT_PROFICIENCY_REQUIREMENTS.md.

Final isolated assembly SHA256:
`C368D709A26B8412871880A2FCC61270F7A840668B09B4CB798611F2CD4B83B9`.
Logs: `obj/unarmed-power-build.log`, `obj/unarmed-power-tests.log` and
`obj/unarmed-daily-tests.log`. Compile the managed suite with
`python tests/EquipmentAchievements/compile_daily_unarmed.py --power`; its
PowerUnarmed.Tests.exe runner uses the same private database/schema safeguards.
No production deployment or retained-career mutation. PostgreSQL stops after tests.

A read-only installed-catalog check and loaded-profile assertions narrow the
remaining work without raising any monster's level:

| Handler | Current qualifying profiles |
| --- | --- |
| Sky Pass | None: all25 profiles3000?3024 are78?118; loaded-profile assertion passes. |
| Moon Box | None: profiles6000?6005 are1;6006 is100; loaded-profile assertion passes. |
| Snake Islands | Snake King3102 is120; integrated and tested below. |
| Labyrinth | AzureBird3147 is140, Talon3149 and Minotaur3155 are120, Howler3156 is125; integrated and tested below. |
| Demon Exterminators | Type58 is135 despite its RedDevilL117 name;18/20/57/83 are87/102/112/118 and excluded. Integrated below. |
| Power Arena | Floors5?7 qualify through their configured profiles and are covered above; raw catalog levels alone are not authoritative here. |

Sky Pass and Moon Box therefore do not need unarmed-credit integration on this
catalog. Other unclassified managed encounters, native acceptance and release
remain open; this does not certify universal encounter coverage.


## Snake Islands victory and unarmed credit (2026-10-02)

The registered level-120 Snake King now saves its accepted life, eligible daily
quest victory (11018), attempt closure and unarmed achievement (10525) in one
transaction before terminal death. Existing ferry admission, level-90 minimum,
distance, pending defeat, attempt identity and Pacific quest-day checks remain.
Armed players still earn the quest reward; saved active-hand equipment excludes
only unarmed credit. Initialization rejects a catalog below the reviewed level.

Database failures leave the boss alive and the same attempt retryable. An uncertain
commit resolves the original life receipt without running quest progression twice.
After confirmed death, saved victory is read back for immediate quest refresh,
including retries whose progress callback was skipped. Replacement admissions are
not removed. Reward claiming remains its existing separate transaction: two bound
Meteors and a bound rune for new contracts, with no daily P7 soul.

The isolated Snake Islands suite passes **32 checks**, including suppressed
unarmed evidence, failed attempt closure, lost commit acknowledgement, concurrent
retry, armed completion, ordinary ferry/jump access, stale attempts, midnight,
defeat recovery, claim rollback, duplicate claims and inventory/evidence reload.
Fixtures now use the actual loaded snake levels instead of level-1 placeholders.
This is controlled server-side combat and simulated calendar evidence, not native
client or a fresh-process career acceptance test.

Validation: `python tests/EquipmentAchievements/compile_daily_unarmed.py --snake`;
private runner `SnakeUnarmed.Tests.exe`, isolated `daily_unarmed_fixture` schema,
loopback PostgreSQL on port25439. Build/test logs are
`obj/unarmed-snake-build.log` and `obj/unarmed-snake-tests.log`.
Validation assembly SHA256:
`687528756504A6059C4E323B8D2A98D8C039C20E7C785ED79C3E19D87F68B54A`.
The database stops after the suite. No live deployment, native acceptance or
retained-career mutation. Labyrinth, Demon Exterminators and other unclassified
managed routes remain open.


## Labyrinth satchel and unarmed credit (2026-10-02)

Registered qualifying monsters on their matching Labyrinth floors now use the
shared accepted-life transaction: level140 AzureBird, level120 Talon/Minotaur and
level125 Howler. Eligible satchel loot and unarmed achievement10525 commit before
terminal death. Saved visit/floor, proximity and pending-defeat checks still govern
quest loot independently of unarmed eligibility. Talon and Howler remain excluded
from diamond/token drops. No monster levels or reward odds changed.

A random result belongs to the monster life and survives transaction retries;
respawn clears it. Satchel entries use the same life identity as the accepted-life
receipt. Lost acknowledgement therefore recovers one existing entry without a new
roll. A failed loot or achievement write leaves the monster alive. Notification
recovery reads only the killer's exact saved entry. Lower-level families retain
the original post-death loot path, including its failure-fencing behavior; this
batch does not claim to make those drops atomic with death.

The isolated Labyrinth suite passes **54 checks**: actual loaded levels, paid
ordinary admission, four-floor production movement, loot and achievement rollback,
uncertain commit, concurrent retry, winning-roll retention, losing rolls, respawn
reset, excluded families, reward claims and persisted inventory/evidence reload.
The shared wilderness/daily-boss/KO regression suite passes **214 checks** on the
same validation assembly. Fixtures use controlled combat, funding and randomness;
no native client, fresh-process career or production acceptance is claimed.

Compile with `python tests/EquipmentAchievements/compile_daily_unarmed.py --lab`.
The private `LabUnarmed.Tests.exe` runner uses the existing loopback PostgreSQL
port25439 and isolated `daily_unarmed_fixture` schema, removed after the suite.
Logs: `obj/unarmed-lab-build.log`, `obj/unarmed-lab-tests.log`,
`obj/unarmed-daily-tests.log`. Validation assembly SHA256:
`138200A5966AB24BF5210DA4CBE44C2959EC3916C8B99AEE469371638CBEA3A4`.
PostgreSQL stopped after validation. No retained-career changes or live release.
Demon Exterminators and unclassified managed encounters remain open.


## Demon Exterminators shared kill transaction (2026-10-02)

The registered Red Devil type58 uses its loaded level135 and now saves the
accepted monster life, every eligible teammate's Demon jar increment, the killer's
eligible Summer Hunt increment and unarmed achievement10525 together before death.
Types18/20/57/83 remain below120 and keep the existing death/counter path. Planet
Weekend accepts only Birdmen/type18 and remains on that lower-level route.

Team recipients still require ordinary current sessions, level120, same map,
proximity, current membership, a matching active daily stage and durable jar
ownership. Counters retain their caps. Run rows are locked in UID order; the
transaction never takes another player's inventory or entity lock. Unarmed credit
belongs only to the direct killer, not every teammate.

New `revora_demon_kill_credits` rows record the life, credited UID and quest day
inside the transaction. Failed teammate, Summer or achievement writes keep the
monster alive and roll back all increments. Lost acknowledgement reuses the life
receipt, restores the recorded recipients even after the team dissolves, and
never increments counters again. Normal committed kills refresh from their saved
results in memory; extra database readback is reserved for recovery. Notification
failure does not reverse durable rewards; existing reconnect/jar reads recover.

Validation uses the complete four-stage ordinary Demon hunt with controlled
lethal damage and time. The final stage tests a deliberately staged teammate and
a Summer admission through its service seam; these are not native admission proof.
The57-check Demon suite covers team rollback, Summer rollback, suppressed unarmed
credit, lost acknowledgement, team-change recovery, concurrent item-triggered
kills, counter caps, six bound daily rewards and inventory/evidence reload.
The141-check Summer suite covers normal NPC admission, multi-day hunting,
materials/crafting, rewards and fresh-character equipped-rune reload. No daily
P7 rewards were introduced.

Runners: `compile_daily_unarmed.py --demon` and `--summer` under
`tests/EquipmentAchievements`. Both use the isolated private loopback database
and remove their `daily_unarmed_fixture` schema. Logs:
`obj/unarmed-demon-build.log`, `obj/unarmed-demon-tests.log`,
`obj/unarmed-summer-tests.log`. Final validation assembly SHA256:
`D73B375BDDFACE0821E089A063402E0585C78FAF8A8463D0F48CD83A8C115277`.
No live deployment, native acceptance or retained-career mutation. Other managed
encounters still require explicit level/coverage classification; this does not
establish universal unarmed-credit coverage or full roadmap completion.


## Guild Beast victory transaction (2026-10-02)

The installed Guild Beast profile7006 is level120. Its existing owned-event
transaction now records eligible direct-killer unarmed credit before committing
its victory and frozen P7 soul entitlement. Cached and saved active-hand weapons
still exclude credit, as does a companion killer. The existing owner/life locks,
ordinary public hunt admission, timeout, claim and reconnect behavior remain.

The complete guild season/Beast/Elite Guild suite passes **1096 checks** on the
final validation assembly. Beast-specific assertions cover its loaded level,
nonlethal exclusion, immediate unarmed credit, real character/item reload,
suppressed-credit rollback of victory and soul, living-boss preservation, reconnect
retry, duplicate death/claim, ordinary unguilded entry and exact bound soul receipt.
The number1096 includes broader guild checks, not1096 new Beast tests.

The initial run passed the Beast assertions but a later Elite Guild plaza fixture
failed because it lacked the restored Nobility donor. The bounded runner now
loads the actual public sobnpcs900042 catalog actor before that suite. This fixture
repair does not establish a native Nobility donation journey.

Compile: `python tests/EquipmentAchievements/compile_daily_unarmed.py --beast`.
Logs: `obj/unarmed-beast-build.log`, `obj/unarmed-beast-tests.log`.
Validation assembly SHA256:
`CA05EE56465C16EC2FF9EEC430BFED4E67A137F22FE28D46C46BA89E8F7423D4`.
Private loopback PostgreSQL stopped after tests; no live deployment, retained
career changes or native-client acceptance.

## Remaining encounter level classification (2026-10-02)

Review the profile actually copied or configured by each spawner, not the display
name or source monster's unrelated original level. Read-only installed catalog
results in `obj/unarmed-remaining-levels.txt` confirm39=95,1413=112,2141=32,
2719=140 and7006=120. Profiles below120 do not qualify for10525 and should not have
their levels raised merely to manufacture an earning source.

| Managed route | Effective profile and conclusion |
| --- | --- |
| KingdomCompetitionField | Player-versus-player death path; no monster eligibility. |
| Elimination rescue/den | Both binding profiles configure42. |
| AncientDevilWorld | Base trial configures70; its separately owned boss has its own producer. |
| WarriorEpicCombat | Tiny Mind70 and Sealing Stele72. |
| SpunGoldCombat | Catalog-validated Cloud2141 at32. |
| DesertTreasureEncounter | Jinx Tomb Bat105. |
| DesertExpeditionEncounters | Configured113/115. |
| DefenseTalismanEncounters | Configured100/105/110. |
| AttackTalismanEncounters | Configured105/110. |
| TempestWingsEncounters | Sky Roc100. |
| FamilyFeudEncounters | Configured97/98. |
| GhostCityCombat | Configured75. |
| FranticMonkeysCombat/Apes | Shared scene profile configures50. |
| SoulGeneralPhantom | Jade Phantom60. |
| HeartEncounter/PlanetEncounter | Copy1413 at112 without overriding level. |
| PotencyHunt | Copies1413 at112; scales HP/attack/defense, not level. |
| Sky Pass/Moon Box | Below120 on the reviewed loaded catalogs, detailed above. |
| PowerBookCombat | Scene profiles103/108/110/112/115/117/**120**; final Minotaur is integrated below. |
| Extraction/ExpeditionField | Roaming1413=112, camp39=95, late boss2719=**140**; late-boss integration is covered below. |
| GuildBeastEvent | Catalog7006=120, integrated and tested above. |

These source/profile findings identified the Power Book final creature (now
integrated below) and the still-open Extraction late boss, followed by a check of
existing producers' branch coverage. They do not prove full native acceptance or
all reward/persistence milestones elsewhere on the roadmap.


## Power Book final Minotaur transaction (2026-10-02)

The level120 final Minotaur now saves its consumed life/corpse receipt and eligible
unarmed achievement together through the accepted-life death decision. A failed
receipt or achievement save leaves it alive and retryable. Its saved `dead_at`
remains unchanged after a lost acknowledgement, so retries cannot extend the
three-second burn window. The later Burn transaction still exclusively owns flame
progress, final plume consumption and stage silver. No new material or silver is
issued by final death. Lower-level hunts retain their existing progress path;
companion kills retain quest behavior without direct-player unarmed credit.

The focused suite passes **87 checks**, covering production combat, ordinary
eligibility/ownership, suppressed corpse and achievement writes, uncertain commit,
duplicate death, actual earned-plume burning, exact three-second expiry, corpse
cleanup and full character reload. Existing hunt/material/store checks cover the
multi-stage counters, timed challenges, material consumption, purchase rollback
and progression receipts. The final-stage combat fixture explicitly stages quest
progress and controls damage/time/randomness; this is not a new uninterrupted
natural-career or native-client acceptance result.

Compile: `python tests/EquipmentAchievements/compile_daily_unarmed.py --book`.
The bounded runner initializes empty fixture tables from the installed schema and
runs Power Book combat, hunt/material and store checks. Logs:
`obj/unarmed-book-build.log`, `obj/unarmed-book-tests.log`.
Validation assembly SHA256:
`6C173CAB457689ADF03A44E61B2EAB4ED42809316FB77913C7D0E757BCC90755`.
The private fixture schema is removed and PostgreSQL stopped afterward. No live
restart/deployment or retained-career changes. Extraction's late boss remains the
confirmed missing route before final existing-producer branch review.


## Extraction late boss and source-route reconciliation (2026-10-02)

Extraction's level140 Specter now uses the accepted-life decision before the
encounter is marked dead. A failed or uncertain unarmed write leaves it alive,
with no temporary relics or bonus issued yet. Confirmed death then marks the
encounter and issues its existing temporary reward once. Registered, current,
nearby event participants are required; fake/remote/unowned callers cannot settle
the late boss. Registered companion kills retain quest behavior without granting
direct-player unarmed credit. Lower-level scouts/camp guardians stay unchanged.

The **64-check** Extraction suite passes, including normal registration/entry,
large-map camps/boats, failed writes, lost acknowledgement, concurrent callbacks,
exactly three temporary relics and500 bonus RVP, full-bag handling, item/purse/soul
rollback, duplicate claim refusal, bound P7 identity and full character reload.
The late killer's unarmed credit persists when the cycle ends, while unextracted
treasure is forfeited. Carried treasure remains intentionally in-memory until
escape; this change does not add restart recovery for an unfinished expedition.
Fixtures control time, damage and movement; native acceptance remains pending.

Compile: `python tests/EquipmentAchievements/compile_daily_unarmed.py --extraction`.
Logs: `obj/unarmed-extraction-build.log`, `obj/unarmed-extraction-tests.log`.
Validation assembly SHA256:
`BE4C7BF35233A1AF0BD5F586EA6CFCC0A1A332F6CF7953AA7701A032E160387A`.
Private fixture cleanup and PostgreSQL shutdown completed; no live deployment.

The current `Entity.Die(Entity)` dispatcher has40 routes including its ordinary
fallback. Manual source review reconciles21 non-monster/below-threshold routes
in the level table above and19 routes with an applicable unarmed producer:

| Producer group | Accepted branch reviewed |
| --- | --- |
| Ancient Devil boss | All owned defeat slots call the helper; four level100 minions are filtered, level120 boss qualifies. |
| Abyss summoned encounter | Each accepted summoned defeat records credit before commit; completion adds its separate quest evidence. Static non-encounter mobs use the ordinary fallback. Activation remains separately gated. |
| Trojan epic | Registered combat passes the actual body/killer to Store.Defeat; accepted encounter transaction records credit. |
| Sword Master, Purple Banshee, Snow Banshee, Terato | Each accepted victory transaction records direct-killer credit before commit, independently of party reward sharing. |
| Exorcism/Spook | All owned grade variants share the victory path; credit belongs to the actual killer, independently of eligible daily recipients. |
| Dragon Slayer | Both minion and ghoul defeat branches reach the shared credit call; monster level is checked centrally. |
| Dis City | Accepted ordinary hunt, ordered crystal and boss branches reach the shared credit call. |
| Four daily bosses, Guild Beast | Owned victory/reward transaction calls, documented above. |
| Power Arena, Snake Islands, Labyrinth, Demon Exterminators, Power Book final, Extraction | Accepted-life integrations documented and tested in this workstream. |
| Ordinary fallback | Final death decision checks active equipment and saves the accepted life/credit; cancellation paths grant none. |

The shared helper rejects companion kills and nonempty cached or persisted active
hands. This closes the identified server implementation/source-route coverage
item on the current catalog. It is not a native-client matrix, a new natural
320-achievement career, full crash recovery for every event, or completion of the
whole roadmap. Existing activation, retained-career, deployment and native gates
remain open under their own requirements.


## Retained calculated barehand Specter victory (2026-10-03)

Retained ordinary character4294907001 now removes its exact active weapon UIDs
through ItemHandler, verifies both cached hands and saved slots4/5 are empty
with the main equipment bank active, and defeats Extraction's level140,
5,000,000-HP HorrifyingSpecter through normal `Attack.HandleAttack` melee.
The complete passing run takes122 attacks. No damage, HP, class, level or gear
stats are supplied. Three earned relics and500 temporary bonus RVP follow the
kill. Ordinary re-equipping restores the same weapon UIDs before boarding.
Early/interrupted holds pay nothing; a complete hold gives three relics, one
bound P7 and660 RVP including the normal extraction/per-item components.
Exact nested prior career and both Extraction claims survive game/database
restart; repeated completion changes nothing.

10525 was already owned from the historical controlled Trojan route. Production
WildMonsterVictory intentionally skips another life/evidence grant in that case.
The original receipt remains single and unchanged; this is stronger calculated
kill evidence on the same retained identity, not a newly issued achievement.

The initial attempt killed the Specter in123 hits, then a fixture assertion
incorrectly demanded an additional life row. Process exit forfeited that
unextracted satchel, as designed. Recovery re-equipped only the exact prior
checkpoint UIDs and compared the whole nested checkpoint: the only differences
were ring60201915 durability6839?6821 and fan60753038 durability6428?6404.
The fixture permits exactly those four nested JSON occurrences, preserving wear;
it does not rewrite the historic checkpoint, delete belongings or reset10525.
The corrected fresh event attempt and cold reload both pass.

Source: `tools/CheckCandidateRetainedUnarmed.cs`; phases
`career-retained-unarmed-{earn,reload}`; checkpoint
`revora_retained_unarmed_probe`, wrapping the retained Beast Trial scope.
Private executable `71F2DA5388C13BBA67FFA936ECBDCB141AE716D21EB2CA78DC151B4AFF380A77`.
Event time, approach and attack cooldown remain controlled; no retaliation,
natural difficulty, native acceptance or live deployment is claimed. Gear/funding
history retains its documented provenance. The full compatible320 route remains
unverified, and no additional identity count is reported.
