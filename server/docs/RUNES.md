# Rune upgrades and prestige

2026-09-16 M6.50b follow-up: [rune admission and recovery](RUNE_ADMISSION_RESEARCH.md)
now enforces the installed class requirements, second rebirth, level 15, five
slots and distinct yellow types. Invalid saved items remain recoverable while
excluded from effects, title/achievement counting and live/offline prestige.
Class/level/rebirth changes refresh derived state; the active equipment set
selects combat rune effects. Build and 2,112 gameplay checks plus three audit
tests pass. [M6.50c](RUNE_ACQUISITION.md) also verifies the complete boss-RVP,
Vesper, maximum-rune and equipped-title route with 16 integration checks.
Native acceptance, deployment and acquisition-cost review remain pending.
Earlier checkpoints below retain their own validation/build history.

2026-09-15 loadout follow-up (code validated; not deployed): actions 57, 58
and 61 now commit equipment swaps, unequips and inventory/rune-bag transfers
under the character's item lock and a database owner-row lock. Exact persisted
UID, owner, type, position, warehouse, stack, binding and EXP preimages must
match. Both sides of a swap commit together; cache, item packets, skills, combat
stats and prestige update afterward. A failed or ambiguous write fences the
session for reload. Full inventory preserves the rune in its bag.

Action 61 is an existing toggle with no proven native nonce. A bounded
per-session cache suppresses repeated transfers of the same UID for one second;
a deliberate reverse works after that boundary. Equip and unequip have
idempotent terminal positions. This does not claim indefinite packet replay
protection. Fake, unloaded, dead, noncanonical and trading/booth sessions cannot
use the new loadout service. Composition now shares canonical-session and full-level equipment validation
with the loadout path (M6.46b).

A successful equip records achievement 11310 for one valid persisted rune and
11311 for five within one primary or alternate loadout. It checks every counted
row against the session's owned item and rejects duplicate slots. Partial sets
are not added together. [M6.46a](FULL_LEVEL_RUNE_ACHIEVEMENTS.md) now confirms the
ten class-specific full-level identities. M6.46b implements their atomic equip
and equipped-upgrade hooks with 589 gameplay checks and three audit tests; see
the linked checkpoint for its build hash and remaining acquisition/live gates.
Receipts commit with the equip and persist after unequipping or reconnecting.
Existing holders receive these receipts on their next genuine equip transition,
not from an unverified historical client bit or a generic inventory visit.

Three yellow rune boosters (Draining Touch, Blood Spawn and Free Soul) formerly
mutated the shared Perfection level table and dereferenced null for unrefined
characters. They now use the character's resettable rune bonuses; Perfection
reads combine them with a copy of the base effect. Other characters and the
shared catalog remain unchanged. Recalculation cannot accumulate the bonuses.
The existing requirement for a base Perfection effect is preserved.

The item loader no longer silently rewrites a duplicate occupied rune slot.
Corrupt duplicates require repair and leave both saved items untouched. A bounded
read-only audit found zero duplicate occupied slots and zero equipped rows with
invalid rune family/level/stack in the current database; this snapshot does not
replace the runtime checks. No live player data was changed.

Validation: `-RuneLoadoutOnly` passes 36 ordinary-player checks, including real
production packet handling, both sets, full bags, swaps, duplicate requests,
SQL failures at each transfer/award boundary, production inventory reload,
skill/prestige updates, and cross-character Perfection isolation. All 1,175
existing regression checks passed before the final derived-skill removal packet
projection; 222 focused checks passed after that final change. Native UI
acceptance and deployment remain pending. The staged assembly SHA-256 is
`2888FAB438FE5B032760F81977842B8E280E4535142DA1738EFF04B9632E4C42`.
The deployment record below describes the earlier composition release only.

The 2026-09-14 correction enables ordinary players to compose blue and yellow
runes with +1 through +8 stones. The previous action-63 handler had its entire
inventory-material branch commented out, returned the unchanged rune, and
the client displayed failure. Its old level loop also failed to terminate
safely at maximum level.

Stone EXP matches the approved 6711 preview: 10, 40, 120, 360, 1,080,
3,240, 9,720 and 29,160. Level costs use differences between cumulative
server rune_levexp rows, verified identical to the installed client table.
Blue runes cap at 27, yellow at 9; red runes remain level 1. Excess EXP at
the final level is discarded; an already-maxed rune consumes nothing.
Same-color unequipped rune materials include their accumulated EXP.
One selected stone UID consumes one unit from its stack.

Target ID, remaining EXP, bound state, and all material consumption commit
in one PostgreSQL transaction. Ownership, location, expected ID/EXP/stack,
trade/booth state, material locks, unique material UIDs, and strict packet
lengths are checked. Bound material binds the target. Invalid batches do not
partially consume items. A database conflict or ambiguous outcome requires
reconnect before another mutation. The client receives the updated item;
equipped rune upgrades also refresh skills, combat stats and prestige.
Login derives rune skills from equipped persisted items, repairing stale
saved skill levels after a crash.

Native requests have no unique composition ID. A bounded per-session cache
suppresses identical packets for one second, allowing later deliberate uses
of the same stack. Transactional item checks prevent reuse of deleted items;
this is not indefinite replay protection for partially consumed stacks.

Prestige now includes categories 21/22/23 (red/blue/yellow), which 6711 sums
for its rune line. Both saved equipped rune sets are included, matching the
server's equipped-item scoring approach. Rune bags, inventory, warehouses,
and invalid color/slot combinations do not count; a slot contributes at most
once. Red scores 3,000; blue ranges from 1,100 to 31,000; yellow from 1,200
to 11,000. All level values match the installed ability_score table. Online
snapshots and the offline ranking rebuild use the same scoring function.

Native protocol and table evidence is in PROTOCOL_RESEARCH.md. Input hashes:

- ability_score.dat: `c446a2c1617a621ba2cbaf7225620f5c35cff29bbc1bf63c83591eb3495fa248`
- rune_levexp.dat: `586956b144f0086904694c178733c4ada2cb40663fe855a7a9a77c1a4610355b`

Validation: build succeeded; `tests/PotencyHunt/Verify-PotencyHunt.ps1
-RunesOnly` passes 21 checks using an ordinary character in a disposable
PostgreSQL schema. These cover both colors, stack consumption, concurrent
duplicates, malformed inputs, ownership, binding, level caps, late transaction
rollback, live equipped upgrades, skill restoration, and offline prestige.
`tests/CharacterStatus/Verify-Prestige.ps1` passes 58 checks, including native
rune score totals, excluded locations, duplicate slots and 23-category packets.
No production player row is modified by the fixtures. In-game UI confirmation
remains separate from these server tests.

Deployed at 2026-09-15T00:00:58Z (September 14 Pacific), server PID 4072,
assembly SHA256 `BFB8E4BAD934691BDF70B3D25F37BBE69269962938AF6F99988ABEE0DA8ADCE7`.
Account/game handshakes and existing event status passed after restart.
No client patch or database migration is required. Ignored deployment records:
`obj/chi-refill-validation/runes-release`; previous assembly/PDB:
`bin/Debug/deployment-backups/runes-20260915-000047`.
