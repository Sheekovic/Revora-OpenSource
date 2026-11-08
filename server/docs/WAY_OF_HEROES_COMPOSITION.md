# Way of Heroes: composition and equipped +3

## Installed rules (2026-10-02)

Private `database/Slayer/stagegoal.txt` specifies:

| Stage | Task | Opening | Requirement | Bound reward |
| --- | --- | --- | --- | --- |
| 403 | 16 | level 101 | Compose equipment once | Three Meteors 1088001 |
| 503 | 23 | first rebirth level 15 | Equip a +3-or-better item | One +2 stone 730002 |

Stage 403 now requires an owned saved plus-composition result that advanced plus
or progress, with at least one material and a recorded equipment target type.
`EquipmentUpgrades` saves that type in `revora_plus_composition_targets`, keyed to
the composition receipt, in the same transaction as payment, item change and
receipt. This avoids making historical qualification depend on still owning the
item or its present item identity. Stone-on-stone composition is excluded by the
catalog equipment predicate. Older untyped receipts are not retroactively guessed
from current items; an additional ordinary equipment composition establishes the
new evidence. No player-facing activity requires GM privileges.

Stage 503 reads saved permanent equipment in the selected bank, checks correct
slot/category and runtime owner/item identity, and requires saved Plus 3-12.
The eligible categories are core equipment, talismans, mount, crop and wing;
arrows, cosmetics, bag/warehouse items and suspicious items do not qualify.
A +12 runtime cache cannot substitute for saved +2. This single-item predicate
does not resolve the separate total-bonus milestones' complete slot-sum rule.

Installed opening gates, capacity checks, bound rewards, ordinary-player guards,
unique claim receipt and atomic item/progress transaction remain. The +2 stone
uses the corrected Plus metadata from the previous batch. Main-stage prizes and
other unimplemented milestones remain refused.

## Ordinary action and restart verification

`tools/CheckCandidateCompositionClaims.cs` adds
`career-composition-claims-earn/reload` for one ordinary non-GM character. Starting
level 140/second rebirth, low-level bound boots and input +2/+3 stones are supplied.
Actual sealed packet 2036 composes the boots; no qualifying composition, equipped
bonus, milestone receipt or reward is inserted by fixture SQL.

Before composition both claims refuse. After the first ordinary composition the
boots are +2. Forging the runtime Plus to 12 still cannot claim stage 503.
Eight concurrent stage-403 requests produce exactly three bound plus-0 Meteors.
Each awarded Meteor is used through the normal level-upgrade handler, with
controlled success rolls and two-second test clock advances. All three are
consumed in three saved successful upgrades; replay preserves the result.

A second composition adds the supplied +3 stone, producing +3 boots with forty
remaining points. The bagged boots cannot claim stage 503. Equipping them and
sending eight concurrent requests awards one bound, correctly graded +2 stone.
Normal composition consumes that reward and produces +3 with eighty points.
Replay does not duplicate reward or progress. Three composition receipts retain
their original target item identities across the intermediate level upgrades.

Normal EntitySave and game/PostgreSQL stop/start are followed by exact scoped
purse/inventory/Chi/Way, embedding/composition, target-type evidence and equipment
upgrade snapshots. Both unique claims, empty inventory and +3/eighty-point result
survive. Replaying both claims after restart changes nothing.

The first run stopped after consuming one Meteor: the next immediate request was
correctly refused by the upgrade cooldown. The fixture now advances its controlled
clock between distinct attempts. Recovery verified the one claim, one composition,
+2 boots and consumed-plus-remaining total of three Meteors, then used the remaining
two. No earning/reward/payment was reset. Resumed earn and fresh cold reload passed.

The isolated committed-source-overlay build passed with existing legacy warnings;
the harness compiled with warnings as errors. Private game SHA-256:
`D36A991AFE22F1EEBE886263D284A3A51985533EB5F9079271A5E23ADFFFBAEA`.
Logs: `arena-process-career-composition-claims-{earn,reload}.log`.
Both game closes completed and PostgreSQL stopped. The old executable/manifest
were preserved; unrelated owner WIP was excluded from the build.

Native/authenticated display, natural starting qualification/material acquisition,
all equipment categories/alternate-bank variants, explicit crash injection and
live deployment remain pending. Supplied stones, controlled rolls/clock and the
fixture recovery are explicit limits. The retained main-career checkpoint is
unchanged. The target-evidence table is an additive prerequisite for future
composition proof; existing historical composition rows are preserved unchanged.


## Shared slots when switching equipment (2026-10-02)

Follow-up review found stage 503 incorrectly applying the alternate-bank offset
to all equipment. `ConquerItem` defines alternate slots only for 21-29; Fan10,
Tower11, mount12, crop18 and wing19 remain shared. The predicate now applies the
offset only to core slots and retains those shared positions in either bank.
Category validation uses the corresponding real slot, with normal ownership,
permanence and saved-Plus checks unchanged. Inactive primary core gear stays
excluded when the alternate bank is selected.

`tools/CheckCandidateSharedBonus.cs` adds `career-shared-bonus-earn/reload`.
An ordinary non-GM character with supplied level/rebirth and +3 boots/Fan equips
the main boots and switches through the normal equipment service. The inactive
boots cannot qualify; the Fan in the bag cannot qualify either. Equipping the
Fan in shared slot10 while alternate equipment remains active allows exactly
one bound +2 stone across eight concurrent requests. The previous predicate
would exclude both the inactive boots and the shared Fan, refusing this claim.

After normal save and game/PostgreSQL stop/start, exact scoped player/items/Way
and claim snapshots match and the reward retains Plus2/binding/stack1. Normal
switches to alternate and primary followed by replay do not issue another reward.
The selected bank itself is not claimed to persist: the test explicitly switches
it after reload. This is a focused slot regression with supplied gear, not a new
natural item-earning demonstration. Other shared categories are source-reviewed;
only Fan10 is exercised by this process case. Existing native/release gates remain.

The isolated committed-source-overlay build passed with existing legacy warnings;
the harness compiled with warnings as errors. Both phases passed first run.
Private executable SHA-256:
`5789A757186BC018FF8F8768E10B406BD5D9FBDCA07CBAED9E9A196FB07B911E`.
Logs: `arena-process-career-shared-bonus-{earn,reload}.log`.
Game and PostgreSQL closes completed, prior executable/manifest preserved,
unrelated owner WIP excluded. No live deployment was performed.
