# Way of Heroes Dragon Soul claims

Implemented and privately validated 2026-10-02; not deployed live.

| Stage | Equipped effect required | Bound reward |
| --- | --- | --- |
| 604 | P1 or higher, unexpired or permanent | 723694 stabilization stone |
| 807 | P4 or higher, unexpired or permanent | 3005127, 1,000 Chi on use |
| 908 | P6 or higher, unexpired or permanent | 3005128, 2,000 Chi on use |
| 1008 | Permanent P7 | 3005129, 3,000 Chi on use |

These are the installed stage definitions. Stage 604 opens at first rebirth level
101; the other three at second rebirth level 121. The catalog's requirement 3
admits temporary or permanent effects, while requirement 2 requires stabilization.
Startup checks pin these exact definitions. Other nonzero-requirement stages
are not enabled by this change.

The existing atomic claim service now locks saved item/effect rows and checks
the selected equipment bank's core slots. Items must match the current loaded
equipment identity and the slot/category, be permanent single items outside
warehouses/wardrobe, and not suspicious. The installed artifact compatibility
service must accept the soul and target. Effect phase must match the catalog;
duration zero is permanent, while temporary duration must be positive and its
saved timestamp unexpired. Future-dated temporary effects do not qualify.
Duplicate purified slot/effect rows fail the claim. Bagged gear and the inactive
equipment bank do not contribute. This is not the ambiguous bonus-total rule.

The existing owner lock, saved opening gate, inventory/session checks, bound
plus-0 reward, saved progress and unique claim receipt are preserved. No effect
is consumed by a claim. Existing purification and stabilization services still
own material payment; Chi packs use their existing atomic consumer.

## Composed private validation

`tools/CheckCandidateSoulClaims.cs` adds `career-soul-claims-earn` and
`career-soul-claims-reload`. One ordinary non-GM character receives supplied level
140, second rebirth, initial CPs, qualifying boots and a compatible bound P7 soul.
The initial gear/soul supply is a fixture, not a new player earning route. Earlier
material earning evidence remains in [artifact stabilization](ARTIFACT_STABILIZATION.md)
and the retained artifact journey; this batch does not repeat event soul drops.

The run buys Meteors at loaded CP-shop prices, packs them through MillionaireLee,
and applies the soul through the normal purification request. It checks exact
Meteor-value consumption and soul removal. Bagged purification cannot claim.
After normal equipment, an expired claim clock still refuses every stage.
Restoring the clock admits the three temporary-eligible milestones once under
eight concurrent requests each; the permanent-P7 stage remains refused.

The awarded small stone and purchased large stones stabilize the equipped soul
through the normal request. The selected points round up to the required amount;
existing behavior consumes all selected stones, including excess points. The
now-permanent effect still cannot claim from the bag. Re-equipping admits the P7
reward once. All three Chi packs consume once under replay and total 6,000 Chi.
Replayed purification/stabilization requests with consumed materials do nothing.

The initial fixture used a legacy Inventory.Add overload that ignored binding
and created stack zero. Purification refused it before any claim. The fixture
now supplies explicit bound, single-item objects. Only its two existing supplied
inputs were corrected under exact owner/UID/preimage checks; bought materials,
effects and rewards were not reset or fabricated. The production Inventory owner
worktree changes were preserved, not included in this batch.

Both composed process phases passed, including game and PostgreSQL stop/start,
exact scoped profile/purse/item/effect/progress/claim/consumer snapshots and
replay refusal. The isolated committed-source-overlay build passed with existing
legacy warnings; the harness compiled with warnings as errors. Private game
SHA-256: `7D21300F1CC5675D75674625424C6B0D51DE38FF0E78227285B55FA7CC0C18D7`.
Logs: `arena-process-career-soul-claims-{earn,reload}.log`. Both normal game closes
completed and PostgreSQL stopped. Previous private executable/manifest retained.

The P7 test exercises higher-phase eligibility for the lower milestones; it is
not a separate exact-P4/P6 threshold matrix or an alternate-bank journey. NPC
proximity and expiry clocks are controlled, packets are in-process and initial
qualifications/materials are supplied. Authenticated/native acceptance, natural
input earning for this character, interrupted commits and deployment remain
pending. The main retained career checkpoint and achievement totals are unchanged.
