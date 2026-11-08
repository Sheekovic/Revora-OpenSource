# Arena result statistics transaction

This inactive migration participant follows the [match ledger](ARENA_MATCH_LEDGER.md).
It is not called from production match callbacks and does not replace the legacy
Win/reset writers yet. Honor awards, EXP Balls, custom prizes, disconnected-player
recovery and settlement remain unfinished.

`ArenaResultStatistics.Record` joins the same transaction that changes the owned
match from active to finished. The caller holds the match lock and both player
item locks; persistent player rows and Arena rows are locked in stable UID order.
Both canonical sessions and their cached statistics must match saved values.
A unique result/player receipt prevents a second application.

The winner and loser each receive one daily match count; only the winner receives
a win, and lifetime wins/losses increment on their respective sides. Daily counts
come from this player's committed result-statistic receipts joined to the saved
Pacific completion day. Legacy mutable daily counters are not accepted as earned
milestone evidence. Activation will need an explicit cutover and daily-reset
policy: the new count represents verified post-cutover matches, not a silently
certified import of old counters.

The point transfer uses the official guide's three-percent rule cited in the
[earning audit](ARENA_HONOR_EARNING_AUDIT.md). Integer rounding is an explicit
Revora choice: floor three percent of the loser's points. The transfer is reduced
if necessary to fit the winner's unsigned capacity, preserving the combined
balance. Zero/low balances cannot underflow. Lifetime counter overflow fails the
whole transaction instead of wrapping. The existing solo/team Arena-point mirror
is saved with both participants' counters and receipts.

Record returns a projection; only after the caller commits may Publish update
both player caches and send their existing statistic packets. The caller retains
both player locks until projection completes and must fence/reload affected
sessions if post-commit projection fails. Existing Arena saves use those owner
locks. The future coordinator must also recheck outcome/lifecycle context before
commit and handle failure before export. This component alone is not an outcome
authorizer or a live earning route.

The isolated build and **57 result/ledger/participation checks** pass, including
39 prerequisite checks. Tests use actual group import/End, then explicitly invoke
the staged transaction. They cover parent rollback, duplicate callbacks, both
players' daily counters, conserved points, solo/team mirrors, statistic save and
production reload, a second match and stale caches. Injected failure on the second
player's receipt, Arena update or team-Arena update rolls back the first player's
writes and terminal result as well; retry succeeds once. Boundary tests cover
floor rounding, zero balances and maximum unsigned point capacity.

Log: `obj/chi-refill-validation/arena-result-statistics-tests.log` (ignored).
Validated build SHA-256:
`b0c46131ea5bb0b0e7abc5a83d25073d55f1886dad0896b084221fb41e2fc5da`.

No production process, installed client or production database was changed.
The overall Arena earning milestone remains open until the coordinator, daily
entitlements, custom prizes and settlement replace the old writers together and
an ordinary earned-Honor purchase/reload journey passes.
