# Equipment blessing transaction repair

Implemented 2026-09-27 against baseline46b102a3. This hardens the existing
1009 action40 service; it does not introduce another protocol family or deploy
anything to the live server.

## Rules and evidence

The [official Blessed Items guide](https://co.99.com/guide/guides/blessed0214.shtml)
confirms Super Tortoise Gems and the1->3,3->5,5->7 tiers at costs1,3,5 gems.
Revora also retains its existing0->1 recipe of five Super Tortoise Gems. That
initial recipe is Revora behavior, not a verified claim from that official guide.
The loaded gem identity is700073.

Eligible targets are owned permanent equipment in the core categories
(headgear, necklace/bag, armor, weapon/offhand, ring/bracelet, boots), in the bag
or a compatible main/alternate equipment position. Arrows, rentals, locked or
suspicious items and excluded cosmetic/talisman/mount categories cannot use this
service. Bound materials cannot improve an unbound target; bound targets accept
bound materials without becoming unbound. No GM or character allowlist is used.

Each request must select exactly the recipe's distinct gem UIDs; one unit is
consumed from each selected stack. Reusing the same sorted selection for the same
target is rejected permanently, including after reload or at a later tier with
the same count. A different tier must use a different selection; this prevents a
late replay of the initial five-gem request from becoming a new -7 payment.

## Persistence and recovery

`EquipmentUpgrades.Initialize` creates `revora_equipment_blessings`, with unique
owner/target/starting-tier and owner/target/material-selection keys. Owner and
item locks, persisted ownership checks, target preimage, material consumption,
blessing result, receipt and any resulting equipment-set achievements share one
transaction. Each affected write must succeed. Cache validation happens before
commit; item/stat/achievement updates follow commit.

Errors roll back or fence the stale session for reload. A committed result is
never undone merely because cache projection fails. A retried completed recipe
cannot mint another blessing or consume the same selection again.

## Frame confidence

Existing source reads target at8, count at26 and UIDs from91. The hardened parser
checks1009/action40, count1/3/5, nonzero distinct identities, exact header and seal,
and either the minimum UID payload (99+4*N total bytes) or the existing shared
builder trailer shape (103+4*N). It rejects truncation and oversized input before
reading variable fields. The shared builder is hash-verified for socket action43;
an action40 sender/native UI path has **not** been verified. This is bounded
hardening of an existing route, not native-client interoperability acceptance.

## Validation

The isolated build and `tests/EquipmentAchievements/Verify-EquipmentSets.ps1
-Earning` pass307 checks across the full equipment earning fixture. Its ordinary
player spends the earned boss purse on all six pieces, upgrade/socket/enchantment
materials,198 +6 stones, second-socket materials and84 Super Tortoise Gems. All
eight full-set achievements10410-10417 survive production character/item reload.
Quality and drill RNG and schedule are controlled; this does not test natural
combat difficulty, statistical odds, live placement or native UI.

Blessing checks cover malformed headers/lengths/counts/seals/identities, wrong
materials, incomplete recipes, fake/unloaded/trading/noncanonical sessions,
locked/rented targets, bound-material rejection, persisted warehouse mismatch,
receipt-write rollback, concurrent duplicate calls, a committed result followed
by projection failure and full reload, stack-unit consumption, delayed replay
between equal-sized tiers and valid bound-to-bound blessing. The seeded negative
fixtures are separate from the ordinary earning journey.

- [x] Existing service has strict material validation and atomic payment/result.
- [x] Ordinary -7 and combined perfect-set earning, exact debits and full reload.
- [x] Controlled rollback and post-commit projection failure recovery.
- [ ] Actual transport loss at the blessing COMMIT response.
- [ ] Native action40 acceptance and live deployment.
