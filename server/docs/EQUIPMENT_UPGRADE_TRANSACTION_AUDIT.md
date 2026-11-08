# Equipment upgrade transaction and earning audit

Reviewed 2026-09-26 against source HEAD 6ac4108 and current working-tree versions
of ItemHandler.cs, ItemUsage.cs and Compose.cs. Those files contain separate owner
work; this audit changes no gameplay or packet behavior.

## Confirmed achievement identities

The installed 6711 Achievement.dat SHA-256 is
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
Decoded identity/meaning, paraphrased:

| ID | Required saved outcome |
| --- | --- |
| 10420 | Meteor level-up attempt succeeds |
| 10421 | Meteor level-up attempt fails |
| 10422 | DragonBall increases item level |
| 10423 | DragonBall quality attempt succeeds |
| 10424 | DragonBall quality attempt fails |
| 10425 | First socket is opened |
| 10426 | Second socket is opened |

At the initial audit all seven producers were missing. The direct adapter below
records six; the subsequent Compose adapter below adds DragonBall level credit
10422. All seven now have source producers, with earning/native limits below. Merely holding an
upgraded item or reading a request/result flag cannot prove the paid operation.

## Original evidence and limits

The [official improving guide](https://co.99.com/guide/guides/improve.shtml)
confirms material-based level upgrading, quantity-dependent success, DragonBall
quality upgrades and the ordered quality tiers. It identifies DragonBall-only
level boundaries for several equipment types; its unspecified weapon subset
is not a complete item-ID policy. It does not publish the exact chance formula.
The [2009 official change list](https://co.99.com/content/2009-05-08/20090508033305310.shtml)
confirms a guaranteed artisan service when the required materials are supplied.
Neither page establishes every rule for this particular 6711 build. Mobile-game
forging guides and player tips were not used to settle PC-client rules.

## Local mutation paths

- ItemUsage.Process actions MeteorUpgrade/DragonBallUpgrade call
  ItemHandler.UpgradeItem (around line 7780). The target/material UIDs select bag
  entries. Material ID chooses the operation: DragonBall quality, Meteor level,
  or MeteorTear reduction. Item update, durability, socket progress and material
  removal are separate persistence operations. Failure halves durability for the
  two upgrade materials. Database outcome and a saved operation receipt are not
  checked together. No new achievement belongs here until mutation is atomic.
- ItemHandler.ComposePlus holds ItemSyncRoot, then delegates to ComposePlusLegacy.
  Modes 6/7 use count fields in two different passes. The mode switch (around
  line 275) is inside `foreach (var item in Minors)` (around line 269), while each
  upgrade branch iterates and consumes the complete selected material array.
  Thus the structure can execute an upgrade branch more than once per request.
  This is a static finding, not a live exploit reproduction.
- The Compose branches check selected UIDs exist and differ from the target,
  but lack a complete distinct-UID, homogeneous-material and saved-preimage
  validation before upgrade. The level branch inspects its first material;
  quality uses array length without proving every entry is a DragonBall.
- Compose accesses Buffer[4], Buffer[5], Buffer[12], indexed words and Count-2
  arrays without local length/count checks. Existing steed validation handles
  its own modes; it is not proof that upgrade packets are validated.
- Direct and Compose formulas differ. Compose supports DragonBall level-up;
  the direct path uses DragonBalls for quality. Do not collapse these operations
  based only on item ID and accidentally award the wrong identity.

## Implementation and validation sequence

1. Build one owned upgrade transaction with explicit operation and selected
   material UIDs. Keep packet parsing separate from mutation policy. Verify the
   relevant client request shape before changing its layout or response.
2. Lock the canonical player and database character, then item rows in a stable
   order. Validate full saved item preimages, exact material types, nonzero
   bounded counts, distinct UIDs, binding, durability and loaded output IDs.
   Cover valid bag and equipped targets supported by each existing entry path;
   do not call a bag-only foundation a completed forging replacement.
3. Compute a bounded server-owned outcome once. Save target ID/durability/socket
   changes, consume selected units, write the operation receipt and corresponding
   achievement(s) in one transaction. A failed roll is a committed paid attempt;
   a database error must restore both target and materials.
4. Project only committed state. Prevent concurrent/repeated charging with a
   documented replay window or verified request identity; do not claim indefinite
   replay protection when the client has no operation nonce.
5. Exercise ordinary handlers with earned shop equipment and legitimately earned
   Meteors/DragonBalls. Cover success, failure, quality versus level identity,
   first/second socket, mixed/duplicate/foreign materials, malformed packets,
   equipped state, bound restrictions, concurrent requests, each suppressed write,
   full reload and post-commit projection failure. Keep native acceptance open.

The initial repair should preserve documented current economy where compatible
with verified inputs and valid catalog outputs. Label retained chance formulas
as Revora behavior until original probabilities are established. Never invent
rates, charges or completed producer checkmarks from this audit.

## Transaction foundation validation

The foundation at commit 858b7be introduced `Game/Features/EquipmentUpgrades.cs`
with explicit Meteor
level, DragonBall level and DragonBall quality operations with separate Direct
and Compose policies. At that commit it was compiled without a startup initializer
or production request caller, and awarded none of 10420-10426. The subsequent
direct adapter below supersedes those integration limits for the direct route.

The service locks the canonical player and saved owner, locks selected items by
UID, checks ownership and target durability/socket preimages, consumes one unit
from each distinct selected material stack, and saves the result and each payment
receipt in one transaction. Selected arrays are copied before validation. A
one-second target cooldown bounds rapid replay; it does not supply indefinite
request deduplication. Failed paid rolls return a committed outcome; persistence
or projection errors fence the character for reload. Current Direct/Compose
chance differences are retained as Revora policies, not verified original odds.

Validation: isolated Debug/x86 build, 661 collection acquisition checks and five
source-audit tests passed. The added ordinary-player transaction cases cover:

- Distinct/homogeneous selections, self-target rejection, trade, locks, binding,
  foreign saved ownership and damaged gear.
- Concurrent direct Meteor requests, selected stack decrement, paid failed rolls
  and their durability penalty, and guaranteed two-Meteor Compose level-up.
- Failed and successful direct DragonBall quality outcomes opening the first and
  second sockets at their respective saved progress thresholds.
- Suppressed target, material, operation-receipt and payment-receipt writes,
  with full character reload and exactly one successful retry in each case.
- A committed guaranteed DragonBall level-up followed by a projection exception;
  full reload restores the upgraded item and spent material without a second
  payment.

These tests supply equipment/material fixtures and call the transaction directly.
They do not prove an earned-material journey, the genuine-client request format,
all equipped/class combinations, all chance boundaries, Compose quality behavior,
or achievement production. The existing test runner also exercises unrelated
collection services; 661 is the entire suite count, not 661 new upgrade cases.
Owner starter-pack work remains outside this commit. Validation uses the current
working-tree assembly; no live server, production database or client was changed.

Remaining work is to validate and connect both ordinary player adapters, cover
all supported equipment contexts and malformed inputs, atomically add the seven
earning outcomes, verify earned materials and reload, and retain the native
acceptance gate. MeteorTear reduction is outside this three-operation foundation
and must retain a separately validated path when adapters are replaced.

## Connected direct adapter and paid achievement credit

ItemUsage actions 19/20 now reach EquipmentUpgrades through the existing
ItemHandler.UpgradeItem entry. The selected material preserves the prior choice:
Meteor raises level, DragonBall raises quality. Target and material lookup run
under the same player lock as the transaction. Startup initializes the two receipt
tables. The old nontransactional Meteor/DragonBall mutation branches were removed.
MeteorTear reduction still uses its separate legacy path; this batch does not
certify that path's persistence or award socket achievements through it.

Successful/failed direct Meteor attempts record 10420/10421; successful/failed
DragonBall quality attempts record 10423/10424. Actual first/second socket
transitions record 10425/10426. Each evidence key identifies the committed upgrade
receipt. Item result, consumed materials, receipts and evidence are atomic. A
suppressed achievement write rolls back the whole operation. Holding or buying an
upgraded item earns none of these transaction identities. Direct eligibility is
ordinary canonical players with safe bag items, no GM/VIP/owner allowlist.

At b7ed685 the service deliberately awarded nothing for its staged Compose
interface; there was no replacement Compose caller or 10422 producer. The
subsequent Compose section below supersedes that limitation.
No new wire fields, actions or result flags were introduced. Source dispatch is
verified; genuine-client upgrade request and presentation acceptance are pending.

Validation: isolated Debug/x86 build; 669 collection-suite checks; five audit tests.
The direct cases now use the actual ItemUsage.Process packet dispatcher, including
concurrent selected-stack consumption, paid success/failure, first/second socket
thresholds, unsafe selections and each suppressed write. Six achievement IDs and
their receipt keys survive production character/item/achievement reload. A newly
suppressed achievement insert rolls back, retries once after reload, and survives
a second login. A direct postcommit projection exception retains the paid result;
reload recovers it and a repeated material UID cannot charge again. Compose
foundation tests still call the service directly and do not establish a player
route. Supplied materials and seeded socket progress are fixtures, not proof of
full acquisition or natural socket accumulation.

The refreshed source inventory is 287/337 with 50 identities missing; at least
33 additional source producers are needed to reach 320. This is a source count,
not roadmap completion or proof that one character can earn 320 achievements.
No live restart, deployment, production migration or native-client test occurred.
Compose integration, earned-material acquisition, complete supported equipment
coverage, natural socket earning and genuine-client validation remain open.

## Ancient Devil material acquisition route

The Ancient boss-world suite now links actual combat rewards to direct upgrade
payments. An ordinary player obtains class amulets through the existing trials,
trade and seal dialogue, defeats the four guards and AncientDevil, and claims the
saved DragonBall reward through the seal NPC. After the existing expiry/recovery
checks, another day's nine earned amulets fund a second completed battle and the
NPC delivers its six-Meteor reward exactly once. Reward draws are deterministic
in this fixture (19 for DragonBall, 20 for Meteors); production odds are unchanged.

The player buys three normal swords (410005) from the loaded silver weapon shop
through ItemUsage.Process, paying 306 silver total. They arrive fully repaired
with zero socket progress. Two exact combat-earned Meteor UIDs and the original
DragonBall UID are consumed through actual upgrade dispatch: Meteor success,
paid Meteor failure and DragonBall quality success. The payment receipts join
back to the claimed Ancient victory item arrays; no material is fixture-inserted.
Full production character/item/achievement reload retains 10420, 10421 and 10423,
changed weapon IDs, the failed weapon's damage, spent materials and exact debit.
It does not grant 10422, 10424, 10425 or 10426.

Validation: 342 Ancient boss-world checks passed using the previously built
b7ed685 working-tree assembly; the fixture was recompiled for this test-only
extension. Its previous entry, combat, rollback, disconnect and expiry checks
remain in the same run. The fixture supplies qualification and initial silver
(7,800,000), scripts lethal damage and controls upgrade outcomes. It proves
combat-material acquisition and spending, not natural combat difficulty, a
zero-funds economy journey, failed DragonBall acquisition or forty-material socket
accumulation. No new reward, price, drop odds, live data or native behavior changed.

## Connected Compose adapter from approved-image evidence

Read-only static inspection of `6711/Env_DX9/Conquer.exe` (the approved
3728bc3f...4fded60 image) resolves the apparent count conflict. Constructor
0x8BFE45 and mode-6/mode-7 callers 0x4AA586/0x5AC206 establish material UIDs from
byte 16, target at 8, material value at 12, and count byte 5 equal to selected
UID count plus two. Declared length is 16 + 4 * selected count. Native helper
0x6A98EE assigns ten points to MeteorScroll 720027 and DBScroll 720028, five to
MeteorBox 723268 and one to ordinary items. The installed catalog confirms those
material identities. This is static evidence, not a captured live exchange;
PROTOCOL_RESEARCH.md records the exact image hash and mount-caller distinction.

The new Compose interceptor validates packet type, exact declared/actual length,
transport seal, 1-40 distinct nonzero material UIDs, target exclusion and the
reported value. Server-owned material types and their matching family determine
value; the reported total must agree. An invalid upgrade request is consumed by
the interceptor and never reaches legacy mutation. A valid request runs once.
Mode 6 chooses Meteor level or DragonBall level from the owned material family;
mode 7 accepts DragonBall quality. Scrolls consume one selected stack unit with
their corresponding point value. Mixed material families are rejected.

The shared transaction now records 10422 for a committed DragonBall level-up and
records appropriate Meteor/quality outcomes for both interfaces. Existing
Compose success/failure ItemUsage result actions remain. Its failure policy
preserves durability, unlike the direct panel's paid failure penalty. Neither
probability formula is claimed to be independently verified original server odds.

Validation: isolated Debug/x86 build, 691 collection-suite checks, 63 mount
breeding regression checks and five audits pass. Tests exercise actual
ComposePlus dispatch with weighted scroll/box selection, exactly one upgrade per
multi-material request, damaged/bound/foreign selection guards shared with Direct,
ten malformed-frame cases, failed quality payment, and equipped targets through
ordinary equip/unequip in main boots slot 8 and alternate slot 28. Suppressing the
new 10422 evidence insert rolls back item, material and receipt; a full reload
permits retry. A subsequent postcommit projection failure reloads the committed
item and achievement without another material charge. No claim is made that
every class, item family or native UI case has been tested.

Source inventory is now 288/337 with 49 missing and at least 32 additional source
identities needed for 320. Complete acquisition/320-per-character feasibility,
natural socket accumulation and native presentation remain open. The earlier
Ancient-material test proves three direct outcomes only. No live process,
production database, client executable or client patch was changed.

## Atomic ordinary repairs for repeated attempts

Direct failed upgrades damage equipment, so repeated natural socket attempts
need reliable repairs. The prior ItemUsage repair action 14 debited silver and
saved durability separately; zero-durability repairs removed five Meteors before
saving the restored item. A failed second write could consume payment without
repairing the item. EquipmentUpgrades.Repair now owns both ordinary bag paths.
The existing VIP equipment repair action 15 and MeteorTear reduction remain
separate legacy paths and are not certified by this repair batch.

The ordinary service locks the player, saved owner and exact item preimages,
then commits durability, payment and a repair receipt together. Partially damaged
items retain the existing integer-proportional silver price, quality multipliers,
ceiling and minimum-one-silver behavior; wider decimal arithmetic avoids the old
uint multiplication overflow. Completely broken items consume exactly five
ordinary Meteors across eligible owned stacks, with no silver debit. Material
selection is deterministic by UID and bound materials cannot repair an unbound
target. Suspicious, locked, rental, warehouse, wardrobe, unsupported or equipped
items are rejected by this bag service. A fully repaired item cannot be charged.

The receipt records target/type, before/after durability and silver, the exact
material UIDs, preimage stacks and consumed quantities. Failed writes roll back
all effects and fence the session for reload. Postcommit projection failure also
fences the session; production login recovers the saved repair. Silver cache
installation advances the ordinary character-save preimage.

Validation: isolated Debug/x86 build, 704 collection-suite checks and five source
audits pass. Added packet-dispatch cases cover concurrent single payment, exact
legacy price, suppressed silver/target/receipt writes, production reload retry,
insufficient broken-item materials, suppressed material removal, consumption
across two stacks with an exact remainder, projection failure, and subsequent
ordinary character save. Damage and repair materials are fixture-supplied here.
This repairs a prerequisite; it does not complete natural socket acquisition or
add an achievement producer. Source count remains 288/337. No deployment or
native-client repair acceptance occurred.

## Combat-earned repeated attempts and socket thresholds

The Snow Banshee encounter suite now gives a separate ordinary player an actual
shop-purchased 410005 sword with zero socket progress. Twenty public encounter
victories supply forty DragonBalls through registered ground loot and the real
pickup handler; every spent UID joins back to a saved victory and collector.
Forty direct quality attempts and forty ordinary repair packets advance progress
without directly setting a socket/progress field. Boundary checks cover attempts
9/10 and 39/40; full character reload at attempts 10 and 29 retains the first
socket and partial second-socket progress. Final reload and ordinary save preserve
both sockets, zero remaining progress, full repaired durability and spent loot.

The sword costs 102 silver and forty repairs cost 1,560 silver at the installed
prices. A twenty-first public victory supplies another collected DragonBall to
the full Compose packet dispatcher, which raises level and records 10422. Final
reload retains that distinct achievement alongside failed-quality 10424, sockets
10425/10426 and monster-pickup 10523. This adds a combat-material route for 10422
and 10424 and a repeated-attempt threshold/persistence test for both sockets.

Validation: 287 Snow Banshee encounter checks pass against the previously built
staged assembly. The test runner recompiled the changed fixture; no new server
build or gameplay change was needed. Starting qualification and 7,800,000 silver
are fixtures. Encounter clocks, lethal damage, loot choices and forty failed
quality rolls are controlled. Unclaimed P7 floor rewards expire between cycles;
no inventory material or socket progress is injected for this route. Production
loot and upgrade probabilities are unchanged.

**Practical-earning limit:** forty forced failed rolls are not representative of
normal quality upgrading. Successful quality upgrades eventually reach Super,
where further quality attempts are rejected. This test must not be presented as
a guaranteed or economically practical forty-DragonBall socket service. The
legacy ItemUsage action 43 findings recorded at this checkpoint were subsequently
resolved by the dedicated EquipmentSockets transaction. Current dispatch, strict
variable-frame parsing, exact selected-material recipes and atomic payment,
socket and achievement writes are documented in
[EQUIPMENT_SOCKET_SERVICE.md](EQUIPMENT_SOCKET_SERVICE.md). That later work also
covers both weapon sockets using six boss-earned DragonBalls. Do not reopen the
old junk-material/framing findings from this historical paragraph. Native socket
UI acceptance remains pending.


## Retained ordinary-character upgrades (2026-10-01)

The retained non-GM character continues from the mined Guard checkpoint. Native
shop 1 supplies a new low-level weapon for its catalog silver price; the CP mall
supplies each DragonBall and Meteor at its loaded server price. Existing combat
equipment is preserved. Forty controlled failed direct quality requests consume
forty paid DragonBalls, earn the first and second sockets through actual progress,
and require forty paid repairs. Further requests cover failed and successful
Meteor upgrades, successful DragonBall quality, and Compose DragonBall level.
All seven identities 10420-10426 coexist with the retained prior career.

The fixture records an exact checkpoint after each attempt and repair. A separate
game/database restart compares the complete career, upgrade, material and repair
ledgers. Replaying an exhausted material UID and repairing the already-repaired
item leave that snapshot unchanged. Same-session failed-attempt replays likewise
make no second payment. The isolated run uses the existing candidate assembly
SHA-256 `07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`;
no production build, deployment or native-client acceptance is implied.

Controls: retained eligibility/funding retain their previously documented mixed
provenance; time, proximity and random outcomes are controlled. This is payment,
earning-transition and persistence evidence, **not** representative socket odds
or a practical forty-DragonBall socket route. The dedicated action-43 service
already provides a separate practical weapon route with exact material payment;
its earlier validation is linked above and was not repeated here. No socket
progress or equipment enhancement is
injected. The first invocation rejected the new phase at the fixture allowlist
before loading the player; adding the explicit phase resolved that harness issue.
