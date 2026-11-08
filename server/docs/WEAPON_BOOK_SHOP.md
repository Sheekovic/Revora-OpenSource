# Transactional weapon-book purchases

Baseline `a7e26b5`, 2026-09-15. M6.27 completes the server-side shop purchase
transaction for the sixteen books in [M6.25](WEAPON_SKILL_BOOKS.md), using the
silver ownership introduced in [M6.26](SILVER_OWNERSHIP.md). Eligible ordinary
players can buy, learn and train these skills through the existing game paths.
No GM, account-name or owner allowlist is required.

## Purchase contract

The nearby NPC must be in the player's current map and present in the loaded
shop catalog, with the requested book listed and silver selected as currency.
The existing 17-tile interaction range is retained. The character must be
canonical, fully loaded, alive, outside trade/booth and not seated at poker.
Purchase itself has no character-level or weapon-proficiency gate; those
requirements apply when learning the book.

The installed server/client catalogs already list all sixteen books in shops
141 and 423. Each currently costs 5,000 silver, as recorded with source hashes
in M6.25. This change reads the server catalog price; it does not alter prices
or install new client content. The reviewed books are nonstacking. A future
catalog that makes them stackable requires a reviewed delivery plan rather
than silently using the legacy stack-merging handler.

Quantity zero keeps the existing one-book meaning. A purchase accepts at most
forty books and requires enough space for the entire quantity. Price
multiplication uses unsigned 64-bit arithmetic before comparison with the
supported silver balance. Insufficient funds or space delivers nothing and
charges nothing. Existing unrelated shop routes retain their dispatch.

While holding the character owner lock, the service locks its entity database
row, compares the live and durable silver balance, and checks the durable
inventory count against the loaded inventory. Pending unsaved silver defers
the purchase; it can proceed after the normal balance save. A stale inventory
requires reload. The service never overwrites a different durable purse merely
to make a purchase succeed.

One database transaction deducts silver, inserts every exact book UID and
records the purchase. The receipt stores shop, item type, normalized quantity,
unit price, total cost, before/after balances, item UIDs and UTC time. The schema
checks receipt arithmetic and quantity bounds. Failure rolls back payment,
items and receipt together. Client balance and inventory updates happen only
after commit. Production item loading restores the committed books, including
their quantity, binding, color and identity.

## Duplicate clicks and framing evidence

Committed receipts suppress another purchase by the same character at the
same shop for the same book and normalized quantity within one second. The
check runs under the database owner lock and survives session replacement or
service reinitialization. Failed transactions leave no receipt and can retry
immediately. At the exact one-second boundary a matching request is a new
purchase. Different items/quantities remain separate purchase requests.

This is a durable duplicate-click window, not a verified unique wire request
identifier. An arbitrarily delayed replay can be another purchase. The current
packet evidence does not justify inventing a nonce or blocking all later
matching purchases. See [protocol evidence](PROTOCOL_RESEARCH.md).

The reviewed book service validates family 1009, declared/actual length,
the existing client suffix, the two-byte action at 20 and quantity at 26.
Accepted frames contain 30 or more payload bytes plus the eight-byte suffix,
up to 128 total bytes. Existing buy aliases 1 and 55 are retained. The outer
dispatcher now checks the complete action prefix before reading it. Reviewed
book buys use action 20 before the legacy equipment fallback at byte 16, so an
opaque byte there cannot redirect buying into an equipment-set switch.
Other action families' variable fields are not certified by this change.

## Validation and remaining gates

The isolated x86 build passes. The EXP/skill/book/silver/shop fixture passes
421 checks, including 54 new purchase checks. These exercise every book,
multi-buy, quantity and price bounds, exact payment/delivery, full inventories,
failure rollback, immediate retry, pending/stale balances, session replacement,
production item reload, concurrent credits/saves/purchases, NPC/catalog
eligibility, malformed frames through both service and packet entry, and
purchase-to-learn-to-fixed Fast Blade achievement completion.

Before the final action-dispatch and receipt-count checks, all 28 event regression modes
passed 1,787 checks. The final revision then passed the 421 focused,
33 mail, 36 rune-loadout and 39 existing CP-shop checks. The silver source guard,
three producer-audit and twelve overlay checks also pass: 545 checks on the
final revision. A silently suppressed receipt is tested as a failed transaction,
so an unrecorded charge cannot commit.

Validated assembly SHA-256:
`0D72E61630F7CBA472C01D2A5FCA457516B4C983E47EF56355304CDB4AE5D9AC`.

Native 6711 buy/multi-buy, balance display and learning/achievement presentation
still require client acceptance. The five historical learning requirements
omitted from the published pharmacist table remain unresolved.
[M6.28](SILVER_PERSISTENCE.md) adds durable silver preimage checks and atomic
online/offline credits. Non-book shop transactions remain open; auction settlement is subsequently
implemented in [M6.29/M6.30](AUCTION_INTEGRATION.md), with native acceptance and
legacy recovery still pending. No live deployment,
client installation or public patch is performed here.
