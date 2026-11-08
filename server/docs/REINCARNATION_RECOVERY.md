# Durable reincarnation recovery

The ordinary paid reset has since been replaced by the owned transaction in
[Paid reincarnation](PAID_REINCARNATION.md). That implementation admits at 110
and uses a typed receipt to recover same-level EXP exactly. The historical
checkpoint below describes the legacy compatibility behavior, not the current
paid service's admission or complete transaction.

2026-09-15. Baseline `7e82b6b`. This closes the saved-progression restoration
defect required by direct EXP rewards. It does not complete the paid reincarnation
service, its historical class-skill combinations or Dark Horn delivery.

## Evidence and policy

The [official reincarnation guide](https://co.99.com/guide/event/2013/rebirth/rebirth-3-1.shtml)
states that the highest pre-reincarnation level returns at level 110 and that
reincarnation itself adds no bonus attribute points or Battle Power. It describes
second-reborn level-110 admission with Oblivion Dew. Source read 2026-09-15;
confidence is high for these published rules.

The active Revora packet service instead admits at level 120 and charges the
configured CP fee plus Oblivion Dew. This checkpoint preserves those existing
admission/price settings. Its request now checks the constructor's minimum before
changing the body or charging, fixing a case where the constructor returned
without resetting but its caller still charged. Matching the complete official
admission, skill and payment contract remains separate work.

Revora already modeled saved reincarnation EXP as `ulong` in memory, but wrote
zero to the database and read it back through `uint`. New snapshots now write
the actual EXP, and the loader reads and validates signed-bigint values without
truncation. Previously stored zeros do not contain recoverable historical EXP;
this change does not invent compensation for those rows.

The restored-level point adjustment is the number of recovered levels multiplied
by the configured normal points per level. The level-110 transition earns its
ordinary points once. Recovery grants no separate reincarnation bonus or mentor
EXP for skipped levels. This is an explicit Revora recovery policy consistent
with its configured level awards, not certification of every old reset formula.
The old `((restored - current) * 3) - 3` expression could lose points or wrap.

Saved EXP is combined with unspent EXP from the award that reaches level 110,
then evaluated against the restored level's curve. This carry policy preserves
the values that the existing in-memory snapshot and ordinary award represented;
the official page does not specify that exact arithmetic. A configured cap is
respected. Already capped characters retain their current EXP under the existing
no-new-awards policy.

An old pending record found when the character is already at or above its saved
level is retired without lowering the character or adding its EXP a second time.
The legacy table has no reset-phase marker; this avoids replaying a snapshot left
by an interrupted reset or earlier partial recovery. Ambiguous historical records
are not represented as fully reconstructable.

## Transaction and ownership

`ExperienceRewardPlan` stops a pending reincarnation award at level 110, applies
the snapshot, then plans any remaining ordinary level gains. Both legacy `Hash`
entry points call the same production EXP path. The caller no longer writes the
old remainder after restoration.

For a recovery, one transaction conditionally updates the complete loaded
thirteen-field progression profile and deletes the exact `(uid, level, EXP)`
snapshot. Both affected-row checks must succeed. A stale profile, changed
snapshot, duplicate row or failed deletion rolls everything back. Failure fences
the session before class-skill/cache projection. After commit, the pending cache
entry is removed and the committed profile is projected without another level
write. A projection failure fences stale in-memory state while preserving the
database result for reload.

New snapshot capture locks the character row, checks for an existing snapshot
and inserts parameterized values. The three legacy reset constructors and the
packet request share the character owner lock with ordinary awards and saves.
The pending snapshot dictionary is concurrent across different characters.
Loading rejects invalid/null values, duplicate owners and conflicting cached
records. The obsolete unconditional snapshot-delete API has been removed.

Packet 1066 requires complete declared fields, type and transport suffix before
any side effect, with canonical-player and trade/booth checks repeated under the
owner lock. An ineligible repeat request keeps its pending record. A ready record
can be restored through the shared recovery path before a new eligible request.
The source-level packet evidence is in [PROTOCOL_RESEARCH.md](PROTOCOL_RESEARCH.md).

## Storage inspection

A bounded read-only inspection of the configured database found `reincarnation`
columns `id`, `uid`, `level` and `experience`, all PostgreSQL `bigint`. It currently
contains zero snapshots. This is evidence about that configured database only.
No schema migration or production row update was performed.

Ignored inspection helper: `obj/chi-refill-validation/bin/ClassicInspect.exe`,
x64, SHA-256 `409084759EA5458E245A85FA800F7E1EC74A7106164062EB782CBDE73F1CCD27`.
It loads protected configuration without printing credentials and uses a read-only
transaction. Fixture tables contain only synthetic ordinary-player identities.

## Validation and remaining work

The isolated Debug/x86 build and **167 focused progression checks** pass. New
coverage includes high-bit snapshot persistence/loading, repeated capture,
negative/duplicate records, threshold behavior, saved level 110, both legacy
entry points, large-award carry, cap arithmetic, stale higher-level characters,
point/EXP overflow, delete failure, changed profile/snapshot preimages, retry
after rollback and 100 concurrent awards racing normal saves. Post-commit failure
keeps durable recovery and prevents the stale cache from earning further EXP.
Malformed frames, detached sessions, trade, pending requests and below-threshold
payment attempts are also checked.

Assembly SHA-256:
`42169C242AECA1BACD953C24A69DBA2248372210FE27708F139FAFE4BC9A9F98`.
Focused log: `obj/chi-refill-validation/reincarnation-focused.log`.
All 27 quest/event fixture modes pass sequentially: **1,549 regression checks**.
Logs: `obj/chi-refill-validation/reincarnation-regression-*.log`. Twelve private
overlay checks and three producer-audit checks also pass. Producer coverage stays
81/337 identities; this recovery prerequisite adds no achievement producer.

The subsequent [level-up transaction checkpoint](EXPERIENCE_REWARD_TRANSACTIONS.md)
also commits class skills and initial Arena admission with the profile and
snapshot. Mentor credit still needs transaction integration. Paid reset still
needs atomic item/CP/equipment/skill changes,
validated class/body choices and the official admission review. Other first/second
rebirth and custom level-reset writers need coordination. Full login/reconnect
and genuine-client panel/EXP display acceptance are not covered by these synthetic
fixtures. Dark Horn must compose all required effects and its item-use receipt
in one recoverable reward transaction before it is enabled.

No live binaries, installed client files or production character rows changed.
