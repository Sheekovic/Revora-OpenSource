# Basic house furniture

Source implementation, 2026-09-20. This connects ordinary purchase, item use,
preview activation, footprint placement, saved ownership, achievement 10101 and destructive
removal. It is not deployed or accepted in the genuine client.

FurnitureNPC 30161 at Twin City 339,528 now offers twelve basic furnishings in
two short dialogue pages, plus Item Box on a third page. Following the owner's
2026-09-20 decision, each decorative piece costs **2,500 CPs**. The separate
Item Box retains its **100,000 silver** price. This is a Revora economy
choice, not a claimed retail price. The old offer to enter an empty furniture
store is superseded by a working purchase service. Starting currency still has
to be earned through other gameplay.

The twelve catalog items 721177–721188 map to terrain models 1–12. This is an
explicit Revora mapping supported by matching installed asset names; the exact
historical server mapping is not claimed. Item boxes now have a separate regular-NPC
profile and storage contract in [the box service](HOUSE_ITEM_BOXES.md). VIP/seasonal
furniture remains unfinished.

## Ownership and transactions

- A purchase requires a nearby ordinary player, a free inventory slot, sufficient
  CPs for decorations (silver for the Item Box) and a two-minute confirmed offer. Its item, payment and receipt commit
  together. Repeated confirmation cannot charge the same offer again.
- Item use requires the player's own saved house, a permanent unlocked inventory
  item and free capacity. It retains an exact item reference/UID/type/stack/binding
  snapshot for two minutes and sends the established action-5 preview control.
- Placement accepts the bounded three-field 2030 request. Appearance must match
  the selected model with direction 0–7. The cell must be walkable, nearby, empty,
  and more than three tiles from an exit. Current player cells are protected.
- Saved house level and map ownership are checked under the owner transaction
  lock. Capacities are 0/8/9/10/12 for levels 1–5. Existing furniture rows count
  conservatively against decorative capacity, excluding type-4 boxes. Box limits
  are separately one at level one and two at levels two through five.
- Exact item consumption, the furniture row, durable owner/material receipt and
  achievement 10101 commit together for decorative furniture. Box placement does
  not manufacture this achievement. The server allocates actor IDs from a
  noncycling database sequence; preview IDs are never accepted as ownership.
- A valid action-6 request removes only an owned, nearby, recorded furniture
  actor in that owner's house. Removal and its retained historical receipt
  commit together. No item or currency is refunded. Previously earned
  achievement credit is retained.

The installed map loader reconstructs furniture from durable rows. Consumed
items and purchase/placement/removal receipts survive reconnect. Legacy furniture
without new ownership evidence cannot be destroyed by this service and needs
explicit reconciliation. The old uncalled `HouseTable.SaveFurniture` is unused.

## Acceptance limits

Validation: isolated build and **447 housing/storage checks** pass, including the
ordinary purchase-to-placement route for all twelve models, concurrent confirms,
saved debits, cold house reconstruction, retained achievement after removal,
all five level capacities, foreign removal refusal, expired/stale selection,
occupied/exit cells, and injected failure of each purchase/placement/removal
transaction participant followed by production reload and retry. All twelve
decoration purchases debit 30,000 CPs total and leave silver unchanged. Exact
2,500-CP and insufficient 2,499-CP boundaries are tested; bound CPs and silver
cannot substitute. Starting silver/CPs and negative-test materials are fixture inputs.
The dedicated packet fixture
passes 82 furniture/click checks after the box interaction extension. Current
combined service validation is recorded in the box service document.

Purchase receipts now record `cps_paid` separately from `silver_paid`. The
idempotent schema update preserves historical silver payments and requires
exactly one positive payment currency. New decorative purchases save 2,500 CPs
and zero silver; Item Box purchases retain their silver receipts. CP deduction
uses the saved prior balance under the character lock, and the post-commit
balance update refreshes the client. No installed item catalog needs a price
patch: this service displays and charges its explicit NPC offer price. No live
schema update or deployment was performed for this price change.

The packet shapes have approved-client static evidence in
[the native research](FURNITURE_NATIVE_RESEARCH.md), with 88 furniture
instruction checks and 65 separate storage instruction checks.
The bounded decoder has 82 synthetic checks. These do not substitute for rendered
preview, placement, rotation, removal or a full Host restart test.

Furniture now blocks the audited per-cell access masks in its full scene footprint.
Placement, reload and removal share those masks; independent terrain and
remaining furniture blockers are preserved. The
[collision investigation](FURNITURE_SCENE_COLLISION.md) records the native world
transform and access-flag evidence, twelve exact scene audits, and limitations.
Height-based walking/jumping and native visual acceptance remain open. Guest
permissions, native box acceptance, VIP furniture and seasonal furniture remain
unfinished. No player allowlist or GM requirement gates these source services.


## Fresh paid twelve-piece furnishing and cold recovery (2026-10-03)

The ordinary fresh career now continues its earned five-level house through all
twelve basic decorative purchases at NPC30161. Each costs exactly2,500 CPs,
for30,000 earned CPs total and no silver. Repeated confirmations do not create
another item or charge. Purchases use the normal offer flow; no item is supplied.

The character enters its owned house through House Admin30156, uses each purchased
item and submits a sealed2030 placement frame at a cell accepted by the production
footprint/occupancy check. Each item is consumed once; replaying its placement
frame leaves the exact snapshot unchanged. All twelve purchase receipts, twelve
active placement records and twelve projected furniture NPCs exist, and10101 is
earned. Bag capacity is respected through at most two purchase/placement trips.

A separate game/database restart verifies the full saved career, house, purses,
items, purchases, placements and furniture rows. Ordinary entry reconstructs all
twelve furniture actors; the actual exit portal and final saved state also pass.
Both `career-fresh-furniture-earn` and `career-fresh-furniture-reload` pass on their
first runtime attempts. Evidence: `tools/CheckCandidateFreshFurniture.cs`,
`revora_fresh_furniture_probe`, and matching private phase logs.

Candidate SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
NPC/portal positioning and a legal placement cell are selected by the fixture.
This proves ordinary paid placement and persistence, not native preview/rendering,
Item Box storage, all furniture profiles or live deployment. Production source
and the separately pending native/release gates are unchanged.
