# Historical quest and event status notes

Archived from QUEST_EVENT_STATUS.md on 2026-09-21 at source baseline d3cb296.
Entries below are historical and may be superseded; use [current status](QUEST_EVENT_STATUS.md)
and the [roadmap](QUEST_EVENT_ROADMAP.md) for current policy and open gates.

# Quest and event status

Magnolia's new daily harvest contracts now include one bound level-1 rune.
Build and 867 checks including shared prerequisites pass; bonus chance and custom
packs are unchanged. Native acceptance/deployment remain pending.

**Release position:** source implementation and isolated validation; native/live
acceptance remains pending. The [bounded release-readiness audit](QUEST_EVENT_RELEASE_READINESS.md)
identifies Moon Box as the first acceptance candidate and the still-unconnected
joint Arena startup. Demon Exterminators' identified reward gap is addressed in
source: new contracts add one bound yellow rune, saved at acceptance; existing
contracts retain their original rewards. Native acceptance/deployment is pending.

Elimination's missed daily P7 source is now corrected: new contracts give a bound
rune and old P7 flags are ignored. Build and 197 checks pass; its global release
gate remains closed. Sky Pass and Snake Islands' daily P7 sources are also
corrected: new contracts give bound runes, old contracts retain Meteors.
Build, 45 Sky and 30 Snake checks pass; native/deployment pending.
Daily Exorcism now adds one bound level-1 rune to new contracts at all three
difficulties. Build and 633 checks including prerequisites pass, covering earned
and paid claims, exact reward persistence and rollback; native/deployment pending.

Ancestor Worship now adds a bound level-1 Tortoise Breaker rune to each new daily
claim, with no P7 or retroactive reward. Build and 521 checks including shared
prerequisites pass; native acceptance/deployment remains pending.
Daily Hunt and Rare Materials now also save a bound level-1 Tortoise Breaker
reward for new contracts, preserving old terms and the no-daily-P7 rule. Build,
581 Daily Hunt and 590 Rare Materials checks including prerequisites pass;
ordinary earning, persistence and rollback are covered, native/deployment pending.
Soul Ball and Release Souls now include the same bound rune on new contracts,
preserving existing/imported terms and the no-daily-P7 policy. Build and 628/523
checks respectively, including shared prerequisites, pass; native/deployment pending.
Recent regression counts are not an overall completion percentage. The audit
reused existing evidence without another build or test run.

Searing Touch's nearby Arena pulse now owns its original caster/match, carrier
and target, validates radius/expiry and credits actual nonlethal HP damage.
Its tests also exposed and fixed the legacy Team Arena attack-permission shortcut
that bypassed target attackability and dereferenced mutable ordinary membership.
Build and 1,040 combined Arena checks pass
([scope](ARENA_OWNED_SCHEDULING.md#searing-touch-nearby-pulse)). Breaking Touch's
percentage timer, shared flame-flag interactions and native/live acceptance
remain open. No deployment or production schema change occurred.

Team Arena Toxic Fog now retains its original caster/match and ticks under combat
ownership. Scoring uses actual HP removed after shield absorption; busy ticks
retry without losing duration, and stale or unowned carried-in poison is cleared.
All eight spell-application branches and the buffer timer use the integration.
Build and 1,029 combined Arena checks pass
([scope](ARENA_OWNED_SCHEDULING.md#toxic-fog-caster-and-timer-ownership)). Native
casting/animation, other direct health timers and live activation remain open.
No deployment or production schema change occurred.

The shared entity damage receiver now holds Team Arena match, actor and departure
ownership through health/score writes and terminal callbacks. It rejects direct
hits during preparation, busy ownership and after deadline/outcome freeze. Build
and 1,018 combined Arena checks pass, including a lethal hit that saves the whole
roster and releases ownership for cleanup
([scope](ARENA_OWNED_SCHEDULING.md#shared-damage-receiver-ownership)). Direct poison
ticks still need caster ownership and timer integration; native/live acceptance
remains open. No deployment or production schema change occurred.

Team Arena shared combat permission now requires current living opponents from
the original admitted roster on its registered map, during the active fight.
Ordinary team removal cannot enable friendly fire; departed owners, expired or
completed fights and busy result ownership reject attacks. Build and 1,011
combined Arena checks pass
([scope](ARENA_OWNED_SCHEDULING.md#combat-permission-from-the-admitted-roster)).
Full health-mutation ownership, bypassing delayed effects, postmatch return timing
and native/live acceptance remain open. No deployment or production schema
change occurred.

Team Arena now enforces ten seconds of preparation in both the main attack
handler and shared entity attack permission, protecting attacker and target until
admission is ready and the interval has elapsed. Build and 997 combined Arena
checks pass, including real attack permission at exact and wrapped boundaries
([scope](ARENA_OWNED_SCHEDULING.md#ten-second-team-arena-preparation)). Native
countdown, bypassing delayed-effect paths and five-second postmatch return remain
open. No deployment or production schema change occurred.

Team Arena timeout now uses five minutes from import in the normal coordinator,
fenced recovery and legacy scheduler. Older invitation creation time cannot
shorten the elapsed fight deadline. Build and 989 combined Arena checks pass,
including old-cutoff rejection, exact deadline, tick wrap and recovery
([scope and official evidence](ARENA_OWNED_SCHEDULING.md#five-minute-team-arena-duration)).
Native countdown, preparation/return timing and production activation remain
open. No deployment or production schema change occurred.

Owned Team Arena no longer automatically loses when its original leader
disconnects with a living teammate present. The surviving member can win or lose
through combat; the original complete roster receives one saved result before
requested leader teardown resumes. Build and 987 combined Arena checks pass
([scope and official evidence](ARENA_OWNED_SCHEDULING.md#leader-disconnect-with-surviving-teammates)).
Explicit leader forfeits keep Revora's existing policy. Full native disconnect/
reconnect acceptance and production activation remain open; no deployment or
production schema change occurred.

Team Exit/Kick/Dismiss packet handlers now notify prepared owned Arena departure
after actual membership removal. The original match leaders and admitted rewards
survive ordinary team leadership changes; members return and original leaders
forfeit through the existing retained paths. Build and 977 combined Arena checks
pass, including 18 command-removal assertions
([scope](ARENA_OWNED_SCHEDULING.md#team-removal-commands)). Dismiss retains its
existing caller-removal behavior. Direct non-packet removal, full concurrent team
mutation ownership, invitation responses and production/native activation remain
open. No deployment or production schema change occurred.

Prepared Team Arena now returns non-leaders who choose Quit/Give Up while retaining
their admitted roster and result ownership until whole-team settlement. Surviving
teammates continue; the last living member's departure records defeat. Return
teleport callbacks cannot recursively complete that recorded departure. Build and
959 combined Arena checks pass, including 24 member-return assertions
([scope and policy evidence](ARENA_OWNED_SCHEDULING.md#voluntary-member-return)).
Team removal/kick/disband integration, admission-time member responses and
production/native activation remain open. No deployment or production schema
change occurred.

The Team Arena departure audit exposed unauthorized team controls: ordinary
members could issue kick and shared-setting requests. Packet handling now checks
the current session and exact leader membership while preserving voluntary exit
and valid ordinary-leader commands. Build and 935 combined Arena checks pass,
including 20 command-authority checks
([scope](ARENA_OWNED_SCHEDULING.md#team-command-authority-prerequisite)). Full team
mutation concurrency, non-leader Arena departure settlement and production/native
activation remain open. No deployment or production schema change occurred.

Owned Arena admission now checks pending signup withdrawal around reservation,
transfer setup/completion and registered solo participation commit. Expected
cancellation rolls back earning evidence without fencing healthy actors; a real
write failure preserves fenced ownership for recovery. Build and 915 combined
Arena checks pass with database-blocked writers and the failed-write overlap
([scope](ARENA_OWNED_SCHEDULING.md#withdrawal-during-admission)). Full live timing,
non-leader team departure, remaining policy decisions and production/native
activation remain open. No deployment or production schema change occurred.

Prepared solo/team Quit and Give Up now retain bounded signup withdrawal requests
and finish them under actor ownership. Pending requests block re-signup and
matchmaking; orphaned invitations and replacement queue registrations are handled
without rewards or unrelated state clearing. Build and 894 combined Arena checks
pass ([scope](ARENA_OWNED_SCHEDULING.md#queued-signup-withdrawals)). Owned fights
retain their existing forfeit coordinator. Full live admission interleavings,
non-leader team departure, remaining policy decisions and production/native
activation remain open. No deployment or production schema change occurred.

Prepared solo/team result callbacks now reject unstarted invitations before the
legacy reward path. Expired solo invitations cannot earn fight points or daily
counts; busy cleanup reuses one pending pair and retries through the scheduler.
Build and 872 combined Arena checks pass
([scope](ARENA_OWNED_SCHEDULING.md#unstarted-result-callbacks)). Full quit/refusal
ownership, cancellation/admission and requeue races, remaining policy decisions
and production/native activation remain open. No deployment or production schema
change occurred.

Owned Team Arena admission now prepares the entire selected roster's daily
counters under its retained actor locks before reservation or transfer. Excluded
members stay untouched; unrelated invitations and member write failures prevent
entry. Build and 862 combined Arena checks pass through public matchmaking,
member result reload and rejected-roster cleanup
([scope](TEAM_ARENA_COUNTER_ROLLOVER.md#prepared-roster-admission)). Shared/monthly
point policy, ranking awards, remaining invitation callbacks and production/native
activation remain open. No deployment or production schema change occurred.

New Team Arena results now reconstruct daily counters on the saved outcome's
Pacific date, including fights that finish after midnight. Versioned captures
preserve older recovery arithmetic; delayed recovery verifies saved-day counts
without replaying results or milestone rewards. Build and 853 combined Arena
checks pass ([scope](TEAM_ARENA_RESULT_DAYS.md)). Solo already uses saved-day
counter reconstruction. Team roster preparation, shared/monthly point policy,
ranking awards, remaining invitation callbacks and production/native activation
remain open. No deployment or production schema change occurred.

Prepared solo invitations now refresh both players' daily state after acceptance
and before registered entry, including invitations spanning midnight. Busy locks
or unmet prerequisites defer admission; failed reset writes prevent entry. Build
and 843 combined Arena checks pass through ordinary queue pairing, registered
combat completion and reload
([scope](ARENA_DAY_ROLLOVER.md#accepted-invitations-across-midnight)). Already
fighting cross-midnight results, team roster preparation, remaining invitation
callbacks, production activation and native acceptance remain open. No deployment
or production schema change occurred.

Prepared Arena scheduling now settles completed daily rankings while the server
runs: one day per thirty-second probe, with pending-result and failed-write
retries. Build and 835 combined Arena checks pass, including the real scheduler,
cross-midnight completion, concurrent retry, next-day signup and earned Honor
delivery ([scope](ARENA_RANKING_SETTLEMENT.md#prepared-runtime-settlement)).
Production startup activation, already-queued day boundaries, team ranking policy
and native validation remain open. No deployment, restart or production schema
change occurred.

Prepared solo signup now performs the saved daily reset before checking points,
so an eligible player with zero points can receive the existing daily fill and
join. Invitation and failed-write cases prevent admission; repeat requests and
normal reload preserve one reset. Build and 824 combined Arena checks pass
([scope](ARENA_DAY_ROLLOVER.md#prepared-solo-signup)). The character reward hook
already exists; production initialization remains disabled. Automatic midnight
settlement, queued-player day boundaries and native validation remain open.
No deployment, restart or production schema change occurred.

Prepared team signup now reconstructs daily win/match counters from saved
Pacific-day results without changing shared points, Honor or lifetime totals.
Receipt failure rolls back, and legacy reset/save paths respect rollover
ownership. Team outcome timestamps now use the shared UTC clock, correcting the
earlier day-boundary fixture gap. Build and 820 combined Arena checks pass
([scope and rule differences](TEAM_ARENA_COUNTER_ROLLOVER.md)). Exact ranking
awards/monthly point policy, production activation and native validation remain
open. No deployment or production schema change occurred.

Team Arena now records nine-win and twenty-match daily entitlements from saved
Pacific-day results for every admitted participant, including recovery, and
delivers each EXP Ball/500 Honor pair atomically with replay protection. Build
and 813 combined Arena checks pass, including threshold rollback, twentieth-match
recovery, full inventory, concurrent delivery and reload
([scope and official evidence](TEAM_ARENA_DAILY_REWARDS.md)). Daily ranking and
counter rollover, production lifecycle activation and native validation
remain open. No deployment or production schema change occurred.

Team spectators now use locked session/map ownership for join, leave and cheer.
Duplicate joins/cheers, unknown fighters and late spectators are rejected, while
replacement sessions survive stale leave callbacks. Watcher updates target the
actual current recipients. Build and 781 combined Arena checks pass
([scope](ARENA_OWNED_SCHEDULING.md#team-spectator-ownership)). Native delivery,
spectator shutdown, voluntary member departure and full lifecycle activation
remain open. No deployment or restart occurred.

Owned combat callbacks now request durable solo defeat/team-side completion,
and owned team CheckEnd disconnects use retained member observation. A surviving
leader keeps fighting after a member death/disconnect. Build and 772 combined
Arena checks pass, including competing combat callbacks under lock contention
([scope](ARENA_OWNED_SCHEDULING.md#owned-combat-and-team-disconnect-callbacks)).
Voluntary member departure, spectators, production startup, team daily settlement,
shutdown and native acceptance remain open. No deployment or restart occurred.

Owned solo combat results now defer win/loss dialogs until the result transaction
commits and statistic publication succeeds. Rollback/fenced recovery sends no
stale result dialog, and completion retries do not duplicate attempts. Build and
764 combined Arena checks pass
([scope](ARENA_OWNED_SCHEDULING.md#owned-solo-result-dialogs-after-commit)).
Native receipt/rendering, remaining callbacks, production startup, team daily
settlement and coordinated shutdown remain open. No deployment or restart occurred.

Owned solo quit/surrender, original team-leader quit/surrender and direct Win
callbacks now use durable coordination before legacy invitation mutations.
Competing callbacks preserve the first loser and settle once. Build and 758
combined Arena checks pass
([scope](ARENA_OWNED_SCHEDULING.md#owned-quit-surrender-and-result-callbacks)).
Non-leader team departures, remaining combat/spectator callbacks, production
startup, team daily settlement, shutdown and native acceptance remain open.
No deployment or restart occurred.

Accepted solo/team matchmaking now selects retained entry after joint startup
readiness. Pre-ownership refusal clears invitations without rewards, and busy
cleanup retries through CheckGroups. Build and 740 combined Arena checks pass,
including actual public matchmaking through saved results for ordinary players
([scope](ARENA_OWNED_SCHEDULING.md#prepared-matchmaking-entry)). Server startup
activation, remaining legacy callbacks, team daily settlement, coordinated
shutdown and native acceptance remain open. No deployment or restart occurred.

Already-owned rejected solo/team entries now clear exact invitation and queue
ownership without a fight result, while preserving replacement sessions and
newer opponents. Retention retries cleanup before retiring the entry. Build and
732 combined Arena checks pass
([scope](ARENA_OWNED_SCHEDULING.md#rejected-invitation-cleanup)). Pre-ownership
capacity refusal, production matchmaking/startup, coordinated shutdown and native
acceptance remain open. No deployment or restart occurred.

The real solo/team CheckGroups schedulers now service explicitly owned entries
through bounded owner queues and exclude those groups from legacy payouts.
Deferred teardown survives removal of the match from the group dictionary;
rejected owned invitations cannot acquire legacy timeout rewards. Build and 728
combined Arena checks pass ([scope](ARENA_OWNED_SCHEDULING.md)). Production
startup/matchmaking activation, rejection handling, coordinated shutdown and
native acceptance remain open. No deployment or restart occurred.

Staged team entry now reserves retention capacity and every selected roster
member's shutdown owner before database reservation or transfer. Failed admission
drains through rejection or durable cancellation without a fight reward. Build
and 726 combined Arena checks pass
([scope](ARENA_TEAM_LIFECYCLE.md#owned-team-entry)). Live composition, coordinated
shutdown and native acceptance remain open. No deployment, production schema
change or restart occurred.

The staged solo entry owner now reserves bounded retention capacity and both
session shutdown owners before reservation or transfer. Entry-time disconnects
remain retained through rejection or durable rollback recovery; successful entry
hands off to result completion. Build and 718 combined Arena checks pass
([scope](ARENA_SOLO_ADMISSIONS.md#owned-solo-entry)). Live composition, team entry
parity, coordinated shutdown and native acceptance remain open. No deployment,
production schema change or restart occurred.

Failed staged registered entries now recover according to saved evidence:
committed entries are preserved, rolled-back entries are cancelled without a
result, and incomplete evidence remains pending. The completion pump retains
cleanup until fenced original sessions release. Build and 708 combined checks
pass ([scope](ARENA_SOLO_ADMISSIONS.md#failed-registered-entry-recovery)).
Automatic capacity/retention ownership and live/native activation remain open.
No deployment, production schema change or restart occurred.

An explicitly selected staged solo entry now commits match-ledger adoption and
both participation receipts/achievements in one transaction, with pre-transfer
eligibility/storage checks. Build and 696 combined checks pass, including actual
imports, replay and either-write rollback
([scope](ARENA_SOLO_ADMISSIONS.md#atomic-registered-entry)). Capacity reservation,
entry-time disconnect recovery and live/native activation remain open. No live
deployment, production schema change or restart occurred.

Arena participation-write failures now invalidate both original players' solo
and team statistic references through the shared Arena recovery path, instead of
leaving partially fenced sessions. Build and 688 combined checks pass
([scope](ARENA_SOLO_ADMISSIONS.md#participation-write-failure-and-arena-cache-ownership)).
Capacity reservation before entry, coordinated ledger adoption, runtime ownership
and native acceptance remain open. No deployment or restart occurred.

Interrupted staged solo Arena transfers now cancel their reservation without a
result, return owned transferred players and retry map cleanup through the
completion pump. Early ending callbacks cannot finalize an unfinished entry.
Build and 686 combined checks pass
([scope](ARENA_SOLO_ADMISSIONS.md#interrupted-transfer-cancellation-and-cleanup)).
Retention attachment, live activation and native interruption acceptance remain
open. No deployment, production schema change or restart occurred.

Staged solo Arena now reserves its original pair durably before transfer, exposes
pending ownership before ledger adoption and rejects abandoned reservations.
Joint cold startup closes unadopted admissions without rewards. Build and 678
combined checks pass ([scope](ARENA_SOLO_ADMISSIONS.md)). Immediate partial-transfer
cancellation/cleanup, retention attachment and live/client acceptance remain open.
No deployment, production schema change or restart occurred.

Joint Arena startup now verifies that interrupted team reservations are actually
closed after its bounded sweep. Exactly 128 completed closures no longer produce
a false failure; suppressed writes and genuine leftover work keep readiness
false and remain retryable. Build and 671 combined checks pass
([scope](ARENA_TEAM_LIFECYCLE.md#interrupted-admission-startup-boundary)).
Live startup composition and native acceptance remain open. No deployment or restart.

Staged solo and team Arena recovery now applies the normal three-minute timeout
to fenced matches with no earlier departure. It preserves damage/tie rules,
retained-departure precedence and atomic settlement, eliminating another pending
match stall. Build and 665 combined checks pass
([scope](ARENA_SHUTDOWN_RETENTION.md#normal-timeout-after-cache-failure)).
Automatic attachment, live writer cutover and native acceptance remain open.
No deployment or restart occurred.

Staged Team Arena now recovers a retained terminal departure when cache failure
prevents its initial result freeze. Recovery validates the whole original roster,
preserves the recorded loser and retries the atomic result before cleanup.
Build and 647 combined checks pass
([scope](ARENA_TEAM_LIFECYCLE.md#fenced-recovery-before-a-retained-team-departure-is-finalized)).
Automatic attachment, live writer cutover and native acceptance remain open.
No deployment or restart occurred.

Staged solo Arena recovery can now settle a retained busy disconnect when a
subsequent cache failure occurs before the outcome is frozen. It validates both
original owners, preserves the recorded loser/cause and retries the atomic
result; cache failure alone cannot invent an outcome. Build and 637 combined
checks pass ([scope](ARENA_SHUTDOWN_RETENTION.md#recovery-before-a-retained-departure-becomes-terminal)).
Automatic attachment and native acceptance remain open. No deployment or restart.

The obsolete Daily Envoy paid reset is retired. It could clear only legacy flags,
not the authoritative daily runs, so an atomic charge would still sell no useful
reset. The menu no longer offers it; replayed option 18 cannot charge CPs or clear
progress, even with the old setting enabled. Build and 559 combined Daily Hunt
checks pass, including ordinary reload and completed eligibility
([scope](DAILY_ENVOY_REWARD_AUDIT.md#retired-legacy-paid-reset)). Paid daily replay
is not implemented. No deployment or restart occurred.

Staged solo Arena recovery now repairs an ended match when only one original
participant's caches were invalidated. It validates both owners and outcome
evidence before fencing the healthy peer, then preserves atomic repair and
reload requirements. Build and 629 combined checks pass
([scope](ARENA_SHUTDOWN_RETENTION.md#solo-recovery-with-one-invalidated-participant)).
Automatic attachment, live writer cutover and native acceptance remain open.
No deployment or restart occurred.

Solo Arena now has staged shutdown retention through the same per-session
contract as Team Arena. Busy/failed results preserve original sessions until
durable settlement, with retryable teardown and healthy-opponent preservation.
Build and 622 combined checks pass ([scope](ARENA_SHUTDOWN_RETENTION.md)).
Automatic attachment, coordinated live writer cutover, shutdown drain and native
disconnect validation remain open. No deployment or restart occurred.

A staged team retention service now hooks the actual one-shot shutdown gate for
explicitly attached matches. It holds canonical actors/reconnect barriers until
settlement and retries teardown/cleanup without repeating rewards. Build and 610
combined checks pass ([scope](ARENA_TEAM_LIFECYCLE.md#staged-team-shutdown-retention-at-the-one-shot-boundary)).
Tests simulate the resume callback; automatic attachment, solo integration,
shutdown drain and native disconnect acceptance remain open. No deployment or
restart occurred.

Staged Team Arena disconnect observation now retains exact admitted member
departures, excludes them from damage/living-side checks and records the first
terminal loss before busy completion. Build and 599 combined checks pass
([scope](ARENA_TEAM_LIFECYCLE.md#staged-observation-before-team-disconnect-teardown)).
The legacy shutdown still releases registration too early; actual deferral,
resumption and native disconnect tests remain open. No deployment or restart
occurred.

Staged Team Arena recovery now handles mixed healthy/fenced original rosters.
It validates all ownership before fencing remaining caches, repairs only matching
saved preimages and retains reload requirements. Build and 592 combined checks
pass ([scope](ARENA_TEAM_LIFECYCLE.md#recovery-with-mixed-healthy-and-fenced-original-sessions)).
Missing/replaced actors remain rejected; live disconnect/shutdown integration
and client acceptance remain open. No deployment or restart occurred.

Staged reserved team imports now cancel interrupted transfers durably without
rewards, then return owned players and release the map through retryable cleanup.
Build and 583 combined checks pass, including map-creation failure, simulated
partial transfer, failed cancellation writes and replay
([scope](ARENA_TEAM_LIFECYCLE.md#interrupted-transfer-cancellation-and-owned-cleanup)).
Native interruption testing and live lifecycle integration remain open. No live
deployment or restart occurred.

Staged Team Arena import now commits a durable planned-roster reservation before
transfer. Shared-point guards honor it; results release it and cold startup
closes no-outcome reservations without rewards. Build and 576 combined checks
pass ([scope](ARENA_TEAM_LIFECYCLE.md#durable-reservation-before-team-transfer)).
Actual admitted members still determine rewards. Partial-transfer cancellation
and live lifecycle integration remain open; no deployment or restart occurred.

A staged joint Arena startup boundary now checks both modes before recovery,
rejects contradictory unsettled ownership, recovers team then solo results and
publishes readiness only after both audits and solo ranking settlement. Build
and 565 combined checks pass, including late-stage failure/retry and ordinary
loaders ([scope](ARENA_TEAM_LIFECYCLE.md#joint-startup-preparation-for-shared-arena-points)).
Live composition and remaining lifecycle cutover are still open. No deployment
or restart occurred.

A staged bounded Team Arena completion queue now retries busy matches and failed
results with backoff, and retains committed matches until owned cleanup finishes.
Build and 559 combined checks pass, including fair retries, clock rollback,
fenced repair and full-roster registration release before cleanup
([scope](ARENA_TEAM_LIFECYCLE.md#bounded-staged-team-completion-queue)).
Startup/disconnect/shutdown composition remains open. No live deployment or
restart occurred.

Staged Team Arena now retains the first canonical leader departure while match
or participant ownership is busy. Later callbacks respect its original leader
and observation day; damage stops while completion is pending. Build and 546
combined checks pass ([scope](ARENA_TEAM_LIFECYCLE.md#first-departure-retained-across-busy-team-completion)).
This is in-memory retention until capture, not crash-safe admission. Live
disconnect/scheduler integration remains open; no deployment or restart occurred.

Staged Team Arena terminal entry points now lock and validate the full admitted
roster before freezing a leader loss or three-minute combat timeout, then reuse
the durable result and notification path. Build and 528 combined checks pass
([scope](ARENA_TEAM_LIFECYCLE.md#staged-terminal-completion-entry-points)).
Busy-departure retention, live callbacks and shutdown integration remain open.
No deployment, restart or installed client change occurred.

Staged Team Arena victory/defeat dialogs now follow durable result commit and
successful cache projection, with at most one attempt per original leader.
Build and 517 combined checks pass, including pre-commit, rollback and stale-cache
suppression ([scope](ARENA_TEAM_LIFECYCLE.md#result-dialogs-after-durable-completion)).
These assertions cover send attempts, not native client delivery. Live lifecycle
integration and client acceptance remain open; no deployment or restart occurred.

Staged daily Arena rollover now preserves shared points earned from committed
team results on the same Pacific day without inflating solo quest counters.
Build and 513 combined checks pass, including leaders/member, wins/losses,
25-hour day boundaries, repeat rollover and production reload
([scope](ARENA_DAY_ROLLOVER.md)). Team daily/season settlement and coordinated
runtime integration remain open. No live deployment or restart occurred.

Staged team recovery now settles failed results under the exact original fenced
roster, including retry of an initial capture failure with unchanged preimages.
It preserves reload fences and separates durable completion from player usability.
Build and 461 combined checks pass
([scope](ARENA_TEAM_LIFECYCLE.md#recovery-while-original-team-owners-remain-fenced)).
Mixed-owner recovery, live retry scheduling and disconnect integration remain open.

A staged team result coordinator now commits capture/results before publication,
fences failed or stale caches, and marks committed results so Export cannot pay
legacy rewards again. Build and 453 combined checks pass, including concurrent
completion, actual export, rollback recovery and simulated stale projection
([scope](ARENA_TEAM_LIFECYCLE.md#staged-team-result-coordinator)).
Live ending/disconnect hooks, scheduling and composition remain open.

Staged solo/team import now checks pending ownership before map allocation or
transfer, retaining the import attempt when entry is deferred. Build and 445
combined checks pass; actual blocked imports leave players in town
([scope](ARENA_TEAM_LIFECYCLE.md#pending-checks-before-map-transfer)).
Durable reservation, retry scheduling and live composition remain open.

Staged solo admission/capture/result/recovery transactions now honor pending
team ownership, and team capture refuses an active non-abandoned solo admission.
Build and 444 combined checks pass, including actual imported matches with
missing in-memory group references
([scope](ARENA_TEAM_LIFECYCLE.md#shared-ownership-between-solo-and-team-transactions)).
Coordinated pre-import admission and runtime replacement remain open.

Pending team outcomes now guard point purchases and daily rollover under the
entity transaction lock, including when group references disappear. Complete
matching bundles release the guard; incomplete teammate receipts retain it.
Build and 441 combined checks pass
([scope](ARENA_TEAM_LIFECYCLE.md#pending-team-ownership-guards)).
Other admission/result writers, day settlement and activation remain open.

Staged Team Arena startup now drains pending captures in bounded chronological
batches and audits completed headers/receipts before readiness. Build and 437
combined checks pass, including failure/retry, duplicate preparation and
incomplete historical evidence
([scope](ARENA_TEAM_LIFECYCLE.md#bounded-team-startup-preparation)).
Composition, pending-owner guards and daily settlement remain open.

Inactive Team Arena startup recovery can now settle captured roster rewards
atomically before sessions or caches exist. Changed balances, inconsistent
snapshots and incomplete/orphan result evidence fail closed. Build and 428
combined checks pass, including rollback, concurrent recovery and production
reload ([scope](ARENA_TEAM_LIFECYCLE.md#startup-recovery-of-captured-team-outcomes)).
Bounded startup enumeration, pending-owner guards and runtime activation remain open.

Staged Team Arena completion now requires a separately committed outcome and
full participant snapshot. Failed reward writes retain that evidence, and pending
intents protect their balance preimages from legacy saves/resets. Build and 416
combined checks pass ([scope](ARENA_TEAM_LIFECYCLE.md#captured-team-outcome-before-reward-completion)).
Offline recovery and runtime capture/retry hooks remain open.

Durable team receipts now protect shared points, team counters and season fields
from stale legacy saves/resets, including reload before solo rollover initialization.
Build and 413 combined checks pass; both save overloads, detached-owner rejection
and ordinary row reload are covered
([scope](ARENA_TEAM_LIFECYCLE.md#durable-team-save-and-reset-ownership)).
The team transaction path remains inactive pending recovery and day-boundary integration.

An inactive Team Arena result transaction now writes the complete admitted
roster, original leader/member economy, counters and both point mirrors atomically.
Build and 409 combined checks pass, including parent rollback, receipt failure,
changed preimages, concurrent replay and production reload
([scope](ARENA_TEAM_LIFECYCLE.md#inactive-result-transaction-participant)).
Intent/recovery, save/reset ownership and live writer replacement remain open.

Legacy Team Arena rewards and export now follow the admitted roster and original
leader roles. Late joiners receive nothing, disbanded participants return before
map disposal, and repeated callbacks do not reapply in-memory awards. Build and
402 combined checks pass, including the actual import/result/export path and
production member save/reload ([scope](ARENA_TEAM_LIFECYCLE.md#legacy-roster-rewards-and-export)).
Durable disconnected-recipient recovery and live acceptance remain open.

Team Arena now records successfully admitted sessions and uses their original
sides for damage and all-dead checks. Outsiders, departed sessions and late hits
cannot score; concurrent totals saturate safely. Build and 396 combined checks
pass, including actual team import and disband-side ownership
([scope](ARENA_TEAM_LIFECYCLE.md#admitted-roster-and-damage-ownership)).
Reward/export roster migration, durability and live acceptance remain open.

Team Arena endings now accept only original leader sessions and freeze a single
outcome across competing callbacks. Original leader disconnects remain valid
after team membership disappears. Build and 386 combined checks pass
([scope and remaining work](ARENA_TEAM_LIFECYCLE.md)). Frozen participant
rosters, durable team rewards and runtime activation remain open.

Legacy Team Arena now counts wins as completed battles, clamps point losses
at zero, preserves legitimate balances above 80,000 and saturates overflow.
Normal teammate statistic updates are restored. Build and 379 combined checks
pass, including ordinary production save/reload
([scope](ARENA_SAVE_OWNERSHIP.md#legacy-team-calculation-correction)).
Durable team result migration and live deployment remain open.

Staged committed Arena cleanup now releases solo invitations and exact-session
queue entries while preserving earned balances, replacement sessions and the
legacy payout path. Fenced owners defer cleanup even after leaving the map.
Build and 373 combined checks pass ([scope](ARENA_COMMITTED_CLEANUP.md)).
Runtime activation and native acceptance remain open.

Staged Arena day rollover now excludes team match ownership, spectators,
invitations and disconnect barriers, protecting shared points during those states.
Build and 368 combined checks pass, including successful fill after ownership
clears ([scope](ARENA_DAY_ROLLOVER.md)). Runtime admission/cleanup integration
remains open.

A staged bounded Arena completion scheduler now rotates queued matches fairly,
backs off failed writes, and separates durable result readiness from cleanup.
Fenced results remain queued until session release permits cleanup. Build and
361 combined checks pass, including capacity, retry timing and failure recovery
([scope](ARENA_COMPLETION_PUMP.md)). Live host construction/ticking and coordinated
legacy-writer cutover remain open.

Initial Arena capture failures can now retry under both unchanged fenced owners,
using the retained first coordinator observation. Statistic/VIP preimages must
still match persistence; cause/day survive midnight and existing snapshots stay
immutable. Build and 348 combined checks pass
([scope](ARENA_STARTUP_RECOVERY.md#retrying-an-initial-capture-failure)). Live retry
scheduling and native reconnect remain open; lost RAM observations are not guessed.

The inactive Arena coordinator can now recover a captured failed result while
both original owners remain fenced and locked. It reuses durable snapshots,
commits pending prizes once and leaves stale sessions disabled. Build and 338
checks pass, including recovery of a failed ninth win and subsequent cleanup
([scope](ARENA_STARTUP_RECOVERY.md#recovery-while-both-failed-owners-remain-fenced)).
Live retry scheduling and outcomes with no durable capture remain open.

The inactive Arena coordinator now retains one first-observed departure request
per match while locks are busy. Retry/timeout processing preserves its loser,
cause and Pacific reward day across midnight; pending score/spectator changes
and early legacy result notifications are blocked. Build and 328 checks pass
([scope](ARENA_DEPARTURE_ENTRY.md#deferred-departure-requests)). The live shutdown
retry pump and failed-capture recovery remain open.

GameState shutdown now elects one final-save owner and removes GamePool/
DisconnectPool entries only when the exact session still owns them. Late old
callbacks preserve replacement sessions and their login barriers. Build and 312
combined checks pass ([scope](ARENA_DISCONNECT_OWNERSHIP.md)). Durable deferred
Arena departure completion and native reconnect remain open.

Arena economic failures now invalidate their exact solo/team cache objects before
fencing the session. Recovery reloads saved values, detached saves reject invalid
objects and late old-session failures preserve replacement caches. Honor purchases
flush team counters before charging. Build and 306 combined checks pass
([scope](ARENA_LOGIN_ROW_RECOVERY.md#economic-transaction-failures)); full native
reconnect and coordinated lifecycle activation remain open.

Character login now recovers existing solo/team Arena rows before first-time
insertion when a cache is missing or has an invalid identity. Team startup
counters retain UInt32 values above 255. Build and 299 combined checks pass
([scope](ARENA_LOGIN_ROW_RECOVERY.md)). Valid-but-stale post-commit cache recovery,
full native reconnect acceptance and deployment remain open.

All three existing Arena point-purchase handlers now share an atomic silver/
solo-points/team-points transaction and persisted duplicate interval. The existing
9,000,000-silver / 1,500-point economy is preserved. Build and 291 combined checks
pass, including the ordinary solo packet handler and write-failure rollbacks
([scope](ARENA_POINT_PURCHASES.md)). Source is wired; live deployment/native button
acceptance remain open.

Managed Arena saves now preserve captured/result/rollover-owned values: solo saves
refresh only identity metadata, team saves preserve shared points/Honor, and late
detached sessions cannot save. Build and 275 combined checks pass across both save
overloads and concurrent stale writes ([scope](ARENA_SAVE_OWNERSHIP.md)).
Coordinator activation still requires disconnect and all shared earning writers.

Staged Arena startup preparation now initializes schemas, drains interrupted
matches, audits finished bundles and settles missed rankings before declaring
Prepared. Durable result owners bypass legacy load/reset even before their first
rollover receipt. Build and 268 combined checks pass, including failure/retry and
128-record budget exhaustion. Server.Run wiring remains open
([scope](ARENA_STARTUP_PREPARATION.md)).

Solo Arena spectator entry, departure and cheers now serialize with cleanup;
ended fights reject joins and failed teleports leave no phantom membership.
Legacy export retries busy owners and authorizes its payout only once. Build and
259 combined checks pass, with an explicit real fixture admission assertion
([scope](ARENA_SPECTATOR_OWNERSHIP.md)). Durable coordinator activation remains open.

Inactive Arena cleanup now requires the complete durable result bundle, returns
only still-owned fighters/spectators and preserves retry state for fenced owners.
Repeated cleanup cannot disturb a later match. Build and 245 combined checks
pass; live lifecycle and legacy Export cutover remain open
([scope](ARENA_COMMITTED_CLEANUP.md)).

Inactive Arena timeout completion now uses three minutes from fight import,
freezes the accepted damage winner and commits before notification. Score updates
reject foreign/late actors and avoid overflow. Build and 234 checks pass,
including tick wrap; live timeout/export cutover remains open
([scope](ARENA_TIMEOUT_ENTRY.md)).

An inactive canonical departure entry now freezes and commits defeat/forfeit/
disconnect outcomes before win/loss notifications. Foreign sessions cannot end
a match; busy owners leave its outcome untouched. Build and 221 checks pass.
Live departure hooks and deferred cleanup remain open
([scope](ARENA_DEPARTURE_ENTRY.md)).

Inactive Arena ranking catch-up now settles missed played days oldest-first in
batches of at most 31, excluding today and already-sealed days. Build and 203
checks pass across year boundaries and repeated passes. Live lifecycle wiring
remains open ([scope](ARENA_RANKING_CATCHUP.md)).

Inactive owned Pacific rollover now fills points once per player/day, preserves
current-day earned results and projects the saved previous ranking without
re-awarding Honor. Managed loads skip legacy host-local reset. Build and 200
checks pass; runtime admission/day orchestration remains open
([scope](ARENA_DAY_ROLLOVER.md)).

A gated character-timer dispatcher now probes pending Arena rewards every thirty
seconds and attempts at most one daily/custom/ranking payment per interval.
Build and 189 checks pass, including full-bag retry, concurrent delivery and
clock rollback. The gate remains inactive until coordinator startup is wired
([scope](ARENA_REWARD_DELIVERY.md)).

Inactive daily ranking payout now commits both Honor mirrors and its payment
receipt atomically. Build and 183 checks pass, including earned-ranking Honor
purchases of Sword Soul/Banshee Spirit through the shop handler and persistence
loaders. Automatic delivery, day reset and live cutover remain open
([scope](ARENA_RANKING_PAYOUT.md)).

Inactive daily Arena ranking now snapshots last committed daily points and
bounded Honor entitlements once, with verified result order and sealed-day
guards. Build and 174 checks pass. Ranking payout, day reset and live cutover
remain open ([scope](ARENA_RANKING_SETTLEMENT.md)).

Inactive Arena startup reconciliation now processes bounded batches, recovers
captured outcomes and closes uncaptured interruptions without rewards. Closed
identities reject late results and capture. Build and 161 checks pass; actual
startup/departure wiring remains open ([scope](ARENA_STARTUP_SWEEP.md)).

Inactive startup recovery now settles one captured Arena outcome, both saved
statistics and pending daily/custom prizes atomically before caches/listeners.
Build and 152 checks pass, including ninth/hundredth milestones, rollback and
repeat startup calls. Enumeration, abandonment and lifecycle activation remain
open ([scope](ARENA_STARTUP_RECOVERY.md)).

Arena outcome capture now atomically snapshots both statistic preimages, recipient
VIP and custom economy. Retries preserve the original prize and reject later
statistic changes. Build and 131 checks pass; offline settlement remains open
([scope](ARENA_OUTCOME_SNAPSHOTS.md)).

Arena outcome intents now persist before the inactive coordinator attempts its
result bundle. Failed bundles retain winner/loser/cause and original Pacific day,
including a retry after midnight. Build and 129 Arena checks pass. Lost-session
intents are retained but offline settlement and departure hooks remain unfinished
([scope](ARENA_RESULT_INTENTS.md)).

The inactive Arena coordinator now commits terminal result, both statistics,
daily/custom entitlements and a complete-bundle receipt together before export.
Build and 114 checks pass, including earned nine-win Honor delivery. Disconnect
cleanup currently removes canonical ownership before End; lifecycle recovery and
legacy writer replacement remain required ([scope](ARENA_RESULT_COORDINATOR.md)).

Custom Arena prizes now have an inactive entitlement and atomic payout participant.
The hundred-win prize freezes configured currencies/materials and recipient VIP;
the optional fifty-loss reward uses the loser's eligibility. Build and 105 Arena
checks pass, including rollback and concurrent payout. Coordinator activation,
reconnect recovery and played earning remain unfinished
([scope](ARENA_CUSTOM_REWARDS.md)). Log: ignored
`obj/chi-refill-validation/arena-custom-entitlements.log`.

Arena prize material conversions now atomically exchange stones/scraps for their
packs, preserve source binding, and survive failed writes and reconnect replay.
Build, 177 conversion/P7 checks and 15 packet checks pass. Materials are fixture
supplied; custom Arena earning and downstream refinery/star use remain unfinished
([scope](ARENA_PRIZE_CONVERSIONS.md)).

Custom-prize prerequisite fixed: weapon/equipment P7 packs now open atomically
and always give bound souls. All 33 listed entries are reachable; failed writes
and reconnect replay preserve the source. Build, 146 pack and 15 packet checks
pass. Arena custom rewards and scrap conversion remain unfinished
([scope](P7_SOUL_PACK_TRANSACTIONS.md)).

Arena daily rewards now have an inactive entitlement/delivery participant:
nine wins and 20 matches each grant 500 Honor plus an EXP Ball, with atomic
Honor mirrors, inventory delivery, full-bag retention and per-day replay
protection. Build and 89 checks pass; coordinator activation, recovery, custom
prizes and settlement remain open ([scope](ARENA_DAILY_REWARDS.md)).

Arena result statistics now have an inactive transaction participant: both
players' match counters, verified daily wins, lifetime counters and conserved
three-percent point transfer commit with result receipts. Build and 57 checks
pass, including second-player write rollback. Honor and live result integration
remain unfinished ([scope](ARENA_RESULT_STATISTICS.md)).

Arena migration now has an inactive durable match ledger and shared completion/
export lock. Build and 39 ledger/participation checks pass. Result payouts,
disconnect/restart recovery and earned Honor spending remain unfinished
([scope](ARENA_MATCH_LEDGER.md)).

Arena Honor audit: shop spending is transactional, but match earning is not yet
verified. Legacy Win misses the winner's battle count, mixes invitation cancellation
with completed results, and runs after match export. Daily ranking reset also has
a consecutive-day defect. Original 20-match/nine-win evidence and a migration
sequence are now recorded; no payout was changed
([audit](ARENA_HONOR_EARNING_AUDIT.md)).

Latest private catalog candidate: Banshee Spirit now describes the implemented
house hunt and rewards, and its exact server description separator defect is
repaired. Four tests and a complete catalog/hash audit pass. Prior Anniversary
weekend and quest content is preserved; nothing is installed or published
([scope](PURPLE_BANSHEE_CLIENT_STAGE.md)).

Purple Banshee now has a complete server encounter candidate: real Honor-shop
purchase, owned house confirmation, 54-million-HP custom frost combat, 200/50
Study rewards and persisted P5/P6/refinery plus bound P7 pickup/reload. Played
Honor earning, catalog installation and native/live acceptance remain open
([scope](PURPLE_BANSHEE_ENCOUNTER.md)).

Latest Snow Banshee integration: the candidate now connects the public Grotto
boss, seven attacks, saved-health recovery, full server HP, atomic team/loot and
killer spirit rewards. The legacy Grotto summon now gives directions instead.
Build, 60 encounter, 79 Sword Master, 617 Soul Ball and 15 packet checks pass.
Native/live acceptance remains open
([scope](SNOW_BANSHEE_ENCOUNTER.md)).

Latest private catalog batch: matching Anniversary weekend text now includes
the envoy location, Pacific calendar and preserved weekday completion rule.
Three tests and a complete row/field/hash audit pass; prior Sword Master text
and the quest journal are retained. The candidate is unsigned, uninstalled and
unpublished ([scope](ANNIVERSARY_WEEKEND_CLIENT_STAGE.md)).

Latest Anniversary batch: the owner's recurring-weekend decision is implemented
in source. Anniversary Envoy at Twin City (323,307) issues new beads on Saturdays
and Sundays, Pacific time; existing beads remain finishable afterward. Build,
546 Anniversary checks and 15 packet checks pass, including the complete
ordinary 15,000-spirit earning route. Client/live acceptance remains open
([scope](ANNIVERSARY_WEEKENDS.md)).

Latest Snow Banshee rewards: the inactive victory participant now persists team
Study Points, weighted loot and an extra bound P7, with recovery and atomic
ordinary pickup. Build, 28 reward checks and 79 Sword Master regression checks
pass. Combat, spirit credit and activation remain unfinished
([scope](SNOW_BANSHEE_REWARDS.md)).

Latest Snow Banshee prerequisite: the reachable public clearing and inactive
single-boss persistence participant pass the build, 33 storage checks and 106
travel checks. Health recovery, stale actor fencing and the thirty-minute
post-defeat timer are covered. Combat, rewards and activation remain unfinished
([scope](SNOW_BANSHEE_PERSISTENCE.md)).

Latest Banshee finding: its summoning item incorrectly entered Promotion Pack
handling and was consumed for unrelated gifts. Item use and stale unowned summon
confirmations now preserve it. Build and 15 packet checks pass; the Grotto and
Purple Banshee earning routes remain unfinished ([evidence and next path](BANSHEE_RESTORATION.md)).

Latest private client batch: Sword Soul now describes the level-100 requirement,
challenge duration, failure conditions and rewards in matching staged server/client
catalogs. Three tests and a whole-file audit confirm only its tooltip changed;
the prior quest journal and inherited content are preserved. The candidate is
unsigned, uninstalled and unpublished ([review](SWORD_MASTER_CLIENT_STAGE.md)).

Latest Sword Master integration: the candidate now connects ordinary item
confirmation to the owned house encounter, seven named skills, full server HP,
atomic team/loot/1,000-spirit victory and failure/startup cleanup. Ordinary shop,
house invitation and production reload routes are covered by the focused fixture.
Build plus 79 encounter, 11 packet and 557 Housing checks pass.
Native/client and live Host gates remain open ([current scope](SWORD_MASTER_ENCOUNTER.md)).
The earlier Sword Master prerequisite-only entries below are superseded.

Latest Sword Master reward batch: victory-transaction participation now records
20 Study Points per eligible frozen teammate, two original-category drops and an
extra bound P7 event soul. House recovery, native pickup, binding, concurrency,
rollback and production item/character reload pass in 557 combined Housing checks.
The fixture supplies the victory; owned combat, spirit credit, admission activation
and native/Host acceptance remain open ([scope](SWORD_MASTER_REWARDS.md)).

Latest Sword Master prerequisite: exact-item consumption and a daily owned-house
attempt now commit together. Build and 464 Housing checks pass, including concurrent
stack consumption, reload, failed-attempt limits and rollback. Player admission
remains disabled pending combat/reward/lifecycle integration
([scope](SWORD_MASTER_ADMISSION.md)).

Latest Honor Store batch: purchases now commit their item, solo/team Honor debits
and receipt atomically, preserve pending earned funds, and suppress rapid duplicate
requests. Build and 488 combined checks pass. Sword Soul remains 8,000 Honor;
its owned boss summon and native/Host acceptance remain open
([scope](HONOR_PURCHASE_TRANSACTIONS.md)).

Latest Sword Master finding: a missed legacy summon continuation could spawn
a boss without rechecking house ownership. It is now disabled without consuming
the Sword Soul or daily attempt. Build and 11 packet checks pass. Atomic Honor
purchases are now implemented; played acquisition and the full owned encounter remain unfinished
([scope](SWORD_MASTER_RESTORATION.md)).

Latest Anniversary catalog batch: a private server/client candidate corrects
the legacy VirtuePack label to AnniversaryBead and describes the current bound-P7
event reward. Three tests and an actual-file scope audit pass. No installed files
changed. NPC 900028 remains absent; the event calendar decision is pending
([evidence](ANNIVERSARY_TRANSACTIONS.md#catalog-identity-correction-candidate-2026-09-21)).

Latest Trojan lifecycle batch: candidate server startup now initializes the
complete quest/trainer/journal route and the world timer runs owned-room cleanup.
Build and 1,776 combined checks pass, including 32-room capacity, nonfatal overflow,
concurrent retry and full cleanup. Live Host restart and native acceptance remain
open ([scope](TROJAN_EPIC_WORLD.md#candidate-lifecycle-and-capacity-acceptance-2026-09-21)).

Latest private content batch: Dis City difficulty, shared weekly limit, separate
rank brackets and bound event P7 text are composed with the reviewed quest journal,
Magnolia custom packs and Summer/Labyrinth item changes. All 19 overlay tests and
an actual-file byte-preservation audit pass. The candidate remains unsigned,
uninstalled and unpublished ([review](DIS_CITY_CLIENT_STAGE.md#current-combined-review-candidate-2026-09-21)).

Latest Dis City source batch: Solar Saint offers Standard, Veteran and Nightmare.
Runs retain their admission difficulty across resumes; daily standings award each
difficulty/edition its own top ten. Rewards stay equal and the weekly completion
limit remains shared. These additional combat settings are explicit Revora tuning
([scope](DIS_CITY_DIFFICULTY.md)). Combined client content is privately staged; genuine-client
acceptance remains unfinished. Isolated build and all 202 Dis City checks pass;
no deployment occurred.

Latest acceptance checkpoint: Trojan weapon arts now have actual first/second
rebirth and reincarnation rollback/reload/relearning coverage: 491 combined checks
pass. The twelve-entry client journal review overlay is prepared and passes seven
synthetic tests; it is unsigned/uninstalled. Private-map navigation and native
acceptance remain open ([review candidate](TROJAN_EPIC_CLIENT_REVIEW.md)).

Latest source checkpoint: Trojan Epic's full NPC route now has receipt-backed
trainer unlocks, catalog level checks, active dual-saber requirements and rebirth
loss rules for Fatal Cross/Mortal Strike. Build and 1699 combined checks
(1433 Trojan assertions) pass ([training evidence](TROJAN_EPIC_TRAINING.md)).
Candidate initialization/scheduling are now connected. Live Host and native
acceptance remain unfinished ([scope](TROJAN_EPIC_WORLD.md)).

Trojan's older storage and research checkpoints are superseded by the full
source/fixture route above. Native room links remain unverified; the legacy
four-boss entrance stays closed. See [storage](TROJAN_EPIC_STORAGE.md) and
[original research](TROJAN_EPIC_RESEARCH.md) for implementation history.

Owner clarification (2026-09-21): **remove direct daily quest P7 rewards only**.
One-time quests, event rewards and both Magnolia custom packs keep their bound
P7 souls. Daily Hunt, Rare Materials, Soul Ball, Release Souls, Magnolia's base
harvest, Exorcism and Worship retain their other completion rewards. Custom
Magnolia packs give one bound P7 soul, 5,000 CPs and 1,000 RVPs. Existing items
and completed receipts are preserved. See [current policy](DAILY_REWARD_POLICY.md).


Current clarification: custom-pack P7 rewards are restored; one-time starter,
epic and rebirth rewards remain unchanged. Direct daily P7 grants stay removed.
The earlier currency-only tooltip and experimental starter v3 journal are obsolete
and must not be deployed. No active client or live server was changed.
Build, 862 Magnolia checks, 1,388 gift checks and five tooltip tests pass.
See [validation](DAILY_REWARD_POLICY.md).

Historical binding batch (superseded for daily grants): quest P7 souls were made **bound**, including both Magnolia custom
packs. Build, 1,350 gift checks, 701 Monk checks and four tooltip tests pass.
Earlier unbound-soul milestone wording is superseded. Existing items are unchanged;
see [binding evidence](QUEST_P7_BINDING.md). No live deployment occurred.


Previous source batch, before the daily reward correction: Worship the Ancestors now commits a bound P7 soul,
original currency/Chi/EXP rewards and daily receipt together. Build and 520 checks
pass, including six ordinary statue routes, rollover, failures and fresh reload.
Native/live acceptance remains open ([evidence](ANCESTOR_WORSHIP_TRANSACTIONS.md)).

Previous source batch: Path Hunt now gives newly accepted hunts a bound
P7 soul and commits rewards/profile/progress together. Build and 552 checks pass,
including six old/new earned profiles, failed writes and unmasked fresh reload.
Courier's reload and autosave-race fixtures are now separated; 531 checks pass.
The composed private journal v2 includes Path Hunt's new reward; 17 journal checks
and hash-gated preparation pass. No client installation or live deployment.

The inventory now covers thirty-three source-validated families. Daily Envoy itself
is a hub for already-counted quests; Worship the Ancestors is now separately
covered ([audit](DAILY_ENVOY_REWARD_AUDIT.md)). Native validation, other
roadmap work and the disabled reset transaction remain open.

Previous source batch: Courier Loop now commits its bound soul, purse,
level puff, VIP and completion together. Build and 531 checks pass, including
first-rebirth prerequisites, three earned profiles, rollback and fresh reload.
VIP saved-value tracking protects claims and autosaves while preserving existing
lifetime/higher/longer benefits. Its verified route is Phoenix Castle; the older
Bird Island feature note is corrected. See [scope](COURIER_LOOP_TRANSACTIONS.md).
The installed mission 90002 journal still points to Bird Island. A composed
private correction now directs players through Cerys to Phoenix Castle and
labels both starter P7 rewards bound; 17 journal tests and hash-gated preparation
pass ([evidence](STARTER_JOURNAL_REVIEW.md)). Native acceptance and installation
remain open. No deployment occurred.

Soul Trial's owned transaction and three-stage earning regression suite passes
510 checks with the new VIP tracking ([scope](SOUL_TRIAL_TRANSACTIONS.md)). The reward inventory now contains
thirty-three source-validated families. Path Hunt and the Daily Envoy
coverage audit are recorded above. All 29 legacy soul candidate identities/P7 grades match both catalogs;
native purification compatibility is still open
([audit](LEGACY_STARTER_QUEST_AUDIT.md)).

The earlier shared progress-save prerequisite passed its build and 1,387 gift
checks, including 13 checks for consistent snapshots and failed-save retries.

Latest validated reward batch: new Elite PK tournaments add a bound P7 soul to earned top-eight
Envoy prizes. Build and the 637-check prize suite pass (including prerequisites),
with actual old/new bracket earning, exact rewards, fresh character reload and
soul-insertion rollback. A second eight-player lowest-level bracket now verifies
all new prize tiers independently of the old-contract bracket. Thirty-two completion families have source-tested
valuable rewards. Native display and release remain open.

Previous batch: new BP PK champions receive one bound P7 soul alongside original
CP/Chi and season tokens. Build, 104 BP checks and 185 shared PK regressions
pass, including all divisions, fresh character reload, older contracts and
expired-token claims. Twenty-eight completion families have source-tested
valuable rewards. Native acceptance, BP honor effects and live release remain open.

Previous batch: each winning spouse in new Couple PK tournaments receives one
bound P7 soul beside their existing purse. Build and 46 checks pass, including
ordinary pair combat, independent claims, rollback, both spouses' fresh character
reloads and older-contract preservation. Twenty-seven completion families have
source-tested valuable rewards; native/live acceptance remains open.

Previous batch: new Weekly, Monthly and Class PK champions receive a bound P7
soul alongside their existing purses and EXP Balls. Build, 65 Local PK and 120
Class PK checks pass, including earned results, rollback/replay, exact items and purses
through fresh character reload, and preserved older class contracts. Twenty-six completion families now
have source-tested valuable rewards. Native/live acceptance remains open.

Previous batch: new Power Arena sessions award permanent unbound five-star
Somersault Cloud mount armor at seventh-floor completion. Build and 106 checks
pass, including both reward choices, old contracts, concurrent claims, failures,
earned equipment and wardrobe/title eligibility through fresh character reload.
Twenty-three quest/event families have source-tested valuable rewards; native
acceptance and deployment remain open.

Previous batch: new Quiz Shows grant one bound P7 soul to each qualifying entrant
beside ranked EXP Balls. Build and 36 Quiz checks pass, including ordinary NPC
answers, exact item reload, rollback/replay and older-show preservation.
Twenty-two quest/event families now have source-tested valuable rewards; the
full inventory, native acceptance and live release remain incomplete.

Previous batch: new Summer daily hunts add one unbound Tortoise Breaker combat
rune alongside fragments. All 142 Summer checks pass, including 33 earned daily
claims, rune-bag admission, capacity, rollback, replay and old-contract handling.
An eligible ordinary character equips the earned rune, reloads it through fresh
character/item loaders and cannot repeat the claim. Twenty-one completion
families have source-tested valuable rewards. Native/live acceptance remains open.

Previous batch: first rebirth grants permanent unbound five-star Armor of Holy
Light, with its own atomic receipt. Build, 401 first-rebirth and 1,839 downstream
second-rebirth checks pass. Twenty completion families have source-tested
valuable rewards. Earned garment wardrobe/title/replay checks now pass (403 first-rebirth
checks), including a fixed replay crash. Native/live release remains open; this is not complete server-wide reward coverage.


Previous batch: Sereth's final second-rebirth rite now grants one unbound P7 soul
for earned Abyss completion or an Exemption Token. Build, 1,832 second-rebirth
and 1,788 Abyss checks pass, including atomic failure recovery and reconnect.
Nineteen completion families now meet the source reward standard; the full
inventory and native/live validation remain incomplete.


Previous batch: Monk and Ninja final weapon awakenings add one guaranteed unbound
P7 soul without replacing the player's weapon enhancements. Build, 701 Monk and
575 Ninja checks pass. Eighteen quest families now have source-tested valuable
rewards; remaining quests and native/live acceptance are still incomplete.


Previous reward batch: new Warrior epic contracts award two unbound level-140
EvilSlayer fists (+12, -7, two empty sockets). Build and 458 checks pass,
including equipment/reconnect and transactional failure recovery. This is the
sixteenth upgraded quest family, not full reward coverage. The Warrior client
journal overlay is privately staged with ten passing tests; native/live
acceptance remains open.


Updated 2026-09-20. **The overall roadmap is not complete.** This page summarizes
the authoritative [milestone checklist](QUEST_EVENT_ROADMAP.md); it does not
replace its evidence links or certify untested live behavior.

Flame Lit now has owned weekly runs, ordinary NPC/item interaction, atomic EXP
claims and achievement 11016. Its 93 checks include packet-driven travel through
the ten stones, normal gate destruction, reward reload, mentor credit and legacy
reincarnation recovery. Native-client/Host acceptance and additional rebirth
receipt cases remain open ([current evidence](FLAME_LIT_STORE.md)).

Latest completed source route: the recovered expansion Elimination rescue quest
for Georgia in Maple Forest. Its material/admission and distinct-plant gathering
rules pass 141 checks. Twelve staged herb sites and the summoning area have
connected terrain approaches with NPC footprints reserved; six synthetic terrain
checks pass. Daily admission, the exact twelve-herb exchange and lost-material
recovery, expired-material cleanup, saved rescue admission and private actor
bindings/private travel/combat now pass 195 store/interaction checks. The earned
route covers Georgia, herbs, spice, Behemoth, Landy, Fire Pills, six den lackeys,
RockLeader, return, one EXP-ball unit, 50 Study Points and achievement 11010.
Den travel, saved kill recovery and final reward rollback/reload are tested;
42 shared combat regression checks pass. Original den 1951 has no installed
binding; the private mine layout on base 1021 is an explicit Revora adaptation.
Startup/tick/item-use integration and simulated interrupted-location recovery
are implemented. Real Host-restart and native-client acceptance remain open.
The [consolidated acceptance checklist](ELIMINATION_ACCEPTANCE.md) records the
remaining real-client and Host checks. The global ReleaseReady gate keeps normal activation disabled
([store](ELIMINATION_STORE.md)). The
similarly named historical events remain separate
([recovered guide and evidence](ELIMINATION_IDENTITY_RESEARCH.md)).

New owner priority: [valuable rewards for every completed quest](QUEST_REWARD_STANDARD.md).
The custom Magnolia packs and guaranteed P7 rewards for newly admitted, fully
completed Elimination/Flame Lit runs are implemented in source. Daily Hunt and
Rare Materials now add guaranteed unbound P7 souls for new contracts, with
547 and 559 checks passing respectively. New Sky Pass and Snake Islands runs
also guarantee an unbound P7 soul, with 40 and 28 route checks passing. Release
Souls and both successful Moon Box endings now add the same guaranteed reward
for new runs, with 504 and 34 checks passing. Soul Ball now adds a guaranteed
unbound P7 soul for new contracts as well, with 610 checks passing. Anniversary
now adds an unbound P7 soul to new bead rewards, with 521 checks passing. Magnolia
now guarantees a harvest soul independently of bonuses, with 853 checks passing.
Ancient Devil adds the same guaranteed soul for new summons, with 327 store and
278 boss-world checks passing. Dis City now adds a final-victory P7 soul for
new expeditions, with 179 integration checks passing. Labyrinth now adds an
unbound P7 soul to new Cloud Diamond exchanges, with 46 checks passing. Daily
Exorcism adds the guaranteed soul for new hunts, with 619 checks passing. The broader
weapon/P7/five-star/rune reward upgrade remains incomplete across other quests.

Current source coverage is **240/337** achievements, with **97** lacking trusted
producers. These are source counts, not overall completion or live acceptance.

| Area | Current status |
| --- | --- |
| Achievement/reward foundation, local CTF and Elite PK | Source and automated routes recorded; genuine-client acceptance remains open. |
| Classic quests, daily hunts, growth/class progression and Guild seasons/Grail | Extensive server work is implemented and tested. Remaining earning/parity gaps and native acceptance are still listed under M4–M7. |
| Magnolia | 20% bonus policy; version-four contracts have 41 audited outcomes. Both custom packs grant a random P7 soul, 1,000 RVPs and 5,000 CPs. Earned opening/reload validated; native acceptance remains open. |
| Normal lottery | Owned exchange, payment, pending-result recovery, two rerolls and once-only claims implemented. All 191 configured prize rows survive claim/reload tests. Native presentation and original box/odds parity remain open. |
| Homecoming and social campaigns | Research/prerequisites exist; campaign eligibility, claims and several original requirements remain unfinished. |
| Genuine cross-server competitions | Realm coordination, ownership, result delivery and awards remain unimplemented roadmap work. |
| Remaining historical rewards | Poker tournament, Fashion Icon, Euro Planet and Honored Hero requirements/earning remain open. |
| Live release | This latest quest/reward work has not been deployed. Full genuine-6711 journeys, relevant Host restart checks and release gates remain pending. |

Ancient Devil's class trials, legitimate amulet collection/trade, nine-amulet
summoning, guarded boss encounter, rewards and achievement 11020 are connected
in source. Startup/recovery checks pass, and private native catalog probes cover
the boss flag and guard profile. The active client is unchanged; full rendered
journeys and real Host restart acceptance remain open
([lifecycle scope](ANCIENT_DEVIL_LIFECYCLE.md)).

Housing, furniture storage, friend visits and mount breeding have server earning
and persistence evidence recorded in the milestone checklist. Their genuine-client
acceptance remains distinct from automated validation. Decorative furniture now
costs 2,500 CPs per piece per owner instruction; 447 housing/storage checks pass.
See the checklist for exact
scope and limitations rather than adding test counts across overlapping fixtures.

No latest quest/event source batch has been deployed to the live Host. Release
readiness still requires the documented client, Host and coordinated patch gates.
