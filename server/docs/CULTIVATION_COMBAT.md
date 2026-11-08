# Cultivation and combat calculation corrections

These corrections apply to ordinary players and companions through shared server calculations. They preserve skill data and active class abilities. They do not establish identical damage between physical, ranged and magic attack types, or prove equal win rates for different skill rotations.

## Evidence and confidence

The installed 6711 `ini/jianghu_attribute_add.dat` (SHA-256 `518115a72517be210150e4d4876df9d65a7d0a811f168542b0d1562ea7c585c8`) decodes to 90 attribute rows. Every numeric ID/type/level/power tuple agrees with the runtime `database/Slayer/JianghuAttributes.txt`. No client content is added to Git.

The official [Chi guide](https://co.99.com/guide/guides/chi_system.shtml) confirms the attribute limits, immunity's role, physical critical multiplier of 1.5, magical multiplier of 2, full-score exception at 400, ranking tier endpoints, and 1 CP per 4 Chi points. The [JiangHu alignment guide](https://co.99.com/news/2013-07-25/age_of_jiang_hu_tip_ii_how_you_assign_suitable_attributes.shtml) confirms adjacency and the alignment stat bonuses. These are published rules, not a claim to possess the official server implementation.

Intermediate Chi ranks 5?14 use linear interpolation between the published rank-4 and rank-15 endpoints, with integer truncation of the amount deducted from the upper endpoint. That interpolation is Revora's explicit rule; exact official intermediate rounding is unverified. Existing UID tie ordering remains because the legacy server has no authoritative Chi donation tie-break record.

Combat mitigation order and Break bypassing higher-BP reduction are Revora rules. They use the existing physical/magical BP reduction curves and common rating units. Exact proprietary CO damage formulas are not claimed as verified.

## Fixed calculations

- Chi attributes accumulate serially, and one character's complete equipment recalculation is serialized. Concurrent additions can no longer overwrite one another.
- Critical and immunity values use the same percentage divisor as other Chi attributes. Rank zero and locked powers grant no rank rewards. Only score 400 earns the unconditional top-three bonus. Ranking endpoints match the published tiers, and online players losing top-50 placement have their stats recalculated.
- JiangHu alignment stops at an inactive gap and processes the next group without skipping its first star. The maximum score is stored as UInt32, so 81,000 does not wrap around UInt16. The existing 2703 writer already carries UInt32.
- Equipment minimum and maximum attacks are assigned to the correct fields. Inner Power final magical attack/defense are counted once, using their respective attributes.
- Hidden class-versus-class damage tables and repeated profession-specific critical rolls are removed. Each core attack path uses its appropriate critical rating minus immunity, capped at 0?100%. Critical multiplication precedes final talisman additions. Objective magic uses magic critical rating.
- Block chance uses its real percentage and halves damage. Two divide-by-zero blocks are fixed. Tortoise reduction works as a percentage and is applied to the core attack paths. The shield modifier that increased incoming damage now reduces it.
- Magic uses flat defense, cultivation defense and percentage defense instead of overwriting the defense result with raw attack. Break and Anti-Break use tenths-of-percent units and affect higher-BP mitigation consistently.
- VIP 5?7 critical bonuses are equal across classes. Fire Taoist no longer receives a hidden 100?130% extra magic critical rating. VIP 6/7 attack grants include the same numeric magical attack bonus as physical attack. Existing VIP health/defense and nobility bonuses remain.
- The shared melee dispatch retains the caller's skill coefficient. No skill database coefficients, cooldowns or class abilities were retuned.

## Controlled class comparison

Ten final class codes (15, 25, 45, 55, 65, 75, 85, 135, 145, 165), all 100 attacker/defender pairs, six core entry paths. Each has level 140, Serf nobility, no equipment/buffs/critical rating, physical attack 40,000, magic attack 40,000, physical defense 10,000, and neutral test skill coefficient (magic flat power 1,000).

| Entry path | Before: smallest/largest hit | After: all class pairs |
|---|---:|---:|
| Melee | 22,507 / 292,600 | 29,260 |
| Rango with skill | 2,571 / 36,000 | 3,600 |
| Rango basic | 3,600 / 3,600 | 3,600 |
| Ranged | 39,900 / 39,900 | 39,900 |
| Magic | 10,248 / 10,248 | 10,248 |
| Magico | 10,248 / 10,248 | 10,248 |

The different columns by attack type retain existing attack-mode scaling. The regression proves class codes no longer introduce arbitrary damage multipliers for equivalent inputs in these six paths. Gear, real skills, cooldowns, range, control and player execution still affect actual PvP results. Additional competitive tuning should be based on live match evidence.

## Validation and rollout

1,245 checks passed on the consolidated staged x86 server:

| Suite | Checks |
|---|---:|
| Combat (600 pair evaluations plus rating/defense scenarios) | 669 |
| Chi/JiangHu attributes, alignment, concurrent recalculation, VIP parity, Inner Power | 67 |
| Wardrobe requirements and persistence | 91 |
| Rune upgrades and rollback | 21 |
| Prestige | 58 |
| Character status and RVP updates | 41 |
| Companions | 57 |
| Chi refill pricing | 12 |
| JiangHu pricing | 19 |
| Potency, daily bosses and event regressions | 210 |

`tests/CombatBalance/Verify-Combat.ps1` uses disconnected ordinary-player fixtures. `tests/PotencyHunt/Verify-PotencyHunt.ps1 -CultivationOnly` loads the actual numeric stat tables and uses an isolated PostgreSQL schema. Existing title/rune tests verify ordinary-player persistence and failure rollback. No fixture modifies live player rows.

The release also corrects inventory RVP update attribute 91 and real 6711 title/wing requirements; see [protocol evidence](PROTOCOL_RESEARCH.md) and [wardrobe requirements](WARDROBE_REQUIREMENTS.md). No new client patch is required. Fresh in-game visuals and competitive win-rate balance are not asserted by these server fixtures.

Deployed 2026-09-15 01:10 UTC as server PID 8044. Live executable SHA-256: `9AECA0A931E29214B26514313F087C16890BEDE526289207B37049BB16319212`. Previous executable/PDB and post-shutdown Inner Power data are preserved under `bin/Debug/deployment-backups/cultivation-20260914-181016`. Startup validation confirmed both existing listeners and protocol handshakes, 1,000 live Potency Serpents, four daily boss schedules, and the larger Extraction map. Title reconciliation also preserves each original title row in its additive database backup table; executable rollback alone does not undo those rows.


## Reachable maximum Chi rolls (2026-09-26)

The production `FastRandom.Next(lower, upper)` excludes `upper`. Both Chi roll
ranges intended to reach the attribute cap previously passed `max`, making
all 100-point attributes impossible through ordinary pursuit and therefore
preventing the 1,600-total Grandmaster requirement. The highest range and
fallback range now pass `max + 1`. Intermediate ranges, sequential 4/10/40
branch checks, minimum values and costs remain unchanged. This adds the legal
maximum to each affected range; it does not guarantee a maximum roll or claim
that Revora's probability distribution matches the original server.

`GeneralData.ChiValue` delegates to the same `RollChiValue` used by deterministic
boundary fixtures. All 13 attributes are checked at both edges of all four
ranges (104 checks), plus four legal full-score powers totaling 1,600. The
cultivation suite passes 179 checks including 74 existing checks. Production
build succeeds with existing legacy warnings. Local evidence:
`obj/chi-roll-build.log` and `obj/chi-roll-tests.log`.

This closes a reachability defect, not the complete Grandmaster earning journey.
Ordinary unlock/pursuit payment atomicity, invalid mode/lock-mask handling,
reconnect recovery, immediate title reconciliation and native acceptance still
need their own integrated validation. No live deployment was performed.


## Ordinary Chi request admission (2026-09-26)

Normal sealed2533 requests now reject invalid powers, locked/unowned pursuit
powers, unknown lock bits, all-held attributes and short bodies before mutation
or automatic refill. Unlock and pursuit require the active ordinary player
session. The actual50-plus50-per-held-attribute cost replaces the fixed200-Chi
refill threshold: a player with exactly50 Chi can roll without buying a refill.

Build succeeds with existing warnings;90 jade/Chi checks pass (57 prior and33
new packet-route checks). A level110 ordinary player pays300 Chi to unlock,
then50 to pursue; the wallet and rolled attributes survive `ChiTable.Load`.
All15 valid masks preserve held attributes and charge exactly their cost.
Invalid requests leave CP and Chi unchanged, including at zero Chi where the
old dispatcher would attempt a refill. Local output:
`obj/chi-admission-build.log`, `obj/chi-admission-tests.log`.

The Grandmaster matrix row remains predicate-only: atomic unlock/pursuit
failure recovery, concurrency and immediate wardrobe reconciliation are not
proved by this successful-path fixture. Native acceptance and deployment stay
pending. See the matching Chi request section in `PROTOCOL_RESEARCH.md` for
source-layout evidence and its limitations.


## Atomic ordinary Chi progression (2026-09-26)

Unlock and pursuit now prepare separate power objects and commit the Chi
balance plus serialized power list in one database transaction. The character
session lease and inventory lock serialize mutation; database locks follow
character then Chi row. Saved level/rebirth, the previous Chi balance and the
previous serialized powers must match the loaded owner. Pending unsaved Chi
gains defer mutation. Local lock/statement timeouts bound database waits.

Only a confirmed commit installs the new balance/list. Exceptions, including
ambiguous commit or post-commit installation failure, fence the session so
background saves cannot overwrite a possibly committed result. Ordinary reload
recovers it. Unlock remains idempotent; each accepted pursuit is a separate
paid roll. The protocol supplies no per-pursuit request identity, so identical
later pursuit requests are not treated as transport-level replay receipts.
Busy leases defer without charging. Rankings/stats and the Chi balance packet
are sent after persistence, outside the actor's mutation lock; cross-player
stat refresh does not retain that lock. Attribute types are selected uniformly
from the remaining legal types, replacing the equivalent unbounded rejection
loop. Attribute-value roll ranges and lock costs are unchanged.

Build succeeds with existing legacy warnings.107 jade/Chi checks pass (90 prior
plus17 transaction/session checks): pre-commit rollback, database write failure,
post-commit recovery, subsequent save, concurrent unlock and pursuits, stale
power/profile revisions, pending gains, lease contention and session removal.
The179 cultivation checks also pass. Logs: `obj/chi-atomic-build.log`,
`obj/chi-atomic-tests.log`, `obj/chi-atomic-cultivation-tests.log`.

Remaining: an integrated four-power Grandmaster earning/title/reload journey,
immediate wardrobe reconciliation, native client acceptance and deployment.
Refill, retreat and other legacy Chi writers are separate from the new power
transaction; this batch does not claim complete transactional coverage of all
Chi features or a full restart/transport-failure acceptance test.


## Ordinary Grandmaster earning route (2026-09-26)

`VerifyGrandmasterEarning`, selected by `-GrandmasterPublicOnly`, starts an
ordinary eligible character with zero CPs, zero Chi and no powers/title. A
scheduled daily boss is killed through the combat entry point; its real bounty
funds the normal2533 refill (1,000 CPs for4,000 Chi). Four ordinary ordered
unlocks cost1,200 Chi and four pursuits cost200. The final full-score power
immediately grants Grandmaster; a normal wardrobe request equips it. Full
character plus Chi reload retains four400-point powers,2,600 Chi,999,000 CPs
and the equipped title. A normal lower pursuit costs50 and immediately removes
and unequips the now-ineligible title; another full reload preserves removal.

`PublishChi` now reconciles achievements/wardrobe after committed progression
under the current actor's session lease and owner lock. Value sampling has
internal deterministic test inputs; production defaults still call the same
FastRandom and rate functions. The fixture selects legal endpoints through the
production range sampler. It does not seed powers, title ownership, balances,
roll receipts or boss bounty receipts. Level110/two rebirths, event time,
lethal combat damage and random outcomes are controlled prerequisites; this
proves the server service chain, not the natural grind or live client behavior.

Build succeeds with existing legacy warnings.423 checks pass:406 shared first
rebirth/schema prerequisites and17 focused earning checks. The107 jade/Chi
checks pass against the same production build. Local logs:
`obj/grandmaster-build.log`, `obj/grandmaster-tests.log`,
`obj/grandmaster-jade-tests.log`. The wardrobe matrix now classifies Grandmaster
as code-validated; native client/Host acceptance and deployment remain pending.


## JiangHu payment admission and Study Point reward (2026-09-26)

Paid training previously added20 Study Points directly and then called
`UpdateStundyPoints(20)`, which added another20 while reporting20 to the client.
The duplicate increment is removed. If there is insufficient UInt16 capacity
for the whole20-point reward, training defers before charging; the exact
capacity boundary succeeds. This preserves the existing service's declared
20-point reward, not a newly asserted original-server economy rule.

Paid training and previous-star restoration now require a successful
`TrySpendConquerPoints` result before changing Jiang state. A stale saved CP
purse can no longer fail payment but still obtain a roll, counter/talent reward
or restored star. The old pricing harness now implements the actual boolean
payment method in its test double instead of failing compilation.

Build succeeds with existing warnings.113 combined jade/Chi/Jiang checks pass
(107 prior plus6 real database-backed Jiang checks). New checks load the normal
purse revision before payment and cover the10/20-CP ladder, a single20-point
award per training, failed training/restoration payment, and reward capacity.
The separate19 source-extracted pricing checks pass with stubbed persistence,
network and stats; they are not ordinary-player persistence evidence.
Local output: `obj/jiang-payment-build.log`, `obj/jiang-payment-tests.log`.

Talent remains predicate-only. CP spending and Jiang/subclass saving are still
separate operations; an atomic progression transaction, free-training recovery,
complete ordinary81,000-score journey, immediate title reconciliation, native
acceptance and deployment remain unfinished.


## JiangHu saved-state round-trip (2026-09-26)

The existing serializer writes daily usage at header7 and mode0 for enabled,
mode1 for disabled. Loading previously assigned header7 to the unused daily
allowance, narrowed it to a byte, and reversed the mode. Loading now restores
`FreeTimeTodeyUsed` as UInt32 and follows the writer's mode convention. The
serialized layout is unchanged; no database migration or inferred wire change
is involved. Already-lost historical daily usage is not reconstructed.

A shared score/alignment calculation now runs both after loading and before
applying combat attributes. Loading formerly summed base stars only, including
stars in inactive stages, and accumulated on repeat loads. Fully aligned saved
builds now load at81,000 instead of40,500 before login stat recalculation;
inactive stages/stars are excluded and repeated loads rebuild the same score.
The existing alignment/stat formulas remain unchanged.

Build succeeds with existing warnings.131 combined checks pass (113 prior plus
18 reload checks): actual Jiang table save/read for both modes and full-width
usage boundaries, repeat loads, a disabled stage and an inactive ninth star.
All179 cultivation checks also pass, including alignment lengths and combat
stat recalculation. The fixture supplies the star graph; it is serialization
and score evidence, not an ordinary81,000-point earning journey. Logs:
`obj/jiang-reload-build.log`, `obj/jiang-reload-tests.log`,
`obj/jiang-reload-cultivation-tests.log`.

Talent remains predicate-only. Atomic training/restore payment and progression,
the ordinary earning route, immediate title updates, native acceptance and
deployment remain open.


## JiangHu save ownership and durable revisions (2026-09-26)

Heartbeat/disconnect saves now require a healthy real owner, the current session
lease, matching registered Jiang object and matching UID. Mutation/save locks
follow owner then Jiang object. Exceptions fence the owner and block that Jiang
object from further saves. Background/global saves can no longer blindly write
an earlier serialized object over a newer database row.

Each loaded/created Jiang object retains its exact saved data preimage. Updates
compare it atomically in PostgreSQL and advance it only after confirmed success.
Shutdown iterates a stable registry snapshot. Offline administrator edits remain
supported under a session lease and the same preimage comparison; an offline
conflict blocks further writes until reload instead of retrying stale data.
This preserves the legacy editor while protecting newer committed revisions.
The blob format and schema are unchanged. Statements use parameters and a
five-second command timeout.

Build succeeds with existing warnings.140 combined checks pass (131 prior plus
9 save-ownership/revision checks): healthy save, fenced/fake/replaced sessions,
registry mismatch, legitimate offline edits, database failure, repeated global
save after failure, and an external revision protected against repeated offline
saves. The19 extracted pricing checks also passed after the lock-order change;
they still use stubbed dependencies. Logs: `obj/jiang-save-build.log` and
`obj/jiang-save-tests.log`.

This is a prerequisite for atomic training, not its completion. CP, Study Point
and Jiang mutation are still separate in the legacy training handler. Talent's
ordinary earning route and all native/deployment gates remain open.


## JiangHu atomic creation and training (2026-09-26)

Ordinary creation now commits the initial 100 Study Points and Jiang record in
one transaction. Repeated or concurrent creation cannot repeat the gift. An
existing blank legacy record initializes without another starting gift because
it cannot establish that the earlier gift was never claimed.

Free and CP training prepare a separate proposed roll, then commit CPs, Study
Points and serialized Jiang progression together. The existing price ladder,
free-training costs, allowed attribute catalog and stage-opening rule remain.
Each accepted training request is a separate paid action; no transport request
identity or retry deduplication is claimed. Owner/session admission, row locks,
purse and saved-blob preimages protect against concurrent or stale writes.
Only committed state reaches the runtime registry and client updates. Training
also reconciles wardrobe eligibility immediately. Creation name reads now
check their declared frame bounds before reading the existing source offsets.

A failure after commit fences the owner and Jiang object. Normal login reloads
that object's durable Jiang state alongside the saved purses; a rolled-back
creation clears its blocked placeholder. Background saves cannot overwrite a
committed result with the pre-commit object. Training preserves runtime timers
and settings and keeps a separate previous-star snapshot for legacy restore.

The isolated production build succeeds with existing legacy warnings. **161
combined database-backed checks pass** (140 prior plus 2 restore compatibility,
11 training transaction/recovery and 8 creation checks). Six separate pricing
checks exercise the production formula. These replace the old stubbed-handler
pricing suite; the database fixtures exercise actual handlers and persistence.
Evidence: `obj/jiang-atomic-build.log`, `obj/jiang-atomic-tests.log`, and
`tests/CultivationPricing/Verify-JiangHu.ps1`.

Restore payment and progression are still separate legacy operations. An
ordinary earned 81,000-point Talent journey remains unproven; Talent is still
predicate-only. Native client acceptance, current Host acceptance and deployment
remain open. No schema migration or live deployment occurred.


## Atomic restore and ordinary Talent earning route (2026-09-26)

Restore now uses the same owner lease, row locks, saved-state preimages and
transaction as training: its 20 CP fee and restored Jiang blob commit together.
The prior-star option is consumed only by the installed committed state; parallel
or repeated restore requests cannot charge again. Training action2 remains
rejected; restore continues through existing message2700 action10. The prior-star
option remains session-only, as before; an uncommitted restore option is not
promised across logout. Committed restoration survives reload without an option
that could replay its charge. Training counters, course, talent and Study Points
are unchanged by restoration. Successful restoration reconciles wardrobe titles.

The Talent fixture follows an ordinary account from zero CPs and no Jiang path:
actual scheduled boss combat pays 1,000,000 CPs; the normal creation packet starts
JiangHu; 81 normal paid-training packets fill nine stages in order. Controlled
legal maximum random outcomes choose one allowed attribute type per stage, so
each aligned stage contributes 9,000 and the last reaches 81,000. The actual
price ladder charges 28,250 CPs and leaves 1,720 Study Points (100 initial plus
81 rewards of20). Talent grants immediately, equips through its normal wardrobe
request and survives full character loading plus a fresh database Jiang load.
A normal lower roll immediately removes/unequips it; a 20 CP restore regains
eligibility, charges once, re-equips and survives another database reload.

**426 Talent checks pass**:406 shared first-rebirth fixture prerequisites plus20
focused route checks. **167 combined Chi/Jade/Jiang checks pass**, including six
new restore cases for rollback, committed-but-not-installed recovery, concurrency,
insufficient funds and invalid training action. Build succeeds with existing
legacy warnings. Evidence: `obj/jiang-restore-build.log`,
`obj/jiang-restore-tests.log`, `obj/talent-public-tests.log`; reproducible route:
`tests/PotencyHunt/Verify-PotencyHunt.ps1 -TalentPublicOnly`.

This proves the server earning route, not its difficulty or expected completion
time. Level140/two rebirths are supplied prerequisites; event time, combat damage
and legal random outcomes are controlled. No native client or real-time grind
was exercised. The working-tree build includes pre-existing owner changes.
Talent is now code-validated; Host/native acceptance and deployment remain open.
