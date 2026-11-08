# Bounded Arena startup reconciliation

The inactive startup recovery service now enumerates active matches in batches
of 1?128, ordered by captured outcome time or admission time. Captured outcomes
use the existing atomic recovery transaction. Uncaptured interrupted matches
receive a unique abandonment receipt with reason startup-without-outcome. They
award no win, loss, Arena-point change, Honor or item. This is an explicit Revora
interruption policy, not a claim about original Conquer restart behavior.

The original admission remains available for audit; closure is represented by
revora_arena_abandoned rather than deleting history or inventing a finished
outcome. Admission replay, terminal result persistence and late intent capture
all reject that closed identity. Startup enumeration excludes closed admissions.
Abandonment locks the admission, verifies no intent/statistic/bundle evidence
exists, and commits its own receipt. Failed receipt insertion leaves the match
open. Matches with inconsistent captured evidence stop recovery rather than
being silently discarded as abandoned.

Each match commits independently. Earlier successful records survive a later
batch failure and are excluded on retry. The caller must keep listeners and
Arena caches closed until all batches drain successfully; a nonzero return means
work was completed and another bounded call is needed. Existing startup guards
remain in force. No runtime/startup hook is activated by this batch.

## Validation

The focused fixture uses only revora_potency_validation. After prior cases finish,
it clears that disposable schema's Arena ledger and dependent receipts to test a
fresh mixed startup batch. It verifies a failed abandonment insert, a one-record
batch limit, mixed captured/uncaptured recovery, repeated empty passes, no rewards
for interrupted matches, rejected late admission/result/capture, incomplete
snapshot rejection and oversized batch rejection. Existing daily/custom reward
and cold recovery checks remain included. Totals are in QUEST_EVENT_STATUS.md;
ignored log: obj/chi-refill-validation/arena-startup-sweep.log.

Actual startup wiring before cache loads, capture before live departures, pending
payout delivery, daily ranking/day-reset replacement and full legacy-writer
cutover remain unfinished. No live process or database was changed. Native and
real process restart acceptance remain separate from the cold-state fixture.
