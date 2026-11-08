# Flower queen earning checkpoint

Research base: `7cde775`, 2026-09-26. Delivery, saved-leader recovery and Sereth vacancy capture are implemented; native acceptance remains pending.

## Confirmed identities and rule

The installed `ini/Achievement.dat` has SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
A read-only decode identifies four separate queen achievements. The mapping to
existing server balance array indexes is deliberately not sequential:

| Achievement | Flower | Balance index |
| --- | --- | --- |
| 11124 | Rose | 0 |
| 11125 | Orchid | 2 |
| 11126 | Tulip | 3 |
| 11127 | Lily | 1 |

The [official flower ranking guide](https://co.99.com/guide/quests/flowersranking.shtml)
identifies the leading female recipient as queen. It does not establish tie
resolution or numeric achievement IDs. Revora already ranks positive lifetime
received totals descending, then UID ascending for ties (`Flowers.MembershipRank`).
Retain that deterministic Revora tie rule. These are separate from the recurring
Mr./Ms. competition and must not be awarded for winning that custom event.

## Existing foundations and remaining correctness work

`FlowerDelivery.Send` atomically consumes a gift, updates both balance rows,
saves a delivery receipt and records achievement 10127. It locks participant
entity rows in UID order. `RestoreStartup` restores offline flower totals and
female/male ranking membership from saved entity bodies; `Flowers.Attach` also
updates membership after appearance changes. The follow-up implementation below adds queen evidence. A cached rank field
alone is not sufficient proof.

Implement winner evidence from saved positive totals and saved female bodies,
using the same category mapping and UID tie rule as the displayed rankings.
Serialize competing flower deliveries before participant database locks, so two
recipients cannot each observe an outdated competing balance. Keep consumption,
totals, receipt and newly earned queen evidence in one transaction. Evidence
failure must roll back the delivery; update client completion only after commit.

Login/startup reconciliation must cover the current saved leaders, including
an offline winner or a vacancy caused by an appearance change. Retain historical
completion after a player loses first place. Do not fabricate historical winners
from current totals, and do not import unsaved legacy flower caches as evidence.
Coordinate membership-changing appearance transactions with ranking admission;
review database lock order before adding shared serialization. Avoid acquiring
another player's item lock while holding the ranking serialization lock.

## Required validation before completion

- Ordinary non-GM recipient earns each of the four categories through real
  gift handling; verify the nonsequential ID mapping independently.
- Second place, zero totals and male love-gift recipients do not qualify.
- Ties agree with live and restored rankings; concurrent competing deliveries
  do not produce awards from stale totals.
- Repeated requests cannot consume twice or duplicate lifetime completion.
- An injected queen-evidence failure rolls back source item, totals and receipt;
  reconnect allows a valid retry without duplicate charges.
- Production reload retains past winners and recognizes current offline leaders;
  appearance membership changes and empty-rank vacancies are covered.
- Native achievement display remains a separate acceptance gate.

## Delivery and reconciliation implementation

`FlowerDelivery.RecordQueen` checks saved female recipients with positive
lifetime totals, excluding a candidate whenever a higher total or equal total
with a lower UID exists. Categories already earned skip ranking work. It does
not sort the entire four-category population for every achievement refresh.
Four explicit catalog mappings record evidence through `ProgressionEvidence`.

Gift transactions serialize on the flower-balance table before acquiring
participant entity rows. Consumption, totals, receipt and the recipient's queen
evidence commit together. A failed queen write rolls back all four. The existing
post-commit achievement refresh publishes completion immediately. Current-leader
reconciliation also runs when progression evidence is read, and historical
completion remains after losing first place.

This is not complete historical reconstruction. The subsequent Sereth transaction
checkpoint below captures new gender-change vacancies; it cannot invent past
unrecorded reigns. The bounded competing-recipient checks below also pass. Source coverage is not proof that these edge cases or a compatible
per-character route are complete. Native display and live deployment remain
pending.

Validation: isolated build and **898 Magnolia checks** pass, including 16 new
queen checks across all four mapped categories. They cover zero/losing ties,
actual ordinary gift handling, queen-write rollback, source retention, replay,
current saved leader recovery and retention after losing first place. Existing
reciprocal-gift and separate-process ranking restoration checks also pass. Logs:
`obj/flower-queen-build.log`, `obj/flower-queen-tests.log`. These do not certify
the appearance-vacancy or competing-recipient cases listed above.

## Competing recipient validation

Follow-up on `2001812`: **926 Magnolia checks** pass, including 28 additional
checks with two independent sender/recipient pairs for each flower category.
Both delivery tasks start together and each replays its request six times.
The checks require bounded completion without fencing, exactly one consumption
per gift, equal final totals with the lower UID earning queen, a later overtake,
and durable historical completion for both accepted reigns. A male recipient
with the maximum stored total cannot earn any female queen achievement.
A higher-UID recipient may legitimately earn a historical achievement if her
transaction commits before the lower-UID competitor; the test allows that.
Log: `obj/flower-queen-concurrency-tests.log`. This is bounded contention evidence,
not a production-scale stress test. No production code changed in this follow-up.

The follow-up investigation identified the concrete vacancy path: Sereth's option 42 calls
`GenderChange.Apply`. `GenderChange.ApplyOwned` deducts CPs, changes the body and
other runtime state, then calls `EntityTable.SaveEntity` and disconnects. Before the follow-up below it did
not durably record an offline successor's queen transition. This required inspecting
this paid service's save/payment boundaries rather than assuming all gender
changes use `AppearanceState.Persist`; first rebirth preserves gender, while the
Sereth service follows a separate path. Preserve unrelated owner changes in
`Npcs.cs` when addressing it. Current-leader reconciliation alone cannot recover
a brief offline reign that ends before that character next logs in.

## Atomic Sereth vacancy capture

Follow-up on `b89bf61`. `GenderChange` now acquires the session lease and owner
lock, rejects active trading/booths and rejects a full bag when a garment needs
unequipping. A preflight entity save flushes pending character state without
charging or changing appearance. The service then takes the same flower-table
serialization lock as gift deliveries before acquiring its owner database row.
One transaction records current queens, validates saved appearance/payment,
charges CPs, saves the new body/face/hair, moves the owned garment to the bag,
saves the Twin City reconnect destination, and records the resulting queens.
Evidence failure rolls back the service transaction, including CPs and garment.

`RecordCurrentQueens` considers all four positive female lifetime rankings,
including offline characters. The winners are processed in UID order through
the existing evidence writer. Historical completion persists if a successor is
later overtaken before logging in. No new packet or proprietary content is used.

After commit, the ordinary CP, appearance and inventory projections are updated.
Repeated confirmations are rejected by closing service admission, followed by
the existing reconnect requirement. Successful sessions remain saveable so the
normal disconnect flow can preserve unrelated pending skills and item progress;
post-commit projection failures require reload and must never retry the charge.

Marriage policy assumption pending owner response: preserve the existing spouse
link rather than clearing one side of a mutual marriage. The user was asked
whether to preserve marriage or require a prior divorce. No spouse field changes
in this implementation. Sereth's existing NPC dialogue and native interaction
still require acceptance testing; the transactional test calls do not certify a
real client reconnect or production crash behavior. Administrative body edits
outside Sereth are not treated as ordinary player earning paths.

Validation: isolated build and **936 Magnolia checks** pass. Ten new checks cover
an offline successor's initial second place, injected evidence failure with
CP/body rollback, garment retention on failure, committed payment/location and
marriage preservation, safe garment movement, durable vacancy credit, rejected
repeat confirmation with safe autosave, loss of rank before the successor's
login, full-inventory rejection, and the public service's disconnect path.
Logs: `obj/flower-vacancy-build.log`, `obj/flower-vacancy-tests.log`.
No live executable, database migration or client file was deployed.


## Retained suppliers, gender rite and marriage (2026-10-01)

The retained non-GM character4294907001 continues from the KO/Meteor checkpoint;
existing ordinary character4294908901 participates without resetting its profile.
Both start single. Read-only preflight finds no positive female totals in any of
the four private ranking categories. No competitor, total or winner is seeded.

Sereth59558 options40/41/42 charge the configured25,000 CPs once, change1003 to
2001 and move the worn garment safely into the bag. A repeat Apply is refused
while the session awaits reconnect. Actual game/database restart restores the
paid appearance and prior career. Stone City storage makes the required room.

The four deliveries then use their actual loaded suppliers:

- One free daily rose sent by the male participant.
- One999-lily bouquet bought by that sender for1,229 CPs after an ordinary CP
  trade funds the purchase and two seed changes.
- Three orchids753003 and one tulip754001 earned by that sender on two different
  Magnolia days: daily-envoy entry, normal seed, paid1-CP reroll to Unique,
  planting and bonus delivery. Seed and bonus rolls are controlled.

Each delivery uses the bounded1150 dispatcher. Its exact consumed UID (or free
allowance), balances and receipt persist; immediate replay leaves the complete
snapshot unchanged. All four rank-one achievements11124-11127 and flower-delivery
10127 coexist with the retained earlier career. Magnolia's two bound4030101
runes remain owned across restart; both completed run records have no P7 soul.
The consumed orchid/tulip UIDs match their saved bonus records. Peer level, EXP,
Study Points, all Magnolia runs/changes/bonuses and other assets are fingerprinted.

The two single players subsequently use the ordinary proposal/accept handler.
Both spouse links and10129 persist across another full restart. Repeated accept
changes nothing. This supplies a real saved marriage prerequisite for the next
retained Couple PK route; no tournament victory is claimed here.

Phases `career-retained-social-{gender,flowers,marry,reload}` pass on unchanged
private game SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Final checkpoint: `revora_retained_social_probe` step6; both `snapshot` and
`harvest` fingerprints must match on reload. The main character now has body2001
and spouse4294908901's saved name; its prior bow remains equipped.

Corrections and limits: the first rose purchase used an inactive V2 listing and
was refused without charge. The initial lily purchase succeeded, but the loaded
catalog marks lilies non-tradable. That unused bouquet and its1,229-CP payment
were retained after an exact full-snapshot comparison; the sender then bought
its own bouquet. No refund, catalog relaxation or inventory injection was used.
Future fixture dates also exposed the existing backward-clock rejection during
flower startup. The private lifecycle helper now sets its flower clock before
Server.Run, using the latest saved positive-ranking day when it is in the future;
it never changes production clock policy or rewrites saved dates.

NPC approach, calendar and random outcomes are controlled. The earlier character
eligibility/funding still has its documented mixed provenance. The1150 frame and
marriage calls establish server-handler coverage, not native packet/UI acceptance
or a competitive public ranking campaign. No production source, live service or
client was deployed. These receipts do not certify the entire320 career.
