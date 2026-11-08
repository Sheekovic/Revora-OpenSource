# Team Arena daily milestones

The official [prize-upgrade guide](https://co.99.com/guide/prize_upgrade/), checked
2026-09-21, specifies an EXP Ball plus 500 Honor for nine daily Team Qualifier
wins, and the same reward for twenty daily matches. It also mentions daily
top-200 ranking rewards; this implementation covers the two participation/win
milestones, not ranking settlement. The page is published rule evidence, not
verification of patch-6711 client delivery or a complete historical ruleset.

Team result completion now records each admitted participant's eligible daily
entitlements in the same transaction as the result and statistic rows. Counts
come from complete persisted rosters on the captured outcome's Pacific date,
not mutable TodayWin/TodayBattles counters. Both winning leaders and admitted
winning members earn the win milestone; losing participants can earn the match
milestone. A unique player/day/milestone receipt prevents replay and preserves
the original source result. Suppressed entitlement writes abort completion.

Captured-result startup/fenced recovery records the same entitlements inside its
recovery transaction. A captured but uncommitted result cannot independently
grant a reward. Existing unverified legacy counters are not retroactive earning
evidence. Historical ranking migration remains separate.

Team payment shares the existing Arena daily item/Honor transaction, selecting
the team receipt table through fixed SQL alternatives. One payment delivers
catalog EXP Ball 723700 and 500 current/lifetime Honor, updates both solo/team
Honor mirrors, inserts the item and marks the receipt atomically. A full inventory
retains the whole entitlement. Successful payment immediately projects the item
and statistic updates; failures retain durable evidence and fence invalid caches.
Team rewards join the staged 30-second reward-delivery probe, at most one team
entitlement per probe. No P7 soul is added.

Fixtures use ordinary admitted leaders and a team member across twenty real
result transactions, including a suppressed ninth-win receipt, a rolled-back
twentieth result recovered through the cold recovery boundary, repeat recovery,
concurrent payment, full inventory, production statistic/item loading and a new
Pacific day. The cold fixture temporarily removes runtime registries and reloads
its original actors; it is not a full process restart or native reconnect test.

The earning and delivery services remain behind the staged Arena lifecycle.
The character update loop already calls ArenaRewardDelivery.Tick; production
startup does not initialize its gate. Ranking settlement, remaining
departure/shutdown work and native validation are unfinished. Team counter
rollover was subsequently connected to prepared signup; see
[counter rollover](TEAM_ARENA_COUNTER_ROLLOVER.md).
No production schema mutation, deployment or restart is part of this batch.

Validation: isolated build and 813 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/team-daily-milestone-build.log` and
`obj/chi-refill-validation/team-daily-milestone-tests.log`.
