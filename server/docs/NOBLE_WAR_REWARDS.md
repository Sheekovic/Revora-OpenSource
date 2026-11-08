# NobleWar valuable reward contract

## Implemented 2026-09-26

A bracket's final survivor earns its existing configured CP, silver and RVP purse
plus one bound random catalog P7 soul. The cycle, bracket, winner UID, all three
currency amounts and chosen soul type are frozen when finishing begins. The
`revora_noble_prizes` table stores that contract before teleport or payment.
Repeated publication must match the same contract. A publication failure keeps
the selected winner for retry even if that character disconnects afterward.

Connected healthy winners are paid automatically when possible. The NobleWar
clerk offers **Claim my prize** for unpaid results. Full inventory, purse capacity
or a failed transaction leaves the result claimable later. Multiple results are
claimed oldest first. No historical paid victories are recreated.

One session-owned transaction checks saved purse preimages, saved bag capacity,
the unpaid receipt and P7 catalog identity; inserts the bound soul; updates CP,
silver and RVP; and marks the receipt paid with its exact soul UID. Committed
balances/items are then installed and sent to the player. Failures fence the
session for reload without consuming the result or making a partial payout.
Normal registration now requires a canonical, loaded ordinary player session.

## Validation

The staged source build and focused disposable-database suite passed **440
checks**: 406 fixture prerequisites and 34 NobleWar assertions. All four bracket
contracts cover configured amounts, catalog P7 binding, receipt identity,
autosave, full reload, concurrent claim replay and duplicate finish. Failure
cases cover publication rollback, offline retry with the frozen soul, item
insertion failure, saved-only full inventory and externally changed RVP; each
retains a claim that succeeds after recovery.

A two-player Duke route uses production registration, StartBracket, a controlled
death callback, last-survivor detection, automatic reward and full reload. The
fixture supplies real map-700 terrain and HP; it does not validate native damage
formulas, NPC packet presentation or production dynamic-map creation. The Earl
payout path is checked as an existing bracket contract; the owner's five-King,
fifty-Prince, remaining-Duke policy makes Earl ordinarily unreachable.

Tracked checks: `VerifyNoblePrizes` in
`tests/PotencyHunt/ConquerPointBagTests.cs`, also registered in the purse suite.
Ignored evidence: `obj/noble-prizes-build.log`, `obj/noble-prizes-tests.log`.
Only the focused suite was run for this isolated event reward batch.

## Admission and return recovery

Each bracket saves its complete admission roster, arena and original PK modes
before entry, under UID-ordered session leases and inventory locks. The durable
cycle key includes the bracket rank, keeping simultaneous brackets separate.
An unreturned older admission prevents another entry; failed writes roll back
the entire roster.

Death, disconnect, missing/departed fighters and bracket closure queue returns.
Startup cancels unfinished admissions without inventing results. Login and event
ticks retry returns under the current session lease. The original PK mode is
restored and sent; exact Twin City position is saved before return acknowledgement.
An acknowledgement failure retains recovery and fences the character for reload.
Committed victory contracts remain claimable independently of these returns.

Final staged build and focused suite: **449 checks** (406 prerequisites + 43
NobleWar assertions). The ordinary Duke route now checks winner/loser mode
restoration and durable return acknowledgement. Additional checks cover partial
admission rollback, blocking a second entry while return is pending, interrupted
admissions on replacement sessions, no fabricated winner, failed acknowledgement
and reconnect recovery, and preservation of completed prizes.

Evidence: `obj/noble-recovery-build.log`, `obj/noble-recovery-tests.log` (ignored).
The production login hook invokes NobleWar recovery before later event services.
No live restart, client patch or live schema update was performed.

## Remaining event gates

The later private process check below closes unfinished Duke admission/return
restart and installed dynamic-map creation. Saved-result/deferred-payout recovery
also passes in the later winner check. Simultaneous bracket/network lifecycle
stress and native-client presentation
remain pending, followed by
the separate deployment gate. Recovery is cancellation and safe return, not
resumption of an unfinished fight. Frozen results not yet committed remain
process-local. Service/reload tests do not prove actual Host restart or native
combat behavior.

## Admitted Duke fight through full process interruption (2026-10-01)

Three private phases, `career-noble-enter`, `career-noble-return` and
`career-noble-reload`, exercise two retained ordinary non-GM characters
4294908401/4294908403 with their loaded Duke ranks. Production Create builds
the arena from the installed map700 floor; no substitute arena or rank is supplied.
The fixture controls CycleStart to open registration and begin the fight, and
uses the normal NPC dispatcher for both registrations. Both players reach the
same production dynamic map alive. Their admission rows and character positions
are saved, then the process exits abruptly before a winner is selected.

Database/game restart ends both unfinished admissions. Production OnLogin
returns both to Twin City1002(408,330), restores each saved PK mode and records
return acknowledgements. Clerk claims grant nothing: no NobleWar prize row is
manufactured. Exact previous purses, item UIDs and nested quest/event receipts
remain unchanged. Another database/game restart preserves those snapshots and
repeat return/claim attempts remain harmless.

This extends the earlier service tests to real private process startup, dynamic
map admission and unfinished-match recovery. It does not verify a completed
fight's reward through process interruption, simultaneous brackets, natural
calendar timing, authenticated sockets, native combat/UI or host power loss.
Those gates remain open; no live deployment or production database change.
The fixture calls NPC/login handlers directly and supplies NPC approach positions.
Candidate SHA256: CE1B425A68EB9082F15EF2C39FD2F0B0EC6D21980360CB1B5EA3FFB708522F10.

## Saved Duke victory and deferred payout across restart (2026-10-01)

The same ordinary Duke pair next registers through the installed clerk and enters
the real arena. The fixture invokes the production loser-disconnect callback;
survivor selection and publication run normally. A second fixture thread holds
the winner's session lease, so automatic delivery defers. The saved result must
contain the exact configured Duke purse and a frozen P7 type, with paid=false
and soul_uid=0. Prior purses/items remain exact before abrupt process exit.
No prize row, winner flag, damage score or reward item is supplied by the fixture.

After database/game restart, both admissions finish return recovery with original
PK modes. The winner uses the ordinary clerk to receive exactly the configured
CP/RVP/gold increments and one bound P7 of the saved type. Its actual item UID
matches the paid receipt. Both players then attempt two further claims without
changing any snapshot. A second database/game restart preserves the exact nested
reward/item/purse/admission/result state and rejects those repeat claims again.

All three phases pass: career-noble-prize-select, career-noble-prize-claim and
career-noble-prize-reload (NoblePrizeProcess). Candidate SHA256 remains
CE1B425A68EB9082F15EF2C39FD2F0B0EC6D21980360CB1B5EA3FFB708522F10.
This closes the committed-result/deferred-payout process scope left open above.
Cycle timing, NPC approaches, disconnect and delivery contention are controlled;
these are direct handler tests, not authenticated fighting or native-client
acceptance. They do not cover interruption before result publication, lost COMMIT
replies, host power loss or simultaneous brackets. No gameplay source change,
new build or live deployment was required for this validation batch.
