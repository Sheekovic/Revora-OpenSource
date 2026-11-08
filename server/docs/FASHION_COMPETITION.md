# Revora monthly Fashion competition

Owner-approved rule: rank permanent garment and mount-armor collections by
verified catalog star points, counting each design once per character. Equal
scores share placement and skip the tied count for subsequent placements. At
least one scored design is required. First, second and third earn Fashion Icon,
Fashion Idol and Fashion Master respectively for 30 days from season close.
This is a custom Revora rule, not a retail acquisition claim.

## Immutable snapshot and ranking foundation (2026-09-22)

`FashionCompetition` now provides a transactional snapshot/finalization operation.
It uses Pacific calendar months, consistent with Revora's other recurring event
calendars. The close is midnight on the first day of the following month. A new
snapshot may be captured in the first minute after that boundary; the precise
capture timestamp is saved. Ownership is the durable database state at capture,
not an inferred history at an earlier instant. Missed windows fail closed;
activation must never retrospectively score a month from later inventory.

The snapshot includes permanent garments/mount armor in inventory, equipment,
wardrobe or warehouse, using the actual durable character owner. `Days` must be
canonical zero (the legacy database stores it as text); temporary, unknown,
noncatalog and zero-star items do not qualify. Catalog entries must match their
IDs and the loaded item definitions. Bound and unbound owned designs both count.
The sorted scoring catalog is hashed, each owner's distinct scored designs are
saved, and SQL `rank()` freezes placements from summed star points.

A per-season database lock serializes concurrent finalization. Season identity,
close time, capture time, catalog hash, ownership and results commit together.
Suppressed snapshot/result writes abort the transaction. Replaying a committed
season does not read today's ownership or rewrite results, even after reload.
The result tables are internal; there is no client route to submit scores.

Build and 136 title-suite checks pass, including nine new snapshot cases:
early close rejection, ranking-write rollback, concurrent finalization,
duplicate-design scoring, tied ranks, temporary/noncatalog exclusion, reload
and later-transfer immutability, missed close rejection, and no premature award.
The fixture uses installed garment/mount star ratings and disposable-schema
ownership. Logs: `obj/chi-refill-validation/fashion-snapshot-build.log` and
`fashion-snapshot-titles.log`.

## Remaining implementation and acceptance

The month coordinator is now connected to the server event tick, and ordinary
wardrobe refresh consumes frozen results. The garment merchant now exposes
standings/rules, and committed grants produce a brief award notice. The matrix
records partial implementation pending process/native acceptance; nothing is deployed. Full ordinary
collection-to-season-to-title validation and actual process restart are pending.
Native acceptance remains pending by owner choice. No runtime refresh, deployment
or client patch occurred.


## Atomic 30-day awards (2026-09-22)

The ordinary progression/wardrobe read now consumes a character's unacknowledged
frozen top-three results. Placement 1 grants Fashion Icon (20522052 / 300 points),
2 grants Fashion Idol (20532053 / 200 points), and 3 grants Fashion Master
(20542054 / 100 points). Mapping is checked against the retained catalog before
granting. Equal ranks receive the same title; skipped placements create no award.
No current cosmetic ownership is consulted after results freeze.

Each result row is locked, its `fashion-revora-v1` award is saved, and receipt
acknowledgement commits in one transaction. Award identity is character/source/
season, preventing duplicate grants. Suppressed grant or acknowledgement writes
abort the transaction and preserve retry. Earned time is the original month close;
expiry is exactly 30 UTC days later. Expired unclaimed results cannot restart a
term. Reload preserves ownership/points/equipped selection; normal wardrobe
reconciliation removes the title and selection at expiry.

Build, 147 title checks and 122 shared progression checks pass. Eleven new award
checks cover grant/acknowledgement failures, tied winners, skipped second place,
post-close transfers, concurrent delayed claims, database reload/equip, exact
expiry, next-season ownership changes, actual second-place Idol and an expired
unclaimed winner. The fixture runs the actual snapshot and consumer across two
simulated month closes. It does not run a monthly scheduler, actual process
restart or a native client. No deployment or private runtime refresh occurred.
Logs: `obj/chi-refill-validation/fashion-awards-build.log`,
`fashion-awards-titles.log`, `fashion-awards-progression.log`.


## Durable monthly coordinator (2026-09-22)

Initialization stores the next Pacific month close once. Repeated initialization
preserves that cursor and never creates pre-activation seasons. The server event
tick polls at most once per ten seconds and processes at most twelve due months
per poll. It uses a locked database cursor, so concurrent processes cannot advance
the same month independently. Pacific UTC offsets are recomputed at each monthly
boundary, including daylight-saving transitions.

A due close inside its one-minute capture window runs the actual ownership/rank
snapshot. If the capture window was missed and no snapshot exists, the coordinator
saves a missed-season record without generating scores or awards, then advances.
A committed snapshot is checked first: if calendar advancement failed afterward,
retry/reinitialization advances from that snapshot even outside the capture
window, without rewriting ownership or falsely marking the season missed.
Unknown or invalid calendar state fails closed and logs only exception type.

Build and 159 title-suite checks pass. Twelve new coordinator checks cover first
startup, not-yet-due close, concurrent finalization, rejected cursor advancement,
reinitialization recovery, Pacific daylight saving, missed closes, catch-up,
repeated startup, bounded twelve-month batches and ten-second polling. Logs:
`obj/chi-refill-validation/fashion-calendar-build.log` and
`fashion-calendar-titles.log`. These are simulated-clock database fixtures, not
an actual process restart or native-client run. Standings/rules access and full
process/native acceptance remain open. No deployment or runtime refresh occurred.


## Garment merchant guide and award notices (2026-09-22)

The installed NPC catalog confirms garment merchant 6002 in Market (1036) at
202,179. The old wardrobe NPC 900015 is absent, so the live catalog entry is the
player access point. Its opening menu now offers Browse garments (the same shop
502 as before), Fashion competition and Farewell. Fashion's optional pages show
brief rules, the next judging date, the last scored season, the first six ranked
entries and the requesting character's own rank/score. Labels identify the scored
month; current ownership is never presented as a finalized score.

The handler requires a healthy ordinary player session, the correct active NPC,
a real nearby actor in the same map and no active trade/booth. Fake actors,
remote requests and unsupported options cannot read through this NPC service.
No GM, VIP, payment, enrollment or character allowlist is required. The merchant's
ordinary shop remains available through Browse garments.

Freshly acknowledged Fashion awards return a brief notice only after the grant
transaction commits. `Achievement.Refresh` then sends it through normal game
messages. Concurrent recovery yields one notice; subsequent reads/logins do not
repeat it. This is a post-commit delivery attempt, not a durable inbox: a process
loss between commit and send may lose the notice, while the title remains saved.

Build, 169 title checks and 122 progression checks pass. Ten new checks cover
catalog presence/location, brief ordinary rules, frozen personal standings,
handler dispatch, proximity, active-NPC identity, fake/unsupported requests and
notice replay/concurrency. Fixtures exercise server dialogue creation; visual
6711 acceptance and actual process restart remain pending. Logs:
`obj/chi-refill-validation/fashion-guide-build.log`, `fashion-guide-titles.log`,
`fashion-guide-progression.log`. No deployment, client patch or runtime refresh.


## Fresh validation-process recovery (2026-09-22)

The coordinator fixture now starts a separate x86 validation process after a
month snapshot commits but cursor advancement is deliberately rejected. The
parent's existing disposable schema is shared through a private inherited stdin
pipe; connection settings never appear in arguments, diagnostics or Git. The
child requires the exact fixture search path, begins with an empty game pool,
empty garment/mount catalogs and an uninitialized coordinator, and has a bounded
20-second lifetime.

The child initializes the source tables, advances the saved cursor outside the
capture window without marking the committed month missed, and materializes the
saved first-place award through `ProgressionEvidence.Read`. It verifies original
close/expiry timestamps, durable result acknowledgement and a committed notice.
Repeated reads neither duplicate the award nor repeat its notice. Reinitializing
with no catalog cache does not recompute ownership or scores. The parent observes
the advanced cursor after the child exits.

The title suite passes 170 parent checks, including one child-process assertion;
the child independently passes five recovery checks. Log:
`obj/chi-refill-validation/fashion-process-titles.log`. Production code is unchanged
from the preceding guide/notice batch. This proves fresh validation-process
persistence, not a game Host restart, socket login, native-client merchant/shop
presentation or production deployment. Those acceptance gates remain open.

## Earned Power Arena collection (2026-09-25)

The Power Arena suite now follows its seven-floor earning path through the
permanent unbound Somersault Cloud's wardrobe admission into monthly Fashion
judging. The actual earned item supplies five stars and a tied first place;
no cosmetic designs or ranking rows are supplied for this claim. The ordinary
character receives and equips Fashion Icon, reloads through production character
and wardrobe loaders, and retains one award receipt. At close plus 30 days the
title expires; the permanent mount still qualifies for Moonlight Wings.
**111 Power Arena checks pass**, including these five additions.

Fashion Icon and Moonlight Wings now have complete server earning evidence.
Fashion Idol and Master remain predicate-only until their collection journeys
are verified. Time and counted monster-death hooks are fixture-controlled; this
does not prove representative combat balance or native presentation. No production
code or deployment changed in this validation batch.

## All placements from earned collections (2026-09-25)

The later [collection acquisition fixture](COLLECTION_ACQUISITION.md) supplies
three ordinary characters with their own earned boss bounties, then actual Vesper
cosmetic purchases. Their saved collections produce first, second and third
places without seeded scoring rows. All three titles grant, equip, survive
production reload and expire at the original deadline. This closes the server
earning gap for Fashion Idol and Master; 55 collection checks pass. Native and
full Host acceptance remain pending.

## Earned purchases and placements across actual game processes (2026-09-27)

- [x] Three eligible ordinary characters each defeat a different normally scheduled
  daily boss through ReceiveAttack and its durable bounty handler. Each earns
  50,000 RVPs; no purse, cosmetic, score or award is seeded.
- [x] Vesper sells three, two and one distinct permanent garments respectively,
  at the existing2500-RVP price. Actual wardrobe requests store the items;
  repeated storage requests preserve one copy. Exact remaining purses are
  42,500,45,000 and47,500 RVPs, with six saved collection items in total.
- [x] Close the actual game, restart PostgreSQL and launch the game at the private
  calendar's next monthly close. Normal startup/tick freezes those collections
  into first, second and third place. Normal wardrobe login recovers Fashion
  Icon20522052, Idol20532053 and Master20542054; ordinary requests equip them.
- [x] A third actual game process restores all three equipped titles before new
  equip requests. Reconciliation and repeated equip preserve the three award
  receipts, exact purses and original close-plus720-hour expiry fingerprints.
- [ ] Authenticated/native merchant, wardrobe and title presentation, natural
  elapsed expiry and live deployment remain pending. Ties, missed-close policy
  and transaction fault injection retain their existing component-test evidence.

Run `tools/Verify-ArenaProcess.ps1 -FashionOnly`. Three launches pass in
`obj/fashion-process-run.log`, against the existing executable SHA256
`F702C021C627E79513165F367F795F554B517985A731034A40BAD3646710ACB8`.
No production source change or rebuild was required. Qualification, proximity,
lethal damage, boss schedule time and monthly judging time are controlled by the
private helper; this is an offline-profile process test, not a native playthrough.
Calendar advancement uses ProcessDue and never writes supplied ownership snapshots
or rankings. The marked clone is removed and private processes stop on success.
No live database, service, installed client or public calendar is modified.

## Earned titles expire across game restarts (2026-09-27)

The private `-FashionOnly` route now passes five game launches: earned purchases,
judging, equipped recovery, expiry boundary and expired recovery. It reuses
game SHA256 `E9EEF3BBD597B78F7823E6314E15A4E9183C15525E2FE9E561E392155A85526E`;
the helper is compiled without rebuilding production code.
Evidence: `obj/fashion-expiry-process.log` (ignored).

- [x] All three ordinary winners retain their equipped earned titles one .NET
  clock tick before the original deadline.
- [x] At that exact deadline, repeated normal equip/login requests leave Fashion
  Icon, Idol and Master unequipped. Monthly coordinator polling cannot renew them.
- [x] Another actual game process restores all three profiles at expiry; repeated
  equip/login requests still refuse the expired titles.
- [x] The three award identities, earned dates and expiry dates retain their saved
  fingerprint. Earned RVP balances remain exact; replay does not change CPs or silver.
- [ ] Native rendering, idle-client expiry notification, natural elapsed duration
  and live deployment remain pending. This is controlled-clock, offline-profile
  server evidence, not a native or authenticated TCP playthrough.

The helper chooses February of the following year for its earned season. Its
March judging plus30-day duration ends before April judging; an explicit calendar
assertion checks that boundary. This avoids confusing a legitimately earned new
monthly title with renewal of the expired award. Each profile is checked at the
same before/at-expiry instants. No event rule or production clock is changed.
The owned clone is removed and private processes stop after success. Inventory
counts remain unchanged because these three earning routes were already validated.
