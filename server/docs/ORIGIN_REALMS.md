# Origin realm foundation

## Owner scope decision ? 2026-10-04

The owner selected **local Revora tournaments only** for the roadmap finish line.
The implemented local CTF, Elite PK and regular Team PK editions remain required,
with native acceptance and release pending. Genuine cross-server transport is
**deferred outside this finish scope**, not implemented or certified. Historical
statements below retaining genuine M9 as a finish requirement are superseded by
this explicit decision. Existing origin groundwork is preserved.


This is the M9 prerequisite for genuine cross-server competitions: distinct
origin realms, canonical identity, exclusive session ownership and
authenticated admission. It does **not** run a cross-server tournament and
does not itself grant wardrobe titles. Origin-qualified CS-CTF awards are a
separate producer ([ORIGIN_CTF.md](ORIGIN_CTF.md)). Origin-qualified CS Elite
PK awards are a separate producer ([ORIGIN_ELITE_PK.md](ORIGIN_ELITE_PK.md)).
Origin-qualified CS Team PK awards are a separate producer
([ORIGIN_TEAM_PK.md](ORIGIN_TEAM_PK.md)).

Retail cross-server rewards need independent origins that compete through a
coordinator. Two maps in one realm are not a cross-server win. This local
Revora foundation stores origin identity as `(realm, character number)` so
the same numeric uid on `origin-a` and `origin-b` are different people.

## Durable contract (2026-09-22)

- Realms register by token (`origin-a`). Replay of the same realm is idempotent.
- Identities register once per `(realm, uid)`. A later trusted local name for
  that same uid updates the stored name; the identity remains the uid.
- `OpenSession` creates one live session per identity. A second owner is rejected.
- `Reconnect` returns the same session id only to the owning process.
- `CloseSession` requires that owner. A later open is a new session.
- `Admit` requires the live owning session. Replaying the same event does not
  insert a second admission.
- `RecordReceipt` requires admission, is unique per `(event, realm, uid)`, and
  does not write wardrobe titles. A failed insert rolls back that receipt.

No extra public bind, CrossClient socket or live-server restart is part of
this slice.

## Tournament coordination (2026-09-22)

`OriginTournaments` coordinates a run across distinct origins:

- `OpenRun` stores a durable coordinator owner.
- Enroll requires the live owning origin session and records that owner.
- Start and finalize require the coordinator owner. A foreign owner is rejected.
- Start requires at least two origin realms on the roster, then freezes it.
- Additional enrollments after start are rejected.
- Finalize writes one placement per frozen entrant and one outbox row each.
  Replay does not rewrite placements. Payload is `placement:N`, not a wardrobe
  title.
- Ack requires the current live session owner for the delivery's exact realm and
  character. A replacement process can acknowledge a pre-restart result; the
  former owner and other origins cannot. Replay is idempotent.
- Schema upgrade from pre-owner tables is fail-closed: complete run, entrant,
  placement and outbox rows are quarantined with owners left unset, then live
  coordinator/owner columns become NOT NULL. Quarantined rows cannot be started
  or acknowledged.

Origin-qualified CS-CTF awards are a separate producer
([ORIGIN_CTF.md](ORIGIN_CTF.md)). Origin-qualified CS Elite PK awards are a
separate producer ([ORIGIN_ELITE_PK.md](ORIGIN_ELITE_PK.md)). Origin-qualified
CS Team PK awards are a separate producer
([ORIGIN_TEAM_PK.md](ORIGIN_TEAM_PK.md)). Live inter-realm battle remains pending.

## Socket origin bind (2026-09-22)

`OriginBind` attaches a logged-in character to this process's origin realm on
the existing game login socket. It does not add a public bind or CrossClient
port.

- `Configure` registers the realm token and acquires an exclusive database-session
  lock before recovering this realm. A competing live owner is refused without
  closing its sessions. An invalid token disables the bind and releases the lock.
- Login registers `(realm, uid)` if needed, reconnects an owned session or
  opens one, then authenticates CS-CTF and CS Elite PK delivery. A rename of the same uid
  updates the stored name and still binds. A foreign live owner or invalid
  realm is skipped (fail-closed, login continues). Database errors are not
  treated as a successful bind.
- Logout clears the wardrobe bind and closes this process's session unless
  another live GameState still holds the same uid.
- Restart recovery replaces the registered process owner and closes its old
  sessions in one transaction, after in-flight session operations finish. Other
  origins remain untouched. Stale owners cannot reopen sessions after takeover.

Native Host restart acceptance and deployment remain pending.


## Owner edition decision (2026-09-30)

The owner selected the recommended local Revora tournament editions for the ten
remaining cross-server wardrobe rewards. Implement actual local competition and
durable results under explicitly named Revora rules; do not claim that origin
tokens or the existing result foundation prove an inter-realm battle. The local
earning paths were subsequently implemented and validated; see
[local tournament evidence](LOCAL_TOURNAMENT_WARDROBE.md). Existing
native/deployment gates remain open. The unanswered-choice statement below is
historical and is superseded by this decision.

## Battle transport review (2026-09-27, source e6584d88)

A targeted source review found no production caller for tournament open/start/
finalize, team formation or origin award capture. The files under
`Sistemas/CrossClient/` contain commented-out legacy sketches, not an enabled
inter-realm transport. Current login binding and result storage do not establish
independent gameplay origins or produce battle-earned placements.

The owner has been asked whether to implement genuine multi-realm competition
or documented single-server Revora editions. That unanswered topology/product
choice must not be inferred from two test realm tokens or from existing unit
fixtures. Until resolved, the ten cross-server wardrobe routes remain
predicate-only, and no public listeners or cross-server rewards are enabled.

## Current implementation boundary (2026-10-03, source36c52c92)

A fresh production-source caller review confirms the2026-09-27 transport finding.
The schema/result methods below exist, but ordinary gameplay does not call the
open/start/finalize/capture sequence:

| Missing production connection | Existing contract |
| --- | --- |
| Open and admit a real multi-origin competition | `OriginTournaments.OpenRun` / `Enroll` / `Start` |
| Derive final placements from shared battle results | `OriginTournaments.Finalize` |
| Capture earned origin wardrobe awards | `OriginCtf.Capture`, `OriginElitePk.Capture`, `OriginTeamPk.Capture` |
| Transfer participants to battle and recover their origin return | No enabled transport; `Sistemas/CrossClient/CrossClient.cs` is a commented legacy sketch |

The M9 checkmarks now explicitly describe award storage, and its open requirements
include implementing the missing battle/admission/return connections before
verifying them. Two database realm tokens or successful foundation fixtures do not
establish this path. The current ten local earning routes remain completed within
their documented private validation scope; none is reopened by this review.

No runtime, deployment or infrastructure change was made by this review. It does
not establish that a second live realm exists, that endpoints are reachable, or
that an independent origin can hand off a genuine6711 client. Those need actual
infrastructure and transport evidence. The original M9 scope remains open.


## Exclusive recovery and stale-owner fencing (2026-10-03)

The previous startup recovery closed all foreign-owner sessions in its realm
without proving the other process had stopped. The new contention regression
fails that build at session preservation; the corrected private build passes.

`OriginBind` holds a non-pooled PostgreSQL connection with a session advisory
lock for the configured realm. A second connection cannot acquire that lock.
Recovery runs on that same physical connection, so losing it cannot silently
recover through a different pooled connection. The database releases its lock
when the connection terminates. Reconfiguring the same owner preserves its
existing connection and sessions.

Recovery takes an exclusive transaction advisory lock before atomically changing
`revora_origin_processes.owner` and closing old sessions. Session open, reconnect,
close and owned-session checks take the shared counterpart and reject a different
registered owner. This includes first registration, when no owner row exists yet.
It prevents an in-flight old-owner write from appearing after completed recovery.
Unconfigured realms retain the existing foundation contract; this is not an
internet-facing realm-authentication mechanism.

Private validation: **159 checks** across origin identity, coordination, CTF,
Elite PK and Team PK. New coverage includes competing lock refusal without session
mutation, other-realm preservation, stale-owner refusal, blocked takeover until an
in-flight transaction finishes, idempotent configuration, broken-connection
recovery refusal and successful subsequent takeover. The test explicitly kills
only its own lease backend in the disposable database. Existing result delivery,
replay, rollback and expiry checks still pass.

Reproduce with `tests/OriginRealms/Verify-OriginRealms.ps1` against the already
running private candidate database. The runner accepts `-ArtifactRoot` and
`-RuntimeRoot`, uses the protected runtime credential, and rejects database
endpoints outside the configured loopback candidate. It creates/drops only its
validation schema; it does not start a game listener or alter career checkpoints.

Deployment remains pending. All older processes for the same realm must be
stopped during rollout because they do not implement this lock protocol. The new
process-owner table is additive. No live service or native client was changed,
and production battle/admission/return transport remains unimplemented.

[Build and regression evidence](research/ORIGIN_RECOVERY_OWNERSHIP.json).


## Result acknowledgement after origin restart (2026-10-04)

The old `Ack` trusted the process UUID frozen into the outbox at enrollment. A
restart therefore left the replacement process unable to acknowledge its result,
while the former owner could still consume it after losing its character session.
The regression reproduces that stale-owner acceptance on the previous build.

Acknowledgement now resolves the delivery's immutable `(realm, uid)`, takes the
run and identity locks, and requires its current owned session. The realm-owner
check also participates in the recovery transition lock. Successful acknowledgement
records the current owner and timestamp once; replay leaves the complete delivery
row unchanged. If an update is suppressed while the row is still pending, it
throws instead of reporting an idempotent replay.

Private validation passes **165 checks**, including old-owner refusal, replacement
acknowledgement, other-origin refusal, suppressed-update recovery, exact-row replay
and two simultaneous acknowledgements committing once. Existing origin, result,
CTF, Elite PK and Team PK tests remain green. Frozen entrant identity and placement
are unchanged; acknowledgement itself does not grant wardrobe rewards.

This is a database-backed restart model, not an independently connected realm or
native battle test. No live service, career checkpoint or installed career runtime
changed. The cross-origin transport and competition-driven caller remain open.
[Exact evidence](research/ORIGIN_RESTART_ACK.json).

## Conflicting final-result retries (2026-10-04)

Previously, `Finalize` returned the same replay result for any syntactically valid
submission after finalization, even if it named a different champion or roster.
The stored winner was protected from overwrite, but a recovering coordinator
received no indication that its submitted result disagreed with the saved one.

Finalized retries now compare every origin-qualified identity and placement under
the existing run lock. Only the same complete result returns the idempotent false
value; conflicts throw without touching the run, placements or outbox. Numeric
UIDs are normalized before comparison, so equivalent ordering/zero-prefix changes
are harmless. Existing rank-range and uniqueness validation remains intact.

The previous build fails the conflicting-winner regression. The corrected private
build passes170 checks, including exact saved-state comparisons and simultaneous
conflicting submissions: one complete result commits and the other is rejected.
No production coordinator caller currently needs migration. A future caller must
surface a conflict instead of treating it as successful delivery.

This closes a result-consistency defect, not the missing cross-server transport
or native competition acceptance. No live deployment or career-runtime replacement.
[Build, regression and scope](research/ORIGIN_RESULT_REPLAY.json).
