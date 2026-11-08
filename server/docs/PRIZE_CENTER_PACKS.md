# Leftover Prize Center coin-reward packs

M6.56b9k, 2026-09-23. Server implementation; not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover **WeaponAccessoryPack
720836**, **TinyClass2MoneyPouch 728898**, **TinyHorseRacingPointsBag 728899**,
**TinyClass1MoneyPouch 728900** and **AccessoryScrapPack 3004257** go through
`PrizeCenterPacks`. All five legacy cases fail closed.

## Stack contract

Leftover combines did `item.StackSize -= 8/6` and then
`Inventory.Remove(item)`. `UseItem` is true for the whole handler. After an
exact 8/6 decrement the stack is 0, so Remove deletes that UID and the
advertised count is consumed. When the clicked stack is larger, remaining
`== 1` deletes the leftover unit with the UID, and remaining `> 1` takes one
extra unit through `Remove(ID, 1)`. Catalog copy advertises an 8-for-1 or
6-for-3K exchange. The owned service consumes **exactly** 8, 8 or 6 from the
clicked stack and keeps any remainder. Direct daily P7 is unchanged.

Random packs consume one unit. Leftover `Remove` of a stack `> 1` already
took one unit because `UseItem` is true; stack 0/1 deleted the UID. The owned
service matches that one-unit consume.

## Consumers

| ID | Catalog | Leftover effect | Owned contract |
| ---: | --- | --- | --- |
| 720836 | WeaponAccessoryPack | `Random.Next(1,78)` accessory, then Remove. No capacity text. Case 78 is unreachable. | same 77 accessories, unbound plus-0, silent; FullInventory if the accessory would not fit; roll 78/missing catalog refuses without consume |
| 3004257 | AccessoryScrapPack, “one of 6” | Count `> 38` → `You need atleast 2 open spots!`; `Random.Next(1,6)` then Remove. Case 6 (360154) is unreachable. | same five scraps, same refusal text/gate, roll 6 refuses without consume |
| 728898 | TinyClass2MoneyPouch, exchange 8 | 8 → Class2MoneyBag 723714, `You have recieved Class2MoneyBag` | exact 8, keep remainder, same misspelling |
| 728900 | TinyClass1MoneyPouch, exchange 8 | 8 → Class1MoneyBag 723713, `You have recieved Class1MoneyBag` | exact 8, keep remainder, same misspelling |
| 728899 | TinyHorseRacingPointsBag, exchange 6 | 6 → +3000 RVP, `You have received 3,000 Revora Points.` | exact 6, keep remainder, entities.revorapoint CAS, overflow refuses |

720836 reachable IDs are leftover cases 1–77 (350001–380014 with the leftover
gaps at 350003/350013 and the 370011/380011 jumps). 3004257 reachable IDs are
350081, 350082, 360151, 360152, 360153. Injected rolls never insert item 0.

Undersized combines still present the leftover MessageBox copy (null
callbacks). Accessory comments that mention 10080-minute duration are not
applied by leftover `Add(id,0,1)` and are not invented here.

## Ordinary issuance

MammonEnvoy NPC **9623** on Prize Center map **1068** is installed in
`public.npcs` and is not a Twin City closed desk. Teleporter **9619** option 1
`Teleport(1068)` is installed. CoinsStealer is in `monsterinfos` and spawned;
its kill path can award GoldCoin **711609**, SilverCoin **711610** and
CopperCoin **711611**.

- Gold option 5: 711609 → 728898 + 3004257
- Silver option 6: 711610 → 720836 + 728899
- Copper option 7: 711611 → 720884 + 728900

720884 remains the owned DragonBall pack. None of the five IDs is listed in
Shop.dat, Shop.txt, emoney, HonorShop, exchange, StageGoal or DropItemTable.
DrunkWatchman / SnowBanshee leftover drop tables can also name 3004257; they
are additional code paths, not a substitute for the Prize Center route.

Receipt table `revora_prize_center_pack_uses`.

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, catalog-only Surprise box 722178,
nested/gear leftover 721222–721245, classified silver leftovers, leftover
medicine bundles 720010–720017, other leftover packs, Host restart, native
acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
