# Skill Team PK restoration

## Current status (2026-09-27)

**Implemented in staged server source; not deployed.** The closed-clerk and
unfinished-cap statements in the initial audit below are historical. They must
not be used as the current implementation checklist.

| Requirement | Current evidence | Still open |
| --- | --- | --- |
| Combat rules | Owned Chi/subclass/BP/equipment projections, whole-character recalculation, HP bounds and restoration have focused fixtures | Catalog619301 has no compatible capped row and is safely refused; native combat and remaining timing races are not proven |
| Round and final earning | Durable round entitlements/delivery, lost-COMMIT reply tests, 33-team/66-player fixture, eight-team TCP combat/final claim and reward persistence across game/database restart | Native presentation and remaining timing races |
| Calendar and clerk | Approved Wednesday19:45-20:00 registration; active world pulse; SkillRegistrar801717 at1002,300,146; coherent private startups | Natural Wednesday observation and live deployment |
| TCP admission/recovery | Player-created teams, ordinary authenticated clerk entry; isolated process termination and cancelled-run return recovery; champion reauthentication and duplicate-claim refusal after restart | Native invitation UX; additional failure timings beyond the tested boundary |

Current implementation anchors are `TeamSkill.cs` (clerk, match ownership and
round ledger), `Thread.TickRestoredEvents` (calendar call), and the existing
stat-projection callers. The installed-clerk implementation and closed-desk
exclusion were rechecked against source; the retained
`obj/skill-tcp-crash.log` ends with two successful private authentication/recovery
cycles and a stopped database. This is a status reconciliation using existing
evidence, not a newly run acceptance test or a release.

Read the later [source activation](#skill-clerk-and-calendar-source-activation-2026-09-27),
[active pulse correction](#skill-calendar-reachability-correction-2026-09-27),
and [TCP interruption evidence](#skill-ordinary-tcp-admission-and-process-interruption-2026-09-27)
for their exact scope. The dated sections below preserve the work's chronology;
unchecked prerequisites superseded by those sections are not fresh work items.

## Evidence and activation boundary (2026-09-27)

Local Skill Team PK is an unfinished roadmap earning path, distinct from regular
Team PK, Quarantine and cross-realm Team PK. Its clerk801717 remains parked/closed
and its world calendar remains commented out. The shared final-result/claim ledger
already supports its CP terms plus bound P7, but that alone does not make the
underlying tournament ready.

The [official PC guide](https://co.99.com/guide/quests/skillteampk.shtml), reviewed
2026-09-27, describes Wednesday registration at19:45, with bracket starts from
20:00 through20:06. It specifies equipment limits by slot (levels120/117/116/112),
Super quality, +4, one socket/first gem and -3 blessing; Earl nobility, no
clan/guild/mentor battle power, subclass level5 except Apothecary, and Chi capped
at60% of its maximum. It also describes a one-minute preparation, ten-minute
knockout matches and fifteen-minute finals.

This current guide is a comparison source, not proof that every value or NPC
coordinate belongs to genuine6711. The [official prize update](https://co.99.com/guide/prize_upgrade/)
lists a different19:55 entry start and per-round EXP/Honor rewards. Resolve the
edition discrepancy explicitly before presenting an exact retail calendar or
reward contract; do not silently combine editions.

Source audit:

- The initial audit found writers for `Entity.InSkillPk` but no stat readers.
  The Chi contribution integration below now adds an ownership-checked reader;
  equipment and other required caps remain incomplete.
- `Match.EndOwned` directly inserts item720981 for a winning Skill team member;
  it has no durable per-match reward receipt or full-bag retry at that site.
- Skill opening was unguarded, assumed a non-null bracket array and announced
  Thursday, while the commented calendar says Wednesday.
- Registration currently uses `SubscribeTimer(1)` and shares legacy match timing.
  Neither has been accepted as the documented Skill edition's timing.

## Completed prerequisite

- [x] Serialize `SkillTeamTournament.Open`, refuse missing/empty/already-open
  brackets and return success only for an actual opening. Replace the incorrect
  Thursday announcement with the actual current registration duration.
- [x]37 entry checks and192 combined ledger/world checks pass after an isolated
  build. The expanded database fixture runs eight concurrent openings for each
  series, verifies one success/four distinct durable brackets/real subscriptions,
  and refuses same-date reopening after the in-memory flag resets.

Evidence: `obj/skill-team-opening-build.log`, `obj/skill-team-entry-tests.log`,
`obj/skill-team-opening-tests.log`. Expected injected database failures remain part
of the existing recovery tests. The private database is stopped. No client or
candidate deployment was needed for this inactive-event prerequisite.

## Initial remaining implementation (superseded where recorded below)

- [ ] Resolve and document the6711-compatible admission, bracket/timer and reward
  contracts using client/server evidence and dated primary sources.
- [ ] Implement event-scoped combat calculations without persisting downgraded
  equipment or altering a player's normal stats. Verify every applicable cap,
  battle-power source, subclass exception, Chi interaction and return to normal.
- [ ] Persist per-match reward entitlement together with its authoritative result;
  deliver with capacity deferral, transaction/replay protection and exact reload.
- [ ] Complete a Skill-series ordinary team-to-final earning fixture, including
  losers, byes, disconnect/replacement, atomic rewards and original-state recovery.
- [ ] Reconnect the clerk and calendar only after those prerequisites, then verify
  actual private startup/socket earning, native presentation and live gates.

No additional reward family, wardrobe classification or Skill activation is
claimed by the opening fix. Do not enable the event solely to test claim storage.


## Owned Chi contribution cap (2026-09-27)

The production `ChiBonuses.Apply` calculation now clamps each effective roll to
60% of that attribute's installed maximum only for a verified imported Skill
fighter. It does not reduce a lower roll by40%. It applies the existing percentage
modifier after clamping, preserving the ordinary calculation outside Skill scope.
The original Chi attributes, points and saved rolls are not changed.

`LocalTeamPkCombat.SkillStats` requires the canonical installed player, active
Skill bracket, exact registered team and match objects, fighting membership and
the actual owned map. It rejects a mere flag, regular Team PK, removed/replaced
members or teams, completed matches and invalid derived bracket indices. Its
checks use existing concurrent collections and acquire no new lock inside stat
recalculation, avoiding reversal of the match/item lock order.

- [x] Isolated build and138 focused scope/contribution assertions pass in
  `tests/TeamPk/SkillChiStatsTests.cs` for all13 Chi attributes: normal maxima,
  capped maxima, lower rolls, percentage ordering, unchanged source values,
  both physical-attack endpoints and invalid/out-of-scope identities.
- [ ] Complete whole-character stat recalculation, HP clamping/restoration,
  equipment, subclass and battle-power limits before activation. Existing Chi
  ranking bonuses are unchanged; their edition-specific treatment still needs
  evidence. These tests do not prove a complete Skill combat profile.

Evidence: `obj/skill-chi-cap-build.log`, `obj/skill-chi-cap-tests.log`. The fixture
uses in-memory ownership and contribution state, not a database or client. No
private candidate refresh or live deployment occurred. Skill PK remains closed.

## Owned subclass contribution cap (2026-09-27)

- [x] The actual item-stat calculation now delegates its unchanged subclass
  formulas to `ApplySubclassStats`. Verified Skill fighters use effective phase5
  at most, except Apothecary. Lower phases and saved phase/level remain unchanged.
- [x] Isolated build and181 combined Chi/subclass assertions pass. The subclass
  fixture covers all seven classes, normal phase9, capped contributions,
  Apothecary, lower phases, invalid phases and restoration outside Skill scope;
  Performer covers all three attack contributions.
- [ ] Battle-power, equipment and whole-character HP restoration remain pending.

Evidence: `obj/skill-subclass-build.log`, `obj/skill-subclass-tests.log` and
`tests/TeamPk/SkillChiStatsTests.cs`. This is an in-memory contribution fixture,
not a persisted-character or client acceptance test. No deployment occurred.

## Owned battle-power calculation (2026-09-27)

- [x] Verified Skill fighters calculate equipment BP with plus capped at4 and
  only the first socket/gem. Existing one-/two-weapon multipliers are preserved.
  Nobility contributes at most Earl; guild, mentor and clan shares are excluded
  from the effective combat total. Level, rebirth and independent BP are retained.
- [x] Normal source BP is separate from the Skill projection, preserving the
  source used by outside mentor/clan recipients. No gear/rank/share fields are
  overwritten by calculating the event total.
- [x] Isolated build and195 combined focused assertions pass, including normal
  restoration, lower values, two-handed and paired weapons, and canonical mentor
  and clan sources contributing outside Skill scope but not inside it.
- [ ] Effective BP display/shared-bonus packet updates, full equipment combat
  stats and whole-character HP restoration still require integration acceptance.

Evidence: `obj/skill-bp-build.log`, `obj/skill-bp-tests.log` and the extended
`tests/TeamPk/SkillChiStatsTests.cs`. In-memory production calculation tests do
not prove database persistence, client display or tournament lifecycle acceptance.
The clerk/calendar remain closed; no private candidate or live deployment.

## Effective equipment contributions (2026-09-27)

- [x] The production item-stat loader selects plus information at no more than4,
  caps effective blessing at3 and ignores the second gem for an owned Skill
  fighter. Lower values remain unchanged. Item IDs, plus, blessing and sockets
  are never rewritten for these calculations.
- [x] Isolated build and202 combined assertions pass. The item fixture exercises
  the real loader against controlled base/plus catalog rows, including attack,
  defense, HP, blessing, both gems outside Skill, first gem inside Skill, lower
  values, absent first socket and normal recalculation after leaving scope.
- [ ] Equipment level selection still needs catalog-backed family/slot validation;
  this batch does not cap base equipment levels. Soul, refinery, perfection and
  other unlisted contributions retain existing behavior pending edition evidence.
- [ ] Whole-character HP, match entry/exit, disconnect and client stat display
  acceptance remain incomplete. The event remains closed.

Evidence: `obj/skill-equipment-build.log`, `obj/skill-equipment-tests.log` and
`tests/TeamPk/SkillChiStatsTests.cs`. The controlled fixture is not validation of
all installed catalog rows or persisted-character/native-client behavior.
No candidate refresh or live deployment occurred.

## Equipment level projection (2026-09-27)

- [x] The item-stat loader now selects effective base and plus rows at the slot's
  level limit. Selection retains numeric family, quality, class and gender;
  lower-level items are unchanged. The search uses at most100 catalog lookups,
  consistent with the existing upgrade path's family/quality identity.
- [x] Earrings117 cap at112; bags121 and rings150 at116; heavy rings151 and
  bracelets152 at117; other listed head/necklace/armor/boots/weapon slots at120.
  Alternate slots use the same limits. Unlisted slots retain their item type.
- [x] Isolated build and240 combined assertions pass. Installed catalog tests
  load all rows for ten checked families and verify high-to-cap selection,
  alternate slots and already-capped items. Missing, wrong-quality and wrong-class
  candidates are rejected. A controlled production-loader fixture verifies that
  capped-level base and plus4 rows are applied together and original contributions
  return outside Skill scope without modifying the item.
- [ ] Preflight every participant's equipped catalog entries before import, and
  handle projection failures transactionally. The selector currently throws on
  missing compatible rows; activation must not expose partial stat recalculation.
- [ ] Validate the remaining installed equipment families, whole-character HP,
  match lifecycle, client display and durable match rewards before activation.

Evidence: `obj/skill-level-build.log`, `obj/skill-level-tests.log`, and
`tests/TeamPk/SkillChiStatsTests.cs` (now requires the private catalog path).
Catalog: `C:/Revora/DisposablePostgres/moon-box-candidate/game/database/itemtype-new.txt`,
SHA256 `1ED7A23B7A34B5E3706277F97E421E3CDF0B269EEB5BE7367A4EBC0E4F22B3EF`.
No proprietary catalog is committed. This does not establish exact retail6711
rules or complete combat normalization. Clerk/calendar and deployment stay closed.

## Recalculation preflight and HP bounds (2026-09-27)

- [x] Skill sign-up validates selected equipment before reserving admission.
  Skill stat recalculation repeats that check under the item lock before rune
  refresh, worn-flag changes or derived-stat reset. It follows active equipment
  selection and computes current weapons instead of trusting the cached pair.
- [x] Successful Skill recalculation clamps existing HP to the final maximum,
  without healing damage or reviving dead players. A tracked applied profile
  preserves that bound on the first normal recalculation. Match import refills
  HP after capped stats have been applied.
- [x] The durable return path explicitly clears/restores a previously applied
  Skill profile before acknowledging return. Normal Team PK has no profile to
  restore and retains its existing behavior.
- [x] Isolated build and249 focused assertions pass, including missing catalog
  rejection before mutation, inactive alternate exclusion, current weapon
  selection, HP clamping, no healing/revival and normal-profile behavior.
- [x] The existing192 database/ledger/world checks pass against the private DB
  after changing the shared return path; expected injected failures still recover.
- [ ] Whole-match import preflight/rollback, equipment changes during matches,
  forced return with a fully initialized Skill character, disconnect recovery
  and native-client HP/BP updates still need integrated acceptance. The focused
  tests exercise the production HP finalizer; they do not prove full lifecycle
  restoration or a live socket round trip. Skill activation stays closed.

Evidence: `obj/skill-profile-build.log`, `obj/skill-profile-tests.log`,
`obj/skill-profile-recovery.log`, `obj/team-pk-ledger-tests.log`.
The in-memory fixture uses ordinary registered sessions with inert sockets so HP
notifications cannot disconnect the synthetic session. Private DB was stopped;
no candidate refresh or live deployment occurred.

## Complete stat recalculation fixture (2026-09-27)

The complete `LoadItemStats` fixture exposed an additional integration bug:
King/Prince/Duke Revora combat bonuses still used the saved rank despite the
battle-power cap. The stat loader now applies those bonus formulas using effective
nobility capped at Earl for verified Skill fighters. The saved rank is unchanged.

- [x] Isolated build and263 combined checks pass. The extended ordinary level140
  Trojan fixture calls the complete production recalculation with owned gear,
  Chi and Wrangler, rather than only invoking contribution helpers.
- [x] Duke, Prince and King match the Earl Skill profile across HP, mana, physical
  and magic attack, defenses, critical/immunity/breakthrough/counteraction,
  final damage modifiers and battle power.
- [x] Explicit `RestoreSkillStats` restores normal max HP, attack and blessing
  without healing. Three repeated Skill/normal transitions compare the complete
  captured profile and do not accumulate bonuses. Gear, saved subclass phase and
  Chi rolls remain unchanged.
- [ ] This fixture is in-memory with inert sockets. It does not establish durable
  character reload, whole-match import rollback, all class/equipment combinations,
  disconnect recovery or native presentation. Those activation gates remain open.

Evidence: `obj/skill-full-profile-build.log`, `obj/skill-full-profile-tests.log`,
`tests/TeamPk/SkillChiStatsTests.cs`. No database or deployment changes were needed.

## Whole-match import failure recovery (2026-09-27)

- [x] Skill import verifies the owning active bracket and serializes on the match.
  It reserves canonical participant sessions and item locks in UID order, then
  validates every selected waiting-area participant before any import mutation.
  Busy ownership defers the attempt; repeated import of an inside match is ignored.
- [x] An import exception or incomplete participant transition immediately stops
  the bracket and makes its result unpayable. Its next owned callback durably
  cancels the run, returns admitted players through existing receipts/restoration,
  and disposes empty owned maps. Regular Team PK retains its importer.
- [x] A cancellation/return failure leaves recovery pending with combat disabled;
  subsequent callbacks retry instead of publishing a result or forgetting returns.
- [x] Isolated build,210 ledger/world checks and263 stat checks pass. New private
  database fixtures cover both invalid gear before import and a deliberate failure
  after the first ordinary player enters. They verify no winner, normal-profile
  restoration, two durable returns, arena disposal and replay. An injected database
  cancellation failure retains pending recovery and succeeds after removal.
- [ ] Skill finals, persisted character reload after an interrupted Skill match,
  equipment mutation races, successful multi-player import concurrency and native
  presentation still require acceptance. Durable per-match rewards remain open.

Evidence: `obj/skill-import-build.log`, `obj/skill-import-recovery.log`,
`obj/team-pk-ledger-tests.log`, `obj/skill-import-stats.log`, and
`tests/TeamPk/WorldTests.cs`. These are real private-world/database failure paths,
with controlled errors and clocks; not native client tests. The private database
is stopped. Clerk/calendar remain closed; no deployment occurred.

## Persisted Skill interruption recovery (2026-09-27)

- [x] A private-world fixture successfully imports two ordinary players using
  installed plus12 armor and a high-level plus12 necklace. Both enter at effective
  full HP, below their normal maximum; a repeated import does not reset the match.
- [x] Both characters save while inside the Skill arena. The fixture removes their
  sessions and dynamic maps, invokes production startup cancellation, then uses
  production entity and inventory loaders for fresh sessions.
- [x] Reload restores Twin City327,303, original Peace mode, ordinary max HP and
  the original armor/necklace identities and plus12 values. Return replay is safe;
  both receipts finish with no invented winner or placement claim.
- [x] The full ledger/world suite passes227 checks. Fixture setup reflects the
  existing login floor of VIP4 and default Duke, and persists actual item
  durability; comparisons therefore use the same ordinary profile before/after.
- [ ] This simulates interruption in one harness process using real persistence
  and production recovery methods. It is not an actual mid-import process-kill or
  native reconnect acceptance. Skill finals and durable per-match rewards remain
  incomplete, as do remaining equipment-family/mutation-race checks.

Evidence: `obj/skill-disconnect-recovery.log`, `obj/team-pk-ledger-tests.log`,
`tests/TeamPk/WorldTests.cs`. No gameplay code or private executable changed in
this batch. The private database is stopped; activation/deployment remain closed.

## Durable Skill round rewards (2026-09-27)

The inherited winner reward720981 is the installed SkillTeamPKPack (one EXP Ball
according to the catalog). This change preserves that reward; it does not resolve
the separate retail edition/calendar discrepancy or change final placement purses.

- [x] A Skill round freezes its winner, loser and eligible imported winner members
  before changing `Done`/qualification state. Outcome and individual entitlements
  commit together. A failed commit leaves the frozen outcome retryable.
- [x] The direct inventory insertion is replaced by receipt-based delivery. Entity
  and inventory ownership, bag capacity, database/in-memory bag agreement and
  canonical session are checked; item insertion and its exact UID receipt commit
  atomically. Earned entitlements survive later bracket cancellation.
- [x] The existing bounded background pulse discovers undelivered round prizes even
  after the player's return receipt is complete. One pending item is delivered per
  selected player per pulse; full bags remain pending.
- [x] Isolated build,240 ledger/world checks and263 stat checks pass. Actual match
  End fixtures cover atomic outcome rollback on entitlement failure, retry,
  repeated End, full-bag deferral, loser exclusion, delivery insertion failure,
  session fencing/reload, background delivery, concurrent replay and exact item
  persistence through the production inventory loader.
- [ ] Add the two new tables (`revora_skill_pk_rounds` and
  `revora_skill_pk_round_rewards`) to the next coherent private Host startup
  acceptance batch. Current evidence exercises production initialization directly
  against the disposable DB, not an updated Host executable.
- [ ] Complete Skill finals, multi-round/three-team reward identity, lost-reply
  and membership/equipment-race acceptance, and native presentation before opening.

Evidence: `obj/skill-round-build.log`, `obj/skill-round-recovery.log`,
`obj/team-pk-ledger-tests.log`, `obj/skill-round-stats.log`, and
`tests/TeamPk/WorldTests.cs`. Fixture schema was isolated; private DB is stopped.
No private candidate refresh or live deployment occurred; Skill remains closed.

## Skill finals and private startup acceptance (2026-09-27)

- [x] A controlled private fixture drives33 ordinary two-player teams (66 players)
  through knockout, three-team switch rounds and the complete Skill final. The
  scheduler and match import/export/result logic run normally; clocks and scores
  are controlled. The two rounds of a three-team match persist distinct identities.
- [x] Every earned round has exactly two imported winner entitlements and matching
  persisted pack receipts. Final cleanup durably returns all66 players and clears
  Skill flags. The top leader earns10000 CP and one bound P7 through the ordinary
  claim service; repeated claiming cannot duplicate payment.
- [x] The combined private ledger/world suite passes314 checks. The fixture adds
  a temporary Skill clerk only to its isolated world for claim proximity; this
  does not make the public parked clerk or calendar available.
- [x] The coherent private executable passed two actual game/database startup and
  shutdown cycles. All33 expected runtime-owned roadmap tables retained identity,
  including both new Skill round tables. Their11 columns have the required types
  and nullability. Listeners were healthy; no emergency cleanup kill was needed.
- [ ] Exact edition/calendar policy, remaining equipment families, membership and
  equipment mutation races, lost-reply coverage and native presentation still
  require acceptance before public activation. Controlled finals are not a native
  combat session or ordinary public-clerk acceptance.

Evidence: `obj/skill-finals.log`, `obj/team-pk-ledger-tests.log`,
`obj/skill-round-startup.log`, `tests/TeamPk/WorldTests.cs`, and
`tools/Verify-MoonBoxDatabase.ps1`. The startup log's old literal label says31;
its executed identity query already required33. That display label is corrected
in this batch without repeating the two successful cycles.

Private executable gameplay is `dea6ae80`, SHA256
`4E5240116AC593859BC2ED57DA3DF715861D860668B07DBC1ED6236241292618`.
The prior private executable/manifest are retained for rollback. The private game
and DB are stopped. No live restart, public activation or client update occurred.

## Approved Revora registration rules and catalog audit (2026-09-27)

- [x] Owner selected documented Revora rules instead of waiting for exact
  6711-era calendar evidence. Registration is Wednesday 19:45-20:00 in the
  server's local time. This is a Revora edition, not a verified retail rule.
- [x] The opening path rejects other times and gives every bracket the same
  fixed 20:00 deadline. A delayed callback within registration can open once;
  existing durable date identities prevent reopening that day's bracket.
- [x] Retain the tested current leader placement prizes: 10,000 / 7,000 /
  5,000 / 3,000 CPs for first / second / third / other top-eight placements,
  plus one bound P7 soul. Imported winning members retain the existing
  SkillTeamPKPack (720981) per earned round, with durable individual receipts.
- [x] Audit all 16,740 loaded equipment rows across 70 families. 8,250 rows
  project to compatible capped rows; under-cap rows remain unchanged. No parse
  exclusions occurred. The audit explicitly asserts the single unresolved row
  below, so another missing projection fails validation.
- [ ] Catalog item 619301 (SoaringSpirit, Hossu, level 129, class 190) has no
  same-quality lower-level row. Production preflight refuses it safely. This
  remains an admission compatibility gap; the passing audit does not claim
  all equipment is supported. No cross-quality replacement was invented.
- [ ] Wire the public calendar and clerk after remaining mutation/lost-reply
  and ordinary access acceptance. Native presentation remains pending under
  the owner's existing decision. No public activation or deployment in this batch.

Calendar/entry checks: 47. Combat/catalog checks: 264. Database/world checks: 315. The database fixture
uses an explicit Wednesday clock for concurrent Skill opening; it checks the
fixed deadline as well as the existing durable single-opening behavior.
Evidence: `obj/skill-revora-calendar-build.log`,
`obj/skill-revora-calendar-entry.log`, `obj/skill-catalog-audit.log`,
`obj/skill-revora-calendar-ledger.log`, and the TeamPk fixtures.

## Committed Skill round replay acceptance (2026-09-27)

- [x] After an injected entitlement-write failure freezes a real match outcome,
  commit that exact outcome through the production recorder while deliberately
  leaving the match unfinished in memory. Eight concurrent recorder retries
  preserve one outcome and one recipient entitlement.
- [x] Retries with altered winner, loser or recipient are rejected without
  changing saved ownership. Retrying match completion with the opposite loser
  still follows the frozen, already committed outcome.
- [x] After cancellation, the exact earned round remains replayable; a new round
  cannot create rewards. Existing return, deferred delivery, concurrent claim
  and production inventory-reload checks still exercise the same recipient.
- [ ] A real lost PostgreSQL COMMIT response and delivery interruption between
  commit and in-memory item installation remain untested. The new checks model
  the committed-database/unadvanced-match boundary explicitly; they do not claim
  a TCP fault, process crash or native-client acceptance.

Evidence: `tests/TeamPk/WorldTests.cs`,
`obj/skill-round-committed-replay.log`, `obj/team-pk-ledger-tests.log`.
The combined database/world suite passes 323 checks. Only fixtures and
documentation changed; no Host refresh or deployment is needed.

## Skill import and membership ownership acceptance (2026-09-27)

- [x] Three ordinary players in two teams exercise the real import path while
  each participant's item lock is held by another thread in turn. Every attempt
  defers with zero imports, no partial movement and no bracket cancellation.
  Releasing the locks permits one complete import.
- [x] An opposing pair acquires the production combat lease. A concurrent call
  to the actual team-removal service waits until the lease releases its ownership,
  then removes the member. That member cannot acquire another hit while still
  standing in the tournament arena.
- [x] Cancellation returns all three participants, including the removed member,
  through durable ordinary-player return handling and clears their Skill flags.
- [ ] This proves the tested item-lock/import and member-removal/hit boundaries.
  It does not yet cover every equipment-changing packet or concurrent join,
  leadership change, session replacement and round-finalization combination.

The combined database/world suite passes337 checks. Evidence:
`tests/TeamPk/WorldTests.cs`, `obj/skill-ownership-races.log`, and
`obj/team-pk-ledger-tests.log`. No gameplay code changed; the existing ownership
implementation passed these checks. Public clerk/calendar and native acceptance
remain pending. The private database was stopped after the fixture.

## Revora Skill round durations (2026-09-27)

- [x] Replace Skill's inherited one-minute combat timeout with ten-minute
  knockout/top-eight-qualification rounds and fifteen-minute matches once the
  top eight enter the final stage, including third-place and championship bouts.
  Regular Team PK keeps its existing timing.
- [x] Freeze each match's duration when the bracket allocates it. Countdown,
  timeout selection and three-team reimports use that match duration. Byes remain
  immediate; the existing one-minute inter-round preparation is unchanged.
- [x] Verify the full clock, last second, expiry and post-expiry behavior for
  all six combat stages. Drive the complete 33-team/66-player Skill fixture again
  and verify actual scheduler-created knockout and final durations, results,
  round receipts and final prizes.

Policy basis: the [official PC Skill Team PK guide](https://co.99.com/guide/quests/skillteampk.shtml),
rechecked 2026-09-27, describes ten-minute knockout rounds and fifteen-minute
matches in the top-eight final stage. Revora adopts those durations explicitly;
this is not a claim that the current guide proves every original 6711 rule.
The approved common 19:45-20:00 registration window and current Revora rewards
remain as documented above rather than adopting the guide's staggered entries
or newer prize table.

Validation: 89 entry/clock checks and 338 database/world checks passed.

Evidence: `obj/skill-duration-build.log`, `obj/skill-duration-entry.log`,
`obj/skill-duration-world.log`, `tests/TeamPk/EntryTests.cs` and
`tests/TeamPk/WorldTests.cs`. Public activation, private Host refresh and native
clock presentation remain pending; no live restart or client update occurred.

## Skill clerk dialogue correction (2026-09-27)

- [x] Replace the legacy Skill clerk's lengthy, contradictory dialogue with
  brief entry, rules, prizes and claim options. It now describes the approved
  common Wednesday 19:45-20:00 entry window and ten/fifteen-minute match timing.
- [x] Remove the unimplemented 1,000-Honor-per-round promise, staggered 20:02 /
  20:04 / 20:06 deadlines and unsupported next-event claim-expiry statement.
  State the actual leader CP purses plus bound P7 and winning-fighter pack.
- [x] Require ordinary town-clerk proximity before handling these options.
  Registration and claims still use the existing authoritative services; successful
  entry does not send a leftover town dialogue after teleportation.
- [ ] The handler remains behind the closed-desk gate. This correction does not
  activate the clerk, calendar, native dialogue or public event. Actual ordinary
  socket/dialogue acceptance must follow the remaining activation prerequisites.

The isolated build passed (`obj/skill-clerk-build.log`). This dialogue/routing
change preserves owner edits elsewhere in `Npcs.cs`; no live or private Host
refresh was performed. No new reward behavior is claimed.

## Real lost-COMMIT-response acceptance (2026-09-27)

- [x] A bounded test-only loopback relay connects exclusively to the disposable
  PostgreSQL port 25439 and requires the `local_team_pk_fixture` schema. It reads
  framed PostgreSQL responses without logging payloads, observes a successful
  COMMIT completion and drops the connection before forwarding that response.
  Both directions and listener are closed after each single-connection trial.
- [x] The round recorder receives an actual Npgsql transport exception after
  the database commits. The match remains unfinished with its frozen outcome;
  concurrent retries preserve one recorded outcome and one reward recipient.
- [x] Repeat the real response loss during reward delivery. The item and its
  exact receipt commit, while the stale session is fenced before in-memory
  installation. The ordinary entity/item reload recovers that exact item once;
  background polling and concurrent delivery replay do not duplicate it.
- [x] The combined database/world suite passes344 checks, including the full
  Skill finals and existing failure/return cases. The earlier simulated
  committed-database boundary is now supplemented by actual transport failure.
- [ ] These are private database transport and ordinary server-session fixtures,
  not a native game-client session or a live rollout. Public clerk/calendar and
  native acceptance remain pending.

Evidence: `tests/TeamPk/WorldTests.cs`, `obj/skill-lost-commit-response.log`,
`obj/team-pk-ledger-tests.log`. No production fault hook or gameplay change was
needed. Only the fixture's transient loopback connection uses the relay; no
credentials or packet payloads are persisted. The disposable database is stopped.

The targeted Hossu follow-up found no explicit 619301 grant in the searched game,
NPC, database and network C# sources or selected local shop/drop configuration.
That is not proof the item is unobtainable. Its missing compatible capped row
remains a documented refusal, not silently mapped to another quality.

## Skill clerk and calendar source activation (2026-09-27)

- [x] Restore clerk801717 as SkillRegistrar at Twin City (300,146), beside the
  regular tournament area. Shared placement is idempotent, checks occupied sites
  and duplicate identities, and only relocates the exact retired jail slot.
  Plaza cleanup preserves both tournament clerks and opens the Skill dialogue.
- [x] Connect the world calendar to the approved Wednesday 19:45-20:00 opening
  guard and durable date identity. Announcements identify the actual clerk site.
- [x] The full 33-team/66-ordinary-player Skill fixture now enters through the
  actual NPC request handler instead of manually reserving admissions. Its final
  leader claims and replays the prize through the same NPC handler. All344
  combined database/world checks pass after the isolated build.
- [ ] Refresh and verify the coherent private executable, including both clerk
  startup markers and cleanup. Actual game-socket/native presentation and live
  deployment remain separate gates; source activation is not a live rollout.

The single catalog gap619301 remains a safe admission refusal. Eligible players
with supported equipment use the ordinary service; no GM or owner allowlist is
introduced. Earlier closed-clerk/calendar notes above describe historical stages
and are superseded by this source-activation section.

Evidence: `obj/skill-clerk-calendar-build.log`,
`obj/skill-clerk-calendar-world.log`, `tests/TeamPk/WorldTests.cs`.

## Skill private startup acceptance (2026-09-27)

- [x] Refresh the coherent private candidate from gameplay98650120, retaining the
  prior executable and manifest. SHA256:
  `0AE42EE305AF883401365798BBB905E2BC5A75595BD637B7483C52C99B6D69CD`.
- [x] Two actual game/database startup and shutdown cycles pass. Both tournament
  clerks appear exactly once, neither is parked by plaza cleanup, listeners are
  healthy, restored-event callbacks return and shutdown needs no emergency kill.
  All33 runtime-owned roadmap tables retain identity across both cycles.
- [ ] A returned event pulse does not prove Wednesday's scheduled opening ran.
  Controlled calendar and ordinary-NPC fixtures supply separate evidence above;
  real game-socket earning and native-client presentation remain pending.

Evidence: `obj/skill-clerk-calendar-startup.log`,
`tools/Check-CandidateGameStartup.ps1`, `tools/Verify-MoonBoxDatabase.ps1`.
The private game and database are stopped. No live deployment or client update
occurred. Owner WIP in `Npcs.cs` and `Thread.cs` remains unstaged and excluded from
this isolated build.

## Skill calendar reachability correction (2026-09-27)

Correction to the preceding source-activation claim: gameplay98650120 placed
its Skill opening call below `WorldTournaments`' unconditional return. Clerk
placement and controlled NPC earning passed, but that calendar call was unreachable.
The prior startup pulse marker did not cover the newly added Skill calendar.

- [x] Move Skill opening into `TickRestoredEvents`, which is invoked before the
  legacy-block return. Use the pulse's local clock and the existing serialized,
  date-owned opening path. Remove the unreachable duplicate call.
- [x] Invoke the actual restored-event pulse with a due Wednesday in the private
  database fixture. All four brackets acquire distinct active saved runs; another
  pulse keeps those same identities. Other callback failures remain isolated.
- [x] Isolated build and346 database/world checks pass, including ordinary NPC
  earning, full finals and actual lost-COMMIT-response recovery.
- [x] Refresh the candidate and verify the corrected composed callback path.
  Actual socket earning and native acceptance still remain separate requirements.

Evidence: `obj/skill-active-pulse-build.log`, `obj/skill-active-pulse-world.log`,
`tests/TeamPk/WorldTests.cs`. This entry supersedes the earlier claim that
source activation alone had connected the calendar to the live timer path.

Corrected private gameplay2ccad3bd passed two actual startup/shutdown cycles:
all33 roadmap tables retained, both clerks ready, the active event pulse healthy,
and no emergency cleanup kill. The private database remains stopped. Evidence:
`obj/skill-active-pulse-startup.log`. Executable SHA256:
`2ADB59E427D0FC7829CBA08CB42641547033E0B509C4B26CC56BCF3D485A524D`.
These nondue startup cycles prove the composed pulse runs; the separate due-clock
fixture proves Skill opening. No live deployment occurred.

## Ordinary Skill clerk TCP acceptance (2026-09-27)

- [x] Extend the existing account/game socket probe to walk the ordinary career
  character to SkillRegistrar, avoiding its occupied tile. Request the main,
  rules and prize menus through actual NPC packets; verify the approved entry
  times, level grouping, match durations and actual rewards without the old
  unsupported Honor promise.
- [x] Closed registration and an unearned prize claim are refused over TCP.
  The character remains beside the clerk with unchanged inventory, CPs, RVPs
  and silver, then walks back to the normal saved town position.
- [x] Both actual private game/database cycles pass authentication, these Skill
  clerk checks, clean shutdown and persistence verification. Existing career,
  rebirth and quest receipts/rewards remain intact across restart; all33 roadmap
  tables retain identity. The private database is stopped.
- [ ] These closed-calendar socket checks do not prove actual socket admission,
  combat, victory or payout. Full earning remains proven by the ordinary NPC
  server fixture above; native visuals and live deployment remain pending.

Evidence: `tools/CandidateAccountWireProbe.cs`, `obj/skill-clerk-wire.log`.
The source-only probe change used private gameplay2ccad3bd without a new Host
build or refresh. No live process, live database or client installation changed.

## Explicit operator opening prerequisite (2026-09-27)

The existing `@steam` console command called calendar-restricted opening and
then created teams even if opening failed. It also tried to admit every online
player directly. That behavior cannot establish an ordinary NPC admission test.

- [x] Separate the existing explicit console opening from automatic calendar
  opening. A manual opening uses fifteen minutes from invocation; the active
  world pulse still enforces Wednesday 19:45-20:00. Both share durable date
  identity, bracket locking and refusal to reopen that date.
- [x] Stop the console command immediately if opening fails. On success it
  retains its existing team-creation behavior, but fighters must enter through
  the ordinary clerk instead of a console-driven admission. This is an operator
  command, not a player-access bypass or new test-only public endpoint.
- [x] Isolated build,89 entry/clock checks and350 database/world checks pass.
  The fixture verifies a Monday manual window, automatic Monday refusal,
  the manual deadline and same-date replay protection.
- [ ] Exercise the manual console checkpoint plus ordinary TCP admission and
  interruption recovery in the next coherent private process batch. Native
  team-creation UX and complete socket combat/earning remain pending.

Evidence: `obj/skill-operator-open-build.log`, `obj/skill-operator-entry.log`,
`obj/skill-operator-world.log`, `tests/TeamPk/WorldTests.cs`.
The private Host was not refreshed for this prerequisite alone. No live change.

## Skill ordinary TCP admission and process interruption (2026-09-27)

- [x] Add `-CheckSkillTeamPk` to the existing tournament-access process harness.
  It requires the ordinary career/tournament checks and selects the existing
  `@steam` console opening, the Skill clerk and kind1 persistence assertions.
  The recent-opening guard preserves saved run history instead of clearing it.
- [x] Show the clerk's entry option only while registration is open; direct closed
  requests still receive an authoritative refusal. The private socket probe waits
  for that opening, sends its own ordinary NPC request and reaches the waiting
  map with unchanged items, CPs, RVPs and silver.
- [x] Terminate only the path/PID-verified isolated candidate while that session
  is connected. PostgreSQL retains the exact active Skill admission and original
  PK mode. No entry prize, round reward, placement or claim is invented.
- [x] Restart and authenticate the same ordinary character. The same run is
  cancelled and its return acknowledged; the character resumes in town. Saved
  career receipts and rewards remain intact, all33 roadmap tables retain identity,
  and the final shutdown is clean. The private game/database are stopped.
- [ ] Team creation in this trial is operator-driven. It does not establish native
  team-invitation UX, combat-phase process interruption, a full TCP victory/payout
  or natural Wednesday observation. Existing server earning and transport-fault
  fixtures cover separate boundaries; native/live acceptance remains pending.

Evidence: `obj/skill-tcp-entry-build.log`, `obj/skill-tcp-crash.log`,
`tools/CandidateAccountWireProbe.cs` and the three private PowerShell harnesses.
Run mode adds `-CheckSkillTeamPk -CheckCourierCrash` to the existing complete-career
`-CheckTournamentAccess` invocation. Historical checkpoint labels still say
Team PK; the explicit Skill NPC marker and database kind1 checks identify this run.
Private executable SHA256:
`84C70DAEB82E62E621510D5BD01713297331C0D103DAB0000EEEED7D791541AA`.
It was built from gameplaye7b0843b plus this batch's NPC menu guards, with owner
WIP excluded. The previous private executable/manifest remain available. No live
process, live database or client installation changed.

## Player-controlled TCP team creation (2026-09-27)

The tournament-access wire probe now sends team-create1023 from the authenticated
ordinary player's connection before its optional operator-opening checkpoint.
It requires the server's action15 then action0 acknowledgements with that player's
UID, and confirms unchanged CPs, RVPs, silver and inventory. The32-byte sealed
request uses the existing server24-byte declared payload, action at4 and UID at8;
this is tested server interoperability, not newly verified native-client evidence.

- [x] Two actual startup/authentication/entry/shutdown cycles independently create
  the team through player traffic. No operator opening or team-creation command
  was issued in this run. The reconnect starts a fresh player-created team.
- [x] Ordinary tournament menus, closed registration and unearned-claim refusals
  still pass. The existing earned career receipts/items and character identity
  remain intact; both server shutdowns are clean and PostgreSQL is stopped.
- [ ] This prerequisite does not prove invitations, multi-player membership,
  open-event admission with that team, tournament combat/final payout, native UI
  or deployment. Historical operator-created admission evidence remains separate.

Evidence: `tools/CandidateAccountWireProbe.cs`, `obj/skill-player-team-wire.log`.
Invocation uses CheckGameStartup, CheckRoadmapStartup, CheckWireAuth,
CheckCharacterEntry, CheckSoulTrialAccess, CheckPathHunt, CheckPromotion,
CheckMoonAccess/Combat/Finish, CheckFirstRebirth, CheckSecondLife,
CheckSecondPromotion, CheckSecondRebirth, CheckTournamentAccess and CheckSkillTeamPk
on Verify-MoonBoxDatabase.ps1, without CheckCourierCrash. Probe compilation treats
warnings as errors. No gameplay build or live deployment was performed.

## Two authenticated players and team framing repair (2026-09-27)

The new `Verify-ArenaProcess.ps1 -TeamWireOnly` suite creates two ordinary
accounts with ephemeral passwords and stored SRP verifiers in its disposable
clone. Each separate client process authenticates through Account/Game TCP and
completes world entry. Accounts/character eligibility are fixture prerequisites;
no team membership is supplied by the host.

- [x] Player0 creates the team through1023. Player1's unsolicited acceptance does
  not join. A real invitation and acceptance joins both canonical sessions.
- [x] Repeated acceptance leaves exactly two members; the ordinary member cannot
  kick the leader. The leader can kick the member, leaving exactly one member.
- [x] Each command is followed by a location request on the same encrypted stream;
  its response verifies that framing remains usable. The private host separately
  checks canonical member/leader references. Inventory, CPs and RVPs stay empty.
- [x] Both clients exit successfully, reject replayed single-use game grants and
  are cleaned up with the isolated host/database. Helper builds use warnings as
  errors. No ordinary gameplay owner-name or GM gate is introduced.

This test found a real production defect after the authorized kick: constructor-
built1023 responses allocated36 bytes but declared a24-byte payload, placing the
server seal four bytes late. Team.cs now allocates32 bytes, preserving that
payload and the eight-byte seal. The old private binary fails the exact test at
`game-frame-seal-kind-1023-length-24`; the corrected binary passes. Request echoes
already used32 bytes, which explains why the earlier team-create test passed.

Evidence: `obj/team-wire-framing-build.log`, `obj/team-invitation-wire.log`,
`tools/CandidateTeamWireProbe.cs`, `tools/CheckCandidateTeamWireLifecycle.cs`.
Passing gameplay SHA256:
`AE53689BB2312A6988263109DC7F9610B63FF8F944BFE87E815740B1CF6908DC`.
The private candidate was refreshed; originals were retained. No live deployment
or native-client test occurred. Open-event admission with these player-formed
teams, final combat/payout over TCP and native invitation UI remain pending.

## Player-created team enters over TCP (2026-09-27)

The two-client suite now re-invites the kicked member, opens the approved Skill
window at a controlled future Wednesday19:45, and has each authenticated player
query the installed registrar and send its own entry option. The fixture supplies
ordinary level100 profiles and initial proximity at1002,302,146; it does not
supply team membership, admission rows or rewards. Calendar time is controlled,
not naturally observed.

- [x] Both real sockets reach the same bracket1 waiting map. The bracket owns one
  team and the database owns two unreturned kind1 admissions for those players.
- [x] No Skill round rewards are created by admission. Their inventories, CPs and
  RVPs stay empty. The existing invitation, replay and kick-authority checks pass.
- [x] Fixed an unrelated LMS login hazard exposed before admission. LmsAdmissions
  tried item/virtue/PK locks before checking for any pending return, so ordinary
  login could be fenced by transient contention despite no saved LMS admission.
  It now checks for a pending return first. Actual returns still acquire all
  three locks and re-read saved state before teleport/save/acknowledgement.
- [x] A deterministic old-binary test fails when another thread holds the item
  lock without any admission. The corrected build succeeds with each of the
  three locks held when no return exists. A supplied ended admission still
  defers under each lock, then returns and acknowledges once locks are free.
  This isolated saved-return fixture does not claim an earned LMS victory.

The initial live-socket failure was `lms-login-return`; the controlled regression
confirms the identified lock-order defect. Tests now reload created profiles
before exercising the full production save on recovery.
Evidence: `obj/lms-login-contention-before.log`,
`obj/lms-login-contention-build.log`, `obj/skill-team-admission-wire.log`.
Passing gameplay SHA256:
`59D7AE758A12E33DB5EDB82BEACB258F14B11265617A1A7E9A9DB2C65193A533`.
The private candidate was refreshed with its predecessor retained. The owned
clone and private processes were cleaned up. Full TCP opponent combat/final
payout, native UI, natural scheduling and live deployment remain pending.


## Eight-team TCP combat and final claim (2026-09-27)

- [x] Sixteen ordinary authenticated clients form eight two-player teams through
  real invitation/accept packets and individually enter through the registrar.
- [x] Actual TCP melee attacks score in eight bracket bouts, including placement
  play. The scheduler settles scored matches and constructs the final standings.
  No team, damage, score, winner or reward is injected. Fixture profiles,
  proximity, calendar opening and match timeout clocks are supplied. The normal
  player timer expires revival protection; bounded repeat swings respect cooldowns.
- [x] All sixteen players return to Twin City. The champion claims through the
  NPC over TCP: 10,000 CPs and one bound P7 soul. A repeated claim leaves one
  durable claim receipt and unchanged reward totals.
- [x] Login with an unrelated item lock no longer queues nonexistent returns
  for LMS, Relic Rush, Wave Defense, Beast Trial, Duel Pit or Noble War. Existing
  admission ownership and actual return locking remain in place. The fixture
  exercises these login paths and the existing pending LMS lock/recovery cases.
- [x] Normal attacks no longer emit an unsolicited legacy inspection-cancel
  packet when neither inspection flag is active. Active inspection cancellation
  is preserved; its native UI remains unverified.

Evidence: `tools/Verify-ArenaProcess.ps1 -TeamWireOnly -ResetOwnedFixture`,
`obj/skill-team-combat-wire.log`, and the private redacted
`arena-process-team-wire.log` (`combat-final-passed scored=8`). Gameplay SHA256:
`D84835DF41CADB027A5928118BAD0DA8E357150D62A6B05D7FE4BC13E22C514E`.
The private test caps its own connection pool at12 for the disposable database's
20-connection limit; production connection settings are unchanged. Owned clone
cleanup passed. This adds TCP earning evidence, not restart persistence of this
specific payout, native DX9 acceptance, natural calendar observation or deployment.


## TCP-earned rewards after restart (2026-09-27)

- [x] Capture the actual eight-team run's sixteen participants after payout:
  exact persisted item UID/type/binding/quantity fingerprints, CPs, RVPs, silver
  and complete final-claim receipt rows. No reward state is supplied.
- [x] Stop and restart the private PostgreSQL instance and launch a new game
  process. All sixteen ordinary profiles reload matching fingerprints, balances,
  receipts and Twin City return state.
- [x] Reauthenticate the champion through real Account/Game TCP, open the clerk
  menu and select the prize option twice. Saved item fingerprints, currencies
  and claim receipts remain identical. This proves duplicate refusal after
  restart; it does not replace native inventory rendering acceptance.

The initial reload passed the sixteen-profile comparison but exposed a probe
error: a new session sent a dialogue continuation without opening the clerk.
The probe now opens the real NPC menu before claiming. The ownership-checked
`-ResumeTeamWireOwnedFixture` option reused the earned, retained clone without
replaying the bracket or supplying rewards. Only its disposable account's
credential was rotated for reauthentication; character state was not rewritten.
Evidence: `obj/skill-team-restart-wire.log` (earning phase and initial probe
failure), `obj/skill-team-restart-resume.log` and private
`arena-process-team-wire-reload.log` (`reload-passed`). Gameplay hash remains
`D84835DF41CADB027A5928118BAD0DA8E357150D62A6B05D7FE4BC13E22C514E`;
no gameplay rebuild or live deployment was required. Owned clone cleanup passed.


## Interrupted authenticated combat recovery (2026-09-27)

- [x] Eight ordinary two-player teams enter and fight through actual TCP. After
  quarterfinal settlements have created round-reward receipts, the next bout
  earns actual attack points. The fixture snapshots all sixteen players and
  abruptly exits the private game process before that bout settles. It does
  not call tournament close or player logout.
- [x] PostgreSQL and the game restart. All sixteen profiles reload unchanged
  item UID/type/binding/quantity fingerprints, CPs, RVPs and silver. Each player
  then reauthenticates through Account/Game TCP and reaches Twin City with
  Skill PK state cleared.
- [x] Every player opens the registrar and tries the final prize option twice.
  No final claim exists, no active tournament remains, every admission is
  acknowledged returned, and the complete round-reward receipt fingerprint is
  identical to its pre-interruption value. Earned prizes survive; an unfinished
  bout and cancelled final create no prizes.

Evidence: `tools/Verify-ArenaProcess.ps1 -TeamWireCrashOnly`,
`obj/skill-team-combat-crash.log`, and private
`arena-process-team-wire-crash.log` / `arena-process-team-wire-recover.log`.
The game exits after five scored bouts (four settled quarterfinals and the next
active bout); calendar/proximity/timeout controls remain fixture-supplied.
Gameplay hash remains
`D84835DF41CADB027A5928118BAD0DA8E357150D62A6B05D7FE4BC13E22C514E`.
The owned clone was removed and no probe processes remained. This verifies one
specific interruption boundary, not every transaction race or native UI path.
No live server restart, deployment or client acceptance was performed.


## Live client Hossu catalog cross-check (2026-09-28)

The619301 cap gap is not explained by a missing server-side copy of a client row.
A read-only check of the current installed client `ini/itemtype.dat` and
`ini/ItemtypeSub.dat`, together with the private runtime's two loaded server
catalogs, is recorded in
[the numeric evidence report](research/SKILL_PK_HOSSU_CATALOG_GAP.json).
The client files were decoded with the existing reviewed
`tools/stage_summer_overlay.py:transform`; no client process was executed.

Both main catalogs contain342 family619 rows. Their only quality1 row is619301:
class190, gender0, level129. Neither has a same-family/quality/class/gender row
at or below the Skill cap120. Both supplemental catalogs parse successfully
(1274 numeric client rows,1336 server rows) and contain no family619 rows.
The report records exact source paths, hashes and numeric fields; no client
catalog bytes or proprietary descriptions are committed.

Reproduction: decode each `.dat` with `transform(raw)`; leave server `.txt`
bytes unchanged; split lines and then `@@` fields; retain numeric IDs with at
least six fields. Select `id // 1000 == 619`, then compare fields2 (class),
4 (level),5 (gender), and `id % 10` (quality). The compatible candidate filter
is quality1, class190, gender0 and level<=120. Every input returns an empty
candidate list. Raw file SHA256 values are in the report.

The ordinary equipment job predicate permits class190 items for Taoists, so
its class number alone does not establish that619301 is unusable. This review
does not establish an acquisition source, change the existing safe refusal,
substitute another quality, or claim complete tournament admission support.

An owner rule question is pending: keep this item unequipped for Skill PK with
a clear player explanation, or define a documented Revora custom stat cap.
No implementation of either new behavior, native acceptance, deployment or
live restart occurred in this evidence batch. The compatibility checkbox stays
open until the selected behavior is implemented and validated.

## Approved Hossu refusal (2026-10-01)

The owner selected unequipping unsupported Hossu619301 before admission.
Preflight now returns an actionable SoaringSpirit message; registration sends
it before refusing reservation. The item stays owned and unchanged, and removal
restores eligibility. The267-check combat/catalog suite passes, including these
three new checks. See [approved rules](APPROVED_ROADMAP_RULES.md). Native message
presentation and live deployment remain pending. The earlier owner question
is resolved.
