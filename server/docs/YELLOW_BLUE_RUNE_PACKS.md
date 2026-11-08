# Yellow and blue rune consumers (M6.56b10h)

Source implementation only. No deployment or live restart. Parent M6.56b and
native Windows/6711 acceptance remain open.

## Contract

| Source | Exact payment | Output |
| --- | --- | --- |
| YellowRuneFragment 3306370 | 20 units | One bound Plus-0 3306510 |
| BlueRuneFragment 3306371 | 20 units | One bound Plus-0 3306511 |
| Random yellow pack 3306510 | One unit | One bound level-1 yellow rune |
| Random blue pack 3306511 | One unit | One bound level-1 blue rune |

Zero/one stack size represents one unit. Fragments are selected in ascending
UID order; the clicked fragment need not be consumed. Partial stacks retain
their exact remainder and binding. All-unbound, bound and mixed inputs retain
the existing always-bound output contract. No cost, reward, stock or fee was added.

Yellow uses `Next(1,15)`: picks 1-12 map to 4030101 through 4031201 in steps of
100, and picks 13 and 14 both produce 4031301. Blue uses `Next(1,10)`: picks 1-9
map to 4020101 through 4020901 in steps of 100. The old blue case 10 (4021001)
remains unreachable. Each committed receipt stores the actual pick and reward UID.
Rune skill metadata comes from the installed catalog and is restored by the
production item loader; rewards retain Plus 0, stack 1, color 3 and catalog durability.

## Admission and persistence

Both ordinary 1009/action-4 and direct `UseItem` reach the owned service. The four
old case bodies throw if bypassed. Valid current-session, alive, fully loaded
ordinary players can use these items; no GM or owner allowlist applies. Trading,
booths, stale sessions and persistence fences block use. Packet handling checks
frame length, packet type, action and client seal.

Sources must be in the bag, outside warehouse/wardrobe, unlocked, not suspicious,
and permanent or unexpired. Capacity uses the slots actually released: a full
bag can exchange fully consumed sources, but cannot keep a source remainder and
add a reward without room.

Under `ItemSyncRoot`, an entity row lock protects each transaction. The complete
durable bag UID set must match the live bag. Each source DELETE/UPDATE checks
owner, type, stack, binding, location, restrictions and expiry preimage and must
affect exactly one row. Reward insertion and the durable receipt must also affect
one row. Source writes, reward and receipt commit together in
`revora_yb_rune_combines` or `revora_yb_rune_pack_uses`. Live installation happens
only after commit. Failure fences the session; production reload restores either
the unchanged payment after rollback or the committed reward and remainder.

## Issuance and validation

This is consumer ownership for existing items. SuperBoss yellow fragment case 17
is unreachable under `Next(1,17)`. NPC 900181 blue options still issue 3306368,
not 3306371. Neither issue is changed or presented as a functioning producer.
Red rune issuance and rewards remain unchanged.

`Verify-PotencyHunt.ps1 -YellowBlueRunePacksOnly` exercises ordinary players,
all random outcomes, exact/insufficient/zero/single/split/remainder payments,
full-bag capacity, bound round trips, restrictions, stale preimages and bag
membership, concurrency and legitimate repeat payments, malformed packets,
silent database failures, rollback/retry, and post-commit installation recovery.
The red rune regression preserves the issuer audit; the star-stone regression
mechanically verifies all 518 cases, 155/265/55/43 totals and 66 intercept families.

Other leftover packs and native acceptance remain pending. Review evidence is
recorded against the published commit in the supervisor coordination mailbox.
