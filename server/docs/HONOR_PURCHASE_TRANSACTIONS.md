# Honor Store purchase transactions

Implemented 2026-09-21 as a prerequisite of the Sword Master earning route.
The production item-buy dispatcher now delegates Honor Store 6000 purchases to
`HonorPurchases`. Every eligible ordinary player uses the same handler; there
is no GM/account allowlist. Prices and stock come from the loaded HonorShop.ini.
Sword Soul (720842) remains 8,000 Honor. Each accepted request buys one unbound
item, matching the prior quantity convention. The Sword Master summon itself
remains unavailable until its owned encounter/reward route is implemented.

## Ownership and persistence

The player must be canonical, fully loaded and outside trade/booth activity,
with owned solo/team Arena statistics and matching live Honor mirrors. The
player inventory lock serializes spending. Existing solo Arena saves already
used that lock; team Arena saves now do too, reject detached statistics and
refuse persistence-fenced sessions. Existing live match Honor increments and
online daily resets use the same lock so they cannot overwrite a concurrent
spend with an earlier read/modify/write result. Their reward amounts are unchanged.

Solo Arena is the existing login authority for Honor. Legacy match rewards may
be accumulated in its canonical in-memory statistic before periodic persistence.
The purchase first flushes that statistic under the owner lock. This saves earned
funds independently of the later debit: if the purchase fails, recovery retains
the saved earned balance. This is not a replacement for an atomic match-result
ledger; unrelated tournament rewards remain outside this purchase transaction.

The purchase then locks the durable player/Arena row, verifies the saved Honor
and bag count, and commits both solo/team current-Honor balances, the new owned
item and `revora_honor_purchases` receipt in one transaction. Every required write
must affect one row. Receipt fields include item UID, type, price, before/after
Honor and UTC purchase time. Item/balance packets and the inventory cache update
only after commit. Failure fences the session for ordinary reload.

A recent receipt suppresses the same item's repeated requests within one second,
including after reconnect. This matches the existing shop anti-double-click
convention; the native request has no purchase nonce, so it is not indefinite
request-replay identification. A later intentional purchase charges again.
Full bags, insufficient Honor and unlisted items do not charge. A stale durable
bag count aborts before any debit/item receipt can commit.

## Validation and limits

The isolated build and all 488 Experience/progression checks pass. Honor cases
use the actual loaded shop price and public item-buy handler, ordinary characters,
concurrent purchases/solo/team saves, same-second suppression, a later purchase,
full bag/unlisted item refusal and detached team-save rejection. Failure fixtures
suppress item insertion, solo debit, team debit and receipt insertion separately;
all roll back the purchase while retaining earned funds. Production item and
Arena recovery readers verify unbound Sword Soul and remaining Honor. A stale
bag case cannot buy. Existing level-up, skills, mentor, Dark Horn, weapon-book,
silver and shop regression coverage remains in the same suite.

Honor funds are fixture-provided, including a pending canonical earned balance;
this does not claim a complete played Arena-to-Sword-Master journey. Native
Honor Store presentation, actual match earning, full Host restart and the owned
house encounter remain acceptance work. No live database, client or running
server changed. The overall Sword Master milestone remains open.
