# Flower and Love Letter restoration

2026-09-20. Baseline `808522c`. Source evidence; not deployed.

Magnolia issues Orchids 753003/753009, Tulips 754001/754003/754009 and Love
Letters 756001/756003/756009. Issuance is atomic, but their existing recipient
delivery is not. This document separates admission fixes from that unfinished
earning-to-recipient contract.

Current follow-up: the durable-delivery implementation below supersedes the
memory-only findings for new deliveries. Offline ranking hydration is now wired
before world-actor creation, with separate-process component restoration now
validated. Historical migration, full Host restart and genuine-client acceptance
remain open.

## Findings and admission repair

`Flowers.SendFlower` read fixed offsets without local framing checks and used
only the low action byte. `GetFlowerTyp` returns zero for unknown items, which
is also the rose type. The handler would increment the recipient's roses and
remove an unrelated owned item. Admission now requires a bounded 1150 frame,
matching declared length and client seal, full action 0/1, a healthy canonical
sender and initialized flower state. Nonzero item UIDs must resolve to a
catalog-present gift in existing families 751..758 with positive quantity.

The exact inbound native frame size remains unverified. The 24..128-byte
admission bound is derived from existing reads plus trailer and existing
bounded-handler conventions; the server's outgoing serializer is 68 bytes.
See [protocol evidence](PROTOCOL_RESEARCH.md). This adds no new packet family.

Validation: isolated build and **738 Magnolia checks** pass. Nine new checks
prove that two unrelated owned items are retained and recipient counters stay
unchanged, and cover null, short, oversized, mismatched-length, wrong-ID,
bad-seal and aliased-action requests. Log:
`obj/chi-refill-validation/magnolia-flower-admission.log`.
These are rejection tests, not completed valid-delivery or native UI acceptance.

## Required remaining implementation

- Replace memory-first counter increments followed by separate item removal
  with one transaction covering exact source consumption, recipient totals,
  daily totals and delivery receipt. Preserve both players' ownership and
  use deterministic lock ordering for concurrent reciprocal gifts.
- Add durable loading and restart recovery. `Database/FlowersTable.cs` has
  commented-out file save/load code; the call sites in Server and GameState
  are also commented. `EntityTable.SetFlowers` currently reuses global memory
  dictionaries, which cannot prove persistence across process restart.
- Complete native acceptance and durable migration for the body-classification
  repair described below; process-local membership is now consistent.
- Replace day-of-month-only free-gift/day reset ownership with a durable date
  contract, checked arithmetic and replay-safe concurrent use.
- Establish recipient, binding, expiry and daily-reset contracts from approved
  client/catalog and official evidence; do not infer complete retail semantics
  merely from ID ranges or the old switch. Rank refresh must follow commit.
- Exercise each earned Magnolia family through ordinary recipient delivery,
  failure rollback, duplicate requests, both participants' reload and restart,
  then genuine 6711 visual acceptance before deployment.

Current code maps 756xxx to the Lilies counter internally and the female-sender
branch projects the Love effect. This is observed source behavior, not proof of
the complete native Love Letter contract. No completed milestone is claimed for
valid gift delivery, ranking persistence or historical migration.

## Consistent membership and committed body changes

Follow-up to `ca1e766`: login now delegates to the same female-body classifier
as gift delivery: 2001/2002 plus the legacy 2005/2006 compatibility profiles.
This takes the union of the two previously accepted sets; it does not claim
new native rendering evidence for the legacy profiles.

`Flowers.Attach` serializes membership changes and retains the existing flower
object from either dictionary. A misplaced record moves to the correct group
without resetting totals, daily totals, remaining free gift or send day. Repeated
attachment is idempotent. Two different records for the same UID are rejected
without changing either history: their correct reconciliation cannot be inferred
from the current memory counters.

Committed appearance projection calls this same membership path when the body
changes and flower state exists. A failed appearance transaction therefore does
not change membership. Moving a record rebuilds both groups' rank lists and
cached arrays under the existing ranking locks, retaining the existing 99-place
display cap and ordering ties by UID. This fixes stale group membership, not
the unfinished transaction ownership of gift delivery or rank persistence.

Validation: isolated build and **36 appearance checks** pass, including eight
supported body classifications, repeat/concurrent attachment, both directions
of committed body change, preserved totals/daily state, prior wrong-group
placement, failed appearance writes and conflicting-history preservation. Log:
`obj/chi-refill-validation/flower-membership-appearance.log`.
The **738 Magnolia regression checks** also pass against the rebuilt server,
including promotion, rebirth and Wrangler journeys. Log:
`obj/chi-refill-validation/flower-membership-magnolia.log`.
No native protocol change, client patch or live deployment.

## Recipient contract evidence and correction

Follow-up to `cf35544`, reviewed 2026-09-20:

- The official [male gift guide](https://co.99.com/guide/guides/gift_ranking.shtml)
  identifies female-to-male kisses, beer, jade and Love Letters and describes
  inventory-item selection. It also describes total and daily received counts.
- The dated [2015 Valentine catalog](https://co.99.com/guide/event/2015/valentine_sale/valentine_item.shtml)
  explicitly assigns flowers to female recipients and Love Letters/beer/kisses/
  jade to male recipients. This predates the target 6711 edition.
- The older [flower guide](https://co.99.com/guide/quests/flowersranking.shtml)
  describes male-to-female flowers. Its free rose eligibility is over level 50;
  the male gift guide states the corresponding female free kiss rule.
- The [2024 PC guide](https://co.99.com/guide/items/2024/1127/flowers___gifts.shtml)
  instead describes a level-120/second-rebirth free gift. This later eligibility
  rule must not silently replace the historical rule for 6711. Exact boundary
  interpretation and bound-gift treatment still need edition-specific evidence.

The existing handler aliased both item ranges onto four counters before
choosing the visual effect from the sender's body. Consequently a male could
consume a Love Letter into a female recipient's Lily count, or a female could
consume an Orchid as a male recipient's beer count. It now requires the
existing flower families 751..754 for action 0 and male-gift families 755..758
for action 1. Existing opposite-body recipient checks still apply.

The isolated build and **754 Magnolia checks** pass. Sixteen added checks cover
all eight issued Magnolia Orchid/Tulip/Love Letter variants: wrong-direction
requests retain the saved item and all counters; correct-direction requests
consume the item and update exactly the expected existing in-memory quantity.
Log: `obj/chi-refill-validation/flower-recipient-contract.log`.
These supplied-item direction tests are not earned-recipient persistence tests.

Next durable implementation must cover paid item delivery and the daily free
gift together, so an old memory-only writer cannot overwrite or bypass stored
totals. Required state includes owner UID, lifetime totals, full daily date and
daily totals, consumed daily allowance, exact source/recipient receipt and
concurrency preimages. Both participant locks and database owner rows need
consistent UID order. Persist source consumption and recipient credit in the
same transaction, check every required write, then project counters/ranks and
effects. Reload must read durable totals rather than regard the global cache as
proof of persistence. Startup rank restoration and ambiguous historical counter
reconciliation remain required; no implementation of this ledger is claimed yet.

## Atomic delivery and durable participant reload

Follow-up to `1a4402d`: `FlowerDelivery` replaces the entire memory-first send
branch for inventory gifts and daily free gifts. Startup initializes
`revora_flower_balances` and `revora_flower_deliveries`. Both players must be
healthy canonical online owners, outside trade/booth, and meet the established
sender/recipient direction. Item ownership, binding, stack, location, lock,
suspicion, wardrobe and expiry preimages are checked during the transaction.
One use consumes one stack unit; legacy zero-stack singletons remain usable.

Participant item locks and database entity locks are acquired in ascending UID
order. Durable balance rows are locked and lifetime totals must match loaded
memory. The transaction consumes the source or daily allowance, updates both
balance rows, credits the exact category/quantity and inserts the delivery
receipt. Every required debit/credit/receipt checks its affected row count.
Failure rolls everything back and fences both participants for reload. Native
counter overflow refuses the entire gift. Inventory, counters, ranks and the
existing effect packet are projected only after commit.

Daily rows use a complete Pacific `yyyyMMdd` date, following existing Revora
daily-quest policy. Same-number dates in different months are distinct. Query
refresh and login expose rolled-over daily totals and allowance; lifetime totals
remain intact. A backwards clock cannot reset a later saved day. The old
day-of-month Reset cannot overwrite managed state. Free-gift eligibility retains
the existing Revora ordinary-player rule; the conflicting historical level rule
is not silently changed. This remains an explicit retail-parity gap.

A bounded per-item one-second debounce prevents repeated simultaneous stack
uses. Spent singleton UIDs and used daily allowances remain unavailable after
reconnect. Remaining stack units may be intentionally used after reconnect;
this is not a claim of a native durable request nonce. Bound items are consumed
as gifts, matching prior behavior; no tradable recipient item is created.

Login reads PostgreSQL totals even when an in-memory flower object already
exists. Tests also evict both participants' dictionaries and ranking references
before reload, preventing cached objects from serving as evidence of durability.
Nonzero memory histories without a durable row are rejected intact rather than
silently seeded to zero. There is no automatic speculative historical import.
At actual process restart, offline participants' ranking positions still require
separate startup hydration; online reload restores that player's totals/rank.

The earning fixture follows every Magnolia Orchid, Tulip and Love Letter variant
through paid grade selection, actual planting, item reload, suppressed receipt
failure, both participant reloads, concurrent retry, memory eviction and durable
recipient reload. Additional fixtures cover daily free-gift receipt failure,
concurrent/reconnected use, month rollover/query refresh, suppressed source and
both balance writes, stacked retry, reciprocal gifts, stale recipient totals,
counter overflow and preservation of unmigrated history. Native effect rendering,
transport-loss behavior and an actual process restart remain acceptance gates.

Validation: isolated build, **792 Magnolia checks** and **36 appearance checks**
pass. Logs: `obj/chi-refill-validation/flower-durable-final.log` and
`obj/chi-refill-validation/flower-durable-appearance.log`. Preserve both new tables
on rollback; never fall back to the old memory-first writer against a deployed
ledger. No production schema, client or live executable was changed.

## Offline ranking startup

Follow-up to `b372479`: first initialization reads saved positive lifetime
balances joined to existing character names and bodies. It validates every
candidate before publishing membership and rebuilds both ranking groups using
the shared ranking projection. Saved recipients appear while offline; subsequent
login attaches to the same record and reloads durable totals. Daily values are
normalized for the current Pacific date without changing lifetime totals.

The read is bounded to 100,001 rows and rejects more than 100,000 positive-balance
owners rather than truncating rankings. Deleted characters are omitted by the
join; their durable delivery data is not deleted. Duplicate owners, invalid
counter widths and future dates reject the snapshot before publication. A live
GamePool or nonempty membership cache also rejects replacement. Empty first-use
ledgers retain existing migration evidence. Repeat initialization does not
replace already restored identities.

`Thread.CreateTournaments` initializes flowers before city guards and living
world actors: guards themselves enter GamePool, so the previous initialization
position would violate the new offline-startup requirement. This placement was
checked against `Server.Run` and the guard creation path; no live startup was run.

Validation: isolated build and **798 Magnolia checks** pass. Six additional
checks cover active-player rejection, invalid-snapshot rejection without partial
publication, offline male/female rank restoration, repeated initialization,
later login without duplicate rank entries and next-day display rollover.
The fixture clears GamePool and all flower caches in its isolated process. It
does **not** claim a real process restart or rendered native ranking acceptance.
Log: `obj/chi-refill-validation/flower-startup.log`.
The existing 99-place display limit is preserved; historical rank-limit parity,
counter migration and live/client rollout remain open.

## Separate-process restoration validation

Follow-up to `26ce47c`: the isolated Magnolia suite launches its compiled
validation executable in a new hidden process with `--flower-restart-check`.
This branch runs before normal fixture setup: it cannot recreate or delete the
parent's schema. The supplied connection must select exactly
`revora_potency_validation`, and the existing ledger must already be present.
Connection information travels through redirected stdin, never command-line
arguments, generated files or logged output. The parent bounds execution to
20 seconds and reads stdout/stderr concurrently; failures report only exception
types from the child. Parent fixture cleanup remains responsible for the schema.

The child proves three things: static enabled/player/membership state is fresh;
the real production initializer restores saved offline male/female totals,
date-normalized daily totals and ranks without any login; and repeated
initialization retains one object/rank entry per owner. The parent requires a
successful exit and all three assertions before passing its process check.

Validation: **799 Magnolia checks plus three child-process checks pass**. The
fixture compiles against the existing isolated server build. Log:
`obj/chi-refill-validation/flower-process-restart.log`.
This is a real fresh-process test of the flower startup component against the
same persisted fixture data, not a full `Server.Run`/Host restart: listeners,
world actor creation, shutdown and the native client are not exercised here.
No production process or database state was changed.

## Complete Top 100 and male gift pages

Follow-up to `5b11f6d`: replace the historical 99-place limit with 100 in all
eight categories and self queries. Live delivery and startup now rebuild from
the same complete membership set, using descending totals then ascending UID
for ties. Previously unranked recipients can fill vacancies immediately.
The official [gift ranking guide](https://co.99.com/guide/guides/gift_ranking.shtml)
describes a Top 100; UID tie-breaking is Revora policy.

Male gift pages now select their corresponding arrays instead of dereferencing
a missing list. Unknown categories and pages beyond the list are rejected.
A flower-specific packet factory allocates space for ten existing 80-byte
records; the old fixed allocation silently omitted record ten. Other ranking
families retain their existing allocation. These offsets are source-derived;
native 6711 rendering remains an acceptance gate.

Validation: isolated build, 73 appearance/ranking checks and 799 Magnolia checks
plus three fresh-process assertions pass. Coverage includes all eight serialized
final pages containing position 100, invalid page/category rejection, stable
ties and vacancy replacement. Logs: `obj/chi-refill-validation/flower-top100-appearance.log`
and `obj/chi-refill-validation/flower-top100-magnolia.log`. No live deployment.

## Fresh earned gifts and ordinary marriage (2026-10-03)

The fresh level 133 Warrior and newly trained level 130 female Warrior continue
from their completed Class PK checkpoint. A read-only preflight found both
single, no owned gifts in the six-character roster, and existing private female
ranking maxima of 99 roses, 999 lilies, 3 orchids and 1 tulip. Those competitors
and totals remain untouched; this batch does not claim a new queen.

The main character sends one free daily rose, buys a 999-lily bouquet through
shop 2888 at its loaded CP price, and earns three orchids 753003 plus one
tulip 754001 through two ordinary Magnolia admissions. Each Magnolia run takes
a normal seed, pays the 1-CP Unique-seed reroll and plants through the normal
item handler. Seed and 20%-bonus outcomes are controlled; they do not measure
random drop odds. The controlled dates are October 2-4, 2041.

All four gifts go through the bounded 1150 dispatcher. The exact source UID or
free allowance is consumed once, the recipient receives totals [1,999,3,1], and
repeating each request changes no saved career state. Both characters earn
10127 from flower-delivery-v1. No gift, balance or delivery receipt is seeded.

A separate process loads those totals, then the male proposes and the female
accepts through the ordinary marriage handler. Both saved spouse links and
10129 are checked; repeating acceptance must change nothing. The final reload
phase validates both loaded spouse links, all four loaded flower totals, saved
delivery receipts and nested prior career state.

All three earning/marriage/reload phases passed on their first execution.
Evidence: CheckCandidateFreshSocial.cs; private candidate SHA256
D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6.
The preflight initially needed a missing namespace import before compilation;
no gameplay state had changed. Calendar, proximity and Magnolia outcomes are
controlled. Native flower/marriage UI, general historical migration, competitive
rank titles, Couple PK victory and deployment remain separate requirements.
