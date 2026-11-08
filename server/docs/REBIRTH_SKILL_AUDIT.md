# 6711 rebirth skills and instructors

This report contains dated checkpoints beginning with source `5df7f9c` on
2026-09-16. Later sections supersede the original classification queue. All five
remaining Pirate/Taoist identities now have focused and composed restart
evidence. M6.55b remains open for the separate Archer effects and broader
weapon-gift/catalog requirements; native acceptance is not implied.

## Evidence

The [official Assassin class guide](https://co.99.com/guide/classes/assassin.shtml)
requires the level-40 Archer promotion and identifies Assassin as an Archer
form. Its learning levels agree with the installed server and client level-zero
records. The [official skill guide](https://co.99.com/guide/skills/m_assassin.shtml)
identifies the same seven arts. These pages disagree on some combat values and
knife requirements, so this change does not adopt their damage/cooldown values
or claim complete combat parity.

| Existing NPC 400 option | Art | Logical spell | Minimum level |
|---|---|---:|---:|
| 12 | Kinetic Spark | 11590 | 50 |
| 13 | Dagger Storm | 11600 | 100 |
| 14 | Path of Shadow | 11620 | 40 |
| 15 | Blade Flurry | 11610 | 40 |
| 16 | Blistering Wave | 11650 | 70 |
| 17 | Mortal Wound | 11660 | 40 |
| 18 | Spirit Focus | 11670 | 90 |

All seven level-zero records specify throwing-knife subtype 613. Learning does
not require an equipped weapon; weapon-use checks belong to combat. The free
training price is the existing Revora policy, not a newly asserted retail price.

Read-only local evidence, SHA-256:

- Server `bin/Debug/database/magictype.txt`:
  `97ecb413566f5fea541090d1a1c1498ed2acf0874efa6056984583c65c187a71`.
- Client `6711/ini/magictype.dat`:
  `618431607e8db81f5e8c6093d801329b73bd9a5af4aa57fc8a38348a832173c2`.
  Decoded in memory using the existing data transform; exact re-encoding was
  checked before reading the numeric identities and requirements.
- Client `6711/ini/MagicEffect.ini`:
  `7cb74ad7c9ed7fabb91da5f0ff1c79ca97bd344f0eec7d2d7b7545aee70d411f`.

No proprietary catalog rows or client assets are added to Git.

## Completed source behavior

Archer Master 400 in public Job Center 1004 now teaches the correct Path of
Shadow, instead of Dagger Storm. All seven selections require a living ordinary
player, the actual nearby active NPC, current promoted Archer class 42..45 and
the listed learning level. The existing 2032 packet path checks body/trailer
and input bounds before deserialization. No GM or account allowlist is used.

The service takes the character owner lock, checks the saved progression and
location, and inserts the learned skill plus `revora_assassin_training` receipt
in one transaction. Client projection follows commit. Repeated requests retain
existing level, EXP, previous training and soul metadata; they cannot create
another receipt or reset the skill. Unexpected row conflicts fence the session
for reload. A later life may legitimately learn a removed art again, so receipts
do not impose a permanent character/spell uniqueness restriction.

The seven arts now belong to the Archer school in the shared rebirth catalog.
First, second and paid rites retain and reset learned arts when the destination
is Archer, preserving previous training; other destinations remove them. After
commit, all three rites clear Path of Shadow, Kinetic Spark, Blade Flurry and
Spirit Focus. Failed transactions preserve the original skills and active modes.
This closes the stale Kinetic Spark flag path that could still select the base
catalog spell after its learned skill was removed.

## Validation and acceptance

Isolated builds and ordinary-player fixtures use the disposable
`revora_potency_validation` schema. The training fixture loads the installed
Archer Master's coordinates and sends actual 2031/2032 requests. It covers each
learning boundary, correct identity, repeated/concurrent requests, saved soul
metadata, full reload, rejected class/location/session/framing, suppressed writes
and external saved-row conflicts. The rebirth fixture first learns all seven
through the ordinary instructor, then checks retention/removal and active modes
through all three rites, including forced rollback and full reload.

Final validation: **310** trainer/promotion checks, **2,115** paid/second/first
rite checks (including 30 new Assassin rite checks), and **3** producer audits.
The source-producer inventory remains 224/337 and is not a per-character
achievement route. No live
server restart, active-client write or public patch is included. Genuine-client
menu, skill icons, status rendering and combat acceptance remain pending.

## Remaining M6.55b work

- Freezing Arrow 5000 and Poisonous Arrows 5002 are persisted by rebirth, but
  currently have no passive combat dispatch. The
  [official rebirth skill guide](https://co.99.com/guide/skills/m_rebornsk.shtml)
  describes freezing as interrupting attacks and restricts poisonous arrows to
  level 70+ with both bow and ammunition below level 70. It does not establish
  every effect duration or encoding needed for this edition.
- Installed 5000 has percent 5, duration 0 and raw status 16; 5002 has percent 10,
  duration 2 and raw status 2. Raw catalog status values must not be copied into
  the runtime flag-index enum: those representations are not proven equivalent.
  Deriving an eight-second freeze from power 8000 or a fifteen-tick poison from
  another raw column remains an inference, not verified retail behavior.
- Continue the full newer-school spell/weapon-gift audit, published matrix gaps
  and unclassified spell identities. Seven corrected arts do not certify the
  entire inheritance catalog or repair historical characters automatically.

## Wind Sage follow-up

M6.55b2 now validates the seven reviewed ordinary Ninja learning selections and
atomic consumption of the three epic scrolls; see [the instructor report](NINJA_SKILL_TRAINING.md).
This fixes a Twilight Scroll loss/replay defect and missing class checks. The
instructor fixtures use deliberately seeded owned scrolls. The separate
[Ninja Epic runtime](NINJA_EPIC_QUEST.md) now validates paid and hunted-material
earning through skills and weapon conversion; rebirth exceptions and native
acceptance remain open.

The next catalog pass identified another 24 trainer-taught logical IDs missing
from the shared school classification at baseline 56c8fa7. Both installed
catalogs contain matching level-zero identities:

| School | Missing logical IDs |
|---|---|
| Warrior | 10470, 11160, 11200, 12660, 12670, 12680, 12690, 12700, 12770 |
| Ninja | 11170, 11180, 11230, 12070, 12080, 12090, 12110 |
| Monk | 12550, 12560, 12570 |
| Pirate | 11140, 14680, 14710, 14720 |
| Fire Taoist | 10309 |

These are a review queue, not a blanket deletion rule. In particular, the
[second-rebirth guide](https://co.99.com/guide/quests/2rebornskill1.shtml)
explicitly distinguishes Super Twofold Blade retention from the other three
new Ninja arts. Existing scroll-based learning must remain available when
implementing the reset rule. The Pirate epic arts are not automatically too
new for this client: [patch 6707 introduced Surging Pirates](https://co.99.com/news/2018-04-12/patch_6707__surging_pirates_expansion__new_server_bloomofdeath_eu_and_more.shtml),
and the installed 6711 catalogs contain all three identities. Combat and exact
inheritance still require their own evidence.

## Ninja inheritance and active summons (2026-09-16)

Baseline: `b8413cc`. Seven trainer-taught Ninja arts were absent from the school
catalog, so the generic unclassified-skill fallback retained all of them even
when the player changed profession. This batch classifies those seven IDs and
applies the documented Epic exceptions in first, second and paid rites.

Evidence and interpretation:

- The [2014 Ninja expansion](https://co.99.com/guide/event/2014/ninjawar/index.shtml)
  explicitly removes Twilight Dance, Shadow Clone and Fatal Spin after rebirth,
  while preserving Super Twofold Blade. This specific exception takes priority
  over the older first-rebirth table's broad same-Ninja wording.
- The [second-rebirth guide](https://co.99.com/guide/quests/2rebornskill1.shtml)
  repeats that distinction. Learned Super Twofold Blade 12080 retains the
  existing cross-destination behavior; the expansion states retention without a
  destination qualifier. This is an explicit interpretation, not a native
  capture of every class combination. The rite never grants an unlearned copy.
- The [first-rebirth skill table](https://co.99.com/guide/reborn/rebornspe.shtml)
  keeps Ninja school arts for a Ninja destination and names Toxic Fog for other
  destinations. Bloody Scythe 11170, Mortal Drag 11180 and Gaping Wounds 11230
  follow the school rule: retain/reset when the destination is Ninja, remove
  otherwise. Their learning-level research caveats remain unchanged.
- The [reincarnation guide](https://co.99.com/guide/pureskills.shtml) shifts the
  retained class history and resets training. Paid reincarnation already uses
  that history planner; it now applies the same specific Ninja exclusions.

| Art | First, second and paid rite behavior |
|---|---|
| 11170 / 11180 / 11230 | Retain learned arts for a Ninja destination; remove otherwise |
| 12080 Super Twofold Blade | Retain if learned; reset level/EXP and preserve previous training |
| 12070 Twilight Dance | Remove, including same-Ninja resets; relearn with a new scroll |
| 12090 Shadow Clone | Remove, including same-Ninja resets; relearn with a new scroll |
| 12110 Fatal Spin | Remove, including same-Ninja resets; relearn with a new scroll |

Only after commit, the three rites clear active Fatal Strike/Shuriken Vortex,
turn Counter Kill off, remove the player's owned registered clones and discard
the old roster. Failed writes preserve skills, flags and summons. Clone
creation copies its monster template instead of rewriting a shared Owner field,
and shares the rite's owner lock. Existing 2812 attacks now validate bounded
reads, unique requested IDs, learned skill and actual owned map actors before
any attack. Their projections use a separate Attack object instead of mutating
the owner's queued attack. Damage formulas and normal attack timing are unchanged;
this is not a claim of complete retail Shadow Clone combat parity.

**2,181 `-NinjaRebirthOnly` checks** pass, including paid/second/first and Assassin
prerequisites. The new journey learns all seven arts through Wind Sage before
all three rites, checks same/other-class destinations and forced rollback,
reloads exact previous training, and repays three new scrolls to relearn the
removed arts. Actual clone creation verifies independent metadata; packet tests
verify a valid attack still damages its target, duplicate/partly foreign lists
cannot deal partial damage, removed skills cannot attack, and another player's
summons survive cleanup. No new live deployment or native display acceptance
is claimed. The 800-check flower regression and three producer audits also pass.
At M6.55b3d1, 17 trainer IDs remained unclassified outside Ninja. The Monk
follow-up below reduces that queue to 14 (Warrior 9, Pirate 4, Fire Taoist 1).


## M6.55b4c1 Monk epic retention and effect retirement

Reviewed 2026-09-16 against source `81e5eec`. Infernal Echo 12550, Grace of
Heaven 12560 and Wrath of the Emperor 12570 were absent from the school map,
so all three could bypass class filtering and remain on a different class.
They now belong to school 6 in the shared first/second/paid rite planner.

The [first-rebirth table](https://co.99.com/guide/reborn/rebornspe.shtml) retains
Monk arts for another Monk life and lists Serenity for other destinations.
The [second-rebirth table](https://co.99.com/guide/quests/2rebornskill1.shtml)
uses the same distinction in its Monk paths. The
[reincarnation guide](https://co.99.com/guide/pureskills.shtml) applies the
shifted three-life history and reset training with remembered skill levels.

**Interpretation, medium confidence:** apply the published general Monk-school
retention rule to these three catalog-confirmed Monk-exclusive arts. The
reviewed sources do not identify a Monk epic exception comparable to the
explicit Ninja expansion exclusions. This is not new native-server evidence
or proof that no separate historical exception ever existed.

All three learned arts are retained/reset for a Monk destination and removed
for other destinations through first rebirth, second rebirth and paid
reincarnation. Retained arts reset current level/EXP and preserve previous
training and soul metadata. The planner never grants an unlearned epic art;
Head Abbot never charges another scroll for an already retained skill.

Successful rites clear pending Wrath state, Oblivion and the character's active
self aura before recalculating equipment/stats. They retire that character's
registered Infernal Echo effects, including the existing 1380/1390 pulse aliases,
without removing another character's effects. Failed transactions preserve
skills, effects and active modes. Floor removal checks exact object identity,
so an expired old effect cannot delete a replacement with the same floor UID.

Infernal Echo creation/pulsing shares the rite's character lock. Pulses require
a live canonical Monk, the learned skill, matching map/owner and the registered
effect object. A pulse claims the existing effect once before damage, preventing
repeated screen ticks and stale post-rite callbacks from applying it again.
Creation also rejects coordinates outside walkable map cells. The player branch
now passes the **caster** into damage calculation; it previously shadowed that
variable with the victim. Existing timing, skill multipliers and nobility scaling
are not asserted to match retail. Full Monk combat parity remains separate.

**2,165 `-MonkRebirthOnly` checks** pass, including 2,115 paid/second/first and
Assassin prerequisites. New cases learn all three arts through actual Head
Abbot packets before each rite, test same/other-class destinations and forced
rollback, reload exact metadata and retry training without another charge.
Registered synthetic pillar fixtures exercise the actual delayed pulse handler:
valid damage, invalid owner/map/session/class/skill/state, concurrent repeat
suppression, foreign-effect preservation and reused-UID removal. A PvP control
holds victim stats fixed while changing caster attack, then changes only victim
attack: damage follows the caster and is unaffected by victim attack.

The separate 621-check hunted Monk quest regression and 2,181-check Ninja
inheritance regression also pass. Native mission 3526/introductory use, genuine
6711 appearance and full damage/effect parity remain open. No live deployment
or active-client modification is included.

## Warrior follow-up (2026-09-16)

The nine Warrior IDs in the original review queue are now classified under
school 2. All three rites retain/reset them only for a Warrior destination;
this applies the general school rule as an explicit interpretation. Backfire
12680 additionally loses its previous-level recovery metadata, as specifically
documented by the official skill guide. Souls are preserved and no unlearned
newer art is granted. Postcommit cleanup retires the character's flags/timers
and serialized Dance pulses, while failed rites and other characters' buffs
are preserved. See [the Warrior report](WARRIOR_SKILL_RESTORATION.md) for
ordinary learning, native-dispatch fixtures and remaining combat limitations.

The earlier 24-ID classification queue now has **five** IDs still awaiting
review: Pirate 11140/14680/14710/14720 and Fire Taoist 10309. This count does
not close the broader catalog/weapon-gift audit or Archer effect-encoding gaps.

## Remaining five identities: corrected classification evidence (2026-10-02)

Current source reviewed at7de75a20. The five IDs remain absent from
`RebirthSkillRules.schools`; `SecondRebirthSkills.classified` derives from that
set and its matrix. Consequently the generic unknown-skill fallback retains
these learned arts for unrelated destinations. No source repair or completion
claim is made by this evidence checkpoint.

The installed server catalog still has SHA256
`97ecb413566f5fea541090d1a1c1498ed2acf0874efa6056984583c65c187a71`.
A bounded level-zero identity read confirms10309 ChainBolt,11140 Windstorm,
14680 SeaBurial,14710 ImmortalForce and14720 BloomofDeath. The earlier paired
client/server identity evidence above remains historical; this pass did not
re-decode or alter the client. Proprietary catalog rows are not reproduced.

The earlier queue's **Fire-only label for10309 is incorrect**. Official
[Water Taoist skills](https://co.99.com/guide/skills/m_watertao.shtml) and
[Fire Taoist skills](https://co.99.com/guide/skills/m_firetao.shtml), reviewed
2026-10-02, both list Chain Bolt as an XP skill. Current Taoist Moon branches
also teach10309. Classification must account for both Taoist destinations;
removing it from a Water destination on the basis of the old label would be
an unsupported regression. The shared-class interpretation is evidence-backed;
exact6711 rebirth retention is not independently captured here.

The official [Pirate skill guide](https://co.99.com/guide/skills/m_pirate.shtml)
identifies Windstorm as Pirate-only. The dated
[2018 epic-skill announcement](https://co.99.com/news/2018-04-09/detailed_information_about_pirate_epic_skills.shtml)
identifies Sea Burial, Immortal Force and Bloom of Death as Pirate epic arts.
The [first-rebirth guide](https://co.99.com/guide/skills/m_rebornsk.shtml)
retains Pirate arts for a Pirate destination and lists Gale Bomb for other
classes. Applying this general rule to these four catalog identities is a
medium-confidence interpretation, not a newly verified6711 exception table.
Do not alter damage, learning levels, scroll costs or weapon requirements from
these pages; the modern guide and local trainer already disagree on some levels.

Implementation prerequisites:

- Classify the four Pirate arts and shared Taoist Chain Bolt without granting
  unlearned skills; preserve reset level/EXP, prior training and soul metadata.
- Review first, second and paid rites together. The second-life classifier is
  derived at startup, and successful rites already run class-mode cleanup.
- Handle the active ImmortalForce flag added by the14710 combat branch before
  claiming complete cross-class cleanup. Inspect Sea Burial's delayed effects
  and any Bloom/Windstorm callbacks for ownership across the rite. Do not remove
  foreign effects or clear state on a failed transaction.
- Exercise ordinary instructor/quest learning followed by each actual rite,
  same-school and unrelated destinations, both Taoist destinations, rollback,
  retry and exact skill reload. Keep native presentation and combat parity open.

This narrows an existing M6.55b task; it does not add a quest family, achievement
receipt, deployment or completed implementation milestone.

## Five-art retention and Pirate effect cleanup (2026-10-02)

The four reviewed Pirate arts now belong to school7. Chain Bolt10309 belongs
to both Water13 and Fire14. First-rebirth and the derived second/reincarnation
classifier therefore retain learned arts for their supported destination schools
and remove them for unrelated destinations. They do not grant unlearned skills.
Retained skills reset current level/EXP while preserving prior training and soul
metadata. This implements the documented interpretation above, not new native
proof of retail retention. The original24-ID classification queue is now covered
in source; the broader rebirth catalog, Archer encoding and weapon-gift audit
remain open.

All three successful rites clear ImmortalForce and retire only the character's
registered Sea Burial effects. Failed transactions leave both unchanged. Effect
removal requires exact map, owner and object identity. Sea Burial creation and
Immortal Force casting now share the character's rite lock and recheck ordinary
Pirate eligibility. Bloom of Death is immediate in the Windstorm branch; this
review found no separate delayed Bloom object requiring retirement.

The delayed Sea Burial callback shares that lock, checks canonical owner, current
Pirate class, learned art, registered effect, map and coordinate bounds, then
claims the pulse once. It retires an unusable registered effect without damage.
Screen expiry delegates removal to this ownership check, so an old callback cannot
delete a replacement floor object that reused its UID. Foreign effects remain.
Existing damage formulas, spell prices and weapon requirements are unchanged;
this is not full Pirate combat parity.

Validation: isolated server build with existing legacy warnings; focused fixture
compiled with warnings as errors;175 checks pass. Tests execute all three actual
rite handlers for Pirate, Water and Fire source profiles, same/shared/unrelated
destinations and injected transaction rejection. They verify exact learned-skill
metadata after character reload, failed-rite effect preservation, foreign-effect
preservation, valid delayed damage, invalid owners/states, concurrent duplicate
pulses, stale UID reuse and no unlearned grant. The private schema is removed and
its PostgreSQL process stopped. Validation assembly SHA256:
`40C1EB40E5A28CF54B3A39458C0F5197D2B4388BC7C29D333374F758E4CFC251`.

Compile the focused fixture with
`python tests/EquipmentAchievements/compile_daily_unarmed.py --rebirth-arts`.
It reuses the private loopback25439 catalog/runtime environment. Initial reuse
of the broad rebirth suite duplicated fixture tables; the focused harness now
initializes only its required Sereth/catalog/schema dependencies.

Learned spell records, prior class qualifications and rite materials are supplied
in this fixture. Ordinary instructor/scroll learning through these rites, actual
cast-versus-rite concurrency, full process restart and native presentation remain
open. These checks do not add retained-career achievements or deploy a server.
The compatibility report only refreshes the changed rite-source hashes; its
321 candidates and unverified320-career conclusion remain unchanged.

## Ordinary Chain Bolt training through rebirth (2026-10-02)

Review of Taoist Moon's legacy options found61 and208 checked level but omitted
class eligibility; option210 duplicated the same spell with a different minimum.
All three now use `TaoistEpicTraining` as free Chain Bolt lessons for promoted
Water132..135 or Fire142..145, at level40. That common minimum matches both
official skill guides cited above and the installed level-zero record. This
replaces the inconsistent level80 legacy alias; it does not change damage or XP.

The existing trainer transaction saves the skill and a training receipt together,
with zero item/scroll fields for this free lesson. Repeated requests preserve
training metadata. The handler validates canonical ordinary ownership, nearby
active Taoist Moon, class, level and saved progression/location under the same
owner lock used by rites. Known lesson options fail closed before quest startup
rather than falling through to the legacy branches. Paid epic lessons retain
their scroll requirements and class restrictions.

The private fixture initializes the real checked Taoist temple/trainer services
and uses catalog Taoist Moon coordinates. Sealed ordinary NPC requests teach
Chain Bolt before all first/second/paid rite cases for both Taoist schools; the
fixture no longer inserts its learned record. It controls later trained metadata,
starting class/level, travel and rite materials. Wrong classes and unpromoted
Taoists cannot use any alias. Repeated lessons preserve level/EXP/previous level
through reload. Remote, dead, low-level and rejected receipt writes grant nothing;
transaction failure fences the session and reconnect permits a clean retry.

The isolated build and244 focused checks pass (including the previous175).
Validation assembly SHA256:
`763E2B1A716EFC8DAC141D8B9BA3B261794FC3EE8775C0BD68D69CE3859F014B`.
No packet layout changed. The private schema is removed and PostgreSQL stopped;
no production process or database was changed. Ordinary Pirate learning/scroll
integration, cast-versus-rite concurrency, full-process and native gates remain
open. The retained career count is unchanged.

## Ordinary Pirate lessons and scroll use through rites (2026-10-02)

Pirate instructor4272 option15 now teaches Windstorm through the owned training
transaction. It preserves the existing free lesson and level15 minimum, requires
an ordinary nearby Pirate70..75 and saves the skill plus a receipt before client
projection. The receipt uses zero scroll fields for this free lesson. Existing
training metadata survives replay; failed writes roll back the skill and receipt
and fence the session for reload. Known instructor options also fail closed while
the Pirate world is unavailable rather than using the legacy free-art branches.

The focused fixture now initializes the real Pirate hub/trainer service, teaches
Windstorm through sealed ordinary NPC requests and uses all three supplied epic
scrolls through ItemUsage. Each learned art has an actual training receipt;
repeated use preserves spare scrolls. These learned records replace direct skill
inserts before every Pirate first, second and paid rite case. The existing Taoist
trainer cases, rite rollback/reload checks and Sea Burial pulse checks remain.
Windstorm additionally passes wrong-class, level14, remote, concurrent confirmation,
failed receipt, reconnect/retry and trained-metadata replay checks.

The initial fixture stopped because the development runtime lacks10271.map.
It now reads the existing private candidate terrain directly and runs the
production hash check during world initialization; no terrain was copied into
the development or live runtime. The reused hub hash is
`268E254AF092F8CEF3E7ED71F69A658467ACA6225C335A1BB77241BD2113E065`.
Only its initializer temporarily uses the private game directory; the fixture
then restores its normal working directory.

The isolated server build and396 focused checks pass. Validation assembly SHA256:
`AC98808707148E3C86AC867E198B3E13476E00F56266A79CD7CF8D1BF900BFBE`.
The fixture compiles with warnings as errors; the server retains existing legacy
warnings. The private schema is removed and its PostgreSQL process stopped.

All five reviewed arts now enter the rite tests through ordinary learning
handlers. Initial level/class qualifications, epic scrolls, subsequent trained
metadata, travel and rite materials remain supplied/controlled. This does not
prove acquisition of those scrolls in the same journey, a natural compatible
career, actual cast-versus-rite concurrency, full Host restart or native display.
Those requirements remain open. No live process or database was changed.


## Pirate cast and delayed pulse across committed rites (2026-10-02)

The focused fixture now exercises an actual decoded Immortal Force cast before
and across each of first rebirth, second rebirth and paid reincarnation. The
ordinary character learns the four arts through the existing instructor and
item-use handlers, equips a catalog-compatible supplied weapon, and successfully
activates Immortal Force as the positive control. The test clears that flag and
its cooldown, holds the character mutation lock, starts another actual cast and
a registered Sea Burial callback on separate threads, then commits the real rite
to Trojan while they cannot mutate the character. Bounded joins verify completion.
After release, the target retains its health, the old pillar is absent and the
Pirate flag stays absent. An additional post-rite cast also cannot restore it.

The first control failed because the fixture lacked a required weapon. After
supplying catalog-compatible equipment, an initial expectation of immediate cast
refusal also failed: this fixture's cast waits for ownership. The final assertion
permits either refusal or waiting and checks the actual safety invariant before
and after the committed rite. No server behavior was changed for these tests.

`python tests/EquipmentAchievements/compile_daily_unarmed.py --rebirth-arts`
compiled with warnings as errors; the isolated runner passed **438 checks**.
It reused the previously built server assembly SHA-256
`AC98808707148E3C86AC867E198B3E13476E00F56266A79CD7CF8D1BF900BFBE`.
The private fixture schema was cleaned and its PostgreSQL process stopped.
No live deployment, restart or client release occurred.

This closes the specifically tested Pirate cast/pulse-versus-committed-rite
interleaving. It does not claim exhaustive scheduler coverage, every spell's
combat behavior or a failed-rite concurrency scenario. Existing sequential
rollback checks remain separate. Qualifications, gear, epic scrolls, rite
materials and the registered delayed effect are supplied; earned scroll
acquisition in the same journey, full-process and native acceptance remain open.


## Earned Pirate scrolls through paid reincarnation (2026-10-02)

The same ordinary second-reborn Pirate now enters the initialized quest hub
through instructor option 150, purchases charts at 27 CPs through sealed NPC
requests, searches them through the treasure NPC, exchanges ten earned Water of
the Sea per scroll at Mermaid and learns each issued scroll through ItemUsage.
No chart, Water or epic scroll is inserted by this journey fixture.

Two journeys cover paid reincarnation to Pirate and to Trojan. Each purchases
and searches 30 charts, spends exactly 810 CPs on charts, obtains three bound
scrolls, and joins their exchange receipts to the actual training receipts by
owner, scroll UID and item type. Duplicate confirmation/use packets leave the
single expected purchase, search, exchange or lesson. Character reloads preserve
ten bound Water before each exchange, the issued scroll before learning, and the
final skill set and CP balance after actual reincarnation. The final rite retains
the learned arts for Pirate or removes them for Trojan and charges only once.

The deterministic draw selects the documented Water consolation branch. This
verifies that branch's earning and persistence chain, not random-distribution
odds or the number of charts a live player would need. Initial level/class,
second-rebirth qualifications, 50,000 CPs and reincarnation dew are supplied by
the existing paid-rite fixture. NPC approaches use controlled teleports; this
is not a natural leveling, income or native travel demonstration. Quest scroll
acquisition itself requires second rebirth, so it cannot precede first/second
rebirth on the same character. Earlier first/second tests correctly use supplied
tradable scrolls rather than pretending to earn them with an ineligible character.

The focused fixture compiles with warnings as errors and passes **584 checks**
against the unchanged isolated assembly
`AC98808707148E3C86AC867E198B3E13476E00F56266A79CD7CF8D1BF900BFBE`.
The search-draw override is restored in finally; the private schema is cleaned
and PostgreSQL stopped. No live service or client was changed. This closes the
same-character paid chart/search/exchange/learning/rite composition in the
isolated hub; full Host restart, native acceptance and applicable release gates
remain open. It does not enable a staged hub on production.


## Updated private candidate baseline (2026-10-02)

The hash-gated `Refresh-CandidateGame.ps1` workflow installed the tested isolated
assembly `AC98808707148E3C86AC867E198B3E13476E00F56266A79CD7CF8D1BF900BFBE`
into the existing private candidate. Its prior executable
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`
and manifest were preserved for rollback. Dependency hashes matched the existing
manifest. This assembly was built with the committed-source overlay that excludes
unrelated owner worktree edits; the refresh manifest records the current worktree
and explicitly does not claim a clean build.

With the updated binary, `career-retained-travel-reload` passed through composed
Server.Run startup against the isolated `revora_candidate_arena_process` database
on loopback PostgreSQL port 25439. The game listeners remained on the configured
private ports. The existing complete retained travel/career snapshot still matched
exactly after loading. The process completed its close path and the wrapper
stopped PostgreSQL; the data directory had no postmaster.pid afterward. Evidence:
private `arena-process-career-retained-travel-reload.log`.

This is candidate-upgrade baseline evidence, not an earned five-art restart
journey. It must not close that open acceptance checkbox or imply native-client
acceptance. The next integration phase must earn/learn the arts and commit the
rite using this binary, preserve its checkpoint, then verify it in a separate
post-restart process. No live server, database or client release was changed.


## Earned Pirate arts across game and database restart (2026-10-02)

`pirate-rite-earn` and `pirate-rite-reload` now pass in the existing composed
private game harness against assembly
`AC98808707148E3C86AC867E198B3E13476E00F56266A79CD7CF8D1BF900BFBE`.
They use two dedicated ordinary-player fixture identities 4294909101/4294909102,
without changing the retained career identity. The private database is
`revora_candidate_arena_process` on loopback port 25439. The game and database
both terminate between phases; the second phase loads the characters anew.

Each character learns Windstorm from Pirate instructor 4272, enters the hub
through its real option, pays for and searches 30 charts, exchanges the earned
Water for all three bound scrolls and consumes those scrolls through ItemUsage.
The route sends sealed NPC request frames through PacketHandler. It buys Oblivion
Dew from Sereth for 2,000 CPs, then submits the actual reincarnation request twice.
The single resulting rite keeps all four arts for Pirate or removes them for
Trojan. Expected chart costs are 810 CPs, plus the dew and configured rite fee.

After restart, the loaded class, level, rebirth count, CP balance and learned
skill membership are checked. An exact checkpoint comparison covers the selected
profile/attribute fields, complete skill and item rows, all chart purchase/search,
scroll exchange/training, Oblivion purchase and reincarnation receipt rows for
each owner. Both have exactly 30 purchases, 30 searches, three linked exchanged-
scroll lessons, one dew purchase and one rite. This does not compare every
entity column or unrelated ledgers. Earned records remain in the private owned
`revora_pirate_rite_process_probe` checkpoint, not as production player data.

Harness compilation uses warnings as errors. Initial fixture corrections were
an enum/Console naming error, adding the two phases to the existing C# argument
allowlist, and replacing the unimplemented NpcRequest.ToArray with the existing
sealed request format. The last issue left the first owned character initialized;
the next run resumed it without resetting the database or other characters.
The successful earn/reload logs are private
`arena-process-pirate-rite-{earn,reload}.log`. Both game close paths completed and
the wrapper stopped PostgreSQL; no postmaster.pid remained afterward.

This closes the composed paid Pirate earning/rite restart requirement. It does
not close first/second-rite or Taoist full-process acceptance, concurrent socket
replacement, interrupted-transaction recovery, native rendering or release gates.
Initial level 110, second-rebirth history, attribute budget and 50,000 CPs are
supplied. NPC approach positions and Water-consolation random draws are controlled;
packets are dispatched in process, not sent by an authenticated native client.
No live service was changed. Repeat earn uses the saved final checkpoint; replay
of the actual earning route requires separately reviewed fixture reset, not
blind deletion of the private database.


## Five-art first/second and Taoist process acceptance (2026-10-02)

The additional `pirate-five-art-earn` / `pirate-five-art-reload` phases pass
against the same reviewed candidate assembly
`AC98808707148E3C86AC867E198B3E13476E00F56266A79CD7CF8D1BF900BFBE`.
Sixteen dedicated ordinary-player fixtures cover first and second rebirth from
Pirate, Water and Fire to a retaining school and to Trojan, plus paid Water/Fire
reincarnation to the other Taoist school and to Trojan. The preceding two paid
Pirate cases complete this bounded 18-case restart matrix.

Pirates learn Windstorm through instructor 4272 and consume supplied tradable
scrolls through ItemUsage. Taoists learn Chain Bolt through instructor 10000.
The source skill level/previous-level/experience values are then controlled at
1/2/90. Actual rite NPC requests and the paid reincarnation request reset retained
skills to level 0, previous level 2 and zero experience or remove unrelated arts.
Repeated final requests leave one receipt and one payment. First/second rite
materials are supplied; paid characters buy dew through Sereth. First-rite
permanent unbound garment delivery and second-rite bound soul ownership are
explicitly checked, alongside material consumption and unchanged/free or exactly
charged CP balances.

The second phase runs after full game/PostgreSQL shutdown and restart, loads
each character's skills and items, checks its class/level/rebirth/skill metadata,
and compares exact scoped snapshots. The snapshot includes selected profile and
attribute fields, rebirth history/body/experience, complete skill/item rows,
first/second/paid receipts and the relevant instructor, purchase and exchange
receipts. It is not a full schema snapshot or an authenticated-client test.

The first run committed the first rite before a fixture snapshot query used a
nonexistent `id` column on the first/second receipt tables. The corrected query
orders their single-owner rows by `uid`; the next run recovered the committed
character without another rite, payment, reward or database reset. Both final
phases compiled with warnings as errors and passed. Private evidence logs are
`arena-process-pirate-five-art-{earn,reload}.log`. The dedicated checkpoint table
is `revora_five_art_process_probe`, UIDs 4294909201?4294909216; no retained career
identity was changed. Both game close paths and PostgreSQL shutdown completed.

This closes the enumerated five-art full-process learning/rite/reload batch.
It does not prove natural character growth, acquisition of supplied first/second
rite materials, native UI/rendering, every class-history combination, or an
exhaustive race/crash matrix. Initial qualifications, attributes, funding,
trained metadata and NPC approaches are controlled. The retained broader career,
native and deployment gates remain separate. No live service changed.


## Remaining evidence and checkpoint reconciliation (2026-10-02)

The original 24-ID queue and its later five-ID remainder are historical counts;
the subsequent Ninja, Monk, Warrior and Pirate/Taoist sections account for that
queue. They are not a claim that every installed spell or newer-class gift has
been audited. The parent roadmap now marks the completed Warrior instructor/
owned stance service and second/paid transaction/attribute work as complete,
while preserving their separate scripted-hit, historical-life and native gates.
This reconciles existing evidence, not a new gameplay run or build.

The official [rebirth skill guide](https://co.99.com/guide/skills/m_rebornsk.shtml)
was checked again on 2026-10-02. It still establishes random attack interruption
for Archer-to-Archer Freezing Arrow, and Poisonous Arrows at level 70+ using a
bow and ammunition each below level 70 after the listed other-class rebirths.
It does not supply the missing timing or patch-6711 status encoding. The current
attack dispatcher still has no logical 5000/5002 passive dispatch. Existing
unrelated Freeze/Poisoned handlers and status animations do not prove those
skills' intended effects. These remain an explicit implementation/evidence gap;
no duration, damage rule or raw-status mapping was invented or enabled.

Other roadmap parent entries can include both completed server subsets and
still-open native or catalog gates. They must be assessed by their dated child
evidence instead of replaying already completed fixtures. The broader remaining
compound-reward inventory is independently open, including owner worktree
changes that are not part of the isolated committed-source build.
