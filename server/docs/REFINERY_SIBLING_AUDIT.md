# Remaining refinery-pack siblings (M6.56b9y)

M6.56b9y, 2026-09-23. Reconciliation only; not owned; not deployed.

Installed catalog identities exist. Ordinary 1009/action-4 still reaches
`UseItem`. Direct `UseItem` is the second path. Fixture injection is consumer
coverage, not issuance. None of these IDs is ordinarily purchasable or dropped.

## Families

GetNextRefineryItem primaries stay **live-split-write**: **723678, 723681,
723690, 724148, 724195, 724198, 724201, 724204, 724207, 724210, 724215,
725160, 725163, 725166, 725169**. Leftover extra 0/1/2, family offsets and
necklace/ring remaps stay. The pack Bound flag copies onto the material.

LegendRefineryPack **723693** stays **live-split-write**. Leftover
`Kernel.Random.Next(1, 97)` yields 1–96. Case 97 and the default 724445 are
unreachable. Leftover adds one unbound plus-0 accessory, then
`ItemUse.Delete`s the pack. There is no inventory-count gate. Installed
catalog is missing 724378, 724379 and 724395–724399, so those leftover rolls
consume the pack without inserting a material.

GainRefineryItem primaries stay **other**: **724130, 724133, 724151, 724154,
724157, 724160, 724163, 724166, 724169, 724172, 724175, 724178, 724181,
724184, 724190**. Leftover `DatabaseRefineryBoxes` mapping and
`Random.Next(1, count)` skip of index 0 stay. Issued material is unbound.

Empty-box GainRefineryItem IDs stay **other**: **725055–725058**. Installed
`refineryboxes` rows exist with `possible=0`, so leftover does not consume.

## Issuance

HonorShop UID **6000** does not stock any of these IDs. Ordinary
`HandleBuyFromNPC` / `HonorPurchases.Buy` therefore issues nothing and does
not debit. They are absent from `emoneyshop.ini`, `emoneyshopV2.ini`,
`Shop.txt`, `Shop.dat`, exchange, StageGoal, `Npcs.cs` `Inventory.Add`,
`DropItemTable`, `MonsterTable`, and `public.monsterinfos.ownitem`.

`Questinfo.ini` Prize copy names 724175 beside owned 724167/724138.
`QuestInfo.Load` stores only `MissionId` and `TypeId` and does not award
Prize lines.

## Next leftover family

HonorShop FancyHossuHairPack **3006319** is owned by
[HOSSU_HAIR_PACKS.md](HOSSU_HAIR_PACKS.md). HonorShop inert Hossu packs are
fenced at Honor buy ([HOSSU_INERT_PACKS.md](HOSSU_INERT_PACKS.md)). Native
acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
