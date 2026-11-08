# 6711 cosmetics restoration checkpoint

2026-09-15, runtime integration baseline `c120166`. Ordinary authenticated
players can use the restored 3510 hair/face/colour service in this source build.
Purchases persist ownership; actual appearance changes atomically earn 10113,
10114 or 10116. Retired dyes redeem silver. Native 6711 window/world acceptance
and live deployment remain pending. No live server or client was changed.

## Selected edition and evidence

The [official customization guide](https://co.99.com/guide/guides/customize.shtml)
describes the historical seven consumable dyes, 200 silver each, and paid haircuts.
The [hairdressing guide](https://co.99.com/guide/guides/haird.shtml) establishes the
Storekeeper acquisition/use loop. These are historical behavior evidence; they
do not override the installed 6711 item and cosmetic tables.

The approved client contains the old dye IDs 1060030-1060090, but their current
descriptions say they redeem 200 silver. Its native colour dialog uses owned
style/colour combinations and a separate purchase/selection sequence. Revora's
old dye switch and formerly incomplete 3510 handler did not establish the intended edition.
Do not award 10113 merely for accepting an old item ID or echoing a message.

| Local evidence | SHA-256 | Numeric facts |
| --- | --- | --- |
| Genuine DX9 Conquer.exe | 3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60 | 3510 constructor, serialization, request builders, receiver and colour ownership predicate |
| ini/hairface_storage_type.dat | 5150d90c59beded341017ecb24ddc89bb933aba578b82bdd7f1cb7ed940e0ca4 | 377 unique kind/ID pairs: 57 hair, 320 face; twelve delimited fields including the final empty field |
| ini/hair_color_type.dat | 348df6eedc66b169150ba9159d3a83bcf9f323a0f7fdbcf56f4361fd7b5702b6 | 270 unique pairs across 45 styles, six colours per style; payment-kind zero, amount 1,000 |
| ini/itemtype.dat | d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a | Seven historic dye identities have silver-redemption descriptions |
| ini/Achievement.dat | df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc | 10113 dye; 10114 hairstyle; 10116 avatar; distinct from armor colour 10115 |

Tables were decoded in memory with the existing reviewed codec. The current
server `database/hairface_storage_type.txt` matches the decoded installed table
when line-ending CR bytes are removed. No client descriptions, proprietary
implementation or binary was added to Git.

## Native static protocol facts

Read-only analysis reused `research/isolated/ghidra-6711-account312/Account6711`,
program `Conquer.6711.original.exe`. The script verified the approved executable
hash before discovering the relevant native symbols. Private decompiler logs are
under `obj/chi-refill-validation/hair-*.log`; they are not release artifacts.

| Evidence address | Confirmed behavior |
| --- | --- |
| 008ca719 | Top-level serializer: field 1 scalar action; fields 2/3/4 singular submessages; field 5 repeated stored entries |
| 008c9acc | Field-2 parameter serializer: scalar fields 1 and 2 |
| 008ca33f, 008ca4c3 | Field-3 colour parameter: two scalar values; builder writes family 0xdb6 and payload length plus four |
| 008ca44a, 004f44a1 | Hair dialog sends action 4 with kind 0/style ID, then action 3 with the same choice |
| 008cbea1 | Face acquisition sends action 4 with kind 1/face ID |
| 004f2b35 | Colour acquisition sends action 7 followed by action 6; already-owned colour sends action 6 alone |
| 008cb59b | Colour entitlement is bit `1 << colour` in the style's stored colour mask |
| 008cb7d3, 008cb149 | Receiver dispatches action 6 to update selected colour; action 5 updates colour ownership |
| 008c9f3a | Stored-entry fields: 1 scalar kind, 2 scalar ID, 3 boolean active, 4 separate scalar, 5 repeated scalars |
| 009326aa | Rule submessage has five distinct scalar fields; hair rule type 5 checks the fifth scalar against the catalog threshold |
| 004f262b, 004f2b35 | Colour table parser preserves its five numeric columns; payment kind zero checks the silver balance |

The stored-entry consumer reads the first two field-5 values for hair entries.
`008cb5f2` returns the map value after its key; `008cb798` assigns active state,
selected colour (field 5 element 0), colour mask (field 5 element 1), then the
separate field-4 scalar. This corrects the earlier provisional label of field 4
as the mask. A response must supply both repeated values for hair entries.
Field 4 is read by `008cb4c3` and passed to the UI's timed-state path; its exact
expiry encoding remains pending.

`00699803` builds preview hair as `colour * 1000 + style`. Actor setter
`00765fbd` splits its UInt16 argument with modulo/division by 1000. The native
10017 attribute 26 branch at `00942aeb` first takes value modulo 10000 and calls
that setter, then processes the higher portion separately. The old server
`HairColor` helper's factor 100 must not be reused by the owned 6711 service.
The colour dialog also sends action 6 directly for its default colour zero;
action 7 remains restricted to paid colours 1-6.

Installed `StrRes.ini` entry 11331 describes VIP hairstyles from VIP level 4.
Native rule type 5 checks the fifth rule scalar against the catalog requirement;
all installed type-5 rows require 4. This supports the VIP interpretation but
avatar follow-up below resolves its separate VIP source and profession filter.
Default ownership migration and body-type normalization still need integration review.
Do not enable zero-price event-only rows as free store purchases.

These are static native facts. Synthetic golden fixtures below are not native
captures. Dynamic appearance/rendering and ordinary player acceptance remain open.

## Implemented prerequisites

`AvaterFaces.Parse` now preserves empty columns instead of using
`RemoveEmptyEntries`. Previously an empty class list shifted the final restriction
into the class field. The parser preserves price, explicit class families and
restriction columns independently. It checks row shape, integer widths,
nonzero identities, duplicate kind/ID pairs, malformed class lists and bounded
catalog size. Reload replaces a completely parsed list; it cannot append duplicate
rows or publish a partially parsed catalog.

`HairFaceRequest.TryRead` is an isolated request parser with a 128-byte envelope
limit, exact 3510 framing/seal, bounded canonical UInt32 varints, singular nested
parameter checks and distinct field-2/field-3 request shapes. It rejects
client-authored stored lists/rules, duplicate scalar/parameter fields, truncation,
unsupported actions and invalid kind/identity/colour bounds. Zero/query and
open-window shapes use the same strict parser. The replacement handler separately
enforces canonical, loaded, living player ownership, excludes trade/booth use,
and limits accepted requests to 20 per second before database work.

`FaceAndHair` now calls the owned service and emits committed responses. It
contains no client-authored prices, ownership or arbitrary appearance assignment.
Failures fence the session for reload. Opening either window sends durable
ownership and refreshes VIP field 39, including zero after VIP expiry.

## Owned transaction and persistence checkpoint

`AppearanceState` now participates in production entity load/save. It retains
exact nullable saved body, face and hairstyle values separately from legitimate
pending session edits. Full saves compare those preimages; transaction writes
lock the durable entity row and compare them before updating. Successful commits
advance the image, and failed writes cannot advance it. Body, face, hair, class
and VIP setters use the same character ownership lock. The existing gender
operation now holds that lock across its appearance changes and save, uses the
6711 colour factor and checks save failure. Its pre-existing CP/equipment payment
flow is not claimed as a newly atomic service by this milestone.

`CosmeticOwnership` is initialized at server startup and reached by the normal
packet handler. It has no GM or owner allowlist. Ordinary-player fixtures exercise
the following behavior:

- Permanent `(character, kind, ID)` ownership, remembered colour per style and a
  purchased-colour mask. Existing raw appearance does not manufacture ownership.
- Installed silver prices, one payment per unlock and one 1,000-silver payment
  per supported style/colour pair. Duplicate requests are free and do not append
  another receipt. Re-selecting the default colour is free.
- Selection requires that character's saved entitlement. Editing an unworn
  style remembers its colour without changing the currently worn style.
- Payment, entitlement, appearance when changed, a receipt and any new matching
  achievement share one database transaction. Cache, currency and achievement
  projections happen only after commit. Suppressed achievement writes abort the
  transaction instead of silently losing the completion.
- Ordinary/VIP purchase rules are separate from event-earned rows. A purchased
  VIP style remains owned after VIP expires. Zero-price event entries cannot be
  purchased. Unknown entries and absent colour combinations are rejected.

Native follow-up after `b959964` resolves the avatar VIP source. Eligibility
function `008cd7c3` checks rule type 5 through getter `00779b41`, reading player
field `+0x5b4`. The 10017 case `0x27` (39) calls setter `00779b48` for that field,
matching Revora's existing VIP update. The avatar window therefore uses the local
player VIP value; it does not need a fifth rule scalar from action 8.

Profession filter `008cc43e` obtains the family through `0077a0a7`, which computes
`(profession % 1000) / 10`. Revora's byte-sized class values make this equivalent
to the service's `Class / 10`. `008cd7c3` also compares catalog type against the
local player's `+0x1ec`, allowing catalog zero. The service's four supported
male/female body identities remain consistent with the installed labels; the
full normalization of that native player field has not been traced.

The avatar catalog parser `008ccf0e` additionally references `ini/Face.lua` for
presentation metadata. The possessed file has SHA-256
`df9f026b532c8d110c2ee5feb92d3e6d55be473e95bf605e5ba8d04d474563de`.
It was not executed or copied into Git. Private logs for this follow-up are
`obj/chi-refill-validation/hair-avatar-*.log`.

`HairFaceResponse` has independent golden-byte checks for the separate choice,
colour, rules and owned-entry messages. It always supplies both unpacked field-5
values for owned hair. Field 4 is zero for permanent ownership; timed ownership
is not implemented. Thirty entries per page keep each response below the native
1,020-byte payload limit; a 1,024-entry boundary fixture verifies pagination.
These checks do not establish real-client rendering or window acceptance.

## Earning paths and alternate services

| Action | Achievement | Trigger |
| --- | --- | --- |
| Select a different owned hairstyle | 10114 | Worn style ID changes after the durable transaction |
| Select a different owned colour | 10113 | Currently worn hair colour changes; editing an unworn style does not count |
| Select a different owned avatar | 10116 | Face ID changes after the durable transaction |

Purchasing alone, replaying a selection, querying/opening a window and retired
dye redemption grant none of these achievements. Achievement packet bits update
immediately and survive a new-session reload. Armor colour 10115 is separate and
is not implemented by this milestone.

Barber 10002 and Mr.Free 3216 now offer a short, world-facing introduction and
open the native hairstyle collection. Their old numeric options cannot directly
assign a style. Mr.Free no longer exchanges a Lucky Amulet for an unrecorded
haircut; this route consumes no amulet. The installed catalog supplies style and
avatar silver prices (500 or 10,000); supported colours cost 1,000 per style.

`RetiredHairDyes` handles 1060030-1060090 in increments of ten through ordinary
item use. Each selected unit returns 200 silver, preserving bound state rules,
ownership, expiration, trade restrictions and purse bounds. Item debit, silver
credit and a receipt commit together. Immediate repeated uses of the same stack
are debounced; a later deliberate use can redeem another unit. Nothing changes
the player's hair or awards a dye achievement through this retired-item path.

There is no inferred migration of old unlocks: the former handler recorded no
ownership. Existing raw appearance is retained until a deliberate owned selection;
old purchases other than the currently visible look cannot be reconstructed from
appearance alone. Zero-price event rows are not free shop inventory. Future
restoration of event-earned cosmetic ownership requires its own trusted producer.

## Validation and remaining gates

The isolated Debug build passes 1,833 checks: 119 ownership/handler/redemption/
reconnect checks, 68 independent catalog/wire checks, 23 appearance checks,
452 experience, 74 progression, 91 titles, 100 BP PK, 116 class PK, 43 couple PK,
78 Elite PK and 669 combat checks. Three achievement-audit fixture tests also pass.
All database tests run sequentially in `revora_potency_validation`.

The reconnect fixture uses the complete production `LoadEntityState` body with
a new session object, then tests ownership, payment replays and achievement bits.
The public login wrapper still invokes that body and both website-grant providers.
The fixture never calls external CP/vote grant delivery against the identity schema.
Fixture support tables contain no production character data or shared sequence defaults.

Validated server SHA-256:
`4c5dc3d21060f2f6bfac0ef576c5e7e77eefad18a936fe67c7d82b1747f478ed`.
Producer inventory is now 108 of 337; 229 producers remain missing. This does not
prove a per-character route to 320 achievements.

Pending: native windows, pagination, avatar eligibility/selection, colour
rendering and nearby-player appearance in a genuine 6711 session; production
rollout and any verified legacy-ownership migration. The exact native body-type
normalization and timed cosmetic ownership remain research gaps. No Host
deployment, public patch, client modification or website publication occurred.

## Armor follow-up

The subsequent [armor dye checkpoint](ARMOR_DYE_EARNING.md) implements the separate
10115 earning path, durable Shopboy visits and Black Tulip's installed ten-Chi
redemption. The combined integration suite now passes 179 checks and the producer
inventory is 109/337. Earlier counts and hashes above describe the prior cosmetics
checkpoint; native visual acceptance remains pending for both services.


### Sereth transactional follow-up

The earlier non-atomic gender-service limitation above is superseded by
[the flower-queen vacancy checkpoint](FLOWER_QUEEN_EARNING.md). CP payment,
appearance, owned garment movement, reconnect location and offline successor
achievements now commit together. Build and 936 Magnolia checks pass; native
client acceptance and deployment remain pending. Marriage links are preserved
under the documented default pending the owner's policy response.

## Retained ordinary service journey (2026-10-01)

The retained ordinary Windwalker purchases a permitted hairstyle and avatar
from the loaded catalog through sealed 3510 packets. Selection uses the owned
entitlements; the hair-colour purchase costs exactly 1,000 silver in addition
to the exact catalog prices. Applying the selected hairstyle, colour and avatar
produces 10114, 10113 and 10116 respectively. Candidate selection obeys sex,
class and unrestricted silver-purchase rules; no entitlement or appearance is
inserted directly into the database.

The same character then supplies an owned Meteor (purchased normally when
absent), pays Shopboy 10063 to enter map 1008 and uses Dye Seller 10064 to change
its equipped armor to a different colour. The original armor UID remains owned,
one Meteor is consumed and one armor-dye receipt records achievement 10115.
An ordinary classic dance action inside Twin City records 10121.

The complete career, owned appearance, paid cosmetic operations, dye visit and
armor receipt snapshot survives a database/game restart. Repeating the hair,
colour and avatar purchases/selections and the dance causes no further payment
or receipt. The prior race rewards and stored items remain in the comparison.
Armor persistence is covered; this batch does not add an armor-dye concurrency
or replay-fault test beyond the service's separately documented coverage.

The first fixture compilation rejected direct access to the internal catalog;
the fixture now reads that existing catalog by reflection, without changing its
visibility or production policy. No runtime state changed on that compile failure.
NPC travel/proximity is controlled. Native appearance/windows remain pending;
this does not establish mount breeding or cosmetic native-client acceptance.

Validation: `career-retained-cosmetics-earn` and
`career-retained-cosmetics-reload` passed on candidate SHA256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
No production code, live deployment or client patch changed.
