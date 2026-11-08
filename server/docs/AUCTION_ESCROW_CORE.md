# Auction escrow and settlement core

Baseline `f04da65`, 2026-09-15. M6.29 adds the durable auction engine and ordinary
player fixtures. **This is staged core work, not a completed player service.**
At this baseline the legacy packet handlers, startup loader and scheduling were
not yet connected. [M6.30](AUCTION_INTEGRATION.md) subsequently implements those
connections. Genuine client acceptance and deployment remain pending.

## Why the old lifecycle cannot certify item acquisition

`Network/GamePackets/Auction.cs` cancellation trusts the supplied item UID
without checking the seller. Delivery replaces the original UID and recreates
items through serializers that omit newer equipment fields. Bidding, refunds,
payment and item movement commit separately. Both legacy snapshot savers delete
all `auctionitems` rows before rebuilding them. The expiry subscriber has no
construction site in the compiled source inventory.

A Revora-owned x64 read-only inspector found **zero persisted legacy auction
rows** in `public.auctionitems` on 2026-09-15. It printed only the count and
column names/types. The table lacks perfection and stabilization-history
columns. This observation does not prove that a running server has no unsaved
in-memory listings, nor authorize deleting future legacy rows.

Inspector: ignored `obj/chi-refill-validation/bin/AuctionInspect.exe`, SHA-256
`969F868B0227A2D887BE115E37A2826E44013161C391FAE68DA496E50425FB8D`.
It uses existing local configuration internally and a read-only transaction;
no player identities or credentials are emitted.

## Engine contract

`AuctionHouse` stores listings in `revora_auctions`, immutable operation records
in `revora_auction_events`, and pending refunds, proceeds and parcels in
`revora_auction_deliveries`. Listing identity is a UUID separate from the
physical item UID. A closed UUID cannot affect a later listing of the same item.

Listing transfers the original `items` row to ownerless escrow and charges its
fee in one transaction. It never deletes or recreates the physical item or its
`itemadding` rows. The existing duration fees are retained: 500 silver for 12
hours, 1,100 for 24, and 2,250 for 48. Other durations and currencies are rejected.
Reserve must be positive; buyout may be zero (bidding only) or at least reserve.

Only eligible, fully loaded canonical ordinary players may use the core. It
rejects active trades/stalls, poker/qualifier participation, bound, locked,
suspicious, equipped, warehoused, inscribed, wardrobe and time-limited items.
Catalog tradability and durable ownership are checked. The saved item fingerprint
and Summer crafting provenance checks prevent known stale or forged input.

Bids use the listing's stored currency and prices. The first bid must meet
reserve; later bids must exceed the held bid. A bidder increasing their own bid
pays only the difference. An outbid player's previous deposit becomes a durable
refund. An offer reaching buyout pays exactly the fixed price. Seller self-bids
are rejected. A buyout atomically records buyer payment, item entitlement and
seller proceeds; full inventories or purses cannot erase the obligations.

Only the seller can cancel an unexpired open listing. Cancellation owes its item
back and refunds any held bid. Expiry settles at the exact end boundary, returns
unsold items, and creates winner/seller deliveries for sold items. The fee is
not refunded. Expiry processes at most 64 listings per transaction.

Claims lock the recipient and a bounded batch of at most 16 deliverable records.
Full inventories and currency caps leave blocked rewards pending without an
expiry. Item ownership, currency updates and claim acknowledgements commit
together, then project into the canonical session. Silver uses the saved-image
contract; CP writes check the current durable balance. Item UIDs, perfection,
active refinery, permanent fallback and paid stabilization credit survive.

## Validation

The isolated x86 build and 60 auction checks pass. Fixtures cover both currencies,
fee and price rules, repeated/self/outbid bids, fixed-price settlement, seller
authorization, cancellation, exact expiry, competing buyouts, original-UID
delivery, refinement/perfection preservation, full purses/inventories, offline
and fresh-session delivery, stale balances/items, relisting identity, and
transaction failure/receipt suppression. Public player rows are not used.

Assembly SHA-256:
`34F075D83FD2F0CAFCBDD1DC2A2A92607B889067A7776B3DA3006F5F477C605D`.
The final revision also passes 452 progression/book/silver, 35 mail, 108
refinery, 19 trade and 16 audit checks: **690 checks** in total.

## Integration still required

- Replace legacy listing/bidding/cancellation/buyout mutation paths with the
  engine and preserve the relevant native replies.
- Validate complete request framing before accessing action, price or item
  fields. Resolve listing identity and delayed native request replay; engine
  UUID protection alone does not prove a wire-level replay identity.
- Replace the legacy snapshot loader/savers and schedule bounded expiry and
  delivery retries. Preserve any legacy rows or unsaved listings for reviewed
  migration; do not silently discard them.
- Wire native list/query projections without making the cache authoritative for
  payments. Review the existing 1321 response length/footer mismatch.
- Verify native 6711 list, bid, buyout, cancellation, pending delivery, reconnect
  and refinery presentation before deployment.

The core was not initialized by production startup in M6.29. See
[M6.30](AUCTION_INTEGRATION.md) for the subsequent handler/lifecycle replacement,
its validation and the remaining native/deployment gates.
