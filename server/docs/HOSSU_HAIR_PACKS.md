# HonorShop FancyHossuHairPack 3006319

M6.56b9z, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of HonorShop
`FancyHossuHairPack` **3006319** go through `HossuHairPacks`. The leftover
case fails closed.

## Ordinary issuance

`Server.cs` loads `HonorShop.ini`. `HandleBuyFromNPC` UID 6000 dispatches to
owned `HonorPurchases.Buy`. Nearby ordinary accounts obtain **3006319** for
**800** Honor Points. A hidden dialog is not required.

`MonsterTable` DrunkWatchman and SnowBansheeSoul/PurpleBanshee leftover
`Next(1, 9)` case 9 names 3006319. `Next(1, 9)` yields 1–8, so those
branches are not ordinary issuance.

HonorShop also stocks inert `LuxuryHossuHairPack` / `LegendaryHossuHairPack`
siblings. They are not folded into this family
([HOSSU_INERT_PACKS.md](HOSSU_INERT_PACKS.md)).

## Leftover terms (preserved)

`Inventory.Count <= 38` is required before consume. Count 39 and 40 refuse
silently without debit. One source unit is consumed. `Random.Next(1, 6)`
yields 1–5 and pays unbound plus-0:

| Pick | ID | Catalog |
| ---: | ---: | --- |
| 1 | 3006165 | NormalHossuHair |
| 2 | 3006166 | RefinedHossuHair |
| 3 | 3006167 | UniqueHossuHair |
| 4 | 3006168 | EliteHossuHair |
| 5 | 3006169 | SuperHossuHair |

Case 6 / SacredHossuHair **3006170** is unreachable. A bound pack still
issues unbound hair. Stack remainders stay. Leftover Center/Yellow
congratulations text stays.

## Owned contract

Source consume, unbound hair insert and receipt `revora_hossu_hair_pack_uses`
commit together under `ItemSyncRoot`. Failed writes fence without live
inventory mutation. Direct daily P7 is unchanged.

## Remaining work

Other leftover packs, Host restart, native acceptance and deployment remain
open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
