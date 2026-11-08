# Leftover BrightStarStone combine 3009001 → 3009002

M6.56b9o, 2026-09-23. Server implementation plus mechanical UseItem census.
Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover **BrightStarStone
3009001** go through `StarStoneCombine`. The legacy case fails closed.

## Census integrity

Re-derived every 518 top-level `ItemHandler.UseItem` case from executable
bodies. Timer writes are `client.Entity` timer/stamp/day-state assignments
(`*Time`/`*Stamp`/`*Days`, including leftover `NoDrugsTime` cooldowns) and
`AddFlag(`. `#region` names and packet-copy fields such as
`upd.FreeTimeTodeyUsed` are not timer writes. `AddRune` is an item output.
Source+timer is live-split-write.

VIP-day 3004121–3004124/3000794 are catalog + unreachable UID 6001 shop /
leftover consumer ([VIP_DAY.md](VIP_DAY.md)). DisguiseAmulet 723724 is catalog + leftover consumer ([DISGUISE_AMULET.md](DISGUISE_AMULET.md)).
Bloodshed Sea 3004230/3004182/3004231–3004238 are owned by
[BLOODSHED_SEA.md](BLOODSHED_SEA.md).
Leftover HP/MP medicine plus LifeFruit (19 `NoDrugsTime` IDs) are owned by
[HEALING_MEDICINE.md](HEALING_MEDICINE.md).
721623/721624/721627/721628/721672/721673 are owned by
[ATTACK_DEFENSE_POTIONS.md](ATTACK_DEFENSE_POTIONS.md).
3000788 is **other**: VIP 8 WhiteDragon summon with leftover `Remove` commented out.

| Kind | Count |
| --- | ---: |
| live-split-write | 159 |
| owned-fail-closed | 261 |
| single-consume | 55 |
| other | 43 |
| **total** | **518** |

Leftover TryUse disposition covers every pre-switch intercept with an exact
source-derived handled-ID catalog (see
[ITEM_USE_COMPOUND_AUDIT.md](ITEM_USE_COMPOUND_AUDIT.md)):

- **fail-closed:** 261 leftover throws of `must use the owned service`
- **independently live:** leftover still calls `TryUse` — P7SoulPacks 3004247/3004248; ArenaPrizeConversions 3004243/3004244/3004245/3009002
- **init-absent:** none; Power Arena packs are owned by [POWER_ARENA_PACKS.md](POWER_ARENA_PACKS.md)
- **leftover-live inert:** TeratoEncounter 720598 and PurpleBansheeEncounter 3004259 silent-seal messages
- **no-legacy-case:** remaining intercept families whose handled IDs are absent from the 518-case switch

## Leftover terms (preserved)

`Contains(3009001, 10)` and `Count <= 31`, then `Add(3009002, 0, 1)` and
`Remove(3009001, 10)`. Refusal text is `You do not have 10 stones, or you lack
5 inventory spaces.` No leftover success text. Output is one permanent unbound
plus-0 stack-1 catalog-durability RadiantStarStone **3009002**.

Owned service consumes **exactly 10** units with the same first-stack>=10
selection as leftover `Remove(ID, n)`. Receipt `revora_star_stone_combines`.
Direct daily P7 is unchanged.

## Ordinary issuance

Vesper NPC **900000014** is parked at Twin City 1002 (396,340) at startup and
sells 3009001 for 400 Revora Points (option 11). Ordinary evidence uses
`Npcs.GetDialog` with the installed NPC, asserts the exact 400-RVP debit, and
reloads the durable source before combining. SuperBoss `MonsterTable.Drop`
of spawned FuriousBear can also floor-drop 3009001. Online-points NPC option 41
and timed Perfection-map awards are additional coded routes.

## Remaining work

Catalog-only Chi pills, Surprise box 722178, nested/gear leftover,
classified silver leftovers, medicine bundles, classified 720654, other
leftover packs, Host restart, native acceptance and deployment remain open.
Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
