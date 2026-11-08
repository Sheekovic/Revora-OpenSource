# Current roadmap startup integration

## Actual isolated game/database cycles (2026-09-25)

The current staged executable was copied into the existing private candidate
runtime through the reviewed transactional refresh script. The previous executable
and manifest were preserved; dependencies and runtime files remained hash-checked.
The candidate manifest records source `80b5de0` and the unrelated owner worktree
changes included in the build. This is not a clean release build.

Tested executable SHA-256:
`545FC9AB3090A2B9AC2D55F086D4DB1CD762C179BE393FA989ECF580436E19A3`.

`tools/Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup` now runs two actual
game/database start/stop cycles. Each game exposes only its expected IPv4 loopback
listeners (19558, 19559, 19619); Arena prepares before its cache loaders; `@close`
exits zero without a forced stop. The disposable PostgreSQL instance uses loopback
25439 with SCRAM, verifies database identity and rejection of a wrong password,
and stops between cycles and after the final check.

After each game exits, a read-only query checks all thirteen current tables:
four Mr./Ms. 2017 tables, eight Heart tables and the Euro jersey purchase ledger.
Every table must belong to the candidate runtime role. A fingerprint of their
names and relation OIDs must remain identical across both database/game cycles,
so initialization cannot silently replace these stores. The check inserts no
synthetic player, reward or campaign rows.

Both cycles passed. A separate first startup/close smoke also passed before the
two-cycle check was added. No candidate listeners or PostgreSQL PID file remained
afterward. Evidence logs are ignored local artifacts:
`obj/current-game-startup.log`, `obj/roadmap-startup-cycles.log` and the harness's
redacted `C:/Revora/DisposablePostgres/moon-box-candidate/game-startup.log`.
The last game log reports the known missing `identity.cp_orders` payment integration;
that external website service is not part of this disposable database.

## What remains open

This proves composed startup, idempotent schema initialization and normal shutdown
for this candidate. It does **not** prove recovery of logged-in fighters, earned
campaign data under a real Host crash/restart, wire authentication, native movement,
UI presentation or combat balance. Earlier fresh-process Heart fixtures provide
separate component recovery evidence; they are not upgraded to full Host acceptance
by this empty-session startup check. Player recovery/native milestones stay open.

Only the existing isolated candidate binary/manifest and disposable database were
updated. The live game, web database, public ports and client patch distribution
were untouched. No new runtime tree was created. The stopped candidate retains its
previous executable/manifest for rollback, and no production deployment is claimed.


## Current committed gameplay startup (2026-09-27)

The gameplay at **64150f30** now passes two actual isolated game/database cycles.
The validation build refreshed all 18 source overlays from that commit and checked
that the project-only Compile additions were exactly the four excluded owner WIP
files. The existing private output/runtime was reused; no new release tree or live
server deployment was created. The candidate manifest retains the worktree-change
inventory and conservatively does not claim a clean release build.

Tested executable SHA-256:
`5A08CB5EAEA7A812846B909AF4D0B2218EF7E7A540DF1EC7CD07F1B66417627A`.

An initial actual startup failed at SoulGeneralQuest's placement guard. The older
candidate database had General Amber, ID4487/model6700/map1020, at **879,562**;
the reviewed 6711 quest requires **865,540**. The existing ordinary quest fixtures
had not exercised that legacy catalog preimage. The guard was retained.

`Prepare-CandidateQuestCatalog.ps1` and its compiled helper now provide an explicit,
transactional **private-candidate-only** correction. They verify the library
manifest, stopped candidate game, exact loopback database/role/data directory and
empty player table, then lock the one NPC row and accept only the recorded old or
already-correct placement. The update is parameterized and preserves all other
columns. Missing, duplicate or unknown placements fail. The second full cycle
exercises the already-correct case. No production catalog was updated. Before a
future deployment, review and stage this prerequisite against that destination;
this tool intentionally cannot target it. Reversal restores only that row's
cellx/celly to the recorded 879/562 after checking the current 865/540 preimage.

Commands used:

```powershell
tools/Refresh-CandidateGame.ps1 -BuildPath obj/mrms2017-validation/bin/Revora.exe
tools/Verify-MoonBoxDatabase.ps1 -PrepareQuestCatalog -CheckRoadmapStartup
```

The refresh preserves the previous executable and manifest. The startup check
now fingerprints **18** runtime-owned tables: the prior 13 plus achievement
evidence, event awards, +composition receipts, Exemption/+stone purchases and
Guild Manager ownership. Both cycles preserve their relation identities, prepare
Arena before caches, expose only ports19558/19559/19619 on IPv4 loopback and exit
zero without a forced stop. PostgreSQL25439 also stops after each cycle. Logs:
`obj/roadmap-current-build.log`, `obj/roadmap-current-cycles.log` and the private
candidate's redacted `game-startup.log`.

This closes current composed startup/shutdown and schema-retention verification.
It does not prove logged-in-player recovery, earned rewards across an actual Host
restart, native client behavior or deployment readiness. Those gates remain open.


## Arsenal deadline startup validation (2026-09-27)

Current committed gameplay at **19f7471b** passes two actual isolated game and
PostgreSQL start/stop cycles using the same private candidate. All 18 source
overlays were compared to HEAD and already matched; the existing four excluded
owner Compile additions and project language-version override remain recorded
limitations, not a clean-release-build claim.

Executable SHA-256:
`E9604697A07AA538E51F91FDF9937766A9214930FA6B44049F07B3EC43614FC5`.

After each game exit the harness now verifies that
`pt_arsenal.enhancement_expires_utc_ticks` is a non-null bigint with zero default,
as well as retaining the 18 roadmap-table identities/owners across both cycles.
Both game starts reported ready listeners and Arena preparation before caches;
both shut down with exit zero and no forced stop. Final inspection found no
candidate listeners on 19558/19559/19619/25439 and no PostgreSQL PID file.
General Amber's private placement check was idempotent in both runs.

Commands are the same as the prior section. Evidence: ignored
`obj/arsenal-host-build.log`, `obj/arsenal-host-cycles.log` and the candidate's
redacted startup log. The private executable/manifest were refreshed with their
previous versions preserved; no new runtime tree, live restart, production
schema edit, client patch or public listener was introduced.

This is actual composed startup/schema validation for the arsenal change. It
contains no logged-in characters and does not prove earned guild/player recovery
across a Host restart, the separate guild-identity publication path, wire login
or native display. Those acceptance gates remain open.


## Boost receipt startup validation (2026-09-27)

Committed gameplay at **d3d89189** passes two actual isolated game/PostgreSQL
start-stop cycles. All18 committed source overlays matched HEAD; dirty production
Compile inputs remained excluded. The existing private candidate was reused,
with its prior executable and manifest preserved. The manifest still does not
claim a clean release build because the owner worktree/project remains modified.

Executable SHA-256:
`8695FBF5A3164F6D7A81A2305A11EC60309335D6C35C3D7C4DAC4D81BDB427C6`.

`Verify-MoonBoxDatabase.ps1 -PrepareQuestCatalog -CheckRoadmapStartup` now checks
19 runtime-owned tables, including `revora_perfection_boosts`, and validates
all10 required Boost receipt columns for exact type and non-nullability. Both
cycles retain relation identities and runtime ownership. The arsenal UTC deadline
check still passes. Game listeners become ready, Arena initializes before caches,
and both games exit zero without forced termination. Final inspection finds no
listeners on19558/19559/19619/25439 and no candidate PostgreSQL PID file.

Logs: ignored `obj/boost-host-build.log`, `obj/boost-host-cycles.log` and the
candidate's redacted startup log. No production schema change, public binding,
live restart, deployment or client installation occurred.

This closes actual startup/schema retention for Boost. It does not prove a
logged-in player's earned receipt recovery across a full Host restart or native
UI acceptance. The prior1242 component checks prove their documented component
reload scope, not those broader gates.


## Arena daily rune schema migration (2026-09-27)

Committed gameplay **2afad888** passes the private Host migration and restart
check using the existing candidate and tested build. All18 source overlays match
HEAD; unrelated dirty production Compile inputs remain excluded. Reused executable
SHA-256: `A3963C477201A8170ABF4060C435B7A142433042AAEF6911016EA6D0CE924C39`.
The previous candidate executable/manifest are retained; no clean-release-build
claim is made for the owner-modified project/worktree.

Before the first game start both daily Arena tables had **zero** rune columns.
The Host added `rune_type` and `rune_uid` to both tables without replacing their
relation identities. After game exit all four columns have the expected types,
nullability and zero default for the frozen rune type. The second game/database
cycle starts with all four columns present and preserves the same21 runtime-owned
tables. A follow-up two-cycle run passes the tightened null-safe default check;
all four columns are retained without a second migration. The count includes the
prior19 tables plus solo and Team Arena daily rewards.

Every game reached its loopback listeners, prepared Arena before caches and
exited zero without forced termination. Final inspection finds no candidate
listeners on19558/19559/19619/25439 and no PostgreSQL PID file. Logs: ignored
`obj/arena-rune-host-cycles.log`, `obj/arena-rune-host-repeat.log`, the existing
`obj/arena-rune-build.log` and candidate redacted startup log.

This is real startup/schema migration and idempotence evidence. No synthetic
characters or awards were inserted. It does not prove earned-player recovery
across a full Host restart, native acceptance or deployment readiness. The
1414 component checks retain their separate scope. Live services, production
schema and client installation were untouched.

## Guild purse migration and Super Guild recovery (2026-09-27)

Private gameplay build **863aac10**, SHA-256
`E439714A3B446A269260B54256294683F4E7ED9DAE289DBB916FE343EE5DA965`,
passes two actual Host/database start/stop cycles. The previous candidate EXE and
manifest remain available for rollback. The18 committed-source overlays were
verified before refresh; the owner-modified project still prevents a clean-release
build claim. No live service or production database was touched.

`Verify-MoonBoxDatabase.ps1 -PrepareQuestCatalog -CheckRoadmapStartup
-CheckSuperGuildRecovery` checks25 runtime-owned relation identities, including
regular Guild War purses and the three Super Guild battle/capture/purse tables.
It verifies27 additional guild column type/nullability expectations alongside the
existing Arena rune, Boost and arsenal deadline checks. The same relation
identities and expected columns survive the second game/database cycle.

After the first game exit, the recovery option stages three uniquely identified
private battle fixtures and two synthetic purse receipts. The second real Host
cancels the active battle without removing its capture, retains the completed
result and exact pending/paid purse fingerprints, and seals the old empty result
without inventing rewards. These are storage-only rows, with no character,
account or actual reward item; they do **not** establish authenticated gameplay
or actual item recovery. Successful verification transactionally removes only
its own UUID-scoped fixtures and confirms that none of its battle rows remain.
A failed run retains its fixtures for inspection instead of silently recreating
or overwriting them.

Both Hosts reached their loopback listeners, initialized Arena before caches and
exited zero without forced termination. Final inspection found no listeners on
19558/19559/19619/25439 and no candidate PostgreSQL PID file. Log: ignored
`obj/super-guild-host-cycles.log` plus the existing redacted startup log. The
candidate remains stopped.

This closes private schema startup/idempotence and synthetic storage recovery
across a real process/database restart. Earned authenticated-player crash/restart
recovery, postcommit transport failure and genuine-client presentation remain
separate pending gates; the1,031 ordinary component checks retain their own scope.

## Clan War soul migration and mixed-claim retention (2026-09-27)

Gameplay **df725485**, including the Super Guild lost-reply correction, passes two
actual private Host/database cycles. Reused tested executable SHA-256:
`7E5FAA713CBA4CAD780D49B7E25325C77636B5F5EE7BAA6AC8310A7D07FDE64C`.
The 18 source overlays match HEAD; owner project changes remain excluded from a
clean-release-build claim. The previous EXE/manifest are retained for rollback.

Before the first game start the existing Clan award table had **zero** soul
columns. `-CheckClanPurseRecovery` stages a uniquely identified private completed
run and legacy pending/paid currency receipts using the old columns. The real Host
adds `soul_type`/`soul_uid`; both old receipts retain exact currency/payment
fingerprints, type0 and null item UID. After the first Host exit, the verifier adds
two storage-only examples of new pending/paid soul receipts. The second real
Host/database cycle retains all four receipts exactly. The verifier checks both
column types/nullability and the type0 default. It removes only its own UUID-scoped
fixtures after success and confirms that the fixture run is gone.

The roadmap check now fingerprints 26 runtime-owned tables, adding Clan awards to
the prior 25. Existing 27 guild, four Arena rune, Boost and arsenal column checks
remain; two Clan column checks are added. The separate Super Guild synthetic
recovery check also passes on this build. Both games reach their loopback
listeners, initialize Arena before caches and exit zero without forced stops.
Final inspection finds no listeners on 19558/19559/19619/25439 listeners or PostgreSQL PID file.

Command: `Verify-MoonBoxDatabase.ps1 -PrepareQuestCatalog -CheckRoadmapStartup
-CheckClanPurseRecovery -CheckSuperGuildRecovery`. Log: ignored
`obj/clan-war-host-cycles.log` and existing redacted startup log. The candidate is
stopped. These fixtures create no characters or actual reward items, so this is
real migration/storage-retention evidence, not authenticated earned-player crash
or native-client acceptance. No live deployment, production schema change, live
restart or client installation occurred.


## Elite Guild War Host schema and result retention (2026-09-27)

Gameplay **89a9d09e**, including the public Elite Guild envoy, atomic prize and
rolled-back capture retry, passes two actual private Host/database cycles.
Reused tested x86 executable SHA-256:
`6D305D1FFB5B33DB9A382EA1976A1583043B6C5FA083EBBB409D5DED409BA4E1`.
All18 committed-source overlays were rechecked against HEAD. The previous EXE
and manifest are preserved; unrelated owner work remains excluded from a clean
release-build claim. No extra source rebuild was needed for this verifier batch.

The roadmap check now fingerprints **28 runtime-owned tables**, adding the two
Elite Guild tables, and checks all18 new column types/nullability. Existing guild,
Clan, Arena, Boost and arsenal checks remain. Both games complete the public-envoy
initialization path before opening their loopback listeners, prepare Arena before
caches, and exit zero through normal close without a forced stop. This confirms
startup composition; ordinary NPC interactions were tested separately in the
1,070 component checks, and native navigation/rendering remains unverified.

`-CheckEliteGuildRecovery` requires the roadmap check. After first Host exit it
stages four UUID-scoped **storage-only** battles: active with a capture, won with
an unclaimed prize, won with a paid receipt, and empty. No account, character or
actual reward item is created. The second real Host/database cycle cancels the
active battle, retains its capture, and preserves exact full-row fingerprints of
the other three results. The synthetic paid receipt includes claimant, item UID
and effect expiry, so those fields are included in retention verification. The
verifier removes only its own fixtures after success and confirms zero remaining
battle/capture rows for those UUIDs; failures retain evidence for inspection.

Super Guild and Clan receipt-retention checks also pass. Clan soul columns were
already present before this run; this repeats old/new terms retention, **not** the
original zero-column migration demonstrated in the previous checkpoint.

Command: `Verify-MoonBoxDatabase.ps1 -PrepareQuestCatalog -CheckRoadmapStartup
-CheckEliteGuildRecovery -CheckSuperGuildRecovery -CheckClanPurseRecovery`.
Log: ignored `obj/elite-guild-host-cycles.log` plus the redacted candidate startup
log. Final inspection finds no listeners on19558/19559/19619/25439 and no candidate
PostgreSQL PID file. The candidate is stopped. Authenticated earned-player crash
recovery, actual transport failure and native-client acceptance remain separate
pending gates. No live deployment, live restart, production schema mutation or
client installation occurred.


## Actual Account listener authentication prerequisite (2026-09-27)

`Verify-MoonBoxDatabase.ps1 -PrepareQuestCatalog -CheckRoadmapStartup -CheckWireAuth`
passes twice on the existing build89a9d09e candidate. The opt-in startup check runs
a bounded client while the real Host is listening, before normal close. It checks
the process path and reference-assembly hash, obtains only the private account's
DPAPI-held password, and connects only to127.0.0.1:19558. Runtime/schema identity,
loopback listeners, normal zero-exit close and28-table retention still pass.

The existing ordinary `MoonBoxTester` account receives a real653-byte SRP challenge,
proves its password and obtains an account-to-game handoff to127.0.0.1:19559.
A proof replay coalesced into the same write is rejected; a separate corrupted
proof is rejected. Client reads use bounded framing and exact-read loops, with
connection/read/write/process timeouts. The login uses split writes, without a
claim about actual TCP receive segmentation. Credentials, proofs and grants are
not printed or saved; frame buffers are cleared after each connection. No
character is created and the game handoff is not consumed in this stage.

This upgrades the former in-process account-verifier evidence to **actual Account
listener authentication across real Host restarts**. It does not establish game
DH exchange, character entry, gameplay or earned-reward crash recovery. Those are
the next separate gates, followed by native-client acceptance. The actual server
may record this private account's successful activity through its normal path.

Log: ignored `obj/account-wire-host.log`. The network probe and launcher are
`tools/CandidateAccountWireProbe.cs` and
`tools/Invoke-CandidateAccountWireCheck.ps1`; the proof calculation reuses the
existing independent SRP fixture. No gameplay rebuild or candidate refresh was
needed. The candidate is stopped; no production service or client was changed.


## Actual Game handoff and replay rejection (2026-09-27)

The Account probe now performs Game DH, sends the sealed 1052 handoff and requires
an exact NEW_ROLE response for the ordinary characterless private account. Reusing
the same grant on a second Game connection exposed a real defect: the old code
returned NEW_ROLE before consuming the grant. GeneralData now consumes it under
LoginSyncRoot before either successful branch and disconnects absent grants.

The negative check failed before the fix and passed afterward in two actual Host/
database cycles, alongside Account proof rejection and 28-table retention. The
isolated build passed; both closes were graceful, exit 0. Final inspection found
no private listeners on 19558/19559/19619/25439. No production restart occurred.
Candidate EXE SHA-256:
`7C6FC3DFD443D731371B4A4236C92257EEE6ED386DA50CE47F153E17D470DC40`.
It is an isolated build from 6dc422d2 plus the handoff fix, not a clean release;
unrelated owner source changes remain excluded through validation overlays.

Command: `Verify-MoonBoxDatabase.ps1 -PrepareQuestCatalog -CheckRoadmapStartup -CheckWireAuth`.
Evidence: ignored `obj/game-handoff-replay-before.log` and
`obj/game-handoff-replay-after.log`. Replay must produce EOF/reset; timeout fails.
Character creation, existing-character entry, earned-reward interruption recovery,
DH receive fragmentation and native DX9 acceptance remain unverified by this probe.


## Ordinary character persistence and login order (2026-09-27)

`Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckWireAuth -CheckCharacterEntry`
now creates the private character through packet1001 when the account has no
character, or authenticates its existing character. It requires bounded, sealed
character-info with the expected name/class/UID; a second Game-grant use must
close. The SQL preimage rejects other characters or a privileged account. After
normal Host close, account/entity/creation-receipt identity and spawn are checked.
The next database/Host cycle must retain the same UID. No gameplay reward is seeded.

The first run created RoadmapProbe (1096321), but exposed a real login failure:
BloodshedSeaBalance rebuilt stats before persisted stat dependencies were loaded.
A private stack-only diagnostic confirmed LoadItemStatsCore as the failing call;
the final build removes that diagnostic. EntityTable now restores boost state
without calculating stats, and LoadData calculates after items, subclasses, spells
and Chi. The persisted boost flags still participate in the final calculation.

After the fix, two real Host/database cycles passed. Both returned the same
character-info; SQL verified ordinary account state2, one owned character, its
creation receipt, Trojan10 and map1002(327,303). Both shutdowns were graceful,
exit0; all28 runtime tables retained. The private character remains for later
reward tests. Empty-character catalog/account checks must not be combined with
this mode; preparation's empty-player guard is unchanged. The old NEW_ROLE-only
probe no longer applies to this now-populated private account.

Isolated EXE SHA-256:
`0AE9B8DB4B748840D2B8466B3833981BA670E7958850B3D8E70CF9C7DEE76622`.
Build passed with existing legacy warnings, from1adf96c1 plus this fix and owner
WIP excluded via source overlays. It is not a clean production release.
Logs: ignored `obj/character-wire-after.log`, `obj/character-load-fix-build.log`.
No production service or native client was changed.

Boost regression validation: the unmodified suite is blocked by its unrelated
static census (expects518 item cases, committed source has517). An ignored fixture
using committed source and explicitly skipping the two static census assertions
passed521 checks including shared setup and all boost runtime assertions. Receipt,
expiry, failure recovery, stacks and replay checks passed. The checked-in reload
fixture now rebuilds after items/spells and measures exact boost contributions
against the loaded baseline; extra saved stats must not be mistaken for stacking.
Logs: `obj/character-boost-committed.log` (census failure) and
`obj/character-boost-runtime.log` (bounded runtime pass). This is not a claim that
the entire unmodified suite passed; the census maintenance remains open.

World loading, ordinary earned rewards across a Host interruption, DH receive
fragmentation and native DX9 acceptance remain pending.


## Ordinary world handshake and Courier acceptance (2026-09-27)

The explicit character probe now requires UID/action acknowledgements for source-
derived 10010 actions SetLocation, ConfirmFriends/Proficiencies/Spells/Guild,
GetSurroundings and CompleteLogin. It then jumps four tiles to323,303, confirms
that position through SetLocation, and interacts within Cerys's normal six-tile
range at319,303. The ordinary2031 NPC-init handler receives options0/1. Bounded
2032 replies must finish and expose option3, corresponding to StageAccept.
A repeat acceptance must retain that stage. The player jumps back to327,303;
server-reported location and post-close SQL identity/spawn checks must agree.

Two real Host/database cycles passed. The first reported `restored=False` and
accepted the quest. The second was required to report `restored=True` before
acceptance, proving restored quest access through the actual ordinary connection.
Game-grant replay and invalid Account proof rejection still pass. Both closes
were graceful, exit0;28-table retention passed. The existing private character and
accepted Courier stage remain saved. No direct quest-variable, character-level or
reward insertion occurred. Log: ignored `obj/world-courier-wire.log`.

Command remains `Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckWireAuth
-CheckCharacterEntry`. The second cycle explicitly sets RequireCourierResume.
Probe dependency checks now also cover the installed protobuf-net DLL. Probe
failures expose only exception type and a fixed check-stage label, never packet
bodies, credentials or grants. No gameplay rebuild or candidate refresh was
needed; this used the previous isolated0AE9B8DB build. All private listeners and
the candidate database are stopped afterward.

This proves server-side world handshake, short movement and persisted quest
acceptance. It does not prove native rendering, the native2032 selection route,
Courier combat/turn-in rewards, abrupt interruption recovery or other quests.
No quest reward or wardrobe-producer completion count increased.


## Courier dialog selection and hunt access (2026-09-27)

The same character-mode command now uses2031 only for initial NPC clicks and
fixed2032 packets for options. In the first cycle, Cerys's option3 delivered the
ordinary player to1002(442,339); Commander91001 offered option10 and accepted the
report. Cerys then exposed hunt option4. It delivered1011(130,250), verified by
SetLocation; public action177 returned to Twin City, followed by short jumps back
to Cerys and the saved spawn. Repeated acceptance preserved the hunt stage.

The second actual Host/database cycle required restored hunt before changing any
stage and passed. Both closes were graceful, exit0; ordinary state2 account,
character/receipt/spawn identity,28-table retention, invalid SRP proof rejection
and single-use game grants still pass. Ignored `obj/courier-hunt-wire.log` records
hunt-restored=False then True. Existing gameplay build reused; no production or
client changes. The private character remains at327,303 with the hunt pending.

This closes the prior server2032 selection-path gap. Native rendering,20 actual
Bandit kills, clerk/turn-in rewards and abrupt interruption recovery remain open.


## Actual Courier earning and saved reward (2026-09-27)

The real socket journey exposed a public travel defect: the old1011(130,250)
landing and attempted130,265 exit are blocked in the loaded terrain. Cerys now
sends players to112,272, a clear five-by-five landing connected to the Bandit
fields. The reviewed944x9441011.map SHA-256 is
`7E8F7A8262F8AE93A314699AF6D276F0324FFF6C32BCC6061BFFC2F336F1E817`.
The probe verifies that hash, landing footprint and short-jump endpoints. This
proves server walkability; native rendering is still a separate gate.

No levels, monsters, kill counters or rewards were inserted by the test. The
ordinary level1 character spent54 of its10000 starter CPs on two normal mall EXP
Balls, consumed them through1009, and reached levels30 then35. Item receipt/removal,
CP deductions and level updates were observed on the socket and level35 survived
restart. From Cerys it traveled, approached actual visible Bandit/BanditL33 spawns,
sent ordinary1022 melee requests with no client-provided damage, and required
server-attributed kill messages. Cerys's count advanced exactly once per death.
Twenty kills were earned in bounded batches, with progress retained across Host/
database restarts. The private account stayed ordinary state2 throughout.

After the twentieth kill the ordinary clerk route unlocked turn-in. The client
received exact deltas of8000CP,80000silver and200RVP, level120, and one new bound
soul UID59752588/type823058. The active catalog confirms CraneRing is P7. SQL
confirmed the single life0 receipt, its configured VIP6/seven-day terms, level120,
and the same owned inventory item. Duplicate turn-in produced no additional
wallet or item grant. Two subsequent real Host/database cycles retained the
exact receipt fingerprint and the bound permanent item and again rejected replay.

Validation: isolated gameplay build passed (legacy warnings retained), EXE SHA-256
`7B3E6FE16454B0AD7E94389A74486FFA5DD7FF0F2D5A3336D53CEFCDDE3E7FCF`.
It is413742b6 plus the landing fix with owner WIP excluded by validation overlays,
not a clean release. Probe compilation uses warnings as errors. Existing table,
identity, invalid-proof and game-grant replay checks remain enabled. Every Host
close here was graceful, exit0; private listeners/database are stopped. No live
service, client, production database or pricing was changed.

Command: `Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckWireAuth
-CheckCharacterEntry`. Three completed full-hunt logs record2->8,8->14,14->20;
the preceding combat check recorded0->2. Ignored logs:
`obj/courier-preparation-wire.log`, `obj/courier-combat-after.log`,
`obj/courier-full-wire-1.log` through`-3.log`, `obj/courier-earned-restart.log`.
The character remains at1002(327,303), with completed Courier and its earned soul.

Abrupt interruption, ambiguous commit recovery on the actual transport, native
rendering/use and other reward families remain open. Legacy mall atomicity is
not certified by the one-time EXP Ball purchase checks.


## Courier live-session process interruption (2026-09-27)

`Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckWireAuth
-CheckCharacterEntry -CheckCourierCrash` passed against the same isolated
candidate used for the actual 20-kill Courier completion. The game executable
was reused; no new gameplay build or deployment was needed.

The first cycle authenticated ordinary RoadmapProbe, loaded its paid Courier
state, rejected another turn-in and returned to the saved spawn. While its Game
TCP connection remained open, the probe emitted a fixed checkpoint. The parent
terminated only its owned, path/hash-checked private Revora process (exit -1).
The probe required EOF or a reset/abort on that live connection; timeouts fail.
This cycle did not send the normal shutdown command. PostgreSQL then shut down
normally and restarted for the second cycle.

The second cycle authenticated again, restored the completed quest, refused
another claim and closed normally (exit 0). Both cycles retained the exact
pre-existing Courier receipt fingerprint, the same bound permanent inventory
soul and level 120. The private database and all candidate listeners were
stopped afterward. Local evidence: `obj/courier-crash-recovery.log`.

This proves recovery of a previously committed, actually earned reward after
an abrupt **game-process** termination with an active player. It does not prove
an in-flight/ambiguous reward commit, database crash, machine power loss, or
native DX9 rendering. Those gates remain open. Crash mode requires a previously
earned receipt; it cannot manufacture quest completion or rewards.


## Soul Trial ordinary acceptance and travel (2026-09-27)

`Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckWireAuth
-CheckCharacterEntry -CheckSoulTrialAccess` passed two real isolated game/database
cycles, using the existing candidate executable and ordinary RoadmapProbe.
The probe jumped near Caelan (900000013), accepted through normal NPC packets,
used each offered travel option, checked its actual destination, and returned
through the public Twin City action. Destinations were 1015(717,571),
1020(565,562), and 1000(500,650). Loaded terrain marks all three cells walkable.

After every journey Caelan still reported zero Birdmen, Macaques and Sand
Monsters, with no turn-in option. The second process restored the accepted trial
without accepting again, and all three travel checks passed again. Both cycles
ended at the saved Twin City spawn and shut down normally. Existing earned
Courier receipt/item checks also passed. Local log: `obj/soul-trial-wire-access.log`.

No Soul Trial kills, reward, or career title are claimed by this check. The next
step is actual combat against the loaded wilderness families (configured default
50 each), then normal turn-in, reward verification and recovery. Read-only catalog
inspection found ordinary Macaque47, SandMonster67 and Birdman87 populations;
stronger named variants must not be mistaken for the ordinary targets. No live
service, catalog, client or production character was changed.


## Soul Trial actual monster combat (2026-09-27)

The ordinary level-120 character continued its previously accepted trial through
real Game TCP combat, with no seeded progress or item/stat changes. The private
probe accepts `-CheckSoulTrialAccess -SoulCombat 1|2|3` (Birdman, Macaque,
SandMonster). It validates the hash and dimensions of each loaded terrain, moves
through walkable endpoints, selects an exact ordinary family name from visible
spawns, sends normal melee, and requires the server's kill attributed to this
character. Caelan must then report exactly one additional kill in that family.
A timeout, player death, unavailable visible target or wrong counter fails.

Two real game/database cycles passed per family. Caelan reported 1/50 on the
first cycle and 2/50 on the second, establishing retained earned progress rather
than merely accepted flags. The same UID now has Birdmen2, Macaques2, Sand
Monsters2. Existing Courier paid receipt and bound item remained intact.

The original short desert route found no ordinary SandMonster and correctly
failed before a kill. The probe subsequently recovered the saved desert player
through the normal public return action, and an extended walkable route through
the loaded spawn area passed both combat cycles. No database teleport, synthetic
death or quest-counter write was used. Local logs:
`obj/soul-macaque-wire.log`, `obj/soul-bird-wire.log`, and
`obj/soul-sand-wire-extended.log`. The initial failed desert attempt is retained
in `obj/soul-sand-wire.log`.

These checks do not complete the 50/50/50 hunts, Soul Trial turn-in, its reward
or Honored Hero. Continue this same character to those requirements. Native
client and deployment gates remain pending; all private processes were stopped.


## Soul Trial full earning, reward and restart (2026-09-27)

The same ordinary RoadmapProbe completed all50 Birdmen,50 Macaques and50 Sand
Monsters through actual Game TCP melee and Caelan's public travel/dialog routes.
All150 deaths were attributed to the character; no counters, reward items,
levels or stats were inserted for this hunt. The previously earned Courier
level120 profile and starter equipment were reused.

The probe now supports bounded batches (`-SoulCombat 1|2|3 -SoulBatch 1..50`),
clamped to the remaining displayed objective. It refreshes visible spawns before
each fight, uses Euclidean jump distances consistent with the production server,
checks the loaded terrain hash, and permits one ordinary Caelan return/travel
when its search route has no target. A child deadline of240 seconds bounds each
combat connection; normal character-only checks retain their60-second limit.
No gameplay limits or production settings were relaxed.

Intermediate probe failures remain recorded: a diagonal-distance error caused a
jump disconnect; stale visible positions prevented one death confirmation; a
short desert search exhausted its targets. Earned progress survived these
interruptions. The final Sand Monster batch reached completion, but the probe
initially rejected Caelan's changed text: `ProgressText` replaces numbers with
"The hunt is done" once all three objectives are complete. The corrected probe
recognizes that normal transition and does not invent missing counters.

The final two-cycle run (`obj/soul-earned-turnin-restart.log`) passed:

- Normal turn-in through Caelan delivered exactly500 RVPs and50000 silver,
  left CPs and level120 unchanged, and added15 unbound723017 EXP Potions plus one
  bound soul (UID59789265, type820073).
- The active `itemtype-new.txt` identifies820073 as MoonHeadgear with
  PurificationLevel7. This verifies grade; native purification/equip presentation
  is not implied.
- Same-session repeat turn-in was refused without wallet/item growth.
- A real game/database restart restored the paid state and refused another
  claim. SQL retained the exact receipt fingerprint and all16 distinct permanent
  reward items, owned by the ordinary character in its inventory. The receipt
  has life0, target_level120, rvp500 and silver50000.
- The existing Courier receipt and permanent bound soul remained intact.
  Both Host exits were0; all candidate listeners and PostgreSQL were stopped.

Combat evidence is also retained locally in `obj/soul-macaque-batch-extended.log`,
`obj/soul-macaque-complete.log` (interrupted second cycle),
`obj/soul-macaque-finish.log`, `obj/soul-bird-fresh-targets.log`,
`obj/soul-sand-complete.log` (interrupted second cycle), and
`obj/soul-final-turnin.log` (completion-text expectation failure).

This closes actual ordinary Soul Trial earning/turn-in and normal-restart
reward acceptance for life0. It does not certify an in-flight ambiguous commit,
abrupt post-award Soul Trial crash, later rebirth lives, native DX9, or production
deployment. Path Hunt and both rebirth rites remain required for this character's
Honored Hero career. Existing candidate gameplay binary reused; no live change.


## Path Hunt full ordinary earning and reward recovery (2026-09-27)

The same ordinary RoadmapProbe accepted Rowan's Path Hunt through normal NPC
packets and earned20 Pheasants,20 Robins,20 Turtledoves and20 Apparitions via
actual Game TCP melee. All four public travel destinations and return journeys
passed. Ten-kill batches across two real game/database cycles per family
verified exact counter growth and restored progress. No quest counters, deaths,
materials, levels or final receipts were seeded.

The shared hunt probe now selects the Path Hunt family with `-CheckPathHunt
-PathCombat 1|2|3|4 -SoulBatch 10`, alongside the existing character/Soul Trial
checks. Both hunts reuse bounded visible-target, movement, actual-death,
completion-dialog, reward and replay checks; new Path Hunt traversal uses the
hash-checked824x824 Twin City terrain (SHA256
`9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10`).
`-CheckPathHunt` requires the existing ordinary-character and Soul Trial checks.

Rowan's normal turn-in delivered exactly100 RVPs,25000 silver,11 unbound EXP
Potions723017 and one bound soul; CPs and level120 were unchanged. The soul is
UID59800984/type823061, identified by the active catalog as LionHeavyRing with
PurificationLevel7. SQL confirmed a valuable life0 receipt and all12 distinct,
permanent reward items in the ordinary character's inventory. Same-session
repeat turn-in was refused without wallet/item growth.

Two additional actual game/database cycles restored the paid quest and refused
repeat claims. The exact receipt fingerprint and all12 reward items survived;
Courier and Soul Trial receipts/items were also verified unchanged. All processes
closed normally with exit0, and the private database/listeners were stopped.

Local logs: `obj/path-pheasant-wire.log`, `obj/path-2-wire.log`,
`obj/path-3-wire.log`, `obj/path-4-wire.log`, and `obj/path-earned-restart.log`.
This is ordinary socket earning and normal-restart acceptance, not an abrupt
reward-commit crash, later-life Path Hunt, native presentation or deployment
claim. The existing private gameplay executable was reused without rebuilding.
The same-character Honored Hero journey now has actual earned Courier, Soul Trial
and Path Hunt receipts; first and second rebirth remain outstanding.


## Ordinary Trojan promotion prerequisites (2026-09-27)

The same earned-career character reached TrojanStar through the actual Twin City
portal1002(475,293) to1004(59,88), walked/jumped near the installed trainer10022 at
(48,45), and used normal NPC offer/confirmation dialogs. The unchanged catalog
and hash-checked1002/1004 terrain were reused. Return through1004(51,73) reached
1002(477,297), followed by the ordinary public return route to the saved spawn.

Class10->11 and11->12 each succeeded on ordinary authenticated sessions, with
immediate class updates, no currency/item charge, and refusal to replay the
consumed offer. These two ranks legitimately require no materials in the loaded
rules. After reconnect, class12 persisted together with achievement receipts
10819 and10820. The next promotion displayed its Emerald requirement and refused
confirmation without that material. No Emerald, rank or receipt was seeded.

The first two runs subsequently timed out awaiting another promotion dialog;
the more precise second diagnostic identifies NPC10022 option1. Reconnecting
restored the committed rank and allowed the next service request. This is an
**unresolved immediate post-promotion dialog issue**, not evidence that consecutive
in-session promotion is reliable. No cause or server fix is claimed by this batch.
The successful final run exercises the restored class12 material refusal and
normal portal exit twice, rather than replaying already-earned promotions.

Local logs: `obj/trojan-promotion-wire.log` (earned11, later timeout),
`obj/trojan-promotion-resume.log` (earned12, later timeout), and
`obj/trojan-promotion-persistence.log` (two complete restart cycles).
The last run retained all three quest reward sets/receipts, class12 and both
promotion achievements; both game exits were0 and candidate processes stopped.
The opt-in `-CheckPromotion` requires the ordinary Path Hunt/Soul Trial checks.

Remaining prerequisites are Emerald/Meteor/Moon Box for ranks13/14/15 and the
Heavenly Stone recipe/first rite, then the second-life prerequisites and rite.
First rebirth requires the final rank, so level120/class12 alone is insufficient.
Native rendering, the post-promotion dialog issue, and deployment remain open.


## Promotion follow-up timeout resolved in the probe (2026-09-27)

The earlier timeout also reproduced on class12 without any promotion: prepare
an Emerald offer, confirm without the material, repeat the consumed confirmation,
reopen the greeting, and select promotion again. Thus a rank transaction was not
required. A temporary private diagnostic build observed the first three2032
selections reaching the handler with action2, a fully loaded player, nearby NPC
and no reload flag; the fourth never reached that handler.

`Client/GameState.cs` configures2032 at three packets per window;
`Client/PacketFilter.cs` uses a500ms window. `PacketHandler.HandlePacket` applies
that filter before NPC dispatch. The unpaced probe's fourth selection exceeded
this existing limit. The probe now waits250ms before each nonzero selection;
server rate limits and promotion code are unchanged. The previously failing
repeat/reopen sequence passed in two full game/database cycles on the original
candidate binary, with all three earned quest reward sets and both promotion
achievements retained. No rank or materials were manufactured for the diagnosis.

Local evidence: `obj/promotion-dialog-repeat.log` (failure without promotion),
`obj/promotion-diagnostic/run.log` (private diagnostic failure), and
`obj/promotion-paced-dialog.log` (two passing cycles). The diagnostic executable
SHA256 was `F4B445028FA1FB7563845A76359BECB5687E62B85D7BC1C4776C53CDA43D1FA8`;
its scoped source copies/targets remain ignored under `obj/promotion-diagnostic`.
It was replaced with the original executable SHA256
`7B3E6FE16454B0AD7E94389A74486FFA5DD7FF0F2D5A3336D53CEFCDDE3E7FCF`
before the passing run. Temporary diagnostics are not a release or source change.

This supersedes the unresolved timeout classification above: it was a probe
request-rate issue, not demonstrated promotion-state corruption. The character
is still legitimately class12; ranks13-15 and both rebirth rites require their
remaining materials. Native client and deployment acceptance remain open.


## Earned-fund Trojan material promotions (2026-09-27)

The ordinary career character used its earned CP purse to buy one Emerald1080001
for1 CP and one Meteor1088001 for3 CPs through the actual mall2888 purchase
packet. Both prices and stock entries were read from the active item/EShopV2
catalog; no catalog or price override was introduced. Exact immediate debits and
one new material UID per purchase were verified.

The character then reached TrojanStar through the actual school portal and
completed class12->13 and13->14 in the same session. Each offer was explicitly
prepared/confirmed; the expected material UID disappeared, no further currency
was charged, and the second promotion added one normal Rainbow Gem700031.
Replaying each consumed offer was refused. The paced follow-up dialogs worked
without reconnecting, corroborating the preceding probe rate-limit correction.

The final-rank offer required a Moon Box and refused confirmation without one.
A subsequent real game/database restart restored class14 and achievement receipts
10819-10822, and allowed the material-refusal/reopen dialog again. The three quest
reward sets/receipts remained intact. A bounded read-only database check after
the run confirmed no Emerald/Meteor remains and exactly one permanent unbound
Rainbow Gem is in the ordinary character's bag. The same item assertions are now
part of the promotion verifier. This material claim does not imply crash-atomic
purchase acceptance for the generic legacy mall implementation.

Evidence: `obj/trojan-material-promotions.log` (two actual cycles), with the
read-only assertion in ignored `obj/check-promotion-materials.ps1`. Original
candidate executable reused, normal exits0 and private database stopped.
Remaining: Moon Box earning/final promotion, Heavenly Stone acquisition and the
first rite, followed by second-life prerequisites and the second rite. No native
or production-deployment gate was closed.


## Ordinary Moon Box entry and durable resumption (2026-09-27)

The same ordinary class 14 career character physically reached Fortune Teller
at Twin City 489,622, entered map 1042 at 28,33 and opened Maggie's public dialog.
The probe then used ordinary return travel to 1002(327,303). No database profile,
quest progress, materials or reward receipts were supplied.

`Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckWireAuth
-CheckCharacterEntry -CheckSoulTrialAccess -CheckPathHunt -CheckPromotion
-CheckMoonAccess` passes two complete private game/database cycles. A read-only
fingerprint before and after each cycle proves the existing run identity and
all saved fields remain unchanged on resumption. It remains in portal phase,
area 1042, with six zero counters, six empty token slots, no jade and no result
receipt. Wire assertions verify no currency or inventory change from entry.
All preceding earned quest rewards and promotion receipts remain intact.
Evidence: `obj/moon-access-persistence.log`; initial entry is also recorded in
`obj/moon-access-wire.log`. Both are ignored local logs. Private game exits were
normal (0); the private database is stopped.

The first approach failed because the probe landed on an NPC-adjacent tile:
`Map.AddNpc` reserves the eight neighboring cells. The corrected approach stops
at 485,618 and Maggie at 22,22, within ordinary talking distance. Server movement
rules and the candidate binary were not changed. This is socket entry/resumption
evidence only: tactic combat, six earned tokens, jade, reward, final promotion,
rebirth and native rendering remain unverified in this career. No live deployment.


## Six Moon Box tokens earned through combat (2026-09-27)

The existing ordinary level 120, class 14 character earned all six distinct
Moon Box tokens through actual ToughHorn combat over Game TCP. Saved counts by
map 1043?1048 are **3, 3, 7, 4, 7, 3** (27 kills). The item UIDs in that order
are 59829082, 59829083, 59826280, 59829080, 59827687 and 59825030. No profile,
material, counter, RNG, monster-health or damage overrides were used.

The bounded `-CheckMoonCombat` extension follows Maggie's normal random routes,
uses hash-verified terrain, attacks visible ToughHorns and observes killer-owned
death packets before checking earned inventory. It preserves already earned
materials and returns through each tactic Ghost. An initial route encountered a
blocked search waypoint; the probe now selects nearby valid terrain. The failed
run's three tokens and partial fourth-tactic progress resumed through normal
login; no progress was reset.

Command: `Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckWireAuth
-CheckCharacterEntry -CheckSoulTrialAccess -CheckPathHunt -CheckPromotion
-CheckMoonAccess -CheckMoonCombat`. `obj/moon-six-tokens-wire.log` records two
passing game/database cycles. Read-only SQL checks each token's exact saved UID,
matching design, ordinary character ownership, bound/permanent bag state and
1?20 kill count. The second cycle retains the exact complete run fingerprint.
Earlier Courier, Soul Trial, Path Hunt and promotion evidence/rewards also pass.
The game exits normally and the private database remains stopped.

This validates combat and six-token persistence, not the final quest claim.
The run remains portal/1042, no jade or completion receipt exists, and the
character returns to 1002(327,303). Death Tactic defeat recovery support was not
exercised by the successful routes and is not certified here. Soul Jade,
incorrect/correct Life ghosts, Moon Box/P7 reward, achievement 11022, final
promotion, rebirth and native acceptance remain pending. Production unchanged.


## Earned Moon Box completion and final Trojan promotion (2026-09-27)

The same ordinary level 120 character exchanged all six earned tokens with
Death's Ghost for one bound Soul Jade and reached Life Tactic. Eleven incorrect
Life ghosts preserved the jade and every existing item. The correct ghost
consumed the jade, awarded unbound Moon Box UID **59831583** and bound permanent
catalog P7 soul **800422**, UID **59831584**, and returned the character to town.
No currency changed. A repeated stale claim from town produced no additional
items; this tests proximity rejection, not a concurrent in-range claim race.

The character then physically entered the school and used the earned box for
Trojan **class 15**. Its exact UID disappeared, one unbound DragonBall appeared,
class updated immediately, the consumed confirmation could not promote again,
and the trainer refused any rank above the final rank. No extra currency charge.

`Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckWireAuth
-CheckCharacterEntry -CheckSoulTrialAccess -CheckPathHunt -CheckPromotion
-CheckMoonAccess -CheckMoonCombat -CheckMoonFinish` passes two real isolated
game/database cycles (`obj/moon-finish-wire.log`). Read-only verification proves:

- One unchanged completion receipt, two reward UIDs, consumed Moon Box, retained
  bound/permanent P7 soul, and no remaining token, jade or Moon Box material.
- The soul design has purification level 7 in the actual candidate catalog.
- Class 15, one unbound permanent DragonBall, promotion achievements 10819?10823
  and earned Moon Box achievement 11022 persist. Earlier quest rewards remain.
- The completed character starts no replacement Moon Box run during the second
  cycle. Saved location remains 1002(327,303); normal private exits, database stopped.

Only socket acceptance tooling changed. Gameplay binary, rewards and RNG remain
unchanged; no materials/profile/receipts were supplied. This completes this
ordinary character's Moon Box ending and first-life trainer promotion path.
First/second rebirth, Honored Hero, native rendering, alternative Moon Box
endings, in-flight crash/ambiguous-commit acceptance and deployment remain open.


## First rebirth earned through the ordinary NPC (2026-09-27)

The existing character used its earned CPs to buy the active mall's Heavenly
Stone/CelestialStone 721259 for 30 CPs. This is an existing ordinary purchase
route, not a supplied material or a new pricing policy. It does not certify the
separate mining/Clean Water/crafting acquisition route or legacy mall crash
atomicity. The first purchase persisted through a failed dialog and was reused
without another purchase after the fix below.

Actual first-rebirth dialog exposed a compatibility defect: character creation
admits male body 1006 and female body 2005 (`EntityTable.CreateEntity`), but
`FirstRebirth` accepted only 1003/1004/2001/2002 at the body-choice step. It fenced
the ordinary 1006 character before consuming the stone. `RebirthRules.SelectedBody`
now accepts both creation bodies and maps the selected slender/sturdy option to
the existing gender-preserving output sizes. Unsupported bodies remain rejected.
22 focused compiled-assembly checks cover all six supported inputs, both choices
and five unsupported inputs. Build passes with existing legacy warnings.

The same ordinary character then completed Sereth's class, body, reward and
final-confirmation dialogs: old class15/level120 becomes class11/level15,
rebirth1, body1003, with 72 total attributes and the correct remembered first
profession/level. One stone is consumed; no additional currency is charged.
The random super gem, 410077 weapon and permanent unbound 187405 garment appear
immediately. Existing items remain, and repeated confirmation grants nothing.

Two real game/database cycles pass with the prior career flags plus
`-CheckFirstRebirth` (`obj/first-rebirth-wire.log`). Read-only SQL checks the exact
consumed stone UID, receipt-bound gem/garment UIDs, unbound permanent rewards,
profile/stat budget, achievements 11030/10315 and unchanged receipt after reload.
All earlier quest, Moon Box and promotion receipts/rewards remain intact. The
new-life login is validated separately instead of falsely requiring old-life
level120 or replaying its quests. Game exits normally; database stopped.

Only the private candidate was refreshed. SHA-256:
`39D7D069AF532F5FD3E7269BAC795D84CAE95A61A6908EF4F49DEC13A1A9658E`.
The build uses the existing committed-source overlays to exclude unrelated owner
WIP; it is not a clean release claim. Refresh now reuses a hash-identical retained
executable backup and preserves differing manifest preimages, avoiding redundant
binary backups. The earlier executable remains available for rollback.

Second-life leveling/promotion, second rebirth, Honored Hero, real socket
mining/crafting acceptance, native rendering and deployment remain open. Female
2005 mapping is covered by the focused checks, not a second actual career run.


## Second-life Courier leveling and receipt isolation (2026-09-27)

The same ordinary reborn character bought and used two existing EXP Balls at
27 CPs each, reaching levels 28 and 33. It allocated earned free attributes
through the existing player packet (1024), verifying the echoed request and
exact strength/vitality increases and point deduction. This exercises the
implemented server packet; it is not new native-client layout evidence.

It then completed the second-life Courier route: Commander report, 20 actual
Bandit deaths, PurseClerk approval and Cerys turn-in. The first search exhausted
nearby visible targets after seven kills; the probe was extended to search
nearby valid terrain from its actual position. Login resumed all seven earned
credits and the remaining 13 kills completed in one hunting visit. No counter,
level, attribute, item or receipt was supplied through the database.

The turn-in's immediate changes were exactly 8000 CPs, 80000 silver, 200 RVPs,
level120 and one bound soul. Repeating the claim made no further change.
With `-CheckSecondLife` added to the previous complete career command,
`obj/second-life-courier-wire.log` records two passing game/database cycles.
Read-only checks retain the life1 receipt and its permanent bound item UID,
class11/rebirth1/level120, and **387 total attributes** (72 at rebirth plus
105 levels at three points each). All life0 quest receipts, Moon Box completion,
promotion achievements and first-rebirth receipt/rewards remain unchanged.

The verifier now distinguishes the original rebirth receipt from legitimate
later progression: level120 is accepted only with the earned life1 Courier
receipt, while the current-level attribute budget remains exact. It does not
replay Soul Trial or Path Hunt just to satisfy this career's title prerequisites.
Private game exits are normal and the database is stopped. Gameplay binary
unchanged from the first-rebirth body fix; no live deployment. Second-life
promotions, second rebirth, Honored Hero and native acceptance remain pending.


## Honored Hero earned and equipped through actual sockets (2026-09-27)

The ordinary career is now complete through Account/Game TCP. After its earned
second-life level120, the character bought the existing Emerald (1 CP), Meteor
(3 CPs) and MoonBoxPackage (30 CPs), opened the package for one unbound 721020,
and completed trainer ranks12?15. Required item UIDs disappeared, one additional
Rainbow Gem and DragonBall appeared, and no extra promotion currency was charged.
Two private cycles preserved that second-life final rank. The original combat-
earned Moon Box receipt and P7 reward remained unchanged.

The second-rebirth token's active price is **888 CPs**. `itemtype-new.txt` says
999, but `Constants.ItemBaseInfosPath2` loads `ItemtypeSub.txt` afterward;
`SafeDictionary.Add` replaces the existing entry, and that row says888. The first
probe expected999 and correctly failed its exact-price assertion after the
successful888 purchase. Its saved receipt and item were retained and reused;
no second purchase was made. No price was changed by this work.

Sereth consumed token UID59844286 and completed the second rite: rebirth2,
class11, level15, both remembered professions15/levels120 and **82 total
attributes**. Unbound weapon UID59845535 and permanent bound catalog P7 soul
800725, UID59845536, appeared. Other inventory UIDs remained, and confirmation
replay produced neither another rite nor another reward. The final bag holds
40 items; no capacity override was used.

Honored Hero20572057 was unavailable before the fifth prerequisite receipt:
the same ordinary equip request made no change. After the second rite,
wardrobe3301 updates granted ownership and the equip request succeeded. On the
next actual game/database restart, login packets restored the equipped title
before another equip request. Read-only SQL verifies one permanent, unrevoked
`honored-hero-revora-v1` / `career-v1` award and the equipped record in `titles`.
The resulting career has all five real prerequisite receipts, both ordinary
leveling/promotion paths and retained historical rewards, without supplied
profiles, quest progress, materials, receipts or clock overrides.

Full command: `Verify-MoonBoxDatabase.ps1 -CheckRoadmapStartup -CheckWireAuth
-CheckCharacterEntry -CheckSoulTrialAccess -CheckPathHunt -CheckPromotion
-CheckMoonAccess -CheckMoonCombat -CheckMoonFinish -CheckFirstRebirth
-CheckSecondLife -CheckSecondPromotion -CheckSecondRebirth`.
`obj/honored-hero-earned-wire.log` records two passing cycles. The SQL checks
include exact token purchase cost/balance/UID, consumption, reward ownership,
P7 catalog phase, old receipt fingerprints, current stat budget and equipped
permanent title. Private game exits are normal; database stopped. Binary remains
39D7D069AF532F5FD3E7269BAC795D84CAE95A61A6908EF4F49DEC13A1A9658E.

This closes one complete ordinary server earning route for Honored Hero and
its actual process-restart gate. Native DX9 rendering and deployment remain
pending. The alternative gem/water crafting and Abyss payment paths still need
actual-socket acceptance; this purchased-material career does not certify them.
Legacy mall/pack crash atomicity and in-flight failure recovery are not proven
by this normal purchase/opening run. Wardrobe classification becomes
`code-validated`, not native-accepted or deployed.
