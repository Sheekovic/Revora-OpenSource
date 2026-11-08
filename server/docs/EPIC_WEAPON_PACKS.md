# Leftover fixed epic-weapon UseItem cases

M6.56b10b, 2026-09-23. Reconciliation only; not owned; not deployed.
Adjacent leftover cases are not one ownership contract.

Ordinary 1009/action-4 still reaches `UseItem` (`GetPositionFromID` Inventory).
Direct `UseItem` is the second path. Fixture injection is consumer coverage,
not issuance. Leftover bodies stay **live-split-write**: `Add(..., 0, 1)`
unbound plus-0, then `ItemUse.Remove` of the clicked item, then a Cyan Center
world announcement. There is no inventory-count gate. `Inventory.Add` refuses
when `Count > 39`; leftover still Removes, so a full bag can consume without
a reward. Census stays 164/256/55/43.

| Source | Catalog | Leftover reward | Dual-role / mapping |
| ---: | --- | --- | --- |
| 3003340 | SolarBlade (`itemtype-new.txt`, TrojanEpicQuest) | 614439 FangCrossSaber plus-0 | TrojanEpicStore loan, GhostReaver drop, NPC 122861 five-item sink |
| 3006016 | DivinePanacea (`itemtype-new.txt`, TaoistEpicQuest) | 620439 ImperialBacksword plus-0 | NPC 48554 issues; NPC 48654 sinks to **619439** |
| 3006226 | SuperHossuToken (`itemtype-new.txt`) | 619439 FierceImmortal plus-0 | no C# `Add`; catalog swap copy names Divine Commander |
| 3006700 | EliteMiracleStone (`itemtype-new.txt`, QuestItem silver-gamble copy) | 622439 AlmightyEmperor`sPillar plus-0 | catalog copy is not leftover C# |
| 3307451 | OracleoftheSea (`ItemtypeSub.txt`) | 670439 OceanDominator plus 671439 MightyRapierofViolence plus-0 | two outputs; no C# `Add` |

Leftover announcement nicknames (ColmilloCruzable, TizonaImperial,
HossuDeUniverso, SuperiorPilarTodoPoderoso, MightyRapierofViolence +
OceanDominator) are not catalog names and are not adopted as extra IDs.

## Why no owned consumer

**3003340** is Solar Blade for owned `TrojanEpicStore` (`SolarBlade=3003340`,
reward 614439 +12 / Bless 7 / two empty sockets). Leftover `MonsterTable`
GhostReaver can floor-drop 3003340. Leftover NPC **122861** removes five
Solar Blades for one plus-0 614439. Leftover UseItem converts **one** blade
into plus-0 614439. Owning that UseItem would consume borrowed quest blades.
Those semantics stay separate.

**3006016** leftover UseItem pays 620439. Leftover NPC **48654** option 1
removes 3006016 and pays **619439**. Leftover NPC **48554** option 1 issues
3006016 from 3006014. The leftover UseItem table contradicts the NPC sink.
Do not pick one mapping.

**3006226** and **3006700** have leftover UseItem tables and catalog
identities, but no `Inventory.Add` producer and no HonorShop / EShop /
Shop.txt / Shop.dat / exchange / StageGoal / DropItemTable stock.
`Booths.Load()` is commented out in `Server.cs`. Catalog copy for 3006226
names an NPC swap that has no C# handler. Catalog copy for 3006700 is a
silver gamble, not the leftover monk-epic table. Those copies are not
adopted.

**3307451** lives in `ItemtypeSub.txt` (loaded through
`ItemBaseInfosPath2`). Leftover pays two plus-0 pirate weapons. No ordinary
producer. Two leftover `Add`s then Remove: at Count 39 the second Add
refuses, then the source is still Removed.

Seller stall **62148** returns `This stall is closed.` before its leftover
5,000,000-CP epic-weapon options. Closed-stall copy is not ordinary
issuance.

## Remaining work

Other leftover packs, Host restart, native acceptance and deployment remain
open. Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.


2026-09-27 evidence update: the [Taoist restoration contract](TAOIST_EPIC_RESTORATION.md)
now resolves the contradictory material purpose using the official 2015 guide
and independent installed identities. Panacea converts a right-hand backsword;
Fish is exchange material. The warning above describes the earlier evidence gap,
not a prohibition on implementing the now-supported conversion. The restored chain now has normal-startup private earning/restart evidence;
native acceptance and deployment remain pending. The legacy mappings above are
historical, not the active restored contract.

The [Pirate restoration contract](PIRATE_EPIC_RESTORATION.md) independently
identifies Heart3307449 as the single-weapon conversion material. Oracle3307451
is a distinct pack; its unsafe consumer and missing issuer remain unresolved.
