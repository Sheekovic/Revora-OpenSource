using System;

namespace Revora
{
    public static class Config
    {
        // =============================================================================
        // OPERATOR PANEL — drop rate, leveling, quest prizes, event prizes
        // =============================================================================
        // HOW TO APPLY A CHANGE
        //   1. Edit a field below.
        //   2. Rebuild Debug|x86.
        //   3. Restart the game host. A running Revora.exe keeps the old numbers.
        //
        // DIFFICULTY (these defaults)
        //   Official Conquer is slow: weeks to 120, almost no CPs from kills, tiny
        //   daily quests. Typical private servers are instant: 50x EXP, 50,000 CPs
        //   per daily, 500 CPs on almost every kill.
        //   These numbers sit in the middle: a few days of regular hunting to 120,
        //   dailies pay a few thousand CPs, events pay more than quests, kills
        //   trickle a little gold and the monster's own item — not a CP printer.
        //
        // DATABASE VS THIS FILE
        //   UseDatabaseRates = false means THIS file owns EXP / spell / proficiency
        //   / gold-drop chance. The configuration table is ignored for those five.
        //   UseDatabasePrizes = false means THIS file owns PremioEventos prizes
        //   (Guild War, PK tournaments). The premioeventos table is ignored.
        //   Historical SQL column swap (only if you turn UseDatabaseRates back on):
        //     configuration.ExperienceRate            -> ExperienceRate
        //     configuration.ProficiencyExperienceRate -> SpellExperienceRate
        //     configuration.SpellExperienceRate       -> ProficiencyExperienceRate
        //
        // WHAT THIS FILE DOES NOT CONTROL
        //   Donation / TQ-point / VIP shops (million-CP cashouts). Lottery.
        //   Player-dropped inventory. Anwen's bound starter kit (except gold).
        //   First/second rebirth item hunts (they pay items, not CPs).
        // =============================================================================

        // --- SQL gates ---
        // false = ignore configuration.ExperienceRate and friends; use the numbers here.
        public static bool UseDatabaseRates = false;
        // false = ignore premioeventos table; use the Guild War / PK tournament numbers here.
        public static bool UseDatabasePrizes = false;

        // =============================================================================
        // LEVELING
        // =============================================================================
        // Kill EXP for a normal (non-VIP) player is roughly:
        //   monster HP * ExperienceRate * ExperienceRateMultiplier
        //   * DailyExperienceMultiplier (* Double EXP) (* Heaven Blessing) (* BP) (/ 2nd reborn)
        // With the defaults that is about 8x official. VIP 8+ is 16x official.
        // Private servers often run 20x–100x plus a free daily x2. Official is 1x
        // and no free daily. 8x keeps hunting moving without a steamroll.

        public static uint ExperienceRate = 8;
        public static uint SpellExperienceRate = 3;
        public static uint ProficiencyExperienceRate = 3;
        // Extra multiply on TOP of the base rates. Leave at 1 unless you want a
        // short event (2 = double this weekend) without touching ExperienceRate.
        public static uint ExperienceRateMultiplier = 1;
        public static uint SpellExperienceRateMultiplier = 1;
        public static uint ProficiencyExperienceRateMultiplier = 1;

        public static byte LevelCap = 140;
        public static ushort AttributePointsPerLevel = 3;
        // Official had no free everyday x2. Set 2 to restore the old private-server daily.
        public static uint DailyExperienceMultiplier = 1;
        // Official VIP did not multiply kill EXP. 2 at VIP 8 is a perk, not a steamroll.
        public static uint VipExperienceMultiplier = 2;
        public static byte VipExperienceMinLevel = 8;
        public static uint DoubleExperienceMultiplier = 2;
        public static uint HeavenBlessingExperiencePercent = 20;
        public static uint SecondRebornExperienceDivisor = 2;
        // Map 1039 training ground: official used it for skills, not character EXP.
        public static bool TrainingGroundBlocksExperience = true;
        public static uint TrainingGroundExperienceDivisor = 50;
        public static uint TrainingGroundSpellExperienceDivisor = 40;
        public static uint TrainingGroundProficiencyExperienceDivisor = 40;
        public static uint AutoHuntExperienceDivisor = 2;
        public static uint GuildExperiencePercentPerLevel = 1;
        public static uint BattlePowerExperiencePercent = 100;
        public static uint VipSpellExperienceMultiplier = 3;
        public static uint VipProficiencyExperienceMultiplier = 3;
        public static byte ProficiencyLevelCap = 20;

        // =============================================================================
        // MONSTER DROPS
        // =============================================================================
        // Item roll: chance = OwnItemRate * ItemDropRateMultiplier + ItemDropRateBonus
        // clamped 0..100. OwnItemRate comes from monsterinfos.ownitemrate.
        // Official is multiplier 1. Private servers often use 10–50. 3 is the middle.

        public static uint ItemDropRateMultiplier = 3;
        public static int ItemDropRateBonus = 0;
        // Revora wilderness +1 common equipment: 100 / 10,000 = 1% per eligible kill.
        public static int MonsterPlusEquipmentBasisPoints = 100;
        // Revora Missing Key policy: 1% on Frozen Grotto floors three/four; original rate is undocumented.
        public static int GrottoKeyBasisPoints = 100;
        // Revora quest policy: Waterlord has a 25% Clean Water chance per death.
        public static int CleanWaterBasisPoints = 2500;
        public static int MeteorDropBasisPoints = 100; // Revora ordinary wilderness drop: 1%.
        // Revora policy: one-percent normal-gem branch per eligible two-second mining tick.
        public static int MiningNormalGemBasisPoints = 100;

        // Floor gold piles. Official monsters drop small silver. This source had gold
        // commented out. Mid-ground: on, 20% chance, 2x the monster-level pile.
        public static bool MoneyDropEnabled = true;
        public static uint MoneyDropRate = 20;
        public static uint MoneyDropMultiple = 2;

        // Direct CPs on kill. Independent rolls stacked on each kill.
        // 1 CP always, then extra 5 / 50 / 250 at the chances below.
        public static bool KillConquerPointsEnabled = true;
        public static uint KillConquerPointsAlways = 1;
        public static int KillConquerPointsChance5 = 50;
        public static uint KillConquerPointsAmount5 = 5;
        public static int KillConquerPointsChance50 = 3;
        public static uint KillConquerPointsAmount50 = 50;
        public static int KillConquerPointsChance250 = 1;
        public static uint KillConquerPointsAmount250 = 250;
        public static byte KillConquerPointsMinVip = 0;

        // Bird Island Birdman family (Soul Trial). Raw attack 731-810 one-shots
        // a level-74 Taoist with +6 kit (~700 HP). Percent of table attack.
        public static uint BirdmanAttackPercent = 20;

        // VIP random gold on kill (40% for 1–3,000,000). That is private-server easy.
        // Off: gold comes from MoneyDrop piles instead.
        public static bool VipKillGoldEnabled = false;

        // Hour of Hunt leftover in Drop() (6, 12, 18, 23). Event is parked; keep the
        // kill bonus off unless you reopen that hour.
        public static bool CrazyHourEnabled = false;

        // Map 2072 Gold War kill gold. Event is parked.
        public static bool GoldWarKillGoldEnabled = false;

        // Named-boss extras. Old values were 2,000,000 / 100,000 / 200,000,000 CPs.
        public static uint DrunkWatchmanCp = 400;
        public static uint NamedMonsterKillCp = 150;
        public static uint NamedMonsterKillRevoraPoints = 2;
        public static uint SoulDevilCp = 2500;
        public static ulong SoulDevilGold = 500000;
        public static uint SoulDevilRevoraPoints = 25;

        // =============================================================================
        // LIVE EVENT PRIZES
        // =============================================================================
        // Last Man Standing runs 12 times a day, so the per-win purse is modest.
        // Old: 20,000 CPs / 2,000,000 gold / 50 Revora Points.

        public static uint LastManStandingCp = 8000;
        public static ulong LastManStandingGold = 750000;
        public static uint LastManStandingRevoraPoints = 25;

        // RelicRush: odd hours (skips 15:00 NobleWar). Hold the relic to score. Top three claim.

        public static uint RelicRushFirstCp = 10000;
        public static ulong RelicRushFirstGold = 1000000;
        public static uint RelicRushFirstRevoraPoints = 30;
        public static uint RelicRushSecondCp = 5000;
        public static ulong RelicRushSecondGold = 400000;
        public static uint RelicRushSecondRevoraPoints = 15;
        public static uint RelicRushThirdCp = 2500;
        public static ulong RelicRushThirdGold = 150000;
        public static uint RelicRushThirdRevoraPoints = 8;

        // Wave Defense: even hours :30. Team of 2-4 holds three posts through 8 waves.

        public static uint WaveDefenseWinCp = 12000;
        public static ulong WaveDefenseWinGold = 1200000;
        public static uint WaveDefenseWinRevoraPoints = 40;
        public static uint WaveDefenseConsolationCp = 3000;
        public static ulong WaveDefenseConsolationGold = 250000;
        public static uint WaveDefenseConsolationRevoraPoints = 10;

        // Beast Trial: odd hours :30. Solo PVE, private map, 8 kill-waves. Heals on.

        public static uint BeastTrialWinCp = 8000;
        public static ulong BeastTrialWinGold = 700000;
        public static uint BeastTrialWinRevoraPoints = 22;
        public static uint BeastTrialConsolationCp = 2000;
        public static ulong BeastTrialConsolationGold = 150000;
        public static uint BeastTrialConsolationRevoraPoints = 6;

        // Boss Rush: every hour :45. Solo PVE, five world bosses one at a time. Heals on. 12 minutes.

        public static uint BossRushWinCp = 5000;
        public static ulong BossRushWinGold = 400000;
        public static uint BossRushWinRevoraPoints = 14;
        public static uint BossRushWinPerfectionStoneId = 3009002;
        public static byte BossRushWinPerfectionStones = 1;
        public static uint BossRushConsolationCp = 1200;
        public static ulong BossRushConsolationGold = 80000;
        public static uint BossRushConsolationRevoraPoints = 4;

        // Duel Pit: twice a day 11:15 and 19:15. 1v1, winner stays 16 rounds. Top four claim.

        public static uint DuelPitFirstCp = 12000;
        public static ulong DuelPitFirstGold = 1000000;
        public static uint DuelPitFirstRevoraPoints = 32;
        public static uint DuelPitSecondCp = 7000;
        public static ulong DuelPitSecondGold = 500000;
        public static uint DuelPitSecondRevoraPoints = 18;
        public static uint DuelPitThirdCp = 4000;
        public static ulong DuelPitThirdGold = 250000;
        public static uint DuelPitThirdRevoraPoints = 10;
        public static uint DuelPitFourthCp = 2000;
        public static ulong DuelPitFourthGold = 100000;
        public static uint DuelPitFourthRevoraPoints = 6;

        // Steed Race: twice a day 13:15 and 21:15. Equip a steed. Top three claim. Old: 100,000 RP to first five.

        public static uint SteedRaceFirstCp = 6000;
        public static ulong SteedRaceFirstGold = 500000;
        public static uint SteedRaceFirstRevoraPoints = 15;
        public static uint SteedRaceSecondCp = 3500;
        public static ulong SteedRaceSecondGold = 250000;
        public static uint SteedRaceSecondRevoraPoints = 8;
        public static uint SteedRaceThirdCp = 2000;
        public static ulong SteedRaceThirdGold = 100000;
        public static uint SteedRaceThirdRevoraPoints = 5;

        // NobleWar is once a day. Old: King 20k/2m, Prince 12k/1m, Duke 8k/500k, Earl 5k/250k.

        public static uint NobleWarKingCp = 18000;
        public static ulong NobleWarKingGold = 1500000;
        public static uint NobleWarKingRevoraPoints = 40;
        public static uint NobleWarPrinceCp = 10000;
        public static ulong NobleWarPrinceGold = 800000;
        public static uint NobleWarPrinceRevoraPoints = 25;
        public static uint NobleWarDukeCp = 7000;
        public static ulong NobleWarDukeGold = 400000;
        public static uint NobleWarDukeRevoraPoints = 18;
        public static uint NobleWarEarlCp = 4000;
        public static ulong NobleWarEarlGold = 200000;
        public static uint NobleWarEarlRevoraPoints = 10;

        // Expedition: old code gave a DB/Meteor/gem on EVERY kill. Mid-ground is a roll.
        // 1088000 DragonBall, 1088001 Meteor, 720027 MeteorTear, 730001 Stone.

        public static int ExpeditionLootChance = 20;
        public static uint[] ExpeditionLootItems = new uint[] { 1088001, 720027, 730001, 1088000 };
        public static uint ExpeditionExtractRevoraPoints = 100;
        public static uint ExpeditionRevoraPointsPerItem = 20;

        // Clan domination tick. Old base 20,000 CPs then split.

        public static uint ClanWarCp = 5000;
        public static uint ClanWarRevoraPoints = 8;

        // =============================================================================
        // PARKED EVENT PRIZES (wired so unparking them does not restore million-CP purses)
        // =============================================================================

        public static uint GuildWarCp = 8000;
        public static ulong GuildWarGold = 2000000;
        public static uint GuildWarMemberCp = 2000;
        public static ulong GuildWarMemberGold = 250000;
        public static uint GuildWarLeaderRevoraPoints = 100;
        public static uint GuildWarOfficerRevoraPoints = 50;
        public static uint GuildWarMemberRevoraPoints = 20;
        public static uint SuperGuildWarCp = 10000;
        public static ulong SuperGuildWarGold = 3000000;
        public static uint PkTournamentCp = 10000;
        public static ulong PkTournamentGold = 1000000;
        public static uint PkTournamentRevoraPoints = 25;
        public static uint RandomBoxRevoraPoints = 50;
        public static uint EliteGuildWarCp = 8000;
        public static ulong EliteGuildWarGold = 1500000;
        public static uint ClassPkCp = 8000;
        public static uint SteedRaceRevoraPoints = 15;
        public static uint PowerArenaStage1RevoraPoints = 200;
        public static uint PowerArenaStage2Cp = 8000;
        public static uint PowerArenaStage2RevoraPoints = 30;
        public static uint PowerArenaStage3Cp = 7000;
        public static uint PowerArenaStage3RevoraPoints = 25;
        public static uint PowerArenaStage4Cp = 6000;
        public static uint PowerArenaStage4RevoraPoints = 22;
        public static uint PowerArenaStage5Cp = 5000;
        public static uint PowerArenaStage5RevoraPoints = 20;
        public static uint PowerArenaStage6Cp = 4000;
        public static uint PowerArenaStage6RevoraPoints = 18;
        public static uint PowerArenaStage7Cp = 3500;
        public static uint PowerArenaStage7RevoraPoints = 15;

        // premioeventos leftovers used by parked Guild War / PK NPC claim.

        public static uint PremioVipCp = 0;
        public static uint PremioVipGold = 0;
        public static uint PremioDropCpMin = 0;
        public static uint PremioDropCpMax = 0;
        public static uint PremioEventosPkCp = 8000;
        public static uint PremioEventosPkGold = 1000000;
        public static uint PremioHombreloco = 0;
        public static uint PremioMestroMarcial = 0;
        public static uint PremioHombreOro = 0;
        public static uint PremioMujerOro = 0;

        // =============================================================================
        // QUEST / STARTER PRIZES
        // =============================================================================
        // Daily Envoy (79882) and the related Magnolia / Spirit Bead / Worship /
        // Desert Guardian / Exorcism turn-ins all used to stamp 50,000 CPs plus
        // a Chi Token and one ExpBall. Official dailies paid EXP/study, not tens
        // of thousands of CPs. 2,500 CPs, 15 Revora Points, the token and ExpBall
        // is the middle. Every quest and event prize also grants Revora Points.

        public static uint DailyQuestCp = 2500;
        public static uint DailyQuestRevoraPoints = 15;
        public static uint DailyQuestChiToken = 729304;
        public static ushort DailyPriceQuestStudy = 25;
        public static ushort DailyRareMaterialsStudy = 15;
        public static ushort DailySoulBallStudy = 40;
        public static ushort DailyExorcismStudy = 25;
        public static ushort DailyDesertGuardianStudy = 50;
        public static ushort DailyMagnoliaStudy = 10;
        public static ushort DailySpiritBeadNormalStudy = 15;
        public static ushort DailySpiritBeadRefinedStudy = 20;
        public static ushort DailySpiritBeadUniqueStudy = 25;
        public static ushort DailySpiritBeadEliteStudy = 40;
        public static ushort DailySpiritBeadSuperStudy = 80;
        public static uint DailyWorshipCp = 2500;
        public static uint DailyWorshipRevoraPoints = 15;
        // Daily Envoy 79882 Twin City 1002 319,307.
        // Skip prices used to be leftover TQ-point numbers (1 / 5 / 10 / 12 / 27 / 270).
        // A daily pays DailyQuestCp (2,500). Skip still grants that purse, so skip costs
        // more than the CP prize. The legacy paid reset is retired.
        public static ushort DailyEnvoyMap = 1002;
        public static ushort DailyEnvoyX = 319;
        public static ushort DailyEnvoyY = 307;
        public static ushort DailyEnvoyAvatar = 84;
        public static byte DailyWorshipMinLevel = 50;
        public static byte DailyPriceMinLevel = 60;
        public static byte DailySoldiersMinLevel = 70;
        public static byte DailyRareMinLevel = 75;
        public static byte DailyMagnoliaMinLevel = 80;
        public static byte DailyExorcismMinLevel = 90;
        public static byte DailySoulBallMinLevel = 100;
        public static byte DailyParadiseMinLevel = 110;
        public static bool DailyEnvoyShowMagnolia = true;
        public static bool DailyEnvoyShowReset = false; // Compatibility only; cannot enable a paid reset.
        public static ushort DailyWorshipMap = 3081;
        public static ushort DailyWorshipX = 56;
        public static ushort DailyWorshipY = 56;
        public static ushort DailySoldiersMap = 1000;
        public static ushort DailySoldiersX = 539;
        public static ushort DailySoldiersY = 649;
        public static ushort DailyMagnoliaMap = 1806;
        public static ushort DailyMagnoliaX = 107;
        public static ushort DailyMagnoliaY = 109;
        public static ushort DailyExorcismMap = 1036;
        public static ushort DailyExorcismX = 251;
        public static ushort DailyExorcismY = 153;
        public static ushort DailyParadiseMap = 0;
        public static ushort DailyParadiseX = 0;
        public static ushort DailyParadiseY = 0;
        public static uint DailyPriceListItem = 729087;
        public static uint DailyPriceBanditBlade = 729096;
        public static byte DailyPriceBanditAmount = 20;
        public static uint DailyPriceBloodyTeeth = 729101;
        public static byte DailyPriceBloodyTeethAmount = 20;
        public static uint DailyPriceBatBlood = 729099;
        public static byte DailyPriceBatBloodAmount = 25;
        public static uint DailyPriceBladeScrap = 729093;
        public static byte DailyPriceBladeScrapAmount = 25;
        public static int DailyPriceRollExclusive = 5;
        public static byte DailyPriceChangeMax = 4;
        public static uint DailyPriceChangeCp = 200;
        public static uint DailyPriceSkipCpPerChange = 250;
        public static uint DailyPriceSkipCpFlat = 5000;
        public static byte DailyPriceHuntDropPercent = 40;
        public static uint DailyRarePhoenixGem = 700001;
        public static uint DailyRareDragonGem = 700011;
        public static uint DailyRareFuryGem = 700021;
        public static uint DailyRareRainbowGem = 700031;
        public static byte DailyRareGemAmount = 3;
        public static int DailyRareRollExclusive = 5;
        public static byte DailyRareChangeMax = 4;
        public static uint DailyRareChangeRevoraPoints = 25;
        public static byte DailyRareStarMax = 3;
        public static uint DailyRareStarRevoraPoints = 10;
        public static ushort DailyRareStarStudyBonus = 10;
        public static uint DailyRareSkipCp = 5000;
        public static uint DailySpiritBeadNormal = 729611;
        public static uint DailySpiritBeadRefined = 729612;
        public static uint DailySpiritBeadUnique = 729613;
        public static uint DailySpiritBeadElite = 729614;
        public static uint DailySpiritBeadSuper = 729703;
        public static uint DailySpiritBeadNormalNeed = 2500;
        public static uint DailySpiritBeadRefinedNeed = 2000;
        public static uint DailySpiritBeadUniqueNeed = 1500;
        public static uint DailySpiritBeadEliteNeed = 1000;
        public static uint DailySpiritBeadSuperNeed = 500;
        public static byte DailySpiritBeadFill = 0;
        // Retained for settings compatibility; Soul Ball grades now follow completed hunts.
        public static int DailySpiritBeadRollExclusive = 5;
        public static uint DailySpiritBeadSuperCp = 800;
        public static uint DailySpiritBeadSkipCp = 5000;
        public static uint DailyEnvoyResetCp = 25000;
        public static uint DailyExorcismSkipCp = 5000;
        public static uint DailyExorcismToken = 711211;
        public static byte DailyExorcismPackTokens = 3;
        public static uint DailyExorcismPack = 720948;
        public static byte DailyExorcismGoldPackTokens = 9;
        public static uint DailyExorcismGoldPack = 729594;
        public static uint DailyDesertSkipCp = 5000;
        public static byte DailyDesertSouls = 3;
        public static uint DailyMagnoliaRefinedSeed = 729307;
        public static uint DailyMagnoliaSuperSeed = 729310;
        public static uint DailyMagnoliaSuperSeedCp = 100;
        public static uint ChiTokenCashCp = 1500;
        public static uint ChiTokenCashRevoraPoints = 5;
        public static uint CollectorZhaoCp = 10;
        public static uint CollectorZhaoRevoraPoints = 1;
        public static uint WelcomeGold = 50000;
        public static uint WelcomeRevoraPoints = 100;
        // Path Hunt (Twin City 1002 319,299, npc 900000016 Rowan). Once per reborn.
        // Twin City flocks after Anwen's level 70 kit. Caelan is the next trial.
        public static uint PathHuntPheasantKills = 20;
        public static uint PathHuntRobinKills = 20;
        public static uint PathHuntDoveKills = 20;
        public static uint PathHuntApparitionKills = 20;
        public static uint PathHuntGold = 25000;
        public static uint PathHuntRevoraPoints = 100;
        public static uint PathHuntExpPotionId = 723017;
        public static byte PathHuntExpPotions = 3;
        public static byte PathHuntPuff0 = 80;
        public static byte PathHuntPuff1 = 95;
        public static byte PathHuntPuff2 = 105;
        public static byte PathHuntPuffPotions = 8;
        // Courier Loop (Twin City 1002 319,303, npc 900000017 Cerys). Once per reborn.
        // Bandits are Phoenix Castle 1011 (not Bird Island).
        public static uint CourierLoopBanditKills = 20;
        public static uint CourierLoopCp = 8000;
        public static uint CourierLoopGold = 80000;
        public static uint CourierLoopRevoraPoints = 200;
        public static byte CourierLoopPuff = 120;
        public static byte CourierLoopVipLevel = 6;
        public static byte CourierLoopVipDays = 7;
        // Soul Trial (Twin City 1002 319,295, npc 900000013). Once per reborn.
        // Desert hunt is Sand Monsters. Courier Loop Bandits are Phoenix Castle.
        public static uint SoulTrialBirdKills = 50;
        public static uint SoulTrialApeKills = 50;
        public static uint SoulTrialSandKills = 50;
        public static uint SoulTrialGold = 50000;
        public static uint SoulTrialRevoraPoints = 500;
        public static uint SoulTrialExpPotionId = 723017;
        public static byte SoulTrialExpPotions = 5;
        public static byte SoulTrialPuff0 = 90;
        public static byte SoulTrialPuff1 = 100;
        public static byte SoulTrialPuff2 = 110;
        public static byte SoulTrialPuffPotions = 10;
        public static uint GuildCreateRevoraPoints = 5000;
        public static uint GuildCreateConquerPoints = 25000;
        public static byte GuildCreateMinLevel = 90;
        public static uint ClanCreateGold = 100000;
        public static uint ClanCreateFund = 50000;
        public static byte ClanCreateMinLevel = 50;
        public static uint CityCommanderHonorGold = 50000;
        public static uint CityCommanderHonorRevoraPoints = 10;
        public static uint CityCommanderHonorCost = 5;

        // =============================================================================
        // REVORAMASTER (Twin City 1002 410,354, npc 900000001)
        // =============================================================================
        // Peak, vestments, and the mount kit used to share one 1,000,000 CP price.
        // They are separate knobs so you can discount one service without the others.
        // VIP 6 is lifetime (VIPDays = 0). Rename reconnects in Twin City.
        // Priced against the current purse: daily quest 2,500 CPs, LMS 8,000 CPs.

        public static uint RevoraMasterPeakPrice = 25000;
        public static uint RevoraMasterVestmentsPrice = 40000;
        public static uint RevoraMasterMountKitPrice = 40000;
        public static uint RevoraMasterRenamePrice = 2500;
        public static uint RevoraMasterVipPrice = 50000;
        public static byte RevoraMasterVipLevel = 6;

        // Sereth gender change. Genuine 6711 bodies only: 1003/1004 <-> 2001/2002.
        public static uint SerethGenderPrice = 25000;
        // Sereth reincarnation. Old lock was 500,000,000 CPs. Dew is 2,000 CPs.
        public static uint SerethReincarnationCp = 5000;
        public static uint NameChangeCp = 5000;
        public static uint HousePermitCp = 8000;
        public static uint CertImproveCp = 4000;
        public static uint CertImprove3Cp = 6000;
        public static uint CertImprove4Cp = 8000;
        public static uint CertImprove5Cp = 10000;
        public static uint BlessMinusOneCp = 5000;
        public static uint SkillSoulLevel1Cp = 2500;
        public static uint SkillSoulLevel2Cp = 5000;
        public static uint PkClear100Cp = 5000;
        public static uint MarketErrandCp = 800;
        public static ulong MarketErrandGold = 15000;
        public static uint MarketErrandRevoraPoints = 8;
        // Twin City merchant clerk 50404. Old 2e9 CPs + 1e12 gold was a leftover lock.
        public static uint MerchantApplyPrice = 25000;
        // Luna 4178 Moon Platform fare. 300 silver is already a walk-up fare.
        public static ulong LunaTeleportGold = 300;
        // Chi / Jiang Hu hunt desk 900043 Twin City 1002 363,339. Old lock was
        // 1,000,000 CPs and level 140 second-reborn. Ordinary first-reborn Chi
        // players can walk up. Rare pack chance is percent 0..100.
        public static uint ChiJiangHuntCp = 5000;
        // Official Chi refill rate: 1 CP buys 4 Chi points; 4,000 points cost 1,000 CPs.
        public static uint ChiFullRefillCp = 1000;
        // Official Jiang Hu costs: paid training rises by 10 CPs, up to 500.
        public static uint JiangHuTrainingStepCp = 10;
        public static uint JiangHuTrainingMaxCp = 500;
        public static uint JiangHuRestoreCp = 20;
        public static ushort JiangHuFreeTrainingStudyPoints = 10;
        public static byte ChiJiangMinLevel = 110;
        public static byte ChiJiangMinReborn = 1;
        public static int ChiJiangRarePercent = 25;
        // Super Khopesh blade 410449. Type 410 (Blade), level 110. Sold at Vesper.
        public static uint KhopeshItemId = 410449;
        public static uint KhopeshRevoraPoints = 5000;

        // AFK CPs every minute for level 140 second-reborn. Old: 10,000–90,000 (VIP<=6)
        // and 50,000–250,000 (VIP>=7). That is a private-server printer. Off by default.
        public static bool OnlineConquerPointsEnabled = false;
        public static uint OnlineConquerPointsPerMinute = 20;
        public static uint OnlineConquerPointsAfternoonBonus = 10;
        public static uint OnlineConquerPointsVip7PerMinute = 40;
        public static uint OnlineConquerPointsVip7AfternoonBonus = 20;

        // Arena Qualifier. Old 100-win purse was 100,000,000 CPs. 50-loss block stole
        // 100,000,000 CPs from the winner (bug); keep that off.
        public static uint ArenaHundredWinsCp = 5000;
        public static ulong ArenaHundredWinsGold = 250000;
        public static uint ArenaHundredWinsRevoraPoints = 50;
        public static byte ArenaHundredWinsMinVip = 5;
        public static bool ArenaFiftyLossesPenaltyEnabled = false;

        // =============================================================================
        // EFFECTIVE RATES / LOAD / KILL EXP
        // =============================================================================

        public static uint EffectiveExperienceRate
        {
            get { return AtLeastOne(ExperienceRate) * AtLeastOne(ExperienceRateMultiplier); }
        }

        public static uint EffectiveSpellExperienceRate
        {
            get { return AtLeastOne(SpellExperienceRate) * AtLeastOne(SpellExperienceRateMultiplier); }
        }

        public static uint EffectiveProficiencyExperienceRate
        {
            get { return AtLeastOne(ProficiencyExperienceRate) * AtLeastOne(ProficiencyExperienceRateMultiplier); }
        }

        // Called once from Server.cs while reading `configuration`.
        // Argument order matches the historical SQL swap:
        //   spellExperienceRate       <- column ProficiencyExperienceRate
        //   proficiencyExperienceRate <- column SpellExperienceRate
        public static void Load(uint experienceRate, uint spellExperienceRate, uint proficiencyExperienceRate, uint moneyDropRate, uint moneyDropMultiple)
        {
            if (UseDatabaseRates)
            {
                ExperienceRate = AtLeastOne(experienceRate);
                SpellExperienceRate = AtLeastOne(spellExperienceRate);
                ProficiencyExperienceRate = AtLeastOne(proficiencyExperienceRate);
                MoneyDropRate = moneyDropRate;
                MoneyDropMultiple = AtLeastOne(moneyDropMultiple);
            }
            Console.WriteLine("Config EXP x" + EffectiveExperienceRate + " spell x" + EffectiveSpellExperienceRate + " proficiency x" + EffectiveProficiencyExperienceRate + " item-drop x" + ItemDropRateMultiplier + "+" + ItemDropRateBonus + " money-drop " + (MoneyDropEnabled ? "on " : "off ") + MoneyDropRate + "x" + MoneyDropMultiple + " daily-quest " + DailyQuestCp + " CPs LMS " + LastManStandingCp + " CPs cap " + LevelCap);
        }

        public static void LoadPrizes(uint vipCp, uint vipGold, uint dropCpMax, uint dropCpMin, uint eventosPkGold, uint eventosPkCp, uint egcGold, uint egcCp, uint guildGold, uint guildCp, uint hombreloco, uint pkTorneosGold, uint pkTorneosCp, uint mestroMarcial, uint hombreOro, uint mujerOro)
        {
            if (!UseDatabasePrizes)
                return;
            PremioVipCp = vipCp;
            PremioVipGold = vipGold;
            PremioDropCpMax = dropCpMax;
            PremioDropCpMin = dropCpMin;
            PremioEventosPkGold = eventosPkGold;
            PremioEventosPkCp = eventosPkCp;
            EliteGuildWarGold = egcGold;
            EliteGuildWarCp = egcCp;
            GuildWarGold = guildGold;
            GuildWarCp = guildCp;
            PremioHombreloco = hombreloco;
            PkTournamentGold = pkTorneosGold;
            PkTournamentCp = pkTorneosCp;
            PremioMestroMarcial = mestroMarcial;
            PremioHombreOro = hombreOro;
            PremioMujerOro = mujerOro;
        }

        // OwnItemRate -> live percent for Kernel.Rate. Clamped 0..100 because Rate() is "value > Random() % 100".
        public static int AdjustedItemDropRate(int baseRate)
        {
            long chance = (long)baseRate * ItemDropRateMultiplier + ItemDropRateBonus;
            if (chance < 0)
                return 0;
            if (chance > 100)
                return 100;
            return (int)chance;
        }

        // Kill-EXP multipliers for addMultiple=true. Order matches the old IncreaseExperience block.
        public static ulong ApplyKillExperience(ulong experience, byte vipLevel, ushort[] gems, uint doubleExperienceTime, uint heavenBlessing, byte reborn, byte guildLevel, int battlePower)
        {
            if (vipLevel >= VipExperienceMinLevel)
                experience *= VipExperienceMultiplier;
            if (DailyExperienceMultiplier > 1)
                experience *= DailyExperienceMultiplier;
            experience *= EffectiveExperienceRate;
            if (gems != null && gems.Length > 3)
                experience += experience * gems[3] / 100;
            if (doubleExperienceTime > 0)
                experience *= DoubleExperienceMultiplier;
            if (heavenBlessing > 0)
                experience += experience * HeavenBlessingExperiencePercent / 100;
            if (reborn >= 2 && SecondRebornExperienceDivisor > 1)
                experience /= SecondRebornExperienceDivisor;
            if (guildLevel > 0 && GuildExperiencePercentPerLevel > 0)
                experience += experience * guildLevel * GuildExperiencePercentPerLevel / 100;
            if (BattlePowerExperiencePercent > 0 && battlePower > 0)
                experience += experience * (uint)battlePower * BattlePowerExperiencePercent / 10000;
            return experience;
        }

        private static uint AtLeastOne(uint value)
        {
            return value == 0 ? 1u : value;
        }
    }
}
