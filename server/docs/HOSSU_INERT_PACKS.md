# HonorShop inert Hossu packs 3006320/3006321

M6.56b10a, 2026-09-23. Server protection. Not deployed. Not a leftover UseItem
ownership. FancyHossuHairPack 3006319 is unchanged
([HOSSU_HAIR_PACKS.md](HOSSU_HAIR_PACKS.md)).

HonorShop UID **6000** stocks packs that ordinary `HandleBuyFromNPC` /
`HonorPurchases.Buy` can issue, but `ItemHandler.UseItem` has no case and no
leftover reward mapping.

| ID | Catalog | Honor cost | Shop recommend copy |
| ---: | --- | ---: | --- |
| 3006320 | LuxuryHossuHairPack | 2000 | random Hossu Hair 300/400/500 Max-HP |
| 3006321 | LegendaryHossuHairPack | 4000 | random Hossu Hair 400/500/800 Max-HP |

Older catalog/HonorShop siblings with the same names and the same missing
consumer:

| ID | Catalog | Honor cost | Shop recommend copy |
| ---: | --- | ---: | --- |
| 3006316 | FancyHossuHairPack | 8000 | same 200/300/400 copy as owned 3006319 |
| 3006317 | LuxuryHossuHairPack | 20000 | same 300/400/500 copy as 3006320 |
| 3006318 | LegendaryHossuHairPack | 30000 | same 400/500/800 copy as 3006321 |

## Installed identities

`HonorShop.ini` is the ordinary producer. `itemtype-new.txt` names each pack
and stores `[Hossu][MaxHP]GiftPack` plus Untradable HP-range copy. Catalog
`data[10]=9` marks Type Others (not dropable). `ItemMinIcon.Ani` has client
icons. `ConquerItemInformation.Parse` does not dispatch UseItem from those
fields. Server UseItem is the numeric `switch (item.ID)`.

No leftover C# maps these IDs onto hairs, HP bonuses, or 3006319's
`Random.Next(1,6)` table. Shop recommend lines and catalog descriptions are
client copy and are not adopted as a reward contract.

Installed hair IDs stop at bound 3006159–3006164 and unbound 3006165–3006170,
then `BlankBook` 3006171. There is no extra luxury/legendary hair set.

These IDs are absent from `emoneyshop.ini`, `emoneyshopV2.ini`, `Shop.txt`,
`Shop.dat`, exchange, StageGoal, `Npcs.cs` `Inventory.Add`, `DropItemTable`,
`MonsterTable`, `Questinfo.ini`, and `public.monsterinfos.ownitem`.

## Why no consumer

`GetPositionFromID` returns Inventory, so ordinary 1009/action-4 still reaches
`UseItem`. The switch has no case, so leftover use is a no-op and does not
consume the pack. `HossuHairPacks` Catalog remains `{3006319,0}` only.

Copying 3006319 leftover picks 1–5 / 3006165–3006169 onto these packs would
invent a mapping.

## Protection

`HonorPurchases.Buy` refuses **3006316, 3006317, 3006318, 3006320, 3006321**
after stock/catalog lookup and before Honor debit or insert, and sends
`This item cannot be used.` Already-held items stay. Owned 3006319 buy-to-use
stays.

Census is unchanged (164/256/55/43); these IDs are not UseItem cases.

## Remaining work

Other leftover packs, Host restart, native acceptance and deployment remain
open. Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
