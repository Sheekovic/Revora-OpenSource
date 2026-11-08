# Ninja Epic Quest restoration

Research baseline: `15aa95e`, runtime batch follows `538d6dd`, 2026-09-16.
M6.55b3b implements the ordinary NPC trials, scroll exchanges and owned katana
conversion. M6.55b3c adds hunted flowers and the material-funded journey.
Inheritance is now implemented; genuine-client acceptance remains open. No production database, live map, client or server process was changed.

## Reviewed route

The [official quest guide](https://co.99.com/guide/quest/2014/ninja_epic_quest.shtml)
admits second-reborn Ninjas at level 70. Four Red and four White Flowers provide
the material route; CP payment is the alternative. Each of the three trials has
a 10% success chance: YinYang Seal, Elemental Jade, then Life's Eye. Failed
later trials return the player to the first area with Stardust. VIP 3 enables
the 270-CP ten-attempt option. Stardust buys skill scrolls, and Life's Eye
transforms a katana into an epic weapon.

Local source preserves additional Revora policy: a single paid attempt costs
27 CPs, Elemental failure pays one Stardust, Wheel failure pays five, and each
of the three scrolls costs ten Stardust. These counts/prices are not all stated
in the official page. The flower payment implementation spends four of each
per first-trial attempt, a Revora interpretation of the material alternative;
do not label that cadence independently verified retail behavior.

| Material | Installed item ID |
|---|---:|
| Red Flower | 722732 |
| White Flower | 722736 |
| YinYang Seal | 3004462 |
| Elemental Jade | 3004463 |
| Life's Eye | 3004464 |
| Stardust | 3004465 |
| Shadow / Twilight / Fatal Scroll | 3004459 / 3004460 / 3004461 |

Item identities use the unchanged hashes in [the instructor report](NINJA_SKILL_TRAINING.md).
Scroll learning is implemented. The combined runtime fixture now earns each
scroll through paid trials and Stardust exchanges before learning it. The later
flower fixture earns materials through actual death and pickup dispatch, then
funds a learned Shadow Clone and equipped epic without quest CP deductions.

## Legacy defects replaced in this batch

- `Random.Next(1,10)` cannot return 10. The single first trial is therefore
  1/9 rather than 10%; the Elemental trial succeeds on three of nine outcomes.
  The 270-CP option makes one draw with four successful outcomes, instead of ten
  independent attempts. One Elemental failure branch also omits its Stardust.
- Payment, material removal, reward insertion and transport are separate writes.
  Full bags, repeated requests and interrupted saves can lose or duplicate value.
- Wind Sage's entry checks rebirth/level without a Ninja class check. The flower
  alternative is absent from the existing first-trial dialogue.
- Correction from the flower audit: legacy flower rolls include both
  quest-gated and unconditional branches. Name/casing defects also skip some
  installed families. The new owned family-ID route replaces flower outcomes
  only for eligible Ninja hunters, suppresses both old flower branches for them,
  and preserves other materials and other players' existing quest rules. Empty
  legacy rolls no longer create item-ID-zero floor entries.
- Life's Eye item use grants a fresh level-130 epic weapon. The NPC conversion
  accepts many non-katana one-handed weapons and replaces their ID with 616439.
  The restoration must validate an owned katana and preserve supported catalog
  level/quality plus earned item metadata and binding.
- The first landing (28,28) is blocked. It now uses (29,28). The other two
  original landings and all nine original actor positions are valid.

## Installed world and corrected terrain evidence

NPC records were queried inside an explicit PostgreSQL read-only transaction.
Wind Sage 4720 is in Job Center 1004 at (36,45), mesh 50026. The installed
coordinates take precedence over differing coordinates in web editions.

| Map | Actor | NPC | Installed position | Runtime position |
|---:|---|---:|---|---|
| 3849 | Divine Shadow | 199445 | 28,33 | unchanged |
| 3849 | YinYang Stele | 199464 | 39,37 | unchanged |
| 3849 | Swapper | 199546 | 28,40 | unchanged |
| 3850 | Divine Shadow | 199444 | 22,34 | unchanged |
| 3850 | Elemental Guard | 199465 | 33,36 | unchanged |
| 3850 | Swapper | 199544 | 24,41 | unchanged |
| 3851 | Divine Shadow | 199446 | 26,29 | unchanged |
| 3851 | Wheel of Life | 199466 | 35,30 | unchanged |
| 3851 | Swapper | 199545 | 27,34 | unchanged |

Correction to the earlier `538d6dd` report: its raw terrain-bit interpretation
was reversed. In the actual map loader raw byte 1 is blocked; setting the
`InvalidCast` indexer to true clears that flag. `Map.AddNpc` also blocks the
actor's 3x3 footprint. The earlier claimed unreachable actors and proposed
moves are withdrawn; none were applied to a live world.

Runtime landings are 3849 (29,28), 3850 (34,39), 3851 (33,35), and return to
Job Center (38,42). The corrected fixture loads the real `Floor`, adds the
installed NPCs with their actual types/collision, then walks its cardinally
connected component and uses the server's six-step interaction rule. All nine
original actors are reachable. Initialization checks the exact terrain hashes,
actor preimages and legal landings; it does not relocate any actor.

Reviewed server terrain SHA-256:

- `3849.map`: `28717b072ef67478060ddc4f3a5acb1547fa9db67ddacb09c1173f985deb7cd6`
- `3850.map`: `88ae0bb59a5f64d1594366c7268dea566edf28b48e05eadc0d66147e9f7c3d19`
- `3851.map`: `dbbc55c34419080392174e2601585f42040442d828e0fbec94690a94c757c636`

Client Questinfo.ini retains SHA-256
`f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`.
The scoped scan found no records referencing these map IDs or trial NPC IDs;
this is not proof that every possible native journal representation is absent.
Do not assign an unrelated mission or achievement to this quest.

## Implemented runtime and validation

`NinjaEpicRules` supplies admission, 0..99 outcomes, bulk resolution and 343
known 601-to-616 katana mappings preserving level and quality. `NinjaEpicStore`
owns the exact payments, item debits, rewards, katana conversion and receipts.
`NinjaEpicQuest` owns brief NPC dialogue, expiring confirmations, ordinary
travel, direct Life's Eye use and durable failed-trial return recovery.

Every eligible second-reborn Ninja at level 70 can use the service. It requires
a canonical loaded player, valid location/NPC, owned materials and enough bag
space for the maximum outcome. CPs, exact material preimages, rewards and the
operation receipt commit together under the player and database owner locks.
Bulk payment makes ten independent unbiased cryptographic draws. Repeated NPC
confirmations consume only one offer; direct Eye use also has a durable
one-second duplicate guard for the same material UID. Binding carries through
materials to rewards and weapons. A katana conversion updates only its ID and
binding in storage, retaining identity, enhancements, sockets and attribution;
it refreshes item/equipment statistics after commit. Saved metadata conflicts
fail closed. Interrupted failure returns resume on login without another draw.

The isolated build and **568 `-NinjaEpicQuestOnly` checks** pass, including the
instructor/promotion prerequisites and corrected rule/catalog/terrain checks.
Validation includes actual NPC request dispatch and legal movement; exact CP
costs and ten-attempt bulk rewards; mixed-bound flower payment; both later-trial
failures; earned Stardust to all three learned skills; earned Eye to an equipped
epic katana; complete reload with enhancements and attribution; either hand;
concurrent confirmation replay and reconnect Eye replay; full bag, invalid
class/level/rebirth/VIP, expiry/cancel, stale objects and saved state; write
suppression across balances, materials, rewards, receipts and the equipped
weapon; late eligibility loss; invalid RNG output; and interrupted-return
recovery. The standalone corrected rule fixture has 68 checks. Three producer
audits pass. No unrelated achievement or mission ID was added.

Startup/NPC/item-use/login integrations are compiled in source. This batch was
not deployed. These are server fixtures, not evidence of rendered 6711
movement, dialogue, inventory, skills or weapon combat acceptance.

## Flower hunting and material-funded journey (M6.55b3c)

Wind Sage offers a free passage to Evil Abyss 1700 (603,641) for eligible
second-reborn Ninjas at level 70. Bruce 3604 at (601,641) explains the flowers
and returns hunters to Job Center (38,42). No Slayer Cup or Abyss enrollment,
material fee or quest-progress mutation is required. Existing first-rebirth
keeper handling stays available to its own eligible players.

A read-only query verified installed spawns for all 32 families 3600..3631.
The fixture loads actual terrain plus NPC collision and proves every configured
spawn rectangle has floor connected to the camp. The map hash remains
`8026193ec8c8cb7f1934ccf7c2e713931a0c65269c8d7a0b67209019bed17d1b`.
No spawn, population, terrain or monster balance edits are needed.

Revora policy gives each canonical player-finished monster life one 24-way
roll: one Red Flower outcome, one White Flower outcome and 22 empty outcomes.
This extends the common legacy unconditional 1/24-per-color roll consistently
across the reviewed families; it is not a verified retail drop rate. Eligible
hunter identity, monster family/owner/map, registered instance, death and
proximity are checked. Replayed failed draws cannot be retried. Companion,
guard, foreign-instance, distant and ineligible kills cannot mint flowers.

Flowers are ordinary tradable ground items with killer protection, existing
team/release rules and expiry. Manual and auto-hunt pickup validate the recorded
origin and atomically save the item plus a unique receipt. Full bags leave the
item on the ground. Failed writes roll back and permit reconnect/retry. The
route preserves separate Slayer Cup progress; the new service does not enroll,
advance or consume the restored Abyss quest.

The isolated build and **800 `-NinjaFlowersOnly` checks** pass, including the
568 trial/trainer prerequisites. Coverage includes all 32 real family death
paths, reachable configured spawns, exact drop distribution, native and auto-hunt
pickup, full bags, ownership/release races, replay, expiry, malformed frames,
mutated origins, write faults, reconnect and retained Cup progress. A complete
ordinary hunt earns 24 flowers, funds three first trials, uses failed later
trials to learn Shadow Clone, and awakens/reloads an equipped katana. Unrelated
monster CP/gold rewards are disabled only inside this fixture so the quest's
zero CP debit is measured independently. The existing **1,784 Abyss quest
checks** and three producer audits also pass. The map expiry/removal path also locks
managed flowers and compares the exact registered object, so an old drop cannot
remove a replacement that reused its UID. This is not a live deployment or a
native rendered-client playthrough.

## Inheritance and remaining acceptance

[Ninja inheritance and summon cleanup](REBIRTH_SKILL_AUDIT.md) now have 2,181
combined rite checks. The three scroll arts are removed by all rites; learned
Super Twofold Blade is preserved. Returning Ninjas can repay the corresponding
scrolls to learn the removed arts again. Active clones and combat modes clear
only after commit; failed rites preserve them.

Validate the full paid and hunted-material routes in genuine 6711, including
native dialogue, inventory, skill/clone display, equipment/stat refresh and
weapon use before deployment. This acceptance gate remains open.

## Guaranteed P7 awakening reward (2026-09-21)

Every newly confirmed final weapon awakening now includes one bound catalog
P7 soul. The existing conversion preserves the owned weapon's enhancements,
attribution and binding rules; this change does not replace it with maximum gear.
Intermediate trials, failed draws and scroll exchanges do not award this soul.

The soul item and its existing epic-reward receipt commit in the same transaction
as material consumption, the weapon conversion and operation receipt. Capacity
reserves one reward slot after actually exhausted material stacks. A stacked
Heart/Eye that remains in a full bag cannot finance an awakening until room is
available. Failed soul insertion rolls back the material and weapon changes.
Existing item-use replay guards and duplicate operation protection remain active.

This service uses short-lived in-memory confirmation offers, not a saved future
quest payout contract. New confirmations use these terms; historical operation
receipts are never reopened or retroactively paid. NPC confirmation and success
text explain the additional bound soul. Existing bound offerings still bind
the converted weapon, while the new soul is always bound.

Validation covers ordinary earned trial routes through final conversion, soul
catalog identity and receipt ownership after production-loader reconnect,
full-bag rejection, immediate item-use replay and injected soul-insert failure.
Native journal/reward display and live deployment remain open.

Build and 575 Ninja checks pass. Log: `obj/chi-refill-validation/ninja-p7-awakening.log` (ignored).


## Paid route across actual game processes (2026-09-27)

`tools/Verify-ArenaProcess.ps1 -NinjaOnly` exercises three private game processes
and an actual PostgreSQL stop/start after the first process. The ordinary non-GM
profile starts without items or CPs; class 55, second rebirth and level 100 are
supplied qualifications. Controlled boss combat earns 1,000,000 CPs and silver.
The normal 1009 purchase dispatcher buys Metal Katana 601003 from loaded silver
shop 5 at its catalog price, then the normal equip handler equips it.

Wind Sage admits the qualified character. A controlled failed draw costs 27 CPs;
a repeated confirmation costs nothing. A successful paid draw costs another 27,
and the two following successful material trials consume the earned Seal and
Jade to produce Life's Eye. The process exits immediately after preparing, but
before confirming, awakening. After database/game restart the original katana
and Eye survive, while the interrupted process's unsaved offer cannot grant anything.
A fresh offer converts the same weapon UID to 616003, consumes the Eye and records
one awakening and one bound soul. Repeated confirmation does not pay twice.
The third process checks exact item UID/type fingerprints, the same equipped
weapon, consumed Eye, five operation receipts, 999,946 CPs and unchanged silver.

This is an offline ordinary-player integration probe. Positions, qualification,
boss damage and random draws are controlled; production trial odds and prices
are unchanged. It does not establish authenticated login, native 6711 rendering,
or a completed flower-hunting route across processes. Native and live deployment
gates remain open. The earlier P7 paragraph has been corrected to the current
bound policy; no historical rewards are retroactively changed.

Validation: all three phases passed against candidate SHA-256
`440D5CFD930640C83A457AE69D51BE17E2C2C15DB537A95EFCA080485C89F65E`.
The final phase also checks the saved soul against the loaded P7 catalog;
the purchase phase requires a matching silver-shop payment receipt. The existing
production build was reused. Redacted local log: `obj/ninja-process.log` (ignored).
