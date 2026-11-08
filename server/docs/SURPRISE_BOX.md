# Surprise box 722178

Current status2026-10-03: M6.56b9d1 implements atomic consumption through
ConquerPointBags. The catalog-only source audit below remains valid: no ordinary
issuer was added or verified. Consumer safety is complete privately; quest
restoration, native acceptance and deployment are not claimed.

## Historical source audit (2026-09-22)

M6.56b9d originally classified this as an unowned leftover consumer.

Installed catalog identity `722178@@SurpriseBox` exists in `itemtype-new.txt`.
The catalog description is leftover Halloween-quest copy. There is no executable
Halloween quest, shop, exchange, StageGoal, NPC `Add`, or `DropItemTable`
producer for this ID. `Shop.dat` does not contain `722178`.

Ordinary 1009/action-4 still reaches `UseItem` because `GetPositionFromID`
defaults to Inventory for non-equipment IDs. Direct `ItemHandler.UseItem` is
the second path. The leftover case is:

- `Inventory.Count <= 38`: consume one source unit and `ConquerPoints += 2000`
- otherwise send `You Must have 1 space in you Inventory To Open You Box`
  (`Color.White`, chat 255)
- no success message
- the PowerEXPBall `722057` grant is commented out, so this is source + CP only

That leftover path is catalog/consumer reachable only. Fixture injection is not
an issuance route. `ConquerPointBags` does not own `722178`. This ID is **not**
moved onto an owned CP service.

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, nested/gear packs 721222–721245
(catalog + unreachable EShop 8422 stock) and other leftover split-writes
remain open. Native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.


## Atomic consumer and process validation (2026-10-03)

The existing source+2000-CP case was still callable for an owned722178. It
removed the source separately from the currency write, exposing held items to
partial delivery. That justifies an atomic consumer independently of restoring
an issuer. ConquerPointBags now handles both ordinary sealed1009/action4 and
direct ItemHandler.UseItem entry. Invalid requests fail closed in that service;
the legacy switch branch is intercepted, including failed/disabled cases.

Preserved terms: one source unit,2000 ordinary CPs, the exact inventory-count
limit of38, the existing refusal text at39/40, no success announcement, and no
commented-out EXP Ball. Binding is preserved on the consumed receipt; bound
boxes still give ordinary CPs as before. No new price, reward or quest is added.
The existing ledger's item-type constraint is extended transactionally to include
722178 while keeping all35 previous IDs. Source deletion/decrement, the durable
CP preimage update and the journal insert commit together. Overflow, ownership,
trade/booth and packet checks use the existing CP-bag service.

Private phases `career-surprise-box-earn` and `career-surprise-box-reload` pass
on ordinary non-GM identity4294918401:

- [x] At39 inventory entries the real use packet preserves every item, CP and
  journal row. At38 it becomes eligible. A malformed seal and a fake-actor
  direct request change nothing.
- [x] A receipt-insert trigger fails after the transaction attempted item/CP
  writes. Both roll back, the session is fenced, and a fresh owner reload
  recovers the exact pre-failure goods and balance. The test trigger is removed.
- [x] Eight concurrent ordinary use requests consume one bound box and save
  exactly one2000-CP journal row. A second box used through direct ItemHandler
  entry pays2000 once; repeating either consumed request pays nothing.
- [x] A previous registry member729911 still consumes for3 CPs after the schema
  extension. Exact balance4003 above the starting purse, inventory and all three
  journal rows survive a separate cold process and repeated initialization.

The catalog boxes and inventory-boundary filler are explicit fixture inputs;
this proves ordinary ownership/consumption, not quest earning or acquisition.
No new completed quest family is counted. Source/malformed/binding checks reuse
existing service protections; this batch does not claim a new exhaustive audit
of every legacy pack or every CP-bag rate.

Isolated build passed. Private executable SHA256:
`4DB4AF1250EC269D4BD74362D30675EE8F383AAF8F789E12415FCF0A2A17625A`.
The unrelated private vote PostgresException remains outside this validation.
No live deployment, restart or client patch occurred. Remaining M6.56b compound
consumers and native acceptance stay open.
