# Auction handler and lifecycle integration

Baseline `944a012`, 2026-09-15. M6.30 connects the escrow engine to the existing
auction packet entry points and event loop. Server integration is implemented
for ordinary eligible authenticated players. **Genuine 6711 acceptance and live
deployment remain pending.** Fixture packets do not prove client UI behavior.

## Requests and durable identities

Families 1320/1321/1322 now dispatch through `AuctionGateway`. The old listing,
bid, buyout, refund, cancellation and UID-recreating delivery branches are
removed. Whole-frame length, declared size, family, suffix and accessed fields
are checked before the legacy accessors run. Source evidence and confidence are
recorded in [PROTOCOL_RESEARCH.md](PROTOCOL_RESEARCH.md).

Every new listing commits a display UID in `revora_auction_wire` with its fee,
physical-item escrow and operation receipt. Identity allocation failure rolls
back all of those changes. The display UID resolves permanently to one listing
UUID; it is never reassigned to a later listing of the same physical item. Buy,
bid and cancel requests use that reference, not an inventory item UID. Ownership,
currency and price still come from the engine's stored listing.

Preview item/effect references use the display UID. The actual `items` and
`itemadding` rows retain the original physical UID, including after delivery.
The server counter reserves display IDs without wrapping and startup advances
it above current item, adding and issued-display IDs. Existing display mappings
survive restart. This is a single game-server integration, not certification
of cross-server item allocation or every legacy counter's exhaustion behavior.

New listing requests necessarily begin with an inventory item UID. A durable
one-second owner/item creation debounce prevents rapid cancel/relist repeats.
No verified creation nonce exists: later matching creation requests are new
listings. Arbitrarily delayed creation replay is not claimed as solved by the
buy/bid/cancel identity protection.

## Boards, replies and deliveries

Public queries read ten listings per database page with currency filtering.
Personal boards are bounded to fifty active listings or fifty leading bids.
Those limits are enforced before new fees/deposits; an immediate buyout remains
available at the leading-bid limit. These are Revora capacity rules, not claimed
original Conquer limits. Queries share a one-second session throttle.

The 1321 record uses a 128-byte frame so its sixteen-byte bidder name no longer
overlaps the transport suffix. Both name fields are encoded within their own
sixteen-byte slots. Page count, total and page are written to the existing
UInt16 field positions. The existing 96-byte personal record is retained.
Source bounds and fixture decoding pass; native paging, preview identity and
name presentation still require acceptance.

Successful native buyouts and cancellations attempt the actor's pending
deliveries immediately. The event loop runs bounded expiry and delivery retries
every ten seconds: up to 64 expiries and 32 online recipients per pass, with up
to 16 deliverable records per recipient. Recipient selection advances through
the pending online IDs, and busy owner locks are skipped for a later pass.
Full inventory/purse and offline status preserve the durable entitlement.
Failed expiry transactions remain pending and are logged for retry/repair.

## Startup and legacy recovery

`ConquerItemTable.LoadAuctionItems` now initializes the engine and gateway after
the item/refinery catalogs load. The snapshot saver no longer deletes and
rebuilds `auctionitems`. The unused legacy expiry subscriber and duplicate
serialization/mutation paths are removed.

Activation fails closed if persisted legacy auction rows or a legacy cache in
the current process exist. They are preserved for recovery. The earlier
read-only observation of zero persisted legacy rows does **not** prove an old
running process has no unsaved listings. Deployment must review that old
process's state before shutdown; a fresh process cannot recover already-lost
memory by inspecting an empty table. No legacy rows are automatically discarded
or reconstructed from incomplete snapshots.

## Validation

The final isolated x86 build passes 89 auction checks, 452 progression/book/silver
checks, 35 mail checks and 16 audit tests: **592 final checks**. The broader
28-mode quest/event sweep passed 1,793 checks before the final 44-byte frame
compatibility and creation-debounce changes. Those final auction boundaries
were exercised through the production packet entry points in the final run.

Coverage includes ordinary-player routing, original-UID delivery, durable display
identity across restart and relisting, malformed frames, exact stored prices,
query bounds, long-name/footer separation, failed identity rollback, scheduled
delivery, busy owners, legacy-state activation gates and non-wrapping reservations.
The engine fixtures continue to cover both currencies, escrow/refund/claim
rollback and refinement/perfection preservation. Validation uses disposable
schemas and does not modify public player rows.

Validated assembly SHA-256:
`DD7064AF77D275DD198387A3D67F6F7349E315B3497961885DBA133013164442`.

No live server restart, deployment, client installation or public patch was
performed. Required native checks are listing and detail presentation, both
currencies, bidding, buyout, cancellation, creation/relisting behavior, paging,
pending delivery after reconnect, balance refresh and all refinement effects.
