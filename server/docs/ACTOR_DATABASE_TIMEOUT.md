# Adventurer movement and database timeouts

## October 3 report: live deployment mismatch confirmed October 4

The owner-supplied `Revora/docs/error-3-10-26.log` contains two authentication
timeouts from `LivingWorldSystem.MoveToward` through `Entity.X` and
`Entity.UpdateDatabase`. This is the write path repaired in commit `61cf7720`.

Observed game PID9048 started October3 at05:35:14 and points to
`bin/Debug/Revora.exe`, hash `724F82B950182287B74ED1227AE705B582B29832EEB82B754937DD2D69DD4417`.
Reflection-only IL inspection finds no fake-owner field read in any of its five
UpdateDatabase overloads. The prepared integrated A9B83286 build has that read
in all five. This inspects the file at the process path, not loaded process memory.

The existing companion suite passes all57 checks against the integrated build,
including production-FullyLoaded fake actor movement with no database configured.
No new movement test or full game startup was needed. The prepared fix works in
that regression; it has not been installed in the running game.

[Binary and regression evidence](research/ACTOR_LIVE_BINARY_CHECK.json).
Include this existing fix in the coordinated release. Do not independently
replace the live executable with the integrated worktree build: its other
source/client dependencies and release gates remain open. No live restart,
database change or deployment occurred. The underlying authentication outage's
cause is still unproven; removing unnecessary writes does not fix every outage.

## Original correction

2026-09-22: reported Npgsql stream/authentication timeouts affected movement,
voting, heartbeat and player buffs. Current local PostgreSQL on 127.0.0.1:25432
accepted TCP and answered a PostgreSQL negotiation probe. A subsequent ordinary
shop purchase suite completed all 77 checks. These checks establish recovery
at observation time; the original interruption's cause remains unproven.

Separately reproduced an unnecessary write path: LivingWorld adventurers have
Fake=true and FullyLoaded=true, so X/Y setters sent character UPDATE commands
for session-only actors. An offline movement regression with the production
FullyLoaded flag failed before the fix (MoveToward, Host can't be null).
Entity.UpdateDatabase now excludes fake owners in all five overloads, before
progression or SQL handling. Real-character persistence retains its existing
path. Build and all 57 companion checks pass with no configured database.

Evidence: obj/chi-refill-validation/actor-db-before.log, actor-db-build.log,
actor-db-tests.log and p6-shop-tests.log. The fix is source/build only; the
running server was not replaced or restarted. It removes synthetic writes,
but does not prove prevention of independent database/network outages.
