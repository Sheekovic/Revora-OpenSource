# Guild-office earning: implementation decisions

Reviewed 2026-09-26 against source base `cb0fc29` and the local 6711 achievement
catalog SHA-256 `df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.

## Evidence and identity

The catalog separates honorary appointment (10221), deputy leader (10222),
manager (10223), supervisor (10224) and steward (10225). Honorary manager or
honorary supervisor must not substitute for their non-honorary achievements.

The [official privileges guide](https://co.99.com/guide/guides/guild1,2.shtml)
distinguishes automatic donation-based manager/supervisor ranks from leader
appointments. It charges the leader's personal CPs for honorary offices:
650 for honorary deputy, 320 for honorary manager, 270 for honorary supervisor,
and 100 for honorary steward. The local `GuildCommand.SendPromote` advertises
these same fees. This supports those prices; it does not verify packet timing,
role tenure or the entire current guide as the exact 6711 ruleset.

The [older official guild guide](https://co.99.com/guide/guides/guild1.shtml)
uses guild-level-dependent office capacities, unlike Revora's fixed capacities.
The later privileges guide also differs in shared battle-power behavior. Do not
silently combine versions or describe the current fixed caps as authentic.

## Findings at source base cb0fc29

- `GuildCommand` groups actions 28/29/34 as direct appointments. It changes
  in-memory members/counters first. The GuildRank setter updates memory only;
  any later character save is separate, and offline recipients lack that path.
  The honorary branches do not deduct the advertised CP fee. (This corrects
  the initial research note's assumption that the setter saved immediately.)
- The deputy limit advertised by `SendPromote` is ten while the handler permits
  six. Honorary roles use four, steward three, aide six and follower ten in
  the current handler. These are local implementation facts, not verified
  original limits.
- `GuildLeadership.TryHandle` already bounds and authenticates leader-transfer
  packets and protects the online/offline ownership transaction. Other office
  changes must receive equivalent validation rather than inheriting the long
  legacy appointment switch.
- `GuildTable.Materialize` calls `CreateMembersRank` before spouse projections.
  `CreateMembersRank` resets offices below rank 920 and then assigns manager,
  honorary and other ranks from runtime ordering. Thus merely saving a paid
  honorary appointment will not preserve it through a full guild reload.
- Profile requests no longer invoke rank calculation. This does not eliminate
  the startup recalculation above. Do not count those calculated ranks as new
  authoritative achievement receipts.

## Remaining coherent implementation

- [x] Resolve one documented 6711/Revora capacity policy and apply it to both
  the appointment UI and server validation. Reuse confirmed fees; reject
  authority forged through stale runtime membership.
- [x] Replace the appointment mutation route with actor/target session ownership,
  ordered character locks, saved guild authority, saved slot counts and one
  transaction for any fee, target office and achievement evidence.
- [x] Preserve explicit paid/appointed offices across production guild loading,
  including offline targets, without letting automatic donation ranks overwrite
  them. Keep spouse derivation and leader transfer consistent.
- [x] Implement manager/supervisor donation selection from durable eligible
  member statistics with deterministic ties and documented category thresholds.
  Preserve separate honorary and ordinary office identities. See the
  [approved Supervisor edition](#approved-supervisor-edition-2026-09-30).
- [x] Verify ordinary online/offline recipients, insufficient funds, caps,
  forged authority, replay, suppressed writes, rank demotion and full
  guild/character reload in the documented isolated
  suites. The dedicated Supervisor restart and authenticated view are below.
- [ ] Close remaining reciprocal/membership/disband race coverage and complete
  genuine6711 office/achievement presentation and deployment. Supplied
  prerequisites do not prove a retained full earning journey.

## Implemented appointment edition

Revora retains its existing server capacities: six deputies, four of each
honorary office, three stewards, six aides and ten followers. The UI now matches
those caps. These are documented Revora rules, not the guide's guild-level
capacity table. Fees use the confirmed personal-CP amounts above. Only the
current saved guild leader can buy honorary appointments; no spouse may spend
the leader's CPs. Existing deputy free steward/aide/follower and manager aide
permissions are retained, with strict lower-rank and same-guild checks.

Actions 28/29/34 and discharge share the validated appointment parser. Actor
and target installation leases, owner/peer inventory locks and the guild gate
protect live ownership. Database character rows are locked by UID before the
guild row. The transaction checks saved actor and target membership/ranks,
counts saved offices, and writes the target rank, any personal fee and earned
achievement together. Failure rolls everything back and fences affected online
sessions. Repeating the same role is a no-op; a deliberate demotion followed by
a new honorary appointment is a new paid appointment. No historical rank cache
is used to backfill achievement credit.

Only honorary appointment 10221, deputy 10222 and steward 10225 are completed
by this route. Honorary ranks do not award ordinary manager or supervisor
achievements. Offline recipients load their evidence on login; demotion keeps
the already earned achievement. Full guild materialization now preserves saved
offices rather than running the unsaved donation-rank reassignment. Spouse
derivation only raises lower offices and cannot erase a higher paid/appointed
office. Manager/supervisor automatic earning and its durable ranking pass remain
open; no replacement ranking algorithm is claimed here.

The suite covers ordinary online/offline recipients, all honorary fees,
insufficient CPs, capacity, replay, demotion/discharge, malformed names, saved
authority mismatch, evidence rollback/retry and the production guild loader.
Extended membership/disband races, original guild-level capacities and native
client acknowledgment remain pending. Existing leadership transfer retains its
separate path and rank behavior.


## Durable donation-input correction (2026-09-26)

The production guild materializer incorrectly loaded `PKDoantion` from the
character's PK penalty field, left `PkDonation` at zero, and loaded
`GuideDoantion` from CP donations. Rankings therefore displayed penalty points
as contribution, counted CPs twice in their raw total, and a later character
save could overwrite a saved guild PK donation with zero.

Loading now reads `GuildPkDonation` into both PK donation projections. Guide
credit stays zero because the reviewed source has no durable Guide donation
producer or saved Guide field; ordinary CP payments are not evidence of
power-leveling guild mates. No historical points are fabricated or migrated.
The saved CP/silver balances and their donation counters are unchanged.

The isolated build and **499 guild-season checks** pass, including three new
checks against production guild materialization, PK/Guide ranking projections,
and character autosave. Distinct fixture values prove that seven saved guild PK
points remain seven while 99 PK penalty points remain 99; 37 CP donation units
do not create Guide credit. Logs: `obj/guild-donation-load-build.log` and
`obj/guild-donation-load-tests.log` (ignored).

## Remaining rule and data work

The [official privilege page](https://co.99.com/guide/guides/guild1,2.shtml)
distinguishes automatic Manager/Supervisor offices from honorary appointments.
The [level table](https://co.99.com/guide/guides/guild1,1.shtml) gives changing
capacities, whereas Revora's disabled legacy routine uses fixed counts, ranks
primarily by arsenal credit and even assigns honorary offices without payment.
Re-enabling that routine is not a valid implementation of the earning paths.

The [donation guide](https://co.99.com/guide/guides/guild1,3.shtml) distinguishes
currency amounts from contribution points: CPs multiply by 20, while silver has
a rate change above 300,000 donation points. Current `GuildDonations.Donate`
stores paid currency amounts. A future official-rule ranking must normalize
those amounts without rewriting the funds or pretending raw totals are already
points. Version-specific thresholds, guild-level capacities, deterministic ties,
protected paid offices and durable missing-category producers still need a
coherent ranking transaction. The [older guide](https://co.99.com/guide/guides/guild1.shtml)
also separates PK and Guide contribution from character penalties and CPs; it
does not supply a justification for the erroneous loader mappings.

Achievements 10223/10224 remain disabled. This repair fixes a prerequisite and
existing persistence behavior; it is not a completed automatic-office feature.
Native ranking display and deployment remain pending.


## Consistent loaded donation rankings (2026-09-26)

The old loader rebuilt every member's ten ranking lists for every newly read
member, while incrementing each category's rank counter across recipients.
Members consequently received different rank numbers for the same ordering.
Equal scores also depended on unordered database reads. Flower rows read unused
alias properties instead of the four saved guild flower counters.

The loader now builds each category once from the complete membership snapshot,
orders by descending value then ascending character UID, and numbers each list
from zero. Each recipient gets an independent list of value-type rows. Existing
silver/total positive-only filters and other category inclusion rules remain.
Materialization refreshes the snapshots after arsenal loading and spouse-rank
projection so their row metadata reflects the completed load.

The isolated build and **512 guild-season checks** pass. Thirteen added checks
verify all ten lists agree across recipients with contiguous ranks, tied PK
scores use UID order, all four saved flower counts survive projection, and
mutating one recipient's list does not change another's. Logs:
`obj/guild-ranking-build.log` and `obj/guild-ranking-tests.log` (ignored).

This repairs loaded ranking data. It does not normalize raw currency donations,
implement live ranking refresh or automatic offices, enable achievements
10223/10224, or verify native pagination/display. Those remain explicit work.


## Contribution score normalization (2026-09-26)

Member score projections now separate paid currency from contribution points:
CP contribution is paid CPs times 20; silver contributes one point per 10,000
silver until 300,000 points (3,000,000,000 silver), then one per 100,000 silver.
Fractional points carry implicitly in the cumulative raw paid amount and do not
round upward. The old silver helper incorrectly switched after only 300,000
silver, and the CP helper multiplied by one.

`DonationScore` calculates the five-category total in decimal arithmetic,
excluding flowers. The legacy ulong total and CP helper saturate at their return
width; the existing guild total and ranking packet projections saturate at uint
width instead of wrapping. Loaded ranking rows now expose contribution points
for silver, CP and total, while saved raw counters, payment preimages and guild
funds retain currency amounts. No treasury or player currency migration occurs.
Automatic office selection must use the wide score rather than display-capped
values. Live ranking refresh and role assignment remain open.

Build and **523 guild-season checks** pass, including eleven new score checks:
eight silver boundaries through the ulong maximum, independent five-category
totals, flower exclusion, display saturation and loaded ranking normalization.
Existing ordinary donation, rollback, autosave and guild materialization checks
also pass. Logs: `obj/guild-score-build.log` and `obj/guild-score-tests.log`
(ignored). The official donation source above supports the numeric interpretation;
genuine 6711 labels, total presentation and deployment remain pending.


## Committed currency donation refresh (2026-09-26)

Successful CP/silver donations now rebuild the guild's ranking snapshots after
their transaction commits and live counters are installed, while the existing
guild ownership gate is held. Snapshot membership comes from the current member
dictionary, and the legacy indexed member view is replaced from that same
snapshot. This includes ordinary members added since startup instead of relying
on the loader's old list. Donor and peer ranking data are available immediately
for subsequent requests; this does not claim an unsolicited native UI refresh.

Duplicate, unaffordable or failed donations do not republish ranking lists. A
failed fund update rolls back currency/contribution changes and leaves the old
snapshot. A reloaded session can retry and publish the committed score once.

Isolated build and **528 guild-season checks** pass, with five new checks for
CP and whole-point silver refresh, donor/peer membership, duplicate suppression,
failed-write snapshot retention and retry after production character reload.
Logs: `obj/guild-live-rank-build.log` and `obj/guild-live-rank-tests.log` (ignored).
Arsenal/flower/membership-change refresh, whole-response ownership/pagination,
automatic offices, native UI acceptance and deployment remain separate work.


## Owned ranking pages (2026-09-26)

Packet 2101 now serializes one current member snapshot under session ownership
and the guild gate. All ten categories share bounded ten-row pagination, with
the existing 20-entry category and 100-entry total limits. Empty and partial
pages report their actual row counts. Invalid framing, stale membership and
busy guild mutation are rejected before a response is constructed.

The isolated build and **863 guild-season checks** pass, including 335 added
checks covering page boundaries, caps, row identity, malformed requests, stale
sessions/membership and contention. Logs: `obj/guild-pages-build.log` and
`obj/guild-pages-tests.log` (ignored). The fixtures verify source-derived bytes;
native navigation remains pending. This completes response ownership/pagination,
not automatic Manager/Supervisor earning or deployment.


## Automatic office implementation contract (2026-09-26)

Rechecked the official guild-level table's HTML cells, rather than inferring
merged columns from extracted text. Manager slots for levels 1..9 are
1,1,2,2,4,4,6,6,8. The Supervisor row has three zero cells, then colspan=4
for one per category and colspan=2 for two: levels 1..3 have none, 4..7 one,
8..9 two. Source: https://co.99.com/guide/guides/guild1,1.shtml .
The privileges guide confirms automatic donation ranking but does not publish
Supervisor minimums. Its Agent thresholds must not be relabeled as Supervisor
thresholds. A question to the owner is pending for positive-score Revora
qualification versus leaving Supervisors locked. No default has been applied.

Current source input audit:

| Input | Durable source | Earning-path limitation |
| --- | --- | --- |
| Silver / CP | entities guildsilverdonation / guildconquerpointdonation, GuildDonations transaction | Normalize raw currency before selection |
| Arsenal | pt_arsenal_inscribed and entities arsenaldonation, GuildArsenalPurchases transaction | Include committed removal and equipment changes |
| Guild PK | entities GuildPkDonation, loader and autosave | No active increment producer found in production C# search; do not invent awards from PK penalty points |
| Guide | None found; loader explicitly zero | Must remain zero until a durable guild power-leveling producer exists |
| Flowers | revora_flower_balances totals, FlowerDelivery transaction | Existing guild flower columns are separate; FlowerDelivery does not update them |

The flower distinction is material: ranking only existing GuildRouses/Lilies/
Orchids/Tulips would leave ordinary new flower deliveries unable to earn the
corresponding offices. The implementation must explicitly select lifetime
received totals (as the official privileges description says), or introduce
and document guild-tenure counters. Do not silently conflate both sources.

Implementation sequence for one coherent earning batch:

1. Resolve the pending threshold edition; encode deterministic UID ties and
   category priority, selecting Managers before category Supervisors. Preserve
   saved appointed/honorary offices and spouse roles.
2. Reconcile one guild under session leases and online owner locks, then the
   guild gate. Lock affected character rows in UID order before the saved guild
   row, matching appointments. Read authoritative contributions and membership
   in that transaction; do not rank display-capped packet rows.
3. Persist rank changes and achievement receipts atomically. Track automatic
   office ownership so a later reconciliation cannot demote a manual appointment.
   Revalidate live ownership before commit; publish after commit or fence on
   installation failure. Offline recipients load their committed ranks/evidence.
4. Wire reconciliation to committed contribution/membership changes and startup
   with bounded retry; avoid starting it while a donor holds incompatible locks.
5. Exercise ordinary online/offline earning, rival displacement, ties, paid
   appointment preservation, concurrent donation/appointment, rollback, retry
   and full reload before enabling 10223/10224. Native acceptance stays separate.

This contract records resolved evidence and concrete missing inputs. It is not
an enabled earning path and does not change source coverage or milestone checks.


## Durable received-flower ranking pages (2026-09-26)

- [x] Read all four guild flower ranking pages from committed
  `revora_flower_balances.totals`, including offline recipients.
- [x] Preserve legacy `GuildRouses/Lilies/Orchids/Tulips` counters and their
  autosave behavior; viewing a lifetime ranking never migrates those fields.
- [x] Validate ordinary gifts, duplicate sends, receipt rollback, independent
  category mapping, UID ties, offline recipients, full guild materialization,
  departure/rejoin, and stale saved viewer membership.
- [ ] Use authoritative contribution inputs in automatic office reconciliation;
  Supervisor minimums still require the pending owner decision.

The official privileges guide describes received flowers as a qualification
input: https://co.99.com/guide/guides/guild1,2.shtml . It does not explicitly
settle lifetime versus guild-tenure accounting. Revora's ranking pages now
explicitly use saved lifetime received totals, the durable earning source of
normal gifts. This is a documented Revora accounting choice, not a claim of
verified original tenure semantics. Received male gift equivalents use the
existing four-category balance mapping; no new gift or award is introduced.

Packet 2101 categories 5..8 retain their existing layout and pagination. Each
request reads at most twenty rows in one parameterized statement, ordered by
that category then UID. The statement uses saved guild membership for both the
viewer and ranked characters. Nullable legacy contribution fields become zero;
Guide remains zero without a producer. Non-flower fields use the existing
normalization and packet saturation. The query has a five-second command
timeout; unavailable flower persistence produces no fabricated fallback page.
Connection opening retains the configured database timeout.

The existing session lease, player lock and guild gate still protect the request.
No database writes, rank appointments, fees, or achievement receipts occur in
this read path. Legacy in-memory donation lists remain compatibility projections;
a future automatic office selector must not treat their flower fields as
lifetime evidence. This completes the ordinary flower display input gap, not
Manager/Supervisor earning.

Validation: isolated Debug x86 build passed with existing warnings. The
`-GuildSeasonOnly` fixture passed 884 checks, including the new ordinary gift
and persistence cases; this total overlaps prior guild suites. Evidence logs:
`obj/guild-flower-build.log`, `obj/guild-flower-tests.log` (ignored).
No live schema, client install, deployment or restart was performed.


## Committed inscription ranking refresh (2026-09-26)

- [x] Rebuild peer Arsenal and Total ranking snapshots after an inscription or
  owner removal commits, under the existing player/session ownership and guild
  gate. Duplicate requests and rolled-back removals retain the previous snapshot.
- [x] Isolated build and889 overlapping guild-season checks pass, including five
  added assertions for ordinary peer views, duplicate suppression, score removal,
  rollback, and the reloaded owner's successful retry.
- [ ] Reconcile changed equipment values, membership changes and automatic offices
  from authoritative inputs. This change covers inscription/removal transactions,
  not every possible arsenal mutation.

Evidence: `obj/guild-arsenal-ranking-build.log` and
`obj/guild-arsenal-ranking-tests.log` (ignored). No deployment, schema change,
client installation or restart was performed.

The Manager investigation also found a separate level-consistency prerequisite.
`EntityTable.LoadEntity` resets the shared guild level to1 and its arsenal flags
on a member login. `ArsenalSystem.work` then adjusts level from runtime donations
and flags; it contains both >=5,000,000 promotion and <=5,000,000 demotion tests.
Thus exactly5,000,000 qualifies for both branches in one pass. This is a source
finding, not a tested earning fix. There **is** legacy progression code; the
problem is that this mutable projection is unsuitable as authoritative office
capacity evidence. Review its threshold, startup/login interaction and published
maintenance timing before enabling level-dependent Manager awards. Supervisor
thresholds remain a separate pending decision.


## Stable guild-level projection (2026-09-26)

- [x] Replace incremental level flags with one calculation: base1 plus the
  number of distinct, unlocked arsenal slots1..8 with donation >=5,000,000.
  Level is therefore bounded1..9 and equality no longer promotes then demotes.
- [x] Reconstruct the projection after loading saved inscriptions, refresh after
  committed inscription/removal and on the existing arsenal tick, and remove
  the per-character login reset of the shared level.
- [x] Isolated build and922 overlapping guild-season checks pass. The33 added
  checks cover every slot just below/exactly at threshold, repeated evaluation,
  locked slots, corrupt prior level, full capacity, empty recovery, ordinary
  inscription earning, login, full guild reload, rollback and successful removal.
- [ ] Complete automatic office reconciliation with transactional authoritative
  inputs and durable rank/achievement ownership. A corrected display projection
  alone is not sufficient evidence for an automatic appointment.

Official threshold evidence: https://co.99.com/guide/guides/guild1,1.shtml .
The guide specifies updates after maintenance. Revora retains its existing
immediate-update edition, now deterministic; this is not a claim of original
maintenance parity. Manual office caps and paid appointments remain unchanged.
The earlier level-consistency finding is fixed for the runtime projection.
Existing battle-power calculation is outside this change.

Logs: `obj/guild-level-build.log`, `obj/guild-level-tests.log` (ignored).
Build retained existing warnings. No production database, deployment, client
installation or server restart. Native level display remains unverified.


## Automatic Manager earning (2026-09-26)

- [x] Reconcile ordinary Manager (10223) from saved contributions, persist the
  office and achievement in one transaction, and retain automatic ownership in
  `revora_guild_managers` so only these appointments can be displaced.
- [x] Preserve paid/manual offices and untracked legacy Managers. Legacy Managers
  reserve capacity but receive no inferred achievement or fabricated ownership.
- [x] Wire startup initialization and a bounded periodic reconciliation: one guild
  per five-second pulse, ascending guild ID with round-robin wrap and no overlap.
- [x] Validate ordinary donation-to-office earning, competing donors, UID ties,
  rollback, offline earning/login, full guild reload, honorary replacement,
  contention, stale membership/contributions/session, and forged runtime level.
- [x] Implement Supervisor earning under the subsequently approved positive-score
  Revora definition; see the [approved edition](#approved-supervisor-edition-2026-09-30).
- [ ] Verify the genuine6711 office/achievement display and deploy the batch.

Eligibility is Revora's documented edition: a positive saved contribution score,
current Member status or an existing ledger-owned Manager assignment, and a slot
at the guild's saved arsenal-derived level. Other offices are preserved rather
than silently replaced. Score uses normalized silver, CP*20, saved arsenal and
Guild PK contributions; Guide remains zero without a producer. Equal scores
use ascending character UID. The confirmed capacity table for levels1..9 is
1,1,2,2,4,4,6,6,8; existing legacy Managers subtract from available slots. This
is distinct from the fixed Revora manual/honorary office capacities.

Official identity/capacity evidence remains the privileges and guild-level guides
linked above. Positive-score admission, preserving non-Member offices and the
five-second per-guild scheduling policy are explicit Revora choices, not claimed
as exact original timing/eligibility. No CP fee or item reward is introduced.
Achievement10223 is historical and remains earned after automatic displacement.

Each pass captures at most800 members, takes nonblocking session leases/player
locks and the guild gate, then locks saved character rows in UID order before the
saved guild row. Runtime identity, rank, membership and contribution counters
must match the saved snapshot; stale inputs defer the pass. Level derives from
saved unlocked arsenal slots and saved inscription sums, never the display byte.
Two-second lock and five-second statement limits bound database waits after the
configured connection-open timeout. Changed ranks, ownership receipts and
achievement evidence commit together. Runtime/client projections follow commit;
uncertain commit or installation failure fences affected online characters for
reload. Offline recipients use the normal entity and title/achievement login path.
Departed or manually reassigned ledger owners are retired without changing their
new office. A failed pass is retried when the guild next comes around.

Validation: isolated Debug x86 build passed with existing warnings;949 overlapping
guild-season checks passed, including27 new Manager assertions. The source audit
now finds321/337 achievement identities and16 missing producers; this is source
coverage, not a full-roadmap completion percentage or native acceptance.
Logs: `obj/guild-manager-build.log`, `obj/guild-manager-tests.log` (ignored).
No live schema initialization, deployment, client install or restart occurred.


## Supervisor ranking evidence and implementation seam (2026-09-27)

New primary evidence: the [Chinese official guild guide](https://zf.99.com/guide/faq/gybp.shtml)
explicitly describes automatic selection of the highest N contributors for each
of nine Supervisor categories, with N taken from the guild-level capacity table.
It does not specify numeric Supervisor minimums, zero-score qualification or ties.
The [English privileges guide](https://co.99.com/guide/guides/guild1,2.shtml)
refers to qualifying donation/flower requirements without publishing those values.
Its Agent thresholds must still not be substituted for Supervisor rules. These
pages establish a ranking basis, not exact 6711 regional/version parity.

Installed client identity check: `ini/synmembertitle.ini` SHA-256
`b82dc994038793fa97d8652af0168212415f8108b6639bd5ddb3f1056835e1bb`.
Its nine specialized ranks851..859 agree with `Game/Enums.cs`: Lily, Rose, PK,
Guide, Silver, Arsenal, CP, Orchid and Tulip. Generic850 and honorary840 are
separate identities. `Cn_Res.ini` also distinguishes these categories and describes
a minimum-score/next-day appointment prompt, but supplies no numeric minimum there.
This is static label evidence, not verified native appointment behavior.

A refreshed owner question now proposes positive-score category ranking, ascending
UID ties and preservation of higher offices as a documented Revora edition. No
answer is assumed. Achievement10224 remains disabled; source coverage stays322/337.

Implementation prerequisite after that decision: coordinate Supervisor selection
with Manager reconciliation rather than adding an independent competing loop.
`GuildManagers.Reconcile` currently admits Member or ledger-owned Manager status;
a newly assigned Supervisor would otherwise become ineligible for later Manager
promotion. Use explicit automatic-office ownership and one ordered selection/
transaction boundary, while preserving paid/manual and untracked legacy offices.
Reuse saved contributions, flower balances, saved arsenal-derived capacities,
ordered session/character/guild locks and atomic rank/achievement evidence.
Guide contribution still has no verified producer and must not be fabricated.
Validate cross-category competition, promotion from Supervisor to Manager, offline
reload, withdrawal/demotion, ties, rollback and ordinary eligibility before marking
any Supervisor earning milestone complete.

## Approved Supervisor edition (2026-09-30)

The owner selected the recommended positive-contribution Revora rules. Supervisor
selection now shares `GuildManagers.Reconcile` with Manager selection, using one
transaction and the existing session leases, online inventory locks and guild gate.
Managers are assigned first. Remaining eligible Members and ledger-owned automatic
office holders compete for category seats. An automatic Supervisor can advance to
Manager; an outgoing Manager can qualify for Supervisor in the same transaction.

Levels 1?3 have no Supervisor seats, 4?7 have one per category, and 8?9 have two.
The saved qualifying arsenal slots determine level. Positive scores qualify;
ascending character UID breaks ties. One character receives at most one automatic
office. Explicit Revora category priority follows descending specialized rank:
Tulip, Orchid, CP, Arsenal, Silver, PK, Rose, Lily. Guide is excluded because no
verified contribution producer exists. This is documented Revora behavior, not
an assertion of exact original regional appointment rules.

CP, arsenal and PK use saved guild contributions; silver uses the existing
normalized contribution score. Flowers use lifetime received totals from
`revora_flower_balances`, not unrelated legacy guild flower counters. PK can rank
saved contributions; no new PK earning producer is claimed. Eligibility does not
require GM status, a particular account or a character allowlist.

`revora_guild_supervisors` records owned automatic offices separately from existing
Manager receipts. Rank, ownership and achievement10224 commit atomically. Existing
manual/honorary offices and untracked legacy category offices are preserved;
legacy category holders reserve their category seats without receiving fabricated
achievement evidence. Capacity loss can demote only owned automatic assignments.
Achievement history survives loss of office. Failed ownership removal or evidence
insertion rolls back the whole pass. Online projections follow commit; offline
players restore the saved rank and achievement on login.

Validation: isolated x86 Debug build passed with existing warnings; the wider
guild-season suite passed 1,095 assertions, including ordinary CP donations,
received flower gifts, category priority, UID ties, Manager/Supervisor transitions,
concurrent replay, failed-evidence rollback, offline earning, ordinary entity/title
login, full guild reload, manual and legacy office preservation, capacity loss,
and level-boundary checks. The fixture supplies saved guild-level eligibility and
starting purses/gift items; it does not claim end-to-end acquisition of those
prerequisites or an authenticated/native client journey. Guild level earning and
flower/CP transactions retain their separately documented tests.

The regression fixture now initializes the current local-team admission schema,
preserves the arsenal deadline default omitted by CREATE TABLE AS, and loads the
two installed clan envoys required by plaza restoration. These are test setup
repairs, not new gameplay behavior.

Assembly SHA-256:
`D9241D67D23AF633100D6B662DA540049976B91A88E7A218BB6606B46D5177EB`.
Ignored evidence: `obj/guild-supervisor-build.log` and
`obj/guild-supervisor-tests.log`. The suite used only the private loopback25439
candidate and its disposable schema; PostgreSQL was stopped afterward.
Native6711 acceptance, a dedicated full-process restart check and deployment remain
pending. Source coverage is334/337; a compatible320-achievement career remains
unproven.


## Dedicated Supervisor full-process recovery (2026-10-01)

`tools/CheckCandidateSupervisorLifecycle.cs` adds `supervisor-capture` and
`supervisor-reload` to the private full-server lifecycle harness. It reuses the
owned Kingdom candidate database and guild; existing character identities are
refused before fixture creation. Three ordinary non-GM members receive controlled
starting purses of2,000 CPs and donate500,400,100 through GuildCommand. Three
qualifying saved arsenal slots supply guild-level eligibility; this does not prove
acquisition of the guild level, inscription items or starting CPs.

The two higher donors earn Manager offices first. The third earns CP Supervisor
rank857 and achievement10224, with a remaining purse of1,900 CPs and saved100-CP
contribution. The probe records exact automatic-office ownership and achievement
evidence rows before graceful server shutdown.

After a real PostgreSQL restart, a fresh full server startup restores the guild
rank. Ordinary entity loading followed by the normal TitleStorage login refresh
restores achievement10224 in the outgoing achievement bitset. Reconciliation
replay preserves the exact saved ownership/evidence rows, contribution and purse.
The process and PostgreSQL startup identities differ from capture. This is full
server plus ordinary loader/login-handler evidence, not SRP/socket or native UX.

Both phases pass against production assembly SHA-256
`5C8FA4FE3F0E06FC3C55F2BFDC0B123820CA36335F96BFEC87FBA91847488EC7`.
Only private test code changed; no production rebuild or deployment was required.
Redacted logs are `obj/supervisor-host-1.log` and `obj/supervisor-host-2.log`.
The private PostgreSQL instance is stopped afterward. Authenticated/native client
acceptance, complete prerequisite earning and live deployment remain unclaimed.


## Supervisor authenticated achievement view (2026-10-01)

The `supervisor-wire` phase starts the full private server against the previously
earned and restarted database, preserving the same rank, contribution, purse,
ownership and achievement evidence. A dedicated Player account authenticates by
SRP and performs the normal Game DH handoff and world-login actions. The separate
wire process requests its own achievement view through packet1136/action1 and
asserts the returned UID,13-word shape and completed10224 bit. It also receives
the expected1,900-CP character balance.

While that socket remains connected, the host verifies its ordinary non-fake
Player identity, guild50003, CP Supervisor rank857,100-CP contribution and existing
10224 evidence. After disconnect, the saved automatic-office and evidence records
remain byte-for-byte equal as JSON text to the original checkpoint. No rank or
achievement is injected for this phase; only the disposable account credential
and character linkage are provisioned for authentication.

The phase passes against assembly
`5C8FA4FE3F0E06FC3C55F2BFDC0B123820CA36335F96BFEC87FBA91847488EC7`;
redacted evidence is `obj/supervisor-wire.log`. The server shuts down gracefully
and the private database is stopped. This verifies server protocol behavior using
the private wire client, not rendering in a genuine6711 client. Native acceptance,
complete prerequisite earning and live deployment remain pending.

## Retained appointed offices and leadership return (2026-10-02)

Retained ordinary character4294907001 continues from the artifact checkpoint as
CareerBanner51002's sole member and leader. Existing spouse4294908901, previously
without a guild, requests admission and receives the leader's invitation through
normal1107 GuildCommand packets. No membership or rank is inserted by the fixture.
The original leader transfers the banner to that member. The new leader appoints
the original character HonoraryManager for320 personal CPs, then Steward, then
DeputyLeader. Those explicit appointments earn10221,10225 and10222 respectively.
The transfer's automatic deputy rank is not used as substitute earning evidence.

An immediate identical honorary request preserves the complete snapshot and
charges nothing. The new leader finally transfers leadership back. Actual full
game/database restart restores the original leader, the admitted member as deputy,
the exact320-CP debit, guild and membership rows, all three achievement receipts,
and the nested prior artifact/equipment/social/event possessions and rewards.
Replaying the former leader's transfer request without leadership authority and
refreshing achievements preserve that state. Neither ordinary character is a GM.

Both `career-retained-office-earn` and `career-retained-office-reload` pass on
unchanged private candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Checkpoint `revora_retained_office_probe` is step6. The spouse's saved55,000-CP
pre-appointment purse includes its earlier controlled Couple PK reward; earlier
character/funding provenance limitations remain unchanged.

Two fixture corrections preceded earning: the first snapshot referenced a
nonexistent appointment ledger (production stores these receipts in progression
evidence), and the first admission attempt omitted friend/partner/mentor loaders
used by the invitation display. Both were corrected using existing production
loaders; no policy or gameplay rule was relaxed. Controlled positioning and
server-dispatched packets do not establish authenticated socket/native acceptance.
Automatic Manager/Supervisor earning on this retained character is still separate;
honorary titles do not satisfy those achievements. No production build, live
restart, release or client patch occurred.

## Retained automatic Manager and restored leadership (2026-10-02)

Read-only preflight finds CareerBanner51002 has one unlocked arsenal category8,
with1,362,330 saved inscription contribution, and the retained founder has
5,100,000 silver donation. The spouse has zero donations. Thus the guild is still
level1 for automatic-office capacity, with one Manager seat and no Supervisor
seat. Earlier retained inscription evidence must not be read as eight unlocked
categories merely because its category identifier is8.

The ordinary leader transfers authority to its previously admitted spouse, who
uses the normal appointment packet to return the founder to Member. Production
GuildManagers.Reconcile selects that member from saved, positive contributions;
no donation, rank, eligibility or evidence is inserted by the fixture. It writes
Manager ownership and achievement10223. A full game/database restart preserves
the office, contribution, receipt and exact prior career snapshot. Reconciliation
replay changes nothing. The acting leader then transfers the banner back; normal
reconciliation removes the obsolete automatic-office ownership while retaining
10223. A second full restart preserves the restored leader, spouse deputy, all
prior assets/rewards and the permanent achievement.

All `career-retained-manager-{earn,restore,reload}` phases pass on their first
execution, on unchanged private candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Checkpoint `revora_retained_manager_probe` is step2. A read-only inspection query
needed the existing text contribution column cast to numeric; no gameplay state
was changed by that correction. Server-handler coverage and earlier mixed funding
provenance remain distinct from authenticated/native acceptance and natural
competitive progression. No production build or live restart occurred.

Retained Supervisor earning remained open at that checkpoint; the follow-up below
closes its server earning route. It requires at least guild level4:
three unlocked categories with at least5,000,000 saved inscription contribution
each, plus eligible members filling the Manager seats so the retained character
can qualify for a specialized office. Existing received flowers can supply a
category score, but cannot replace guild-level or ranking prerequisites. Use
ordinary acquisition, inscription and donation paths; do not seed those totals.

## Retained earned guild level and Tulip Supervisor (2026-10-02)

The same ordinary founder buys three class sets through RevoraMaster900000001
options2/12, paying the configured quote for each and retaining the exact bound
+12, two-socket item UIDs. Stone City storage provides space. An ordinary
15,000,000-silver donation funds the Armor1 and Ring3 arsenal unlocks for5,000,000
and10,000,000 guild silver; Headgear8 was already unlocked. Owned core pieces plus
purchased set pieces are inscribed through bounded2203 packets. Each of the three
categories reaches exactly5,449,320 contribution, twelve inscriptions total, and
the character's saved arsenal contribution becomes16,347,960. Repeated unlocks
charge nothing. Full game/database restart preserves purchases, funds, items,
inscriptions and prior career state. This supplies guild level4 without seeding
arsenal totals, donation or eligibility.

Existing ordinary arena participants4294908902/4294908903 join through requests
and leader invitations. Normal CP trades from the retained founder fund their
normal guild donations of818,498 CPs each. Their prior profiles, items and purses
are preserved/accounted for. After a temporary handover to the saved spouse and
normal discharge to Member, production reconciliation selects the two higher
contribution scores as Managers and the retained character as Tulip Supervisor.
The qualifying tulip was earned and delivered in the earlier Magnolia route;
no flower total, office, contribution or achievement is inserted by the fixture.

Actual restart retains the Supervisor rank, automatic ownership and10224 evidence.
Reconciliation replay is unchanged. The acting leader returns the banner, and
production reconciliation retires the obsolete Supervisor ownership while
preserving10224. Another restart restores the original leader, spouse deputy,
both donor Managers and exact nested item/effect/currency/event evidence. All
five retained guild-office achievements now have compatible server earning paths.

All `career-retained-guildlevel-{earn,reload}` and
`career-retained-supervisor-{earn,restore,reload}` phases pass on first execution
with unchanged private game SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Final checkpoint `revora_retained_supervisor_probe` is step3; the earlier
`revora_retained_guildlevel_probe` step3 remains historical evidence. No production
source/build or live service changed. Clock/approach controls, supplied opponent
qualification and earlier mixed-provenance currency remain explicit limitations;
these results do not certify natural competitive progression or native UX.
