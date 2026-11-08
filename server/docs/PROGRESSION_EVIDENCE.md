# Trusted achievement and event-award foundation

Roadmap: [M1](QUEST_EVENT_ROADMAP.md). Implemented and automatically validated;
not deployed or visually verified in the live 6711 client.

The server no longer accepts achievement completion reported by a client.
Only known 6711 IDs with committed server evidence appear in achievement state
and count toward Rising Star. The original thirteen-word packet layout is retained.

## What this batch implements

- A factual 337-ID catalog, preserving the installed bit positions and excluding
  79 undefined positions. No proprietary presentation text is included.
- Bounded read requests and rejection of client completion requests, including
  forged identities, aliased IDs and malformed frames. Reads are rate-limited.
- Durable, idempotent achievement records with source, source key and rule version.
- An immutable first-load backup of original legacy achievement text and its known
  bit count. The original `achievement` table is also preserved.
- A transactional award repository supporting permanent summer crafting awards,
  seven-day local Elite PK wings and the documented fourteen-day Team PK edition.
- Wardrobe reconciliation that retains legitimate permanent awards, enforces
  expiry online/offline and preserves an earned wing's equipped selection on reload.
- Five current-state achievement producers usable by ordinary players:

| ID | Confirmed server observation |
| --- | --- |
| 10106 | Hold at least 3,000,000 silver |
| 10107 | Equip an actual mount in the mount slot |
| 10108 | Equip that mount with lineage/+ level 12 |
| 10218 | Have a matching character and guild membership identity |
| 10219 | Be that guild's leader |

These are historical milestones once observed: subsequently spending silver,
unequipping a mount or leaving a guild does not erase their completion.
They are rechecked on login and through wardrobe reconciliation, normally within
ten seconds online. Changed achievement packets are sent after the evidence
transaction commits. External event grants are refreshed within thirty seconds;
a future completion producer should invalidate the character's cached evidence
and reconcile immediately after its own transaction commits.

**This batch does not activate CTF, Elite PK or a summer event.** The repository
provides their persistence seam. Award insertion has no player-facing arbitrary
grant endpoint. Unknown sources and unrelated reward/source combinations are
rejected; existing current-condition wardrobe requirements remain enforced.

## Migration behavior to review before activation

Legacy client-reported flags are retained as archival evidence and are not promoted
to trusted completions. On activation, the displayed achievement set and Rising Star
eligibility would be rebuilt from recorded server evidence and the five confirmed
observations above. A player with unsupported old flags could therefore see less
progress or lose Rising Star eligibility. This migration has not been applied to
the live server.

The remaining quest/tournament producers and per-character route to 320 are tracked
separately in the roadmap. The synthetic 320-row fixture proves the threshold and
persistence behavior; it does not prove that 320 real gameplay objectives are
currently reachable. Do not advertise that outcome yet.

An achievement legacy-load failure now fails character loading instead of continuing
with an empty replacement. Backups use additive tables and retain the first original
text even if a later session presents different data. New character evidence is
isolated by character UID. The old `addachi` administrative shortcut no longer
fabricates earned progress.

## Transaction and time behavior

`ProgressionEvidence.Complete` and `Grant` require a caller-owned database transaction.
They are intended to participate in the future quest completion, payment or material
consumption transaction. Conflicting retries do not overwrite the original source,
roll or expiry. A failed transaction does not install a corresponding in-memory
achievement or wardrobe grant.

Tables are `revora_achievement_backup_v1`, `revora_achievement_evidence` and
`revora_event_awards`. Revocation is an internal/operator action; no normal player
can request it. Reconciliation excludes revoked, future, expired and invalid grants.
The existing wardrobe blob and its original backup table remain compatible.

All award instants use explicit PostgreSQL `TimestampTz` parameters and UTC reads.
This matters with the pinned Npgsql 4.1 driver, whose default `DateTime` write
mapping predates the UTC inference introduced in Npgsql 6. Tests use a different
database-session timezone when writing awards. PostgreSQL's microsecond precision
is accounted for in the exact-boundary fixtures.
See [Npgsql date/time documentation, including pre-6.0 behavior](https://www.npgsql.org/doc/types/datetime.html).

## Validation

The isolated server build succeeded. New/updated fixtures passed:

- **63 progression checks:** catalog validity; legacy archive retention; all 337
  forged completion requests; truncated/malformed requests; ordinary-player state
  producers; duplicate/concurrent evidence and awards; rollback; failed-write retry;
  fake-player exclusion; permanent/temporary ownership; timezone and exact expiry;
  reload, revocation, equipped selection and synthetic 319/320 boundaries.
- **91 wardrobe regression checks:** existing progression thresholds, collections,
  CTF leadership, Elite status, persistence, request authorization and SQL rollback.

Validation commands, after the isolated build described in
[Companion AI validation](../tests/CompanionAI/README.md):

```powershell
& tests/PotencyHunt/Verify-PotencyHunt.ps1 -ProgressionOnly
& tests/PotencyHunt/Verify-PotencyHunt.ps1 -TitlesOnly
```

Fixtures use the existing disposable `revora_potency_validation` schema and clean
it up. They run sequentially because that schema is shared. They do not start the
server entry point or listeners. Local result logs are ignored under
`obj/chi-refill-validation/progression-tests.log` and `progression-titles.log`.

Validated isolated `Revora.exe` SHA-256:
`b658c95d320063317722bab1f58bd35a9dbf248f99b162f8d7700f142e1d8a61`.
No runtime executable, live database migration or client patch was deployed.

Before live activation, review the legacy-progress migration impact, preserve the
matching original title backups, and check an ordinary character's achievement UI,
new-completion display, wardrobe points and nearby-player wing appearance on the
approved client. Restore the previous executable plus matching saved title state
if rolling back; do not delete original backup/evidence tables as part of rollback.


## M2 producer follow-up

Local CTF now records flag planting (10230) and victory for winning contributors
(10301) through the transaction API. See [local CTF](LOCAL_CTF.md) for its rules,
48 event checks and pending live validation. The five current-state producers
above describe the M1 baseline.

## M3 producer follow-up

Local Elite PK now commits its highest-bracket champion result and seven-day
Infernal Beast award (60010020, source `elite-pk-local-v1`) atomically. Champion
status 16 is reconstructed from the same active receipt and expiry. An old
champion flag alone no longer supplies wardrobe eligibility. No Elite PK
achievement ID was confirmed, so this producer grants no achievement completion.
See [local Elite PK](LOCAL_ELITE_PK.md) for the 78 event checks, rules, preserved
legacy state and pending live validation. The 48 CTF, 63 progression and 91
wardrobe regression checks also pass with this implementation.

## M4.1 producer follow-up

Demon Exterminators now records 11001 from the final ordinary-player NPC
transaction after four saved stages totaling 5,100 legitimate monster kills.
Jar consumption, five bound EXP Ball items and completion evidence commit
together. Neither a client counter nor possession/purchase of a reward supplies
completion evidence. See [quest rules and validation](DEMON_EXTERMINATORS.md)
for daily boundaries, team attribution, the explicit Revora reward delivery,
44 quest checks and remaining real-client gates.

## M4.2 producer follow-up

Moon Box records 11022 only when the correct saved Life ghost accepts the run's
Soul Jade and delivers the Moon Box. The three-Meteor ending completes the run
without that achievement, matching the installed client predicate. Six tracked, earned
tactic tokens must first be exchanged for that jade. Refusal, surrender and
possession of unrelated materials or a purchased box never complete the quest.
See [Moon Box rules and validation](MOON_BOX.md) for 31 quest checks, 324
regressions, persistence, explicit Revora choices and remaining client gates.

## M4.3 producer follow-up

Power Arena records 11013 from the seventh-floor Agate transaction after the
owned seven-floor progression. It records 11014 when committed Agate badge
rewards reach twelve across sessions. Client counters, purchased badges and
pack exchanges never contribute earning evidence. See [Power Arena](POWER_ARENA.md)
for 95 ordinary-player Arena checks, 355 regressions, saved defeat recovery,
atomic pack payment/opening and remaining client gates.

## M4.4a producer follow-up

Release the Souls records 11012 only when three distinct, saved soldier
inspections authorize the guardian's final reward transaction. Paid assistance
completes the daily mission without that achievement. Native mission 6049 and
legacy display fields are projections of the same record; neither can authorize
a claim. See [Release the Souls](RELEASE_THE_SOULS.md) for ordinary-player
checks, the installed three-soul edition, persistence and remaining client gates.
