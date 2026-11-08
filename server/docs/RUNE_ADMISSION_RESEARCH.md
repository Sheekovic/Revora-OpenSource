# Rune admission and acquisition checkpoint

2026-09-16 UTC. Research baseline `f3d66ac`; M6.50b now implements the
server admission and effect guards below. Native acceptance and the full
acquisition journey remain pending.

## Evidence

The [official February 2018 rune Q&A](https://co.99.com/news/2018-02-02/runesqa.shtml)
requires second rebirth and level 15. Its original layout has one red, one blue
and three distinct yellow types per set. Red/blue runes must match the class;
it mentions a universal red rune. The [launch class guide](https://co.99.com/news/2018-02-01/allrunes.shtml)
identifies the named class skills. These are the original rune edition, not the
later mobile Mystic Rune feature.

The installed 6711 `ini/RuneSys.lua` independently sets `lev=15`, `mete=2`.
SHA-256: `17d6185793bd249c81134accfdfcc8f245a27b8f87bc97b3fb99010cdc855324`.
Read-only in-memory decoding of `ini/itemtype.dat` found 397 rune rows. All
397 class/level pairs match the running tree's `database/itemtype-new.txt`.
Client SHA-256: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
Server catalog SHA-256: `1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef`.
No donor/client file was modified or added to Git.

Installed red/blue class fields are 10,20,40,50,60,70,80,132,142,160. Every yellow
class field is zero; all item-level fields are zero. There is no class-zero red
rune in this catalog. The general level requirement therefore comes from the
rune system, not the item's level column. Water/Fire minimum class fields are
132/142; the exact promotion comparison must follow reviewed equipment rules,
not display-name matching or treating both as generic Taoist.

## Implemented admission and recovery

New equips require second rebirth, level 15, a valid installed rune level and the
catalog's class/promotion minimum. Each set has slots 101-105 or 121-125: one
red, one blue, three distinct yellow types. A higher-level copy may replace the
same type in the same slot. The same type may appear once in each separate set.
Unsupported 106-108 positions reject new equips. Existing rows remain saved and
owned; ordinary unequip returns them to the rune bag. Under-level, wrong-class
or first-rebirth characters may still recover their existing equipped runes.

Equip/equipped-upgrade evidence verifies saved class, level and rebirth against
the current owner under its database row lock, then validates post-change item
preimages. All composition, including rune-bag upgrades, verifies that profile
before consuming materials. Illegal combinations cannot mint the generic or
class-specific achievements. Historical legitimate receipts remain historical.

`RuneProgress.LegalRunes` supplies one rule to skills, Chosen One, achievement
counting and live/offline prestige. Invalid old items contribute nothing without
being deleted. Repeated yellow types within a set are both excluded until the
player removes one. Duplicate persisted slots retain the earlier loader's
explicit repair requirement; this change does not silently choose a row.
Offline prestige now reads stacks and retains duplicate rows for validation.

Stat recalculation reconstructs derived rune skills from the selected legal
set. Class, rebirth and level setters synchronize on the owner and refresh
skills, stats, prestige and title eligibility. The runtime uses its existing
`AlternateEquipment` state; tests exercise `GeneralData.SwitchEquipment` with
different levels of the same yellow type, including a downgrade. Native rune
panel selection/presentation still needs verification. Rune skills are replaced
when their level changes, not retained by the general AddSpell duplicate guard.
Prestige keeps the existing scoring of both valid saved sets; active combat
effects use only the selected set.

There is no implicit grant of a universal red rune absent from the installed
catalog. The item-level field is not used as a substitute for the system unlock.
Changing class does not destroy previously purchased runes or earned receipts.

## Validation

Build passed. The 185 loadout checks include 56 admission checks: all ten matching
red/blue pairs and foreign-class rejections, Water/Fire promotion minimums,
second-rebirth level-15 boundaries, same-type replacement, primary/alternate
switching, extra-slot recovery, persisted duplicate-type recovery, a fraudulent
five-maxima combination, immediate class/level/rebirth changes, stale database
profile rejection and material preservation. Existing 129 loadout/receipt checks
also pass with legal matching runes and the installed spell definitions.

Final-build regression suites: composition/offline prestige 23, titles 91,
progression 74, experience/skill persistence 452, guild seasons 344, default
boss/Potency 216, prestige 58, combat 669. Total **2,112 gameplay checks**, plus
three producer-audit tests. Source coverage remains 167/337; no new producer
identity or complete acquisition journey is claimed by admission enforcement.

Server SHA-256:
`21ADC947B0C0819A97FBC1D56212945E2A2B7E9406AA21F4DE24B9C6E9D48085`.
No live deployment, client installation or player-data migration was performed.

## Acquisition follow-up

[M6.50c](RUNE_ACQUISITION.md) now verifies a complete ordinary server route:
83 earned daily-boss bounties fund Vesper's initial runes and all same-color
materials, followed by maximum upgrades, equip and uncached wardrobe reload.
Its 16 integration checks pass on the same M6.50b build. The route costs
4,131,250 RVPs; alternative stones/essences and practical balance remain review
work. Native acceptance remains pending. Vesper's separate non-rune shelves
use a different legacy purchase path and are not covered by the rune transaction
claim.
