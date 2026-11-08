# Dis City jades and durable Chi refills

2026-09-15, implementation baseline `10bebad`. This completes jade use as a
reward prerequisite. It does not yet provide the four Dis City stage earning
paths or the newer Labyrinth's Yang Jade reward source.

## Evidence and use policy

The [official Dis City guide](https://co.99.com/guide/quests/monstercity.shtml)
and installed item descriptions associate Yin Jade 3008059 and Yang Jade 3008060
with a 300-Chi single use and a 1,200-Chi paired use, eligible below 8,000 Chi.
The existing item-use action is retained; no new native packet layout is inferred.

Right-click either jade. If an owned, unlocked, unexpired opposite jade is in the
bag, consume one unit of each; otherwise consume the selected jade alone. Prefer
a counterpart of the same binding, then its lowest UID. Bound/unbound mixtures
are allowed because the resulting Chi is not transferable. Locked counterparts
are preserved. A full bag does not block use because no new item is inserted.

Revora's explicit boundary policy checks the balance before use and grants the
full reward. Thus 7,999 becomes 8,299 with one jade or 9,199 with a pair; 8,000
or more refuses use and preserves the items. Automatic counterpart selection and
the crossing-boundary behavior are implementation choices consistent with the
descriptions, not captured proof of the original client's exact interaction.

## Transaction and persistence

Ordinary, fully loaded authenticated players can use these items. The owner
inventory lock, current canonical session, loaded Chi revision, item references,
database owner/type/stack/binding/position/warehouse/lock preimages and eligibility
are checked. Chi credit, both item consumptions and a `revora_dis_jade_uses`
receipt commit together. Client inventory and the existing Chi update packet
change only afterward. Failures fence the session for reload and roll back the
whole transaction. A bounded one-second per-UID guard covers both halves of a
pair; a later deliberate use of remaining stack units is allowed. Reconnect
reloads durable consumption and balance; an already consumed item cannot replay.

Chi wallet reads, assignments, increments and save snapshots now share the owner
lock. Background saves compare the last loaded/committed balance before updating
the wallet and existing power blobs. A pending legacy credit or spend must save
before jade use or refill; it is not silently overwritten. External purse changes
refuse the stale write. Reload clears prior power lists and restores retreat
entries without assigning into an empty list.

The native Chi refill entry now commits CP deduction and Chi credit in one
transaction. A 0-to-4,000 refill remains 1,000 CPs by default, with partial prices
rounded up proportionally. Exact Chi and CP preimages prevent overwriting pending
changes. Duplicate requests at a full balance do not charge again. A failed Chi
write rolls back the CP deduction; the live balances update only after commit.

This batch does not certify the source-level transactions of older Chi pills,
NPC exchanges, random study or retreat purchases. Their numerical wallet changes
use the shared lock, but their original material/payment/progression operations
still need their own atomicity audits. No new achievement is granted for using
jades or purchasing a refill.

## Validation

The isolated build and `Verify-PotencyHunt.ps1 -JadesOnly` pass 57 checks: ordinary
eligibility, both single jades, paired stacks, competing requests, full bags,
binding, locks, expiry, foreign/stale item ownership, receipt failures, production
Chi reload, pending/external balances, concurrent saves, retained retreat powers,
refill prices, insufficient funds, duplicate charges and rollback after CP debit.
The production Chi packet writer is checked for the committed balance.

All 1,351 previous gameplay checks passed before the final transactional refill
change. The final build then passed the 57 jade/refill checks plus 74 cultivation
and 32 mail checks. The separate pricing script checks ten production price
calculations, including maximum unsigned arithmetic. Five producer-audit tests
pass; the producer count remains 77/337.

Staged assembly SHA-256:
`D0943B40DBAC7F37C2B7205B5D56090900B1A52E72A1DE8E6E43BA1B2148E8AE`.
A read-only production audit found two Chi rows, no out-of-range wallets and no
saved retreat blobs; the configured points default is zero. Retreat restoration
is therefore covered by disposable fixtures, not a production-data example.
No production player data was changed. Native 6711 presentation and live
deployment remain separate pending gates.
