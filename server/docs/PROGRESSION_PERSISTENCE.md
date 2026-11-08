# Progression persistence checkpoint

2026-09-15. Baseline `391b797`. This implements the persistence primitive needed
for Dark Horn; the actual consumable exchange is still incomplete.

## Saved state and conditional writes

`EntityTable.LoadEntity` now registers the exact saved progression image before
marking a character fully loaded. It contains 13 fields: level, EXP, four base
stats, free attribute points, class, rebirth count, both former classes and both
former levels. The image belongs to that specific character object and session.
Pending in-memory gains remain distinct from the last committed database values.

The normal owner-locked character save adds parameterized comparisons against
all 13 saved fields. It advances the saved image only after the update succeeds.
A changed database preimage prevents the write, fences the character and requires
reload instead of replacing newer progression. Existing immediate byte-field
writes use the same comparison and update their durable image while preserving
pending cached EXP/stat changes. This avoids making a later legitimate save fail
merely because its own level setter already wrote the new level.

Nullable legacy fields retain their actual null preimage. A successful save may
write the loader's existing zero default, but does not pretend null was already
zero when checking ownership of the saved state. The strict progression reader
rejects negative or out-of-range values; it does not silently parse them as zero.
The existing command builder gained typed, null-safe Int64 conditions. Other
legacy command construction is not claimed to have been converted wholesale.

`CharacterProgression.Persist` writes a complete profile inside a caller-owned
transaction, with the same expected-state checks. Item consumption and a reward
receipt can participate in that transaction. It does not advance the in-memory
saved image before commit. `ApplyCommitted` then installs the committed profile
under the same owner lock, using existing character updates while suppressing a
second database mutation from the level/class setters. Projection failures fence
the character, preserving the committed database result for reload.

The transaction primitive does not provide NPC eligibility, item ownership,
reward calculation, receipt identity or recovery policy by itself. Its caller
must supply those checks, commit before projection and keep the owner lock until
projection finishes. The ordinary load path enables tracking for ordinary
characters without GM or named-account gates. Legacy synthetic/unloaded actors
retain their prior save behavior; a consumable service must require a loaded image.

## Storage evidence

A read-only inspection of the configured database confirms `entities.experience`
is PostgreSQL `bigint`, not unsigned 64-bit storage. Transactional profiles enforce
the signed storage range even though the numeric EXP calculator uses `ulong`.
Normal high-level curve values fit. The configured database currently has two
character rows; both passed the range audit, with no negative EXP, out-of-range
byte/stat fields or nullable progression fields. This is a bounded check of that
configured database, not evidence about other deployments or archived databases.

The ignored x64 inspection helper reads protected configuration without printing
credentials and executes inside a read-only transaction. Latest executable:
`obj/chi-refill-validation/bin/ClassicInspect.exe`, SHA-256
`2D71984110FCBC71B2B44D5764634C059666367D33FF166E0CCBB6725A83157D`.

## Validation and limits

The isolated Debug/x86 build passes. The expanded EXP fixture passes **121 checks**.
New coverage exercises the production reader, pending cache gains, conditional
production saves, tracked immediate level/class/rebirth writes, all 13 external
field conflicts, legacy nulls and invalid signed EXP. Concurrent normal awards
and conditional saves preserve the complete final EXP balance.

A fixture receipt constraint fails after both profile persistence and item
deletion; transaction disposal rolls both back and retains the old loaded image.
A successful profile commit is projected while an UPDATE-rejecting database
trigger is active, proving projection needs no second database write. The next
production character save preserves that committed result.

That failure test uses a synthetic owned Meteor, not a Dark Horn earning route.
The successful projection test covers the profile transaction, not a completed
NPC exchange. Complete login/reconnect, new-character entry and native display
acceptance remain unverified for this checkpoint. Legacy reset writers and
multi-record level-up effects still need their full transaction review.

Assembly SHA-256:
`FADEA42235516E38369B7C511D9FDE11F3B5522DB4F8043DBD0B4192A4F32341`.
Direct log: `obj/chi-refill-validation/progression-store-final.log`.

All 27 existing quest/event fixture modes pass: **1,549 regression checks**.
Logs are under `obj/chi-refill-validation/progression-store-regression-*.log`.
Twelve private-overlay and three producer-audit tests also pass. Coverage remains
81/337 earning identities; this prerequisite adds no new achievement producer.

The subsequent [reward-plan checkpoint](EXPERIENCE_REWARD_PLAN.md) now computes
base stats, free points and skill/Arena/mentor EXP intentions before mutation.
It validates calculated-profile rollback and recovery, and removes unrelated
mentor blessing/stone grants from level-ups. Complete side-effect persistence
remains open.

The later [level-up transaction checkpoint](EXPERIENCE_REWARD_TRANSACTIONS.md)
now persists skills, initial Arena admission and reincarnation recovery with the
profile. Next: integrate mentor credit, coordinate remaining reset writers, then connect issued
Dark Horn consumption with its direct EXP reward, failed-write/replay protection
and reload recovery. No live binaries, installed client content or production rows were
changed by this source checkpoint.
