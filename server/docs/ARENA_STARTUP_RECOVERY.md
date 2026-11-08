# Arena captured-result startup recovery

ArenaStartupRecovery.Recover(run) settles one admitted active match with an
existing durable outcome intent and both participant snapshots. It is inactive
until the complete Arena lifecycle cutover. The caller must run synchronously
before loading solo/team Arena caches or starting listeners; it is not a general
online/offline mutation endpoint. Guards reject populated session pools, match
registrations, either Arena cache or existing Account/Game listener objects.
The guards are checked before work and before commit. Startup integration must
retain exclusive lifecycle ownership; these checks alone are not a login barrier.

Recovery locks the saved match/intent and both saved owners in stable UID order.
Both statistic preimages and the team point mirror must still match capture.
It applies the same conserved, floored three-percent transfer, both lifetime
results and daily receipt-derived counters. Daily/custom milestones use the
original Pacific day, VIP and economy. Result, both statistic receipts, mirrored
points, pending rewards and the complete-bundle marker commit together. Failed
writes roll back the entire settlement; captured evidence remains for retry.
A repeat finished result does nothing. A match lacking an observed outcome is
left active rather than inventing a winner. No inventory/currency payout occurs
at startup: existing entitlement delivery remains responsible for that step.

## Evidence and remaining integration

Focused fixtures earn 8 or 99 registered prior wins through the coordinator,
capture the next disconnect outcome, then isolate all runtime registries to model
cold startup. Recovery finishes ninth/hundredth results and records their pending
rewards. Injected solo/team statistic, statistic-receipt, bundle-receipt and
milestone writes all roll back. Repeated startup calls settle once. Production
Arena loaders verify the recovered values. Session/cache guards and uncaptured
matches are covered. Test totals are in QUEST_EVENT_STATUS.md; ignored log:
obj/chi-refill-validation/arena-startup-recovery.log.

This is an isolated cold-state simulation, not a real process restart or native
combat test. The actual fixture group is restored only for cleanup; production
recovery has no live group. Unchanged preimages are required, so stale or changed
rows stop recovery rather than being overwritten. LastArenaPointFill and legacy
local-day resets still need coordinated migration before activation.

Pending-run enumeration, abandoned active-match handling, startup wiring before
ArenaTable.Load/TeamArenaTable.Load, live disconnect/forfeit hooks, automatic
reward delivery, ranking settlement and legacy writer replacement remain open.
Source startup currently loads the Arena caches before opening the listeners;
recovery must be inserted before those cache loads. No live process, database
schema or installed client was changed by this batch.

Bounded enumeration and uncaptured interruption closure are now implemented
([startup sweep](ARENA_STARTUP_SWEEP.md)). Actual startup wiring, departure
hooks and legacy-writer replacement remain unfinished.


## Recovery while both failed owners remain fenced

The inactive coordinator also exposes RecoverFenced(group). It takes the match
lock and both owner locks without waiting, requires the exact registered ended
match/map and canonical players, and requires both sessions to be fenced with
invalidated solo/team statistic objects. A healthy or replaced session cannot
use this route. The shared recovery transaction checks the saved participant and
winner/loser identities against that group and rechecks ownership before commit.
Startup Recover(run) retains its separate empty-registry/listener guards.

This route uses only the existing saved intent and statistic/policy snapshots.
It validates durable preimages and commits the result, both point/statistic rows,
earned entitlements and bundle marker together. It does not re-enable sessions,
project new values into invalid objects or deliver prizes. Replay requires the
complete matching bundle and both participant statistic receipts. A failed write
returns Failed with its transaction rolled back; a busy lock returns Busy. Missing
intent returns NotReady, never an invented outcome.

The fixture fails a real ninth-win bundle, retries with the fault still present,
then recovers concurrently without clearing either session fence. It verifies one
committed ninth win and pending daily prize, production reload, replay, replacement
and busy-owner rejection, and cleanup after both registrations are released. It
also rejects a fenced RAM ending without durable capture. Ignored log:
obj/chi-refill-validation/arena-fenced-recovery-tests.log. No native reconnect or
actual shutdown pump is claimed.

Live coordinator/shutdown wiring remains disabled. The host still needs to own
retry scheduling and canonical registration until completion, then release the
fenced sessions for fresh loading. Initial capture failures can use the stricter recapture path below only while
the original observation and both unchanged fenced owners remain available.


## Retrying an initial capture failure

Before its first database capture, CompleteCore now retains the first coordinator
observation (loser, cause and UTC time) in the existing per-match outcome slot.
An earlier departure request retains priority. Thus a failed generic result or
timeout capture also has an original observation available for a bounded retry.
This does not assign the timestamp of an earlier unobserved legacy death; it
preserves the first coordinator observation, as the original capture did.

RecoverFenced can retry capture using that retained observation. CaptureFenced
requires the ended owned map/match, both original canonical fenced sessions,
invalidated statistic objects and their owner locks. It locks the saved owners
and verifies all five cached statistic fields plus VIP against persistence before
saving the two snapshots and intent atomically. A changed preimage, missing owner
or missing observation cannot be replaced with guessed values. Once captured,
the existing recovery transaction completes the result. Sessions stay fenced.

Policy values are frozen at the first successful durable capture; an earlier
failed transaction did not establish a saved policy receipt. Existing captured
snapshots remain immutable. The original observed cause/day is retained across
midnight even if policy configuration changes before successful capture.

Tests inject failures at intent and snapshot insertion, keep the failure active
for one retry, reject a changed durable point preimage, then restore that fixture
preimage and recover across midnight without clearing session fences. Replay
retains one result. The existing missing-observation rejection remains covered.
Ignored log: obj/chi-refill-validation/arena-capture-recovery-tests.log.
A crash before durable capture still loses RAM observations; startup must not
invent that outcome. Live retry scheduling and native reconnect remain open.
