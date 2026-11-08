# Equipment-set achievement implementation contract

Reviewed 2026-09-27. All ten equipment-set identities now have producers using
the owner-approved Revora definitions. Main-set and gem observations read durable
items; alternate-set credit requires an actual switch to the complete bank.
Source presence is not ordinary earning acceptance.

## Confirmed client identities

Read-only inspection of the approved local `ini/Achievement.dat`, SHA256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`,
confirms these numeric requirements. The existing catalog codec was used with
its expected hash. No proprietary catalog or descriptions are copied into Git.

| Identity | Requirement to implement, paraphrased |
| --- | --- |
| 10410 | Complete equipped set at Unique quality |
| 10411 | Complete equipped set at Elite quality |
| 10412 | Complete equipped set at Super quality |
| 10413 | Complete equipped set with +12 bonuses |
| 10414 | Complete equipped set with -7 blessing |
| 10415 | Sockets throughout the equipped set |
| 10416 | Enchantment throughout the equipped set |
| 10417 | Complete set combining Super, two sockets, +12 and -7 |
| 10428 | Twelve high-quality socketed gems |
| 10432 | An alternate equipment set |

The [official achievement guide](https://co.99.com/guide/guides/achievement.shtml)
confirms equipment observations as achievement triggers, but does not enumerate
these predicates or their slot exclusions. The
[Way of Hero guide](https://co.99.com/guide/guides/hero_system.shtml) describes
six-or-more equipment pieces for its own quality challenges. That is a useful
related-system lead, not proof of these achievement identities' slot rules.
A newer [Hero Biography event](https://co.99.com/guide/event/hero_biography_-_apemount.shtml)
also mentions six pieces; its different event and date prevent treating that as
original 6711 achievement evidence. Sources were reviewed on the date above.

## Approved Revora rules

The owner approved this definition on 2026-09-27: headgear, necklace/bag, armor,
main weapon, ring/bracelet and boots, plus any equipped offhand. Cosmetics,
talismans, mount and crop do not count. This is documented Revora policy,
not a claim about verified retail slot rules.

Implementation details:

- Main equipment positions 1,2,3,4,6,8 are mandatory. A present offhand at5
  must also qualify, including shields, Hossu and valid one-handed weapons.
  Arrows are ammunition and excluded from attribute requirements. Malformed,
  stacked or duplicate occupied core/offhand slots invalidate the observation.
- Quality7/8/9 means Unique/Elite/Super; higher quality earns lower milestones.
- Every required piece must have exactly +12 for10413, exactly -7 for10414,
  and an enchantment from1 through255 for10416. Invalid over-limit values fail.
- 10415 requires a valid first socket and either no second socket or a valid
  second socket. Empty holes count as sockets. 10417 requires both sockets,
  Super quality, +12 and -7 on every required piece at the same observation.
- For10410?10417 and10428 only main-bank rows count. Alternate rows cannot fill missing main slots.
  Bag, warehouse and other-owner items cannot contribute.
- Existing equipment-observer rental rules remain: permanent or unexpired
  rentals with valid timestamps. Durability does not affect equipped-possession
  achievements. The permanent-only cosmetic collection rule is separate.
- On2026-09-27 the owner approved10428 as12 Super gems in the main core set plus
  equipped offhand; each filled socket counts, including repeated gem types.
- The owner approved10432 as a complete six-core-slot alternate bank plus valid
  offhand if present, earned when the player switches to it. Both definitions
  are Revora interpretations.10432 validates saved alternate slots21,22,23,24,26,28
  and any non-ammunition offhand25 against the switching character?s cached items.
  It never awards from a normal inventory observation or a repeated request to
  activate an already active alternate bank.

## Existing integration and acceptance

`Game/Features/EquipmentAchievements.cs:Record` already observes six separate
single-item achievements from durable owned equipment rows. It excludes duplicate
slots, unrecognized catalog IDs, incompatible positions and expired rentals.
`ProgressionEvidence` calls it inside the same transaction that reads the saved
achievement snapshot. The eight set checks extend that path; they do not grant from
client packet claims, bag contents, an administrative equipment grant alone or
an in-memory item list that disagrees with persistence.

The implementation must distinguish active/alternate banks, validate each slot,
require all mandatory slots and apply all combined10417 conditions to one
simultaneous set. Test missing/mismatched/duplicate slots, arrows, optional
weapons, thresholds, expired rentals, empty versus filled sockets, reload and
replay. Prove an ordinary equip/upgrade earning journey before changing route
classification; a predicate unit test alone is insufficient.

- [x] Hash-bound identity and numeric-condition inspection.
- [x] Identify current durable observer integration and missing producer IDs.
- [x] Owner approves the documented six-core-slot plus offhand definition.
- [x] Implement eight set observers and refresh source inventories (330/337).
- [x] Isolated build and103 database checks: thresholds, every missing core slot,
  duplicate/mismatched slots, optional weapons/shields/Hossu, ammunition, invalid
  sockets, valid/expired rentals, owner/bank isolation, rollback and replay.
  Saved achievements survive connection-pool reset and removal of equipment.
- [x] Ordinary acquisition/equip/upgrade and full character reload for10410,
  10411-10417:307 earning checks pass (scope below).
- [x] Ordinary -7 and combined perfect-set earning after the [blessing repair](EQUIPMENT_BLESSING.md).
- [x] Owner approves the Super-gem count and alternate-set switch definitions.
- [x] Implement and validate10428 and10432:127 database and429 combined earning checks.
- [ ] Native presentation and deployment remain separate gates.

## Reproducing the bounded database check

Build the isolated Revora executable and dependencies into
`obj/mrms2017-validation/bin`, then run
`tests/EquipmentAchievements/Verify-EquipmentSets.ps1`. It compiles the fixture
against that executable with warnings as errors, checks the fixed private
PostgreSQL tool hashes and endpoint, starts/stops only that disposable instance,
and creates/drops only `equipment_set_fixture`. It uses existing DPAPI-protected
local credentials and preserves public candidate data. Output is recorded in
`obj/equipment-set-tests.log`. Tests seed persisted equipment; they do not claim
an ordinary-player acquisition journey or native client acceptance.

## Earned quality, sockets and enchantment (2026-09-27)

`Verify-EquipmentSets.ps1 -Earning` reuses the ordinary boss-bounty fixture and
shared production character-reload helpers. The character starts with no money,
items or materials and earns one scheduled boss bounty through the damage/death
handler and durable reward transaction. It then spends that reward through the
ordinary shop handlers:

- Six silver-stock pieces from configured shops4,1 and5 (Trojan coronet118003,
  necklace120005, armor130005, blade410005, ring150005 and boots160015).
- Twenty-four bought DragonBalls upgrade the equipped pieces through Refined,
  Unique, Elite and Super.10410-10412 appear only at their full-set thresholds.
- Sixty-one bought DragonBalls open one socket on each piece.10415 appears only
  when the sixth piece is socketed. Replaying each request consumes nothing extra.
- Six bought enchanting gems are consumed through ordinary unequip/enchant/equip.
  10416 appears only after the sixth enchanted piece is re-equipped.
- Full production character/item reload retains all five achievements, one row
  per identity, six equipped items, exact loaded-price CP expenditure and the
  unspent50,000 earned RVPs. No GM privilege or achievement insertion is used.

The fixture controls schedule and boss damage, places the existing configured
shops nearby, and fixes quality-upgrade RNG to a successful draw. This proves
server earning/transaction/reload behavior, not natural combat difficulty,
statistical odds, live NPC placement or native presentation. It uses the same
private instance and its own `equipment_earning_fixture` schema; public player
history is preserved. Shared fixture sources are compiled from HEAD to exclude
owner WIP, with the two owned equipment-earning sources taken from the worktree.
No production executable changed in this follow-up.

The zero-result enchantment limitation found during this route is now repaired:
[range evidence and365-check validation](EQUIPMENT_ENCHANTMENT.md). The58 added
checks include inclusive endpoints, unsupported material preservation and retaining
a better existing enchantment. Native presentation/deployment remain pending.

## Earned +12 extension (2026-09-27)

The same ordinary character continues from its purchased Super/socketed/enchanted
set. Six33-piece purchases from the existing CP mall provide198 +6 stones730006
at the loaded price of2,916 CPs each:577,368 CPs total. The protected shop creates
correct Plus6 item values and suppresses repeated purchase requests. Six normal
2036 Plus compositions consume the selected stones and reach +12 with zero capped
progress. Repeated compose requests consume nothing more; saved receipts prove
six purchases and six33-material compositions.10413 appears after the sixth piece,
and the complete production character/item reload preserves it once, all six
+12 pieces, the exact403,967 remaining CPs and unspent50,000 RVPs. The fixture
advances the existing shop clock between intentional purchases.206 checks pass;
this extends the same route, not206 independent player features.

### Historical blessing-path finding (repaired in this follow-up)

The initial10414/10417 earning review could not rely on the former legacy
`Network/GamePackets/ItemUsage.cs` `Mode.Bless` handler (around line673). It takes
material UIDs from offset91 using the count at26, verifies only inventory presence,
and selects0->1->3->5->7 from counts5/1/3/5. It does not verify Super Tortoise Gem
identity or unique material IDs. It changes the in-memory blessing before separate
item removals and `UpdateBless`, without one owner-locked database transaction.

The [transaction repair](EQUIPMENT_BLESSING.md) replaces that mutation with strict framing,
owned eligible target/material validation, exact distinct gem payment, binding
checks, atomic item/result/receipt persistence, replay protection and safe reload.
Record the protocol evidence and test malformed requests, wrong/duplicate gems,
insufficient payment, concurrency, rollback and ordinary purchased-gem earning.
This finding does not authorize claiming the original blessing packet layout as
native verified; current evidence is the existing server handler.

## Completed server earning for all eight set identities (2026-09-27)

The expanded307-check route purchases second sockets and84 Super Tortoise Gems,
then uses the repaired ordinary blessing requests through all four tiers on each
piece. The final piece earns10414 and10417 atomically with its payment. All eight
identities survive full production reload once, alongside the equipment and exact
remaining CP balance. See [blessing evidence and limits](EQUIPMENT_BLESSING.md).
The earlier185/206 counts above describe narrower historical scopes. Native
presentation, actual action40 client acceptance and deployment remain unchecked.

## Gem-count and alternate-switch implementation (2026-09-27)

10428 counts Super gems3,13,23,33,43,53,63,73 in valid main core/offhand
sockets, including repeated types. Empty holes, lower grades, talisman gems,
alternate equipment, cosmetic slots and malformed socket layouts do not count.
The requirement is12 filled sockets; an extra mandatory quality/plus threshold
is not imposed.

10432 is called after a successful primary-to-alternate transition under the
character item lock. The owner row is locked while validating durable items and
writing the idempotent evidence. Failed switch checks and ownership alone earn
nothing. Database failure fences the session; switching again after reconnect
can retry. No payment or item mutation is introduced by the observer.

The private database predicate fixture passes127 checks, including the new
gem threshold, excluded values, alternate ownership, missing slots, malformed
rows, optional offhand and rollback cases. Native client acceptance and live
deployment remain pending. The legacy gem-embedding payment path is not made
transactional by this achievement change.

The combined ordinary-player fixture passes429 checks. A non-GM character spends
earned CPs on12 Super Tortoise Gems, embeds them through the existing socket
handler, and earns10428 only at the twelfth filled socket. The same purchased
gear moves through ordinary unequip/equip calls into the alternate bank. A
successful actual switch earns10432; owning the bank, an empty-bank switch, a
rejected flying switch, and a cache-only offhand do not. Repeated switches and
full production character/item reload retain exactly one receipt per identity.
The fixture calls server handlers directly; it is not native client acceptance
or crash/ambiguous-commit testing of legacy gem payment.

Subsequent prerequisite repair: [gem embedding](GEM_EMBEDDING.md) now commits
payment/result/evidence together, rejects insertion into occupied or absent
holes, and preserves the other gem during removal. The combined fixture now
passes449 checks, including concurrent insertion, rollback and postcommit
projection recovery. This supersedes the earlier nontransactional embedding
limitation above; native and real transport-loss gates remain open.

The later [successful COMMIT reply-loss fixture](GEM_EMBEDDING.md#successful-commit-response-loss-2026-09-27)
raises the combined count to453 and verifies actual transport-loss recovery of
ordinary paid10428 earning. Other operations retain their own untested transport
boundaries; this result does not close Host/native acceptance or deployment.
