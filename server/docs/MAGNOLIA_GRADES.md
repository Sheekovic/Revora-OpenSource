# Magnolia grades and paid seed changes

Date: 2026-09-16. M6.56b2b1, ordinary-player server route; not deployed.

The Garden Keeper now supplies a free Normal seed or a Super seed for 100 CPs.
An ordinary level-80+ gardener may bring one eligible seed back, review a
1-CP random change or a 100-CP guaranteed Super replacement, and plant the
result at either existing flower bed. All five grades now have distinct EXP
and Study Point rewards. Existing custom CP/RVP rewards and the Chi Token
remain in the same complete daily transaction.

## Evidence and edition

The [Magnolia guide](https://co.99.com/guide/quest/2012/Magnolias_All_Around.shtml)
confirms the five grades, 1-CP seed change, 100-CP Super choice and these values:

| Grade | Item | EXP minutes | Study Points |
| --- | --- | --- | --- |
| Normal | 729306 | 60 | 10 |
| Refined | 729307 | 90 | 20 |
| Unique | 729308 | 120 | 50 |
| Elite | 729309 | 180 | 100 |
| Super | 729310 | 300 | 300 |

The [official Price Hunt guide](https://co.99.com/guide/quests/2012/Everything_Has_A_Price.shtml)
explicitly equates 60 minutes with one EXP Ball. Rewards use Revora's existing
level-dependent ball value multiplied by minutes/60, with overflow-safe integer
scaling and ordinary progression/recovery. This verifies the unit conversion,
not historical parity of the underlying Revora ball formula. At the level cap,
new EXP is zero while the other rewards remain available.

The old guide's Study Point payment is superseded by the
[September 9, 2016 removal](https://co.99.com/news/2016-09-09/studypoints.shtml).
[Patch 6711 released May 3, 2018](https://co.99.com/news/2018-05-03/patch_6711__supreme_bound_roulettes_event_and_bug_fix_.shtml).
Therefore this service offers CP payments only; it does not restore the removed
Study Point exchange. Confidence is high for these dated official statements.

All five IDs exist in the loaded server `itemtype-new.txt` and in the active
6711 `ini/itemtype.dat`, decoded read-only with the existing Revora transform
and verified by an exact encrypted round trip. Client SHA-256:
`D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.
No client files were changed or packaged.

## Explicit Revora rules

The official pages do not disclose the lottery distribution. Revora uses
Normal/Refined/Unique/Elite/Super weights of 50/30/15/4/1 out of 100. A change
may produce the same or a lower grade. The review states these odds and the
price before payment. The 1% Super outcome makes the expected random-change
cost 100 CPs, matching the guaranteed option, while lower grades remain useful.
These are deliberate Revora balancing rules, not inferred original odds.
The free Normal starting seed is also an explicit policy; the public guide
does not specify the initial selection algorithm. Already-Super seeds cannot
be charged for another change or downgraded.

Grade-specific extra gifts named in the guide are not implemented by this
batch. Their exact identities, binding, complete usable earning routes and
probability/selection rules remain M6.56b2b2. Do not mark the whole five-grade
quest or original prize parity complete from this milestone.

## Ownership and persistence

`revora_magnolia_runs` gains rule version, frozen EXP minutes, current seed UID
and change counter. Existing rows default to version zero/60 minutes and retain
their saved study and other rewards, including historical fee receipts. Fresh
issuance uses version one and grade terms. Existing seeds without a current
record are adopted once into today's daily contract when changed or planted.
A carried seed can complete the current day; future-dated records still block
backward-clock duplicates. Version-one planting requires the recorded current
seed identity and type, preventing substitution of another grade.

A two-minute change review is bound to the ordinary canonical player, current
Pacific day, seed UID/type and displayed guaranteed price. Confirmation consumes
that offer. It requires the physical keeper, no trade/booth, a living loaded
character, sufficient CPs and a single unlocked permanent seed. Legacy zero
stack singletons remain usable. Multiple or stacked seeds must be separated.
The complete bag UID set must match durable ownership.

Under the owner lock and one database transaction, the service locks the
character, debits the exact seed preimage, charges the checked CP balance,
creates a fresh seed preserving binding, changes the daily terms and records
one `revora_magnolia_changes` receipt. The daily counter and source UID guard
its update. Every required write checks its affected row count. Failed source,
payment, replacement, terms, receipt or random draws roll back together.
Randomness is cryptographically generated without modulo bias and drawn only
after eligibility and debit checks. A full bag works because one owned seed
releases exactly the needed slot. After commit, item and CP updates use the
existing client projection; uncertain failures fence the session for reload.
A fresh review is required before another intentional paid change.

The shared planting transaction keeps full progression, skill, Arena, mentor,
rebirth, point-balance and Chi Token protections. Its inventory comparison is
now UID-exact. The Garden Keeper 2032 path checks body 20..120, exact trailer and
bounded input length, plus physical proximity and any nonzero echoed NPC UID.
Native layout remains source/fixture-confirmed, not newly client-captured.

## Validation and remaining gates

Focused Magnolia validation passes 552 checks, including prerequisite fixtures.
Every grade is earned through the ordinary CP change, walked to a real bed,
planted and production-reloaded both bound and unbound. All 100 lottery slots
match the disclosed distribution. Tests cover concurrent payment/autosave,
replay/reconnect, direct Super, old promised terms, full capacity, carried seeds,
all required suppressed writes, malformed RNG, ineligible sessions/items,
stale durable balances/inventory, expiry/day rollback, malformed continuations,
substituted grades and frozen displayed fees. All checks use the disposable
validation schema; no live player data is mutated.

Final validation: isolated Debug x86 build; Magnolia 552, travel 105 and
Elite claims 575 (1,232 gameplay checks), plus three achievement audits.
The producer inventory remains 219 present / 118 absent, not per-player proof.
Ignored logs: `obj/chi-refill-validation/magnolia-grades-first.log`,
`magnolia-grades-travel.log` and `magnolia-grades-elite-regression.log`.
Server SHA-256: `6AAB1665C69864A0D0BE0C66C75DEA1AD47689F55EEBC9C943550C4E8F66E4C6`.
Harness SHA-256: `F4157C48F5FEA5796FCFB7064C40E836B92092B4973BD203748CB39B0E1E57E9`.
Genuine 6711 dialogue, effects, immediate item/EXP/study display and reconnect
acceptance remain open. No live deployment, restart, patch or website change
is included. Rollback must preserve daily/change receipts to prevent replay.

2026-09-20 follow-up: [Magnolia bonuses](MAGNOLIA_BONUSES.md) implement the owner-selected 20% bonus chance with 38 catalog-confirmed entries for new version-two contracts; version one retains 37. Three mappings, remaining downstream use chains and native acceptance remain open; this is not complete historical parity.
