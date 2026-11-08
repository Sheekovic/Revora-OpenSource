# Recurring collection expeditions

Owner choice, 2026-09-30: use recurring Revora event rewards for the four missing
cosmetic collections. This is a documented Revora edition, not restoration of
the original Monkey Party, European Championship, Celestial Fox or Dream Manager.

## Ordinary earning rule

Rowan's existing Planet expedition opens on Saturday and Sunday, Pacific time.
Each character can complete one expedition per weekend: defeat100 Birdmen on
Bird Island, defeat the three owned sentries, recover their cache and deliver it
to Rowan before Monday. No GM identity, payment or account allowlist is required.

New admissions freeze the next collection step. Eight successful weekends give
all19 permanent, bound designs below. Skipped or failed weekends do not advance
the step. Existing ownership does not alter the saved reward recipe. No random
roll, purchase or sacrifice of a previously owned design is required.

| Step | Monkey | Jersey | Basketball uniform | Additional mount |
| --- | --- | --- | --- | --- |
| 1 | 200553 | 193735 | 194675 | Celestial Fox200595 |
| 2 | 200554 | 193745 | 194685 | — |
| 3 | 200555 | 193755 | 194695 | — |
| 4 | 200556 | 193765 | 194705 | — |
| 5 | 200557 | 192655 | — | — |
| 6 | 200558 | 192665 | — | — |
| 7 | Solar Monkey200559 | — | — | — |
| 8 | Lunar Monkey200560 | — | — | — |

Store the designs in the ordinary wardrobe to qualify for Immortal Fate,
Slam Dunk Leader, Euro Collector and Saint Rider. Their existing collection
predicates remain unchanged. Duplicate requests cannot mint extra designs.

## Saved terms and recovery

Existing admissions migrate with `collection_step=0` and keep their original
wing-only terms. New runs freeze a step from1 through8. The character-row lock
serializes admission/delivery; a partial unique index prevents two completed
runs from claiming the same step. Item inserts, reward UID receipt, run completion
and any fourth-expedition wing award commit in one transaction.

Insufficient bag space leaves the cache unclaimed; make room and retry before
the weekend closes. Failed item writes roll back all gifts and completion. A
postcommit interruption relies on ordinary item loading, not another grant.
Completing all eight steps closes collection admission for that character.
Old completed runs are not reopened or retroactively granted items; previous
four-weekend finishers can earn the new collection steps on later weekends.

The Planet wing still arrives on the fourth total expedition, once, for30 days.
Later collection deliveries do not renew it. The collection cosmetics themselves
have no rental timer. Native DX9, calendar pacing and deployment remain separate
acceptance gates.

## Validation

The isolated game build passes. `tools/Verify-ArenaProcess.ps1 -PlanetOnly`
passes its capture, eight weekly processes, collection verification and two wing
expiry processes. Gameplay SHA256:
`BAB0F4EA58A64D2998780D68F1A2A010085D07ED9CF6EDD42D6653388FA1B1C1`.
Logs: `obj/planet-collections-build.log`, `obj/planet-collections-process.log`.

The ordinary character earns800 loaded Birdman kills,24 owned sentry deaths and
eight cache deliveries through production handlers. The first ten hunt kills
survive abrupt game exit and database restart. Subsequent game processes reload
earned state. All19 distinct bound permanent designs enter the actual wardrobe;
their item UIDs join the saved completion receipts. All four collection titles
equip through normal title handlers in the final fresh process. Repeat delivery
and further admission after step8 leave the saved snapshot unchanged.

The first delivery refuses37 occupied bag slots without consuming the cache.
After removing those supplied capacity controls, a database trigger rejects the
fourth gift insert: all earlier inserts and completion roll back. Retry delivers
the full recipe once. The original wing still has one award and its original
expiry, with exact-boundary equip refusal verified separately.

Fixture corrections during implementation: the injected failure initially ran
without selecting Rowan, so it correctly did not reach a grant; the restored
character initially skipped `CoatStorage.Login`, so it lacked the normally loaded
equipped wardrobe slot. Both were corrected to follow production interaction.
These failed fixture attempts are not claimed as gameplay bugs.

Limits: profile eligibility, proximity, calendar/timeout clocks and lethal damage
are controlled. This is production NPC/monster-death/wardrobe dispatch, not real
TCP attacks or eight elapsed calendar weeks. Old-contract preservation follows
the zero-default migration and explicit zero-step branch; this batch does not
claim a separate earned legacy-contract regression. The focused older Planet
fixture's progress-text expectation was updated, but that broad prerequisite suite
was not rerun. The successful driver removed its owned clone and stopped private
PostgreSQL. No public patch, live database migration or server restart occurred.
