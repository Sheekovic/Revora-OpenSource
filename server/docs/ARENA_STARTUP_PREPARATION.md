# Arena startup preparation

ArenaStartupRecovery.PrepareStartup is the staged startup sequence for the durable
Arena lifecycle. It first requires empty session, match and solo/team statistics
registries and absent listener objects. It initializes coordinator schemas plus
the Pacific rollover schema, recovers or abandons interrupted admissions in
128-record batches, checks finished-result bundle completeness, then settles
missed ranking days in 31-day batches. Only a final successful startup guard sets
Prepared. The configured budget is 1?100 batches for each phase; exhaustion throws
and leaves Prepared false. Committed earlier batches remain available for retry.

A failed ranking write cannot erase recovered results, but it cannot declare
startup prepared either. A finished ledger missing its bundle marker or either
participant statistic receipt fails closed, including for an already-sealed day.
No outcome is inferred for an uncaptured interruption. Pending prizes remain
pending until ordinary player delivery; preparation does not pay them itself.

ArenaDayRollover.Managed now recognizes durable result statistics as well as a
rollover receipt. This prevents legacy Arena loading/reset from wiping recovered
points and counters before the player's first managed rollover. This ownership
check requires coordinator schemas before the rollover schema is used, as done
by the new preparation sequence.

`Server.Run` now calls this method after loading the item catalog and before
constructing workers, loading Arena caches or opening Account/Game listeners.
The composed-startup section below records actual isolated process validation.
Native and live-fighter restart acceptance remain open; the earlier fixture
counts below retain their original scope. Prepared is the runtime selection
boundary for owned admission/results, not a deployment certification.

Disposable-schema tests simulate empty startup registries, not an actual process
restart. They cover live-registry rejection, recovery followed by a failed ranking
write, retry/replay, stale point-fill loading without a rollover receipt, incomplete
finished bundles, a 129-admission batch boundary and invalid budgets. See
QUEST_EVENT_STATUS.md for the passing count. Ignored log:
obj/chi-refill-validation/arena-startup-preparation-tests.log.
No listeners, production schema, live deployment or client files were changed.

## Cold-process catalog prerequisite (2026-09-21)

The disposable candidate probe exposed a missing startup prerequisite hidden by
the broad fixtures: `Prepare` invokes reward initializers that require
`ConquerItemInformation.BaseInformations`. Without the catalog, preparation
previously reached a null reference. It now rejects a missing catalog explicitly
before initializing/recovering Arena stores and leaves `Prepared` false.

At the time of that probe, `Server.Run` constructed/initialized `Thread` before
loading the item catalog, then loaded Arena caches afterward. The ordering fix
below moves the single catalog load ahead of workers. Joint preparation must
still be connected between catalog loading and worker creation during cutover.

`tools/Verify-MoonBoxDatabase.ps1 -CheckArenaStartup` now starts a separate x86
process for each of two isolated PostgreSQL lifecycle cycles. The helper requires
the fixed loopback candidate database, its non-superuser runtime role and empty
character/Arena tables. It checks the missing-catalog refusal, loads the real
candidate catalog, calls joint preparation twice, and invokes both production
Arena cache loaders. All checks passed; no sessions or Account/Game listeners
were created, and the database was stopped afterward.

Evidence: `obj/chi-refill-validation/arena-cold-process-build.log` and
`obj/chi-refill-validation/arena-cold-process-tests.log`.
Tested assembly SHA-256:
`C30A697FC2D5DD2318006458BD7922A2A7FEE223AB1FEB1BAFF3C2FE037F1668`.
The build includes the separately owner-modified `System/IniFile.cs`; this is not
a clean-source release. The helper loads the validation assembly, not the older
game executable recorded in the private runtime manifest. Existing gameplay
regression evidence was reused; the entire Arena suite was not rerun for this
catalog preflight.

This is cold composition/database evidence, not a full Host startup or restart
with ordinary fighters. That probe did not activate joint preparation; the composed-startup section below
supersedes that integration state.
The operator drain described below now precedes disconnect/exit when joint
preparation is active. Full Host shutdown/restart acceptance remains a cutover
prerequisite. Native entry, play, reward delivery and postmatch return acceptance
remain open.

## Operator shutdown drain (2026-09-21)

When joint preparation is active, `@close` and `@restart` close listeners and
request a bounded five-second Arena drain before disconnecting players or
exiting. If work remains, the process stays alive, admissions remain closed and
the operator receives an explicit instruction to retry. An unprepared server
retains its existing shutdown behavior; this change does not activate Arena.

Admission leases cover ordinary signup, matchmaking, owned imports and watcher
entry. The drain closes new leases and waits for existing leases before removing
queued non-fighters. Active matches keep their normal combat/timeout outcome;
shutdown does not turn player-disconnect iteration order into wins and losses.
Retained completion pumps save results and clean maps/sessions. Empty match,
queue, withdrawal and retention state is required before reporting success.
Scheduled work remains visible while dequeued and executing.
One retained worker prevents overlapping drain attempts if a database operation
outlasts the caller's wait. A timed-out worker is not killed; the process stays
available for recovery and a later operator retry.

The isolated build and 1,102 combined Arena checks pass. Component validation
covers the admission barrier, queued withdrawal, ordinary
solo/team matches, refusal of signup/watchers after shutdown, completion and
replay. This does not certify all non-Arena asynchronous disconnect work, an
actual Host exit with fighters, startup activation, or native client behavior.
Evidence logs: `obj/chi-refill-validation/arena-shutdown-build.log` and
`obj/chi-refill-validation/arena-shutdown-tests.log`.

Validation includes the existing owner modification in `System/IniFile.cs`;
that file is excluded from this commit. No live release was produced.

## Catalog before workers (2026-09-21)

`Server.Run` now loads the item catalog once, immediately before constructing and
initializing `Thread`. Catalog failure therefore precedes worker startup. Joint
Arena preparation now also rejects an existing `Server.Thread`; it must run
before timers can observe partially prepared stores or perform Arena resets.
The cold helper tests this guard with an uninitialized owner sentinel, without
launching timer pools, then removes it and verifies successful preparation.

The isolated build passed. One database lifecycle cycle ran the cold Arena
helper and the actual updated game executable. The game opened only its expected
loopback listeners, accepted `@close`, exited zero without a forced stop, and
left no candidate game/database listeners. The log contained the known absent
`identity.cp_orders` and `identity.xtremetop_votes` integrations; those services
are not included in the disposable database.

Evidence: `obj/chi-refill-validation/catalog-startup-order-build.log` and
`obj/chi-refill-validation/catalog-startup-order-process.log`. Tested executable
SHA-256: `D8789285418D8A7B899A144B7513740D290B601AF64B5A23E7ADEEBA20AB60BE`.
The private runtime manifest records source changes and preserves its previous
executable/manifest. The build includes the owner's separate `System/IniFile.cs`
change, so it is not a clean-source release. No production files changed.

This startup smoke test had no fighters and did not activate joint Arena in
`Server.Run`. It does not certify prepared Arena shutdown with live sessions or
native gameplay. The earlier 1,102-check gameplay evidence is reused; no broad
Arena suite was rerun for this ordering change.

## Composed startup activation (2026-09-21)

`Server.Run` now calls `ArenaStartupPreparation.Prepare(100)` immediately after
the single item-catalog load. Recovery must succeed before `Thread` construction,
either Arena cache loader and either Account/Game listener. There is no fallback
to the legacy result path after preparation failure. The bounded recovery limit
uses the existing maximum of 100 passes; exceeding a recovery postcondition
fails startup rather than ignoring pending results.

The startup emits a non-secret preparation marker and a prepared-cache marker.
`Check-CandidateGameStartup.ps1` requires exactly one of each in the correct
order, in addition to its loopback listener and normal-close checks. Combining
`-CheckArenaStartup -CheckGameStartup` now runs two database lifecycle cycles.
Both cold helpers and both actual updated game processes passed. Each game
reached its three expected loopback listeners and exited zero through `@close`,
without a forced stop; all candidate listeners were gone afterward. The known
missing payment/vote tables remain absent from this disposable database.

Build/process logs: `obj/chi-refill-validation/arena-composed-startup-build.log`
and `obj/chi-refill-validation/arena-composed-startup-process.log`.
Tested executable SHA-256:
`2959C0462A6AE79E0A1A4560263B42BB6D4E117C48CC95DBE80702EFBE927A6D`.
The private runtime manifest records the build/source changes and preserves its
previous executable/manifest. The owner's `System/IniFile.cs` modification is
included in this test build, not committed as part of this change.

This activates the ordinary owned Arena routes in the candidate source. No live
server or client was updated. The actual process tests used an empty character
catalog and had no fighters, so they do not prove in-flight match shutdown,
reward recovery with ordinary sessions or native gameplay. Existing 1,131 Arena
and 495 Experience checks remain separate focused evidence, not proof of those
combined lifecycle gates. The remaining release checklist is ARENA_CUTOVER.md.
