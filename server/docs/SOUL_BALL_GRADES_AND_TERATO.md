# Soul Ball progression and Terato earning

Follow-up to `bd13166`, implemented on 2026-09-16. The daily service now starts
with Normal and advances through Refined, Unique, Elite and Super following
completed bead hunts. The registered Terato encounter supplies its documented
1,000-spirit bonus within the same transaction that records victory and loot.

## Evidence and scope

The [official Spirit Bead guide](https://co.99.com/guide/quest/spirit-bead.shtml)
confirms a Normal first bead, a finer bead following completion, five grades,
and 1,000 spirits for Terato Dragon. It does not specify every transition after
a Super or paid direct completion. The Normal restart used here follows the
older Revora Envoy's stated Super-cycle convention; exact original behavior
for these transitions remains an edition-validation question.

The current registered Terato source is `TeratoEncounter.Family = 9916100`,
spawned by the owned Dragon Pill routes at the Royal Guardian in Twin City or
the sixth-floor Grotto seal. Identity and eligibility come from that
coordinator's live actor, paid summon, party, killer and victory record. A
monster's displayed name or copied family number is insufficient evidence.

The existing configured study, EXP, CP, RVP, blessing and payment amounts are
unchanged. Other named bosses and wild leaders still need their own proven
earning sources. The legacy Thrilling Spook NPC currently constructs monsters
inline from RAM quest flags, without a durable encounter record; that route
needs its complete Exorcism lifecycle repaired before it can provide equally
strong bonus evidence. No name-based shortcut was added.

## Grade behavior

- The next free grade derives from the latest committed completion's exact
  bead type. Repeated dialogue, reconnecting, waiting overnight or discarding
  an unfinished bead cannot advance it.
- An accepted bead retains its grade, progress and snapshotted reward terms.
  Lost-bead replacement retains the same grade.
- Completing Super begins another Normal cycle. Paid direct completion also
  begins a Normal cycle. The explicit `paid_finish` flag records that action
  even when its configured price is zero; historical nonzero completion fees
  remain recognized during migration.
- A legacy completion-only record has no proven grade and starts at Normal.
  A completed adopted bead with a known type advances from that actual grade.
- An earned Super is free. The old paid-Super continuation cannot charge for
  it, and the NPC no longer offers a redundant paid choice.
- The obsolete random-grade setting remains readable for configuration
  compatibility but no longer controls issuance. The dialogue describes the
  actual grade progression.

## Terato transaction

Victory acquires the killer owner's inventory lock without waiting, then the
encounter lock. A busy owner leaves the paid boss alive for another attack;
the path never waits for that owner while an encounter caller holds its lock.
The existing coordinator revalidates actor identity, party membership, range,
expiry and current player/companion ownership.

Within the existing victory transaction, `DailySoulBall.RecordTerato` requires
both locks, the still-registered live actor and the just-recorded victory for
this killer. It verifies the exact carried bead and active daily record,
increments spirits by up to 1,000 without exceeding the requirement, and
inserts `revora_soul_ball_bosses` evidence. The evidence records the unique
source run, daily run, recipient and before/after count.

The spirit change, source receipt, boss victory, team study entitlements and
three loot entitlements commit together. If any required write fails, all of
them roll back and the boss remains resumable. The reconnect path reconstructs
the already-paid summon and allows another attempt. Study delivery and floor
loot retain their existing durable recovery mechanisms.

Only the admitted killer receives the spirit bonus. A registered companion
credits its canonical player owner; nearby teammates still receive the normal
Terato study reward. Without a valid carried bead there is no banked bonus for
a later quest. An already-full bead does not generate another credit. A
released corpse, counterfeit actor or outsider cannot reuse the source.

## Validation and remaining gates

The isolated build passed. Final suites:

| Suite | Checks |
| --- | ---: |
| Soul Ball, including shared rebirth prerequisites | 559 |
| Grotto, Dragon Slayer and Terato | 262 |
| Price Hunt | 511 |
| Release Souls | 35 |
| Quest packets | 9 |
| Experience | 452 |
| Gameplay total | 1,828 |
| Producer audit tests | 3 |

The five-day cycle fills all default requirements using 3,750 canonical
level-97 death callbacks, redeems each grade and reloads between days. This
fixture controls map placement and source invocation. The earlier issued
Super journey still exercises ordinary hunt-list travel and 250 actual
`Entity.Die` calls.

The new boss route issues a Normal bead, makes three paid Royal offerings,
records 1,000/1,000/500 earned spirits, reconnects between victories and redeems
the entire reward. Offering materials and lethal damage are fixture supplied;
the independent Grotto suite retains its earned Dragon Pill, summon, combat
and loot journey checks. Failure fixtures suppress daily progress, boss-credit
receipts and Terato loot writes, then reconnect and resume the paid encounter.
Additional cases cover counterfeit actors, outsiders, busy owner locks,
companion registration and teammate/killer attribution.

Final harness SHA-256:
`839B7DB5F742DB2B49B011D47C38F18E1CF68679ACC2D8BF1A4CE8B4B12BE4C6`.
Ignored logs: `obj/chi-refill-validation/soul-grades*.log`.

No achievement producer was invented: the audit remains 219 present and 118
absent, an upper bound rather than a per-character earning proof. Remaining
work includes other leader/boss bonuses, Exorcism and Anniversary transactions,
original numerical/transition parity and native 6711 acceptance. No live
deployment, production schema change or client patch was performed.
