# Taoist Epic restoration contract

2026-09-27. The server earning route is integrated into normal startup and
verified in private game/database processes. Native-client acceptance and live
deployment remain pending. Earlier staged checkpoints below retain their
historical scope; this current checkpoint supersedes their activation gates.

## Normal-startup activation

`Server.Run` prepares the temple and `Thread.CreateTournaments` initializes the
quest before Account/Game listeners open. The acceptance probe now requires the
quest and actors to exist; it does not call initialization itself.

The Revora economy is 27 CPs for the optional qualifying first trial, one/three
Fish for failed Elite/Super trials, 10 Fish per scroll and 50 per Hossu. These are
explicit local defaults, not verified original prices or a claimed owner reply.

The isolated build passed. `Verify-ArenaProcess.ps1 -TaoistRouteOnly` passed all
three phases against SHA-256
`1A79A9C214C00D255D6FE6464ACCF7F358CC5981BC21CE11D0CC332EDF5862DF`.
The non-GM route buys its input weapon and hunts six flowers, then verifies oven
progress, interrupted confirmation, same-UID conversion and the bound P7 reward
across game/database restarts. Character qualifications and silver are supplied;
combat/draw outcomes are controlled. This restores the supplier evidence for
mastery11222. Combat mastery is now verified at the bounded scope below; the
320-achievement career remains unproven.


The shared `-MonkOnly` and `-NinjaOnly` regression suites also passed all three
process phases each against this same build, including reward restart checks.

## Original-rule evidence

The [official 2015 quest guide](https://co.99.com/guide/quest/2015/taoist_epic_quest.shtml)
requires second rebirth and level70+. Taoist Moon admits players to the temple.
Three Red and three White Flowers pay for the first oven; a qualifying paid
alternative costs49 CPs. Each of the three successive stages has a10% success
chance, progressing through Treasure Leaf and Star Crystal to Divine Panacea.
The Panacea transforms the right-hand backsword. Later-stage failures yield
Yin Yang Fish, exchangeable for rewards including a left-hand Hossu. Scrolls teach
Taoist epic skills. The guide does not publish the missing fish quantities or
exchange prices. Its two Taoist Moon coordinate references disagree, so neither
is treated as a verified installed actor coordinate.

## Installed numeric identities and contradictions

The loaded item catalog independently maps3006013 to Treasure Leaf,3006014 to
Star Crystal,3006015 to Yin Yang Fish and3006016 to Divine Panacea. It identifies
3006017/3006018/3006019/3006020 as the Flame Lotus/Aurora Lotus/Breaking Touch/
Blessing Touch scrolls.
Only numeric facts and provenance belong in Git, not the proprietary catalog.

Legacy NPC46554 removes the wrong flower identities in two separate writes,
charges27 CPs without the original published eligibility check, and grants the
Leaf unconditionally.47554 converts one or five Leaves unconditionally.48554
offers separate final items rather than a reviewed failure result.48654 then
consumes Panacea for619439 (Hossu), or Fish for620439 (backsword). Item use instead
consumes Panacea and inserts a new620439, losing any notion of converting an
existing weapon. These are inconsistent legacy implementations, not two equally
valid original meanings. The official guide plus installed material identities
establish Panacea as the backsword conversion material; Fish is not that material.

## Implementation sequence

- [x] Resolve material identity and final weapon purpose with independent evidence.
- [x] Verify the installed temple index and server terrain, including reachable
  approaches to all five proposed actors after reserving their footprints.
- [x] Verify actual loaded actors and ordinary entry/exit in private acceptance.
  The route uses3933; legacy10332 teleports are intercepted when it is enabled.
- [x] Build the staged transactional trial/conversion store and verify core
  offerings, rollback and exact converted-weapon recovery across process restart.
- [x] Replace the whole oven chain with eligibility/proximity checks and one
  transaction per offering/result. Add deliberate confirmation, owned material
  preimages, capacity checks, operation receipts and retry-safe recovery.
- [x] Record explicit Revora choices wherever the original guide leaves fish
  counts/exchange prices unspecified or the existing27-CP economy is preserved.
  Do not label those choices original rules or silently adopt a contradictory sink.
- [x] Privately convert the selected owned right-hand421-family backsword to its compatible
  620-family suffix, preserving its enhancements and binding, and atomically grant
  the owner's one-time-quest bound P7 reward. Check every loaded catalog mapping;
  this is not an equip/render test of every individual weapon.
- [x] Integrate private flower acquisition for eligible Taoists while preserving
  existing Ninja/Monk ownership, drop rates and routes.
- [x] Validate private Fish exchanges and four matching scroll-learning routes,
  including binding, wrong-class rejection, rollback, replay and restart.
- [x] Project mission3418 from committed trial receipts and preserve its history
  after material consumption, forged journal requests and process restart.
- [x] Check private item framing, scroll eligibility and obsolete flower actions;
  protect Panacea conversion and intercept both old material-burning exchanges.
- [x] Replace conflicting NPC and item-use paths together. Test ordinary acquired
  input weapons/materials, failed draws, full bags, stacked/bound sources, replay,
  rollback, lost replies and actual restart, with no new GM/account allowlist.
- [ ] Validate genuine6711 presentation and preserve signing/deployment gates.

Neither620 mastery11222 nor the full320 career is certified by this contract.
A fixture-created Divine Panacea is consumer coverage, not completion of the
quest's acquisition chain. Existing historical item claims are not reopened or
retroactively paid.

## Temple prerequisite evidence

The approved local client's reviewed `ini/GameMap.dat` identifies3933 as
`map/map/epicm-taoist.7z` (opaque field256). Its SHA256 is
`86a54928a9682617371542cdb5a72707c799b92a5c820e3581183adaa94f32b0`.
The private candidate's3933.map is132 by132,17432 bytes, SHA256
`e0ad4f2c8242e8722f7a12679946012c5d972cbd0339d563d2dc8ea9e09e9716`.
These are installed identity and server geometry evidence, not a comparison
against the client's decoded DMap or native rendering acceptance.

`tools/check_taoist_temple.py` checks those exact inputs without starting a
server or modifying them. The proposed landing84,77 reaches495 cells using
cardinal moves only after reserving each proposed talker's3-by3 footprint,
matching `Map.AddNpc`'s placement rule. All five guide locations have reachable
approaches within two cells: Sun King81,75; Refined Oven80,80; Elite Oven64,64;
Super Oven48,48; Pure Taoist76,83. This establishes usable terrain for integration;
it does not establish runtime NPC identities, admission or reward handlers.

Reproduce from the repository:

```powershell
python tools/check_taoist_temple.py --client C:\Users\Administrator\Desktop\Revora\6711 --database C:\Revora\DisposablePostgres\moon-box-candidate\game\database
```

The next integration must check persisted/global NPC identity collisions,
Taoist Moon's actual loaded position, and entry/return handling before enabling
the replacement quest. No public deployment or client patch was made by this check.

## Staged transaction implementation (2026-09-27)

`TaoistEpicStore` and `TaoistEpicRules` implement the three-stage material chain
and owned right-hand backsword conversion. The subsequent coordinator described
below provides NPC and item-use dispatch, but has no production initializer.
This remains a private candidate rather than a released fix.

Each offering locks the owner, validates inventory and weapon preimages, and
commits payment, material debit, rewards and operation receipts together.
Conversion changes the same weapon's identity and inherited binding, preserving
its remaining metadata, and inserts a bound P7 soul in the same transaction.
Both source and corresponding620-family target must exist in the loaded catalog.
In particular421301 has no620301 counterpart and is rejected; the private test
buys compatible421005 from shop5 and converts it to620005.

The inactive draft economy keeps27 CPs for the optional first trial, three of
each flower for its free alternative, and10% independent success per stage.
Draft failure/exchange prices are1/3 Fish for Elite/Super failure,10 Fish per
scroll and50 per619439 Hossu. These quantities and opening paid trials to every
eligible Taoist are explicit Revora choices, not verified original rules.
The owner preference question remains pending; these values must be settled
before activation. Private exchange and learning coverage is recorded below.

Validation completed against private build SHA256
`D59546506399A0BA97EC1570AA56E6786A199870CED3AF64D98443512F659D27`:

- `tests/TaoistEpic/Verify-Rules.ps1`:1084 checks covering profession, rebirth,
  level boundaries, all100 trial draws at each stage, invalid draws/stages,
  scroll identity and missing/wrong-family conversion targets.
- `tools/Verify-ArenaProcess.ps1 -TaoistStoreOnly`: two separate game processes
  with a PostgreSQL restart between them. An ordinary non-GM fixture executes
  free/paid failures and successes, later-stage Fish outcomes, and conversion.
  Qualifications, CPs, flowers and weapon enhancements are supplied by the
  fixture; the backsword is purchased through the real shop handler. Store calls
  supply a test context directly, so this does not verify NPC proximity or entry.
- A forced reward-insert error rolls back material consumption, weapon changes
  and operation receipts. Retrying succeeds; replaying the same offer cannot
  grant another soul. Restart preserves the exact item UID fingerprint, CPs,
  silver, Fish, bound soul and converted weapon with+12,-7,enchant255 and both
  Super Tortoise Gems. The source Panacea is bound and binds the converted weapon.

Pending before activation: normal-startup wiring and the coherent activation
review. Private NPC/item framing, scroll eligibility and the legacy-consumer
audit are recorded below. Bag capacity,
stack conservation, stale/concurrent offers and loaded catalog mappings are
covered below. The roadmap's full Taoist
milestone stays unchecked and supplier-unproven; staging does not increase the
career count.

## Private NPC and hunted-route integration (2026-09-27)

`TaoistEpicQuest` now validates the terrain and global/persisted actor identity
before placing Sun King199580, Refined Oven199581, Elite Oven199582, Super
Oven199583 and Pure Taoist199584. Existing NPC footprints cannot overlap these
sites. Initialization was exercised twice without creating duplicates. The
loaded Taoist Moon10000 is in1004 at56,33. Return coordinates are chosen from
walkable points near that actual actor rather than a conflicting guide position.

Ordinary opening and reply dispatch now reach the coordinator. Its reply route
uses the existing strict NPC body bounds. Moon options150/151/154 expose the
quest, temple and Abyss flower hunt. The shared flower producer recognizes
eligible Taoists only while this coordinator is enabled; existing Ninja/Monk
requirements and rates remain unchanged. Bruce3604 returns Taoists to Moon.
The four obsolete oven NPCs cannot consume items while the new coordinator is
enabled. Panacea item use then invokes the same atomic conversion store.

`tools/Verify-ArenaProcess.ps1 -TaoistRouteOnly` passed three distinct game
processes with a database restart after the first, using private build SHA256
`D3608F080256015D63919516F2666747CEA303F6F5CE478BC675BB6BD14F0CDD`:

- A non-GM second-reborn Water Taoist buys421005 from shop5, equips it, enters
  the Abyss through Moon and kills six actual loaded source monsters. Controlled
  drop rolls yield three flowers of each color; real pickup processing records
  six receipts and rejects pickup replay. Qualifications and starting silver
  are supplied; CPs remain zero. Combat damage and trial draws are controlled.
- Bruce returns the player to Moon, who admits them to3933 at84,77. Ordinary
  NPC handlers consume the hunted flowers through all three ovens. The resulting
  Panacea survives abrupt process exit with an unconfirmed awakening offer.
- After process/database restart, the lost confirmation alone earns nothing.
  A fresh Sun King offer converts the original weapon and grants one bound P7.
  Repeated confirmation is harmless; the player returns to Moon. A third process
  verifies the exact item UID fingerprint, weapon, soul, silver and zero CPs,
  with four saved trial/awakening operations and no remaining Panacea.

The fixture explicitly calls `Initialize`; normal startup does not. It drives
ordinary server handlers on offline non-GM profiles, not authenticated client
sockets or native rendering. Production activation and genuine6711 acceptance
remain pending until the remaining consumers and validation cases are complete.

## Four-scroll learning and exchange recovery (2026-09-27)

The installed catalog includes a fourth scroll,3006020, for Blessing Touch.
The following mappings agree with the existing Taoist Moon options, spell enum
and loaded catalogs. Spell12400 is named Searing Touch in `magictype.txt` and
Breaking Touch in the existing enum/dialogue; it is distinct from12390.

| Scroll | Spell | Taoist Moon option | Eligible class |
|---|---|---|---|
|3006017 Flame Lotus|12380|161|Fire142..145|
|3006018 Aurora Lotus|12370|163|Water132..135|
|3006019 Breaking Touch|12400|160|Fire142..145|
|3006020 Blessing Touch|12390|162|Water132..135|

Numeric provenance: loaded `itemtype-new.txt` SHA256
`1ED7A23B7A34B5E3706277F97E421E3CDF0B269EEB5BE7367A4EBC0E4F22B3EF`;
`magictype.txt` SHA256
`97ECB413566F5FEA541090D1A1C1498ED2ACF0874EFA6056984583C65C187A71`.
The retained minimum learning level52 comes from the existing instructor.
Learning an owned scroll does not introduce a rebirth requirement; earning Fish
through the quest still requires second rebirth and level70. These are server
integration contracts, not a new claim of original retail eligibility.

`TaoistEpicTraining` intercepts the four old free-learning actions while the
private quest is enabled. It consumes one owned, unlocked, permanent matching
scroll, inserts the skill and a learning receipt in one owner-locked transaction.
Duplicate knowledge consumes nothing. Pure Taoist adds Blessing Touch as option5,
leaving Hossu on option4 and the original three scroll choices unchanged.

`tools/Verify-ArenaProcess.ps1 -TaoistTrainingOnly` passed with build SHA256
`D18CA0A482F354625086C5B39440FB51CA29CA91D7B1EB1AA7DF236E4A75855A`.
Two non-GM profiles (Water135 and Fire145) receive supplied bound Fish stacks;
this suite isolates consumers rather than claiming those Fish were hunted.
Each uses the real Pure Taoist dialogue to purchase all four bound scrolls and
a bound Hossu. Wrong-class learning consumes nothing. Eight concurrent learning
requests produce one skill and one debit; each profile learns its two arts.
An additional exchanged scroll remains intact when the known art is requested.

A forced learning-receipt failure rolls back the scroll debit and skill insert;
reload and retry succeed. A separate game process after PostgreSQL restart
loads the skills through `SkillTable.LoadSpells`, replays learning requests and
verifies exact remaining item fingerprints, binding, zero CPs, six exchange
receipts and two learning receipts per character. The logged PostgresException
in the first phase is the deliberately injected rollback failure. The rule
fixture now passes1085 checks, including the fourth scroll identity.

Normal startup still does not initialize the Taoist quest. Native acceptance,
remaining edge cases, journal integration and the final legacy-consumer audit
remain open; this batch is not production activation.

## Bag capacity, stacks and stale offers (2026-09-27)

The catalog permits100 Fish per stack. The first implementation inserted three
separate Fish rows on a Super Oven failure; it now inserts one stack of three
and reserves one reward slot. A full bag may therefore proceed when its last
Crystal is consumed, but an input stack that would remain in the full bag cannot
proceed. Quantity, binding and receipt writes stay in the same transaction.
The store rejects source stacks above their catalog limit and stacked weapons.

`tools/Verify-ArenaProcess.ps1 -TaoistEdgesOnly` passed against build SHA256
`AFDB5025AE1173545196E60FCA3747EC28A8B29A1684F047810B4290C5047BF9`:

- A40-slot bag with one Crystal exchanges it for one bound three-Fish stack;
  a40-slot bag with two stacked Crystals is rejected without a debit. A101-Fish
  source stack is rejected because the catalog maximum is100.
- Changing the saved material after preparing an offer causes rejection without
  further writes. A failed context check and expiry at the exact two-minute
  boundary preserve the inventory and receipt count. Eight concurrent commits
  of one offer produce exactly one debit and reward.
- Seventeen supplied Crystals are offered through Super Oven's real NPC handler
  with controlled failure draws. The resulting51 bound Fish occupy17 slots;
  Pure Taoist consumes50 and leaves exactly one Fish plus one bound Hossu.
  A separate game process after PostgreSQL restart preserves their exact UID
  fingerprint and receipt count. Replaying confirmation after restart is harmless.
  The Crystals are supplied to isolate these transactions, not claimed as hunted.
- All285 loaded421-family identities are checked.280 map to existing620-family
  targets with the same suffix;421060,421070,421080,421100 and421301 are rejected
  because no matching target exists. This establishes catalog availability,
  complementing the earlier actual converted-weapon persistence test; it does
  not claim native rendering or full equipment acceptance of all280 forms.

No startup activation, deployment, client patch or roadmap career-count increase
is included in this batch.

## Receipt-derived Taoist journal (2026-09-27)

Installed `ini/Questinfo.ini` (SHA256
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`)
identifies mission3418, type6, minimum level70, the supported Taoist classes,
Moon10000 in1004 at56,33, and Divine Panacea as its prize.

`TaoistEpicJournal` derives status from saved operations rather than inventory
possession or the legacy quest blob. The first free or paid trial starts the
journal. A successful final oven producing Panacea finishes it. Separate flags
record personally earned Leaf, Crystal and Panacea; buying or receiving an input
does not invent the earlier successes. Using a traded Crystal may therefore
produce a finished journal with only the Panacea flag. Consuming Panacea later
does not erase completion. This is the documented Revora journal interpretation
of the installed mission/prize, not verified original objective-counter behavior.

Mission3418 joins the existing1134 quest list and1135 detail serialization. The
three historical flags occupy the established counter fields12/16/20; native
client presentation remains pending. The coordinator registers the journal,
successful transactions refresh it, and reconnect reconstructs it from receipts.
Untrusted legacy rows are suppressed in the projected list. Begin requests and
NPC-dialog quit requests refresh authoritative state without authoring or deleting
trial history.

`tools/Verify-ArenaProcess.ps1 -TaoistJournalOnly` passed against build SHA256
`B75D7E1A8707C9AC8C45074E48E27DE57883A672C9FE7F1178ADD9573611EA17`.
An ordinary profile with a supplied Panacea and legacy kill counter99 remains
Available. Forged Begin/Finished and quit requests leave both authoritative
progress and the legacy blob unchanged. A paid failure starts the journal;
paid Leaf, Elite Crystal and Super Panacea successes advance its three flags.
Removing Panacea and restarting both game and PostgreSQL preserves Finished,
the flags, four receipts and the exact946-CP balance. A second profile using a
supplied Crystal proves the final-stage-only completion rule independently.
Qualifications and CPs/materials are supplied; draws are controlled. No native
client session or production activation is claimed by this fixture.

## Remaining consumer checks (2026-09-27)

The source audit found two additional Taoist Moon actions156/157. They used
short-circuit sequential removals of722734/729780/1072040 to issue obsolete
flower identities3003303/711894. The coordinator now intercepts the whole150..157
quest branch while enabled and directs players to the real flower hunt; these
actions no longer consume materials in the private route.

Panacea's family1009 action4 path now reaches `TaoistEpicQuest.TryHandle` before
generic item handling. Owned Panacea requests require32..128 total bytes, an
exact declared-body-plus-eight-byte length, family1009, action4 and the existing
client seal. This reuses the service framing contract; it is not a native packet
capture. The direct item-use path independently rejects a foreign object even
when it copies an owned item's UID. Training also reuses the catalog-bounded
material predicate, so a malformed multi-item stack of a nonstackable scroll is
not accepted.

`tools/Verify-ArenaProcess.ps1 -TaoistInputsOnly` passed against build SHA256
`60E9D3C3D34E5B11D0CA0564933B96EA0F4C901D464CA893FFDFBC2DB1C0AFFE`:

- Both obsolete flower actions preserve their supplied materials. Short, wrong
  declared length, wrong family, bad seal and oversized Panacea requests preserve
  the exact items and produce no operation. A copied foreign item object does
  not convert the weapon. A valid request converts the shop-bought backsword
  once and grants one bound soul; replay does not repeat it.
- Level51, active trade, remote position, mismatched NPC echo, locked/temporary
  scrolls and an oversized scroll stack do not learn Blessing Touch or consume
  the supplied scroll. Restoring the original item metadata permits one valid
  learning operation. A second process after PostgreSQL restart verifies the
  converted weapon, consumed Panacea/scroll, learned skill, exact item fingerprint
  and one receipt per operation.

The first fixture run restored a legacy zero-sized single-item stack as1 and
correctly hit the stale item preimage guard. The fixture now restores its actual
original stack value; no production ownership check was relaxed.

Source references to the quest materials are confined to the new coordinator,
store/rules/journal/training and the identified old Npcs/ItemHandler cases. The
four old free-learning actions are intercepted by the matching scroll trainer
while enabled; existing learned skills and rebirth retention are not revoked.
All activation claims remain private: normal startup still does not initialize
the feature, so the old production behavior is not claimed fixed by this batch.


## Authenticated Water and Fire earning routes (2026-09-27)

- [x] Separate ordinary Water135 and Fire145 characters enter the temple
  through Taoist Moon and use actual authenticated NPC/item packets for paid
  trials, three oven stages, weapon awakening, Fish exchanges and training.
- [x] Each awakens its shop-purchased421005 backsword to620005 with the same
  UID, receives one bound P7 soul, earns90 Fish and exchanges them for a Hossu
  and all four scroll types.
- [x] Water learns12390/12370; Fire learns12400/12380. Opposite-path requests
  preserve their scrolls and teach nothing. Repeated learning/consumed Panacea
  requests do not repeat consumption or rewards.
- [x] PostgreSQL/game restart and fresh authentication preserve the exact item
  UID/type/binding/quantity fingerprint, weapon UID,163 CPs, two learned skills
  and two retained opposite-path scrolls for each character.

`tools/Verify-ArenaProcess.ps1 -TaoistWireOnly` passed all four phases in
`obj/taoist-wire-process.log` on gameplay SHA256
`DD61E4AD95D583179236E66A11FD820D2A0A39275CB175404C3F51D802C407CB`.
The socket probe decodes immediate item/binding, CP and1103 skill updates. Each
character has99 quest-operation receipts totaling837 CPs and two training
receipts, unchanged after reconnect and repeated training requests. The owned
private database is removed after all phases pass.

Controls: supplied second-rebirth/level/class eligibility,1000 CPs and fixture
silver; the base backsword is bought/equipped through production handlers before
socket login. Proximity to the NPCs is supplied. Rolls force one full successful
progression and30 Super-stage failures to cover both Panacea and Fish without
an unbounded random grind. No Leaf, Crystal, Panacea, Fish, Hossu, scroll, soul
or skill is seeded. This is path/transaction coverage, not a natural-odds or
full movement test. The flower-funded first oven and mastery11222 training are
not established by this paid-route socket batch. Native DX9 acceptance and
coordinated live deployment remain pending.


## Earned weapon combat mastery and persistence (2026-09-27)

`Verify-ArenaProcess.ps1 -TaoistStoreOnly` now passes four phases: store,
reload, combat training and training reload, using private game SHA-256
`DD61E4AD95D583179236E66A11FD820D2A0A39275CB175404C3F51D802C407CB`.
The store fixture buys sword421005 through the ordinary shop handler, earns
Panacea through controlled quest-store trials, and awakens that same weapon
into620005. It supplies eligibility, funds, flower inputs and +12/-7/enchant255/
two Super gems; it is not an authenticated or fully self-funded character journey.

The added phase reloads that awakened weapon and invokes production melee
against startup-spawned non-boss monsters. Proximity and the attack cooldown
clock are controlled, and incidental money/CP/equipment drops are disabled.
Damage, monster HP, proficiency XP rates and proficiency levels are not assigned.
The observed run earned family620 level12 with1885 remaining XP after30813
attack attempts and12 kills. This verifies combat progression, not real-time
leveling speed or a native-client training session.

After saving and restarting both game and database, exact level/XP match the
saved snapshot. Achievement11222 has one durable evidence row before and after
two repeated achievement refreshes. The owned disposable database was removed
after all phases passed. Evidence: `obj/taoist-combat-mastery.log` and private
`arena-process-taoist-train.log` / `arena-process-taoist-train-reload.log`.
No production gameplay change, rebuild, live deployment or native acceptance
is claimed by this fixture-only batch.


## Authenticated flower-funded oven and restart (2026-09-28)

`tools/Verify-ArenaProcess.ps1 -TaoistWireOnly -ResetOwnedFixture` passes all four
phases on private game SHA256
`59B88E6D1FCC30877C4856ABC00451786CF62A46D7BF7394B288FF5A2883573D`.
The Water character now hunts the first offering instead of buying it:

- Authenticated Taoist Moon dialogue sends the ordinary Water Taoist to the
  Abyss.214 real melee packet attempts kill six startup monsters (three family
  3609 and three3613); normal death/drop handling produces three flowers of each
  color under deterministic drop rolls.
- Actual socket pickups, each repeated once, immediately update the inventory
  and create exactly six owned pickup receipts. The exit NPC returns the player
  to Taoist Moon, and temple entry uses the socket dialogue.
- The first oven consumes the six hunted flowers for no CPs. Further socket
  offerings produce Panacea, same-UID backsword awakening and one bound P7 soul.
  Thirty subsequent paid trials supply90 Fish for the existing Hossu/four-scroll
  exchanges and class-specific learning checks.
- Water's99 operations cost810 CPs, leaving190; Fire retains its all-paid837-CP
  route, leaving163. Both have exactly two learned class-specific arts, with
  opposite-path scrolls retained.
- PostgreSQL/game restarts and fresh authentication preserve exact item
  fingerprints, binding, weapon UID, currency and skills. Consumed flowers stay
  absent and the six pickup receipts remain unchanged. Repeated Panacea use,
  pickup and training requests do not duplicate rewards or consumption.

The fixture supplies eligibility,1000 CPs, base-weapon purchase funds,30 medicines
and actor proximity/screen registration. Flower/oven rolls are controlled. Attack
cooldowns, combat damage, monster HP and proficiency XP are not overridden. The
observed successful hunt used214 attack commands. An initial run supplied only12
medicines and stopped after three flower pickups when those medicines ran out;
increasing the fixture's medicine budget was the only correction. This is reward
path coverage, not starter-gear balance or natural-odds certification.

The private helpers compile with warnings as errors. No game source change,
production restart, native client acceptance or deployment is included. This
closes flower-funded socket acceptance; the separate documented mastery fixture
remains the combat-mastery evidence, not a full authenticated320-achievement
career. The owned disposable database was removed after all four phases passed.
