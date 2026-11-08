# Arena outcome recovery snapshots

Outcome capture now saves both participants' pre-result Arena points, daily
wins/matches, lifetime wins/losses and VIP alongside the configured custom prize
amounts, minimum VIP and optional loss policy. Each snapshot belongs to the saved
intent and participant UID. Source statistics and VIP are checked against the
saved rows under owner locks; both snapshots and the intent commit together.
Failure to insert either snapshot rolls back the intent. Existing captured
intents cannot silently acquire a newer snapshot on retry.

The result-statistic transaction checks the captured preimage before changing
points or counters. A later legitimate statistic change is preserved and forces
reconciliation instead of being overwritten. Custom milestone decisions use the
captured VIP and economy, while still validating current canonical ownership and
saved/current VIP consistency. Changing VIP or configuration after a failed
hundredth result cannot alter the original prize. Payout still uses the resulting
immutable entitlement, with its existing transaction/replay protection.

Build and 131 combined Arena checks pass. The hundredth-win fixture captures VIP
5 and 5,000 CPs, changes the player to VIP 0 and policy to VIP 6/123 CPs, fails the
entitlement write, then retries and delivers the original eligible prize. Added
checks roll back failed snapshot inserts and reject a changed statistic preimage.
Ignored log: obj/chi-refill-validation/arena-outcome-snapshots.log.

This remains inactive infrastructure. Offline settlement, startup abandonment,
login/final-save coordination, departure hooks, daily ranking settlement and
replacement of legacy writers remain open. Snapshots make that recovery possible;
they do not themselves execute it. Older incomplete intents without snapshots
fail closed rather than inferring historical eligibility. No live deployment.

Per-result startup settlement is now implemented and fixture-validated
([recovery scope](ARENA_STARTUP_RECOVERY.md)); enumeration, abandoned matches
and live lifecycle integration remain unfinished.
