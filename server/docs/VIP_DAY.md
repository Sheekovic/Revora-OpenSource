# Leftover VIP-day tokens 3004121–3004124 / 3000794

M6.56b9u, 2026-09-23. Reconciliation only; not owned; not deployed.

Installed catalog identities exist. Ordinary 1009/action-4 still reaches
`UseItem`. Direct `UseItem` is the second path. Leftover cases stay
**live-split-write**. Fixture injection is consumer coverage, not issuance.

| ID | Catalog | Leftover use |
| ---: | --- | --- |
| 3004121 | VIP4Token | VIP 5 only: consume, `VIPLevel=6`, `VIPLevelDays=6`, `VIPDays=2` |
| 3004122 | VIP5Token | VIP 5 and `VIPDays<=2`: consume, set 5 days; `VIPDays>2` refuses stacking |
| 3004123 | VIP6Token | VIP 5 and `VIPDays<=5`: consume, set 15 days; `VIPDays>5` refuses stacking |
| 3004124 | VIP7Token | no VIP gate: consume, `VIPLevel=7`, `VIPLevelDays=7`, `VIPDays+=30` |
| 3000794 | VIP7FreedomBox (catalog 7,999 CP Freedom Suit copy) | consume, `VIPLevel=7`, `VIPLevelDays=7`, `VIPDays+=1` Christmas text |

3004122 leftover, after a successful 5-day grant, still evaluates the
`VIPDays>2` branch and also sends the cannot-stack `MessageBox`. Live fields
keep the grant. Catalog copy for 3000794 is not adopted.

`VipState` already CAS-saves `entities.viplevel/vipdays/vipleveldays` on
`SaveEntity`. Leftover `UseItem` mutates live VIP fields and leftover
autosave may persist them separately from the item delete. There is no owned
receipt or single item-use transaction. Login can also decrement `VIPDays`
when `LastResetTime` is a day old.

## Issuance

`SlayerPointshop.ini` (also copied as `RacePointShop.ini`) stocks
3004121/3004122/3004123 at 20,000 / 60,000 / 150,000. Dedicated buy UID
**6001** (`REVORAPointhop`) runs before the generic map-NPC gate and
**returns immediately** when `Shop` is null. Installed `Server.cs` comments
out the only `RevoraPointhop.Load()` call. `Thread.cs` never loads it. The
installed database has no `public.npcs` / `sobnpcs` row for 6001. An ordinary
`HandleBuyFromNPC` for UID 6001 therefore issues nothing and does not debit.

3004124 and 3000794 are absent from that shop file. None of the five IDs
appear in `emoneyshop.ini`, `emoneyshopV2.ini`, `Shop.txt`, `Shop.dat`,
exchange, StageGoal, `Npcs.cs` `Inventory.Add`, `DropItemTable`, or
`MonsterTable`. `task_reward_type.ini` lists 3000794 as client prize copy.
`QuestInfo.Load` does not award Prize lines. `Booths.txt` is a player booth
dump.

A leftover Twin City RVP dialog still writes `VIPDays` directly (2/5/15 days)
and disconnects. That is a separate leftover split-write of VIP fields, not
item issuance, and stays open.

## Remaining work

DisguiseAmulet 723724 is catalog + leftover consumer
([DISGUISE_AMULET.md](DISGUISE_AMULET.md)). Catalog-only/other leftover packs, Host restart,
native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
