# Durable Arena daily ranking snapshot

The inactive Arena migration now records one immutable ranking and Honor
entitlement per eligible player/day. Eligibility comes from finished result
receipts that day, not cached TodayWin/TodayBattles. Points come from each player's
last committed result on that day, so later-day activity cannot rewrite history.
Equal points use ascending UID as the explicit Revora tie rule.

Result receipts now carry a sequence and ordering_verified flag. New online and
startup-recovered results set the flag under their owner transaction locks.
Existing migrated receipts default to unverified: settlement refuses to invent
historical order. A sequence generated during migration is not treated as proof
of the original order. This resolves equal outcome timestamps for new receipts.

The original Revora formula pays (1001-rank)*1000 Honor. The positive range is
preserved for ranks 1?1000 (1,000,000 down to 1,000); later ranks receive zero.
This bounds the old unsigned-wrap defect. These are Revora economy values, not
claimed retail Conquer ranking rewards. Both ranking and source result are stored.

Close(day) requires a completed Pacific date and blocks result-table writers
while it checks unresolved outcomes and snapshots all rows. Any pending outcome
on or before that day must first be recovered or explicitly closed. Suppressed
rank inserts roll back the day seal too. Repeated or concurrent close calls do
not rewrite an existing day. Admission checks the seal before writing and again
before commit; finish/startup recovery also reject a sealed result day.

Build and combined test results are recorded in QUEST_EVENT_STATUS.md. Fixtures
cover range boundaries, current-day rejection, pending outcomes, unverified
historical ordering, failed inserts, concurrent snapshot creation, equal timestamps,
equal points, exclusion of later-day participants, consecutive days and late
admission rejection. Ignored log: arena-ranking-settlement.log under
obj/chi-refill-validation/. Fixtures use actual coordinator results for ranking;
no native client or production database is involved.

This batch records entitlements only. Ranking Honor payout, past-season client
projection, point-fill/day-reset ownership, day enumeration, live departure hooks
and coordinated legacy-writer replacement remain unfinished. No live initializer
or listener callback invokes the new Arena coordinator. The existing legacy reset
still runs until cutover; this file does not claim its runtime bug is fixed yet.

Atomic Honor delivery and server-side earned-Honor shop purchases are now
implemented and fixture-validated ([payout scope](ARENA_RANKING_PAYOUT.md)).

## Prepared runtime settlement

The solo match scheduler now calls `ArenaRankingSettlement.Tick` after draining
owned completions. Joint startup preparation must have succeeded. One process
gate permits at most one probe every thirty seconds, with one completed Pacific
day per probe, oldest first. The existing transactional snapshot owns replay
protection; the schedule itself never grants Honor or mutates a player cache.

An unresolved admitted result keeps its day open. A failed snapshot rolls back
its seal and rewards, logs the exception type and retries after the interval.
It does not abandon a match to force a ranking. A backward clock permits a fresh
probe followed by normal throttling; already sealed days remain immutable.
Pending Honor then reaches the existing character reward-delivery hook.

The ordinary-player fixture uses coordinator results and the real match
scheduler, an unresolved cross-midnight match, a suppressed reward insert,
concurrent retry, next-day signup with previous-rank projection, Honor delivery,
later-day settlement and clock rewind. Scheduler isolation removes unrelated
fixture groups; this is not native-client or full-process acceptance.

Production startup remains inactive. Players queued across midnight, team
ranking policy, remaining lifecycle callbacks and native validation are still
open. No production schema change, deployment or restart is included.

Validation: isolated build and 835 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/scheduled-ranking-build.log` and
`obj/chi-refill-validation/scheduled-ranking-tests.log`. The first run exposed
old ranking seals retained by the fixture's incomplete table cleanup; the
corrected disposable setup explicitly clears both matches and ranking days.
Automatic delivery and coordinated runtime activation remain unfinished.
