# Atomic Euro jerseys and Big Fan earning

## Source implementation (2026-09-25)

The loaded EShopV2 catalog supplies jersey designs 192655 and 192665 through the
CP mall (2888) and an NPC shop (432). The generic legacy CP branch added inventory
before deducting currency. `EuroJerseyShop` now intercepts these two item types
before that branch, using the existing checked item-purchase frame and the same
transaction pattern as ExemptionTokenShop. Other shop items retain their existing
handlers at this initial checkpoint; the extension below supersedes that scope.
The dormant legacy RVP shop is not enabled.

The service requires a healthy canonical ordinary player, valid shop membership,
positive loaded item price, bounded quantity and sufficient bag space. The global
mall keeps its existing remote access; other shop IDs require a real nearby NPC
in the same map. Trade, booth and poker participation exclude purchases. Owned bag
UIDs are checked against durable inventory under the character lock. CP or bound
CP payment, item insertion and `revora_euro_jersey_purchases` receipt commit in one
transaction; installation and currency projection follow commit. Failures fence
the session for reload. Identical purchases within one second are suppressed using
the durable receipt; this is bounded duplicate-request protection, not a wire
request-ID protocol. A later deliberate purchase is allowed.

Prices come from the existing catalog, without a new override. The tested 192655
cost is **1,200 CPs**. Bound-CP purchases produce bound jerseys and do not debit
ordinary CPs. Startup initializes the receipt table/index alongside the existing
shop services. No client packet layout changes or client patch are required by
this source change; actual native shop acceptance remains pending.

## Complete ordinary Big Fan route

The collection fixture continues its ordinary character's earned world-boss
currencies. It buys 192655 through the actual shop handler, stores it through the
wardrobe request, and confirms a jersey alone does not grant Big Fan. It then buys
Vesper's existing 200564 mount for 2,500 earned RVPs and admits it to the wardrobe.
The pair grants Big Fan; it does not grant the six-design Euro Collector title.
Character/item/wardrobe reload preserves the equipped title and both exact debits.

Isolated build and **79 collection checks pass**. Additional checks cover excessive
quantity, unstocked shop, remote NPC and active-trade refusal, immediate identical
retry, suppressed receipt rollback, bound-CP separation and bound-item reload.
The bound-payment case supplies its bound currency solely to test that transaction;
the Big Fan earning case uses the previously earned ordinary CP/RVP bounty.
Time, inter-map placement and boss damage remain controlled fixture inputs.

The wardrobe audit advances only Big Fan: 32 code-validated, 34 predicate-only,
six unimplemented rewards. Full Host restart, socket/native presentation and live
deployment remain pending. No running server or live database was changed. The
isolated build retains the owner's unrelated WIP. Rollback restores the prior
binary/source while preserving committed purchase receipts and owned items.

## Compatible CP-shop cosmetics extension

The same transaction path now handles every reviewed collection garment/mount
already stocked in an existing CP EShop. It preserves shop membership, loaded
prices, remote mall versus nearby-NPC access, currency selection, bag checks and
receipt replay handling. Non-CP shops and unrelated item families keep their
existing dispatch. No new stock, price, NPC or currency conversion is introduced.
The internal class and receipt table retain their historical jersey names to
preserve existing receipts and startup compatibility.

The [fifty-garment earning route](COLLECTION_ACHIEVEMENTS.md) passes within the
261-check collection suite. It buys forty distinct non-jersey garments, verifies
immediate duplicate requests debit once, and suppresses the receipt insert for
one of those purchases: payment and the item insert both roll back. A production
reload then permits a successful retry. This extends transactional validation
beyond the original two jerseys; native shop presentation remains pending.

## Existing talisman stock extension

The same atomic transaction also handles loaded CP-shop stock 201009 and 202009.
This replaces their generic purchase dispatch without changing prices, stock,
binding choice or eligibility. Other talismans and RevoraMaster's separate kit
remain outside this extension. The historical receipt-table name is retained.

The [earned talisman route](EQUIPMENT_ACHIEVEMENTS.md#earned-talisman-follow-up)
passes in the 319-check collection suite: 2,580 boss-earned CPs purchase both,
immediate retries debit once, suppressed payment/item/receipt writes roll back,
and ordinary equipment plus production login retains achievement 10418 once.
