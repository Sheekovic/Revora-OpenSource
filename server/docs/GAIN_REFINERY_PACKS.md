# HonorShop leftover GainRefineryItem packs

M6.56b9x, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of thirty leftover
`GainRefineryItem` packs stocked on HonorShop UID **6000** go through
`GainRefineryPacks`. Those leftover cases fail closed.

## Ordinary issuance

`Server.cs` loads `HonorShop.ini`. `HandleBuyFromNPC` UID 6000 dispatches to
owned `HonorPurchases.Buy` before the generic map-NPC gate. Nearby ordinary
accounts obtain these packs with Honor Points. A hidden dialog is not required.
Gold shops, EShops, exchange and StageGoal do not stock these IDs.

Owned IDs (HonorShop medium/superior stock): **724131, 724132, 724134, 724135,
724152, 724153, 724155, 724156, 724158, 724159, 724161, 724162, 724164, 724165,
724167, 724168, 724170, 724171, 724173, 724174, 724176, 724177, 724179, 724180,
724182, 724183, 724185, 724186, 724191, 724192**.

Unstocked `GainRefineryItem` siblings and empty-box IDs stay leftover with no
ordinary producer ([REFINERY_SIBLING_AUDIT.md](REFINERY_SIBLING_AUDIT.md)).

## Leftover terms (preserved)

One source unit is consumed. `Kernel.DatabaseRefineryBoxes` supplies Type,
Position and Untradable. Matching `Kernel.DatabaseRefinery` rows are collected
in load order. Leftover `Random.Next(1, Possible.Count)` skips index 0. The
issued material is unbound even when the pack is bound; leftover does not copy
Bound and does not set Bound from Untradable. Stack remainders stay. Empty-box
IDs do not consume. `Refinery.LoadItems` runs after `Thread.Init`, so box
lookup is at use time.

## Owned contract

Source consume, unbound material insert and receipt
`revora_gain_refinery_pack_uses` commit together under `ItemSyncRoot`. Failed
writes fence without live inventory mutation. Direct daily P7 is unchanged.

## Remaining work

Other leftover packs, Host restart, native acceptance and deployment remain
open. Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
