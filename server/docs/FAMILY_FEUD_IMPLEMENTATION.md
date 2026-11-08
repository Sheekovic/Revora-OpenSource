# Family Feud implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Current evidence

- [x] Hash-gated installed missions 1819-1827: equipment quest, level 95-115,
  no profession restriction, strict ordered predecessors and no duplicate keys.
- [x] Seven quest-item identities and two level-97 Super reward candidates.
- [x] Chamber reference checked against both installed indexes and server terrain.
- [x] Transactional class pack opening, persistence and fault/replay checks.
- [x] Ordinary-player persistent quest earning route through NPCs and combat.
- [x] Owned summons, disguise deadline, chamber movement puzzle and token recovery.
- [ ] Native client/Host acceptance, deployment and publication.

Run `python tools/audit_family_feud.py --client ../../6711 --runtime
bin/Debug/database --output docs/research/FAMILY_FEUD_CONTRACT.json` from the
repository. It exports derived metadata only. The reviewed inputs pass; probes
with a missing mission, duplicate field, wrong chamber or wrong token all fail.
The [contract](research/FAMILY_FEUD_CONTRACT.json) records exact input hashes.

The installed chain uses Chang (8796), Felix (4705, Bird Island 686,547),
Guide Zhang (4706, Bird Island 669,780) and Commander Kerry (4707, chamber
1818 at 56,55). Chang must use Revora's existing replacement NPC on integration.
Tokens 710562-710566 are Red, Indigo, Black, Pitch and Scarlet; 710567 is Golden.
The final pack is 728248. Candidate contents are DragonNecklace 120189 for
physical classes and GoldBag 121189 for Taoists, both level 97 and quality 9.
This is catalog evidence, not proof that pack opening is implemented.

The journal describes fifty Bandits L97 before their leader, an Indigo drop
from L98, ten Bandits before a courier, ten Deliverers, and a Scarlet drop from
Senior Bandits. The disguise lasts 90 seconds; the chamber puzzle lasts ten.
Mission 1826's NPC location label says chamber although its coordinates put
Guide Zhang outside on Bird Island. Hunting hints also conflict between text
and coordinates; runtime spawn and terrain checks must determine directions.

## Chamber gap and implementation sequence

Map 1818 is absent from both exact installed GameMap indexes and from
`bin/Debug/database/maps/1818.map`. Map 1819 is registered as Hallowmas;
its adjacent number is not evidence that it is the missing chamber.
Do not teleport a player into 1818 or substitute 1819 without further evidence.
First identify a suitable existing chamber asset and check native/server terrain.
If original identity cannot be established, document a Revora chamber adaptation
with a private, hash-gated client overlay and owned server scene.

The original guide consulted during research is
<https://co.99.com/guide/quests/familyfeud.shtml>. It describes colored puzzle
spots in blue, green, red, yellow, black order but does not establish their
coordinates or random-drop probabilities. These must not be presented as
verified original rules. The Facebook edition has differing travel timing and
rewards; the installed 6711 chain is the local implementation contract.

Next implement pack opening, then persisted stage/material ownership, owned
summons and deadlines, movement validation, ordinary NPC/journal integration,
fault/replay/relogin tests and the composed private client overlay. Final bound
P7 remains permitted for this one-time quest under the owner's reward policy;
it must be frozen and delivered transactionally with the final rewards.

## Reward pack validation

Pack 728248 now selects the verified level-97 Super necklace or Taoist bag
for supported classes. Unknown classes retain the unopened pack. Binding is
inherited from the source; no plus levels, sockets or EXP are bundled into
this equipment-only opener. The quest coordinator must award its EXP Ball and
frozen bound P7 separately in the final transaction.

The isolated x86 build passes. The disposable-database run passes 435 checks
(406 shared prerequisites plus 29 Family Feud pack checks). Coverage includes
ten classes through ordinary item dispatch, exact reward/receipt persistence,
bound and unbound sources, concurrent duplicate opens, full singleton versus
stacked inventory and reward-insert rollback/retry. Logs are ignored local
`obj/family-pack-build.log` and `obj/family-pack-tests.log`.

This proves pack opening, not quest earning or native display. No live schema,
server, client or deployment was changed. The chamber prerequisite remains open.

## Runtime and exterior prerequisites

The read-only loaded-catalog probe and hash-gated terrain audit are recorded in
[the runtime snapshot](research/FAMILY_FEUD_RUNTIME.json) and
[the exterior audit](research/FAMILY_FEUD_TERRAIN.json).

- BanditL97 is family 55; BanditL98 is 79. Both share hunting rectangles
  (362,469)+(192,117) and (335,329)+(96,70). Journal hints lie elsewhere.
  Shared-terrain hunting approaches (362,469) and (339,329) are connected.
- Banditti is 84, level 98. BanditKing 85 is a boss and is not proof of the
  required Senior Bandit. Existing Banditleader 1401 is level 52 on map 1215,
  not the required level-97 quest leader. Courier/Deliverer/Senior variants
  need explicit quest-owned definitions, spawn ownership and recovery.
- Guide 4706 already exists at 636,656, unlike journal 669,780. Kerry 4707
  exists on absent map 1818. Felix 4705 was absent from the NPC query.
  Initialization must reconcile these records without duplicate NPCs.
- Felix and the journal Guide position are connected in static terrain: 354
  four-direction steps between approaches. This does not prove that a player
  can complete the 90-second disguise journey in the native client.

The pack capacity guard is explicitly enabled for 728248 in committed source;
its full-stack safety must not depend on unrelated owner changes that broaden
capacity checks for other packs. Existing owner work remains unstaged.

## Persistent referral and token hand-ins

`FamilyFeudStore` now implements acceptance through stage 7, with one run per
character/rebirth, level 95 minimum and no artificial upper level cap. The
acceptance freezes the final soul identity. Five hand-ins require exact tracked
items owned by this run; owning a matching catalog item alone is insufficient.
Each hand-in consumes its token and records stage/silver in one transaction.
The last replaces Scarlet with bound Golden Token and saves a UTC deadline
90 seconds ahead. Expired attempts can renew only with the tracked Golden Token;
active attempts cannot extend their deadline. NPC Advance cannot bypass stage 7.

Build and 503 checks pass: 406 shared, 29 pack and 68 store checks. Store checks
cover all three rebirths, low-level eligibility, stage/reward-identity reload,
untracked-token denial, repeated hand-ins, Golden Token insert rollback, silver
persistence and exact/expired UTC deadline renewal. Colored token entitlement
is seeded explicitly by the store fixture; this is not encounter earning proof.
Logs: `obj/family-store-build.log` and `obj/family-store-tests.log`.

This store is not initialized by Host yet. Monster earning, missing-token
recovery, disguise visuals, owned chamber admission/maze, final reporting and
rewards, NPC/journal integration and native acceptance remain pending.

## Final reward transactions

The store now implements stage 8 reporting on Bird Island and stage 9 final
claim in Twin City. Reporting commits 6,000 silver and the capped EXP award
with stage 9. Claiming requires three free slots and commits bound pack 728248,
bound EXP Ball 723911 and the previously frozen bound P7 soul with stage 10.
Duplicate calls cannot repeat either reward. Unknown/changed soul catalog
entries fail closed. The total route will include seven earlier 100-silver
payments; the maze coordinator still needs to provide the seventh.

Reward tests explicitly seed stage 8 to isolate persistence from the unfinished
maze. They cover all three rebirths, premature and wrong-map claims, full bag,
failed report rollback (silver/EXP/stage), final P7 insert rollback, exact bound
item UID persistence and duplicate claims after reload. These checks are not
ordinary quest completion or native-client acceptance.

The isolated build passes. The expanded store suite passes 530 checks (406
shared + 29 pack + 68 referral/token/deadline + 27 final reward). Direct EXP
amount is checked below the cap and at the cap. Logs remain private in
`obj/family-rewards-build.log` and `obj/family-rewards-tests.log`.

## Public hunting and tracked-token recovery

The store now validates exact BanditL97 (55), BanditL98 (79) and Banditti (84)
catalog names/levels and rejects guard/reviver/boss variants. Registered public
Bird Island deaths require the actual killer, matching world ownership and
nearby position. A corpse is consumed once, including a losing roll or failed
transaction; its lifecycle may reset only after revival.

Fifty L97 kills persist the leader prerequisite in stage 2. Ten Banditti kills
persist the courier prerequisite in stage 4. Stage 3 L98 kills can grant one
tracked bound Indigo Token. Its 50% chance is a documented Revora rule, not
a recovered original probability. Owned leader/courier spawning and their token
rewards still need integration; reaching the kill threshold alone grants neither.

Recovery restores only a missing tracked token belonging to the current stage
and rebirth, retires the old entitlement, and records the replacement UID in one
transaction. Items existing anywhere in persistent inventory cannot be copied;
consumed tokens cannot be recovered. Golden Token recovery does not renew the
disguise deadline; renewal remains a separate expired-attempt action.

Build and 543 checks pass (previous 530 plus 13 public-hunting/recovery checks).
The hunt fixture drives real combat deaths then calls the store directly; the
production death/revival hooks remain pending. Tests cover fifty corpse credits
with replay attempts, persisted counters, wrong family, losing-roll replay,
earned bound Indigo, bank denial, recovery rollback/reload and actual Indigo
hand-in. Red/maze prerequisites are explicitly seeded in isolated fixtures.
Logs: `obj/family-hunt-build.log` and `obj/family-hunt-tests.log`.

## Owned Bandit encounters

`FamilyFeudEncounters` now creates private-credit monsters on public Bird Island:
leader after 50 L97 kills, courier after ten Banditti, ten Deliverers for Pitch,
and Senior Bandits for Scarlet. Profiles reuse verified Bandit mesh 201 with
explicit Revora combat tuning (20,000/27,610 HP, 600-900 attack, 250 defence).
Senior Scarlet chance is 50%, a Revora rule. No unrelated catalog boss is reused.

Each player owns one five-minute encounter; the registry is capped at 32. Only
the owning character or its current registered companion may fight it. Damage
and death hooks protect credit with owner locks. Each encounter has a unique
transaction receipt, and token/count writes commit atomically with that receipt.
A full bag or failed token write does not consume the saved prerequisite. A
fresh encounter can retry; the same corpse cannot reroll. Ordinary loot is
suppressed for these actors.

Three candidate landings per region are terrain-audited. Spawn rechecks the
server terrain hash, world ownership, occupancy and player proximity. Regions
are (362,469) for the leader, (525,345) for courier/deliverers and (570,416) for
seniors, with alternate landings two/four cells east. Cleanup removes only
registered owned actors on death, timeout, changed stage or replaced session.

Combat hooks are installed, but player-facing Start and recurring Tick calls
await the quest coordinator. The tests invoke those directly. They therefore
prove owned combat/reward behavior, not automatic encounter spawning during a
normal quest. Chamber, journal/NPC integration and native acceptance remain open.

The isolated build and 559 checks pass (previous 543 plus 16 owned-encounter
checks). Combat tests use actual ReceiveAttack/death handling and include foreign
damage denial, prerequisite denial, one-owner capacity, death replay, exact ten
Deliverers, losing/winning Senior rolls, timeout, replacement login, full bags,
failed token insert rollback and successful fresh-encounter retry. Logs:
`obj/family-encounters-build.log`, `obj/family-encounters-tests.log`.

## Owned chamber and ordered puzzle

Revora uses a private dynamic copy of installed map 1801 for the missing
Bandit Chamber. This is an explicit adaptation, not reconstruction of original
map 1818. No client map registration is changed. The
[chamber audit](research/FAMILY_FEUD_CHAMBER.json) verifies exact native/server
terrain with six NPC footprints, entry, Kerry approach and five landing pads.
Named StoneColumn markers identify BlueEye, GreenEye, RedEye, YellowEye and
BlackEye; native appearance is unaccepted.

Admission requires stage 7, tracked Golden Token, proximity to Guide Zhang and
an unexpired saved disguise deadline. Each owned scene has a durable visit,
20-minute lifetime and eight-scene capacity. All four production teleport
overloads reject foreign admission and internal teleport puzzle skips.

Kerry starts the ten-second puzzle. Accepted positions must visit (50,60),
(54,64), (58,64), (60,60), (58,60) in order. Wrong order or expiry resets the
attempt; duplicate observation of the last pad does not advance it. The exact
ten-second boundary is inclusive. Solving persists step 5; returning to Kerry
consumes the token, awards the seventh 100 silver and saves stage 8. Return
travel saves the public location before retiring/releasing the owned scene.
The original colored order is retained; coordinates are Revora placements.

Build and 585 checks pass (previous 559 plus 26 chamber checks), including real
entry/return, production teleport rejection, ordered/reset/expired/duplicate
pads, exact deadline, final-report requirement and finish rollback. Puzzle
fixtures install positions then call the store directly. Accepted walk/jump
hooks, NPC dialogue and native movement acceptance remain pending. Offline and
startup recovery still need Family Feud-specific fault/reconnect tests.
Logs: `obj/family-chamber-build.log`, `obj/family-chamber-tests.log`.

## Ordinary quest coordinator

The source now initializes Family Feud and registers missions 1819-1827. Chang
options 160-162 lead into Felix, Guide Zhang and the owned Kerry/eye NPCs.
Guide Zhang is moved from the exact known legacy position (636,656) to the
journal position (669,780); unexpected model/position changes fail closed.
Quest journal mutations are protected in both request paths. Item use gives
brief directions without consuming tokens. Eligibility is level 95+, any
supported ordinary character, once per rebirth; no owner/GM allowlist.

Public combat hooks count L97/Banditti kills and award Indigo. The coordinator
spawns owned encounters near their verified regions, using a bounded three-second
scan of eligible quest participants. Their existing combat hooks award the other
tokens. NPC hand-ins, recovery, all seven 100-silver payments, 6,000-silver report,
EXP and the bound final reward bundle are now connected end to end.

Bandit disguise uses the existing cosmetic appearance service with mesh 201
and the saved 90-second deadline; it does not change Body, Face, class, health
or transformation state. Login first retires stale appearance ownership before
restoring the remaining deadline. Entering the chamber clears the cosmetic.
Active unrelated transformations are preserved. Native appearance remains pending.

The existing accepted-arrival callback now observes the chamber only after a
walk/jump destination is accepted and installed. It verifies map and coordinates;
teleport/login arrivals cannot advance the puzzle. The ordinary route tests use
the production jump handler for all five eyes, resetting fixture movement
history to avoid wall-clock sleeps. This verifies geometry and callback wiring,
not native timing acceptance or a new packet format.

Family Feud-specific offline/startup failure tests and the composed private
journal/item overlay remain the next work. Native/Host acceptance, deployment,
signing/publication and aggregate achievement 10931 remain unproven.

The final isolated build and ordinary route suite pass 510 checks (406 shared
prerequisites + 104 route checks). All three rebirth states finish via actual
NPC dispatch and combat; tests also cover underlevel/remote denial, forged
journal Begin requests, ordinary token use, expired disguise admission, token
recovery, appearance expiry/relogin, accepted jump progress, 6,700 total silver,
final bound P7, repeated claim denial and persisted completion. The separate
585-check foundation suite retains its own scope; totals must not be added.
Logs: `obj/family-quest-build.log`, `obj/family-quest-tests.log`.

## Recovery verification and live population gap

The expanded foundation suite passes 599 checks (previous 585 plus 14 recovery
checks). It verifies actual admission, timeout, retained Golden Token, renewal,
replacement login, offline return, suppressed database return preserving an
active receipt, idempotent retry, eight-scene capacity and rejected ninth
allocation without leaking maps. Startup interruption is simulated by removing
the in-memory scene while retaining its durable visit; no actual Host restart
or live database mutation was performed. The fixture now explicitly binds its
seeded deadline as DateTimeOffset; production QuestInventory already binds UTC
timestamptz. Log: `obj/family-recovery-tests.log`.

**A public Banditti population repair is required before world acceptance.**
The loaded family-84 rectangles at (114,312)+(95,45) and (293,559)+(56,37)
contain respectively zero and three server-walkable cells. Shared native/server
terrain confirms a connected point at (348,579), but three cells are inadequate
evidence of a reliable ordinary hunting population. The runtime respawner samples
inside the rectangles. The complete route fixture creates its own registered
monsters, so its success does not prove these loaded spawn regions work.

The exterior audit now records these counts. Fix the quest population on audited
terrain before finalizing journal directions; do not conceal the gap by marking
native/world acceptance complete. Client text staging remains pending, and the
known golden-token tooltip still points to the obsolete (641,660) location.

## Population repair and private client text

The loader now corrects only the two exact legacy family-84 rectangles to
(518,339)+(10,10) and (540,338)+(10,10), preserving configured counts and
respawn timing. Catalog identity and the server terrain hash are checked before
correction. Unrelated/custom definitions remain untouched; no database migration
or live spawn mutation was performed. The static native/server terrain audit
verifies all 200 replacement cells are walkable and connected.

The isolated build passed. The real-loader population suite passed 423 checks
(406 shared prerequisites + 17 population checks), including configured counts,
corrected bounds, ten actual combat kills and ordinary respawn credit. The
fixture copies public catalog rows into the guarded disposable schema only.
Logs: `obj/family-population-build.log`, `obj/family-population-tests.log`.
These checks close the source population gap above, not native acceptance.

`tools/ClientPatches/prepare_family_feud_journal.py` composes the nine mission
entries and Golden Token/reward pack tooltips over the Hero's Wings overlay.
Private output: `obj/chi-refill-validation/family-feud-journal/`. Directions now
match the repaired hunt and adapted chamber; unrelated sections are preserved.
SHA-256 outputs:

- Questinfo: `ee7e50b04038c2959f8dc1047fe3f2551c3a6862224ab0dd9cc47d5e6035d1e2`
- itemtype.txt: `b0893b6d5121bb9d7753b73cf165d2de26dc94c5e0025f660d92967b94334c00`
- itemtype.dat: `bc8093973a603800d86b2f3acd6e475afc75adaf7d827b4550e4eb367ede230b`

Nothing installed, signed, published or deployed. Native appearance, travel,
journal and actual Host acceptance remain pending. Next independent root:
Desert Treasure (1828).


### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001, life2, follows the public NPC and
  portal route. Calculated attacks complete fifty BanditL97 kills, BanditL98,
  ten Banditti and the owned Leader, Courier, ten Deliverers and Senior encounters.
- [x] The earned disguise enables chamber entry and clears on admission. Five
  normal jump-handler actions advance the saved puzzle one step each; Kerry
  consumes the token and returns the character at stage8.
- [x] A separate process reloads that completion, reports and receives three
  bound rewards (728248, 723911 and one P7 soul), with exactly 6,700 silver
  across the quest. Another process preserves exact career, material, encounter,
  puzzle and reward state; repeat final claim leaves the snapshot unchanged.

Evidence: `career-retained-family-hunt`, `career-retained-family-claim`, and
`career-retained-family-reload` all passed on private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The fixture controls clock, drop rolls and approach positions; puzzle movement
uses the jump handler and combat uses calculated attacks. Native rendering and
human navigation remain pending. Nothing deployed. Retained aggregate13/19;
achievement10931 remains incomplete with six chains outstanding.
