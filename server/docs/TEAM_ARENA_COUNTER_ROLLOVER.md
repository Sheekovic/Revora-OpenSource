# Team Arena daily counter rollover

## Prepared roster admission

Owned team admission now prepares daily counters for the exact selected roster,
including admitted members who did not sign up themselves. The retention owner
holds the match and every original participant lock before preparation. It owns
shutdown cleanup before any reset, then reserves and transfers the same roster
only when all preparations succeed on one Pacific date.

Only the two leaders may retain their expected opposing invitation during this
operation. An unrelated member invitation, existing match/spectator ownership,
pending result or unhealthy actor prevents preparation. The ordinary idle reset
continues to reject invitations. Players excluded from the eligible roster are
not reset or transferred.

Each reset remains a separate atomic, free transaction. If a later participant's
receipt fails, that reset rolls back and fences its cache; earlier committed
resets remain replay-safe. No match reservation or map transfer happens. The
existing rejected-admission retention path owns cleanup. Points, Honor and
lifetime statistics are not modified by roster preparation.

The fixture exercises the public team matchmaker with two leaders, an admitted
member and an excluded low-level member. It checks new-day counters before
entry, result reload, unrelated invitation preservation and a targeted member
receipt failure after both leaders reset. Native-client presentation, complete
shutdown acceptance and production startup activation remain open.

The failed-member fixture explicitly releases its transportless session before
retention cleanup. The existing public-matchmaker fixture selects the intended
loser by identity, independently of waiting-dictionary enumeration order.

Validation: isolated build and 862 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/team-admission-day-build.log` and
`obj/chi-refill-validation/team-admission-day-tests.log`.

TeamArenaDayRollover reconstructs an idle ordinary player's TodayWin and
TodayBattles from complete saved team results on the current Pacific date. The
operation holds the original actor and durable character row, verifies the team
statistic preimage, then commits counters and one player/day receipt together.
It changes neither shared Arena points, Honor, lifetime totals nor rank/season
fields. Current-day results survive rollover; old cumulative daily counters do
not leak into a new day's display.

Ordinary team outcome freeze now uses the shared Arena UTC clock and rejects
non-UTC observations before changing the outcome. Previously that path used
DateTime.UtcNow directly, so the milestone fixture's injected next day did not
change ordinary team result timestamps. The current-day retention assertion
exposed this gap; the corrected fixture now verifies an actual saved result on
the new date rather than relying only on unchanged entitlement counts.

Invitations, active matches, spectating, trades, booths, disconnect barriers and
pending solo/team durable ownership prevent rollover. A saved later date rejects
clock rewind. Failure rolls back the counter update and fences the original
cache. Success updates the current statistic and sends it immediately. The
receipt also marks the player managed, preventing legacy statistic resets/saves
from replacing the new state.

Prepared Team Qualifier signup calls this operation before creating an invitation.
Production joint startup remains disabled. The fixture directly exercises
Prepare: busy eligibility, parallel replay, normal statistic loading, clock
rewind, a suppressed receipt, legacy-reset protection, and retention of a real
current-day result after previous-day matches. Native signup/display acceptance
and a background refresh for already-online idle players remain unverified.

## Published rules and remaining differences

The official [detailed prize guide](https://co.99.com/guide/prize_upgrade/team_qualifier.shtml)
and [quest guide](https://co.99.com/guide/quests/teamqualifier.shtml), checked
2026-09-21, distinguish monthly Arena-point reset from daily win/match prizes.
Both mention daily top-200 team Honor but do not provide the award amounts in
their text. They describe five-minute matches and a three-percent point transfer;
Revora now uses a [five-minute team timeout](ARENA_OWNED_SCHEDULING.md#five-minute-team-arena-duration),
but retains fixed participant point awards and a shared solo/team point balance.
Those reward policies are differences, not verified
retail parity. The two pages also publish different opening hours.

This change therefore resets daily counters only. Exact ranking payouts,
persistent team ranking identity, monthly/shared-point policy and full rule
alignment remain unresolved. No ranking amount is inferred from the solo Arena
formula. Production lifecycle activation and native validation remain open;
no deployment, restart or production schema mutation occurred.

Validation: isolated build and 820 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/team-counter-rollover-build.log` and
`obj/chi-refill-validation/team-counter-rollover-tests.log`.
