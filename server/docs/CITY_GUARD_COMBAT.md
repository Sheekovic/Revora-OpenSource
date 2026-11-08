# City guard durability and pursuit

Named city officers now have at least 1,000,000 HP; Darius has at least 3,000,000. These are minimum profiles applied after equipment and cultivation calculations. Recalculating stats preserves existing wounds, does not stack bonuses, and gives ordinary players no guard bonuses.

| Minimum stat | Officer | Darius |
| --- | ---: | ---: |
| Physical defense | 16,000 | 24,000 |
| Flat magic defense | 8,000 | 12,000 |
| Magic defense percentage | 40% | 55% |
| Immunity | 60% | 80% |
| Counteraction | 60% | 80% |
| Block chance | 25% | 35% |

Attack stats retain their existing equipment-based values. Guards remain killable. Street officers respawn after twelve seconds and Darius after five seconds.

A landed player hit now enters the shared accepted-hit handler, applies calculated damage and starts a five-second street warning. Previously an early handler intercepted some attacks before normal target resolution, discarded their damage and teleported the player; other attack paths missed it. Repeated hits cannot reset the warning or postpone pursuit. Red/black names retain pursuit on sight. At most two nearby officers pursue one offender; dead, removed or out-of-range assignments release their slots. Pursuit retains the existing eighteen-tile acquisition range, five-minute incident duration, movement and guard update cadence (roughly one second).

The arena sentence now follows killing an officer. Darius can target an actively sentenced player after the warning; the previous target predicate excluded everybody in his own arena. Visitors without an active sentence cannot damage him or be selected as his opponent. Defeating him uses the existing release path and returns the player to Twin City. Disconnected, dead, unattackable and spectating players cannot be pursued.

## Verification

`tests/CompanionAI/Verify-Guards.ps1` compiles offline fixtures against the staged server assembly. Its 33 checks exercise actual stat recalculation, accepted combat hits, the guard update loop, movement, warning expiry, repeat hits, assignment replacement, arena targeting, lethal damage, sentencing, teleport and release. Characters are ordinary non-GM players with buffered network fixtures; no live account, database or server listener is used by this test.

Regression checks: 24 adventurer retaliation, 57 companion navigation, 669 combat calculations/class pairs, 210 event/boss rewards and 67 cultivation checks. Total: 1,060 checks passed. Client movement and difficulty with live equipment have not been visually verified during this rollout. No client patch is required.

Deployed 2026-09-15T02:19:19.9909260Z as PID 1000, SHA-256 `724F82B950182287B74ED1227AE705B582B29832EEB82B754937DD2D69DD4417`. Both existing listeners and protocol handshakes, the 1,000-monster Potency population, four daily boss schedules and Extraction startup passed validation. Rollback: `deployment-backups/guards-20260914-191824`.
