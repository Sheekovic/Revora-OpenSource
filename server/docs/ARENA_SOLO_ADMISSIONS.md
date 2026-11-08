# Staged solo Arena admission reservations

Solo import now has a durable reservation seam before either player transfers.
When joint Arena startup has initialized the staged admission table, the existing
admission guard locks both original actors and their entity rows, checks pending
solo/team ownership, inserts the original pair and commits before ImportCore.
Failed reservation writes leave the import latch and player maps untouched.

The reservation uses the group's existing ParticipationKey. Before the match
ledger adopts it, both participants count as pending through ArenaMatchLedger's
shared check, including cross-mode admission and consumers of that guard. Ledger
start validates the exact pair and rejects a missing required reservation or an
abandoned one. Historical groups without the new reservation marker retain the
existing migration behavior. An adopted reservation remains historical evidence;
the match ledger then owns result/abandonment eligibility.

Joint cold startup initializes the table and closes reservations without a match
ledger row in batches of 128. A final read verifies no interrupted reservations
remain before preparation continues. Existing ledger rows are preserved for their
normal result recovery. No result, participation reward or win/loss is granted
for merely reserving or abandoning an admission. Stale actors cannot later start
a reservation that startup abandoned.

The new table is `revora_arena_admissions`; production initialization is not wired
to Server.Run. Preserve this table once activated rather than reverting to a
writer that ignores pending reservations. This batch does not deploy a schema.

The fixture exercises real import rejection from a database constraint, successful
retry, exact-pair reservation, both-owner pending checks, replayed import, ledger
adoption, cold abandonment and rejection of a stale post-abandonment start.

Remaining work includes bounded retention-capacity reservation/attachment before entry can race
with disconnect, recurring ticks, shutdown drain and coordinated legacy writer
cutover. Cold fixture recovery is not a full Host restart or native transfer test.
The whole Arena lifecycle milestone remains open.

Validation: isolated build and 678 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/solo-admission-build.log` and
`obj/chi-refill-validation/solo-admission-tests.log`.

## Interrupted transfer cancellation and cleanup

Reserved solo imports now contain transfer exceptions and distinguish an
interrupted admission from a played result. An interrupted transfer cannot freeze
a winner or loser. Both original owners are locked and checked before the
reservation is marked abandoned in a transaction; a ledger-adopted match cannot
be cancelled through this path. The saved cancellation is verified before its
in-memory marker permits cleanup.

Cleanup uses the existing owned-player/map path, restoring the transferred
player's pre-entry PK mode, clearing original invitation ownership and releasing
the map. Busy, failed-write or ineligible-owner cleanup remains retryable through
the bounded completion pump. Replayed imports cannot consume another reservation
or re-enter a cancelled match. Cancellation itself awards no points, statistics,
participation or result rewards.

Tests cover an actual map-creation exception, simulated partial transfer, an
active-match cancellation rejection, a busy original owner, suppressed database
updates, pump retry, player return, map cleanup and replay without rewards.
Simulation of a partial transfer is not native network interruption acceptance.
Automatic retention attachment, full shutdown drain, coordinated live activation
and native transfer/reconnect validation remain open.

Validation: isolated build and 686 combined Arena checks pass. The fixture also
checks PK-mode restoration and rejection of a result before reserved entry is
complete. Ignored logs: `obj/chi-refill-validation/solo-cancel-build.log` and
`obj/chi-refill-validation/solo-cancel-tests.log`.

## Participation-write failure and Arena cache ownership

Entry's participation receipt still has its own transaction. A failure there now
uses ArenaCacheRecovery for both original players, marking their solo and team
statistics as requiring reload and removing those exact references from shared
ranking caches. The previous generic quest fence disabled player actions but
left the Arena cache references unmarked, which does not satisfy the fenced
result-recovery ownership contract.

The fixture forces failure on the second participation receipt. It checks that
both receipts roll back, both players are disabled, both loaded statistic
references require reload and both shared caches drop the stale entries. Its
existing transaction-retry check resets fixture state; it is not a native
reconnect test. The generic cache helper's exact-reference removal continues to
protect replacement sessions.

This is a prerequisite correction, not completed automatic retention attachment.
Entry still needs a composed owner that reserves bounded retention capacity
before transfer, coordinates match-ledger adoption with participation, retains
disconnects throughout entry and owns recurring cleanup/shutdown work. No live
activation is included.

Validation: isolated build and 688 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-entry-fence-build.log` and
`obj/chi-refill-validation/arena-entry-fence-tests.log`.

## Atomic registered entry

An explicit RegisterLedgerOnEntry setting on a match selects the staged entry
transaction. Live match creation does not set it. Registered import requires
completed joint startup preparation, initialized participation storage and the
ordinary level/invitation/fight eligibility checks. Eligibility is checked again
under the admission guard's original-owner locks before reservation and transfer.

For a successfully transferred registered pair, participation now adopts the
reservation into the match ledger inside the same transaction that records both
first-participation receipts and achievement evidence. The extracted
StartInTransaction operation validates the parent transaction, match ownership,
pair, map, reservation and open ranking day. The existing standalone Start still
owns and commits its own transaction through that shared implementation.

Only the combined commit marks ParticipationRecorded and refreshes achievement
views. A missing/suppressed ledger insert or failed second participation receipt
rolls back all newly written entry evidence. The pre-transfer reservation remains
pending and both Arena cache references are fenced for recovery. A previous
first-participation receipt remains historical evidence; repeated successful
entry does not rewrite or duplicate it.

Fixtures invoke actual registered imports on ordinary players and verify one
ledger plus both achievements, replay, a suppressed ledger write and failure of
the second receipt. They also reject under-level entry and missing participation
initialization before transfer. They release fixture registrations explicitly;
this does not demonstrate full reconnect or Host restart.

Automatic retention-capacity reservation, ownership across entry-time disconnects,
recovery of failed entry transactions while the process remains running, and
coordinated runtime activation/native acceptance still remain open. The registered
path is a composition seam, not an enabled player-facing release.

Validation: isolated build and 696 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-entry-transaction-build.log` and
`obj/chi-refill-validation/arena-entry-transaction-tests.log`.

## Failed registered-entry recovery

Registered participation failures now retain an EntryRecoveryRequired marker.
Before ordinary result completion, the bounded pump locks the original match
and canonical fenced actors, then examines persisted entry evidence under the
entity-row locks. An incomplete registered entry cannot freeze a fight result.

If the match ledger exists, its pair/map must match, the reservation must remain
valid, and both participation/achievement records must exist. Recovery recognizes
that entry without rewriting or duplicating its evidence. Both sessions remain
fenced; the ordinary result recovery path still owns any subsequent settlement.
Missing committed evidence keeps recovery pending rather than cancelling a
potentially earned match.

If no ledger row exists, the entry transaction rolled back. Recovery cancels the
reservation through the verified abandonment write, without creating a result or
new participation reward. The pump retains map cleanup until fenced original
registrations release. Attached solo shutdown retention now recognizes durable
admission cancellation as permission to resume teardown, as team retention does.

The fixture covers both write failures from actual registered imports, rejection
of replaced sessions, suppressed cancellation updates, pump retry and post-release
cleanup. A simulated post-commit projection failure verifies that complete saved
entry survives, while temporarily missing evidence prevents recovery. The fixture
restores its deliberately removed row before proceeding; it does not infer a
production repair policy or simulate native transport delivery.

Automatic retention ownership before transfer, capacity reservation, runtime
scheduling/shutdown and native acceptance remain open. This batch does not
activate the registered path on the live server.

Validation: isolated build and 708 combined Arena checks pass. Recovery locks
durable character identities directly after verifying fenced ownership; it does
not invoke the normal gameplay persistence helper that rejects invalidated
sessions. Ignored logs: `obj/chi-refill-validation/arena-entry-recovery-build.log`
and `obj/chi-refill-validation/arena-entry-recovery-tests.log`.

## Owned solo entry

`ArenaDisconnectRetention.Import` is the staged entry owner. It acquires the
match and original sessions, reserves bounded retention capacity, and installs
both shutdown owners before invoking registered import. Capacity exhaustion
rejects entry before database reservation or player transfer. Callers must keep
ticking the same retention instance even when Import returns false: it can still
own failed-entry recovery.

A departure during preparation retains its original actor, reconnect barrier,
and first observed UTC time without consuming the one-shot shutdown gate. A
pre-commit departure rolls back registered entry evidence. The completion pump
then cancels the reservation durably and releases fenced actors before map
cleanup. A committed entry hands off to ordinary result completion; a retained
departure is forwarded with its original timestamp. Rejected, unimported
invitations remain the matchmaker's responsibility after retention releases them.

Fixtures use actual registered imports on ordinary players and verify capacity
rejection, normal saved disconnect settlement, healthy-opponent preservation,
and reentrant departures at reservation and ledger-start boundaries. Both failed
entry cases drain without a fight result or participation receipt. The injected
clock callback exercises shutdown ownership, not native socket delivery; resume
callbacks exercise the one-shot gate and exact registration release rather than
the full ancillary shutdown path.

Validation: isolated build and 718 combined Arena checks pass. Ignored logs:
`obj/chi-refill-validation/arena-owned-entry-build.log` and
`obj/chi-refill-validation/arena-owned-entry-tests.log`. Live matchmaker/startup
composition, team entry parity, coordinated shutdown and native acceptance stay
open. No production schema changes, deployment or restart occurred.
