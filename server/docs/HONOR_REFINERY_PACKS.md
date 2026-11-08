# HonorShop leftover GetNextRefineryItem packs

M6.56b9w, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of thirty leftover refinery packs
stocked on HonorShop UID **6000** go through `HonorRefineryPacks`. Those
leftover cases fail closed.

## Ordinary issuance

`Server.cs` loads `HonorShop.ini`. `HandleBuyFromNPC` UID 6000 dispatches to
owned `HonorPurchases.Buy` before the generic map-NPC gate. Nearby ordinary
accounts obtain these packs with Honor Points. A hidden dialog is not required.

Owned IDs (HonorShop medium/superior stock): **723679, 723680, 723682, 723683,
723691, 723692, 724149, 724150, 724193, 724194, 724196, 724197, 724199, 724200,
724202, 724203, 724205, 724206, 724208, 724209, 724211, 724212, 725161, 725162,
725164, 725165, 725167, 725168, 725170, 725171**.

Unstocked primary siblings stay leftover live-split-write with no ordinary
producer ([REFINERY_SIBLING_AUDIT.md](REFINERY_SIBLING_AUDIT.md)). HonorShop
`GainRefineryItem` packs are owned by [GAIN_REFINERY_PACKS.md](GAIN_REFINERY_PACKS.md).

## Leftover terms (preserved)

One source unit is consumed. `GetNextRefineryItem` extras 0/1/2 and leftover
ID offsets/remaps stay: necklace 724348/724349 +=150, ring `>=724466` +=4.
The pack Bound flag copies onto the material. Stack remainders stay.

## Owned contract

Source consume, material insert and receipt `revora_honor_refinery_pack_uses`
commit together under `ItemSyncRoot`. Failed writes fence without live
inventory mutation. Direct daily P7 is unchanged.

## Remaining work

Other leftover packs, Host restart, native acceptance and deployment remain
open.
Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
