# Arena result transaction coordinator

The coordinator combines terminal match storage, both player statistics, daily
milestone entitlements and custom prize decisions into one transaction before
export. It remains inactive: no production callback or initializer invokes it.
This preserves the requirement to replace legacy writers together with lifecycle
recovery, rather than paying alongside them.

Complete acquires the group lock and tries both owner locks in stable UID order.
It never waits for an owner while holding the group lock, because combat/quit
can hold an owner lock before entering match operations. Busy returns without a
write and can be retried. Only canonical participants on the registered owned
map with a terminal result can proceed. Both locks remain held through commit
and statistic projection. Errors fence both sessions, including projection errors
after commit; no reward is delivered from this method.

A final revora_arena_result_commits receipt proves that all transaction
participants completed. A repeated callback returns AlreadyCommitted only with
that receipt, matching winner/loser/participants and both statistic receipts.
A ledger result written by an incomplete migration helper is not sufficient.
Exported or not-ready groups cannot authorize completion. Payout occurs separately
after export/revival, using saved entitlements and their own payment receipts.

## Validation

Build and 114 combined Arena checks pass. New coordinator checks cover concurrent
completion, duplicate recognition, nine registered fixture-driven wins, rollback
at the final bundle receipt (including ninth-win entitlement), post-export payment
of earned 500 Honor and EXP Ball, exported/partial-result rejection, and a busy
owner lock followed by a successful retry. Existing custom reward tests include
hundred-win and fifty-loss policies. Ignored log:
obj/chi-refill-validation/arena-coordinator-tests.log.

Failed-result retry clears the fixture fence only; it is not a full reconnect.
The earned Honor test uses actual import/start/end/transaction calls with fixture
outcomes; it is not native combat or a complete signup-to-Honor-shop journey.

## Required lifecycle integration

Read-only source inspection confirms Shutdown saves Arena statistics and removes
the player from Kernel.GamePool before the later QualifierGroup.End(this) call.
That End currently records only RAM outcome. The current ledger requires canonical
sessions still on the imported map, so invoking Complete at the old cleanup point
would reject the disconnected participant. CheckGroups also currently exports
before calling legacy Win, destroying map ownership before result persistence.

Integration must capture a server-owned forfeit intent before session removal,
retain/recover pending outcomes across failed writes, prevent final saves from
overwriting committed results, and handle restart-abandoned active matches.
Invitation cancellation must not earn results. Kill, timeout, explicit quit and
disconnect need distinct evidence-backed paths. The daily reset/ranking writer
and ordinary payout recovery must migrate with activation. Until those paths
exist, this coordinator must not be wired into only the convenient success path.
Native/client and live restart gates remain open; no process was restarted.

Outcome capture is now a separate durable transaction before this bundle
([intent scope](ARENA_RESULT_INTENTS.md)). A later retry uses its original
Pacific timestamp; departed participants still require offline recovery.
