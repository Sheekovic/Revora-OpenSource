# Tournament and super EXP pack transactions

M6.56b0, 2026-09-22. Server implementation; not deployed.

Ordinary players can use catalog packs 720793, 720981 (one hour) and 728201
(two hours) through the existing item-use path. One transaction consumes one
owned unit, saves the double-EXP duration and records the use.

## Evidence and policy

The live `ItemHandler` branches granted those exact timer lengths after removing
the item, with no receipt and no durable timer preimage. [EXP potion
transactions](EXP_POTION_TRANSACTIONS.md) already repaired ordinary 10/60-minute
potions and left these identities open.

Catalog copy for 720793/720981 mentions immediate EXP; 728201 mentions five-times
kill EXP. Those readings are not adopted. This repair keeps the executable
durations and the installed double-EXP kill multiplier (`Config.DoubleExperienceMultiplier`).
The pack is preserved whenever an EXP bonus is already active, matching ordinary
potions. Team Skill prize issuance of 720793/720981 is unchanged.

## Ownership, persistence and packets

Only a healthy canonical ordinary session may use its exact owned, unlocked,
non-suspicious inventory item outside trade/booth. Warehouse, equipment,
wardrobe, expired or substituted sources cannot be consumed. Binding and
zero-stack historical singletons are preserved. The existing 1009 action-4
frame is bounded to 32..128 total bytes.

Under the inventory owner lock, the transaction locks the character, checks
the saved timer preimage and complete bag UID set, consumes one source unit,
writes the timer and inserts a `revora_exp_pack_uses` receipt. Failed writes
roll back together and require reload before retrying. Direct `ItemHandler.UseItem`
uses the same service.

## Remaining work

Leftover TimeDrop hourglass use is repaired in [TIMEDROP_HOURGLASS.md](TIMEDROP_HOURGLASS.md).
Prizewell issuance stays closed.
Protection pill 3002559 is repaired in [EXP_PROTECTION.md](EXP_PROTECTION.md).
Magnolia issuance of these packs, 5x/immediate catalog interpretations,
genuine-client timer display and deployment remain open. The parent
[M6.56b inventory](COMPOUND_REWARD_INVENTORY.md) is not exhaustive.
