# RevoraMaster mount-kit transaction audit

## Confirmed source behavior

Inspected after commit 4cebdef. This is source evidence, not a reproduced live
incident. The source repair and its validation are recorded below.

`Game/Features/RevoraMasterSystem.cs` exposes the mount-kit quote through option
4 and purchase through option 14. ApplyMountKit checks the five catalog entries
and live bag capacity, then calls Charge before GiveMountKit. Charge uses
Entity.TrySpendConquerPoints, which persists its own currency operation under
PurseLock. GiveMountKit independently calls GiveMaxedItem five times, each using
Inventory.Add with CreateAndAdd and a separate ConquerItemTable.AddItem write.

The catch in ApplyMountKit returns the entire configured CP price without
removing any previously delivered items. Therefore the sequence cannot ensure
all-or-nothing payment and delivery if a later item creation fails. A process
interruption between the charge and all five creations also has no purchase
receipt from which to determine the complete intended delivery.

The mount-kit quote does not create a session-bound offer or snapshot its price;
option 14 directly calls ApplyMountKit. The method does not serialize capacity,
payment and delivery under the character item lock. This is a local service
finding; it does not claim the outer NPC dispatcher permits remote access.

## Existing product contract to preserve

- Existing configured CP price and ordinary eligible player access.
- Five bound bag items: Steed 300000, Fan 201009, Tower 202009, Crop 203009,
  Wing 204009; +12 and damage reduction 7.
- Fan uses two Super Thunder gems, Tower two Super Glory gems, Wing one of
  each; Crop has no sockets. Preserve existing steed appearance and durability.
- Five real bag slots, excluding stored wardrobe items. No automatic equipment.

## Required repair and validation

1. Bind a short-lived offer to the canonical player session, quoted price and
   relevant NPC context; consume it once under the item lock.
2. Validate saved bag identity and saved currency before mutation. Persist the
   exact CP debit, all five owned item rows and a durable purchase receipt in
   one database transaction.
3. Project committed payment/items only after commit. On projection failure,
   fence and recover from saved state; do not refund a committed purchase.
4. Exercise the ordinary NPC flow, absent/expired/cancelled offers, duplicate
   clicks, trade/booth/session guards, full bags and stored cosmetics.
5. Suppress each item insertion, currency debit and receipt write independently;
   require rollback of the complete kit and payment, then test a safe retry.
6. Verify exact item properties, immediate state, production reload and the
   earned-purse acquisition path before describing the service as complete.

Native presentation and live deployment remain separate pending gates. Other
RevoraMaster services are outside this mount-kit audit.

## Atomic purchase repair

RevoraMaster now initializes a dedicated purchase-receipt table and binds each
mount quote to its player-session object, price and two-minute expiry. The NPC
dispatcher observes dialogue before early returns, clearing the quote on
cancellation, other dialogue or invalid service context. Confirmation consumes
one offer under ItemSyncRoot and checks the ordinary canonical session, nearby
NPC, trade/booth state, catalog, CP balance and five available bag slots.

Under the saved owner lock, exact saved bag UIDs (excluding wardrobe storage)
must equal live bag UIDs. The CP debit, all five item rows/properties and the
receipt commit together. Postcommit installation uses committed currency/items;
exceptions fence the character for reload and never refund committed delivery.
Other RevoraMaster service transactions remain outside this change.

The first reload test exposed an existing Steed representation detail:
ConquerItem.NextRed aliases Bless, and the item loader projects NextSteedColor
over that byte. The transaction now persists the corresponding packed value as
well as the existing kit fields, preserving the pre-reload representation.
This is source/loader evidence, not a new verified native packet or combat claim.

The isolated build and 484 combined collection/acquisition checks pass. The
existing boss-funded ordinary character, already holding stored cosmetics,
buys the kit without additional supplied CPs. At the tested configured price of
40,000 CPs its balance changes from 975,360 to 935,360. Fault injection suppresses
each of five item insertions, item-property update, debit and receipt: every
attempt rolls back the entire purchase, then production reload permits retry.
Tests also cover missing/cancelled/expired quotes, trade and distance rejection,
insufficient bag slots, concurrent confirmations and exact paid-item reload.
Sockets, appearance and durability survive production item loading.

The trade test initially exposed an offer surviving the outer dispatcher's busy
return; observing invalid context before that return corrects it. Additional
booth, stale-session, changed-price and postcommit projection fault scenarios
were not directly exercised by that initial batch; the follow-up below covers
them. Native UI/combat acceptance and live
deployment remain pending. Owner WIP was present in the isolated build; no live
server, schema or client was changed.

## Recovery and service-context follow-up

The isolated build and 489 combined collection/acquisition checks pass. Added
ordinary NPC tests invalidate a quoted offer when the player opens a booth,
loses canonical session ownership, or the configured price changes. Restoring
the context does not resurrect the consumed/invalidated quote or charge CPs.

A normally unset internal callback permits deterministic failure immediately
after commit and before client projection. With that fault injected, the player
is fenced, all five items and the second intentional purchase receipt remain
saved, and the CP debit remains committed. Production reconnect restores the
whole kit; repeated confirmation neither charges again nor refunds the purchase.
The same boss-funded character pays for this second deliberate purchase.

This tests the postcommit recovery boundary, not every possible failure position
within packet transmission or item installation. The callback is restored by
the fixture. No production listener or client was restarted or deployed; native
presentation and live acceptance remain pending.
