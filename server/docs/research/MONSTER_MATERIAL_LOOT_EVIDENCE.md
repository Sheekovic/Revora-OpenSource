# Meteor and DragonBall monster source audit

Read-only installed-catalog query on 2026-09-26 00:52:35 UTC found no monsterinfos
row with ownitem 1088001 (Meteor). The only row with ownitem 1088000 (DragonBall)
was family 4211, HollowBeast, level 133, ownitemrate 500. Its left join to
monsterspawns returned no placement. This does not rule out dynamically spawned
actors; it establishes that the installed legacy own-item catalog currently
does not provide a Meteor source.

Reproduction source: tools/MonsterMaterialCatalogEvidence.cs. It uses existing
local protected runtime settings without printing them, runs a read-only
transaction against public.monsterinfos/public.monsterspawns, selects only
material IDs 1088000/1088001 with parameters, and emits public game-catalog
fields plus an observation timestamp. It does not change schema or rows.

Relevant production routes:

- Database/MonsterTable.cs's OwnItemID drop branch reads these catalog values.
  It creates an ordinary floor item and currently relies on legacy inventory
  pickup. Network/GamePackets/ItemHandler.cs removes that floor item before
  confirming the inventory save. This route needs provenance and an atomic
  claim before reliable achievement evidence can be added.
- SnowBansheeRewards has a durable boss-origin DragonBall floor entitlement.
  This is the tested 10523 route documented in MONSTER_DRAGONBALL_ACHIEVEMENT.md.
- AncientDevilBattles draws either one DragonBall or six Meteors on the final
  boss victory, but Claim delivers them through the seal reward service, not
  a ground pickup. Do not silently equate this quest reward with 10524's
  monster-loot instruction.
- ExpeditionField adds selected materials to a temporary extraction satchel.
  These can originate from both monster encounters and guarded caches. The
  item ID alone cannot prove a Meteor was looted from a monster.

10524 remains missing. Next work must either establish an existing genuine
monster-origin Meteor collection route or design an explicitly documented
Revora source, then implement atomic item/claim/achievement writes, provenance,
ordinary eligibility, retry and reload tests. No new drop odds or sources were
introduced by this audit. Source-producer count remains 280/337.
