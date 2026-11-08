# Owned boss kill achievements

## Snow Banshee and Sword Master follow-up (2026-09-25)

The same hash-bound installed achievement catalog identifies Snow Banshee 10527
and Sword Master 10528. Their registered encounter victory transactions now write
`snow-banshee-kill-v1` and `sword-master-kill-v1` evidence respectively, keyed by
the committed encounter UUID. Existing canonical killer and registered-companion
ownership checks determine the recipient. Reward eligibility and prices do not
change. No historical victory backfill is introduced.

The isolated build passes, followed by 95 Sword Master checks and 76 Snow Banshee
checks, plus five achievement audit tests. Coverage includes actual lethal combat
dispatch, concurrent callbacks, corpse replay, permanent evidence after production
login, companion-owner credit and suppressed evidence insertion rolling back
victory and rewards. Snow Banshee additionally verifies that a nearby non-killing
team leader receives Study Points but not the kill achievement. Suppressed loot
and Soul Ball writes likewise leave no kill receipt. Recovery retries grant it.

These fixtures control combat damage, clocks and prerequisite characters/resources;
they do not prove combat balance or the complete acquisition of every prerequisite.
Sword rollback retry uses the existing explicit fixture fence reset; Snow Banshee
recovery uses production character reload. Native rendering, full Host acceptance
and deployment remain pending. The build includes unrelated owner WIP.

The current producer inventory is 255 present / 82 absent out of 337, leaving at
least 65 more identities before a source upper bound can reach 320. Historical
counts below describe their original checkpoint rather than current completion.

Date: 2026-09-16. M6.57. Server implementation; native acceptance and deployment
remain pending.

The installed 6711 catalog separates killing Terato Dragon (10526), killing
Thrilling Spook (10529), and receiving the Dragon Slayer quest soul (10932).
The existing owned Terato and Exorcism encounters already committed victories
and rewards, but their kill achievements had no trusted producer. Both are now
recorded in the actual victory transaction.

## Evidence and attribution

The read-only installed `6711/ini/Achievement.dat` has SHA-256
`DF78DD498353B0B089F9B4228CADA5D79F4547AAE7B705349816671B2C29EBFC`.
The earlier reviewed decode and exact encryption round trip establish the
numeric identities and visible conditions. No proprietary catalog is committed.
No new protocol family is used; existing achievement notifications and queries
project the committed evidence. Native display acceptance is still unverified.

Only the registered encounter body can produce evidence. Existing admission,
canonical-session, team, distance, lifetime and companion-ownership checks run
before victory. The actual killer receives the achievement; a registered
companion credits its ordinary player owner. Nearby teammates retain their
separate quest/study rewards without receiving a kill they did not make.
Matching a monster name, copying a family/actor UID, arriving nearby or buying
a daily quest completion is insufficient.

Terato uses source `terato-kill-v1`, and Spook uses `spook-kill-v1`; each evidence
record refers to its durable encounter UUID. Evidence and victory commit
together with their existing reward entitlements and applicable Soul Ball
credit. The code verifies that evidence exists after insertion, so a silently
suppressed insert rolls back the whole victory and leaves its living encounter
recoverable. Later legitimate victories preserve the first permanent receipt
and continue granting their normal eligible rewards. Corpse replays grant no
additional evidence or rewards.

The post-commit refresh updates the normal achievement projection. Production
character reload plus the normal achievement send reloads the saved evidence.
Terato's separate Dragon Slayer achievement still requires collecting its quest
soul; killing the boss alone does not grant 10932. All three Spook difficulties
share 10529, while the paid quest shortcut grants none. No retrospective grant
from legacy bits or historical encounter rows is added in this batch.

## Validation and remaining scope

The isolated Debug x86 build passed. Grotto/Dragon Slayer tests pass 269 checks,
including the fully earned pill route, actual lethal combat, nearby versus
distant/non-killer attribution, companion ownership, failed evidence insertion,
reload and a second legitimate kill. Exorcism tests pass 602 checks, including
all three difficulties, paid-shortcut exclusion, team attribution, production
reload and achievement/victory rollback followed by hunt recovery.

Related Soul Ball and progression suites pass 596 and 74 checks respectively:
1,541 gameplay checks total, plus three achievement audit tests. Ignored logs:
`obj/chi-refill-validation/boss-achievements-grotto-final.log`,
`boss-achievements-spook-second.log`, `boss-achievements-souls.log`, and
`boss-achievements-progression.log`.
Server SHA-256:
`193B42986C21B93E38656A59558D982B8C9D7668256E5A1ABD654C0344F8BD82`.
Final harness SHA-256:
`4340FA9C8ADA4A28021D6037E6DC1E25978D68C11162EDD0A76670A8CD899F8D`.

Only the disposable validation schema is mutated. The producer audit now
records 221 present and 116 absent identities. At least 99 more identities are
needed before even a source-level upper bound reaches 320; a compatible
ordinary-character journey remains separately required. This does not complete
Rising Star, the remaining quests, Magnolia's bonus rewards or native gates.

No live server, client, patch or website was changed. Preserve existing victory
and achievement receipts during rollback; removing a producer is not a reason
to delete legitimately earned records.
