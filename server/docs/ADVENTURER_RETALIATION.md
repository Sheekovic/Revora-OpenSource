# Adventurer retaliation

A real, nonzero player hit now wakes a registered adventurer instead of being silently discarded. The accepted-hit path applies actual damage, then records the aggressor. Name prefixes alone cannot enroll an entity in this behavior. Adventurers retain their existing equipment, class stats and damage calculations; they can still be killed.

## Combat behavior

- Separate 100 ms timer; tactical decisions are spaced by at least 180 ms. Normal city/event timers remain at their existing rates.
- A clear pursuit jump covers at most six tiles. Blocked routes use the existing bounded pathfinder. Approach can lead a moving target by one tile.
- Melee attacks have a 450 ms minimum interval (normally about 600 ms with decision scheduling). Spell attacks wait at least 600 ms or their longer database cooldown. Mana, stamina and learned spell checks remain active.
- Archers and Taoists jump out of close melee range, then can fire on the same decision. Evasion has a 900 ms cooldown. Water Taoists can use their existing learned heal below 35% health.
- The first live aggressor remains the target. Subsequent hits cannot reset the attack cooldown. Nearby bystanders and the adventurer's employer are never selected.
- The fight lasts until the aggressor dies, disconnects, teleports, becomes unattackable, escapes 36 tiles from the original fight, or stops hitting for 30 seconds. Hired adventurers also remain within a 16-tile target leash of their employer. There is no pursuit teleport.
- Hiring is unavailable during a fight. Existing owner commands can still de-escalate their companion through intent cleanup. Death/revival clears the fight.
- Spawn/revival restores the adventurer's calculated maximum HP, instead of retaining the much smaller pre-equipment HP value.

## Provocation rules

PK or Team mode is required to deliberately attack an adventurer. Twin City has a narrow registered-adventurer exception similar to guards: provoking an adventurer permits that adventurer to retaliate against the aggressor. This does not enable ordinary player-versus-player attacks in Twin City. Market and other protected maps stay protected. Same-map/session identity, visibility, attackability and terrain checks apply; spectators cannot provoke a fight.

Normal monster combat and companion assisting continue when retaliation ends. These actors receive no fake-player kill bounty, quest rewards or looting privileges. Existing player death processing remains responsible for the defeated aggressor.

## Verification

`tests/CompanionAI/Verify-Retaliation.ps1` exercises ordinary-player fixtures through the real accepted-hit handler and fast timer, with buffered disconnected network fixtures and no database access. It covers provocation, wall rejection, jump pursuit, focus, cooldowns, kiting, mana, expiration, logout, death, map changes, protected maps, employer safety, hiring restrictions and the normal player-death path.

The rollout also runs the existing companion, combat-calculation and event regressions. Server-side behavior is verified; a fresh visual client test is still useful to judge the difficulty and movement feel with live gear.

Validation result: 960 passed checks (24 retaliation, 57 companion, 669 combat and 210 event). Deployed 2026-09-15T01:56:35.5832929Z as PID 8972, SHA-256 `53161B6852CBF98D99556BE4BF5C158B45F70D45B64BB2BD3E73C335C8FC229C`. Both existing listeners and protocol handshakes, the 1,000-monster Potency population, all four daily bosses and Extraction startup were healthy. Rollback is preserved under `deployment-backups/adventurers-20260914-185624`.
