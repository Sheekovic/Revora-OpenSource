# Champion achievement identity review

2026-09-26, source base `e711ad6`. Achievement 11123 remains disabled.

The installed achievement catalog, SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`,
requires receiving the Champion title. This identifies the achievement but does
not identify its earning tournament or a numeric title key.

The installed `ini/Title.ini`, SHA-256
`62de06f8145472cb53f15608f06268c4a062f8c6aa23599bf34dbbda137fd46e`,
contains separate low/high Elite PK champion titles at 12 and 16. The production
`ElitePkEvent.SynchronizeChampion` projects the reviewed high Elite PK title from
saved event evidence. This is a plausible related title, not proof that either
identity satisfies 11123. Proprietary catalog contents are not copied into Git.

The [official achievement guide](https://co.99.com/guide/guides/achievement.shtml)
mentions receiving Champion among honor achievements, alongside weekly/monthly
PK victories. It does not link the title to an event or numeric identity.
The [weekly/monthly PK guide](https://co.99.com/guide/quests/pkwar.shtml) uses
champion as a winner description, but does not map that description to 11123.
The [Elite PK guide](https://co.99.com/guide/quests/elite_pk.shtml) describes its
winners and prizes; that wording alone does not resolve this catalog identity.
These are official sources reviewed on the date above, not native-client traces.

Existing `LocalPkResults` producers already distinguish weekly/monthly admission
(11116/11117), weekly/monthly victory (11118/11119), and class victory (11131).
Do not attach the extra Champion achievement to all those results merely because
their winners can be called champions. Champion's Arena is also a distinct legacy
system; its class name is not evidence of the title requirement.

Next evidence needed: a version-appropriate official title rule or an approved
client/server trace showing the particular title award and achievement together.
An owner-approved documented Revora rule would also resolve the product choice,
but must be labeled custom and attached to a durable, ordinary-player tournament
result. Do not infer a custom decision from source coverage reaching 320.

No producer, gameplay behavior, deployment or roadmap completion count changed
in this checkpoint. The next independent implementation work can proceed while
this identity remains unresolved.


## Reviewable Revora implementation seam (2026-09-27)

Source reviewed at `4cfd8ee5`. A new owner question proposes achievement 11123
for the personal winner of a completed local Elite PK bracket. This is a
**pending product decision**, not inferred original behavior or an enabled grant.

The existing `ElitePkResults.Finish` transaction is the authoritative boundary.
`ElitePkPlacements.Record` verifies the elimination graph and requires the
champion to have won a contested bout. Only then does `Finish` save
`state='complete'`, `outcome='champion'` and the nonzero winner. Cancelled,
empty, bye-only and wholly uncontested runs cannot support the proposed credit.
An in-memory champion title or the `winner` argument alone is insufficient.

If approved, add `ProgressionEvidence.Complete` for 11123 inside that same
transaction when `earned` is true, using the run key and personal winner UID.
Apply the achievement to all four brackets; preserve the existing separate
high-bracket wing rule (60010020). Recovery must join the completed champion run,
its winner's registration and place-1 row, and validate contested winner evidence.
It must not require the winner to retain a time-limited title or wait for pack
collection. Repeated finalization, login and recovery must retain one achievement.
Do not alter tournament scheduling, admission, reward amounts or expiry.

Required validation: ordinary contested winner versus loser, all four brackets,
bye-only and cancelled runs, timeout scoring, forged/inconsistent placements,
rollback of final result/evidence together, repeated close/login, and an actual
ordinary tournament route followed by a new process and saved achievement reload.
Native display and deployment remain separate gates. Existing Elite PK earning
checks can be extended; seeded title flags cannot substitute for that route.
