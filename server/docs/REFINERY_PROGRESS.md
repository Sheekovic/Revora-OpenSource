# Refinery rewards and persistence

2026-09-15, source baseline `05668be`. Code and disposable-database validation;
not deployed or accepted in the live 6711 client.

Dis City's Super/Sacred packs depend on working refinery materials. The former
application handler replaced effects and consumed materials through separate
writes, stored inconsistent timestamp formats, and allowed a bound material on
unbound equipment. Stabilization used the artifact price table. Temporary expiry
was not connected to the online event tick, and a new temporary effect discarded
the old permanent effect.

## Selected rules and evidence

The [official refinery guide](https://co.99.com/guide/guides/refinery.shtml)
supports a seven-day equipment effect, VIP extensions, cumulative stabilization
credit, and restoration of a permanent effect after a temporary replacement.
Costs for levels 1-6 are 10, 30, 70, 150, 270 and 430 points. A Permanent Stone
provides 10; a Big Permanent Stone provides 100. Bound materials require bound
equipment under the [bound-item rule](https://co.99.com/guide/items/bound_cps.shtml).

The retained server VIP policy gives 7/8/9/11/14 days at VIP 0/1/2/3/4+, matching
the documented [VIP extensions](https://vips.99.com/page/v2/vip_service.html).
The seven days describe the applied effect, not an expiry imposed on the loose
material in a player's bag. Equipment-family compatibility uses the installed
refinery catalog; bows, backswords and shields keep their specific material
families instead of accepting general one-hand materials.

## Ordinary-player behavior

Application and stabilization require the canonical loaded player, owned
inventory/equipment, no trade or booth, and a live character. The character item
lock and database owner-row lock cover each transaction. Exact material/target
UID, owner, type, position, warehouse, binding and stack preimages are checked.
Both inventory and equipped targets work. One material unit applies an effect;
one selected stone UID consumes one unit from its stack. Duplicate stone UIDs
are rejected. Insufficient points consume nothing.

A permanent and temporary effect can coexist as separate `itemadding` rows.
Their stored stabilization credit survives replacements. A previously stabilized
Super item needs another 160 points for Sacred, rather than another 430. When
credit already covers the replacement, stabilization consumes no stones, even if
the request selected some. Excess selected points are not additional credit.
Legacy permanent effects retain at least their level's cost as a compatibility
rule; this does not certify their historical payments or award achievements.

All effect changes, saved item identity/lifetime, credit and consumption commit
together. Afterwards the server updates inventory, equipment, skills' derived
combat stats, prestige and existing client effect packets. Failure fences the
session for reload. A one-second same-effect guard prevents immediate duplicate
application from repeatedly consuming a material stack. This is not a claim of
an indefinitely replay-proof native protocol.

The loader derives the active effect from saved rows, preserving a permanent
fallback and avoiding the former 32-bit timestamp reread. Effect lookup excludes
an expired temporary effect; the once-per-second online scan commits its cleanup
and recalculates equipped combat stats. Failed cleanup cannot make the expired
effect valid again. The existing item-expiry entry point uses the same transaction.
The old artifact-mode branch can no longer stabilize a refinery at artifact cost.
Soul purification/stabilization otherwise remains a separate legacy subsystem.

## Dis City packs

`3008057` and `3008058` now open through the ordinary item-use route. Their pools
contain 32 installed tradable material identities each, respectively level five
and six. Selection is uniform within the selected catalog pool; the original
game's undisclosed per-material probabilities are not claimed. Bound packs
produce bound material. Opening is atomic with a receipt and exact owned stack
preimage. A full bag blocks a partial-stack opening; a single pack can replace
itself in a full bag. A bounded per-session one-second guard suppresses immediate
stack duplicates. Foreign/copy objects and stale ownership cannot open a pack.

These handlers restore pack use and direct refinery application. Yin/Yang jade use
has since been [restored separately](DIS_CITY_JADES.md). Dis City's four-stage
world route, treasury and weekly completion remain pending.
No source-coverage achievement is added just for possessing or opening a pack.

## Validation and release

`tests/PotencyHunt/Verify-PotencyHunt.ps1 -RefineryOnly` passes 108 checks. Coverage
includes all 64 pack material outcomes, actual application and stabilization,
Super/Sacred costs, retained credit, exact expiry, permanent fallback, bound and
foreign items, production inventory reload, equipped stat changes, duplicate
requests, full bags, and injected failures during application, stone consumption,
expiry and pack receipt insertion. All 1,211 existing regression checks passed before the final explicit expiry
packet projection; the final build then passed all 108 refinery checks again.
The five producer-audit checks pass, with unchanged coverage of 77/337. The
original 6711 window and visual state still require native acceptance.
Staged assembly SHA-256:
`E2A5C5BE0210B3AA68FA8C43ECFFE943DA79F17A0BEECF62331FDC613149EB74`.

A read-only production audit found no unknown/mismatched saved refinery effects,
no duplicate permanent/temporary rows, and no refined item missing an effect row.
No production player records were changed. Schema addition is limited to the
pack-opening receipt table at normal feature initialization. Live deployment and
client verification remain separate roadmap gates.

## Remaining transfer gate

The [mail restoration](MAIL_REWARD_PERSISTENCE.md) now transfers the original item
UID and retains permanent/temporary refinery rows and stabilization credit in one
durable claim. Mail delivery and reload have automated coverage; native acceptance
remains pending. Auction cancellation and settlement still reissue items and need
their own ownership/payment/serialization restoration before deployment of the
new multi-effect state. The original direct-use validation above does not certify
the auction lifecycle.
