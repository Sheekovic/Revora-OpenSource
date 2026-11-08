# Praying Stone earning transaction

Baseline `53d053c`. M6.36 completes the selected Praying Stone consumption and
mentor blessing earning path for ordinary authenticated players. Code and
fixtures are complete; native 6711 acceptance and deployment remain pending.

## Rules and evidence

The [official Heaven Blessing guide](https://co.99.com/guide/guides/heavenblessing.shtml)
confirms right-click use and durations of 3, 7 and 30 days. Existing Revora item
IDs 1200000, 1200001 and 1200002 map to those sizes. The
[official mentor guide](https://co.99.com/guide/guides/mentors.shtml) identifies
Praying Stones as the blessing contribution source. Generic welcome, festival,
quest and direct duration grants therefore no longer create mentor credit.
Claiming a mentor reward also cannot recursively credit another mentor.

The original exact blessing share is not established by those guides. Revora's
existing whole-hour calculation remains explicit policy: floor(source seconds /
36,000), yielding 7, 16 and 72 mentor hours for the three sizes. Both bound and
unbound stones retain their existing eligibility. No undocumented binding
exclusion or retroactive credit is invented.

## Transaction and replay boundary

The existing 1009 action 4 path intercepts only a currently owned Praying Stone.
It validates a complete 32?128 byte sealed 1009 frame before delivery. The native
112-byte fixture includes a nonzero timestamp at offset 22; action validation
does not mistake timestamp bytes for the action. The internal UseItem entry
uses the same service. Other item families keep their existing dispatch.

A successful use debits one unit of the exact selected saved item, adds the
entire duration, credits its saved mentor membership, and records a receipt in
one transaction. Inventory, blessing and contribution views update after commit.
A full duration field preserves the whole item. Locked, suspicious, expired,
wardrobe, foreign, stale-stack or ambiguously mentored items cannot be consumed.
Save-preimage checks prevent overwriting a newer duration.

A consumed item UID cannot be reused. For stacks, a bounded per-session history
suppresses repeated use of the same UID for one second; distinct selected stones
can still be used in succession. The inherited frame has no proven per-click
nonce, so later identical stack requests cannot be distinguished from intentional
new uses. This is bounded rapid-repeat protection, not indefinite network replay
identity. Receipts preserve each committed stack preimage and delivered amounts.

## Validation

50 targeted checks pass through the native item-use handler or its existing
internal entry: all three durations and both binding states, ordinary ownership,
selected stack debit, rapid and concurrent use, offline mentor reload/claim,
generic-source exclusion, concurrent contribution/claim ordering, wide lifetime
history, malformed frames and item guards. Fault injection covers item debit,
mentor credit, duration grant, receipt insert and silently skipped debit/receipt;
each failure rolls back all four obligations and succeeds after reload.

1167 related checks pass across mentor claims/admission/departure, EXP/books,
jades, mail, refinery, quest packets, Power Arena, progression, titles and auctions.
Together with 16 producer/overlay/silver audits, this batch has **1233 checks**.
The preceding M6.35 complete selected regression run passed 2,601 checks.
Final isolated `Revora.exe` SHA-256:
`B263D7D1FC1A659CCBE532440B6B7F9BE433B15F157ED78664D796AD89909519`.
Logs: ignored `obj/chi-refill-validation/praying-stones-final.log` and
`praying-stones-regression-*.log`. All writes used disposable fixture schemas.

No live schema, server binary, client content or public patch was changed.
Dropped +1 equipment contribution still needs a proven monster-origin pickup
transaction. Historical duplicate memberships, mentor BP, native acceptance and
the broader missing-achievement roadmap remain open. Producer coverage remains
101/337; this earning prerequisite does not fabricate another achievement.

M6.37 subsequently restores the missing common wilderness +1 equipment source
and its pickup/contribution transaction; see [monster equipment earning](MONSTER_EQUIPMENT_EARNING.md).
The other native, historical-state and roadmap gates above remain open.

## Legacy medium stone follow-up (2026-09-16)

M6.56b2b2c adds exact legacy ID 720578 to the same owned service. The loaded
server and active 6711 catalogs describe both 720578 and 1200001 as seven-day
PrayingStone(M). The client catalog was decoded read-only with an exact
encryption round trip; SHA-256
`D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.
This resolves the item use identity, not proof that a historical Magnolia draw
selected this particular alias. Magnolia bonus issuance remains pending.

The selected legacy stone now consumes one exact unit, grants 604,800 seconds
and credits 16 mentor hours under the existing explicit Revora share policy.
Both bindings work. Historical zero-stack singletons are consumed by checked
deletion; positive stacks decrement once. A permanent item with an inconsistent
remaining-time field is rejected, and canonical session eligibility is rechecked
before commit. Stack client updates restore the normal item mode afterward.

Startup migrates the four existing receipt constraints transactionally to admit
720578 and zero-stack sources while preserving all old rows and requiring the
correct duration/share for every accepted identity. A failed migration rolls
back its constraint changes. Receipt data must be retained during rollback;
reverting to the older schema after new uses is not a safe rollback procedure.

The expanded Praying Stone suite passes 98 checks, including 48 new checks for
populated old-schema migration and repeat initialization, both bindings with
zero/single/stacked medium stones, concurrent autosave/use, production character
and inventory reload, remaining stack use, five suppressed transaction writes,
bounded malformed frames and strict migrated constraints. Mentor blessing
regressions pass 39 and Magnolia passes 552: 689 gameplay checks, plus three
achievement audits. Current producer coverage is 219/337 (118 absent), not
per-character achievement proof.

Isolated Debug x86 build passed. Server SHA-256:
`D25484D6E19B2E43DEB7CC2C153A623167A45B24C6A8600505C58EC59D324B38`.
Harness SHA-256:
`EEDD9DE4F5B7AD306B3A26AE95ED5EA1E02F92F991759BC1F9869B2C69CB697C`.
Ignored logs: `obj/chi-refill-validation/medium-stone-third.log`,
`medium-stone-mentor.log`, `medium-stone-magnolia.log`.
Only the disposable validation schema was changed. Genuine 6711 display/use
acceptance, deployment and the remaining Magnolia reward chains stay open.
