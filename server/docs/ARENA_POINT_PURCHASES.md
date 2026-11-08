# Atomic Arena point purchases

The existing solo interactive handler and both solo/team PacketHandler purchase
branches now call ArenaPointPurchases.Buy. Thread initializes the purchase receipt
schema alongside Honor purchases. This is wired source for all eligible ordinary
players in the next deployment; it does not depend on activating the new result
coordinator. The production server has not been rebuilt/restarted in this batch.

The preserved Revora price is 9,000,000 silver for 1,500 shared Arena points when
the current balance is at most 1,500. No new retail price claim is made. Canonical
living players need matching solo/team identities and point mirrors, no active
fight/invitation, no trade and no booth. If durable recovery schemas are active,
an unresolved admission also blocks purchase after the live group is gone.

Under the owner lock, existing unflushed legacy statistics are saved before the
purchase transaction; managed saves preserve their durable values. The transaction
locks the owner row, checks a persisted one-second purchase interval, verifies both
point preimages, updates both mirrors, saves silver through SilverBalance and
writes a receipt. Failure rolls back all purchase changes and fences the session.
A stale silver/point preimage cannot be silently overwritten. After commit, silver
and both Arena statistic packets update the client caches immediately. Purchased
points do not count as match wins or achievement evidence. Existing legacy earned
statistics flushed before a failed purchase are not rolled back as purchase work.

A zero balance can buy again at 1,500 after the interval, reaching 3,000; above
1,500 no purchase is accepted. Concurrent duplicate requests and clock rollback
cannot repeat a debit during the persisted interval. This is bounded duplicate
protection for a protocol with no request identity, not permanent deduplication
of arbitrarily delayed requests.

The disposable-schema fixture invokes the existing solo interactive handler for
concurrent ordinary-player requests, reads the production statistic loader,
checks threshold/clock behavior and injects failures at both point rows, silver
and the receipt. It also covers insufficient funds, active fights and unresolved
admission closure. The other packet branches share the same call and are checked
by source/build; native solo/team button acceptance and real reconnect remain
open. See QUEST_EVENT_STATUS.md for the passing total. Ignored log:
obj/chi-refill-validation/arena-point-purchase-tests.log.
