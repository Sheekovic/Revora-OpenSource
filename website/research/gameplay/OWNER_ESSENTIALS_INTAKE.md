# Owner essentials gameplay-resource intake

Reviewed 2026-08-29. This document records a static, data-only intake of the
owner-authorized supporting package. It does not grant rights to bundled
third-party material and does not approve any resource for production merely
because it is present or malware-scan clean.

The original and extracted files remain outside Git under the ignored research
reference `research/isolated/owner-complete-20260829`. No included executable,
DLL, installer, script, server binary, decoder, or database tool was run.

## Package identity and archive safety

| Field | Value |
|---|---|
| Owner filename | `ReVora-Essentials.zip` |
| Ignored local reference | `research/isolated/owner-complete-20260829/owner-complete.download` |
| Bytes | 28,932,493 |
| SHA-256 | `dbd508b66abf5966e2b326f4a7cc45e20a5350c8f18a9845579f7022b6a7a457` |
| ZIP entries | 845: 835 files and 10 directories |
| Uncompressed bytes | 486,721,093 |
| Encrypted entries | 0 |
| Unsafe paths | 0 |
| Duplicate names | 0 |
| Case-collision groups | 0 |
| Symlinks or special files | 0 |
| CRC result | All entries passed |

The archive has five same-content groups covering ten members. None duplicates
an item, monster, spawn, skill, drop, or DMap catalog. The two `GameMap.dat`
members are identical. Cross-archive comparison found no content duplicate with
the retained owner source or MySQL ZIP except an empty placeholder. The
reference rows in `granger.sql` are nevertheless byte-identical by table to the
corresponding rows in the retained `revora.sql`; those rows must be deduplicated
by their source/table digests rather than imported twice.

## Malware and executable-material finding

ClamAV 1.4.3 with 3,628,034 signatures reported zero detections across the 834
non-empty outer extracted targets. Direct inspection of the solid nested RAR was
required because scanning the outer RAR alone did not expose its nested result.

`Navicat.rar` has SHA-256
`3c401920f43f5ab7a92f47c47ed32939c622e10aa55d3c9033640f3770a132dc` and
contains a Navicat installer, cracks/key generators, and another RAR. Direct
mode-0600 extraction, without execution, found two distinct `keygen.exe` files
detected as `Win.Trojan.Banload-10873`:

- `0478e48f124255417fea0cde1d1d72906b695a9b8c1f5e09c32f711155b43831`
- `2275c117692e545885e6aab8c52faff9f3672d8d52147cd56d9807f221bb76cc`

The complete Navicat subtree is rejected. It must never enter Git, a build,
deployment, package, test fixture, or development PATH. `MySql.Data.dll`
(`0f0b047ae12781611645a75078797b0146f96c28756f21d5c63ec23d2021b869`)
is also rejected: Revora uses Npgsql and does not adopt bundled legacy binaries.
A clean result for another member is neither a safety guarantee nor a license.

## Format and category inventory

Category-manifest hashes below are SHA-256 over sorted records of
`relative-name NUL byte-length NUL content-sha256 LF`. They identify the
reviewed collection without checking proprietary payloads into Git.

| Category | Files | Bytes | Category-manifest SHA-256 |
|---|---:|---:|---|
| DMaps | 192 | 458,232,872 | `1791135fdaecaaa6e506a195bd039e5185fb2887963bf594636ef2d66e036ec2` |
| Puzzle files | 204 | 1,531,676 | `0d32ff793854b47bf1b66a0764d78238f72801adfb635ec5a52916447cc0aa11` |
| Scene files | 275 | 202,116 | `22575947d9825a3bbd4ebbc2a4fe894b9083b931a7b45f15a2aa09cf4a288bcb` |
| Scene-part files | 126 | 72,448 | `d81db3e1ecb9a8c123c01e310dbe33be8a1bc100d5183d9cc6c2b2e0acc64139` |
| Text catalogs | 16 | 1,422,657 | `e58e0fd5863892be5027a0f475f06b37ef3691162d2fd0f05b30d938eb604be6` |
| DAT catalogs/state | 12 | 125,805 | `80fe73ab72ca7ff7df8f6926011251c03a6c488a50d0d871c4a0c71b0e99d45a` |
| INI catalogs/config | 4 | 146,352 | `2f38affdb67899ce14d54ad91942f76b4454f0ee125c810b46c627432a50559e` |

There is additionally one SQL export, one DLL, one RAR, one character-state
file, and two guild-war state files. Rendering-oriented puzzle/scene/part files
are inventoried but are not currently needed by the authoritative server and
must not be committed as proprietary client assets.

## Resource resolution and conflicts

| Resource | Verified material | Resolution | Required handling |
|---|---|---|---|
| Map index | `GameMap.dat`: 222 map IDs, 179 unique document paths, SHA-256 `6cdcddc3e62b088738f0202276ebd4791864d6703d0e035ada75ab9fc877d9a6` | Former map-index blocker resolved with exceptions | 213 mapping records resolve; Kunlun 1-9 are missing. Dynamic/event aliases need explicit semantics. |
| DMaps | 192 unique files; 121 numeric-1004, 20 `DMAP101`, 50 numeric-1003 and one `DMAP100` | Structurally resolved | All 192 pass the bounded dimension and row-checksum parser for their reviewed header variant. This proves parser/catalog compatibility, not production activation. |
| Proven active maps | Supplied `forum.DMap` SHA-256 `980ab5448e1ca7b2b41ec3e8f90e6edf755dd32bb5acbab50fc49c336a994b5f` | Exact match to production | May remain hash-pinned. |
| Conflicting active map | Supplied `newplain.DMap`: 6,140,828 bytes, SHA-256 `cf76b99e7786b4f580f949b2f4d60e9eb2501e994a777df50d52e543b0b7999c` | Conflict | It differs from the genuine-proven production map. Never replace the approved map automatically. |
| Items | `Items.txt`: SHA-256 `40cc0138d48ed76247ca212e6ddf9add839b58e40865a262134c5956cf3ea609` | Former absence blocker resolved, integrity review required | Header says 8,405. The bounded field parser sees 8,409 rows and 8,408 unique IDs; ID 188905 is duplicated with the same imported fields. Importer records the discrepancy and deduplicates only the identical definition. |
| Item additions | `ItemAdd.ini`: 5,063 records, 422 base IDs, SHA-256 `61f95b4825ed2b847e0693859d5d99736c975c19e5f98d55a0344577f9834546` | Resolved | Base-family semantics mean rows cannot be validated by naive direct item-ID equality. |
| Monster types | `MobInfos.txt`: 189 unique types, SHA-256 `4f0106834578fab90410a0aefdfd99e298233ee637bf6f2fd8de33fda572bbd8` | Former blocker resolved | Bound every field and normalize 21/23-field variants. |
| Monster spawns | `MobSpawns.txt`: 1,004 regions, 162 referenced monster types, 53 maps, SHA-256 `555055a610dbb98a624c85014eca5d3df9bb43d88bb10c2590cf776920a8c97e` | Former blocker resolved with map-alias gap | No missing monster reference. Maps 8004-8006 have 15 regions/495 declared monsters but no direct `GameMap.dat` entry; establish dynamic-map ownership first. |
| Specific drops | `DropRates.txt`: 44 monster groups and 181 rules, SHA-256 `051037bd29d23f5ccebd48c22fdfebf9391eaf140e63951914d6a893d11c2adb` | Former blocker resolved | All item references resolve; 34 rates use locale decimal commas and need culture-independent parsing. |
| Equipment drops | `EquipDrops.txt`: 1,841 unique rules, SHA-256 `c9682e376ed4f12c50406d6de68274a8ab5faa3e1e2b7b4faac22a31b1e17f32` | Resolved | All item references resolve. |
| Magic/skills | `MagicType.txt`: 764 source records, 431 unique skill IDs and 759 unique ID/level records, SHA-256 `4a2042a76438e1f003ace13732327ddb76cc39b40defa7e9bf719fe38b7ce4b0` | Former blocker substantially resolved | Five repeated ID/level records use the donor's reviewed ordered-last replacement rule. One row has 49 fields while 763 have 48. `Skills.dat` is a donor-generated 381-record cache, not an authority over the text/client evidence. |
| Level/stat data | `ExpNeed.dat`: exactly 129 UInt32 thresholds, SHA-256 `7e5545f138028713335e455edf3b630b45f8cc147e1024f5b55409a47c04f01b`; `Stats.txt`: 480 keys | Resolved | Parse bounded, little-endian and culture-independent; corroborate class semantics with exact client. |
| Canonical NPC shop file | Root `Shop.dat`: 34 shops, 1,250 references/678 unique items, SHA-256 `bfc0d22a6c814a0df0f25c1203b968da585badb531a2828dcc4be050451b231a` | Former catalog blocker resolved | All item references resolve; this is the path the owner source actually loads. |
| Conflicting shop file | `shop/Shop.dat`: 32 shops, 1,293 item references and 84 unresolved references, SHA-256 `9dc830b03a9ab60e90401348fe18766023f15e99490ad9375787f7ccce095c36` | Rejected as canonical | Preserve for research only until its patch/provenance is established. |
| SQL reference data | `granger.sql`: SHA-256 `e9971b72badb113ad27f6c0131bceb07953bffb1443d6caa6a600d091ee70657` | Deduplicated source | Same 49 tables. Reviewed reference tables overlap the retained `revora.sql`; provenance-bound import uses the retained exact-hash export once and never executes either raw dump. |
| Portals | SQL has 222 canonical rows; `Portals.txt` has 218 rows/215 unique | Conflict resolved in favor of reviewed SQL/runtime provenance | Owner source loads SQL portals. Still validate map/doc/cell relationships before complete import. |
| Lottery | `Lotto.txt`: 1,314 records/183 first-field item IDs, SHA-256 `b6523c4678153a99d8fca95e5ddd792e1d0827851b270fdf988793baaae4e4ae` | Present, not adopted | Add a bounded parser, item closure and exact-client reward semantics before release. |
| Squamas | `Squamas.txt`: 84 five-field records, SHA-256 `b254a056e9b28670b77106f9cb90a2d851cfac2fbd44d94f3b0c60d923207cf8` | Present, not adopted | Validate map/cell ownership and rewrite timer/state handling. |
| Quiz | `QuizShow.txt`: 72 records, SHA-256 `97bc0b63e9f2cd6c4489ed03bc4761203b75cff6c77cafb15d5bd0f8ff9780b4` | Present, not adopted | Text/content review and declarative event model are required. |
| Event/DMap-owner catalogs | `EventMaps.dat`: 8 IDs, SHA-256 `1a764788dc1b22c810ad6293d4f857fec7a464553c7ea1991615ceedceb4b221`; `DMapOwner.dat`: 45 rows, SHA-256 `470f9de839ad6434f06e26e22e803cc9a9177014cf9767d94744adf68a23e494` | Present, not authoritative | The donor loaders are commented; establish runtime semantics before use. |

The package also supplies drop-rate configuration, lottery/squama data,
companions, class stats, event-map metadata, furniture assets and persisted
legacy state. Presence is not adoption: each active category still needs a
versioned schema, source digest, referential validation, exact-client packet
evidence, transaction tests and a feature gate.

## Transactional staging result

The combined first-party import path validates 20,338 Essentials catalog rows
and 7,103 allowlisted owner-SQL reference rows. A disposable PostgreSQL 17
apply and idempotent second apply succeeded with every feature gate remaining
disabled. The staged, releasable counts are: 222 map records, 213 map documents,
8,408 item definitions, 5,063 enhancement rules, 189 monster definitions, 989
map-closed spawn regions, 181 specific drop rules, 1,841 equipment-drop
candidates, 759 magic type/level records, 129 experience thresholds, 480 class
stat records, 1,371 map-closed NPCs, 158 map-closed portals, 27 NPC-bound shops
and 1,094 shop-item rows.

Quarantine is explicit rather than inferred: nine missing Kunlun DMaps, spawn
maps 8004-8006, NPC maps 9999/10200, nineteen absent portal map IDs, seven
`Shop.dat` IDs without reviewed NPC placement, 417 unresolved SQL custom-shop
item references and fourteen SQL/Essentials ItemAdd conflicts. SQL `customshop`
and `itemadd` are retained as provenance-bound reference rows; neither overrides
the canonical Essentials shop/enhancement catalogs.

## Sensitive or intentionally excluded material

The following categories are never eligible for automated import:

- `Config.ini`, which can contain database or service secrets;
- `Users/Characters/*` and other historical character state;
- legacy accounts, password/reset data, private identifiers or sessions;
- online-player, IP, logs, payment, voting, VIP, ban and ranking histories;
- guild ownership, house ownership, nobility and kill-board snapshots;
- `Navicat.rar`, both nested keygens, installer and crack material;
- `MySql.Data.dll` and every other compiled legacy dependency;
- raw donor SQL execution, legacy MySQL/NHibernate persistence, or dynamic SQL;
- puzzle/scene/part/client assets in Git or public packages;
- unknown-license third-party fragments.

## Provenance boundary

The package is `owner-provided-authorized-data`. The owner source remains the
primary gameplay-behavior donor. The exact approved 5165 client remains the
authority for packets and client state. Revora remains authoritative for
authentication, AccountId/Character UID ownership, PostgreSQL, networking,
cryptography, transactions, rate limits, replay protection, auditing and service
lifecycle.

No general LICENSE, COPYING or NOTICE was found in this resource package.
Owner authorization does not convert bundled Navicat, MySQL, client, crack or
other third-party material into reusable code. Consequently this intake does not
require a new `THIRD_PARTY_NOTICES.md` claim; notices must be added only when a
specifically licensed component is actually incorporated.

The machine-readable implementation/resource state is
`research/gameplay/OWNER_GAMEPLAY_ADOPTION_LEDGER.json`.
