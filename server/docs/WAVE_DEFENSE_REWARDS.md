# Wave Defense reward claims

2026-09-21. Source-tested claim change; not deployed or native-client accepted.

A saved eight-wave victory now awards one random bound P7 soul alongside the
configured CP/silver/RVP purse. The existing fourth-wave consolation keeps its
currency-only reward. Pending saved claims qualify; completed claims remain
closed. Unknown claim kinds are rejected rather than treated as victories.

The canonical ordinary player owns the inventory and variable locks. Saved
entitlement consumption, currency updates and any soul insert commit in one
transaction, with row-count and balance-preimage checks. Inventory/purse capacity
failures leave the prize pending. Consolation needs no inventory space. Exceptions
roll back before commit and require a reconnect; client projection follows commit.

Build and 162 combined Local PK/LMS/Relic Rush/Wave Defense checks pass, including
18 new assertions covering both payouts, suppressed variable/item writes, full
bags, purse limits, concurrent claims, exact fresh item/currency reload, replay,
stale cached claims and invalid reward kinds. Logs:
`obj/chi-refill-validation/wave-prize-build.log` and `wave-prize-tests.log`.

These original claim tests start with saved entitlements; they do not certify
completing eight waves through the actual encounter. The result-delivery batch
below supersedes the earlier single-flag overwrite gap.

## Saved team results and queued prizes

Each instance has a unique result ID and records its admitted character IDs.
Victory/failure freezes the reward kind and currently eligible players before
writing the whole team result transaction. Failure retains that outcome for the
next instance tick; later wave/death/departure changes cannot rewrite it. A
suppressed member insert rolls back the whole roster. Repeated completion cannot
duplicate saved results or reset imported prizes.

Leaving before result selection retains the existing exclusion from later team
rewards. Disconnecting after selection does not discard an already selected
prize. A failure before wave four records no reward; fourth-wave consolation
remains currency-only. Existing legacy claims are preserved and collected first.
Then the oldest queued result transfers atomically into the player's saved claim,
and the existing claim transaction pays it. Later wins queue independently.

Build, 172 focused combined event checks and 216 existing gameplay checks pass.
Result fixtures invoke the actual completion/tick retry paths at controlled wave
states: partial roster rollback, frozen outcome/eligibility, repeated completion,
separate wins, import rollback, returning owners, legacy-plus-queued collection,
early departure and consolation. Ignored logs:
`obj/chi-refill-validation/wave-result-build.log`, `wave-result-tests.log` and
`wave-result-regression.log`.

The admission/return batch below addresses those source prerequisites. Remaining:
full eight-wave ordinary-player earning, real-process/native acceptance and deployment.
An interruption before the result transaction commits still loses the unfinished
instance's in-memory progress. These fixtures are not an eight-wave gameplay or
server-restart acceptance test. Wave Defense restoration remains incomplete.

## Reserved admission and recoverable returns

`WaveAdmissions` saves all team members, the instance ID/map and original combat
modes before teleport. The ordinary team registration path rejects noncanonical,
dead, trading or vending participants. Actor locks are acquired without waiting
under the event gate; a busy actor defers the whole entry. Failed reservations
transfer nobody and dispose the unused arena. A later setup/transfer failure
marks the instance for cancellation and return, without selecting a prize.

Disconnect, login and closure use saved return records. The town destination and
original combat mode are applied and saved before return acknowledgement. Failed
acknowledgements require reload and retain the record; failed round-end writes
retain the instance for close retry. Cleanup uses the original leader UID, even
after that leader leaves. Startup marks interrupted reservations ended; login
returns their owners without inferring a winner. Saved result prizes survive.

Build, 182 focused event checks and 216 existing gameplay checks pass. New tests
enter through ordinary four-player team registration with catalog/terrain fixtures,
check failed admission and actor contention, reserve all modes, disconnect/reload
the leader, exercise interrupted recovery and acknowledgement failure, admit a
later instance, and retry closure after failed end persistence and leader departure.
Logs: `obj/chi-refill-validation/wave-admission-build.log`,
`wave-admission-tests.log` and `wave-admission-regression.log`.

Recovery tests clear instance indexes and load fresh characters in the same test
process; they do not prove an actual server restart. The cleanup batch below
extends companion-backed admission and resource ownership. A complete eight-wave
encounter, real-process/native entry/return and deployment remain open.

## Arena and companion release

Owned Wave arenas now load terrain without unrelated ambient catalog spawns or
portals. Closing waits until every saved return is acknowledged and no ordinary
player occupies that arena. A busy or offline pending return retains the field
and its existing instance slot until recovery completes; it is not discarded to
free capacity. Close retries remain idempotent.

After returns, cleanup removes only this instance's companion objects, disposes
their screen and pet timer subscriptions, clears owned NPC/monster/floor state,
removes its PK-free entry and disposes the exact registered map instance. Generic
result fixtures and other maps are not disposed. NPC/global companion removal
checks object identity. `Pet.DisposeTimers` is internal so the owning event can
release its subscriptions explicitly.

Build and 192 focused combined event checks pass, including occupied-map retention,
release after acknowledged returns, ordinary two-/three-player admission, exact
missing companion counts and disposal of both timer families. Timer factories are
initialized in the fixture without running background callbacks. This verifies
admission and cleanup, not companion combat effectiveness or an eight-wave run.
Logs: `obj/chi-refill-validation/wave-cleanup-build.log` and
`wave-cleanup-tests.log`. Native and process acceptance/deployment remain open.

## Eight-wave progression and missing-catalog rejection

Previously a missing monster definition could silently skip spawning, allowing
an empty wave to count as cleared. Each wave now requires its exact configured
regular monster and, at waves four/eight, its boss definition. Invalid wave/post
state or a spawning exception aborts the instance. Return cleanup then runs without
selecting a consolation or victory. The old fallback to a Pheasant is removed.

Build, 233 focused combined event checks and 216 existing gameplay checks pass.
The added journey registers a fresh four-player ordinary team, advances through
all eight actual wave spawns, verifies live enemies prevent advancement, both boss
HP values and every full regroup interval, then claims each player's bound P7/purse
and reloads exact item identities with replay rejected. Separate missing regular,
mini-boss and final-boss catalog cases verify safe cancellation without rewards.
Logs: `obj/chi-refill-validation/wave-progression-build.log`,
`wave-progression-tests.log` and `wave-progression-regression.log`.

Monster deaths and clock progression are controlled by the fixture. It does not
exercise combat damage, companion combat effectiveness, a real process restart,
or native gameplay. Those acceptance gates and deployment remain open; this is
server progression/reward evidence, not full combat acceptance.

## Companion pursuit and melee

Wave companions previously stood still and passed a null attack to the monster
combat handler, which rejects player-type actors. Their city-guard target filter
also excluded bosses. They now select living monsters from their own instance's
wave roster, approach through bounded per-companion navigation, and use the
existing melee calculation and world-actor death path. Terrain/sight checks
prevent attacking through walls. Movement is limited to one step per 250 ms and
attacks to one per second, subject to the event tick frequency. Route/cooldown
state is released with the instance. Companion spell/proficiency collections are
initialized: the actual lethal-damage fixture exposed a null collection in the
death handler that entry-only checks had missed.

Build, 251 focused event checks and 216 existing gameplay checks pass. Two- and
three-player entries exercise pursuit, actual nonlethal and lethal damage,
cooldowns, boss eligibility, foreign roster exclusion, dead companions and a
blocked tile. Lethal damage clears the last enemy using the real death handler.
Logs: `obj/chi-refill-validation/wave-combat-build.log`,
`wave-combat-tests.log` and `wave-combat-regression.log`.

These are isolated server fixtures with controlled positions and time. A full
eight-wave combat balance run, native movement/attack presentation, process
acceptance and deployment remain unchecked.

## Monster retargeting and post damage

Target selection now verifies the monster belongs to the instance's wave roster,
keeps its assigned post while it stands and selects a surviving post after its
destruction. Nearby registered players or companions can draw it away; dead,
departed, invisible and (for melee monsters) flying defenders are excluded.
Player identity must match the active game session. Selection is serialized with
instance changes and clears stale targets. The screen callback consults event
selection before ordinary player AI, including when its own player was the
previous target; it cannot fall back to unrelated world actors.

Build, 275 focused event checks and 216 existing gameplay checks pass. Added
fixtures verify post reassignment, player/companion distraction and departure,
invisibility, stale-target clearing, roster exclusion, no-target behavior, actual
post damage through the combat handler, and retargeting through the actual screen
callback. Tests use controlled actor positions; they do not establish full combat
balance or native acceptance. Logs: `obj/chi-refill-validation/wave-target-build.log`,
`wave-target-tests.log`, `wave-target-regression.log`.

At this stage, movement still depended on nearby-player screen callbacks. The
following batch removes that dependency; full combat balance remains unverified.

## Autonomous pressure on unattended posts

The event's existing one-second update now owns wave monster decisions, movement
and attacks. Player screen callbacks skip Wave AI, so additional spectators cannot
accelerate attack timing. Monsters pursue their assigned posts beyond the generic
view range using bounded, cached routes that respect terrain, diagonal corners and
monster occupancy. Configured movement/attack delays remain minimum delays;
the one-second event tick limits their effective frequency. Frozen maps stay frozen.
Attacks use the existing combat handler and stop immediately if a lethal player
hit closes the instance. Dead monsters release routing, timing and target state;
closing the event clears all remaining state.

Build, 291 focused event checks and 216 existing gameplay checks pass. For ordinary
two-/three-player teams, a fixture moves all defenders away, places one live wave
monster 25 tiles from its post and blocks its direct route. Actual event ticks
move the monster around the obstacle, damage and destroy the post, then select a
surviving post. Repeated/earlier ticks cannot accelerate movement or attacks.
Cleanup and the existing eight-wave payout/reload fixtures also pass. The screen
callback fixture now verifies that it leaves Wave decisions to the event.
Logs: `obj/chi-refill-validation/wave-autonomy-build.log`,
`wave-autonomy-tests.log`, `wave-autonomy-regression.log`.

Positions and time are controlled in these server tests. Complete team combat
balance, native movement/attack presentation, process acceptance and deployment
remain open.

## Composed startup reservation recovery

On 2026-09-21, the existing private candidate was refreshed to gameplay commit
`5da095a5470a24a3949dfe8a304b694175b490e3` after its build and focused checks.
The manifest records the pre-existing owner change in `System/IniFile.cs`; this
is not a clean-source release. Previous executable and manifest backups were
preserved. Candidate executable SHA-256:
`6D35CB6164EFE0B0F6BBAD2DFEE63621BE308B093EBB5286E631838177529EC4`.

`tools/Verify-MoonBoxDatabase.ps1 -CheckWaveRecovery` passes three actual game
startup/normal-close checks across two PostgreSQL process cycles. It inserts two
synthetic Wave admission rows while the game is stopped, then verifies actual
startup marks them ended while preserving their unacknowledged return state and
creating no victory/consolation result. The second database/game restart preserves
that state. Probe rows are removed only after both checks succeed. All listeners
were private loopback; game exits were zero without forced termination. Game and
database remain stopped. Log: `obj/chi-refill-validation/wave-process-recovery.log`.

This closes only composed startup recovery of saved reservations. It does not
prove authenticated player entry, native reconnect/return, earned payouts across
process restart, complete combat balance or deployment. No production service or
client files were changed.

## Post protection and immediate defeat

Registered defense posts accept damage only from living, registered wave monster
objects in the same instance. The check runs at both combat execution and target
validation, protecting against player melee/spell selection, companion attacks
and unregistered monsters. It uses the actual post/monster objects, not names or
UIDs alone, and rejects attacks after the outcome has been selected.

After autonomous monster attacks, the event recounts standing posts before
progression or rewards. Losing the final post now fails and closes the event in
that same tick. Build, 301 focused event checks and 216 existing gameplay checks
pass, including post protection, actual final-post destruction/cleanup and no
reward entitlement for a first-wave loss. Logs:
`obj/chi-refill-validation/wave-post-build.log`, `wave-post-tests.log`,
`wave-post-regression.log`. Native combat presentation and full balance remain
unchecked. This source change is newer than the private process-tested candidate
above; it has not been deployed or refreshed into that runtime.

## One shared life per actual player death

An actual monster-kill fixture reproduced a double life deduction: `Die(Entity)`
dispatches event death during `DropRandomStuff` and again afterward. Wave's first
callback revived the player, but the second callback spent another life. The
event now requires a dead player object still admitted to that same arena before
deducting a life. The post-revival callback therefore cannot spend another life.

Build, 314 focused event checks and 216 existing gameplay checks pass. A fresh
ordinary four-player team receives four distinct deaths through both Entity death
overloads. Each death consumes exactly one life; the first three revive at half
health, extra post-revival callbacks do nothing, and the fourth ends the event
and returns the team without a first-wave prize. The test failed on the first
death before the fix. Logs: `obj/chi-refill-validation/wave-lives-before.log`,
`wave-lives-build.log`, `wave-lives-tests.log`, `wave-lives-regression.log`.
These are server-state checks; native death/revival presentation and full balance
remain unverified. The process-tested private runtime is still the earlier
`5da095a` build, and this change is not deployed.

## Four-player earned results across process restart (2026-10-01)

`career-wave-earn`, `career-wave-claim` and `career-wave-reload` pass on private
full-server SHA256
F020C8A2B264B6D420B62A5DFA5421387E11B8D5F6AC44BB8305FEEC1608049B.
Four ordinary non-GM characters form a supplied team and enter through the
installed WaveWarden NPC, with controlled proximity and registration time.
The production arena and all eight monster rosters are created normally.
Live enemies prevent progression; controlled lethal ReceiveAttack calls clear
those rosters. Each of seven regroup intervals refuses advancement at seven
seconds and starts the next wave at eight. All four saved victories remain
unimported when the event returns the team, acknowledges their admissions and
closes the instance.

The game exits via Environment.Exit before anyone claims. After game/database
restart, exact purse, item, result and admission rows match the saved fingerprint.
OnLogin and ordinary NPC claims pay each player's configured victory currencies
and exactly one bound P7 soul. Each receipt imports once; repeated claims leave
all four inventories and purses unchanged. A further game/database restart
preserves exact item UIDs/rows and consumed results and again rejects replay.
`revora_wave_process_probe` stores the final fingerprint.

The first attempt exposed a fixture error: raw seeded characters had not passed
through LoadEntityState, so guarded character saves rejected their town returns.
The four victory records survived that failed process. The explicit
`career-wave-bootstrap-recover` phase loaded those existing characters normally,
recovered their pending returns and paid their prior saved prizes before saving
`revora_wave_bootstrap_probe`. No result, reward or character was deleted to retry.
The successful eight-wave run starts from that recovered checkpoint and verifies
both prizes per character persist; it does not count the fixture failure as a
production defect or normal gameplay failure. Fresh seed construction must always
use the normal loader before this journey.

This proves a four-player server-state route with controlled damage and clock,
not normal combat balance, two-/three-player companion earning, authenticated
team packets, native presentation or interrupted mid-wave resumption. Unfinished
rounds remain forfeited at startup. No production code, live service or client
changed; the separate retained achievement career is unchanged.

The later [admitted-round interruption batch](EVENT_INTERRUPTION_RECOVERY.md)
passes actual NPC entry, abrupt exit before progress, town/combat-mode recovery,
no unearned prize and exact prior rewards across two cold restarts. It supersedes
the missing early-round process boundary only; later-wave/native gates remain.

## Fresh four-player combat and cold rewards (2026-10-03)

`CheckCandidateFreshWave.cs` continues the fresh career after CTF. The ordinary
Warrior, Archer, Monk and Trojan form a four-player team through sealed creation
and invite/accept handlers, approach WaveWarden and enter through NPC900000007.
Their existing earned profiles, equipment, currency and prior event receipts
are preserved. No characters, damage amounts, monster deaths or results are seeded.

All eight waves pass:102 monsters defeated through103 calculated melee attacks
by the Warrior. The three teammates are admitted participants; this does not
claim four active damage contributors. The fixture checks that a seven-second
intermission cannot advance the wave and that the normal eight-second transition
can. All four participants return alive, with ended/returned admissions and four
unpaid victory records persisted before process exit.

A separate process pays each player12,000 CPs,40 RVPs,1,200,000 silver and one
permanent bound P7 soul. The exact four item UIDs, currency increments and consumed
results are checked. Repeated claims give no additional item or currency. Final
fresh-process recovery requires the exact nested career checkpoint, each loaded
P7 UID/grade/binding, four consumed results and unchanged state after more replays.

Calendar, NPC approach, attack approach/cooldowns and event ticks are controlled.
Normal damage and production team/NPC/reward handlers are exercised; native
rendering, natural-time balance, sustained retaliation and a competitive field
are not certified. Mid-wave recovery retains its separately documented scope.
The existing private candidate is unchanged:
`D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6`.
Logs: `arena-process-career-fresh-wave-{earn,claim,reload}.log` under
`C:\Revora\DisposablePostgres\moon-box-candidate`. No live deployment.
