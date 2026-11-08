# Live combat and native Ninja 5165 integration

## Scope and evidence boundary

This work starts from `feature/native-ninja-5165` commit
`9fb24fd297c67b3a7da2a785958e596184fb7713`. It adds a reusable live-entity and
combat runtime, strict encrypted Host dispatch, PostgreSQL transactional
persistence, atomic Ninja creation, dual-Katana handling, skill persistence,
and ordered Ninja promotion persistence. It does not replace Revora identity,
world, movement, inventory, or networking architecture.

Evidence terms used here are strict:

- **Exact-client static evidence**: derived from the approved `Conquer.exe`
  SHA-256 `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`
  and its approved data files without executing them.
- **Owner-source evidence**: behavior inspected as authorized text/data. It is
  corroboration, not wire authority.
- **Automated encrypted evidence**: an exact-layout fixture passed through the
  real DH/Blowfish Game reader and production dispatcher. It is not a genuine
  Windows client.
- **Genuine-client evidence**: observed traffic from the approved Windows
  client. There is no genuine combat/Ninja traffic yet.

Production feature gates default to disabled. Deployment of disabled code does
not constitute Ninja activation or client acceptance.

## Architecture

`LiveCombatEntityRegistry` owns a bounded concurrent UID index. Every snapshot
contains kind, immutable authenticated owner for players, map/instance, server
coordinates, health, attackability, version, and monotonic last-touch time.
Registration is unique. Relocation, revive, removal, and health mutation are
serialized by an entity semaphore and version check. Nearby discovery filters
by exact map/instance and a bounded radius. Disconnect removes the player,
receipts, visibility state, and transient Ninja states; map changes relocate the
same entity and clear transient Ninja states.

`LiveCombatPipeline` is the common normal/skill mutation boundary. It resolves
the authenticated attacker and authoritative target, validates ownership,
life, attackability, map/instance, exact server coordinates, range, PvP policy,
frequency, learned skill, profession, equipment, stamina, cooldown, and target
mode. The client never supplies authoritative ownership, map, health, or
damage. Request identity is deterministic and bounded to 256 in-memory receipts
per attacker.

PostgreSQL uses a serializable transaction and durable operation receipt. For a
player target, health and the receipt commit together using expected row
version and health. For a skill, stamina, player-target health, skill
experience/level, and receipt commit together. A database conflict applies no
in-memory change. A successful database commit is followed by the serialized
entity mutation while its mutation gate is held. Monster health is deliberately
session-owned; it resets with the live spawn catalog on Host restart.

`LiveCombatRuntime5165` bridges only authenticated handoff-backed Game sessions.
It loads enabled reviewed monster spawns, registers gated players, sends
visible `10014` monster records, processes `32/1022`, broadcasts `1022`/kill
results to nearby players, and sends `1103`, `1105`, stamina status, and removal
packets. `Genuine5165RuntimeComponent` keeps malformed or unsupported,
well-framed combat semantic errors inside the session loop without disturbing
movement, NPC, inventory, or warehouse dispatch.

## Exact packet evidence and dispatch

The exact layouts and addresses are retained in
[PROTOCOL_RESEARCH.md](PROTOCOL_RESEARCH.md). Implemented dispatch is:

1. encrypted Game stream framing and footer validation;
2. packet ID `1022`, declared length exactly 32;
3. action at offset 20: normal melee `2`, ranged `28`, or magic `24`;
4. authenticated sender UID at offset 8 for normal attacks and in the decoded
   magic transform for skills;
5. target UID and claimed target coordinates validated against live state;
6. strict zero/reserved-tail validation;
7. generic combat pipeline;
8. exact normal/skill result and optional kill broadcast.

Static client evidence proves the field transforms and receive layouts. The
owner source corroborates the normal result and action-14 death shape.
Automated encrypted fixtures prove fragmented normal input, coalesced replay
plus movement, malformed-tail recovery, monster spawn, PvP in an explicitly
allowed test map, all seven skill requests reaching the pipeline, and continued
movement. They do not prove client presentation.

## Migration V034

`db/migrations/V034__disabled_live_combat_and_native_ninja.sql`:

SHA-256: `b4eb96ee692271c0441f98b268de6fd9516c86b02d3e18ce7d34b126844e26c1`.

- permits profession stages 50-55 while retaining the enumerated profession
  constraint;
- permits the `ninja` base-stat family;
- extends combat receipts with request fingerprint, target version, health,
  damage, death, skill, stamina, and skill-experience evidence;
- creates `game.ninja_character_initialization` for idempotent creation;
- creates `game.ninja_promotion_receipts` for replay-safe ordered promotion;
- creates `gameplay_reference.live_monster_spawn_points` with source provenance,
  range checks, and disabled-by-default rows;
- records the exact owner `Stats.dat` source hash and all 120 rows from its
  fifth fixed-width block, decoded as four little-endian UInt16 fields;
- creates disabled `combat.runtime` and `ninja.native` gates and transitive
  dependencies;
- grants only required read/insert permissions to the restricted Game identity.

The migration is additive except for replacing enumerated check constraints
with strict supersets. Its checksum is recorded in the release evidence. A
rollback before activation is database restore from the protected backup; a
reviewed down migration may also drop only empty V034 tables/columns and restore
the old constraints. Once Ninja rows exist, rollback must preserve them and use
a reviewed data migration rather than dropping state.

## Creation, equipment, and professions

Profession 50 creation is one atomic transaction tied to immutable `AccountId`
and Character UID. An account-derived transaction-scoped PostgreSQL advisory
lock plus the locked account row serializes same-account creation before a UID
is allocated. Character, attributes, vitals, stat snapshot,
inventory profile, progression state, wallets, and Ninja-initialization receipt
commit together. Concurrent duplicate creation returns the committed character
and allocates no second character.

Katana is never accepted by an integer range alone: the authoritative item row
must exist and then satisfy sparse exact-client bounds. Ninja stages may equip
a validated Katana in right or left hand. A second Katana requires the first
hand to contain a Katana. Non-Ninja professions cannot use the Ninja exception.
Inventory, equipment, and warehouse retain the existing immutable Item UID and
ownership transaction rules.

Promotion persistence supports only consecutive 50->51->52->53->54->55
transitions, validates the modeled level gate, locks the character row, updates
the profession once, and writes a replay-safe receipt in the same serializable
transaction. No promotion NPC route is enabled because an exact reviewed NPC
option flow has not been established. Ninja rebirth remains gated because the
generic rebirth transformation/loadout catalogs do not yet prove Ninja lineage.

## Skill implementation status

| ID | Name | Runtime result | Evidence and limitation |
|---:|---|---|---|
| 6000 | Twofold Blades | Targeted damage, stamina/cooldown/range/dual-Katana validation, XP | IDs, costs, range, levels and percentage are static-data proven; final damage base is conservatively configured and not genuine-proven. |
| 6001 | Toxic Fog | Current-HP bounded damage without reducing below one; Poisoned status | Client/data support is proven; periodic poison cadence and canonical stacking remain provisional. |
| 6002 | Poison Star | Self status with resource/cooldown and cleanup | Status request/update path is automated-encrypted only; item-use suppression integration is not present. |
| 6003 | Counter-Kill | Self status with resource/cooldown and cleanup | Activation is implemented; reactive counterattack resolution is not yet connected, so canonical behavior is incomplete. |
| 6004 | Archer Bane | Flying-target-only bounded current-HP damage | Request is decoded and safely rejected because live flying state is not yet authoritative. It is not claimed playable. |
| 6010 | Shuriken Vortex | Self status; incoming nonzero normal damage bounded to one | The defensive transform is connected to the normal pipeline; timing/formula remains provisional pending genuine traffic. |
| 6011 | Fatal Strike | Self status; outgoing monster normal damage uses evidenced 500% transform | Target acquisition/movement conveniences of the client skill are outside this server combat slice. |

All seven requests validate profession, learned level, equipment when required,
stamina, monotonic cooldown, map, range, target mode, replay, and persistence.
Temporary states clear on disconnect, target death, and map change. Skill XP is
currently a conservative one point per accepted use. A level advances only when
the imported exact magic record threshold and next-level row exist; experience
then resets as corroborated by the authorized owner source. These mechanics are
implemented and automated-tested, not claimed canonically or genuine-proven.

## Feature-gate and activation boundary

Both `combat.runtime` and `ninja.native` are disabled by V034. Evaluation is
transitive: every dependency must be enabled, have its migration/catalog gate
satisfied, and be globally or identity-canary authorized. `combat.runtime`
depends on `combat.pk` and `monsters`; `ninja.native` additionally depends on
combat, skills, and rebirth. A disabled dependency cannot be bypassed merely by
enabling the leaf gate.

The production database currently has no enabled live monster spawn rows and no
approved Katana catalog rows. The existing `SHEEKOVIC` account already owns a
non-Ninja character under the one-character-per-account model. It must not be
deleted, renamed, or converted silently. A genuine Ninja creation campaign
therefore requires an explicitly authorized fresh canary account (or a separate
reviewed multiple-character/reset policy).

## Test evidence

The final report records exact commands and counts. Notable coverage includes:

- entity uniqueness, lookup, relocation, bounded cleanup, map/instance
  isolation, revive, concurrent damage, and exactly-once death;
- normal PvE/PvP, range/coordinate/frequency failures, replay/conflict,
  persistence failure, and nearby broadcast selection;
- strict normal/skill/spawn/kill packet layouts and malformed tails;
- atomic concurrent Ninja creation, restart persistence, dual Katana, skill
  stamina/XP/replay/level advance, and promotion 50-55;
- encrypted DH/Blowfish normal and Ninja campaigns plus movement after combat;
- existing world-entry, movement, jump, portal, NPC, inventory, equipment,
  shop, and warehouse regressions;
- production-shaped Host startup/shutdown with both new gates disabled.

No combat/Ninja genuine Windows owner acceptance has occurred. No test is
reported green when skipped or blocked.

## Remaining safe gates

Before Ninja activation, import the exact sparse Katana/item records, add a reviewed live monster
canary spawn, establish authoritative flying state, finish Counter-Kill and
Poison Star integrations, prove promotion routing, and obtain a fresh immutable
canary identity. PvP remains deny-by-default unless a reviewed map rule enables
it. Full AI, revive dispatch, drops, repair/upgrade, and live trade are separate
vertical slices; consequently Katana behavior in those unimplemented runtime
paths is not claimed. Canonical combat formulas also remain a separate evidence
gate.

## Dedicated account-canary activation (V035)

The exact normalized identity lookup for `sheekoninja` on 2026-08-30 resolved
one active Player account, immutable AccountId
`ee87b451-86bf-425e-850e-fd7f479d6796`. It had zero character rows, zero legacy
Character UIDs, and no active `gm` assignment. No credential, hash, ticket, or
session column was selected. Runtime authorization uses only this immutable ID;
the username is not a runtime gate.

`V035__native_ninja_canary_catalog.sql` imports 548 exact sparse records from
the approved client `itemtype.txt` (SHA-256
`e15b066b324cf9eac62a7137f96026ec1e12b12aea516b1605fce99fafd69cc4`):
273 Katanas, 88 Ninja veils, 88 Ninja hoods, and 99 Ninja vests. Each row retains
its source-record SHA-256. The migration also defines a disabled Pheasant type-1
spawn, UID `400000035`, on map 1002 at DMap-accessible `(454,455)`. Enabling that
single row is a separate deployment operation.

A zero-character account can now be authorized before character creation via
`gameplay.account_feature_canaries`. After authentication, the runtime still
requires the supplied Character UID to belong to that same immutable AccountId.
For the dedicated canary only, genuine Ninja creation atomically creates five
bound test items (two type-601301 FenceKatanas and types 112000, 123000, 135000),
learns the seven evidenced level-zero skills, credits exactly 1,000,000 CP for
one reviewed maximum-level service, and writes one append-only grant receipt.
All character, skill, wallet, item, location, and receipt rows commit or roll
back together. The grant is not available to a non-canary Ninja account.

Dual Katana state is authoritative: a left-hand Katana requires a right-hand
Katana; the right-hand Katana cannot be removed while the left hand remains
occupied. Both immutable Item UIDs retain the ordinary inventory and warehouse
single-location guarantees.

Counter-Kill now resolves as a bounded reactive normal-attack branch. Exact
MagicType data proves its 30% activation chance and level damage percentages;
the server derives a deterministic replay-stable roll from attacker, defender,
and request identity, applies at most one counter mutation, and never recursively
invokes combat. Poison Star suppresses evidenced action-4 recovery-item use until
monotonic expiry and clears on death, logout, disconnect, and map transition.
Flying state is server-owned transient entity state, cannot be supplied by a
1022 request, clears on map transition, and is removed by accepted Archer Bane.

Toxic Fog remains explicitly provisional: exact data proves a one-minute status,
20 described applications, level percentages, and the never-below-one bound, but
not an independently reconciled patch-5165 tick scheduler. The activated canary
therefore applies the conservative bounded initial current-HP mutation and status
only; it does not fabricate periodic damage. Counter-Kill's deterministic mapping,
Twofold base damage, one-point skill experience, Fatal Strike acquisition/motion,
and Shuriken Vortex timing remain configurable or provisional pending genuine
traffic. Ninja rebirth remains disabled and is no longer an ordinary Ninja gate
dependency.

The next safe step is the single genuine Windows campaign against the isolated
canary after V035, the deterministic Host, the immutable account canaries, and
the one Pheasant row are deployed and verified. Global combat and Ninja remain
disabled/canary-scoped until that evidence is complete.
