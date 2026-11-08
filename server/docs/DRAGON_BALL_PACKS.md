# Leftover DragonBall packs 720028 / 720884

M6.56b9j, 2026-09-22. Server implementation; not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover **DBScroll 720028**
and **DragonBallPack(5) 720884** go through `DragonBallPacks`. Both legacy
cases fail closed. Neither pack sends a success message. Capacity refusal
sends `Constants.FullInventory` (`Your inventory is Full.`).

Leftover `Inventory.Add(1088000, 0, count)` creates one bag slot per unit
while `Count <= 39`. DragonBall catalog stack size is 0, so those units do
not stack. The owned service inserts the same number of plus-0 stack-1
`1088000` items. DBScroll 720028 copies pack binding; 720884 rewards remain
unbound.

| Pack | Catalog | Outputs | Legacy count gate | Atomic capacity |
| ---: | --- | --- | --- | --- |
| 720028 | DBScroll, “Equivalent of 10 DragonBalls” | 10× plus-0 1088000, binding copied from the pack | `Count <= 31` | all ten outputs must fit; source slot is credited only when deleted |
| 720884 | DragonBallPack(5), “get 5 Dragon Balls” | 5× unbound plus-0 1088000 | `Count <= 30`, including refusal at 31 | the leftover count-30 gate is preserved even though five outputs would physically fit |

720028’s leftover `Count <= 31` precheck could consume a remaining stack and
then grant a partial ten through `Inventory.Add`. The owned transaction keeps
that outer precheck and additionally requires room for every concrete output
when the source slot is kept. That repairs the live partial-grant defect; it
does not rebalance rewards.

Receipt table `revora_dragon_ball_pack_uses`. Direct daily P7 is unchanged.

## Ordinary issuance

**720028** is packed by MillionaireLee NPC **11275** on Twin City map 1002
(304,433). `TwinCityPlaza.PackingClerkId` parks that desk; it is not a closed
desk. Option 2 consumes exactly ten `1088000` in UID order and inserts one
720028. Any bound ball in that payment binds the scroll. Unpack copies that
binding onto the ten balls. 720884 stays unbound. Chi clerks
4712/9894 consume a DBScroll for 8000 Chi and are sinks, not producers.

**720884** is issued by MammonEnvoy NPC **9623** on Prize Center map **1068**
(50,50), installed in `public.npcs`. Teleporter **9619** option “Change
Center.” teleports ordinary players to 1068. Option 7 consumes CopperCoin
**711611** and adds 720884 plus 728900. `CoinsStealer` is loaded in
`monsterinfos` / `Monsters.txt` and spawned; its kill path can award 711611.

Neither ID is listed in Shop.dat, Shop.txt, emoney shops, HonorShop, exchange,
StageGoal or `DropItemTable`.

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, catalog-only Surprise box 722178,
nested/gear leftover 721222–721245, classified silver leftovers, leftover
medicine bundles 720010–720017, other leftover packs, Host restart, native
acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
