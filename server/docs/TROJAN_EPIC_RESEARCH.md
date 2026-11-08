# Trojan Epic restoration baseline

Research checkpoint, 2026-09-21. This does not complete a new earning route.
The subsequent [durable storage prerequisite](TROJAN_EPIC_STORAGE.md) is implemented
and tested; ordinary world integration remains unfinished.
The [derived snapshot](research/TROJAN_EPIC_BASELINE.json) records input hashes,
public catalog metadata and read-only database observations. No client assets,
account information or credentials are included.

## Verified local evidence

- The installed 6711 journal has twelve linked Trojan missions: 3269, 3278,
  3275, 3270, 3276, 3271, 3277, 3272, 3273, 3279, 3280, 3274.
  They specify level 120 and professions 10 through 15.
- The legacy server instead exposes a four-boss material loop through NPCs
  66712, 127881, 127871, 127861 and 122861. Those actors and the four monster
  definitions exist in the queried database. Each monster has one spawn group;
  this is not proof of simultaneous population or effective combat damage.
- Maps 4121 through 4124 alias terrain 3825. Its server grid is 96 by 96 and
  its client archive exists. Each entry at 45,45 has a static walking approach
  within six tiles of its room NPC at 34,54. Shared aliases must not be changed
  globally to repair an individual quest room.
- All eight native journal NPC IDs (including final-stage General Pak 10603)
  were absent from the database query. The audit derives these IDs from the
  journal and rejects incomplete database query coverage. Journal map IDs
  3830 through 3832 lack queried map rows, standalone server grids and client
  index entries. They may represent retail dynamic instances; this does not
  establish that their client terrain assets are missing.
- The five legacy item identities agree between server and client. Reward
  614439 is FangCrossSaber, profession 15, level 140, the maximum loaded level
  in the 614 weapon family. The legacy grant does not provide the owner's
  requested +12, -7, two-socket enhancements.

`Npcs.cs` checks level/class/rebirth in the entry menu, but its legacy terminal
options do not repeat those checks locally. The general dispatcher checks map
NPC membership and trading; it does not establish an owned Trojan progression
transaction. The 50-million-CP shortcut and final material exchange separately
debit and add the item. These are restoration targets, not a demonstrated exploit.
`MonsterTable.cs` has name-based material drops rather than an owned quest-stage
contract. Native acceptance, progression persistence and replay safety remain
unverified.

Further source tracing establishes that the legacy entrance is intentionally
closed: `TwinCityPlaza.ClosedDeskIds` contains 66712 and `Npcs.GetDialog` calls
`HandleClosed` before reaching its legacy switch. Traveling to jail via NPC
10081 does not reopen it. Trojan Star's option 50 instead sends players to
Twin City 270,194; it does not enter an owned trial. Option 49 accepts classes
10 through 25 at level 40 and grants all four epic arts, without a completion
receipt. These are source findings, not native-client observations. Restoration
must replace the entrance and trainer together with owned completion/learning;
simply reopening Pak would expose the old exchanges.

## Retail reference and differences

The [official 2014 guide](https://co.99.com/guide/quests/2014/trojan_epic_quest/index.shtml)
describes four story stages and 90 energies, with one free energy daily. It lists
level-120 or reborn Trojans as eligible. Weapon conversion inherits attributes;
permanent souls survive while temporary souls do not. Breath Focus follows the
first completion, Super Cyclone the seventh, and two sabers enable the other
listed epic skills. Its coordinates differ from this installed journal.

This reference is not verification of 6711 wire behavior. The local journal's
level requirement and legacy second-rebirth restriction differ from the guide.
Do not relabel the existing four-boss loop as the original story or invent
energy packet semantics from the guide's overlapping stage-day counts.

## Implementation sequence

- [x] Capture bounded read-only catalog evidence and static room reachability.
- [x] Add synthetic parser and terrain rejection tests.
- [ ] Resolve the native journal's instance mapping and initial ordinary-player
  travel route; establish the chosen eligibility and energy contract from local
  evidence, documenting any Revora adaptation.
- [ ] Replace Trojan Star's stale travel and unearned epic-learning branches
  with the restored entry and durable completion checks. Review rebirth learning
  separately; legacy inheritance code is not evidence of earned completion.
- [ ] Implement per-character durable stages, eligible monster credit and
  admission checks for every action. Isolate quest instances where required.
- [ ] Replace split debit/grant paths with one owned transaction, receipts,
  rollback and duplicate-claim protection. Apply the valuable weapon policy
  without destroying an existing converted weapon's attributes. One-time P7
  rewards remain allowed and bound under the current owner policy.
- [ ] Validate non-GM earned completion, failure recovery, concurrent claims,
  autosave interaction and fresh persistence reload. Audit skill unlocks.
- [ ] Prepare matching journal content, then perform native client acceptance
  and a separately authorized coherent deployment.

Reproduce catalog evidence with `tools/Read-TrojanCatalog.ps1`, then pass its
ignored JSON output to `tools/audit_trojan_epic.py --snapshot ... --output ...`.
The harness runs a read-only transaction and rolls it back. It does not deploy
or mutate gameplay data. The audit's four-direction grid search is conservative
static evidence, not a live navigation test. It reserves the talker's full
three-by-three footprint, matching the ordinary `Map.AddNpc` branch. Seven
synthetic tests pass, including rejection of an entry overlapping that footprint.
