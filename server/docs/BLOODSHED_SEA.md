# Bloodshed Sea boosts 3004230 / 3004182 / 3004231–3004238

M6.56b9t, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover Bloodshed Sea boosts
go through `BloodshedSeaBoosts`. Those leftover cases fail closed.

## Ordinary issuance

Installed ConquerPoints EShops **16834 / 16852 / 16853 / 16854 / 16855 /
16856** stock all ten IDs. Those UIDs have `public.npcs` rows on Bloodshed Sea
**3846**. `ItemUsage` Mode.BuyFromNPC (raw action 1) dispatches to
`HandleBuyFromNPC`, which requires only a nearby map NPC (distance ≤17) and
stock membership. A hidden leftover dialog option is not a server
authorization boundary. Nearby ordinary accounts obtain these potions through
that buy handler without choosing a visible shop line.

## Leftover terms (preserved)

Use requires map **3846**. One source unit is consumed. Each ID grants an
80-second stamp that refresh overwrites without stacking the numeric bonus.
`LoadItemStats` applies leftover amounts from the boolean flags. Attribute
3004230 adds through `BaseMinAttack`/`BaseMaxAttack`/`BaseMagicAttack` so
`GemAlgorithm` cannot overwrite the bonus:

| ID | Catalog | Leftover bonus |
| ---: | --- | --- |
| 3004230 | SuperUniversalPotion | +3000 Min/Max/MagicAttack (`IncreaseAttribute`) |
| 3004182 | DragonTear | `GodlyShield` flag |
| 3004231 | DragonBreakPotion | +1500 Breaktrough |
| 3004232 | DragonStrikePotion | +15000 CriticalStrike and SkillCStrike |
| 3004233 | DragonAnti-breakPotion | +1500 Counteraction |
| 3004234 | DragonImmunityPotion | +15000 Immunity |
| 3004235 | DragonP-AttackPotion | +3000 PhysicalDamageIncrease |
| 3004236 | DragonM-AttackPotion | +3000 MagicDamageIncrease |
| 3004237 | DragonP-DamagePotion | +3000 PhysicalDamageDecrease |
| 3004238 | DragonM-DamagePotion | +3000 MagicDamageDecrease |

Off-map 3846 sends leftover `This~potion~can~only~be~used~in~the~Bloodshed~Sea!`
and consumes nothing. Leaving and re-entering 3846 does not strip or
double-apply an active bonus. Expiry clears durable stamps then live flags
and `LoadItemStats`.

Flag 57 (`GodlyShield`) is shared with leftover RaceGuard. The production
CharactersCallback path (`ExpireSharedShield`) keeps leftover RaceGuard
expiry: it only runs off map 3846 after `GuardStamp+10s`. It clears
`RaceGuard` then removes flag 57 and leftover DivineShield UI only when no
Bloodshed `shield_at` remains. Bloodshed expiry leaves flag 57 in place while
leftover RaceGuard is still live. Ordinary RaceGuard expiry is unchanged
when there is no Bloodshed grant. The callback's generic timed-flag sweep runs
first; shared ownership therefore also restores the surviving owner's flag and
clears expired RaceGuard ownership when that sweep has already removed flag 57.
While `shield_at` is present, flag 57 uses a server-owned long-lived flag timer;
`Tick` persists the null stamp before it removes that flag. A failed expiry
write therefore fences the session without first dropping the live shield.
Projection sends the remaining DivineShield UI duration on use and reload;
a later GuardPotion keeps the longer Bloodshed duration instead of replacing
it with the race potion's 10-second UI.

## Owned contract

Source consume, `revora_bloodshed_state` type/timer stamps, and receipt
`revora_bloodshed_boost_uses` commit together under `ItemSyncRoot`. Failed
writes fence without live flag/stat mutation. Reload projects remaining
stamps before `LoadItemStats`. Direct daily P7 is unchanged.

## Remaining work

VIP-day tokens are catalog + unreachable UID 6001 shop / leftover consumer
([VIP_DAY.md](VIP_DAY.md)). DisguiseAmulet 723724 is catalog + leftover consumer
([DISGUISE_AMULET.md](DISGUISE_AMULET.md)). Catalog-only/other leftover packs, Host restart, native
acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
