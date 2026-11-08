# Leftover nested and gear packs 721222–721245

M6.56b9f, 2026-09-22. Reconciliation only; not owned; not deployed.

Installed catalog identities exist for all 24 IDs in `itemtype-new.txt`,
including `PromotionPack` 721222, `ToughDrillPack` 721223, and the Super*
gear packs 721224–721245. Ordinary 1009/action-4 still reaches `UseItem`
because `GetPositionFromID` defaults to Inventory for non-equipment IDs.
Direct `ItemHandler.UseItem` is the second path.

`emoneyshop.ini` lists every ID under EShop 8422 (MoneyType CP). Stock
membership is not an ordinary-player issuance route. The generic shop-buy
handler in `ItemHandler.HandleBuyFromNPC` enters an EShop only when
`client.Map.Npcs.TryGetValue(request.UID, out npc)` succeeds, except the
explicit global UID 2888. It then looks up that same request UID in
`EShopFile.Shops`. The configured installed database has no `public.npcs`
or `public.sobnpcs` row with `id=8422`. `emoneyshopV2.ini` contains neither
8422 nor any of 721222–721245. There is no other current shop, exchange,
StageGoal, NPC `Add`, or `DropItemTable` producer.

These 24 leftover cases stay **live-split-write**. They are **not** moved
onto an owned pack service. Fixture injection is consumer coverage, not
issuance.

## Leftover consumer terms (unchanged)

| Pack | Level | Leftover count gate | Outputs | Refusal channel |
| ---: | ---: | --- | --- | --- |
| 721222 | 10 | `Count < 38` | plus-8 730008, plus-6 730006 | two-argument `Message.System` |
| 721223 | 10 | `Count < 38` | plus-0 1200005, plus-3 730003 | `Color.Red` / `Message.TopLeft` |
| 721224–721245 | 10 | `Count < 32` | plus-0 1200000, plus-0 723700, five plus-0 723017, plus-5 White empty-socket catalog-durability gear | `Color.Red` / `Message.TopLeft` |

Level text is `You must be atleast level 10 to open the Pack`. Capacity text
is `You need to make atleast 2 free spots in your inventory.` or
`... 8 free spots ...`. There is no success message.

Gear identities: 131069, 111069, 130069, 118069, 135069, 112069, 134069,
114069, 133049, 113049, 120129, 150139, 121129, 152149, 160139, 410139,
420139, 480139, 900049, 601139, 421139, 500129. `Color.White=9`,
`Gem.EmptySocket=255`. Leftover `UseItem` sets `SpiltStack=true` before the
switch, so five `723017` units may occupy one stacked inventory object.
723700 remains ExperienceConsumables. 1200000 remains PrayingStones. Direct
daily P7 is unchanged.

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, catalog-only Surprise box
722178, other leftover packs, Host restart, native acceptance and deployment
remain open. Parent [M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.


## Source recheck and sequencing (2026-10-03)

Before selecting an atomic-consumer implementation, a recheck of committed
`ItemHandler.UseItem` at79d12947 confirms that all22 gear cases set only
`SocketOne=EmptySocket`. `ConquerItem(true)` zero-initializes its buffer, leaving
`SocketTwo=NoSocket`; the plural wording above must not be interpreted as two
empty sockets. The committed24 cases give unbound outputs. Existing owner work
in progress changes those outputs to copy the source pack's binding. Preserve
that work; do not silently replace its reward terms with the older baseline.

These catalog-only packs still lack a verified ordinary issuer. Their consumer
safety work remains pending; it is not a completed quest or event. The current
priority queue's demonstrated event lifecycle gaps take precedence. This source
recheck changes no reward, ownership, issuer or deployment state.
