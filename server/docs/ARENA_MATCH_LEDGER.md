# Durable Arena match ledger prerequisite

`ArenaMatchLedger` is an inactive migration participant. No production startup,
match callback, payout or daily settlement is wired to it yet. Existing reward
writers remain unchanged until the result/counter/prize migration can replace
them together. This batch does not make Honor earning complete.

## Record ownership

Start requires a successfully imported, registered dynamic Arena, accepted
ordinary level-40+ competitors, canonical sessions and Fight state. A GUID-keyed
row freezes both distinct player IDs, the assigned map and UTC admission time.
Repeated starts are idempotent only for the same active identity; missing or
suppressed writes cannot masquerade as successful admission.

Finish is a caller-owned transaction participant. It requires the match lock,
the same still-registered map and participants, a completed group and a winner
and loser drawn from those participants. It locks the saved admission row and
records one terminal result with UTC time and the Pacific completion day. It
rejects results before admission time, missing starts, stale ownership, foreign
winners and replay. Parent rollback retains the active receipt. Future counters,
Honor and prize entitlements must join this exact transaction before commit.

`QualifierGroup` now exposes its canonical imported-map ownership separately
from the not-yet-ended predicate used by participation achievements. Both End
methods and Export use the same participation lock. Thus a future result
transaction can inspect and commit the terminal result before Export removes
its evidence. Existing first-participation behavior is regression-tested.

## Validation and limits

The isolated build and **39 ledger/participation checks** pass. The fixture uses
real group Import and End, including dynamic-map transfers. It covers invitation
rejection, duplicate starts, unfinished and missing-start results, parent rollback,
suppressed insert/update, clock rollback, concurrent finish, Pacific completion
day, repeat initialization, repeated End, export rejection and foreign/stale
participants. The 23 existing participation checks are included in that total.
Log: `obj/chi-refill-validation/arena-match-ledger-tests.log` (ignored).

Build SHA-256: `a3a746c48a279ca29338cbe490e187286fced6ecd8b7170291bc32c41be24cb1`.

The ledger currently requires both canonical sessions to remain present on the
owned map. Disconnection/forfeit handling and restart cancellation/recovery need
an explicit persisted lifecycle before activation; they are not silently treated
as completed wins. Outcome reasons and time-limit/damage authority also belong to
that coordinator. Existing mutable legacy results must not be imported as earned
ledger evidence. Full end-to-end tests must exercise the gameplay result path,
not merely call Finish or seed Honor.

Remaining: integrate durable start/terminal coordination, both participants'
statistics, point transfer, 20-match/nine-win entitlements, custom prize policy,
ranking settlement, safe cleanup/retry and earned Honor purchase/reload. See the
[complete audit and sequence](ARENA_HONOR_EARNING_AUDIT.md). No live process,
production database or installed client was changed.
