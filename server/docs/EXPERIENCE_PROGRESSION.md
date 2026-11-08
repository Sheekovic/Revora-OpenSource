# Experience arithmetic and durable reward prerequisites

2026-09-15. Numeric checkpoint `11b86d4`; subsequent planning/ownership checkpoint
based on that commit.

## Implemented numeric corrections

The public `GameState.ExpBall` value is now `ulong`. At level 135 the existing
server formula gives **28,396,442,314 EXP**, where its old `uint` return discarded
the high bits and returned 2,626,638,538. The formula and its floor-division order
are preserved. These are verified source arithmetic values, not a claim that the
server's entire level curve matches an official Conquer edition.

`DataHolder.LevelExperience` now uses the full stored table: levels 137, 138 and
139 require 1,174,030,000,000, 1,761,040,000,000 and 2,641,550,000,000 EXP respectively.
Previously all three used level 136's 547,859,950,814 threshold. Out-of-table lookups
still clamp; normal progression still stops at the configured level cap.

Fractional enlightenment, mentor, blessing and offline-training rewards use exact
integer scaling. The intermediate product may exceed 64 bits when the final result
fits; a final result outside `ulong` throws instead of wrapping. No float or signed
32-bit conversion is used in those paths. Their existing 600/606-unit conventions
are preserved rather than silently changing reward rates.

Offline training elapsed time is floored and clamped to 0–900 minutes **before**
conversion to `ushort`. Future dates award zero; long absences cannot wrap to a
small elapsed value. Its existing rate remains one ball per 300 training minutes,
up to three balls. Preview loops stop at the configured level cap. This is separate
from the documented quest reward convention of one ball per 60 minutes.

## Level planning and character ownership

`ExperienceMath.Advance` precomputes the level, remainder and ordered level-up
transitions using wide intermediate addition. Normal awards use that result.
First/second rebirth restoration retains its existing per-transition point policy;
the restored level now respects the configured cap. At or above the cap, a new
award still leaves the character unchanged. An unrepresentable EXP remainder or
free-point total fails before level-up side effects. Auto-hunt credit cannot wrap.

`IncreaseExperience` holds the same character owner lock as `SaveEntity` for its
whole read/modify/write. Fenced characters stop accruing unsavable experience.
The existing class-skill and level-120 base-stat behavior remain in place; this
does not silently remap Pirate's existing Monk stat profile or invent new values.

The 1024 attribute handler validates a complete payload and transport suffix,
widens the sum of all four requested deltas, and rejects target-stat overflow.
The authenticated canonical player's owner lock covers spending the free points,
all four stat writes and derived-stat refresh. Malformed, fake, unloaded, inactive
or fenced requests cannot allocate points. A repeated valid delta can spend
remaining points; without a verified nonce this packet does not distinguish a
network replay from another legitimate allocation. It cannot mint free points.

These operations still use the existing character-save lifecycle. The lock
prevents saves from observing a partially applied allocation or ordinary EXP
award; it does not make every level-up side effect one database transaction.
The subsequent [progression persistence checkpoint](PROGRESSION_PERSISTENCE.md)
adds loaded preimages, conditional saves and transactional profile application.
The subsequent [complete stat-plan checkpoint](EXPERIENCE_REWARD_PLAN.md) expands
that fixture to 137 checks; complete consumable delivery remains open.

## Validation

The isolated Debug/x86 build passes. `-ExperienceOnly` now runs 91 checks, including
every nonzero byte level against an arbitrary-precision oracle, explicit high-level
and rounding examples, overflow and timestamp boundaries, ordinary-player awards,
five production high-level crossings and the existing cap policy. Additional
checks cover nine class variants at levels 120/121, first/second rebirth restoration,
pre-mutation overflow failures, 2,000 concurrent EXP increments, all truncated
attribute frames, wrapped request sums, full destination stats and concurrent
allocation/level-up/save ownership.

The save fixture invokes production `EntityTable.SaveEntity`, concurrently reads
the stored five-field point total, and verifies final EXP, strength and unspent
points in the database. Its disposable table copies production column types and
synthetic fixture identities only; unrelated account-creation constraints and
production character data are not copied. This verifies the ordinary save path,
not full login recovery or transactional consumable reward delivery.

All 27 existing quest/event fixture modes pass sequentially: **1,549 regression
checks**, including 141 Dis City checks. Twelve private-overlay checks and three
producer-audit tests also pass. Audit coverage remains 81/337 earning identities;
this numeric prerequisite adds no new achievement producer. Regression logs are
kept under ignored `obj/chi-refill-validation/progression-plan-regression-*.log`;
the direct checks are in `experience-plan-final.log`. The final two focused checks
add auto-hunt overflow and detached-session rejection; the production assembly
is unchanged from the full regression run.

Current assembly SHA-256:
`D679155877616DD59832CE8C4E40A486983FCAC6AFF9DEF878DEC37D322EB8D1`.

## Work required before a consumable direct-EXP reward

Dark Horn must grant direct EXP worth five balls with an atomic item-use receipt,
not merely call the legacy award routine after deleting the horn. Its source
quantity is recorded in [Dis City exchange evidence](DIS_CITY_EXCHANGES.md).

The following existing writers explain why arithmetic alone does not complete it:

- The subsequent [level-up transaction checkpoint](EXPERIENCE_REWARD_TRANSACTIONS.md)
  now commits loaded level profiles, class skills, initial Arena admission and
  reincarnation snapshot consumption together. Mentor credit remains separate;
  a consumable cannot yet rely on the entire award being one transaction.
- Rebirth and reincarnation reset several progression fields. The subsequent
  [reincarnation recovery checkpoint](REINCARNATION_RECOVERY.md) preserves full
  snapshot EXP and commits its restoration/removal with the complete profile.
  Its constructors share the owner lock; complete reset payment and the other
  first/second rebirth writers still require their transaction review.
- Mentor prize totals are derived from apprentice rows at login. Online gains,
  claims and saves need consistent row ownership so a durable level-up credit is
  neither lost by a stale save nor repeated after reconnect.
- Offline training claims still need owned admission and a once-only durable
  claim. The two previews currently value all whole balls at the starting level,
  while the claim reevaluates each ball after level changes. The new numeric helper
  does not claim to resolve that pre-existing projection or replay defect.
- Additional free-point awards exist in Courier Loop, Path Hunt, Soul Trial and
  Revora Master. Coordinate them with the same character progression ownership;
  checking only the new NPC's transaction is insufficient.

Base stats and free points are now precomputed by the subsequent reward-plan
checkpoint. Next implementation should finish the complete set of side effects,
persist item consumption and the entire result together,
then update caches and packets after commit. Preserve legitimate level-up effects,
and cover failure rollback, ordinary non-GM use, cap behavior, rebirth, concurrent
save/allocation and reconnect recovery before enabling the Dark Horn exchange.

No new native packet layout, live deployment or installed client change is included
in this arithmetic checkpoint. The complete Dark Horn milestone remains open.
