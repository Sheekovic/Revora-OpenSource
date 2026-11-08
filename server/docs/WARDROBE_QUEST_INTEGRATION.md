# Wardrobe storage and quest bag accounting

Wardrobe items use `position=0`, `warehouse=0`, `inwardrobe=1` when stored rather
than equipped. A saved bag count must exclude these rows before comparing it with
the ordinary inventory or checking free slots. Counting all position-zero rows
can fence a legitimate character or reject a reward even when the bag has room.
This became visible when the existing earned collection character continued into
Pickaxe purchasing and mining; those two services were fixed in 3d3def8.

## Dragon Slayer route

The following saved bag counts now exclude wardrobe storage: GrottoKeyDrops,
DragonSlayerHunt, DragonPillCrafting, TeratoEncounter and TeratoRewards. Ownership,
bag capacity, offering payments, reward transactions and replay checks remain.

The Grotto suite now supplies a cosmetic fixture and deposits/takes it off through
ordinary wardrobe handlers, verifying the saved stored location. Key hunters on
both relevant floors, the character completing all seven ghoul hunts and Dragon
Pill crafting, the Terato killer and the public accessory-loot recipient own stored
wardrobe items during their existing routes. The final assertion preserves the
original stored design through the complete hunt/craft/summon/victory sequence.
The supplied cosmetic is not described as an earned garment. Combat, loot rolls
and qualified starting characters remain controlled fixture inputs.

The first integrated run exposed Terato's separate offering-count query, which
was corrected before final validation. No live database, client or server changed.
The isolated build and final 299 Grotto/Dragon Slayer/Terato checks pass. Existing
owner WIP was present in the build; native-client and live deployment gates remain.

## House bosses and Snow Banshee

SwordMasterAdmission, SwordMasterRewards, PurpleBansheeAdmission,
PurpleBansheeRewards and SnowBansheeRewards now exclude wardrobe storage from
their saved bag counts. Their existing primary combat fixtures deposit and take
off a supplied garment before the paid summon (house bosses) or public combat
(Snow Banshee). The routes retain the same admission payments, victory rewards,
pickup replay checks and production reward reload. An explicit saved-row check
preserves the cosmetic after loot collection. This does not establish the earning
source of the supplied garment, house/honor prerequisites or native presentation.
The isolated build and 97 Sword Master, 78 Snow Banshee and 89 Purple Banshee
checks pass. Live deployment and native acceptance remain pending.

## Clean Water and Heavenly Stone

CleanWaterDrops and HeavenlyStoneCrafting now exclude wardrobe storage from
their saved bag counts. The ordinary adventure fixture deposits and takes off a
supplied garment before purchasing the Pickaxe, mining the seven recipe gems,
collecting Clean Water and crafting the Heavenly Stone. A saved-row assertion
preserves the stored garment through crafting and the production reload. The
existing journey also completes the paid island crossing and first-rebirth rite.
The starting purse, garment, eligible character, time and loot draws are fixture
inputs; this does not prove an earned cosmetic source or native presentation.

The isolated build and all 248 adventure checks pass, including the existing
payment, failure, replay and reconnect checks. Owner WIP was present in the build.
No live deployment or native acceptance was performed.

## Honor and house purchases

HonorPurchases and Housing certificate purchases now exclude wardrobe storage
from their saved bag counts. The Sword Master fixture now deposits its supplied
garment before buying the challenge item for 8,000 Honor and buying/building the
house, extending the earlier admission/loot coverage to both prerequisites. The
five-level housing fixture also owns a stored garment throughout all certificate
purchases, upgrades and production reloads, and verifies that its stored row is
preserved. Starting Honor, CPs, silver and the garment remain supplied fixtures;
these checks do not establish their earning sources.

The isolated build, 97 Sword Master checks and 559 housing checks pass. Existing
transaction rollback, replay, house ownership and reconnect checks remain in the
suites. Owner WIP was present in the build; live deployment and native acceptance
remain pending.

## Class promotions

ClassPromotion now excludes wardrobe storage from its saved bag count. Each of
the ten school routes (including both Taoist branches) deposits and takes off a
supplied garment before its five rank advances. All fifty promotion identities
retain their level requirements, exact material debits, existing rewards and
duplicate-confirmation checks. Each route preserves the stored garment; the
final Windwalker character also preserves it through production reload.

The two existing Moon Box quest journeys now carry a stored garment while earning
and spending their actual Moon Box or Meteor at the trainer. These provide earned
offering coverage; the cosmetics, starting eligibility and other matrix materials
remain supplied fixtures. The isolated build and 291 promotion checks pass,
including existing refusal and transaction-failure cases. Owner WIP was present
in the build; native acceptance and live deployment remain pending.

## Reward packs and arena scrap conversion

P7SoulPacks and DisCityPacks now exclude wardrobe storage from their saved bag
counts. The first integrated scrap-to-P7 run also exposed a separate identity-list
check in ArenaPrizeConversions: it compared wardrobe rows with ordinary bag UIDs.
That query now excludes wardrobe storage while retaining exact sorted UID equality.

The P7 fixture owns a stored garment for every listed weapon/equipment soul outcome
with both source binding states, and checks the garment after production reload.
The existing scrap conversion-to-pack-to-bound-P7 journey also carries a stored
garment across both reloads. Packs and scraps are supplied fixtures, not claimed
as event-earned inputs. P7 output binding and source eligibility are unchanged.

The refinery fixture carries a stored garment across all listed level-five/six
Dis City material outcomes, stack use and failed-receipt recovery. Superior-pack
quality-boundary cases also preserve wardrobe storage; their existing result
persistence check uses production item loading, not a full character reconnect.

The isolated build, 311 P7/conversion checks and 184 refinery checks pass. Existing
capacity, replay, binding and failed-write checks remain. Owner WIP was present
in the build; live deployment and native acceptance remain pending.

## Monster equipment and mentor stones

MonsterEquipmentDrops and MentorStoneClaims now exclude wardrobe storage from
their saved bag counts. The ordinary pickup fixture owns a stored garment while
receiving generated +1 equipment and contributing a mentor composing point. The
existing hundred-pickup journey now gives both apprentice and mentor a stored
garment before earning and claiming one +1 Stone. The claimed stone is checked
through production item loading; both stored garment rows remain unchanged.

Monster/drop selection and eligible relationships are controlled fixtures, and
picked equipment is moved to warehouse storage by fixture SQL between pickups.
This proves pickup-to-contribution-to-claim behavior, not a natural drop-rate,
warehouse UI, full character reconnect or native-client playthrough.

The isolated build, 52 monster-equipment checks and 29 mentor-stone checks pass,
including existing full-bag, partial delivery, concurrent claim and rollback
cases. Owner WIP was present in the build; no live deployment was performed.

## Battle Power tournament purses

BattlePowerTournament now excludes wardrobe storage from its saved bag count
when delivering champion items. Each of the three existing ordinary champion
journeys deposits and takes off a supplied garment after the controlled match
and before claiming. Exact CP/Chi rewards, bound P7 soul and season-token UIDs
survive replay and production character reload; the stored garment is preserved.
The cosmetic is not an event reward, and this extension covers claim/reload,
not wardrobe-dependent admission or combat balance.

The first run exposed fixture setup ordering: the existing profile-save helper
sets Vitality before equipment stat recalculation. It now runs before wardrobe
use, as required for the fixture's ordinary character profile. No production
wardrobe or health checks were relaxed. The isolated build and 110 BP tournament
checks pass, including capacity, overflow, rollback, season and token-exchange
regressions. Owner WIP was present in the build; live/native acceptance is pending.

## Arena daily and custom rewards

ArenaDailyRewards and ArenaCustomRewards now exclude wardrobe storage from
their saved bag counts. Existing ordinary match-result fixtures deposit and take
off a supplied garment before the winner's daily and hundred-win prize delivery.
The daily route retains pending full-bag rewards and pays exact Honor/EXP Balls;
the custom route retains its frozen eligibility, currency and six-item contents
through suppressed writes and concurrent retries. Both preserve the stored
garment and retain their production statistic/item-loader persistence checks.

Fixture Vitality is set before wardrobe equipment recalculation. Match outcomes
are controlled; the garment is supplied. Fault recovery in these fixtures resets
the fence explicitly, and is not described as a full character reconnect. The
daily bag-count implementation is shared with team rewards, but this added
wardrobe scenario directly exercises solo delivery. Existing team regressions
remain covered by the suite.

The isolated build and 1,135 Arena daily/prerequisite checks pass. Owner WIP was
present in the build; live deployment and native acceptance remain pending.

## Auction delivery

AuctionHouse now excludes wardrobe storage from the saved bag count used for
item delivery. Both silver and CP purchase fixtures deposit and take off a
supplied garment before bidding. A full forty-item bag preserves the purchased
item in escrow; releasing one slot permits exactly one delivery after service
reinitialization. Original item identity, perfection and refinery state retain
their existing assertions. Production character saves and a subsequent item
load preserve the delivered equipment and the stored garment row.

The purses, sale item and cosmetic are supplied fixtures. The wardrobe extension
does not claim a full character reconnect or native auction UI acceptance. The
isolated build and 93 auction checks pass, including existing bid, ownership,
capacity and rollback checks. Owner WIP was present in the build; not deployed.

## Ancient Devil certificates and battle rewards

AncientDevilStore and AncientDevilBattles now exclude wardrobe storage from
their saved bag counts. The ordinary Windwalker trial fixture deposits and takes
off a supplied garment before entry, then completes combat, certificate claim,
amulet exchange and production reload with its stored garment preserved.

The battle fixture gives the summoner a stored garment after the prerequisite
offering journey. Existing paid recovery, Dragon Ball and six-Meteor claims,
capacity checks, failed-write rollback and production reload run with that garment
present. Its saved row remains unchanged. The rollback assertion now counts bag
rows explicitly so it can distinguish retained wardrobe storage from reward items.

These are controlled combat/store fixtures; the garment is supplied and this
extension does not prove native presentation or additional reward editions. The
isolated build and 331 Ancient battle/prerequisite checks pass. Owner WIP was
present in the build; live deployment and native acceptance remain pending.

## Flame Lit rune progression

FlameLitStore now excludes wardrobe storage from its saved bag count. The main
ordinary store fixture deposits and takes off a supplied garment before beginning
the rune sequence. It retains full-bag replacement, failed-step rollback, lost-rune
restart, ordered lighting, time gates and final reward checks. Production reload
after the claim preserves the stored garment. The failed-replacement assertion
counts bag rows explicitly, independently of that stored cosmetic.

The isolated build and 95 Flame checks pass, including the suite's separate
packet-driven walking/dialogue journey. The added wardrobe scenario exercises
the store journey; it does not establish wardrobe coverage of every movement
fixture or native presentation. Owner WIP was present in the build; no live
deployment was performed.

## Guild rewards and Holy Grail

GuildSeasonClaims, GuildSeasonPrizes, GuildWarPurses and HolyGrailCrafting now
exclude wardrobe storage from their saved bag counts. The ordinary successor
leader deposits a supplied garment before claiming the earned weekly token;
the same stored design survives Bronze redemption and production reload. The
twelve-season Grail candidate carries a stored garment through earned tokens,
Gold exchanges, trophy rollovers, crafting and production reload. The war-purse
leader also deposits a garment before payout and preserves it after reload.

Existing role ownership, claim windows, full-bag behavior, exact payments,
suppressed-write rollback and replay checks remain. Two rollback assertions now
count bag rows explicitly rather than counting unrelated stored cosmetics.
The cosmetic and starting characters are supplied fixtures; battle outcomes and
season clocks remain controlled, not native balance or calendar acceptance.

The isolated build and 353 guild-season checks pass. Owner WIP, including the
uncommitted GuildIdentity changes, was present in the build and is not part of
this commit. Live deployment and native acceptance remain pending.

## Staged Elimination storage

EliminationStore now excludes wardrobe storage from its saved bag count. The
ordinary store fixture carries a supplied stored garment through herb collection,
replacement, spice progression, cleanup and reload. A separate den fixture adds
a stored garment before combat and preserves it through final reward and reload.
Existing assertions retain the daily rune reward and absence of P7 rewards.
Rollback bag assertions now exclude wardrobe storage explicitly.

The isolated build and 201 Elimination checks pass. These exercise staged code
with controlled fixtures, not a released player service. EliminationLifecycle
still has `ReleaseReady=false`; no activation, deployment or native acceptance
was performed. Owner WIP was present in the validation build.

## Exact-pattern audit status

A bounded source search originally found 26 further exact bag-count queries.
The fixes above leave zero occurrences of the original unfiltered exact pattern
in Game/Features. Route coverage and its limits are recorded above; this is not
proof that every inventory query in the repository is correct. Staged events
remain staged; correcting inventory accounting does not authorize enabling them
or mark their native acceptance complete. Other query
spellings and identity-list checks are outside this exact-pattern inventory;
ArenaPrizeConversions was an additional confirmed identity-list defect, fixed above.

A follow-up single-line search for count/UID selections with position zero found
seven apparent candidates. Inspection of their complete SQL showed six already
exclude wardrobe rows on continuation lines (AncientDevilStore, three
EliminationStore selections, FlameLitStore and TrojanEpicStore). The remaining
BattlePowerTournament query verifies one freshly inserted reward by exact UID
and item type, rather than counting the bag. No additional defect was confirmed
by that bounded search; aliases, generated SQL and other directories remain
outside its scope.
