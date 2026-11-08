# Warrior learning and rebirth restoration

Reviewed 2026-09-16 against `e42f59a`, with instructor work based on `6c86f60`.
M6.55b5a records the evidence; M6.55b5b1 implements the original twelve reviewed
catalog learning paths. M6.55b5b2b4a restores the thirteenth, Defensive Stance. The
inheritance and self-effect ownership work below closes M6.55b5c and part of
the cast audit; remaining combat, epic earning and native acceptance stay open.

## Version-specific evidence

Both installed catalogs were read without modification. The encrypted client
catalog was decoded in memory and re-encoded to the exact original before
reading logical IDs, level-zero names, learning levels and weapon columns.

| Logical ID | Installed name | Catalog minimum level | Weapon column |
|---:|---|---:|---:|
| 10470 | ShieldBlock | 40 | 900 |
| 11160 | DefensiveStance | 70 | 900 |
| 11200 | MagicDefender | 40 | 900 |
| 12660 | TwistofWar | 0 | 61624624 |
| 12670 | ScarofEarth | 15 | 61624624 |
| 12680 | Backfire | 60 | 61624624 |
| 12690 | WaveofBlood | 15 | 61624624 |
| 12700 | ManiacDance | 0 | 61624624 |
| 12770 | Pounce | 60 | 900 |

The rows agree between server and client. The weapon column is recorded as raw
catalog evidence, not a new interpretation of every encoded subtype. The
server loader decomposes it into three decimal groups. A learning requirement
is separate from the equipped-weapon requirement enforced when casting.

SHA-256 provenance:

- Server `bin/Debug/database/magictype.txt`:
  `97ecb413566f5fea541090d1a1c1498ed2acf0874efa6056984583c65c187a71`.
- Client `6711/ini/magictype.dat`:
  `618431607e8db81f5e8c6093d801329b73bd9a5af4aa57fc8a38348a832173c2`.

The [official Warrior guide](https://co.99.com/guide/skills/m_warrior.shtml)
identifies Warrior God as the instructor for the non-book skills. It explicitly
excludes Backfire from recovering its former training level after rebirth.
That exception must be applied to previous-level metadata, not just current
level/EXP. The page's present Backfire minimum is 100, whereas both installed
6711 catalogs say 60. Its Magic Defender prose says 70 while its own table and
both catalogs say 40. Its two level-zero fist skills use a published minimum
of 15; the catalogs contain zero. These are discrepancies to resolve explicitly,
not reasons to claim the current guide precisely describes 6711.

[Patch 6323](https://co.99.com/news/2016-09-08/patch_6323__new_version_invincible_warrior_.shtml)
dates the fist expansion to September 2016, before the target client. The
[Invincible Warrior guide](https://co.99.com/guide/classes/invinciblewarrior/index.shtml)
separately identifies the five fist arts and their weapon-use requirements.
No required learning scroll for those five was established by these sources.

## Source defects established

- Warrior God 10001 uses separate, non-atomic legacy `AddSpell` calls. Option
  12 grants five skills at 140, including the shield-based Pounce under an
  epic label. Option 5 grants Accuracy, Shield, Superman and Maniac Dance
  together at 40. Existing spell metadata is not protected by a transaction
  coupling the profile, learned skill and an instructor receipt.
- Option 6 advertises Dash 1051 at 61. **Neither installed catalog contains
  logical ID 1051 at any level.** Pounce 12770 does exist in both. Do not
  manufacture a 1051 row or silently claim that the two are the same skill.
  Replace the obsolete menu entry with an accurately named, separately
  evidenced skill path.
- Shield Block and Magic Defender each call `AddSpell` twice. Defensive Stance
  option 10 is hidden in the menu but still reachable by an authored request.
- Defensive Stance's cast case 11160 is entirely commented out in
  `Game/Attacking/Handle.cs`; existing damage formulas still inspect its state.
  Learning it alone cannot be reported as a working defensive skill.
- All nine listed IDs are absent from `RebirthSkillRules` school 2. The shared
  generic previous-level preservation also lacks Backfire's exception.
- The existing Maniac Dance and Backfire cast branches write the timer to
  `attacked` but the flag/stamp to `attacker`. Their owner, learned-skill,
  equipment, timing and post-rite lifecycle need separate validation before
  claiming combat parity.

## Implementation sequence

1. Replace the reviewed instructor selections with nearby ordinary-player
   transactions. Preserve already learned metadata, reject forged actor/frame
   requests, and resolve the documented learning-level differences explicitly.
   Keep the Flying Moon book route and promotion service separate.
2. Repair or explicitly disable the nonfunctional stance path while validating
   actual shield/fist casting ownership. Correct the target/caster timer mixups
   with focused real-handler fixtures; do not infer a new wire layout.
3. Classify the confirmed school arts, implement Backfire's exceptional reset,
   and retire committed prior-life modes through first, second and paid rites.
   Verify failed rites preserve the original state and cold reload preserves
   the intended metadata. General school retention remains an interpretation
   unless a more specific Warrior exception is found.
4. Audit and implement the separate [Warrior Epic Quest](WARRIOR_EPIC_QUEST.md)/owned fist earning
   route, then validate actual 6711 menus, icons, weapons and effects before
   deployment. Instructor learning alone is not a complete epic earning path.

No active-client file, production database row or live process was changed by
this research. No proprietary catalog rows or client assets are added to Git.

## Ordinary instructor implementation (M6.55b5b1)

`WarriorTraining` replaces the legacy Warrior God case, preserving promotion
options 1/3 and using the actual public Job Center actor. Its main menu offers
promotion, shield/XP arts and individual fist lessons. Opening the fist menu
at 140 grants nothing automatically. Already known skills retain their level,
previous level, EXP and soul metadata through repeat requests and reconnects.

| Option | Lesson | Minimum level | Current class |
|---:|---|---:|---|
| 5 | Superman / Accuracy / Shield / Roar | 3 / 15 / 15 / 40 | 20..25 |
| 6 | Pounce 12770 | 60 | 20..25 |
| 9 | Shield Block 10470 | 40 | 20..25 |
| 11 | Magic Defender 11200 | 40 | 22..25 |
| 20 | Twist of War 12660 | 15 | 20..25 |
| 21 | Scar of Earth 12670 | 15 | 20..25 |
| 22 | Backfire 12680 | 60 | 20..25 |
| 23 | Wave of Blood 12690 | 15 | 20..25 |
| 24 | Maniac Dance 12700 | 15 | 20..25 |

Version policy: use matching installed learning levels where nonzero, including
Backfire 60. Use the published 15 floor for the two fixed fist arts with zero
catalog minima. Magic Defender requires the level-40 promotion (class 22+)
as an explicit interpretation of the guide's promotion language; its table
and both installed catalogs support level 40 despite conflicting prose.
Learning remains free, as in Revora's existing instructor; equipped-weapon
checks are separate cast responsibilities. This table is not a claim of exact
current retail learning policy.

Option 5 teaches only currently eligible missing XP arts. All newly taught
arts in that selection share one transaction with the profile and individual
receipts. A failure on the fourth art therefore rolls back the previous three.
All selections require a living canonical ordinary player, the actual active
nearby actor, correct class/rank/level and saved profile/location. A nonzero
actor echo must match 10001 and malformed NPC frames are rejected early.
Skills are projected only after commit; an unexpected saved-row conflict
requires reload. The startup catalog check runs in `CreateTournaments`, after
`SpellTable.Load` in `Server.Run`.

At the initial instructor milestone, hidden option 10 granted no skill and
returned a brief unavailability message; the menu omitted it without deleting
existing saved skills. The Defensive Stance restoration below supersedes that
temporary restriction with ordinary learning and owned casting. No new Dash 1051 row is fabricated. Pounce is presented
under its own identity, not asserted to be an alias of Dash.

Validation: isolated build and **330 combined trainer/promotion checks**,
including all eight individual boundaries, all six Warrior ranks, grouped XP
thresholds, non-GM requests, concurrent replay, metadata reload, the inactive
stance, missing Dash replacement, malformed/foreign requests, profile/skill/
receipt failure injection, late group rollback and saved ownership conflicts.
The adjacent Archer/Assassin instructor's **310-check regression** also passes.
Three producer audits remain at 224/337 IDs and do not prove per-character
achievement completion. This is source-only; native menu/skill acceptance and
live deployment are not included.

## Inheritance and owned self-effects (M6.55b5c / M6.55b5b2a)

The nine installed newer arts now belong to school 2 in both shared rite
planners. Already learned arts are retained/reset for a Warrior destination
and removed for other destinations through first rebirth, second rebirth and
paid reincarnation. This follows the published general school rule as an
explicit interpretation; no more specific Warrior retention exception was
established. The planners never grant an unlearned newer art. Legacy learned
Defensive Stance was classified before its learning/casting restoration below.

Backfire has a separate reset helper shared by both planners: current level,
EXP **and previous level** become zero. Other retained skills preserve previous
training as before, while soul metadata remains intact. This encodes the
[published Backfire exception](https://co.99.com/guide/skills/m_warrior.shtml);
it does not claim the current generic trainer implements every retail skill
recovery behavior or migrate historical character rows automatically.

After a successful rite, `WarriorModes.Clear` removes that character's Warrior
flags, shield/stance/defender states, XP/KO bookkeeping and associated timers
before the existing equipment/stat recalculation. Shield detail resets reuse
the existing 113/128 display projections. Failed transactions leave those
states intact. The initial cleanup avoided legacy `RemoveMagicDefender`, which
cleared every teammate without an effect owner identity. The later owned
Magic Defender service below replaces that helper; another character's
buffs are not removed by this character's rite.

The actual Maniac Dance and Backfire dispatch cases now call an owned
self-cast service. They require a live canonical Warrior, the learned art and
two distinct equipped, nonbroken subtype-624 fists. Existing resource/cooldown
checks apply. State, KO counters and timers are assigned to the caster, never
the supplied target. Backfire uses its catalog duration; Dance retains the
existing fifteen-second duration. Recasting an active effect cannot extend it.

The production Dance timer calls the same owned helper used by fixtures. It
requires learned/equipped/class ownership and the active effect, shares the
character lock with the rites, and claims a pulse at most once per second.
The first scheduled observation after casting can pulse immediately; a stale
timer timestamp from before the cast cannot pulse it. Expired, removed-skill,
dead, changed-school or unequipped effects cannot deliver delayed pulses.
Existing damage calculations remain unchanged in this batch.

Validation includes ordinary instruction before all three rites, same/other
school destinations, forced transaction failure, full metadata reload and
trainer replay; legacy stance is explicitly seeded only for persistence
coverage. Effect fixtures exercise actual decoded spell dispatch with another
player as its supplied target, paired-fist and ownership rejection, timer
expiry, concurrent duplicate suppression, nearby monster damage and cleanup
with a real teammate. The final Warrior fixture passes **2,176 combined
checks** (including 2,115 existing paid/second/first/Assassin checks). The
separate **2,165-check Monk** and **2,181-check Ninja inheritance regressions**
also pass. Three producer audits remain unchanged at 224/337 IDs, with 113
missing sources; this count is not per-character achievement proof.

Remaining combat work is substantial. The Defensive Stance, Magic Defender
caster-ownership and Backfire counter follow-ups
are described below. Other fist
damage/XP and exact native presentation also need review. This milestone
does not certify overall Warrior balance or combat parity and is not deployed.

At the earlier counter review, the installed Backfire levels 0..6 have powers
55/60/65/70/75/80/90, percent 100 and duration 10, matching the published HP
percentages. The then-existing damage branch instead tested a 30% chance, sometimes
replaced incoming damage with a random 10,000..50,000 amount, then left the
effect active and changed incoming damage to 1. The earlier note that this branch preceded event protection was incorrect:
`ReceiveAttack` already checks all three owned events at its entry, before
this branch; later checks are duplicates. At that milestone, one-use claims,
stamina restoration and actual hit fixtures were still pending.


## One-use Backfire counter

The follow-up replaces the old random counter with one activation owned by the
canonical player session. Catalog level at cast time fixes its power. On the
first accepted damaging hit within ten seconds, it consumes the activation,
removes the visible flag, counters for floor(current HP * power / 100), capped
at 50,000, and restores 30 stamina. The existing 15,000 ms skill cooldown remains
in force after consumption. The shared training-ground cooldown exception is
unchanged. A zero-damage, reflected, self, expired, foreign-map or invalid-owner
hit cannot consume it. The learned art, Warrior class and two usable equipped
fists must still be present when the hit arrives.

The power, duration, cooldown, one-use and stamina terms agree with the installed
catalog and [official skill guide](https://co.99.com/guide/skills/m_warrior.shtml).
Revora resolves this reaction from **pre-hit current HP**, then lets the original
strike follow its normal path. This ordering is an explicit implementation
interpretation, not a native-client/retail timing claim. Backfire has no documented
physical immunity, so the former incoming-damage-to-1 override is removed. Stamina
uses Revora's existing natural-regeneration ceilings (100 ordinary, 160 blessed)
and never reduces an already higher value; this does not certify retail stamina
caps or unrelated regeneration formulas.

Cast, counter claim and successful-rite retirement share a short per-session mode
lock. Incoming attacks never acquire the defender's inventory lock. The claim
finishes before event scoring, damage and death callbacks run, avoiding an extra
cross-character lock dependency. A claim accepted before retirement may finish
its already accepted counter; retirement prevents later claims. Concurrent hits
cannot claim the activation twice. Cast power is a snapshot, so skill training
during an active effect cannot silently strengthen that effect.

The normal player hit handler retains its initial event leases and protection.
A counter scores for the defender and uses normal death handling. It does not
call the attack handler recursively. Ordinary monster attacks have a separate
legacy dispatch: its 26 direct damage branches now call the same owned claim
through event protection. Thrilling Spook and Terato's separate owned combat
services also call it after encounter admission. Their normal victory handlers
remain responsible for durable kill evidence, quest credit and boss rewards.

Native Backfire animation, exact client/retail strike timing, other fist damage
and skill-training formulas, and Magic Defender/Defensive Stance native acceptance
remain separate unfinished work. Nothing in this batch is deployed.

The separate guard/adventurer and summon integrations are recorded below. Full
Backfire combat acceptance remains within the open parent milestone, alongside
native checks.


Validation: isolated build; **2,229 combined Warrior checks**, **613 Exorcism
checks** and **277 Grotto/Terato checks**. The Warrior fixture learns Backfire
through the actual ordinary-player instructor before its combat cases; individual
higher levels are selected as numerical fixtures, not represented as completed
skill-level earning journeys. Cases cover every catalog percentage, cap, stamina,
repeat/cooldown, invalid and stale ownership, cast-level snapshots, concurrent
real hits, player deaths, ordinary melee/magic monster dispatch, and an actual
Elite PK match's preparation, score and lethal result boundary. Spook/Terato
fixtures cover nonlethal and lethal named attacks and committed victory/Study
Point outcomes. These boss-specific cases seed the learned art to isolate combat;
the instructor earning route is independently exercised by the Warrior suite.
Three producer audits remain at 224/337 present, 113 missing; no new achievement
producer or per-character completion claim is made.

## AI and companion Backfire (M6.55b5b2b2a)

Actual guard strikes and adventurer retaliation now invoke the same counter.
An AI attacker must be the exact fake body in both its live session and its
guard/adventurer registry. A name prefix is insufficient. These actors receive
no player qualifier credit. Their normal death handlers still schedule revival
and clear retaliation. A lethal street-officer counter sends the player to
Darius; defeating Darius with a counter releases the player. Both location
changes persist. The old strike stops if that death changes the defender's map.

Summons require a canonical ordinary owner on the same map and exact identity
in the appropriate existing registry. Single companions and shadow clones also
require map registration; the separate pet collection owns its bodies directly.
The map can register companions in either dictionary based on UID. A replaced
ownership slot cannot consume the defender's Backfire.

`Die(Entity)` now clears only the defeated companion's slot. In particular,
killing a clone leaves the owner's other clone, collection pet and single
companion intact. This is not a claim that every legacy death overload or map
dictionary operation has been redesigned for concurrent removal.

Validation: **2,255 combined Warrior checks**, **37 guard checks**, **28 adventurer
retaliation checks**, **57 companion navigation/combat checks** and **2,181 Ninja
rebirth/clone regression checks**. The database
fixture learns Backfire through the ordinary instructor, exercises guard
sentencing/release and checks saved map IDs. It creates collection pets and
clones through their real creation methods, supplies a single-companion fixture,
then exercises real monster attack dispatch for lethal and nonlethal counters.
Replacement tests cover all three ownership slots. Synthetic HP/attack values
isolate these boundaries; they do not prove a summon acquisition journey or
retail damage balance. The in-memory AI fixtures seed the learned skill and
fists; they are combat tests rather than additional earning-route evidence.
Native animation, exact retail ordering and deployment remain pending.

## Magic Defender source ownership (M6.55b5b2b3a)

Baseline `ba51e71`. The installed server catalog gives 11/12/13/14/15-second
durations, range 4, 30 stamina and a 20,000 ms cooldown. The
[official Arabic guide](https://qahr.99.com/guide/skills/m_warrior.shtml) agrees
with those durations; the current
[English guide](https://co.99.com/guide/skills/m_warrior.shtml) lists 22..30.
This implementation preserves the installed duration profile. Both guides
describe the shield requirement, four-pace team protection, movement interruption
and the caster's BP as the comparison source. An attacker at least 20 BP above
a given caster defeats that source. These sources do not settle every regional
or historical rule difference.

The actual decoded 11200 cast now requires a live canonical Warrior with the
learned available art and an equipped, unbroken subtype-900 shield. Main-hand
emptiness no longer causes the generic weapon check to reject a valid shield.
The character lock serializes resource/cooldown admission; a live recast does
not extend the effect or charge twice. The existing training-ground resource
and cooldown exemption remains unchanged. Duration and level are captured at
cast time. A stale queued refresh cannot erase a newer activation.

A session owns its active cast, original map/coordinates and expiry. Recipient
flags and compatibility fields are display projections, never evidence of an
active caster. Both `Calculate.Magic` and `Calculate.Magico` check self and exact
canonical teammates on the same map within four paces. They compare attacker BP
with each caster, not the recipient. A teammate cannot relay protection, and
one caster ending an effect does not revoke another caster's shield.

Coordinate/map changes interrupt the source even when a caster returns to the
same tile. Death, disconnect cleanup and committed life cleanup retire it.
Losing the learned skill, usable shield or canonical session invalidates the
source when checked. Team identity and membership are rechecked at every damage
decision. Recipient movement, periodic observation and cast/cancel operations
reconcile remaining duration. Team/equipment changes are reflected by the next
periodic refresh, while damage decisions already use current eligibility.
Refreshes never acquire another character's inventory lock. Display reconciliation
can follow concurrent world changes on the next tick; it is not damage authority.

The old three-BP checks, recipient-owned immunity, copied full-duration timers
and blanket teammate removal are removed. The generic attack handler no longer
cancels the aura before every spell request; the documented movement rule drives
cancellation. Existing status/detail 128 and spell-use packets are reused.
This certifies ownership and the two common magic calculators, not every legacy
scripted boss/guard hit that bypasses them. Those damage classifications and
genuine-6711 display acceptance remain separate open work. No live deployment.

The focused disposable fixture (`Verify-PotencyHunt.ps1 -WarriorDefenderOnly`)
passes **133 checks**, including its material-earning prerequisites. It obtains
the art from Warrior God with ordinary characters, then tests all five durations,
exact stamina, an empty main hand, invalid casts, concurrent duplicate requests,
19/20-BP boundaries, both real magic calculators, actual monster magic dispatch,
four/five-pace boundaries, movement, overlapping casters, stale flags, membership,
equipment/skill loss, session replacement, retirement, real player death, the
disconnect cleanup service and cold reload. Higher skill levels and BP bonuses
are numerical fixture inputs, not evidence of their complete earning journeys.
The full Warrior suite passes **2,314 checks**. Companion navigation/combat
(**57**), guards (**37**), adventurer retaliation (**28**) and Grotto/Terato
(**277**) pass; the three
producer audits remain at 224/337 present and 113 missing. These checks do not
certify new achievement routes or overall class balance.

### Remaining scripted-damage audit

At `98a72cd`, the separate monster dispatcher has 26 Backfire call sites: 22
use fixed random-damage branches rather than the common physical/magic
calculators, and the final four use those calculators. The fixed branches
include Ice Queen/banshees, Chaos Ripper, a shared named-boss group, Dragon
King/Lava Beast and White Dragon/Sun Devil. They display spell IDs such as
30010/30011/30012 and 7013/7014/7015/7017, but a `SpellUse` animation alone does
not establish a physical, magical or special-damage rule. City guards likewise
use their own raw attack roll. These paths remain in M6.55b5b2b3b pending an
evidence-backed classification; neither shield skill is claimed to protect
against every legacy scripted hit. Spook's owned attacks use the melee
calculator, while Terato's owned phases explicitly choose melee or magic.
This audit changes no unrelated boss balance or encounter rewards.

## Defensive Stance learning and combat (M6.55b5b2b4a)

Baseline `98a72cd`. Warrior God now offers option 10 to ordinary level-70+
Warriors with the level-70 promotion (class 23+). It uses the existing atomic
profile/skill/receipt transaction, preserving a trained skill on retry or
reconnect. The instructor has nine individual newer arts and four grouped XP
arts. Opening a menu still grants nothing; the player selects the lesson.

The active stance requires the canonical live owner, learned available 11160
and an equipped unbroken shield. A main-hand weapon is not required. Mounted,
flying and transformed characters cannot activate it. The accepted catalog
snapshot supplies 200% total physical defense for 120 seconds at level zero,
or 300% for 240 seconds at level one, with no stamina cost. A second decoded
cast cancels it. The installed server and English guide agree on that profile;
the Arabic guide's second-level 400% is recorded as a regional discrepancy,
not silently substituted into the 6711 rules.

The new owned calculation applies the defense multiplier once, including
legacy ranged paths and the common melee/ranged paths. It replaces seven extra
damage deductions and an unchecked skill lookup that used unrelated fatigue
or incompatible percentages. It does not mutate the character's base equipment
defense. Existing XP-shield adjustment runs before the common melee stance
multiplier; this combination is an explicit Revora interpretation, not native
retail stacking proof. Melee now selects transformation defense based on the
defender rather than the attacker. Magic defense is unchanged.

Actual jump, run, mounted-slide and mount-cast handlers reject those actions
while the stance is active. Walking remains available. Rejected movement
reuses the existing FlashStep position correction. Map changes, death,
disconnect cleanup, committed life cleanup, expiry and invalid skill/equipment
ownership retire the source; returning to a valid state does not revive a
retired activation. Queued observations from before a cast cannot clear it.
The server emits detail 126 on activation and clears it on retirement, even
when rebirth has already reset the compatibility fields.

Validation: isolated build; **111 focused stance/material checks**, **339
Warrior instructor/promotion checks** and **669 combat checks** (including 600
class pairs). The full Warrior suite passes **2,351 checks**, including the
stance cases. Companion (**57**), guard (**37**) and retaliation (**28**)
regressions also pass. The ordinary-player stance fixture learns through the NPC and
tests real decoded casts, all duration/percentage boundaries, rejected states,
movement/mount handlers, real melee/ranged/magic results, a transformed attacker,
first rebirth, and cold reload. Its outbound packet check uses a disconnected
fixture queue and a synthetic cipher key, decrypts in memory and confirms the
committed rebirth emits the stance detail reset. No live credentials, socket
listener or packet capture is involved. Higher skill levels are fixture inputs;
their complete training journey and genuine-client acceptance remain open.
The existing scripted-damage and broader Warrior balance caveats still apply.


## Defensive Stance earned training and cold persistence (2026-10-03)

M6.55b5b2b4c closes the server training gap while preserving the native and
scripted-hit classification gates. The common magic-dispatch tail previously
awarded100 base EXP even after a refused or cancelling stance request. Generic
skill advancement also used the current level's70 requirement rather than the
next level's100 requirement; intermediate EXP depended on a later general save.

The owned stance handler now awards the same100 base EXP only after valid
activation checks. It uses the existing server rate, gem, VIP and training-ground
multipliers with saturation at the installed27000 threshold. Practice below
level100 can fill that bar, but only a qualifying activation at100+ advances the
skill. Cancelling, missing a shield and terminal-level casts award no EXP. The
shared dispatcher excludes11160 from its generic EXP tail to avoid duplicate or
unearned credit. Other spells retain their existing path.

Training is saved before the effect activates or its skill update is sent. A
failed/uncertain save fences the session; reload recovers the saved skill state.
The current activation keeps its accepted catalog profile even when that cast
finishes training:120 seconds now,240 seconds on the next activation. The
[official Warrior guide](https://co.99.com/guide/skills/m_warrior.shtml), checked
2026-10-03, agrees with the installed70/100 character requirements and200/300%
profiles. It does not publish the training rate. The100 base EXP and below100
bar-capping behavior are documented Revora rules, not claimed retail parity.

Private candidate `career-stance-training-practice`, `-promote` and `-reload`
passed on a fresh ordinary identity4294918302:

- [x] Actual Warrior God lesson, silver purchases of a sword/shield, ordinary
  equipment requests and decoded11160 casts; no skill or EXP was granted.
- [x] An injected skills-write failure rolls back saved EXP, leaves the stance
  inactive and fences the session. A fresh owner reload recovers zero EXP.
- [x] At the loaded candidate rates, the first accepted activation earns900 EXP
  and saves it immediately. Cancellation and shieldless casting preserve it;
  reloading the owner preserves the partial bar.
- [x] Repeated valid activations reach27000 from zero, still at skill level0.
  A separate process preserves the full bar at character level70. Controlled
  level99 eligibility remains insufficient; controlled level100 permits level1.
- [x] The training-completing cast retains120 seconds; its next valid activation
  uses240. A further cold process restores level1/zero EXP without reviving the
  timed effect. Max-level activation/cancellation changes no saved skill, goods,
  lesson receipt or currency.

Fixture qualifications (class23, level70, attributes and starting silver) are
supplied. The99/100 character-level boundaries are controlled, not an earned
character-level journey. Cast timers are advanced/reset by the private adapter;
no production cooldown changed. The ordinary skill training itself starts at
zero. An earlier development identity8301 is retained separately and is not the
final earning proof. The final run also distinguishes cancellation's generic
EXP leak from the initial mistaken assumption that the stance awarded no EXP.

An isolated build passed; private executable SHA256:
`43EF1336911F685C79E7C2D84A0908CA4A136FCBC8D6D23B1BC7F13D8C7EFA09`.
The loaded candidate still emits its unrelated vote PostgresException. No live
restart, client change, reward rebalance or deployment occurred. Native stance
presentation, broader combat balance and scripted boss-hit classification remain
open; this does not close the compatible320-achievement career.


## Legacy boss dispatch follow-up (2026-10-03)

Overlapping legacy name groups and the generic fallback could apply multiple
hits in one monster callback. The [single-hit dispatch fix](LEGACY_BOSS_HIT_DISPATCH.md)
reproduces2,925 damage before repair and validates112 scripted hits across14 names,
85 generic-spell fallbacks, ordinary melee and cold health recovery afterward.
The22 raw-damage sites' physical/magic/special classification remains open;
this does not complete the broader shield-skill or native-combat gates.
