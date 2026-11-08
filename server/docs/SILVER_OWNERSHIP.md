# Silver balance ownership prerequisite

Baseline `9698594`, 2026-09-15. M6.26 supplies the balance-ownership prerequisite
for transactional weapon-book purchases. The previous silver property setter
was unlocked. Even adding a lock to that setter alone would leave each compound
assignment's earlier read outside the purchase/save lock.

## Changes

`Entity.AdjustMoney(decimal)` now calculates and applies the entire signed
delta under the owning character's `ItemSyncRoot`. It rejects fractional,
negative-result and overflowing-result balances without changing the purse.
The supported range is zero through `Entity.MaximumSilver` (`Int64.MaxValue`,
9,223,372,036,854,775,807), matching the existing database column.
Fenced characters cannot mutate silver until recovery. Absolute assignments
also use that owner lock; normal login still loads into a new entity.

A production-save boundary test exposed the old mismatch: the property could
hold `UInt64.MaxValue`, but that value cannot fit in `entities.money`. Read-only
schema inspection confirmed that both `money` and `moneysave` are signed
`bigint`. The new maximum round-trips through the production character saver
and database reader. Absolute assignments above it are rejected before mutation.
Mail, trade, daily-boss and local/couple-PK reward guards now defer rewards
that exceed this durable range. Gold pickup leaves such a drop on the ground.
Vote credit retains its existing saturation policy at the corrected maximum.
No database schema is changed.

The ignored Revora x64 inspector at
`obj/chi-refill-validation/SilverInspect.exe` ran in a read-only transaction,
emitting only column names/types. SHA-256:
`A84CBB84E146FF4A1DA46626A1A5F0B067D34ACDCF9FF7D9013426948EFC6F28`.

The public balance getter and its internal field write use atomic 64-bit
volatile access, so the x86 server can read a complete balance without taking
another player's owner lock merely to inspect it. Notifications retain the
existing silver update packet after the mutation. No packet layout is changed.

The migration replaces 233 compiled compound assignments in 41 source files.
It covers NPC charges, monster rewards, event rewards, item handlers, ground
pickup, bank operations and the existing auction/booth/poker callers. Each
call keeps its existing amount expression and invokes the owned delta API.
Poker's all-in read/zero pair now uses an atomic `TakeAllMoney` operation.
The vote gold calculation preserves its existing saturation policy while
holding the owner lock through calculation, projection and its explicit save.
`EntityTable.UpdateMoney(GameState)` now locks through its balance read and
database write, matching the existing full-character-save ownership.

A source guard scans the actual project compile list for new compound silver
assignments, ignoring comments, string literals and uncompiled research files.
It is a regression guard, not a general semantic proof. Remaining absolute
entity assignments were inspected: production login, privileged operator/GM
sets and post-commit projections in mail, trade, local PK and daily bosses.
Those projections already run under their owner locks. Generated database
records, trade-offer fields, UI controls and enum constants are separate data.

## Validation

The isolated x86 build passes. Focused tests cover concurrent credits and
deductions, serialization with full and explicit balance saves, a writer blocked
by the owner lock, invalid deltas, the 64-bit bounds, racing all-in withdrawals,
fencing/recovery and concurrent x86 reads. Eighteen new ownership/range checks
bring the EXP/skill/book/silver fixture to 367 passing checks. A complete
28-mode event regression passed 1,782 checks before the final storage-range
correction. After that correction, targeted mail (33), local PK (60), couple
PK (43), Summer trade (17) and Potency/boss/event (211) checks all pass: 364
targeted checks, including five new full-purse deferral cases.

The final revision also passes 25 gold-pickup, 33 guard and 669 combat checks,
plus the silver source guard, three producer-audit and twelve overlay checks.
That is 1,474 checks on the final revision, in addition to the earlier broad
event sweep. No native client or live deployment acceptance is claimed.

Validated assembly SHA-256:
`0B1D8BDD07720BF62C913E2EB59BB9928910C5E7A29B6E85427CE726AB599B2D`.

## Remaining persistence and purchase work

This does not make every caller a durable payment/reward transaction. Existing
services that grant an item, alter another wallet, write an award marker or
transfer property separately from changing silver still need their complete
transaction restored. In particular:

- [M6.27](WEAPON_BOOK_SHOP.md) now commits weapon-book quantities, wide price
  multiplication, item delivery, silver deduction and a durable duplicate-click
  window. Native shop acceptance and arbitrarily delayed replay identity remain
  unverified.
- Ordinary silver deltas retain the existing save policy; they do not each
  create a durable receipt. A crash can still lose unsaved rewards.
- [M6.28](SILVER_PERSISTENCE.md) now replaces the offline credit with an atomic
  wide increment, coordinates online credits, and tracks a separate durable
  silver preimage through login, full saves and integrated reward transactions.
  Conflicting snapshots require reload. [M6.30](AUCTION_INTEGRATION.md) later
  integrates auction settlement and delivery; native acceptance, creation-replay
  limits and legacy recovery remain documented gates.
- Existing amount expressions are not all wide arithmetic. The delta API
  prevents balance-result wrap; it cannot reconstruct an amount that already
  overflowed before the method was called.
- Underflow/overflow now raises an exception before changing the balance.
  Legacy callers with earlier side effects still need transactional recovery;
  their partial-write behavior is not certified by the balance tests.

No live server, client files, public patch or website is changed by this batch.
