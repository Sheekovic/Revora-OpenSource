# Weapon mastery achievement restoration

2026-09-15, baseline `fef9c0e`. Code validated; deployment and live client acceptance pending.

The installed `Achievement.dat` defines level-twelve weapon mastery conditions.
Its SHA-256 is
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
The installed `WeaponSkillName.ini`, SHA-256
`6DA827F97CE10C7EC5FB07A306D7A73E1949D401BA0917C46D1AC2075A1AC84E`,
maps these names to numeric proficiency identities. This is client-content and
server-source evidence; no new packet layout or captured-client claim is made.
Proprietary catalog contents remain outside Git.

| Proficiency ID | Achievement ID |
| --- | --- |
| 0 | 10321 |
| 420 / 410 / 460 / 540 / 580 | 10322 / 10323 / 10324 / 10325 / 10326 |
| 560 / 490 / 561 / 510 / 530 / 430 | 10327 / 10328 / 10329 / 10330 / 10331 / 10332 |
| 450 / 480 / 481 / 440 | 10401 / 10402 / 10403 / 10404 |
| 500 / 610 / 601 / 421 | 10405 / 10406 / 10407 / 10408 |
| 900 | 11132 |
| 611 / 612 / 511 | 11210 / 11211 / 11212 |
| 613 / 614 / 616 | 11213 / 11214 / 11215 |
| 617 / 620 / 622 | 11221 / 11222 / 11223 |
| 624 / 626 / 671 / 670 | 11302 / 11303 / 11322 / 11323 |

The observed current proficiency must have the matching dictionary and record
ID, and a level from twelve through the supported cap of twenty. A previous
rebirth level, temporary display field or client-reported completion is not new
evidence. Existing server combat awards experience through
`IncreaseProficiencyExperience`; the same proficiency records are saved and
loaded by `SkillTable`. Reconciliation commits each mastery once and retains
it after proficiency reset, weapon changes and reconnects.

The initial review left Scythe 11212 disabled because 511 and 550 shared a
proficiency display name. The later catalog/combat review below resolves the
supported Revora identity as 511; 550 remains excluded.

The isolated build and 75 mastery checks pass. Every mapped identity crosses
eleven to twelve through the production experience method, saves through the
production proficiency writer and restores through its loader. Checks also
cover historical reset/reload, concurrent evidence replay, mismatched identity,
corrupt level, unresolved aliases, storage rollback and retry. These tests prove
the earning producer and persistence; they do not certify ordinary acquisition
of every historical weapon or a complete 320-achievement route.

The 74 progression, 91 wardrobe, 74 cultivation, 42 Couple PK, 116 Class PK and
21 rune regressions pass: 418 regression checks. Five catalog/prerequisite audit
checks pass. Staged server SHA-256:
`923AA856D6FE307814ACC8749155CB562FE7F7A8FA9C31017B8BF0C274CD0273`.
No live executable or client file was replaced.


## Scythe identity resolution (2026-09-26)

Review base `df2e294`. The name table still contains both 511 and 550 as
Scythe. It is insufficient evidence on its own. Additional local evidence:

- Installed server `bin/Debug/database/itemtype-new.txt`, SHA-256
  `d66c0489807d877e19053c8c04994d6738ffccf5c86565f216399f5dbfd55279`,
  has 343 family-511 item records and no family-550 records.
- Installed client `ini/itemtype.dat`, SHA-256
  `ab428fe7d3932d98ead2ece8cdc20c548ee909723792fb4de955df7dddfa7753`,
  independently has the same family counts after decoding. Family 511 spans
  the installed WildScythe through SilenceScythe equipment.
- `ItemHandler.IsScythe` classifies IDs 511000..511439, and its equipment
  checks treat family 511 as the Ninja scythe. Ordinary shop stock includes
  511004 and subsequent 511 equipment tiers.
- `Handle` derives the weapon proficiency from item ID divided by 1000 and
  awards combat proficiency experience to that identity. Its special remaps
  for other weapons do not remap 511 to 550. `SkillTable` persists the same ID.

The [official Ninja Scythe item guide](https://co.99.com/guide/items/ninja_scythe.shtml)
corroborates the weapon category and proficiency progression, but does not
publish numeric item/proficiency IDs. Numeric resolution relies on the local
catalogs and server path above, not that website or an executable trace.

Achievement 11212 now observes owned current proficiency 511 at level 12..20.
The unused 550 name row grants nothing. This is an evidence-backed mapping for
Revora's installed weapon family; native achievement UI behavior remains
unverified. No client files were modified or copied into Git.

The isolated build and 78 mastery checks pass: loaded catalog classification,
all 34 supported proficiency identities, level-eleven rejection, actual server
experience progression, production save/load, historical retention, identity
and level corruption rejection, unused 550 rejection, concurrent reconciliation
and persistence rollback/retry. Ordinary purchase/equip/melee journeys for every
historical weapon and a compatible per-character 320 route remain separate.


## Proficiency award arithmetic repair (2026-09-27)

The ordinary combat caller supplies earned damage to
`GameState.IncreaseProficiencyExperience`. Review found two independent defects
before achievement observation: the first call for an unknown proficiency created
its record but discarded the supplied EXP, and existing records used wrapping
32-bit rate/gem/VIP multiplication and EXP addition. For example,
2,147,483,648 EXP at rate 2 became zero; adding 3,000,000,000 to an existing
2,000,000,000 wrapped below the final level threshold.

The method now processes the first positive award after registering its record.
Zero awards do not create a proficiency. Decimal intermediates preserve the
existing effective rate, integer gem bonus, training-ground division and VIP order.
After division, saturation uses only the EXP still needed to reach the configured
cap. This also bounds VIP multiplication for the supported numeric inputs.
Level-up consumes that wide balance before storing the remaining uint; a capped
record no longer accumulates unused EXP. No configured rates, thresholds or
maximum levels change. The effective-rate configuration getter remains unchanged.

Validation uses the existing isolated production build and
`tools/Verify-ArenaProcess.ps1 -MasteryOnly`. It checks first-award retention,
zero-award behavior, gem/training/VIP rounding, rate overflow, addition overflow,
VIP overflow, gem overflow with an exact multi-level remainder, maximal numeric
bonuses and cap behavior. All 34 installed proficiency families then cross the
level-11/12 boundary through the repaired method and save via `SkillTable`.
After actual database/game restart, all 34 level-12 records reload; resetting
the in-memory proficiency levels preserves exactly one saved mastery achievement
for each of those 34 identities.

The threshold preconditions and numerical awards are supplied test inputs. This
is not an ordinary purchase/equip/combat journey for all historical weapons, an
earned rebirth journey or a compatible 320-achievement career. Those acquisition
and career gates remain open, as do native display and live deployment.

Private build SHA-256:
`D6EEE3E24C517266D0BAAD98E40BDF909AD7B1AEDA2D1DB4EC30A308585B32D0`.
Local logs: `obj/proficiency-award-build.log` and
`obj/mastery-awards-process.log` (ignored). No live server was replaced/restarted.


## Mastery preservation during rebirth (2026-09-28)

A new regression reproduced lost attainment on private game
`A40A9692DAD1C63CEF1C6F7E53A4F77F02AA98CDFDEA413FB7E352F7A8E67403`:
a canonically loaded ordinary character advanced all 34 proficiencies from level
11 (one XP short) to 12 through `IncreaseProficiencyExperience`, saved them, and
completed Sereth's first-rebirth dialogue without an achievement refresh. The
rite succeeded and reset all levels to zero, but mastery evidence was absent.
Previously the lifecycle fixture explicitly refreshed before a simulated reset,
which concealed this ordering bug.

`AchievementCatalog.ObserveMasteries` now shares the existing 34 mappings with
the normal observer. First rebirth, second rebirth and paid reincarnation persist
these current-level observations inside their existing owner-locked transaction,
before resetting proficiencies. The observation key is unchanged, making later
refreshes idempotent. `PreviousLevel` alone still does not grant mastery.

Validation on private game
`59B88E6D1FCC30877C4856ABC00451786CF62A46D7BF7394B288FF5A2883573D`:

- Isolated x86 build passed with existing legacy warnings.
- `tools/Verify-ArenaProcess.ps1 -MasteryOnly -ResetOwnedFixture` passed both game
  phases and the intervening PostgreSQL restart.
- The actual first-rebirth NPC sequence preserved all 34 earned observations
  while resetting current levels to zero and retaining previous levels of12.
- Suppressing the final rite receipt rolled back all 34 new observations and
  proficiency resets, retained the Heavenly Stone, and allowed successful retry
  after canonical reload.
- Subsequent game-process load and repeated achievement refresh retained exactly
  one evidence row per mastery.

The profile level, stone and near-threshold proficiency state are supplied test
inputs; the final XP award, NPC rite and database transaction are production
paths. This is not a complete combat-training career or a native client test.
The second-rebirth and paid-reincarnation call sites use the same helper, but
this new end-to-end regression has exercised first rebirth only. Their specific
pre-refresh mastery journeys remain to be checked. No live deployment occurred.


### Second rebirth and paid reincarnation follow-up (2026-09-28)

The same private game hash above now passes the expanded `-MasteryOnly` suite
for all three reset paths. Two additional ordinary characters earn prerequisite
first/second rites through Sereth; eligibility levels, materials, CPs and
near-threshold proficiencies are supplied fixture inputs. The paid character's
attribute budget includes the supplied95 levels before the production reset.

Each additional character earns all34 mastery thresholds through the production
XP award without refreshing achievements. An injected failure at its final rite
receipt leaves the payment item, CP balance and level12 proficiencies intact,
with no new mastery evidence or rite receipt. Canonical reload then permits one
successful NPC/packet confirmation, consumes one payment, charges exactly the
configured fee for paid reincarnation, resets proficiency levels, and preserves
all34 achievements. Repeated confirmation cannot charge or award again.

Across a real game-process and PostgreSQL restart, all three characters retain
an exact saved snapshot of item UIDs/types/binding/position/quantity, character
level/class/rebirth/CPs and proficiency level/previous-level/XP. Canonical reload,
repeated achievement refresh and stale confirmations preserve that snapshot and
exactly102 mastery evidence rows (34 per character). Previous-level-only values
are explicitly checked to produce no current mastery observations.

The first expanded-fixture attempt exposed inherited VIP4 eligibility combined
with the earlier arithmetic test's maximum VIP multiplier; its XP award exceeded
the fixture's level12 target. The fixture now explicitly fixes VIP0 and multiplier1
for these threshold probes. No gameplay rate was changed. Both phases then
passed on `59B88E6D1FCC30877C4856ABC00451786CF62A46D7BF7394B288FF5A2883573D`.

This closes the reset-specific ordinary-player regression for second rebirth and
paid reincarnation left open above. It does not establish an organically earned
320-achievement career, authenticated/native NPC acceptance, or live deployment.
