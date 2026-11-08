# Nobility weekly reset recovery

Implemented 2026-09-25. The reset clears donation and Rey values and advances
revora_nobility_reset in one checked database transaction. Reset and donation
transactions share an advisory lock; the in-process reset gate also covers
board sorting. Donations check the current week before reading their donation
preimage or accepting payment.

The first checkpoint imports the legacy reset date when available; otherwise
it starts at the current server-local Sunday without clearing existing donations.
The legacy file is no longer written. Subsequent weeks use the database marker.

The board is reconstructed from committed rows before the in-memory checkpoint
advances. A failure before commit preserves both old rows and the old marker.
A failure after commit permits reconstruction on retry without clearing later
donations. This does not claim coherent caches across multiple game processes.
Existing rank rules and paid-Rey semantics remain separate review work.

Validation: isolated Debug/x86 build and the full purse review pass, with zero
review failures and the existing 407 prerequisite counter. Eighteen additional
reset assertions cover suppressed donation-row updates, suppressed marker updates,
an interrupted post-commit projection, concurrent reset retries, ordinary-player
donations after recovery, and cold checkpoint/full-character reload preserving
new-week donations and purses. These eighteen assertions are outside the 407
counter. Test funds and time are fixtures; no genuine-client icon or live
deployment acceptance is claimed. The build contains unrelated owner edits.

Rank achievements, ordinary earned-funds/NPC routes, paid-Rey reconciliation
and native/live acceptance remain pending. No live schema or process changed.
