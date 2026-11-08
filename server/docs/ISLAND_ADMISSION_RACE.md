# Sky Pass and Snake Islands admission cleanup race

## Correction (2026-09-28)

Both quests install their active admission before teleporting under the player's
item lock. Their cleanup ticks previously inspected the map outside that lock.
A tick could see the pre-arrival home map, wait for the admission lock, then
cancel the run after the player had successfully arrived. Snake's admitted kill
could consequently have no active attempt to credit; Sky's gate route could
already be abandoned.

Both ticks now recheck the canonical player, active run identity and current map
after acquiring the lock. Snake also recomputes the quest day before choosing
departure versus midnight cleanup. A completed arrival or replaced attempt is
left alone. Genuine departures and expired quest days retain their cancellation
rules; rewards and eligibility did not change.

## Reproduction and validation

The actual-process fixture obtains an ordinary NPC admission, holds the owner
lock, exposes the pre-arrival map and starts a cleanup worker. After observing
the worker blocked, it restores the arrival map before releasing the lock. This
recreates the identified ordering without fabricating any quest record.

On old private executable6CF84170, both Snake and Sky failed the unchanged-run
snapshot assertion: the waiting cleanup canceled the arrived admission. On the
corrected build both preserve it. The fixture then makes a genuine departure,
verifies cancellation, takes a new ordinary admission and completes its earned
route. Thus avoiding cancellation is not achieved by disabling cleanup.

The isolated committed-source build passed with existing legacy warnings.
Corrected private executable SHA256:
`13F3B2EDB5482A9923D408D5DD4348CCAA0E8A2113418295C305BCAC8CCEA281`.

Both `tools/Verify-ArenaProcess.ps1 -SnakeOnly -ResetOwnedFixture` and
`-SkyOnly -ResetOwnedFixture` pass all three phases. Beyond the race assertions,
these cover abrupt exit with earned pending rewards, game/database restart,
authenticated NPC claiming, immediate exact bound inventory, achievement
persistence, another fresh login and duplicate refusal. Helpers compile with
warnings as errors. The producer inventory remains332/337; only Snake's source
line reference changed. Both owned clones were cleaned and PostgreSQL stopped.

The ordering is controlled in the fixture, and damage/proximity remain supplied
for the first earning phase. This proves the cleanup defect and its correction,
not native movement or complete socket combat. The earlier spontaneous Snake
missing-victory observation lacked an attempt-state trace, so its exact cause
cannot retrospectively be confirmed. Keep the added diagnostics. Native
acceptance and deployment remain pending; no live process changed.
