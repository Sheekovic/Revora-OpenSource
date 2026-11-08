# First Study Points from committed rewards

Date: 2026-09-16. M6.58. Server implementation; not deployed.

The installed 6711 catalog defines achievement 10319 for receiving Study Points
for the first time. It previously had no trusted producer. Eight existing
ordinary-player reward services now record it in the same transaction that
credits the points:

| Source | Durable reward identity |
| --- | --- |
| Magnolia planting | Character and daily run |
| Price Hunt | Character and daily run |
| Rare Materials | Character and daily run, including purchased stars |
| Soul Ball | Owned bead/commission run |
| Exorcism | Daily claim, earned or paid |
| Legacy Anniversary redemption | Owned completed bead run |
| Terato Dragon | Actual Study Point entitlement payment |
| Release the Souls / Desert Guardian | Character and daily run, earned or paid (M6.59) |

Paid quest completion can legitimately award this achievement when its full
reward credits positive Study Points. It still cannot substitute for a boss
kill or an unrelated quest achievement. Anniversary's missing production
admission NPC remains unresolved; this hook does not establish a new-player
campaign route.

## Evidence and transaction boundary

The read-only installed achievement catalog has SHA-256
`DF78DD498353B0B089F9B4228CADA5D79F4547AAE7B705349816671B2C29EBFC`.
Its previously reviewed decode identifies 10319; no client content or packet
layout changed. Existing achievement send/notification paths project the
committed result, with genuine 6711 visual acceptance still pending.

`StudyRewardEvidence.Record` requires the canonical healthy character, owner
lock and active reward transaction. It verifies that the saved Study Point
balance equals the prior owner balance plus the full positive reward. Only
then does it record `study-reward-v1`, keyed by the source and durable run.
A zero-point contract grants no evidence. A silently suppressed evidence insert
fails the transaction instead of completing an unrecorded award.

Quest material/payment, currencies, items, EXP effects, daily completion and
first-study evidence therefore roll back together. Terato's victory is a prior
transaction: a failed payout keeps its full unpaid entitlement, including when
the failure is the achievement write. Reconnecting retries that payout once.
Later legitimate grants retain the original permanent achievement receipt.
Client achievement refresh follows commit and cache installation.

Raw balances, legacy client bits, pending boss entitlements and administrative
assignments are not used to backfill this achievement. A character who already
holds Study Points can earn it from a new verified credit. Other legacy sources
remain separate work: study books, direct item/NPC grants, Jiang Hu transfers
and unconverted direct reward handlers. Desert Guardian was subsequently
converted in M6.59 with checked balances, items, completion and both achievement
receipts; see [Release the Souls](RELEASE_THE_SOULS.md).

## Validation and remaining gates

The affected suites exercise ordinary complete planting, hunt/return, mining,
bead redemption, Exorcism and Terato payment journeys. Each new evidence write
has a suppressed-insert test that verifies rollback, production reload and
successful retry. Tests also check the saved source key and normal achievement
packet projection. A preserved zero-study Magnolia contract grants its other
rewards without granting 10319. Terato's pending payment survives failed
evidence, reconnect and repeated payout attempts.

The isolated Debug x86 build passed. Final suites: Magnolia 558, Price Hunt
519, Rare Materials 531, Soul Ball 601, Exorcism 609, Anniversary 516 and
Grotto/Terato 275. This is 3,609 checks across suites (including their shared
prerequisite checks), plus three achievement audit tests. Ignored logs under
`obj/chi-refill-validation/`: `study-evidence-magnolia-second.log`,
`study-evidence-price.log`, `study-evidence-rare.log`,
`study-evidence-souls.log`, `study-evidence-spook.log`,
`study-evidence-anniversary.log` and `study-evidence-grotto.log`.
Server SHA-256:
`E6DE121BF8330E5F25E849380FCD22D2120128374C44089F2801B63F97F4B09B`.
Final harness SHA-256:
`A92BA81DE62A6EE8E08051B4170C78874974EE1F29DA0B5B7476DA8792301AF7`.

Only the disposable validation schema is mutated. Source coverage is now
222/337, with 115 absent producers; at least 98 more identities are needed
before a source-level upper bound reaches 320. A compatible per-character
journey remains separately required. No live server, migration, client patch or
website publication is included. Preserve earned evidence and reward receipts
during rollback.
