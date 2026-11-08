# Tempest Wings implementation contract

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

Current private package (2026-09-27): `tempest-wings-composed-v2`, inheriting Fan v3 and Tower v2 Net Peddler at325,371. Earlier versions are superseded; native acceptance remains pending.

Status: ordinary NPC/journal earning, paid travel, owned combat, feather recovery
and final rewards are tested; composed private client patch is staged. Nothing deployed;
achievement10931 follows the tested nineteen-chain Revora rule linked above.

## Evidence and edition selection

The hash-bound installed6711 journal contains mission200, Type4 equipment quest,
level120-200, all classes, TaskmasterChang8796 at1004(51,49), MrWonder10850 at
1011(212,196), RocFeather3006392 and bound EliteTempestWing204008. The item catalog
confirms TempestWing equipment level100 and MeteorScroll720027. Its additional
checked quest3429 is absent from the installed journal, so it cannot serve as a
trusted server completion prerequisite.

The [official PC guide](https://co.99.com/guide/quest/2015/tempest_wings.shtml),
reviewed2026-09-26, describes level100 or rebirth, five Meteor Scrolls to enter
Rainbow Sky, killing Roc for its feather, and exchange through MrWonder. It does
not establish original drop odds, combat statistics, repeat cadence or a native
map identity. Its feather caption mentions ApeCity while the walkthrough and
installed item tooltip agree on Phoenix212,196; use the matching latter route.

Revora implementation will follow the official level100-or-reborn admission,
not the unrelated legacy level130/second-reborn gate; the composed journal must
state that edition explicitly. Once per character, five owned Meteor Scrolls for
the initial permit, no duplicate payment after interrupted entry, bound Elite
TempestWing204008 plus an additional bound P7 frozen on admission follow the
owner's one-time reward policy. The paid permit/reentry and P7 addition are Revora
rules, not claimed original behavior. No daily P7 producer is introduced.

## Current source findings

Npcs.cs, region Tempest Wings Task, uses NPC600065 rather than the journal actors.
Its option1 loops back to itself after a level130/second-reborn check. Option3
independently spends50,000CP and teleports to1100(141,111); that branch does not
repeat the option1 eligibility check. Option2 removes a feather before independent
random inventory writes for204009 or3006594. There is no complete-quest receipt,
atomic exchange or duplicate-safe delivery in that branch. Next(1,13) also makes
its Nr==13 branch unreachable. This is not the promised journal earning route.

Database/MonsterTable.cs, SkyRoc drop branch, creates two generic ground feathers
by name. Such untracked items must not become proof of an owned quest encounter.
Keep historical items distinct from new earned entitlements; do not delete them
or infer completion from inventory possession. Inspect loaded monster identity,
spawn and NPC catalogs before choosing the actual owned actor profile.

## Spatial evidence

The [terrain audit](research/TEMPEST_WINGS_TERRAIN.json) verifies shared public
cells for TaskmasterChang and MrWonder. Both GameMap indexes map legacy1100 to
star01.7z; its entry141,111 is walkable in client and server. However136 blocked
cells differ across the224x224 floor. Therefore this is not accepted owned combat
terrain yet, and the filename/legacy teleport does not prove original Rainbow Sky.
Record a private corrected floor or explicitly documented scene adaptation,
verify paths with reserved NPC/combat footprints and keep native acceptance open.

## Implementation sequence

- [x] Extract exact mission/item identities and record conflicting eligibility,
  currency/reward branches and absent checked quest3429.
- [x] Audit public/candidate scene bindings and every blocked cell; identify136
  discrepancies rather than certifying the existing legacy floor.
- [x] Seven contract tests pass: original evidence; missing/duplicate identity;
  altered reward/objective; missing/renamed items. Reproduce with
  `python tests/test_tempest_wings_contract.py`; log obj/tempest-contract-tests.log.
- [x] Inspect loaded NPC/monster/spawn catalogs and validate a private combat layout.
  [Runtime evidence](research/TEMPEST_WINGS_RUNTIME.json): SkyRoc7696 is level140,
  mesh997,50MHP; spawn455463458 is1100(136,103),12x15,count1. Queried NPC IDs
  8796,10850,600065,4482 have no rows. These are legacy facts, not accepted tuning.
- [x] Stage `obj/chi-refill-validation/tempest-scene` with exact native blocked cells
  in `server/database/maps/tempest-1100.map`; public1100 and both indexes unchanged.
  Entry141,111 reaches Roc136,103 in13 walk steps and exit approach143,111 in2,
  reserving the exit NPC145,111's3x3 footprint. Native portal190,162 is not walk
  connected to admission; owned travel must explicitly handle portal requests
  and provide the reachable exit. Static routes do not prove jump/native behavior.
  Five checks pass (`python tests/test_tempest_scene.py`): reserved layout,
  blocked footprint, isolated fight, malformed floor and unreviewed native input.
- [x] Durable once-per-character acceptance freezes the P7 choice. Five-scroll
  payment spans bound/unbound stacks, atomically records exact debits and the
  permit, and reuses that permit after reconnect without another charge.
  Isolated build and440 checks pass (406 shared prerequisites +34 Tempest):
  level100-or-reborn, late-context and stale-stack rollback, locked/insufficient
  scroll refusal, concurrent reentry, saved reward identity and corrupt-receipt
  refusal. Reproduce with `tests/PotencyHunt/Verify-PotencyHunt.ps1 -TempestPermitOnly`.
  Public initialization/admission is now wired in the following checkpoint.
- [x] Internal paid private visit, owned Roc through actual combat/death hooks,
  tracked kill/feather, timeout/disconnect/replacement-login return and cleanup.
  Fifteen-minute visits, eight concurrent scenes,100KHP/800-1200attack/350defence
  level100 Roc are explicit Revora trial tuning, not original combat statistics.
  Verified mesh997 is reused; ordinary public SkyRoc remains unchanged.
- [x] Earned kill persists with a full bag; receipt-backed feather recovery does
  not mint another while the earlier UID exists anywhere. Atomic exchange saves
  one bound Elite wing204008 and the frozen bound P7, preserves historical
  feathers, and refuses duplicate/completed claims.
- [x] Explicit private compact-floor loader avoids public1100 precedence and
  native-DMap misparsing; size/dimension/cell checks happen before publication.
  Existing CreateDynamic signature is preserved for other quests and fixtures.
  Isolated build and483 checks pass (406 shared prerequisites +77 Tempest): all
  rebirth states, all50,176 terrain cells, malformed floors, real kill/foreign
  damage, interrupted visits, full bags, reconnect, replacement feathers,
  rollback and suppressed payment/material receipt writes. Reproduce with
  `tests/PotencyHunt/Verify-PotencyHunt.ps1 -TempestEncounterOnly`.
  Earlier440 permit checks overlap; totals must not be added. The existing
  standalone dynamic-map allocator also passes all13 regression checks against
  this build (terrain failure, concurrent allocation/disposal, stale ownership,
  counter wrap and exhaustion).
- [x] Expose Chang8796 at1004(51,49), Wonder10850 at1011(212,196) and owned
  SkyKeeper9910500 at145,111. Static terrain checks pass; the actual startup
  footprint conflict found later is corrected below.
  Chang mesh12090 and Wonder/SkyKeeper3580 explicitly reuse Revora appearances.
  Legacy600065/4482 handlers are intercepted, including while activation is off.
  Both journal request handlers protect200/3429; only200 is displayed from saved
  state. No GM/name allowlist. Tick, login, death refresh and portal routes wired.
- [x] Build and470 public-route checks pass (406 shared +64 Tempest): ordinary
  classes15/55/165 across rebirth0/1/2, level100 unreborn and level15 reborn entry,
  real NPC dispatch, paid travel, automatic Roc, real damage/death, earned feather,
  NPC return/recovery/claim, forged journal/remote NPC/distant portal refusal,
  valid nearby inherited portal and saved rewards. The fixture runs normal login
  item-stat calculation after reload; reborn level-up must not start with an
  uninitialized StatHP. Earlier internal totals overlap, not additional coverage.
  Reproduce `tests/PotencyHunt/Verify-PotencyHunt.ps1 -TempestPublicOnly`.
- [x] Compose mission200 and feather tooltip over attack-talisman-composed-v2,
  adding the dedicated corrected floor while preserving prior map/index repairs.
  Six checks pass: malformed/missing/duplicate inputs, unrelated-byte/stat
  preservation, idempotence, encrypted round trip, repeated readback and input
  hashes. Private output `obj/chi-refill-validation/tempest-wings-composed`;
  [numeric manifest](research/TEMPEST_WINGS_PATCH.json). Client level1 visibility
  permits reborn players below100; server enforces level100-or-reborn. Removes
  the unverified3429 client check. Run `python tests/test_tempest_wings_patch.py`.
- [ ] Native6711/Host acceptance, signing, transactional installation and deployment
  remain pending. No live schema, service, inventory or installed client changed.

Audit source: tools/audit_tempest_wings.py; numeric report:
[contract](research/TEMPEST_WINGS_CONTRACT.json). No production schema, service,
client tree, inventory, CP balance or public event was changed in this batch.

## Earned admission and actual process recovery (2026-09-27)

- [x] Fix actual startup: the retired legacy Tempest NPC600065 is loaded from
  `sobnpcs` at1011(211,195), whose surrounding blocked tiles overlap Mr. Wonder
  at212,196. Skip that retired identity on map1011 in both NPC catalog loaders.
  Preserve its database row, all terrain checks and the journal coordinates.
  Earlier component fixtures did not include this legacy actor.
- [x] Start a supplied eligible ordinary character with no CPs or items; earn the
  normal scheduled boss bounty through the combat/death hook. Buy50 Meteors at
  the loaded CP catalog price and pack five scrolls through MillionaireLee.
  Chang acceptance and Wonder admission consume exactly those five scrolls.
- [x] Interrupt the actual game process, restart the database/game and re-enter
  through Wonder using the same saved permit, without a second scroll charge.
  Defeat the owned Roc, receive its tracked feather, return through SkyKeeper
  and claim one bound EliteTempestWing204008 plus one bound P7 soul.
- [x] A third game process reloads the character; repeated claim preserves the
  exact reward UIDs, saved quest fingerprint and remaining CPs. One permit,
  five scrolls spent, one tracked kill and two saved bound rewards remain.
- [ ] Native DX9/authenticated-client UX, natural combat balance, lost final
  COMMIT reply and installation/deployment remain pending.

Run `tools/Verify-ArenaProcess.ps1 -TempestOnly`. Three launches pass in
`obj/tempest-process-run.log`; candidate executable SHA256
`FAC82BD882B89A33FDDE03784B72166A18F6398336684ADC6C5271706549DF74`.
The helper supplies eligibility, proximity and damage, and advances the boss
clock; it does not seed quest materials, progress, kill receipts or rewards.
This proves the server earning path and process persistence, not native combat.
The exact reviewed private terrain is staged temporarily and removed afterward;
configuration is restored, the owned clone is removed and PostgreSQL is stopped.
No live service, database or installed client is changed. Achievement10931 stays
pending its aggregate definition; this does not add an achievement producer.

### Retained career and equipment aggregate (2026-10-02)

- [x] Ordinary retained character4294907001 buys50 Meteors at the configured CP
  price, exchanges them for five scrolls and pays all five for its permit. Spare
  items move through its owned house box. Calculated ranged combat defeats the
  owned Roc and earns the tracked feather; normal exit saves the return.
- [x] A separate process reloads the feather and races four final NPC requests.
  Exactly one bound Elite Wing204008 and one bound P7 soul are granted, with
  no silver change. Another restart preserves exact payment, kill, feather,
  visit, career and reward state; repeat final claim changes nothing.
- [x] Before claim, achievement10931 is absent. Claim emits exactly one receipt
  with source equipment-quests-revora-v1, key nineteen-chains-cross-life and
  rule_version1. Its packet bit survives reload. Independent full predicates
  confirm all19 equipment chains completed on this same UID.

All three phases passed: `career-retained-tempest-hunt`,
`career-retained-tempest-claim`, `career-retained-tempest-reload`. Private binary:
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The runner temporarily stages existing hash-reviewed Tower and Tempest floors
and removes only unchanged files it created. Proximity/public-map placement are
controlled. Native rendering/navigation and deployment remain pending.
The broader retained audit is315 stored/22 absent identities with mixed
provenance;19/19 equipment completion does not prove the full320 career target.
