# LivingWorld startup ownership

## Confirmed race and repair (2026-09-27)

`Server.Run` constructs the world timer before calling `Thread.CreateTournaments`.
The latter initializes LivingWorld. Previously `LivingWorldSystem.Create` set
`Ready=true` before calling `FillCities`, without acquiring the timer's `TickGate`.
The already-running timer could therefore enter `Tick` and run a second
`FillCities` concurrently with initialization.

Both population loops read the mutable `Actors` list without taking its `Gate`;
`SpawnOne` appends under `Gate`. The timer's existing `TickGate` prevents two timer
callbacks overlapping, but did not protect against the initializer. In addition
to inconsistent population counts, concurrent list reads/writes are unsafe.

Initialization now holds the same `TickGate`, finishes population, then publishes
a volatile readiness flag. Repeated successful initialization is a no-op. A
failed initialization cannot expose ready status. The timer remains nonblocking
when another owner holds its gate, and normal population/AI rules are unchanged.
No persistence schema, reward, public listener or client file changes.

## Regression evidence and limits

`tests/LivingWorldStartup/Verify-Startup.ps1` compiles a focused x86 regression
with warnings as errors against the private candidate assembly. It runs with an
empty world and no configured database. A separate initializer thread must not
complete or publish readiness while the test owns the timer gate. Once released,
initialization completes, publishes readiness and permits subsequent ticks and
idempotent initialization.

The old candidate fails that ownership assertion; the repaired build passes.
Logs: `obj/living-world-startup-before.log`,
`obj/living-world-startup-after.log` (ignored). The empty-world fixture proves
exclusion and publication order, not a reproduced NullReferenceException or
every possible population/combat race.

The initial Guild Beast acceptance run had a one-off startup NullReferenceException
whose original frame was obscured by its runner's rethrow. This repair addresses
a source-confirmed startup race, but cannot retrospectively prove that exception's
cause. The runner now preserves the original stack for any recurrence. Do not
describe a passing rerun as proof that all startup failures have been eliminated.

The isolated source-overlay game build passed with existing legacy warnings;
unrelated owner work was excluded. Private gameplay SHA256 is
`E9EEF3BBD597B78F7823E6314E15A4E9183C15525E2FE9E561E392155A85526E`.
The complete five-phase `-GuildBeastOnly` journey passes on that build:
Guild War capture and payout, funded beast hunt, post-midnight claim after the
game/database restart, and exact reward/achievement persistence in the final
process. All five startups complete. Log: `obj/living-world-startup-process.log`
(ignored). This verifies the repair in the composed private game as well as the
focused concurrency fixture; event clocks and combat inputs remain controlled.
Live deployment and native acceptance remain pending.
