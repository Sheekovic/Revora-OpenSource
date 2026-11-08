# Arena retained-result process recovery

2026-09-27. Private server-process validation; not native or authenticated-match acceptance.

`tools/Verify-ArenaProcess.ps1` reuses the fixed disposable PostgreSQL cluster on
127.0.0.1:25439. It clones its private candidate database into the fixed marked
`revora_candidate_arena_process` database. The original candidate's character and
quest history is not altered by fixture queries. A reset/removal requires that
exact name and the fixture ownership comment; failed clones are retained for
inspection. Successful validation removes only that marked clone.

`Invoke-CandidateArenaLifecycle.ps1` verifies the private runtime manifest and
x86 assembly, rejects occupied test ports, temporarily selects the clone, and
restores the original configuration bytes/hash in its finally block. It runs
only the loopback Account19558/Game19559/protection19619 candidate. Output is
redacted; credentials remain transient child environment values. PostgreSQL
executables retain the existing exact-hash checks and SCRAM requirement.

The earlier discarded probe failed before admission because its minimal entity
insert omitted full-schema required fields. The replacement is standalone x86
and uses the required column contract from CharacterCreation.Commit, without
relaxing constraints or depending on the x64 component fixture assembly.
Five supplied ordinary/non-GM profiles use reserved UIDs4294903001..4294903005.
Eligibility and match outcomes are controlled inputs, not an earned career.

## Lifecycle exercised

1. Start the actual game composition and verify prepared Arena state and both
   loopback listeners. Import one owned Solo match and one owned Team match,
   including a non-leader teammate, through the existing registered import path.
   Before the interrupted match, earn eight Solo results through that path and
   the normal completion pump, including return and ownership release each time.
   No saved match counts, wins, reward entitlements or items are supplied.
2. Suppress terminal result writes with fixture-only database triggers. Capture
   both real match intents, then request normal shutdown. Verify it returns
   pending with maps/session ownership and durable intents still retained.
3. The interrupted Solo result is the ninth win. Deliberately terminate the test process without its normal cleanup, remove
   only the fixture triggers, and restart PostgreSQL and the actual game.
4. Verify one Solo commit with two participant statistics and one Team result
   with three statistics. Verify expected winners, nine saved Solo wins,
   Solo1237/763 points and shared point mirrors. Restore the winner through the
   production character/item loaders and ordinary reward-delivery service.
   Verify500 Honor, one EXP Ball and one bound Tortoise Breaker rune from the
   recovered wins9 entitlement, with nothing awarded to the other participants.
5. Close normally and start/verify again: points, win/loss counters, current
   Honor, reward item UIDs and payment timestamp must retain the same fingerprint.
   Repeated delivery keeps exactly two reward items and one paid entitlement.

The probe does not use authenticated client sockets. It does not test natural
matchmaking, native invitation/combat/spectator UI, the custom prize
threshold, an authenticated socket return, or production deployment.
Those gates remain open. A process interruption uses deliberate Environment.Exit
after the refused shutdown; it is not a power-loss or kernel-crash simulation.

Reproduce after building and explicitly refreshing the private candidate:
`tools/Verify-ArenaProcess.ps1`. `-ResetOwnedFixture` is only for an inspected,
failed clone with the exact ownership marker. `-RecoverOwnedFixture` resumes an
already captured clone only when that marker, one captured run-pair and removal
of the injected triggers are verified. It preserves the existing earned results.
Logs remain ignored under the
private candidate root and `obj/arena-process-recovery-run.log`.

Validated: capture plus two verify launches pass, including a database restart,
normal final shutdown and replay fingerprint equality. Private game executable
SHA256: `B7CF8163BFB1523A89B87019CC2764587AB4068715F7634F4650A0598D1B8018`.
This build includes the equipment achievements, gem-embedding repair and the
recovered Arena rune correction below;
it excludes unrelated owner work through the existing committed-source overlay.
The prior private executable/manifest are retained; no live executable changed.

## Recovered reward parity defect and fix

The extended process test initially failed: the ninth result recovered, but its
new daily entitlement had rune_type=0. ArenaStartupRecovery.Rewards omitted the
rune column used by ArenaDailyRewards.Record. New startup-recovered wins9 and
matches20 entitlements now include4030101, matching the ordinary completion
contract. Existing rows remain unchanged through ON CONFLICT DO NOTHING;
historical entitlements and paid receipts are not rewritten.

The regression crosses real game and database restarts at the ninth result,
then production character/item reload and reward delivery. It proves the bound
rune is part of the paid recovered reward and its item UID/payment timestamp
survive another restart without duplication. The twenty-match branch shares
the corrected writer but is not a separately played process journey here.

## Earned Honor purchase after recovery (2026-09-27)

The extended fixture continues the same winner and opponent through eleven more
registered Solo matches after the recovery checks, reaching twenty completions.
Its eligible starting profiles include 100 Vitality; after loading, the normal
stat/HP recalculation supplies positive maximum HP. This is a qualified profile,
not an earned level-one career. No Honor, match counts or reward items are seeded.

The unchanged Honor Shop catalog prices Fancy Hossu Hair Pack (3006319) at 800
Honor. A purchase request after the ninth-win reward (500 Honor) must fail
without changing balance, inventory or receipts. The twentieth match earns the
second 500-Honor entitlement, EXP Ball and bound rune. A normal shop-handler
request then buys one pack, leaving 200 current and 1,000 historical Honor.
The losing participant also receives the participation milestone's 500 Honor,
EXP Ball and bound rune, despite having no wins.

A further actual game restart reloads the same five winner items and 200-Honor
balance in both Arena mirrors. Repeated reward delivery must not refund the
spent Honor or duplicate either daily reward. The purchase receipt must still
join the owned pack and retain its 1,000-to-200 debit. The repeated buy check
here proves insufficient-funds rejection, not deduplication with a second
purchase's funds available. Pack opening, native shop UI, natural matchmaking,
and an interrupted twentieth-match result remain separate gates.

### VIP preimage defect

The earned continuation exposed a production mismatch: LoadEntityState applies
an effective VIP floor of 4 while VipState retains the originally loaded durable
VIP level (0 for these profiles). ArenaResultIntents compared effective VIP to
stored VIP directly and fenced both participants instead of committing the next
result. It now validates the stored level against VipState's loaded preimage;
the outcome snapshot still freezes the effective VIP for the existing prize
rules. A changed stored VIP remains rejected. The comparison requires the
owner lock and also supports the coordinator's fenced capture path.

The resumed earning/purchase and restart checks pass against private executable
SHA256 `81DD8EECB3A01F6865A0E426729C5546BB8A27CF99EB9A8D354A6303DD4EA322`.
No live server or client was changed. `-ResumeSpendOwnedFixture` resumes only an
exactly marked clone with completed recovery fingerprint and removed triggers;
it avoids replaying the earlier fixed-nine-win checks after additional matches.

Final clean run: capture, two recovery verifications and two earning/purchase
phases pass on the corrected build. The owned clone is removed, original private
configuration restored, and private game/database listeners stopped.

## Team leader and member daily earning (2026-09-27)

The same three Team participants whose first result survived interruption now
continue to nine registered wins for the two-player side. Each new match uses
the production import, outcome coordinator and completion pump; the five-second
return is allowed to elapse normally. All participants must return to town and
release their match/session ownership before the next import. Outcomes and
qualified starting profiles remain controlled inputs; no saved wins, currency,
entitlements or reward items are supplied.

Both the leader and the non-leader earn 500 Honor, one EXP Ball and one bound
Tortoise Breaker rune. The losing participant has nine completed matches but
no wins and receives no daily entitlement yet. Ordinary reward delivery plus
repeated Pay calls retain one paid entitlement per winner, matching owned item
rows and both Honor mirrors. Another actual game process reload repeats these
checks without replaying matches or duplicating prizes.

The complete seven-launch sequence passes: capture, two recovery checks, two
Solo earning/purchase checks and two Team checks, including the database restart
at the original interruption. It reuses private executable
`81DD8EECB3A01F6865A0E426729C5546BB8A27CF99EB9A8D354A6303DD4EA322` without a new
production build. Evidence: `obj/arena-team-earning-run.log`. The owned clone is
removed on success and original private configuration restored. This does not
prove Team twenty-match rewards, actual authenticated combat, native team UX,
or interruption at the ninth Team result. Live deployment remains pending.


## Team ninth-win and twentieth-match interruption (2026-09-30)

Run `tools/Verify-ArenaProcess.ps1 -TeamDailyOnly`. This focused four-process
fixture completes the previously open Team daily-reward counter/recovery path.
It uses three ordinary characters: a winning leader, a winning non-leader and
an opposing leader. Registered match imports and controlled result outcomes are
used; this is not authenticated matchmaking or combat acceptance.

1. Complete eight Team results with normal completion/return ownership. Suppress
   the ninth result row, capture its real durable intent, and verify no ninth
   statistics commit. Normal shutdown refuses to discard the retained group and
   all three sessions. Deliberately exit the process without normal cleanup.
2. Remove only the fixture trigger and restart PostgreSQL/game. Startup recovery
   commits the ninth result and all three statistics. The two winners each receive
   500 Honor, one EXP Ball 723700 and one bound rune 4030101; the loser receives
   no wins9 prize. Continue through nineteen completed matches, then repeat the
   suppressed-write/pending-shutdown/process-exit boundary for match twenty.
3. Recover the twentieth result at startup. Each winner now holds 1,000 current
   and historical Honor, two EXP Balls and two bound runes. The twenty-loss
   opponent receives 500 Honor, one EXP Ball and one bound rune from matches20.
   All three have twenty saved statistics and the correct win/participation
   counters. Both Arena Honor mirrors match their paid prizes.
4. Start the game again and repeat delivery. Saved Solo/Team point and counter
   fields, Honor, item UIDs and payment timestamps match the prior fingerprint.
   Repeated payments produce no additional reward objects or Honor.

All four phases pass on executable SHA-256
`99E6736D25B8DE087E95DD8562CB8D26901ABBD884D872E0D57439C809C948D5`.
Probe compilation treats warnings as errors. No new gameplay build was required;
the existing candidate implements these result/payment paths. The successful
driver removed its marked clone and stopped private PostgreSQL. Logs remain in
ignored `obj/team-daily-process.log` and private `arena-process-team-daily-*.log`.

These are pending-result write interruptions followed by deliberate process
exit, not a lost-COMMIT-reply, power-loss or kernel-crash simulation. Actual
socket matchmaking/combat, native team UI and live deployment remain open.


## Solo twentieth-result interruption (2026-09-30)

`tools/Verify-ArenaProcess.ps1 -SoloTwentyOnly` now covers the previously open
interrupted twentieth result. Two non-GM characters complete nineteen registered
Solo results through the existing completion/return path. The winner has the
already-paid ninth-win prize: 500 Honor, EXP Ball 723700 and bound rune 4030101.
The opponent has no nine-win reward.

A fixture trigger suppresses the twentieth statistics insert. Its real result
intent remains saved, while its commit and matches20 entitlements remain absent.
Normal shutdown reports pending and retains both match owners. The fixture then
exits the actual game process without cleanup, removes only its trigger, and
restarts PostgreSQL and the game. Startup recovery commits exactly one result
with both statistics and makes the participation prizes deliverable.

The winner recovers 1,000 current/historical Honor and four prize objects; the
opponent recovers 500 Honor and two objects. Both have twenty participation
statistics, the proper win counts and matching Honor mirrors. Exact point/counter,
Honor, item UID and paid-timestamp fingerprints survive another game process.
Repeated delivery changes nothing. All three phases pass against executable
SHA-256 `99E6736D25B8DE087E95DD8562CB8D26901ABBD884D872E0D57439C809C948D5`.

This uses controlled outcomes on registered matches, not authenticated socket
matchmaking/combat. It tests a retained write failure plus deliberate process
exit, not a lost-COMMIT-reply or power failure. Native UI, custom prize thresholds
and deployment remain separate gates. No production code/build was changed.
The driver removed its owned clone and stopped private PostgreSQL; logs remain
ignored under `obj/solo-twenty-process.log` and private `arena-process-solo-twenty-*.log`.
