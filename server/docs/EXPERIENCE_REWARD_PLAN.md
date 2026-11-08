# Complete base-stat planning for EXP rewards

2026-09-15. Baseline `8c1fc52`. This is another direct-EXP prerequisite; Dark Horn
is not yet enabled and the complete Dis City milestone remains open.

## Behavior

`ExperienceRewardPlan` calculates the resulting thirteen-field character profile
before any player mutation, packet or database write. Each earned transition
contains its destination level, validated automatic base stats when applicable,
and cumulative free attribute balance. The plan also identifies the existing
class skill, level-70 Arena transition and per-transition mentor EXP quantity.

Ordinary `IncreaseExperience` now applies these precomputed steps. A multi-level
award that crosses level 120 uses the installed level-120 base stats and then
adds free points for the subsequent levels. Reborn characters retain their
existing free-point policy. Skill identities, installed class-profile mappings,
derived-stat refresh order, configured multipliers and cap behavior are retained.
Pirate still uses the existing Monk profile; unrecognized jobs retain the Taoist
fallback. These inherited mappings are not newly certified official class rules.

`DataHolder.GetStats` now validates all four fields before applying any of them.
Previously an invalid fourth field could leave the first three already changed.
Planning validates every required transition, so invalid level-120 data cannot
leave a preceding level-119 reward partially applied. Extra fields, invalid
numbers and out-of-range attributes fail before application. Ordinary rewards
also reject final EXP outside the database's signed-bigint capacity before any
level, stat or skill effect.

## Mentor source correction

The [official PC mentor guide](https://co.99.com/guide/guides/mentors.shtml), read
2026-09-15, distinguishes three sources:

- Monster, quest and online-training EXP can contribute mentor EXP; team-shared
  EXP and bound EXP Balls are excluded.
- Praying Stone use contributes blessing time.
- A +1 item looted from a monster contributes stone credit; bought or traded
  items do not. One hundred points can be exchanged for one +1 Stone.

The prior level-up routine added one blessing unit and one stone point at every
level transition. Those unrelated grants are removed from that routine and from
its apprentice-row save. Existing earned balances are retained. Mentor EXP still
uses Revora's existing `destination level * 2` rate and existing prize cap; the
official source does not establish that numerical rate. Confidence is high for
the source categories, not for an exact official conversion formula.

This does not complete mentor persistence or source dispatch. The legacy award
signature still conflates several EXP sources; offline mentor contribution,
bound-ball/team exclusions, simultaneous apprentice gains and mentor claims need
the shared transaction review. Praying Stone and +1 loot producers are not added
or certified by this change.

## Validation

The isolated Debug/x86 build passes. The focused fixture passes **137 checks**,
including the earlier 121 progression, ownership and persistence checks. New
checks cover a three-level transition across automatic/free stats, thirteen-field
agreement with the ordinary award path, planning without player mutation,
Arena/skill intentions, cap behavior, signed storage overflow and malformed
later stat profiles. Ordinary mentor/apprentice identities preserve EXP credit
while leaving all blessing and stone fields unchanged in cache and PostgreSQL.

An additional transaction fixture persists the actual calculated level-121
profile. A failing receipt constraint rolls back its level, stats and points.
Successful commit and projection survive the normal character save, and the
production reader recovers all thirteen expected values. This is a profile
transaction test, not a completed Dark Horn claim or a full login simulation.

Assembly SHA-256:
`1878655A3908EC247B50BF18F7B12E0FBC0CD0CF1B9DFC262EE8CDA115619168`.
Focused log: `obj/chi-refill-validation/reward-plan-focused.log`.
All 27 quest/event fixture modes pass sequentially: **1,549 regression checks**.
Logs: `obj/chi-refill-validation/reward-plan-regression-*.log`. Twelve private
overlay checks and three producer-audit checks also pass. The producer inventory
remains 81/337 identities; this prerequisite adds no new achievement producer.

## Remaining direct-EXP work

The plan describes base progression and identifies several side effects. The
subsequent [level-up transaction checkpoint](EXPERIENCE_REWARD_TRANSACTIONS.md)
commits skills, initial Arena admission and reincarnation restoration with loaded
level profiles; mentor credit and the actual Dark Horn receipt remain open.
The preceding [reincarnation recovery checkpoint](REINCARNATION_RECOVERY.md)
fixes saved-EXP truncation, coordinates the three reincarnation reset constructors,
and atomically restores the profile with snapshot consumption. Complete reset
payment and the remaining side-effect transactions still need integration before
the reward can safely consume an issued Dark Horn. The first/second rebirth saved-level
policy also remains distinct from a verified saved-EXP restoration policy.

No native packet layout, installed client file, production row or live binary is
changed by this source checkpoint. Full native acceptance and deployment remain
separate roadmap gates.
