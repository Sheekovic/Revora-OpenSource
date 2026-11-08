# Local Team and Skill PK recovery

## Entry audit (2026-09-27)

These are the legacy local Team/Skill PK tournaments in
`Game/Features/Tournaments/TeamSkill.cs`, distinct from Quarantine, Team Arena and
cross-server result delivery. They are not yet included among the54 verified
reward families.

The regular Team PK schedule is active in `Thread.cs` on Saturday18:56. The Skill
PK schedule is commented out. This batch does not enable it or change calendars.

Two verified source defects are fixed:

- `ElitePkStatus.InSkillTeam` returned before checking regular Team PK whenever
  Skill PK was inactive, and could dereference absent tournament arrays. It now
  checks each series independently and requires the exact registered team object.
  `Team.AllowADD` uses this result to prevent additions during an existing match.
- Both level-group helpers mapped120?129 to group3, bypassing group2. They now
  share the four boundaries already stated by the NPC rules:1?99,100?119,
  120?129 and130+. The entry checks and legacy prize helper use the same mapping.

An isolated build passes (`obj/team-pk-entry-build.log`).
`tests/TeamPk/EntryTests.cs` passes16 deterministic checks for independent regular
and skill membership, stale team identity, invalid/missing state and all bracket
boundaries. These are component checks, not full match or reward acceptance.
The documented committed-source build overlay preserves unrelated owner work.
No private candidate refresh, production deployment or client acceptance occurred.

## Next milestone: durable results and purses

Both `TeamTournament.GetReward` and `SkillTeamTournament.GetReward` set the
in-memory `RceiveReward` flag before their NPC branch changes silver. Top-eight
state is separately written by `SaveTop` to an INI file. Those operations cannot
atomically guarantee one payment across failure or restart. The per-character
`ClaimedTeamPK`/`ClaimedSTeamPK` flags are not a durable per-round purse ledger.
The regular NPC reports four silver tiers:1B,500M,250M and10M for remaining top8;
this audit records existing amounts and does not endorse their balance.

Required implementation sequence:

1. Persist a stable round identity and final bracket placements, freezing the
   eligible leader and reward terms before any payment. Preserve historical
   results without inventing new payable victories.
2. Commit currency, the owner's valuable event reward (bound P7 or another
   explicitly specified eligible reward) and a per-round claim receipt together.
   Keep insufficient capacity pending; make concurrent/repeated claims safe.
3. Replace both NPC claim paths with the durable service, preserving ordinary
   nearby-player access and existing eligibility. Do not enable Skill PK merely
   to test its claim storage.
4. Verify an ordinary earned result, exact reload, full-bag deferral, failed-write
   retry and fresh-process recovery. Only then count an additional reward family.

Full matches, membership concurrency, legacy match participation awards, native
presentation and live deployment remain open; the entry fixes do not certify them.


## Stable run and final-result ledger (2026-09-27)

New runs now use a deterministic identity per local calendar date, series and
level bracket. `revora_local_team_pk_runs` records active/complete/cancelled state.
An existing completed or cancelled date cannot reopen. Initialization cancels
unfinished records without manufacturing placements; completed records survive.
Historical INI results are not imported into this ledger.

Before publishing the final ranking, the tournament freezes eight distinct leader
UIDs, existing silver/CP tiers and one catalog P7 type per leader. One transaction
saves the complete snapshot. An identical retry succeeds; a different result
cannot overwrite it. Failed writes retain the frozen runtime result for retry.
These soul types are reserved reward terms, not delivered items yet.

Callbacks are serialized and release their owner on failure. Reopening a later
date resets the previous runtime stage and rosters; stopped subscriptions are
cleared so a later round can subscribe. Entry closes when its registration timer
expires. A new round's intermediate Top8 entries cannot pass the legacy claim
gate before final-result persistence. Historical INI behavior is unchanged here.

The isolated build and18 entry/14 database checks pass. Database checks cover
stable identity, separate series/brackets/dates, repeated starts, real catalog P7
selection, failed result writes and retry, exact currency arrays, immutable
leaders, conflicting retries, duplicate leaders, both series' terms, component
startup cancellation and retained completed results. Logs:
`obj/team-pk-ledger-build.log`, `obj/team-pk-ledger-tests.log`.
Tests use a uniquely owned fixture schema on the existing private candidate DB;
no ordinary character records or live services are changed.

**Still required:** replace the legacy claim flag/payment with atomic per-leader
claims against these saved terms; ordinary earned match/claim/reload tests;
actual process interruption and composed startup; native/live acceptance. The
callback restart changes are source/build checked, not a real weekly schedule
acceptance. This foundation does not increase the54 verified reward-family count.


## Atomic saved-term claims (2026-09-27)

The two legacy NPC claim branches now call the ledger service instead of setting
`ClaimedTeamPK`/`ClaimedSTeamPK` and paying separately. For a new completed result,
the ordinary nearby leader receives the frozen series-specific currency plus
one permanent bound P7. Owner and result rows are locked; currency, item and the
unique per-run/per-leader receipt commit together. Inventory/currency capacity,
saved preimages, canonical session, trade state and NPC proximity are checked.
A full bag defers everything. Failed writes fence the session for reconnect;
repeated or concurrent requests cannot pay the same receipt again.

The legacy INI results are not converted to fresh entitlements. This service
reads only the new saved final results. It does not depend on the mutable legacy
claim flags or a player's current team/level after earning the placement.

Isolated build,18 entry checks and26 combined ledger/claim checks pass. The12 new
checks cover unplaced/foreign-NPC refusal, full-bag deferral, injected item-write
rollback, concurrent payment, exact frozen-item identity/binding/permanence,
regular silver versus Skill CP terms, repeat claims, retained receipts and trade
refusal. Fixture accounts are non-GM; placements are supplied to the result
service, not earned through a complete tournament. Logs:
`obj/team-pk-claims-build.log`, `obj/team-pk-ledger-tests.log`.

**Public access remains incomplete:** both417171 and801717 are parked/closed by
`TwinCityPlaza`; the early closed-desk dispatcher still intercepts them. The
claim fixture supplies nearby NPCs directly. No ordinary NPC earning/claim route,
full character reload, process-crash, native or deployment gate is marked done.
Next: restore the regular clerk with a complete ordinary entry/match/result/claim
fixture; keep Skill PK's disabled schedule unchanged. The54-family count stays
unchanged. Only the two claim hunks in the owner's modified `Npcs.cs` are staged;
the committed-source validation overlay includes those same changes.


## Timeout winner correction (2026-09-27)

Before reopening the regular clerk, the match-path audit found that `Match.Export`
passed the higher-scoring team to `End`, whose argument is the loser. Timed-out
unequal-score fights therefore eliminated the rightful winner. The comparison
now selects the lower score. The existing equal-score rule (first fighter loses)
is preserved; this is not a new tie-break policy.

A fixture invokes the actual `Match.Export`/`End` methods with two non-GM teams
and an expired match clock. It failed against the previous build for900/100 and
passes after the change. Both score orderings, equal scores and repeated exports
now pass:24 entry/match checks total, isolated build successful
(`obj/team-pk-timeout-build.log`). Scores are supplied by the fixture, not earned
through attack packets. No database/result fixtures or live services were changed.
Clerk restoration and the complete ordinary admission/match/claim/reload journey
remain pending; this correction prevents final placements from preserving an
inverted timeout result when that route is restored.


## Ordinary eight-team world route (2026-09-27)

The regular clerk is restored as TeamRegistrar at1002 (291,146); the existing
invitation landing (293,146) stays outside its footprint. Placement is idempotent
and only replaces this UID at its exact retired plaza slot,6000 (58,74), rejecting
other identities. Its short handler runs before the legacy closed-desk fallback,
checks ordinary nearby ownership and offers entry or saved-purse claims. The
Skill clerk and its disabled schedule remain unchanged.

Admission now owns the bracket callback and team command locks, checks the open
registration deadline, active same-level-group members and NPC proximity, and
confirms waiting-area arrival before registering. Repeated member entry does not
reset fighter state. At registration close, incomplete teams are removed. Match
import cannot pull teammates from unrelated maps. Finals and undersubscribed
rounds return entrants from the owned waiting area to1002 (327,303); each match
restores the previous PK mode on return. Failed final travel keeps the close
retryable. Durable crash-time admission/return recovery remains unimplemented.

The world fixture uses the installed1002/2068/700 terrain and eight ordinary
single-member teams. It drives normal NPC admission, actual dynamic match
import/export, semifinals, third place and final, saved eight-place results,
normal town return, NPC claim, production item reload and a fresh production
character-state reload. Exact1B silver and the frozen permanent bound P7 remain
owned; replay does not pay twice. It exposed and fixed Team PK's incorrect reads
from the retired Solo Elite PK bracket array during match entry/completion.
The fixture checks actual arena arrival, not just a teleport request.

**Validation:** isolated build,24 entry/match checks and53 combined ledger/claim/
world checks pass. `tests/TeamPk/Verify-Ledger.ps1` compiles the combined DB/world
fixture when supplied the existing private-candidate connection through its
protected environment; the test enforces loopback25439/revora_candidate_game and
owns only its temporary schema. Logs: `obj/team-pk-world-build.log`,
`obj/team-pk-ledger-tests.log`. The Npcs validation overlay includes exactly the
new dispatch hunk; unrelated owner edits remain excluded.

**Limits:** supplied match scores and advanced fixture clocks are controlled
inputs, not attack-packet or natural-calendar evidence. More-than-eight-team
knockouts, multi-member teams, actual composed startup/retired-clerk placement,
process-crash recovery, map cleanup, sockets, native acceptance and live deployment
remain open. This is one regular Team PK server reward family, not certification
of Skill PK or the complete event. No private refresh or live deployment.


## Multi-member and preliminary-round return coverage (2026-09-27)

The world fixture now uses17 teams of two ordinary players (34 entrants), covering
the three-team preliminary phase as well as the top-eight bracket. Both members
enter through the clerk, retaining one fighter-state object. Leaders start in
Capture mode and teammates in Peace mode; each original mode survives match and
town returns. Only placed leaders qualify: the winning teammate and a leader
eliminated before top eight both fail the ordinary NPC claim without payment.
The earned winning purse still passes exact item and character reload/replay.

The preceding16-team run exposed a real cleanup defect: `Purge` removes losing
teams before the final, while the old final return enumerated only surviving
teams. Those preliminary losers remained in the waiting map. A separate admitted
entrant list now survives elimination and supplies the final/cancelled return;
it resets only when a new round begins. The16-team fixture failed before this
fix and passed afterward. The retained17-team fixture passes all34 returns.

Isolated build and24 entry/match checks pass; the combined ledger/claim/world
suite passes107 checks (`obj/team-pk-multiteam-build.log`,
`obj/team-pk-ledger-tests.log`). Counts include repeated entrant checks, not107
new feature tests. Scores and clocks remain controlled inputs. Five-member
teams, brackets above24, disconnected/replaced sessions, durable crash returns,
owned map cleanup, composed startup, sockets/native and deployment remain open.
No live or private runtime deployment; tracked reward-family count remains55.


## Full teams, knockout stage and owned-map cleanup (2026-09-27)

The retained fixture now uses25 five-player teams (125 ordinary entrants), covering
the above24 knockout stage, its bye, the top-eight bracket and final. Every member
enters without replacing the team's fighter state; all125 return with their
original PK modes. Leader-only payment and production character/item reload still
pass. The earlier17-team run remains separate evidence for the three-team phase.

Every match allocation now registers its map with the owning tournament. Final
cleanup removes only those exact map objects, after checking that no character
remains in them, and clears stale match arrays. Base maps and the reusable waiting
area remain registered. A spectator occupying an arena keeps closure retryable;
a later tick completes cleanup once that map is empty. A later round cannot
replace a runtime that still owns unreleased match maps. Entrants still in an
owned arena are included in final return, with their previous PK mode restored.

Isolated build,24 entry/match and141 combined ledger/world checks pass. The last
two checks prove occupied-map deferral and registry cleanup down to the three
fixture base maps plus its reusable waiting area. Counts include repeated entrant
assertions, not141 independent features. Logs: `obj/team-pk-cleanup-build.log`,
`obj/team-pk-ledger-tests.log`. The spectator's presence is supplied as a fixture
map occupant; native watcher controls are not accepted by this test.

The tested full-team and above24 paths are now verified at component/world scope.
Disconnected/replaced sessions, durable admission/return crash recovery, actual
composed startup, combat packets, natural scheduling, native presentation and
live deployment remain open. Skill PK remains closed. No private refresh or live
deployment;55 tracked reward families unchanged.

## Actual private startup (2026-09-27)

- [x] The candidate built from gameplay through `2ff63bca` passes two actual
  isolated game/database startup and normal shutdown cycles. Both starts emit
  the restored TeamRegistrar marker at map 1002 (291,146).
- [x] The runtime role owns both local Team PK tables. Their table identities,
  together with the other 28 checked roadmap tables, survive the database restart.

Command: `tools/Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup`.
Evidence: `obj/team-pk-host-startup.log`. Candidate executable SHA-256:
`3036794BCC3575079A6B42DD28E079D32241C7CF0E61C7F3D0A15CF7798A7176`.
The previous candidate executable and manifest are retained. This is an isolated
validation build, not a clean release or live deployment. Candidate game and
PostgreSQL are stopped. This check establishes startup/schema/clerk placement;
it does not establish in-flight reward or admission recovery across a crash.
Disconnect recovery, durable returns, combat packets, natural scheduling and
native acceptance remain open. Skill PK stays closed; 55 families remain tracked.

## Replaced-session final return (2026-09-27)

- [x] Runtime admissions retain character identity and the entry PK mode instead
  of a GameState reference. Final return resolves the current canonical session,
  restores that captured mode and returns it from this bracket's waiting/owned
  match maps. A replacement session's default PrevPKMode is not used.
- [x] The 125-player fixture replaces an admitted member's session before final
  cleanup, places the replacement in an owned arena with a deliberately incorrect
  PrevPKMode, and verifies town return, original mode and untouched stale session.

Isolated build, 24 entry/match checks and 143 combined ledger/world checks pass.
Logs: `obj/team-pk-session-build.log`, `obj/team-pk-session-tests.log`.
This is a controlled session replacement, not actual socket reconnection. It
covers a healthy replacement already present at cleanup; it does not prove a
concurrent login race, offline return after closure, continued match membership,
or durable admission/return recovery across a process crash. Those remain open.
No candidate refresh, native acceptance or live deployment in this batch.

## Durable admissions and offline return (2026-09-27)

- [x] Reserve an admission before arena/waiting-area teleport, with the original
  PK mode and a unique pending owner across both series. Same-run readmission
  cannot replace that record. Admission holds session and team/item ownership.
- [x] Login projects a pending entrant to Twin City (327,303) with the captured
  mode, leaving the receipt pending until the installed session completes return.
- [x] Final return persists the town destination and acknowledgement together.
  The existing recovery scheduler scans at most 32 pending online owners per
  poll, retaining offline owners for later login. Current participants are left
  in their bracket; an active callback defers recovery of its mutable maps.
- [x] Failed acknowledgement preserves the pending receipt and fences the session;
  a fresh production entity load retries it. Interrupted runs are cancelled on
  initialization without deleting their pending admissions or inventing rewards.
- [x] Isolated build, 24 entry/match checks and 156 combined ledger/world checks
  pass. The ordinary 125-player bracket saves and acknowledges all admissions.
  A separate reserved entrant is saved in the waiting area, removed from the
  session pool, recovered after component initialization, subjected to an injected
  return-commit failure, and recovered through fresh production entity loaders.
- [x] Two actual isolated game/database start/normal-stop cycles retain all three
  Team PK tables under the runtime role (31 checked roadmap tables total).

Logs: `obj/team-pk-admission-build.log`, `obj/team-pk-admission-tests.log`,
`obj/team-pk-admission-host.log`. The private candidate executable SHA-256 is
`6E32067EBB6E478F1378C1AD009E11548A09219188A2266199E4B4C829FF0505`.
Prior candidate artifacts are retained; both private processes are stopped.

This batch does not prove an in-flight process kill, a concurrent socket login,
continued participation after reconnect, or native presentation. A reconnecting
entrant returns to town rather than resuming a stale team session. PK restoration
is performed for recovery; general persistence of ordinary PK-mode preferences
across later logins is outside this change. Registration/match item awards still
need their separate retry/atomicity audit. Skill PK stays closed. No live deploy.

## Receipted regular registration reward (2026-09-27)

- [x] Eligible regular registration records `entry_earned` on its durable
  admissions before attempting delivery. The existing item 720793 remains
  unbound; its type, quantity and registration eligibility are unchanged.
- [x] Item insertion and the exact `entry_item` receipt commit together under
  session/item ownership and the character row lock. Repeated timer/delivery
  attempts cannot mint another item. A unique index protects receipt item identity.
- [x] A full bag retains the entitlement. The bounded existing return poll also
  delivers pending entry rewards after return or reconnect; delivery is independent
  of the tournament's later completion/cancellation state.
- [x] The 125-player fixture verifies 125 exact persisted rewards and replay.
  Full-bag deferral, injected item-insert failure, fresh-session recovery, eight
  concurrent delivery calls, and production item reload pass. Isolated build,
  24 entry/match checks and 162 combined ledger/world checks pass.

Logs: `obj/team-pk-entry-reward-build.log`, `obj/team-pk-entry-reward-tests.log`.
Two nullable/defaulted columns and an item-identity index migrate existing
admissions during initialization. Component initialization is exercised repeatedly;
this batch has not refreshed the private candidate or run an actual migration
against its existing table. The Skill PK match item (720981) and legacy rank-pack
helpers remain separate audit work; Skill PK remains closed. In-flight process
termination, concurrent socket reconnect and native/live acceptance remain open.

## Actual entry-reward migration and recovery (2026-09-27)

- [x] The existing private admission table migrates through actual game startup.
  Both entry-reward column types/nullability/default and the valid unique partial
  item-receipt index are checked after each of two game/database cycles.
- [x] Fixed private storage fixtures cover an offline pending return with an
  unpaid reward, a returned/paid reward, and a returned/unpaid reward. The actual
  next startup cancels their active run without creating placements; an exact
  row fingerprint proves all three admission/reward records stay unchanged.
- [x] The fixtures are removed after verification; no account, character or item
  is created. Both game exits are normal and the private database is stopped.

Command: `tools/Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckLocalTeamPkRecovery`.
Log: `obj/team-pk-entry-recovery-host.log`. Candidate gameplay: `5510b36e`;
executable SHA-256:
`C5CDEB4F90D95EE9D19E158BEE3BB8303949C5BB5EC6A4028BAF9E8FE8D589CD`.
Prior candidate artifacts are retained. No additional gameplay build was needed.

This is actual startup/migration and stored-state recovery evidence, not an
in-flight crash or an earned-player socket test. Those gates, natural scheduling,
combat packets, native validation and live deployment remain pending. Skill PK
remains closed. The earlier 162 combined and 24 entry/match checks remain the
component earning/reward evidence; these counts are not added to startup cycles.

## Scheduled opening and invitations (2026-09-27)

- [x] Registration announcements now match the existing Saturday 18:56 server-time
  schedule and one-minute window, naming Team Registrar at Twin City (291,146).
- [x] `Open` serializes opening attempts and reports whether brackets actually
  started. The world scheduler sends invitation/reset UI only for that successful
  opening, rather than repeating it on every callback through second 35.
- [x] The location invitation is available to ordinary eligible players without
  requiring guild membership. The original invitation landing remains (293,146).
- [x] Isolated build and 34 entry/match/schedule checks pass: Saturday boundary,
  delayed callback through second 35, closed times/days, unavailable brackets and
  repeated already-open calls. Earlier 162 ledger/world checks are unchanged.

Log: `obj/team-pk-schedule-build.log`. Tests use supplied timestamps for the
schedule predicate; they do not observe a natural Saturday opening or exercise
concurrent successful database-backed opens. Those acceptance limits, in-flight
crash/reconnect, native and live deployment remain open. No private refresh.

## Concurrent opening acceptance (2026-09-27)

- [x] Eight parallel calls to the actual `TeamTournament.Open` against the
  private fixture database yield exactly one successful invitation trigger.
- [x] Four real bracket instances persist four distinct active run identities,
  enable registration, and register their real timer callbacks.
- [x] Resetting the UI's Opened flag does not permit the same date to start again.
  The fixture cancels its runs and disposes its subscriptions and waiting maps.

The full combined ledger/world suite passes 166 checks using the existing
`cc759fe8` gameplay build; no new gameplay build was required. Log:
`obj/team-pk-concurrent-opening-tests.log`. The fixture registers callbacks in
an unstarted private timer pool, so it verifies opening concurrency and durable
identity but does not claim wall-clock scheduling or actual invitation rendering.
Earlier 34 entry/match/schedule checks remain separate evidence. Natural calendar,
in-flight crash/socket reconnect, combat and native/live gates remain open.

## Damage-handler scoring and team defeat (2026-09-27)

- [x] Replace four divergent legacy scoring blocks with a shared match validator:
  canonical player sessions, opposing registered teams, actual shared arena and
  match identity, fighting placements and an unfinished imported match.
- [x] Credit every accepted lethal/nonlethal hit once before death, capped to
  remaining target HP. Saturate uint scores rather than wrapping them around.
  Missing defender matches and self/same-team scoring are safely rejected.
- [x] Physical and magic defeat checks run after death. A match ends when the
  opposing team has no living members, not before the final death is applied.
- [x] The actual `Handle.ReceiveAttack` route verifies physical/magic nonlethal
  damage and five alternating physical/magic lethal hits. Each hit credits the
  remaining HP and only the fifth death ends the match. The surviving higher-UID
  team continues through the existing full 125-player earning fixture.

Isolated build, 34 entry/match/schedule checks and 183 combined ledger/world
checks pass. Logs: `obj/team-pk-combat-build.log`, `obj/team-pk-combat-tests.log`.
Other matches still use controlled scores and clocks. Extra helper checks cover
self/same-team rejection, missing matches, overkill and saturation. The test calls
the damage handler directly: socket decoding, attack eligibility/calculation,
concurrent health/timeout races, native presentation and natural scheduling are
not certified. No private refresh or live deployment. Skill PK stays closed.

## Shared combat and match lifecycle ownership (2026-09-27)

- [x] The local Team PK combat lease holds match, character-session, item and
  health ownership across the whole damage callback, score update and death.
  It uses nonblocking acquisition: a contended or invalid match hit returns zero
  before health mutation. Unrelated world combat receives no local Team PK lease.
- [x] Import, End and Export share the match monitor, preventing lifecycle/timeout
  handling from interleaving with a hit's HP and score mutations.
- [x] Thirty-two parallel actual damage calls preserve exact equality between
  accepted damage, target HP loss and team score. A deliberately held match lock
  rejects a cross-thread hit with no state change. Export blocks while the match
  is owned and resumes without changing the already resolved winning team.

Isolated build, 34 entry/match/schedule and 187 combined ledger/world checks pass.
Logs: `obj/team-pk-combat-ownership-build.log`,
`obj/team-pk-combat-ownership-tests.log`. This verifies the bounded combat/lifecycle
contention scenarios above; it is not a general stress or exhaustive race proof.
The tests still call the damage handler directly and control bracket clocks.
Socket attack validation/calculation, in-flight process failure/reconnect,
natural scheduling and native/live acceptance remain open. No private refresh
or live deployment; Skill PK remains closed.

## Ordinary TCP clerk access and startup-order correction (2026-09-27)

The earlier startup marker proved that Team Registrar was initially placed; it
**did not prove final availability**. Actual ordinary TCP testing found that the
later `TwinCityPlaza.Apply` moved NPC 417171 back to map 6000 after placement.
The prior composed-startup checkmark must be read with that limitation.

- [x] Exclude the restored regular clerk from the plaza parking pass, preserving
  all historical slot spacing. Skill PK stays retired at map 6000 (62,74).
- [x] Startup verification now rejects a later parking log for the regular clerk.
- [x] Ordinary non-GM character RoadmapProbe reaches Team Registrar over the real
  account/game TCP path, receives the top-eight menu, has closed registration and
  an unearned purse refused, and retains exact inventory/currencies.
- [x] Both actual private game/database cycles pass, including persisted earned
  career receipts and return to Twin City (327,303). The test return route uses
  (327,280) as a waypoint after a landing at (318,294) was rejected by the server.
- [x] Isolated build, 34 entry/match/schedule and 188 combined ledger/world checks
  pass, including the subsequent plaza pass's restored/retired NPC selection.

Logs: `obj/team-pk-plaza-build.log`, `obj/team-pk-plaza-tests.log`,
`obj/team-pk-clerk-wire.log`. Candidate executable SHA-256:
`466A033CE27CF48735808F47456AED1F8F9B0F17DED4563A09A7AE99CA9A4553`.
The prior executable/manifest are retained. Candidate game and database stopped.
This proves closed-calendar TCP access/refusals, not open-calendar entry, earned
TCP tournament claims, socket combat, in-flight crash/reconnect, native rendering
or deployment. Those remain open. No live deployment; Skill PK remains closed.

## Actual admission interruption and active lifecycle hooks (2026-09-27)

The real interruption test exposed a second integration gap: `WorldTournaments`
has an unconditional return before the old guild-reward and Team PK sections.
The local return poll and scheduled opening were therefore unreachable despite
passing isolated component tests. The regular Team PK opening block and return
poll now execute before that return. Other legacy blocks remain disabled and
require their own integration audit. Earlier schedule checkmarks established
predicates/concurrent opening, not execution by the composed world timer.

- [x] Ordinary TCP RoadmapProbe reaches the clerk. The isolated operator's existing
  `@team` command opens registration and creates its one-person team; admission
  itself uses the normal NPC request and the character's correct level bracket.
- [x] Kill only the hash/path-checked private game process while that session is
  admitted. Confirm its active, unreturned receipt survived with existing career
  rewards unchanged. No placements or tournament rewards are manufactured.
- [x] Login now completes a pending local return immediately after canonical
  session activation, before ordinary gameplay resumes; failed recovery refuses
  continued login. The active world timer still retries later/offline returns
  and deferred participation rewards.
- [x] After the wiring repair, the same interrupted admission recovers through
  actual TCP login and survives another full game/database restart. Its exact
  run identity and PK mode persist, the run is cancelled, return is acknowledged,
  entry reward remains unearned, and no placements/items/currency are invented.
- [x] The repeat-crash guard refuses recently used private openings instead of
  clearing retained run history. Its deliberate refusal was verified.

Evidence: `obj/team-pk-live-crash.log` records successful live admission/termination
and the initially failed recovery; `obj/team-pk-live-return.log` records the two
passing recovery cycles. Build: `obj/team-pk-active-hooks-build.log`; guard:
`obj/team-pk-crash-repeat-guard.log` (expected refusal). Candidate SHA-256:
`6E60869DF011988F2DC99E27503EFBB00F3F4D03AFCD5EFA7B423007DD208226`.
The existing `-CheckCourierCrash -CheckTournamentAccess` combination selects the
Team PK admission checkpoint, with the completed-career flags. `-CheckLocalTeamPkReturn`
selects recovery of a previously interrupted admission without opening a new run.
Earlier harness logs use the historical Courier label for generic session termination;
the Team PK checkpoint and database assertions identify the actual tested event.

This proves one in-flight admission interruption, not interruption during combat,
result freeze, currency/item commit or post-commit projection. Operator-created
team membership does not establish native team invitation UX or a full TCP
winner journey. Natural Saturday execution remains unobserved. No live deployment
or native acceptance; private game/database stopped and original artifacts retained.
34 entry/match/schedule checks also pass; the earlier 188 world/ledger checks were
not rerun for this hook-only change.


## Skill opening prerequisite (2026-09-27)

Skill opening now has the regular series' serialized success result and null/empty
checks.37 entry and192 ledger/world checks pass, including concurrent four-bracket
Skill opening and same-date replay refusal. The clerk and calendar stay closed.
The source/official-guide comparison found missing Skill combat-cap readers,
non-durable per-match item delivery and unresolved timing/edition differences.
[Evidence and implementation sequence](SKILL_TEAM_PK_RESTORATION.md).


## Regular Team PK authenticated finals and persistence (2026-09-28)

`Verify-ArenaProcess.ps1 -RegularTeamWireOnly` passes the regular series through
the shared private socket harness on unchanged game SHA-256
`A40A9692DAD1C63CEF1C6F7E53A4F77F02AA98CDFDEA413FB7E352F7A8E67403`.
Regular mode selects registrar417171 at291,146, regular tournament arrays and
kind0 receipts; it does not route players through the Skill registrar or claim
its CP purse. Defaults retain the existing Skill fixture behavior.

- [x] Sixteen ordinary accounts authenticate, form eight two-player teams through
  actual create/invite/accept packets, and enter via the regular registrar.
- [x] Production melee packets earn positive scores in eight bouts. The bracket
  finalizes with the expected winning leader; all sixteen return to Twin City.
- [x] The champion claims through NPC packets twice and receives exactly the
  existing1B-silver purse, no CPs and one bound P7 soul, with one final receipt.
- [x] Across game/database restart, all sixteen exact item fingerprints, final
  claim receipts and CP/RVP/silver balances match. Authenticated subsequent
  clerk claims leave those snapshots unchanged.

The shared fixture supplies character eligibility, positions, registration
opening and match/preparation deadlines. It waits for normal revive protection
and attack cooldowns; scores and damage are not assigned. This proves the regular
100-119 bracket's controlled eight-team path, not naturally observed Saturday
scheduling, every bracket/team count, native presentation or a full-duration
competitive balance review. Client-visible currency rendering is not asserted
by this server-balance snapshot. Mid-combat crash remains a separate pending
regular-series case; the earlier admission-crash check is not relabeled.

Evidence: `obj/regular-team-wire.log`, private
`arena-process-team-wire-regular.log` (scored=8), and
`arena-process-team-wire-regular-reload.log`. The first attempt timed out on the
second account's initial SetLocation response after authentication, with no
explanatory database exception in its redacted log. One unchanged retry passed;
that timeout is unexplained, not a resolved production defect. Repeated phase
logs overwrite earlier instances. No gameplay build, deployment or production
data change occurred. The owned clone was removed following success.


## Interrupted regular TCP combat recovery (2026-09-28)

`Verify-ArenaProcess.ps1 -RegularTeamCrashOnly` now passes on the same private
`A40A9692DAD1C63CEF1C6F7E53A4F77F02AA98CDFDEA413FB7E352F7A8E67403`
executable. It uses the prior ordinary eight-team socket invitation, admission
and combat route, then abruptly exits the game after the fifth scored bout.
It does not call tournament closure or player logout before interruption.

- [x] Before exit, all sixteen regular entry rewards must be earned and have
  durable item IDs, while no final claim exists. Snapshot each character's
  exact item/currency/claim state and all run/UID/saved-PK-mode/entry receipts.
- [x] Restart the database and game, require canonical return to Twin City and
  exact character reward snapshots, then authenticate all sixteen accounts.
- [x] Each player attempts the regular clerk claim twice; no final claim or
  completed regular run is manufactured, no active run or pending return remains,
  and all frozen entry receipts and item IDs are unchanged.

Evidence: `obj/regular-team-crash.log`, private
`arena-process-team-wire-regular-crash.log` and
`arena-process-team-wire-regular-recover.log`. Both phases passed on their first
attempt in this batch; this does not resolve the earlier isolated login timeout.
The previously documented eligibility, proximity and accelerated match deadlines
remain controlled. No damage/score is assigned and no saved rewards are seeded.
This closes the regular mid-combat interruption scope at the tested checkpoint;
interruption during final result commit or reward projection is not covered by
this test. Natural calendar, native UI/combat and live deployment remain open.
No gameplay rebuild or production change. The owned clone was cleaned.
