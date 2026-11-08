# Leftover silver pouches 3006594 / 723721 / 723723

M6.56b9g, 2026-09-22. Server implementation; not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover pouches **3006594**,
**723721** and **723723** go through `SilverPouches`. Those three legacy cases
fail closed. Silver uses `SilverBalance`. There is no leftover success text.
Overflow above `Entity.MaximumSilver` refuses without source loss.

Executable leftover amounts are preserved even where catalog copy differs.

| ID | Catalog | Silver | Ordinary producer |
| ---: | --- | ---: | --- |
| 3006594 | MagicSpar(200Thousand) | 200000 | ChiJiang clerk SOBNPC 900043 on Twin City 1002 (396,348); pack exchange |
| 723721 | Class9MoneyBag | 100000000 | live `MonsterTable.Drop` SuperBoss floor drop; FuriousBear is spawned |
| 723723 | TopMoneyBag (catalog names 500,000,000) | 200000000000 | ShelbyRewards NPC 30000 on market 1036 (240,249) silver wrap |

## Classified leftover (not owned)

| ID | Why |
| ---: | --- |
| 3001266 | FirstCredit `Data` path requires GM `primercredito` points; leftover pays 200,000,000 gold (catalog names 100,000) |
| 723722 | no shop/exchange/StageGoal producer; `task_reward_type.ini` listing is not issuance; leftover pays 300,000,000 |
| 3007365–3007368 | leftover pays 250e9 / 2e9 / 50e9 / 500e9 silver (catalog copy differs, including 3007368 “300 CPs”). `SlayerPointshop.ini` stocks them for dedicated `REVORAPointhop` UID 6001, which bypasses the generic map-NPC gate. Installed startup comments out `Database.RevoraPointhop.Load()`; `Shop` stays null. The buy branch now fail-closes when uninitialized, so a forged UID-6001 packet neither throws nor issues/debits. NPC/SOBNPC 6001 absence is secondary. |

Fixture injection of classified IDs is consumer coverage, not issuance.
Receipt table `revora_silver_pouch_uses`. Direct daily P7 is unchanged.

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, catalog-only Surprise box 722178,
nested/gear leftover 721222–721245, classified silver leftovers, other leftover
packs, Host restart, native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
