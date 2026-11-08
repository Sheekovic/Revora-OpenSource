# RevoraMaster lifetime VIP transaction

2026-10-01. This repair is a prerequisite for the retained career's ordinary
sign-in route: sign-in requires level140, second rebirth and VIP5. The separate
collection fixture supplied eligibility; its VIP is not copied into this career.

Previously ApplyMaxVip charged CPs, changed three VIP properties and saved the
character separately. A persistence failure could leave payment without the
upgrade. The service now uses the existing owned VIP persistence API and one
transaction for the conditional CP debit, all three VIP fields and a purchase
receipt. Only after commit are the balance and VIP projected to the client.

A near-NPC quote records the configured price and level for one minute. Confirmation
requires the same ordinary session, proximity and unchanged configuration. The
quote is consumed before execution; repeated confirmation cannot spend it twice.
Other dialogue invalidates it. Insufficient funds and expired/changed quotes do
not charge. An equal permanent or higher VIP is not charged or downgraded. Equal
but temporary VIP may buy permanence. Prices and configured maximum are unchanged.
Persistence ambiguity fences the session for normal reload, rather than guessing
a refund or applying only part of the upgrade.

## Private retained-career evidence

career-vip-fault, career-vip-earn and career-vip-reload pass on
C84847CD37D507F1C47D07BAC2F94F0241DCDF2C6FCBBFB5C097EC9A7F204D4B.
The retained ordinary character matches its previous newcomer checkpoint before
starting. A direct confirmation without a quote preserves its exact state. A
temporary trigger refuses the VIP update after the attempted CP debit: rollback
preserves the prior CP/VIP/career snapshot and no purchase receipt survives.

After restart, normal offer/confirmation consumes the actual configured price
from previously earned CPs and grants lifetime VIP. One receipt records the exact
balance delta. Repeated confirmations and a fresh offer for already-permanent VIP
do not charge. Exact full game/PostgreSQL restart preserves the nested career,
purse, items and VIP state with that single receipt. The checkpoint is
revora_career_vip_probe. The character still needs normal level progression before
sign-in; no level, VIP or rebirth eligibility is supplied by this helper.

The build retains existing warnings; helpers compile with warnings as errors.
Native display, live deployment and the full320 career remain pending. Private
calendar/proximity and the earlier career fixture qualifications still apply.
