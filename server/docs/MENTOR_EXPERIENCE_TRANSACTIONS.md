# Mentor EXP contribution and claim checkpoint

Baseline: `b436353`, 2026-09-15. This is the M6.21 prerequisite for direct
Dis City Dark Horn EXP rewards. Server implementation and isolated fixtures;
not deployed and not accepted in the genuine 6711 mentor panel.

## Behavior

- An eligible loaded apprentice's level-up commits its full progression,
  class skill, initial Arena admission, reincarnation recovery and mentor EXP
  in one transaction. A missing or duplicate relationship aborts the award and
  fences the character for reload. An offline mentor retains database credit.
- Claims 1/2 of packet 2067 consume owned database contributions and commit
  the mentor's complete EXP reward together. Client balances and UID do not
  determine ownership or amount. Failed transactions retain the contributions.
- One claim consumes at most 30,300 legacy contribution units. Excess remains
  claimable; lifetime totals remain unchanged. Replayed/concurrent requests
  cannot spend the same credit twice. A new request can spend remaining credit;
  no native request nonce has been verified.
- Maximum level, death, cursed status, configured blocked training ground,
  incomplete loading and fenced sessions cannot spend unusable contributions.
- Relationship changes, contributions, claims and their cache projections use
  a shared mentor lock, acquired after the initiating character owner lock.
  Never acquire another character's owner lock while holding that mentor lock.
  Unrelated blessing/+1 saves no longer write EXP columns. Repeated relationship
  loads reset cached totals before rebuilding them.
- Team-share kill EXP and item-flagged bound EXP Balls use the source exclusion.
  A mentor prize cannot recursively generate upstream credit. Other direct
  quest and monster EXP retains contribution eligibility.

The [official mentor guide](https://co.99.com/guide/guides/mentors.shtml) confirms
monster/quest/online-training EXP sources and excludes team-share EXP and bound
EXP Balls. It does not establish the exact numeric conversion. Destination
level times two credit, the 30,300 cap and 600 units per EXP Ball equivalent are
retained Revora tuning, not a claim of verified official arithmetic. Restoration
steps do not fabricate credit for skipped levels.

## Integration boundary

`ExperienceRewards.Persist` requires the character owner and mentor locks plus
a caller-owned transaction. Its caller commits all reward/receipt writes, then
applies `effects.MentorCredit` while retaining the mentor lock. Normal
`ExperienceRewards.Commit` performs that sequence. The Dark Horn exchange must
use this same complete transaction and post-commit projection boundary.

Ordinary claims reconcile the displayed EXP prize from queried database rows,
including when a cached apprentice is absent. Existing cached lifetime figures
and online apprentice projections are updated after commit. Native packet
presentation and reconnect acceptance still require a real client test.

## Historical data findings and remaining work

A read-only metadata/count inspection of the configured local database found
591 apprentice rows and 49 apprentice identities occurring more than once.
There were zero rows with null mentor/apprentice identities or negative EXP.
These are aggregate observations of this database, not an audit of every
deployment. EXP columns are PostgreSQL bigint; legacy plus/blessing columns are
also bigint, while current cache fields are ushort.

Duplicate identities require reviewed remediation before deployment. Do not
sum, select the newest row, or delete rows without proving their relationship
and reward history. The new claim/credit transaction rejects ambiguity instead
of inventing a payable balance. The inspection prints only column metadata and
aggregate counts, not names, credentials or connection settings.

Mentor blessing and +1 stone claim transactions, their source eligibility,
relationship admission/removal fees and delays, BP sharing and the complete
ordinary EXP Ball item-use transaction remain separate unfinished work. This
checkpoint does not certify those services or all historical contribution data.
The legacy EXP Ball branch routes the item's binding flag; intrinsic item-ID
binding rules across historical inventory imports still need verification.

Inspection helper: Revora-owned ignored `ClassicInspect.cs`, framework x64,
read-only transaction, SHA-256
`5EAD5B330978729599084D11DD430DB08E7221703CADF29067759AD44DA7FAC1`.

## Validation

Isolated Debug/x86 build uses `obj/chi-refill-validation/` and leaves the running
server binary untouched. Fixtures use ordinary non-GM canonical sessions and
the isolated `revora_potency_validation` schema. They exercise contribution
rollback, claim rollback/recovery, the visible cap, stale unrelated saves,
production relationship loading, malformed requests, concurrent claims,
claim-versus-credit ordering, source exclusion and ambiguous historical rows.

The build passes; 213 EXP/progression/mentor checks, all 1,549 quest/event
regressions, three producer-audit tests and 12 overlay tests pass. Assembly
SHA-256: `91137AE88E04F59157E6D2DB41756C99ADB73DFA7527E4505E820C5775730987`.
Native mentor UI and deployment remain pending.

Follow-up (2026-09-16): the ordinary standard/power EXP Ball item-use transaction
is now completed in [M6.56a](EXPERIENCE_CONSUMABLES.md), including bound standard
ball exclusion and atomic mentor credit. The historical limits above describe
the earlier checkpoint; intrinsic binding rules still need catalog evidence.
