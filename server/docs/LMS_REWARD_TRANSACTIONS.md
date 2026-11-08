# Last Man Standing claim transactions

## Fresh five-player calculated victory and cold prize (2026-10-03)

The fresh main UID1096322 and four existing ordinary contributors continue from
the completed Duel Pit checkpoint. All five have their earned profiles and
possessions, are alive and have no pending LMS claim. Normal NPC registration
admits the roster to the composed private arena for cycle `204104221200`.

Four `Attack.HandleAttack` melee attacks eliminate the four lower-level
opponents. The fixture supplies no damage value, death, score or selected
winner. The event records one champion and one unclaimed entitlement. All five
admissions end and return to Twin City with their original PK modes. Defeated
players then use ordinary revival, with only its eligibility timestamp advanced;
this leaves them alive for meaningful non-winner claim refusal checks.

A separate game/database process loads the unpaid result. The winner's normal
NPC claim pays8,000 CPs,25 RVPs,750,000 silver and exactly one random bound P7,
retaining every prior bag UID. The four non-winners receive nothing. Repeated
claims leave the complete snapshot unchanged. Another cold process verifies the
exact saved reward UID and binding, every player's loaded physical bag against
the database, zero remaining claim markers and unchanged state after replay.
The full prior career, all five profiles/items/vaults, selection, victory,
offline-prize rows and admissions remain included in the snapshot.

`career-fresh-lms-earn`, `career-fresh-lms-claim` and
`career-fresh-lms-reload` all pass on their first execution. The authoritative
checkpoint is now `revora_fresh_lms_probe`, stage2, using
`FreshLmsSnapshot(p, players)` with the same roster order as Duel Pit:
1096324,1096325,1096326,1096323,1096322. Earlier checkpoints are historical after
this advancement. Source: `tools/CheckCandidateFreshLms.cs`.
Redacted logs:
`C:\Revora\DisposablePostgres\moon-box-candidate\arena-process-career-fresh-lms-*.log`.
Unchanged private game SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

Calendar, registration/peace waits, attack stamps, NPC proximity and sequential
attacker actions are controlled. Opponents are not modeled as competing human
fighters. This is calculated server earning and persistence evidence, not a
natural tactics/balance test,32-player load test or genuine6711 acceptance.
No production code, live restart, deployment or client patch changed.

## Bound P7 champion reward (2026-09-21)

Every saved, unconsumed LMS victory now pays one random bound P7 soul alongside
the configured CP/silver/RVP purse. This applies to existing pending victories
and recovered offline victories; completed claims remain closed. It is an event
reward, not a daily quest payout. Registration/result text advertises the soul.

The claim requires one free inventory slot. The item insert, three currencies
and entitlement decrement share one database transaction. A suppressed insert
rolls everything back; projection occurs only after commit, with the existing
reload fence on failure. Multiple queued victories each consume one entitlement
and award one soul.

Build and 109 focused Local PK/LMS checks pass, including full inventory,
entitlement/item-write rollback, concurrent claims, multiple wins, an ordinary
fighter's actual elimination/claim route, exact item UID and binding after fresh
reload, and replay rejection. Logs: `obj/chi-refill-validation/lms-soul-build.log`
and `lms-soul-tests.log`. These are in-process ordinary-player fixtures; native
display, authenticated process lifecycle and deployment remain open.

The following sections record earlier implementation evidence.

2026-09-21. Source change only; not deployed or native-client accepted.

Previously `TryClaim` cleared `lms-claim`, changed CPs, silver and RVPs in memory,
then saved quest variables and the character separately. Failure could consume
the entitlement without a complete saved payout, or leave partial currency state.

The claim now requires the canonical ordinary player and holds the character
item lock plus variable-vault lock. It snapshots the configured prize, checks
all purse limits, and reads the saved entitlement under a transaction/row lock.
Only an unconsumed saved entitlement can authorize payment. All three currency
updates and the variable-vault transition commit together; affected-row and
balance-preimage checks reject stale state. The normal committed CP, RVP and
silver projection paths update the client only after commit. Failures leave the
transaction uncommitted and require a character reload before further mutation.

No GM requirement, prize amount, schedule, eligibility or P7 policy changed.
An existing saved pending entitlement remains claimable. Unsaved or already
consumed entitlements cannot be recreated by merely changing the memory flag.

## Validation

Isolated build and 71 Local PK checks pass, including six new LMS assertions:

- A suppressed variable-vault update leaves cached currency unchanged and
  rolls back all three saved credits.
- Reload retains the failed claim for retry.
- Concurrent requests grant exactly one complete purse.
- A fresh character reload retains exact balances and a consumed entitlement.
- A stale memory flag cannot revive an already consumed database entitlement.

The fixture supplies a saved winning entitlement and invokes the actual claim
method. It does not simulate winning an LMS match. Ignored logs:
`obj/chi-refill-validation/lms-claim-build.log` and
`obj/chi-refill-validation/lms-claim-tests.log`.

## Durable winner recording and queued purses

The follow-up adds `revora_lms_victories`, keyed by the existing local calendar
cycle (`yyyyMMddHHmm`). Selecting the sole remaining admitted UID freezes that
winner before recording. A row for that cycle and an increment of the saved
pending-prize count commit together. Repeated callbacks for the same cycle cannot
increment it again; a different UID cannot replace the recorded winner.

An unsuccessful write leaves the selected winner in the event's pending state,
which Tick retries before proceeding with another cycle. The event gate uses a
nonblocking actor-lock attempt to avoid waiting against actor-to-event death
callbacks. No cached claim changes before commit. Successful recording precedes
the return/announcement/reset. Subsequent wins add to earlier pending prizes;
each claim atomically consumes one. Existing single pending flags remain one
claim. Prize amounts still use the existing configured amounts at claim time.

Build and **78 focused checks** pass, including victory replay, two unclaimed
wins, suppressed winner writes, exact payouts across reloads, and actual Finish
failure/retry. The finalization fixture supplies a selected survivor and a test
map; it does not simulate an entire competitive match. Logs:
`obj/chi-refill-validation/lms-victory-build.log` and
`obj/chi-refill-validation/lms-victory-tests.log`.

## Remaining earning and release gates

### Fight-limit completion

The existing fifteen-minute limit previously left `Running` true forever when
more than one fighter survived: the timeout branch neither selected a winner nor
closed the round. It now closes that round without a winner and returns its
survivors to Twin City. Before the deadline, the same roster continues normally.
A sweep cannot cancel an already frozen result; cancellation also rechecks the
cycle, running state and roster count after the initial snapshot.

Build and 85 focused checks pass. Three new assertions exercise the actual sweep,
private-map teleport/return, no-victory outcome and frozen-result exclusion.
The fixture registers its map-700 path before using the real dynamic teleport;
the initial missing-path fixture failure was corrected, not bypassed by directly
assigning player coordinates. Logs:
`obj/chi-refill-validation/lms-timeout-build.log` and
`obj/chi-refill-validation/lms-timeout-tests.log`.

### Disconnected winner delivery

A frozen winner no longer needs an online session to finalize. If the canonical
session is absent or unavailable, one transaction records the same victory plus
an unapplied offline-prize receipt. It does not mutate the old session's vault or
currencies. The offline write locks the saved character row and uses a one-second
lock timeout; contention or write failure retains the same pending result for
retry. Existing cycles cannot create another offline receipt.

When the ordinary winner next checks or claims prizes, the owner/variable locks
and a database transaction import pending receipts into the saved claim count.
The count update and receipt markers commit together, preserving earlier pending
prizes. Suppressed writes roll back both; reload and retry remain possible.
The existing atomic purse claim then consumes one entitlement. An index bounds
the lookup to that character's unapplied receipts.

Build and 82 focused checks pass, including actual finalization after removing
the winner's session, replayed offline results, reconnect import/claim and
suppressed import-marker writes. These fixtures do not close native transport
or full process-restart acceptance. Logs:
`obj/chi-refill-validation/lms-offline-build.log` and
`obj/chi-refill-validation/lms-offline-tests.log`.

### Selected-winner startup recovery

Finalization now first commits `revora_lms_selected_winners`, keyed by cycle.
An existing selection must have the same UID. This write happens before the
nonblocking actor-lock attempt, so a busy actor can defer payout without leaving
the selection solely in memory. Currency and pending-prize counters are untouched
by selection persistence.

`Create` initializes the stores and recovers saved selections before opening the
event. It delivers missing results through the offline-prize transaction, without
old roster or pending fields. Already finalized cycles are excluded. Conflicting
winner identities fail startup rather than changing the recipient. A pass handles
at most 128 results; additional work prevents startup, while committed deliveries
survive retry. No historical winner is inferred from a character's current map.

Build and 89 focused checks pass. New assertions cover a busy actor, immutable
selection, failed recovery preserving selection and repeated recovery after
clearing pending fields. These are actual recovery-component calls, not a full
Host process restart. Logs: `obj/chi-refill-validation/lms-selected-build.log`
and `obj/chi-refill-validation/lms-selected-tests.log`.

### Admission reservations and interrupted-round returns

Before transferring any fighter, LMS commits the entire eligible roster with
each character's original PK mode and the cycle/arena identity. Admission takes
actor locks without waiting under the event gate; a busy actor defers entry.
Failed reservation writes transfer nobody. A failed arena transfer cancels the
round, retains the reservations for return, and cannot generate a winner.
These rows prove reserved entry, not that every transfer completed.

Startup ends unfinished reservations after recovering committed winner
selections. It does not resume combat or infer a winner. Login forfeits any old
participation and processes the saved return before travel progress. Elimination,
disconnect and normal completion also queue returns. Offline owners recover on
login; busy online owners retry through the event tick. Destination persistence
precedes acknowledgement, so a failed acknowledgement retains a safe retry.
Returns restore the recorded PK mode in the current session. This does not add
general PK-mode persistence to the legacy character profile.

Build and 100 focused checks pass, including actual registration/start,
reservation failure, actor contention, failed transfer, timeout, elimination,
winner delivery, recovery-component calls, ordinary character reload and failed
return acknowledgement. Logs: `obj/chi-refill-validation/lms-admission-build.log`
and `obj/chi-refill-validation/lms-admission-tests.log`.
These are isolated database/component fixtures, not a full Host process restart
or a genuine-client playthrough. Full restart/native acceptance remains open.
If selection never committed before process loss, no winner can be recovered
from a nonexistent row; that interrupted match ends without a prize.
The legacy flag cannot reconstruct historical wins that were never saved.
Keep those gaps distinct from tested committed-victory and claim persistence.
Native NPC/currency presentation and deployment remain pending. This change
does not certify the whole Last Man Standing earning path.

### Actual process startup reservation probe (2026-09-21)

`tools/Verify-MoonBoxDatabase.ps1 -CheckLmsRecovery` passed using the private
candidate at gameplay commit `d145104`. The probe first boots the composed game
to initialize its stores, seeds two synthetic admission reservations, and boots
the game again. Both reservations become ended but remain unreturned; no victory
is fabricated. It then stops/restarts PostgreSQL and boots a third game process.
The same return obligations remain. The two probe rows are removed on success.

All three launches exposed only the configured loopback Account/Game/Protection
listeners, accepted normal `@close`, and exited zero without forced termination.
Both database lifecycle cycles passed; no candidate listeners remained. Evidence:
`obj/chi-refill-validation/lms-process-recovery.log`.

The refreshed executable SHA-256 is
`5D4DFDDF2EE8FC91E7088875EA1C26165C1844F40D373688777BD3E63A2C59C8`.
`tools/Refresh-CandidateGame.ps1` verifies the prior executable and unchanged
dependencies, preserves the old executable/manifest, and records the new source
commit plus tracked source modifications. The owner-modified `System/IniFile.cs`
is recorded; this is not a clean-source release. No live files were updated.

This proves startup wiring and reservation durability across actual processes.
The seed rows are synthetic reservations, not players admitted over the network.
It does not prove native login/return, crash timing during a real fight, or
selected-winner payout through a restarted server. Those acceptance gates remain
open; the broader roadmap item is deliberately unchecked.


## Selected winner through full process restart (2026-10-01)

Three private phases now pass: `career-lms-select`, `career-lms-claim`, and
`career-lms-reload`. The existing ordinary event characters4294908401/4294908402
register at the installed LMS NPC and enter its actual dynamic arena through
scheduler Tick. No roster, winner, entitlement or reward rows are seeded.
The losing fighter's production OnDisconnect callback leaves one survivor.
A second thread holds the survivor's item lock so finalization saves its immutable
selection but cannot yet record/deliver the victory. The process exits abruptly
while that lock remains held. Assertions establish one selection and no victory
for that cycle, with prior rewards unchanged.

Cold startup recovers the victory and offline receipt from that selection.
Ordinary OnLogin callbacks return both fighters to1002(408,330), with their
recorded original PK mode restored, and acknowledge both ended reservations.
The winner's NPC claim imports/consumes the entitlement and pays exactly the
configured CP/RVP/silver amounts plus one bound P7 soul. Both winner and loser
repeat the claim without payment. A further game/database restart retains exact
items, purses, variables, selection, victory, offline receipt and admission rows;
the awarded UID remains owned and another claim changes nothing.
Checkpoint: `revora_lms_process_probe`, done=true with exact snapshot and item UID.

The first claim probe expected Capture mode, but these actors entered in mode0.
It stopped after returning the first actor and before claiming. The corrected
assertion reads each admission's recorded original mode, and the same saved
match resumes successfully; no match/reward was recreated. An earlier attempt
before the corrected race probe found its prerequisite checkpoint absent and
made no LMS admission. These fixture corrections are not gameplay changes.

Candidate SHA256:
`05F7A80A0EC5F74ED699076019CE7FFD9F2B5B2646F7C69562FEC1A4E43BD9A6`.
No LMS production source changed. Scheduler deadlines, NPC approaches and lock
contention are controlled; the disconnect is a direct event callback, not a
socket drop or competitive combat. An actual interrupted match with no selected
winner, lost COMMIT replies, physical power loss, native UI and deployment remain
separate gates. The main retained career is unchanged. No live server restarted.


## Unfinished match through full process restart (2026-10-01)

The complementary no-selection path now has an actual private-process fixture.
`career-lms-interrupted-enter` registers both existing ordinary event characters
at the installed NPC, starts through the scheduler, saves both living fighters
on the dynamic arena and exits without eliminating anyone. Before exit, exactly
two unfinished/unreturned admissions exist and no winner selection exists.
No admission or outcome rows are fixture-seeded.

`career-lms-interrupted-return` runs after a full game/database restart. Startup
ends the reservations; each ordinary OnLogin returns its owner to1002(408,330)
and restores the original mode from the admission record. Both returns are
acknowledged. Each NPC prize request leaves claim count zero. No selection,
victory or offline prize exists for the interrupted cycle. Exact previous item,
purse, skill, race-result, achievement and earlier event snapshots are unchanged,
including the previously earned LMS soul and purse.

`career-lms-interrupted-reload` repeats cold load, login return and claim refusal
against the saved exact snapshot. All three phases pass. The separate checkpoint
`revora_lms_interrupted_probe` is done=true; the earlier winner checkpoint remains
historical rather than being overwritten with the new admission rows.

No production code/build changed in this batch. Candidate SHA256 remains
`05F7A80A0EC5F74ED699076019CE7FFD9F2B5B2646F7C69562FEC1A4E43BD9A6`.
NPC approaches and schedule deadlines are controlled, with direct login callbacks
and no competitive combat or native-client connection. Lost-COMMIT replies,
physical power loss, native acceptance and live deployment remain open.
