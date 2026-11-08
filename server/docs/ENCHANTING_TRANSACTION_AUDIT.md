# Enchanting transaction and edition audit

## Current implementation reference (2026-10-01)

The amount-policy and zero-result notes below are historical. Commit `fc7bb8fb`
implemented the documented gem-family ranges in `ItemEnchantment.Bounds`, with
inclusive endpoints and rejection of unsupported refined/super Thunder/Glory
materials. [Outcome repair and validation scope](EQUIPMENT_ENCHANTMENT.md).
The retained career subsequently earned full-set enchanting through ordinary
gem purchases/use and cold recovery. Do not repeat the zero-result repair from
the older open notes. Exact original6711 numerical parity and native acceptance
remain separate from these implemented rules; this reconciliation ran no new
enchanting tests.

## Earned tool and material route

The original ordinary collection character now continues from the same committed
daily-boss bounty into enchanting. Its untouched 10,000,000 silver buys a 1,000
silver Pickaxe through the installed Storekeeper and pays the Conductress's 100
silver Plain Mine fare. The production mining intent/tick creates a Normal Dragon
Gem with saved mining provenance. Ordinary unequip, enchanting and equip consume
that exact mined UID and earn 10430. Production entity/item reload retains the
enchanted Pickaxe, achievement, consumed gem and exact 9,998,900 silver balance.
No tool, gem or spending currency is supplied for this extension. Qualification,
boss damage, mining time and the selected mining draw remain controlled fixture
inputs; it does not certify combat difficulty, random-rate balance or native UI.

This cross-feature route exposed two real wardrobe integration defects. The
WeaponBookShop/Pickaxe transaction and MiningRewards compared the live bag count
to all saved position-zero items, including wardrobe storage. Their saved bag
counts now exclude `inwardrobe` rows. The character's existing collection remains
owned and is no longer mistaken for bag contents. Bag capacity checks still apply.

Build, 462 collection route checks, 137 mining regression checks and 495
experience/book-shop regression checks pass. The
enchanted-tool route is earned under the retained Normal-gem outcome calculation;
the owner-selected replacement amount policy, complete gem-family behavior and
native/deployment gates remain open.

## Atomic selected-material repair

`ItemEnchantment.Apply` now owns the mutation. Production initialization creates
`revora_item_enchantments`; the existing action-28 handler supplies its currently
calculated roll. The service locks the canonical owner's item context, locks the
database owner and both item rows, verifies saved preimages, consumes one unit
of the selected gem UID, saves the non-decreasing result and writes a receipt in
one transaction. It never substitutes an unrelated 700013. Single units are
deleted; stacked inputs are decremented. Identical target/material requests within
one second are suppressed using committed receipts. Later deliberate attempts
remain possible. This is not indefinite replay protection without a client
operation identifier.

Only permanent, unlocked, non-suspicious bag equipment and materials qualify.
Core equipment categories 1–6 and 8 are supported. Both full IDs require loaded
catalog entries. Bound gems cannot upgrade an unbound target; no binding is
silently removed. Trading, booths, poker, simulated/stale/fenced players and
non-canonical inventory references are rejected. Post-commit projection failure
requires production reload; it does not refund or reapply a committed mutation.

Build and 456 collection checks pass. New checks exercise ordinary enchant/equip
handlers with supplied weapons/materials, selected Normal gem stack consumption
under concurrent callbacks, untouched unrelated Super gem, denied contexts,
suppressed material/target/receipt writes, production reload retry, equipped
achievement 10430 and final item/achievement reload. No complete earned
gem-to-weapon route or native presentation is claimed. The build includes owner WIP.

The amount calculation is unchanged while the owner edition choice is pending.
Existing non-Dragon refined/super zero-result branches are **not corrected** by
this transaction repair. Numerical parity, binding-rule parity, full prerequisite
earning, native acceptance and deployment remain open.

## Confirmed-rule safeguards (2026-09-25)

The handler now rejects non-gem full IDs before checking the Gem suffix, refuses
an already-capped 255-HP target without consuming material, and preserves the
existing HP when a roll is lower. The installed client supplies independent
evidence: StrRes.ini entries 10469/10474 describe the cap and retained result;
ChatTips.ini describes retaining a better prior enchantment. File SHA-256 values:

- StrRes.ini: B527242F6AC16D9F5C5CB4257202962684E8B9D1ADE5D3A3E1693F0BF681BF0D
- ChatTips.ini: 051C837D06070F56BC2D48A10353A94910DF59C7E6253ACC81AE85E74E3A2236

The isolated build and 442 collection checks pass. New supplied-material cases
reject a Meteor whose suffix previously matched a gem, retain an unconsumed gem
at 255 HP, and preserve 254 HP through the existing Super Dragon roll. This is
server fixture evidence, not native-client acceptance or an earned enchanting
journey. No client text/assets are committed.

At that safeguard checkpoint, wrong-material consumption and separate writes were
still open; the atomic repair above supersedes those two findings. Numerical
outcome rules remain unverified for 6711. The owner has been asked to choose
custom fixed increments or the documented legacy random edition. No replacement
amount policy is assumed or activated.

Reviewed 2026-09-25 at source commit 5e9b328, which includes equipment guard
9d2c4ab. This audit does not certify or repair the ordinary enchanting route.

## Original source defects at the audited commit

`Network/GamePackets/ItemHandler.cs`, `EnchantItem`, is called by the current
`ItemUsage` action 28 handler (and two older handler files). Both selected UIDs
are looked up in the player's bag, but the operation then:

- Classifies the material using a byte cast of `ID % 1000`, without establishing
  that its full ID identifies a gem. For example a Meteor's 1088001 suffix matches
  the Normal Phoenix enum. This is source-level acceptance, not a native-client
  exploit demonstration.
- Writes the new enchantment before material removal, in a separate database
  operation. A failure can therefore leave a changed item without the intended
  payment. There is no encompassing owner lock, transaction or durable receipt.
- Removes item type 700013 in both outcome branches rather than the selected UID.
  Its return value is ignored. The selected material may survive, an unrelated
  Super Dragon Gem may be charged, or no gem may be consumed at all.
- Replaces the previous value even when the new roll is lower. Most refined and
  super non-Dragon families currently yield zero because their branches are
  commented out. `Random.Next` also excludes the stated upper bounds.

Do not mark achievement 10430's earning route complete merely because the saved
equipment observation passes. That producer neither fixes nor certifies this
mutation path.

## Edition evidence

The official legacy/iPad guide
https://co.99.com/ipad/guides/enchanted.shtml describes random family/grade ranges
and preserving a better existing value. It is evidence for that edition only.
The official PC 2018 update
https://co.99.com/guide/event/2018/melt/ instead says that fixed improvement based
on gem type and grade replaces those old random ranges. The page does not publish
the numerical increments needed to implement the replacement.

Neither source alone proves genuine 6711's exact numeric contract. Restoring the
commented random ranges must not be represented as verified 6711 behavior. The
obvious candidate PC guide URL `/guide/items/enchanted.shtml` was inaccessible;
that observation does not prove the information is unavailable everywhere.

## Implementation requirements

1. Resolve the patch-specific numeric rule from authoritative evidence or an
   explicitly documented owner-selected Revora edition; retain a clear separation
   between the rule and the transaction implementation.
2. Validate exact supported gem IDs, target categories, canonical player ownership,
   binding compatibility, bag location, rental/lock state and frame boundaries.
3. Serialize on the owner item lock and lock the database owner. Verify both saved
   item preimages; consume one unit of the selected UID and update the target in
   the same transaction as a durable result/material receipt.
4. Handle stacked materials and request replay without duplicate consumption.
   Project committed inventory and item changes only after commit; fence and
   recover on a projection failure or ambiguous persistence outcome.
5. Test payment/output/receipt failures, stale sessions and preimages, spoofed gem
   IDs, invalid target slots, all supported family/grade boundaries and binding,
   stacked input, retry, concurrency, production reload and ordinary equipment.
6. Continue an earned material route through enchanting, equipment and 10430.
   Keep native-client and production deployment acceptance separate.

The full-set product question remains independent: this transaction work does not
need to wait for a decision about which equipment slots constitute a complete set.
