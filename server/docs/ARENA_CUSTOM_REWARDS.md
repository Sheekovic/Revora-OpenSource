# Arena custom milestone rewards

This source participant is intentionally inactive until the owned match
coordinator replaces legacy payout writers. No production initializer or match
callback invokes it yet. It records and pays the existing Revora custom policy;
these are not claimed original Conquer Online rewards.

The hundredth verified win on a Pacific day snapshots 500 Honor, configured CPs,
silver and RVPs, and six unbound material units: one 3009002, two 3004243, two
3004244 and one 3004245. Defaults are 5,000 CPs, 250,000 silver, 50 RVPs and VIP 5.
No direct P7 soul is granted. Resulting P7 packs enforce bound souls when opened.
The optional fifty-loss policy awards 500 Honor only and stays disabled by default.
Its eligibility now checks the losing recipient's VIP, correcting the old writer's
check against the winner. Loss count comes from actual lost result receipts.

## Ownership and frozen decisions

Record requires the registered imported match's terminal transaction, both owner
locks, canonical sessions and committed-in-transaction result statistics. Counts
come from immutable results on the match's saved Pacific day, never RAM counters.
The exact milestone records an eligibility decision even if VIP is too low or the
loss policy is disabled. Decisions are unique per owner/day/milestone. Policy,
minimum VIP, recipient VIP, amounts, item types and originating run are saved.
Later VIP/configuration changes do not rewrite either eligible or ineligible rows.
An unsaved decision aborts the parent result and both statistics updates.

Pay takes the durable owner lock and one eligible unpaid decision. It checks both
Honor mirrors, saved currencies, silver preimage and bag capacity. Honor, CPs,
silver, RVPs, all six separate item units and payment receipt commit together.
Insufficient bag space or numeric capacity retains the entire reward. Failure
rolls back all writes and fences the session. After commit, currency, inventory
and solo/team statistic packets are projected. CP projection uses the existing
ApplyCommittedConquerPoints method, avoiding an extra persistence write.
No eligible loss reward creates an item or grants extra currencies.

## Evidence and remaining integration

The isolated fixture runs registered import/start/end/result transactions for
ordinary non-GM participants, including 99/100/101 wins, 49/50 losses, differing
winner/loser VIP, policy-disabled and next-day policy-enabled decisions. Injected
entitlement failure rolls back the hundredth result. Delivery checks full-bag
retention, simultaneous requests and failed item, entity, Honor-mirror and paid
receipt writes. Production statistic/item loaders verify the delivered values.
Retry after injected payout failures clears only the fixture fence; this is not
a full production reconnect proof. Match outcomes are fixture-driven, not native
played combat. Test totals and log location are in QUEST_EVENT_STATUS.md.

Activation, disconnect/forfeit/restart result ownership, automatic pending reward
recovery and daily ranking settlement remain open. Legacy custom payouts still
operate on their old path until coordinated replacement. Native Honor/currency
refresh, actual earned Honor spending and live restart acceptance remain open.
