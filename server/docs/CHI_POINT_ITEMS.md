# Leftover Chi pills and packs

M6.56b9c, 2026-09-22. Server implementation; not deployed.

Ordinary 1009/action-4 and direct `UseItem` of the six Chi consumables with
a catalog non-admin producer, plus five catalog-only pills, go through
`ChiPointItems`. All eleven legacy cases fail closed. `ChiWallet` remains
the persistence owner. There is no invented 4,000 cap. Checked `uint`
overflow refuses without source loss or payment. Messages are sent only
after the committed wallet and source are installed.

REVIEW-326: the four Way of Heroes Chi prizes are catalog contracts only while
their stage predicates remain unimplemented. Fresh stage issuance is refused;
held packs remain usable. Shop/exchange pill routes are unchanged.

729480 is the installed SalesPamphlet catalog identity and is not a Chi
handler. `DropItemTable` is not activated.

| ID | Catalog | Chi | Legacy System text | Constructor | Non-admin issuance |
| ---: | --- | ---: | --- | --- | --- |
| 729481 | VitalPill | 500 | `You received 500 Chi Points!` | named `Color.Red` | exchange shop row 620 |
| 729242 | Vital Pill | 500 | `You recieved 500 Chi Points` | named `Color.Red` | EShop V2 stock and exchange shop rows 7/26/477 |
| 3005126 | NormalChiPack | 500 | `You received 500 Chi Points!` | two-argument System | Way of Heroes `StageGoal` prize; issuance currently refused |
| 3005127 | MediumChiPack | 1000 | `You received 1000 Chi Points!` | two-argument System | Way of Heroes `StageGoal` prize; issuance currently refused |
| 3005128 | SeniorChiPack | 2000 | `You received 2000 Chi Points!` | two-argument System | Way of Heroes `StageGoal` prize; issuance currently refused |
| 3005129 | ExtremeChiPack | 3000 | `You received 3000 Chi Points!` | two-argument System | Way of Heroes `StageGoal` prize; issuance currently refused |

The two-argument `Message` constructor already paints System text red, matching
the named `Color.Red` constructor. 729242’s misspelling and missing exclamation
are preserved.

## Catalog pills with no ordinary producer (owned consumers)

These five IDs are installed catalog identities and now use the same
transaction. Fixture injection proves the consumer. It is not an earned path.
`DropItemTable` still lists four of them and stays unreferenced. No shop,
exchange, or Way of Heroes stock was added.

| ID | Catalog | Chi | Success text | Constructor | Ordinary producer |
| ---: | --- | ---: | --- | --- | --- |
| 3000051 | SmallChiPill | 5 | `You received 5 Chi Points!` | named `Color.Red` | none; inactive `DropItemTable` only |
| 729476 | ChiPill(100) | 100 | `You received 100 Chi Points!` | two-argument System | none; inactive `DropItemTable` only |
| 729477 | ChiPill(200) | 200 | `You received 200 Chi Points!` | named `Color.Red` | none; inactive `DropItemTable` only |
| 729478 | ChiPill(300) | 300 | `You received 300 Chi Points!` | named `Color.Red` | none; inactive `DropItemTable` only |
| 729479 | ChiPill(400) | 400 | `You received 400 Chi Points!` | named `Color.Red` | none |

729479’s leftover text said 5 while `AdjustChiPoints(400)` paid 400. The
owned message reports the committed 400. One source unit is still consumed.
A zero or single stack releases its slot; a stacked remainder stays, including
its bound flag. No output slot is required, so a full bag can still pay.

`Initialize` replaces `revora_chi_item_uses_item_type_check` so the table
accepts the original six IDs and these five. Historical receipts stay.
Repeated `Initialize` is safe. Changing only `CREATE TABLE IF NOT EXISTS`
would leave an existing six-ID check in place.

Downstream Chi spend/save/reload stays on `ChiWallet` / `ChiTable`. A pending
wallet revision still defers use. A fenced session does not let a later save
overwrite the committed award. Direct daily P7 is unchanged.

## Remaining work

Catalog-only Surprise box 722178, nested gear packs and other leftover
split-writes remain open. 721330 stays unresolved. Gem option 6 stays
menu-only. Native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
