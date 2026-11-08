# Revora Planet weekend expedition

Owner-approved custom rules, 2026-09-22. These are Revora earning rules,
not a reconstruction of the original Euro 2016 campaign.

## Contract and current implementation

One saved completion per character per Pacific weekend; four distinct completed
weekends unlock Wings of Planet (`60096009`, catalog wing 6009/6009).
The wing expires 30 days after the fourth completion; a delayed claim must not
extend it. Hunt, guarded encounter and delivery must all be completed.

The initial batch implemented the calendar; saved hunt progression is described below. `PlanetWeekend.Current` accepts
UTC and returns Saturday midnight through Monday midnight in Pacific time,
with an exclusive closing boundary and a Saturday-date identity. Both repeated
autumn hours share that identity. Spring and autumn weekends correctly last
47 and 49 hours. The full Rowan route and fixed-term wing grant are now implemented; current evidence and remaining gates are recorded below.

## Reviewed route for implementation

1. Rowan, the existing Path Hunt NPC `900000016` at Twin City 1002 (319,299),
   introduces the expedition. Keep his existing services available.
2. Hunt 100 Birdmen (monster type 18) on Bird Island, map 1015. Existing Summer
   Hunt code establishes this monster/map pair. Require ordinary authoritative
   kill credit; ambient kills before admission cannot count.
3. Enter an owned private copy of map 700 for a guarded cache encounter.
   Beast Trial already uses this arena source through `Map.CreateDynamic`.
   Candidate positions: entry (48,41), guards (44,48)/(52,48), captain (48,53),
   cache (48,48). Require the encounter's owned enemies, then cache recovery.
4. Deliver the recovered cache to Rowan during the same weekend. Record the
   unique character/weekend completion atomically before acknowledging delivery.

Local catalog evidence: Birdman has 8,952 HP and attack 146-162;
AlienSerpent, candidate encounter base 1413, has 35,050 HP and attack 900-900.
These are catalog stats, not measured player damage or finalized encounter
balance. Use encounter-local copies rather than changing shared monster types.

Local map evidence: `bin/Debug/database/maps/700.map` is 96 by 96 tiles. The
17 by 17 square centered at (48,48) is walkable in the server collision data.
SHA-256: `575ec41dc5ac6523278e029dd11fdb0bce5a121b3a4c04fab2e4b327860d9b31`.
Map 1005 has the same local collision bytes. This validates server geometry,
not native-client rendering or navigation.

## Validation and remaining gates

Build passes; the title suite passes 183 parent checks, including 13 calendar,
catalog and identity checks, plus the existing five fresh-process Fashion
checks. Logs: `obj/chi-refill-validation/planet-calendar-build.log` and
`planet-calendar-titles.log`. No production deployment or runtime refresh.

Remaining: player-facing admission menu, actual Host restart acceptance, delivery
transaction,
four-weekend award and fixed expiry, brief player dialogue, ordinary-player
end-to-end tests, combat tuning and native acceptance. Native acceptance stays
pending by owner choice. Calendar tests do not establish any of these gates.

## Saved admission and hunt (2026-09-22)

`revora_planet_runs` now stores a unique character/weekend contract with a run
UUID, frozen UTC boundaries, acceptance time, capped kill count and stage.
Ordinary admission requires a canonical living player beside Rowan. Concurrent
requests return the same contract; repeated admission never resets progress.
No GM requirement or character allowlist is used. Admission is an internal seam
until the encounter/delivery path is ready; no unfinished quest is offered in
Rowan's menu.

The existing owned-monster death wrapper now credits an admitted nearby killer
for Birdmen on map 1015. It already serializes each actual monster death, so
repeat/concurrent callbacks cannot double count one life. Row locks serialize
distinct deaths; the hundredth kill advances to `encounter` atomically. No
teammate credit, pre-admission credit, future timestamp, wrong target, remote
kill, synthetic player or stale session can advance this hunt.

The cache belongs to each session through weak references. Login reloads durable
progress; old sessions cannot continue after the canonical session is replaced.
No unadmitted hunter incurs a Planet database query. Failed or suppressed writes
fence the session rather than reporting unsaved progress. Encounter completion,
delivery and wing grants are still absent, and hunt receipts cannot substitute
for them.

Build and 27 Planet checks pass, including actual server monster-death calls,
concurrent cap transitions, cache loss, a replacement canonical session,
weekend expiry and injected write failures. Loot/experience are excluded from
this max-level fixture. This is not a socket reconnect, actual Host restart or
native-client test. The shared Demon Exterminators regression also passes;
see `planet-hunt-build.log`, `planet-hunt-tests.log` and
`planet-hunt-demon.log` under `obj/chi-refill-validation`.
No runtime refresh, public client patch or deployment was performed.

## Guarded encounter and cache recovery (2026-09-22)

`PlanetEncounter` now reserves an owned dynamic copy of map 700 after the saved
hundredth Birdman kill. It supports at most 16 simultaneous solo arenas. Each
attempt lasts 15 minutes or until the Pacific weekend closes, whichever comes
first. No entry fee, GM requirement or player allowlist applies. The entry seam
is still internal; Rowan's menu remains unexposed until delivery and awards are
implemented.

Two Astral Sentries have 70,100 HP and attack 1,200; the Astral Captain has
210,300 HP and attack 1,800, using copies of the installed AlienSerpent model
and combat definition. These are server attack stats, not guaranteed damage to
players. Shared AlienSerpent stats remain unchanged. The regular monster AI
supplies movement/attacks; the event's combat gate accepts only the canonical
owner or their owned companion. Unrelated players and forged teleports cannot
enter or affect the encounter. Native combat pacing and HUD appearance remain
unverified.

The actual dynamic-map loader exposed an error in the initial raw-map review:
byte 1 is blocked, not walkable, under this repository's Floor setter. The
initial 48,22 proposal was rejected by the entry guard and corrected to 48,48.
Entry now verifies all five positions against loaded Floor data. The map file
hash is unchanged. Updated route: entry 48,41; sentries 44,48 and 52,48; captain
48,53; cache 48,48. This correction does not claim native rendering acceptance.

A durable attempt records admission before teleport and three distinct defeated
guard bits. Each owned death commits its bit before removing the monster.
Suppressed writes fence the player and leave the guard alive. Defeating all
three unlocks the cache interaction but does not itself advance delivery.
Recovering the cache atomically records victory and changes the matching
weekend run to `delivery`. There is no tradable proof item to duplicate.

Collection returns the player beside Rowan (normally 321,299, with a bounded
walkable fallback), saves the safe position, and acknowledges the return in a
transaction. Failed acknowledgement leaves the earned cache and a pending
return. Reconnect recovery retries it. Empty arenas are disposed only after
players have left them. Death, timeout, leaving or session replacement abandons
an unfinished fight; the 100-kill hunt stays earned and a retry starts with
fresh guards. Startup cancels unfinished attempts, preserves cache victories,
and never infers a completion from position or partial guard kills.

Build and 60 focused Planet checks pass. The fixture earns its hunt through
actual Birdman death callbacks, enters the actual dynamic arena, tests combat
ownership, defeats the owned guards and collects the cache. It covers invalid
entry, duplicate deaths, failed guard/delivery/return writes, timeout, controlled
dead-player state, replacement session and explicit loss of process caches.
These are controlled server fixtures, not a full damage-based native fight,
actual socket reconnect or actual Host restart. The existing Local PK/LMS/Relic/
Wave/Beast regression passes 388 checks after the shared teleport/combat hooks.
Logs: `planet-encounter-build.log`, `planet-encounter-tests.log` and
`planet-encounter-localpk.log` under `obj/chi-refill-validation`.

Still required: Rowan delivery, four distinct completed weekend receipts,
atomic 30-day wing award, brief player entry/progress menu, full-route tests,
actual process/native acceptance and deployment. No runtime refresh or public
release was performed.

## Rowan delivery and four-weekend award (2026-09-22)

The player route is now wired through Rowan's existing native dialogue:
**Planet expedition**, **Take the weekend hunt**, **Enter the guarded passage**,
and **Deliver the cache**. The optional rules page explains Pacific weekends,
four distinct completions and the thirty-day term. Existing Path Hunt options
are preserved. The route uses ordinary authenticated characters without GM,
VIP, name or account allowlists. There is no fee or inventory proof item.

Each delivery requires the current weekend's saved hunt, collected victory
cache and acknowledged return. A unique character/weekend completion records
its exact run and attempt. The fourth completion and the `planet-revora-v1`
wing grant commit in the same transaction. A suppressed award rolls back the
fourth completion, leaving its cache deliverable. The award starts at that
fourth delivery and expires exactly 720 hours later. It is one award per
character; later weekends, repeated delivery, login and expiry do not renew it.
Weekends need not be consecutive. Unknown or partial histories cannot qualify.

The ordinary wardrobe refresh immediately publishes the grant. Database-backed
wardrobe reload/equip and precise expiry/unequip pass. Four-weekend progress is
not inferred from title ownership, current map, manually completed quest bits,
or participation in another character's encounter.

Build and **94 Planet checks** pass. The fixture uses earned Birdman death
callbacks, owned guard deaths, actual dynamic-map entry/cache collection and
Rowan's packet dispatcher for the later weekend entries/deliveries. All four
completion rows trace to those saved earning receipts; they are not seeded
final completions. Tests include nonconsecutive weekends, concurrent first
delivery, no early award, fourth-award failure/retry, unchanged expiry across
reload, ordinary equip, exact expiry/unequip and no renewal after expiry.
The shared title suite passes **183 parent checks plus five Fashion child
checks**; Rowan's original Path Hunt regression passes **552 checks**.
Logs: `planet-delivery-build.log`, `planet-delivery-tests.log`,
`planet-delivery-titles.log`, `planet-delivery-path.log` under
`obj/chi-refill-validation`.

This closes the automated server earning route, not the remaining acceptance
gates: actual Host restart across earned progress, full damage-driven combat
balance, genuine-client map/NPC/HUD/dialogue/wardrobe acceptance, and deployment.
Native acceptance stays pending by owner choice. No live server refresh or
public patch publication occurred. The wardrobe matrix now classifies this
one route as `code-validated`, with `live_acceptance=pending`.

## Fresh-process recovery fixture (2026-09-22)

The Planet suite now starts a separate x86 validation executable with empty
session, encounter and progress caches. Its inputs are earned by the parent:
one actual hundred-kill hunt with a defeated guard, one collected cache with
an intentionally unacknowledged return, and the completed four-weekend wing
route. No attempt, final completion or wing award is inserted as fixture proof.
The parent stops those offline fixture sessions without graceful abandonment;
the child executes the normal initialization/interruption/login recovery seams.

The child cancels the interrupted fight while retaining its earned hunt,
acknowledges the cache's pending safe return, delivers that recovered cache
through Rowan, and reloads/equips the already earned wing. Repeated recovery
cannot duplicate a completion or grant. The original expiry survives the
process boundary and is enforced at its deadline. Parent readback confirms the
child's recovered return and completion remain committed after it exits.

The first child run exposed missing fixture initialization for Perfection
ability scores during wardrobe refresh. The normal `PerfectionManager.Load`
loads that dependency; the child now loads the same score data. No production
change was needed. The child constructs canonical ordinary fixture sessions
for existing persisted UIDs; it does not perform wire authentication or a full
`EntityTable` character load. Its initial world map represents the normal safe
login fallback. This is fresh validation-process evidence, not an actual Game
Host/socket/native restart acceptance result.

Validation: **101 parent Planet checks plus 9 child checks pass**. The harness
compiles with warnings as errors. Log:
`obj/chi-refill-validation/planet-process-tests.log`. The child is hidden,
bounded to thirty seconds, and accepts only the named disposable schema.
Connection credentials travel through redirected stdin and are never written
to command-line arguments, fixture files or output. Production source remains
unchanged from the preceding batch; no deployment or runtime refresh occurred.

## Combat execution and normal monster AI (2026-09-22)

A new direct-execution test reproduced a real gap: target selection rejected
outsiders, but `new Handle(null, guard, outsider)` still applied damage through
the legacy monster branch. The fix adds Planet ownership checks at direct
monster execution and final damage application, with a nonblocking owner item
lock for the duration of combat. This matches encounter lifecycle ordering and
prevents damage during a contended owner transition. Copied monster identities,
foreign players and expired scenes are rejected before HP changes. Ordinary
spell execution retains its existing path; this change does not alter damage
formulas or sentry/captain stats.

The fixture calls the normal screen monster callback and verifies target
acquisition, movement into melee range on the loaded map, actual damage,
cooldown, and no acquisition of flying/invisible players. Outgoing packets are
captured in a bounded offline queue without opening a socket. One observed
unarmored level-140 fixture hit was 382 damage with the sentry's 1,200 attack;
this is evidence that catalog attack is not guaranteed player damage, not a
balance target or a promise for equipped characters.

The last three weekends of the earned wing route now defeat all nine guards
through `Calculate.Melee` and `ReceiveAttack`, from adjacent walkable tiles,
instead of invoking death directly. The offensive fixture uses controlled
20,000 attack; it validates calculation/application and durable defeat credit,
not a retail-equivalent build, packet cadence or class balance.

Build, **122 parent Planet plus 9 child checks**, and **388 existing event
checks** pass. The pre-fix failure is recorded in `planet-combat-before.log`;
passing logs are `planet-combat-build.log`, `planet-combat-tests.log` and
`planet-combat-localpk.log`, all under `obj/chi-refill-validation`.
Actual Host/native acceptance and representative equipped-class combat tuning
remain pending. No production deployment, runtime refresh or patch release.

## Four earned weekends across actual game processes (2026-09-27)

- [x] Start the private game composition, create an eligible ordinary profile,
  accept Rowan's hunt and defeat ten actual loaded Birdmen through ReceiveAttack.
  Interrupt the game process and restart PostgreSQL and the game. Production
  character loading plus normal Planet login hooks retain exactly ten kills.
- [x] Finish the remaining90 kills, enter through Rowan, defeat the two sentries
  and captain, collect the cache through its NPC and deliver to Rowan. Repeated
  delivery records only one completion and grants no early wing.
- [x] Complete three additional nonconsecutive Pacific weekends, each in a new
  game process. Every weekend earns100 loaded Birdman kills and a new guarded
  cache; the fourth delivery alone grants Wings of Planet60096009. Opening the
  wardrobe before delivery still permits immediate equip afterward.
- [x] A sixth actual process restores the equipped wing before another equip
  request. Repeated acceptance/delivery leaves exactly four completions,400 saved
  kills, one wing receipt and the same earned-time/expiry/source fingerprint.
  The grant's expiry remains exactly720 hours after the fourth completion.
- [ ] Authenticated/native client acceptance, naturally elapsed expiry, combat
  balance and deployment remain pending. Interrupted private-fight recovery and
  injected transaction failures retain their separate component-test coverage.

Reproduce: `tools/Verify-ArenaProcess.ps1 -PlanetOnly`. All six launches pass in
`obj/planet-process-run.log`, using the existing executable SHA256
`F702C021C627E79513165F367F795F554B517985A731034A40BAD3646710ACB8`.
No production code change or rebuild was needed. The helper supplies eligibility,
proximity, lethal damage and synchronized private event/death/wardrobe clocks.
It neither seeds hunt progress, completions or prizes nor substitutes synthetic
monsters for the loaded Birdmen. It is an offline-profile server process test,
not a wire-authenticated or native-client playthrough.

The initial helper passed the first weekend but shifted its saved simulated clock
outside the second weekend through implicit timestamp conversion. Explicit
DateTimeOffset parameters fixed the test metadata; production quest timestamps
already use explicit TimestampTz parameters. No event calendar rule was relaxed.
The marked database clone is removed and all private test processes stop after
success. Live services, database and installed client remain unchanged.

## Earned expiry boundary and expired restart (2026-09-27)

`tools/Verify-ArenaProcess.ps1 -PlanetOnly` now completes eight game launches:
the existing interrupted hunt, four earned weekends and equipped recovery, then
two additional expiry phases. It reuses the reviewed private game binary
`E9EEF3BBD597B78F7823E6314E15A4E9183C15525E2FE9E561E392155A85526E`.
All eight phases pass in `obj/planet-expiry-process.log` (ignored).

- [x] Load the earned, equipped wing one .NET clock tick before its saved expiry.
- [x] At the exact expiry, normal wardrobe equip/login handling removes the wing.
  Repeated equip and Rowan delivery requests cannot restore or extend it.
- [x] Start another actual game process at the expired timestamp; normal loading
  and repeated wardrobe/delivery requests still leave the wing unequipped.
- [x] Preserve all four completions,400 kills and the sole award's original
  earned-time/expiry/source fingerprint. Replay leaves CPs, RVPs and silver unchanged.
- [ ] Authenticated/native UX, natural elapsed expiry, combat balance and live
  deployment remain pending. These phases use a controlled UTC clock, not thirty
  elapsed wall-clock days. They do not prove automatic expiry notification to an
  idle connected native client.

No production code, reward rules, schema or installed client changed. The runner
compiles only its private helper, removes its owned clone after success and stops
its private database. The earlier six-phase report remains historical evidence;
this entry adds expiry and expired restart coverage without changing inventory counts.
