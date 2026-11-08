# Equipment enchantment outcome repair

Reviewed2026-09-27. The legacy dispatcher calculated positive refined/super
outcomes only for Dragon Gems; other accepted refined/super gems could be consumed
for zero HP. It also used exclusive random upper bounds that omitted published
maximum values.

`ItemEnchantment.Enchant` now resolves the gem's range centrally, draws with an
inclusive maximum and delegates to the existing owned transaction. `Apply` checks
the outcome against that same range before payment. A lower legitimate result
still consumes the selected gem but retains the better saved enchantment;255 HP
cannot consume another gem.

The [official desktop enchantment guide](https://co.99.com/guide/guides/enchanted.shtml)
was checked directly. It publishes these HP ranges:

| Gem | Normal | Refined | Super |
| --- | --- | --- | --- |
| Dragon | 1-59 | 100-159 | 200-255 |
| Phoenix | 1-59 | 60-109 | 170-229 |
| Tortoise | 1-59 | 40-89 | 170-229 |
| Rainbow | 1-59 | 80-129 | 170-229 |
| Moon | 1-59 | 60-109 | 140-199 |
| Violet | 1-59 | 60-109 | 140-199 |
| Fury | 1-59 | 40-89 | 90-149 |
| Kylin | 1-59 | 40-89 | 70-119 |

Normal Thunder/Glory gems retain Revora's existing1-59 behavior. Their refined
and super variants have no confirmed outcome in this guide and are rejected
without payment, replacing the former zero-result consumption. This is explicitly
a retained Revora compatibility policy, not an original-rule claim for those gems.

The isolated build and `Verify-EquipmentSets.ps1 -Earning` pass365 total checks.
The58 new cases cover both inclusive endpoints for all24 documented gem/grade
combinations, the two retained normal variants, unsupported materials, zero-roll
rejection, preservation of a better value and the cap. Each supported endpoint is
observed through the public enchant handler, paid item persistence and replay;
these boundary fixtures seed their inputs separately from the ordinary earning
journey. Earlier checks still prove all eight full-set achievements through earned
funding, purchases, upgrades and full character/item reload.

No packet layout, price or live instance changed. Native presentation and live
deployment remain pending.
