# Paid reincarnation transaction

2026-09-16. Source baseline `cbdebb1`. This replaces the ordinary Sereth/native
1066 reset and Sereth's Oblivion Dew purchase. Live deployment and genuine-client
preview/rendering acceptance remain separate release gates.

## Rules and evidence

The [official reincarnation guide](https://co.99.com/guide/event/2013/rebirth/rebirth-3-1.shtml),
read 2026-09-16, supports second-reborn level-110 admission, one Oblivion Dew,
reset to level 15, no new attribute/Battle Power bonus, recovery at 110, shifting
the three-life class window, and retaining only the immediately preceding life's
summon. Confidence is high for those published rules, not a claim that the page
completely specifies patch 6711.

Revora keeps its configured CP rite fee (default 5,000), Sereth location and
2,000 CP dew purchase as explicit edition rules. Pending exact second-life
recovery must be settled before another reset; the service does not discard an
owed level-130 receipt to admit a new paid life. Recorded class and level history
shift together. Existing attribute ownership is preserved: subtract the configured
ordinary level points between the old level and 15, assign the destination's
level-15 base stats, and retain the remainder as unspent points. Recovery returns
the missing ordinary points once. This does not retroactively repair an already
incorrect historical budget or fabricate historical EXP.

The shared three-life skill planner receives the shifted history. The previous
life's documented summon is the only retained class summon; known unrelated
summons are removed. Learned skill/proficiency levels become previous levels,
while current level/EXP reset. The existing matrix's Windwalker composition and
unclassified-spell/Archer audit limitations remain under M6.55b. This change
does not certify complete retail skill parity or alter the existing pure-skill
learning policy. Equipment in both sets uses the reviewed rebirth lowering
rules; offhands move to the bag only when there is space. No new rebirth gift.

## Ownership and durability

An ordinary authenticated, alive, non-trading player speaks to nearby Sereth.
Opening the native window creates a two-minute session offer with exact profile,
body, bag/equipment, skill/proficiency rows, selected dew UID/stack and CP balance.
The existing framed 1066 request chooses a reviewed class and a same-sex body
size. A request without that offer cannot charge or reset. Cancel, expiry,
movement, changed belongings/balance/fee or a replacement session invalidate it.
The offer is consumed before mutation, preventing duplicate concurrent payment.

One owner-locked database transaction validates saved preimages and commits CP,
dew, all thirteen progression fields, body, equipment, skills, proficiencies,
the compatible legacy recovery row and a typed `revora_reincarnations` receipt.
Every changed row count is checked. A partial write rolls back; an unexpected
failure fences the session for reload. Existing packets and caches update only
after commit, without a required disconnect or a second payment write.

The receipt records old level/EXP, shifted history, class/body choices, consumed
dew, point budget and before/after CP. One active receipt per owner is enforced.
The latest typed receipt owns recovery, including when already at the saved
level 110. Saved EXP and the current award carry survive exactly once. Both
legacy deletion and typed receipt retirement participate in the same progression
transaction; a reconnect retains the committed result. Recovered receipts also
prevent the old level-only shortcut from applying a second recovery.

The dew purchase has its own explicit review/confirm offer, exact saved bag and
balance, atomic item/payment/receipt writes and immediate inventory/CP update.
No GM or character allowlist applies. Native frame offsets are source-derived;
see [protocol confidence](PROTOCOL_RESEARCH.md#paid-reincarnation-ownership-2026-09-16).

## Validation status

Isolated x86 build passed. The paid-reincarnation mode passed **2,085 checks**,
including its first/second-life prerequisites and 100 ordinary class transitions.
The shared experience mode passed **452 regression checks**; three producer
audit tests passed. These suites have overlapping prerequisites, not 2,537
independent feature scenarios. Source coverage stays 224/337; no unrelated
achievement was granted by this reset.

The paid suite exercises ordinary non-GM Sereth and actual 2032/1066 dispatch,
male/female body choices, shifted class and level histories, all selected
payments, learned skill/proficiency recovery levels, both equipment sets,
stacked bound dew, exact/full bags, cancellation, wrong/expired offers,
malformed requests, changed price/balance/owned rows, concurrent confirmations,
and suppressed writes to each transaction participant. Expiry after writes
rolls back everything. An injected postcommit projection failure preserves the
complete durable reset and requires a production reload without another charge.

Recovery checks cover saved level 110 and 139, high-bit EXP, crossing the 110
boundary, already-ready recovery, duplicate awards, normal saves and reconnect,
another paid life after either recovery level, and failed legacy-row deletion.
They also exposed and fixed an existing skill-planner defect: re-granting an
already owned bonus must update its row and preserve its previous level rather
than attempting another insert. Catalog-valid skill levels are used.

Commands: `Verify-PotencyHunt.ps1 -PaidReincarnationOnly` and `-ExperienceOnly`.
Ignored local logs: `obj/chi-refill-validation/paid-reincarnation.log` and
`paid-reincarnation-experience.log`. No live installation, restart, client patch
publication or native visual confirmation occurred. M6.55b's remaining skill
audit and M6.55d's historical reconciliation are still open.
