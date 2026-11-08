# Owner monster and Stamina migration (5165)

Status: complete compatible implementation, validated for controlled production rollout. The implementation was built from checkpoint `302708c10574bdb814b6a7ef2579e1fe75896285`; production evidence is appended only after the release gate. This document distinguishes active normal-world behavior from deliberately gated event dependencies.

## Proven source inventory

The authorized textual Owner Source is retained at `research/isolated/owner-source-20260829/extracted/ReVora`; the authorized resource package is at `research/isolated/owner-complete-20260829/extracted`; normalized hash-reviewed records are at `research/isolated/economy-campaign.mgmJ0g/essentials`.

The static initialization chain is `Program.cs` -> `Database.LoadMobs` in `Database.cs` -> `MobInfos.txt` and `MobSpawns.txt` -> `Game/World.cs` map collections -> `Game/Mob.cs` runtime `Step`, attack, death, drop and respawn behavior. Packet ingress is in `PacketHandler.cs` and `PacketHandling/Attack.cs`; map access is in `Game/DMap.cs`; screen packets are in `Main/Packets.cs`; specific drops are parsed by `DropRates.cs`. Stamina is held and synchronized by `Game/Character.cs`: status ID 8, cap 100, standing regeneration 3/second, sitting regeneration 15/second, no regeneration while dead/flying/transformed/attacking or within 500 ms of activity, 30 Stamina removed when hit while sitting, full Stamina on revive, and generic skill `StaminaCost` consumption. The donor uses separate monster-step and monster-attack loops; that scheduling mechanism is not copied.

Owner data contains 189 monster templates, 1,004 spawn generators requesting 49,857 entities, 181 explicit drop rules and 1,841 level-indexed equipment-drop candidates. It references 53 spawn maps. The reviewed package contains 222 map records and 192 DMaps. Three spawn map identifiers (8004, 8005, 8006), representing 15 generators and 495 requested entities, lack releasable map closure. Twenty-one imported generators (28 entities) are event-gated. The compatible normal-world set is therefore 968 generators, 49,334 entities, 143 used monster types and 45 maps. All 189 templates remain imported; no template is silently discarded.

## Revora adaptation

- `StaminaResource` is a deterministic monotonic-clock resource shared by all professions. It persists through `game.character_attributes`, synchronizes with status 8, and has bounded centralized regeneration.
- `LiveCombatEntityRegistry` maintains concurrency-safe UID and per-map indexes.
- `LiveCombatPipeline` supports player-to-monster, player-to-player and monster-to-player mutations with authenticated ownership, map/instance/range/life validation and exactly-once receipts.
- `LiveCombatRuntime5165` loads the complete enabled generator catalog, uses durable server-owned UIDs in the exact-client monster interval `400001–599999`, emits 10014 spawns, manages viewport entry/leave, and uses one bounded Host-lifetime scheduler. Only maps with active combat sessions are ticked. There is no thread per monster.
- AI covers donor normal-world passive/patrol, player hunt, chase, DMap-valid step, bounded leash region, physical/ranged attacks, death/corpse removal and respawn. Magic monsters use the donor's `SkillUse` layout (`1105`) and reviewed magic ID/level. `HuntBlueNames`/guard behavior targets only authoritative `flashing` PK state; guards are not player-attackable and never produce ordinary rewards.
- `MapCatalog5165.LoadReviewedOwnerCatalog` verifies manifest paths, lengths and SHA-256 before parsing. All 50 imported spawn-relevant DMaps load. All 49,334 active positions resolve to accessible cells. Donor generator 142 legitimately requests four entities from one cell on map 1016; Revora prefers distinct cells, then permits DMap-valid co-location while retaining unique entity UIDs.
- V037 extends the normalized catalog and adds monster attack/reward and Stamina receipt structures. It disables bespoke UID 400000035 so Twin City Pheasants come only from the standard generator.

## Current Pheasant root cause

The prior UID `400000035` was a handcrafted row in `gameplay_reference.live_monster_spawn_points` from V035. It used a one-row presentation path and was not backed by the Owner Source generator, AI population maintenance, drops or Stamina runtime. It was registered as a monster entity—not an NPC—but was an incomplete parallel spawn path. V037 disables it and the runtime no longer reads that table. Standard type-1 Pheasants now originate from reviewed generator rows. V040 allocates every reviewed generator member a durable UID beginning at `400001`, within the exact 5165 client's strict monster interval.

## Completed reward and ground-item boundary

`LiveCombatPipeline` binds the final hit to one deterministic operation, while `game.monster_death_resolutions` adds the immutable `(monster_legacy_uid, spawn_generation)` death identity. The HP mutation, death receipt, character EXP/level/stat mutation, item-instance allocation, and all ground-drop rows commit in the same serializable PostgreSQL transaction. A retry loads the prior resolution; it cannot reroll rewards. Ground item UIDs come from the non-overlapping `300000000–399999999` sequence, and active monster UIDs remain in the exact-client interval `400001–599999`.

## Genuine targetability and moved-player persistence correction

The first genuine post-deployment canary attempt rendered Pheasants but produced
no normal melee frame. Repeated skill frames decoded skill `6001` with target
UID zero and changing bytes `26..27`; the Host categorized them as
`target-missing`. The monster scheduler continued ticking map 1002 but recorded
zero attacks. Exact-client disassembly proves monster selection is restricted
to `400000 < UID < 600000`; Revora's former `400000000+` allocation therefore
made a visible entity non-targetable. V040 stores one immutable assignment per
source/generator/ordinal and the Host refuses any monster spawn outside the
client interval.

A separate monster-attack defect was found in the durable version boundary.
Player movement increments the live entity version but does not mutate
`game.character_vitals.row_version`. The monster pipeline formerly submitted
the moved live version as the PostgreSQL expected version, so the first hit
after movement conflicted. Live snapshots now track a distinct persistence
version; movement changes only the live version, while HP/death/revive commits
advance both under the serialized entity gate. Real PostgreSQL regression
coverage moves a loaded player before a monster hit and proves the HP mutation,
receipt, and durable version commit exactly once.

`PostgreSqlGroundDropRuntime` implements authoritative protected pickup, three-cell distance, map/instance checks, inventory capacity, wallet cap, ownership transfer, currency ledger entry, command receipt, expiry and restart recovery. Serializable retry handles deadlock/serialization races. The database prevents an item from occupying ground plus inventory/equipment/warehouse at once. Expired item rows remain immutable audit tombstones and are excluded from inventory presentation. Packet `20/1101` is used for appearance, removal and pickup; item pickup is followed by authoritative `1008`, and silver pickup by `10017`.

Owner drop behavior is adapted deterministically: all 181 explicit rules and 1,841 level-indexed equipment candidates are loaded; DragonBall/Meteor/common equipment and silver rolls use a death-ID-derived PRF rather than a shared unsafe RNG. Placement searches a bounded, DMap-valid 3x3 area and never overwrites an active ground cell. If no cell is available, the remaining candidate is omitted atomically rather than creating an unreachable item. Team sharing is not activated: V023 contains only persistence scaffolding and Revora has no reviewed live party membership/range runtime yet.

Character skill experience is applied by the existing transactional Ninja skill path on every accepted skill use. V039 closes the generic combat-progression boundary: character EXP is awarded for every accepted damaging monster hit, the donor's separate `MaxHP / 10` kill benefit is committed with death rewards, and normal physical/ranged attacks derive weapon proficiency only from authoritative equipped right/left-hand rows. Same-family dual weapons receive the donor's 150% award; mixed weapons receive right-hand 100% and left-hand 50%. Violet gems (`51/52/53`) apply the donor multipliers, eligibility is level-gated, family `105` remains explicitly excluded, and the donor's first-use behavior creates a level-zero proficiency before later gains.

Proficiency is durable in `game.character_proficiencies`; each operation's result is retained in `game.combat_proficiency_results` under the same combat receipt. HP, hit EXP, proficiency, kill benefit and ground rewards therefore share one serializable commit and replay identity. The approved client SHA `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f` dispatches packet `0x401` at disassembly address `0x590a89`; its handler reads proficiency DWORD `+4`, level WORD `+8`, and experience DWORD `+12`. Revora emits the reconciled `16/1025` layout on change and reconnect. Live party sharing remains gated because V023 has no active party membership/range runtime.

## Sitting, magic and special classifications

The donor's sitting rule is `Action == 250` plus non-magic damage: subtract 30 Stamina, floor at zero, and stand. Monster physical/ranged attacks and ordinary player physical attacks commit HP, Stamina and standing transition together. Replayed attacks reload the committed Stamina result. Monster magic does not deduct sitting Stamina, matching the donor. Regeneration and attacks share the same per-character semaphore and monotonic clock.

All 33 active magic-capable templates resolve a reviewed nonzero magic ID/level and emit exact `1105` presentation before authoritative HP/death updates. A magic attack is canceled if its target dies, disconnects, leaves the map/instance or becomes out of range.

Classification reconciliation is server-owned: `normal`, `guard`, `boss`, `event-special`, and `passive`. The imported catalog currently classifies 167 normal, 1 guard, 7 boss and 14 event-special templates. Event-special generators remain disabled unless their controlling event exists. The guard is bound to `HuntBlueNames` and authoritative PK state; absent that state it patrols but does not attack ordinary players.

## Exact catalog reconciliation

| Category | Generators | Requested entities | Release state |
|---|---:|---:|---|
| Compatible normal world, 45 maps | 968 | 49,334 | Active |
| Event/quest controlled | 21 | 28 | Retained, explicit `event-gated`, disabled |
| Missing approved map/DMap (`8004–8006`) | 15 | 495 | Retained in `monster_spawn_dependency_gates`, incompatible until data exists |
| Total Owner Source | 1,004 | 49,857 | Balanced exactly |

The activated population by map is: `1000:1667`, `1001:1531`, `1002:3130`, `1005:1`,
`1011:2215`, `1013:120`, `1014:79`, `1015:3613`, `1016:60`, `1020:2701`, `1025:5`,
`1026:9`, `1027:8`, `1028:6`, `1040:25`, `1043–1049:110 each`, `1051:98`, `1075:950`,
`1076:1605`, `1077:1623`, `1082:10`, `1204:1454`, `1207:2462`, `1208:2216`,
`1210:3089`, `1211:1905`, `1212:2066`, `1214:1450`, `1215:2525`, `1216:2313`,
`1300:1978`, `1351:839`, `1352:976`, `1353:1509`, `1354:2279`, `2021:925`,
`2022:650`, `2023:82`, and `3030:390`. This is 45 maps and 49,334 entities exactly.

The 21 gated rows are controlled by donor special types/maps (Dis City, Guild/CounterClock/Treasure/event bosses and maps `2020`, `8000–8003`). Their controlling systems are not complete Revora runtimes, so enabling them would create orphan behavior. Maps `8004–8006` do not exist in the approved reviewed DMap closure and are not fabricated. V038 retains all 15 rows with source hashes and activation conditions.

## Validation evidence

- Release solution build: 0 warnings, 0 errors.
- `Revora.GameplayTests`: 54 passed, including durable replay of monster- and player-origin sitting hits and donor dual-weapon proficiency calculations.
- `Revora.HostTests`: 21 passed with approved maps, the complete spawn catalog, disposable PostgreSQL load gate and published-executable SIGTERM gate enabled.
- `Revora.ProtocolTests`: 5165 character/movement/portal/NPC/Ninja/dialog/correction/spawn fixtures passed.
- `Revora.IntegrationTests`: encrypted-loopback fragmentation/coalescing passed.
- `Revora.FeatureGateTests`: 5 passed; `Revora.UnitTests`: 10 passed.
- V037 through V039 applied transactionally to disposable restores. The PostgreSQL suite proves death/reward/progression atomicity, exact replay, first-use and subsequent proficiency progression, rollback, protected pickup, concurrent pickup, expiry isolation and restart persistence.
- Full runtime startup loaded exactly 49,334 monsters in 1,193 ms and retained 28,263,232 bytes in the measured isolated run, below the enforced 30-second/512-MiB ceiling. With one authenticated active-region player, the three-second window produced 12,134 bounded AI ticks, 3,453 ms aggregate process CPU, 19 PostgreSQL transactions, five outbound packets and 981 ms maximum scheduler delay. Central schedulers disposed cleanly.
- The complete Playable suite passed 17/17 using dynamic ports and the disposable PostgreSQL database. Separate encrypted campaigns passed for economy, all seven Ninja skills, normal/magic monster combat, 1101 drop/pickup, fragmentation/coalescing, replay and post-combat movement.

## Production deployment evidence

- Git implementation commit: `45bc3200ea9cd4c007e246c40864e8870b7f8d14` on
  `feature/owner-monsters-stamina-5165`; the private remote matched before deployment.
- V037 checksum: `7646e4b16e0dd1a4167d370c0a7fe58328ef7191507610ff7951610e7adac3c8`;
  V038 checksum: `d7388f31fce9a917c3bb16f3440dec07e4121293224fb763c55d0c6e90f1c0e1`.
- Protected backup: `/root/revora-game-backups/20260830T223327Z-owner-monsters-stamina`.
  Its custom-format archive passed `pg_restore -l`, restored with original ownership, accepted both
  migrations through the restricted identity, and produced the same 9-account/3-character/21-item
  counts before catalog validation. The catalog was loaded twice on that restore without drift.
- Two Release single-file publishes were byte-identical. The installed and loaded Host SHA-256 is
  `7cd7e2d81dc423323252a28e485975e255eb4efdbd21a87822a8287dd6623d06`; installed and
  `/proc/<pid>/exe` inode were both `2389563`. ClamAV and secret/private-key/development-path scans
  reported no findings.
- The first systemd start attempt did not reach the executable because the staged application
  directory retained mode `0700`. The error was the exact `status=200/CHDIR` boundary, not a Host
  runtime failure. Directory ownership/mode was corrected to `root:revora-game 0750`, systemd's
  failed state was reset, and the clean deployment invocation reached exactly one READY with
  `NRestarts=0`. This packaging correction changed no application bytes.
- Production loaded 189 templates, 968 generators and 49,334 entities. Final idle sampling was
  approximately 470 MB cgroup memory and 0.6% of one CPU, with no unhandled exception. Web and
  registration returned HTTP 200; Admin, Game, PostgreSQL, nginx and AMP remained active; `nginx -t`
  passed. Pre/post account, character, item, location, warehouse, wallet, skill and non-Stamina
  attribute digests matched exactly.

The completion audit added transactional per-hit EXP/proficiency in commit
`1b27495e163df4c3c78af96d523dffb4af1eeb3a`. V039 checksum is
`20607476730524eb17b4ea5408f301d438ac09eb6f713b3aecde9668238b4e9f`.
The protected pre-V039 backup is
`/root/revora-game-backups/20260830T235900Z-owner-monster-progression`; its archive SHA-256 is
`a4dcb42da64769a6095956917b7e498acb695113ec7b4e55df14abdaab3ee881` and a real isolated restore
preserved 9 accounts, 3 characters, 21 items, 189 templates and the 968/49,334 active population
before accepting V039. The final deterministic Host SHA-256 is
`bec660c63ce46963b88aeb8a3a2e481f6bfc8d64454577c09af38402df85f874`; installed and loaded inode
are both `2390378`. The controlled restart reached one READY with `NRestarts=0` and loaded exactly
189 templates, 968 generators and 49,334 entities.

One genuine client was moving the canary character during the deployment window. The graceful
restart drained that connection and saved its authoritative movement from map 1002 `(438,404)` to
`(438,394)`. Field-level pre/post comparison proves all immutable character fields stayed identical;
accounts, items, locations, warehouse, wallets, skills and non-Stamina attributes also matched.
This was legitimate live movement, not a V039 mutation, and it was preserved rather than rolled back.

## Remaining explicit dependencies

- Maps `8004–8006`: blocked by absent approved map/DMap data.
- Twenty-one event generators: gated until each controlling event/quest runtime is implemented and reviewed.
- Live party reward sharing: gated on a genuine party membership/range runtime; V023 tables alone are not treated as transferred behavior.
- Genuine client presentation of post-hit character EXP and `16/1025` proficiency remains an owner-acceptance boundary; exact-client static evidence and encrypted Host fixtures pass.
- Event-specific boss scripts, global announcements and quest counters remain with their event gates. Compatible normal bosses use common AI, damage, death, reward and respawn behavior.

No unsupported map, event or dynamic donor script is executed.

## Genuine monster-targeting correction deployment

The correction is committed on `feature/genuine-monster-targeting-fix` at
`8408d9592b479dc56a4c64685898cca8453b0d11`. V040 checksum is
`cc688d562e917c5d0f0d98bfa3de696ad4521e571b6f03b91d7fecb81c1fbdeb`.
It assigned 49,362 reviewed generator members unique UIDs `400001–449362`;
49,334 belong to the active normal population and zero active assignments fall
outside the exact-client interval. The isolated restore preserved 9 accounts,
3 characters, 21 items, 189 templates, 968 active generators and the complete
49,334 active population before accepting V040.

The protected rollback is
`/root/revora-game-backups/20260830T234639Z-genuine-monster-targeting`.
Its database archive SHA-256 is
`e2bfef606fbffc93ce227b7d3c373e7464078108bb4c34997c77bcb12b9dacb9`;
the prior Host SHA-256 is
`bec660c63ce46963b88aeb8a3a2e481f6bfc8d64454577c09af38402df85f874`.
The deterministic corrected Host SHA-256 is
`0140e0ec1a0d42939069ef188c42c22547a0721cdfa47dc8e2ca8b1c17f78e9d`.
Installed and loaded inodes both resolved to `2390382`; the controlled restart
reached one READY with `NRestarts=0` and loaded 189 templates, 968 generators
and 49,334 entities. Pre/post digests for accounts, characters, items,
locations, warehouse rows, wallets, skills and non-Stamina attributes matched.

Automated gates after the correction include: Release build with zero warnings
and errors; Gameplay 55/55; Native Login 59/59; Feature Gates 5/5; Unit 10/10;
Web 53/53; Admin 6/6; Host 19/19 with complete population load; Playable 17/17;
real PostgreSQL ownership/replay/rollback/restart coverage; encrypted normal
monster attack/death/drop/pickup/replay; and all seven encrypted Ninja skills.
Genuine Windows validation of client selection, melee, skill targeting and the
first monster attack remains the only acceptance boundary for this correction.

## Genuine combat and Stamina follow-up

The first genuine post-V040 campaign found four independent integration
defects: moving-target coordinates were compared for exact equality, Twofold
used a fixed one-damage base, transient movement/attack activity permanently
blocked Stamina regeneration, and the previously unreachable PostgreSQL
regeneration update had an ambiguous `row_version` reference. The correction
keeps all combat decisions server-authoritative: packet coordinates are only a
bounded viewport claim, current entity state and range decide the attack, and
Twofold uses the Owner Source physical-damage pipeline before its skill
multiplier. `character_attributes.row_version` is now explicitly qualified.

Regression coverage includes stale displayed coordinates through the encrypted
Host, authoritative response coordinates, a non-one-damage Twofold result,
100→70→73 encrypted Stamina consumption/regeneration, real PostgreSQL compare-
and-swap persistence, replay, movement after combat, and all seven Ninja skills.
Genuine Windows presentation remains the final acceptance boundary.

## Operational rollback

The Host rollback is an exact timestamped copy of the prior `/srv/revora-game/app`; map rollback restores the prior two-file map directory. Database rollback is the protected pre-migration custom-format archive plus roles archive. Because V037–V039 are additive, the fast operational rollback first restores the Host/maps and disables `combat.runtime`; database restore is reserved for a reviewed full rollback decision.
