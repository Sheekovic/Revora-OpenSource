# EXPCarePill scrap combine

M6.56b5, 2026-09-22. Server implementation; not deployed.

Ordinary players can combine ten EXPCarePillScrap **3002560** through the
existing item-use path. One transaction consumes exactly ten scraps from the
clicked stack, inserts one bound permanent EXPCarePill **3002559**, and adds
exactly 3,000 Revora Points.

## Evidence and policy

The live `ItemHandler` branch subtracted ten from the stack, mutated
`RevoraPoint`, added a bound pill, then removed the whole source item. A stack
larger than ten therefore lost its remainder. This repair keeps the executable
10 / 1 bound pill / 3,000 RVP terms and keeps the remainder on the source.

Direct daily P7 is unchanged. Catalog identities are not required; 3002559 /
3002560 are custom Revora items. Leftover TimeDrop hourglass use is repaired in
[TIMEDROP_HOURGLASS.md](TIMEDROP_HOURGLASS.md).

## Ownership, persistence and packets

Only a healthy canonical ordinary session may use its exact owned, unlocked,
non-suspicious inventory scrap outside trade/booth. Warehouse, equipment,
wardrobe, expired or substituted sources cannot be consumed. Binding and
zero-stack historical singletons are preserved; stacks below ten award nothing.

A remaining source after ten are consumed needs one free bag slot for the pill.
An exact ten-stack can combine in a full bag because the source is deleted as
the pill is inserted. Revora Point overflow refuses the combine without
consuming scraps.

Under the inventory owner lock, the transaction locks the character, checks the
complete bag UID set and the durable `entities.revorapoint` preimage, debits
ten from the clicked source, inserts the bound pill, adds 3,000 RVP and writes
a `revora_exp_care_combines` receipt. Failed writes roll back together and
require reload. Direct `ItemHandler.UseItem` uses the same service. The legacy
3002560 case is fail-closed.

## Remaining work

Magnolia issuance, genuine-client display and deployment remain open. The parent
[M6.56b inventory](COMPOUND_REWARD_INVENTORY.md) is not exhaustive.
