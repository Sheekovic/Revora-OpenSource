# Native Ninja support for the approved 5165 client

## Outcome and safety boundary

The approved client contains conclusive native Ninja presentation, equipment,
and skill-protocol support. Commit
`9fb24fd297c67b3a7da2a785958e596184fb7713` is the frozen static-evidence and
domain foundation described by this document. The subsequent live entity,
combat, encrypted Host, and PostgreSQL work is tracked separately in
[LIVE_COMBAT_NATIVE_NINJA_5165.md](LIVE_COMBAT_NATIVE_NINJA_5165.md). Neither
automated encrypted fixtures nor static analysis are described as genuine
Windows-client acceptance.

Starting baseline: `feature/native-ninja-5165` from
`d442147262924ad6db658f0ad5bb3be2a1b5b4d2`.

## Reproducible evidence inventory

The approved tree is retained outside Git at
`/root/revora-research/launcher-canonical/revora-5165-stable`. Its
`Conquer.exe` SHA-256 is
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
It was inspected statically and never executed during this work.

Representative commands:

```text
sha256sum Conquer.exe ini/ProfessionalName.ini ini/itemtype.dat ini/MagicType.dat
grep -n -i ninja ini/ProfessionalName.ini ini/selectCharacter.ini
find c3 sound pic -type f | grep -Ei 'ninja|601[0-9]{3}'
Revora.ClientEvidence tq-file-decrypt ini/itemtype.dat <ignored-output>
Revora.ClientEvidence tq-file-decrypt ini/MagicType.dat <ignored-output>
objdump -D -Mintel Conquer.exe
```

The decoder is Revora-owned, bounded to 16 MiB, and implements the documented
TQ seed-9527 file transform. Decoded proprietary data and disassembly remain in
ignored research storage and are not committed.

Measured records:

- Professions 50–55: Intern Ninja, Ninja, Middle Ninja, Dark Ninja, Mystic
  Ninja, Ninja Master.
- Character selector entries: `vocation8` male Ninja and `vocation9` female
  Ninja.
- 273 Katana definitions in sparse quality-coded IDs 601000–601339; all have
  required profession 50. The range must still be checked against a loaded
  item definition; it is not treated as a guarantee that every integer exists.
- 88 Veil records 112000–112109, 88 Hood records 123000–123109, and 99 Vest
  records 135000–135109 with required profession 50.
- Seven native skill IDs: 6000, 6001, 6002, 6003, 6004, 6010, and 6011.
- Owner-provided `Stats.dat` SHA-256
  `dbc2076b14a98fc5303c916a9b033d347b99a09d3f7e482392c28d3c939e6281`
  contains a fifth 120-level stat family. Sentinel rows are level 1
  `(1,8,1,0)`, level 40 `(19,106,17,0)`, level 70 `(33,181,29,0)`, level 100
  `(48,256,41,0)`, and level 120 `(57,306,49,0)` in
  strength/agility/vitality/spirit order.

Exact packet addresses, layouts, file hashes, official references, and source
pins are recorded in [PROTOCOL_RESEARCH.md](PROTOCOL_RESEARCH.md).

## Foundation gap matrix at commit 9fb24fd

This table is intentionally frozen at the foundation boundary. See the live
runtime report for current status.

| Area | Baseline gap | Current branch | Evidence/remaining gate |
|---|---|---|---|
| Character creation | Parser allowed base profession 50, but no Ninja-specific initialization | Existing 1001 validation remains unchanged; profession policy is modeled | Client selector and 1001 profession are static-proven. Atomic Ninja stat/profile initialization is missing. |
| Profession stages | DB allowed 50 only; runtime family mapping omitted Ninja | 50–55 and promotion levels 1/15/40/70/100/110 modeled | DB constraint and promotion persistence for 51–55 are not migrated. |
| Stat allocation | No Ninja family in `base_character_stats` | Exact Stats.dat checkpoints modeled | Full 120-row validated importer/migration and creation hook are missing. |
| Equipment validation | Generic requirements; no Katana/Ninja policy | Katana, Veil/Hood/Vest bounds and dual-hand policy modeled | Production item catalog lacks the client-derived 601 records; live equipment policy is not wired to these rules. |
| Normal attacks | No live 1022 combat dispatch | Exact 32/1022 skill codec exists; ordinary combat remains unchanged | Live entity targeting/damage/PvP is absent. |
| Ninja skills | No Ninja runtime | All seven definitions, authoritative validation, bounded formulas/statuses, and exact 1103/1105 codecs added | Not connected to encrypted Host dispatcher or PostgreSQL skill state. No genuine Ninja traffic yet. |
| Status effects | No Ninja state machine | Session engine applies/expirs and clears temporary Ninja states | Durable/online entity status synchronization and exact status packets remain missing. |
| Skill learning/leveling | Generic tables only | IDs/levels validated by learned-skill map | Promotion NPC learning rules, experience persistence, and 1103 update path remain missing. |
| Rebirth/second rebirth | Generic progression excludes Ninja family/stages | Compatibility helpers identify Ninja family only | Canonical 5165 lineage transformations and integration are missing. |
| NPC promotion | No Ninja promotion route | No unsafe donor script adopted | Declarative promotion NPC/action remains missing. |
| Persistence | Generic character/skill tables exist | No production schema mutation | Full Ninja stats, professions, skills, proficiency and status persistence migration remains missing. |
| Death/revive/logout/map cleanup | No Ninja states | Deterministic cleanup methods added and tested | Host lifecycle calls are not wired because live skill sessions are not enabled. |
| PvE | Monster/combat scaffolding only | Ninja validator supports authoritative creature targets | No production monster entity runtime or genuine damage sequence. |
| PvP | No live player combat | Ninja validator supports player target/map/range/flying predicates | PK rules, teams/guild interactions, damage/status broadcasts, and genuine validation are missing. |
| Client presentation | Unknown | Native assets, motions, sounds, icons and exact receive branches proven | Presentation still requires genuine owner acceptance after server integration. |
| Observability | No Ninja audit taxonomy | No production instrumentation deployed | Sanitized per-skill accept/reject categories must be added with Host integration. |
| Tests | No Ninja tests | Exact codecs plus 13 domain test groups added | Encrypted Host, PostgreSQL restart, multiplayer, and genuine-client campaigns remain missing. |

## Implemented security boundary

`NinjaPackets5165.ParseSkillAttack` requires exact declared length 32, ID 1022,
action 24, and sender UID equality with the authenticated session. `1105`
target rows are bounded to 255, have exact length, reject zero UIDs, and require
zero reserved words. The domain engine rejects wrong professions, unknown or
unlearned skills, forged skill levels, missing dual Katanas where required,
dead/self/foreign-mode targets, wrong maps, excessive range, insufficient
stamina, cooldown bypass, conflicting replay, and invalid status retention.
Cooldown input is monotonic elapsed milliseconds supplied by the owning server
session; client wall-clock values are never authority.

Formulas are named, bounded functions derived from exact MagicType values:
Twofold 110/115/120/125/130%, Toxic Fog 10–50% current HP without reducing
below one, Counter-Kill 20/28/36/42/50%, Fatal Strike 500%, Archer Bane 10%
current HP without reducing below one, and Vortex incoming nonzero damage fixed
at one. They are not connected to production combat pending complete field and
genuine-client validation.

## Reference selection

The owner-provided 5165 source is authorized behavior evidence, but its global
state, authentication, raw SQL, broad exception handling, and timer patterns
were not copied. Comet 5065/5187 and COPS v7 are licensed secondary references.
OpenConquerEmulator5517, HeroesCoServer, Redux, and World Conquer Source V2 were
not copied because of patch mismatch, incomplete behavior, or unresolved
licensing. Exact pins and license classifications are in
[PROTOCOL_RESEARCH.md](PROTOCOL_RESEARCH.md).

## Monk feasibility (separate and excluded)

The approved 5165 profession and selector evidence contains Ninja but not
Monk. Official chronology places Monk in the 5350–5351 era in December 2010.
A native Monk implementation therefore requires a controlled client upgrade
to at least 5351 (or a later reviewed target such as 5517), including protocol,
data, asset, launcher-manifest, and regression requalification. Backporting
would require authorized assets plus extensive client data and likely
executable control-flow changes, increasing antivirus, integrity, maintenance,
and security risk. A future reviewed client upgrade is safer than modifying
the approved 5165 executable. No Monk or Samurai code/data was added here.

## Deployment and next gate

Production is unchanged. A partial Host was not built or deployed because the
live combat/entity/persistence dependencies above make a genuine Ninja owner
campaign unsafe and incapable of meeting the requested vertical acceptance.
The smallest safe next step is to implement the generic live 1022 combat/entity
runtime and PostgreSQL Ninja catalog/profile migration behind a disabled gate,
then pass encrypted multi-entity fixtures before requesting one genuine Ninja
creation/combat campaign.
