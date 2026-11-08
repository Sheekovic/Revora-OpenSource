# Leftover 3-in-1 medicine bundles 720010–720017

Current status2026-10-03: M6.56b9h1 atomically handles all eight held bundles
through HealingMedicine. The historical issuance audit remains valid: no shop
or quest issuer is restored. Native acceptance and deployment stay open.

## Historical source audit (2026-09-22)

M6.56b9h originally classified these as unowned leftover consumers.

Installed catalog identities exist for all eight bundles and their pill
outputs in `itemtype-new.txt`. Ordinary 1009/action-4 still reaches `UseItem`
because `GetPositionFromID` defaults to Inventory. Direct `UseItem` is the
second path. Leftover cases stay **live-split-write**. Fixture injection is
consumer coverage, not issuance.

| Bundle | Catalog | Leftover outputs |
| ---: | --- | --- |
| 720010 | Amrita | three plus-0 unbound 1000030 |
| 720011 | Panacea | three plus-0 unbound 1002000 |
| 720012 | Ginseng | three plus-0 unbound 1002010 |
| 720013 | Vanilla | three plus-0 unbound 1002020 |
| 720014 | RecoveryPill | three plus-0 unbound 1001030 |
| 720015 | SoulPill | three plus-0 unbound 1001040 |
| 720016 | RefreshingPill | three plus-0 unbound 1002030 |
| 720017 | ChantPill | three plus-0 unbound 1002040 |

Gate is `Inventory.Count <= 38`. Refusal is `Constants.FullInventory`
(`Your inventory is Full.`). There is no success message. Leftover `UseItem`
sets `SpiltStack=true` before the switch, so three stackable pills may occupy
one inventory object. Output catalog stack size is 5.

## Issuance

`ShopFile.Load()` reads `Constants.ShopsPath` (`database\shops\Shop.dat`).
That file stocks **720011–720013** on gold shops 8159–8162 and 8593–8596
(SkillTeamPKTeleporter). Stock membership is not ordinary issuance. The
generic shop-buy handler enters a shop only when
`client.Map.Npcs.TryGetValue(request.UID, out npc)` succeeds, except UID 2888.
Production map loading can populate `Map.Npcs` from `public.npcs`,
`public.sobnpcs`, or `public.furniture`. The installed database has no rows for
those eight shop UIDs in any of the three tables. `emoneyshop.ini`,
`emoneyshopV2.ini`, `Shop.txt`, exchange, StageGoal, `Npcs.cs` and
`DropItemTable` do not produce any of 720010–720017. 720010 and 720014–720017
are catalog + leftover consumer only.

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, catalog-only Surprise box 722178,
nested/gear leftover 721222–721245, classified silver leftovers, other leftover
packs, Host restart, native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.


## Atomic bundle delivery (2026-10-03)

The previous consumer-only classification left callable item exchanges exposed
to source loss or incomplete output delivery. The owned medicine service now
intercepts both validated1009/action4 and direct ItemHandler.UseItem for all
720010-720017 IDs. Each transaction consumes one owned bundle unit, inserts a
fresh three-pill stack and records its UID in `revora_medicine_bundle_uses`.
Existing pill stacks are not modified or merged. Failed transactions roll back
source, output and journal together; uncertain sessions are fenced for reload.

The output mapping above, plus0, unbound pills, no success message and original
Inventory.Count<=38 gate are preserved. A separate three-pill stack fits the
installed five-pill catalog limit, which is checked before consumption. Bundle
opening changes neither HP nor MP and does not consume a pill. Actual healing
still follows the existing health-mutation and NoDrugsTime path. Output binding
is unchanged even for bound source bundles.

Private phases `career-medicine-bundle-earn` and `-reload` pass on ordinary
non-GM identity4294918501:

- [x] All eight mappings deliver exactly one stack of three correct unbound,
  plus0 pills; bound/unbound source fixtures alternate. Four wire cases use
  eight concurrent requests; the other four use direct ItemHandler entry.
- [x] Inventory count39 refuses without change; count38 permits delivery.
  Malformed-seal and fake-actor requests preserve the exact snapshot.
- [x] A failing journal-insert trigger, after attempted source/output writes,
  rolls everything back and fences the session. A fresh owner reload restores
  the exact pre-failure state; the trigger is removed before retry.
- [x] Consumed-source replay through either entry changes nothing. Opening
  bundles leaves HP/MP unchanged. Test-only capacity filler is removed after
  the boundary case; no other character's possessions are changed.
- [x] A separate cold process preserves eight delivery receipts, eight output
  stacks totaling24 pills, their binding/quality and the currency snapshot.
  Repeated service initialization preserves that state.

Bundles and boundary filler are supplied ownership fixtures, not earned rewards.
This closes consumer safety only: no new ordinary issuer, healing-rate change,
quest family or general item-consumer completion is claimed. Existing medicine
consumption and health ownership are unchanged; these checks exercise unpacking.
The dated split-write census is not a new current total.

An isolated build passed. Candidate executable SHA256:
`943AA1FBE2BF19433045F4CAF5CDA0FE4BCBEF2EC1EFAE9CD703995346FC6435`.
No live process, native client or deployed release changed. Remaining compound
consumers and native acceptance remain open.
