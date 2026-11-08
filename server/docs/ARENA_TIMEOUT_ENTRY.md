# Owned Arena timeout completion

The inactive coordinator now exposes EndTimedOut. An unfinished imported match
must reach three minutes from ImportTime, not the earlier matchmaking CreateTime.
Signed elapsed subtraction handles the 32-bit millisecond counter wrapping during
that short interval. Canonical map/session ownership and nonblocking acquisition
of both owner locks remain required. The winner is selected from accepted damage
under the match lock, then frozen and committed before result notifications.
Equal damage preserves Revora's existing player-two tie rule; no retail tie rule
is claimed. Duplicate callbacks cannot alter the saved outcome or cause.

A callback encountering an already-ended RAM outcome does not invent a new timeout
or departure cause: it uses generic Result unless an earlier durable intent already
supplies the cause. Existing durable causes remain immutable.

UpdateDamage now accepts only the exact canonical participants of a started,
owned, unfinished match. Updates serialize under its lock and saturate the 32-bit
wire total instead of wrapping. Score packet construction happens under the lock;
sending happens afterward because Send can synchronously trigger disconnect.
Packet layouts are unchanged. The generic End() also freezes its outcome before
notification, using the shared participant-safe FreezeOutcome path.

Build and combined test totals are in QUEST_EVENT_STATUS.md. Focused tests cover
foreign score rejection, concurrent score accumulation, old CreateTime with a
not-yet-expired ImportTime, exact deadline, counter wrap, concurrent completion,
late damage, score saturation, the preserved tie rule and pre-existing RAM outcome
cause handling. The fixture advances the match clock; it does not claim native
three-minute combat acceptance. Ignored log:
obj/chi-refill-validation/arena-timeout-tests.log.

Live CheckGroups still uses its legacy timeout/export/payout flow. Switching that
flow, live departure hooks, bounded deferred cleanup and final-save ownership must
be completed together. No live coordinator initialization, restart or deployment
occurred in this batch.

A subsequent deferred-departure change makes EndTimedOut process an already
accepted departure before applying timeout eligibility/damage selection. Such a
result retains the departure cause and observed time; it is not labeled Timeout.
See ARENA_DEPARTURE_ENTRY.md#deferred-departure-requests.
