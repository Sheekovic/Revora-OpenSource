# EXP protection pill transactions

M6.56b4, 2026-09-22. Server implementation; not deployed.

Ordinary players can use EXPCarePill **3002559** through the existing item-use
path. One transaction consumes one owned unit, saves one hour of
`ExpProtectionTime` and records the use.

## Evidence and policy

The live `ItemHandler` branch removed the item and assigned
`ExpProtectionTime = 3600` with no receipt and no durable timer preimage.
[TimeDrop hourglass issuance](TIMEDROP_HOURGLASS.md) is closed, so this is the
next explicit-contract active compound item on the M6.56b inventory.

The existing duration is preserved: one hour. Active protection is not
overwritten or shortened. Combining ten 3002560 scraps is repaired in
[EXP_CARE_COMBINE.md](EXP_CARE_COMBINE.md). Direct daily P7 is unchanged.

## Ownership, persistence and packets

Only a healthy canonical ordinary session may use its exact owned, unlocked,
non-suspicious inventory item outside trade/booth. Warehouse, equipment,
wardrobe, expired or substituted sources cannot be consumed. Binding and
zero-stack historical singletons are preserved. The existing 1009 action-4
frame is bounded to 32..128 total bytes.

Under the inventory owner lock, the transaction locks the character, checks
the saved protection preimage and complete bag UID set, consumes one source
unit, writes `entities.protectiontime` and inserts a
`revora_exp_protection_uses` receipt. Failed writes roll back together and
require reload before retrying. Direct `ItemHandler.UseItem` uses the same
service. The legacy 3002559 case is fail-closed.

`ExpProtectionBalance` keeps the durable preimage separate from pending
countdown ticks. Countdown, autosave and grants share the owner lock. Login
refresh sends the existing timer without extending or writing it. A PK death
that still has more than ten minutes of protection subtracts 600 committed
seconds through the same setter. Killer scrap 3002560 issuance stays outside
this transaction.

## Remaining work

Leftover TimeDrop hourglass use (3007985/3007989/3007992) is repaired in
[TIMEDROP_HOURGLASS.md](TIMEDROP_HOURGLASS.md). Magnolia issuance,
genuine-client timer display and deployment remain open. The parent
[M6.56b inventory](COMPOUND_REWARD_INVENTORY.md) is not exhaustive.
