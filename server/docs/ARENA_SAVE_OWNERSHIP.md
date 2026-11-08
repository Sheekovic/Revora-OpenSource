# Arena statistic save ownership

After the managed rollover schema is initialized, an outcome snapshot, committed
result statistic or day-rollover receipt establishes durable ownership. Including
snapshots protects the pending recovery preimage before the result bundle commits.
The same predicate protects the existing loader and legacy reset path.

For a managed canonical player, both ArenaTable.SaveArenaStatistics overloads
save only level, class, name and model. Points, current/history Honor, current and
previous season counters/rank and point-fill time remain owned by their durable
transaction participants. The team saver preserves its independent team counters
and identity metadata but cannot overwrite shared points or Honor. An absent or
replaced managed session cannot save even metadata. Existing owner locks serialize
these saves against in-process match, payout and rollover transactions; connection
reuse avoids a second connection for the ownership query. A snapshot owner index
keeps that lookup from scanning match history. Unmanaged saves retain
their previous behavior.

This is protection against stale saves, not cache reconciliation. A wrong cache
must still reload before spending or entering another owned match; transaction
preimage checks continue to enforce that rule. These protections do not activate
the coordinator or rearrange the live disconnect sequence. Before activation,
all remaining writers of shared points/Honor, including team reward paths, must
be reconciled with durable ownership. Dropping a legacy RAM-only award is not an
acceptable replacement for migrating that earning path.

The combined disposable-schema fixture covers unmanaged compatibility, captured
but unfinished outcomes, both save overloads, identity metadata and independent
team counters, production reload, committing the protected snapshot, concurrent
stale saves after result commit and a detached late save. It deliberately changes
fixture caches to prove they cannot overwrite durable values; this is not an
actual socket disconnect/reconnect test. See QUEST_EVENT_STATUS.md for the passing
total. Ignored log: obj/chi-refill-validation/arena-save-ownership-tests.log.
No live deployment, restart or client change is included.

## Legacy team calculation correction

The existing TeamQualifier.Win path now uses a locked result projection with
saturating point arithmetic. Losses subtract at most the current balance rather
than wrapping an unsigned integer and treating every balance above 80,000 as
invalid. Wins preserve the existing random leader award of 30?49 points and
member award ten points lower. Both outcomes count one completed battle;
win/loss counters saturate at their packet field limit. Removing dangling reward
conditions also restores the normal statistic send for each teammate.

The combined fixture exercises the projection called by Win, boundary balances,
win/loss counters, both ordinary legacy saves and the production team row loader.
It does not simulate a full team fight or make results atomic: frozen team
membership, durable result identity, replay protection, recovery and integration
with managed solo points are still required before lifecycle activation. Log:
obj/chi-refill-validation/team-result-bounds-tests.log.

Team result receipts now also establish managed ownership once the inactive team
transaction participant is initialized. Both team saver overloads then preserve
all result/season fields, points and Honor; canonical identity metadata remains
saveable. Team reset is owner-locked and skips managed/invalidated statistics.
Shared solo loading and reset preserve team-owned points even before solo
rollover initialization. See ARENA_TEAM_LIFECYCLE.md for validation and remaining
recovery/day-boundary integration. Production activation remains pending.
