# Atomic nobility donations

Implemented 2026-09-25 as the next prerequisite from
NOBILITY_ACHIEVEMENT_CHECKPOINT.md. This does not yet add rank achievements.

NobilityDonations.Pay commits the selected purse debit, checked nobility-row
insert/update and a durable payment receipt in one transaction. Owner locks and
the shared nobility reset gate serialize payment against in-process resets.
It verifies canonical ordinary-session eligibility, current donation/purse
preimages, amount bounds and sufficient funds before publishing a new donation.
Cache, board and icon updates happen after commit; failures fence the session.
A saved one-second duplicate-request window survives character reload.

Existing rates remain: silver pays face value, CPs cost the ceiling of donation
value divided by 50,000, and the bound-CP NPC pays ten donation silver per bound CP.
Packet currency values remain limited to silver/CP. NPC 900042 and 761124 now use
the same atomic service for CP and bound CP respectively. The old public Donate
helper now represents a paid silver donation; there are no remaining production
callers that pre-charge and then call it. NPC input/dialog eligibility outside
this service and native acceptance remain separate review points.

The database-backed replay receipt and startup initialization are in
revora_nobility_payments and NobilityTable.Load. No live schema was changed.
The service does not mint a rank from a supplied packet field or add 10318/11120.
Board sorting still uses existing Revora rules.

Validation: isolated Debug/x86 build and complete purse review pass with zero
failures. Thirty new atomic-service assertions cover all three currencies,
concurrent clicks, exact CP rounding, suppressed purse/donation/receipt writes,
cold Board reload from NobilityTable plus full character load, and reconnect
replay. Six earlier rejection cases also pass. The runner's 407 prerequisite
counter excludes these separate review assertions. The final NPC wiring was
compiled; the NPC dialog path itself was not exercised in this batch. Qualified
characters and funds are fixtures. Unrelated owner edits were in the build.

Follow-up: NOBILITY_RESET_RECOVERY.md records the atomic weekly checkpoint and
recoverable board projection, with eighteen passing interruption/recovery checks.
Remaining: reconcile paid Rey and earned rank semantics, add authoritative rank
achievements, validate ordinary earned funds and NPC access, then native/live
acceptance. Multiple live processes do not share coherent board caches.
No deployment or client patch occurred.
