# Leftover Conquer Point bags

M6.56b9a, 2026-09-22. Server implementation; not deployed.

Ordinary 1009/action-4 reaches `ItemHandler.UseItem` for these IDs because
`GetPositionFromID` returns inventory for non-equipment items. Direct
`UseItem` shares the same owned service.

One transaction consumes one owned unit, persists the existing CP award from
an exact durable `entities.conquerpoints` preimage, and inserts
`revora_cp_bag_uses`. The thirty-five `#region Bags Cps` amounts are unchanged.
720685 still sends only the Nimbus 138000 CP world announcement after a
successful commit; unlike the other 34 cases, its legacy branch had no
personal receipt message. The thirty-five legacy switch cases are fail-closed.

| IDs | CP |
| --- | ---: |
| 729911 | 3 |
| 3001133, 720657 | 5 |
| 729912 | 6 |
| 720656, 3001134 | 10 |
| 3001135, 720655 | 20 |
| 720658 | 25 |
| 720663, 720659 | 50 |
| 720660, 720664 | 100 |
| 720665 | 200 |
| 720675 | 250 |
| 720653 | 270 |
| 720681, 720676, 3001136 | 500 |
| 720677, 720682, 720687 | 1000 |
| 720661 | 1350 |
| 720683, 720688 | 2000 |
| 720693 | 2500 |
| 720666 | 2700 |
| 720689 | 4000 |
| 720694 | 5000 |
| 720662 | 6900 |
| 720695 | 10000 |
| 720678 | 13500 |
| 720667 | 13800 |
| 720684 | 27000 |
| 720685 | 138000 |

Stack remainders and zero-stack singletons are preserved. Overflow above
`uint.MaxValue` refuses without debit.

## Remaining work

MeteorBox 723268, Chi pills, EXP-ball pack 720757 and other leftover
split-writes remain open. Native acceptance and deployment remain open.
Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.


## Surprise Box registry extension (2026-10-03)

The owned registry now also includes722178 for its existing2000-CP exchange,
with its legacy38-entry capacity limit and no success message. A transactional
constraint migration retains the35 previous IDs. Concurrent wire use, direct
use, receipt-write rollback/fencing, old-ID regression and cold persistence pass.
Boxes are supplied ownership fixtures; no ordinary issuer or new quest route is
claimed. [Full scope](SURPRISE_BOX.md#atomic-consumer-and-process-validation-2026-10-03).
