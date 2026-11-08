# Arena daily Honor and EXP Ball entitlements

> Current-source note (2026-09-25): the inactive/no-initialization statements below
> describe the original milestone. Server.Run now calls joint Arena startup
> preparation. The nine-win reward still does not prove winning the first nine
> matches; see [the achievement checkpoint](ARENA_ACHIEVEMENT_CHECKPOINT.md).
> This is source wiring, not confirmation of live deployment.

This inactive participant implements the official PC Qualifier's nine-win and
20-match daily rewards described in the [earning audit](ARENA_HONOR_EARNING_AUDIT.md).
Each milestone grants 500 current/history Honor and one EXP Ball. Local catalog
723700 is named ExpBall and its tooltip identifies the usual ten-per-day EXP
consumable. Unbound item delivery is Revora's explicit policy; the guide does
not establish item binding. No P7 is awarded by these daily milestones.

`ArenaDailyRewards.Record` joins the owned match-result/statistic transaction.
Counts come from committed per-player result-statistic receipts for the saved
Pacific completion day. A unique player/day/milestone key makes both entitlements
independent and non-repeatable. Eight wins or nineteen matches earn nothing.
A suppressed entitlement write rolls back the terminal result and both players'
statistics. Legacy RAM counts cannot create these entitlements.

Pay processes one pending entitlement while holding the owner item lock and
durable player row. Full inventory or current/history Honor overflow retains the
whole reward. Solo/team current and history balances must agree with their saved
values before both mirrors, the item insert and paid receipt commit together.
The item and 500 Honor never split across transactions. Existing inventory and
Arena statistic packets publish only after commit. Failures fence the stale
session and leave unpaid entitlements available for recovery.

The isolated build and **89 daily/result/ledger/participation checks** pass. The
fixture imports and ends 21 distinct matches to earn the first day's milestones,
then nine more on a new Pacific day. It invokes the staged result transaction
explicitly; this is not yet automatic production combat-to-reward integration.
Checks include ninth-result rollback, repeated thresholds, full bag, concurrent
payment, failed item insert with both Honor mirrors rolled back, production
statistic/item loaders and the independent next-day entitlement. The write-failure
retry clears only the fixture fence; complete character reconnect is still a
coordinator acceptance requirement, not claimed from that fixture shortcut.

Log: `obj/chi-refill-validation/arena-daily-rewards-tests.log` (ignored).
Validated build SHA-256:
`cb4cb47b719d5e974a3cacbe571f51d7348cfa18043247307fcbcad9cc31e8ed`.

There is no production initialization, periodic payout recovery or match callback
hook yet. The coordinator still must replace legacy writers, persist disconnect/
forfeit and startup outcomes, handle custom hundred-win prizes and daily ranking
settlement, retain the full owner locks through projection, and validate ordinary
earned-Honor spending/reload. No live process, installed client or production
database was changed. The overall Arena migration remains incomplete.


## Valuable solo and Team Arena daily rewards (2026-09-27)

The earlier inactive-participant description is historical: the owned solo and
Team Arena coordinators now record and deliver daily entitlements. This update
adds one **bound level-one Tortoise Breaker rune4030101** to each newly recorded
nine-win and twenty-match milestone, alongside500 Honor and the existing EXP Ball.
The item is the same class-neutral combat rune used by other daily quest rewards.
No P7 soul is added to either daily route. This bonus is Revora policy, not an
original Conquer reward claim.

Both entitlement tables gain `rune_type` (default0 for existing rows) and a unique
nullable `rune_uid`. New earned entitlements freeze4030101; older pending rows
retain their original Honor/EXP Ball prize, and paid rows are never reopened.
The rune UID and receipt constraints distinguish pending/paid and old/new terms.

New bundles require two free inventory slots. Both Honor mirrors, EXP Ball,
bound rune, first-Honor evidence and paid receipt commit together under the
existing player/item locks. Full bags keep the complete entitlement pending.
A failed rune insert rolls back Honor and the EXP Ball as well. Projection uses
existing item/statistic packets after commit; no client patch is introduced.

The expanded ordinary solo/team fixtures earn their milestones from owned match
results, check the two-slot boundary, exact rune counts/binding, concurrent payout,
old pending entitlements and production statistic/item loaders. A rune-only insert
failure demonstrates rollback after the EXP Ball insert. These fixtures do not
claim native combat or a logged-in character surviving a real Host restart.
Live deployment and native acceptance remain pending. Startup schema acceptance
is recorded in the follow-up below.

Validation: isolated build passed with existing legacy warnings; **1,414 Arena
daily/recovery/prerequisite checks passed**. Logs: ignored `obj/arena-rune-build.log`
and `obj/arena-rune-tests.log`. Existing retries clear only the fixture fence after
proven rollback; they are not full reconnect acceptance. Counts overlap earlier
Arena suites and must not be added together.

Follow-up: the actual private Host added all four rune columns to the existing
solo/team tables without replacing them, then passed restart and idempotence
checks. See `ROADMAP_STARTUP_ACCEPTANCE.md`. Earned-player Host recovery and
native acceptance remain open; the private candidate is stopped.
