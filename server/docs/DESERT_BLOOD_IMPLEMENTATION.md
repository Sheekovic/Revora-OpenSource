# Desert Blood implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Confirmed contract

- [x] Installed 6711 missions 1800?1807: minimum level 85, client maximum 105,
  unrestricted class, ordered eight-stage chain. The first two sections have
  exactly two identical ActivityType=0 keys; later sections have one.
- [x] Harvey 4621 at 1000,495,579; Stone Column 4622 at 1000,386,103;
  Obscure Warrior 4623 at 1801,48,51; Aunt Peach 4633 at 1015,765,602;
  Chang is adapted from 8796 to Revora 900071727 at 1002,473,300.
- [x] StoneSpell 721843, 200 Blade Ghost kills plus BluntSword 721845,
  five BladeGhostFang 721846, Harvey'sLetter 721841 and PeachBag 721842.
  Note 721844 exists but its assignment to Aunt Peach's letter remains inferred.
- [x] Journal payments: six 100-silver stages, then 6,000 silver and one hour
  of experience; final pack 728242 (catalog name AmbitionPack).
- [x] Client and runtime map indices agree on Crypt 1801, mission.7z, value 128.
  Server terrain exists and is hashed. NPC candidates are Harvey 380,
  StoneColumn 381/383, ObscureWarrior 382 and AuntPeach 384. Client npc.ini
  contains duplicate 381 sections; candidates are retained without claiming
  route/model runtime acceptance.

[Original guide](https://co.99.com/guide/quests/desertblood.shtml), consulted
2026-09-26, confirms the Crypt errands, level-83 Blade Ghost materials, the
200-kill task, delivery and readable letter, and per-rebirth eligibility.
It specifies Elite level-90 Ring/level-85 Bracelet plus EXP Ball(E). It mentions
one-twelfth EXP ball from the Crypt column but does not establish a repeat limit
or publish material-drop odds. Those details need explicit Revora rules.

Evidence: `tools/audit_desert_blood.py`,
`docs/research/DESERT_BLOOD_CONTRACT.json` and
`docs/research/DESERT_BLOOD_ACTOR_CANDIDATES.json`. Hash-gated contract audit
passed against installed journal/items. Guide hunt coordinates differ from the
6711 journal; use the journal's Blade Ghost hint at 1000,165,276 when supported
by spawn/terrain evidence. No original proprietary quest script was recovered.

## Implemented reward pack

- [x] Ordinary transactional opening of 728242: known physical classes receive
  CrystalRing 150178 (level 90); Taoists receive JadeBracelet 152168 (level 85).
  Both are Elite, permanent +0 and preserve source binding. Unknown classes
  keep the pack. Consumption, reward and receipt persist atomically.
- [x] Build and **435 focused checks pass** (406 shared + 29 pack checks):
  rollback/retry, duplicate use, binding, ten class selections, reload and
  unsupported-class retention. Logs: `obj/desert-pack-build.log` and
  `obj/desert-pack-tests.log`.
- [ ] Earned referrals, Stone Spell, saved hunting/materials and recovery.
- [ ] Owned Crypt entry, warrior/column interactions, return and bounded rewards.
- [ ] Harvey/Peach delivery, readable Note and valuable final bound rewards.
- [ ] Ordinary NPC/item/death/journal integration and full-route checks.
- [ ] Native/Host acceptance, tooltip correction, deployment and publication.

The final quest will grant its bound EXP Ball(E) separately with the pack and
owner-approved one-time bound P7. The current pack tooltip includes EXP Ball(E),
so its private correction must accompany the eventual earning route. Nothing
is deployed; pack opening alone is not a completed quest. Achievement 10931 is
unproven. Continue with the earned hunt and safe Crypt lifecycle.

## Persistent store validation (2026-09-26)

- [x] Saved, ordered per-rebirth progression and frozen bound P7 selection.
- [x] Earned Stone Spell, 200 Blade Ghost kills, guaranteed Blunt Sword and
  five fangs; tracked bound material ownership, storage checks and lost-item recovery.
- [x] Transactional letter/Peach Bag/Note delivery, six 100-silver payments,
  6,000 silver plus one EXP ball of direct experience, and final bound pack,
  EXP Ball(E) and frozen P7. These are store operations, not yet public NPC services.
- [x] Build and 511 focused checks pass: 406 shared, 29 pack, 76 store checks.
  Coverage includes three rebirth states, actual monster deaths, corpse replay,
  stored/lost materials, full-bag deferred sword recovery, write rollback,
  level-cap experience, concurrent final claims and reload persistence.
  Logs: `obj/desert-store-build.log`, `obj/desert-store-tests.log`.

Explicit Revora rules: spell and fang drops use 50%; both BladeGhost families
count toward 200 kills, with a guaranteed sword at completion. Full inventory
retains the earned sword entitlement. Lost fangs require replacement kills;
other earned quest documents and the sword can be restored only when their
tracked item no longer exists. Stored items cannot be duplicated. The read
Stone Spell remains the return permit until Peach Bag delivery. Note 721844
is assigned to Aunt Peach's letter as a documented Revora interpretation.
The Crypt column's additional experience remains unimplemented until its
bounded lifecycle is wired; these tests do not establish private scene access.

Read-only runtime catalog evidence: family 17 BladeGhost (level 82), family 76
BladeGhostL83 (level 83), both ordinary monsters. Desert map 1000 spawn rows
21 and 70 use origin 122,269, extent 108,150, caps 75 and 39 respectively.
Column NPC 4622 exists at 386,103 with lookface 3810. Other required NPCs still
need ordinary route integration. No production schema or deployment changed.

## Crypt placement evidence

- [x] Hash-bound client/server map 1801 audit: matching 126x126 terrain.
- [x] Entry 50,55 reaches the Warrior approach 46,51 in eight four-direction
  steps and the return column approach 42,50 in thirteen steps after reserving
  both 3x3 NPC footprints. Warrior center 48,51 is the journal position;
  return column center 40,50 is an explicit Revora placement.
- [ ] Owned scene lifecycle, durable visits, authorized travel and bounded
  column experience. Static terrain evidence does not satisfy these gates.

Reproduce with `python tools/audit_desert_blood_terrain.py --client ../../6711
--output docs/research/DESERT_BLOOD_TERRAIN.json` from the server repository
(join the command onto one line). The audit rejects changed bindings, hashes,
dimensions, blocked cells and disconnected approaches. It reads the client
archive without executing client code or writing extracted proprietary files.

## Crypt lifecycle implementation

- [x] Bounded eight-scene registry on audited map 1801; exact owner/session,
  terrain, actor and metadata ownership; no inherited public actors or portals.
- [x] Earned, read Stone Spell admission for stages 2/3/4/6, with Harvey's
  Letter additionally required at stage 6. Visits are durable with one active
  visit per character, a 20-minute deadline and saved arrival/return locations.
- [x] All four Entity teleport overloads reject foreign Crypt entry.
- [x] Normal/expired return and reconnect recovery retire the visit and release
  evacuated terrain. Return 1000,384,103 is clear on both server/native terrain.
- [x] Revora column rule: one-twelfth EXP ball once per rebirth, with a durable
  receipt, level-cap handling, ownership/proximity and active-visit checks.
- [x] Build and 563 focused checks pass (406 shared + 29 pack + 76 store +
  52 Crypt checks). Actual entry/return, intruder teleport rejection, expiration,
  repeated entry, concurrent column claims, failed receipt rollback and
  reconnect recovery are tested in all three rebirth states.
  Logs: `obj/desert-visits-build.log`, `obj/desert-visits-tests.log`.
- [ ] Public NPC coordinator, Warrior progression, startup/tick/login wiring,
  item/death/journal integration and full ordinary route remain unfinished.
- [ ] Offline process-restart and scene-capacity fault coverage remains pending.

Tests explicitly initialize and drive these components. They do not establish
that ordinary players can currently start this quest through the public NPCs.
No live database initialization, deployment or client acceptance was performed.

## Ordinary server route

- [x] Chang/Harvey/Stone Column/Warrior/Aunt Peach progression is connected to
  ordinary NPC requests. Public NPC coordinates/models are checked at startup.
- [x] Earned monster deaths, spell/note item use, revival replay reset, startup
  interrupted-visit recovery, login recovery and visit ticking are wired.
- [x] All eight journal missions reflect persisted stages, with both quest-start
  packet families protected from client-forged progress.
- [x] Build and 494 ordinary-route checks pass (406 shared + 88 route checks).
  All three rebirth states run from acceptance through actual deaths, Crypt
  entry/hand-ins/return, delivery, reading, reward and pack opening after reload.
  Underlevel/remote acceptance and forged journal starts are rejected.
  Logs: `obj/desert-quest-build.log`, `obj/desert-quest-tests.log`.
- [ ] Private journal/tooltip overlay, exterior native terrain routes, native
  rendering/interaction and actual Host acceptance remain pending.
- [ ] Extended offline restart/capacity fault coverage remains pending.

These tests use ordinary non-GM characters and production NPC/item/death paths
in the disposable database. The server source route is implemented, but it has
not been deployed or accepted in the native client. The test found and fixed a
missing MsgTaskStatus protection in addition to the NpcRequest protection.

## Private client overlay and exterior terrain

- [x] `tools/ClientPatches/prepare_desert_blood_journal.py` composes all eight
  journal entries on the Tomboy bundle without changing unrelated sections.
  It corrects Chang's identity, directions, eligibility ceiling and reward text.
- [x] Pack 728242 tooltip states the actual level-90 Ring/level-85 Bracelet;
  the separately awarded EXP Ball is no longer advertised as pack contents.
  TXT and encrypted DAT are independently hash-checked and preserve the earlier
  Tomboy tooltip correction. Transform round trip and rewrite idempotence pass.
- [x] Private files staged under
  `obj/chi-refill-validation/desert-blood-journal/staged/ini/`; unchanged live
  client and source files are rechecked. Journal SHA256:
  `669faa70c47812a82884df64ff22f2aa166f1e2b1fd6aa0bdf680a0238419503`.
- [x] `tools/audit_desert_blood_exterior.py` verifies shared native/server walking
  with quest NPC footprints: Harvey approach to column approach 881 steps,
  Harvey to Blade Ghost hint 927, sampled Bird Island city point to Aunt Peach
  approach 75. Evidence: `docs/research/DESERT_BLOOD_EXTERIOR.json`.

The sampled Bird Island start is not a proven inter-city landing. Other live
objects and jump routes are outside this audit. The private overlay is unsigned,
uninstalled and unpublished. Native rendering, interaction, navigation and
Host acceptance remain pending. Extended offline/capacity fault checks remain
an independent server-side task before moving to the next quest root.

## Offline and capacity fault validation

- [x] Disconnect after actual saved admission: offline tick persists the return,
  retires the visit and releases its terrain.
- [x] Startup recovery simulation starts from actual admission, removes the
  session and in-memory scene, and preserves its durable active visit. A
  suppressed entity-location write aborts recovery without retiring the receipt.
  Retrying succeeds atomically; repeating recovery changes nothing.
- [x] Eight different ordinary owners can reserve scenes; a ninth is rejected
  without allocating a map. Releasing all reservations restores the map count.
- [x] 572 focused checks pass (406 shared + 29 pack + 76 store + 61 Crypt),
  including nine new offline/capacity checks. Log: `obj/desert-recovery-tests.log`.

This is a deterministic simulation of lost process state, not a real Host kill
or restart. Native/Host acceptance, inter-city arrival and publication remain
pending. The next independent quest root is Hero's Wings, missions 1808-1818.


### Retained career verification (2026-10-02)

- [x] Ordinary retained character 4294907001, life2, follows Chang and the
  Desert City NPCs, earns and reads the spell through calculated BladeGhostL83
  combat, enters the crypt and returns through its column. Two hundred further
  calculated BladeGhost/BladeGhostL83 kills save the full hunt and its material.
- [x] A separate process reloads the hunt/material, completes the remaining
  crypt visits and five further material kills, then travels through public
  portals to the final report. Chang awards three bound items (728242, 723911
  and one P7 soul), with exactly 6,600 silver across the quest.
- [x] A third process preserves exact career, materials, crypt visits and final
  reward UIDs. Repeating Chang's claim leaves the snapshot unchanged.

Evidence: `career-retained-blood-hunt`, `career-retained-blood-claim`, and
`career-retained-blood-reload` all passed on private game SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The fixture controls drop rolls and positioning; it does not inject kill damage
or quest receipts. Native navigation/rendering and deployment remain pending.
Retained aggregate11/19, with eight chains remaining; achievement10931 incomplete.
