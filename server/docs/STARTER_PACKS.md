# JustCreated starter-pack chain

> Retired from new-character grants on 2026-09-25 by owner request. New
> characters start at 1002 (327,303) with a pending RevoraStarterPack entitlement.
> The description below documents the legacy chain for already-owned items;
> it no longer describes new-character delivery. See REVORA_STARTER_PACK.md.

M6.56b8a through M6.56b8d, 2026-09-22. Server implementation; not deployed.

Ordinary new characters receive pack **727026** from the same `JustCreated`
login path that issues **723753**. Owned use covers
727026 → … → terminal **727050**. This chain is not merged with
[WelcomePacks](WELCOME_PACKS.md).

## Executable gates and rewards

| Pack | Gate | Currency | Items |
| --- | --- | --- | --- |
| 727026 | level ≥ 1 | +15,000 silver | 10×722136, 5×1001000, successor 727027 |
| 727027 | level ≥ 5 | +1,500 silver | 5×1001000, successor 727028 |
| 727028 | level ≥ 10 | +5,000 silver | 3×723790, 721623, 721624, successor 727029 |
| 727029 | level ≥ 15 | none | 5×723790, 3×1060020, successor 727030 |
| 727030 | level ≥ 20 | +500 CP | successor 727031 |
| 727031 | level ≥ 25 | none | 203009, successor 727032 |
| 727032 | level ≥ 30 | none | 1200000 PrayingStone(S), successor 727033 |
| 727033 | level ≥ 35 | none | 723017 EXP Potion, successor 727034 |
| 727034 | level ≥ 40 | none | 723017 EXP Potion, successor 727035 |
| 727035 | level ≥ 45 | none | 1200001 PrayingStone(M), successor 727036 |
| 727036 | level ≥ 50 | none | 1200002 PrayingStone(L), successor 727037 |
| 727037 | level ≥ 55 | none | 723017, retired BlackTulip 723584, successor 727038 |
| 727038 | level ≥ 60 | none | 723700 EXP Ball, lily 752001, successor 727039 |
| 727039 | level ≥ 65 | none | 2100025 City Guard bottle identity, successor 727040 |
| 727040 | level ≥ 70 | none | 2×723700 EXP Ball, successor 727041 |
| 727041 | level ≥ 75 | none | 1200000 PrayingStone(S), 721672, 721673, successor 727042 |
| 727042 | level ≥ 80 | none | 2×723700 EXP Ball, successor 727043 |
| 727043 | level ≥ 85 | none | 2×723017 EXP Potion, successor 727044 |
| 727044 | level ≥ 95 (L90 region/message) | none | 723268 MeteorBox, 2×723017, successor 727045 |
| 727045 | level ≥ 95 | none | 723268 MeteorBox, 2×723017, successor 727046 |
| 727046 | level ≥ 100 | none | 751099, successor 727047 |
| 727047 | level ≥ 105 | none | 1200000 PrayingStone(S), 2×723017, successor 727048 |
| 727048 | level ≥ 110 | none | 2×751099, successor 727049 |
| 727049 | level ≥ 115 | none | 2×700012 if raw class ≤75, else 2×700002, successor 727050 |
| 727050 | level ≥ 120 | none | 193300, 3×751099, 721627, 721628 (terminal) |

Inventory.Add still creates one bag slot per unit. Exact output capacity
credits the source slot only when the source is deleted. Final-tranche counts
are 2, 4, 3, 3 and 6. Outputs stay unbound plus-0 as in the live handler.
727044 keeps the executable level-95 gate and the legacy “Level 90” refuse
message. 727049 uses the raw `Entity.Class <= 75` threshold. After 727050’s
level/capacity branch the service still sends the Level-120 refusal text,
including after a successful open, matching the executable extra send.

## Persistence

Under the inventory owner lock the service checks the live source, complete
durable bag UID set and restrictions. Silver uses `SilverBalance`. CP uses
`entities.conquerpoints` compare-and-swap. Failed writes roll back together.
Direct `ItemHandler.UseItem` shares the service. All twenty-five 727026–727050
legacy cases are fail-closed. Existing `revora_starter_pack_uses` rows for
727026–727045 are kept when the item-type check is widened. 727049 compares
the locked durable class preimage with the live cache before choosing gems.

Downstream 723017 remains ExperiencePotions, 1200000/1200001/1200002 remain
PrayingStones, 722136/723700 remain ExperienceConsumables, and 723584 remains
RetiredBlackTulip. 752001 is the lily flower identity. 2100025 is the City
Guard bottle catalog identity. 721623/721624, 721672/721673 and 721627/721628
are owned leftover Attack/Defense potions ([ATTACK_DEFENSE_POTIONS.md](ATTACK_DEFENSE_POTIONS.md)).
723268 remains the existing MeteorBox handler.
751099, 193300, 700012 and 700002 remain catalog identities without a new
ordinary-use claim.

## Remaining work

The 727026–727050 starter-chain source tranche is complete. Exhaustive
legacy review, native-client acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
