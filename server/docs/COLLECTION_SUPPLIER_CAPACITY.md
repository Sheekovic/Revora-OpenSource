# Collection supplier capacity

## Current release correction (2026-10-04)

The current installed mall differs from the historical earning candidate. Its
reviewed supplier union contains 250 garments but only 186 mounts: all 17 fixed
mount packs used by the earned collection route are absent from shop 2888.
The previously recorded 203-mount supply must not be attributed to these files.

`tools/ClientPatches/prepare_collection_mount_packs.py` privately stages those
17 packs in the server and native V2 mall catalogs at their unchanged 2,799 CPs.
It checks both item catalogs, preserves all existing stock and other shop blocks,
saves exact originals/hashes, and verifies an idempotent transformation. It does
not publish, replace live files, or change rewards/prices. The existing fixed
pack handlers supply the same permanent mount designs previously earned.

The production-loader probe now counts legitimate overlap between suppliers
instead of rejecting the updated garment catalog. All 17 staged packs load;
reviewed supply returns to 250 garments/203 mounts. Existing earning, payment,
opening and cold-recovery evidence is reused; this probe proves catalog supply
only. Native display/purchase and coordinated release remain pending.

[Exact evidence and release dependency](research/COLLECTION_RELEASE_SUPPLY.json).
Private artifacts: `obj/collection-mount-packs/manifest.json`, `originals/`,
`staged/`, and `staged-supply.log`. Preserve the shared garment-window stock when
including this restoration in the final release.


Latest follow-up (2026-10-03): the tested [Planet extension](PLANET_COLLECTION_EXCHANGE.md)
adds101 mounts and five earned weekend deliveries. The compiled supplier union
now supports200 garments/203 mounts, with260 credits for257 exchange designs.
This supersedes the102-mount/160-credit prerequisite findings below. All257
exchange purchases and exact fresh-process recovery pass; the combined200-mount
achievement and native/live gates remain open.

## Current source-catalog supply (2026-10-03)

The catalog correction supports203 mounts, but the actual reviewed supplier
union supports only102. Garments remain421 compatible/200 supplied. At least98
additional distinct mount designs need ordinary sources before a200-mount test
can succeed. The two newly counted Vesper designs explain the increase from100
to102; the other two newly compatible designs are outside the reviewed union.

The read-only probe now accepts an optional path to the current numeric C#
collection allowlist, validating its restricted array syntax and uniqueness.
This avoids rebuilding the game just to audit a four-ID allowlist change. Its
suppliers, item definitions and wardrobe metadata still come from the existing
candidate's production loaders. The output explicitly labels the source override;
it must not be represented as the deployed or compiled game allowlist.

The x86 probe compiles with warnings as errors and runs without database access,
server startup or item grants. [Current evidence](research/COLLECTION_SUPPLY_CURRENT.json)
records input/output hashes and all compatible designs outside reviewed supply.
Native appearance and actual combined earning remain pending.

There is a second concrete prerequisite: Planet admission stops after eight
collection deliveries, and the exchange balance counts at most eight distinct
steps at20 credits each. Its156 stock designs currently fit that160-credit cap.
Expanding stock by98 or more without another earned-credit path cannot support
the combined200-garment/200-mount route. Preserve existing receipts, one reward
per design, weekly admission, payment serialization and permanent binding when
extending the approved collection event. Do not solve this by granting credits,
lowering thresholds or reclassifying existing rentals.

Reproduce with the documented compiler command below, then pass the absolute
`Game/Features/CollectionAchievementCatalog.cs` path as the probe's sole argument
while running from the private candidate game directory. Without an argument,
the probe retains its original compiled-catalog behavior.

Reviewed 2026-09-27 using the existing private candidate, without database access,
server startup, item grants or purchases. This is a supplier upper bound, not a
completed character collection or a claim that all designs fit one character.

`tools/CheckCollectionShopCapacity.cs` invokes the production item loader and both
EShop loaders, then intersects shop 2888 and Vesper's actual cosmetic arrays with
the compiled compatible collection catalog. Secondary item overrides and merged
shop stock are therefore included. It excludes absent items and zero prices,
deduplicates designs and prints numeric identities and prices for inspection.

| Supplier | Garments | Mounts |
| --- | ---: | ---: |
| CP mall 2888 | 71 | 0 |
| Vesper | 11 | 20 |
| Distinct union | 82 | 20 |

All 71 mall garments together list 41,833 CPs. Each included Vesper design lists
2,500 RVPs. These are current catalog prices, not a proposal to change the economy.
Vesper's two incompatible dog designs do not count. The result agrees with the
existing earned fifty-garment fixture's 71-design mall stock.

Even assuming all these designs can be owned together, these suppliers lack at
least 18 designs for 100 garments and 30 for 50 mounts. The shortfalls for the
200-garment and 100-mount tiers are 118 and 80 respectively. The entire compatible
mount catalog still has only 199 designs, so supplier work alone cannot enable
200 mounts. Other events, packs and drops are outside this audit: their actual
supplies must be inventoried before claiming a global scarcity or adding stock.

Next implementation prerequisite: identify existing ordinary event/quest outputs
outside this union, verify permanent ownership and compatibility, and connect
enough distinct outputs to the missing thresholds. Retain the pending owner
decision on the four named cosmetic collection supply editions. Do not lower
thresholds, count rentals, enable dormant shops or insert completion receipts to
make Rising Star appear reachable.

## Reproduction

Compile the read-only probe against the reviewed candidate assembly with the x86
.NET Framework compiler, then run it with the candidate game directory as its
working directory. It reads relative `database` catalog paths. It needs no database
credentials and does not call the server entry point.

```powershell
& C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /platform:x86 /warnaserror /r:obj\mrms2017-validation\bin\Revora.exe /r:System.Core.dll /out:obj\mrms2017-validation\bin\CheckCollectionShopCapacity.exe tools\CheckCollectionShopCapacity.cs
```

The probe compiled without warnings and ran successfully. Numeric output is in
ignored `obj/collection-shop-capacity.log`. Evidence hashes:

| Input | SHA-256 |
| --- | --- |
| Candidate Revora.exe | `7D86AB5305F15DFE9C77E9849B27A166597C3D497B71A4BF1E49CB43A10F8055` |
| itemtype-new.txt | `1ED7A23B7A34B5E3706277F97E421E3CDF0B269EEB5BE7367A4EBC0E4F22B3EF` |
| ItemtypeSub.txt | `6E318B1E53B0FB28036BD547B5CB78914E3E66B1272D8054259B6B0BCAA66AEA` |
| emoneyshop.ini | `3549E60A10E4391508F6731932850102565B8A3239EA60B241975394EBBDA3C9` |
| emoneyshopV2.ini | `0F81B11689546FEE470C04CE5114778B8E2E35B1B0378087743C8D30A4DC56D6` |

Native display, ordinary payment/delivery of every listed design, and live
deployment remain separate gates. No runtime or stock changed in this batch.

## Existing pack and event follow-up

The expanded probe reads the compiled EasterGoldPacks reward array and checks
the reviewed 30-choice range. Beyond the direct-shop union, it adds two garment
identities (193555, 193775) and eight mounts (200481, 200499, 200500, 200543,
200563, 200570, 200571, 200574). Thus shops plus the entire Easter output pool
have an upper bound of **84 garments and 28 mounts**, not a proven earned set.
[Easter issuance](EASTER_GOLD_PACKS.md) identifies sign-in and stone-combine
routes, eligibility and closed sources. A pack consumer alone is not acquisition.

The reviewed legacy `ItemHandler` MountArmorPack switch maps 25 packs to mounts.
Of these, the actual merged mall stocks **17** with positive prices and compatible
outputs: 3000254 and 3000256 through 3000271. Each costs **2,799 CPs**, totaling
**47,583 CPs**. Their 17 outputs do not overlap the 28 above, bringing this
potential mount supply to **45 designs**. Five more designs are still needed for
the 50 tier even if every route passes. The eight other switch entries are not
counted merely because they have handlers.

These packs currently add the output and remove the source separately. They are
already classified `live-split-write` in the compound item audit. Next concrete
implementation batch: preserve each output's permanent, unbound, -1 metadata;
make source consumption/output/receipt atomic; validate existing mall purchase,
opening, replay, rollback, wardrobe deposit and reload using ordinary accounts.
Do not label the 45-design upper bound an earned milestone before those checks.
The owner has been asked separately whether recurring events may add permanent
lower-star collection rewards alongside their valuable main prizes; no answer
or new reward policy is assumed.

Follow-up: the [17 stocked pack route is now atomic and earned across three
processes](MOUNT_PACK_EARNING.md). This supersedes the split-write classification
above for those 17 packs only. The combined 45-design route and 50-mount milestone
remain unproven; eight unstocked legacy cases are unchanged.

Other source references were classified rather than counted as acquisitions:

- First rebirth's 187405 and Power Arena's 200449 duplicate Vesper stock.
- War Award 900046 and Prizewell 900035 contain cosmetic exchanges, but both
  are intercepted by `TwinCityPlaza.ClosedDeskIds` before legacy NPC dispatch.
- TreasureBox lists ten garment outputs, but its legacy claim is not an atomic
  owned reward route. Event admission/calendar and persistence remain unverified.
- Wardrobe metadata repair, cosmetic barter input IDs, GM examples and commented
  NPC branches are not new permanent reward producers.

The expanded probe compiled with warnings as errors and ran successfully against
the same hash-recorded candidate and catalog inputs. Source reviewed at 721f50c8;
the local owner edits in pack and dispatch files were neither changed nor used as
the basis for new completion claims. Native and production gates remain open.


### Remaining source check (2026-09-27)

A bounded follow-up at `4cfd8ee5` found no issuer for the eight unstocked mount
packs in the gameplay, database or NPC source trees. The TreasureBox class has
no production caller for `Generate` and no active admission/calendar route in
Thread/Server; its remaining legacy NPC reward call does not establish access.
Do not restore its split-write payout merely to count its garment IDs. These
sources remain excluded, and no new collection reward policy has been assumed.


## Current supplier union (2026-09-30)

The owner-approved [Planet collection edition](PLANET_COLLECTION_REWARDS.md)
supersedes the earlier unanswered-policy notes. The probe now reads the compiled
`FixedOutputPacks.MountFor` and `PlanetWeekend.CollectionRewards` contracts,
rather than maintaining a second legacy pack mapping. Only positively priced,
stocked packs and compatible collection identities are counted.

| Reviewed union | Garments | Mounts |
| --- | ---: | ---: |
| CP mall and Vesper | 82 | 20 |
| Shops, 17 atomic mount packs and Planet | 90 | 44 |
| Above plus the complete Easter pool | 92 | 52 |

Planet supplies 19 distinct rewards (10 garments and nine mounts), but two of
each category overlap the earlier supplier union. Adding 19 to the old totals
would overstate capacity. The 50-mount tier now has sufficient potential supply:
it requires at least six of the eight additional Easter mounts alongside the
44-design union. The [combined 50-mount route now passes](COLLECTION_FIFTY_MOUNTS.md)
through ordinary sign-in issuance, purchases, wardrobe deposits and restart on
one character. The full 52-output potential pool remains unproven as an earned
set. The 100-garment tier still lacks eight designs;
100 mounts and 200 garments also remain unsupported by this reviewed union.

The read-only probe compiled with warnings as errors and passed against private
candidate SHA-256
`0F73F292FEB583E0DC46549AEA54933FF3EA38A8C49630DAD86F1C9AB2B542E1`
and the catalog inputs recorded above. No database writes, server startup,
reward grants or live deployment were performed by this probe.


## Compatible unsupplied designs and star limits (2026-10-01)

The read-only probe now loads the actual wardrobe metadata and checks the same
ID/type/item-presence intersection used by CollectionAchievements. It reports
compatible designs outside the reviewed supplier union, grouped by stars. This
is not a claim that every other possible legacy supplier has been exhaustively
ruled out, nor that an uncovered design is an earned reward.

| Category | Compatible catalog | Reviewed supplier potential | Additional for100 | Additional for200 |
| --- | ---: | ---: | ---: | ---: |
| Garments |421|92|8|108|
| Mounts |199|52|48|148; impossible with this catalog |

| Stars | Garments outside reviewed supply | Mounts outside reviewed supply |
| --- | ---: | ---: |
|1|100|39|
|2|61|32|
|3|112|52|
|4|53|17|
|5|3|7|

The uncovered five-star garments are189135,193055,193275; mounts are200021,
200022,200220,200221,200410,200428,200525. These numeric candidates require reward
and ordinary earning validation before any supplier claim. Even adding all ten
five-star candidates would leave five designs missing for100 garments and41 for
100 mounts. Therefore a five-star-only supplier expansion cannot close the tiers.
Do not relabel stars, lower collection thresholds or count rentals to hide this.

A proposed Revora event exchange could supply lower-star permanent designs as
additional collection rewards while retaining valuable primary event prizes.
The owner was asked whether to add an earnable exchange or leave unsupported
collection tiers unavailable. No answer, event currency, pricing, output recipe
or activation is assumed in this audit. The reviewed92/52 totals still include
potential random outputs and are not a completed character collection.

The x86 probe compiles with warnings as errors and passes against candidate
`5C8FA4FE3F0E06FC3C55F2BFDC0B123820CA36335F96BFEC87FBA91847488EC7`.
Wardrobe input `database/coat_storage_type.txt` SHA-256:
`469AC492D7445ED6759827F04602A77E57179C361218DFFCF79788FE165F4977`.
Other catalog input hashes are recorded above. Numeric output is retained in
ignored `obj/collection-current-capacity.log`. No database, server startup,
item grant, stock mutation or deployment was performed.


## Approved exchange (2026-10-01)

The owner accepted the Revora exchange. [Planet collection credits](PLANET_COLLECTION_EXCHANGE.md) now supply 108 additional garments and 48 mounts, bringing potential capacity to 200/100. All156 exchange designs pass ordinary dispatch purchase and wardrobe checks; combined Planet gifts earn the100-garment achievement. This supersedes the unanswered-policy and92/52-only notes above. The full combined200/100 journeys are still unverified;200mounts remain impossible with199compatible designs.


## Compiled exchange inventory verification (2026-10-01)

The read-only capacity probe now loads the compiled exchange arrays directly.
It rejects duplicate/overlapping additions, missing item or wardrobe metadata,
wrong category identities and stock outside the reviewed3?5star range. It
confirms exactly108new garments and48new mounts, with the full supplier union
at**200garments/100mounts**. Compatible catalog totals remain421/199. This verifies
the capacity arithmetic against executable stock; it does not convert a possible
supplier pool into a completed character journey.

The x86 probe compiles with warnings as errors and passes against assembly
`102604CE650FAD38027D5FA07EEFAB57D1A0CFB887217AFF9F5D912F6748627D`
and the catalog inputs recorded above. Numeric output is retained in ignored
`obj/collection-exchange-capacity.log`. It opens no database and performs no
purchases or grants. [Full Planet/exchange collection recovery](PLANET_COLLECTION_EXCHANGE.md#complete-collection-recovery-follow-up-2026-10-01)
now passes separately for175earned designs and the100garment achievement.


## Combined earning follow-up (2026-10-01)

The [one-character200garment/100mount journey](COLLECTION_TWO_HUNDRED_AND_HUNDRED.md) now passes ordinary earning, exact game/database restart recovery and replay. This supersedes the earlier unverified-combination notes, without claiming native acceptance or a compatible320-achievement career. The200-mount tier remains unavailable with199compatible designs.
