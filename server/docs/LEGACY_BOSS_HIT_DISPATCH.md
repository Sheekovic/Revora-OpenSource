# Legacy boss single-hit dispatch

2026-10-03. Private candidate fix; not deployed. This addresses M6's legacy
monster dispatcher independently of the unresolved physical/magic classification.

`Handle.Execute` used independent name groups for Ice Queen/banshees, Chaos
Ripper, shared named bosses, Dragon King/Lava Beast and White Dragon/Sun Devil.
Names overlap those groups. After a scripted branch applied its damage/status,
a later group could apply another hit, followed by the common melee/magic hit.
The unmodified private candidate reproduced2,925 damage from one Chaos Ripper
callback despite its individual scripted roll being1,000-1,999.

A per-dispatch flag now records an applied scripted hit. Later name groups are
skipped, and the common calculation is skipped after the existing monster
animation message. A spell that has no scripted damage branch still reaches the
common calculator. Original random ranges, status operations, area loops and
Backfire calls are unchanged. This reduces damage that previously stacked through
fallthrough; it is not a boss-stat rebalance or a claim of retail combat parity.

## Validation

- `career-legacy-boss-hit-baseline` reproduced2,925 on prior executable
  `943AA1FBE2BF19433045F4CAF5CDA0FE4BCBEF2EC1EFAE9CD703995346FC6435`.
- The isolated x86 build passed. Only the stopped private candidate was refreshed,
  with its previous executable and manifest preserved.
- `career-legacy-boss-hit-verify` passed112 scripted hits across14 legacy names,
  each within1,000-1,999; the final run observed1,001-1,998. It also passed85
  selected generic-spell fallbacks and a separate ordinary melee fallback.
- `career-legacy-boss-hit-reload` preserves the dedicated ordinary profile's
  saved normal health and empty inventory after game/database restart.

The fixture uses a registered synthetic monster copied from an installed family
and ordinary non-GM profile4294918901. Names/spell inputs and a large temporary
health ceiling isolate dispatch behavior; fallback tests give the monster a
positive5,000 magic-attack input. It does not establish natural boss difficulty,
all spell branches, secondary area targets, counter-kill ordering or native FX.
Early verifier failures incorrectly treated every named-boss spell as scripted,
then expected positive magic damage from a zero-magic fixture. A reload assertion
also incorrectly expected the temporary100,000-HP ceiling to persist; the fixture
now restores the character's calculated normal maximum before saving. Those
fixture corrections did not require additional production changes or weaken the
1,000-1,999 scripted-hit check. The complete corrected verify/reload run passes.

Source: `tools/CheckCandidateLegacyBossHit.cs`; probe
`revora_legacy_boss_hit_probe`; logs under
`C:/Revora/DisposablePostgres/moon-box-candidate/arena-process-career-legacy-boss-hit-{baseline,verify,reload}.log`.
Fixed executable SHA256:
`6FAB9165AB907FEF8BB82D6F3FA0CB6E454448D20AEB92C9A8BA24386D0762D6`.

The22 fixed-damage call sites still need evidence-backed physical/magic/special
classification. This fix does not claim that Magic Defender or Defensive Stance
protects against every scripted attack. Owned encounter controllers keep their
existing earlier dispatch paths. No new quest, reward or achievement count is
claimed; client acceptance and live deployment remain separate gates.


## Secondary area-target eligibility (2026-10-03)

The prior private executable reproduced 1,767 damage to an ordinary nearby
player with `Attackable=false`. Nine legacy area loops trusted cached screen
membership and also replaced the primary target with each secondary target.
They now keep the primary target unchanged and reject dead, foreign-map,
protected, unloaded, persistence-fenced or noncanonical ordinary players.
A primary body appearing in its own screen is skipped. Existing fake actor
handling remains allowed after body identity, map and life checks.

`career-legacy-boss-area-verify` passes against two ordinary profiles
4294919001?4294919002: no protected damage/Chaos Cycle, equal single damage to
an eligible primary and secondary, no primary duplication, and no damage/status
to a foreign-map or removed-session entry. The fixture controls screen entries
and selects Ice Queen spell 30011 through bounded random attempts. It exercises
the real Handle callback, but does not claim native rendering, every area spell,
or concurrent target mutation coverage. All nine loops use the same predicate;
existing ranges, damage rolls and spell effects are preserved.

The isolated build and existing boss-hit verify/reload regression use executable
`F1FF87120645900CB1F7B288D1B9EB55519A3C124C037D7B9F383A9AAF616B9F`.
Fixture: `tools/CheckCandidateLegacyBossArea.cs`; persistent probe:
`revora_legacy_boss_area_probe`. Private candidate only; no live deployment.

## Secondary target save/medicine contention (2026-10-03)

The eligibility predicate did not hold a secondary player's persistence/health
locks across the damage calculation, Backfire, status effects and health update.
The surrounding attack lease owns the primary, not all cached screen targets.
A concurrent save or medicine transaction could therefore overlap the secondary
hit. Individual Hitpoints setter locking does not protect the whole operation.

All nine legacy area loops now acquire the existing nonblocking
HealthMutationLease for each secondary and recheck eligibility under that lease.
Contention skips damage, counter and status effects for that target. It does not
wait while holding the primary's locks; using/continue releases partial ownership.
No new lock order, damage formula, range or reward policy is introduced.

The ordinary two-player fixture now holds the secondary's ItemSyncRoot on a
separate worker, confirming the loaded HealthBalance owns that player. Before
repair, the test reproduced1777 damage despite that lock on executable
`3B0741F840844A1116C4FB9297E8279EF8C418BB4A3130901007CDF72A363A0E`.
After repair it observes zero secondary damage/no Chaos Cycle while the primary
still takes damage. Releasing the lock restores matching single hits and the
secondary status. Existing protected/foreign-map/stale-session cases also pass.
The first fixture attempt incorrectly required the separate expedition-pill
synchronization flag; it was corrected to assert the normal loaded health owner.

The private x86 build passes. Area contention, legacy single-hit regression and
its cold reload use executable
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
This is one exercised area spell plus source coverage of nine lease sites; it
does not claim every animation, simultaneous attacks on all targets, or native
acceptance. No live restart/deployment; unrelated working edits are excluded.

The separate original damage classification remains unresolved. On2026-10-03,
the [official Snow Banshee guide](https://co.99.com/guide/monsters/snow_banshee.shtml)
provides skill shapes/effects but no physical-versus-magical mitigation mapping
for these legacy spell IDs. Animation names alone are insufficient evidence to
change Magic Defender or Defensive Stance behavior. This synchronization repair
does not close M6.55b5b2b3b/M6.55b5b2b4b or certify retail numerical parity.
