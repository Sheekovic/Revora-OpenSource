# Dis City exchange evidence and implementation

2026-09-15, baseline `f4d48b4`.

## Star Sword

The [current official Dis City guide](https://co.99.com/guide/quests/monstercity.shtml)
and installed item 723088 agree on a Star Sword exchange for a choice of Phoenix,
Elegant or Celestial Dress. The older Facebook guide has different objectives
and reward timing; its shared-PK Dark Horn hand-in is not the selected 6711
four-stage edition.

The installed/server catalogs contain variants 137020/137023, 137030/137033 and
137040/137043. The suffix-3 variants are the fixed clothes entries selected here.
All require level 100 and have no class or gender restriction in those catalog
fields. Existing equipment classification places them in **armor slot 3**. They
are not Wardrobe garment-slot items and do not justify any garment-collection
achievement. Solar Saint explains that distinction before the exchange.

Installed `3dobj.ini` contains the 137020, 137030 and 137040 appearance families
for player body profiles. Its SHA-256 is
`9202de93aa033d9a32bab73e72a1fe7bee659ee36ac7ce87c9c92cf3ea49e822`.
This supports appearance-family selection, not a native rendering claim.

Revora chooses a 20% Star Sword chance on the owned summoned Pluto's defeat.
The exact rate is local tuning. The outcome is saved with the boss-death receipt,
then delivered with the final weekly claim. Repeated final-claim requests do not
reroll it. Full bags preserve the entire victory claim. Issuance, final rewards,
the weekly completion and achievement commit together; each issued sword is tied
to the completed run through a deferred foreign key and unique run/type identity.

Solar Saint's Twin City exchange offers one exact issued sword UID for one minute.
The player chooses a permanent bound dress. The sword, issued-item usage receipt,
new dress and exchange receipt change in one transaction. The replacement fits a
full bag. Failed writes preserve the sword, and repeated choices cannot silently
consume another sword. Locked, expiring, foreign or unissued items are ineligible.
Inventory updates follow commit. Ordinary players can use the exchange; no GM
or named-character gate is present.

## Dark Horn prerequisite

Historical findings at this checkpoint are superseded by the subsequent
[Dark Horn implementation](DIS_CITY_DARK_HORN.md). Its owned weekly earning
and atomic direct EXP exchange now pass server fixtures. Native acceptance
and deployment remain pending.

The current Dis City guide and installed item 790001 specify 300 minutes of EXP.
The official [Everything Has A Price guide](https://co.99.com/guide/quests/2012/Everything_Has_A_Price.shtml)
explicitly equates 60 minutes with one EXP Ball, supporting **five EXP Balls' worth**
for that stated reward. This resolves the quantity; it does not certify the
emulator's existing numeric EXP calculation.

Source findings still requiring implementation:

- `GameState.IncreaseExperience` changes EXP, level, base stats/free attributes,
  class skills, mentor rewards and other level-up state through separate writes.
  Calling it after deleting a horn can lose the reward on a partial failure.
- The subsequent [experience arithmetic checkpoint](EXPERIENCE_PROGRESSION.md)
  corrects the public EXP Ball range, truncated final level-table entries and
  fractional reward calculations. The expanded progression checkpoint now has
  91 checks, including ordinary-player awards, level crossings from 135 through
  140, allocation ownership and production saves. These corrections do not certify the
  server curve as original CO values or make the legacy award transaction durable.
- `EntityTable.SaveEntity` already takes the inventory-owner lock, but the legacy
  EXP routine and direct EXP/level assignments do not all share a single durable
  progression transaction. A new reward must coordinate with those writers and
  preserve legitimate level-up effects, including rebirth restoration.

Dark Horn is therefore not issued as a usable completed reward in this batch.
Implement its durable EXP path, then connect earning and consumption with failure,
cap, rebirth, level crossing, concurrent-save and reconnect checks. Do not substitute
an inventory package for the promised direct EXP exchange and mark it complete.

## Remaining scope

Star Sword's expanded fixture passes 141 Dis City checks, including a real owned
boss reward, deferred outcome preservation, full-bag exchange, failed writes,
unissued/locked items, expired offers, concurrent/replayed choices, all three
outfit choices, production item loading and the ordinary equipment handler for
each dress. Two extra sword choices use seeded historical completed runs with
the production issuer; the fixture does not claim three full dungeon runs.
Twelve private-overlay checks and three producer-audit checks pass. The source
inventory remains at 81/337 reviewed earning identities.
Related regressions pass: 91 title/wardrobe, 75 weapon-mastery and nine quest-packet
checks (175 total).

The isolated Debug/x86 build passed. Assembly SHA-256:
`F7136BAA1447079F4CB61B167319DE79AD0CD89813FD7AE560E30E5C36C85905`.

The updated private patch adds the Star Sword tooltip and final-quest chance text,
retaining the previous Summer, Labyrinth and Dis City changes. Stage:
`obj/chi-refill-validation/dis-city-sword-overlay`.

| Output | SHA-256 |
| --- | --- |
| `ini/itemtype.dat` | `c4979cfdf30dda28f69ae9b826e6361b11b1a88c8a95444cb43b6e0b14c8680d` |
| `ini/Questinfo.ini` | `b4f6c9f157c06ac24aa300696fd50419f13b2568193a74f2388ac3982be5eb3e` |

Difficulty choices, Dark Horn, next-day ranking prizes and native
acceptance remain open. No installed client files, public patches, production
player records or live binaries are changed by this source batch.
