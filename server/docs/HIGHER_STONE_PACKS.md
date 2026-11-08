# Leftover +2 and +3 stone packs 729022 / 729023

M6.56b10e, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover **+2StonePack 729022**
and **+3StonePack 729023** go through `HigherStonePacks`. The leftover cases
fail closed.

## Ordinary issuance

MillionaireLee NPC **11275** on Twin City 1002 options 4 and 5 are owned by
`MillionaireStonePacking`, together with option 3 for **723712**. Option 4
consumes exactly ten **730002** and inserts **729022**. Option 5 consumes
exactly ten **730003** and inserts **729023**. `public.npcs` has id 11275 map
1002, and the Twin City packing clerk is not a closed desk.

Payment is UID order and exact. A fully consumed stack releases its slot; a
remainder keeps the slot. Stone Plus is not checked, because these IDs already
name the tier and ordinary stones are often Plus 0. Any bound unit in the
payment binds the pack. All-unbound payment still inserts an unbound Plus-0
pack. Locked, suspicious, warehouse, equipped, wardrobe, and expired stones
are not material.

The exchange, pack insert, and receipt `revora_millionaire_stone_packs` commit
together under `ItemSyncRoot`. The same contract owns option 1 (ten 1088001
into 720027), option 2 (ten 1088000 into 720028), and option 7 (ten 723700
into 720757). Option 6 is menu text only and has no handler. It is not an
implemented exchange.

HonorShop, EShop, Shop.txt, Shop.dat, exchange, StageGoal, DropItemTable and
MonsterTable do not stock 729022 or 729023. A 2018 vote log names 729023 and
is not an ordinary producer. No other C# `Add` exists.

Installed catalog identities are present for both packs and for Stone
730002 / 730003. Pack display names in `itemtype-new.txt` are the same garbled
bytes as 723712.

## Leftover terms

Leftover required `Count <= 30`, then `Remove` the clicked pack and
`Add(730002, 2, 10)` or `Add(730003, 3, 10)`.

Owned capacity is stack-aware and does not keep the shared `Count <= 30` gate:

- ten rewards need ten slots
- a depleted pack (`StackSize <= 1`, including a zero-stack singleton) releases
  its slot, so `Count + 9 <= 40` (Count 31 pays, Count 32 refuses)
- a stacked remainder does not release its slot, so `Count + 10 <= 40`
  (Count 30 pays, Count 31 refuses)

Leftover `Count <= 30` would refuse a depleted pack at Count 31 even though
the released slot makes the ten stones fit. That refusal is not adopted.
Full-bag refusal still uses leftover `Constants.FullInventory`.

NPC issuance is unbound. Owned rewards copy the pack Bound flag, so a bound
held pack cannot unpack into unbound stones. Unbound NPC packs still pay
unbound Plus-2 or Plus-3 stones.

Source consume, ten plus-aware stone inserts and receipt
`revora_higher_stone_pack_uses` commit together under `ItemSyncRoot`. A
failure after commit, before the live bag is updated, fences for reload.
Reload then shows the committed stones.

## Remaining work

Gem option 6 stays menu-only, with no handler. Meteor scroll 720027,
DBScroll 720028, and EXP Ball pack 720757 unpacking preserves pack binding.
DragonBallPack(5) 720884 and MeteorBox 723268 stay unbound.
721330 stays unresolved and absent from the installed catalogs. MeteorTearPack
723711 is owned by [FixedOutputPacks](FIXED_OUTPUT_PACKS.md). Other leftover
packs, Host restart, native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
