# Canonical Arena departure entry point

The inactive coordinator now exposes EndForPlayer for Defeat, Forfeit and
Disconnect. It accepts only the exact registered participant session, acquires
the match lock and both owner locks without waiting, then freezes the outcome
and commits it through the existing intent/result/reward bundle. Busy returns
without changing the RAM outcome. Removed or replaced canonical sessions are
not accepted. A later departure cannot reverse an already committed outcome.
The first saved cause is retained through duplicate callbacks.

Outcome freezing performs no packet send. The coordinated entry sends win/loss
notifications only after the result transaction commits. This matters because
GameState.Send can synchronously call Disconnect on a transport failure, which
would otherwise remove canonical ownership before capture. Statistic projection
still follows commit. The existing public End(loser) now also validates exact
participant reference and freezes Done before notifications, fixing its former
fallback that treated any foreign session as player two. Packet layouts did not
change.

Build and combined test totals are in QUEST_EVENT_STATUS.md. Fixtures cover all
three departure causes, outsider rejection, busy-lock delay, concurrent callbacks,
immutable winner/loser, cause persistence, failed capture with both sessions fenced,
removed-session rejection and refusal to use a player departure as a timeout.
The fixture invokes the coordinator directly, not a real socket disconnection.
Ignored log: obj/chi-refill-validation/arena-departure-tests.log.

Live death/quit/transport hooks do not call this entry yet. Callers still need a
bounded deferred-cleanup path for Busy, retention/reconciliation for failed
capture, committed-result export after session removal, and protection from
legacy final-save/result writers. An outcome that never reached durable capture
cannot be claimed recovered by the startup intent service. Those are required
activation work, not completed features. No server restart or deployment occurred.


## Deferred departure requests

EndForPlayer now retains the first valid request in one immutable per-match slot,
including the exact departing GameState, explicit cause and observed UTC time.
A short independent gate coordinates request acceptance with FreezeOutcome; it is
released before acquiring the match or owner locks and performs no database work.
Thus a busy match lock as well as a busy owner lock can return Busy without losing
the request or releasing session ownership. Requests are bounded to one per match;
there is no unbounded global callback queue.

RetryDeparture and the coordinator timeout pass prioritize that request. Competing
forfeit/disconnect calls cannot select another loser. Pending requests block score
updates and spectator entry; an intervening legacy End respects the first request
and leaves notification to the coordinator after commit. A foreign End caller is
still rejected. CaptureAt persists the original observed time/day once owner locks
are available, so a retry across midnight cannot move the result to another reward
day. Statistics/VIP/economy snapshots are captured under the existing owner locks
at durable capture; the request slot itself is not a durable snapshot.

Combined tests cover busy match and owner locks, competing requests, later damage
and spectators, foreign ending, timeout processing, midnight retry, durable cause/
day, replay and an intervening legacy ending. Ignored log:
obj/chi-refill-validation/arena-deferred-departure-tests.log. Notifications use
existing packets; no native UI acceptance is claimed.

The slot is in memory until intent capture succeeds. It cannot survive a crash
before that capture, and a failed capture requires the strict fenced-owner recapture path described
in ARENA_STARTUP_RECOVERY.md#retrying-an-initial-capture-failure. Actual
Disconnect must retain canonical ownership and its login barrier until processing
is safe; it must schedule retries before consuming the one-shot final shutdown
phase. This batch does not wire that shutdown pump or activate the result
coordinator. Never treat a stored RAM request or Busy as permission to tear down
the session or claim a durable result.
