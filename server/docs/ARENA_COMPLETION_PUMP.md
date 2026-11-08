# Bounded Arena completion retries

ArenaCompletionPump is a staged composition-root service. Each instance has a
validated capacity of 1?4096 matches and deduplicates by immutable match identity.
Track accepts an owned imported match or a match with established durable result
proof. Capacity rejection does not mutate/release the rejected match; the caller
must retain it and retry admission to the scheduler. One future host instance must
own this responsibility before enabling the result lifecycle.

Tick accepts a work budget of 1?128 queue entries. It uses a rotating queue and
visits each selected entry at most once per pass, so a busy match cannot starve
a later match. Queue metadata locks are not held during database work. Concurrent
ticks do not process the same queue simultaneously. Pending departures, ordinary
ended results, timeouts and fenced recovery dispatch to their existing coordinator
paths. Busy/NotReady entries retry after one second. Failed operations back off
1, 2, 4, 8, 16, then 30 seconds, measured after the attempt. Clock rollback permits
one fresh attempt; repeated ticks at that same time still obey the new delay.

Only Committed/AlreadyCommitted result evidence sets the exact group's sticky
DurableResultReady flag. Once set, later passes skip result processing and retry
cleanup only. This lets the host observe result readiness even after the queue
entry is removed. Fenced players remain registered until the host releases them;
cleanup retains the entry while those owners still need teardown. Only successful
cleanup removes the queue entry. Failure and backpressure never discard results.
The scheduler itself neither closes sockets nor releases player registrations.

Tests cover capacity and deduplication, a busy predecessor with a successful later
match, retry timing, released capacity, completed re-tracking, injected bundle
failure counts through a nontransactional fixture sequence, backoff, clock rollback,
fenced result recovery, readiness before teardown and cleanup after registration
release. They exercise real result/recovery transactions and shared cleanup, not
native-client or actual socket shutdown. See QUEST_EVENT_STATUS.md for totals.
Ignored log: obj/chi-refill-validation/arena-completion-pump-tests.log.

The scheduler is compiled but not constructed/ticked by the live host yet. Startup
preparation, admission/rollover, deferred socket shutdown and replacement of every
legacy/shared result writer must be coordinated before activation. In particular,
legacy Export must not remove a queued uncommitted match, and a single player
fenced by an unrelated service still requires explicit departure handling; the
pump does not invent authority to recover healthy/unowned participants. No live
deployment, restart or client changes are included.
