# Mining and first-rebirth gem supply

M6.53a-c, 2026-09-16 UTC; baseline `a06da9b`. Ordinary players can buy and
equip a Pickaxe, enter the mines, earn all seven normal Heavenly Stone gems,
and combine them with hunted Clean Water. This completes the automated gem
supply route, not the final first-rebirth transition or native acceptance.

## Evidence and selected edition

The official [mining guide](https://co.99.com/facebook/mine.shtml) identifies
Pickaxes and Hoes, a 1,000-silver Pickaxe purchase, and distinct mine gem pools.
Plain Mine supplies Phoenix, Dragon and Fury; Forest supplies Phoenix, Dragon,
Rainbow and Kylin; Canyon and Desert supply Violet and Moon. Jail and Meteor
Zone have the Forest subset. The guide also lists other gems and rewards that
are outside this seven-gem restoration. No unpublished official probability
is inferred. Its level-15 purchase statement and level-40 Meteor Zone entry
do not override the installed tools' level-70 equipment requirement.

The restored normal-gem branch selects uniformly within each listed pool.
`Config.MiningNormalGemBasisPoints=100` means a **Revora 1% chance per eligible
two-second tick**. A miss continues through the previous 40% extra-loot branch,
preserving its ore/scrap/stone order and catalog filtering. This is not a claim
that the complete historical mining economy has been reproduced. Nonpositive
gem configuration disables that branch; values above 10,000 clamp to 100%.

Installed evidence, inspected without modifying production data:

| Source | Identity |
| --- | --- |
| Pickaxe / Hoe | 562000 / 562001; level 70, durability 1099 |
| Pickaxe store | Gold shop 1; Storekeeper, Twin City (434,312); 1,000 silver |
| Conductress | 10080; Twin City (423,356); existing Mine Cave option 5 |
| Forest assistant | 125; map 1011 (934,562), enters 1025 (30,71) |
| Canyon assistant | 126; map 1020 (523,889), enters 1026 (142,105) |
| Desert assistant | 127; map 1000 (312,8), enters 1027 (142,105) |
| Plain / Forest / Canyon / Desert | 1028 / 1025 / 1026 / 1027, each using its own terrain |
| Jail mines | 6000 and 6001, each using its own terrain |
| Meteor Zone mine | Map 1218, explicitly using terrain 1025 |

Read-only inspector: ignored `obj/chi-refill-validation/MiningSourceInspect.cs`,
compiled x64 against the reviewed local Revora/Npgsql assemblies. Executed
SHA-256 `AEDC1F60D8AAD8399D399FAA926297FB93EE545AB6FD0B9F9A2CF99DDE56D186`.
It uses protected configuration and a read-only transaction, printing only
the relevant public NPC and map definitions.

Installed file SHA-256 values:

- `database/itemtype-new.txt`:
  `1ED7A23B7A34B5E3706277F97E421E3CDF0B269EEB5BE7367A4EBC0E4F22B3EF`
- `database/shops/Shop.dat`:
  `9CBF67CF0EDF03A781A50D50F581544878BA352A3019CC91CB14B411F21552F3`
- `database/Portals.ini`:
  `4A9A681103915CA232BE7C98DEFD69CF08AE5639205822967EE678985EA0AAA9`

## Player services and ownership

The installed Pickaxe is now covered by the existing protected silver-shop
transaction. Stock, seller proximity, price, bag capacity, loaded purse preimage
and duplicate purchase interval are checked. The payment, item and purchase
record commit together; the inventory and purse update immediately afterward.
Legitimate pending silver gains remain spendable without overwriting an offline
balance change. The historical `revora_weapon_book_purchases` table also records
these purchases; its existing item-type column already supports this catalog ID.

The Conductress's Mine Cave option formerly charged 100 silver and stopped at
Twin City (100,360). No installed Twin City portal leads to Plain Mine, while
the old Twin City assistant handler's NPC 4293 is actually placed on map 5001.
The repaired option takes eligible level-70 players directly to the installed
Plain Mine landing, retaining the 100-silver fare and existing PK restriction.
It checks proximity, physical destination, terrain and ordinary player state.
The fare commits with a resumable passage. A map-transition trigger marks the
passage used when the player enters 1028; a failed teleport leaves it available
after reconnect, without another charge. Later intentional trips pay normally.
One `revora_plain_mine_passages` row per character holds cumulative trips and
the latest fare. It is not an unlimited historical trip log.

Action 99 starts a server-owned mining session. It requires an owned, durable,
permanent Pickaxe or Hoe in the selected right-hand slot (4 or 24), the installed
equipment level, an allowed physical mine and a canonical loaded living player.
Trading, vending and poker exclude mining. The request cannot select an item or
supply a reward time. Repeated starts retain the current two-second deadline;
misses also consume that interval. Movement, teleport, death, changed equipment,
full bags and replaced sessions stop mining. Reconnect requires a new intent.

Every successful yield locks the owner and checks persisted map, equipped tool,
bag count and durable next-yield time. Item insertion and the mining state update
commit together, followed by the normal immediate item update. Stale or suppressed
writes roll back the yield and require reconnect. `revora_mining_state` has one
row per character: next permitted yield, cumulative yield count, last item and
type, and map. It does not retain provenance for every historical mined item.
No new achievement is granted for mining or crafting a stone.

The existing protobuf action dispatcher now rejects invalid declared lengths,
families, suffixes and malformed payloads before handling any family-10010
action. Mining and ordinary portal-action fixtures use this actual boundary.
No client binary, item catalog, shop file or portal file was changed.

## Validation and remaining gates

Isolated legacy Debug/x86 build passed. Tested server SHA-256:
`DC3FB988FF4049D4C08F007012C2F68C7992C518E0781E45045C5329B0A5449A`.

The **94 mining checks** exercise the installed store and Conductress, normal
equipment handling, Plain Mine exit, installed world portals, Forest/Canyon
assistants, all seven gems, actual Waterlord death and pickup, Sereth crafting,
and production character/item reload. They also cover all seven map identities,
the Hoe, alternate equipment, exact chance/time boundaries, concurrent ticks,
malformed action frames, stale tool/map/bag state, suppressed writes, failed
fares, interrupted travel and reconnect without duplicate payment.

Deterministic rolls and clock advancement make the route reproducible; they do
not establish live drop pacing. Fixture movement places the character beside
existing portals/NPCs before invoking their real handlers. The Waterlord leg
still places the character on map 1212, so ordinary access to that hunt remains
open. The test neither grants recipe gems directly nor completes rebirth.

The same build passes first-rebirth materials **74**, experience/weapon books/
silver shops **452**, travel **105**, promotions **266**, Grotto **262**,
progression **74** and default hunt/boss rewards **216**: **1,543 gameplay checks**,
plus **3 producer-audit tests**. Suites ran sequentially in the disposable schema.
Ignored logs: `obj/chi-refill-validation/mining.log` and `mining-*.log`.
Achievement producer inventory remains **217/337**; wardrobe totals are unchanged.

Still pending: ordinary Waterlord access, the atomic final rebirth rite and its
reviewed skill/item rewards, native 6711 mining animation/tool visibility/travel
and inventory acceptance, real-time balance review, and live deployment. The
server has not been restarted, installed files have not been replaced and no
client patch has been published.

Subsequent [M6.54 island-access validation](WATERLORD_ACCESS.md) replaces the
fixture-only Waterlord placement with the complete walked NPC/portal route,
protects its paid travel and verifies the full return to Sereth. Its stronger
journey evidence supersedes the access limitation above; native acceptance and
the final rebirth transaction remain open.
