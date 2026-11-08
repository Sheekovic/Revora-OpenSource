# Equipment achievement checkpoint

## Enchanted item observation

Achievement 10430 observes owned saved equipment with enchantment 1–255 in a
valid core slot (1–6 or 8), after the existing slot, rental, stack and ownership
checks. It records one historical receipt. The fixture tests zero, 1, 255, 256
and negative values across eleven item definitions, including excluded cosmetics
and talismans. These are supplied-state checks, not a complete gem-to-enchantment
earning route. The later enchanting transaction audit and earned route are recorded below.

Isolated build, 439 collection/equipment checks, 122 progression checks and five
achievement audit tests pass. See [rule research](EQUIPMENT_SET_RULE_RESEARCH.md)
for the still-unresolved full-set requirements and pending owner decision.

The later [earned enchanting route](ENCHANTING_TRANSACTION_AUDIT.md#earned-tool-and-material-route)
now connects boss gold, ordinary Pickaxe purchase and mine travel, a mined Normal
Dragon Gem, atomic enchanting, equipment and production login. It closes that
specific material route; complete gem-family amount rules remain unresolved.

## Accessory equip request guard

The ordinary accessory equip handler now rejects requested slots other than 15
and 16. Previously accessories bypassed category correction entirely, allowing
an unrelated slot to reach inventory/equipment mutation. Twelve invalid requests
cover ordinary armor/weapons/talismans, alternate accessory positions and overflow
values. Each preserves the earned Terato item in the bag and creates no equipment
achievement. Valid left equip, production login and ordinary removal/re-equip to
the right remain supported. This does not certify the rest of the legacy equip
handler's transaction, class or concurrency behavior.

## Single Super item (2026-09-25)

Achievement 10409 now records a loaded Super-quality item in a matching saved
active equipment slot. Quality uses the existing equipment ID suffix rule;
garments and weapon accessories are explicitly excluded because their suffixes
identify designs. Mount armor, steeds, bottles, relics and alternate equipment
are outside this observation. A valid one-hand weapon may occupy the left slot;
two-hand weapons and backswords are not accepted there by this observer.

The same ownership, stack, warehouse, rental and duplicate-slot checks as the
other equipment achievements apply. Receipts are historical and idempotent.
The existing boss-funded Fan/Tower route earns 10409 through ordinary equipment
and retains it through production login; the bag purchase alone does not qualify.
Armor, headgear, boots, lower-quality talismans, cosmetic suffixes and offhand
weapons have supplied-state coverage, not independent earning-route certification.

Build, 395 collection/equipment checks, 122 progression checks and five achievement
audit tests pass. Full-set semantics and native presentation remain pending.

Updated 2026-09-25. Source implementation and isolated server validation only;
not deployed or accepted in the native client.

Achievement 10419 records an equipped garment, 10418 an equipped Fan or Tower,
and 10503 an equipped weapon accessory. IDs are tracked in the hash-checked
achievement inventory. `EquipmentAchievements.Record` reconciles saved ownership
inside the existing progression evidence transaction, with idempotent receipts.
Items must have a loaded definition, their correct equipment slot, warehouse zero
and stack size one. Ambiguous duplicate occupants of a slot are excluded.
Bag items, warehouse items, wrong slots, invalid rental data and expired rentals
do not qualify. Valid rentals can satisfy an equipment achievement; the owner's
permanent-only rule applies separately to collection milestones.

The garment achievement survives the existing ordinary character's boss-funded
purchases, wardrobe equipment, withdrawal and production reload. The initial
Fan, Tower and accessory checks used supplied saved equipment fixtures; earned
follow-ups are recorded below. Reconciliation uses existing progression
refresh/login behavior; immediate native presentation is not certified.

Validation: isolated build, 307 collection/equipment checks, 122 progression
checks, five achievement audit tests and five collection capacity tests passed.
The equipment cases cover permanent and active rental equipment, all exclusions
above and repeated reconciliation. Existing unrelated owner changes were present
in the build; this is not a clean release artifact.

## Earned accessory follow-up

The existing Dragon Slayer route now continues from earned ghoul pills and the
crafted Dragon Pill through Terato's committed victory, floor reconstruction,
public pickup of the seven-day accessory 350001, ordinary equipment and login.
Pickup alone does not award 10503; valid saved accessory equipment does. Production
entity/item reload restores both the equipment and exactly one receipt.

The expanded Grotto/Dragon Slayer/Terato suite passes 280 checks using the already
built server assembly. Combat damage, loot selection and prerequisite character
qualifications are controlled by the fixture; this does not certify combat
balance, random drop rates or native presentation. The accessory is legitimately
time-limited and does not add a permanent collection design.

Full equipment-set predicates and native client acceptance remain open.

### Left-slot correction

The original helper `GetPositionFromID` returns the right-accessory slot for all
accessory types; even the 380001 fixture therefore tested slot 15. Earlier claims
that this covered both slots were incorrect. The ordinary equipment path accepts
either hand for `ItemHandler.IsAccessory` items. Achievement reconciliation now
accepts those loaded accessory definitions in either slot 15 or 16, retaining
ownership, rental and duplicate-occupant checks. Non-accessories in slot 16 do
not qualify. No equipment dispatch or client protocol was changed.

The earning fixture now equips Terato's actual loot in slot 16 and verifies that
production login preserves that slot and its achievement. The earlier slot-15
route passed in commit 366811b. Current build, 324 collection checks and 280
Grotto route checks pass. Supplied-state cases explicitly exercise both hand slots.

## Earned talisman follow-up

The original ordinary boss earner purchases the existing CP mall's 201009 Fan
and 202009 Tower for a combined 2,580 CPs, after completing the fifty-garment
route. Both use loaded catalog prices and stock; no currency is supplied for
these purchases. The final purse is 975,360 CPs and 2,500 RVPs.

These two stocked items now use the existing atomic CP shop transaction instead
of the generic purchase path. Debit, item creation and purchase receipt commit
together. Suppressing each of those three writes leaves the saved purse and
inventory unchanged, fences the stale session, and permits retry after production
reload. Immediate identical retries charge once. Existing bound-CP, stock,
proximity and inventory protections are preserved by the same transaction.

The Fan in the bag does not award 10418. Ordinary equipment of the Fan and Tower
persists slots 10 and 11; production entity/item reload retains both items and
exactly one achievement receipt. The isolated build and expanded collection suite
pass 319 checks. Native purchase/equipment presentation and deployment remain
pending. RevoraMaster's separate mount-kit purchase was subsequently repaired and validated
in MOUNT_KIT_TRANSACTION_AUDIT.md.

## Socketed equipment milestone (2026-09-25)

Achievement 10429 now observes a saved socketed item in a valid active core
slot (1?6 or 8), Fan or Tower slot. Identity comes from the same hash-bound
Achievement.dat catalog cited in EQUIPMENT_SET_RULE_RESEARCH.md. Revora treats
one or two sockets as satisfying the single-item milestone. An empty socket
(255) or a defined nonzero gem value qualifies; malformed values, an isolated
second socket and cosmetic fields do not. Existing ownership, category, rental,
stack and duplicate-slot guards still apply. This observation does not certify
the legacy socket-opening or gem-insertion transactions, or gem-family compatibility.

The ordinary boss-funded collector buys the atomic RevoraMaster mount kit,
equips its socketed Fan through normal item dispatch and retains exactly one
10429 receipt through production entity/item login. Buying the kit in the bag
alone earns no credit. No extra currency or socket material is supplied for
this route. The regular mall Fan is unsocketed and is not its source.

Isolated build and 580 collection/equipment checks pass, including empty/filled,
one/two-socket and malformed-value observations across eleven item definitions.
The suite also retains mount-kit rollback, replay and postcommit recovery checks.
Native display and live deployment remain pending; unrelated owner edits were
present during validation, so this is not a clean release build.

## Retained Fan, Tower and bought accessory (2026-10-02)

Ordinary retained character4294907001 continues from the Couple PK checkpoint.
One owned722136 reward moves to Stone City storage to free a bag slot. The existing
mount-kit Fan201009/UID60753038 and Tower202009/UID60753039 are withdrawn through
the warehouse handler and equipped normally in slots10/11. The installed CP mall
2888 sells Broom350011 for199 CPs; the character buys it and equips its exact UID
in accessory slot15. The earlier bow, marriage, event rewards and all bank items
remain preserved. This reuses the documented paid mount-kit provenance, not a
claim to have repeated the independent Fan/Tower quest chains.

Achievements10418 and10503 are saved and survive actual game/database restart.
`career-retained-accessories-earn` and `career-retained-accessories-reload` pass
on unchanged private candidate SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
Checkpoint `revora_retained_accessories_probe` is step5; exact nested possessions,
currencies, equipped UIDs and earlier reward ledgers match on cold reload.
Repeated achievement refresh preserves the complete snapshot.

The initial fixture checked receipts before calling the achievement refresh.
Equip and purchase were already saved, and the next load added exactly the two
legitimate equipment receipts. A full snapshot comparison confirmed these were
the only changes; a one-time exact-hash recovery accepted that state without
resetting items or inserting evidence. The helper now refreshes achievements
before saving a checkpoint. The recovery branch was removed and the cleaned
fixture passes another cold reload. NPC approach is controlled; prior career
funding retains its documented mixed provenance. Native UI/visual acceptance and
live deployment remain pending. No production build or restart was performed.
