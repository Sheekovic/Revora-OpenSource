# Staged solo and team shutdown retention

Solo Arena now shares the per-session `IArenaShutdownRetention` contract with
Team Arena. `GameState.TryBeginShutdown` consults that owner before consuming its
one-shot shutdown flag. Unattached sessions keep their existing behavior.

`ArenaDisconnectRetention.Attach` validates and locks the admitted original pair,
rejects conflicting ownership, and reserves bounded completion capacity before
assigning the session owner. Its round-robin tick drives the existing solo result
coordinator and completion pump. A requested disconnect preserves the canonical
actor and reconnect barrier until the exact match has durable result proof.

After settlement, requested shutdown resumes; a healthy unrequested opponent
stays online. A fenced opponent is disconnected after recovery so invalid caches
cannot resume play. A resume callback that leaves shutdown unconsumed and the
original registration present stays attached for retry. Retention continues
through owned map cleanup, without repeating the shutdown or result rewards.

Validation uses ordinary admitted actors and the actual one-shot gate. The 622
combined Arena checks include a busy opponent lock, rejected result writes,
recovery of both fenced participants, no-op callback retry, healthy opponent
preservation, reconnect-barrier removal, one-shot teardown, map cleanup and a
production statistics reload proving the departing player's saved loss. The
isolated build passed. Logs are ignored local artifacts:

- `obj/chi-refill-validation/solo-retention-build.log`
- `obj/chi-refill-validation/solo-retention-tests.log`

The fixture callback consumes the shutdown gate and releases registrations; it
does not exercise native socket teardown or all ancillary logout services.
Neither retention service is created or attached by live composition yet.
Automatic attachment must cover the match-entry race and capacity failure, and
runtime composition must own recurring ticks, bounded shutdown drain and the
coordinated replacement of legacy result writers. Missing-owner recovery and
native disconnect/reconnect acceptance also remain open. No deployment, restart
or production schema change is part of this batch.

See [team lifecycle](ARENA_TEAM_LIFECYCLE.md#staged-team-shutdown-retention-at-the-one-shot-boundary)
for the team-specific roster and interrupted-admission behavior.

## Solo recovery with one invalidated participant

The completion pump already routes ended matches to recovery when either actor
requires reload. Recovery now accepts a mixture of fully fenced and healthy
original participants, instead of requiring both to have failed together. It
locks and validates both canonical owners, statistic identities and cache states
before changing either cache. Busy, replaced and partially invalid owners are
rejected without invalidating the healthy peer.

Before fencing the remaining healthy participant, recovery requires the retained
departure to agree with the loser, or an existing durable intent matching the
match and outcome. An unobserved RAM ending cannot authorize this transition.
Repair then uses the existing strict saved-preimage transaction; both sessions
remain fenced for reload, including after a failed write. A complete saved bundle
is recognized on replay without duplicate counters or rewards.

The fixture covers preserved durable intent, replaced and busy owners, failed
bundle insertion and rollback, successful retry/replay, ordinary statistics
reload and a RAM-only ending with no recorded evidence. Runtime attachment and
native disconnect gates above remain unchanged.

Validation: isolated build and 629 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/solo-mixed-build.log` and
`obj/chi-refill-validation/solo-mixed-tests.log`.

## Recovery before a retained departure becomes terminal

A busy original participant can delay freezing an observed disconnect. If a
cache fails during that delay, the ordinary completion path rejects the fenced
actor. Previously the pump only selected recovery for an already-ended match,
leaving this sequence unable to settle.

The pump now routes any fenced pair to recovery. For a still-active match,
recovery requires an existing retained departure with a UTC observation and an
original participant identity. It validates and locks both canonical owners
before freezing that original loss. The usual fenced capture then preserves the
recorded cause/time and repairs the complete result transaction. A cache failure
without a recorded departure cannot invent a winner or loser.

The fixture records a real busy disconnect, invalidates the opponent, verifies
replacement-session rejection before outcome mutation, suppresses result-bundle
insertion, then retries through the actual completion pump. It checks the saved
disconnect cause, original loser, ordinary statistics reload and owned map
cleanup after registration release. An active unobserved match remains unchanged.
These are staged component checks; automatic admission attachment and native
shutdown/reconnect acceptance remain open.

Validation: isolated build and 637 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/solo-pending-build.log` and
`obj/chi-refill-validation/solo-pending-tests.log`.

## Normal timeout after cache failure

An active fenced match with no retained departure remains undecided until its
normal duration expires (three minutes for solo, now
[five minutes for team](ARENA_OWNED_SCHEDULING.md#five-minute-team-arena-duration)).
Both solo and team recovery then use their
existing damage comparison and tie rule after locking and validating the original
owners. Earlier retained departures still take precedence. This closes the case
where rejecting all fenced owners from normal timeout completion otherwise left
an admitted match permanently pending.

Solo recovery records a UTC Timeout observation before capture, preserving its
cause across retries. Team recovery invokes its existing timeout freeze, retaining
the outcome timestamp. Both paths use the existing atomic fenced settlement,
leave invalidated sessions requiring reload, and permit map cleanup only after
the normal durable-result and registration checks.

Fixtures cover one millisecond before and exactly at the deadline, damage wins,
ties, clock wrap, replay rejection and cleanup in both modes. Existing departure
and rollback fixtures remain part of the combined suite. This extends the earlier
unobserved-match rule: cache failure alone cannot select an outcome, but a valid
expired match clock supplies the ordinary timeout event. Live integration and
native-client acceptance remain open.

Validation: isolated build and 665 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/fenced-timeout-build.log` and
`obj/chi-refill-validation/fenced-timeout-tests.log`.
