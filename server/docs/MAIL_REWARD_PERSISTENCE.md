# Durable mail rewards

Mail claims now persist the reward and its consumed attachment together. This is
a prerequisite for event parcels and for refinery equipment transfers; it does
not restore the separate auction lifecycle or create a new event reward source.

## Ownership and recovery

Claims require an ordinary, fully loaded canonical player session, the owner's
inventory lock, and an unchanged durable mailbox revision. Gold and CP claims
compare the stored purse before crediting it. Item-type attachments insert the
item in the same transaction as consuming the attachment. Concrete parcels move
the original ownerless item row into the recipient's inventory, preserving its
UID, perfection data, all refinery rows and stabilization credit.

Foreign-owned items cannot be claimed. An arbitrary ownerless UID is insufficient:
the returned-item action also requires a persisted mail attachment for this
recipient. Missing item references remain in the mailbox for repair. Full bags,
overflow and failed database writes preserve rewards. Post-commit failures fence
the session for reload rather than replaying payment. Empty letters can be
deleted; unclaimed rewards cannot be discarded.

Generic mailbox saves may append new letters but cannot rewrite existing rewards.
The existing server-side AddPrize helper saves before announcing arrival. Future
quest/event producers still need to issue their letter and source completion in
one transaction; this helper alone does not provide that source-level guarantee.

## Legacy compatibility and startup

The existing byte-count/BinaryWriter blob format is retained, including zero-ID
letters and zero padding. Duplicate identities, oversized records and unknown
trailing data require repair. A read-only audit on 2026-09-15 parsed all 13,316
saved mailboxes and 10,239 letters, with zero format failures. These contained no
concrete item references; item delivery was tested in the disposable database.
No production mail contents, player identifiers or credentials were exported.

Startup no longer deletes all ownerless item rows or clears auction snapshots
before reading them. Ownerless rows may be mail, auction or detained-item escrow;
cleanup requires evidence from the owning subsystem. The canonical single-item
loader now restores the same full equipment metadata as inventory loading.
Refinery catalogs load before auction item effects are materialized.

## Validation and remaining gates

Focused tests cover concurrent/replayed claims, all four attachment kinds, purse
and mailbox write failures, full bags, original-UID equipment preservation,
foreign ownership, returned-item entitlement, stale saves, missing references,
zero-ID compatibility, reload and bounded mailbox pagination.

The isolated build passed. The mail fixture passes 32 checks. All 1,319 existing
gameplay regression checks passed before the final list-pagination bounds change;
the final build then passed all 32 mail checks. Five producer-audit tests pass and
coverage remains 77/337. Staged assembly SHA-256:
`D06DA27F4623C616CE2D2885A164787F5AFEDB4D7562E2438957A2473A0ECCB5`.

The 1045 action width remains a source-consistent inference, not a captured native
protocol fact. Native 6711 list, text, claim and returned-item presentation remain
pending. The auction's bidding, settlement and cancellation transactions still
need restoration before the multi-effect refinery deployment gate can close.
This batch is staged source and validation only; no live deployment or restart.
