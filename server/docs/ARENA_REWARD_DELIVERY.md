# Bounded pending Arena reward delivery

The character timer now calls a gated ArenaRewardDelivery.Tick. The gate is
initialized only by the new Arena result coordinator, which is not initialized
by production startup yet. Therefore this hook does not enable new payouts
alongside the current legacy writers.

A healthy canonical session probes at most once per thirty seconds, using one
query for pending daily, custom and ranking entitlements. It attempts at most
one payment from each family per interval. Payments retain their existing owner
locks, saved balance checks and atomic payment receipts. A full bag can retain
an item reward while an independent ranking Honor reward remains deliverable.
A newly created session has no timer state and can probe on its first character
tick. Database receipts, rather than the timer, prevent replay across sessions.
The schedule is weakly owned by the session and cannot retain disconnected
players indefinitely. Busy player locks return without blocking the timer.

Clock rollback permits one immediate fresh probe, then resumes the interval.
A failed read-only probe logs only UID and exception type and leaves the session
healthy for a later retry. Payment failures retain their stricter persistence
fence; later families are skipped and the character callback returns before its
heartbeat save when that fence is set.

Build and focused test totals are in QUEST_EVENT_STATUS.md. Tests exercise a
real earned daily entitlement, full-bag retention, interval limiting, concurrent
later delivery, rollback of the clock and no duplicate grant. Ranking delivery
also uses the dispatcher for an earned ranking entitlement. Full native login,
production reconnect and actual timer scheduling acceptance remain open; fixtures
invoke Tick directly with ordinary canonical characters.

Ignored log: obj/chi-refill-validation/arena-delivery-tests.log. Arena day-reset
ownership, lifecycle hooks and legacy writer replacement remain unfinished.
No live deployment or server restart occurred.
