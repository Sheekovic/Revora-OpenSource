# Level-5 installment route: evidence and implementation contract

2026-09-20, inspected source `115949b`. This is research for the remaining
housing milestone; no installment service, showroom or new reward is enabled.

Implementation follow-up: [the connected service](HOUSING_INSTALLMENTS.md) now
supersedes this research checkpoint's missing showroom/payment handlers. Build
and 290 housing checks pass. Price parity is corrected below; full earned/native acceptance
gates remain open; no production deployment is implied.

## Published route

The primary [PC level-5 guide](https://co.99.com/guide/guides/lv5house.shtml)
confirms that a level-4 owner visits House Admin, enters a display home, and
speaks with Senior House Manager. The total payment is 5 Dragon Balls,
20 Lucky Amulets, 40 Euxenite Ores, 20 Meteor Scrolls and 30 super gems. Partial
deliveries are allowed, the manager reports submitted amounts, and completion
upgrades the house after furniture is removed. The page does not prescribe
a mandatory delivery order, per-visit quantity or expiry. Do not invent those
as retail requirements.

The guide says Thunder, Glory and Tortoise gems are not needed and that the
other gems suffice. Seven standard super families are therefore confirmed
inputs; the wording alone does not prove that the three named families are
rejected by every edition. A seven-family-only initial policy must be identified
as a conservative Revora eligibility rule, not stronger verified retail evidence.
The [general housing guide](https://co.99.com/guide/guides/house_change.shtml)
places the manager at 101,102 and independently corroborates the material total.

## Installed evidence

Run `python tools/audit_level5_housing.py`. The audit reads only owned local
catalogs and server terrain; it does not execute the client, query a database,
change files or emit proprietary content. It verifies the existing item hashes,
11 material identities and the ordinary walking approach while reporting the
loaded-catalog prices below. `--require-price-parity` passes: both prices are 5 CPs.

| Material | Installed identity |
| --- | --- |
| Dragon Ball | 1088000 |
| Lucky Amulet | 723087 |
| Euxenite Ore | 1072031 |
| Meteor Scroll | 720027 |
| Standard super gems | 700003, 700013, 700023, 700033, 700043, 700053, 700063 |

Both item catalogs agree on these identities. The client contains
`NpcType1297`, named SeniorHouseManager, with model 266. Following the existing
NPC-template/mesh relation (e.g. HouseAdmin template 806, mesh 8060), mesh 12970
is the intended manager appearance. This is static inference, not rendered
acceptance. `npc.ini` SHA-256 is
`b06db1e2484992a1b58eabd7a3d884da07ac728a4a183d0fd18f2dccd5b20497`.

The installed family-house server floor 3024 is 170 by 170, SHA-256
`bbb2abb9cf4f023af233c4a3df078de59e3e3505dd5b655b061977011172bae8`.
Both landing 90,70 and manager position 101,102 are walkable. Reserving the
manager's cell plus its eight neighbors, a four-direction search reaches
96,105 within speaking distance in 59 steps. This proves server-floor reachability
only. The class-5 client map-index evidence already appears in `HOUSING_SERVICE.md`.

Bounded read-only PostgreSQL inspection found no public NPC named for the manager,
no actors on map 3024, no map-catalog row whose ID or base is 3024, and no monster
spawns on 3024. This is a snapshot, not a permanent reservation. Source ID 30161
already belongs to the furniture-store travel dialog and must not be reused.
A showroom initializer must check current map/NPC ownership and collisions before
publishing an overlay; it must never repurpose a private player house.

## Existing earning source and uncovered price mismatch

Correction: the earlier audit read unused `itemtype.txt`. The actual base catalog
is `itemtype-new.txt`, selected by `Constants.ItemBaseInfosPath`. Lucky Amulet
costs 5 CPs in both that catalog and the client, so twenty cost 100 CPs. All 28
progression-shop prices agree. Seventy shop checks now pass with the production
catalog and stock loaders, without price overrides. The old discrepancy and
1,500-CP total are withdrawn. No installed prices were changed.
See [the corrected audit](PROGRESSION_SHOP_PRICE_PARITY.md).

An old NPC 338111 branch also mentions selling amulets, but read-only catalog
inspection found no actor with that ID. It is not evidence of a reachable earning
route and must not be counted as one.

## Next implementation requirements

1. Restore the display-home visit and manager using the existing 3024 terrain,
   confirmed appearance and reachable coordinates. Verify normal entry, return,
   reconnect and unchanged private-house ownership. Keep presentation concise.
2. Store five bounded per-character submitted counters, plus exact owned-item
   debit receipts. Allow partial deliveries without an invented stage order;
   never take more than the outstanding amount. Preserve bound input provenance.
3. Freeze material identities, quantities and preimages in a short-lived offer;
   consume confirmation once. Under the database owner lock, atomically debit
   materials and credit submitted counts. Failed inserts/updates/deletes must
   roll back completely and fence before recovery. Include stack splits and
   singleton handling; forty Euxenite Ores cannot require simultaneous free space
   beyond the ordinary forty-slot bag.
4. Expose accurate submitted/outstanding quantities after reconnect. Reject
   foreign, locked, equipped, stored, temporary and otherwise ineligible inputs.
   Require the canonical level-4 owner near the manager for payment/claim.
5. Once all totals are paid, explicitly confirm construction. Commit the level-5
   house, durable identity, completion marker, full material provenance and
   existing achievement 10122 together, with the usual empty-house checks.
   Prevent a different level-5 route from stranding an active paid contract;
   reconcile any externally changed house level without losing deposits.
6. Exercise actual ordinary shop/mining/composition earning sources, partial
   deposits, concurrent confirmations, exact completion, reconnect and each
   failed participant. Validate a genuine-6711 playthrough separately. Do not
   check the installment milestone for a staged ledger or a showroom alone.

The published totals have no additional silver/CP construction fee. Initial
partial deposits must not grant a certificate, title or completed house. No
production mutation, restart or release was performed for this research.
