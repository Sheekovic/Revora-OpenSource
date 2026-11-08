# Character growth achievement restoration

2026-09-15, baseline `c7808a2`. Code validated; live acceptance and deployment pending.

The installed achievement catalog, SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`,
establishes these numeric thresholds: 10310 at level 130, 10311 at level 140,
and 10317 at 5,000 Virtue Points. They are current server-state observations,
persisted through the existing atomic evidence transaction. They do not require
a quest claim or GM privilege. Earning a milestone remains historical after
spending Virtue or losing levels through rebirth. Fake and reload-fenced actors
cannot establish evidence; client-authored achievement completion stays disabled.

The ordinary level setter persists the level before the observation. Virtue uses
the existing earned balance and production increment method. The achievement is
retained independently of later balance changes, as with the existing silver
milestone. This does not certify the first/second rebirth quest routes or their
unfinished multi-write finalization.

## Battle power prerequisite correction

The [official calculation guide](https://co.99.com/guide/guides/calculate.shtml)
describes additive level, item and shared-power contributions, and limits guild
power to fifteen. The previous calculator added `ExtraBattlePower` in its initial
level sum and again at the end. It also truncated guild power to a byte while the
client update already capped it at fifteen. Correct those defects, check null
before accessing the entity, and consistently read the supplied entity's state.
Existing equipment contributions and two-handed weapon policy are preserved.

At this earlier checkpoint, BP threshold achievements were disabled pending
mentor/clan review. The subsequent [BP sharing milestone](BATTLE_POWER_SHARING.md)
resolves source margins, the taper and canonical eligibility, and enables
10312-10314 at 100/200/300 BP. Native presentation remains pending.

## Validation

The isolated build passes 74 progression checks, including threshold boundaries,
the production level write and Virtue increment, persistent evidence reload,
concurrent reconciliation, whole-batch rollback/retry and excluded actors.
Seventy-four cultivation checks include null/ownerless calculation, different
receiver/subject, exactly-once shared power, guild cap, contribution removal,
twenty-point super/+12/two-super-gem armor and unworn-gear exclusion.
The 91 wardrobe, 42 Couple PK, 59 local PK, 116 Class PK, 23 Arena and 42 Labyrinth
regressions pass: 521 checks total. Five producer/prerequisite audit checks pass.

Staged server SHA-256:
`FA1F7F22C545066A9F722D4292FBC5F7B195EEE569E52E39DDEAE41F6A2E4CC6`.
The installed server and client were not replaced.
