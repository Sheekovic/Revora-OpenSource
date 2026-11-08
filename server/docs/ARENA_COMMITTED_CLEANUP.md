# Arena committed-result cleanup

The inactive result coordinator exposes Cleanup independently of Complete. It
requires a finished match with the exact original participant and outcome IDs,
the complete transaction bundle marker and both statistic receipts. An intent,
an active admission or a terminal ledger alone cannot authorize cleanup.

Cleanup takes the match lock and nonblocking owner locks for both fighters and
the current spectator snapshot. Busy returns without changing player state.
Only canonical sessions still referring to this match are eligible for mutation.
Only those still on its exact map are returned and, for fighters, have their
original PK mode restored. Already-departed players are not teleported, revived
or assigned an old PK mode. Replaced sessions and later match references are left
alone. Spectators receive the existing watcher-close packet after detachment.

A fenced player still on the map retains the map and retryable cleanup. Earlier
successful departures need not be repeated. After processing owners, cleanup
conditionally removes the exact group registration, disposes only its owned map
and marks completion. Repeated callbacks cannot affect a subsequent match.
No rewards or balances are written by cleanup. Database/cleanup exceptions return
Failed and log only the exception type; the durable result remains available.

This entry remains inactive alongside the result coordinator. The subsequent spectator ownership batch also routes legacy Export through the
shared cleanup and serializes spectator admission/removal; see
ARENA_SPECTATOR_OWNERSHIP.md. Durable-result cutover still must drain busy cleanup,
coordinate disconnect/final saves and replace legacy result writers.
The current fixture tests direct owned cleanup, not the live spectator race or a
native-client fight/reconnect. These remaining gates are not completed features.

Validation uses the combined Arena fixture in a disposable database schema; see
QUEST_EVENT_STATUS.md for the latest successful count. Focused cases include
uncommitted and incomplete-bundle rejection, busy owners, concurrent cleanup,
later matches, already-departed and replaced sessions, spectator return and a
fenced second fighter followed by a partial-cleanup retry. The fence is reset by
the fixture only; this does not claim production reconnect recovery. Ignored log:
obj/chi-refill-validation/arena-cleanup-tests.log.

Durable cleanup also resets the original fighters' solo invitation, acceptance,
status and Arena state, then sends the statistic update. Queue removal compares
both the key and exact session reference, so departed entries can be removed
without removing a replacement session. Earned balances and team state remain
unchanged. A different invitation or a canonical fighter awaiting persistence
reload defers cleanup, including when that fighter has already left the map.
Legacy Export retains peer references for its existing Win writer; only the
coordinator requests durable state reset. The focused fixture checks these
boundaries in arena-cleanup-state-tests.log. Live admission and disconnect
integration remain pending.
