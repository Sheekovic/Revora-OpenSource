# Steed Race prize recovery

The existing prize NPC cleared `steed-claim` before separately adding CPs,
silver and Revora Points, then saved variables and the character. That ordering
could lose or partially pay a prize after a failed database operation. A rank
outside 1-3 also fell through to the first-place reward.

SteedRaceClaims now consumes the saved rank marker, updates all three saved
purses, merges unrelated quest variables, and inserts a payout receipt in one
checked transaction. It uses the existing configured first/second/third rewards.
The caller must be an ordinary canonical loaded character at the real prize NPC,
not trading, running a booth or playing poker. No GM or account allowlist is
required. Overflow leaves the prize pending. A postcommit projection failure
requires reconnect and recovers the saved result without a second payment.
Registration and race admission reject characters with a pending prize.

Startup creates `revora_steed_claims`; no live schema or process was changed.
Legacy saved rank-only markers remain claimable at the current configured price.
New races freeze all three prizes at start in `revora_steed_runs`, and each
admission/finish lives in `revora_steed_results`. The saved claim contains the
run UUID as two exact unsigned words. A new claim must match its unclaimed
result; consuming that result and paying all purses share the same transaction.
The finish packet's displayed CP prize also reads the frozen result.

Validation: staged Debug/x86 build and 31 isolated claim checks pass. Tests use
ordinary characters, real NPC claim dispatch, concurrent requests for each rank,
suppressed CP/silver/vault/receipt writes, exact purse recovery, invalid ranks,
remote/trading/overflow/unsaved requests, unrelated variable retention, pending
prize registration refusal, external durable purse changes, and postcommit
projection failure followed by full character/item/variable reload. Pending
prizes are fixture-seeded; these are claim tests, not completed-course evidence.

## Durable admission and finish order

`PrepareEvent` creates a run with the selected track, finish region, ten-minute
expiry and frozen prize arrays. `Join` requires the current registration roster,
canonical mounted character, Riding skill, no pending prize and an unopened gate;
admission is saved before teleport. Both legacy Join callers now pass through
these checks. Successful gate opening is saved before the start signal.

Finishing requires the same admitted live session, the current track, mount/Ride,
open gate, unexpired run, and existing 22-cell finish region. A row lock allocates
one unique place and saves its pending prize marker atomically. Duplicate finish
requests cannot take another place; fourth and later places earn no top-three
prize. Withdrawal removes the live admission. Startup aborts interrupted active
runs, while completed result entitlements remain claimable after reconnect.

The expanded 55-check suite validates duplicate admissions/finishes, unopened
and expired gates, unadmitted and withdrawn participants, finish region,
concurrent distinct places, suppressed result/order/vault writes without rank
gaps, start-frozen CP pricing, postcommit finish projection recovery, simulated
startup interruption, and rollback/retry of result consumption during payout.
Fixtures call the result service with mounted ordinary characters and controlled
positions/time; they do not prove genuine course traversal or native packets.

## Course validation and earned achievements

The server now records continuous accepted movement from the admitted start.
Walks/slides are limited to two tiles and jumps to twenty; every interpolated
crossed tile must be walkable. Pre-gate movement is refused. Changing maps,
teleporting forward, losing the mount during movement or changing the canonical
session invalidates that run's course evidence. Corrective teleports to the same
accepted coordinate do not erase progress. A disconnected rider cannot resume
an old admission; interrupted races are aborted on startup.

Finishes use each track's configured radius instead of the old universal 22.
Desert Race previously started at (42,585), already inside that old finish
region. Its Revora start is now (568,14), beside configured gate (564,14).
This is a terrain-backed Revora correction, not a verified original coordinate.

A valid finish saves achievement 11113 with its result and pending prize in one
transaction; rank one also saves 11122. Fourth and later finishers earn completion
without a top-three currency prize. The live achievement cache refreshes only
after commit. A failed evidence write rolls back rank allocation and entitlement.
These identities come from installed 6711 Achievement.dat (SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`).

Build and all 84 isolated checks pass, including full achievement reload and
suppressed achievement-write rollback. The suite exercises real Entity.Move calls along walkable routes on
all seven installed server floors. The exact successful step counts are:

| Track | Start | Finish | Radius | Steps |
|---|---|---|---|---|
| 9929 | 568,14 | 30,563 | 6 | 544 |
| 2066 | 633,432 | 85,371 | 6 | 941 |
| 1950 | 88,149 | 420,431 | 4 | 733 |
| 2064 | 175,250 | 200,153 | 6 | 1234 |
| 2061 | 60,400 | 899,816 | 10 | 1170 |
| 2063 | 450,520 | 682,484 | 10 | 1327 |
| 2062 | 150,350 | 330,170 | 6 | 1208 |

Terrain SHA-256 provenance (`bin/Debug/database/maps/<track>.map`):

```
9929 568aef25c4651e802dedfd8f72b6bbb4c95b91aab7d91982122ac712f822fae0
2066 a84da97a80c8ca6b1f42cb1836de4234dac1c65d8266a040a680cc9dc051909d
1950 081bc55ee5b5ac0ceb10dd3f0df2431cfc8c99bdfb76371cf708cb652a5603f1
2064 04259f8762dbd02947181a721e15ff5c7443339810d3a5ded19c8108633dc21a
2061 bb40b9258e9f816f7fdb0f067ab2bde896d8d94196a36aa53f093664a246e95f
2063 05cbe18e6879c0aace20a593d58fcc75158dd043e7c7e8cee64ba25cc8020061
2062 eb28d6fa4499f4925852b46f54c67128e480c9b17f79f1149a5721b9170d56f9
```

## Remaining race acceptance

Scheduled ordinary registration, Join and prize dispatch are covered by the
September26 fixture. The full-process continuation below additionally verifies
course2063, first/second finishes and unpaid-prize recovery through actual restarts.
The other six tracks and third/fourth places retain their isolated-test scope.
Genuine DX9 gate rendering, mounted jump/slide behavior, natural movement timing,
race-item effects and live deployment remain pending. Mid-course startup return
now passes on installed Dungeon and Ice tracks as documented below.
Server-side earning evidence is not native or production acceptance.


## Valuable top-three prizes (2026-09-26)

New Revora races now include one bound P7 soul in each top-three prize alongside
the existing configured currencies. Each rank's soul type is selected from the
loaded P7 catalog and frozen in the race-start transaction. Finishing copies that
rank's type into the owned result with the placing, course proof, achievements
and pending claim. Fourth and later places retain completion achievement credit
without a top-three item or currency prize. This is an owner-directed Revora
reward addition, not a claim about the original retail prize.

Migration gives old runs/results zero soul types. Historical rank-only markers
and old admitted runs retain their promised currency-only prizes. Existing
receipts are not reopened. The race NPC's registration text announces the new
bound soul, and its claim response confirms actual delivery.

Claims now hold the character's session-installation lease as well as its owner
and variable locks. For a soul-bearing result, both live and saved bag capacity
must allow one item. The bound soul, its UID in the claim receipt, three purse
updates, variable consumption and result payment commit together. A full bag
retains the entire prize; retry cannot split currencies from the item. Saved
catalog mismatches fail closed rather than substituting another reward.

Validation: isolated build and **108 Steed Race checks** pass, including the
existing seven installed-terrain movement journeys. Added coverage verifies
all top-three rewards and fourth-place exclusion, old contract preservation,
full bags, suppressed item/receipt writes, concurrent retries, postcommit
projection failure and full production character/item/variable reload with
exact bound soul UIDs. Test log: `obj/steed-prize-tests.log`; build log:
`obj/steed-prize-build.log` (ignored). Complete scheduled registration/Join,
fresh Host restart, native prize/movement presentation and deployment remain
pending as described above.


## Scheduled registration and session ownership (2026-09-26)

Registrations now retain their exact GameState owner. Invitations and signups
reject fake, stale, incomplete, fenced, trading or booth sessions, alongside the
existing event exclusions. A replacement login must register again; a late old
session's disconnect cannot erase that fresh signup. Disconnect removes only its
own roster entry. Startup selection and Join require the same registered live
session, and the start announcement counts successful track admissions. Fewer
than two successful joins closes the race instead of announcing a viable match.

The new fixture enters through actual NPC registration, drives the real Tick
registration/start/gate branches with controlled scheduler deadlines, and uses
normal race creation, durable admission, teleport and finish behavior. The race
constructor retains normal initialization while an internal fixture option skips
its legacy timer subscription. The fixture does not open listeners or launch a
Host. Track metadata is test-owned and race-record writes are excluded; terrain
and configured race coordinates come from the installed routes.

After the selected course's accepted movement, the production finish returns the
winner to Twin City and the ordinary prize NPC delivers the bound P7 soul. This
closes the source-level registration-to-reward gap, not the native UI, real-time
movement pacing, finish-packet interoperability or fresh-Host acceptance gates.
Fault-injected mid-admission/teleport cleanup and broader scheduler races remain
separate coverage gaps.

Validation: isolated build and **119 Steed Race checks** pass. Eleven new checks
cover NPC registration/session guards and the scheduled registration-to-prize
journey. Logs: `obj/steed-entry-build.log` and `obj/steed-entry-tests.log` (ignored).
No production character, server process, client or public patch was changed.

## Riding acquisition prerequisite (2026-10-01)

Mount Trainer's four paid lessons now commit skill, silver and receipt together.
The ordinary Riding acquisition needed for race eligibility survives forced
rollback, concurrent requests and actual game/database restart. Existing lesson
prices are preserved; War Cry's displayed price now matches its100,000-silver
charge. [Implementation and validation](MOUNT_TRAINER_LESSONS.md).
This prerequisite does not certify the remaining full-course process/native gates.

## Full-process ordinary course and unpaid-prize recovery (2026-10-01)

Two existing ordinary event characters4294908401/4294908402 acquire their actual
race prerequisites. A decoded trade transfers10,000 earned CPs between them;
each buys the configured40,000-CP RevoraMaster mount kit and equips its owned
+12 steed through ItemHandler. The other four kit items remain owned. The second
rider learns Riding for50,000 silver through the repaired Mount Trainer; the first
retains its previously paid lesson without another charge. Skills, kit receipts,
items and balances survive the preparation restart.

Both register through the installed SteedRace NPC. Controlled scheduler deadlines
drive the actual Tick registration/start/gate branches and normal random track
selection, creation and admission. An attempted finish before the gate opens
produces no prize. The selected track is2063. Each rider follows1,327 consecutive
accepted Entity.Move steps along loaded walkable terrain, then the production
finish allocates first and second place and returns them to Twin City. No course
progress, finish ranks, claim markers, mounts or skills are fixture-seeded.

The game closes the race and exits abruptly with both prizes still unpaid.
After full game/database restart, the NPC delivers the exact frozen rewards:

| Place | CPs | RVPs | Silver | Bound P7 type |
| --- | --- | --- | --- | --- |
| First | 6,000 | 15 | 500,000 | 823061 |
| Second | 3,500 | 8 | 250,000 | 824020 |

Each result creates exactly one new bound P7 item. The ordinary claim clears its
pending marker atomically with currency/item delivery. Immediate replay changes
nothing. Another full restart reloads both equipped steeds, Riding skills, exact
currency balances and awarded item UIDs; another NPC claim preserves the complete
snapshot. The result/claim/skill/kit/vault/achievement records and prior event
rewards remain exact. The main retained career is untouched at212 stored identities.

Four phases pass: `career-race-prepare`, `career-race-earn`, `career-race-claim`,
`career-race-reload`. Checkpoint: `revora_race_process_probe` step3 through
`RaceProcessSnapshot`. Private game SHA256:
`3875BAE7BD0161A2F384B24296CF577647CDC660A18B50BC05A868D9F145E110`.
The initial probe compilation attempted an internal vault accessor; using the
existing public indexer corrected that before gameplay. No production source
change or new production build was needed for this process batch.

Limits: this is one randomly selected course and two placements in full processes;
the other six tracks and third/fourth places retain their earlier isolated-test
scope. Movement calls are accepted server steps, not socket packets or naturally
paced native riding. Scheduler deadlines and NPC approach are controlled. A
completed race is interrupted before claim, not a mid-course crash or an in-COMMIT
failure. Native rendering, race-item effects, movement timing and live deployment
remain open. No live game restart or client release was performed.


## Mid-course startup return (2026-10-01)

Startup previously aborted active runs without returning their saved riders.
The legacy login map list also omits Desert9929 and Cactus2066. Initialization
now atomically updates map/X/Y to Twin City1002(408,330) for saved riders still
on a matching recorded closed/aborted race course. The query covers all seven
configured courses and runs before Account/Game listeners. It leaves result
entitlements and prizes intact; a later startup retries an interrupted return.
It does not move characters whose current map no longer matches their course.

Validation correction: e0b01d4e's five phase names were dispatched to the older
RaceProcess verifier instead of RaceReturnProcess. Those pass markers did not
prove the claimed mid-course recovery. Routing is now explicit before the general
race prefix. The corrected Desert attempt exposed a second issue: this private
catalog has neither9929 nor2066, and Create retried forever when no configured
track existed. The owned stalled probe was terminated after catalog verification.
Create now chooses randomly from catalog-present tracks and returns Settings=null
when none are available, allowing the existing scheduler cancellation path.

Five corrected phases pass: `career-race-return-interrupt-dungeon`,
`career-race-return-recover-dungeon`, `career-race-return-interrupt-ice`,
`career-race-return-recover-ice`, and `career-race-return-reload`.
Each track admits the same two ordinary mounted riders through NPC registration
and scheduler start. Each rider takes20 accepted Entity.Move steps, saves on the
course and exits abruptly. Cold startup returns both database rows and loaded
characters to1002(408,330). Both runs are aborted, all four result rows have no
rank/finish/claim, and prize requests give nothing. Exact prior items, purses and
event rewards remain unchanged; Riding and equipped mounts load. A final cold
restart verifies stable recovery. The real checkpoint now contains two recovered
rows for2063/2064. No admission rows or saved map positions were fixture-seeded.
Both interruption phases also test a configuration containing only absent9929:
Create returns null settings in under one second instead of looping.

Private candidate SHA256:
`05F7A80A0EC5F74ED699076019CE7FFD9F2B5B2646F7C69562FEC1A4E43BD9A6`.
Build succeeds with existing warnings. Track choice, scheduler deadlines and NPC
approach are controlled. This does not establish missing Desert/Cactus admission,
same-process reconnect, native riding, in-COMMIT failure or physical power loss.
No live restart, deployment or client release occurred.

## Retained purchased-mount course (2026-10-01)

The retained ordinary Windwalker starts without an equipped steed or Riding.
It buys the existing Mount Kit through RevoraMaster (900000001, options 4/14),
verifies the exact configured CP debit, equips the newly purchased steed and
learns Riding through Mount Trainer 9881 option 5. Other purchased kit items
remain owned; no mount, spell or funds are injected. The initial preflight
stopped before racing because these prerequisites were absent; it did not fail
an earned result or reset a claim.

The retained player and an existing ordinary mounted racer register through
NPC 900000012 option 1. An early finish attempt before the gate opens grants
nothing. Once the gate opens, both follow the loaded walkable terrain through
`Entity.Move`: 1,170 accepted steps each on map 2061. The retained character
finishes first and the peer second; both return to Twin City with saved unclaimed
results. The winner receives course-completion 11113 and first-place 11122.

A fresh process claims through NPC option 2 for both characters. Each receives
exactly its saved CP/RVP/silver prize plus one bound P7 soul, preserving all prior
bag UIDs. Another process reload and twice-repeated claims preserve the exact
career, peer currencies/items, variable vaults, lessons, results and claim snapshot.
The retained career includes previous PK, Arena, Grail and stored reward state.

Calendar/registration/gate waits and NPC proximity are controlled. Racers are
advanced sequentially, so this is course/result/reward evidence, not a competitive
speed or packet-timing test. Movement uses the server handler, not native input.
The peer's mount/lesson was earned in an earlier fixture. This batch covers one
loaded course; native presentation and live deployment remain open.

Validation: `career-retained-race-earn`, `career-retained-race-claim` and
`career-retained-race-reload` passed on candidate SHA256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
No production code, live restart, deployment or client patch changed.

Receipt inventory adds four identities: course11113/11122 and observed mount
state10107/10108. Observed-state receipts retain that provenance; they are not
relabelled as distinct quest-completion contracts.

## Fresh earned mounts, course and cold prizes (2026-10-03)

The fresh career UID1096322 and existing trainee UID1096323 complete
`career-fresh-race-prepare`, `career-fresh-race-earn`,
`career-fresh-race-claim` and `career-fresh-race-reload`. All four initial phases
pass. The final read-only cold phase passes again after adding explicit checks
for course receipt identities and persisted step counts; earning is not replayed.

The main keeps its earned starter mount. A normal mutual trade transfers40,000
of its earned CPs to the trainee, which buys the five-piece mount kit from
RevoraMaster for the configured40,000 CPs and equips the purchased steed normally.
The repeated purchase confirmation adds no charge or items. Both characters
buy Riding7001 from the existing Mount Trainer for50,000 silver each. Repeated
lesson requests leave their silver unchanged. Cold preparation preserves the
paid kit, equipment, both lessons and the entire prior Quiz/Purple career.

Both register through the race NPC and enter the randomly selected track2062.
An attempted finish before the gate opens grants nothing. After the controlled
gate wait, each follows the loaded walkable terrain through1,208 accepted
`Entity.Move` steps. Saved course steps and distinct tile counts are checked.
They finish first/second, return to Twin City, and leave both prize results
unclaimed over a game/database restart.

The winner claims6,000 CPs,15 RVPs,500,000 silver and one bound P7. The runner-up
claims3,500 CPs,8 RVPs,250,000 silver and one bound P7. These are compared to the
prizes frozen in their saved race results. All previous bag UIDs remain owned.
A separate final process verifies each exact reward UID in its loaded inventory,
mount/Riding retention, the complete snapshot and unchanged state after repeated
claims. Main11113/11122 and peer11113 have `steed-course-v1` evidence.

The authoritative checkpoint is `revora_fresh_race_probe`, stage3, and
`FreshRaceSnapshot` nests the prior career, both players' skills, mount purchases,
lessons, variable vaults, race runs/results and claim receipts. Historical Quiz
and earlier checkpoints must not be rerun after this advancement.
Source: `tools/CheckCandidateFreshRace.cs`. Private logs:
`C:\Revora\DisposablePostgres\moon-box-candidate\arena-process-career-fresh-race-*.log`.
Unchanged game SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

Registration and gate timing, NPC approach and sequential racer movement are
controlled. This proves the server course and reward path on one selected track;
it does not establish competitive speed, native input/rendering or all-course
coverage. No production code, live restart, deployment or client patch changed.
