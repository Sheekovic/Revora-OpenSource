# Leftover Attack/Defense potions 721623/721624/721672/721673/721627/721628

M6.56b9p, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover Attack/Defense potions
go through `AttackDefensePotions`. The six leftover cases fail closed.

## Leftover terms (preserved)

StarterPacks ordinarily issue the six IDs:

| IDs | Pack | Leftover Time / Increase / AddFlag | Leftover text |
| --- | --- | ---: | --- |
| 721623 / 721624 | L10 727028 | 15 | `Your attack will increase during the next 15 minutes` / `Your Defense will increase during the next 15 minutes` |
| 721672 / 721673 | L75 727041 | 30 | 30-minute leftover texts |
| 721627 / 721628 | L120 727050 | 45 | 45-minute leftover texts |

721623/721672/721627 write leftover `StigmaStamp`, `AddFlag(Stigma, n, true)`,
`StigmaTime = n` and `StigmaIncrease = n`. 721624/721673/721628 write the
matching MagicShield fields. `n` is 15, 30 or 45. Leftover messages say
minutes; leftover Time/AddFlag use those same numbers. A later potion of the
same family overwrites stamp, time, increase and the durable grant. Attack and
defense grants persist independently.

## Owned contract

One owned unit is consumed with stack remainder kept. Durable
`revora_attack_defense_state` stores the leftover grant seconds, increase and
UTC grant stamp. Remaining duration is `grant - elapsed whole seconds` on
reload. Receipt table `revora_attack_defense_potion_uses`. Failed source,
timer or receipt writes roll back together. Direct daily P7 is unchanged.

## Remaining work

Unrelated leftover source+timer families, Host restart, native acceptance and
deployment remain open. Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
