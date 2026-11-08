# Leftover AncientStone / HolyStone combine 3004246 → 720121

M6.56b9n, 2026-09-23. Server implementation; not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover **AncientStone 3004246**
go through `AncientStoneCombine`. The legacy case fails closed.

## Leftover terms (preserved)

Catalog identity is `AncientStone` with copy `Collect~200~to~get~a~P7`. Leftover
executable required **20 total** bag units via `Contains(3004246, 20)` and
`Count <= 31`, then `Add(720121, 0, 1)` and `Remove(3004246, 20)`. Refusal text
is `You do not have 20 pieces, or you lack 5 inventory spaces.` There is no
leftover success text. The 200-unit / P7 catalog wording and the “5 spaces”
copy are not adopted as gates.

A stack of 0 counts as one leftover unit. Output is one permanent unbound
plus-0 stack-1 catalog-durability EasterGoldPack **720121** regardless of source
binding.

Owned service consumes **exactly 20** units. If any one stack is larger than 1
and at least 20, leftover `Remove` takes the whole 20 from that first stack and
leaves earlier smaller stacks. Otherwise units are taken in `Objects` order and
the last touched stack keeps its remainder. Leftover whole-object over-delete of
undersized stacks and the extra `UseItem` `Remove(ID,1)` path are not replayed.

Receipt table `revora_ancient_stone_combines`. Direct daily P7 is unchanged.

## Ordinary issuance

SuperBoss `MonsterTable.Drop` for FuriousBear / FatDemon / ChaosRipper /
DragonKing / NemesisTyrant / GoldOctopus / IceQueen / IceKing / ThunderDragon
can floor-drop 3004246 on VIP 6 (10 rolls) and VIP 7 (15 rolls) via reachable
`Random.Next(1, 17)` cases 5 and 10. FuriousBear is spawned. NPC **900031**
can summon FuriousBear in the xx:56 window. Floor pickup is ordinary.

GM event chat `santo` can also issue 3004246. No gold, emoney, exchange,
StageGoal or Shop.dat producer.

The resulting 720121 is already owned by [EASTER_GOLD_PACKS.md](EASTER_GOLD_PACKS.md).

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, catalog-only Surprise box 722178,
nested/gear leftover 721222–721245, classified silver leftovers, leftover
medicine bundles 720010–720017, classified 720654, other leftover packs, Host
restart, native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
