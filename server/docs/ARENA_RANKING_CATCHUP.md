# Missed-day Arena ranking catch-up

The inactive ranking service now exposes ClosePending(limit), accepting 1?31
played days per call. It selects unsealed finished-result days older than the
current Pacific date, oldest first, and delegates each to the atomic Close(day)
transaction. Current-day matches stay open. Empty unplayed dates do not create
rewards. A repeat pass finds no already-settled days.

Each day commits independently, so completed earlier days survive a later failure.
Failures are not swallowed or skipped: unresolved outcomes and unverified result
ordering still stop settlement for repair/recovery. Startup must drain captured
result recovery first, then ranking catch-up, before enabling admission and per-
player rollover. Runtime day orchestration is still not wired.

Focused fixtures commit matches on December 31 and January 1, 2 and 3, then test
one-day and larger batches. They verify oldest-first ordering, year boundaries,
repeat calls, current-day exclusion and oversized-limit rejection. Prior ranking
rollback, payout, daily rollover and earned Honor-shop tests remain included.
Build/test totals are in QUEST_EVENT_STATUS.md; ignored log:
obj/chi-refill-validation/arena-ranking-catchup.log.

No live process, database or client was changed. Before-signup rollover, departure
capture, runtime recovery, season-view refresh and coordinated legacy writer
replacement remain unfinished. This is source and fixture evidence, not a real
server restart or native client acceptance claim.
