# Retained career storage prerequisite

## Stone City warehouse (2026-10-01)

The ordinary game-session constructor in `Client/GameState.cs` registers Stone
City warehouse 4101 along with the other city warehouses. The private retained
career loader had only registered six city/market warehouses and omitted Stone
City. Its full bags therefore did not establish exhaustion of ordinary storage.
This batch adds the existing Stone City warehouse to the fixture's combined
warehouse setup. Production capacity and access rules do not change.

The loaded banker is WarehouseMan 4101 on map 1213 at 425,269. The retained
character had no items in that warehouse and a full 40-item inventory after the
BP champion claim. Twenty owned bound 722136/723467 items were deposited through
normal banker interaction and sealed warehouse action packets. The bag fell to
20, the bank rose to 20, and each transferred UID remained owned.

After a database/game restart, all twenty saved UIDs load into the normal bank.
One is withdrawn and redeposited through the same banker. The complete retained
career/BP snapshot matches exactly afterward, covering prior items, rewards,
progression, achievements, Chi and event histories. No reward was deleted, no
capacity was increased, and no item was inserted directly into storage.

Passing phases: `career-stone-storage` and `career-stone-reload`.
Candidate SHA256:
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Checkpoint: `revora_stone_storage_probe`, containing the snapshot and twenty UIDs.

Physical travel/proximity is controlled by the fixture; this does not prove a
continuous route to Stone City or native warehouse presentation. Earlier normal
trades remain valid completed transfers, but were not evidence that every bank
was full. This adds no achievement credit. No live process, deployment or client
patch changed. The freed inventory supports remaining ordinary earning paths.
