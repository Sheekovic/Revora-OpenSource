# Way of Heroes Jiang Hu score rewards

Implemented and privately validated 2026-10-02; not deployed live.

The installed stages and the [official Way of Hero guide](https://co.99.com/guide/guides/hero_system.shtml)
agree on these Jiang Hu score/reward amounts:

| Stage | Score | Bound prize | Chi from pack |
| --- | ---: | --- | ---: |
| 703 | 10,000 | 3005126 | 500 |
| 805 | 20,000 | 3005127 | 1,000 |
| 906 | 30,000 | 3005128 | 2,000 |
| 1004 | 50,000 | 3005129 | 3,000 |

Stage 703 opens at second rebirth level 15; the other three at second rebirth
level 121. The existing ordinary creation/training requirements still apply.

Claims now lock and read the saved Jiang record, validate its owner, exact shape,
stage/star activation flags and installed stage-specific attribute identities,
and use the existing loader's alignment-aware score calculation. The runtime
score cache and client progress cannot qualify. Empty ownership scores zero;
malformed ownership fails without an award. Existing owner/transaction, capacity,
session, bound plus-0 output, progress and unique receipt protections remain.
The existing Chi pack consumer provides atomic consumption and wallet credit.

## Ordinary training and restart evidence

`tools/CheckCandidateJiangClaims.cs` adds `career-jiang-claims-earn` and
`career-jiang-claims-reload`. A separate ordinary non-GM character starts with
supplied level 140, second rebirth and 50,000 CPs. No Jiang score, trained star,
claim prize or Chi reward is inserted directly.

The normal creation packet creates the Jiang path and awards its existing 100
Study Points. Fifty-four ordinary paid training requests fill six stages. Legal
maximum rolls and the first allowed attribute per stage are controlled, so each
completed aligned stage scores 9,000; the result is 54,000. Every debit and Study
increase is checked against the production price ladder, including its counter
cap at 49. Initial funding and natural roll odds are not demonstrated.

Before each next threshold is reached, a forged runtime score of 81,000 still
cannot claim. Each actual saved threshold admits eight concurrent claim requests
but awards only one bound pack. Repeated item use consumes each pack once. The
four awards total exactly 6,500 Chi. A fresh game process after PostgreSQL
stop/start restores the scoped profile, purses, inventory, progress, claims,
consumer receipts and Jiang progression; replay leaves them unchanged.

Jiang's serialized relative countdown field is deliberately excluded from that
snapshot: loading reconstructs its timestamps. All other serialized fields are
compared. This is not a wall-clock countdown persistence claim. Separate decoder
checks reject a wrong owner, trailing data, truncation, invalid stage/star flags
and an out-of-catalog attribute without changing the database.

The isolated committed-source-overlay build passed with existing legacy warnings;
the composed harness compiled with warnings as errors. Both phases passed.
Private game SHA-256:
`5427840C924D94662BCF0770B979B7983053906F73E45B8ACEF8234DA4848CC4`.
Logs: `arena-process-career-jiang-claims-{earn,reload}.log`. A fixture compile
argument type was corrected before gameplay. The first run then exposed its
incorrect expectation of an uncapped training counter at training 50; the fixture
resumed from saved stars, payments and already-consumed rewards after correcting
that expectation. Production training behavior and saved earning were preserved.
The reload phase additionally ran the decoder checks after their addition.

This uses in-process packets, supplied starting qualifications and controlled
rolls. Authenticated/native acceptance, natural funding/progression, interrupted
claim commits and deployment remain open. The retained main-career character is
unchanged; these checks do not add a claim of compatible 320-achievement earning.

## Senior Training Pill research history

The [official training discount guide](https://co.99.com/guide/quest/2014/jiang-hu-training-discount.shtml)
confirms that Senior Training Pills increase the chance of an Ultra attribute.
The [training spree guide](https://co.99.com/guide/event/2014/jianghu_training_spree.shtml)
values a pill at 50 CPs for that event. Neither source supplies an exact boost
probability or establishes the installed client's request encoding.

**Superseded implementation status:** the research below preceded the now-tested
server pill consumer and higher Chi claims. See [the current implementation,
validation and native-use gate](WAY_OF_HEROES_SENIOR_PILLS.md). The distribution
measurement below remains applicable.

At that research checkpoint, the atomic training path accepts free/paid actions and restoration; it
does not consume 3003126. An older unused roll branch contains `higher` modifiers,
but this is not proof of pill consumption, correct odds or an active client path.
A documented Revora 10% highest-quality boost was proposed to the owner; no answer
had arrived when this batch was completed. Higher Chi-stage pill rewards remain
pending that earning/use contract. No pill odds or packet encoding were invented
in this batch.

### Follow-up: measured roll distribution (2026-10-02)

`tests/CultivationPricing/Measure-JiangQuality.ps1` extracts and executes the
production `GetStatusLevel` method with every possible pair of random results.
All 92 branches passed; branch weights account for the nested, exclusive upper
bound and the special `Next(1,1)` result. This is exhaustive branch arithmetic,
not a random sample or a native-client test. The resulting level probabilities
are 29.858098251%, 15.572383965%, 12.000955394%, 9.620003013%, 7.834288727%,
and 25.114270650% for levels 1 through 6 respectively.

A flat 10% highest-quality rate would therefore reduce the existing chance.
The implementation recommendation is a **10% independent rescue of a normal
non-highest result**, preserving normal highest results. That yields a final
highest-quality probability of 32.602843585%, rather than replacing the normal
distribution or adding ten percentage points. This is a proposed Revora rule,
not an original Conquer probability or an implemented effect.

The inspected active request seam is `JiangHu.ExtraStudy` forwarding action,
star and stage bytes 9/10/11 to `JiangTraining.Train`. It does not establish a
pill toggle or item UID. The old `higher` branch is outside that atomic path;
its `Random.Next(4,6)` cannot itself produce level 6. Do not wire a guessed
request action to that branch.

Next implementation contract: an ordinary validated item-use request arms one
durable pending boost. Refuse another pill while armed. Consume that pending
boost only in the same transaction as the next successful free/paid training;
restoration, rejected training and transaction rollback must preserve it.
Persist pill ownership consumption and the arming receipt atomically, with
ordinary-player cold-restart, concurrent/replayed use, insufficient-payment,
restore and rollback checks before enabling 702/804/905/1003. Verify an actual
6711 item-use path; if the pill opens a training UI instead of emitting the
normal item-use request, use an evidence-backed ordinary service entry point.
No production code, live instance or milestone checkmark changed in this audit.

## Fresh natural training and all four score rewards (2026-10-03)

The earned ordinary level133/class25/twice-reborn career continues from its Chi
checkpoint. Three owned non-pill items move into Desert City storage for claim
capacity. Normal creation makes the Jiang path and adds100 Study Points
(865 to965). All four score claims refuse before creation/training. The first
of the19 bound pills earned from Chi arms one durable boost; a second pill is
refused while it is pending. That boost survives a separate process restart.

Ordinary paid training fills empty stars, then selects potential score gains
and uses the ordinary paid restore action when a roll fails to improve strength.
No attribute type, quality, strength, training result, currency or Study Point
value is supplied. Normal random rolls and the pill's production boost remain
active. Each accepted training costs the configured ladder amount and grants20
Study Points; each restore charges its configured CP price, preserves Study
Points and restores the prior star UID/strength.

Six successful bounded training processes reach50,320 strength after824 rolls,
including625 paid restores and412,250 earned CPs spent in total. Their saved
endpoints are120/240/360/480/600/824 rolls and strengths
22,685/36,805/41,180/42,270/44,330/50,320. The first five batches cap at120 rolls;
the last permits240 with a210-second limit. Every iteration is checkpointed.
These are observed costs for this run, not guaranteed prices to reach a score.

All19 earned pills have consumed training receipts and none remains pending.
A separate claim process grants703/805/906/1004's bound packs and consumes them
through normal item packets for500/1,000/2,000/3,000 Chi:6,500 total. Item reuse
and repeated stage requests do nothing. A final game/database restart verifies
all four nonlegacy claims, exact wallet increase, pill receipts, loaded bags,
nested career state and loaded Jiang strength. The claim decoder independently
calculates50,320 from the saved Jiang data. Latest checkpoint is
`revora_fresh_jiang_probe`, stage2; earlier probes remain historical.

Two fixture failures remain documented. First, the negative second-pill check
used a helper that demands consumption, so it failed on a correct refusal after
creation and the first pill had already committed. A read-only comparison found
only the three stored item moves,100 Study Points and consumed pill62126617 with
one pending boost. Recovery creates only the probe, requiring the prior Chi hash
`28913528093677d71cb83bd6855fa39d0b05c8e2d698295c026ec8e1457dc3c8`
and exact reviewed Jiang snapshot
`00b0976f04bdb5540596c2cdee60fe2258f99307d5a2c40f2d6cbc1ff0cfaf99`,
zero strength,965 Study Points and18 remaining pills. Correct negative-use
packets verify refusal without expecting deletion. Second, an empty-star score
gain assertion failed before any training; the selector now handles untrained
stars separately. Neither correction regrants items, creates training results
or bypasses payments. Failed logs and comparison snapshots remain private.

Native Jiang UI/combat acceptance and deployment remain pending. No production
source/build changed. Candidate SHA256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
