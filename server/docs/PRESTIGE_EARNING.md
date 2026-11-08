# Prestige wardrobe earning and persistence

## Earned equipment baseline (2026-09-26)

The complete [Perfection earning route](PERFECTION_TEMPERING.md#earned-648-wardrobe-threshold-2026-09-26)
now also compares the ordinary character's online Prestige with the database-only
startup rebuild. The character is removed from the live game pool during that
rebuild so a session refresh cannot hide a saved-profile mismatch.

This exposed a production bug: login gives non-donors Duke rank, while the offline
Prestige loader left them at rank0 when they had no Nobility.Board entry. Online
Prestige was299,668 but offline Prestige was292,668. The loader now uses the same
Duke fallback as EntityTable.Load and NobilityInformation. Existing donor-board
ranks still take precedence. No nobility rules or scoring weights were changed.

Build succeeds with existing legacy warnings. **1,062 combined checks pass**
(`obj/earned-prestige-build.log`, `obj/earned-prestige-baseline-tests.log`). All23
score categories and the299,668 total match after the normal character save and
offline database rebuild. The fixture supplies an empty production-shaped
subclasses table required by the offline loader; it does not grant subclasses.
The suite includes earlier earning and transaction checks, not1,062 new tests.

| Earned character category | Points |
| --- | ---: |
| Perfection | 243,000 |
| Plus | 30,000 |
| Nobility (ordinary Duke) | 7,000 |
| Quality | 4,400 |
| Level | 3,500 |
| Sockets | 3,500 |
| Item level | 2,568 |
| Protection | 2,100 |
| Gem quality | 1,600 |
| Attributes | 1,000 |
| Rebirth | 1,000 |
| Other twelve categories | 0 |
| Total | 299,668 |

Scholar/World-renowned20272027 requires400,000, leaving100,332 points; Superman/
World Dominator20262026 requires600,000, leaving300,332. Neither reward is falsely
granted at this baseline. Next work must earn compatible cultivation, equipment,
rune or other progression on this same character; supplied score overrides do
not prove either title. Both matrix rows remain predicate-only.

The Perfection route's controlled eligibility, time, RNG and combat limitations
still apply. This is not actual Host restart/native acceptance. The build included
unrelated owner work already present in the workspace; the commit contains only
the one-line offline-rank fix, its integrated regression and documentation.
No live deployment, schema mutation or client patch installation.


## Earned 400,000 title (2026-09-26)

The same first-reborn character now spends its earned boss CPs on a4,000-point
Chi refill, unlocks/pursues Dragon and Phoenix to400 each, and is refused the
second-rebirth Tiger unlock without a charge. It creates JiangHu and completes81
paid rolls to81,000 inner strength. Legal maximum RNG outcomes are controlled;
no Chi/Jiang score or rebirth override is supplied. Total Prestige reaches396,268,
and the400,000 title remains absent at that point.

The ordinary donor NPC then spends100,000 earned CPs in one confirmed donation,
with cancellation, range, replay and concurrent-confirmation checks. In this
isolated board it earns King rank, taking the score to401,268. Scholar/
World-renowned20272027 becomes eligible and equips through the wardrobe handler.
The route spends129,250 CPs total:1,000 Chi refill,28,250 Jiang training and100,000
nobility donation. King rank depends on the actual donation competition; this
fixture is not a guarantee that the same payment always earns King in production.

Full character, Chi and Jiang database reload retain the title, selection, exact
payment, first rebirth, powers and score. A database-only offline Prestige rebuild
matches all23 categories. **1,079 combined checks pass**
(`obj/earned-prestige-cultivation-tests.log`). This includes the reused equipment,
quest, transaction and donor suites, not1,079 new tests. The donor helper preserves
its standalone one-bounty expectation and accepts the explicit100-bounty expectation
for this linked route. Wardrobe login is performed after reconnect before equip.

The600,000 reward is still absent, leaving198,732 additional points to earn.
The400,000 route is proven under current Revora rules; inherited Perfection
admission/phase decisions and actual Host/native acceptance remain pending, so
matrix classification remains conservative. Tests/docs only; existing production
build reused. No live deployment, balance/rule changes or schema mutation.

## Earned second rite on the continuing character (2026-09-26)

The same character buys an Exemption Token from the loaded CP mall for 888 of
its earned boss CPs. Normal Sereth dialogue and concurrent confirmations consume
that token once and commit second rebirth. The one-time reward includes a bound
catalog P7 soul linked to the rite's receipt; earlier owned quest souls are
preserved and are not mistaken for this new reward.

The real reset is retained: level 15, class 11, with the old class 15/level 140
recorded for later recovery. All twelve earned item identities retain level-54
Perfection and its ownership, including equipment returned to inventory. Chi,
Jiang, payment and rebirth history survive normal reload. All 23 online/saved
Prestige categories agree at **376,534**, leaving **223,466** to reach 600,000.
The previous 401,268/400,000-title checkpoint remains valid before the rite; it
is not the character's current score after the reset.

The starting first-reborn fixture now explicitly supplies valid first-life
history (class 15, level 130) before Family Feud begins. That first life and the
initial level 140 remain supplied eligibility, not newly claimed earning. No
second-rebirth history, token, payment or completion receipt is fabricated.
The token-funded rite earns the rebirth achievement without claiming the separate
Abyss quest achievement.

**1,089 combined checks pass** (`obj/earned-prestige-second-tests.log`), including
the 1,079 reused journey checks and ten new second-rite checks. Production build
from `dcc31622` reused; tests and documentation only. The first probe exposed a
fixture assumption that the bag contained just one P7 soul; the final check
identifies the newly granted soul and verifies its exact receipt instead.

Next: earn third-life levels and compatible cultivation/equipment progression.
The second-rebirth gate is now satisfied for the installed level-15 Inner Power
and rune routes. The 600,000 title, original Perfection-rule decisions and actual
Host/native acceptance remain unfinished. No live changes were performed.

## Earned full Inner Power (2026-09-26)

The same second-reborn character uses PotencyGuide to enter map 1217, kills 175
managed PotencySerpents, receives their floor spawns, picks up and opens their
Supreme Exploit Packs, then returns through the field guide. The legal Supreme
roll and lethal damage are controlled test inputs. Production 10%/5%/1% odds are
unchanged; this is not a claim about practical grind time or level-15 combat
balance. Normal kill experience advances this run's character to level 70.

At the installed InnerPowerShop, normal item requests buy all 17 books for
3,600 of the character's earned boss CPs. Their unlock requests consume each book
once; 111 training requests spend exactly 174,200 of the 175,000 hunted potency.
All stages complete at 1,700 Inner Power score, with 800 potency remaining and
no RVP spending. Entity/item reload and cold award/spend recovery preserve the
state, purse and all 23 online/offline Prestige categories. Current Prestige is
**380,484**, leaving **219,516** to reach 600,000.

**1,116 combined checks pass** (`obj/earned-inner-power-tests.log`), including
1,089 earlier journey checks and 27 new earning checks. The fixture completes
normal login's Inner Power identity binding and HP setup after entity-only reload,
and delivers normal floor spawns before the screen-owned pickup path.

The proposed RVP supplier was audited first: NPC 900019 is installed on map 5001
at 88,45. Ordinary access to that legacy location was not established, so this
route does not use or certify it. The verified public hunt supplies the potency.

Validation used committed C# sources plus the equipment socket handler namespace fix:
18 locally modified production inputs were replaced with temporary HEAD copies
for compilation and four uncommitted compile entries were excluded. The owner's
working files were preserved. The committed `ItemUsage` socket dispatch lacked
its namespace qualifier; this batch includes only that existing local correction
from the shared file. Build passed with legacy warnings
(`obj/committed-inner-earning-build.log`). No live deployment or client changes.

## Earned runes with purchased stones (2026-09-26)

The same level-70 second-reborn character buys five class-compatible runes from
Vesper for 3,250 of its remaining 5,600 earned RVPs. It purchases 24 +8 stones
from the installed mall at 26,244 CPs each, spending 629,856 earned boss CPs.
Twelve stones max the blue rune and four max each yellow rune; the red rune is
already at its cap. Rune-bag transfer, material composition and equipment use
ordinary handlers. Immediate purchase and consumed-material replays are inert.

The equipped set adds exactly 67,000 Prestige and earns Chosen One. Full item,
character and wardrobe reload preserves it, exact balances and all 23 online/
offline score categories. Current total is **447,484**, leaving **152,516** for
600,000. This proves one complete set, not the alternate combination or the
600,000 title. Starting eligibility and earlier controlled combat/RNG inputs keep
the limitations documented above; native-client acceptance remains pending.

**1,130 combined career checks pass** (`obj/earned-rune-stones-tests.log`):
1,116 earlier checks plus 14 new rune checks. The independent shop suite passes
585 combined checks, including 13 new stone cases. Do not sum overlapping runs.
[Atomic purchase change](RUNE_STONE_PURCHASES.md) preserves existing stock and
prices, adds no schema, and passes the committed-source build with legacy
warnings (`obj/rune-stone-purchase-build.log`). No live deployment or restart.

## Earned alternate rune combination (2026-09-26)

The continuing character earns one more scheduled boss bounty through the normal
combat/death handler: 1,000,000 CPs, 50,000 RVPs and 10,000,000 silver, backed by
one additional paid receipt. Event time and lethal damage are controlled inputs;
this does not certify combat balance. The character then buys, upgrades and equips
an independent alternate rune combination in slots 121-125. Its existing primary
combination remains equipped. Each set costs 3,250 RVPs and 629,856 CPs.

All ten earned runes survive character/item/wardrobe reload and contribute
**134,000 rune Prestige**. Eight purchase receipts back the 48 consumed +8 stones;
replay checks and exact currency assertions cover both sets. All 23 online/offline
categories match. The observed total, including progression from the additional
boss death, is **514,814**, leaving **85,186** for the 600,000 milestone.

**1,145 combined career checks pass** (`obj/earned-alternate-runes-tests.log`),
including the previous 1,130 checks and 15 added alternate-route checks. This is a
fixture/docs batch using the previously built committed production code; no new
build or deployment was needed. Native acceptance remains pending.

Next: earn the level-110 gate before unlocking Tiger and Turtle Chi. The ordinary
`GeneralData.UnlockChi` handler requires level 110, successive 300-point powers
and the appropriate rebirth count. Do not assign levels or bypass those gates to
claim a completed earning route. Other compatible progression is still needed;
600,000 Prestige is not complete.

## Earned level gate and four Chi powers (2026-09-26)

The same character was level 103 after its alternate-rune funding boss. One
additional scheduled boss death earns level 110 through ordinary combat EXP.
The fixture supplies no level or EXP assignment for this gate. Its exact
1,000,000-CP, 50,000-RVP and 10,000,000-silver bounty adds one durable paid receipt.
Event time and lethal damage remain controlled inputs, not a combat-balance claim.

After character reload, ordinary Chi unlock/study packets acquire Tiger and
Turtle using 700 of the existing paid points. Legal maximum rolls are controlled;
all four powers reach 400 points, 2,600 Chi points remain, and CPs are unchanged
by these requests. Full reload and all 23 offline/online score categories agree.
Current Prestige is **562,884**, leaving **37,116** to reach 600,000.

**1,151 combined checks pass** (`obj/earned-four-chi-tests.log`): the previous
1,145 plus six new checks. The shared scheduled-bounty helper preserves the
alternate-rune funding assertions. This fixture/docs batch uses the already
built production code; no live deployment or restart. Native acceptance remains
pending. Next: compatible class/equipment and remaining earning progression;
the 600,000 reward is still unproven.

## Earned level recovery and original offhand (2026-09-26)

The second rite had returned the original perfected offhand to inventory.
The continuing class-11 character equips that same UID in slot 5 through the
ordinary item handler, without another purchase or class assignment. Ten more
scheduled boss deaths earn the second-life recovery threshold and restore its
recorded level 140. Every bounty retains the exact currency/receipt checks;
combat damage and event timing remain controlled fixture inputs.

Full reload preserves the offhand's Perfection 54, level 140 and every online/
offline category. Current Prestige is **585,587**, leaving **14,413** for 600,000.
**1,165 combined checks pass** (`obj/earned-restored-equipment-tests.log`), including
1,151 prior checks plus 14 new recovery checks. No production change, deployment
or native-client acceptance is claimed by this fixture/docs batch.

The remaining category audit identifies equipment enhancement as a candidate:
Plus currently contributes 30,000, while the earned quest pieces remain available
for upgrades. Before joining that route, fix its committed persistence boundary.
`ItemHandler.ComposePlusLegacy`, `Compose.Plus` branch, separately writes Plus,
PlusProgress and removes each material after changing the in-memory target.
A partial database failure can leave progress and consumption inconsistent.
The outer item lock is not a database transaction. It is not acceptable to call
this a persistence-verified earning route just because successful upgrades work.
Next batch: owned atomic +composition with ordinary material eligibility, exact
preimages, binding, duplicate-material rejection and rollback/reload tests, then
join purchased stones to the existing earned gear. Do not change reward amounts
or grant a fabricated 600,000 score to bypass this prerequisite.

## Earned 600000 title under current rules (2026-09-26)

After atomic ordinary +composition replaced its split-write path, the same
character buys twelve +8 stones at the installed price: 314,928 earned CPs.
Four stones raise each original quest headgear, necklace and armor from +0 to
+12, keeping the item UIDs, binding and Perfection 54. Their combined enhancement
adds 30,000 Prestige, taking the character from 585,587 to **615,587**.

The actual threshold reconciles Superman / World Dominator (20262026), and an
ordinary wardrobe request equips it. Full character/item/cultivation/wardrobe
reload preserves the equipped title, exact currency payment and enhancements.
All 23 offline/online score categories agree. **1,193 combined checks pass** in
`obj/earned-superman-tests.log`: 1,165 prior checks plus 20 transaction checks and
eight new earning/title checks. [Implementation and recovery](PLUS_COMPOSITION.md).

This completes the 400,000 and 600,000 title earning/equip/persistence checkpoints
under current server rules. It does not resolve the inherited original Perfection
rule decision, replace controlled initial eligibility/combat/RNG with real grind
or PvP evidence, or constitute Host/native acceptance. Those remain explicit gates;
the conservative wardrobe-matrix classification is retained. No live deployment.
Next roadmap work should target other unfinished earning paths, rather than add
more Prestige points beyond this now-verified threshold.
