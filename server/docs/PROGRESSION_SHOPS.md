# Twin City progression shops — patch 1.0.8

Current verification, 2026-09-20: all 28 loaded server prices match the client.
The earlier 75-CP Lucky Amulet claim inspected the unused `itemtype.txt`;
the active `itemtype-new.txt` charges 5 CPs. Seventy shop checks now pass without
price or stock overrides. See [the correction](PROGRESSION_SHOP_PRICE_PARITY.md).

Owner-requested native shops on map 1002:

| Shop | Coordinate | NPC UID | Mesh | Currency |
| --- | --- | --- | --- | --- |
| SubclassSupplies | 424,339 | 91008 | 46070 | CPs |
| InnerPowerShop | 428,343 | 91009 | 46080 | CPs |

Both database rows use NPC type 1 (`Shop`), with the same UID in server and
client `Shop.dat`. Client `npc.ini` templates 4607/4608 reuse the existing
Storekeeper model and motions. No NPC dialogue is required to open either shop.

## Subclass catalog

| Item | Item ID | CPs each |
| --- | --- | --- |
| Celestial Stone | 721259 | 30 |
| Bomb | 721261 | 10 |
| Yin Yang Fruit | 711188 | 150 |
| Lucky Amulet | 723087 | 5 |
| Meteor | 1088001 | 3 |
| Performer Card | 711679 | 150 |
| Saddle | 723903 | 3 |
| Diligence Book, 5 study points | 723340 | 1 |
| Endurance Book, 20 study points | 723341 | 4 |
| Cordiality Book, 50 study points | 728883 | 10 |
| Modesty Book, 500 study points | 723342 | 99 |

The material quantities used by the existing subclass enrollment logic cost
150 CPs for Martial Artist, 100 for Warlock, 150 for Chi Master, 100 for Sage,
30 for Apothecary, 150 for Performer, and 120 for Wrangler.

## Inner Power catalog

All 17 `ReqItemType` entries in the runtime `NeiGongInfo.ini` are stocked:
Universal Concept A/B and Secret of Breath A/B cost 100 CPs each; Demon Rider
A/B and Dragon Tactics A/B cost 200; Boundless Heart A/B/C and Doctrine of
Deity A/B/C cost 250; Puzzle of Life A/B/C cost 300. Books are consumed by the
existing Inner Power interface, with its existing progression requirements.
One of every book costs 3,600 CPs. Potency is not included in those prices.

## Vesper runes

Vesper remains at map 1002, 396,340 (runtime NPC 900000014). Its former Khopesh
category and purchase option are removed. The Runes dialogue has paginated
red, blue and yellow categories: 10 red runes at 1,000 RVPs, 10 blue runes at
750 RVPs, and 13 yellow runes at 500 RVPs. All are level 1. The red/blue lists
include all ten professions, including Windwalker. Class restrictions apply
when equipping, not when buying. Other Vesper shelves keep their existing stock.

## Payment and persistence

These purchases use the ordinary player path without GM or name allowlists.
The server validates the NPC, current map, distance, trading state, catalog,
quantity, balance and bag capacity. Regular CPs are required at the two native
shops; bound-CP requests are rejected. Compatible unbound stacks can be filled
even with 40 occupied slots. Amounts and total prices cannot overflow.

Currency deduction and item inserts/stack changes commit in one PostgreSQL
transaction before client notifications. Requests and character/bag saves use
the session item lock. RVP awards elsewhere can remain in memory until a save;
the rune transaction persists the current authoritative balance with its item.
On persistence failure, purchases are disabled and the connection is closed;
character and bag snapshots are prevented from overwriting a potentially
committed transaction until the player logs in again.

The four subclass study books now consume exactly one owned item and save its
study award atomically. Consumed item references cannot be replayed. Reading
is rejected before the ushort study limit would overflow. This also fixes
Modesty Book falling through to Meteor Scroll and implements Cordiality Book's
advertised award.

## Client data and release evidence

`tools/ClientPatches/prepare_progression_shops.py` stages the data patch and
preserves originals under the ignored validation directory. The source is the
owner's existing 6711 client, not a downloaded donor. Original `itemtype.dat`
SHA-256: `58784821002aa73173903a9a7d61d782ee6d5d25fa0e2873cd1558294735f976`.
The byte transform was derived from the locally possessed CypterPLm format
evidence; the entire original passes decrypt/encrypt round-trip equality.
Only CP field 36 is changed in the selected plaintext rows. Server prices are
changed in the actively loaded `database/itemtype-new.txt`, not the unused
`itemtype.txt`. These are catalog prices and therefore also apply at any other
shop selling the same IDs.

The ZIP contains only `ini/Shop.dat`, `ini/npc.ini`, and `ini/itemtype.dat`.
No executables, credentials or signing keys are included. The existing Web
`PlayerPatchCatalog` discovers versioned ZIPs; its dynamic latest endpoint
does not use the old static `latest.json`. This is an incremental release in
that existing pipeline, not a signed modern launcher profile.

Validation: 39 checks against the built server, including real PostgreSQL
transactions in a disposable schema with the runtime item schema, ordinary
player purchases, all 17 Inner Power books, all 33 rune options, concurrent
purchases, book consumption and injected database failure rollback. The
disposable schema is removed after validation. Existing character-status and
prestige fixtures also pass. Live-client visual interaction remains an owner
check; offline tests do not establish that the windows have been clicked in
the genuine client.

## 1.0.9 native Buy hotfix

The original client validation missed a hardcoded silver-price check in the
native NPC Buy path. See [SHOP_BUY_6711.md](SHOP_BUY_6711.md) for evidence and
limitations. The 26 zero-silver-price client entries now use 1 as a compatibility
value; actual charges remain the CP prices above. Carry at least one silver
per requested unit of a corrected item; that silver is not deducted. Restart
the client after installing the patch. This does not require a game restart.
