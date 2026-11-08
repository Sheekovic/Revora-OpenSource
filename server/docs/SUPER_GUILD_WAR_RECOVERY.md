# Super Guild War recovery work

## Runtime lifecycle replay correction (2026-09-27)

The Wednesday event on map1234 retained a separate legacy payout loop.
`SuperGuildWar.End` previously paid without checking whether the event was active;
repeated calls could repeat CP, RVP, gold, pack and token rewards. Repeated Start
calls also reset an already active round. This is independent of regular Sunday
Guild War's durable battle/purse ledger.

Start and End now share a nonblocking Interlocked lifecycle guard. Start does
nothing for an active event; End does nothing for an inactive event. The guard
is released in finally and never waits while another lifecycle call is busy.
Existing winner selection, schedule, amounts and item/honor rules are unchanged.

Isolated build passed with existing legacy warnings; **997 guild-season checks
passed**. The new fixture loads map1234's real configured terrain alias and
installed pole/gates. An ordinary non-GM winner captures the pole through the
combat path. Parallel/repeated Start cannot reset that capture; parallel/repeated
End pays exactly one legacy currency/item bundle; a later Start remains possible.
The fixture uses private database rows and simulated transport, not native UI.
Logs: ignored `obj/super-guild-lifecycle-build.log` and
`obj/super-guild-lifecycle-tests.log`. Counts overlap previous guild suites.

## Remaining migration

This is process-local replay protection, not a durable payout transaction.

- [x] Persist the Super Guild War instance, accepted captures and terminal result;
  distinguish it from Sunday seasons and cancel interrupted unfinalized instances
  (component validation below; real Host/native acceptance remains pending).
- [x] Snapshot canonical eligible ordinary members and configured rewards with the
  winning result, using explicit ownership and deterministic lock ordering.
- [x] Replace the legacy multi-write payout with atomic currency/item/honor claims,
  frozen valuable rewards, pending full-bag/offline delivery and recovery receipts.
- [x] Remove swallowed payout failures in favor of safe logging and retained claims.
- [x] Verify component failure/reload, combat-to-claim and automatic delivery.
- [x] Private actual Host/database restart: schema retention and synthetic
  unfinished-battle/pending-purse/paid-receipt recovery.
- [ ] Verify earned authenticated-player crash/restart recovery and native
  presentation before deployment.

The runtime-only correction above originally retained separate payout writes.
The atomic-purse migration below now replaces that loop. No live deployment or restart.

## Durable battle lifecycle (2026-09-27)

`revora_super_guild_battles` identifies each scheduled or GM-started battle by UUID;
a unique partial index permits one active instance. `revora_super_guild_captures`
records monotonically numbered captures, canonical guild IDs, accepted damage and
UTC timestamps. Start persists before activation. A capture atomically changes
its battle keeper/counter, receipt, guild win/loss statistics and saved pole.
End freezes the terminal result before invoking the still-legacy payout loop.
Startup cancels interrupted active instances and restores the previous completed
winner by guild ID. It does not infer a historical winner from a display name.
An existing defender may retain victory without a new capture, as in the legacy
runtime rules. No new schedule or participation requirement was introduced.

Pole combat now shares the nonblocking lifecycle gate, validates the ordinary
session and canonical guild membership, and caps credited damage at remaining HP.
Scores are event-owned `ulong` values; they no longer reset another event's shared
`Guild.WarScore`. Equal scores resolve by ascending guild ID (documented Revora
determinism). A lethal hit cannot fall through into damage against the reset pole.
A failed precommit capture restores HP/score so it can be retried. Initially an
ambiguous commit only failed closed through its durable counter; the reconciliation
implementation below now restores a matching committed capture. Battle rows lock before ordered guild
rows and the pole row. Notifications follow committed in-memory capture state.

Isolated build and **1,006 guild-season checks passed**, including ordinary combat,
rejected stale membership, suppressed capture/result writes, restored lethal-hit
retry, exact capture/statistics persistence, defender/repeated-end replay rejection,
and component startup recovery retaining the completed winner while cancelling an
interrupted instance. Logs: `obj/super-guild-ledger-build.log` and
`obj/super-guild-ledger-tests.log`. This is the existing overlay build that excludes
unrelated owner work; it is not clean-release or authenticated/native acceptance.

At this intermediate checkpoint, the result row was not a reward receipt and
legacy payouts remained incomplete after a crash. The following migration closes
that split-write path. Neither checkpoint deploys or refreshes a private Host.

## Atomic winning purses (2026-09-27)

`SuperGuildWarPurses` freezes eligible recipients, rank, configured CP/RVP/gold,
loose tokens, honor and a selected **bound permanent P7 soul** with the terminal
battle result. The UUID/character primary key and sealed result prevent repeat
finalization from creating additional purses. A finalization retry after an
ambiguous commit recognizes the already-sealed result. Historical closed results
are sealed without invented recipients or retroactive P7 grants.

Eligibility remains presence on map1234 in the winning guild at finalization,
including fallen members. The snapshot requires canonical authenticated character,
guild and member identities, matching rank, and a recognized legacy reward rank;
leader identity is verified. Absent, fake, unknown-rank and losing-guild characters
are excluded. Going offline **after earning** does not remove the entitlement.
Leadership is snapshotted under its guild gate; ordered character database locks
precede the battle result update. No inventory lock is taken under the war gate.

| Frozen rank | Currency terms | One-day bound items | Six-day honor |
| --- | --- | --- | --- |
| Leader | SuperGuildWarCp/Gold, GuildWarLeaderRevoraPoints | Pack3302411 + four tokens723467 | Yellow/status32 |
| Leader spouse | SuperGuildWarCp/Gold, GuildWarOfficerRevoraPoints | Pack + two tokens | Yellow/status32 |
| Deputy / deputy spouse | SuperGuildWarCp/Gold, GuildWarOfficerRevoraPoints | Pack + two tokens | Blue/status31 |
| Recognized member ranks | GuildWarMemberCp/Gold/RevoraPoints | Pack | Under-blue/status33 |

Every new eligible purse also contains its frozen bound permanent P7. The old
deputy branch redundantly issued two extra unbound tokens before its two bound
tokens; the canonical officer bundle is two bound tokens, matching the regular
war purse. This is a documented Revora correction. No daily quest gains P7.

The existing production reward tick polls up to32 online pending owners every10
seconds, independent of whether the battle is still active. Delivery uses the
session-install guard, inventory lock and character row lock. Wallet preimages,
all item inserts, honor and the paid receipt commit together. Full bags, overflow,
trade/booth use and offline sessions leave the complete entitlement pending;
stale/failing persistence fences the session with redacted failure logging.
Currencies, inventory and honor project after commit. Claims after promotion or
configuration changes retain their frozen terms. Honor expires six days from the
result; delayed collection cannot renew it. Temporary packs/tokens receive one day
from collection; the P7 remains permanent. No unverified achievement is added.

Isolated build passed with existing legacy warnings; **1,031 guild-season checks
passed**. Ordinary combat-to-result tests cover all six eligible rank/liveness
cases and ineligible identities, failed result/recipient/seal writes, full bags,
wallet/item/status/receipt rollback, late P7-insert failure, stale RVP, overflow,
concurrent collection, promotion/configuration changes, exact item/character
reload, preserved offline claims after service reinitialization, production-tick
reconnect delivery and expired-honor handling. Existing regular-war checks remain
in this overlapping suite. Logs: `obj/super-guild-purse-build.log` and
`obj/super-guild-purse-tests.log`. The overlay excludes unrelated owner work.

Earned authenticated-player Host crash/restart acceptance, transport-failure-after-commit
exercise, genuine-client presentation and deployment remain open. The capture reconciliation added below is separate from recovery of finalized
purses and has its own component evidence. No live schema mutation, deployment or server restart.

### Private Host restart evidence

Build863aac10 passes two real isolated Host/database cycles with25 runtime-owned
tables retained and27 guild column expectations verified. The second Host
cancels a synthetic unfinished battle, retains its capture and preserves exact
pending/paid purse fingerprints; an old empty result is sealed without rewards.
All UUID-scoped storage fixtures were removed after verification. Both games
exited zero without forced termination; the candidate is stopped. This is storage
recovery evidence, not an earned-player or native acceptance claim. See
[Host record](ROADMAP_STARTUP_ACCEPTANCE.md#guild-purse-migration-and-super-guild-recovery-2026-09-27).

## Lost-reply lifecycle reconciliation (2026-09-27)

A start attempt retains its UUID until the saved start is verified. Retrying an
ambiguous insert uses that same UUID and accepts only its active, unfinalized,
zero-capture record with the expected defender. The original saved start time is
preserved. The active-instance index still prevents a second battle. This adds no
new scheduled-start window; it makes existing start retries idempotent.

A lethal capture records its pending sequence before persistence. If the write
fails, recovery locks and reads the battle after the original transaction has
finished. An unchanged counter confirms no capture committed. An advanced counter
is accepted only when its capture receipt agrees on battle, sequence and keeper.
Recovery reloads authoritative guild statistics and restores the keeper, pole and
round state without incrementing wins/losses again. Missing/mismatched evidence
cannot finalize rewards or accept further pole damage. Failed recovery attempts
are rate-limited to one per second; ordinary healthy hits perform no new recovery
query. The score tick, the next eligible hit and End can retry recovery; End
recovers the keeper before snapshotting recipients. Failures log exception types.

Isolated build and **1,034 guild-season checks passed**. Fixtures model a committed
start/capture whose in-memory installation was lost, verify exact UUID/start-time
reuse, reject a mismatched keeper receipt, restore statistics once through the
score tick, and recover the winning keeper before finalization. Existing rollback,
ordinary earning, atomic purse and reload tests remain in the overlapping suite.
Logs: `obj/super-guild-capture-recovery-build.log` and
`obj/super-guild-capture-recovery-tests.log`.

This is deterministic component fault modeling, not an actual dropped PostgreSQL
acknowledgment or native-client crash test. The prior real Host storage evidence
remains tied to build863aac10; no schema changed and that private candidate was
not refreshed/restarted for this code-only batch. Authenticated-player crash,
transport fault injection, native presentation and deployment gates remain open.

## Active scheduled lifecycle (2026-09-27)

The restored purse poll was active, but the battle's Wednesday start/end calls
were below the world timer's unconditional return. `SuperGuildWar.Tick` now runs
in the active restored-event pulse; the unreachable duplicate block is removed.

- The existing server-local opening window remains Wednesday 20:00:01?20:00:59.
- A SHA-256-derived UUID identifies each local Wednesday date. Restart cancellation
  and completed rows retain that identity; neither can reopen in the same slot.
- Opening saves a UTC `closes_at` deadline for 21:00 server-local time. Three-second
  polling closes overdue battles even after a delayed callback. Failed settlement
  retains the active battle for retry through the existing atomic purse path.
- Pending start retries reconcile the saved UUID and timestamp. If the reply was
  lost, a late retry installs and closes the already-saved battle. A start that
  never committed cannot create a new battle after its deadline.
- Scheduled battles reset the defending winner, so an empty new battle cannot
  award the previous winner. Deadline checks refuse late pole damage. Explicit
  administrative starts retain their separate unscheduled lifecycle.
- Active score publication uses the same serialized tick. Existing start messages
  and ordinary NPC entry remain; the old unreachable invitation block is removed,
  not claimed as restored native invitation acceptance.

Validation: isolated build and **22 focused database/calendar checks** pass in
`tests/WarCalendar/SuperGuildScheduleTests.cs`. Coverage includes twelve concurrent
opening calls, early/late boundaries, next-week identity, lost start installation,
restart refusal, injected start/result failures, late settlement, empty-prize
refusal and manual lifecycle compatibility. The fixture creates and drops only
its own `super_guild_schedule_fixture` schema on the fixed private database;
production tables and earned-character history are untouched.

Two actual private game/database startups execute the event pulse, verify the
new nullable UTC deadline column and retain all 31 checked roadmap tables. Both
processes stop normally. Logs: `obj/super-guild-schedule-build.log`,
`obj/super-guild-schedule-tests.log`, `obj/super-guild-schedule-startup.log`.
Candidate executable SHA-256:
`34C28568FD0A89A9E2B9E566A75DB16B57F2695D5689B5229A970AED35437958`.

These tests use controlled clocks and empty scheduled battles. They do not prove
a naturally due Wednesday match, scheduled ordinary-player admission/combat/prize
collection, in-flight process interruption, native UI or live deployment. Prior
purse tests remain evidence for their original scope, not a new end-to-end claim.
Elite Guild War's separate calendar still requires this lifecycle work.

## Scheduled ordinary-player journey (2026-09-27)

`tests/WarCalendar/SuperGuildJourneyTests.cs` passes **20 focused checks** using
ordinary, non-GM, non-fake characters and the installed NPC/arena catalog:

- NPC380 is an open Twin City service. It refuses closed entry, then admits the
  leader, a guild member and a rival leader after the Wednesday calendar opens.
- Controlled damage through the normal handler captures the installed pole and
  writes its receipt. A delayed deadline tick freezes the winning purses.
- Only the present winning leader/member receive entitlements. A rival in the
  arena and an absent winning-guild member receive nothing.
- The ordinary member receives the configured currency and two-item purse through
  the automatic poll. A full bag defers all of the leader's currency and items.
- Once space is available, subsequent bounded polls deliver the leader's exact
  configured CP/silver/RVP amounts, four war tokens, the existing timed item and
  one permanent bound P7. The saved paid receipt includes that soul's UID.
- Repeated polling/claiming cannot pay again. Production entity/item reload
  preserves the leader's guild role, exact currencies and six reward items;
  another claim leaves them unchanged.

The fixture uses the same reviewed ordinary-player/map/reload helper binary and
private-only connection safeguards documented in the Elite Guild journey. It
creates and drops only `super_guild_journey_fixture`, reads installed catalogs,
and leaves public characters and earned history untouched. As in the existing
helper, the entities table is a column-only fixture: this is not a production
registration/schema-constraint test. It does not invoke the helper's `Main` or
configuration reader.

Evidence: `obj/super-guild-journey-tests.log`; no poll failure/exception marker;
private database stopped normally. No gameplay binary change, rebuild or live
restart was necessary. Both scheduled guild wars now have in-process ordinary
server earning coverage. Clocks, travel to the pole and damage amounts remain
controlled; Account/Game TCP combat, natural calendar time, actual in-flight
process interruption, native presentation and deployment remain open.

## Scheduled earning across actual processes (2026-09-27)

The full-schema disposable game now verifies a scheduled battle and two pending
purses across three actual game processes, with a PostgreSQL restart between
the first and second. `tools/Verify-ArenaProcess.ps1 -SuperGuildOnly` runs this
sequence with the existing owned-fixture and loopback safeguards.

- The first process creates two ordinary non-GM profiles and supplies their
  saved guild membership/leadership. A controlled Wednesday20:00:01 local clock
  opens the event through its calendar tick. Both players enter through NPC380.
  Controlled damage through the normal attack handler captures the installed
  pole. A delayed21:02 tick settles the event and freezes two unpaid purses.
- The process exits immediately with both inventories empty and no CP payout;
  the harness stops and restarts PostgreSQL before launching the next game.
- The second process loads the ordinary characters through production entity,
  guild and item loading, then invokes the ordinary purse claim service twice.
  Each receives exactly its saved CP/RVP/silver entitlement. The leader receives
  four war tokens, the existing timed item and one permanent bound P7; the member
  receives the timed item and one permanent bound P7. Each soul UID appears in
  its paid receipt. This phase tests the claim service directly; the automatic
  poll remains covered by the preceding in-process journey.
- The third process reloads and claims again. Saved item UID/type/binding/stack
  fingerprints, currencies and payment timestamps remain identical. Guild roles
  survive, and one won battle with keeper50000 retains its frozen-prize flag.

All three phases pass in `obj/super-guild-process.log` (ignored), using game SHA256
`9B72D1C220424D314A9C2E9879B98EED6C063C294C202F642CA2E1AAB1800B33`.
The first harness run reached the successful capture marker but rejected it
because the new phase marker was absent from the runner. After correcting that
runner check, the complete clean-fixture sequence above passed. No game binary
change or production restart was needed.

This extends the earlier column-only component fixture with actual production
schema constraints and restart recovery of earned pending prizes. Guild creation,
combat damage, travel to the pole and event time remain controlled. It does not
establish authenticated Account/Game combat, naturally due calendar execution,
interruption during an active battle, native presentation or live deployment.


## Active capture through abrupt process interruption (2026-10-03)

Three private phases now pass: `career-guild-interrupt-capture`,
`career-guild-interrupt-recover` and `career-guild-interrupt-reload`.
`tools/CheckCandidateGuildInterruption.cs` uses ordinary non-GM profile4294918701
and supplied guild51101 affiliation. It controls separate eligible event clocks,
uses the installed NPC admission for each war, and sends a controlled lethal
hit through the normal pole-damage handler. Each battle saves one capture for
the guild while still active. The process then exits abruptly, without End or
a winner/prize fixture. These two independently controlled clocks deliberately
exercise both recovery paths in one batch; they are not simultaneous natural
calendar slots or authenticated TCP combat.

Production initialization in the next game process cancels both unfinished
battles, preserves their captured guild/number/history, restores each previous
committed winner, and restores both poles to full health. Repeated opening ticks
for the same cancelled schedule do not create a replacement battle. Both NPCs
refuse new admission. Repeated ordinary Elite clerk claims and direct Super
purse-service claims give nothing: no Super purse row exists for the cancelled
battle, and Elite retains zero soul type and no claim timestamp.

Exact player item/currency state, guild state and capture records remain equal
to the pre-exit snapshot. Another database/game restart preserves the exact
cancelled-result rows, including their original cancellation timestamps, and
repeats the same no-entry/no-prize checks. Probe: `revora_guild_interrupt_probe`.
Recovery means cancellation, not resumption of combat. NPC approach/return to
Twin City is fixture-controlled; automatic evacuation or native travel is not
claimed. Existing winner/payout restart evidence remains separate.

The first recovery assertion incorrectly expected Super `purses_frozen=false`.
`SuperGuildWarPurses.Initialize` deliberately seals every terminal battle,
including cancellations, without generating payouts. Read-back confirmed both
battles were already cancelled and no reward was created. The corrected fixture
checks the documented sealed flag plus zero purse rows; it resumed those same
saved battles, without deleting or recreating their evidence. Both corrected
recovery phases pass. No gameplay defect or production source change resulted.

Private game SHA256 remains
`943AA1FBE2BF19433045F4CAF5CDA0FE4BCBEF2EC1EFAE9CD703995346FC6435`.
Logs: `C:/Revora/DisposablePostgres/moon-box-candidate/arena-process-career-guild-interrupt-{capture,recover,reload}.log`.
Natural scheduling, authenticated combat, native presentation and live deployment
remain open. This closes the active-battle process-interruption scope only.


## Post-load login evacuation (2026-10-03)

The recovery/reload fixture now invokes production `GeneralData.LoginMessages`
from each controlled war-map position (1234 and 2071), rather than manually
teleporting directly to town before the admission checks. Both calls return the
ordinary player to Twin City 1002 at 408,330 without a persistence fence. Existing
exact inventory/currency/guild/capture snapshots and no-prize assertions still
pass, including a second game/database restart. No production change was needed.

Both `career-guild-interrupt-recover` and `career-guild-interrupt-reload` passed
on executable `F1FF87120645900CB1F7B288D1B9EB55519A3C124C037D7B9F383A9AAF616B9F`.
This tests the post-load handler with controlled map placement. It does not
replay authentication, prove native travel, or establish automatic evacuation of
online players when a war ends. The earlier abrupt-interruption evidence is
preserved; this extension does not create another battle or award.
