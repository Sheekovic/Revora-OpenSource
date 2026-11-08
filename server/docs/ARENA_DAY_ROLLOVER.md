# Owned Pacific Arena day rollover

ArenaDayRollover.Prepare runs before solo signup when joint Arena startup
preparation has succeeded. Production startup has not enabled that boundary;
the reset is not a periodic timer operation. It requires a canonical, alive, idle participant and
checks saved level/statistics and both point mirrors under the owner lock.
Unclosed admitted matches for that player prevent rollover; a previous day with
results must have its ranking snapshot sealed first.

A unique owner/day receipt makes point fill replay-safe. No current-day solo or team result
means the existing ArenaPointFill(level) rule supplies base points and daily
counters clear. If current-day results already exist, rollover preserves earned
points. Solo counters derive only from solo receipts; team battles do not advance
solo daily milestones. Team result timestamps use the Pacific calendar, including
23/25-hour daylight-saving days, and are queried only after team schema initialization. Previous
day rank/points come from the immutable ranking snapshot; previous wins/losses
come from result receipts. A day with no played ranking shows zeros. Lifetime
wins/losses and Honor are not reset or awarded. Ranking payment remains separate.

Solo statistics, team point mirror and rollover receipt commit together. Failed
writes fence the session and roll back the whole change. Cache/statistic packets
follow commit. A midnight crossing before commit rolls back for retry. Clock
rollback cannot reopen an earlier filled day. Failed projection can reload the
saved state normally: ArenaTable's loader and legacy reset skip their host-local
reset for players with owned rollover receipts. Unmanaged players retain old
behavior until the full cutover; this is not a claim that all legacy reset paths
are globally replaced yet.

## Validation

Build and 200 combined Arena checks pass. New fixtures cover active-match refusal,
preservation of already-earned daily points, waiting for ranking, failed solo/team
updates and receipt insertion, concurrent refill, December 31 to January 1,
previous-day projection, lifetime preservation, production Arena reload, prevention
of legacy duplicate Honor, clock rollback and consecutive inactive days. The
fixture's failure retry clears its fence; full client reconnect remains a separate
gate. Ignored log: obj/chi-refill-validation/arena-day-rollover.log.

Runtime initialization, missed-day ranking enumeration, before-signup rollover,
season-view refresh across all online/offline participants, departure capture and
legacy writer replacement remain unfinished. Level point-fill values are retained
from Revora's current server; they are not new retail claims. No live deployment,
restart or installed client update occurred.

Missed played-day ranking enumeration is now implemented
([catch-up scope](ARENA_RANKING_CATCHUP.md)); startup and live admission
orchestration remain unfinished.


Idle eligibility also excludes team match ownership, solo/team spectating,
solo/team PlayWith invitations and any DisconnectPool barrier for that character.
These checks run both before and after acquiring the owner lock. They prevent
shared point resets during reserved/active participation or pending teardown.
The focused fixture checks that both persisted mirrors and the rollover receipt
remain untouched in each state, then clears ownership and verifies the normal
level-140 fill. It uses real match reservations, not native team combat. Combined
build/test totals are in QUEST_EVENT_STATUS.md; ignored log:
obj/chi-refill-validation/arena-rollover-idle-tests.log. Runtime admission and
cleanup must clear their own invitation/queue state before requesting rollover;
this guard is not a substitute for that lifecycle work.

Team-point retention additionally exercises real admitted team results and export
for both leaders and a member, wins/losses, both saved point mirrors, repeated
rollover and the production solo loader. Boundary cases cover the second before
Pacific midnight, midnight itself, the final second of a 25-hour fall-back day,
and the next midnight. Team daily/season counter settlement and coordinated
runtime activation remain open; this change only protects the shared balance.

Latest validation: build and 513 combined Arena checks pass. Ignored log:
`obj/chi-refill-validation/team-rollover-tests.log`.

## Prepared solo signup

The ordinary solo signup handler now calls the saved rollover before testing
whether the player has any Arena points. This lets an eligible returning player
with zero points receive the existing daily base fill before entering the queue.
An invitation, pending result, unsealed previous ranking or failed rollover
prevents signup. The legacy path remains unchanged until joint preparation.

The signup fixture uses a non-GM character and checks invitation refusal, a
successful reset and enqueue, repeated signup, ordinary statistic reload and a
suppressed receipt write that rolls back and prevents admission. Existing
rollover fixtures cover retained results, ranking prerequisites and idle
ownership. This does not establish automatic midnight ranking settlement,
resetting players already queued across midnight, native panel rendering or
production activation.

Validation: isolated build and 824 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/solo-signup-rollover-build.log` and
`obj/chi-refill-validation/solo-signup-rollover-tests.log`.

## Accepted invitations across midnight

Prepared solo matchmaking now checks the day again after both players accept,
before setting their fight state or creating/transferring the match. It acquires
both original actor locks without waiting, checks their waiting-list identities
and requires a reciprocal accepted invitation. The rollover service permits that
specific invitation while still rejecting unrelated invitations, existing match
ownership, spectators, disconnect barriers and unavailable players.

Each player's existing daily reset remains its own atomic transaction. Both must
succeed on the same Pacific date before admission proceeds. If the second reset
cannot complete, a completed first reset is harmless and replay-safe; no fight
starts. Busy actors and unmet ranking prerequisites leave the accepted invitation
pending for a later pass. Failed writes retain the persistence fence. The normal
idle/signup reset still rejects every invitation.

Fixtures use ordinary signup, real queue pairing, acceptance, registered entry,
owned completion and normal statistic reload. They cross midnight while the
invitation is outstanding and cover changed reciprocal ownership, lock contention
and a suppressed reset receipt. Previously queued players are refreshed when
their invitation is accepted; their idle queue panel is not periodically reset.

This does not settle the separate policy for an already fighting match that
finishes on a later date, team roster day preparation, remaining invitation
quit/refusal callbacks or native-client acceptance. Production startup activation
and deployment remain open.

Validation: isolated build and 843 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/accepted-midnight-build.log` and
`obj/chi-refill-validation/accepted-midnight-tests.log`. The existing public
matchmaking fixture now saves the same level as its character, as admission
correctly checks that persistence preimage before resetting points.
