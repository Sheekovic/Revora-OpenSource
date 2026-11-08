# Seasonal cup statistics (M7.2d)

All five cups now contribute their installed catalog statistics once through
the ordinary equipment calculation. Gold Prize and Gold Trophy no longer add
legacy flat bonuses, duplicate physical defense or a separate 20% combat modifier.
The shared item parser now reads the distinct skill-critical field at column 40.

## Evidence and intended values

Read-only installed client input: `6711/ini/itemtype.dat`, SHA-256
`D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.
The existing Revora decoder read the five rows in memory. The server's effective
`itemtype-new.txt` rows agree; older `itemtype.txt` Gold Prize/Trophy rows contain
different local names and inflated values but are superseded during catalog load.
No client/catalog file was modified.

| Item | HP / MP | Physical attack | Magic attack | Physical defense | Critical / skill critical / immunity | Breakthrough / counteraction |
| --- | --- | --- | --- | --- | --- | --- |
| Bronze Prize 2100055 | 900 | 0 | 0 | 0 | 0 | 0 |
| Silver Prize 2100065 | 1,200 | 0 | 0 | 0 | 0 | 0 |
| Gold Prize 2100075 | 1,500 | 1,000 | 1,000 | 1,000 | 3% each | 3% each |
| Gold Trophy 2100085 | 1,500 | 0 | 0 | 0 | 1% each | 1% each |
| Holy Grail 2100095 | 1,600 | 1,100 | 1,100 | 1,200 | 3% each | 3% each |

Gold, Bronze, Silver and Grail values also match the
[official seasonal reward description](https://co.99.com/news/2019-05-21/22nd_super_guild_war_on_05/25-06/22.shtml).
Trophy bonuses are supported by the installed 6711 row. Physical attack applies
equally to minimum and maximum attack. Stored rating units are 100 per percent
for critical/skill critical/immunity and 10 per percent for breakthrough/counteraction.

## Calculation scope

The dedicated base-stat branch covers only these five item IDs in the bottle
slot. Ordinary primary/alternate equipment selection runs first, so inactive
items do not stack. Broken items remain excluded by the existing durability gate.
Equipment recalculation, cup removal and Gold Prize rollover leave no lingering
item-type combat flag. Physical, ranged and magic legacy damage paths no longer
apply an additional percentage just because the attacker or defender wore Gold.

Existing non-cup calculations and item upgrades/refinery handling remain outside
this correction. The ordinary stat serializers and packet layouts are unchanged.

## Validation

Final staged build SHA-256:
`02F87A32FE9F72D7E37A4B2CB32B0CD01F5053BAA4F6BE92FFA152B1356ABBC1`.

The guild-season suite passes **254 checks**, including 123 new cup checks:
five catalog contracts; all five cups across ten classes; repeated recalculation;
removal; active/alternate selection and fallback; broken items; and four real
structure-damage dispatchers comparing identical calculated attack statistics
with and without a Gold Prize equipped.

Related suites pass: combat 669, refinery 174, progression 74 and Potency/daily
bosses/Extraction 216. Total: **1,387 checks**. The complete staged guild battle,
claim, exchange and rollover fixtures also remain green.

The implementation is staged, not deployed. Genuine-client status display remains
open. [M7.2e](GUILD_WAR_PURSES.md) adds atomic regular Sunday rank purses. [M7.3](HOLY_GRAIL_CRAFTING.md) now adds
Grail crafting and both wardrobe grants.
