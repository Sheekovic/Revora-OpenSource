# EXP profile, class skill and Arena transactions

2026-09-15. Baseline `eacc7f1`. Ordinary loaded-player level-ups now persist their
complete base progression, newly learned class skill, initial Arena admission
and any consumed reincarnation snapshot together. Mentor contributions and the
issued Dark Horn exchange remain open.

## Transaction boundary

`ExperienceRewards.Commit` writes the calculated thirteen-field profile through
the loaded-preimage checks, persists required skill/Arena changes and consumes
the exact reincarnation snapshot in one PostgreSQL transaction. Cache/packet
projection follows commit under the character owner lock. Database failure rolls
back every participating row and fences the session. Projection failure retains
the durable result and fences stale cache for reload.

The transaction is used for level transitions on ordinarily loaded profiles,
without GM or named-account gates. Sub-level EXP continues through the existing
owner-locked save lifecycle. Synthetic/untracked actors retain the legacy path;
new consumable services must require the loaded progression image. The five
existing class-skill identities are preserved: Trojan 1110, Warrior 1020, Archer
8002, Ninja 6011 and Monk 10490.

An already learned, unsaved skill is inserted with its actual level, previous
level, EXP and soul fields. A durable existing skill is checked for exactly one
owned row and retained. Ordinary pending spell-training EXP is not silently
replaced by a new level-zero skill. The skill becomes `Available` only after the
transaction commits.

The lower-level `ExperienceRewards.Persist` and `ReincarnationRecovery.Consume`
methods accept the caller's connection/transaction. A future Dark Horn receipt
and material consumption can therefore share the same transaction with its
profile and effects. Tests deliberately fail an outer receipt after profile,
skill and Arena writes and verify that all three roll back. This is a composition
fixture, not a completed Dark Horn service.

## Skill save ownership and rune projection

`SaveSpells` now saves the ordinary skill batch transactionally under the owner
lock and character-row lock. It preserves `PreviousLevel`, requires exactly one
affected row per skill, and marks new skills durable after commit. A failed second
insert rolls back the first; the old implementation marked each skill saved
before its insert and then continued after exceptions. Failures now use redacted
logging and fence the session.

Common add/remove/train paths, the first/second rebirth routine, reincarnation
constructors and rune projection share the character owner lock. Individual skill
removal deletes its durable row before removing the cached skill. Failed deletion
keeps both. Full skill deletion and inserts/updates use parameterized operations.
These checks prevent duplicate/missing rows and in-process save/removal races;
they are not a complete field-by-field CAS scheme for external skill writers.

Rune abilities derive from equipped rune items. The ordinary skill saver skips
the existing rune-skill identity set, and the loader ignores historical rune
rows. `GameState.LoadData` already refreshes rune abilities after item and ordinary
skill loading. The existing projection now uses the owner lock. A stale skill
row cannot increase an equipped rune's level or restore an unequipped rune's
ability. Historical rows are retained; no production data cleanup is performed.

## Arena behavior and recovery

The [official Qualifier guide](https://co.99.com/guide/quests/arena.shtml), read
2026-09-15, says initial points depend on level and points reset at daily
settlement. It does not prescribe resetting points on every level-up. The
unconditional assignment of 1,000 points in `Entity.Level` is removed, so earned
Arena points survive ordinary and restored level changes.

Initial level-70 admission uses the existing `ArenaPointFill(70)` value of 1,000.
If login already created a zero-point row below level 70, the first non-reborn
level-70 transition fills that row without changing honors or wins; both point
mirrors now update together as described below. Retraining after rebirth cannot reclaim this initial award. The saved
Arena level may lag the cached level, so the conditional update accepts an
existing below-70 zero-point row rather than assuming every prior level was
already saved to Arena storage.

Online Arena saves take the same owner lock before opening their own connection
and recheck the canonical statistics object. An older cache object cannot replace
the active player's balance. If initial admission committed before a projection
failure, the character loader now reads the existing Arena row when the global
cache is empty; it does not blindly insert another identity. The recovery reader
retains the existing daily rollover policy and rejects duplicate identities.

## Validation and storage evidence

The isolated Debug/x86 build passes, with **197 focused progression checks**.
Added cases cover all five durable class skills and production reload/save,
existing pending skills, first Arena admission, the login-created zero-point
row, preserved honors/Team Arena balance, no repeated initial points after
rebirth, concurrent Arena and skill saves, detached statistics, failed batch
inserts/deletion, duplicate skill prevention, stale rune rows, outer receipt
rollback and post-commit projection recovery through production readers.

All 27 quest/event modes pass: **1,549 regression checks** on the same production
assembly. The last two focused checks expand rune reload coverage without a
production code change. Logs are under
`obj/chi-refill-validation/experience-effects-focused.log` and
`experience-effects-regression-*.log`.
Twelve private-overlay checks and three producer-audit checks also pass. Producer
coverage remains 81/337 identities; this prerequisite adds no achievement source.

Assembly SHA-256:
`88DC10BFD0CDABBC27F4E2BD9D087585D8BF73221156CFB483FDFB3CA0B52C88`.

A bounded read-only inspection found 65 skill rows and six Arena rows in the
configured database, with no null identities or duplicate skill/Arena identity
keys. This does not certify other deployments. Ignored x64 inspection helper
SHA-256: `8AE44BCB03B28F08E93FC1374CC469F6402702BCFA3A91CF5C610F41D5C3ED4C`.
It loads protected configuration without printing credentials. The fixture copies
production column types into isolated tables, without production rows or sequence
defaults.

## Remaining work

### Reported rune skill collisions (2026-09-20)

The owner supplied live `SaveSpells ... skills_pkey` errors for skill IDs
40305, 40310, 40304, 40309, 40303 and 40302 after a successful login.
Current source already excludes these item-derived abilities in both
`LoadSpells` and `SaveSpells`; it uses a transactional `skill-save` failure
path rather than the reported per-skill logger. The log therefore describes
the older live save path, not the current source implementation. No new
production change or data deletion is warranted from those messages alone.

`VerifyReportedRuneSkillCollisions` now reproduces all six historical rows
alongside unsaved derived abilities and a genuine learned skill in the isolated
database. Repeated saves retain exactly seven rows, preserve the historical
values and persist the ordinary skill's level, previous level and EXP. Reload
plus the production rune projection does not grant any of the six unequipped
abilities. A post-reload save remains healthy. All four added assertions pass
within **456 Experience checks**, run with
`tests/PotencyHunt/Verify-PotencyHunt.ps1 -ExperienceOnly`.
Local log: `obj/chi-refill-validation/reported-rune-collisions.log`.
These checks do not establish that the live executable has received the fix;
deployment and full native login acceptance remain pending.

### Outstanding acceptance and integrations

Mentor EXP credit and claims still use separate legacy writers. They must join
the direct reward transaction with source eligibility, ownership and replay
protection. Paid rebirth/reincarnation equipment, CP and item changes and other
custom reset writers still need their complete transaction review. The new
ordinary level transaction is not a claim that those services are finished.

Full login/reconnect, native skill/EXP/Arena display and deployment acceptance
remain open. Recovery tests call production readers; they do not simulate every
login dependency. Dark Horn earning/consumption, its receipt and client text are
still not enabled as a completed feature. No live binaries, installed client
files or production character rows changed.

## Shared initial Arena points (2026-09-21)

The original level-70 path updated only solo points, leaving Team Arena's
shared-balance mirror zero. `ExperienceRewards.Persist` now includes the Team
Arena row in the same transaction as the profile, skill and solo enrollment.
A missing uncached Team row is created; an existing owned zero-point row is
updated. Existing Honor and result counters are preserved. Conflicting nonzero
points, wrong ownership, duplicate/missing rows and suppressed writes reject the
transaction instead of replacing an earned balance.

Only after commit, both canonical statistic objects receive the new points and
send their existing statistic packets. A projection failure invalidates the
Arena caches before fencing the session for reload. This does not rewrite any
packet layout. The earlier description/tests that preserved unrelated Team
points through initial enrollment are superseded by this shared-point rule;
a conflict is retained for recovery, not overwritten.

Build and 495 focused Experience checks pass: new/existing Team rows, preserved
Honor/counters, production-reader reload, stale/nonzero balance rejection,
suppressed insert/update rollback and retry, both concurrent Arena saves,
rebirth non-regrant and rollback with an outer reward receipt. The previous
1,116-check Arena run is separate evidence and was not rerun for this change.
Logs: `obj/chi-refill-validation/initial-arena-mirror-build.log` and
`obj/chi-refill-validation/initial-arena-mirror-tests.log`.
The build includes the owner's separate `System/IniFile.cs` change. No live
release or native-client validation occurred, and this does not repair existing
historical mirror conflicts automatically.
