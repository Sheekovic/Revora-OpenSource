# Player booth payment and achievement

Achievement 10128 is buying an item from a booth, as identified in the installed
hash-bound Achievement.dat catalog used by the achievement inventory.

Ordinary player-owned booths now settle silver through the existing ordered
buyer/seller lock and item-transfer transaction used for CP sales. Previously
silver debit, seller credit and inventory movement were separate operations.
The transaction validates saved silver preimages, rejects insufficient funds or
signed database overflow, saves both balances and transfers the exact owned item.
It also rejects a saved wardrobe item even if the cached item claims otherwise.
Postcommit updates install both balances and move the item; failure fences both
sessions for reload. Silver prices retain their 64-bit representation. CP prices
remain limited to the CP purse range.

Achievement 10128 commits with payment and ownership transfer, only for the buyer.
The first completion retains the transferred item UID as its source key. Failed
payments award nothing; replay after sale cannot pay or transfer the same listing
again. Subsequent purchases retain one historical achievement per character.

The fixtures use ordinary non-GM accounts with supplied funds and a tradable
Meteor. They call normal booth-buy dispatch, inject failure at each database write,
check external purse conflicts, insufficient funds, overflow, full bags, bound
items and concurrent requests, then use production character/item login to restore
successful silver transfers and the achievement. These prove transaction behavior,
not a complete hunt-to-silver-to-purchase economy or native booth UI acceptance.

Synthetic system-stock booths in Booths2 retain a separate legacy path and receive
no new achievement producer in this change. Their sale transaction and listing creation/cancellation remain separate audit
work; player booth proximity is addressed below.
No historical purchase backfill, live deployment or client patch is included.

Validation (2026-09-25): isolated Debug/x86 build and full purse review suite
passed with zero review failures, including fourteen added silver-booth assertions
and the updated CP reload assertion. The runner reports 407 prerequisite checks;
its separate review assertions are not all included in that counter. Five
achievement inventory tests pass. Unrelated owner edits were present in the
build; this is not a clean release artifact.

## Active booth and proximity (2026-09-25)

Player purchases require the exact requested booth to be the seller's current
registered booth, with its spawn owned by that seller. Buyer and seller must
share the same live map object and map ID; the booth map must agree. Buyers may
purchase through the existing screen boundary (Constants.nScreenDistance = 19).
The seller must remain within one tile of the booth, matching the adjacent booth
spawn. These are explicit Revora access rules, not asserted retail wire behavior.

The transaction checks this context under both character locks and rechecks it
before commitment. Normal packet dispatch rejects stale/null booth owners before
dereferencing their state. The fixture checks the exact screen boundary and one
tile outside it, moved seller, wrong map/owner, unregistered booth, replaced booth
and null owner for both currencies. Rejections preserve funds, item ownership and
achievement absence. Listing creation/cancellation and system-stock booths remain
separate work. Native interaction and deployment remain pending.

Validation: isolated build, full purse review with zero failures (including 16
new booth-context assertions), and five achievement inventory tests passed.
Existing owner edits remained outside this commit and were present during build.

## Boss-funded earning route (2026-09-25)

The collection acquisition fixture now extends its original ordinary collector
through a real player sale. The seller's unbound Fan 201009 was bought from the
mall using its first daily-boss bounty and later displaced into the bag by the
mount-kit Fan. A second ordinary character starts with no purchase currency,
defeats its own scheduled boss and receives 1,000,000 CPs and 10,000,000 silver.
No funds or merchandise are injected for these trades.

The server's normal booth-open Data action and item-listing handler create the
stall and silver listing. Concurrent purchase requests transfer the original Fan
once for 100,000 silver and award the buyer's 10128. Production entity/item login
restores ownership and both purses. That buyer then opens a booth and lists the
same Fan for 1,000 CPs; the original seller buys it using its remaining earned CPs.
Repeated requests do not pay twice, and both characters retain one achievement
through another production login. Exact item UID, silver and CP balances are checked.

This closes the independently earned funding and merchandise prerequisite for
these two player-booth routes. Character qualification and boss combat damage
remain controlled by the fixture; native UI, combat balance, stall cancellation,
system-stock booths and live deployment remain unverified. The expanded fixture
compiles against the existing isolated server build and passes 588 collection
acquisition checks. No production source change was needed for this follow-up.

## Retained player-shop purchase (2026-10-02)

Existing spouse4294908901 buys one unbound Meteor through the ordinary CP mall
handler at its configured price, then opens a booth through sealed OwnBooth data
and lists that exact item for100,000 silver. The retained main purchases it through
the booth handler. Assertions verify the exact item transfer,100,000 silver debit
and credit, unchanged CP balances during the player sale, and buyer10128.
Replaying the same purchase changes neither ownership nor either purse.

Full game/database restart preserves the exact nested career, both players'
balances/items, purchased Meteor UID and buyer receipt.
`career-retained-booth-{earn,reload}` pass on unchanged candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`;
`revora_retained_booth_probe` finishes at step2. No item or currency was created by
the fixture for this sale; earlier funding retains its documented mixed provenance.
Both sessions are ordinary non-GM fixtures with controlled Twin City proximity.
This validates a silver booth sale, not native UI, authenticated networking or a
new CP-sale acceptance run. No production code, build or deployment changed.
