# Lottery and flower achievement earning

2026-09-20, baseline `c8ec7c4`. This batch connects two missing achievement
producers to restored ordinary-player reward paths. Genuine 6711 presentation
and live deployment remain pending.

## Evidence and requirements

The installed `6711/ini/Achievement.dat` has SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
A bounded read of records 10118 and 10127, using the existing seed `0x2537`
transform and an exact encryption round trip, confirms these numeric mappings:

| Achievement | Required action | Committed producer |
| --- | --- | --- |
| 10118 | Receive a lottery prize | Normal lottery claim, after inserting the owned item and saving the claimed draw |
| 10127 | Give or receive a flower or kiss | Successful delivery, recorded for both participants |

This is installed-client content evidence, not a newly verified wire layout or
proof of native notification rendering. Existing achievement packets are reused.
The file's flower/kiss wording does not establish that letters, tins or jade
gifts count. Those gifts retain their delivery behavior without earning 10127
by themselves. All four flower families, kisses and the successful daily free
flower/kiss are eligible. No GM status or character allowlist is required.

## Durable behavior

Lottery preview, ticket exchange, payment and rerolls do not establish receipt
of a treasure. Claiming the saved prize inserts 10118 with that draw's identifier
in the same transaction as the item and claim receipt. A full inventory or a
failed write leaves the paid prize unclaimed and awards no achievement.

Flower delivery inserts both 10127 records with the same delivery identifier
inside its existing two-owner transaction. Item consumption or daily allowance,
recipient counters, delivery receipt and both first-time achievements commit
together. Failure on the second participant also rolls back the first. The
existing canonical-session checks, gender/category rules, lock ordering, replay
guards and counter bounds continue to apply.

After commit, new evidence invalidates and refreshes achievement/wardrobe state.
The production title-login path restores it after character and inventory reload.
Each character retains one lifetime record per achievement. Later eligible
prizes or gifts still succeed without rewriting that first record. Legacy bits,
inventory possession and client completion requests are not earning evidence;
this batch does not backfill historical players.

`ProgressionEvidence.Complete` now distinguishes a real existing record from an
insert that saved nothing. The latter throws, aborting the surrounding award
transaction. Subclass progression uses this shared guarantee directly instead
of its duplicate private wrapper. This also restores the source scanner's
ability to account for its fourteen already implemented constant-ID producers;
those fourteen are not new achievements in this batch.

## Validation scope

The Magnolia suite tests actual earned flowers and lottery tickets, NPC exchange,
paid drawing and prize claiming, binding, duplicate requests, inventory and
achievement reload. Added flower failures suppress the sender or recipient
achievement for both owned flowers and free gifts, then retry concurrently.
The free kiss and a later owned kiss verify both gender direction and unchanged
lifetime uniqueness. Letter-only cases remain negative controls.

The lottery fixture adds the suppressed-achievement case to its existing payment,
result, item and receipt rollback matrix. A paid preview has no 10118; claiming
immediately projects it; production title login restores it. Existing progression
tests reject client-supplied completion requests for all 337 IDs.

The first Magnolia run exposed a fixture omission: its character/item reload
did not call the production title-login step before checking packet state. The
test now performs that step explicitly. No production login behavior was changed.

Validation: isolated build, 1,327 gift/lottery checks, 838 Magnolia checks,
74 progression checks, 86 subclass checks and three producer-audit tests pass.
The Magnolia run preceded removal of the redundant subclass wrapper; the final
build and focused subclass suite verify that consolidation. Logs are ignored
under `obj/chi-refill-validation/`: `lottery-achievement.log`,
`magnolia-gift-achievements.log`, `gift-achievement-progression.log` and
`gift-achievement-subclasses.log`.

The refreshed source inventory contains **226 of 337 producer identities**, up
from 224, with 111 still missing. This is neither a completion percentage nor a
verified route for one character to earn Rising Star's 320 achievements.
No live deployment.
