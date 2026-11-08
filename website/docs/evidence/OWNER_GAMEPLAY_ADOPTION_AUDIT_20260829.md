# Owner gameplay donor adoption audit — 2026-08-29

This is a read-only evidence audit of the authorized owner source and MySQL dump
against the current Revora implementation and production Host. It does not
authorize imports, deployment, or the next gameplay milestone.

## Audit snapshot

- Repository: private `Sheekovic/Revora`
- Audited branch: `feature/genuine-portal-fix`
- Audited source commit: `f3d59685be05e7a2ff954b448a24219c23c9f618`
- Production Host SHA-256:
  `ab40623d0a069077fdce51473b4f79b441919c4048a8d72008b2ca3fd6299f77`
- Production service at audit: enabled, active/running, zero automatic restarts
- Exact approved client evidence SHA-256:
  `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`

No code, database rows, services, firewall rules, or production configuration
were changed while producing the audit.

## 1. Donor identification

### Retained material

| Material | Exact local path | Owner filename | Bytes | SHA-256 |
|---|---|---|---:|---|
| Owner source archive | `/root/Revora/research/isolated/owner-source-20260829/owner-source.download` | `ReVora.zip` | 10,793,402 | `060df1cf72e5be6701b354c6879a68d06c03e941f54deb5d5dfc91051774e9a2` |
| MySQL archive | `/root/Revora/research/isolated/owner-source-20260829/legacy-database.download` | `revora.zip` | 109,779 | `f5e88d2729306ca6946356a52b3964f9ddb245db9f1e40a0539e0e126ade3d63` |
| SQL archive member | No current extracted path; member `revora.sql` | `revora.sql` | 1,145,278 | `d912367110f5a18836234060fc43425b7d853ee441dbba461a2d99c90ae42f9c` |

The temporary extracted trees are no longer present and their former tmpfs paths
were not recorded, so those paths are `Unknown`. Both original ZIPs remain
available, readable, and pass `unzip -t`. See
[OWNER_SOURCE_INTAKE.md](../../research/gameplay/OWNER_SOURCE_INTAKE.md).

### Source inventory

- Language: C#.
- Framework: .NET Framework 4.5, Visual Studio/MSBuild ToolsVersion 12.0.
- Output: executable server; unsafe compilation enabled.
- Primary architecture: x86, with x64 configurations also present.
- Target: patch 5165 (`WhoIsPacket` version 5165, movement 10005, jump action 137).
- Archive entries: 843; uncompressed bytes: 40,183,341.
- C# files: exactly 672.
- Project files: `ReVora/Revora.csproj` and `ReVora/Granger.csproj`.
- Solution files: `ReVora/Revora.sln` and `ReVora/Granger.sln`.
- Both project files contain 566 compile items and share one project GUID,
  namespace, assembly name, and logical server implementation.
- Bundled compiled artifacts: 12 DLLs, 5 EXEs and 6 PDBs; none was loaded,
  built, copied, committed, or deployed.
- DMap files in the source archive: zero.
- MySQL tables: exactly 49.

All 672 C# files were structurally inventoried. The number proven individually
and semantically reviewed is `Unknown` because there is no per-file review ledger.
There are 106 archive C# files outside each project's 566 compile-item list.
No corrupt or unreadable archive portion was identified, but the large NPC, item,
quest and event areas remain incompletely reviewed.

The intake scan used ClamAV 1.4.3 with 3,628,034 signatures and reported zero
detections for the two archives and 727 extracted scan targets. A clean scan is
not a provenance or license grant.

### Evidence and provenance boundaries

- **Owner source:** exact `ReVora.zip`; primary authorized gameplay-behavior donor.
- **Owner MySQL data:** exact `revora.sql`; curated reference data only.
- **Exact 5165 client:** final packet authority; proprietary evidence, not donor
  source and not distributed through Git.
- **COPS v7:** secondary licensed corroboration, commit
  `84919e25e79eded39314ea50bfd787427eadd59f`, archive SHA-256
  `11b17a893109e7d4e851c5101e6bd0cabd561a01005fd7602054a70d36815052`,
  BSD-style four-clause license.
- **Redux, HeroesCoServer and Impulse:** behavioral references only; no reusable
  license/provenance was established.
- **Revora-owned:** Account/Game servers, AccountId identity, PostgreSQL,
  cryptography, framing, tickets, packet codecs, state machines, rate limits,
  persistence, service lifecycle and reporting.

See [LEGACY_GAMEPLAY_SOURCE_MATRIX.md](../../research/gameplay/LEGACY_GAMEPLAY_SOURCE_MATRIX.md)
and [OWNER_SOURCE_FEATURE_MATRIX.md](../../research/gameplay/OWNER_SOURCE_FEATURE_MATRIX.md).

## 2. Complete adoption matrix

`D:` denotes a path inside the owner source archive. `R:` denotes a current
Revora path.

### Foundation, world and NPC systems

| # | System | Donor evidence | Revora evidence | Status | Genuine result, gap and next action |
|---:|---|---|---|---|---|
| 1 | Account login/authentication | D:`Connection.cs`, `Cryptographer.cs`, `Main/AuthWorker.cs`, `MysqlDB/Domain/DbAccount.cs`; `accounts` 8 rows | R:`src/Revora.Host/Hosting/Genuine5165RuntimeComponent.cs`, `AccountCipher5165`, `PostgreSqlGameDataStore.AuthenticateAsync`; `identity.game_accounts` | `Production-proven` | Genuine 276/1086, credential verification and 1055 work. Donor auth is rejected; retain Revora. |
| 2 | Account-to-character ownership | D:`Game/Character.cs`, `DbCharacter.cs`; `accounts`, `characters` | `AccountId`, `game.characters.account_id` FK and one-character-per-account constraint in V001 | `Production-proven` | Lookup uses authenticated immutable AccountId, never username or character name. |
| 3 | Character creation/selection | D:`PacketHandling/CharacterMaking.cs`, `DbCharacter.cs`; `characters` 5, `charstats` 6 | 1001 parser, `TryCreateCharacterAsync`, V005/V006 | `Production-proven` | SHEEKOVIC was atomically created and selected; historical donor rows were not imported. |
| 4 | World entry/reconnect | D:`Game/Character.cs`, `Game/World.cs`, `WhoIsPacket.cs` | 1006, action 74/10010, 1110 and initialization in `Genuine5165RuntimeComponent` | `Production-proven` | Same-session creation-to-world and normal reconnect are proven. |
| 5 | Walking/running | D:`PacketHandling/WalkRun.cs`, `Game/World.cs` | `GameplayWorld5165.WalkAsync`, 20/10005 codec and optimistic persistence | `Production-proven` | Genuine walk/run, authoritative response, persistence and reconnect passed. |
| 6 | Jumping | D:`PacketHandling/Jump.cs`, `DMap.cs` | `GameplayWorld5165.JumpAsync`, 32/10010 action 137 and action-108 correction | `Production-proven` | Genuine jumping works without weakening origin, distance, collision or rate limits. |
| 7 | Maps/DMap validation | D:`DMap.cs`, `Game/World.cs`; `dynamicmaps` 143; zero DMap files | `MapValidation5165`, `MapCatalog5165`; two private hash-pinned DMaps | `Partially adopted` | Maps 1002/1004 work. Expansion requires reviewed DMaps and map/document semantics. |
| 8 | Portals/map transitions | D:`Dbase/Portal.cs`, `DbPortal.cs`, `Game/Character.cs`; `portals` 222 | `UsePortalAsync`, `TransitionMapAsync`, actions 85/86, 1110; V009/V010 | `Partially adopted` | Five rows imported; IDs 73 and 64 genuinely prove both 1002→1004 and 1004→1002. |
| 9 | Nearby-player synchronization | D:`Game/World.cs`, `Enum/ScreenSize.cs`, movement/jump handlers | Symmetric 18-cell visibility, 10014 spawn and action-135 removal in `GameplayWorld5165` | `Implemented but awaiting genuine owner acceptance` | Deterministic two-client tests exist; no recorded two-genuine-client acceptance. |
| 10 | NPC loading/visibility | D:`Game/NPC.cs`, `DbNpc.cs`; `npcs` 1,373 | `GameplayReferenceCatalog5165`, NPC visibility; V009/V010 | `Partially adopted` | Five placements load; UID 10000 was individually owner-proven. Other 1,368 rows remain untouched. |
| 11 | NPC interaction/dialogs | D:`PacketHandling/NPCDialog.cs`, `NPCBase.cs`, `NPCHandler.cs` | Exact 2030/2031/2032 codecs, fixed dialog, replay state and rate limit | `Production-proven` | UID 10000 open, option, Goodbye, duplicate close, then genuine jump/walk all passed. |
| 12 | NPC scripts/actions | D:`NPCs/` 221 C# files; `custombuttons` 58, `customdialogs` 43 | One Revora-owned bounded fixed dialog; no legacy script runtime | `Reviewed but not adopted` | Dynamic handlers/raw SQL are excluded; review and convert one bounded declarative action at a time. |

### Items and economy

| # | System | Donor evidence | Revora evidence | Status | Genuine result, gap and next action |
|---:|---|---|---|---|---|
| 13 | Item definitions | D:`Game/Item.cs`, `Items/IItem.cs`, `Items/Manager.cs`, `DbItem.cs`, `DbItemInfo.cs`; no base-definition data in dump | No runtime item catalog | `Blocked` | Obtain an authorized exact-5165 item-definition resource before activation. |
| 14 | Inventory | D:`Game/Item.cs`, `PickItemUp.cs`, `DropAnItem.cs`, Character collections | No gameplay inventory aggregate/table/runtime | `Reviewed but not adopted` | Requires item catalog, exact packets, ownership, transactions and replay protection. |
| 15 | Equipment | D:`Structures/Equipment.cs`, `EquipStats.cs`, `Main/ItemPacket/Equip.cs` | 1006 appearance fields only | `Reviewed but not adopted` | Appearance serialization is not equipment adoption; sequence after inventory. |
| 16 | Upgrades/composition/quality/sockets | D:`Compose.cs`, `Enchant.cs`, `SocketGem.cs`, `DBUpgrade.cs`, `MeteorUpgrade.cs`; `itemadd` 5,063 | V009 `item_upgrade_rules`, importer, live rows 0 | `Scaffold only` | Table/parser are not gameplay; item catalog and transactional consumption are missing. |
| 17 | Shops/buy/sell | D:`Main/ItemPacket/Shops.cs`, `DbShopItem.cs`; `customshop` 445 | V009 `shop_entries`, importer, live rows 0 | `Scaffold only` | Must follow item catalog, inventory and currency ledger. |
| 18 | Warehouse | D:`PacketHandling/Warehouse.cs`, `Structures/Banks.cs`, Warehouseman handlers | None | `Reviewed but not adopted` | Requires inventory ownership and exact deposit/withdraw codecs. |
| 19 | Currency/economy | D:Character money/CP fields and shop handlers | Basic 1006 fields only; no transactional wallet | `Scaffold only` | Add checked ledger operations before shops. |

### Monsters, combat, progression and social systems

| # | System | Donor evidence | Revora evidence | Status | Genuine result, gap and next action |
|---:|---|---|---|---|---|
| 20 | Monsters/spawn tables | D:`Game/Mob.cs`, `Bosses/MobBase.cs`, `DbMonstertype.cs`, `DbSpawn.cs` | None | `Blocked` | Referenced monster/spawn tables are absent from the supplied dump. |
| 21 | Monster AI/movement | D:Mob/Boss classes | None | `Reviewed but not adopted` | Sequence after definitions/spawns; rewrite global loops and broad catches. |
| 22 | Combat/damage/death | D:`PacketHandling/Attack.cs`, `AttackMemorise.cs`, `Game/Mob.cs` | None | `Reviewed but not adopted` | Exact packets and authoritative formulas remain engineering work. |
| 23 | PK rules/statuses | D:`ClassPK`, `ElitePK`, `SkillPK`, status fields | None | `Reviewed but not adopted` | Sequence after combat/death; use Revora authorization and audit. |
| 24 | Drops/loot | D:`DropRates.cs`, `DbDropRule.cs`, `DbMineDrop.cs`, `DropAnItem.cs`; `drop_stats` 1 historical row | None | `Blocked` | No reusable drop-rule catalog exists in the dump. |
| 25 | Skills/spells/proficiencies | D:`Features/Skills.cs`, `DbSkill.cs`, `DbMagicType.cs`, `DbProficiency.cs`; skills/proficiencies 0 | None | `Blocked` | Required definitions and exact formulas are absent. |
| 26 | Stats/leveling | D:`DbLevExp.cs`, `Game/Character.cs`; `charstats` 6 historical rows | Level/basic stats only | `Scaffold only` | No XP/progression loop; needs validated tables and combat awards. |
| 27 | Rebirth/progression | D:`DbRebornPath.cs`, rebirth NPCs | None | `Reviewed but not adopted` | Sequence after classes, skills, stats and equipment. |
| 28 | Teams | D:`Features/Teams.cs`, `PacketHandling/Team.cs` | None | `Reviewed but not adopted` | Requires exact state machine, expiry and authorization. |
| 29 | Friends/enemies | D:`PacketHandling/Friends.cs`, `DbAssociate.cs`; `associates` 0 | None | `Reviewed but not adopted` | Use immutable Character UID relationships and privacy controls. |
| 30 | Trading | D:`PacketHandling/Trade.cs` | None | `Reviewed but not adopted` | Requires complete inventory/currency and atomic two-party commit. |
| 31 | Guilds | D:`Features/Guilds.cs`, Guild classes; `guilds` 1, `guildmembers` 1 | None | `Reviewed but not adopted` | Historical rows excluded; rewrite ownership/permissions using Character UID. |
| 32 | Marriage/social | D:`PacketHandling/Marriage.cs`, Marriage NPC | Spouse placeholder in 1006 only | `Reviewed but not adopted` | Placeholder serialization is not a social system. |
| 33 | Quests | D:`NPCs/Quests/`, quest item handlers | Packet 1134 safely classified only | `Not reviewed` | Hundreds of handlers require manual review; no dynamic execution. |
| 34 | Events/tournaments | D:`Events/`, `Features/Events/`; `events` 0 | None | `Not reviewed` | Duplicated frameworks/global state have not received full semantic review. |
| 35 | Teleport/GM commands | D:`Teleport.cs`, `Chat.cs`, `MySql/Commands.cs` | No legacy command port | `Intentionally excluded` | Privilege parsing/raw SQL are unsafe; future commands must use Revora Admin authorization. |
| 36 | Player persistence | D:MySQL/NHibernate repositories | PostgreSQL AccountId/UID, creation, position/map/version | `Partially adopted` | Core character state is proven; inventory/equipment/social/progression are absent. |
| 37 | Importers/migrations | D:49-table dump | V009, V010 and `Revora.OwnerGameplayImport` | `Partially adopted` | Four tables extractable; only portals/NPCs have live rows. |
| 38 | Chat/messaging | D:`PacketHandling/Chat.cs`, chat logs | Initialization/system talk only | `Scaffold only` | No genuine player chat/moderation path. |

### Additional donor systems

| # | System | Donor evidence | Revora evidence | Status | Genuine result, gap and next action |
|---:|---|---|---|---|---|
| 39 | Housing/furniture | D:`Features/HouseTable.cs`, furniture handlers; `furniture` 103 | None | `Reviewed but not adopted` | Sequence after items/inventory/maps; historical furniture excluded. |
| 40 | Mining | D:OldMiner NPCs, `DbMineDrop.cs` | None | `Reviewed but not adopted` | Missing mine/drop data; rewrite reward authority. |
| 41 | Arena/rankings/nobility | D:`Features/Arena.cs`, `DbNobility.cs`; `arena` 3, `tops` 661 | None | `Reviewed but not adopted` | Historical rankings/player rows excluded. |
| 42 | Personal shops | D:`Features/PersonalShops.cs` | None | `Reviewed but not adopted` | Depends on inventory, currency, trade and ownership locking. |
| 43 | Weather | D:`Features/Weather.cs` | None | `Reviewed but not adopted` | Needs exact packet evidence and deterministic state. |
| 44 | Legacy anti-cheat/bot-jail | Donor ban/bot-jail paths; `bannedchars` | Revora rate/protocol controls only | `Intentionally excluded` | Do not import opaque heuristics or legacy privilege paths. |
| 45 | Payments/VIP/voting | Payment handlers and payment/IP/vote tables | Independent Revora public identity system | `Intentionally excluded` | Sensitive historical/payment/network data must never be imported. |
| 46 | Proxy/interserver/firewall runtime | D:`ProxyClient`, `ProxyNetworkEvents`, firewall DLL | Supervised direct Revora Host | `Intentionally excluded` | No dynamic proxy, firewall mutation or bundled assembly. |

## 3. Quantified adoption

The denominator is the 46 systems above.

| Classification | Count |
|---|---:|
| Systems identified | 46 |
| `Production-proven` | 7 |
| `Implemented but awaiting genuine owner acceptance` | 1 |
| `Partially adopted` | 5 |
| `Scaffold only` | 5 |
| `Reviewed but not adopted` | 18 |
| `Not reviewed` | 2 |
| `Intentionally excluded` | 4 |
| `Blocked` | 4 |

“Not started” means `Reviewed but not adopted` plus `Not reviewed`: 20 systems.

### Database transfer

- Tables structurally inventoried/classified: 49/49.
- Tables with reviewed numeric transforms: 4/49.
- Table categories with rows adopted in production: 2/49.
- Total donor INSERT rows: 10,615.
- Sensitive/historical account, player, payment, IP or audit rows imported: zero.

| Category | Donor available | Production | Genuine acceptance |
|---|---:|---:|---:|
| `dynamicmaps` rows | 143 | 0 | 0 |
| DMap files in donor archive | 0 | 2 from separate reviewed client evidence | 2 |
| Portals | 222 | 5 | 2 individual rows |
| NPC placements | 1,373 | 5 | UID 10000 individually proven |
| Dialog buttons | 58 | 0 | 0 |
| Dialog rows | 43 | 0 | 0 |
| Shop entries | 445 | 0 | 0 |
| Item-upgrade rules | 5,063 | 0 | 0 |
| Furniture rows | 103 | 0 | 0 |
| Monster/spawn rows | 0 | 0 | 0 |
| Skill/proficiency rows | 0 | 0 | 0 |

Loaded DMaps:

- `newplain.DMap`: 6,577,748 bytes,
  `68a19eefb77933cc82cec52ca83ebc4fff08c9acf5a8852c8744c594e7ffe121`.
- `forum.DMap`: 60,180 bytes,
  `980ab5448e1ca7b2b41ec3e8f90e6edf755dd32bb5acbab50fc49c336a994b5f`.

V009 and V010 are applied with ledger checksums:

- V009: `8d55d3c147b3e1eba434d1f0f072cac8da338911438e7145ef9a83007b4f8b49`.
- V010: `6ed84679325f8238c9b385ff1e4cd98a0950368b7286a1815605dce8dab7bf3c`.

### Conservative feature coverage

A feature counts as complete only when its whole matrix row is
`Production-proven`. Partial and synthetic-only slices receive zero. These are
breadth percentages, not estimates of remaining effort.

| Domain | Completed/denominator | Coverage |
|---|---:|---:|
| Foundational networking/authentication, systems 1–4 | 4/4 | 100% |
| World/movement, systems 5–10 | 2/6 | 33.3% |
| NPC interaction, systems 11–12 | 1/2 | 50% |
| Items/economy, systems 13–19 | 0/7 | 0% |
| Combat/progression, systems 20–27 | 0/8 | 0% |
| Social, systems 28–32 plus 38 | 0/6 | 0% |
| Quests/events, systems 33–34 | 0/2 | 0% |
| Overall player-facing gameplay, systems 3–34, 36 and 38–43 | 5/39 | 12.8% |

## 4. Blockers and unfinished work

### Hard blockers

| System | Evidence | Missing | Smallest safe resolution |
|---|---|---|---|
| Item definitions | Dump has `itemadd` but no base catalog | Exact 5165 types, stats, slots and durability | Owner provides or authorizes a hash-pinned exact-5165 item resource. |
| Map expansion | 143 map rows, zero donor DMaps; only two live DMaps | Collision/elevation and document mapping | Provide/authorize exact-client DMaps and activate one map at a time. |
| Monsters/spawns | Source has `DbMonstertype`/`DbSpawn`; dump has neither table | Definitions/spawn associations | Provide a reviewed reference export. |
| Drops | Only historical `drop_stats`; no rules | Drop pools/rates | Provide reviewed rules or approve a new minimal catalog. |
| Skills/proficiencies | Tables have zero rows; magic data absent | Definitions/formulas | Provide exact patch-compatible reference data. |
| Third-party fragments | No archive-wide license | File-level reuse rights | Establish provenance/license or independently rewrite. |

### Evidence blockers

- Nearby-player synchronization needs one genuine two-client acceptance.
- Item, inventory, shop and warehouse packet paths need exact-client static proof.
- NPC script outcomes require handler-by-handler review and bounded genuine tests.
- Later combat/social flows require genuine packet classification after their
  prerequisite runtime exists.

### Engineering work, not blockers

PostgreSQL inventory ownership, item instances, wallet ledgers, shops, warehouse,
equipment, teams, friends, trading, guilds, monster actors and declarative NPC
actions are ordinary unfinished engineering work.

### Sequencing decisions

- Shops require item definitions, inventory and currency first.
- Warehouse requires inventory/item-instance identity.
- Equipment/upgrades require inventory and item definitions.
- Monsters require maps and spawn data.
- Combat precedes drops and progression.
- Trade/personal shops require inventory and currency.
- Quests/events require safe NPC actions plus their gameplay dependencies.

## 5. Reconciliation of earlier counts

| Earlier statement | Reconciled result |
|---|---|
| Approximately 672 C# files | Exactly 672 archive C# members; 566 referenced by each project definition. |
| 49 MySQL tables | Correct: 49 `CREATE TABLE` statements. |
| 222 portals | Correct: 222 rows; 5 imported; 2 individually genuine-tested. |
| 1,373 NPCs | Correct: 1,373 rows; 5 imported; UID 10000 individually proven. |
| 445 shop entries | Correct; zero imported. |
| 5,063 item-upgrade rules | Correct; zero imported. |

### Adapted from owner material

- Previous/current-position model and eight direction deltas.
- DMap accessibility/elevation semantics.
- 18-cell symmetric screen membership and spawn/remove lifecycle.
- Portal placement/transition semantics and five portal rows.
- NPC range/visibility semantics and five NPC placement rows.
- Packed-dialog count corroboration.

### Independently written by Revora

- Account/Game networking, crypto and framing.
- AccountId/Character UID identity and PostgreSQL persistence.
- Tickets/handoffs, rate limits and audit reporting.
- Character registration/world entry.
- Exact 10005, 10010, 2030, 2031 and 2032 codecs.
- Concurrent world sessions, transactional map transitions and replay handling.

### Untouched

No donor directory is vendored. The remaining 217 portal rows, 1,368 NPC rows,
all shops/upgrades, player/history/payment rows, scripts, items, inventory,
monsters, combat, progression, social systems, quests/events, bundled binaries,
MySQL/NHibernate and donor network/auth architecture remain untouched.

## 6. Current genuine production milestone

The exact owner client has genuinely proven:

- remote Account 276/1086 and successful 1055;
- Game DH/Blowfish, 1052 and character/world entry;
- walking/running, jumping, position persistence and reconnect;
- portal 73 from map 1002 to 1004;
- portal 64 from map 1004 to 1002;
- movement/jumping after transitions;
- NPC UID 10000 visibility and activation;
- dialog open, X close, option selection and duplicate-Goodbye close;
- jumping and walking after Goodbye without disconnecting.

Production portal events occurred at `19:02:44Z` (to map 1004), `19:03:27Z`
(to map 1002), and again later. Final NPC acceptance at `20:10:38.273Z`
accepted both genuine Goodbye records; a jump followed at `20:10:39.076Z` and
movement followed from `20:10:40.518Z`.

Milestone commits:

| Milestone | Commit |
|---|---|
| Same-session character entry | `3a86affc1968939ee95465a5dc30e5f33de3810c` |
| Exact world-entry action | `a7865ecb52f1d2783ef670112fd921f3b48fda51` |
| Owner-source movement lifecycle | `94367db6ff4f9ca30a826e03af66d3de1d106430` |
| Genuine jump correction | `e5acc5f5fb0e07e1fda35421b4a754f7e8067f0d` |
| Jump evidence | `d41b33ebcd0d1e11d09499de3d7bdb26f825d02d` |
| Portal/map/NPC slice | `31e5baabcb7513fddbebc5f95b07bb6f2978eaa1` |
| Genuine portal correction | `a8c051712670c180bfcff6dc9509c34c711d594f` |
| Genuine duplicate Goodbye | `7a4b70240f4671e83899cb88d463756c12b82b4f` |
| NPC replay/rate hardening | `f3d59685be05e7a2ff954b448a24219c23c9f618` |

The installed production Host is the cumulative `f3d5968` deployment, SHA-256
`ab40623d0a069077fdce51473b4f79b441919c4048a8d72008b2ca3fd6299f77`.

## 7. Recommended continuation

The dependency audit rejects `shops → inventory → warehouse`. A shop cannot
safely transact before authoritative item definitions, inventory ownership and
currency operations exist.

### Slice 1: item catalog and inventory foundation

- Donor: `Game/Item.cs`, `Items/IItem.cs`, `Items/Manager.cs`, `DbItem.cs`,
  `DbItemInfo.cs`, pickup/drop handlers.
- Scope: item definitions, immutable item instances, Character UID ownership,
  slots/stacks, add/remove/move/use, persistence and reconnect.
- Required evidence: exact item-information and inventory mutation packets.
- Required migration: additive definitions and owned item instances with checked
  ownership/slot constraints.
- Exclude reflection-loaded item classes, donor raw SQL, historical player items
  and arbitrary scripts.
- Automated gate: concurrency, replay, capacity, cross-account ownership,
  reconnect and UID invariants.
- Genuine gate: receive one reviewed item and retain the same instance on reconnect.
- Rollback: Host plus additive inventory migration only; preserve characters.

### Slice 2: shops and transactional currency

- Dependency: Slice 1.
- Donor: `Main/ItemPacket/Shops.cs`, `DbShopItem.cs`, `customshop` 445 rows.
- Scope: one reviewed NPC shop/currency with atomic buy/sell and authoritative price.
- Required evidence: exact open/list/buy/sell/error packet layouts.
- Import only a minimal reviewed catalog subset; add wallet ledger/idempotency.
- Reject client price, overflow/negative balance, replay and donor payment/VIP code.
- Genuine gate: open shop, buy, reconnect, sell and verify authoritative balance.
- Rollback: disable shop feature; preserve legitimate completed transactions.

### Slice 3: warehouse

- Dependency: Slice 1 inventory; preferably Slice 2 transaction error handling.
- Donor: `PacketHandling/Warehouse.cs`, `Structures/Banks.cs`, reviewed
  Warehouseman handlers.
- Scope: one NPC, deposit/withdraw, capacity and reconnect persistence.
- Required evidence: exact open/list/deposit/withdraw/error layouts.
- Migration must prevent an item existing in inventory and warehouse at once.
- Automated gate: replay, concurrency, cross-account UID, capacity and rollback.
- Genuine gate: deposit one item, reconnect, withdraw, then walk/jump.
- Rollback: disable warehouse endpoint while preserving item ownership records.

## Executive summary

1. **Transferred:** movement/view behavior, five portals, five NPC placements and
   the Revora-owned genuine Account/Game, character, world, movement, portal and
   bounded NPC-dialog path.
2. **Partially transferred:** two maps, 5/222 portals, 5/1,373 NPCs, core
   player/map persistence, nearby-player synchronization and reference importers.
3. **Not transferred:** items, inventory, equipment, shops, warehouse, economy,
   monsters, combat, drops, skills, progression, social systems, quests/events,
   housing, mining, arena and weather.
4. **Intentionally excluded:** donor authentication/network/MySQL architecture,
   sensitive historical data, payments/IP/voting, unsafe commands, bundled
   binaries, proxy/firewall components and arbitrary scripts/plugins.
5. **Genuinely blocked:** item definitions, additional DMaps, monster/spawn data,
   drop rules, skills/proficiencies and unlicensed third-party fragments.
6. **Can continue without owner input:** exact-client static research, safe schemas,
   inventory/ledger state machines, deterministic tests and declarative NPC APIs.
7. **Owner input needed:** authorize/provide exact 5165 item definitions, later
   DMaps, monster/spawn/drop/skill data, and resolve provenance for any specific
   non-owner-authored fragment proposed for reuse.
