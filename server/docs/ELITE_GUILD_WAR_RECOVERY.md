# Elite Guild War earning and recovery

## Verified legacy path

Elite Guild War uses map2071, installed pole88816 and NPC handler115522. The
scheduler currently lists Monday, Tuesday, Thursday, Friday and Sunday, starting
at21:01:05 and ending after22:00. Its dialogue advertises Saturday and a five-minute
claim period, but those promises do not match the shown scheduler/claim conditions.
GM start/end paths also exist. These are legacy Revora rules, not verified original
Conquer rules.

The audited legacy leader claim branch cleared global claim/keeper flags before separate writes
for its effect,500,000,000,000 silver and200,000 RVPs. It had no battle/result or
atomic reward receipt. The effect's native identity, persistence and expiry also
need verification. The migration below replaces those writes; native presentation remains unverified.

## Owned pole combat (2026-09-27)

- [x] Put start/end/score/capture operations behind one nonblocking runtime gate.
  An active start cannot reset the round; an inactive end cannot reopen its flag.
- [x] Validate the canonical pole, ordinary session, guild/member ownership,
  distance and active event at the damage boundary. Reject fake/stale/remote
  attackers, replacement pole objects and defender damage.
- [x] Cap accepted damage at remaining HP, use event-owned64-bit scores, and stop
  the lethal hit from falling through into damage against the newly reset pole.
  Equal scores resolve by ascending guild ID as documented Revora determinism.
- [x] Isolated build and1,040 overlapping guild-season checks. The fixture loads
  the installed pole and real configured terrain alias, attacks through the normal
  damage handler, and covers inactive/invalid attackers, partial damage, concurrent
  lethal hits, repeated starts and endings. No native UI is simulated.
- [x] Persist battle instances, captures and terminal results, then freeze and
  atomically deliver the eligible winning claim with a valuable reward (below).
- [x] Reconcile dialogue with the existing schedule, replace old flags as claim
  authority, and verify ordinary NPC access, transaction failure and component reload.
- [x] Verify private Host startup/schema and synthetic result retention (below).
- [ ] Verify authenticated-player crash recovery and native effect identity/presentation.

Logs: ignored `obj/elite-guild-combat-build.log` and
`obj/elite-guild-combat-tests.log`. The existing overlay excludes unrelated owner
work. This runtime prerequisite does not certify the legacy guild-statistics or
prize writes, add a completed reward family, or deploy/restart anything.

## Durable winning prize and public envoy (2026-09-27)

- [x] New UUID battle/capture ledgers own start, capture and terminal settlement.
  Captures atomically save the result counter, guild statistics and pole. A retried
  start reuses its UUID; uncertain captures reconcile saved receipts before more
  damage/settlement; repeated completed settlement preserves the frozen prize.
  Startup cancels interrupted battles without fabricating a win or a prize.
- [x] A winning result freezes **500,000,000,000 silver, 200,000 RVPs and one random
  bound permanent P7 soul**. Currencies retain legacy amounts; the soul follows
  the owner's valuable-event-reward policy. The prize belongs to the guild and
  its current canonical leader may collect once. A later leadership transfer
  cannot let the previous leader claim. No prize is backfilled from old flags.
- [x] Claims lock the session, inventory, guild leadership and saved owner/guild
  rows. Saved balances, bag capacity, item ownership count and current leadership
  must agree. Currency, soul, existing indexed effect and claim receipt commit
  together; failed or uncertain writes fence the session for reload. Full bags
  and overflowing balances leave the entire prize pending.
- [x] Pending prizes survive later events and startup. The old advertised
  five-minute deadline had no enforcing claim condition; the Revora edition
  explicitly keeps earned prizes available. Claims remain closed during a war.
- [x] Restore retired NPC115522 from jail to **Twin City (282,161)**, the existing
  invitation destination. Preserve its installed appearance, reject duplicate
  identities or an occupied destination, update both NPC registries, and exempt
  it from the plaza's parking/closed-desk lists. This is startup runtime placement;
  it does not rewrite the installed catalog. Ordinary nearby dialogue admits
  players to2071(47,131) during the war and lets winning leaders claim afterward.
- [x] Dialogue now states Mon/Tue/Thu/Fri/Sun,21:01?22:00 server time. The scheduler
  remains unchanged (start at21:01:05). No unsupported Saturday or five-minute
  expiry promise remains.
- [x] Isolated build and **1,064 overlapping guild-season checks** pass. New checks
  cover the installed envoy/terrain, actual NPC handler admission and claiming,
  full bag/RVP overflow, suppressed result/currency/item/status/receipt writes,
  concurrent claims, leadership transfer, cleared legacy flags, fixed item identity
  and bound status after character/item reload, and interrupted-battle cancellation.
  Lost start/capture/result projections are modeled against saved rows; this is
  not transport-fault injection or authenticated Host crash acceptance.

The existing `FlagsUpdate.Flags.JQ_dunpaichuantou` uses **index176** in the active
flag enum. Claiming preserves that effect with one saved day from collection and
an immediate timed server flag; an already longer expiry is preserved. The legacy
alternate mask enum is not used by this route. No new client packet, verified
native visual, or wardrobe Champion11123 earning identity is claimed.

Logs: ignored `obj/elite-guild-settlement-build.log` and
`obj/elite-guild-settlement-tests.log`. The build overlays committed source plus
this NPC change to exclude unrelated owner work. Private Host schema/storage acceptance subsequently passed (below). Live
deployment, authenticated-player crash recovery, genuine-client navigation/rendering
and effect identity remain pending. No server restart or client installation was performed.

## Rolled-back capture retry (2026-09-27)

- [x] Fault injection at the battle counter, capture receipt, guild statistics and
  pole write proves each failed transaction leaves all saved capture state unchanged.
- [x] Fix a demonstrated recovery gap: a lethal hit retained zero HP after rollback,
  but the score pulse only checked uncertain commits and did not retry that capture.
  The existing three-second production score pulse now retries the retained lethal
  damage, then resets the pole only after capture persistence succeeds. No additional
  player hit is required. The battle can subsequently settle once with its prize pending.
- [x] The new recovery assertion fails on `b5e95070` and passes with this correction.
  Isolated build and **1,070 overlapping guild-season checks** pass. Logs are
  `obj/elite-capture-retry-before.log`, `obj/elite-capture-retry-build.log` and
  `obj/elite-capture-retry-after.log` (ignored). This covers SQL rollback and the
  production score method, not real network failure, elapsed scheduler integration,
  authenticated Host crashes or native acceptance. No deployment or restart.

## Private Host acceptance (2026-09-27)

- [x] Build89a9d09e passes two real isolated game/database cycles with normal
  shutdown,28 runtime-owned tables and18 Elite Guild column checks.
- [x] Actual startup retains exact synthetic pending/paid/empty results, cancels
  an interrupted battle without a reward and preserves its capture. UUID-scoped
  fixtures are removed after success; the candidate is stopped.
- [ ] Authenticated earned-player crash and native-client acceptance remain open.
  [Exact evidence and limits](ROADMAP_STARTUP_ACCEPTANCE.md#elite-guild-war-host-schema-and-result-retention-2026-09-27).

## Active scheduled lifecycle (2026-09-27)

The original automatic start/end calls were unreachable below the world timer's
return. `EliteGuildWar.Tick` now runs in the active restored-event pulse and the
unreachable duplicate calendar is removed.

- Event days remain Sunday, Monday, Tuesday, Thursday and Friday, in server-local
  time. Opening is now accepted from **21:01:05 through 21:01:59**, rather than
  requiring the timer to observe exactly second five. No later catch-up opening
  is created without an already-pending start attempt.
- Each local event date has a deterministic UUID. Completed and startup-cancelled
  rows cannot be reopened by calendar polling, even after a restart.
- Opening saves a UTC `closes_at` corresponding to 22:00 server time. The serialized
  three-second pulse sends scores, issues one closing reminder in the last ten
  minutes, and settles overdue battles through the existing atomic result path.
- Failed start/result writes remain retryable. A lost start reply reconciles its
  original row; after the deadline, only an already-committed start can be
  recovered and settled. A never-committed attempt cannot mint a late battle.
- New scheduled battles clear the previous keeper. Empty events cannot inherit
  another event's winner or prizes. Late pole hits are refused. Explicit operator
  starts retain their separate unscheduled lifecycle.
- The existing start announcement and ordinary envoy service remain. The removed
  legacy invitation popup is not presented as restored native acceptance.

Validation: isolated build and **29 focused database/calendar checks** pass in
`tests/WarCalendar/EliteGuildScheduleTests.cs`. Coverage includes all five allowed
days, both excluded days, opening boundaries, concurrent opening, saved deadline,
closing-reminder state, lost replies, restart refusal, injected opening/settlement
failures, late recovery, empty rewards and manual compatibility. The tests use
controlled clocks and empty battles. They create and remove only their own
`elite_guild_schedule_fixture` schema on the fixed private database.

The **22 Super Guild calendar checks** also pass against the updated binary.
Both suites now seed a previous in-memory keeper before opening and verify its
removal from the runtime and saved scheduled battle; their original check counts
are unchanged.

Two actual private game/database startup/shutdown cycles invoke the active event
pulse, retain 31 checked roadmap tables and verify both wars' nullable UTC deadline
columns. Neither cycle needs an emergency kill. Candidate SHA-256:
`FD7B6816A0B1B99EBA4611CE137825A46AB12954772E9A041ADBD3B67B6B440C`.
Logs: `obj/elite-guild-schedule-build.log`, `obj/elite-guild-schedule-tests.log`,
`obj/super-guild-schedule-tests.log`, `obj/elite-guild-schedule-startup.log`.

Scheduled ordinary-player admission, combat and prize collection, natural elapsed
calendar time, actual in-flight process interruption, native UI and live deployment
remain open. This batch proves scheduler/recovery integration, not an end-to-end
player victory. No reward-family or wardrobe completion count changes.

## Scheduled ordinary-player journey (2026-09-27)

`tests/WarCalendar/EliteGuildJourneyTests.cs` now connects the scheduled lifecycle
to the existing player services. **15 checks pass** against the current private
candidate assembly:

- A non-GM, non-fake guild leader is refused entry while closed, then enters via
  envoy115522 after the calendar tick opens the saved battle.
- Real installed arena terrain and the real pole are loaded. A controlled lethal
  hit through the ordinary damage handler creates one durable guild capture.
- A delayed deadline tick, rather than a manual `End`, freezes the winning result.
- Another guild receives nothing; a full bag defers the whole winner's prize.
- The leader claims exactly500,000,000,000 silver,200,000 RVPs and one permanent
  bound catalog-P7 soul through the NPC. Its receipt records the real item UID.
- The eligible leader cannot claim twice. Production entity/item reload retains
  the exact currencies, soul and guild leadership; another NPC claim adds nothing.

The fixture reuses the existing `PotencyHunt.Tests.exe` map/player/reload helpers
without invoking its `Main` or configuration reader. Reviewed helper binary
SHA-256: `A1F2B3918B72BF5914FD123530965415E23CA5D5E833AB424CFF9BE48739A1FB`.
The source requires that helper beside the validation `Revora.exe`/Npgsql assembly.
It reads a transient `REVORA_QUARANTINE_TEST_CONNECTION` environment variable,
requires127.0.0.1:25439/revora_candidate_game, clears the variable, refuses an
existing fixture schema, and drops only its own schema afterward. Catalog reads
are read-only; no public player rows are copied or changed. Player construction
uses the existing helper's column-only entities fixture; this is not a production
registration/schema-constraint test.

Evidence: `obj/elite-guild-journey-tests.log`; private database stopped normally.
No gameplay binary changed, so no additional deployment or server build was
needed. Candidate gameplay remains the previously recorded hash.

Scope remains in-process: clocks, travel to the pole and damage amount are
controlled; these tests do not send Account/Game TCP attack packets or demonstrate
natural elapsed time, actual in-flight process crashes, native UI or deployment.
The ordinary scheduled server earning path is covered; those broader gates are
still unfinished. Reward-family and wardrobe counts are unchanged.

## Earned pending prize across actual processes (2026-09-27)

`tools/Verify-ArenaProcess.ps1 -EliteGuildOnly` now passes three actual game
processes against the full-schema disposable database. PostgreSQL is restarted
between the first and second processes.

The first process supplies an ordinary non-GM leader's saved guild affiliation,
opens a controlled Monday21:01:05 calendar slot and enters through envoy115522.
A controlled hit through the normal damage handler captures the installed pole;
a delayed22:02 tick settles the battle. Forty supplied bag-padding items cause
the ordinary NPC prize claim to defer the entire reward. The process exits
immediately with the battle won and its prize still unclaimed.

The second process loads the character, guild and forty items through production
loaders. The same NPC refuses the full-bag claim without changing silver/RVPs or
the paid flag. Removing one padding item creates one slot. The NPC then grants
exactly500,000,000,000 silver,200,000 RVPs and one permanent bound catalog-P7 soul;
the bag returns to forty items. CPs remain zero. Another claim grants nothing,
and the saved result records the actual leader, soul UID and claim timestamp.

The third process reloads and claims again. Exact item UID/type/binding/stack
fingerprints, silver, RVPs, CPs, soul UID, claim timestamp and guild leadership
are unchanged. One won battle retains the expected keeper. Thus the full bag
does not lose an earned pending prize or permit a partial payment across restart.

Evidence: `obj/elite-guild-process.log` (ignored), all three phases passed.
Game SHA256 remains
`9B72D1C220424D314A9C2E9879B98EED6C063C294C202F642CA2E1AAB1800B33`;
only the private fixture/runner changed. These phases use production schema
constraints rather than the earlier column-only component fixture. Affiliation,
bag padding, event clock, travel to the pole and damage remain controlled.
Authenticated combat, natural calendar time, interruption during an active
battle, native UI and live deployment remain pending.


## Active capture through abrupt process interruption (2026-10-03)

The combined Super/Elite fixture now passes ordinary NPC admission, controlled
pole capture, abrupt active-battle exit and two cold recovery phases. Elite's
unfinished battle is cancelled without a prize; capture history and player/guild
assets survive. Its pole restores full health and the prior committed winner.
The cancelled schedule cannot reopen, its clerk refuses admission and repeated
claims add nothing. Exact result timestamps survive the second restart.

[Shared evidence, fixture correction and limitations](SUPER_GUILD_WAR_RECOVERY.md#active-capture-through-abrupt-process-interruption-2026-10-03).
This closes active-battle process interruption only. Affiliation, event clocks,
lethal damage and NPC approach are controlled; automatic evacuation, authenticated
combat, native presentation and deployment remain outside this check.


The later [post-load login evacuation check](SUPER_GUILD_WAR_RECOVERY.md#post-load-login-evacuation-2026-10-03)
also passes for Elite map2071 through the production login-message handler,
with exact prior assets and cancelled results preserved across another restart.
Map placement is supplied; authenticated/native login remains unverified.
