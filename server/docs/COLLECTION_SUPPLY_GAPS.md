# Remaining collection supply gaps

2026-09-27 follow-up: [official acquisition mechanisms and loaded-ID evidence](research/COLLECTION_EVENT_ACQUISITION.md) now identify original event leads for the four remaining collections. Original-versus-Revora supplier policy is pending; no new earning route is claimed.

Follow-up: the Big Fan lead below is now implemented and validated through the
[atomic jersey earning route](EURO_JERSEY_PURCHASES.md). It is code-validated;
the other four collection supply gaps remain. The dated findings/counts below
record the pre-implementation audit and are not the current completion totals.

Reviewed 2026-09-25 at source checkpoint `d974c9e`. This bounded audit covers
production C# item references, the shop loaders and the installed runtime
`bin/Debug/database/shops` INI/TXT/DAT files. It is not a claim that no historical
or externally provisioned supplier ever existed. No proprietary catalog files
were added to Git and no live database or service was changed.

| Reward | Required designs | Current evidence and remaining work |
| --- | --- | --- |
| Saint Rider | Permanent 200553–200560 | Vesper supplies 200559/200560 with earned-purchase evidence. No explicit grant references or shop-file matches for 200553–200558 were found in the audited scope. Recover a real producer for those six variants before claiming the set complete. |
| Big Fan | One Euro jersey design plus 200563 or 200564 | Vesper sells 200564. Two jersey designs appear in the actively loaded EShopV2 catalog; six Legend editions occur in a legacy RVP list. Prove the ordinary shop request, catalog price, payment and persistence for a jersey and combine it with an earned mount. |
| Euro Collector | Six distinct designs, accepting either edition per design | Legend editions 193775/193785/193795/193805/193815/193825 are listed at 3,000,000 each in the legacy RVP list. A populated file does not establish an active shop. The two EShopV2 designs alone cannot satisfy all six. |
| Immortal Fate | Celestial Fox 200595 | Old NPC 900046 option 45 exchanges two FirstCreditPoints; NPC 900035 option 45 exchanges 100 each of eggs 710060–710064. These split debit/grant branches do not prove an ordinary, durable earning route. Establish currency/material acquisition, actual NPC access and atomic exchange before counting either. Fantasy fox 200596 is a different requirement. |
| Slam Dunk Leader | 194695/194705/194675/194685 | Old credit exchanges and the legacy RVP catalog mention 194695/194705. No explicit source or installed shop-file matches for 194675/194685 were found in this scope. 194715/194725 also appear in the legacy list but are not the required IDs and cannot be substituted. |

## Euro shop evidence

`TitleRequirements.cs` accepts six pairs:
193735/193775, 193745/193785, 193755/193795, 193765/193805,
192655/193815 and 192665/193825. Owning both editions of one pair still counts
as one design.

`ServerBase/Constants.cs` points `RaceShopPath` to `SlayerPointshop.ini`.
`Database/Shops/RevoraPointhop.cs` parses this file into `REVORAPointhop.Shop`.
The production-source reference search found its declaration/loader and the
purchase branch, but no direct caller of `REVORAPointhop.Load()`. The purchase
branch in `Network/GamePackets/ItemHandler.cs` returns when that static shop is
null. This supports an unproven/inactive-by-default route, not a completion claim.
It does not rule out external initialization absent from the audited source.

If restored, that branch also needs repair: it adds the item before calling
`TrySpendRevoraPoints`, ignores the debit result, and tells an insufficient buyer
they lack Champion Points despite checking RVPs. Enabling a loader alone would
therefore be insufficient and could expose a non-atomic purchase path.

`EShopV2File.Load()` does have a startup call in `Server.cs`. Its installed list
contains 192655 and 192665; those are two of the required six designs. The next
independent acquisition task is to verify one of these two through the actual
shop handler and combine it with Vesper's 200564, without reviving the dormant
legacy RVP shop or inventing an all-six acquisition claim.

## Credit and egg exchanges

The explicit production assignments found for FirstCreditPoints are account
loading, its entity setter and administrative chat grants. This search did not
prove a normal player earning source. Administrative provisioning is not evidence
for ordinary collection acquisition. NPC placement, egg acquisition and the old
exchange flags also require verification; this audit did not mutate them.

## Runtime catalog provenance

SHA-256 values at inspection:

| Local runtime file | SHA-256 |
| --- | --- |
| shops/SlayerPointshop.ini | `95F58C00B8C6B68CDF3CFD34A27B7D50CD5173175EB7B05D69B2F137BDFF9700` |
| shops/Shop.dat | `9CBF67CF0EDF03A781A50D50F581544878BA352A3019CC91CB14B411F21552F3` |
| shops/emoneyShopV2.ini | `0F81B11689546FEE470C04CE5114778B8E2E35B1B0378087743C8D30A4DC56D6` |

All five rewards remain predicate-only. Current totals stay at 31 code-validated,
35 predicate-only and six unimplemented; native acceptance remains pending.

## Remaining supplier decision (2026-09-27)

A focused current-source check found710060-710064 grants only in administrative
chat and commented MonsterTable drop branches. Prizewell900035 is parked on6000
and remains in TwinCityPlaza's closed-desk list. Its old Celestial Fox exchange
therefore does not establish ordinary earning. The owner has been asked whether
to supply the missing permanent designs for Saint Rider, Euro Collector, Immortal
Fate and Slam Dunk Leader through recurring events or existing-price Vesper
purchases. No answer, supplier activation or earning-rule change is assumed.
