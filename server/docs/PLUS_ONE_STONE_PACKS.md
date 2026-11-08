# Leftover +1 stone pack 723712

M6.56b10d, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover **+1StonePack 723712**
go through `PlusOneStonePacks`. The leftover case fails closed.

Mil.Ginseng Box **721330** stays leftover live-split-write. It has a leftover
consumer and no ordinary producer. MeteorTearPack **723711** is now owned by
[FixedOutputPacks](FIXED_OUTPUT_PACKS.md).

## Ordinary issuance

MillionaireLee NPC **11275** on Twin City 1002 option 3 is owned by
[MillionaireStonePacking](HIGHER_STONE_PACKS.md). It consumes exactly five
**730001** and inserts one **723712**. Any bound stone in that payment binds
the pack. All-unbound stones, including ordinary Plus 0 stones, still produce
an unbound pack. `public.npcs` has id 11275 map 1002.

HonorShop, EShop, Shop.txt, Shop.dat, exchange, StageGoal, DropItemTable and
MonsterTable do not stock 723712. Historical GM logs are not ordinary
producers.

721330 is absent from `itemtype-new.txt` and `ItemtypeSub.txt`. 723711 is
catalog MeteorTearPack. Neither has a C# `Add` producer.

## Leftover terms (723712)

Leftover required `Count <= 36`, then `Remove` the clicked pack and
`Add(730001, 1, 5)`: five **Stone** items at **Plus 1**. Owned capacity is
stack-aware: five rewards need five slots; a depleted pack releases one slot
(`Count + 4 <= 40`); a stacked remainder does not (`Count + 5 <= 40`).
Leftover `Count <= 36` for stacked packs is not adopted.

NPC issuance copies binding from the consumed payment: any bound stone
binds the pack. Owned rewards copy the pack Bound flag so a bound held pack
cannot unpack into unbound stones. All-unbound payments still yield unbound
packs and unbound Plus-1 stones.

A stacked pack with leftover `UseItem` set removes one stack unit. Source
consume, five stone inserts and receipt `revora_plus_one_stone_pack_uses`
commit together under `ItemSyncRoot`. Full-bag refusal uses leftover
`Constants.FullInventory`. Census after this batch is 161 live-split-write /
259 owned-fail-closed / 55 single-consume / 43 other. Intercepts 64. Leftover
matrix 259/6/0/2/33.

## Remaining work

721330 leftover three 1002050 remains unresolved. 723711 is owned separately.
Other leftover packs, Host restart, native acceptance and deployment remain
open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
