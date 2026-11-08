# Monk epic restoration

Reviewed 2026-09-16 against baseline `65a1099`. M6.55b4a implements Head Abbot's
ordinary scroll learning service. M6.55b4b now implements the ordinary hunted
earning route and owned weapon conversion. The shared inheritance rule and prior-life effect cleanup are now implemented
under M6.55b4c1. Mission 3526 now projects durable personal trial evidence;
its corrected navigation catalog is privately staged under M6.55b4c2a.
The introductory Sutta is implemented under the explicit Revora policy below.
Full combat parity and genuine-client acceptance remain open.

## Evidence and learning contract

The [official Monk skill table](https://co.99.com/guide/skills/m_monk.shtml)
and installed server/client level-zero rows agree on these logical identities.
The [2015 epic quest guide](https://co.99.com/guide/quest/2015/monk_epic_quest.shtml)
and installed scroll descriptions identify the matching payments and instructor.

| Head Abbot option | Art | Logical spell | Minimum level | Scroll consumed |
|---|---|---|---|---|
| 23 | Infernal Echo | 12550 | 40 | 3007568 |
| 24 | Grace of Heaven | 12560 | 50 | 3007566 |
| 25 | Wrath of the Emperor | 12570 | 15 | 3007567 |

All current Monk ranks 60..65 can learn an unlearned art with the exact owned
scroll. Training uses public Job Center 1004, installed Head Abbot 4271,
canonical authenticated ownership and six-pace proximity. This replaces the
old free level-140 path, repeated AddSpell calls and misleading rejection.
The compact menu asks players to bring a sacred scroll.

The official page distinguishes equipment needed to cast/activate the arts.
Training does not invent an equipped-weapon or personal quest-receipt gate;
class, catalog level and scroll ownership are authoritative here. Quest
admission's level-70/second-rebirth requirement is separate from learning an
owned scroll. Combat requirements are not verified by these instructor tests.

Read-only decoded catalogs, SHA-256:

- Server `database/magictype.txt`: `97ecb413566f5fea541090d1a1c1498ed2acf0874efa6056984583c65c187a71`.
- Client `ini/magictype.dat`: `618431607e8db81f5e8c6093d801329b73bd9a5af4aa57fc8a38348a832173c2`.
- Server `database/itemtype.txt`: `594ffe5f252c9445a6087004372519a6f150f089c7bff42d4ec30400911a0e88`.
- Client `ini/itemtype.dat`: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.

## Transaction and validation

`MonkTraining` holds the character lock and database owner lock, checks the saved
profile/location and exact scroll row, then commits debit, new skill and receipt
together. Bound copies are spent first. Legacy zero-stack singletons consume
one row; actual stacks consume one unit. Existing skills preserve every training
field and spend nothing. Failed writes roll back and require reload. The receipt
has no permanent character/spell uniqueness constraint that would block valid
later-life relearning. Existing skill/inventory projections follow commit.

Head Abbot's 2032 requests now use the existing bounded frame validation and
reach the service before legacy dispatch overwrites the echoed actor. No new
packet layout, native capture or live deployment is claimed.

**372 `-MonkTrainingOnly` checks** pass (266 promotion prerequisites plus 106
instructor checks). Ordinary, non-GM packet routes cover all three arts, exact
minimum levels, bound/unbound payments, all six ranks, simultaneous first
requests, wrong scroll/material rejection, repeated clicks and full reload.
They preserve practiced skill level/EXP/previous-level/soul metadata. Negative
cases include malformed frames, wrong actor, distance, trade/death/session
eligibility, locked/temporary/stored items, every participating suppressed
write, and saved profile/skill/item ownership conflicts. These fixtures seed
owned scrolls: they prove training, not an earned Monk Epic Quest journey.

The isolated build and three achievement-producer audits also pass. The producer
upper bound remains 224/337 IDs with 113 missing; this service does not establish
additional achievement earning paths.

## Implementation sequence

- [x] Verify installed terrain and client actor appearances; register missing
  runtime map/actors and validate actual movement and NPC collision.
- [x] Document explicit Revora odds, failure incense and scroll exchange prices;
  disclose them before each offering.
- [x] Add owned three-trial transactions and the Rosewood Incense-to-scroll route.
- [x] Provide ordinary flower hunting, durable pickup and a complete hunted
  material-to-skill journey.
- [x] Implement owned Boundless Heart weapon conversion with catalog-preserving
  gear metadata; the quest item is 3007564, not Inner Power books 3007115..17.
- [x] Apply the documented general Monk retention rule to the three confirmed
  epic arts and validate postcommit active-state cleanup through all three rites
  ([interpretation, evidence and 2,165 checks](REBIRTH_SKILL_AUDIT.md)).
- [x] Implement mission 3526 progress from durable trial proof and privately stage
  the corrected navigation links (native UI acceptance remains separate).
- [ ] Verify genuine 6711 gameplay/display before deployment.

The guide gives three red and three white flowers for the first trial, then
Spiritual Gem and Buddha's Necklace for the later trials. A failed second trial
returns to Fortune Arhat; a failed third returns to Altar Cleanser. Do not copy
the Ninja failure route blindly. Boundless Heart converts the right-hand beads
first; repeat for the second weapon. Rosewood Incense rewards from later trial
failures can fund skill scrolls. The runtime implementation and its deliberate
Revora economy are described below.

## Installed world investigation before M6.55b4b

Read-only public database queries on 2026-09-16 find Head Abbot 4271 at
Job Center 1004 (80,45), mesh 50038, talker type 2. No Golden Cicada,
Fortune Arhat, Altar Cleanser, Victory Buddha or White Dragon quest actor is
installed by name/known mesh. No map metadata exists for 3980..3989 or with
base map 3983. Proposed source-owned NPC IDs 199483..199487 have no database
or source collision at this baseline. These are proposed Revora identities,
not recovered official server IDs.

Both installed map indexes register **3983 -> map/map/epicm-monk.7z**.
The server already has its 116-by-116 floor. A bounded extraction of the
single archive entry `epicm-monk.DMap` shows version 1004/0, puzzle
`map\puzzle\epicm-monk.pul`, zero portals and eleven decorative type-4
objects. All 13,456 access cells exactly match the server floor; raw access
1 is blocked, 0 walkable. No terrain repair is needed.

| Input | SHA-256 |
|---|---|
| Server `database/maps/3983.map` | `43a5268b7a4fdb9696ad854586d533ef842ae96c5080abacccbc3f80a0fa05d5` |
| Client `map/map/epicm-monk.7z` | `535784fb5b7f954208733b4357476bf5c17ff7aa02db40f491ec81cf9d064e2e` |
| Extracted `epicm-monk.DMap` | `7153c64a7af8cd47f4a427fec0d56d681505d253a848ca017ef7ea39eab43ace` |
| Client `ini/Questinfo.ini` | `f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14` |
| Client `ini/npc.ini` | `b06db1e2484992a1b58eabd7a3d884da07ac728a4a183d0fd18f2dccd5b20497` |
| Client `ini/NpcX.ini` | `60107f543d4bdb861984ea5021a3713f735ea1956d97c4b74956ec00fe66a1ce` |

The installed NPC catalog supplies meshes 50970 Golden Cicada, 38420 Fortune
Arhat, 38430 Altar Cleanser, 38440 Victory Buddha and 38450 White Dragon.
Mesh 50970 is also used by Revora's unrelated Caelan Soul Trial; preserve its
actor and route. NPC identity and appearance are separate.

Candidate positions are (86,68), (79,81), (62,76), (60,55), (54,63), in the
order above, with landing (87,71). A four-neighbor Python flood fill after
blocking each actor's 3-by-3 footprint reaches 2,194 cells and a six-pace
approach to all five. This is a preliminary floor calculation; the runtime
fixture must still verify actual Map/Floor registration, collisions and player
movement. White Dragon shares the proposed 3983 hub as a Revora placement:
the official page calls its location Oriental Fairyland, while other actors
are in Western Paradise. A separate native map identity for that name has
not been established.

Native mission **3526** names the three trials, requires level 70 and classes
60..65, and advertises items 3007564/3007565. Its Head Abbot link is stale for
this server: NPC 8314 at (34,33), rather than installed 4271 at (80,45).
Do not claim working native quest navigation until that mismatch is handled.
Mission 3526 must not be completed merely by receiving or trading a Heart.
The 343 installed Prayer Beads IDs in family 610 have matching family-622
Divine Pillars preserving item suffix/quality and level. Family 623 has no
server item rows despite its appearance reference in Golden Cicada's outfit.
Use verified 610 -> 622 conversion only, with exact catalog validation.

Official material quantities and the trial graph are established above.
Published sources reviewed so far do not establish numerical success rates,
incense quantities or scroll exchange prices. The
[official 2016 player interview](https://co.99.com/news/2016-01-13/interview_of_first_monk_to_get_monk_epic_weapon_in_titan_sever.shtml)
only describes Spiritual Gem acquisition as difficult; it is not quantitative
rate evidence. Runtime work should state a deliberate Revora economy where
those values remain unavailable, and explain every charge/chance before
confirmation. No runtime map, actors, native mission or quest transactions
were installed by this read-only investigation. The subsequent source changes
below now register the world at startup, without deploying it live.

## M6.55b4b ordinary runtime and earned journey

Source startup now registers map 3983 with default status 8200, preserving
compatible operator metadata and rejecting conflicting identity/ownership.
The production Map constructor loads its existing floor. Initialization checks
the exact terrain hash, required items and NPC identity conflicts in both saved
data and loaded maps, then installs the five missing actors. Existing Caelan
and Job Center NPCs remain intact. The proposed coordinates and landing above
now pass actual production-floor and player-movement checks.

Head Abbot offers sacred trials and flower hunting. Golden Cicada explains
the route and converts either equipped hand. Fortune Arhat, Altar Cleanser and
Victory Buddha conduct their respective trials; White Dragon sells the three
skill scrolls. Every actor offers a free return near Head Abbot at (78,42).
Bruce returns Monk hunters there and Ninja hunters to their existing landing.

The world is available to ordinary authenticated second-reborn Monks/Saints
of level 70+, without GM status, VIP admission or an account allowlist. The
instructor can separately teach an eligible owner of a traded scroll, under
M6.55b4a's catalog levels.

### Explicit Revora economy

Numerical odds and incidental quantities below are **Revora policy**, not a
claim of recovered official rates. Flower payment and the three-stage item
sequence follow the documented quest. All trials have independent **50%**
success probability, with no CP charge, disclosed before confirmation.

| Actor | Offering | Success | Failure |
|---|---|---|---|
| Fortune Arhat | 3 Red + 3 White Flowers | Spiritual Gem | No item |
| Altar Cleanser | 1 Spiritual Gem | Buddha's Necklace | 1 Rosewood Incense |
| Victory Buddha | 1 Buddha's Necklace | Boundless Heart | 3 Rosewood Incense + 1 Spiritual Gem |
| White Dragon | 10 Rosewood Incense | One selected epic skill scroll | No random roll |
| Golden Cicada / owned Heart use | 1 Boundless Heart | Equipped Prayer Beads 610xxx -> 622xxx | Reject unsupported gear without spending |

The returned Gem on Victory failure makes the guide's retry at Altar possible;
it is an explicit Revora restoration policy. There is no copied Ninja map
return or automatic teleport between these actors. A scroll costs ten incense
regardless of art. Each flower retains one outcome in the existing 24-way
Abyss roll; the other 22 outcomes drop no quest flower. Eligible Monks now
share the proven origin-bound pickup service and existing
`revora_ninja_flower_pickups` receipt table. That historical table name does
not restrict it to Ninja characters. Legacy duplicate flower branches are
suppressed for the new eligible route while other monster loot is preserved.

### Ownership, replay and recovery

Offers expire after two minutes and capture inventory/equipment and character
state. Confirmation holds the owner lock, locks the saved owner, verifies
exact profile/location and all owned item rows, debits exact materials and
writes every reward plus operation/material/reward receipts in one transaction.
Any bound offering binds its output, including the returned Gem. Victory
reserves space for all four possible reward items before taking the necklace.

Equipment conversion changes only item type/binding in the saved row. It
preserves UID, level/quality, enhancement, sockets, refinement, perfection,
attribution and other metadata. All 343 supported pairs also exist in the
installed client catalog with matching levels. Direct Heart use prefers the
right hand and then the left; its durable short replay guard prevents a
repeated item request, including after reconnect, from immediately spending
a second Heart. Unsupported weapons and Inner Power's similarly named books
cannot enter this conversion.

Failed writes roll back payments and rewards; the session is fenced for reload.
Committed exchanges project items/stats only afterward. Lost ephemeral offers
require a new review rather than an automatic repeat charge. No permanent
one-per-character limit blocks legitimate repeat quests or later-life learning.

### Validation and remaining gates

The isolated build and **621 `-MonkEpicOnly` checks** pass, including the 372
instructor/promotion prerequisites. Fixtures use the production map loader,
registered ordinary NPCs, actual movement and actual monster death/pickup
handlers. They exercise all 32 flower families and a complete hunted-material
journey to all three learned arts and an epic weapon, followed by full reload.
The journey executes 31 committed quest exchanges with zero quest CP debit;
unrelated configured kill-currency drops are disabled only inside the fixture.

Negative cases cover class/level/rebirth/session admission, malformed frames,
actor mismatch, stale/cancelled/expired offers, concurrent confirmation replay,
capacity boundaries, saved ownership changes, RNG range errors, all suppressed
transaction writes and late failures after earlier material debits or three
already-written incense rewards. Heart tests cover both hands, enhancement
reload, immediate/reconnect replay, wrong materials and gear-write rollback.
The separate **800-check Ninja flower regression** passes, and the Monk fixture
also verifies a Ninja can hunt and return correctly while Monk support is
active. Three achievement producer audits remain unchanged at 224/337 IDs
with 113 missing sources.

This is source implementation and automated ordinary-player earning evidence.
No active-client files, production map/NPC rows, live server process or public
patch were changed. Native mission 3526 still has its stale client NPC link
and is not claimed integrated; no quest journal completion is fabricated from
item possession. Buddhist Sutta's optional introductory use/EXP is also outside
this route. Genuine 6711 display/combat acceptance remains unchecked under the parent
milestone; the subsequent inheritance implementation is documented in the
[shared skill audit](REBIRTH_SKILL_AUDIT.md).

## Durable native journal (M6.55b4c2a, 2026-09-16)

The earlier unintegrated-journal limitation above is superseded in source by
`MonkEpicJournal`. Mission 3526 is Available for eligible Monks until their
first actual trial attempt, Accepted after an attempt, and Finished after
personal successful Fortune, Altar and Victory receipts. Its three objectives
are historical booleans: failures and later attempts do not erase earned
success. This is an explicit Revora journal policy, not a claim about native
repeat-state semantics. Finished status grants nothing and does not prevent
another set of trials for the other weapon.

The overlay uses the existing operation receipts and never reads item
possession as progress. Scroll exchanges, awakening with a purchased Heart,
client Begin/quit messages and serialized legacy quest entries cannot author
its objectives. Historical evidence survives changing schools; characters
without evidence must satisfy the actual class/rebirth/level eligibility to
see Available. Existing quest blobs are preserved, with conflicting entries
suppressed only in the display. A replacement login gets a fresh SQL-derived
view; superseded connections cannot reuse the cache.

`tools/ClientPatches/prepare_monk_journal.py` stages only mission 3526's Begin
and Finish links from stale actor 8314 at (34,33) to actual Head Abbot 4271 at
(80,45), map 1004. It requires the approved DX9 executable and exact catalog
preimage, preserves unrelated bytes, refuses conflicting existing artifacts,
and retains an original copy plus rollback hashes under ignored
`obj/chi-refill-validation/monk-journal`. The original Questinfo SHA-256 is
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`;
the staged hash is
`bdcdae995799339ce1fb6e5e1fe8fd9f4359f4640e513365624bd52865f9ebc5`.
This is an unsigned private validation artifact, not an installed or published
patch. Genuine-client navigation and objective presentation remain unverified.

Validation: isolated build; **645 combined Monk checks**, including actual
trial results, immediate objectives, malformed/replayed requests, eligibility,
legacy-data preservation, cold reload, superseded sessions, repeat trials,
school changes and late-transaction rollback; **9 shared quest packet checks**
and **1,101 Abyss journal regression checks**.
Achievement producer coverage is unchanged at 224/337, with 113 missing sources.
No live deployment is included.

## Sutta introduction (M6.55b4c2b, 2026-09-16)

The [official Monk quest guide](https://co.99.com/guide/quest/2015/monk_epic_quest.shtml)
and installed item 3007561 support an introductory Sutta and 30-minute EXP.
The original acquisition method, repeat allowance and remote-use transport
were not established. These parts are **Revora policy**, not asserted retail
behavior: Head Abbot's "A sacred text" option gives each eligible second-reborn
Monk/Saint of level 70+ one bound permanent book, free. Reading beside the
actual Head Abbot in Job Center grants the introduction's EXP once per
character. Use elsewhere keeps the book and directs the player to (80,45);
it does not bypass event exits or teleport restrictions.

The gift and reading have separate durable owner-keyed receipts. A pre-existing
owned Sutta may be read even without a gift receipt, but the same character
cannot collect another reward through a traded book, stack, later rebirth,
reconnect or concurrent request. A stacked book loses exactly one unit.
Reading first prevents a later redundant free gift. A lost or discarded gift
is not automatically reissued; the introduction remains optional and does not
gate the actual epic trials.

The EXP amount uses the existing time-reward convention, half the character's
level-based EXP Ball amount. It consumes no daily EXP Ball allowance and follows
the shared level, attribute, mentor and rebirth-recovery transaction path.
At the level cap or while cursed, the book is kept for a later eligible use.
The exact owned book debit, profile progression, dependent EXP effects and
lifetime reading receipt commit together. A rollback preserves the material
and reward eligibility; cache/item/EXP projections occur only after commit.
No item purchase, introductory reading or gift creates successful trial
evidence for mission 3526.

Validation: isolated build and **694 combined Monk checks**. The added cases
cover the actual ordinary Head Abbot gift and native item-use request,
concurrent duplicate gifts/readings, all six Monk ranks, bound/unbound existing
books, stacked debit, full bag, cap/curse/ineligible/foreign-item rejection,
malformed item packets, level-up and cold reload, forced profile/item/receipt
failures and reconnect retries; **452 shared EXP checks** and **3 producer
audits** also pass (coverage remains 224/337, not per-character proof).
Genuine 6711 presentation remains open; no live deployment or
active-client change is included.

## Guaranteed P7 awakening reward (2026-09-21)

Every newly confirmed final weapon awakening now includes one bound catalog
P7 soul. The existing conversion preserves the owned weapon's enhancements,
attribution and binding rules; this change does not replace it with maximum gear.
Intermediate trials, failed draws and scroll exchanges do not award this soul.

The soul item and its existing epic-reward receipt commit in the same transaction
as material consumption, the weapon conversion and operation receipt. Capacity
reserves one reward slot after actually exhausted material stacks. A stacked
Heart/Eye that remains in a full bag cannot finance an awakening until room is
available. Failed soul insertion rolls back the material and weapon changes.
Existing item-use replay guards and duplicate operation protection remain active.

This service uses short-lived in-memory confirmation offers, not a saved future
quest payout contract. New confirmations use these terms; historical operation
receipts are never reopened or retroactively paid. NPC confirmation and success
text explain the additional bound soul. Existing bound offerings still bind
the converted weapon, while the new soul is always bound.

Validation covers ordinary earned trial routes through final conversion, soul
catalog identity and receipt ownership after production-loader reconnect,
full-bag rejection, immediate item-use replay and injected soul-insert failure.
Native journal/reward display and live deployment remain open.

Build and 701 Monk checks pass. Log: `obj/chi-refill-validation/monk-p7-awakening.log` (ignored).


## Hunted route and retry across game processes (2026-09-27)

`tools/Verify-ArenaProcess.ps1 -MonkOnly` covers three private game processes,
including a PostgreSQL stop/start after forced termination of the first game.
The ordinary non-GM character has supplied class 65, second rebirth and level 100;
it begins with no CPs or items and the fixture's 100,000 silver. The normal 1009
purchase dispatcher buys Prayer Beads 610003 from silver shop 5, and the equip
handler places those purchased beads in the right weapon slot.

Head Abbot admits the character to the existing Abyss hunt. Six monsters taken
from the loaded map's live population are killed using controlled combat input.
Controlled flower draws produce three Red and three White Flowers, picked up
through the normal floor-item packet handler. Replayed pickups leave exactly six
owner receipts. No flower or trial material is inserted directly into inventory.
Ordinary monster CP/gold rewards are disabled inside this test process so quest
payment can be measured independently.

Bruce returns the character to Head Abbot, who admits it to map 3983. The earned
flowers pay for the successful Fortune trial, its Gem pays for Altar, and a
controlled Victory failure consumes the Necklace and awards three Incense plus
a retry Gem. After saving and an abrupt process exit, database/game restart
preserves that exact Gem UID and all three Incense. Retrying Altar and Victory
produces a Heart, which awakens the same purchased weapon UID into 622003 and
awards one bound P7 soul. Repeated confirmation grants nothing further.

The third process checks exact item UID/type fingerprints, the equipped weapon,
soul binding and loaded P7 catalog identity, six trial/awakening receipts, one
awakening, the original shop payment receipt, consumed trial materials, retained
Incense, zero CPs and the expected remaining silver. Production trial odds,
flower rates and prices are unchanged. This extends the earlier loader-reconnect
checks with actual process recovery; it does not exercise all three scroll arts
across processes or establish authenticated/native-client acceptance. Native
6711 presentation, private patch installation and live deployment remain open.

The historical reward paragraph above now reflects the current bound P7 policy.
No existing reward receipt or player item is retroactively changed.

Validation: all three phases passed against the existing candidate SHA-256
`440D5CFD930640C83A457AE69D51BE17E2C2C15DB537A95EFCA080485C89F65E`.
No production build or live restart was needed. Redacted local log:
`obj/monk-process.log` (ignored).
