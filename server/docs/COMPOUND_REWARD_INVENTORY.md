# Active compound reward inventory (M6.56b)

Bounded source pass, 2026-09-22, HEAD at start `4ae4ca6`, continued through `ead18142`.
M6.56b9a enumerates every top-level `ItemHandler.UseItem` case. Direct daily P7
remains prohibited.

## Method

Traced ordinary-player item-use (`ItemHandler.UseItem` / `ItemUsage` action 4)
and already-owned daily quest services. Ordinary 1009/action-4 reaches `UseItem`
when `GetPositionFromID` is inventory (0), the default for non-equipment IDs.
A path is **compound** when one completion writes two or more of: item,
currency, EXP, Study, Chi/RVP, or a daily/timer counter. Lexical ID presence
is not treated as availability. The exhaustive case table is
[ITEM_USE_COMPOUND_AUDIT.md](ITEM_USE_COMPOUND_AUDIT.md).

## Already atomic (not repaired here)

| Path | Access | Repeat | Terms | Persistence | Status |
| --- | --- | --- | --- | --- | --- |
| Ordinary EXP potions 722700/723017 | item-use 1009/4 | while bonus is zero | 10/60 minute double-EXP | `revora_exp_potion_uses` + durable timer | [EXP_POTION_TRANSACTIONS.md](EXP_POTION_TRANSACTIONS.md) |
| Tournament/super EXP packs 720793/720981/728201 | item-use 1009/4 | while double-EXP timer is zero | 3600/3600/7200s at installed 2x kill multiplier | `revora_exp_pack_uses` + durable timer | [TOURNAMENT_EXP_PACKS.md](TOURNAMENT_EXP_PACKS.md) |
| EXPCarePill 3002559 | item-use 1009/4; leftover bags / 3002560 combine | while protection timer is zero | `ExpProtectionTime=3600` | `revora_exp_protection_uses` + durable timer | [EXP_PROTECTION.md](EXP_PROTECTION.md) |
| EXPCarePillScrap 3002560 x10 | item-use 1009/4 | while the clicked stack has ten or more | consume 10; one bound 3002559; +3,000 RVP; keep remainder | `revora_exp_care_combines` + `entities.revorapoint` | [EXP_CARE_COMBINE.md](EXP_CARE_COMBINE.md) |
| TimeDrop hourglasses 3007985/3007989/3007992 | leftover bag item-use 1009/4 | while both timers are zero | 1800/3600/7200s TimeDropSlayer and double-EXP | `revora_timedrop_uses` + durable timers | [TIMEDROP_HOURGLASS.md](TIMEDROP_HOURGLASS.md) |
| Welcome packs 723753→723776 | JustCreated 723753; item-use 1009/4 | level/rebirth gates | existing class/currency/item terms including 1,000,000 CP and L110 duplicate jewelry | `revora_welcome_pack_uses` + CP/silver | [WELCOME_PACKS.md](WELCOME_PACKS.md) |
| Starter packs 727026→727050 | JustCreated 727026; item-use 1009/4 | level 1/5/…/120 | existing silver/CP/item terms; 727044 gates at 95; 727049 raw class ≤75; 727050 terminal | `revora_starter_pack_uses` + CP/silver | [STARTER_PACKS.md](STARTER_PACKS.md) |
| Leftover CP bags (35 IDs) | item-use 1009/4 | while the bag is owned | exact 3–138000 CP; 720685 Nimbus announcement | `revora_cp_bag_uses` + CP CAS | [CONQUER_POINT_BAGS.md](CONQUER_POINT_BAGS.md) |
| 10 EXP Ball pack 720757 / MeteorBox 723268 | item-use 1009/4 | 720757 Count≤39 plus exact 10-output room; 723268 Count≤30 | 10×723700 / 5×1088001 unbound plus-0 | `revora_fixed_output_pack_uses` | [FIXED_OUTPUT_PACKS.md](FIXED_OUTPUT_PACKS.md) |
| Leftover Chi items with a current producer (6 IDs) | item-use 1009/4 | while the item is owned | exact +500…+3000 Chi; 729242 misspelling | `revora_chi_item_uses` + ChiWallet | [CHI_POINT_ITEMS.md](CHI_POINT_ITEMS.md) |
| Leftover silver/study bags 3005117–3005125 | item-use 1009/4 | while the bag is owned | exact leftover silver/study; catalog copy for 3005118–3005122 names larger silver | `revora_silver_study_bag_uses` + SilverBalance | [SILVER_STUDY_BAGS.md](SILVER_STUDY_BAGS.md) |
| Silver pouches 3006594 / 723721 / 723723 | item-use 1009/4 | while the pouch is owned | exact leftover 200,000 / 100,000,000 / 200,000,000,000 silver; 723723 catalog names 500,000,000 | `revora_silver_pouch_uses` + SilverBalance | [SILVER_POUCHES.md](SILVER_POUCHES.md) |
| Study books 720774 / 720775 | item-use 1009/4 confirm | while the book is owned | leftover +50 Study; catalog once-a-day copy is not enforced | `revora_study_book_uses` + study CAS | [STUDY_BOOKS.md](STUDY_BOOKS.md) |
| DragonBall packs 720028 / 720884 | item-use 1009/4 | while the pack is owned | 10/5 unbound plus-0 1088000; Count≤31 / Count≤30 | `revora_dragon_ball_pack_uses` | [DRAGON_BALL_PACKS.md](DRAGON_BALL_PACKS.md) |
| Prize Center packs 720836 / 728898 / 728899 / 728900 / 3004257 | item-use 1009/4 | while the pack is owned | leftover random accessories/scraps or exact 8/8/6 combines; remainder kept | `revora_prize_center_pack_uses` | [PRIZE_CENTER_PACKS.md](PRIZE_CENTER_PACKS.md) |
| EasterGoldPack 720121 | item-use 1009/4 | while the pack is owned | leftover Random.Next(1,31) garments/mounts; case 31 unreachable | `revora_easter_gold_pack_uses` | [EASTER_GOLD_PACKS.md](EASTER_GOLD_PACKS.md) |
| Daily Sign-In packs 3100011 / 3100013–3100017 | item-use 1009/4; Daily Sign-In `MsgSignIn` | while the pack is owned | leftover items plus CP/silver/RVP/BoundCps; 3100014/3100016 BoundCps | `revora_daily_sign_in_pack_uses` | [DAILY_SIGN_IN_PACKS.md](DAILY_SIGN_IN_PACKS.md) |
| AncientStone combine 3004246 | item-use 1009/4 | while 20 bag units are owned | leftover 20-unit Count≤31 combine into unbound 720121 | `revora_ancient_stone_combines` | [ANCIENT_STONE_COMBINE.md](ANCIENT_STONE_COMBINE.md) |
| BrightStarStone combine 3009001 | item-use 1009/4; Vesper 900000014 | while 10 bag units are owned | leftover 10-unit Count≤31 combine into unbound 3009002 | `revora_star_stone_combines` | [STAR_STONE_COMBINE.md](STAR_STONE_COMBINE.md) |
| Attack/Defense potions 721623/721624/721672/721673/721627/721628 | item-use 1009/4; StarterPacks 727028/727041/727050 | while the potion is owned | leftover Stigma/MagicShield 15/30/45 Time, Increase and AddFlag | `revora_attack_defense_potion_uses` + durable grant | [ATTACK_DEFENSE_POTIONS.md](ATTACK_DEFENSE_POTIONS.md) |
| Power Arena packs (48 IDs) | item-use 1009/4; Wang 500010 badge exchange | while the pack is owned | leftover 16-family/3-tier extras and 724445 exception | `revora_power_pack_results` + `revora_power_exchanges` | [POWER_ARENA_PACKS.md](POWER_ARENA_PACKS.md) |
| HP/MP/LifeFruit medicine (19 IDs) | item-use 1009/4; Welcome 723753 | while the pill is owned | leftover ItemHP/ItemMP/LifeFruit and NoDrugsTime refuse/expire; durable Poison Star/Serenity/Tranquility/Qualifier lifecycle | `revora_healing_medicine_uses` + HP/MP CAS + cooldown | [HEALING_MEDICINE.md](HEALING_MEDICINE.md) |
| Bloodshed Sea boosts (10 IDs) | item-use 1009/4; EShop 16834/16852-16856 | while the potion is owned on map 3846 | leftover 80-second stamp/refresh without stacking | `revora_bloodshed_boost_uses` + durable type/timer | [BLOODSHED_SEA.md](BLOODSHED_SEA.md) |
| HonorShop leftover refinery packs (30 IDs) | item-use 1009/4; HonorShop UID 6000 | while the pack is owned | leftover GetNextRefineryItem extra/offset/remap | `revora_honor_refinery_pack_uses` | [HONOR_REFINERY_PACKS.md](HONOR_REFINERY_PACKS.md) |
| HonorShop leftover GainRefineryItem packs (30 IDs) | item-use 1009/4; HonorShop UID 6000 | while the pack is owned | leftover DatabaseRefineryBoxes mapping; unbound material | `revora_gain_refinery_pack_uses` | [GAIN_REFINERY_PACKS.md](GAIN_REFINERY_PACKS.md) |
| HonorShop FancyHossuHairPack 3006319 | item-use 1009/4; HonorShop UID 6000 | while the pack is owned | leftover Next(1,6) picks 1-5 / 3006165-3006169; Count<=38 | `revora_hossu_hair_pack_uses` | [HOSSU_HAIR_PACKS.md](HOSSU_HAIR_PACKS.md) |
| Gem / Dragon Soul packs 3005130, 3005132, 3005133, 3005135 | item-use 1009/4; Way of Heroes substages 502/601/708/803/709/903/1006 | primary equipped sockets, super gems, or a recorded Snow Banshee/Terato kill; one claim | leftover uniform bound reward; claim is bound plus 0 | `revora_way_of_heroes_claims` and `revora_gem_dragon_soul_pack_uses` | [GEM_DRAGON_SOUL_PACKS.md](GEM_DRAGON_SOUL_PACKS.md) |
| HonorShop inert Hossu packs | HonorShop UID 6000 buy refuse | N/A | no leftover UseItem mapping; already-held items stay | none | [HOSSU_INERT_PACKS.md](HOSSU_INERT_PACKS.md) |
| Red rune fragment/pack 3306369/3306509 | item-use 1009/4; NPC 900181 Online Points | while the fragment/pack is owned | 20 units to bound pack; Next(1,11) picks 1-10 bound red runes | `revora_red_rune_combines` + `revora_red_rune_pack_uses` | [RED_RUNE_PACKS.md](RED_RUNE_PACKS.md) |
| Standard/power EXP Balls | item-use | 10/day standard | existing ball curve | `revora_exp_ball_days` | [EXPERIENCE_CONSUMABLES.md](EXPERIENCE_CONSUMABLES.md) |
| Elite PK prize bundles | owned pack open | placement claim | configured bundle | pack receipts | [ELITE_PRIZE_PACK_TRANSACTIONS.md](ELITE_PRIZE_PACK_TRANSACTIONS.md) |
| Daily Hunt / Rare Materials / Soul Ball / Release Souls / Magnolia harvest / Exorcism / Ancestor Worship / Demon Exterminators | owned daily NPCs | Pacific day | configured purse/EXP/study/Chi/rune; no daily P7 | family receipts | [DAILY_REWARD_POLICY.md](DAILY_REWARD_POLICY.md) |

Daily Envoy 79882 is a hub, not a second prize ([DAILY_ENVOY_REWARD_AUDIT.md](DAILY_ENVOY_REWARD_AUDIT.md)).

## Highest-risk explicit-contract gap (this batch)

Prizewell TimeDrop issuance is closed. Leftover hourglass use is repaired in
[TIMEDROP_HOURGLASS.md](TIMEDROP_HOURGLASS.md). EXPCarePill 3002559 use and
3002560 combine were repaired in M6.56b4/M6.56b5.

Magnolia version-four custom-pack issuance is already implemented
([MAGNOLIA_TRANSACTIONS.md](MAGNOLIA_TRANSACTIONS.md), [MAGNOLIA_GIFT_USE.md](MAGNOLIA_GIFT_USE.md)).
Native Magnolia acceptance remains pending. Starter packs 727026–727050 are
repaired in [STARTER_PACKS.md](STARTER_PACKS.md). Leftover CP bags are repaired
in [CONQUER_POINT_BAGS.md](CONQUER_POINT_BAGS.md). 720757 and 723268 are
repaired in [FIXED_OUTPUT_PACKS.md](FIXED_OUTPUT_PACKS.md). Eleven Chi consumers, including catalog pills 3000051/729476–729479 with no
new issuer, are repaired in [CHI_POINT_ITEMS.md](CHI_POINT_ITEMS.md). Surprise
box 722178 is catalog-only leftover source+CP with no ordinary producer
([SURPRISE_BOX.md](SURPRISE_BOX.md)).
Remaining live split-writes include catalog-only Surprise box 722178,
nested/gear packs 721222–721245 (catalog +
unreachable EShop 8422 stock; [NESTED_GEAR_PACKS.md](NESTED_GEAR_PACKS.md)),
classified silver pouches 3001266/723722/3007365–3007368 (GM FirstCredit,
lottery table, uninitialized UID-6001 shop; [SILVER_POUCHES.md](SILVER_POUCHES.md)),
medicine bundles 720010–720017 (catalog + unreachable Shop.dat stock;
[MEDICINE_BUNDLES.md](MEDICINE_BUNDLES.md)) and other leftover packs
([ITEM_USE_COMPOUND_AUDIT.md](ITEM_USE_COMPOUND_AUDIT.md)). DragonBall packs
720028/720884 are repaired in [DRAGON_BALL_PACKS.md](DRAGON_BALL_PACKS.md). Prize Center
packs 720836/728898/728899/728900/3004257 are repaired in
[PRIZE_CENTER_PACKS.md](PRIZE_CENTER_PACKS.md). EasterGoldPack 720121 is
repaired in [EASTER_GOLD_PACKS.md](EASTER_GOLD_PACKS.md); JoyCPPack 720654
stays classified leftover. Daily Sign-In packs 3100011/3100013–3100017 are
repaired in [DAILY_SIGN_IN_PACKS.md](DAILY_SIGN_IN_PACKS.md). AncientStone
3004246 combine is repaired in [ANCIENT_STONE_COMBINE.md](ANCIENT_STONE_COMBINE.md).
Silver/study bags
3005117–3005125 are repaired in [SILVER_STUDY_BAGS.md](SILVER_STUDY_BAGS.md).
Silver pouches 3006594/723721/723723 are repaired in
[SILVER_POUCHES.md](SILVER_POUCHES.md).

## TimeDrop issuance (closed; not repaired)

Prizewell 900035 options 101/102 are unreachable: the NPC is jailed and
`HandleClosed` returns before the switch. `Random.Next(1,5)` never selects
3007992. See [TIMEDROP_HOURGLASS.md](TIMEDROP_HOURGLASS.md). No replacement
daily claim is invented.

## Remaining split-write item-use branches (not this batch)

The UseItem switch census after this batch is 271 owned-fail-closed, 149 live
split-write, 55 single-consume and 43 other (518 numeric top-level IDs, 66 intercepts).
VIP-day items 3004121–3004124/3000794 are catalog + unreachable UID 6001
shop / leftover consumer ([VIP_DAY.md](VIP_DAY.md)). DisguiseAmulet 723724
is catalog + leftover consumer; Collector Zhao is a sink
([DISGUISE_AMULET.md](DISGUISE_AMULET.md)). Attack/Defense potions, Power Arena packs,
HP/MP/LifeFruit medicine, Bloodshed Sea boosts, HonorShop leftover
GetNextRefineryItem packs, HonorShop leftover GainRefineryItem packs and
HonorShop FancyHossuHairPack 3006319 are owned
([HONOR_REFINERY_PACKS.md](HONOR_REFINERY_PACKS.md), [GAIN_REFINERY_PACKS.md](GAIN_REFINERY_PACKS.md),
[HOSSU_HAIR_PACKS.md](HOSSU_HAIR_PACKS.md)).
HonorShop inert Hossu packs 3006320/3006321 and older 3006316–3006318 are
fenced at Honor buy ([HOSSU_INERT_PACKS.md](HOSSU_INERT_PACKS.md)).
Leftover epic-weapon packs 3003340/3006016/3006226/3006700/3307451 stay
leftover ([EPIC_WEAPON_PACKS.md](EPIC_WEAPON_PACKS.md)).
Red rune 3306369/3306509 are owned ([RED_RUNE_PACKS.md](RED_RUNE_PACKS.md)).
Yellow/blue 3306370/3306371 fragments and 3306510/3306511 packs now use atomic consumer transactions
([YELLOW_BLUE_RUNE_PACKS.md](YELLOW_BLUE_RUNE_PACKS.md)); there is no new issuance route.
+1StonePack 723712 is owned ([PLUS_ONE_STONE_PACKS.md](PLUS_ONE_STONE_PACKS.md)).
+2StonePack 729022 and +3StonePack 729023 are owned ([HIGHER_STONE_PACKS.md](HIGHER_STONE_PACKS.md)).
MillionaireLee options 1/2/3/4/5/7 now pack atomically.
Gem option 6 is menu text only and has no handler.
720028 and 720757 unpacking copies pack binding. 720884 and 723268 stay unbound.
MeteorTearPack 723711 is owned by FixedOutputPacks. Mil.Ginseng Box 721330 stays leftover.
Unstocked refinery siblings stay leftover ([REFINERY_SIBLING_AUDIT.md](REFINERY_SIBLING_AUDIT.md)).
Highest leftover families:
catalog-only Surprise box 722178,
nested/gear packs 721222–721245 (catalog + unreachable EShop 8422 stock),
classified silver pouches 3001266/723722/3007365–3007368 (uninitialized UID-6001
shop), medicine bundles 720010–720017 (catalog + unreachable Shop.dat stock),
VIP-day tokens 3004121–3004124/3000794 (catalog + unreachable UID 6001 shop),
DisguiseAmulet 723724 (catalog + leftover consumer),
unstocked refinery siblings ([REFINERY_SIBLING_AUDIT.md](REFINERY_SIBLING_AUDIT.md)),
leftover epic-weapon packs ([EPIC_WEAPON_PACKS.md](EPIC_WEAPON_PACKS.md)),
classified leftover 721330 ([PLUS_ONE_STONE_PACKS.md](PLUS_ONE_STONE_PACKS.md)); MeteorTearPack 723711 is owned ([FIXED_OUTPUT_PACKS.md](FIXED_OUTPUT_PACKS.md))
and other leftover packs.
LuxuryGoldEquipmentPack 3005368 stays leftover: the chat command `testitem` is not an ordinary producer, and its `Random.Next(1, 4)` case 4 is unreachable ([GEM_DRAGON_SOUL_PACKS.md](GEM_DRAGON_SOUL_PACKS.md)).
Legacy NPC numeric soul shops
behind closed-stall returns stay excluded
([P7_REWARD_SOURCE_AUDIT.md](P7_REWARD_SOURCE_AUDIT.md)).

Way of Heroes now atomically owns level stages 101, 201, 301, and 401,
rebirth stages 501 and 701, subclass stages 504, 608, and
711, and the seven gem/Dragon Soul substages above. Disposable production
experience, rebirth, subclass enrollment, one socketed
weapon, and one Terato kill each claim once. The other socket and super-gem
thresholds still use saved equipped rows. Chi packs 3005126-3005129,
guild stage 303, super-talisman stage 408, main prizes, and every other substage stay refused.
Held packs remain usable.

Next checkpoint: repair guild payment/membership persistence and its claim
evidence atomically before restoring stage 303. MCP 334 review reproduced a
split join write; MCP 338 review reproduced orphaned disband state, reused-ID
eligibility, recruitment bypass, and lost founder rank after projection failure.
The unsafe integration was removed. Character Save no longer writes GuildID
from memory, and packet 2227 enforces session, blacklist and existing guild
recruitment requirements. These guards do not complete atomic guild ownership
or enable 303. MCP 343 also reproduced incomplete startup identity/recovery,
reset guild metadata, and stale-session departure in the incarnation proposal;
that integration remains removed. Missing-pointer saves now preserve rank,
guild commands reject stale/fenced callers, and both reciprocal acceptance
routes recheck recruitment requirements. Review 348 retains a shared legacy guild-row materializer and fixes startup
relationship ordering. It rejects the enrollment-date incarnation proposal:
same-day ID reuse and cached guild objects do not establish durable identity.
A disconnected guild-identity/roster read checks a nullable row incarnation and
member identities without startup or reconnect wiring. Review 353 rejects
incomplete rosters, conflicting marks, multiple leaders and managed-to-legacy
downgrades. The disconnected read now also returns stored metadata, donations,
arsenal, incarnation-bound allies, and the stored inscribed-item columns
(plus, sockets, bound, durability, refinement, lock, and the direct item token).
A partial DTO projection copies item/arsenal, roster, selected guild fields,
and saved item additions, and does not publish a guild. Review 363 rejects
malformed item values, orphan bindings for the incarnation and cross-guild
inscriptions of bound items. Saved purification, permanent refinery, temporary
refinery, and stabilization credit are read with a direct addition-row binding.
Review 367 rejects reused addition IDs inheriting stale marks: a nonempty unique
token on the addition row must match the binding, item lifetime, and guild
incarnation. Token columns have no default or backfill. Production addition
writers are unchanged and cannot yet produce this managed image.
Raw addition ids and timestamps are not treated as active effects. Same-day
reuse is a different incarnation. Review 358 rejects contradictory association
ownership, missing or unbound peers, and transferred inscribed items. A replaced
item uid is refused unless its token and binding match. Stage 303 stays refused.
A disconnected union read now requires a union-row token and guild/member
bindings. A stored union number is not that lifetime. Guild members may keep a
different union id, and an independent member need not join the guild. Review
373 reconciles saved guild references against union bindings in both directions,
refuses stale incarnation marks, and preserves verified guild links in the DTO.
Character rows now carry `revora_entity_token`, and guild and union member
marks must match it. A roster can name more than one union lifetime. Stored
role columns stay raw, while leader and array slots produce separate derived
values. The same read now keeps saved face, body, and a nobility row bound to
that character token. Donation rank stays unresolved because live sorting needs
the whole board. `Guild.BlackList` is still process memory, so an empty list is
not a recovered blacklist. `Project` copies the saved guild fields, incarnation,
and ally and enemy identities. It does not decode `adend` or publish a guild.
`Derive` reports item time, lock, purification, and refinery activity from a
supplied clock and catalog without deleting rows or rewriting the saved
refinery id. `DeriveScores` reports arsenal donation, battle power, and
prestige from supplied catalogs, with separate ItemAdding and ItemSoul scalar
results. Only ItemAdding rewrites its refinery id; a legacy saved id without
addition rows emits no ItemAdding effect. No arsenal packet is sent and saved
donation stays raw. Soul combat-stat catalogs and arsenal aggregate/order/cast
calculations remain open, as do guild rank and advertisement
behavior remain unresolved. Startup and reconnect still do not call either
path. Paid creation and stage 303 stay unfinished. See
[SILVER_STUDY_BAGS.md](SILVER_STUDY_BAGS.md). Other Way of Heroes substages and
main prizes still need their own server predicates. Stage 408 stays refused until a plus-independent
super-talisman count has an ordinary acquisition path. Do not treat seeded gear
or a synthetic victory as production earning for the other socket thresholds.

M6.56b parent stays open until every remaining live split-write family is
repaired. Native acceptance and deployment remain open.


## Surprise Box consumer safety closure (2026-10-03)

M6.56b9d1 now atomically owns the existing722178 source+2000-CP consumer.
The earlier catalog-only producer classification still applies; no issuer or
new quest family is created. The previous unowned-consumer statement is
superseded because held boxes remained executable and exposed to split writes.
Capacity, malformed/fake refusal, concurrent/direct consumption, rollback/retry,
old-ID compatibility and exact cold persistence pass privately.
[Evidence](SURPRISE_BOX.md#atomic-consumer-and-process-validation-2026-10-03).
The dated census above is historical; this is not a full re-enumeration. Other
compound consumers, native acceptance and deployment remain open.


## Medicine bundle consumer closure (2026-10-03)

M6.56b9h1 now atomically owns720010-720017 held-item delivery through
HealingMedicine, with the exact existing three-pill mapping and capacity gate.
The earlier unowned-consumer statement is superseded, while its catalog-only /
unreachable-stock issuance audit remains valid. All eight outputs, concurrency,
rollback/retry and cold persistence pass privately; no new quest family or
issuer is counted. [Evidence](MEDICINE_BUNDLES.md#atomic-bundle-delivery-2026-10-03).
Other compound consumers and native/deployment gates remain open.
