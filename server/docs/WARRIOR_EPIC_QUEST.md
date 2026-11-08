# Rise of the Warrior: 6711 restoration

## Revora reward override (2026-09-21)

New admissions now award two **EvilSlayer 624439** fists: level 140, +12,
-7 damage reduction, two empty sockets, unbound. This owner-directed reward
supersedes the historical level-40 bound reward design documented below.
Eligibility remains ordinary level-40+ Warriors; receiving a weapon does not
bypass its level-140 equipment requirement. The quest remains once per character.

The loaded server catalog `bin/Debug/database/itemtype-new.txt` identifies
624439 as class 20 and level 140; no higher level exists in its 624 family.
Catalog SHA-256: `1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef`.
Initialization rejects a changed maximum/class profile. This is server catalog
evidence, not a rendered client item inspection or retail reward claim.

Admission freezes `valuable_reward=true`; migration defaults existing contracts
to false, preserving their bound 624078 pair. Both item UIDs, enhancement fields,
reward receipts and final completion commit atomically. Failed enhancement
updates, second-item insertion, receipts or completion writes roll back the pair.
Two free inventory slots are still required; claim replay cannot mint weapons.
NPC admission and final offer describe the new reward.

Build and 458 isolated Warrior checks pass. Coverage includes the ordinary
NPC/item/combat route, full inventory, concurrent claims, old-contract reload,
enhancement-write failure, second-item rollback, and both equipped rewards
surviving reconnect. The equipment fixture explicitly supplies level 140 and
its allocatable stat points after earning the pair; it does not prove leveling
from 70 to 140. Log: `obj/chi-refill-validation/warrior-max-reward.log` (ignored).

The installed client journal still describes the historical level-40 reward.
A matching private journal overlay is now staged (see below). Native reward/gear
display, real Host restart and deployment remain open. No live binary or client file was changed here.


Research baseline `5371a16`, 2026-09-16; storage validation added 2026-09-20.
This establishes the quest contract and terrain prerequisites for M6.55b5d.
The ordinary server earning route is implemented and tested through NPC, item,
movement, combat, equipment and reload paths. **Genuine-client acceptance and
deployment remain pending.** Earlier implementation notes below record the
successive stages of restoration; the current route is summarized at the end.
Warrior instructor learning and owned casts are tracked separately in
[Warrior skill restoration](WARRIOR_SKILL_RESTORATION.md).

## Published route and installed edition

The official [Rise of the Warrior guide](https://co.99.com/guide/quest/rise_of_the_warrior.shtml)
describes a level-40 journey: obtain the ancient book from Warrior God, activate
Memory Jade Slips, witness the sealed fists, investigate the formation, break
the Sealing Stele, open the chest, defeat the Mind of Evil, then speak with
Miracle's Spirit for elite level-40 fists. The
[Spanish quest guide](https://conquista.99.com/guide/quests/tarea_%C3%89pica_de_guerrero.shtml)
also requires a level-40 Warrior. Neither source supplies numerical encounter
balance, a replay policy or reliable 6711 wire behavior.

Installed evidence is more specific than the modern guide's location names:

| Journal order | Mission | Required prior mission | Relevant installed target |
|---:|---:|---:|---|
| 1 | 3678 | None | Warrior God 10001; ancient book introduction |
| 2 | 3679 | 3678 | Memory Jade Slips; memory cave 10009 |
| 3 | 3695 | 3679 | Chest 19253 in memory cave |
| 4 | 3680 | 3695 | Sealing Stele 19256; formation world 10010 |
| 5 | 3681 | 3680 | Chest 19254 in cave 10011 |
| 6 | 3682 | 3681 | Mind of Evil 19257 in cave 10011 |
| 7 | 3683 | 3682 | Miracle's Spirit 19259 in cave 10011 |

All seven entries have minimum level 40 and name a pair of item **624078** as
the full-route reward. Both installed item catalogs identify that item as
**SkyFlame**, Warrior class 20, level 40, elite quality. They also agree on
Memory Jade Slips **3200206** and Warrior God's Letter **3200207**. The letter
is an invitation, not established as the ancient book itself. Do not invent an
ancient-book item ID. The reference's additional item-link arguments are not
proof of a +3 or bound reward; those fields need separate interpretation.

The introductory journal accepts profession 0, while subsequent entries list
20 through 26. Revora's implemented Warrior ranks are 20 through 25. Admission
and reward must use actual supported Warrior ranks, not invent a class-26
promotion because it appears in a client journal.

## Missing world and conflicting navigation

A read-only transaction against the current public catalogs found Warrior God
10001 at **1004 (42,74)**, mesh 50006, talker type 2. It found no map rows for
10009/10010/10011, no actors 19250 through 19260, no NPCs or monster spawns in
those worlds, and no monster template named for Sealing Stele, Mind of Evil or
Miracle. These are observations of this server catalog, not assumptions about
all installations. Existing source has no handlers for the seven missions or
the jade/letter item IDs. The maps and actors must be registered before entry.

The journal disagrees with itself: Warrior God is listed at (33,65); chest
19253 appears at both (42,40) and (57,53); several objective links point to
1002 (132,550) or the Job Center even though their finish records name the
dedicated quest worlds. The current English guide's Spirit coordinate
(110,110) cannot fit the installed 80-by-80 final cave. Do not copy those
coordinates into teleport handlers.

The installed `npc.ini` has presentation profiles 4131 (Chest), 4132
(Guarding Stele), 4133 (Sealing Stele), 4134/4135 (two Mind of Evil variants)
and 4136 (Miracle's Spirit). These identify existing NPC presentation assets;
they do **not** establish attackable monster meshes, health bars or combat
packet behavior. In particular, an NPC mesh and a monster mesh are not
interchangeable. Combat actor representation still needs evidence.

The client map index and existing server grids supply all three world assets:

| Base world | Client archive under `map/map/` | Grid | Proposed landing | Proven walking approaches |
|---:|---|---|---|---|
| 10009 | `sswq-zs03.7z` | 80 x 80 | (50,55) | Both conflicting chest locations |
| 10010 | `sswq-zs01b_new.7z` | 120 x 120 | (35,45) | Stele at (83,86), approached from (81,86) |
| 10011 | `sswq-zs02.7z` | 80 x 80 | (50,55) | Chest (41,39), evil (49,47), Spirit (39,44) |

`python tools/audit_warrior_epic.py` verifies the item catalog round trip and
selected client/server identity fields, all seven prerequisite links, NPC
profile names, map-index bindings, archive availability, exact reviewed server
grid hashes and cardinal walking routes. It reserves each candidate actor's
cell and eight neighbors, matching the stricter ordinary-talker collision
footprint. Reachable cells after reserving those footprints are 908, 3,321 and
1,030 respectively. The stele's own terrain cell is blocked; its approach is
walkable. This proves static server-grid access, not scene playback, combat,
dynamic-world ownership or native rendering.

The verifier prints SHA-256 provenance without copying proprietary content.
Principal catalog hashes:

- Server item catalog: `594ffe5f252c9445a6087004372519a6f150f089c7bff42d4ec30400911a0e88`.
- Client item catalog: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
- Client journal: `f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`.
- Client NPC profiles: `b06db1e2484992a1b58eabd7a3d884da07ac728a4a183d0fd18f2dccd5b20497`.
- Client map index: `86a54928a9682617371542cdb5a72707c799b92a5c820e3581183adaa94f32b0`.

## Implementation contract

1. Persist one character-owned route with ordered stage transitions and issued
   jade identity. Admission uses a living ordinary level-40+ Warrior at the
   actual instructor. Make the book a brief NPC interaction unless an installed
   book item is identified. Preserve skill-learning and promotion selections.
2. Register the three evidenced base worlds; create bounded owner-scoped
   instances for the scene and combat so another character cannot finish a
   trial or steal its reward. Use verified landings, resumable stage entry,
   cleanup and a free exit. Disconnect, death and process restart must not
   duplicate progress or strand the player.
3. Select combat representations from proven 6711 assets and packet paths.
   The stele requires actual accepted destruction and the evil requires an
   actual owned victory. Dialogue, client journal bits and an unowned corpse
   must never substitute for combat proof. Document Revora numerical balance
   explicitly where retail evidence is absent.
4. Commit the final victory, unique completion and **two** 624078 items together
   with capacity and saved-ownership checks. Use a once-per-character, bound
   quest reward as an explicit Revora economy policy; failed writes or a full
   bag retain the earned claim. Preserve existing equipment and avoid a free
   conversion to unrelated maximum-level fists.
5. Project all seven journal entries from saved server evidence, protect them
   from authored progress, and stage corrected navigation separately. Validate
   ordinary earning, equipping both fists, replay, concurrent requests, item
   ownership, rollback and cold reload. Keep genuine-client scene/combat/UI
   acceptance and deployment pending until actually exercised.

An adjacent defect must be reconciled during implementation: legacy
`ItemHandler` case **3006347** consumes that item and grants **624439**, despite
both installed catalogs identifying 3006347 as **Pandora's Call**, an unrelated
invitation. It is not evidence of a Warrior epic earning route. Do not reuse
that item as a quest token, remove existing player equipment, or claim its
current grant represents the published trial. Its appropriate invitation
destination belongs to the separate Pandora route; this research changes no
live item, inventory or handler. The subsequent server implementation now preserves
this invitation and reports that its destination is not open, instead of consuming
it for an unrelated maximum-level fist. Pandora's own earning route remains separate.

## Encounter profile follow-up

The installed `monster.dat` (SHA-256
`99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585`)
also contains SealingStele (TypeID 2769, 30,000 HP, level 72, Boss 0),
TinyMindofEvil (2770, 50,000 HP, level 70, Boss 1), MatureMindofEvil
(2771, 100,000 HP, level 70, Boss 1), and GiantMindofEvil
(2772, 200,000 HP, level 70, Boss 1). The existing codec round-trips the
catalog exactly, and `database/Monsters.txt` independently contains the same
profile identities. The public server monster table has no rows at those IDs.

These existing exact-name profiles can supply native health presentation;
their TypeID fields must not be assumed to equal the spawn packet's body mesh.
The stage-to-variant selection and correct combat model mapping remain to be
established. No new asset alias, numerical parity, native rendering or monster
spawn is claimed from this static inspection.

## Persistent journey implementation (M6.55b5d2 in progress)

### Binary model-catalog evidence, September 20

`python tools/audit_warrior_models.py` now follows the installed binary catalogs,
rather than assuming the older text INIs contain every model. The exact
`armor.dbc` key **916000000** references geometry/texture **9997700**, the same
pair referenced by `3DSimpleObj.dbc` NPC object **770**. That resolves to
`c3/npc/770/1.c3` and `c3/texture/9997700.dds`. Body **916** also has its own
indexed animation resources under `c3/monster/916`, including action 310.
This is strong static evidence for the Sealing Stele combat representation;
it is not proof of native spawn/action rendering.

Adjacent bodies **917, 918 and 919** each have geometry, textures and indexed
actions 100, 110, 120 and 310, and all selected resources exist in this client.
Adjacency alone does not prove their correspondence to the three Mind of Evil
names. Do not equate monster profile TypeIDs 2769–2772 with packet body meshes.
The next runtime implementation can use 916 for the stele; selecting an evil
body still requires either stronger identity evidence or an explicitly documented
Revora presentation choice followed by genuine-client acceptance.

The audit prints SHA-256 provenance for every inspected catalog and selected
resource. It bounds all record/string offsets, rejects conflicting selected
resources and unsafe paths, and never executes, modifies or copies client files.
Duplicate identical catalog entries are accepted; unrelated conflicting entries
are marked ambiguous, not resolved using an invented load-order assumption.

`WarriorEpicStore` implements the seven ordered steps, one issued jade identity,
owned encounter generations, recorded victories and the final two-item claim.
Every mutation requires a canonical eligible ordinary Warrior, the character's
inventory lock, saved profile/location agreement and a caller-owned world
validation callback checked again before commit. Runtime handlers will provide
that callback from registered actors; a client-authored journal bit is never
an input to these transactions.

The jade is consumed only when it matches the admission's issued UID. A lost
jade can be reissued with that same UID only while the initial stage remains
pending and no saved item with that UID exists. An unrelated jade cannot stand
in for it. Reopening combat retires the prior encounter generation, and victory
requires the active encounter to belong to this character and this stage.

The final claim verifies both recorded combat victories, checks two free bag
slots and writes two separate bound level-40 elite fists, two item receipts and
the final stage in one transaction. It preserves a pending claim on capacity
failure. A duplicate request, including after cold reload, cannot repeat the
reward. No CP or gold debit is part of this journey.

Validation: isolated server build and **376 combined checks** using
`tests/PotencyHunt/Verify-PotencyHunt.ps1 -WarriorEpicStoreOnly`, comprising
339 existing Warrior instructor/promotion checks and 37 storage checks.
The latter cover issued-item ownership/recovery, ordered progression, stale
and foreign encounter IDs, late loss of world proof, concurrent admission and
claim replay, two-slot capacity, cold reload and failed admission/reward writes.
A failure specifically on the second fist rolls back the first fist and its
receipt. Invalid class, level, death, trade, fake or replaced sessions cannot
create a route.

These are storage integration checks in the disposable validation schema.
The fixture supplies the world-proof callback directly; it does not certify
combat or the player journey. There is currently no startup initialization,
NPC, item-use or encounter caller for this store. M6.55b5d2 stays open until
those runtime paths and ordinary-player earning are implemented and validated;
native acceptance and deployment also remain pending.

### Journal integration, September 20

`WarriorEpicJournal` projects the seven installed mission IDs from saved route
stages through the existing full-GUI path. It reads the current database stage
on each projection, so a reconnect or lost post-commit send does not leave an
obsolete cached view. Historical completion survives changing class; unadmitted
ineligible players receive no route. Legacy entries are masked without deleting
them. Client Begin and Quit paths cannot author the owned route or erase that
legacy data when the journal is enabled. No objective-counter layout is invented.

The isolated build and combined Warrior suite now pass **395 checks**: the
previous 376 plus 19 journal assertions covering all stages, cold reload,
class changes, legacy conflicts, seven native request pairs and neighboring
quest isolation. The journal has no production initializer yet: it will be
enabled with the complete playable quest, not as a player-facing partial route.

### Owned scene construction, September 20

`WarriorEpicScenes` now creates bounded character/session-owned instances of
10009, 10010 and 10011 according to saved progress. Each allocation validates
the reviewed terrain hash, uses an independent grid, reserves a unique dynamic
map ID and checks its landing. It installs the evidenced memory chest and final
chest/Spirit presentation profiles. Combat positions are deliberately left for
attackable actors; constructing a scene cannot grant progress or rewards.

The allocator requires the canonical living eligible Warrior and inventory lock,
rejects duplicate owner allocations and caps pending scenes at 32. Registration
is distinct from actual presence: an owner still in town has not entered the
scene. Release is idempotent after successful cleanup, refuses occupied maps,
and verifies map identity before removing registrations. It does not yet provide
travel, automatic evacuation/expiry or combat scheduling; those belong to the
remaining quest coordinator.

The `Map` constructor has a new explicit `loadCatalog=false` overload used only
by these instances. Existing constructors retain their catalog-loading behavior.
Private scene creation therefore does not query or inherit public NPC, monster
or portal catalogs. Tests use the actual reviewed grids and exercise all three
worlds, independent owners, duplicate/stale sessions, stage mismatch, occupied
cleanup and reward noninterference. **406 combined Warrior checks pass**,
including 11 new scene assertions; **694 Monk epic regression checks pass**
against the shared map constructor. No production initializer or live deployment
was added.

### Scene travel and recovery, September 20

The scene service now admits only its canonical owner through a scoped server
transfer, calls the actual teleport path and checks the character save result.
All four teleport overloads reject outsiders entering a registered Warrior
scene. Once inside, ordinary local teleports must stay on valid floor within
the existing short movement radius. Entry cannot skip the scene's saved stage.

Free exit returns the player to verified Job Center tile **1004 (44,74)** and
saves that location before releasing the map. The service's recovery sweep
handles death, a 30-minute deadline, departed owners and replaced/disconnected
sessions. It revives a defeated owner before returning them. The existing
`EntityTable.LoadEntity` dynamic-map recovery (IDs >=7008) already returns a
cold-loaded occupant to Twin City; the test exercises that actual loader and
proves the saved quest stage survives, rather than assuming map IDs cannot be
reused after a restart.

The isolated build and **416 combined Warrior checks** pass, including actual
teleport rejection, saved arrival/exit, death, cold reload, stale-session cleanup
and deadline evacuation. The sweep is not scheduled by production startup yet:
NPC/item coordination and combat must be connected before enabling the route.
No game restart or live quest deployment was performed.

### Owned combat integration, September 20

`WarriorEpicCombat` now spawns the formation stele and final-cave evil only for
the current eligible owner at the correct saved stage. The scene must actually
contain that session. Summoning records a new owned encounter generation; repeat
requests retain the existing actor. Actual lethal damage reaches the entity
death hook and commits the matching victory before removing the actor. The final
two-fist claim checks both resulting receipts. No dialogue step can supply them.

The stele uses body **916**, 30,000 HP and level 72. It remains stationary and
cannot attack. Revora selects body **917** for `TinyMindofEvil`, with the installed
50,000-HP/level-70 exact-name boss profile; its association with the retail evil
is **not verified**. This is a Revora presentation choice using existing audited
assets pending native review. Other explicit Revora balance values are defense
80, evil physical attack 300–450, 1.5-second attacks and 800/400-ms movement.
The existing damage calculations still determine final damage to each player.

Attack admission and direct damage reject foreign/replaced sessions, stale
actors, trade and expired scenes. Damage acquires the owner's lock without
blocking on another operation; cleanup and victory use that same ownership.
Registered owned companions are admitted by identity and map membership.
Unrelated PvP/actors in the private scenes are blocked. Scene release removes
pending actors, and late corpse callbacks cannot record another victory.
Legacy monster drops are suppressed for these encounter families.

The build and **428 combined Warrior checks** pass. New integration assertions
include nonlethal damage, a rejected foreign lethal hit, trade suspension,
both actual lethal combat paths, repeat summons/deaths, stage gating, interrupted
cleanup and claiming the pair from real encounter receipts. The fixture still
invokes admission/chest services directly: NPC/item coordination, periodic sweep
scheduling and genuine-client combat/rendering acceptance remain open.
The shared combat paths also pass **613 Exorcism regression checks**.

## Current ordinary-player route

`WarriorEpicQuest` is connected to startup, bounded NPC dialogue, item use and
the scene recovery sweep. Warrior God's existing promotion and skill options
remain available. A level-40+ Warrior chooses **The ancient fists**, reads a brief
book introduction and accepts one bound Memory Jade Slips. Using that owned jade
in the Job Center enters the memory and consumes its exact issued UID. The memory
chest leads to the formation; breaking the registered stele opens the final cave;
the final chest releases the evil; its registered defeat unlocks Miracle's Spirit.
The Spirit grants two separate bound elite level-40 SkyFlame fists atomically.

The memory chest, formation Guarding Stele and final Spirit provide free exits.
The formation exit actor is an explicit Revora convenience using installed NPC
profile 4132 at (35,42); it cannot grant combat progress. Terrain reachability
was re-audited with its footprint. Warrior God resumes saved incomplete stages,
including rebuilding an interrupted combat generation. Lost jade recovery retains
the original issued UID. Completed characters cannot replay the reward.

**453 combined checks pass** with `Verify-PotencyHunt.ps1 -WarriorEpicStoreOnly`
(the retained switch now includes the complete route). The end-to-end fixture uses
an ordinary non-GM Warrior, framed NPC and jade-use handlers, valid walking paths,
actual damage callbacks and the normal equipment handler. Both earned fists equip
with the catalog's agility requirement and persist equipped after cold reload.
The fixture allocates available stat points to meet that requirement; it does not
relax production equipment rules. Full-bag, replay, interrupted memory/formation,
free exits and the ordinary skill menu are also exercised. Tests remain isolated
from live account/character mutations.

Remaining gates: inspect the staged journal navigation and actual NPC/map
appearance and combat/health presentation in genuine 6711, review the chosen evil
model and level-40 fight balance, and perform an authorized backed-up deployment.
No native visual acceptance, exact retail evil-model identity or live availability
is claimed by the server integration tests.

## Private client journal overlay

`python tools/ClientPatches/prepare_warrior_journal.py` stages the seven corrected
Warrior entries at `obj/chi-refill-validation/warrior-journal/staged/ini/Questinfo.ini`.
It composes over the reviewed `quest-client-bundle-v1` journal and applies the
existing Monk mission-3526 navigation correction first. It does not discard the
earlier Summer, Labyrinth or Dis City edits and does not copy their other assets.
All unrelated sections and the existing Warrior reward, prerequisite and level
fields remain byte-for-byte unchanged by the Warrior transformation.

Warrior God links now use 1004 (42,74); the memory chest uses 10009 (42,40);
the stele uses 10010 (83,86); the final chest, evil and Spirit use their reviewed
10011 coordinates. Brief objective/story text matches the implemented journey.
The jade objective describes item use rather than linking to a nonexistent mob.
The evil link uses the exact `TinyMindofEvil` profile name. The malformed original
introduction text is replaced only inside the targeted entry.

Hashes:

- Installed preimage: `f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`.
- Prior bundle journal: `1cf153510ca3767b49345192c57e852e44abd122f27fd36bc9f993c84028f310`.
- Composed private output: `3a8fb7bc198a33291123350bfffab54bcc550cdcfc376db1537c18b3b97b74fe`.

The stage verifies the approved DX9 executable hash, preserves an exact original,
rejects differing existing artifacts, checks transformation idempotence and verifies
that installed files did not change. Eight synthetic transformation tests pass,
including malformed/duplicate fields and identities, field preservation, coordinate
mapping and line endings. The staging command was also rerun successfully against
the actual reviewed files. The output is unsigned, uninstalled and unpublished.

Native review must still verify: each journal link inside a private map; objective
checkboxes/statuses; correct map/NPC appearance; stele and evil movement, death
and HP presentation; and returning/resuming through Warrior God. Combat metadata
IDs 19256/19257 remain catalog references, while server monsters have allocated
entity UIDs. Static coordinate corrections do not prove the native client resolves
those links correctly. Use the staged journal with the matching server build and
retain the prior bundle's other reviewed files; do not install this overlay alone
against the old live server.

### Combined private client bundle (2026-09-20)

`python tools/stage_quest_client_bundle.py --version 2` now composes the reviewed
Monk/Warrior journal with the existing Dis City item definitions and Abyss boss
profiles at `obj/chi-refill-validation/quest-client-bundle-v2`. The command ran
successfully against all three exact-hash source assets and the approved DX9
executable. It rechecked installed preimages and staged output hashes before
publishing the private directory. Version 1 is preserved; `--version 1` remains
available to reconstruct that prerequisite in a fresh workspace.

The v2 journal hash is the composed output recorded above. Item and monster
hashes remain those of v1. Its review manifest records all six input provenance
manifests, including v1 and the Warrior overlay. No active client files, live
server process or production database changed. This is an unsigned review
artifact, not a distributable release. The native review checklist above remains
open; composition does not establish rendering or ordinary-player acceptance.

## Native monster-profile validation (2026-09-20)

`tools/ClientPatches/Verify-BossProfiles.ps1 -Profile warrior` passed eight
checks using the existing disposable x86 GameData harness. The exact-name native
lookup resolves the following unchanged profiles in both the baseline and the
privately staged Abyss catalog:

| Profile | MaxLife | Boss flag |
| --- | ---: | ---: |
| SealingStele | 30,000 | 0 |
| TinyMindofEvil | 50,000 | 1 |
| MatureMindofEvil | 100,000 | 1 |
| GiantMindofEvil | 200,000 | 1 |

The implemented journey currently uses the first two profiles. Resolving the
larger variants does not implement additional encounters or prove retail model
identity. No Warrior monster catalog edit is needed for these names and values.

Provenance: owner-held `6711/Env_DX9` dependencies, copied into the existing
ignored fixture and hash-checked before loading. GameData SHA-256 is
`8f5687dd11a17e7950358c19aefbb197e220f8f1fc24442a826ea0e28a2e409c`;
the complete dependency allowlist remains in `VerifyBossProfiles.cs`.
The baseline catalog is
`99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585`,
and the staged catalog is
`df6431be254159ce83a812532b6f24330df799bfec66e1cfc2ba6436b5d6970a`.
The successful Warrior probe executable hash was
`244a5826b60e00f9f0844c5f2d19844c61e79c1d1c174d1281f16ef089715d6e`.
This older compiler produces different binary hashes on subsequent compilations;
each run prints its actual hash before execution.

The wrapper now runs hidden with a 20-second process timeout and captures stdout
and stderr. Retaining the process handle before waiting fixes Windows PowerShell
returning an unavailable exit code after an otherwise successful probe. Both
variants must finish with exit code zero. Regression runs passed all 60 Abyss
checks and 22 earlier boss-profile checks. These are native data-loader results,
not rendered HP bars, model animations, journal navigation or a player journey.
The genuine-client acceptance and deployment gates remain open.

## Maximum-reward journal overlay (2026-09-21)

`python tools/ClientPatches/prepare_warrior_journal.py` now stages the reward-aware
composition in `obj/chi-refill-validation/warrior-max-reward-journal`. The prior
`warrior-journal` artifact remains preserved. All seven Warrior `Prize` fields
state two unbound level-140 EvilSlayer fists, +12, -7 and two empty sockets, with
an explicit note that previously accepted trials retain their promised reward.
The text uses no inferred item-link parameter layout. The original navigation
fixes and prior quest/Monk composition remain; unrelated bytes and admission
level/prerequisites are preserved.

Private output SHA-256:
`160a4d649f5120216cba33f813c5612f16c291715a296bb200f4795b4e2387de`.
Ten synthetic transformation checks pass, including both newline formats,
missing/duplicate reward fields, unchanged eligibility and idempotence. Staging
against the hash-checked installed files succeeded and verified they remained
unchanged. The artifact is unsigned, uninstalled and unpublished.

A read-only decode/round-trip check also confirms the installed client item row
624439 is EvilSlayer, class 20, proficiency 12, level 140, matching the server.
Client `ini/itemtype.dat` SHA-256:
`d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
This establishes catalog identity; rendered gear statistics, journal text fit,
private-map links and native equipment use still need genuine-client acceptance.

## NPC opening repair and actual process recovery (2026-09-27)

The central `Npcs.GetDialog` dispatcher omitted `WarriorEpicQuest.HandleNpc`.
Although 2032 replies reached the coordinator in `PacketHandler`, initial private
NPC openings went through `GetDialog`, which had neither the coordinator hook
nor legacy cases for the four private actors. It now routes Warrior Epic before
the existing Trojan coordinator. Ordinary Warrior trainer menus retain their
separate handler; this hook does not bypass eligibility, proximity or ownership.

`tools/Verify-ArenaProcess.ps1 -WarriorOnly` now validates the route in three
actual game processes using an ordinary account in the marked disposable clone:

1. With a supplied level-100 Warrior profile and no items/CPs, Warrior God gives
   the introduction, issues one bound jade and refuses duplicate issuance.
   The normal item-use packet consumes that jade and enters the memory. Opening
   and selecting the memory NPC leads to the formation; the owned Sealing Stele
   victory opens the final cave. Opening its chest releases Tiny Mind of Evil.
   The probe saves the character and exits immediately during that active fight.
2. After database/game restart, login recovery returns the character to Twin
   City at stage 5, with no jade or rewards. Warrior God's resume option restores
   the final cave and its full-health owned enemy. The exact interrupted
   encounter is retired. Victory plus the Spirit's claim grants exactly two
   unbound level-140 EvilSlayer fists, each +12, -7 and two empty sockets.
   Repeating the claim does not grant a second pair.
3. A fresh game process verifies the same two item UIDs and attributes, seven
   saved stage receipts, two victory receipts and two reward rows. CPs remain
   zero and silver remains at the initial 100,000.

All three phases pass. The original test attempt exposed the missing direct
NPC dispatch hook before reaching jade issuance; the corrected build passes
opening and selection through the central NPC dispatcher throughout the route.
The test controls proximity and lethal combat inputs and supplies qualification;
it does not insert completed quest/reward rows or prove leveling, combat balance,
authenticated native play, journal rendering or level-140 equipment use.

Build completed with existing legacy warnings. Candidate executable SHA-256:
`440D5CFD930640C83A457AE69D51BE17E2C2C15DB537A95EFCA080485C89F65E`.
Logs: ignored `obj/warrior-process-build.log`, `obj/warrior-process.log`, and the
candidate's redacted `arena-process-warrior-{capture,finish,verify}.log` files.
The failed clone was inspected and reset through the existing ownership guard;
the successful clone was removed and private PostgreSQL stopped. Unrelated owner
edits in `Npcs.cs` were preserved and excluded from the staged change/build.
No live deployment or client patch installation occurred.

### Cross-dispatch regression guard

`python -m unittest discover -s tests -p test_npc_dispatch_coverage.py -v`
passes four focused tests. All 22 feature coordinators directly registered by
name in the 2032 reply dispatcher (including the current Elite PK instance) have
an opening registration inside `Npcs.GetDialog`. The committed source was checked
separately from the owner's worktree edits. No additional missing hook was found.

The regression reproduces the original Warrior omission by removing that hook and
requiring it to be reported missing. A commented-out hook, a string containing it,
or a matching call in an unrelated method cannot satisfy the check. This is a
bounded source-wiring guard, not a C# semantic analyzer: it does not establish
runtime eligibility, NPC placement, offers, client rendering or complete earning
for the other coordinators. Dynamic dispatch using other method names and future
syntax changes require review. Achievement/wardrobe completion counts are unchanged.

## Fresh production-created Warrior with calculated combat (2026-10-03)

The earned level122 Warrior now completes the ordinary jade/memory/chest route
with its prior equipment. One calculated hit defeats Sealing Stele and two
defeat Tiny Mind of Evil. The unclaimed stage6 and two victory receipts survive
a process restart before ordinary resume/claim. Exactly two unbound624439 fists
with +12/-7/two empty sockets, seven stages and two reward rows survive another
cold process; duplicate final/trainer requests preserve prior assets. No supplied
qualification, health, damage or completed stages are used. The level140 equip
requirement and native/balance/live limits remain open.
[Full evidence and current checkpoint](CAREER_STARTING_BOUNDARY.md#fresh-rise-of-the-warrior-and-max-weapon-recovery-2026-10-03).
