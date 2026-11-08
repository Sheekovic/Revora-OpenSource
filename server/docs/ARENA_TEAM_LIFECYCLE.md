# Team Arena lifecycle migration

Team Arena still uses legacy reward delivery and is a prerequisite for activating
the durable solo Arena lifecycle: both modes write the same point balance. It is
not yet a durable event earning path.

## Implemented outcome ownership

QualifierGroup.End accepts only the exact original Player1 or Player2 session as
the losing leader. Null, unrelated leaders and same-UID replacement sessions
cannot select the outcome. The original leader remains valid if team membership
has already been removed. CheckEnd recognizes that leader's disconnect before
consulting mutable team membership.

A per-match lock freezes winner, loser, ending time and terminal flags together.
In legacy mode only the first accepted ending sends outcome notifications, after
the fields are frozen. Initialized staged mode defers them until durable completion. Competing timeout/leader callbacks cannot rewrite the result. Existing
timeout policy remains higher total damage wins, with the second side winning
a tie. This is an in-memory ownership correction, not a durable result receipt.

The combined Arena fixture checks foreign and replacement sessions, the actual
CheckEnd disconnect callback with missing team membership, concurrent endings,
replay stability and timeout selection. It does not prove real-client packet
rendering, socket teardown, reward replay protection or an entire team fight.
Ignored log: obj/chi-refill-validation/team-outcome-tests.log.

## Admitted roster and damage ownership

Import snapshots eligible candidate sessions from each side, rejects overlapping
sides or missing eligible leaders, and permits only one import attempt per group.
Only sessions that actually reach the owned map and receive this group reference
enter the recorded roster. The internal snapshot accessor returns a copy.
Fight setup uses those recorded participants. Damage is enabled only after both
leaders have been admitted.

Damage and all-dead checks use recorded sides rather than current team leadership
or membership. Damage requires the exact canonical session, match reference and
map; outsider and departed-session callbacks cannot score. Opposite-side damage
credit is applied only after establishing the caller's admitted side. Totals
saturate, and the outcome lock serializes damage with timeout selection. Ended
matches reject late damage. Score updates go to recorded current participants.

The fixture performs actual team import, verifies copied roster isolation,
repeated-import protection, member and opposite-side scoring, outsider rejection,
continued side ownership after disband, departed-session rejection, concurrent
increments, saturation and late-hit rejection. This does not yet test every
partial-admission/cleanup failure or a complete client team encounter. Log:
obj/chi-refill-validation/team-roster-tests.log.

## Legacy roster rewards and export

Win now selects recipients from the admitted roster of a finished match. Leader
bonuses use the original side leader, not the current team leader flag. A late
joiner or unplayed invitation has no admitted reward recipients. A per-match
random delta and per-session applied set prevent repeated callbacks from applying
the legacy point/counter projection twice while the match object remains alive.
Only canonical, unfenced sessions still owned by the match are projected.

Export serializes with outcome selection and spectator admission, attempts owner
locks in UID order and retries when an owner is busy. The scheduler keeps the
terminal flag while retrying. It returns admitted fighters and owned spectators
before removing the exact group registration and disposing the map. Team
membership may be gone. Later match references and replacement sessions are
preserved; admitted member invitation and exact-session queue state are cleared.
Canonical fenced actors still on the map retain retryable cleanup. Repeated
completed export is inert. New spectator admission is closed after the outcome.

The fixture now runs actual import, disband, result/export and production saves
plus team-row reload. It covers a late joiner receiving nothing, original
leader/member awards, busy-owner deferral, participant return before map disposal,
member queue reset and repeated export/result callbacks. It does not prove
atomic rewards or durable replay protection across a crash. Disconnected or
fenced recipient recovery remains a required migration step; the in-memory
applied set must not be treated as a durable entitlement receipt. Log:
obj/chi-refill-validation/team-export-tests.log.

## Inactive result transaction participant

TeamArenaResultStatistics now provides Initialize, Record and Publish for a
future coordinator. No production lifecycle calls it. Each group owns a UUID,
a UTC outcome observation and a frozen 30?49-point leader delta shared with the
legacy path. Record requires a parent transaction, the match lock and all exact
canonical participant locks. It verifies admitted sides and statistic identities,
locks entity rows in UID order and checks both saved point mirrors plus all four
team result counters against their in-memory preimages.

One transaction writes the match receipt, every participant's before/after
statistics, team counters and both point mirrors. Leader/member increments and
bounded losses follow the existing policy. Replays require the same outcome,
delta, roster size, participant identities and roles, with no missing or extra
participant receipts. A valid replay returns no projection. Publish validates all
cache preimages first and installs committed values before sending updates.
The caller must commit first and hold all locks through Publish.

The disposable-schema fixture uses actual team import/outcome, parent rollback,
a forced later-participant receipt failure, a changed member preimage, concurrent
retries, both point mirrors and production team reload. An incomplete receipt
bundle fails closed. Log: obj/chi-refill-validation/team-transaction-tests.log.

This participant does not yet coordinate a ranking-day boundary or replace the
live legacy writer. Recovery entries are described below. Intent capture and
save/reset protections are described below.
Those are required together before activation; it is not a completed earning path.

## Durable team save and reset ownership

After the inactive team participant is initialized, a team statistic receipt
marks that player as managed independently of solo rollover initialization.
The shared Arena ownership check recognizes it, preserving points through both
solo save overloads, solo reset and loading an old point-fill timestamp.

Both team save overloads retain only canonical identity metadata for managed
team players. They cannot overwrite points, Honor, result counters or season
fields. Detached sessions cannot save even metadata. Team reset now takes the
existing owner lock and leaves managed or invalidated statistic objects intact.
Unmanaged team counters retain their legacy save/reset behavior, including for
players managed only by the solo result lifecycle.

The fixture corrupts cached economics deliberately, exercises both save overloads,
checks preserved committed rows and refreshed metadata, removes canonical session
ownership, then reloads with solo rollover initialization disabled. Both reset
paths preserve the loaded team result. This is not a socket reconnect test and
does not migrate the remaining team/solo earning writers or add daily rollover.
Team transaction initialization is still absent from production composition.
Log: obj/chi-refill-validation/team-save-tests.log.

## Captured team outcome before reward completion

Capture now commits a separate immutable outcome and participant snapshot while
the caller retains the match and canonical owner locks. It shares the roster,
persisted preimage checks and bounded reward calculation with Record. The capture
contains winner/loser identities, original UTC observation, frozen delta, roles,
and every participant's before/after points and four counters. Failure anywhere
rolls back the entire capture without changing player balances.

Record requires that complete captured identity and exact before/after values.
Even a coherent later change to both cache and database cannot replace the
captured preimage. Captured snapshots establish managed save/reset ownership
before reward receipts exist. An existing capture is never overwritten; a
committed result with a missing capture cannot mint a replacement intent from
post-result balances. Replays validate the saved outcome time using PostgreSQL's
timestamp equality and require the complete original roster.

The test coordinator commits capture separately before its reward transaction.
The fixture verifies failed capture rollback, survival across reward rollback,
pre-result save protection, coherent cache/database drift rejection and the
existing result replay/reload checks. Log:
obj/chi-refill-validation/team-intent-tests.log.

Production capture/retry hooks remain absent. Startup recovery is described below.
A process failure before Capture commits still leaves no durable outcome to recover. This
is a recovery prerequisite, not proof of crash-safe runtime earning.

## Startup recovery of captured team outcomes

The inactive TeamArenaStartupRecovery.Recover entry settles one captured match
before sessions, disconnect barriers, either Arena cache, either match registry
or listeners exist. Unknown IDs with no evidence return without inventing a
result. Existing result/snapshot evidence without its captured header fails
closed. A private gate serializes calls within the process.

Recovery validates two admitted leaders, unique participants, a maximum of five
players per side, bounded fields and the captured before/after calculation using
the same policy helper as online completion. It locks saved owners in UID order,
requires exact team-counter and shared-point preimages, then writes the result,
all participant receipts and both point mirrors in one transaction. No player
objects or caches are projected. The startup guard is checked again before commit.

An already committed result must exactly match the captured header and every
participant receipt. Missing/changed evidence is an error, not permission to pay
again. A valid replay performs no writes and need not match current balances,
which may have changed through later legitimate activity.

The fixture performs actual import/outcome/capture, then swaps registries to
simulate startup. It tests live/loaded-state rejection, absent and orphan evidence,
receipt failure rollback, changed saved balances, inconsistent captured rewards,
concurrent recovery, complete-bundle replay and production team-row reload.
This is not a real process restart or native-client reconnect. Log:
obj/chi-refill-validation/team-recovery-tests.log.

Startup composition, coordinated pending-owner admission/spending guards,
fenced-session recovery and daily settlement remain open. Bounded enumeration
and preparation are described below. No production initialization, recovery hook or deployment is included.

## Bounded team startup preparation

Sweep accepts 1?128 pending captures, ordered by original completion time and
UUID, and runs the startup-only recovery transaction for each. Already completed
results are not selected again. Each successful recovery commits independently,
so a later failure does not discard earlier progress.

PrepareStartup accepts 1?100 passes, initializes the staged schema, drains up to
128 captures per pass and fails if pending work remains when its pass budget is
exhausted. It then audits every completed header against its capture and checks
both roster counts plus all receipt values. This historical consistency audit is
a database query; it does not load the whole history into process memory. Missing
or changed completed evidence prevents readiness even if no work is pending.
Prepared becomes true only after the audit and final startup ownership check.

The fixture covers invalid bounds, a one-match chronological sweep, failure after
earlier committed progress, successful retry, repeated preparation and an
incomplete historical bundle. It uses three actual imported/captured matches for
batch behavior, not a full 128-match load or a production timing benchmark. The
negative receipt tests restore their exact fixture snapshots afterward so later
preparation tests start from deliberate, consistent evidence. Log:
obj/chi-refill-validation/team-sweep-tests.log.

Composition still does not call this entry. Coordinated solo/team startup order,
pending-owner guards and daily settlement must be completed before activation.

## Pending team ownership guards

The staged team participant exposes a transaction-scoped pending check. A
captured owner remains pending until the complete result header and every
participant receipt match the captured identity, roles and before/after values.
An incomplete teammate receipt therefore keeps the leaders pending too. Capture
checks for another pending outcome after locking its entity rows, preventing an
overlapping captured result from replacing the first outcome's balance evidence.

The ordinary Arena point-purchase transaction and staged daily rollover both
check pending team ownership under their entity row lock. They defer without a
charge or mutation even if the in-memory team group reference has disappeared.
A complete result bundle releases this guard; current balance checks still apply
before any subsequent purchase or rollover. The query remains disabled until
team transaction initialization, which is still absent from live composition.

The fixture exercises the actual purchase and rollover entry points with a
pending member whose group reference is temporarily absent. Neither operation
charges, writes a receipt or fences the player. It also checks guard release after
completion and renewed pending status when another participant's result receipt
is missing. Log: obj/chi-refill-validation/team-pending-tests.log.

This does not yet guard every solo/team admission, result writer or shared-balance
service. Those integration points and coordinated day settlement remain required
before activation.

## Shared ownership between solo and team transactions

Staged solo ledger admission locks the two entity rows in UID order when the
team schema is initialized, then refuses admission while either participant has
a pending team outcome. Solo snapshot capture, result statistics and startup
recovery also reject a pending team owner under their entity locks. The result
transaction therefore cannot change shared points before the team outcome settles.

Conversely, team outcome capture checks the solo ledger after locking its owners.
An active, non-abandoned solo admission prevents a new team capture even if the
old in-memory group reference is absent. The solo pending query works before
abandonment recovery initialization and recognizes abandoned rows once that
schema is initialized. These database checks serialize with the captured-result
and point-purchase transactions through the same entity locks.

The fixture imports a solo pair for a pending team member and confirms the staged
ledger refuses to insert it. A second case creates an actual solo admission,
clears its group references, imports a team pair and confirms capture fails
without a team intent or point changes. Existing transaction/recovery fixtures
continue to pass. Log: obj/chi-refill-validation/arena-cross-mode-tests.log.

Pre-transfer pending checks are now described below. Coordinated durable
admission, deferred completion and runtime writer replacement remain required.

## Pending checks before map transfer

Once the staged team schema is initialized, both solo and team Import methods
enter ArenaAdmissionGuard before claiming their one-time import attempt or
allocating a map. The guard takes the match lock and canonical owner locks in UID
order with nonblocking acquisition, checks existing solo/team/spectator ownership,
and reads pending solo/team evidence while holding entity row locks. Rejection
leaves the players in place and does not consume the import attempt.

Database locks are released before teleport or socket sends, while in-process
owner locks remain held through import. This avoids holding a database row lock
across synchronous disconnect/save callbacks. Database provider failures defer
entry and log only the exception type. Neither lifecycle is enabled by this
change: the guard branch depends on team initialization, which production still
does not call.

The fixture now verifies that a pending team member's actual solo Import leaves
both players in town with no map, group ownership or ledger row. The reverse
case verifies that an active solo admission prevents actual team transfer and
that the unadmitted team cannot capture a result. Other ordinary imports continue
through the combined transaction/recovery suite. Log:
obj/chi-refill-validation/arena-admission-tests.log.

This is not durable reservation or complete scheduling/retry integration. It
serializes in-process import with owned writers; startup composition, atomic
admission lifecycle, partial-transfer recovery and native acceptance remain open.

## Staged team result coordinator

TeamArenaResultCoordinator.Complete now combines capture, result transaction and
post-commit publication for an ended, registered match with its owned map. It
uses nonblocking match/UID-ordered owner locks and requires unchanged canonical
participants with valid statistic objects. Busy and NotReady do not capture or
award. Capture commits separately; the result transaction commits the whole
roster before cache publication.

The group records durable result readiness after the database commit. That flag
suppresses its legacy reward projection during Export, preventing a second award
when admitted players return. It proves database completion, not cache readiness.
A committed replay validates the full receipt bundle and checks current saved
statistics against the actors' caches. If projection is stale, or a transaction
fails, every involved cache is invalidated and fenced for reload. Canonical
fenced actors still on the map keep cleanup retryable.

The fixture covers busy deferral before capture, concurrent completion, durable
result followed by real Export without double rewards, forced result rollback,
offline recovery of retained capture, and a committed result with deliberately
stale caches. The stale-cache case is a simulation, not a real socket failure or
reconnect. Log: obj/chi-refill-validation/team-coordinator-tests.log.

No live callback invokes this coordinator. Deferred disconnect, live
recovery/reload, completion scheduling and startup composition still need
coordinated integration. Staged outcome notification now follows successful
commit and projection, as described below.

## Recovery while original team owners remain fenced

The staged coordinator now exposes RecoverFenced. It acquires the registered
match and each original admitted owner's lock without waiting. Every actor must
still be the exact canonical session, own this group, be fully loaded, have
Action zero and a persistence-reload fence, and hold invalidated solo/team
statistic objects. Healthy, replaced, partially invalidated or unowned sessions
cannot use this recovery authority.

Under those guards, a failed initial capture may retry against unchanged saved
preimages, using the original outcome observation and frozen economy. A captured
outcome uses the same recovery transaction as startup, additionally checking the
entire original roster and outcome against the owned group. Recovery revalidates
fenced ownership before commit, marks durable readiness and leaves all actor
caches fenced and unprojected. It does not re-enable a player or release session
registrations. Cleanup remains held while canonical fenced actors occupy the
map; it can finish after their original registrations release.

The fixture covers healthy/replacement rejection, busy-owner deferral, a failed
reward transaction recovered once without cache publication, repeated recovery,
cleanup after simulated registration release, initial capture failure, changed
preimage rejection and successful retry of unchanged original evidence. The
registration release is simulated; this does not claim live disconnect/reconnect
integration. Log: obj/chi-refill-validation/team-fenced-tests.log.

All-roster fenced recovery is explicit and narrow. Mixed healthy/fenced rosters,
live retry scheduling and bounded shutdown/reload orchestration remain open.
A crash before capture commits still cannot recover an outcome from RAM alone.

## Remaining integration requirements

- Complete partial-admission and cancelled-invitation cleanup, including tests
  for failed leader teleports and all participant/spectator exit races.
- Persist match identity, admitted roster, outcome and economy decisions before
  atomic shared-point and team-counter writes. Recover pending earned results
  after disconnect/restart without awarding twice or losing an earned reward.
- Coordinate the roster-based export with the durable result, preserving
  disconnected/fenced recipients' entitlements until recovery completes.
- Validate ordinary teams through import, combat, timeout, forfeit, disband,
  disconnect and reconnect before runtime activation and native acceptance.

See ARENA_SAVE_OWNERSHIP.md for shared balance protections and the legacy
arithmetic/save-reload correction. No deployment or activation is included.

## Result dialogs after durable completion

When the staged team result schema is initialized, End freezes the outcome but
suppresses the old immediate victory/defeat dialogs. Complete attempts those
dialogs only after committing the result, checking replay projection, and
publishing statistics. Uninitialized live legacy behavior remains unchanged.

The notification method requires the group lock, a committed marker and the
original leader's owner lock. Each send rechecks the canonical session, group,
admitted role and cache validity; a prior send may synchronously disconnect a
player. A per-group set limits each leader to one send attempt. Failed capture,
rolled-back results and failed cache projection do not attempt dialogs. Fenced
recovery does not send gameplay results through invalidated caches.

Fixture evidence covers suppression before commit, original-leader attempt
ownership after concurrent completion, rollback and stale projection. These are
send-attempt assertions, not captured native packets or proof of client delivery.
No durable notification outbox or reconnect dialog replay is claimed. Startup,
completion scheduling, live departure integration, team settlement and client acceptance
remain unfinished; no live deployment occurred.

Validation: build and 517 combined checks pass. Ignored log:
`obj/chi-refill-validation/team-notify-tests.log`.

## Staged terminal completion entry points

EndForPlayer accepts only an original leader session; EndTimedOut applies the
existing damage winner and second-side tie rule after 180,000 elapsed ticks from
successful import, rather than matchmaking creation. Complete remains available
for already-frozen outcomes. All three use one transaction/projection path.

Before freezing a new outcome, the coordinator requires the registered group,
owned map, both admitted leaders, disjoint recorded sides, and canonical valid
statistics for every admitted actor. Group and owner locks are nonblocking and
owner locks are acquired by UID. Busy or replaced ownership returns without
freezing RAM state or capturing an intent. Once frozen, a failed database write
retains the captured outcome and fences the roster; existing fenced recovery can
settle that same result. Later terminal callbacks cannot reverse a committed
result or pay it twice. Result dialogs still follow successful projection.

The fixture uses ordinary admitted leaders and a member: foreign/replaced
sessions, busy member, concurrent completion, opposing later departure, deadline
boundary, tick wrap, damage/tie selection, forced receipt failure and fenced
recovery. This is coordinator coverage, not actual socket disconnect handling.
The first departure while Busy is now retained as described below. Callbacks and
the scheduler do not invoke these methods, and shutdown must still defer
canonical-session removal.
Team daily settlement and coordinated startup/cutover remain unfinished.

Validation: build and 528 combined checks pass. Ignored log:
`obj/chi-refill-validation/team-terminal-tests.log`.

## First departure retained across busy team completion

EndForPlayer records the first canonical admitted leader request and its UTC
observation time under a short separate gate, before attempting the match/actor
locks. It does not acquire those locks or access the database while holding the
gate. Later requests cannot replace it. RetryDeparture, Complete and timeout
completion use the retained request. FreezeOutcome shares the gate, so even an
existing legacy End callback cannot reverse that first accepted loss. OutcomeUtc
uses the original observation time, preserving its Pacific result day when a
busy retry crosses midnight. Damage updates also share the gate and stop after
a retained request rather than changing the terminal match's damage totals.

The retained request is in memory until normal capture succeeds; it is not a
crash-safe admission or departure journal. Player/session locks and canonical
ownership are revalidated before result capture. Scheduler wiring, disconnect
retention, mixed-owner recovery and durable admission remain required. A process
loss before capture still cannot reconstruct this request.

Validation holds the actual group lock on another task and submits both leaders'
requests, verifies the first remains and no intent is saved, then tries explicit
retry, timeout and legacy ending paths. All preserve the original losing leader
and prior Pacific day, with one result payment and stopped damage updates.
Existing member-lock, replaced-owner, rollback and fenced-recovery tests also run.
No native socket/client behavior or live deployment is claimed.

Validation: build and 546 combined checks pass. Ignored log:
`obj/chi-refill-validation/team-pending-tests.log`.

## Bounded staged team completion queue

TeamArenaCompletionPump is an instance owned by future lifecycle composition; no
live timer creates or ticks it. It tracks admitted groups by their immutable
result UUID, deduplicates repeated tracking, caps capacity at 4,096 and limits a
tick to at most 128 entries. Round-robin attempts skip busy group locks rather
than blocking another match. Healthy endings, retained departures, due timeouts
and fenced recovery use the existing coordinator. Database failures back off
from one to 30 seconds; busy/not-ready entries wait one second. A clock rollback
permits one fresh attempt and resets the retry baseline.

Cleanup now has a coordinator entry requiring the exact match's durable marker
and registration. Export still owns participant/spectator locks and returns
players before disposing the map. A saved result is not queue completion until
cleanup finishes: fenced canonical actors retain their map, and a later tick
can clean up after their registrations are released. Cleanup retries do not
repeat result transactions or legacy rewards. Capacity rejection never silently
removes ownership or abandons a match; the future caller must handle rejection.

The fixture exercises duplicate/capacity handling, a busy match lock, fairness,
reusable slots, delayed retries, failure attempt counts, increased backoff and
clock rollback. It then repairs a real failed team result, retains fenced
cleanup, calls session-registration release for the entire admitted roster and
verifies map cleanup. It also rejects an excessive tick budget. This bounds
queue work by attempt count, not database wall time; connection/command timeouts
still apply. Real disconnect orchestration, startup wiring, shutdown drain,
released-owner recovery and ordinary native-client acceptance remain open.

Validation: build and 559 combined checks pass. Ignored log:
`obj/chi-refill-validation/team-pump-tests.log`.

## Joint startup preparation for shared Arena points

ArenaStartupPreparation.Prepare is the staged composition entry point for both
Arena modes. It clears its readiness flag on every attempt, rejects listeners,
sessions, queued disconnects, groups or loaded statistics, initializes both
schemas and checks for overlapping unsettled solo/team ownership. That overlap
contradicts the capture ownership rules, so startup rejects it before applying
either outcome rather than guessing an ordering for conflicting shared points.

Team captured results recover and audit first, followed by solo recovery,
interrupted-match abandonment and historical ranking settlement. Readiness is
published only when both preparations succeed and registries remain empty. The
batch limit is 1..100 for each existing bounded recovery stage. This is not one
cross-mode database transaction: a late ranking failure preserves earlier
committed recoveries, keeps readiness false and permits an idempotent retry.

The fixture uses disjoint ordinary solo and team captured results in a disposable
schema. It rejects live registries and deliberately conflicting evidence, fails
ranking after both result recoveries, retries without duplicate points/receipts,
loads both results through production loaders and rejects a newly queued
disconnect after prior successful preparation. Empty-registry substitution
simulates cold startup; it is not a real process restart. Server.Run still does
not invoke this boundary. Admission, writer cutover, daily/team settlement,
disconnect/shutdown integration and native-client acceptance remain open.

Validation: build and 565 combined checks pass. Ignored log:
`obj/chi-refill-validation/joint-startup-tests.log`.

## Durable reservation before team transfer

Joint startup now initializes TeamArenaAdmissions. In that staged mode, the
existing admission guard commits the planned two-sided roster and original
leaders under actor/entity locks before ImportCore creates/transfers to a match
map. Failed reservation writes roll back before the import latch is consumed.
Repeated imports return before creating another reservation. Each side is
limited to five distinct eligible players. The live legacy startup does not
initialize this schema, so this is not a deployed migration.

Admission receipts are planned-roster evidence, not proof that every teleport
succeeded. Actual admitted participants still determine rewards. Capture checks
that those participants and leaders belong to their reserved sides; startup
recovery checks captured sides against the reservation too. Older captured
results without reservation receipts remain recoverable migration evidence.
An in-process group marked as reserved cannot capture if its receipt disappears.

Outstanding reservations participate in the same pending-ownership guard used
by solo/team admission, point purchases, rollover and result capture. They also
protect managed statistics from legacy balance resets/saves. A committed result
releases the reservation as part of the existing atomic result transaction.
Failed-transfer members receive no result award but remain reserved until the
match settles or startup closes it.

Cold startup records bounded abandonment receipts for reservations without a
captured outcome or committed result. It invents no winner, counters or rewards.
Captured outcomes remain for recovery. A stale actor cannot later award an
abandoned reservation. Startup replay leaves both the abandonment and paid
results unchanged. Interrupted-transfer cancellation and owned cleanup are implemented below.
Live disconnect/shutdown wiring and client tests remain open.

Fixtures force reservation-roster insertion failure and exercise actual import,
retry, both leaders plus a member, repeated import, result release, cold
no-outcome abandonment, stale-actor rejection and captured-result recovery.
Cold startup uses substituted empty registries, not an actual process crash.

Validation: build and 576 combined checks pass. Ignored log:
`obj/chi-refill-validation/team-admission-tests.log`.

## Interrupted transfer cancellation and owned cleanup

Reserved imports retain their original planned sessions. ImportReserved contains
transfer/setup exceptions and rechecks every planned actor after setup. A failed
transfer or invalid actor marks the admission interrupted, disables combat and
attempts durable cancellation plus cleanup. No partial team fight is started
with a missing planned participant. Original PK mode is recorded before transfer
so cleanup can restore it even when setup stops before the normal mode switch.

CancelInterrupted requires the exact registered group, its saved reservation,
interrupted/unready state and original actor locks. It locks the entity rows,
verifies saved leaders and the complete reserved UID roster, rejects any captured
or committed outcome, and saves the abandonment receipt transactionally. Only
then does it mark cancellation ready for cleanup. Failure preserves the
reservation for retry. It does not fence healthy caches or invent a result.

Export uses the reserved roster for cancelled admissions and skips legacy reward
code. It returns owned actors, clears their invitations/queues and disposes the
map last, preserving replaced sessions and later match ownership. Busy or fenced
cleanup remains pending. The completion queue can track an interrupted import
even if no dynamic map was created, retry cancellation and remove the entry only
after cleanup. Cancellation and cleanup replays do not award points or counters.

The fixture induces a real map-creation exception after reservation. A second
fixture simulates partial transfer after real import, including one leader back
in town, and checks a busy owner, failed cancellation receipt, queue-driven
retry, player return, map release, unchanged balances/counters and replay. That
simulation is not proof of native teleport/socket interruption behavior. Live
composition, disconnect/shutdown integration and client acceptance remain open.

Validation: build and 583 combined checks pass. Ignored log:
`obj/chi-refill-validation/team-cancel-tests.log`.

## Recovery with mixed healthy and fenced original sessions

RecoverFenced now accepts a mixture of properly fenced and healthy original
admitted sessions, provided at least one is fenced. It first locks the entire
roster and verifies that every actor is either fully fenced with both invalidated
statistic objects or a canonical healthy owner of the same group. Missing,
replaced, partially invalid or busy ownership is rejected/deferred without
invalidating additional healthy caches. All-healthy groups still use Complete.

For an uncommitted match, the remaining healthy caches are invalidated before
capture/recovery. The repair therefore runs against one uniformly fenced roster,
validates saved preimages and preserves reload requirements for every actor.
It never publishes new balances through mixed cache states. An already-durable
match does not invalidate healthy caches merely because another member needs
reload. Cleanup still waits for the affected canonical sessions to be released.

The fixture starts with one invalidated participant and healthy teammates,
checks replacement and busy rejection, then exercises queue-driven repair and
post-registration-release cleanup. Production loaders verify each participant's
saved battle and mirrored points. A second case changes a saved point preimage:
recovery refuses to overwrite it or partially pay; restoring fixture evidence
allows retry, with old caches still fenced. This does not authorize replacing
or reconstructing missing live actors. Real disconnect/shutdown retention,
released-owner recovery, lifecycle activation and client acceptance remain open.

Validation: build and 592 combined checks pass. Ignored log:
`obj/chi-refill-validation/team-mixed-tests.log`.

## Staged observation before team disconnect teardown

GameState.ShutDown currently saves and releases canonical registration before
calling TeamQualifierGroup.CheckEnd. That legacy order cannot serve durable
whole-roster completion. ObserveDisconnect is the staged entry point intended
before teardown; it does not yet replace the socket/shutdown callback.

The group records exact admitted departing sessions under its short departure
gate. An original leader departure ends that side; a member departure only ends
the side when no unrecorded living admitted teammate remains. Earlier retained
disconnected actors are excluded from that decision, damage scoring and the
normal all-dead combat check. Mutable Team membership cannot change their side.
The first terminal observation records the original losing leader before trying
match locks, preserving it against a later opposing departure.

Retained is an explicit coordinator outcome: the nonterminal member's canonical
session must stay available for the eventual roster transaction. Busy likewise
requires deferral, not permission to release the actor. A terminal request uses
the existing completion and retry paths; all original admitted actors retain
the established result-counter policy. The method does not by itself stop socket
teardown or reserve queue capacity.

Tests use a four-player admitted roster, foreign/forged references, repeated
member observation, scoring after departure, a dead leader plus sequential member
departures, a busy group and opposing later departure. A separate combat check
verifies that a retained departed member cannot keep a dead side alive. These
are staged observer calls, not real socket disconnect tests. Runtime work still
must retain canonical actors and reconnect barriers, schedule bounded completion,
resume the one-shot shutdown only when durable ownership permits it, and validate
native disconnect/reconnect and service shutdown behavior.

Validation: build and 599 combined checks pass. Ignored log:
`obj/chi-refill-validation/team-disconnect-tests.log`.

## Staged team shutdown retention at the one-shot boundary

TeamArenaDisconnectRetention owns explicitly attached admitted groups and their
original sessions. Attach locks and validates the complete roster, rejects
conflicting owners, and reserves capacity in its bounded completion pump before
installing per-session shutdown ownership. Runtime startup/import does not yet
create or attach this service; no live instance has been activated.

GameState.TryBeginShutdown now holds the item/owner lock and consults that owner
before consuming the one-shot flag. A pending team result retains the canonical
session, records its departure and establishes the existing DisconnectPool login
barrier. Arena admission rejects a consumed shutdown or disconnect barrier. This
keeps disconnect and subsequent admission from competing over the same actor.
Unattached sessions retain the ordinary one-shot behavior.

The service ticks result recovery/cleanup and a bounded round-robin retention
queue. It resumes requested shutdown only after durable result/cancellation;
healthy unrequested teammates stay online. Invalidated teammates are sent through
normal Disconnect after recovery. Resume and detachment occur under the actor
lock, re-reading whether shutdown was requested. A callback that returns without
starting teardown and leaves the original canonical registration is retained for
retry. Original-session release cannot remove a newer registration. The service
stays scheduled until map cleanup finishes even when teardown has already resumed.

The fixture calls the actual TryBeginShutdown gate on ordinary admitted actors,
checking nonterminal member retention, busy terminal loss, one-shot preservation,
reconnect barriers, failed-result recovery and healthy-peer preservation. Its
resume delegate consumes the actual gate and releases registrations; it does not
exercise full network teardown or ancillary logout services. A no-op callback is
also retried. Default runtime callbacks call ResumeArenaShutdown for an observed
request and Disconnect for an invalidated peer.

Composition must still attach before disconnect can race with match entry,
handle attachment-capacity failure, own recurring ticks and bounded shutdown
drain, and coordinate solo Arena retention and legacy writer cutover. The service
is not ready for live activation until these paths and native disconnect/reconnect
are validated. This is a staged team teardown bridge, not completed server
shutdown or a deployed Arena lifecycle.

Validation: build and 610 combined checks pass. Ignored log:
`obj/chi-refill-validation/team-retention-tests.log`. An interrupted fixture run
left its owned disposable schema; after verifying its process was absent, the
harness cleanup command removed that schema before the successful rerun.

## Fenced recovery before a retained team departure is finalized

The team completion pump now routes cache failures to recovery before as well as
after terminal freeze. Recovery of an active match requires a retained terminal
departure from an original leader, a UTC observation, complete admission and
the registered match/map. It locks and validates the whole original roster
before freezing that loss. Missing or replaced owners cannot authorize an
outcome or invalidate the healthy peers.

After freezing, the existing whole-roster fenced transaction captures the
original observation and repairs statistics/point mirrors atomically. Failure
retains the frozen result and fenced roster for retry; settlement does not
re-enable stale sessions. Map cleanup waits for the original registrations to
release. A cache failure with no terminal departure leaves an active match
without a fabricated result.

The fixture uses actual leader-disconnect observation while a roster lock is
busy, then invalidates one participant. It checks replacement rejection, failed
result insertion, bounded pump retry, one result, every participant's ordinary
statistics reload and final map cleanup. This is staged component coverage;
automatic retention attachment, live writer cutover, shutdown drain and native
disconnect/reconnect acceptance remain open.

Validation: isolated build and 647 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/team-pending-build.log` and
`obj/chi-refill-validation/team-pending-tests.log`.

## Interrupted-admission startup boundary

Joint startup preparation now checks for remaining interrupted reservations after
its bounded closure loop. A reservation counts as interrupted only when it has
neither a captured intent, a durable result nor an abandonment receipt, matching
the sweep's selection. The probe is read-only and uses EXISTS; it cannot close
another batch beyond the configured write budget.

Previously a full final batch always threw, including exactly 128 closures with
nothing left. Conversely, fewer affected rows caused the loop to stop without
checking whether a suppressed insert had left work pending. The new postcondition
allows the exact completed boundary and keeps Prepared false when any interrupted
reservation remains. Independently committed closures survive failure and are
recognized on retry.

The isolated fixture exercises 128 rows, 129 rows and a trigger-suppressed
single-row closure through the actual joint Prepare entry point with one allowed
batch. It checks remaining reservations, readiness and successful retry. This
does not activate joint startup in Server.Run or claim a live Host restart.

Validation: isolated build and 671 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-startup-boundary-build.log` and
`obj/chi-refill-validation/arena-startup-boundary-tests.log`.

## Owned team entry

`TeamArenaDisconnectRetention.Import` requires prepared joint startup and the
team admission/result stores. The group selects one eligible roster snapshot;
the retention owner holds that match and its actors, reserves bounded capacity,
and installs every member's shutdown owner before the same roster is reserved
and transferred. A full owner rejects another match before reservation or transfer.
The legacy Import entry point remains separate from this staged composition path.

During preparation, a requested departure holds the original registration and
reconnect barrier without consuming the one-shot shutdown gate. Admission checks
reject pre-reservation departures; post-transfer checks cancel interrupted
admission if any selected player began shutdown or acquired a disconnect barrier.
The pump retries durable cancellation and cleanup before releasing retained
shutdown. Successful entry hands off to normal team result settlement. The caller
must tick the same instance even after a false Import result, because failed entry
can still require recovery. Unimported rejected invitations remain matchmaker-owned.

Fixtures cover all three ordinary roster members, capacity rejection, contended
disconnect settlement, healthy opponent preservation, reentrant disconnect during
reservation, and actual map-creation failure followed by durable cancellation.
They do not simulate native transport callbacks during each transfer or full
ancillary session teardown. Live startup/matchmaker scheduling, coordinated
shutdown and native acceptance remain open; this source path is not deployed.

Validation: isolated build and 726 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/team-owned-entry-build.log` and
`obj/chi-refill-validation/team-owned-entry-tests.log`.
