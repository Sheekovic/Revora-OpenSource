# Team Arena result-day counters

Solo Arena already reconstructs daily wins and battles from receipts on the saved
outcome date. Team Arena previously incremented cached daily counters. A fight
finishing after midnight could therefore project yesterday's counts even though
the team daily reward service correctly counted only today's saved results.

New team captures use counter version 2. Before capturing the result, the service
counts that participant's complete saved team rosters on the outcome's Pacific
date and adds this match. It stores those daily counts with the existing point
and lifetime projections. The result transaction recomputes and verifies the
capture before committing every participant and daily entitlement together.
The current run is excluded from the count, including when its result header has
already been inserted inside the transaction.

Existing intents acquire `counter_version = 1` when the schema is initialized.
They retain their captured increment arithmetic; new inserts explicitly use
version 2. Points, transfer amounts, lifetime totals, Honor and daily reward
thresholds are unchanged. This is a Revora consistency correction, not a new
claim about historical retail timing or monthly team point rules.

Unfinished recovery checks the captured version and saved-day counts while
holding all participant database rows. It uses the captured outcome date even
if recovery runs on a later day. Finished replay verifies the saved bundle rather
than recomputing its historical counts against newer matches. It therefore
cannot replace an old receipt with a later same-day total.

Fixtures exercise twenty earned team results followed by a new day, admitted
members, an imported match ending after midnight, rolled-back settlement, delayed
cold recovery, fabricated counter rejection, ordinary statistic reload, later
same-day results, finished replay and an explicitly represented version-1
capture. The cold fixture isolates registries inside the test process; this is
not a native reconnect or full server restart.

The existing staged startup boundary remains inactive in production. No live
schema change, deployment or restart is included. Team roster preparation before
entry, monthly/shared point policy, exact ranking awards, remaining invitation
callbacks and native-client acceptance remain open.

Validation: isolated build and 853 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/team-result-day-build.log` and
`obj/chi-refill-validation/team-result-day-tests.log`.
