# Remaining Prestige cultivation prerequisites

Source review: 2026-09-26, following the earned 401,268-point character in
[Prestige earning](PRESTIGE_EARNING.md). This is a prerequisite audit, not an
additional passing earning test or native acceptance result.

The findings below describe commit `4d7e2271`. The subsequent
[Inner Power transaction implementation](INNER_POWER_PROGRESS.md) closes the
unlock/training persistence gaps. Both runes and the installed first Inner Power
gong require second rebirth; the continuing first-reborn character still needs
an earned rite before using either route.

## Rune admission

`Sistemas/Runas/RuneProgress.cs`, `CanUse` and `Eligible`, require second rebirth
and level 15. The continuing character is first-reborn. Buying or upgrading rune
items cannot make their Prestige eligible on that character. Do not bypass this
rule or replace the character with a supplied second-reborn profile.

The existing `tests/PotencyHunt/AbyssRebirthTests.cs` proves a separate character's
quest-funded second rite and reload. It is a candidate for joining the continuing
career, not evidence that this career already earned the rite. The rite resets
level to 15, so subsequent calculations must account for the resulting level,
equipment and cultivation eligibility rather than preserving a fictitious level
140 snapshot. The 600,000-point route remains unfinished.

## Inner Power progression must be made durable

Potency purchase recovery already has a database ledger in
`Game/Features/InnerPowerPurchases.cs`. That does not make the following packet
mutations transactional:

| Source | Finding | Consequence |
| --- | --- | --- |
| `Network/PacketHandler.cs:376`, action 4 | Sets the gong unlocked before removing its book; no check that this gong was already unlocked | A repeated unlock request can consume another owned book without new progression |
| `Sistemas/InnerPower/InnerPower.cs:263` | `isUnlockedNeiGong` checks the previous gong, or accepts gong 1 | This helper does not supply the missing duplicate-unlock guard |
| `Network/PacketHandler.cs:403`, action 3 | Spends potency and advances level in memory, then sends updates | No durable progression commit occurs in this action |
| `Database/InnerPowerTable.cs:144`, `Save` | Serializes progression to `database/InnerPower.txt` through file replacement | Atomic file replacement does not join the separate inventory database transaction |

These are source-confirmed ordering and guard gaps. No live crash or player loss
was reproduced. A normal later save/reload test alone would miss the interruption
between book removal and saving progression.

Next implementation batch: make unlock and training authoritative in a durable
per-character transaction, including item ownership/preimages and potency spend;
recover committed progression before publishing client updates. Preserve existing
pack and NPC potency checkpoints during migration, and reject repeat unlocks
without consuming a second book. Validate rollback at both item and progression
writes, concurrent requests, replay, reconnect and offline Prestige parity in an
isolated database. Do not claim complete earning until the actual book supplier
and eligible training route are included. No live schema migration is authorized
by this source review.
