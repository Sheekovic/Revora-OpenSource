# Subclass progression and achievement restoration

2026-09-15, baseline `2263501`. Implementation validated in isolation;
genuine-client acceptance and deployment pending.

The seven subclass enrollment, training and promotion paths now use one owned
service. Enrollment consumes the installed required materials and inserts the
P1 subclass and achievement in one transaction. Learning an already-owned
subclass consumes nothing. Training pays the saved study-point price and advances
one level, capped at nine. Promotion advances one earned phase and enforces the
installed character-level and rebirth requirements. P9 and its achievement commit
together. Item stacks, balances, subclasses and achievement updates reach the
client only after their respective transaction commits.

## Evidence and selected rules

Client numeric interoperability evidence, all outside Git:

| File | SHA-256 |
| --- | --- |
| `ast_prof_inauguration.ini` | `0D5863A195340D238A3722B62C1A8ACF3DBF29D9F62892CD4C88AFB980DD3BC6` |
| `ast_prof_promote.ini` | `1DD2694FB8895012F65DBC907B5841D58512AD3E526B40D97B31A2BC68E382B6` |
| `SubProfessionInfo.ini` | `37F72BE058933365B7EED0E21DD7F16BA513E7CA4468552D14736131E4982297` |

The [official subclass guide](https://co.99.com/guide/quests/sub-class.shtml)
corroborates level-70 enrollment, required materials and phase-specific bonuses.
The installed numeric tables are the selected 6711 requirements. Existing
server study-price curves are retained; their historical price edition has not
been independently certified. Performer enrollment uses the installed single
711679 item, removing the legacy alternative that could consume two fees.

| Subclass | ID | Enrollment material/count | P1 / P9 achievements |
| --- | ---: | --- | --- |
| Martial Artist | 1 | 721259 / 5 | 10922 / 10923 |
| Warlock | 2 | 721261 / 10 | 11203 / 11204 |
| Chi Master | 3 | 711188 / 1 | 10924 / 10925 |
| Sage | 4 | 723087 / 20 | 10926 / 10927 |
| Apothecary | 5 | 1088001 / 10 | 10920 / 10921 |
| Performer | 6 | 711679 / 1 | 10928 / 10929 |
| Wrangler | 9 | 723903 / 40 | 11201 / 11202 |

| Promotion | Minimum character level | Minimum rebirths |
| --- | ---: | ---: |
| P1 | 70 | 0 |
| P2 / P3 / P4 | 90 / 110 / 120 | 0 |
| P5 / P6 / P7 | 90 / 110 / 120 | 1 |
| P8 / P9 | 120 / 130 | 2 |

Training level and promotion phase are distinct. Attribute calculation now uses
the valid earned phase, including the existing restricted-event cap. Previously
level-nine training could supply full bonuses while still P1. Performer also
added flat 1,000 attack to transient totals that the final calculation overwrote;
its bonus now enters the base totals at the installed phase amount, 100 through
800 for P1-P8 and 1,000 for P9. Wrangler adds 100 HP at P1 and 1,200 at P9.
Invalid phase/level combinations contribute nothing.

The server's existing nine Performer dance entitlements are preserved and can be
reconstructed from saved membership on subclass information/login projection.
Their detailed historical phase-specific unlock policy is not certified by this
batch; no new dance achievement is granted.

## Ownership, replay and recovery

Only canonical, ordinary authenticated sessions can mutate progression. The
service owns the inventory lock and locks the durable character row before
payment. Materials require exact UID, character ownership, type, location,
binding and stack preimages. Partial stacks retain their exact remaining units.
Subsequent progression requires the exact previously saved level and phase.

Study spending waits when the legacy cached balance differs from storage, without
charging or discarding pending points. This prevents the restored path from
overwriting an unsettled balance. Other legacy study-point earning/spending paths
still need a shared-wallet audit; this is not a claim to have rewritten all of them.
Storage or post-commit installation failures require reload before more mutations.

Duplicate learning is idempotent, capped training cannot spend again, and a P9
receipt cannot repeat. In the existing packet contract, separate valid training
commands request successive paid levels; no unverified request nonce or expected
level field is invented. The full ushort action, family, declared length and
actual bounded frame are checked before any offset access or mutation.

Existing legacy P1/P9 rows are preserved but not automatically converted into
completion receipts. They were created through the old unvalidated progression
path. A separate migration audit is required for existing holders who already
passed these terminal steps. The new producers certify newly committed enrollment
and P9 promotion, not historical client bits or generic subclass UI visits.

## Validation

Sixty-four isolated checks pass through the production subclass dispatcher,
including all seven complete paid paths, concurrent enrollment, actual storage
reload, fourteen achievement receipts, bound/mixed stacks, foreign material
ownership, study shortage, failed level/enrollment/final-promotion transactions,
unsettled balance recovery, level/rebirth gates and the real final stat calculator.
The isolated build passes; broader regression results are recorded with the final
batch below. The native request and display interpretation remains source-based;
live 6711 UI acceptance is still required before deployment.

The 1,111 existing quest, event, progression, wardrobe, mastery, cultivation and
rune regression checks pass. Five catalog/prerequisite audit checks pass.
After preserving the original source encoding and removing literal SQL assembly
from two fixture assertions, the isolated build and 64 subclass checks passed
again. Final staged server SHA-256:
`0B388D2101259098301AF7E747E6039BFA39EF46CA60DC46948803763C1EF0AE`.
Nothing was deployed or installed in the live client.

## Required-write follow-up

2026-09-20, baseline `3fed60f`: enrollment now requires one inserted membership
row and training requires one persisted Study Point debit. P1/P9 achievement
issuance must either insert its receipt or find an already durable receipt for
that player/achievement. A suppressed insert is not mistaken for a harmless
duplicate. Existing durable evidence remains compatible with paid enrollment.

Previously, a database trigger returning no row could consume materials without
membership, grant training without payment, or commit membership without its
achievement evidence. These failures now roll back the entire transaction and
fence the session for reload before projecting success.

The isolated build and **71 subclass checks** pass, including suppressed
membership/evidence inserts, suppressed Study Point updates, retry and production
membership/evidence reload. Log: `obj/chi-refill-validation/subclass-required-writes.log`.
No packet layout or catalog prices changed. This is not a full review of legacy
material metadata eligibility, unsettled Study Point ownership or native UI.

## Enrollment material ownership follow-up

Baseline `6798b49`: material selection now excludes locked, suspicious, wardrobe,
warehouse, equipped and expired items. A locked stack does not prevent another
eligible stack of the same material from paying. Both bound and unbound eligible
materials remain supported, as do legacy zero-stack singletons and unexpired
timed materials. This follows the existing owned-consumable restrictions rather
than changing enrollment prices or subclass eligibility.

The transactional debit checks saved lock/suspicion/wardrobe flags and exact
duration/expiry metadata as well as owner, ID, location, stack and binding.
Cached eligibility is rechecked at consumption. If a later stack has changed,
the entire transaction rolls back, restoring earlier consumed stacks and
withholding membership/evidence. Uncertain durable ownership fences for reload;
ordinary cached ineligibility simply preserves the materials.

The isolated build and **86 subclass checks** pass. Fifteen added checks cover
cached restrictions, stale saved metadata across two stacks, timed use and
changed expiry, and selecting a usable stack alongside a locked stack. Log:
`obj/chi-refill-validation/subclass-material-ownership.log`.
The **729 Magnolia regression checks** also pass, including the eight-day earned
saddle-to-Wrangler journey. Log:
`obj/chi-refill-validation/magnolia-material-ownership.log`.
No packet changes or live deployment. Study balance concurrency and native
acceptance remain separate work.

## Fresh earned career through all seven subclasses (2026-10-03)

The fresh level133, twice-reborn Warrior continues from the saved JiangHu
checkpoint with 17,445 earned Study Points and no subclass memberships.
`CheckCandidateFreshSubclass.cs` uses ordinary non-GM sessions, shop91008
purchases at installed CP prices, item723342 use (500 Study Points), and the
production subclass enrollment/training/promotion dispatcher. It neither seeds
memberships nor supplies currency, Study Points or achievement receipts.
Enrollment checks exact material consumption. Every training step checks the
installed cost and saved balance; repeated enrollment and capped training or
promotion must leave the checkpoint unchanged. Each successful step saves the
character and spells before recording the private probe snapshot.

The fixture loads all five town warehouses before loading items. Preparation
found ten free slots sufficient for the largest enrollment; no additional
warehouse deposits were needed. The final restart checks every loaded warehouse
and all five roster inventories against saved item identities, all fourteen P1/P9
`subclass-v1` receipts, seven level9/P9 memberships, and Performer dances1415?1423.

Validation is performed against the existing private candidate server SHA-256
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
This is server-side earning/persistence evidence through decoded requests, not
native 6711 UI acceptance or a live deployment. Historical-holder migration and
other outstanding roadmap requirements remain separate.

All four bounded earning batches passed. Enrollment and Study Book purchases
spent 48,023 earned CPs in total. The character consumed 477 books and spent
255,500 Study Points, leaving exactly 445:
`17,445 + 477 * 500 - 255,500 = 445`.

| Subclass | CP spent in this route |
| --- | ---: |
| Martial Artist | 2,724 |
| Warlock | 6,040 |
| Chi Master | 12,030 |
| Sage | 8,020 |
| Apothecary | 3,099 |
| Performer | 8,070 |
| Wrangler | 8,040 |

These costs reflect this character's prior earned materials and Study Points;
they are not a universal quoted price for a new account. Private logs are
`arena-process-career-fresh-subclass-prepare.log`, numbered earning logs1?3,
and the final `arena-process-career-fresh-subclass-earn.log`, under
`C:\Revora\DisposablePostgres\moon-box-candidate`.
A runner-only extra-parenthesis parse error occurred before the second batch
started; correcting that ignored wrapper did not mutate gameplay state.

The separate `career-fresh-subclass-reload` phase passed, including exact
checkpoint equality, loaded ownership, fourteen receipts and capped replay
refusal. Log: `arena-process-career-fresh-subclass-reload.log`.
