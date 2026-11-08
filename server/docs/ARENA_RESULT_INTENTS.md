# Durable Arena outcome capture

The inactive result coordinator now saves an outcome intent before attempting
the complete result/statistics/reward transaction. The intent is a separate
committed record keyed by the admitted match GUID. It stores winner, loser,
first-capture UTC timestamp, Pacific day and a server-supplied cause. A failed
result bundle no longer erases this evidence or moves the result to a later day
when retried. Match-row and intent-row locks use the same order.

Capture requires the group lock, both owner locks, canonical participants on the
registered imported map, an already-ended group and exact participant outcome.
The admitted ledger row must match both identities and map. Duplicate captures
validate the original winner/loser and keep its timestamp/day/cause. A finished
legacy result without an intent cannot manufacture one through this path.
Suppressed inserts fail before the result bundle can run. This does not make a
database outage survivable by itself: an outcome whose capture never committed
still requires a lifecycle policy before cleanup can proceed.

Causes supported are Result, Defeat, Timeout, Forfeit and Disconnect. Current
coordinator calls use Result; fixture calls exercise the four explicit causes.
No production death, quit, timeout or disconnect hook is wired yet. Thus the
saved timestamp is first capture, not a claim that native event time is already
captured at the correct hook. The eventual hooks must call capture before loss
of canonical session/map ownership.

## Verification and remaining recovery

Focused tests preserve a ninth-win outcome across failed bundle insertion and
retry on the next day, retaining the original day and entitlement. Each explicit
cause survives completion. Removing a participant from the canonical pool leaves
a saved intent and active ledger row; completion correctly refuses to pretend
that an offline participant is still live. Restoring the same fixture session
allows completion. This is ownership-loss evidence, not full reconnect or restart
recovery. Failed capture records no result or fabricated intent.

Build and combined test totals appear in QUEST_EVENT_STATUS.md. Ignored log:
obj/chi-refill-validation/arena-intents-tests.log. Existing coordinator concurrency,
reward atomicity, custom policy and daily earning fixtures remain included.

Offline settlement still needs durable participant/statistic ownership, protection
against concurrent login/final saves, original eligibility/economy snapshots for
pending milestones, and startup treatment of abandoned active matches. Canonical
session removal cannot simply be bypassed in the current result participant.
Pending outcome consumption, departure hooks, reward recovery, ranking reset and
legacy writer replacement remain unfinished. This batch stays inactive in live
callbacks and makes no native/client/deployment claim.

Participant statistic preimages and original custom reward eligibility/economy
are now saved with capture ([snapshot scope](ARENA_OUTCOME_SNAPSHOTS.md)).
Offline settlement and lifecycle activation are still unfinished.
