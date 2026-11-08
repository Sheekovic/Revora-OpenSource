# Owned house item boxes

Source implementation, 2026-09-20. Ordinary players can buy ItemBox 721189 from
FurnitureNPC 30161 at Twin City 339,528, select it inside their owned house,
place it, and use its separate 60-slot storage. This is not deployed or accepted
in a running genuine client.

The furniture service's third page offers an Item Box for 100,000 silver. This
remains the storage-box policy after the owner's decorative furniture change to
2,500 CPs per piece; it is not a claimed retail
price or the zero-price catalog fields. Confirmation has the same two-minute
offer, exact payment, once-only receipt and inventory-slot checks as the other
furnishings. Fixture starting silver is supplied; these tests do not establish
a complete silver-earning journey.

## Placement and ownership

Level one allows one box; levels two through five allow two. Box counts are
separate from decorative furniture capacities. The documented housing research
supplies these limits. Existing legacy type-4 actors count conservatively against
box capacity, but this service does not manufacture ownership for old actors.

Selection holds an exact owned, unlocked, permanent item's UID, type, stack and
binding. The bounded native placement request must match model 820 and direction
0-7. A regular type-4 NPC is used, rather than a decorative terrain scene.
The approved-client evidence and remaining interpretation limits are recorded in
[the storage protocol ledger](HOUSE_STORAGE_PROTOCOL.md).

Placement checks house ownership and capacity under the saved owner lock, then
commits item consumption, NPC creation and the durable ownership/material record
together. The shared noncycling furniture sequence allocates the actor/storage
key. Existing item or storage state under that key refuses the placement.
Box placement alone does not award decorative-furniture achievement 10101.

Boxes use Revora's existing ordinary-NPC three-by-three clearance policy. All
cells must be walkable, free of NPC/player overlap and away from house exits.
They use the independent furniture blocker flag so removal cannot open base
terrain or erase another furnishing's blocker. This footprint is a server policy,
not a newly verified native terrain mask.

## Access, transfers and removal

The bounded action-0 click validates the actual owned box in the current owned
house, within six tiles, then selects it and sends the existing warehouse-window
command. A configured secondary password must already be unlocked; otherwise
the established password window is requested and the player must reopen the box.
Rendering and the password-window sequence still require native acceptance.

Each registered box is tagged as house storage. Requests must use kind 20.
Listing and each transfer check the selected actor, map, proximity, password,
session/trade state and durable placement against the NPC row. Deposits and
withdrawals retain exact item identities/quantities and commit before runtime
projection or success replies. The transfer's ownership check runs inside the
same owner transaction as the item-location change. Town, VIP or forged remote
requests do not bypass these checks for a house box.

Removal checks all persisted contents under the actor key while holding the
owner lock. Nonempty boxes remain in place. Empty removal commits the retained
historical record and NPC deletion together, then removes the runtime container
and actor. There is no item/currency refund. Concurrent deposit/removal has only
two valid outcomes: a surviving box containing the item, or a removed box with
the item still in the player's bag.

Before item hydration, saved active boxes are reconstructed from matching house,
NPC and ownership records. Invalid profiles, excess capacity, foreign-owned
contents or orphaned box items stop projection and require reconciliation; they
are not silently dropped. Existing legacy house bucket 100 and historical sash
collisions are not reassigned by this feature. While housing is enabled, new
deposits into bucket 100 are refused so the default bucket cannot bypass paid
box placement. Existing items remain withdrawable through the legacy route;
its broader historical authorization/reconciliation gates remain open.

## Validation and release gates

The isolated build and 444 housing/storage checks pass. The box route covers
ordinary paid purchases with concurrent confirmation, level-one and level-two
limits, separate furniture capacity, placement replay, separate stored stacks,
wrong-box/foreign/kind/password/map refusal, cold map and item reconstruction,
nonempty removal, whole-stack withdrawal, transfer rollback/reload, sixty slots,
deposit/removal concurrency, each placement participant failing, removal failing,
and corrupted ownership/orphan recovery. Negative-case materials are supplied.

The shared Guild season regression passes 347 checks and Magnolia passes 838.
The 82 furniture/click and 193 warehouse protocol checks pass. All twelve scene
profiles still match their source masks, and three scene plus three achievement
audit tests pass. The native storage audit has 65 instruction assertions. These checks do not prove native presentation, a Host restart,
legacy-data migration, height-based movement or client interaction acceptance.
Warehouse item-record display fields also remain subject to the native acceptance
limits in the storage protocol ledger; saved-item integrity is not proof of every
rendered item attribute.

Initialization extends the two owned furniture item-type constraints to include
721189 in one transaction. No production schema, live data, Host, client files
or public patch was changed. Before deployment, back up the relevant tables,
review existing legacy houses/boxes, and complete ordinary-client purchase,
preview, click, password, storage, reconnect and empty-removal acceptance.

## Retained storage and prestige startup regression (2026-10-02)

The retained career's bag and seven banks were full. The ordinary player trades
its unbound permanent-P6 Frost Blade to its existing spouse, preserving ownership
in the nested snapshot, buys an Item Box721189 for100,000 silver from Furniture
30161 options22/40, enters its existing house through House Admin30156 option2,
and places the box through the normal item/2030 handlers. Opening uses2031 and
transfers use sealed1102 requests. Nineteen existing uninscribed items move into
the box, leaving twenty bag slots free for the remaining quest chains.

The store phase passed, but the next full startup failed in
`PrestigeRanking.LoadSavedPlayers`: its equipped/rune filters read warehouse IDs
as UInt16. House box actors occupy1,800,000,000?1,899,999,999, so an ordinary saved
deposit triggered OverflowException before the server was ready. Both filters
now read UInt32, matching persisted storage identity width and retaining the
zero-warehouse eligibility condition. Unrelated cosmetic-wing work in this file
is excluded from this change and from the private validation build.

A committed-source overlay build passes with existing warnings (warnings were not
treated as errors for this legacy build). Only the stopped private candidate was
refreshed, preserving the prior executable and manifest. Candidate SHA-256 changes
from `07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`
to `34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
The same database was retained: no rollback or deletion of the saved box/items.
`career-retained-box-reload` now passes through real startup, exact nested snapshot
comparison, nineteen restored items, withdrawal/redeposit of the same UID and
unchanged duplicate deposit. Probe `revora_retained_box_probe` ends at step3.
The original store phase passed on the prior binary; it was not repeated after
purchase. An initial fixture compile failure accessing an internal HouseBox field
was corrected to use the registered actor key before the store phase executed.

This is an ordinary-player storage prerequisite for the remaining equipment
quests. Native box UI and live deployment remain pending. No live restart or
public release was performed, and no additional quest completion is claimed.
