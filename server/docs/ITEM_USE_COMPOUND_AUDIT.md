# Ordinary item-use compound audit (M6.56b9a)

Server source pass, 2026-09-22. Not deployed. Not native-client acceptance.

2026-09-27 follow-up: the 17 stocked mount packs 3000254 and 3000256-3000271
are now owned by FixedOutputPacks with fail-closed legacy cases. Their historical
`live-split-write` rows below are superseded by [atomic earning/process
evidence](MOUNT_PACK_EARNING.md). Historical census totals are not recomputed here;
the other eight legacy mount-pack cases are unchanged.

## Method

Enumerated every top-level `switch (item.ID)` case in `ItemHandler.UseItem`, grouping C# fall-through labels with the shared body.
Ordinary 1009/action-4 reaches `UseItem` when `GetPositionFromID` returns `Positions.Inventory` (0), the default for non-equipment IDs. Direct `ItemHandler.UseItem` is a second path.
A case is **compound** when one completion writes two or more of: source consumption, item outputs, silver, CP, RVP, Chi, EXP/timer, or Study.
Timer writes are executable `client.Entity` timer/stamp/day-state assignments (`*Time`/`*Stamp`/`*Days`, including `NoDrugsTime` cooldowns) and `AddFlag(`. `#region` names and packet-copy fields such as `upd.FreeTimeTodeyUsed` are not timer writes.
`AddRune` is an item output. Source+timer is live-split-write.
TryUse intercepts ahead of the switch are already-owned services. Leftover case bodies are classified independently of intercept names: fail-closed throw, leftover `TryUse` delegate (independently live), or leftover-live (reachable if the intercept returns false because initialization is absent).
Owned-fail-closed effect labels record the owned contract, not the throw body.

## Switch census after this batch

| Kind | Count |
| --- | ---: |
| live-split-write | 145 |
| other | 43 |
| owned-fail-closed | 275 |
| single-consume | 55 |
| **total top-level case IDs** | **518** |

## Already atomic intercepts (ahead of the switch)

FlameLitQuest, EliminationQuest, HouseFurniture, NinjaEpicQuest, MonkEpicQuest, WarriorEpicQuest, MonkSutta, SwordMasterEncounter, PurpleBansheeEncounter, TeratoEncounter, DragonSlayerHunt, RetiredHairDyes, RetiredBlackTulip, PrayingStones, WeaponSkillBooks, ExperienceConsumables, ExperiencePotions, TournamentExpPacks, ExperienceProtection, ExpCareCombine, TimeDropHourglasses, WelcomePacks, StarterPacks, ConquerPointBags, FixedOutputPacks, DragonBallPacks, PrizeCenterPacks, EasterGoldPacks, DailySignInPacks, AncientStoneCombine, StarStoneCombine, AttackDefensePotions, ChiPointItems, SilverStudyBags, SilverPouches, StudyBooks, ChiTokenCash, QuestGiftPacks, PenitenceAmulets, ElitePkPacks, EliteNestedPacks, P7SoulPacks, ArenaPrizeConversions, MagnoliaQuest, DailyPriceQuest, AnniversaryQuest, DailySoulBall, DisCityJades/Treasury/DarkHorn/Relics/Quest/Packs, ExorcismRewards, PowerArenaPacks, HealingMedicine, BloodshedSeaBoosts, HonorRefineryPacks, GainRefineryPacks, HossuHairPacks, RedRunePacks, YellowBlueRunePacks, PlusOneStonePacks, HigherStonePacks, GemDragonSoulPacks, SummerCrafting, LabyrinthItems.

## This batch repair

M6.56b10k owns Way of Heroes gem and Dragon Soul packs 3005130, 3005132, 3005133 and 3005135 ([contract](GEM_DRAGON_SOUL_PACKS.md)). The census is now 145/275/55/43. Intercept families are 67. LuxuryGoldEquipmentPack 3005368 stays live-split-write: its only source reference outside UseItem is the chat command `testitem`, and `Random.Next(1, 4)` never reaches 4.

M6.56b10j owns catalog Chi pills 3000051 and 729476–729479 inside ChiPointItems ([contract](CHI_POINT_ITEMS.md)). The census is now 149/271/55/43. Intercept families stay 66 because the existing family was extended. No issuance route was added. 729479 now reports the committed 400. 729480 stays SalesPamphlet.

Leftover `#region Bags Cps` bags are owned by [CONQUER_POINT_BAGS.md](CONQUER_POINT_BAGS.md). Fixed-output packs 720757, 723268 and 723711 are owned by [FIXED_OUTPUT_PACKS.md](FIXED_OUTPUT_PACKS.md). The six Chi items with a current ordinary producer and the five catalog-only Chi pills are owned by [CHI_POINT_ITEMS.md](CHI_POINT_ITEMS.md). 729480 remains SalesPamphlet. Surprise box 722178 is catalog-only leftover source+CP ([SURPRISE_BOX.md](SURPRISE_BOX.md)). Silver/study bags 3005117â€“3005125 are owned by [SILVER_STUDY_BAGS.md](SILVER_STUDY_BAGS.md). Nested/gear packs 721222â€“721245 are catalog + unreachable EShop 8422 stock / leftover consumer-only ([NESTED_GEAR_PACKS.md](NESTED_GEAR_PACKS.md)). Silver pouches 3006594, 723721 and 723723 are owned by [SILVER_POUCHES.md](SILVER_POUCHES.md). 3001266, 723722 and 3007365â€“3007368 stay leftover split-writes. Medicine bundles 720010â€“720017 are catalog + unreachable Shop.dat stock / leftover consumer-only ([MEDICINE_BUNDLES.md](MEDICINE_BUNDLES.md)). Study books 720774/720775 are owned by [STUDY_BOOKS.md](STUDY_BOOKS.md). DragonBall packs 720028/720884 are owned by [DRAGON_BALL_PACKS.md](DRAGON_BALL_PACKS.md). Prize Center packs 720836/728898/728899/728900/3004257 are owned by [PRIZE_CENTER_PACKS.md](PRIZE_CENTER_PACKS.md). EasterGoldPack 720121 is owned by [EASTER_GOLD_PACKS.md](EASTER_GOLD_PACKS.md). JoyCPPack 720654 stays leftover split-write. Daily Sign-In packs 3100011/3100013â€“3100017 are owned by [DAILY_SIGN_IN_PACKS.md](DAILY_SIGN_IN_PACKS.md). AncientStone/HolyStone 3004246 combine is owned by [ANCIENT_STONE_COMBINE.md](ANCIENT_STONE_COMBINE.md). BrightStarStone 3009001 combine is owned by [STAR_STONE_COMBINE.md](STAR_STONE_COMBINE.md). Attack/Defense potions 721623/721624/721627/721628/721672/721673 are owned by [ATTACK_DEFENSE_POTIONS.md](ATTACK_DEFENSE_POTIONS.md). The 48 reachable Power Arena refinery packs are owned by [POWER_ARENA_PACKS.md](POWER_ARENA_PACKS.md). HP/MP/LifeFruit medicine is owned by [HEALING_MEDICINE.md](HEALING_MEDICINE.md). Bloodshed Sea boosts 3004230/3004182/3004231-3004238 are owned by [BLOODSHED_SEA.md](BLOODSHED_SEA.md). Thirty leftover HonorShop GetNextRefineryItem packs are owned by [HONOR_REFINERY_PACKS.md](HONOR_REFINERY_PACKS.md). Thirty leftover HonorShop GainRefineryItem packs are owned by [GAIN_REFINERY_PACKS.md](GAIN_REFINERY_PACKS.md). Unstocked GetNextRefineryItem primaries, LegendRefineryPack 723693, GainRefineryItem primaries and empty-box 725055-725058 stay leftover with no ordinary producer ([REFINERY_SIBLING_AUDIT.md](REFINERY_SIBLING_AUDIT.md)). HonorShop FancyHossuHairPack 3006319 is owned by [HOSSU_HAIR_PACKS.md](HOSSU_HAIR_PACKS.md). HonorShop inert Hossu packs 3006320/3006321 and older 3006316-3006318 have no leftover UseItem mapping; HonorPurchases refuses new Honor spend ([HOSSU_INERT_PACKS.md](HOSSU_INERT_PACKS.md)). Leftover epic-weapon packs 3003340/3006016/3006226/3006700/3307451 stay leftover live-split-write ([EPIC_WEAPON_PACKS.md](EPIC_WEAPON_PACKS.md)). Leftover red rune 3306369/3306509 are owned by [RED_RUNE_PACKS.md](RED_RUNE_PACKS.md). Yellow/blue leftover rune consumers are owned by [YELLOW_BLUE_RUNE_PACKS.md](YELLOW_BLUE_RUNE_PACKS.md). Leftover +1StonePack 723712 is owned by [PLUS_ONE_STONE_PACKS.md](PLUS_ONE_STONE_PACKS.md). Mil.Ginseng Box 721330 stays leftover and is absent from installed catalogs. MeteorTearPack 723711 is owned by FixedOutputPacks. Leftover +2StonePack 729022 and +3StonePack 729023 are owned by [HIGHER_STONE_PACKS.md](HIGHER_STONE_PACKS.md). MillionaireLee options 1/2/3/4/5/7 now pack atomically; option 6 has no handler. Mechanical census now treats leftover VIP-day items and DisguiseAmulet 723724 source+timer as live-split-write. 3000788 VIP8 WhiteDragon summon is other (consume commented).

## Remaining live split-write families (not this batch)

| Family | Example IDs | Effects |
| --- | --- | --- |
| Catalog-only Surprise box | 722178 | source + 2000 CP; no ordinary producer; PowerEXPBall grant commented out |
| Nested/gear packs | 721222-721245 | source + items; catalog + unreachable EShop 8422 stock |
| Medicine bundles | 720010-720017 | source + items; catalog + unreachable Shop.dat gold shops 8159-8162/8593-8596 |
| Classified silver pouches | 3001266, 723722, 3007365-3007368 | source + silver; GM FirstCredit / lottery-table / uninitialized UID-6001 shop |
| Classified JoyCPPack | 720654 | source + 25,000 RVP; catalog 1,380 CP; GM FirstCredit |
| VIP-day items | 3004121-3004124, 3000794 | source + timer; catalog + unreachable UID 6001 shop / leftover consumer ([VIP_DAY.md](VIP_DAY.md)) |
| Disguise amulet | 723724 | source + timer; catalog + leftover consumer; Collector Zhao 3952/2070 is a sink ([DISGUISE_AMULET.md](DISGUISE_AMULET.md)) |
| Unstocked refinery siblings | GetNext primaries, LegendRefineryPack 723693, Gain primaries and empty-box 725055-725058; catalog + leftover consumer ([REFINERY_SIBLING_AUDIT.md](REFINERY_SIBLING_AUDIT.md)) | mixed |
| Leftover epic-weapon packs | 3003340, 3006016, 3006226, 3006700, 3307451; catalog + leftover consumer; dual-role 3003340/3006016 ([EPIC_WEAPON_PACKS.md](EPIC_WEAPON_PACKS.md)) | source+items |
| Classified ginseng box | 721330; leftover consumer; no ordinary producer; absent from catalogs ([PLUS_ONE_STONE_PACKS.md](PLUS_ONE_STONE_PACKS.md)) | source+items |
| Other leftover packs | remaining live-split-write packs | mixed |

721269/720957 Meteor/Dragon Soul custom packs are already owned by QuestGiftPacks.

## TryUse handled IDs (source-derived)

Exact acceptance sets from each pre-switch `TryUse` predicate (`Handles`, `Matches`,
`Material`/`Identity`, `Book`, ranges, and catalog keys). Notes use inclusive ranges;
`A-B/10` means every ID from A through B in steps of 10.

| Family | Handled IDs |
| --- | --- |
| FlameLitQuest | 729960-729970 |
| EliminationQuest | 723019 |
| HouseFurniture | 721177-721189 |
| NinjaEpicQuest | 3004464 |
| MonkEpicQuest | 3007564 |
| WarriorEpicQuest | 3200206 |
| MonkSutta | 3007561 |
| SwordMasterEncounter | 720842 |
| PurpleBansheeEncounter | 3004259 |
| TeratoEncounter | 720598 |
| DragonSlayerHunt | 711189-711195,720591-720597 |
| RetiredHairDyes | 1060030-1060090/10 |
| RetiredBlackTulip | 723584 |
| PrayingStones | 720578,1200000-1200002 |
| WeaponSkillBooks | 725005,725010-725014,725026-725027,725029-725031,725040-725044 |
| ExperienceConsumables | 722057,722136,723700,723744,723911 |
| ExperiencePotions | 722700,723017 |
| TournamentExpPacks | 720793,720981,728201 |
| ExperienceProtection | 3002559 |
| ExpCareCombine | 3002560 |
| TimeDropHourglasses | 3007985,3007989,3007992 |
| WelcomePacks | 723753,723755,723767,723772,723774,723776 |
| StarterPacks | 727026-727050 |
| ConquerPointBags | 720653,720655-720667,720675-720678,720681-720685,720687-720689,720693-720695,729911-729912,3001133-3001136 |
| FixedOutputPacks | 720757,723268,723711 |
| DragonBallPacks | 720028,720884 |
| PrizeCenterPacks | 720836,728898-728900,3004257 |
| EasterGoldPacks | 720121 |
| DailySignInPacks | 3100011,3100013-3100017 |
| AncientStoneCombine | 3004246 |
| StarStoneCombine | 3009001 |
| AttackDefensePotions | 721623,721624,721627,721628,721672,721673 |
| ChiPointItems | 729242,729481,3005126-3005129,3000051,729476-729479 |
| SilverStudyBags | 3005117-3005125 |
| SilverPouches | 723721,723723,3006594 |
| StudyBooks | 720774-720775 |
| ChiTokenCash | 729304 |
| QuestGiftPacks | 710212,720027,720957,721269,723714 |
| PenitenceAmulets | 720128,723727 |
| ElitePkPacks | 720714-720729 |
| EliteNestedPacks | 720049,720731,729626 |
| P7SoulPacks | 3004247-3004248 |
| ArenaPrizeConversions | 3004243-3004245,3009002 |
| MagnoliaQuest | 729306-729310 |
| DailyPriceQuest | 729087 |
| AnniversaryQuest | 3001172 |
| DailySoulBall | 729611-729614,729703 |
| DisCityJades | 3008059-3008060 |
| DisCityTreasury | 3006982 |
| DisCityDarkHorn | 790001 |
| DisCityRelics | 723088 |
| DisCityQuest | 3006981,3006983-3006984 |
| DisCityPacks | 720399,3008057-3008058 |
| ExorcismRewards | 720948,729594 |
| PowerArenaPacks | 723130-723135,723651-723677,723684-723686,724136-724147 |
| HealingMedicine | 1000000-1000040/10,1001000-1001040/10,1002000-1002050/10,725065,725066,723726 |
| BloodshedSeaBoosts | 3004230,3004182,3004231-3004238 |
| HonorRefineryPacks | 723679,723680,723682,723683,723691,723692,724149,724150,724193,724194,724196,724197,724199,724200,724202,724203,724205,724206,724208,724209,724211,724212,725161,725162,725164,725165,725167,725168,725170,725171 |
| GainRefineryPacks | 724131,724132,724134,724135,724152,724153,724155,724156,724158,724159,724161,724162,724164,724165,724167,724168,724170,724171,724173,724174,724176,724177,724179,724180,724182,724183,724185,724186,724191,724192 |
| HossuHairPacks | 3006319 |
| RedRunePacks | 3306369,3306509 |
| YellowBlueRunePacks | 3306370,3306371,3306510,3306511 |
| PlusOneStonePacks | 723712 |
| HigherStonePacks | 729022,729023 |
| GemDragonSoulPacks | 3005130,3005132,3005133,3005135 |
| SummerCrafting | 3303704-3303707,3303764-3303765,3303772-3303776,3303797-3303801 |
| LabyrinthItems | 721540 |

## Leftover TryUse disposition (per leftover case body)

Intercept names are not ownership proof. Every pre-switch `TryUse` family is listed.
Numeric rows are the exact intersection of that family's handled IDs with the 518-case switch.
`no-legacy-case` means that intersection is empty; handled IDs still appear in the catalog above.

| Family | ID | Disposition | Notes |
| --- | ---: | --- | --- |
| FlameLitQuest | - | no-legacy-case | 729960-729970 |
| EliminationQuest | - | no-legacy-case | 723019 |
| HouseFurniture | - | no-legacy-case | 721177-721189 |
| NinjaEpicQuest | - | no-legacy-case | 3004464 |
| MonkEpicQuest | - | no-legacy-case | 3007564 |
| WarriorEpicQuest | - | no-legacy-case | 3200206 |
| MonkSutta | - | no-legacy-case | 3007561 |
| SwordMasterEncounter | - | no-legacy-case | 720842 |
| PurpleBansheeEncounter | 3004259 | leftover-live | 3004259 |
| TeratoEncounter | 720598 | leftover-live | 720598 |
| DragonSlayerHunt | - | no-legacy-case | 711189-711195,720591-720597 |
| RetiredHairDyes | - | no-legacy-case | 1060030-1060090/10 |
| RetiredBlackTulip | - | no-legacy-case | 723584 |
| PrayingStones | - | no-legacy-case | 720578,1200000-1200002 |
| WeaponSkillBooks | - | no-legacy-case | 725005,725010-725014,725026-725027,725029-725031,725040-725044 |
| ExperienceConsumables | - | no-legacy-case | 722057,722136,723700,723744,723911 |
| ExperiencePotions | - | no-legacy-case | 722700,723017 |
| TournamentExpPacks | 720793 | fail-closed | 720793,720981,728201 |
| TournamentExpPacks | 720981 | fail-closed | fail-closed |
| TournamentExpPacks | 728201 | fail-closed | fail-closed |
| ExperienceProtection | 3002559 | fail-closed | 3002559 |
| ExpCareCombine | 3002560 | fail-closed | 3002560 |
| TimeDropHourglasses | 3007985 | fail-closed | 3007985,3007989,3007992 |
| TimeDropHourglasses | 3007989 | fail-closed | fail-closed |
| TimeDropHourglasses | 3007992 | fail-closed | fail-closed |
| WelcomePacks | 723753 | fail-closed | 723753,723755,723767,723772,723774,723776 |
| WelcomePacks | 723755 | fail-closed | fail-closed |
| WelcomePacks | 723767 | fail-closed | fail-closed |
| WelcomePacks | 723772 | fail-closed | fail-closed |
| WelcomePacks | 723774 | fail-closed | fail-closed |
| WelcomePacks | 723776 | fail-closed | fail-closed |
| StarterPacks | 727026 | fail-closed | 727026-727050 |
| StarterPacks | 727027 | fail-closed | fail-closed |
| StarterPacks | 727028 | fail-closed | fail-closed |
| StarterPacks | 727029 | fail-closed | fail-closed |
| StarterPacks | 727030 | fail-closed | fail-closed |
| StarterPacks | 727031 | fail-closed | fail-closed |
| StarterPacks | 727032 | fail-closed | fail-closed |
| StarterPacks | 727033 | fail-closed | fail-closed |
| StarterPacks | 727034 | fail-closed | fail-closed |
| StarterPacks | 727035 | fail-closed | fail-closed |
| StarterPacks | 727036 | fail-closed | fail-closed |
| StarterPacks | 727037 | fail-closed | fail-closed |
| StarterPacks | 727038 | fail-closed | fail-closed |
| StarterPacks | 727039 | fail-closed | fail-closed |
| StarterPacks | 727040 | fail-closed | fail-closed |
| StarterPacks | 727041 | fail-closed | fail-closed |
| StarterPacks | 727042 | fail-closed | fail-closed |
| StarterPacks | 727043 | fail-closed | fail-closed |
| StarterPacks | 727044 | fail-closed | fail-closed |
| StarterPacks | 727045 | fail-closed | fail-closed |
| StarterPacks | 727046 | fail-closed | fail-closed |
| StarterPacks | 727047 | fail-closed | fail-closed |
| StarterPacks | 727048 | fail-closed | fail-closed |
| StarterPacks | 727049 | fail-closed | fail-closed |
| StarterPacks | 727050 | fail-closed | fail-closed |
| ConquerPointBags | 720653 | fail-closed | 720653,720655-720667,720675-720678,720681-720685,720687-720689,720693-720695,729911-729912,3001133-3001136 |
| ConquerPointBags | 720655 | fail-closed | fail-closed |
| ConquerPointBags | 720656 | fail-closed | fail-closed |
| ConquerPointBags | 720657 | fail-closed | fail-closed |
| ConquerPointBags | 720658 | fail-closed | fail-closed |
| ConquerPointBags | 720659 | fail-closed | fail-closed |
| ConquerPointBags | 720660 | fail-closed | fail-closed |
| ConquerPointBags | 720661 | fail-closed | fail-closed |
| ConquerPointBags | 720662 | fail-closed | fail-closed |
| ConquerPointBags | 720663 | fail-closed | fail-closed |
| ConquerPointBags | 720664 | fail-closed | fail-closed |
| ConquerPointBags | 720665 | fail-closed | fail-closed |
| ConquerPointBags | 720666 | fail-closed | fail-closed |
| ConquerPointBags | 720667 | fail-closed | fail-closed |
| ConquerPointBags | 720675 | fail-closed | fail-closed |
| ConquerPointBags | 720676 | fail-closed | fail-closed |
| ConquerPointBags | 720677 | fail-closed | fail-closed |
| ConquerPointBags | 720678 | fail-closed | fail-closed |
| ConquerPointBags | 720681 | fail-closed | fail-closed |
| ConquerPointBags | 720682 | fail-closed | fail-closed |
| ConquerPointBags | 720683 | fail-closed | fail-closed |
| ConquerPointBags | 720684 | fail-closed | fail-closed |
| ConquerPointBags | 720685 | fail-closed | fail-closed |
| ConquerPointBags | 720687 | fail-closed | fail-closed |
| ConquerPointBags | 720688 | fail-closed | fail-closed |
| ConquerPointBags | 720689 | fail-closed | fail-closed |
| ConquerPointBags | 720693 | fail-closed | fail-closed |
| ConquerPointBags | 720694 | fail-closed | fail-closed |
| ConquerPointBags | 720695 | fail-closed | fail-closed |
| ConquerPointBags | 729911 | fail-closed | fail-closed |
| ConquerPointBags | 729912 | fail-closed | fail-closed |
| ConquerPointBags | 3001133 | fail-closed | fail-closed |
| ConquerPointBags | 3001134 | fail-closed | fail-closed |
| ConquerPointBags | 3001135 | fail-closed | fail-closed |
| ConquerPointBags | 3001136 | fail-closed | fail-closed |
| FixedOutputPacks | 720757 | fail-closed | 720757,723268,723711 |
| FixedOutputPacks | 723268 | fail-closed | fail-closed |
| FixedOutputPacks | 723711 | fail-closed | fail-closed |
| DragonBallPacks | 720028 | fail-closed | 720028,720884 |
| DragonBallPacks | 720884 | fail-closed | fail-closed |
| PrizeCenterPacks | 720836 | fail-closed | 720836,728898-728900,3004257 |
| PrizeCenterPacks | 728898 | fail-closed | fail-closed |
| PrizeCenterPacks | 728899 | fail-closed | fail-closed |
| PrizeCenterPacks | 728900 | fail-closed | fail-closed |
| PrizeCenterPacks | 3004257 | fail-closed | fail-closed |
| EasterGoldPacks | 720121 | fail-closed | 720121 |
| DailySignInPacks | 3100011 | fail-closed | 3100011,3100013-3100017 |
| DailySignInPacks | 3100013 | fail-closed | fail-closed |
| DailySignInPacks | 3100014 | fail-closed | fail-closed |
| DailySignInPacks | 3100015 | fail-closed | fail-closed |
| DailySignInPacks | 3100016 | fail-closed | fail-closed |
| DailySignInPacks | 3100017 | fail-closed | fail-closed |
| AncientStoneCombine | 3004246 | fail-closed | 3004246 |
| StarStoneCombine | 3009001 | fail-closed | 3009001 |
| AttackDefensePotions | 721623 | fail-closed | 721623,721624,721627,721628,721672,721673 |
| AttackDefensePotions | 721624 | fail-closed | fail-closed |
| AttackDefensePotions | 721627 | fail-closed | fail-closed |
| AttackDefensePotions | 721628 | fail-closed | fail-closed |
| AttackDefensePotions | 721672 | fail-closed | fail-closed |
| AttackDefensePotions | 721673 | fail-closed | fail-closed |
| ChiPointItems | 729242 | fail-closed | 729242,729481,3005126-3005129 |
| ChiPointItems | 729481 | fail-closed | fail-closed |
| ChiPointItems | 3005126 | fail-closed | fail-closed |
| ChiPointItems | 3005127 | fail-closed | fail-closed |
| ChiPointItems | 3005128 | fail-closed | fail-closed |
| ChiPointItems | 3005129 | fail-closed | fail-closed |
| ChiPointItems | 3000051 | fail-closed | fail-closed |
| ChiPointItems | 729476 | fail-closed | fail-closed |
| ChiPointItems | 729477 | fail-closed | fail-closed |
| ChiPointItems | 729478 | fail-closed | fail-closed |
| ChiPointItems | 729479 | fail-closed | fail-closed |
| SilverStudyBags | 3005117 | fail-closed | 3005117-3005125 |
| SilverStudyBags | 3005118 | fail-closed | fail-closed |
| SilverStudyBags | 3005119 | fail-closed | fail-closed |
| SilverStudyBags | 3005120 | fail-closed | fail-closed |
| SilverStudyBags | 3005121 | fail-closed | fail-closed |
| SilverStudyBags | 3005122 | fail-closed | fail-closed |
| SilverStudyBags | 3005123 | fail-closed | fail-closed |
| SilverStudyBags | 3005124 | fail-closed | fail-closed |
| SilverStudyBags | 3005125 | fail-closed | fail-closed |
| SilverPouches | 723721 | fail-closed | 723721,723723,3006594 |
| SilverPouches | 723723 | fail-closed | fail-closed |
| SilverPouches | 3006594 | fail-closed | fail-closed |
| StudyBooks | 720774 | fail-closed | 720774-720775 |
| StudyBooks | 720775 | fail-closed | fail-closed |
| ChiTokenCash | - | no-legacy-case | 729304 |
| QuestGiftPacks | - | no-legacy-case | 710212,720027,720957,721269,723714 |
| PenitenceAmulets | - | no-legacy-case | 720128,723727 |
| ElitePkPacks | - | no-legacy-case | 720714-720729 |
| EliteNestedPacks | - | no-legacy-case | 720049,720731,729626 |
| P7SoulPacks | 3004247 | independently-live | 3004247-3004248 |
| P7SoulPacks | 3004248 | independently-live | independently-live |
| ArenaPrizeConversions | 3004243 | independently-live | 3004243-3004245,3009002 |
| ArenaPrizeConversions | 3004244 | independently-live | independently-live |
| ArenaPrizeConversions | 3004245 | independently-live | independently-live |
| ArenaPrizeConversions | 3009002 | independently-live | independently-live |
| MagnoliaQuest | - | no-legacy-case | 729306-729310 |
| DailyPriceQuest | - | no-legacy-case | 729087 |
| AnniversaryQuest | - | no-legacy-case | 3001172 |
| DailySoulBall | - | no-legacy-case | 729611-729614,729703 |
| DisCityJades | - | no-legacy-case | 3008059-3008060 |
| DisCityTreasury | - | no-legacy-case | 3006982 |
| DisCityDarkHorn | - | no-legacy-case | 790001 |
| DisCityRelics | - | no-legacy-case | 723088 |
| DisCityQuest | - | no-legacy-case | 3006981,3006983-3006984 |
| DisCityPacks | - | no-legacy-case | 720399,3008057-3008058 |
| ExorcismRewards | - | no-legacy-case | 720948,729594 |
| PowerArenaPacks | 723130 | fail-closed | 723130-723135,723651-723677,723684-723686,724136-724147 |
| PowerArenaPacks | 723131 | fail-closed | fail-closed |
| PowerArenaPacks | 723132 | fail-closed | fail-closed |
| PowerArenaPacks | 723133 | fail-closed | fail-closed |
| PowerArenaPacks | 723134 | fail-closed | fail-closed |
| PowerArenaPacks | 723135 | fail-closed | fail-closed |
| PowerArenaPacks | 723651 | fail-closed | fail-closed |
| PowerArenaPacks | 723652 | fail-closed | fail-closed |
| PowerArenaPacks | 723653 | fail-closed | fail-closed |
| PowerArenaPacks | 723654 | fail-closed | fail-closed |
| PowerArenaPacks | 723655 | fail-closed | fail-closed |
| PowerArenaPacks | 723656 | fail-closed | fail-closed |
| PowerArenaPacks | 723657 | fail-closed | fail-closed |
| PowerArenaPacks | 723658 | fail-closed | fail-closed |
| PowerArenaPacks | 723659 | fail-closed | fail-closed |
| PowerArenaPacks | 723660 | fail-closed | fail-closed |
| PowerArenaPacks | 723661 | fail-closed | fail-closed |
| PowerArenaPacks | 723662 | fail-closed | fail-closed |
| PowerArenaPacks | 723663 | fail-closed | fail-closed |
| PowerArenaPacks | 723664 | fail-closed | fail-closed |
| PowerArenaPacks | 723665 | fail-closed | fail-closed |
| PowerArenaPacks | 723666 | fail-closed | fail-closed |
| PowerArenaPacks | 723667 | fail-closed | fail-closed |
| PowerArenaPacks | 723668 | fail-closed | fail-closed |
| PowerArenaPacks | 723669 | fail-closed | fail-closed |
| PowerArenaPacks | 723670 | fail-closed | fail-closed |
| PowerArenaPacks | 723671 | fail-closed | fail-closed |
| PowerArenaPacks | 723672 | fail-closed | fail-closed |
| PowerArenaPacks | 723673 | fail-closed | fail-closed |
| PowerArenaPacks | 723674 | fail-closed | fail-closed |
| PowerArenaPacks | 723675 | fail-closed | fail-closed |
| PowerArenaPacks | 723676 | fail-closed | fail-closed |
| PowerArenaPacks | 723677 | fail-closed | fail-closed |
| PowerArenaPacks | 723684 | fail-closed | fail-closed |
| PowerArenaPacks | 723685 | fail-closed | fail-closed |
| PowerArenaPacks | 723686 | fail-closed | fail-closed |
| PowerArenaPacks | 724136 | fail-closed | fail-closed |
| PowerArenaPacks | 724137 | fail-closed | fail-closed |
| PowerArenaPacks | 724138 | fail-closed | fail-closed |
| PowerArenaPacks | 724139 | fail-closed | fail-closed |
| PowerArenaPacks | 724140 | fail-closed | fail-closed |
| PowerArenaPacks | 724141 | fail-closed | fail-closed |
| PowerArenaPacks | 724142 | fail-closed | fail-closed |
| PowerArenaPacks | 724143 | fail-closed | fail-closed |
| PowerArenaPacks | 724144 | fail-closed | fail-closed |
| PowerArenaPacks | 724145 | fail-closed | fail-closed |
| PowerArenaPacks | 724146 | fail-closed | fail-closed |
| PowerArenaPacks | 724147 | fail-closed | fail-closed |
| HealingMedicine | 1000000 | fail-closed | 1000000-1000040/10,1001000-1001040/10,1002000-1002050/10,725065,725066,723726 |
| HealingMedicine | 1000010 | fail-closed | fail-closed |
| HealingMedicine | 1000020 | fail-closed | fail-closed |
| HealingMedicine | 1000030 | fail-closed | fail-closed |
| HealingMedicine | 1000040 | fail-closed | fail-closed |
| HealingMedicine | 1001000 | fail-closed | fail-closed |
| HealingMedicine | 1001010 | fail-closed | fail-closed |
| HealingMedicine | 1001020 | fail-closed | fail-closed |
| HealingMedicine | 1001030 | fail-closed | fail-closed |
| HealingMedicine | 1001040 | fail-closed | fail-closed |
| HealingMedicine | 1002000 | fail-closed | fail-closed |
| HealingMedicine | 1002010 | fail-closed | fail-closed |
| HealingMedicine | 1002020 | fail-closed | fail-closed |
| HealingMedicine | 1002030 | fail-closed | fail-closed |
| HealingMedicine | 1002040 | fail-closed | fail-closed |
| HealingMedicine | 1002050 | fail-closed | fail-closed |
| HealingMedicine | 725065 | fail-closed | fail-closed |
| HealingMedicine | 725066 | fail-closed | fail-closed |
| HealingMedicine | 723726 | fail-closed | fail-closed |
| BloodshedSeaBoosts | 3004230 | fail-closed | 3004230,3004182,3004231-3004238 |
| BloodshedSeaBoosts | 3004182 | fail-closed | fail-closed |
| BloodshedSeaBoosts | 3004231 | fail-closed | fail-closed |
| BloodshedSeaBoosts | 3004232 | fail-closed | fail-closed |
| BloodshedSeaBoosts | 3004233 | fail-closed | fail-closed |
| BloodshedSeaBoosts | 3004234 | fail-closed | fail-closed |
| BloodshedSeaBoosts | 3004235 | fail-closed | fail-closed |
| BloodshedSeaBoosts | 3004236 | fail-closed | fail-closed |
| BloodshedSeaBoosts | 3004237 | fail-closed | fail-closed |
| BloodshedSeaBoosts | 3004238 | fail-closed | fail-closed |
| HonorRefineryPacks | 723679 | fail-closed | 723679,723680,723682,723683,723691,723692,724149,724150,724193,724194,724196,724197,724199,724200,724202,724203,724205,724206,724208,724209,724211,724212,725161,725162,725164,725165,725167,725168,725170,725171 |
| HonorRefineryPacks | 723680 | fail-closed | fail-closed |
| HonorRefineryPacks | 723682 | fail-closed | fail-closed |
| HonorRefineryPacks | 723683 | fail-closed | fail-closed |
| HonorRefineryPacks | 723691 | fail-closed | fail-closed |
| HonorRefineryPacks | 723692 | fail-closed | fail-closed |
| HonorRefineryPacks | 724149 | fail-closed | fail-closed |
| HonorRefineryPacks | 724150 | fail-closed | fail-closed |
| HonorRefineryPacks | 724193 | fail-closed | fail-closed |
| HonorRefineryPacks | 724194 | fail-closed | fail-closed |
| HonorRefineryPacks | 724196 | fail-closed | fail-closed |
| HonorRefineryPacks | 724197 | fail-closed | fail-closed |
| HonorRefineryPacks | 724199 | fail-closed | fail-closed |
| HonorRefineryPacks | 724200 | fail-closed | fail-closed |
| HonorRefineryPacks | 724202 | fail-closed | fail-closed |
| HonorRefineryPacks | 724203 | fail-closed | fail-closed |
| HonorRefineryPacks | 724205 | fail-closed | fail-closed |
| HonorRefineryPacks | 724206 | fail-closed | fail-closed |
| HonorRefineryPacks | 724208 | fail-closed | fail-closed |
| HonorRefineryPacks | 724209 | fail-closed | fail-closed |
| HonorRefineryPacks | 724211 | fail-closed | fail-closed |
| HonorRefineryPacks | 724212 | fail-closed | fail-closed |
| HonorRefineryPacks | 725161 | fail-closed | fail-closed |
| HonorRefineryPacks | 725162 | fail-closed | fail-closed |
| HonorRefineryPacks | 725164 | fail-closed | fail-closed |
| HonorRefineryPacks | 725165 | fail-closed | fail-closed |
| HonorRefineryPacks | 725167 | fail-closed | fail-closed |
| HonorRefineryPacks | 725168 | fail-closed | fail-closed |
| HonorRefineryPacks | 725170 | fail-closed | fail-closed |
| HonorRefineryPacks | 725171 | fail-closed | fail-closed |
| GainRefineryPacks | 724131 | fail-closed | 724131,724132,724134,724135,724152,724153,724155,724156,724158,724159,724161,724162,724164,724165,724167,724168,724170,724171,724173,724174,724176,724177,724179,724180,724182,724183,724185,724186,724191,724192 |
| GainRefineryPacks | 724132 | fail-closed | fail-closed |
| GainRefineryPacks | 724134 | fail-closed | fail-closed |
| GainRefineryPacks | 724135 | fail-closed | fail-closed |
| GainRefineryPacks | 724152 | fail-closed | fail-closed |
| GainRefineryPacks | 724153 | fail-closed | fail-closed |
| GainRefineryPacks | 724155 | fail-closed | fail-closed |
| GainRefineryPacks | 724156 | fail-closed | fail-closed |
| GainRefineryPacks | 724158 | fail-closed | fail-closed |
| GainRefineryPacks | 724159 | fail-closed | fail-closed |
| GainRefineryPacks | 724161 | fail-closed | fail-closed |
| GainRefineryPacks | 724162 | fail-closed | fail-closed |
| GainRefineryPacks | 724164 | fail-closed | fail-closed |
| GainRefineryPacks | 724165 | fail-closed | fail-closed |
| GainRefineryPacks | 724167 | fail-closed | fail-closed |
| GainRefineryPacks | 724168 | fail-closed | fail-closed |
| GainRefineryPacks | 724170 | fail-closed | fail-closed |
| GainRefineryPacks | 724171 | fail-closed | fail-closed |
| GainRefineryPacks | 724173 | fail-closed | fail-closed |
| GainRefineryPacks | 724174 | fail-closed | fail-closed |
| GainRefineryPacks | 724176 | fail-closed | fail-closed |
| GainRefineryPacks | 724177 | fail-closed | fail-closed |
| GainRefineryPacks | 724179 | fail-closed | fail-closed |
| GainRefineryPacks | 724180 | fail-closed | fail-closed |
| GainRefineryPacks | 724182 | fail-closed | fail-closed |
| GainRefineryPacks | 724183 | fail-closed | fail-closed |
| GainRefineryPacks | 724185 | fail-closed | fail-closed |
| GainRefineryPacks | 724186 | fail-closed | fail-closed |
| GainRefineryPacks | 724191 | fail-closed | fail-closed |
| GainRefineryPacks | 724192 | fail-closed | fail-closed |
| HossuHairPacks | 3006319 | fail-closed | 3006319 |
| RedRunePacks | 3306369 | fail-closed | 3306369 |
| RedRunePacks | 3306509 | fail-closed | 3306509 |
| YellowBlueRunePacks | 3306370 | fail-closed | 3306370 |
| YellowBlueRunePacks | 3306371 | fail-closed | 3306371 |
| YellowBlueRunePacks | 3306510 | fail-closed | 3306510 |
| YellowBlueRunePacks | 3306511 | fail-closed | 3306511 |
| PlusOneStonePacks | 723712 | fail-closed | 723712 |
| HigherStonePacks | 729022 | fail-closed | 729022 |
| HigherStonePacks | 729023 | fail-closed | 729023 |
| GemDragonSoulPacks | 3005130 | fail-closed | refined gem pack |
| GemDragonSoulPacks | 3005132 | fail-closed | super gem pack |
| GemDragonSoulPacks | 3005133 | fail-closed | purification level 4 |
| GemDragonSoulPacks | 3005135 | fail-closed | purification level 6 |
| SummerCrafting | - | no-legacy-case | 3303704-3303707,3303764-3303765,3303772-3303776,3303797-3303801 |
| LabyrinthItems | - | no-legacy-case | 721540 |
## Exhaustive top-level case table

| ID | Kind | Effects |
| ---: | --- | --- |
| 720010 | live-split-write | source+items |
| 720011 | live-split-write | source+items |
| 720012 | live-split-write | source+items |
| 720013 | live-split-write | source+items |
| 720014 | live-split-write | source+items |
| 720015 | live-split-write | source+items |
| 720016 | live-split-write | source+items |
| 720017 | live-split-write | source+items |
| 720020 | other |  |
| 720028 | owned-fail-closed |  |
| 720030 | single-consume | source |
| 720031 | single-consume | source |
| 720032 | single-consume | source |
| 720121 | owned-fail-closed |  |
| 720598 | other |  |
| 720599 | other |  |
| 720650 | single-consume | source |
| 720651 | single-consume | source |
| 720652 | single-consume | source |
| 720653 | owned-fail-closed |  |
| 720654 | live-split-write | source+rvp |
| 720655 | owned-fail-closed |  |
| 720656 | owned-fail-closed |  |
| 720657 | owned-fail-closed |  |
| 720658 | owned-fail-closed |  |
| 720659 | owned-fail-closed |  |
| 720660 | owned-fail-closed |  |
| 720661 | owned-fail-closed |  |
| 720662 | owned-fail-closed |  |
| 720663 | owned-fail-closed |  |
| 720664 | owned-fail-closed |  |
| 720665 | owned-fail-closed |  |
| 720666 | owned-fail-closed |  |
| 720667 | owned-fail-closed |  |
| 720671 | single-consume | source |
| 720672 | single-consume | source |
| 720673 | single-consume | source |
| 720675 | owned-fail-closed |  |
| 720676 | owned-fail-closed |  |
| 720677 | owned-fail-closed |  |
| 720678 | owned-fail-closed |  |
| 720681 | owned-fail-closed |  |
| 720682 | owned-fail-closed |  |
| 720683 | owned-fail-closed |  |
| 720684 | owned-fail-closed |  |
| 720685 | owned-fail-closed |  |
| 720687 | owned-fail-closed |  |
| 720688 | owned-fail-closed |  |
| 720689 | owned-fail-closed |  |
| 720693 | owned-fail-closed |  |
| 720694 | owned-fail-closed |  |
| 720695 | owned-fail-closed |  |
| 720757 | owned-fail-closed |  |
| 720774 | owned-fail-closed |  |
| 720775 | owned-fail-closed |  |
| 720793 | owned-fail-closed | timer |
| 720828 | other |  |
| 720836 | owned-fail-closed |  |
| 720884 | owned-fail-closed |  |
| 720981 | owned-fail-closed | timer |
| 721157 | single-consume | source |
| 721158 | single-consume | source |
| 721222 | live-split-write | source+items |
| 721223 | live-split-write | source+items |
| 721224 | live-split-write | source+items |
| 721225 | live-split-write | source+items |
| 721226 | live-split-write | source+items |
| 721227 | live-split-write | source+items |
| 721228 | live-split-write | source+items |
| 721229 | live-split-write | source+items |
| 721230 | live-split-write | source+items |
| 721231 | live-split-write | source+items |
| 721232 | live-split-write | source+items |
| 721233 | live-split-write | source+items |
| 721234 | live-split-write | source+items |
| 721235 | live-split-write | source+items |
| 721236 | live-split-write | source+items |
| 721237 | live-split-write | source+items |
| 721238 | live-split-write | source+items |
| 721239 | live-split-write | source+items |
| 721240 | live-split-write | source+items |
| 721241 | live-split-write | source+items |
| 721242 | live-split-write | source+items |
| 721243 | live-split-write | source+items |
| 721244 | live-split-write | source+items |
| 721245 | live-split-write | source+items |
| 721261 | single-consume | source |
| 721330 | live-split-write | source+items |
| 721507 | live-split-write | source+items |
| 721508 | live-split-write | source+items |
| 721509 | live-split-write | source+items |
| 721623 | owned-fail-closed | source+timer |
| 721624 | owned-fail-closed | source+timer |
| 721627 | owned-fail-closed | source+timer |
| 721628 | owned-fail-closed | source+timer |
| 721672 | owned-fail-closed | source+timer |
| 721673 | owned-fail-closed | source+timer |
| 721853 | single-consume | source |
| 722150 | single-consume | source |
| 722178 | live-split-write | source+cp |
| 722450 | single-consume | source |
| 723130 | owned-fail-closed | source+items |
| 723131 | owned-fail-closed | source+items |
| 723132 | owned-fail-closed | source+items |
| 723133 | owned-fail-closed | source+items |
| 723134 | owned-fail-closed | source+items |
| 723135 | owned-fail-closed | source+items |
| 723268 | owned-fail-closed |  |
| 723340 | other |  |
| 723341 | other |  |
| 723342 | other |  |
| 723583 | single-consume | source |
| 723651 | owned-fail-closed | source+items |
| 723652 | owned-fail-closed | source+items |
| 723653 | owned-fail-closed | source+items |
| 723654 | owned-fail-closed | source+items |
| 723655 | owned-fail-closed | source+items |
| 723656 | owned-fail-closed | source+items |
| 723657 | owned-fail-closed | source+items |
| 723658 | owned-fail-closed | source+items |
| 723659 | owned-fail-closed | source+items |
| 723660 | owned-fail-closed | source+items |
| 723661 | owned-fail-closed | source+items |
| 723662 | owned-fail-closed | source+items |
| 723663 | owned-fail-closed | source+items |
| 723664 | owned-fail-closed | source+items |
| 723665 | owned-fail-closed | source+items |
| 723666 | owned-fail-closed | source+items |
| 723667 | owned-fail-closed | source+items |
| 723668 | owned-fail-closed | source+items |
| 723669 | owned-fail-closed | source+items |
| 723670 | owned-fail-closed | source+items |
| 723671 | owned-fail-closed | source+items |
| 723672 | owned-fail-closed | source+items |
| 723673 | owned-fail-closed | source+items |
| 723674 | owned-fail-closed | source+items |
| 723675 | owned-fail-closed | source+items |
| 723676 | owned-fail-closed | source+items |
| 723677 | owned-fail-closed | source+items |
| 723678 | live-split-write | source+items |
| 723679 | owned-fail-closed | source+items |
| 723680 | owned-fail-closed | source+items |
| 723681 | live-split-write | source+items |
| 723682 | owned-fail-closed | source+items |
| 723683 | owned-fail-closed | source+items |
| 723684 | owned-fail-closed | source+items |
| 723685 | owned-fail-closed | source+items |
| 723686 | owned-fail-closed | source+items |
| 723690 | live-split-write | source+items |
| 723691 | owned-fail-closed | source+items |
| 723692 | owned-fail-closed | source+items |
| 723693 | live-split-write | source+items |
| 723711 | owned-fail-closed | source+items |
| 723712 | owned-fail-closed | source+items |
| 723721 | owned-fail-closed |  |
| 723722 | live-split-write | source+silver |
| 723723 | owned-fail-closed |  |
| 723724 | live-split-write | source+timer |
| 723725 | live-split-write | source+items |
| 723726 | owned-fail-closed | source+timer |
| 723753 | owned-fail-closed |  |
| 723755 | owned-fail-closed |  |
| 723767 | owned-fail-closed |  |
| 723772 | owned-fail-closed |  |
| 723774 | owned-fail-closed |  |
| 723776 | owned-fail-closed |  |
| 723855 | live-split-write | source+items |
| 723856 | live-split-write | source+items |
| 723859 | live-split-write | source+items |
| 723860 | live-split-write | source+items |
| 723861 | live-split-write | source+items |
| 723862 | live-split-write | source+items |
| 723863 | live-split-write | source+items |
| 723864 | live-split-write | source+items |
| 723865 | live-split-write | source+items |
| 723900 | live-split-write | source+items |
| 723901 | live-split-write | source+items |
| 723902 | live-split-write | source+items |
| 724002 | live-split-write | source+items |
| 724130 | other |  |
| 724131 | owned-fail-closed | source+items |
| 724132 | owned-fail-closed | source+items |
| 724133 | other |  |
| 724134 | owned-fail-closed | source+items |
| 724135 | owned-fail-closed | source+items |
| 724136 | owned-fail-closed | source+items |
| 724137 | owned-fail-closed | source+items |
| 724138 | owned-fail-closed | source+items |
| 724139 | owned-fail-closed | source+items |
| 724140 | owned-fail-closed | source+items |
| 724141 | owned-fail-closed | source+items |
| 724142 | owned-fail-closed | source+items |
| 724143 | owned-fail-closed | source+items |
| 724144 | owned-fail-closed | source+items |
| 724145 | owned-fail-closed | source+items |
| 724146 | owned-fail-closed | source+items |
| 724147 | owned-fail-closed | source+items |
| 724148 | live-split-write | source+items |
| 724149 | owned-fail-closed | source+items |
| 724150 | owned-fail-closed | source+items |
| 724151 | other |  |
| 724152 | owned-fail-closed | source+items |
| 724153 | owned-fail-closed | source+items |
| 724154 | other |  |
| 724155 | owned-fail-closed | source+items |
| 724156 | owned-fail-closed | source+items |
| 724157 | other |  |
| 724158 | owned-fail-closed | source+items |
| 724159 | owned-fail-closed | source+items |
| 724160 | other |  |
| 724161 | owned-fail-closed | source+items |
| 724162 | owned-fail-closed | source+items |
| 724163 | other |  |
| 724164 | owned-fail-closed | source+items |
| 724165 | owned-fail-closed | source+items |
| 724166 | other |  |
| 724167 | owned-fail-closed | source+items |
| 724168 | owned-fail-closed | source+items |
| 724169 | other |  |
| 724170 | owned-fail-closed | source+items |
| 724171 | owned-fail-closed | source+items |
| 724172 | other |  |
| 724173 | owned-fail-closed | source+items |
| 724174 | owned-fail-closed | source+items |
| 724175 | other |  |
| 724176 | owned-fail-closed | source+items |
| 724177 | owned-fail-closed | source+items |
| 724178 | other |  |
| 724179 | owned-fail-closed | source+items |
| 724180 | owned-fail-closed | source+items |
| 724181 | other |  |
| 724182 | owned-fail-closed | source+items |
| 724183 | owned-fail-closed | source+items |
| 724184 | other |  |
| 724185 | owned-fail-closed | source+items |
| 724186 | owned-fail-closed | source+items |
| 724190 | other |  |
| 724191 | owned-fail-closed | source+items |
| 724192 | owned-fail-closed | source+items |
| 724193 | owned-fail-closed | source+items |
| 724194 | owned-fail-closed | source+items |
| 724195 | live-split-write | source+items |
| 724196 | owned-fail-closed | source+items |
| 724197 | owned-fail-closed | source+items |
| 724198 | live-split-write | source+items |
| 724199 | owned-fail-closed | source+items |
| 724200 | owned-fail-closed | source+items |
| 724201 | live-split-write | source+items |
| 724202 | owned-fail-closed | source+items |
| 724203 | owned-fail-closed | source+items |
| 724204 | live-split-write | source+items |
| 724205 | owned-fail-closed | source+items |
| 724206 | owned-fail-closed | source+items |
| 724207 | live-split-write | source+items |
| 724208 | owned-fail-closed | source+items |
| 724209 | owned-fail-closed | source+items |
| 724210 | live-split-write | source+items |
| 724211 | owned-fail-closed | source+items |
| 724212 | owned-fail-closed | source+items |
| 724215 | live-split-write | source+items |
| 725000 | single-consume | source |
| 725001 | single-consume | source |
| 725002 | single-consume | source |
| 725003 | single-consume | source |
| 725004 | single-consume | source |
| 725015 | single-consume | source |
| 725016 | single-consume | source |
| 725018 | single-consume | source |
| 725019 | single-consume | source |
| 725020 | single-consume | source |
| 725021 | single-consume | source |
| 725022 | single-consume | source |
| 725023 | single-consume | source |
| 725024 | single-consume | source |
| 725025 | single-consume | source |
| 725028 | single-consume | source |
| 725055 | other |  |
| 725056 | other |  |
| 725057 | other |  |
| 725058 | other |  |
| 725065 | owned-fail-closed | source+timer |
| 725066 | owned-fail-closed | source+timer |
| 725067 | live-split-write | source+items |
| 725068 | live-split-write | source+items |
| 725160 | live-split-write | source+items |
| 725161 | owned-fail-closed | source+items |
| 725162 | owned-fail-closed | source+items |
| 725163 | live-split-write | source+items |
| 725164 | owned-fail-closed | source+items |
| 725165 | owned-fail-closed | source+items |
| 725166 | live-split-write | source+items |
| 725167 | owned-fail-closed | source+items |
| 725168 | owned-fail-closed | source+items |
| 725169 | live-split-write | source+items |
| 725170 | owned-fail-closed | source+items |
| 725171 | owned-fail-closed | source+items |
| 727000 | live-split-write | source+items |
| 727026 | owned-fail-closed |  |
| 727027 | owned-fail-closed |  |
| 727028 | owned-fail-closed |  |
| 727029 | owned-fail-closed |  |
| 727030 | owned-fail-closed |  |
| 727031 | owned-fail-closed |  |
| 727032 | owned-fail-closed |  |
| 727033 | owned-fail-closed |  |
| 727034 | owned-fail-closed |  |
| 727035 | owned-fail-closed |  |
| 727036 | owned-fail-closed |  |
| 727037 | owned-fail-closed |  |
| 727038 | owned-fail-closed |  |
| 727039 | owned-fail-closed |  |
| 727040 | owned-fail-closed |  |
| 727041 | owned-fail-closed |  |
| 727042 | owned-fail-closed |  |
| 727043 | owned-fail-closed |  |
| 727044 | owned-fail-closed |  |
| 727045 | owned-fail-closed |  |
| 727046 | owned-fail-closed |  |
| 727047 | owned-fail-closed |  |
| 727048 | owned-fail-closed |  |
| 727049 | owned-fail-closed |  |
| 727050 | owned-fail-closed |  |
| 727991 | live-split-write | source+items |
| 728201 | owned-fail-closed | timer |
| 728204 | live-split-write | source+items |
| 728305 | live-split-write | source+items |
| 728525 | other |  |
| 728526 | other |  |
| 728527 | other |  |
| 728883 | other |  |
| 728898 | owned-fail-closed |  |
| 728899 | owned-fail-closed |  |
| 728900 | owned-fail-closed |  |
| 729022 | owned-fail-closed | source+items |
| 729023 | owned-fail-closed | source+items |
| 729122 | live-split-write | source+items |
| 729242 | owned-fail-closed |  |
| 729476 | owned-fail-closed | source+chi |
| 729477 | owned-fail-closed | source+chi |
| 729478 | owned-fail-closed | source+chi |
| 729479 | owned-fail-closed | source+chi |
| 729481 | owned-fail-closed |  |
| 729793 | live-split-write | source+items |
| 729911 | owned-fail-closed |  |
| 729912 | owned-fail-closed |  |
| 1000000 | owned-fail-closed | source+timer |
| 1000010 | owned-fail-closed | source+timer |
| 1000020 | owned-fail-closed | source+timer |
| 1000030 | owned-fail-closed | source+timer |
| 1000040 | owned-fail-closed | source+timer |
| 1001000 | owned-fail-closed | source+timer |
| 1001010 | owned-fail-closed | source+timer |
| 1001020 | owned-fail-closed | source+timer |
| 1001030 | owned-fail-closed | source+timer |
| 1001040 | owned-fail-closed | source+timer |
| 1002000 | owned-fail-closed | source+timer |
| 1002010 | owned-fail-closed | source+timer |
| 1002020 | owned-fail-closed | source+timer |
| 1002030 | owned-fail-closed | source+timer |
| 1002040 | owned-fail-closed | source+timer |
| 1002050 | owned-fail-closed | source+timer |
| 1060020 | single-consume | source |
| 1060021 | single-consume | source |
| 1060022 | single-consume | source |
| 1060023 | single-consume | source |
| 1060024 | single-consume | source |
| 1060039 | single-consume | source |
| 1060100 | single-consume | source |
| 1060101 | single-consume | source |
| 3000051 | owned-fail-closed | source+chi |
| 3000254 | live-split-write | source+items |
| 3000256 | live-split-write | source+items |
| 3000257 | live-split-write | source+items |
| 3000258 | live-split-write | source+items |
| 3000259 | live-split-write | source+items |
| 3000260 | live-split-write | source+items |
| 3000261 | live-split-write | source+items |
| 3000262 | live-split-write | source+items |
| 3000263 | live-split-write | source+items |
| 3000264 | live-split-write | source+items |
| 3000265 | live-split-write | source+items |
| 3000266 | live-split-write | source+items |
| 3000267 | live-split-write | source+items |
| 3000268 | live-split-write | source+items |
| 3000269 | live-split-write | source+items |
| 3000270 | live-split-write | source+items |
| 3000271 | live-split-write | source+items |
| 3000277 | live-split-write | source+items |
| 3000278 | live-split-write | source+items |
| 3000279 | live-split-write | source+items |
| 3000280 | live-split-write | source+items |
| 3000281 | live-split-write | source+items |
| 3000678 | live-split-write | source+items |
| 3000787 | single-consume | source |
| 3000788 | other |  |
| 3000794 | live-split-write | source+timer |
| 3001119 | live-split-write | source+items |
| 3001133 | owned-fail-closed |  |
| 3001134 | owned-fail-closed |  |
| 3001135 | owned-fail-closed |  |
| 3001136 | owned-fail-closed |  |
| 3001264 | live-split-write | source+items |
| 3001266 | live-split-write | source+silver |
| 3001267 | live-split-write | source+items |
| 3001280 | live-split-write | source+items |
| 3001405 | live-split-write | source+items |
| 3001523 | single-consume | source |
| 3001607 | single-consume | source |
| 3002131 | live-split-write | source+items |
| 3002559 | owned-fail-closed |  |
| 3002560 | owned-fail-closed |  |
| 3002747 | live-split-write | source+items |
| 3002748 | live-split-write | source+items |
| 3002749 | live-split-write | source+items |
| 3002848 | live-split-write | source+items |
| 3002849 | live-split-write | source+items |
| 3002850 | live-split-write | source+items |
| 3002851 | live-split-write | source+items |
| 3002852 | live-split-write | source+items |
| 3002853 | live-split-write | source+items |
| 3002854 | live-split-write | source+items |
| 3002855 | live-split-write | source+items |
| 3002856 | live-split-write | source+items |
| 3002857 | live-split-write | source+items |
| 3003227 | single-consume | source |
| 3003228 | single-consume | source |
| 3003229 | single-consume | source |
| 3003230 | single-consume | source |
| 3003231 | single-consume | source |
| 3003232 | single-consume | source |
| 3003311 | single-consume | source |
| 3003312 | single-consume | source |
| 3003340 | live-split-write | source+items |
| 3003498 | single-consume | source |
| 3003650 | single-consume | source |
| 3004085 | live-split-write | source+cp |
| 3004121 | live-split-write | source+timer |
| 3004122 | live-split-write | source+timer |
| 3004123 | live-split-write | source+timer |
| 3004124 | live-split-write | source+timer |
| 3004182 | owned-fail-closed | source+timer |
| 3004230 | owned-fail-closed | source+timer |
| 3004231 | owned-fail-closed | source+timer |
| 3004232 | owned-fail-closed | source+timer |
| 3004233 | owned-fail-closed | source+timer |
| 3004234 | owned-fail-closed | source+timer |
| 3004235 | owned-fail-closed | source+timer |
| 3004236 | owned-fail-closed | source+timer |
| 3004237 | owned-fail-closed | source+timer |
| 3004238 | owned-fail-closed | source+timer |
| 3004243 | other |  |
| 3004244 | other |  |
| 3004245 | other |  |
| 3004246 | owned-fail-closed | source+items |
| 3004247 | other |  |
| 3004248 | other |  |
| 3004249 | live-split-write | source+items |
| 3004257 | owned-fail-closed |  |
| 3004259 | other |  |
| 3004296 | single-consume | source |
| 3004349 | live-split-write | source+items |
| 3004350 | live-split-write | source+items |
| 3004351 | live-split-write | source+items |
| 3005117 | owned-fail-closed |  |
| 3005118 | owned-fail-closed |  |
| 3005119 | owned-fail-closed |  |
| 3005120 | owned-fail-closed |  |
| 3005121 | owned-fail-closed |  |
| 3005122 | owned-fail-closed |  |
| 3005123 | owned-fail-closed |  |
| 3005124 | owned-fail-closed |  |
| 3005125 | owned-fail-closed |  |
| 3005126 | owned-fail-closed |  |
| 3005127 | owned-fail-closed |  |
| 3005128 | owned-fail-closed |  |
| 3005129 | owned-fail-closed |  |
| 3005130 | owned-fail-closed | source+items |
| 3005131 | other |  |
| 3005132 | owned-fail-closed | source+items |
| 3005133 | owned-fail-closed | source+items |
| 3005135 | owned-fail-closed | source+items |
| 3005368 | live-split-write | source+items+cp |
| 3005891 | live-split-write | source+items |
| 3005892 | live-split-write | source+items |
| 3005894 | live-split-write | source+items |
| 3006016 | live-split-write | source+items |
| 3006226 | live-split-write | source+items |
| 3006319 | owned-fail-closed | source+items |
| 3006347 | other |  |
| 3006594 | owned-fail-closed |  |
| 3006700 | live-split-write | source+items |
| 3007108 | other |  |
| 3007109 | other |  |
| 3007110 | other |  |
| 3007365 | live-split-write | source+silver |
| 3007366 | live-split-write | source+silver |
| 3007367 | live-split-write | source+silver |
| 3007368 | live-split-write | source+silver |
| 3007985 | owned-fail-closed | timer |
| 3007989 | owned-fail-closed | timer |
| 3007992 | owned-fail-closed | timer |
| 3009001 | owned-fail-closed |  |
| 3009002 | other |  |
| 3100011 | owned-fail-closed | source+items+silver+cp+rvp |
| 3100013 | owned-fail-closed | source+items+silver+cp |
| 3100014 | owned-fail-closed | source+items+boundcps |
| 3100015 | owned-fail-closed | source+items |
| 3100016 | owned-fail-closed | source+items+silver+cp+rvp+boundcps |
| 3100017 | owned-fail-closed | source+items |
| 3300809 | live-split-write | source+items |
| 3306369 | owned-fail-closed | source+items |
| 3306370 | owned-fail-closed | source+items |
| 3306371 | owned-fail-closed | source+items |
| 3306509 | owned-fail-closed | source+items |
| 3306510 | owned-fail-closed | source+items |
| 3306511 | owned-fail-closed | source+items |
| 3307451 | live-split-write | source+items |
| 3600023 | single-consume | source |
