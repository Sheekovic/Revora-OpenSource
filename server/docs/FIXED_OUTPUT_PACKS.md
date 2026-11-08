# EXP Ball pack, MeteorBox and MeteorTearPack

M6.56b9b owned 720757 and 723268. M6.56b10i adds MeteorTearPack 723711.
Server implementation; not deployed.

Ordinary 1009/action-4 and direct `UseItem` of **720757**, **723268** and
**723711** go through `FixedOutputPacks`. All three legacy cases are
fail-closed. None of the packs sends a success message. Capacity refusal
sends `Constants.FullInventory`.

| Pack | Outputs | Legacy count gate | Atomic capacity |
| --- | --- | --- | --- |
| 720757 | 10× plus-0 723700 EXP Ball, binding copied from the pack | `Count <= 39` kept as outer eligibility | all ten outputs must fit; source slot is credited only when deleted |
| 723268 | 5× unbound plus-0 1088001 Meteor | `Count <= 30`, including refusal at 31 | the unusual count-30 gate is preserved even though five outputs would physically fit |
| 723711 | 5× plus-0 1088002 MeteorTear, binding copied from the pack | `Count <= 36` for every stack | zero or single source fits through Count 36; a stacked remainder fits through Count 35 |

723711's old gate did not credit a released slot differently from a stacked
remainder. The owned check does: five rewards need five slots, and only a
deleted zero/single source releases one. A bound pack cannot unpack into
unbound Meteor Tears. An unbound pack still unpacks into unbound Meteor Tears.
723268 remains unbound even when the box itself is bound. 720757 binding is
unchanged.

`revora_fixed_output_pack_uses` already existed with
`CHECK(item_type IN (720757,723268))`. `Initialize` drops that constraint if
present and adds `CHECK(item_type IN (720757,723268,723711))`. Historical
receipts stay. Repeated `Initialize` is safe. There is no new shop stock,
fee or issuer for 723711. Mil.Ginseng Box **721330** is still absent from
`itemtype-new.txt` and `ItemtypeSub.txt`; its leftover three-1002050 branch
stays live-split-write. Gem option 6 stays menu-only.

720757’s old `Count <= 39` precheck could consume the source after
`Inventory.Add` returned false part-way through ten inserts. The owned
transaction keeps that outer precheck and additionally requires room for
every concrete output. That repairs the live partial-grant defect; it does
not rebalance rewards.

Receipt table `revora_fixed_output_pack_uses`. 723700 remains
ExperienceConsumables. 1088001 remains the Meteor catalog identity used by
NPC upgrade/crafting sinks (3-Meteor and 10-Meteor NPC spends, nested
upgrade `case 1088001`); it has no ordinary UseItem consumable. 1088002
remains MeteorTear and is still the equipment-upgrade material at
`case 1088002`. Opening 723711 does not consume that tear.

## Remaining work

Catalog Chi pills 3000051/729476–729479 are owned by ChiPointItems. Catalog-only Surprise box 722178, nested gear packs and other leftover
split-writes remain open. Native acceptance and deployment remain open.
Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
