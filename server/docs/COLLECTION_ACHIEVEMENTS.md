# Permanent wardrobe collection achievements

## Current catalog reconciliation (2026-10-03)

The supported numeric capacity is now **421 garments / 203 mounts**. The
199-mount limit described in the dated sections below is superseded. Existing
committed `ItemHandler` equip checks explicitly exempt `SteedArmor` from weapon
proficiency for both first-life and reborn characters. For200621-200624, both
installed catalogs agree on ID, class0, level0 and gender0; the server's otherwise
nonbinding proficiency1 is the only eligibility difference from client0.

The audit accepts only these four IDs with those exact numeric preimages and
mount category2. It still rejects a proficiency2 mismatch, any other ID or
category, and changed class/level/gender requirements. All four already have
server wardrobe entries; no metadata, item file, price or client file was changed.
`CollectionAchievementCatalog.Mounts` now includes them. Permanent ownership,
distinct-design counting and the200 threshold remain unchanged.

Six focused Python tests pass, including wrong-category and altered-eligibility
refusals plus agreement between the generated evidence and production allowlist.
The installed catalog audit reports203 compatible mount designs. Vesper's
existing goods84/85 sell200623/200624 for2500 RVPs each; catalog/purchase-branch
presence alone is not a new200-design earning test. No C# game build, native
acceptance or live deployment occurred in this reconciliation. Achievement11232
remains uncredited until an ordinary200-design route and its persistence are
validated; it is no longer blocked by a199-design numeric capacity limit.

The map/admission review also confirms these are separate release boundaries:
1363 has staged terrain/index files (not missing source terrain), Elimination
has `ReleaseReady=false`, and Abyss admission requires explicit activation.
Their native and release gates are not resolved by this catalog correction.

2026-09-25. Owner decision: count permanent cosmetics only, once per design.
Source validated; native client acceptance and live deployment remain pending.

The installed achievement catalog (SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`)
defines separate garment/mount thresholds at 1, 10, 50, 100 and 200. The
[official wardrobe guide](https://co.99.com/guide/quest/2016/wardrobe.shtml)
describes collection by wardrobe deposit and separate garment/mount counts.
It does not establish rental eligibility; permanent-only eligibility is the
owner's Revora rule, not a claimed original rule.

| Designs | Mount achievement | Garment achievement |
| --- | --- | --- |
| 1 | 11224 | 11225 |
| 10 | 11226 | 11227 |
| 50 | 11228 | 11229 |
| 100 | 11230 | 11231 |
| 200 | 11232 | 11301 |

`CollectionAchievements.Record` reads a single saved ownership snapshot in the
existing progression transaction. Only owned, permanent, single-instance cosmetic
rows marked in the wardrobe, outside warehouses, in the stored or correct equipped
slot qualify. SQL counts distinct design IDs, so duplicates cannot advance a tier.
The normal loaded/canonical wardrobe handlers commit item movement before requesting
reconciliation. A new deposit invalidates the achievement refresh delay, so its
first-design achievement is sent without waiting for the periodic refresh.

The evidence table retains achieved milestones after takeoff, withdrawal or later
loss of ownership. Current collection titles still use their existing live ownership
requirements and can disappear on withdrawal. Client completion requests remain
untrusted. Existing permanent owners are evaluated at login/reconciliation; historical
client bits and old rental ownership do not establish a receipt.

## Catalog capacity and exclusions

The read-only [capacity audit](research/COLLECTION_CAPACITY.json) records hashes
of both wardrobe catalogs and **both primary and secondary item catalogs** for the
client and server. The server loads ItemtypeSub after the primary file and its
SafeDictionary.Add overwrites prior definitions. The audit follows that order.
Conflicting client primary/secondary identities are excluded rather than assuming
an unverified client precedence. It compares numeric ID, class, proficiency, level
and gender; local display-name differences are recorded separately and do not
change numeric eligibility. No proprietary catalog or descriptions enter Git.

The earlier primary-file-only audit was incomplete. Its claim that seven mount
items lacked definitions in both products was incorrect: 200618?200624 are present
in both secondary catalogs. 200618?200620 match; 200621?200624 require proficiency
1 on the server but 0 in the client. Those four remain excluded pending a reviewed
eligibility decision. The old 196-design bound is superseded by 199.

| Category | Original server wardrobe | Client wardrobe | Shared compatible before repair | Supported after repair |
| --- | --- | --- | --- | --- |
| Garments | 475 | 421 | 330 | 421 |
| Mount armor | 181 | 203 | 142 | 199 |

`WardrobeCatalogRepair.Restore` adds the reviewed 91 garment and 57 mount omissions
after ordinary StorageItem loading. Their numeric category, index and stars come
from the client wardrobe metadata; both products already define the items with
matching numeric eligibility. It requires a loaded server item definition and never
overwrites an existing entry or moves an existing entry between categories. Repeated
repair is idempotent. It changes no runtime input file or client file. The production
achievement allowlist now recognizes all 421/199 compatible identities, while still
requiring the correct loaded category and saved permanent ownership.

The server catalog still retains pre-existing server-only designs and star values;
this additive repair does not certify those entries or silently rewrite their rarity.
They cannot count toward these achievements unless in the reviewed allowlist.

**The 200-mount achievement remains unreachable under the compatibility rule.**
At least one of the four remaining proficiency disagreements needs resolution and
client acceptance. No new item definition is required for these four; their existing
requirements disagree. Catalog presence alone also does not establish a supplier.
Do not lower the threshold or equate producer calls with proven player routes.

## Validation and next work

The isolated x86 build succeeds with existing warnings and unrelated owner WIP;
it is not a clean release candidate. The collection fixture passes **150 checks**,
including the earned first permanent garment and mount, immediate reconciliation,
historical retention through withdrawal/takeoff/switching and full production login,
exactly one receipt each, all threshold boundaries and replay, rental/bag/warehouse/
wrong-slot exclusion and twenty duplicate garment instances that cannot cross ten
designs. Higher count boundaries and duplicate items are supplied rollback fixtures,
not claimed earned collections. The existing ordinary route owns five garments and
six mounts from one actual scheduled boss bounty and paid Vesper purchases.

The progression regression suite passes **122 checks**; eight Python capacity and
achievement audits pass. Local logs: `obj/collection-achievements-build.log`,
`obj/collection-achievements-tests.log`, `obj/collection-progression-tests.log`.

Next: review missing catalog identities, implement validated catalog reconciliation,
establish ordinary suppliers and earned routes to higher tiers, and perform genuine
6711 presentation acceptance. These remain unchecked. The source inventory becomes
250/337 producers, leaving 87 absent and at least 70 more source identities before
even considering a compatible 320-achievement journey.

## Earned ten-design follow-up

The same ordinary character now reaches both ten-design milestones through existing
paid services. It retains the original five garments and six mounts, adds the
earned-CP Euro jersey and RVP alpaca from the Big Fan route, then buys these seven
additional permanent designs from Vesper:

| Category | Vesper options | Design IDs |
| --- | --- | --- |
| Garments | 75, 60, 61, 62 | 193445, 193525, 193565, 193625 |
| Mount armor | 81, 87, 88 | 200531, 200572, 200573 |

All twenty designs are in the reviewed compatible catalog. Nineteen Vesper
purchases cost 47,500 of the original 50,000 earned RVPs, leaving 2,500; the Euro
jersey costs 1,200 of the boss-earned CPs. The separate bound-CP transaction fixture
still owns a second jersey in its bag; it does not contribute to either ten-design
collection and its supplied currency does not finance these purchases.

The suite passes **172 checks**. It rejects the nine-design boundary for each
closet, suppresses the tenth-garment achievement insert, verifies that the committed
item deposit remains saved while no achievement is granted, then successfully
reconciles it without another purchase. Exactly four collection receipts exist
(first and tenth in each closet). Withdrawing one item from each closet, running
production character/item/wardrobe login, and redepositing both preserve the
historical milestones, all twenty item identities and the exact remaining purse.

This extends earning coverage to 11226 and 11227, not the 50/100/200 milestones.
The original qualification and controlled combat fixture remain: it supplies
level/class/rebirth eligibility and the final attack damage, then uses the actual
scheduled boss defeat/bounty and NPC purchase paths. It does not certify leveling,
boss difficulty, native packet rendering or an actual logged-in Host journey.
Log: `obj/collection-ten-earned-tests.log`. The existing isolated binary was reused;
no production code, prices, release artifacts or live processes changed in this
test/documentation batch.

## Catalog repair validation

The expanded collection fixture passes **176 checks**, including all 148 restored
metadata entries, idempotence, preservation of an existing server entry, refusal
to create wardrobe metadata without a loaded item, and the existing earned first/
ten-design routes. The runtime check exposed the secondary-catalog omission in the
earlier audit; the corrected evidence now includes those input hashes and override
semantics. Five Python tests check capacity classification, missing/truncated item
records, display-name-only differences, and both production numeric catalogs against
the derived evidence.

The isolated build succeeds with existing warnings and unrelated owner WIP. Logs:
`obj/wardrobe-catalog-build.log` and `obj/wardrobe-catalog-tests.log`. New catalog
entries are not newly granted items; suppliers, earned 50/100/200 routes and native
appearance remain open. Vesper's 200623/200624 entries are present through server
secondary item data, but are excluded from collection achievement counting because
of their proficiency disagreement. The tested ten-mount route uses other designs.
No live deployment or native client acceptance was performed.

## Earned fifty-garment route

The existing merged CP mall stock (EShop/EShopV2, shop 2888) includes 71 compatible
garment designs. The ordinary character from the ten-design route buys forty
additional distinct designs, selected by existing positive CP price then item ID,
for **20,860 CPs** from its original boss-earned purse. Along with the previously
bought 1,200-CP Euro jersey, its remaining ordinary balance is **977,940 CPs**;
RVPs remain **2,500**. No price or shop stock is changed. The bound-CP jersey fixture
remains in the bag and does not contribute to these fifty designs.

The CP cosmetic purchase handler now applies the established atomic payment/item/
receipt transaction to other reviewed cosmetics already stocked by CP EShops.
Receipt-table naming remains compatible with the original Euro purchases. See
[transaction scope](EURO_JERSEY_PURCHASES.md). This does not enable the dormant RVP
shop or certify stock in an inaccessible NPC shop as an earning route.

The collection suite passes **261 checks**. All forty purchases pay once under
immediate replay, deposit through the ordinary wardrobe handler, and increment
the distinct permanent collection. Forty-nine does not grant 11229; fifty grants
it without granting 11231. A suppressed non-jersey purchase receipt rolls back
both the saved currency debit and new item. The earned achievement persists through
withdrawal of the fiftieth garment and production character/item/wardrobe login,
with exactly one receipt and the exact remaining purse.

The isolated build succeeds with existing warnings and owner WIP. Logs:
`obj/fifty-garments-build.log` and `obj/fifty-garments-tests.log`. Qualification,
controlled boss-combat and native-acceptance limits remain as above. This proves
an additional ordinary server-side earning path for 11229; it does not prove
50 mounts, either 100-design tier, 200 designs, or a complete Rising Star journey.
No live server or client deployment was performed.


## Fifty-mount combined earning checkpoint

The [one-character supplier journey](COLLECTION_FIFTY_MOUNTS.md) now earns 50
permanent mount designs through Planet, Vesper, stocked mount packs and sign-in
Easter packs. The 49/50 boundary, exact item/currency recovery and saved 11228
achievement packet bit pass. Higher collection tiers and native/live gates
remain open; this supersedes earlier unproven fifty-mount notes.


## Combined earning follow-up (2026-10-01)

The [one-character200garment/100mount journey](COLLECTION_TWO_HUNDRED_AND_HUNDRED.md) now passes ordinary earning, exact game/database restart recovery and replay. This supersedes the earlier unverified-combination notes, without claiming native acceptance or a compatible320-achievement career. The200-mount tier remains unavailable with199compatible designs.
