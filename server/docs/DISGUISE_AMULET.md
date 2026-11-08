# Leftover DisguiseAmulet 723724

M6.56b9v, 2026-09-23. Reconciliation only; not owned; not deployed.

Installed catalog identity is `DisguiseAmulet` (`Right~click~on~it~to~disguise~you~into~a~monster.`).
Ordinary 1009/action-4 still reaches `UseItem`. Direct `UseItem` is the
second path. The leftover case stays **live-split-write**. Fixture injection
is consumer coverage, not issuance.

Leftover use consumes one unit, picks `DataHolder.Disguises`
(111, 224, 117, 152, 113, 833, 116, 245, 223, 112, 222, 114, 221, 115, 220)
with `Kernel.Random.Next(Length)`, sets `TransformationTime=110`,
`TransformationStamp=Now`, `TransformationMaxHP=3000`, sends SpellUse 1360
level 4, and scales hitpoints onto 3000. A second use while already
transformed restores `Hitpoints=MaxHitpoints` then applies a new disguise.
`Thread` expires the form with leftover `Untransform()` after 110 seconds.
`TransformationTime` / `TransformationID` are not entity columns, so reload
drops the form while the item delete can persist.

## Issuance

No `Inventory.Add(723724)` producer. The ID is absent from `emoneyshop.ini`,
`emoneyshopV2.ini`, `Shop.txt`, `Shop.dat`, `SlayerPointshop.ini`,
`RacePointShop.ini`, exchange, StageGoal, `DropItemTable`, and
`MonsterTable`. `QuestInfo.Load` does not award Prize lines.

Collector Zhao leftover cases **2070** and **3952** option 3 still
`Inventory.Remove(723724, 1)` and pay leftover `Config.CollectorZhaoCp` (10)
plus `CollectorZhaoRevoraPoints` (1). That is a sink, not ordinary issuance.

GM `@itemid 723724` logs are not an ordinary producer.

## Remaining work

Catalog-only/other leftover packs, leftover Collector Zhao CP/RVP sinks,
Host restart, native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
