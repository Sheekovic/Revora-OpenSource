# Owned Arena scheduling

## Breaking Touch caster and timer ownership

`GameState.BreakTouch` now holds the original Team Arena combat lease through
its existing stack/burst application and records the caster/match after a
successful proc. Invalid or busy Arena ownership rejects the application before
stack changes. The buffer callback routes its percentage timer through
`TeamArenaBreakingTouch`, preventing fallthrough into unowned Arena HP writes.

The two-second timer revalidates the original caster and match, defers busy
actors without consuming duration, and scores only actual nonlethal HP loss.
Detoxification reduces the percentage; Azure Shield absorbs damage before HP.
Expired matches, departed casters and carried-in unowned effects cannot continue
earning damage. The existing proc chance, stack percentages and tick count are
unchanged. These are Revora ownership/scoring rules, not newly verified retail
balance. Effect state is not persisted across sessions.

The focused fixture installs the post-proc state through the ownership recorder
and exercises actual timer health/score mutation, cadence, full/partial shields,
busy retry, caster departure, carried-in effects, detoxification, one-HP targets
and match expiry. It does not simulate native spell input or certify the random
proc/burst chain. Shared timer ownership is addressed below; other direct health
writes, joint live startup and native presentation remain open.

### Removed-stack timer guard

The Arena recorder now requires a remaining `lianhuaran` stack. The legacy
fourth-stack burst removes all four flags but then sets the timer count to 20;
recording that state previously restarted percentage damage using the previous
stack's cached value. A post-burst record now clears the Arena timer, and each
owned tick cancels if all stack flags have disappeared. Tests install an actual
stack flag and cover removal, post-burst recording and a subsequent new stack.

The removed-stack guard alone did not arbitrate Searing Touch's `FlameStamp` /
`FlameTime` expiry against Breaking Touch's separate countdown. The following
shared-owner change addresses that conflict without claiming combat parity.

The follow-up isolated build and Arena suite pass 1,056 checks, including three
additional removed-stack/post-burst assertions. Logs:
`obj/chi-refill-validation/breaking-stack-build.log` and
`obj/chi-refill-validation/breaking-stack-tests.log`.

Validation: isolated build and 1,053 Arena daily/prerequisite checks passed,
including 13 new Breaking Touch assertions. Logs are ignored under
`obj/chi-refill-validation/breaking-touch-build.log` and
`obj/chi-refill-validation/breaking-touch-daily-tests.log`.

## Shared flame timer ownership

The character callback now expires shared flags through an actor-locked helper.
It leaves an owned Breaking Touch effect to its own countdown instead of erasing
it using an unrelated/default Searing Touch timestamp. A new owned Breaking
Touch clears the old flame caster/match; nearby flame pulses also reject a stack
currently owned by Breaking Touch. A new Searing Touch application cancels the
old percentage timer and records its own caster/match.

Searing Touch's full flag transition now holds the combat lease, including
removals. A busy or invalid Arena application cannot partially remove a prior
stack or award proc experience. Existing stack progression, proc chances,
percentages and duration inputs remain unchanged. The latest successful
application owns the shared effect state; this is an explicit Revora consistency
rule, not newly verified retail skill parity. The separate legacy stack
progression and combined spell/burst behavior still need native acceptance.

Focused additions exercise unrelated expiry, stale nearby pulses, both ownership
transitions, a busy caster before flag mutation, cancelled percentage damage and
flame expiry. They call the actual application/expiry/timer helpers with ordinary
Arena actors, rather than reproducing their mutations in the fixture. Native
spell input, proc distribution and visual ordering remain unverified.

Isolated build and 1,063 Arena daily/prerequisite checks pass, including seven
new shared-owner assertions. Validation logs:
`obj/chi-refill-validation/arena-shared-flame-build.log` and
`obj/chi-refill-validation/arena-shared-flame-tests.log`.

## Searing Touch nearby pulse

The four existing Searing Touch stack applications now record the caster's
original Team Arena match. Its character-timer pulse routes Arena participants
through an owned helper rather than the direct legacy HP subtraction. The helper
owns caster/target combat plus the carrier actor, checks the original match,
living carrier, fourth-stage flag, duration and ten-tile radius, and retains
ordinary attack permission. It credits the actual nonlethal HP reduction to the
caster's admitted side. The existing per-callback pulse frequency is unchanged;
this is not a new retail cadence claim.

Missing/stale caster or carrier ownership, expired effects and busy actor locks
cannot fall through to legacy Arena damage. Non-Arena pulses retain their old
behavior, with a missing-caster null check. Fixtures cover HP/score agreement,
one-HP targets, busy carrier, radius, carrier departure, missing caster, target
attackability and effect expiry. Native spell/flag presentation remains open.
That attackability fixture exposed a legacy Team Arena early return before the
normal target check. It now uses the already-validated opposing roster and checks
target attackability, without dereferencing a possibly removed ordinary team.

The direct-timer audit also identified an active separate Breaking Touch path:
`GameState.BreakTouch(client)` installs percentage/duration state. Its original
unowned buffer subtraction is now covered by the separate Breaking Touch helper
above; it is not part of Toxic Fog or this nearby pulse's validation scope.
Both legacy effects also share the four `lianhuaran` flags; their stack/refresh
interaction still requires a unified original-effect audit.
Regeneration and other direct HP writes also remain outside the shared receiver.
Build and 1,040 combined Arena checks pass, including eleven pulse assertions.

## Toxic Fog caster and timer ownership

All eight existing Toxic Fog spell-application branches now share one application
helper. An Arena application acquires combat ownership and records its original
caster and match on the target; ordinary non-Arena effects retain the existing
duration/percentage/flag behavior. The two weapon-poison proc branches already
exclude base map 700 and are not enabled by this change.

The buffer callback routes Arena poison to an owned two-second tick. It rechecks
the original match/caster under the combat lease, credits actual HP removed to
that caster's admitted side, and consumes Azure Shield before HP. Poison remains
nonlethal. Busy locks defer the tick without consuming duration; expired matches,
departed/replaced casters and effects without original Arena ownership cannot
continue. Carried-in poison without a matching caster record is cleared rather
than attributed to an arbitrary opponent. These are Revora's ownership and
scoring rules, not a new claim about retail damage formulas.

Fixtures cover non-leader attribution, cadence/replay, complete and partial shield
absorption, busy-caster retry, lost match ownership, carried-in effects and the
deadline. Native spell-input/animation acceptance and other direct timer health
writes remain open. No poison state is persisted across a client/session restart.
Build and 1,029 combined Arena checks pass, including eleven poison assertions.

## Shared damage receiver ownership

`ReceiveAttack(Entity, Entity, ...)` now obtains a Team Arena combat lease before
entering its existing damage implementation. For an Arena pair it acquires the
match gate, both actor locks in UID order, and the departure gate using nonblocking
attempts, then revalidates active opposing admitted owners. The lease spans health,
score and synchronous death/result callbacks. Invalid or busy ownership yields
zero damage. All acquired gates release on return or exception; non-Arena calls
continue through the existing implementation.

Fixtures invoke the actual receiver without first calling attack permission.
They check preparation rejection, busy target rejection, a valid hit's matching
health/score changes, lethal-hit roster settlement, scheduler cleanup and rejection
after deadline or outcome freeze. This closes
the shared receiver gap; it does not cover every direct health write. In particular,
`Thread.cs` poison ticks mutate HP directly and currently attribute qualifier
damage via `ArenaStatistic.PlayWith`, which does not identify a Team Arena poison
caster. Caster ownership and direct timer damage remain required follow-up work.
Native combat/reconnect acceptance and live activation remain open.
Build and 1,018 combined Arena checks pass, including seven receiver assertions.

## Combat permission from the admitted roster

Shared entity attack permission now requires both Team Arena owners to belong to
opposite sides of the same original admitted roster. It verifies current living
sessions, exact group/map registration, preparation, deadline and active outcome
under match/departure ownership. Busy match ownership declines the attack instead
of blocking on a result transaction. Departed participants cannot attack or be
targets while retained for persistence. Full PK mode or ordinary team removal
cannot turn an originally admitted teammate into an opponent.

The existing shared permission checks still run after this Arena guard; it does
not grant attacks that other combat rules reject. Fixtures exercise admitted
opponents, friendly fire after team removal, busy ownership, forged group
references, disconnected owners with surviving teammates, the deadline and a
completed result before cleanup, including wrapped clocks. Permission checking
alone does not serialize health mutation with settlement. Delayed effects that
bypass this check, full combat mutation ownership and native acceptance remain
open; no protocol layout or class-damage formula is changed.
Build and 1,011 combined Arena checks pass, including 14 additional combat-access
assertions.

## Ten-second Team Arena preparation

The main attack handler and shared entity attack permission now use the same
ten-second Team Arena preparation predicate. It refuses combat until admission
is ready and measures elapsed time from import using wrap-safe subtraction. The
shared check protects both the attacker and target, including attack callers
outside the main player-input branch. Solo and Champion preparation logic is
unchanged.

This implements the preparation duration stated in the
[official Team Qualifier guide](https://co.99.com/guide/quests/teamqualifier.shtml).
Fixtures exercise the real shared attack check at entry, at 9,999 milliseconds
and at exactly 10,000 milliseconds with normal and wrapped ticks, plus target
protection when the attacker has no team-match reference. Native countdown
presentation, delayed effects that bypass shared attack permission and the
five-second postmatch return gate remain separate work. No new packet layout is
inferred or claimed verified.
Build and 997 combined Arena checks pass, including eight preparation assertions.

## Five-minute Team Arena duration

Team Arena uses one five-minute elapsed-time predicate measured from successful
import. Normal coordinated timeout, fenced recovery without a retained departure,
and the legacy scheduler all use it. The legacy scheduler previously used group
creation time, which could shorten a fight after invitation/entry delay. Solo
Arena timing is unchanged. Explicit departures still take precedence over timeout.

The [official Team Qualifier guide](https://co.99.com/guide/quests/teamqualifier.shtml)
documents five-minute matches. Tests cover a match remaining active at the old
three-minute cutoff, one millisecond before five minutes, exact-deadline damage
and tie results, older creation time, counter wrap and fenced recovery. Native
countdown, ten-second preparation and five-second return presentation still need
separate verification; this change does not claim those timing behaviors complete.
Build and 989 combined Arena checks pass.

## Leader disconnect with surviving teammates

Owned Team Arena disconnect observation now treats the original leader like any
other admitted contestant: a side loses when no living, present, non-departed
participant remains. Losing the leader's connection alone does not forfeit the
remaining member's fight. The existing shutdown owner retains the disconnected
leader's canonical actor and login barrier until the complete roster result is
durable; departed actors cannot contribute damage.

The [official Team Qualifier rules](https://co.99.com/guide/quests/teamqualifier.shtml),
rechecked 2026-09-21, explicitly describe defeat when all contestants on a side
are knocked out or disconnected. This supports the survival rule; it does not
verify every historical 6711 behavior. Explicit original-leader Quit/Give Up and
team-removal forfeits remain Revora's existing policy, and the legacy unowned
branch is outside this staged cutover.

Fixtures retain the leader through actual shutdown gating, let the surviving
member win or die through the combat callback, verify saved results for the whole
roster and resume only the requested teardown. Existing failure/recovery fixtures
that require terminal leader departure now explicitly make that leader the last
living contestant. Native disconnect/reconnect acceptance remains open.
Build and 987 combined Arena checks pass, including ten leader-survival assertions.

## Team removal commands

After packet 1023 successfully removes a player through Exit Team, Kick or the
existing Dismiss branch, a prepared owned Team Arena routes that original player
through its Quit handler. Members use the retained return request; original match
leaders use the existing forfeit coordinator. The mutable team's newly selected
leader does not become the match leader. Exact admitted reward ownership remains
until the coordinated result is saved and cleanup completes.

The notification is deliberately at the player-command boundary. `Team.AddLider`
internally removes/re-adds remaining teammates while updating ordinary leadership;
those bookkeeping operations must not count as Arena departures. This does not
change Dismiss into full disband: its current handler removes the caller and may
transfer ordinary leadership. A replay that no longer removes a player cannot
notify another departure.

Fixtures exercise member exit, leader kick, leader exit and Dismiss through the
actual packet handler, with opposing result ownership busy, command replay,
leadership transfer and one persisted result for every admitted participant.
Build and 977 combined Arena checks pass, including 18 removal assertions.
Direct non-packet removals, full concurrent team mutation ownership, invitation
responses and native/live acceptance remain open. No reward policy changes.

## Voluntary member return

Prepared Team Arena Quit/Give Up now queues a non-leader's return through the
existing bounded withdrawal service. Under match and actor ownership, it records
the participant's departure, returns the character using the normal arena return
path and restores combat state. Busy ownership or failed return leaves the request
retryable. Repeated successful requests do not resurrect or teleport the member
again. The admitted roster, group reference and shutdown retention remain until
the whole-roster result commits and cleanup completes; no immediate reward is
issued. Existing damage and signup guards exclude the returned participant.

This follows Revora's retained-member disconnect semantics: surviving teammates
continue; the last living participant's departure records the side's defeat.
The [official guide](https://co.99.com/guide/quests/teamqualifier.shtml), inspected
2026-09-21, documents all contestants knocked out/disconnected as a loss but does
not specify voluntary mid-match member return. That detail is an explicit Revora
policy, not verified historical 6711 parity. Existing leader forfeit behavior is
unchanged. Team removal/kick/disband integration, admission-time member responses,
complete mutation concurrency and native/live validation remain open.

Fixtures cover Quit/Give Up with both surviving and defeated sides, busy actor
ownership, duplicate requests, return with retained roster, damage/re-entry
exclusion and one saved member result through actual scheduler cleanup.
Build and 959 combined Arena checks pass. The last-living-member fixture exposed
the normal teleport's combat-end callback; already-recorded departed members now
skip that duplicate callback so the retained result is drained by the scheduler.

## Team command authority prerequisite

The departure audit found that packet 1023 allowed a non-leader to kick teammates
or change team loot/join settings. The handler now requires a current ordinary
player and, for administrative team actions, the exact leader and member entry.
The requested target UID never supplies authority. Teamless dismiss and truncated
requests return without mutation. Leaders retain the existing commands; members
retain voluntary exit. These are player team controls, not GM-only services.

Fixtures exercise all eight restricted actions as a non-leader, six settings as
the leader, permitted kick/exit/dismiss, forged targets and stale/same-UID
replacement sessions through the actual packet handler. No reward policy or
admitted roster is changed. Full team mutation concurrency, non-leader Arena
departure settlement and native/live validation remain open.

### Team entry identity checks

Packet 1023 acceptance now requires the actual current team leader and that
leader's exact membership entry. A same-UID replacement cannot inherit the
previous session's authority. Missing, dead or unavailable applicants are
rejected before dereferencing or joining them. Invitation acceptance cannot name
an ordinary member as its leader. Create Team cannot overwrite an existing
membership, leaving the former team's roster behind.

Ordinary packet-handler fixtures cover missing applicants, a member supplied as
the leader, dead applicants, replacement sessions, repeated creation and both
successful acceptance paths with replay. This is an identity prerequisite for
team event admission. Invitation-consent tracking is added below; serialization
of all concurrent ordinary-team mutations remains separate work.

Build and 1,070 combined Arena checks pass, including seven new team-entry
assertions. Validation logs: `obj/chi-refill-validation/team-entry-owner-build.log` and
`obj/chi-refill-validation/team-entry-owner-tests.log`. Native team UI and live
Arena activation remain pending.

### Team invitation and application consent

Ordinary team acceptance requires the corresponding invitation or join request.
Each applicant session has two transient slots in a weak-key table: one invitation
and one application. A slot records the exact original leader, team object and
issuance time. It expires after 60 seconds using wrap-safe elapsed time. This is
an explicit Revora consent policy, not a claim about original 6711 timeout parity.
Requests do not persist across sessions and cannot grow a global UID backlog.

The two directions cannot consume each other's consent. Acceptance revalidates
the current living sessions, original team, leadership, join setting and capacity
while holding the applicant and leader actor locks without waiting. Successful
membership consumes both slots. Busy ownership leaves consent retryable; replay
after leaving cannot restore membership. A same-UID replacement or a newly
created team cannot inherit the original request.

Packet-handler fixtures exercise both normal consent/acceptance flows, unsolicited
acceptance, consumed-request replay, wrong-direction acceptance, wrapped expiry,
busy retries, concurrent acceptance and a replacement team. These checks do not
certify native invitation popups or fully serialize legacy non-packet Team.Add,
removal and leadership-transfer paths. Those lifecycle gaps remain open.
Build and 1,077 combined Arena checks pass, including seven new consent
assertions. Logs: `obj/chi-refill-validation/team-consent-build.log` and
`obj/chi-refill-validation/team-consent-tests.log`.

### Core team membership ownership

Core `Team.Add` and `Team.Remove` now serialize the affected actor and roster
mutation. Addition rejects another team's owner, duplicate membership and full
capacity before changing the player reference. Removal requires the original
session and membership entry, so a stale same-UID actor cannot remove its
replacement. Disposing an old membership only clears that membership's own team
reference under the actor lock. Clan-sharing refresh runs outside the roster
mutation lock.

Leadership transfer now updates the remaining leader flags and broadcasts the
leader identity directly. It preserves surviving membership objects instead of
removing and re-adding everyone. Lowest-level metadata is recalculated after
removal; the previous code compared a member UID against the team UID and scanned
before removing the departing member. Team construction refuses existing owners,
and both ordinary creation entry points serialize their check and creation.

Concurrent fixtures cover competing team additions, repeated additions/removals,
same-UID stale removal, disposal after joining another team, capacity and stable
survivor membership through leadership transfer. These are core ownership checks,
not certification of every packet-command authorization interleaving or direct
legacy caller. Native leadership/roster presentation and event activation remain
open. The final build and 1,084 combined Arena checks pass, including seven new
core-membership assertions. Logs: `obj/chi-refill-validation/team-membership-build.log` and
`obj/chi-refill-validation/team-membership-tests.log`.

### Packet team mutation ownership

Kick, dismiss, exit and join/loot settings now acquire a command lease before
their handler runs. It takes the affected actor locks in UID order and the team
roster lock without waiting, then revalidates the sender's current session,
original membership and required leadership. Kick also holds the exact original
target's membership. The lease spans authorization and mutation, preventing a
leadership change between those two steps.

Busy commands make no partial changes and may be retried. A retry rechecks
authority; it cannot use leadership lost in the meantime. Exiting members retain
their own leave permission. The existing Arena removal notification remains in
the owned command path; roster/results still follow the event's retained-owner
rules. Direct legacy callers and native UI acceptance are separate concerns.

Fixtures cover a busy target, busy roster, settings retry, leadership loss before
retry, and concurrent authorized kick/voluntary exit through the actual packet
handler. Logs: `obj/chi-refill-validation/team-command-owner-build.log` and
`obj/chi-refill-validation/team-command-owner-tests.log`.

The final build and 1,089 combined Arena checks pass, including five new command
interleaving assertions. A separate 46-check Couple PK run also passes with the
same build, including ordinary spouse reward reload, exact bound soul ownership
and no duplicate payment across recovery/replay. Its log is
`obj/chi-refill-validation/team-command-couple-tests.log`. These component checks
do not close native team/event acceptance or deployment.

## Withdrawal during admission

Pending signup withdrawals now participate in the owned admission guard's
checks around database reservation. Team roster preparation checks them before
and after daily reset. A request noticed after reservation commits stops transfer
setup and uses the existing interrupted-admission cancellation/cleanup path.
Team transfer completion also checks pending requests before declaring admission
ready.

Registered solo participation checks pending signup withdrawal before writing
and before committing its earning transaction. An expected withdrawal returns
without commit; it does not mark a healthy cache invalid. Import then clears the
uncommitted fight-start marker and cancels its reservation, returning transferred
players through the existing cleanup path. A completed admission still uses the
owned forfeit coordinator for subsequent departures.

Fixtures hold real PostgreSQL locks while owned import runs on another thread.
They enqueue withdrawal with actor locks held, then release the blocker and
drain the normal schedulers. The late cases inspect `pg_locks` to establish a
waiting writer on the team reservation or solo participation table. Assertions
distinguish a rolled-back reservation from a committed reservation later cancelled
after solo transfer, and require no participation/result rewards or cache fences.

A real write failure concurrent with withdrawal keeps fenced map ownership for
the existing entry-recovery service. It must not take the healthy cancellation
shortcut. A separate fixture forces that overlap, recovers a cancelled
reservation, then explicitly releases the transportless original sessions and
drains map/withdrawal cleanup without point changes.

These controlled server/database interleavings do not prove every packet,
transfer, disconnect or native-client timing. Full live/native acceptance and
production activation remain open. No deployment or production schema change
is included.

Validation: isolated build and 915 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/withdrawal-admission-build.log` and
`obj/chi-refill-validation/withdrawal-admission-tests.log`.

## Queued signup withdrawals

Prepared solo and team Quit/Give Up callbacks now retain signup withdrawal
requests before changing invitation or queue state. Requests belong to the
original session reference, with a maximum of 1,024 pending sessions per mode.
The FIFO scheduler attempts at most sixteen requests per pass and retries busy
actors. Metadata locks never enclose actor locks, database work or packet sends.

Pending withdrawals block signup, queue pairing, accepted-invitation processing
and the prepared admission handoff. They wait for any unfinished rejection group
before permitting another signup. Cleanup removes queue entries by exact actor
reference and changes only that mode's signup state. A disconnected/replaced
session cannot remove the replacement's queue registration.

Callbacks already targeting an owned fight retain the existing coordinator
entry point and its first-observation timestamp. If a queued signup withdrawal
later encounters an owned match, it requests that match's forfeit rather than
clearing match references. Non-leader team departure remains a separate unfinished
feature. The ordinary unprepared legacy path is unchanged.

Fixtures exercise both public Quit and Give Up handlers for both modes, busy
requester/opponent locks, duplicate requests, re-signup/matchmaking exclusion,
eventual invitation cleanup and unpaired queue withdrawal. Existing owned fight
callback regressions run with these changes. Orphaned invitations preserve the
peer's newer opponent, and delayed old-session withdrawals preserve replacement
queue registrations. They do not establish every live
admission/withdrawal interleaving, full transport shutdown or native UI acceptance.
Production activation remains open; no deployment or schema change is included.

Validation: isolated build and 894 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-withdrawal-build.log` and
`obj/chi-refill-validation/arena-withdrawal-tests.log`.

## Unstarted result callbacks

After joint startup preparation, solo/team `Win` callbacks still delegate owned
matches to their durable result coordinators. Without an owned match, they now
use invitation rejection and return before the legacy reward path. In particular,
an expired solo invitation no longer awards fight points or daily counters to
the other player without an imported match.

Rejection requires the original reciprocal invitation. Under a short per-mode
gate it reuses one pending unimported group for that pair. It stops creating
cancellation records when that mode already has 1,024 owned unimported groups.
Busy actor locks leave the group
for the existing rejection scheduler. Repeated callbacks do not create duplicate
cleanup groups or reward evidence. Cleanup remains responsible for exact actor
and queue ownership and preserves newer opposing references.

Fixtures use ordinary expired invitations through both public waiting loops,
plus concurrent direct result callbacks while one actor lock is held. They
verify eventual queue cleanup, no teleport, unchanged persisted points and no
participation/result receipts or daily counters. They do not establish complete
quit/refusal callback ownership, cancellation racing accepted admission, every
requeue race or native-client acceptance. Those lifecycle cases and production
activation remain open. No deployment or production schema change is included.

Validation: isolated build and 872 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/unstarted-arena-build.log` and
`obj/chi-refill-validation/unstarted-arena-tests.log`.

The existing solo and team `CheckGroups` scheduler entry points now tick entry
owners registered by their staged `Import` methods. Each mode uses a queue of
owners independent of the match dictionaries. A scheduler pass services at most
16 owners, each with the existing 16-entry completion/retention budget. The
existing per-instance tick gates and retry backoff remain authoritative.

An owner registers once while holding its instance gate. After its tick, that
same gate protects requeue or retirement when its retained group count reaches
zero. This preserves teardown retries after durable match cleanup removes the
match from the legacy group dictionary. A later import can register the owner
again. No periodic timers or threads are added.

Groups entering through these owners retain an explicit completion-owner marker.
The legacy scheduler skips their timeout/export/payout branch, including rejected
invitations. Rejected entries already held by a retention owner now use the
rejection cleanup below; invitations refused before ownership, including capacity
exhaustion, still need matchmaker removal or retry. Neither an owned invitation
nor its drained retention can fall back to legacy fight rewards.
The team scheduler snapshots its legacy group enumeration because exports can
remove groups while it runs.

The fixture invokes the actual public CheckGroups methods with ordinary owned
matches, temporarily setting unrelated legacy fixture groups aside. It verifies
saved solo/team results and healthy opponents, a deferred team teardown that
outlives match removal, rejected-entry draining, and rejection past the legacy
timeout. These are server-side fixtures, not native network or Host shutdown tests.

Only owned imports register here. Existing Attach-only callers continue
to own their tick schedule. Matchmaking now selects owned entry after joint
startup preparation, as described below. Production startup activation,
coordinated shutdown and native acceptance remain open.
No deployment, production schema change or restart is part of this change.

Validation: isolated build and 728 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-scheduler-owner-build.log` and
`obj/chi-refill-validation/arena-scheduler-owner-tests.log`.

## Rejected invitation cleanup

Both groups expose a lock-protected rejection path limited to unimported,
unreserved groups with no map or outcome. Retention retries that path for entries
rejected after ownership was acquired. Its retained entry cannot retire until
invitation cleanup completes, even if session teardown has already resumed.

Cleanup removes exact original queue registrations and resets the original
leaders' invitation/status fields, sends their statistic update, and removes the
exact group. It does not teleport, write result evidence, change points or award
rewards. A replacement registration is preserved, as is a current actor whose
opponent or group now belongs to a different invitation. Fenced canonical actors
defer mutable cache cleanup until retention releases them. Team members retain
their normal team membership; only the two matchmaking leaders own invitations.

Fixtures seed both queues before reentrant entry rejection and verify cleanup,
unchanged no-result behavior, replacement registration preservation and a newer
opponent's invitation. Transport failures during the statistic send and native UI
delivery remain unverified. This completes cleanup for already-owned rejections;
capacity refusal before ownership and production matchmaking integration remain
open.

Validation: isolated build and 732 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-rejected-entry-build.log` and
`obj/chi-refill-validation/arena-rejected-entry-tests.log`.

## Prepared matchmaking entry

The accepted-invitation branches of both public VerifyAwaitingPeople methods now
call ImportMatched. Before joint startup preparation they preserve the existing
legacy path. Once Prepared is true, each mode uses its own 1,024-match retention
owner and never falls back to legacy import after a failed owned attempt.
Construction of these owner objects does not initialize database tables or enable
joint startup on the running server.

Each attempted group is marked before admission. Refusals before retention
ownership, including full capacity, call the no-result invitation cleanup. If a
player lock is busy, the existing CheckGroups loop retries that cleanup and skips
the legacy timeout/export branch. The invitation remains in its existing group
dictionary while pending, without consuming an extra retention slot. Already-owned
failures continue through their scheduled retention owner.

Fixture coverage includes successful entry through the shared matchmaking seam,
capacity refusal without reservation/transfer, busy rejection followed by actual
scheduler cleanup, and the actual public solo/team matchmaking methods through
ordinary saved results and invitation cleanup. The fixture advances its scheduler
clock beyond the established retry delay instead of requiring immediate retries.

Server.Run does not yet call joint startup preparation. Startup must initialize
participation and the full shared lifecycle before activation, and coordinated
shutdown, remaining legacy callback ownership, team daily settlement and native
acceptance remain open. This source wiring is not a live release.

Validation: isolated build and 740 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-matchmaking-owner-build.log` and
`obj/chi-refill-validation/arena-matchmaking-owner-tests-final.log`.

## Owned quit, surrender and result callbacks

For a group with a completion owner, solo DoQuit/DoGiveUp and the two originally
admitted team leaders' DoQuit/DoGiveUp now request the result coordinator before
any legacy queue, invitation or UI mutations. A busy coordinator retains the
first departure and its observation time for scheduler retry. Solo quit/surrender
records Forfeit; the existing team intent stores the original losing leader and
time without a distinct forfeit cause column.

Direct Win calls for an owned group also enter the coordinator after validating
the original winner/loser pairing. They cannot run the legacy payout or clear
invitation references before settlement. Team Export still performs its owned
post-commit cleanup; its Win call recognizes the already committed result.
Unowned matches keep their legacy behavior.

Ordinary-player fixtures call all three public callbacks in both modes while an
opponent lock is held. They verify unchanged points/invitation references while
busy, retry from the opposing player, original-loser persistence, one committed
result and scheduler map/invitation cleanup. They exercise server callbacks, not
native packet delivery. Non-leader voluntary team departure, other legacy combat
and spectator callback ownership, full shutdown and native acceptance remain
separate unfinished work. Production startup is still not activated.

Validation: isolated build and 758 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-callback-owner-build.log` and
`obj/chi-refill-validation/arena-callback-owner-tests.log`.

## Owned solo result dialogs after commit

Owned solo Group.End calls now freeze the combat outcome without sending the
legacy win/loss dialog. CompleteCore authorizes notifications only after the
result transaction commits and statistic publication succeeds. This covers both
direct combat endings completed by the pump and coordinated departures.

The notification method requires the match lock, each recipient's actor lock,
the original current group/session and valid solo/team caches. It records one
attempt per original winner/loser. Repeated completion cannot attempt another
dialog, and a rollback or failed cache projection cannot authorize a dialog.
Fenced recovery settles durable evidence without notifying from stale actors;
normal reconnect loads saved results. Unowned legacy matches retain their
existing notification behavior.

The ordinary-player fixture freezes a real owned combat result, checks that no
notification is authorized before completion, then verifies both attempts after
commit and no duplicate on replay. A constraint-induced result rollback verifies
zero attempts through fenced recovery and cleanup. These counters prove server
send attempts, not native delivery, rendering or receipt. Production startup,
remaining callback work and native acceptance are still open.

Validation: isolated build and 764 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-owned-dialog-build.log` and
`obj/chi-refill-validation/arena-owned-dialog-tests.log`.

## Owned combat and team disconnect callbacks

GameState.EndQualifier now requests an owned solo Defeat through the result
coordinator. Team CheckEnd retains its admitted-side living-player test, then
requests the losing original leader through the coordinator. It does not freeze
the result before the coordinator can acquire the original actor locks. The
existing first-departure timestamp survives opposing combat callbacks and retry.

For an owned team, CheckEnd's disconnect branch uses the retained disconnect
observer. A departing member is excluded from further living-side/scoring checks
while a remaining fighter can continue; terminal departures enter normal durable
settlement. The ordinary combat branch rejects noncanonical or foreign actors.
Legacy groups retain their existing paths.

Fixtures invoke the actual EndQualifier callback for solo death and complete team
defeat while the opponent lock is busy, then invoke opposing/repeated callbacks
and the real scheduler. They also cover a member death with a surviving leader
and the actual CheckEnd disconnect branch for a nonterminal member. This is
server callback coverage, not a native attack/animation or socket teardown test.
Voluntary non-leader departure, spectator callbacks, production startup, team
daily settlement, coordinated shutdown and native acceptance remain open.

Validation for owned combat callbacks: isolated build and 772 combined Arena
checks pass. Ignored logs: `obj/chi-refill-validation/arena-combat-owner-build.log`
and `obj/chi-refill-validation/arena-combat-owner-tests.log`.

## Team spectator ownership

Team BeginWatching now acquires the match and spectator actor locks, validates
the original playable session and all four Arena role references, and verifies
the actual transfer before registering watcher membership. Repeated or competing
joins cannot claim the same spectator twice. Ended, departing or disposed matches
reject new watchers.

DoLeave and DoCheer delegate to lock-protected group operations using a captured
group reference. Leave validates the original session and watcher membership,
returns the actor from the owned map, then clears membership and announces the
updated list. A stale callback cannot move a replacement session. Cheer requires
actual watcher/map ownership and a known original team leader, counts once per
spectator, and saturates counters. Invalid fighter IDs no longer consume cheers.

Watcher-list broadcasts go to each canonical current spectator and admitted
fighter, rather than repeatedly targeting the departing spectator or depending
on mutable Team objects. The existing packet builders and layouts are preserved.

Ordinary fixtures cover failed transfer, busy admission, concurrent duplicate and
competing joins, unknown/repeated cheers, replacement-session protection,
concurrent leave and late admission/cheer rejection. Native rendering, transport
failure during transfer/send, spectator shutdown orchestration and production
acceptance remain unverified. These guards also apply to legacy team matches.

Validation: isolated build and 781 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/team-spectator-owner-build.log` and
`obj/chi-refill-validation/team-spectator-owner-tests.log`.

## Five-second postmatch return (2026-09-21)

The [official Team Qualifier guide](https://co.99.com/guide/quests/teamqualifier.shtml),
checked 2026-09-21, specifies five seconds between the match ending and return.
The owned completion path previously called Export immediately after committing;
the legacy scheduler separately used a four-second check. Both now use one
wrap-safe five-second predicate measured from the frozen outcome's DoneStamp.
Export enforces it itself, so a direct call cannot bypass the wait. Retained
owned matches additionally require DurableResultReady before any return. Failed
admission cancellation bypasses the postmatch delay because no match completed.

Result persistence and notification remain independent of the return delay.
Players, spectators and the map remain owned until normal cleanup is permitted;
shutdown therefore also waits for this cleanup. Repeated return cannot replay
result statistics. Ordinary fixtures check zero elapsed, 4,999 ms, exactly
5,000 ms and unsigned timer wrap; they verify saved results, retained players/map,
return to the town map and idempotent cleanup. Existing non-timing fixtures use
an explicitly advanced return clock, while the new timing cases control it.
Native countdown/rendering and exact return-position acceptance remain open.

Build and 1,113 combined Arena checks pass.
Evidence logs: `obj/chi-refill-validation/team-return-window-build.log` and
`obj/chi-refill-validation/team-return-window-tests.log`. The existing owner
`System/IniFile.cs` modification is part of the test build but not this commit.
No protocol layout, production files or client assets changed.

## Level-70 eligibility boundary (2026-09-21)

The common Team Arena eligibility predicate now permits exactly level 70,
matching the official guide linked above. Previously `<= 70` excluded level-70
leaders from matching and level-70 teammates from roster selection. The existing
ordinary matchmaking fixture now enters two level-70 leaders and their eligible
level-70 member, leaving an added level-69 teammate in town without result credit.
It verifies saved result reload through the production TeamArenaTable reader.

Build and 1,116 combined Arena checks pass. Evidence logs:
`obj/chi-refill-validation/team-level-boundary-build.log` and
`obj/chi-refill-validation/team-level-boundary-tests.log`.
The fixture starts with consistent saved Arena balances. Progression from level
69 still needs the shared-point mirror correction recorded in ARENA_CUTOVER.md.
Native acceptance, joint startup activation and deployment remain pending.
