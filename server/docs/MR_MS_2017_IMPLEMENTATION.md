# Mr./Ms. 2017: next earning route

Status: owner approved recurring in-game Revora edition on 2026-09-25;
player service unimplemented.
The 2016 local event must retain its own receipts and awards.

## Evidence reviewed 2026-09-25

[Official 2017 guide](https://co.99.com/guide/event/mr____ms__conquer_2017_guide.shtml)
requires paid registration, reviewed photographs, local qualification, intermediate
elimination and all-server finals. Unbound roses, lilies and orchids count one
support point each; tulips count two. Its local top-three table names Most Radiant
for 360 days without separating male/female title identities. Final winners receive
a year-long category title; Romance wings last permanently for first, 360 days for
second and 180 days for third. The intermediate dates disagree between sections.
The guide also describes separate fan contributions and garment-fragment rewards;
those are not candidate support totals. This evidence does not define a recurring
Revora calendar, tie policy, or photo-free substitute.

The [2018 Heart-wing announcement](https://co.99.com/news/2018-03-01/the_war_between_single_and_cp_will_start_.shtml)
only establishes faction support as an earning theme. It does not supply a support
threshold or award lifetime; Heart wings cannot be silently added to this contest.

## Installed identities and unresolved mapping

`Sistemas/WardRobe/StorageManager.cs` defines Peerless Beauty 20402040,
Super Idol 20412041, Most Radiant 20442044, Most Attractive 20452045,
and Romance wings 60126012. Their presence is not an earning producer.
Category mapping and whether one year means 365 days require an explicit contract;
do not assume the guide's locally duplicated title wording resolves both keys.

## Approved format and implementation contract

Owner chose a recurring in-game Revora edition on 2026-09-25. This removes the
photo-review website and all-server prerequisite; it does not pretend a local
contest is an all-server victory. The following are explicit Revora adaptations:

- Monthly Pacific calendar, separate from 2016 receipts. Free registration, one
  immutable category per character per month. Mr uses bodies 1003/1004; Ms uses
  2001/2002, consistent with the existing local event. No GM or owner allowlist.
- Only real unbound flower items can supply support. Reject male-gift substitutes,
  locked/stored/suspicious items and self-votes. Each donation consumes exactly
  one inventory unit, records its exact UID/preimage and adds the bundle's flower
  quantity times the category weight in one transaction. No free synthetic ballot.
- Rank each category by total support descending, then registration time, then
  character UID. Zero-support entrants receive no rank award. Freeze votes,
  standings and entitlements under one season lock. First three share neither
  rank nor an extra prize on ties; the published tie-break defines the order.
- Female top three receive Most Radiant 20442044; male top three Most Attractive
  20452045, each 360 days. This category mapping is a documented Revora choice.
- Female first receives Peerless Beauty 20402040; male first Super Idol 20412041,
  each 365 days. A year is fixed at 365 days for this edition.
- Each category's top three receive Romance wings 60126012: permanent / 360 days /
  180 days. Keep separate versioned lifetime sources so a later timed award cannot
  downgrade an existing permanent wing. Expiry is anchored to season close.
- Do not claim the historical garment/accessory/fan-point rewards are restored.
  This edition's valuable rewards are its wardrobe titles and Romance wings.
- Brief ordinary NPC registration, category/candidate selection, inventory flower
  selection, rules and standings. Pin displayed identities so changing rankings
  cannot redirect a click. Normal title refresh recovers offline awards once.

Implementation must prove atomic debit, replay/concurrent packet handling, month
boundaries, delayed closes, result persistence, offline claims, expired claims and
permanent-versus-temporary precedence. Native acceptance and live deployment stay
separate. No reward source, public NPC or release gate is enabled by this document.

## Registration and donation store (2026-09-25)

Implemented in `Game/Features/MrMs2017.cs`, initially without runtime hooks.
The result checkpoint below adds startup and wardrobe recovery. `Register` requires a
canonical ordinary loaded player and matching durable body; duplicate registration
preserves its original record. `Donate` requires an entered target, an unbound
catalog flower, an exact saved inventory preimage and a nonempty per-click request
identity. It consumes one bundle unit and records support in the same transaction.
The shared season lock excludes closed/stale months. Postcommit installation
failure fences the player for reload instead of treating stale inventory as spendable.

Validation: isolated output build passed using the existing legacy x86 settings;
Title suite passed 325 checks, including 20 new registration/donation checks.
Coverage includes 3-flower tulip weighting, stacked/final-unit consumption,
duplicate and parallel requests, self/bound refusal, suppressed receipt rollback,
month rollover and postcommit failure. The rollback retry fixture verifies saved
preimages and explicitly rearms its test player; it is not a production reconnect
fixture. Full reload, NPC interaction, results, award lifetimes and native acceptance
remain open. No new ordinary-player service is enabled by this store alone.

Commands: MSBuild Revora.csproj with Configuration=Debug, Platform=x86,
ImportDirectoryBuildProps=false, TreatWarningsAsErrors=false, OutputPath and
IntermediateOutputPath under obj/mrms2017-validation. The existing
Verify-PotencyHunt.ps1 -TitlesOnly harness was redirected to that isolated output
by obj/verify-mrms2017.ps1. Logs: obj/mrms2017-build.log and obj/mrms2017-tests.log.
The binary was built from the current workspace, including preserved preexisting
changes; this is focused feature evidence, not clean-release acceptance.

## Monthly results and award recovery (2026-09-25)

`MrMs2017.Results.cs` freezes the top three supported entrants per category using
the donation transaction's season lock. Close time is the next Pacific month's
midnight, including daylight-saving conversion. Concurrent closes cannot rewrite
results; zero-support entrants receive no prize. Ties use registration time then
character UID. Result insertion and the close marker commit together.

Startup initializes the tables. Existing progression/wardrobe reads recover up to
12 pending seasons per character in a transaction: exact source receipts plus an
award acknowledgement. Missing acknowledgements roll back all grants. Existing
receipts must match their title and dates before recovery can acknowledge them.
Prizes use the approved tier lifetimes measured from close, including permanent
first-place Romance wings. This does not activate registration through an NPC.

Validation: isolated legacy x86 build passed; title suite passed **342 checks**
(17 additional result/award checks). Tests cover concurrent freezes, support
ordering, UID tie-breaking, zero support, acknowledgement rollback, concurrent
recovery, exact receipt recovery, ordinary wardrobe ownership, delayed expiry and
a permanent winner subsequently receiving a third-place temporary wing. The
second-season placement is a persisted-result fixture, not a second full voting
campaign. A fixture timezone mismatch was corrected by explicitly storing its UTC
close as DateTimeOffset. Production writes use explicit timestamptz parameters.

Still pending: brief NPC route with pinned selections, automatic monthly closing,
full reconnect acceptance and genuine-client validation. No live deployment or
server restart was performed. Prior workspace/validation limitations still apply.

## Automatic closing (2026-09-25)

The world timer now calls `MrMs2017.Tick`. After successful table initialization,
it polls at most once every 30 seconds, taking at most 12 saved overdue seasons
per pulse. Existing season rows are the durable work queue; only registration or
donation creates an active month, so downtime does not manufacture empty events.
Concurrent timers share the transactional season lock with donation and closing.
A failed close remains pending for a later pulse. Errors report exception type
without credentials. Calendar writes do not touch inventory or send player rewards;
normal wardrobe recovery handles the resulting prize receipts.

Isolated build and **349 title checks** passed. Seven added checks cover downtime
recovery, the exclusive current-month boundary, concurrent pulses, idempotent
polling, preservation of the next window, and Pacific daylight/standard close
times. NPC selection and full reconnect/native acceptance remain pending. Source
integration is not a live deployment; the running server was not restarted.

## Ordinary-player NPC route (2026-09-25)

NPC 380 in Twin City now offers **Romance flower contest**, separately from the
2016 edition. The menu supports free entry, current top-three standings per
category, paginated contestant and inventory-flower selection, a named-recipient
confirmation, and rules. No GM status or owner identity is required. Contestants
are paged by stable character ID; the player's displayed candidate IDs, inventory
UIDs, season and confirmation request are pinned to that session. Inventory writes
use the existing atomic donation path under the player inventory lock.

Only displayed actions are accepted. Confirmation is consumed before donation;
repeated clicks cannot reuse it. Farewell, a different NPC action, or a month
change clears the old selection. Every page requires a canonical ordinary player,
the Twin City NPC within six tiles, and no active booth or trade. Standings read
committed support. One selected bundle consumes one stack unit, not the stack.

The isolated x86 build and **361 title checks** pass, including 12 new NPC checks:
ordinary registration/donation, forged action refusal, category selection,
confirmation-before-consumption, single-unit debit, replay refusal, updated
standings, farewell invalidation, rollover refusal and distance gating. Fixtures
exercise the page/action methods and farewell dispatcher; they do not prove native
dialog rendering or full network/reconnect acceptance. Those remain open, as does
live deployment. Existing owner changes remain outside this feature commit.

## Pagination and wardrobe reconstruction (2026-09-25)

The title suite now passes **379 checks** against the unchanged NPC checkpoint
build. Added coverage enters ten ordinary contestants, traverses both candidate
pages, rejects an absent option, traverses nine inventory bundles forward/back,
and sends concurrent confirmations. Only the pinned ninth bundle is consumed;
the pinned ninth contestant receives its support. A separate winner reconstruction
clears in-memory wardrobe/achievement objects, reloads wardrobe data and verifies
the permanent Romance wing remains owned and equipped after timed titles expire.
This is database-backed wardrobe reconstruction, not a complete socket reconnect
or process restart. Flowers remain fixture-supplied, so their ordinary acquisition
is an explicit remaining earning prerequisite. No additional build was necessary
because this checkpoint changes fixtures and audit documentation only.

## Magnolia-earned flower journey (2026-09-25)

The existing Magnolia route supplies contest-eligible flowers without a new shop:
free unbound normal seed, planting reward CPs, next-day one-CP grade lottery,
Elite seed planting and a possible unbound orchid bonus. The new combined fixture
starts the donor with durably zero CPs, earns the upgrade funds, produces and
reloads the orchid, donates through the contest NPC, closes the month and recovers
both category champions' two titles plus permanent Romance wings.

The full **Magnolia suite passed 879 checks**, including 12 checks for this route.
The test controls valid seed/bonus random outcomes; this proves a reachable earned
path, not a guaranteed reward or measured probability. Live 20% bonus odds remain
unchanged. Time advances cover three planting days and monthly close. Character
level remains fixture-provided and inter-map positioning is direct test setup;
the new fixture does not establish native travel, elapsed real-time play or socket
reconnect. Production inventory reload and durable support/award paths are exercised.
The initial test used a stale town map reference after other fixtures replaced the
registered map; using the currently registered map fixed that fixture failure.

All five 2017/ Romance entries now have a server-validated ordinary earning route
in the wardrobe audit. Genuine-client and full-process acceptance remain pending.

## Magnolia earning through actual game/database restarts (2026-09-27)

`tools/Verify-ArenaProcess.ps1 -FlowerContestOnly` passes four game launches and
two PostgreSQL restarts on the owned private clone. Evidence is
`obj/flower-contest-process.log` (ignored). It reuses game SHA256
`E9EEF3BBD597B78F7823E6314E15A4E9183C15525E2FE9E561E392155A85526E`;
only the test helper is compiled, with warnings treated as errors.

- [x] An ordinary donor starts with zero CPs, enters the loaded garden through
  the Daily Envoy and plants a free normal seed. On two later controlled days,
  earned CPs pay the ordinary one-CP grade lottery; planting type729308 produces
  two unbound orchid bundles under controlled valid bonus draws.
- [x] Force process exit after saving the donor, restart game/database and reload
  both earned bundles and the exact CP balance. No flower, completion, vote,
  score or prize receipt is supplied by the helper.
- [x] Two ordinary male/female contestants register through the loaded NPC380.
  The donor selects each category and confirms one flower donation through the
  normal dispatcher. Duplicate confirmations give no second credit; each saved
  candidate receives exactly3 points and both flowers are consumed.
- [x] Force another exit before judging, restart game/database at monthly close,
  and let the real judging/recovery code produce both champions. Each can equip
  the local title, champion title and Romance wings. Database checks require
  six grants:360-day local titles,365-day champion titles and permanent wings.
- [x] Another process restores both equipped champion titles and wings before
  new equip requests. Repeated judging/recovery retains the exact six-award
  identity/time fingerprint, consumed flowers and donor CP balance.
- [ ] Authenticated/native play, natural elapsed calendar time, other placements,
  and live deployment remain outside this process test. Existing component
  evidence for other placements is not replaced by this two-champion route.

The helper supplies eligible profiles, NPC proximity/planting coordinates and
valid RNG/UTC clock values; normal production odds and clocks are unchanged.
It uses the installed NPCs and production item/character loaders. This is an
offline-profile game-process journey, not an authenticated socket playthrough.
The initial helper compile omitted its new source file; the explicit compile list
was corrected before the complete successful run. No production-code fix was
needed. The marked clone is removed and private processes stop on success;
live services, installed client and wardrobe inventory counts remain unchanged.

## All six placements across process restarts (2026-09-27)

The same four-phase `-FlowerContestOnly` route now earns six orchid bundles over
seven controlled Magnolia days and donates one to each of six ordinary entrants.
Both categories contain three equal-score contestants registered at the same
controlled instant. Saved placements follow the documented UID tie-break.

- [x] All six placements earn and equip their local titles and Romance wings;
  only the two champions receive champion titles.
- [x] Each recipient's wing source and expiry match placement: permanent for1st,
  360 days for2nd and180 days for3rd. All six local titles last360 days; the two
  champion titles last365 days. Exactly14 matching grants are required.
- [x] The final game process restores every selected title and wing before new
  equip requests. Award identities/times, consumed flowers and donor CP balance
  retain the same fingerprint/state after repeated judging and recovery.

All four phases and both database restarts pass in
`obj/flower-placements-process.log`, on the same game hash above. The first run
stopped at a helper cast of the integer placement column to `long`; correcting
the conversion allowed the complete route to pass without a production change.
The previous two-champion report is superseded for placement coverage. Native,
authenticated TCP, natural-time and deployment gates remain open; these tests
still use controlled RNG, eligibility, proximity and calendar time. No wardrobe
inventory-count increase is claimed.
