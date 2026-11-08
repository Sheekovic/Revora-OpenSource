using System;
using System.Linq;
using Revora.Game;
using System.Drawing;
using Revora.Client;
using System.Threading;
using Revora.Database;
using Revora.Game.Features;
using Revora.Network.Sockets;
using System.Threading.Generic;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using Revora.Game.ConquerStructures;
using Revora.Game.Features.Tournaments;
using Revora.Interfaces;
using Revora.Network;

namespace Revora
{
    public unsafe class Thread
    {
        public static StaticPool ReceivePool, SendPool, GenericThreadPool;
        public TimerRule<GameState> Buffers, Characters, AutoAttack, Companions, Prayer;
        public TimerRule<ClientWrapper> ConnectionReceive, ConnectionReview, ConnectionSend;
        public static TimerRule<FloorItem> FloorItems;
        public PoleDomination PoleDomination;
        public CaptureTheFlag CTF;
        public XScape.DelayedTask DelayedTask;
        public SteedRace SteedRace;
        public ArsenalSystem Arsenal;
    
        public PoleEmulator PoleEmulator;
        public List<KillTournament> Tournaments;
        public HeroOfGame HeroOfGame = new HeroOfGame();
        public PlanderWar PlanderWar = new PlanderWar();
        public Thread()
        {
            GenericThreadPool = new StaticPool(32).Run();
            ReceivePool = new StaticPool(32).Run();
            SendPool = new StaticPool(32).Run();
        }
        public void Init()
        {
            Prayer = new TimerRule<GameState>(PrayerCallback, 1000, ThreadPriority.BelowNormal);
            Buffers = new TimerRule<GameState>(BuffersCallback, 1000, ThreadPriority.BelowNormal);
            Characters = new TimerRule<GameState>(CharactersCallback, 1000, ThreadPriority.BelowNormal);
            AutoAttack = new TimerRule<GameState>(AutoAttackCallback, 1000, ThreadPriority.BelowNormal);
            Companions = new TimerRule<GameState>(CompanionsCallback, 1000, ThreadPriority.BelowNormal);
            ConnectionSend = new TimerRule<ClientWrapper>(connectionSend, 1);
            ConnectionReceive = new TimerRule<ClientWrapper>(connectionReceive, 1);
            ConnectionReview = new TimerRule<ClientWrapper>(connectionReview, 60000, ThreadPriority.Lowest);

            Subscribe(WorldTournaments, 1000);
            Subscribe(time => LivingWorldSystem.TickRetaliation(), 100);
            Subscribe(time => ArenaBots.Tick(), 240);
            Subscribe(ServerFunctions, 5000);
            Subscribe(ArenaFunctions, 1000, ThreadPriority.AboveNormal);
            Subscribe(TeamArenaFunctions, 1000, ThreadPriority.AboveNormal);
            Subscribe(ChampionFunctions, 1000, ThreadPriority.AboveNormal);
        }
        public void CreateTournaments()
        {
            // Restore offline ranking owners before guards/adventurers enter GamePool.
            FlowerDelivery.Initialize();
            Tournaments = new List<KillTournament>();
            // Legacy kill/class tournaments parked in docs/LEGACY_EVENTS.md
            #region Torneo de Nobleza
            #endregion
            #region Class PK Tournament
            //map = Kernel.Maps[1730];
            //Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0, (client) =>
            //    {
            //        client.Player.ConquerPoints += 10000;
            //        client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.TopTrojan, 1, DateTime.Now.AddDays(7).AddHours(-1));
            //    }, "Gana el torneo de tu profecion y se el mejor de tu clase", (p) => { return p.Player.Class >= 10 && p.Player.Class <= 15; },
            //    "Class PK War (Trojan)"));
            //Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0, (client) =>
            //{
            //    client.Player.ConquerPoints += 10000;
            //    client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.TopWarrior, 1, DateTime.Now.AddDays(7).AddHours(-1));
            //}, "Class PK War (Warrior)", (p) => { return p.Player.Class >= 20 && p.Player.Class <= 25; },
            //    "Gana el torneo de tu profecion y se el mejor de tu clase"));
            //Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
            //    (client) =>
            //    {
            //        client.Player.ConquerPoints += 10000;
            //        client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.TopArcher, 1, DateTime.Now.AddDays(7).AddHours(-1));
            //    }, "Class PK War (Archer)", (p) => { return p.Player.Class >= 40 && p.Player.Class <= 45; },
            //    "Gana el torneo de tu profecion y se el mejor de tu clase"));
            //Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
            //    (client) =>
            //    {
            //        client.Player.ConquerPoints += 10000;
            //        client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.TopNinja, 1, DateTime.Now.AddDays(7).AddHours(-1));
            //    }, "Class PK War (Ninja)", (p) => { return p.Player.Class >= 50 && p.Player.Class <= 55; },
            //    "Gana el torneo de tu profecion y se el mejor de tu clase"));
            //Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
            //    (client) =>
            //    {
            //        client.Player.ConquerPoints += 10000;
            //        client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.TopMonk, 1, DateTime.Now.AddDays(7).AddHours(-1));
            //    }, "Class PK War (Monk)", (p) => { return p.Player.Class >= 60 && p.Player.Class <= 65; },
            //    "Gana el torneo de tu profecion y se el mejor de tu clase"));
            //Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
            //    (client) =>
            //    {
            //        client.Player.ConquerPoints += 10000;
            //        client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.TopPirate, 1, DateTime.Now.AddDays(7).AddHours(-1));
            //    }, "Class PK War (Pirate)", (p) => { return p.Player.Class >= 70 && p.Player.Class <= 75; },
            //    "Gana el torneo de tu profecion y se el mejor de tu clase"));
            //Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
            //    (client) =>
            //    {
            //        client.Player.ConquerPoints += 10000;
            //        client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.TopWaterTaoist, 1, DateTime.Now.AddDays(7).AddHours(-1));
            //    }, "Class PK War (Water Taoist)", (p) => { return p.Player.Class >= 130 && p.Player.Class <= 135; },
            //    "Gana el torneo de tu profecion y se el mejor de tu clase"));
            //Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
            //    (client) =>
            //    {
            //        client.Player.ConquerPoints += 10000;
            //        client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.TopFireTaoist, 1, DateTime.Now.AddDays(7).AddHours(-1));
            //    }, "Class PK War (Fire Taoist)", (p) => { return p.Player.Class >= 140 && p.Player.Class <= 145; },
            //    "Gana el torneo de tu profecion y se el mejor de tu clase"));
            //Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
            //  (client) =>
            //  {
            //      client.Player.ConquerPoints += 10000;
            //      client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.DragonWarriorTop, 1, DateTime.Now.AddDays(7).AddHours(-1));
            //  }, "Class PK War (Dragon Warrior)", (p) => { return p.Player.Class >= 80 && p.Player.Class <= 85; },
            //  "Gana el torneo de tu profecion y se el mejor de tu clase"));
            #endregion
            #region Class PK2 Tournament
           /* map = Kernel.Maps[1730];
            Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Tuesday, 22, 0, (client) =>
            {
                client.Player.ConquerPoints += 10000;
                client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.Top2Trojan, 1, DateTime.Now.AddDays(7).AddHours(-1));
            }, "ClassPK2 War (Trojan)", (p) => { return p.Player.Class >= 10 && p.Player.Class <= 15; },
                "Gana el torneo de tu profecion y se el mejor de tu clase + Top"));
            Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Tuesday, 22, 0, (client) =>
            {
                client.Player.ConquerPoints += 10000;
                client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.Top2Warrior, 1, DateTime.Now.AddDays(7).AddHours(-1));
            },
                "ClassPK2 War (Warrior)", (p) => { return p.Player.Class >= 20 && p.Player.Class <= 25; },
                "Gana el torneo de tu profecion y se el mejor de tu clase + Top"));
            Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Tuesday, 22, 0,
                (client) =>
                {
                    client.Player.ConquerPoints += 10000;
                    client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.Top2Archer, 1, DateTime.Now.AddDays(7).AddHours(-1));
                }, "ClassPK2 War (Archer)", (p) => { return p.Player.Class >= 40 && p.Player.Class <= 45; },
                "Gana el torneo de tu profecion y se el mejor de tu clase + Top"));
            Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Tuesday, 22, 0,
                (client) =>
                {
                    client.Player.ConquerPoints += 10000;
                    client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.Top2Ninja, 1, DateTime.Now.AddDays(7).AddHours(-1));
                }, "ClassPK2 War (Ninja)", (p) => { return p.Player.Class >= 50 && p.Player.Class <= 55; },
                "Gana el torneo de tu profecion y se el mejor de tu clase + Top"));
            Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Tuesday, 22, 0,
                (client) =>
                {
                    client.Player.ConquerPoints += 10000;
                    client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.Top2Monk, 1, DateTime.Now.AddDays(7).AddHours(-1));
                }, "ClassPK2 War (Monk)", (p) => { return p.Player.Class >= 60 && p.Player.Class <= 65; },
                "Gana el torneo de tu profecion y se el mejor de tu clase + Top"));
            Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
                (client) =>
                {
                    client.Player.ConquerPoints += 10000;
                    client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.TopPirate2, 1, DateTime.Now.AddDays(7).AddHours(-1));
                }, "ClassPK2 War (Pirate)", (p) => { return p.Player.Class >= 70 && p.Player.Class <= 75; },
                "Gana el torneo de tu profecion y se el mejor de tu clase + Top"));
            Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
                (client) =>
                {
                    client.Player.ConquerPoints += 10000;
                    client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.Top2Water, 1, DateTime.Now.AddDays(7).AddHours(-1));
                }, "ClassPK2 War (Water Taoist)", (p) => { return p.Player.Class >= 130 && p.Player.Class <= 135; },
                "Gana el torneo de tu profecion y se el mejor de tu clase + Top"));
            Tournaments.Add(new KillTournament(map.MakeDynamicMap().ID, WeekDay.Monday, 22, 0,
                (client) =>
                {
                    client.Player.ConquerPoints += 10000;
                    client.Player.AddTopStatus((ulong)FlagsUpdate.Flags.Top2Fire, 1, DateTime.Now.AddDays(7).AddHours(-1));
                }, "ClassPK2 War (Fire Taoist)", (p) => { return p.Player.Class >= 140 && p.Player.Class <= 145; },
                "Gana el torneo de tu profecion y se el mejor de tu clase + Top")); */
            #endregion

            ClanWar.Create();
            CTF = new CaptureTheFlag();
            ElitePKTournament.Create();
            SteedRace = new SteedRace();
            Arsenal = new ArsenalSystem();
            PlanderWar PlanderWar = new PlanderWar();
            TeamElitePk.TeamTournament.Create();
            TeamElitePk.SkillTeamTournament.Create();
            DelayedTask = new XScape.DelayedTask();
            PoleDomination = new PoleDomination(30000000);
            CityGuardSystem.Create();
            PotencyHunt.Create();
            PotencyGuide.Create();
            DailyWorldBosses.Create();
            LivingWorldSystem.Create();
            RevoraMasterSystem.Create();
            WelcomeGuideSystem.Create();
            LastManStandingSystem.Create();
            NobleWarSystem.Create();
            ExpeditionEvent.Create();
            RelicRushSystem.Create();
            WaveDefenseSystem.Create();
            BeastTrialSystem.Create();
            BossRushSystem.Create();
            DuelPitSystem.Create();
            SteedRaceSystem.Create();
            SoulTrialSystem.Create();
            PathHuntSystem.Create();
            AzurePillQuest.Create();
            ConnectorQuest.Create();
            SpunGoldQuest.Initialize();
            ArtisanFeatherQuest.Initialize();
            MysticRingQuest.Initialize();
            FranticMonkeysQuest.Initialize();
            SoulGeneralQuest.Initialize();
            MadManQuest.Initialize();
            GhostCityQuest.Initialize();
            NewcomerIntroduction.Initialize();
            TomboyQuest.Initialize();
            DesertTreasureQuest.Initialize();
            DesertExpeditionQuest.Initialize();
            DefenseTalismanQuest.Initialize();
            AttackTalismanQuest.Initialize();
            TempestWingsQuest.Initialize();
            FamilyFeudQuest.Initialize();
            HeroWingsQuest.Initialize();
            DesertBloodQuest.Initialize();
            CourierLoopSystem.Create();
            RevoraPointShopSystem.Create();
            CpShopClerkSystem.Create();
            P6SoulShop.Create();
            ParkNpc(900018, 6000, 50, 70);
            ParkNpc(899986, 6000, 54, 70);
            ParkNpc(900173, 6000, 58, 70);
            ParkNpc(900035, 6000, 62, 70);
            ParkNpc(900000014, 1002, 396, 340);
            ParkNpc(9438, 1002, 396, 356);
            ParkNpc(59558, 1002, 396, 364);
            ParkNpc(10080, 1002, 423, 356);
            ParkNpc(900046, 6000, 66, 70);
            ParkNpc(3215, 1002, 327, 250);
            ParkNpc(9450, 1002, 319, 317);
            ParkNpc(DailyQuestEnvoySystem.NpcId, Config.DailyEnvoyMap, Config.DailyEnvoyX, Config.DailyEnvoyY);
            DailyQuestEnvoySystem.Create();
            TwinCityPlaza.Apply(ParkNpc);
            ElitePkEvent.PlaceEnvoy();
            LocalPkWar.PlaceGeneral();
            CouplePkEvent.PlaceGuide();
            DemonExterminators.Initialize();
            DemonExterminators.PlaceNpc();
            MoonBoxQuest.Initialize();
            MoonBoxQuest.PlaceNpcs();
            DisCityPacks.Initialize();
            ExorcismRewards.Initialize();
            DisCityJades.Initialize();
            WeaponBookShop.Initialize();
            TravelScrollShop.Initialize();
            GemComposition.Initialize();
            SteedBreeding.Initialize();
            DisCityQuest.Initialize();
            DisCityWorld.PlaceSaint();
            PowerArenaEvent.Initialize();
            PowerArenaEvent.PlaceNpcs();
            ReleaseSoulsQuest.Initialize();
            ArenaParticipation.Initialize();
            QuizShowEvent.Initialize();
            QuizShowEvent.PlaceNpc();
            SummerHunt.Initialize();
            PlanetWeekend.Initialize();
            PlanetEncounter.Initialize();
            PlanetEncounter.RecoverInterrupted();
            HeartEncounter.Initialize();
            AtomicTrade.Initialize();
            Friendships.Initialize();
            TradePartnerships.Initialize();
            MentorDepartures.Initialize();
            MentorStoneClaims.Initialize();
            MentorBlessingClaims.Initialize();
            PrayingStones.Initialize();
            CosmeticOwnership.Initialize();
            RetiredHairDyes.Initialize();
            RetiredBlackTulip.Initialize();
            ArmorDyeShop.Initialize();
            ShelbyRewards.Initialize();
            TravelProgress.Initialize();
            ForgottenRealms.Initialize();
            GrottoPassage.Initialize();
            GrottoKeyDrops.Initialize();
            CleanWaterDrops.Initialize();
            MeteorDrops.Initialize();
            DailyHuntDrops.Initialize();
            DailyPriceQuest.Initialize();
            AncestorWorship.Initialize();
            DailyRareQuest.Initialize();
            DailySoulBall.Initialize();
            MiningRewards.Initialize();
            MiningPassage.Initialize();
            TrainingGroundPassage.Initialize();
            MoonPlatformPassage.Initialize();
            AdventureTravel.Initialize();
            Housing.Initialize();
            HouseInstallments.PlaceManager();
            HousingMaterials.Initialize();
            HousingMaterials.PlaceAgent();
            HeavenlyStoneCrafting.Initialize();
            FirstRebirth.Initialize();
            SecondRebirth.Initialize();
            NinjaTraining.Initialize();
            MonkTraining.Initialize();
            WarriorTraining.Initialize();
            WarriorEpicQuest.Initialize();
            PowerBookQuest.Initialize();
            TrojanEpicQuest.Initialize();
            HonorPurchases.Initialize();
            ArenaPointPurchases.Initialize();
            ArenaAdminBalances.Initialize();
            AncientDevilQuest.Initialize();
            FlameLitQuest.Initialize();
            EliminationLifecycle.Initialize();
            NinjaEpicQuest.Initialize();
            MonkEpicQuest.Initialize();
            TaoistEpicQuest.Initialize();
            AssassinTraining.Initialize();
            PaidReincarnation.Initialize();
            ExemptionTokenShop.Initialize();
            EuroJerseyShop.Initialize();
            ItemEnchantment.Initialize();
            EquipmentUpgrades.Initialize();
            EquipmentSockets.Initialize();
            ExperienceConsumables.Initialize();
            ExperiencePotions.Initialize();
            TournamentExpPacks.Initialize();
            ExperienceProtection.Initialize();
            ExpCareCombine.Initialize();
            TimeDropHourglasses.Initialize();
            WelcomePacks.Initialize();
            StarterPacks.Initialize();
            RevoraStarterPack.Initialize();
            RebirthGarmentPack.Initialize();
            P7SoulChoicePack.Initialize();
            CpShopPurchases.Initialize();
            ConquerPointBags.Initialize();
            FixedOutputPacks.Initialize();
            DragonBallPacks.Initialize();
            PrizeCenterPacks.Initialize();
            EasterGoldPacks.Initialize();
            DailySignInPacks.Initialize();
            AncientStoneCombine.Initialize();
            StarStoneCombine.Initialize();
            AttackDefensePotions.Initialize();
            HealingMedicine.Initialize();
            BloodshedSeaBoosts.Initialize();
            HonorRefineryPacks.Initialize();
            GainRefineryPacks.Initialize();
            HossuHairPacks.Initialize();
            RedRunePacks.Initialize();
            YellowBlueRunePacks.Initialize();
            PlusOneStonePacks.Initialize();
            HigherStonePacks.Initialize();
            GemDragonSoulPacks.Initialize();
            WayOfHeroesClaims.Initialize();
            MillionaireStonePacking.Initialize();
            ChiPointItems.Initialize();
            SilverStudyBags.Initialize();
            SilverPouches.Initialize();
            StudyBooks.Initialize();
            QuestGiftPacks.Initialize();
            LotteryDraws.Initialize();
            PenitenceAmulets.Initialize();
            ChiTokenCash.Initialize();
            ElitePkPacks.Initialize();
            EliteNestedPacks.Initialize();
            P7SoulPacks.Initialize();
            ArenaPrizeConversions.Initialize();
            MagnoliaQuest.Initialize();
            DragonPillCrafting.Initialize();
            DragonSlayerHunt.Initialize();
            TeratoEncounter.Initialize();
            SwordMasterEncounter.Initialize();
            PurpleBansheeEncounter.Initialize();
            SnowBansheeEncounter.Initialize();
            ExorcismQuest.Initialize();
            AnniversaryQuest.Initialize();
            AnniversaryQuest.PlaceNpc();
            MonsterEquipmentDrops.Initialize();
            MentorAdmissions.Initialize();
            SummerHunt.PlaceNpc();
            SkyPassQuest.Initialize();
            SkyPassWorld.PlaceNpcs();
            SnakeIslandsQuest.Initialize();
            SnakeIslandsQuest.PlaceNpcs();
            LabyrinthQuest.Initialize();
            LabyrinthQuest.PlaceNpcs();
            ReleaseSoulsQuest.PlaceNpcs();
        }

        private static void ParkNpc(uint npcId, ushort mapId, ushort x, ushort y)
        {
            foreach (Map map in Kernel.Maps.Values)
            {
                INpc npc;
                if (map == null || map.Npcs == null) continue;
                if (!map.Npcs.TryGetValue(npcId, out npc)) continue;
                map.RemoveNpc(npc);
                if (Map.SecoundNpc.ContainsKey(npcId))
                    Map.SecoundNpc.Remove(npcId);
                if (Kernel.Maps.ContainsKey(mapId))
                {
                    npc.MapID = mapId;
                    npc.X = x;
                    npc.Y = y;
                    Kernel.Maps[mapId].AddNpc(npc);
                }
                break;
            }
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("npcs").Set("mapid", mapId).Set("cellx", x).Set("celly", y).Where("id", npcId).Execute();
            }
            catch
            {
            }
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("sobnpcs").Set("mapid", mapId).Set("cellx", x).Set("celly", y).Where("id", npcId).Execute();
            }
            catch
            {
            }
            Console.WriteLine("NPC " + npcId + " parked on map " + mapId + " " + x + "," + y);
        }

        private void connectionSend(ClientWrapper wrapper, int time)
        {
            ClientWrapper.TrySend(wrapper);
        }
        private void connectionReview(ClientWrapper wrapper, int time)
        {
            ClientWrapper.TryReview(wrapper);
        }
        private void connectionReceive(ClientWrapper wrapper, int time)
        {
            ClientWrapper.TryReceive(wrapper);
        }
        #region registro EpicaTaoista(Energia)
        public bool Register(FloorItem item)
        {
            if (item.TimerSubscriptions == null)
            {
                item.TimerSubscriptions = FloorItems.Add(item);
                return true;
            }
            return false;
        }
        public void Unregister(FloorItem item)
        {
            if (item.TimerSubscriptions == null) return;
            if (item.TimerSubscriptions != null)
            {
                item.TimerSubscriptions.Dispose();
                item.TimerSubscriptions = null;
            }
        }
        #endregion
        private bool Valid(GameState client)
        {
            if (client == null) return false;
            if (client.Fake) return Revora.Game.Features.ArenaActors.Player(client, false);
            if (client.Socket == null || !client.Socket.Alive || client.Entity == null)
            {
                client.Disconnect();
                return false;
            }
            return true;
        }
        public bool Register(GameState client)
        {
            client.LastClientOK = DateTime.Now;
            if (client.TimerSubscriptions == null)
            {
                client.TimerSyncRoot = new object();
                client.TimerSubscriptions = new IDisposable[]
                {
                    Buffers.Add(client),
                    Characters.Add(client),
                    AutoAttack.Add(client),
                    Companions.Add(client),
                    Prayer.Add(client),
                };
                return true;
            }
            return false;
        }
        public void UnRegister(GameState client)
        {
            if (client == null || client.TimerSubscriptions == null || client.TimerSyncRoot == null) return;
            lock (client.TimerSyncRoot)
            {
                if (client.TimerSubscriptions != null)
                {
                    foreach (var timer in client.TimerSubscriptions)
                        timer.Dispose();
                    client.TimerSubscriptions = null;
                }
            }
        }

        private void PrayerCallback(GameState client, int time)
        {
            if (!Valid(client)) return;
            Time32 Now = new Time32(time);
            if (client.Entity.Reborn > 1) return;
            if (client.Entity.HandleTiming)
            {
                if (!client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Praying))
                {
                    foreach (Interfaces.IMapObject ClientObj in client.Screen.Objects)
                    {
                        if (ClientObj != null)
                        {
                            if (ClientObj.MapObjType == Game.MapObjectType.Player)
                            {
                                var Client = ClientObj.Owner;
                                if (Client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CastPray))
                                {
                                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, ClientObj.X, ClientObj.Y) <= 3)
                                    {
                                        client.Entity.AddFlag((ulong)FlagsUpdate.Flags.Praying, StatusFlagsBigVector32.PermanentFlag, true);
                                        client.PrayLead = Client;
                                        client.Entity.Action = Client.Entity.Action;
                                        Client.Prayers.Add(client);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (client.PrayLead != null)
                {
                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, client.PrayLead.Entity.X, client.PrayLead.Entity.Y) > 4)
                    {
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Praying);
                        client.PrayLead.Prayers.Remove(client);
                        client.PrayLead = null;
                    }
                }
            }
        }
        #region Apuesta Cps
        public static ISkill LearnableSpell(ushort spellid)
        {
            ISkill spell = new Spell(true);
            spell.ID = spellid;
            return spell;
        }
        public static bool ZonaTroyano = false;
        public static bool ZonaNinja = false;
        public static bool ZonaMonje = false;
        public static bool Room4 = false;
        public static bool Room5 = false;
        public static bool Room6 = false;
        public static uint ZonaTroyanoPremio = 0;
        public static uint ZonaNinjaPremio = 0;
        public static uint ZonaMonjePremio = 0;
        public static uint Room4Price = 0;
        public static uint Room5Price = 0;
        public static uint Room6Price = 0;
        #endregion
        private void BuffersCallback(GameState client, int time)
        {
            if (!Valid(client)) return;
            using(var health=Revora.Game.Features.HealthMutationLease.Acquire(client.Entity,null))
                if(health.Allowed)BuffersOwned(client,time);
        }
        private void BuffersOwned(GameState client, int time)
        {
            if (!Valid(client)) return;
            Time32 Now = new Time32(time);
            #region Runes
            #region Healer
            if (client.Entity.Healer > 0 && (!client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Dead)))
            {
                if (!client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.HealingSnow))
                {
                    if (!client.Entity.Dead && client.Entity.Hitpoints != client.Entity.MaxHitpoints || client.Entity.Mana != client.Entity.MaxMana)
                    {
                        if (Now >= client.Entity.HealerStamp.AddSeconds(5))
                        {
                            if (client.Entity.Hitpoints != client.Entity.MaxHitpoints)
                                client.Entity.Hitpoints = Math.Min(client.Entity.Hitpoints + client.Entity.Healer, client.Entity.MaxHitpoints);
                            if (client.Entity.Mana != client.Entity.MaxMana)
                                client.Entity.Mana = (ushort)Math.Min(client.Entity.Mana + client.Entity.Healer, client.Entity.MaxMana);
                            client.Entity.HealerStamp = Time32.Now;
                        }
                    }
                }
            }
            #endregion
            #endregion
            #region Check Flags
            foreach (var flag in client.Entity.BitVector.GetFlags())
            {
                if (flag.Expire(DateTime.Now))
                {
                    client.Entity.RemoveFlag((ulong)(FlagsUpdate.Flags)flag.Key);
                }
            }
            #endregion
            #region WindWalker
            #region HealingSnow
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.HealingSnow))
            {
                if (Time32.Now > client.Entity.HealingSnowStamp.AddSeconds(5))
                {
                    var spell = client.Spells[12950];
                    if (spell != null)
                    {
                        var Spell = SpellTable.GetSpell(spell.ID, spell.Level);
                        #region Regeneracion De Vida y Mana Por Segundo (5)
                        if (Spell.Level == 1)
                        {
                            client.Entity.Hitpoints += 800;
                            client.Entity.Mana += 800;
                        }
                        if (Spell.Level == 2)
                        {
                            client.Entity.Hitpoints += 2700;
                            client.Entity.Mana += 2700;
                        }
                        if (Spell.Level == 3)
                        {
                            client.Entity.Hitpoints += 4800;
                            client.Entity.Mana += 4800;
                        }
                        if (Spell.Level == 4)
                        {
                            client.Entity.Hitpoints += 6200;
                            client.Entity.Mana += 6200;
                        }
                        if (Spell.Level == 5)
                        {
                            client.Entity.Hitpoints += 7600;
                            client.Entity.Mana += 7600;
                        }
                        if (Spell.Level == 6)
                        {
                            client.Entity.Hitpoints += 9900;
                            client.Entity.Mana += 9900;
                        }
                        #endregion
                        #region All Level´s XP Energy
                        client.XPCount += 1;
                        #endregion
                    }
                    client.Entity.HealingSnowStamp = Time32.Now;
                }
            }
            #endregion
            #region JusticeChant
            if (client.Spells.ContainsKey(12870))
            {
                if (!client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.JusticeChant))
                {
                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.JusticeChant,StatusFlagsBigVector32.PermanentFlag,false);
                }
            }
            else
            {
                if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.JusticeChant))
                {
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.JusticeChant);
                }
            }
            #endregion
            #region GreenEffect
            if (client.Entity.GreenEffect > 0)
                if (Now > client.Entity.GreenEffectStamp.AddSeconds(3))
                {
                    client.Entity.GreenEffectStamp = Time32.Now;
                    client.Entity.GreenEffect -= 1;
                    WindWalker.SendGreenEffect(client);
                }
            #endregion
            #endregion
            WarriorModes.DanceTick(client, Now);
            #region BreakingTouch
            if (!TeamArenaBreakingTouch.Tick(client, Now) && client.Entity.lianhuaranLeft > 0)
            {
                if (Now >= client.Entity.lianhuaranStamp.AddSeconds(2))
                {
                    float Percent = client.Entity.lianhuaranPercent;
                    if (client.Entity.Detoxication != 0)
                    {
                        float immu = 1 - client.Entity.Detoxication / 100F;
                        Percent = Percent * immu;
                    }
                    client.Entity.lianhuaranLeft--;
                    if (client.Entity.lianhuaranLeft == 0)
                    {
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran);
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran02);
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran03);
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran04);
                        return;
                    }
                    client.Entity.lianhuaranStamp = Now;
                    if (client.Entity.Hitpoints > 1)
                    {
                        uint damage = Game.Attacking.Calculate.Percent(client.Entity, Percent);
                        if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                        {

                            if (damage > client.Entity.AzureShieldDefence)
                            {
                                damage -= client.Entity.AzureShieldDefence;
                                Game.Attacking.Calculate.CreateAzureDMG(client.Entity.AzureShieldDefence, client.Entity, client.Entity);
                                //client.Player.RemoveFlag2((ulong)FlagsUpdate.Flags.AzureShield);
                            }
                            else
                            {
                                Game.Attacking.Calculate.CreateAzureDMG((uint)damage, client.Entity, client.Entity);
                                client.Entity.AzureShieldDefence -= (ushort)damage;
                                client.Entity.AzureShieldPacket();
                                damage = 1;
                            }
                        }
                        else
                            client.Entity.Hitpoints -= damage;

                    }
                }
            }
            
            #endregion
            #region JiangHu
            if (client.Entity.MyJiang != null)
            {
                client.Entity.MyJiang.TheadTime(client);
            }
            #endregion
            #region BlockShield
            if (client.Entity.BlockShieldCheck)
            {
                if (Now > client.Entity.BlockShieldStamp.AddSeconds(client.Entity.BlockShield))
                {
                    client.Entity.BlockShieldCheck = false;
                    client.ReloadBlock();
                }
            }
            #endregion
            #region ScurvyBomb
            if (client.Entity.OnScurvyBomb())
            {
                if (Now > client.Entity.ScurbyBombStamp.AddSeconds(client.Entity.ScurbyBomb))
                {
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.ScurvyBomb);
                    FlagsUpdate upgrade = new FlagsUpdate(true);
                    upgrade.UID = client.Entity.UID;
                    upgrade.Append((byte)FlagsUpdate.DataType.Fatigue, 0, 0, 0, 0);
                    client.Send(upgrade.ToArray());
                }
                else if (Now > client.Entity.ScurbyBomb2Stamp.AddSeconds(2))
                {
                    if (client.Entity.Stamina >= 5)
                    {
                        client.Entity.Stamina -= 5;
                        client.Entity.ScurbyBomb2Stamp = Time32.Now;
                        client.Entity.AddFlag((ulong)FlagsUpdate.Flags.ScurvyBomb,20,true);
                    }
                    client.Entity.Stamina = client.Entity.Stamina;
                    client.Entity.ScurbyBomb2Stamp = Time32.Now;
                }
            }
            #endregion
            #region Bless
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CastPray))
            {
                if (client.BlessTime <= 7198500)
                    client.BlessTime += 1000;
                else
                    client.BlessTime = 7200000;
                client.Entity.Update((byte)FlagsUpdate.DataType.LuckyTimeTimer, client.BlessTime, false);
            }
            else if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Praying))
            {
                if (client.PrayLead != null)
                {
                    if (client.PrayLead.Socket.Alive)
                    {
                        if (client.BlessTime <= 7199000)
                            client.BlessTime += 500;
                        else
                            client.BlessTime = 7200000;
                        client.Entity.Update((byte)FlagsUpdate.DataType.LuckyTimeTimer, client.BlessTime, false);
                    }
                    else
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Praying);
                }
                else
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Praying);
            }
            else
            {
                if (client.BlessTime > 0)
                {
                    if (client.BlessTime >= 500)
                        client.BlessTime -= 500;
                    else
                        client.BlessTime = 0;
                    client.Entity.Update((byte)FlagsUpdate.DataType.LuckyTimeTimer, client.BlessTime, false);
                }
            }
            #endregion
            #region XPList
            if (!client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.XPList))
            {
                if (Now > client.XPCountStamp.AddSeconds(3))
                {
                    #region Arrows
                    if (client.Equipment != null)
                    {
                        if (!client.Equipment.Free(5))
                        {
                            if (ItemHandler.IsArrow(client.Equipment.TryGetItem(5).ID))
                            {
                                Database.ConquerItemTable.UpdateDurabilityItem(client.Equipment.TryGetItem(5));
                            }
                        }
                    }
                    #endregion
                    client.XPCountStamp = Now;
                    client.XPCount++;
                    if (client.XPCount >= 100)
                    {
                        client.XPCount = 0;
                        if (client.Entity.InAutoHunt) return;
                        client.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
                        client.XPListStamp = Now;
                    }
                }
            }
            #endregion
            #region KoSpell
            if (client.Entity.OnKOSpell())
            {
                if (client.Entity.OnCyclone())
                {
                    int Seconds = Now.AllSeconds() - client.Entity.CycloneStamp.AddSeconds(client.Entity.CycloneTime).AllSeconds();
                    if (Seconds >= 1)
                    {
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Cyclone);
                    }
                }
                if (client.Entity.OnSuperman())
                {
                    int Seconds = Now.AllSeconds() - client.Entity.SupermanStamp.AddSeconds(client.Entity.SupermanTime).AllSeconds();
                    if (Seconds >= 1)
                    {
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Superman);
                    }
                }
                if (client.Entity.OnSuperCyclone())
                {
                    int Seconds = Now.AllSeconds() - client.Entity.SuperCycloneStamp.AddSeconds(client.Entity.SuperCycloneTime).AllSeconds();
                    if (Seconds >= 1)
                    {
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.SuperCyclone);
                    }
                }
                if (client.Entity.OnDragonCyclone())
                {
                    int Seconds = Now.AllSeconds() - client.Entity.DragonCycloneStamp.AddSeconds(client.Entity.DragonCycloneTime).AllSeconds();
                    if (Seconds >= 1)
                    {
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.DragonCyclone);
                    }
                }
                ushort completedKo = client.Entity.ResetExpiredKo();
                if (completedKo > 0) Database.KoBoard.New(client.Entity.Name, completedKo);
                if (client.Entity.OnOmnipotence())
                {
                    int Seconds = Now.AllSeconds() - client.Entity.OmnipotenceStamp.AddSeconds(client.Entity.OmnipotenceTime).AllSeconds();
                    if (Seconds >= 1)
                    {
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Omnipotence);
                    }
                }
            }
            #endregion
            #region ToxicFog
            if (!Game.Features.TeamArenaPoison.Tick(client, Now) && client.Entity.ToxicFogLeft > 0)
            {
                if (Now >= client.Entity.ToxicFogStamp.AddSeconds(2))
                {
                    float Percent = client.Entity.ToxicFogPercent;
                    //Percent = Percent / 100 * (client.Player.Detoxication / 100F); Removido por Reparación
                    client.Entity.ToxicFogLeft--;
                    if (client.Entity.ToxicFogLeft == 0)
                    {
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Poisoned); return;
                    }
                    client.Entity.ToxicFogStamp = Now;
                    if (client.Entity.Hitpoints > 1)
                    {
                        uint damage = Game.Attacking.Calculate.Percent(client.Entity, Percent);
                        if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                        {
                            if (damage > client.Entity.AzureShieldDefence)
                            {
                                damage -= client.Entity.AzureShieldDefence;
                                Game.Attacking.Calculate.CreateAzureDmg(client.Entity.AzureShieldDefence, client.Entity, client.Entity);
                                //client.Player.RemoveFlag2((ulong)FlagsUpdate.Flags.AzureShield);
                            }
                            else
                            {
                                Game.Attacking.Calculate.CreateAzureDmg((uint)damage, client.Entity, client.Entity);
                                client.Entity.AzureShieldDefence -= (ushort)damage;
                                client.Entity.AzureShieldPacket();
                                damage = 1;
                            }
                        }
                        else client.Entity.Hitpoints -= damage;
                        SpellUse suse = new SpellUse(true);
                        suse.Attacker = client.Entity.UID;
                        suse.SpellID = 10010;
                        suse.AddTarget(client.Entity.UID, damage, null);
                        client.SendScreen(suse, true);
                        if (client != null)
                            client.UpdateQualifier(client.ArenaStatistic.PlayWith, client, damage);
                    }
                }
            }
            #endregion
            #region Transformations
            if (client.Entity.Transformed)
            {
                if (Now > client.Entity.TransformationStamp.AddSeconds(client.Entity.TransformationTime))
                {
                    client.Entity.Untransform();
                }
            }
            #endregion
            #region FreezeSmall
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FreezeSmall))
            {
                if (client.RaceFrightened)
                {
                    if (Now > client.FrightenStamp.AddSeconds(20))
                    {
                        client.RaceFrightened = false;
                        {
                            GameCharacterUpdates update = new GameCharacterUpdates(true);
                            update.UID = client.Entity.UID;
                            update.Remove(GameCharacterUpdates.Flustered);
                            client.SendScreen(update, true);
                        }
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.FreezeSmall);
                    }
                    else
                    {
                        int rand;
                        ushort x, y;
                        do
                        {
                            rand = Kernel.Random.Next(Game.Map.XDir.Length);
                            x = (ushort)(client.Entity.X + Game.Map.XDir[rand]);
                            y = (ushort)(client.Entity.Y + Game.Map.YDir[rand]);
                        }
                        while (!client.Map.Floor[x, y, MapObjectType.Player]);
                        client.Entity.Facing = Kernel.GetAngle(client.Entity.X, client.Entity.Y, x, y);
                        client.Entity.X = x;
                        client.Entity.Y = y;
                        client.SendScreen(new TwoMovements()
                        {
                            EntityCount = 1,
                            Facing = client.Entity.Facing,
                            FirstEntity = client.Entity.UID,
                            WalkType = 9,
                            X = client.Entity.X,
                            Y = client.Entity.Y,
                            MovementType = TwoMovements.Walk
                        }, true);
                    }
                }
            }
            #endregion
            #region Stunned
            if (client.Entity.Stunned)
            {
                if (Now > client.Entity.StunStamp.AddMilliseconds(2000))
                {
                    client.Entity.Stunned = false;
                }
            }
            #endregion
            #region Freeze
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Freeze))
            {
                if (Now > client.Entity.FrozenStamp.AddSeconds(client.Entity.FrozenTime))
                {
                    client.Entity.FrozenTime = 0;
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Freeze);
                    GameCharacterUpdates update = new GameCharacterUpdates(true);
                    update.UID = client.Entity.UID;
                    update.Remove(GameCharacterUpdates.Freeze);
                    client.SendScreen(update, true);
                }
            }
            #endregion
            #region Stun
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Stun))
            {
                if (client.RaceDizzy)
                {
                    if (Now > client.DizzyStamp.AddSeconds(5))
                    {
                        client.RaceDizzy = false;
                        {
                            GameCharacterUpdates update = new GameCharacterUpdates(true);
                            update.UID = client.Entity.UID;
                            update.Remove(GameCharacterUpdates.Dizzy);
                            client.SendScreen(update);
                        }
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Stun);
                    }
                }
            }
            #endregion
            #region Mentor
            client.ReviewMentor();
            #endregion
            #region ChaosCycle
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ChaosCycle))
            {
                if (Now > client.FrightenStamp.AddSeconds(5))
                {
                    client.RaceFrightened = false;
                    {
                        GameCharacterUpdates update = new GameCharacterUpdates(true);
                        update.UID = client.Entity.UID;
                        update.Remove(GameCharacterUpdates.Flustered);
                        client.SendScreen(update);
                    }
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.ChaosCycle);
                }
            }
            #endregion
            #region IceBlock
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FreezeSmall))
            {
                if (Now > client.FrightenStamp.AddSeconds(client.Entity.Fright))
                {
                    GameCharacterUpdates update = new GameCharacterUpdates(true);
                    update.UID = client.Entity.UID;
                    update.Remove(GameCharacterUpdates.Dizzy);
                    client.SendScreen(update, true);
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.FreezeSmall);
                }
                else
                {
                    int rand;
                    ushort x, y;
                    do
                    {
                        rand = Kernel.Random.Next(Game.Map.XDir.Length);
                        x = (ushort)(client.Entity.X + Game.Map.XDir[rand]);
                        y = (ushort)(client.Entity.Y + Game.Map.YDir[rand]);
                    }
                    while (!client.Map.Floor[x, y, MapObjectType.Player]);
                    client.Entity.Facing = Kernel.GetAngle(client.Entity.X, client.Entity.Y, x, y);
                    client.Entity.X = x;
                    client.Entity.Y = y;
                    client.SendScreen(new TwoMovements()
                    {
                        EntityCount = 1,
                        Facing = client.Entity.Facing,
                        FirstEntity = client.Entity.UID,
                        WalkType = 9,
                        X = client.Entity.X,
                        Y = client.Entity.Y,
                        MovementType = TwoMovements.Walk
                    }, true);
                }
            }
            #endregion
            #region GodlyShield
            BloodshedSeaBoosts.ExpireSharedShield(client);
            #endregion
            #region OrangeSparkles
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.OrangeSparkles))
            {
                if (Time32.Now > client.RaceExcitementStamp.AddSeconds(15))
                {
                    var upd = new GameCharacterUpdates(true)
                    {
                        UID = client.Entity.UID
                    };
                    upd.Remove(GameCharacterUpdates.Accelerated);
                    client.SendScreen(upd);
                    client.SpeedChange = null;
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.SpeedIncreased);
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.OrangeSparkles);
                }
            }
            #endregion
            #region PurpleSparkles
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PurpleSparkles))
            {
                if (Time32.Now > client.DecelerateStamp.AddSeconds(10))
                {
                    {
                        client.RaceDecelerated = false;
                        var upd = new GameCharacterUpdates(true)
                        {
                            UID = client.Entity.UID
                        };
                        upd.Remove(GameCharacterUpdates.Decelerated);
                        client.SendScreen(upd);
                        client.SpeedChange = null;
                    }
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.PurpleSparkles);
                }
            }
            #endregion
            #region CarryingFlag
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CarryingFlag))
            {
                if (Time32.Now > client.Entity.FlagStamp.AddSeconds(60))
                {
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.CarryingFlag);
                }
            }
            #endregion
            #region DragonFury
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.DragonFury))
            {
                if (Time32.Now > client.Entity.DragonFuryStamp.AddSeconds(client.Entity.DragonFuryTime))
                {
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.DragonFury);
                    FlagsUpdate upgrade = new FlagsUpdate(true);
                    upgrade.UID = client.Entity.UID;
                    upgrade.Append((byte)FlagsUpdate.DataType.DragonFury, 0, 0, 0, 0);
                    client.Send(upgrade.ToArray());
                }
            }
            #endregion
            #region DragonFlow
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.DragonFlow))
            {
                if (Time32.Now > client.Entity.DragonFlowStamp.AddSeconds(8))
                {
                    int stamina = 0;
                    if (client.Entity.HeavenBlessing > 0) stamina = 50;
                    var spell = client.Spells[12270];
                    if (spell != null)
                    {
                        if (client.Entity.Stamina != 100 + stamina)
                        {
                            var Spell = Database.SpellTable.GetSpell(spell.ID, spell.Level);
                            client.Entity.Update(10, "leedragonblood", true);
                            client.Entity.Stamina += (byte)Spell.Power;
                        }
                    }
                    client.Entity.DragonFlowStamp = Time32.Now;
                }
            }
            #endregion
            #region Bloodshed~Sea item's
            BloodshedSeaBoosts.Tick(client);
            #endregion
            #region TeamAura
            if (!client.TeamAura)
            {
                if (client.Team != null && !client.Entity.Dead && client.Team.Teammates != null)
                {
                    foreach (var pClient in client.Team.Teammates)
                    {
                        if (client.Entity.UID != pClient.Entity.UID && Kernel.GetDistance(client.Entity.X, client.Entity.Y, pClient.Entity.X, pClient.Entity.Y) <= Constants.pScreenDistance)
                        {
                            if (pClient.Entity.Aura_isActive && pClient.Socket.Alive && pClient.Entity.UID != client.Entity.UID && pClient.Entity.MapID == client.Entity.MapID)
                            {
                                if (pClient.Entity.Aura_actType == (ulong)FlagsUpdate.Flags.TyrantAura ||
                                    pClient.Entity.Aura_actType == (ulong)FlagsUpdate.Flags.FendAura ||
                                    pClient.Entity.Aura_actType == (ulong)FlagsUpdate.Flags.MetalAura ||
                                    pClient.Entity.Aura_actType == (ulong)FlagsUpdate.Flags.WoodAura ||
                                    pClient.Entity.Aura_actType == (ulong)FlagsUpdate.Flags.WaterAura ||
                                    pClient.Entity.Aura_actType == (ulong)FlagsUpdate.Flags.FireAura ||
                                    pClient.Entity.Aura_actType == (ulong)FlagsUpdate.Flags.EarthAura)
                                {
                                    client.TeamAura = true;
                                    client.TeamAuraOwner = pClient;
                                    client.TeamAuraStatusFlag = pClient.Entity.Aura_actType;
                                    client.TeamAuraPower = pClient.Entity.Aura_actPower;
                                    client.Entity.AddFlag(client.TeamAuraStatusFlag, StatusFlagsBigVector32.PermanentFlag, true);
                                    client.doAuraBonuses(client.TeamAuraStatusFlag, client.TeamAuraPower, 1);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            #endregion
            #region TeamMemberPos
            if (client.Team != null)
            {
                if (client.Entity.MapID == client.Team.Lider.Entity.MapID)
                {
                    Data Data = new Data(true);
                    Data.UID = client.Team.Lider.Entity.UID;
                    Data.dwParam = client.Team.Lider.Entity.MapID;
                    Data.ID = Data.TeamMemberPos;
                    Data.wParam1 = client.Team.Lider.Entity.X;
                    Data.wParam2 = client.Team.Lider.Entity.Y;
                    Data.Send(client);
                }
            }
            #endregion

        }
        private void CharactersCallback(GameState client, int time)
        {
            if (!Valid(client)) return;
            //Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize();
            Time32 Now32 = new Time32(time);
            DateTime Now64 = DateTime.Now;
            TitleRequirements.Tick(client);
            ArenaRewardDelivery.Tick(client);
            ClanWarResults.Tick(client);
            if (client.Entity != null && client.Entity.PersistenceRequiresReload) return;
            if (client.Entity != null && client.Entity.FullyLoaded && Now32 > client.LastEntitySave.AddSeconds(GameState.HeartbeatSaveSeconds))
            {
                client.LastEntitySave = Now32;
                client.HeartbeatSave();
            }
            #region bless fixeado
            if (client.Entity.ItemBless > 64)
            {
                client.Entity.ItemBless = 64;
            }
            #endregion
            #region SearingTouch
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran) || client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran02) || client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran03) || client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran04))
            {
                Game.Features.TeamArenaFlame.Expire(client, Now32);
            }
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran04))
            {
                if (!Constants.PKForbiddenMaps.Contains(client.Entity.MapID))
                {
                    foreach (var ClientObj in client.Screen.Objects)
                    {
                        if (ClientObj != null)
                        {
                            if (ClientObj.MapObjType == Game.MapObjectType.Player)
                            {
                                var attacked = ClientObj.Owner;
                                if (Game.Features.TeamArenaFlame.Pulse(client, attacked, Now32)) continue;
                                if (client.Entity.FlameOwner == null) continue;
                                if (Game.Attacking.Handle.CanAttack(client.Entity.FlameOwner.Entity, attacked.Entity, null, false))
                                {
                                    short dist = Kernel.GetDistance(client.Entity.X, client.Entity.Y, attacked.Entity.X, attacked.Entity.Y);
                                    if (dist <= 10)
                                    {
                                        using(var health=Revora.Game.Features.HealthMutationLease.Acquire(attacked.Entity,null)){
                                            if(!health.Allowed)continue;
                                        if (attacked.Entity.Hitpoints > 2000)
                                        {
                                            attacked.Entity.Hitpoints -= 2000;
                                            attacked.Entity.SendSysMesage("Your hitpoints droped by 2000 duto the flame on you");
                                        }
                                        else
                                        {
                                            attacked.Entity.Hitpoints = 1;
                                            attacked.Entity.SendSysMesage("Your hitpoints droped to 1 duto the flame on you");
                                        }
                                        }
                                    }
                                }
                            }
                        }

                    }
                }
            }
            #endregion
            #region EventoGMQuest
            if (false)
            {
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 00)
            {
                if (Server.Carnaval == 0)
                {
                    Game.EventoGMQuest.Load();
                    Server.Carnaval = 10;

                }
            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 01)
            {
                if (Server.Carnaval == 10)
                {

                    Server.Carnaval = 0;
                }
                Kernel.SendWorldMessage(new Message("[GM] The Free CPs event has begun. Hurry to Dragon City for the gifts.", System.Drawing.Color.Black, Message.Center), Server.Values);
            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 03)
            {

                Game.EventoGMQuest.Load2();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 06)
            {

                Game.EventoGMQuest.Load3();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 09)
            {

                Game.EventoGMQuest.Load4();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 12)
            {

                Game.EventoGMQuest.Load5();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 15)
            {

                Game.EventoGMQuest.Load6();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 18)
            {

                Game.EventoGMQuest.Load7();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 21)
            {

                Game.EventoGMQuest.Load8();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 24)
            {

                Game.EventoGMQuest.Load9();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 27)
            {

                Game.EventoGMQuest.Load10();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 30)
            {

                Game.EventoGMQuest.Load11();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 34)
            {

                Game.EventoGMQuest.Load12();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 38)
            {

                Game.EventoGMQuest.Load13();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 42)
            {

                Game.EventoGMQuest.Load14();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 45)
            {

                Game.EventoGMQuest.Load15();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 48)
            {

                Game.EventoGMQuest.Load16();

            }
            if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 52)
            {

                Game.EventoGMQuest.Load17();
                Kernel.SendWorldMessage(new Message("[GM] The Free CPs event in Dragon City has ended. It returns within one hour.", System.Drawing.Color.Black, Message.Center), Server.Values);

            }
            if (DateTime.Now.Hour == 12 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 40)
            {
                if (Server.Carnaval2 == 10)
                {
                    //
                    Server.Carnaval2 = 0;
                }
            }
            if (DateTime.Now.Hour == 12 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 45)
            {
                if (Server.Carnaval2 == 0)
                {
                    Server.Carnaval2 = 10;
                }
            }
            if (DateTime.Now.Hour == 23 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 40)
            {
                if (Server.Carnaval3 == 10)
                {

                    Server.Carnaval3 = 0;
                }
            }
            if (DateTime.Now.Hour == 23 && DateTime.Now.Minute == 00 && DateTime.Now.Second == 45)
            {
                if (Server.Carnaval3 == 0)
                {
                    Server.Carnaval3 = 10;
                }
            }
            }
            #endregion  
            #region Controlador de PuntosDeAtributos Slayer
            bool gmStats = client.Account != null
                && (client.Account.State == AccountTable.AccountState.GameMaster
                    || client.Account.State == AccountTable.AccountState.Administrador);
            if (!gmStats && client.Entity.Agility > 100 && client.Entity.Level >= 140 && client.Entity.Reborn >= 2) //todas las clases
            {
                if (client.Entity.Class >= 10 && client.Entity.Class <= 165)//Todas las clases.
                {
                    client.MessageBox("You cannot have more than 100 Points in agility");
                    client.Entity.Atributes = 538;
                    client.Entity.Vitality = 0;
                    client.Entity.Spirit = 0;
                    client.Entity.Agility = 0;
                    client.Entity.Strength = 0;
                    client.Disconnect();
                }

            }

            if (!gmStats && client.Entity.Atributes >= 538 && client.Entity.Vitality > 0 && client.Entity.Level >= 140)
            {
                client.MessageBox("You have passed the blood point limit"); client.Entity.Atributes = 538;
                client.Entity.Vitality = 0;
                client.Entity.Spirit = 0;
                client.Entity.Agility = 0;
                client.Entity.Strength = 0;
                client.Disconnect();
            }
            if (!gmStats && client.Entity.Atributes >= 538 && client.Entity.Spirit > 0 && client.Entity.Level >= 140)
            {
                client.MessageBox("You have passed the limit of points in spirit"); 
                client.Entity.Atributes = 538;
                client.Entity.Vitality = 0;
                client.Entity.Spirit = 0;
                client.Entity.Agility = 0;
                client.Entity.Strength = 0;
                client.Disconnect();
            }
            if (!gmStats && client.Entity.Atributes >= 538 && client.Entity.Agility > 0 && client.Entity.Level >= 140)
            {
                client.MessageBox("You have passed the Agility point limit"); client.Entity.Atributes = 538;
                client.Entity.Vitality = 0;
                client.Entity.Spirit = 0;
                client.Entity.Agility = 0;
                client.Entity.Strength = 0;
                client.Disconnect();
            }
            if (!gmStats && client.Entity.Atributes >= 538 && client.Entity.Strength > 0 && client.Entity.Level >= 140)
            {
                client.MessageBox("You have passed the strength point limit"); 
                client.Entity.Atributes = 538;
                client.Entity.Vitality = 0;
                client.Entity.Spirit = 0;
                client.Entity.Agility = 0;
                client.Entity.Strength = 0;
                client.Disconnect();
            }
            if (!gmStats && client.Entity.Vitality > 538 && client.Entity.Level >= 140)
            {
                client.MessageBox("You have passed the point limit"); 
                client.Entity.Atributes = 538;
                client.Entity.Vitality = 0;
                client.Entity.Spirit = 0;
                client.Entity.Agility = 0;
                client.Entity.Strength = 0;
                client.Disconnect();
            }
            if (!gmStats && client.Entity.Spirit > 538 && client.Entity.Level >= 140)
            {
                client.MessageBox("You have passed the point limit"); 
                client.Entity.Atributes = 538;
                client.Entity.Vitality = 0;
                client.Entity.Spirit = 0;
                client.Entity.Agility = 0;
                client.Entity.Strength = 0;
                client.Disconnect();
            }
            if (!gmStats && client.Entity.Agility > 538 && client.Entity.Level >= 140)
            {
                client.MessageBox("You have passed the point limit"); 
                client.Entity.Atributes = 538;
                client.Entity.Vitality = 0;
                client.Entity.Spirit = 0;
                client.Entity.Agility = 0;
                client.Entity.Strength = 0;
                client.Disconnect();
            }
            if (!gmStats && client.Entity.Strength > 538 && client.Entity.Level >= 140)
            {
                client.MessageBox("You have passed the point limit"); 
                client.Entity.Atributes = 538;
                client.Entity.Vitality = 0;
                client.Entity.Spirit = 0;
                client.Entity.Agility = 0;
                client.Entity.Strength = 0;
                client.Disconnect();
            }

            #endregion
            #region GoldenTree
            if (Kernel.GoldenTree != null)
            {
                var gTime = Kernel.GoldenTree.StartTime;
                var blossomTime = gTime.AddSeconds(-5);
                if (blossomTime.Day == DateTime.Now.Day && blossomTime.Hour == DateTime.Now.Hour && blossomTime.Minute == DateTime.Now.Minute && blossomTime.Second == DateTime.Now.Second)
                {
                    new MsgGlobalLottery().Ready(client);
                }
                else if (gTime.Day == DateTime.Now.Day && gTime.Hour == DateTime.Now.Hour && gTime.Minute == DateTime.Now.Minute && gTime.Second == DateTime.Now.Second)
                {
                    new MsgGlobalLottery().Blossom(client);
                }
                else if (gTime.Day == DateTime.Now.Day && (gTime.Hour + 2) == DateTime.Now.Hour && gTime.Minute == DateTime.Now.Minute && gTime.Second == DateTime.Now.Second)
                {
                    new MsgGlobalLottery().End(client);
                }
            }
            #endregion
            #region Apuesta Cps por profesion
            #region Zona Troyano
            if (ZonaTroyano == false)
            {
                int entered1 = 0;
                foreach (Client.GameState Player in Kernel.GamePool.Values)
                {
                    if (Player.Entity.MapID == 4573 && (!Player.Entity.Dead))
                    {
                        entered1++;
                    }
                }
                if (entered1 > 1)
                {
                    ZonaTroyano = true;
                }
                else if (entered1 == 1)
                {
                    foreach (Client.GameState Player in Kernel.GamePool.Values)
                    {
                        if (Player.Entity.MapID == 4573 && (!Player.Entity.Dead))
                        {
                            if (Time32.Now > Player.Entity.EsperaPelea.AddSeconds(999999))
                            {
                                Player.Entity.AddConquerPoints(ZonaTroyanoPremio);
                                ZonaTroyanoPremio = 0;
                                Player.Entity.Teleport(1002, 318, 192);
                            }
                        }
                    }
                }
            }
            else
            {
                int alive1 = 0;
                foreach (Client.GameState Player in Kernel.GamePool.Values)
                {
                    if (Player.Entity.MapID == 4573 && (!Player.Entity.Dead))
                    {
                        alive1++;
                    }
                }
                if (alive1 == 1)
                {
                    foreach (Client.GameState Player in Kernel.GamePool.Values)
                    {
                        if (Player.Entity.MapID == 4573)
                        {
                            if (!Player.Entity.Dead)//winner
                            {
                                Player.Entity.Teleport(1002, 318, 192);
                                Player.Entity.AddConquerPoints(ZonaTroyanoPremio * 2);
                                Player.Entity.EsperaPelea = Time32.Now;
                                ZonaTroyano = false;
                                Kernel.SendWorldMessage(new Network.GamePackets.Message(string.Concat(new object[] { "Congratulations! " + Player.Entity.Name + " has won " + ZonaTroyanoPremio * 2 + " CPs in Trojan Zone." }), System.Drawing.Color.Black, 0x7db), Server.GamePool);
                                ZonaTroyanoPremio = 0;
                                _String str = new _String(true)
                                {
                                    UID = Player.Entity.UID,
                                    TextsCount = 1,
                                    Type = 10
                                };
                                str.Texts.Add("sports_victory");
                                Player.SendScreen(str, true);
                                Player.Entity.WinnerWaiting = Time32.Now;
                                Player.Entity.UnGanador = true;
                            }
                            else//loser
                            {
                                Player.Entity.Teleport(1002, 318, 192);

                                _String str = new _String(true)
                                {
                                    UID = Player.Entity.UID,
                                    TextsCount = 1,
                                    Type = 10
                                };
                                str.Texts.Add("sports_failure");
                                Player.SendScreen(str, true);
                                Player.Entity.Action = Game.Enums.ConquerAction.None;
                                Player.ReviveStamp = Time32.Now;
                                Player.Attackable = false;
                                Player.Entity.TransformationID = 0;
                                Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
                                Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
                                Player.Entity.Hitpoints = Player.Entity.MaxHitpoints;
                                Player.Entity.Ressurect();
                            }
                        }
                    }
                }
            }
            #endregion
            #region Zona Ninja
            if (ZonaNinja == false)
            {
                int entered2 = 0;
                foreach (Client.GameState Player in Kernel.GamePool.Values)
                {
                    if (Player.Entity.MapID == 4574 && (!Player.Entity.Dead))
                    {
                        entered2++;
                    }
                }
                if (entered2 > 1)
                {
                    ZonaNinja = true;
                }
                else if (entered2 == 1)
                {
                    foreach (Client.GameState Player in Kernel.GamePool.Values)
                    {
                        if (Player.Entity.MapID == 4574 && (!Player.Entity.Dead))
                        {
                            if (Time32.Now > Player.Entity.EsperaPelea.AddSeconds(999999))
                            {
                                Player.Entity.AddConquerPoints(ZonaNinjaPremio);
                                ZonaNinjaPremio = 0;
                                Player.Entity.Teleport(1002, 318, 192);
                            }
                        }
                    }
                }
            }
            else
            {
                int alive2 = 0;
                foreach (Client.GameState Player in Kernel.GamePool.Values)
                {
                    if (Player.Entity.MapID == 4574 && (!Player.Entity.Dead))
                    {
                        alive2++;
                    }
                }
                if (alive2 == 1)
                {
                    foreach (Client.GameState Player in Kernel.GamePool.Values)
                    {
                        if (Player.Entity.MapID == 4574)
                        {
                            if (!Player.Entity.Dead)//winner
                            {
                                Player.Entity.AddConquerPoints(ZonaNinjaPremio * 2);
                                Player.Entity.EsperaPelea = Time32.Now;
                                ZonaNinja = false;
                                Player.Entity.Teleport(1002, 318, 192);
                                Kernel.SendWorldMessage(new Network.GamePackets.Message(string.Concat(new object[] { "Congratulations! ", Player.Entity.Name, " has won ", ZonaNinjaPremio * 2, " CPs in Ninja Zone." }), System.Drawing.Color.Black, 0x7db), Server.GamePool);
                                ZonaNinjaPremio = 0;
                                _String str = new _String(true)
                                {
                                    UID = Player.Entity.UID,
                                    TextsCount = 1,
                                    Type = 10
                                };
                                str.Texts.Add("sports_victory");
                                Player.SendScreen(str, true);
                                Player.Entity.WinnerWaiting = Time32.Now;
                                Player.Entity.UnGanador = true;
                            }
                            else//loser
                            {
                                Player.Entity.Teleport(1002, 318, 192);
                                _String str = new _String(true)
                                {
                                    UID = Player.Entity.UID,
                                    TextsCount = 1,
                                    Type = 10
                                };
                                str.Texts.Add("sports_failure");
                                Player.SendScreen(str, true);
                                Player.Entity.Action = Game.Enums.ConquerAction.None;
                                Player.ReviveStamp = Time32.Now;
                                Player.Attackable = false;
                                Player.Entity.TransformationID = 0;
                                Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
                                Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
                                Player.Entity.Hitpoints = Player.Entity.MaxHitpoints;
                                Player.Entity.Ressurect();
                            }
                        }
                    }
                }
            }
            #endregion
            #region Zona Monjes
            if (ZonaMonje == false)
            {
                int entered3 = 0;
                foreach (Client.GameState Player in Kernel.GamePool.Values)
                {
                    if (Player.Entity.MapID == 4575 && (!Player.Entity.Dead))
                    {
                        entered3++;
                    }
                }
                if (entered3 > 1)
                {
                    ZonaMonje = true;
                }
                else if (entered3 == 1)
                {
                    foreach (Client.GameState Player in Kernel.GamePool.Values)
                    {
                        if (Player.Entity.MapID == 4575 && (!Player.Entity.Dead))
                        {
                            if (Time32.Now > Player.Entity.EsperaPelea.AddSeconds(999999))
                            {
                                Player.Entity.AddConquerPoints(ZonaMonjePremio);
                                ZonaMonjePremio = 0;
                                Player.Entity.Teleport(1002, 318, 192);
                            }
                        }
                    }
                }
            }
            else
            {
                int alive3 = 0;
                foreach (Client.GameState Player in Kernel.GamePool.Values)
                {
                    if (Player.Entity.MapID == 4575 && (!Player.Entity.Dead))
                    {
                        alive3++;
                    }
                }
                if (alive3 == 1)
                {
                    foreach (Client.GameState Player in Kernel.GamePool.Values)
                    {
                        if (Player.Entity.MapID == 4575)
                        {
                            if (!Player.Entity.Dead)//winner
                            {
                                Player.Entity.AddConquerPoints(ZonaMonjePremio * 2);
                                Player.Entity.EsperaPelea = Time32.Now;
                                ZonaMonje = false;
                                Player.Entity.Teleport(1002, 318, 192);
                                Kernel.SendWorldMessage(new Network.GamePackets.Message(string.Concat(new object[] { "Congratulations! ", Player.Entity.Name, " has won ", ZonaMonjePremio * 2, " CPs in Monks Zone." }), System.Drawing.Color.Black, 0x7db), Server.GamePool);
                                ZonaMonjePremio = 0;
                                _String str = new _String(true)
                                {
                                    UID = Player.Entity.UID,
                                    TextsCount = 1,
                                    Type = 10
                                };
                                str.Texts.Add("sports_victory");
                                Player.SendScreen(str, true);
                                Player.Entity.WinnerWaiting = Time32.Now;
                                Player.Entity.UnGanador = true;
                            }
                            else//loser
                            {
                                Player.Entity.Teleport(1002, 318, 192);
                                _String str = new _String(true)
                                {
                                    UID = Player.Entity.UID,
                                    TextsCount = 1,
                                    Type = 10
                                };
                                str.Texts.Add("sports_failure");
                                Player.SendScreen(str, true);
                                Player.Entity.Action = Game.Enums.ConquerAction.None;
                                Player.ReviveStamp = Time32.Now;
                                Player.Attackable = false;
                                Player.Entity.TransformationID = 0;
                                Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
                                Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
                                Player.Entity.Hitpoints = Player.Entity.MaxHitpoints;
                                Player.Entity.Ressurect();
                            }
                        }
                    }
                }
            }
            #endregion
            #region ApuestaCps
            if (client.Entity.UnGanador == true)
            {
                {
                    switch (client.Entity.MapID)
                    {
                        case 4573://ZonaTroyano
                            {
                                ZonaTroyano = false;
                                break;
                            }
                        case 4574://ZonaNinja
                            {
                                ZonaNinja = false;
                                break;
                            }
                        case 4575://room 3
                            {
                                ZonaMonje = false;
                                break;
                            }
                        case 4576://room 4
                            {
                                Room4 = false;
                                break;
                            }
                        case 4577://room 5
                            {
                                Room5 = false;
                                break;
                            }
                        case 4578://room 6
                            {
                                Room6 = false;
                                break;
                            }
                    }
                }
            }
            #endregion
            #endregion
            #region Titles
            if (client.Entity.Titles.Count > 0)
            {
                foreach (var titles in client.Entity.Titles)
                {
                    if (Now64 > titles.Value)
                    {
                        client.Entity.Titles.Remove(titles.Key);
                        if (client.Entity.MyTitle == titles.Key)
                            client.Entity.MyTitle = TitlePacket.Titles.None;
                        client.Entity.RemoveTopStatus((UInt64)titles.Key);
                    }
                }
            }
            #endregion
            #region Die Delay
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Dead) && !client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ghost))
            {
                if (Now32 > client.Entity.DeathStamp.AddSeconds(2))
                {
                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.Ghost,StatusFlagsBigVector32.PermanentFlag,false);
                    if (client.Entity.Body % 10 < 3)
                        client.Entity.TransformationID = 99;
                    else client.Entity.TransformationID = 98;
                    client.SendScreenSpawn(client.Entity, true);
                    client.Entity.SendSpawn(client, true);
                }
            }
            #endregion
            #region MagicDefender
            MagicDefenderProtection.Refresh(client,Now32);
            WarriorStance.Tick(client,Now32);
            #endregion
            #region GuildRequest
            if (Now32 > client.Entity.GuildRequest.AddSeconds(30))
            {
                client.GuildJoinTarget = 0;
            }
            #endregion
            #region EnlightenPoints
            if (client.Entity.EnlightenPoints >= 100)
            {
                client.Entity.Update((byte)FlagsUpdate.DataType.EnlightPoints, client.Entity.EnlightenPoints, true);
            }
            else if ((client.Entity.EnlightenPoints < 100) && client.Entity.ContainsFlag((byte)FlagsUpdate.DataType.EnlightPoints))
            {
                client.Entity.RemoveFlag((byte)FlagsUpdate.DataType.EnlightPoints);
            }
            #endregion
            
            


            #region Extra treasure points[puntosVIP]
            if (Time32.Now > client.LastTreasurePoints.AddMinutes(1))
                //{
                //    if (client.Player.MapID == 1002)
                //        client.Player.TreasuerPoints += 2;

                //    client.Player.TreasuerPoints++;
                //    client.LastTreasurePoints = Time32.Now;
                //}
            #endregion
                #region OnlineTraining
                if (client.Entity.HeavenBlessing > 0)
                {
                    if (Now32 > client.LastTrainingPointsUp.AddMinutes(1))
                    {
                        client.OnlineTrainingPoints += 3;
                        if (client.OnlineTrainingPoints >= 30)
                        {
                            client.OnlineTrainingPoints -= 30;
                            client.Entity.OnlineTraining += 2;
                            client.Entity.Update((byte)FlagsUpdate.DataType.OnlineTraining, FlagsUpdate.OnlineTraining.ReceiveExperience, 0);
                        }
                        client.LastTrainingPointsUp = Now32;
                        client.Entity.Update((byte)FlagsUpdate.DataType.OnlineTraining, FlagsUpdate.OnlineTraining.IncreasePoints, 0);
                    }
                }
                #endregion
            #region Minning
            if (client.Mining)
                Game.ConquerStructures.Mining.Mine(client);
            #endregion
            #region Efecto Premium
            if (client.Account.State == AccountTable.AccountState.Premium)
            {
                client.Entity.AddFlag((ulong)FlagsUpdate.Flags.BlueBall,StatusFlagsBigVector32.PermanentFlag,false);
            }
            #endregion
            #region Efecto VIP
            if (client.Entity.VIPLevel == 9)
            {
                client.Entity.AddFlag((ulong)FlagsUpdate.Flags.PurpleBall, StatusFlagsBigVector32.PermanentFlag, false);
            }
            #endregion
            #region Attackable
            if (client.JustLoggedOn)
            {
                client.JustLoggedOn = false;
                client.ReviveStamp = Now32;
            }
            if (!client.Attackable)
            {
                if (Now32 > client.ReviveStamp.AddSeconds(5))
                {
                    client.Attackable = true;
                }
            }
            #endregion
            #region TimeDropSlayer drop[VIP]
            client.Entity.TickTimeDropSlayer(Now32);
            #endregion
            #region DoubleExperience
            client.Entity.TickDoubleExperience(Now32);
            #endregion
            #region ExpProtectionTime
            client.Entity.TickExpProtection(Now32);
            #endregion
            #region HeavenBlessing
            if (client.Entity.HeavenBlessing > 0)
            {
                if (Now32 > client.Entity.HeavenBlessingStamp.AddMilliseconds(1000))
                {
                    client.Entity.HeavenBlessingStamp = Now32;
                    client.Entity.TickHeavenBlessing();
                }
            }
            #endregion
            #region Enlightment
            EnlightenmentProgress.Tick(client);
            #endregion
            #region Estar en linea en slayer
            if (Revora.Config.OnlineConquerPointsEnabled && client.Entity.Level >= 140 && client.Entity.Reborn == 2 && client.Entity.VIPLevel <= 6)
            {
                if (!client.Entity.Dead)
                    if (Time32.Now > client.Entity.CPSgold.AddMinutes(1))
                    {
                        uint cps = Revora.Config.OnlineConquerPointsPerMinute;
                        if (Now64.Hour >= 12) cps += Revora.Config.OnlineConquerPointsAfternoonBonus;
                        client.Entity.CPSgold = Time32.Now;
                        client.Entity.AddConquerPoints(cps);
                    }
            }
            #endregion
            #region Estar en linea en slayer
            if (Revora.Config.OnlineConquerPointsEnabled && client.Entity.Level >= 140 && client.Entity.Reborn == 2 && client.Entity.VIPLevel >= 7)
            {
                if (!client.Entity.Dead)
                    if (Time32.Now > client.Entity.CPSgold.AddMinutes(1))
                    {
                        uint cps = Revora.Config.OnlineConquerPointsVip7PerMinute;
                        if (Now64.Hour >= 12) cps += Revora.Config.OnlineConquerPointsVip7AfternoonBonus;
                        client.Entity.CPSgold = Time32.Now;
                        client.Entity.AddConquerPoints(cps);
                    }
            }
            #endregion
            #region PKPoints
            lock (client.PkSyncRoot)
            {
            if (Now32 >= client.Entity.PKPointDecreaseStamp.AddMinutes(5))
            {
                client.Entity.PKPointDecreaseStamp = Now32;
                client.Entity.ChangePkPoints(-1);
            }
            }
            #endregion
            #region OverHP
            if (client.Entity.FullyLoaded)
            {
                if (client.Entity.Hitpoints > client.Entity.MaxHitpoints && client.Entity.MaxHitpoints > 1 && !client.Entity.Transformed)
                {
                    client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                }
            }
            #endregion
            #region BeastGuard
            //if (Now64.Hour == 20 && Now64.Minute == 59 && Now64.Second <= 00)
            //{
            //    if (client.Guild != null && client.Guild.PoleKeeper)
            //    {
            //        Game.Entity.SendWorldMessage("You can now GuildLeader let~a~Guild~Beast~out go up now");
            //    }
            //}
            #endregion
            //Perfeccion
            #region Tarea de  Perfeccion
            #region salida
            if (false && (Now64.DayOfWeek == DayOfWeek.Tuesday || Now64.DayOfWeek == DayOfWeek.Thursday || Now64.DayOfWeek == DayOfWeek.Saturday))
            {
                if ((Now64.Hour == 15 || Now64.Hour == 21) && Now64.Minute == 43 && Now64.Second <= 10)
                {
                    Game.Entity.name = new object[] { "The perfection system entry is over!" };
                    Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[Perfection]", System.Drawing.Color.Red, 2500), Server.GamePool);
                    Game.Features.DisCity.Signup = false;
                }
            }
            #endregion
            {
                #region Evento de [Sistema de Perfeccion]
                if (false && (Now64.DayOfWeek == DayOfWeek.Tuesday || Now64.DayOfWeek == DayOfWeek.Thursday || Now64.DayOfWeek == DayOfWeek.Saturday))
                {
                    if ((Now64.Hour == 15 || Now64.Hour == 21) && Now64.Minute == 00 && Now64.Second <= 10)
                    {
                        if (client.Entity.Level >= 110)
                        {
                            Game.Entity.name = new object[] { "The Perfection event has begun. Run to Dragon City and take up the task." }; Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[Perfection]", System.Drawing.Color.Red, 2500), Server.GamePool);
                            Game.Features.DisCity.Signup = true;
                            foreach (var client1 in Server.GamePool)
                            {
                                if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                                    return;
                                if ((Now64.Hour == 15 || Now64.Hour == 21) && Now64.Minute == 00 && Now64.Second <= 20)
                                {

                                    client.MessageBox("The Perfection task is open. Will you go to Dragon City?", p => { p.Entity.Teleport(1002, 297, 152); }, null, 60);
                                }
                            }
                        }
                    }
                    if ((Now64.Hour == 15 || Now64.Hour == 21) && Now64.Minute == 45 && Now64.Second >= 00)
                    {
                        if (client.Entity.MapID == 4020)
                        {
                            Kernel.SendWorldMessage(new Message("Time has run out for the Perfection task. Only those already in stage 2 remain.", System.Drawing.Color.White, Message.Center), Server.GamePool); client.Entity.Teleport(1020, 558, 563);
                            client.Inventory.Add(3009002, 0, 2);
                        }
                    }
                    if ((Now64.Hour == 15 || Now64.Hour == 21) && Now64.Minute == 59 && Now64.Second >= 30)
                    {
                        if (client.Entity.MapID == 4020 || client.Entity.MapID == 40201 || client.Entity.MapID == 40202 || client.Entity.MapID == 40203)
                        {
                            Kernel.SendWorldMessage(new Message("The Perfection event has ended. It returns Tuesday, Thursday, and Saturday at 15:00 and 22:00.", System.Drawing.Color.White, Message.Center), Server.GamePool); client.Entity.Teleport(1020, 558, 563);
                            client.Inventory.Add(3009001, 0, 1);
                        }
                    }
                }
                #endregion
                #region Mapa de Sistema de perfeccion
                //if (!client.Player.Dead)
                if (client.Entity.MapID == 4023)
                    if (client.Entity.Hitpoints <= 0)
                    {//Chavarro
                        Random R = new Random();
                        int G = R.Next(1, 2);
                        if (G == 1) client.Entity.Teleport(4023, 302, 653);
                        if (G == 2) client.Entity.Teleport(4023, 285, 630);

                        client.Send(new Message("Hurry! Ultimate Pluto still holds Perfection Fragments.", System.Drawing.Color.Red, Message.Whisper));
                    }
                #endregion
            }
            #endregion
            #region RemoveTop
            if (client.Guild != null && !client.Guild.PoleKeeper || client.Guild != null && client.AsMember.Rank != Enums.GuildMemberRank.DeputyLeader)
            {
                if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TopDeputyLeader))
                {
                    client.Entity.RemoveTopStatus((ulong)FlagsUpdate.Flags.TopDeputyLeader);
                }
            }
            if (client.Guild != null && !client.Guild.PoleKeeper || client.Guild != null && client.AsMember.Rank != Enums.GuildMemberRank.GuildLeader)
            {
                if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TopGuildLeader))
                {
                    client.Entity.RemoveTopStatus((ulong)FlagsUpdate.Flags.TopGuildLeader);
                }
            }
            #endregion
            #region AutoHunting
            if (client.Entity.AutoRev > 0)
            {
                if (client.Entity.HeavenBlessing > 0 && client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Dead) && client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ghost))
                {
                    if (Time32.Now > client.Entity.AutoRevStamp.AddSeconds(20))
                    {
                        client.Entity.Action = Game.Enums.ConquerAction.None;
                        client.ReviveStamp = Time32.Now;
                        client.Attackable = false;
                        client.Entity.TransformationID = 0;
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
                        client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                        client.Entity.Mana = client.Entity.MaxMana;
                        client.Entity.AutoRev = 0;
                    }
                }
            }
            #endregion
            #region Twin  Effect
            if (client.Entity.MapID == 1002)
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 312, 279) < 17 && !client.Effect)
                {
                    client.Effect = true;
                    if (client.Entity.MapID == 1002)
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ItemID = 1024;
                        floorItem.MapID = 1002;
                        floorItem.X = 312;
                        floorItem.Y = 279;
                        floorItem.Type = Network.GamePackets.FloorItem.Effect;
                        client.Send(floorItem);
                    }
                }
                else
                {
                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 312, 279) > 17)
                    {
                        client.Effect = false;
                    }
                }
            }
            #endregion
            #region beginnerarea
            if (client.Entity.MapID == 1004)
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 52, 50) < 17 && !client.Effect1)
                {
                    client.Effect1 = true;
                    if (client.Entity.MapID == 1004)
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ItemID = 794;
                        floorItem.MapID = 1004;
                        floorItem.X = 52;
                        floorItem.Y = 50;
                        floorItem.Type = Network.GamePackets.FloorItem.Effect;
                        client.Send(floorItem);
                    }
                }
                else
                {
                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 52, 50) > 17)
                    {
                        client.Effect1 = false;
                    }
                }
            }
            if (client.Entity.MapID == 1002)
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 439, 386) < 17 && !client.Effect1)
                {
                    client.Effect1 = true;
                    if (client.Entity.MapID == 1002)
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ItemID = 794;
                        floorItem.MapID = 1002;
                        floorItem.X = 311;
                        floorItem.Y = 286;
                        floorItem.Type = Network.GamePackets.FloorItem.Effect;
                        client.Send(floorItem);
                    }
                }
                else
                {
                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 439, 386) > 17)
                    {
                        client.Effect1 = false;
                    }
                }
            }
            #endregion
            #region market equipment
            if (client.Entity.MapID == 1036)
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 184, 205) < 17 && !client.Effect)
                {
                    client.Effect = true;
                    if (client.Entity.MapID == 1036)
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ItemID = 812;//794 
                        floorItem.MapID = 1036;
                        floorItem.X = 184;
                        floorItem.Y = 205;
                        floorItem.Type = Network.GamePackets.FloorItem.Effect;
                        client.Send(floorItem);
                    }
                }
                else
                {
                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 184, 205) > 17)
                    {
                        client.Effect = false;
                    }
                }
            }
            if (client.Entity.MapID == 1002)
            {

                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 429, 390) < 17 && !client.Effect10)
                {
                    client.Effect10 = true;
                    if (client.Entity.MapID == 1002)
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ItemID = 993;//746 
                        floorItem.MapID = 1002;
                        floorItem.X = 301;
                        floorItem.Y = 290;
                        floorItem.Type = 10;
                        client.Send(floorItem);
                    }
                }
                else
                {
                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 429, 390) > 17)
                    {
                        client.Effect10 = false;
                    }
                }
            }
            if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 429, 368) < 17 && !client.Effect9)
            {
                client.Effect9 = true;
                if (client.Entity.MapID == 1002)
                {
                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                    floorItem.ItemID = 1021;//746 
                    floorItem.MapID = 1002;
                    floorItem.X = 301;
                    floorItem.Y = 268;
                    floorItem.Type = 10;
                    client.Send(floorItem);
                }
            }
            else
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 429, 368) > 17)
                {
                    client.Effect9 = false;
                }
            }

            if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 438, 377) < 17 && !client.Effect8)
            {
                client.Effect8 = true;
                if (client.Entity.MapID == 1002)
                {
                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                    floorItem.ItemID = 1026;//746 
                    floorItem.MapID = 1002;
                    floorItem.X = 310;
                    floorItem.Y = 277;
                    floorItem.Type = 10;
                    client.Send(floorItem);
                }
            }
            else
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 438, 377) > 17)
                {
                    client.Effect8 = false;
                }
            }
            if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 429, 388) < 17 && !client.Effect2)
            {
                client.Effect2 = true;
                if (client.Entity.MapID == 1002)
                {
                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                    floorItem.ItemID = 1021;//746 
                    floorItem.MapID = 1002;
                    floorItem.X = 301;
                    floorItem.Y = 288;
                    floorItem.Type = 10;
                    client.Send(floorItem);
                }
            }
            else
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 429, 388) > 17)
                {
                    client.Effect2 = false;
                }
            }
            if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 450, 368) < 17 && !client.Effect11)
            {
                client.Effect11 = true;
                if (client.Entity.MapID == 1002)
                {
                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                    floorItem.ItemID = 1021;//794 
                    floorItem.MapID = 1002;
                    floorItem.X = 322;
                    floorItem.Y = 268;
                    floorItem.Type = 10;
                    client.Send(floorItem);
                }
            }
            else
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 450, 368) > 17)
                {
                    client.Effect11 = false;
                }
            }
            if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 450, 388) < 17 && !client.Effect4)
            {
                client.Effect4 = true;
                if (client.Entity.MapID == 1002)
                {
                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);

                    floorItem.ItemID = 1021;//794 
                    floorItem.MapID = 1002;
                    floorItem.X = 322;
                    floorItem.Y = 288;
                    floorItem.Type = 10;
                    client.Send(floorItem);
                }
            }
            else
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 450, 388) > 17)
                {
                    client.Effect4 = false;
                }
            }
            if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 436, 444) < 17 && !client.Effect3)
            {
                client.Effect3 = true;
                if (client.Entity.MapID == 1002)
                {
                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                    // floorItem.MapObjType = Game.MapObjectType.Item; 
                    floorItem.ItemID = 765;//794 
                    floorItem.MapID = 1002;
                    floorItem.X = 308;
                    floorItem.Y = 344;
                    floorItem.Type = Network.GamePackets.FloorItem.Effect;
                    client.Send(floorItem);
                }
            }
            else
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, 436, 444) > 17)
                {
                    client.Effect3 = false;
                }
            }
            #endregion
            #region SnowBansheeSoul
            if (Kernel.SpawnBansheeSoul)
            {
                if (Time32.Now > Kernel.SpawnBansheeSoulStamp.AddMinutes(30))
                {
                    Kernel.SpawnBansheeSoul = false;
                }
            }
            #endregion
///توبات الساعه          
            #region OK Diario [xx:15] [xx:17] - [xx:29] [xx:31] - [xx:45] [xx:47]
            if (false && (Now64.Minute == 15 && Now64.Second == 5 || Now64.Minute == 29 && Now64.Second == 5 || Now64.Minute == 45 && Now64.Second == 5))
            {
                client.MessageBox("The Top Player contest has begun. Will you take the field?",
                      (p) => { p.Entity.Teleport(1002, 313, 262); }, null, 60);
            }
            #endregion
           
          
            if (false)
            {
            #region Carrera de caballo
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            
            if ((Now64.Hour == 3 || Now64.Hour == 7 || Now64.Hour == 11 || Now64.Hour == 17 || Now64.Hour == 22) && Now64.Minute == 59 && Now64.Second == 01)
            {

                client.MessageBox("The Twin City stables are calling. Will you ride? The purse is paid in Revora Points.",
                p => { p.Entity.Teleport(1002, 289, 149); }, null, 90);
            }
           
                #endregion
            ///Crosserver
            #region [CrossActivado]MapaMaligno
            //if (client.Player.MapID == 6000 || client.Player.MapID == 6001 || client.Player.MapID == 102 || client.Player.MapID == 6002 || client.Player.MapID == 6003 || client.Player.MapID == 6004)
            //    return;
            //if ((Now64.Hour == 16 && Now64.Minute == 20 && Now64.Second == 10) || (Now64.Hour == 20 && Now64.Minute == 20 && Now64.Second == 10))
            //{
            //    Kernel.SendWorldMessage(new Message("[CrossActivado]MapaMaligno entre servidores esta activado Une tu equipo y lucho por Ser el mejor servidor!", Color.White, Message.Center), Server.GamePool);

            //    client.MessageBox("[Cross]Quieres ir a el MapaMaligno entre servidores?",
            //    p => { p.Player.Teleport(1002, 240, 217); }, null, 60);
            //}
            #endregion
            #region [CrossActivado]SuperPkElite
            //if (client.Player.MapID == 6000 || client.Player.MapID == 6001 || client.Player.MapID == 102 || client.Player.MapID == 6002 || client.Player.MapID == 6003 || client.Player.MapID == 6004)
            //    return;
            //if ((Now64.Hour == 20 && Now64.Minute == 00 && Now64.Second == 50))
            //{
            //    Kernel.SendWorldMessage(new Message("[CrossActivado]SuperPkElite entre servidores esta activado Une tu equipo y lucho por Ser el mejor servidor!", Color.White, Message.Center), Server.GamePool);

            //    client.MessageBox("[Cross]Quieres ir a el SuperPkElite entre servidores?",
            //    p => { p.Player.Teleport(1002, 240, 217); }, null, 60);
            //}
            #endregion
            ////////////7
            #region C-O-N-Q-U-E-R
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if ((Now64.Hour == 07 && Now64.Minute == 02 && Now64.Second == 0) || (Now64.Hour == 13 && Now64.Minute == 02 && Now64.Second == 0) || (Now64.Hour == 19 && Now64.Minute == 02 && Now64.Second == 0) || (Now64.Hour == 01 && Now64.Minute == 02 && Now64.Second == 0))
            {
                Kernel.SendWorldMessage(new Message("The C-O-N-Q-U-E-R tournament has begun! Win PaqSagradaRefinada!", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Do you want to go to the C-O-N-Q-U-E-R tournament?",
                p => { p.Entity.Teleport(1002, 347, 249); }, null, 60);
            }
            #endregion
            #region PkdeEsposos]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.DayOfWeek == DayOfWeek.Friday && Now64.Hour == 18 && Now64.Minute == 00 && Now64.Second <= 1)
            {

                client.MessageBox("The couples' contest has opened in Twin City. Will you stand with your spouse?",
                 p => { p.Entity.Teleport(1002, 696, 520); }, null, 60);
            }
            #endregion
            #region TorneoPkComun]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (!TwinCityPlaza.IsClosed(900032) &&
                ((Now64.Hour == 17 && Now64.Minute == 00 && Now64.Second == 59) || (Now64.Hour == 22 && Now64.Minute == 00 && Now64.Second == 59)))
            {

                client.MessageBox("A common PK melee has broken out. Super weapons and armor are on the line. Will you attend?",
                 p => { p.Entity.Teleport(1002, 276, 296); }, null, 60);
            }
            #endregion
            #region SuperBossDeOro]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.DayOfWeek == DayOfWeek.Sunday && Now64.Hour == 18 && Now64.Minute == 01 && Now64.Second <= 1)
            {

                client.MessageBox("The Golden Super Boss has risen. Its hide is worth a full armory. Will you hunt it?",
                 p => { p.Entity.Teleport(1002, 227, 245); }, null, 60);
            }
            #endregion
            ///// الوحوش الكبيره
            #region SuperBoss


            #region GoldOctopus[xx:00]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 59 && Now64.Second == 59)
            {
                Kernel.SendWorldMessage(new Message("A golden octopus has surfaced in Deep Sea 2 [210,154]. Its hoard is real.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt the Golden Octopus?",
                p => { p.Entity.Teleport(1002, 309, 248); }, null, 60);
                Kernel.SacarPulpo = false;
            }
            #endregion
            #region IceKing[xx:06]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 05 && Now64.Second == 55)
            {
                Kernel.SendWorldMessage(new Message("The Ice King has appeared on Kingdom Map [304-309]! Fine prizes await the hunter.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt the Ice King?",
                p => { p.Entity.Teleport(1002, 309, 248); }, null, 40);
                Kernel.SacarBruja = false;
            }
            #endregion
            #region ChaosRipper[xx:12]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 11 && Now64.Second == 55)
            {
                Kernel.SendWorldMessage(new Message("Chaos Ripper has appeared in Frozen Grotto 5 [349-174]! Fine prizes await the hunter.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt Chaos Ripper?",
                p => { p.Entity.Teleport(1002, 309, 248); }, null, 60);
                Kernel.SacarGuardian = false;
            }
            #endregion
            #region NemesisTyrant[xx:18]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 17 && Now64.Second == 55)
            {
                Kernel.SendWorldMessage(new Message("NemesisTyrant has appeared in Deep Sea [225-102]! Fine prizes await the hunter.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt NemesisTyrant?",
                p => { p.Entity.Teleport(1036, 197, 205); }, null, 30);
                Kernel.SacarNemesis = false;
            }
            #endregion
            #region DragonKing[xx:24]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 23 && Now64.Second == 55)
            {
                Kernel.SendWorldMessage(new Message("Dragon King has appeared in Frozen Grotto 6 [497-424]! Fine prizes await the hunter.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt Dragon King?",
                p => { p.Entity.Teleport(1002, 309, 248); }, null, 60);
                Kernel.SacarDragon = false;
            }
            #endregion
            #region IceQueen[xx:30]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 29 && Now64.Second == 55)
            {
                Kernel.SendWorldMessage(new Message("The Ice Queen has appeared in Frozen Grotto 2 [497-424]! Fine prizes await the hunter.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt the Ice Queen?",
                p => { p.Entity.Teleport(1002, 309, 248); }, null, 60);
                Kernel.SacarReina = false;
            }
            #endregion
            #region FatDemon[xx:36]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 35 && Now64.Second == 55)
            {
                Kernel.SendWorldMessage(new Message("Fat Demon has appeared on Demon Map [041-040]! Fine prizes await the hunter.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt Fat Demon?",
                p => { p.Entity.Teleport(1002, 310, 249); }, null, 60);
                Kernel.SacarGordo = false;
            }
            #endregion
            #region NemesisTyrant[xx:43]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 42 && Now64.Second == 55)
            {
                Kernel.SendWorldMessage(new Message("NemesisTyrant has appeared in Deep Sea [292-352]! Fine prizes await the hunter.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt NemesisTyrant?",
                p => { p.Entity.Teleport(1036, 197, 205); }, null, 30);
                Kernel.SacarNemesis1 = false;
            }
            #endregion
            #region ThunderDragon[xx:49]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 48 && Now64.Second == 55)
            {
                Kernel.SendWorldMessage(new Message("Thunder Dragon has appeared on Kingdom Map [304-309]! Fine prizes await the hunter.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt Thunder Dragon?",
                p => { p.Entity.Teleport(1002, 309, 248); }, null, 40);
                Kernel.SacarTrueno = false;
            }
            #endregion
            #region FuriousBear[xx:56]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.Minute == 55 && Now64.Second == 55)
            {
                Kernel.SendWorldMessage(new Message("Furious Bear has appeared on Kingdom Map [304-309]! Fine prizes await the hunter.", Color.White, Message.Center), Server.GamePool);

                client.MessageBox("Will you hunt Furious Bear?",
                p => { p.Entity.Teleport(1002, 309, 248); }, null, 40);
                Kernel.SacarOso = false;
            }
            #endregion
            #region Great Pheasant
            #region GreatPheasant(Nor.1) [Domingo y Miercoles]
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
            if (Now64.DayOfWeek == DayOfWeek.Sunday || Now64.DayOfWeek == DayOfWeek.Wednesday)
            {
                if (Now64.Hour == 1 && Now64.Minute == 01 && Now64.Second == 09  ||
                    Now64.Hour == 4 && Now64.Minute == 01 && Now64.Second == 09  ||
                    Now64.Hour == 8 && Now64.Minute == 01 && Now64.Second == 09  ||
                    Now64.Hour == 12 && Now64.Minute == 01 && Now64.Second == 09 ||
                    Now64.Hour == 16 && Now64.Minute == 01 && Now64.Second == 09 ||
                    Now64.Hour == 20 && Now64.Minute == 01 && Now64.Second == 09 
                   )
                {
                    Kernel.SendWorldMessage(new Message("Great Pheasant (Nor.1) has appeared in the Hidden City. Sundays and Wednesdays only.", Color.White, Message.System));
                    Kernel.SendWorldMessage(new Message("Great Pheasant (Nor.1) has appeared in the Hidden City. Sundays and Wednesdays only.", Color.White, Message.Center), Server.GamePool);

                    client.MessageBox("Will you hunt the Super Boss Great Pheasant (Nor.1)?",
                    p => { p.Entity.Teleport(1020, 526, 499); }, null, 40);
                    Kernel.SacarFaisan = false;
                }
            }
                       
            #endregion
            #region Mapa de GreatPheasant(Nor.1)
            //if (!client.Player.Dead)
            if (client.Entity.MapID == 1645)
                if (client.Entity.Hitpoints <= 0)
                {//Chavarro
                    Random R = new Random();
                    int G = R.Next(1, 7);
                    if (G == 1) client.Entity.Teleport(1020, 700, 623);
                    if (G == 2) client.Entity.Teleport(1020, 627, 830);
                    if (G == 3) client.Entity.Teleport(1020, 827, 550);
                    if (G == 4) client.Entity.Teleport(1020, 362, 561);
                    if (G == 5) client.Entity.Teleport(1020, 481, 310);
                    if (G == 6) client.Entity.Teleport(1020, 341, 558);
                    if (G == 7) client.Entity.Teleport(1020, 279, 275);

                    client.Send(new Message("You have fallen and left the map.", System.Drawing.Color.Red, Message.Whisper));
                }
            #endregion
            #endregion
            #region Soldado Slayer
            #region SlayerSoldier
            if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                return;
           
            if (Now64.Hour == 3 && Now64.Minute == 01 && Now64.Second == 09 && client.Entity.Reborn <=1  ||
                Now64.Hour == 7 && Now64.Minute == 01 && Now64.Second == 09 && client.Entity.Reborn <= 1 ||
                Now64.Hour == 11 && Now64.Minute == 01 && Now64.Second == 09 && client.Entity.Reborn <= 1 ||
                Now64.Hour == 15 && Now64.Minute == 01 && Now64.Second == 09 && client.Entity.Reborn <= 1 ||
                Now64.Hour == 19 && Now64.Minute == 01 && Now64.Second == 09 && client.Entity.Reborn <= 1 ||
                Now64.Hour == 23 && Now64.Minute == 01 && Now64.Second == 09 && client.Entity.Reborn <= 1 
               )

            {
                Kernel.SendWorldMessage(new Message("SoldierSlayer has appeared in the Hidden City (421, 428), shedding blood. Hunt it for a rich purse.", Color.White, Message.System));

                client.MessageBox("Will you hunt SoldierSlayer?",
                p => { p.Entity.Teleport(1015, 441, 729); }, null, 40);
                Kernel.SoldadoSlayer = true;
            }
            }
           
            if (client.Entity.MapID == 1022)
            {
                if (client.Entity.NobilityRank == NobilityRank.King
                    || client.Entity.NobilityRank == NobilityRank.Prince
                    || client.Entity.NobilityRank == NobilityRank.Duke
                    || client.Entity.NobilityRank == NobilityRank.Earl
                    || client.Entity.NobilityRank == NobilityRank.Baron
                    || client.Entity.NobilityRank == NobilityRank.Knight
                    )
                {
                    
                        client.Entity.Teleport(1002, 330, 266);
                   }
            }
            #endregion
            #region Mapa de Soldado SLayer
            //if (!client.Player.Dead)
            if (client.Entity.MapID == 1022)
                if (client.Entity.Hitpoints <= 0)
                {//Chavarro
                    Random R = new Random();
                    int G = R.Next(1, 7);
                    if (G == 1) client.Entity.Teleport(1015, 962, 673);
                    if (G == 2) client.Entity.Teleport(1015, 815, 855);
                    if (G == 3) client.Entity.Teleport(1015, 716, 665);
                    if (G == 4) client.Entity.Teleport(1015, 362, 561);
                    if (G == 5) client.Entity.Teleport(1015, 325, 370);
                    if (G == 6) client.Entity.Teleport(1015, 251, 162);
                    if (G == 7) client.Entity.Teleport(1015, 219, 581);

                    client.Send(new Message("You have fallen and left the soldier map.", System.Drawing.Color.Red, Message.Whisper));
                }
            #endregion
            #endregion
            #endregion

            #region SkyRoc[PlumaRoc]
            if (!client.Entity.Dead)
                if (client.Entity.MapID == 1100)
                    if (client.Inventory.Contains(3006392, 1))
                    {//Chavarro
                        client.Entity.Teleport(1011, 213, 199);

                        client.Send(new Message("Congratulations! " + client.Entity.Name + ", you may now claim your Tempest Wings.", System.Drawing.Color.Red, Message.Whisper));
                    }
            #endregion
            #region SkyDevil[Autocaza]
            if (!client.Entity.Dead)
                if (client.Entity.MapID == 1010)
                    if (client.Inventory.Contains(711899, 1))
                    {//Chavarro
                        client.Entity.Teleport(1002, 299, 303);

                        client.Send(new Message("Congratulations! " + client.Entity.Name + ", you may now claim VIP5PaqHonor.", System.Drawing.Color.Red, Message.Whisper));
                    }
            #endregion
            //PK grupo
            #region PK grupo
            if (false && (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004))
                return;
            if (false && Now64.DayOfWeek == DayOfWeek.Saturday && Now64.Hour == 12 && Now64.Minute == 01 && Now64.Second == 03)
            {
                client.MessageBox("The PK Group tournament has begun. Will you stand with your team?", p => { p.Entity.Teleport(1002, 337, 259); }, null, 60);
            }
            #endregion
            //PK Profesiones
            #region PK Profesiones
            if (false && (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004))
                return;
            if (false && Now64.DayOfWeek == DayOfWeek.Monday && Now64.Hour == 20 && Now64.Minute == 01 && Now64.Second == 03)
            {
                client.MessageBox("The Class Tournament has begun. Will you fight for your Top title?", p => { p.Entity.Teleport(1002, 309, 145); }, null, 60);
            }
            #endregion
            //mapas reparado de toeneo PK Jheisson
            #region repaired sand tournaments
            //Recuerde agregar en handle el mapa tambien al drop de items
            if (
                client.Entity.MapID == 200 || client.Entity.MapID == 201 || client.Entity.MapID == 210 || 
                client.Entity.MapID == 202 || client.Entity.MapID == 211 ||client.Entity.MapID == 203 || client.Entity.MapID == 212 ||
                client.Entity.MapID == 204 || client.Entity.MapID == 213 ||client.Entity.MapID == 205 || client.Entity.MapID == 214 || 
                client.Entity.MapID == 206 || client.Entity.MapID == 215 ||client.Entity.MapID == 207 || client.Entity.MapID == 216 || 
                client.Entity.MapID == 208 || client.Entity.MapID == 217 || client.Entity.MapID == 209 || client.Entity.MapID == 218 ||
                client.Entity.MapID == 128 || client.Entity.MapID == 129 || client.Entity.MapID == 123 ||
                client.Entity.MapID == 127 || client.Entity.MapID == 126 || client.Entity.MapID == 8877 ||
                client.Entity.MapID == 146 || client.Entity.MapID == 147 || client.Entity.MapID == 148 ||
                client.Entity.MapID == 149 || client.Entity.MapID == 150 || client.Entity.MapID == 151 ||
                client.Entity.MapID == 130 || client.Entity.MapID == 10360 || client.Entity.MapID == 8881 ||
                client.Entity.MapID == 132 || client.Entity.MapID == 10231 || client.Entity.MapID == 10232 ||
                client.Entity.MapID == 10233 || client.Entity.MapID == 10234 || client.Entity.MapID == 10235 ||
                client.Entity.MapID == 10236 || client.Entity.MapID == 10237 || client.Entity.MapID == 10238 ||
                client.Entity.MapID == 10239 || client.Entity.MapID == 5571 || client.Entity.MapID == 5576 ||
                client.Entity.MapID == 1991 || client.Entity.MapID == 3071 || client.Entity.MapID == 4445 ||
                client.Entity.MapID == 1735 || client.Entity.MapID == 1734 || client.Entity.MapID == 1733 ||
                client.Entity.MapID == 1732 || client.Entity.MapID == 1731 || client.Entity.MapID == 1730 ||
                client.Entity.MapID == 1950 || client.Entity.MapID == 2578 || client.Entity.MapID == 2057 ||
                client.Entity.MapID == 2061 || client.Entity.MapID == 2062 || client.Entity.MapID == 2063 ||
                client.Entity.MapID == 2064 || client.Entity.MapID == 5928 || client.Entity.MapID == 6421 ||
                client.Entity.MapID == 2072 || client.Entity.MapID == 10566 ||
                client.Entity.MapID == 60 || client.Entity.MapID == 10230 || client.Entity.MapID == 3333 || client.Entity.MapID == 3334 ||
                client.Entity.MapID == 3335 || client.Entity.MapID == 7777 || client.Entity.MapID == 4444 ||
                client.Entity.MapID == 50 || client.Entity.MapID == 60 ||client.Entity.MapID == 1365 || client.Entity.MapID == 1366 ||
                client.Entity.MapID == 1367 || client.Entity.MapID == 1368 ||client.Entity.MapID == 1090 ||
                client.Entity.MapID == 1508 || client.Entity.MapID == 1518 ||client.Entity.MapID == 7001 || client.Entity.MapID == 1801 ||
                client.Entity.MapID == 2065 || client.Entity.MapID == 8883 ||client.Entity.MapID == 1280 || client.Entity.MapID == 1281 ||
                client.Entity.MapID == 1282 || client.Entity.MapID == 1283 ||client.Entity.MapID == 1284 || client.Entity.MapID == 1096 ||
                client.Entity.MapID == 1097 || client.Entity.MapID == 1098 ||client.Entity.MapID == 2014 || client.Entity.MapID == 2072)
                if (client.Entity.Hitpoints <= 0)
                {//Chavarro
                    Random R = new Random();
                    int G = R.Next(1, 7);
                    if (G == 1) client.Entity.Teleport(1002, 239, 310);
                    if (G == 2) client.Entity.Teleport(1002, 242, 388);
                    if (G == 3) client.Entity.Teleport(1002, 305, 455);
                    if (G == 4) client.Entity.Teleport(1002, 380, 443);
                    if (G == 5) client.Entity.Teleport(1002, 433, 442);
                    if (G == 6) client.Entity.Teleport(1002, 403, 355);
                    if (G == 7) client.Entity.Teleport(1002, 423, 287);

                    client.Send(new Message("You have fallen and left the arena.", System.Drawing.Color.Red, Message.Whisper));
                }
            #endregion
            ////
            #region ConteoMuertes Todos los Mapas
             if (client.Entity.MapID == 1005)
            // {
            ////Si Queres que solo sea en Un Mapa Simplemente Saca las Llaves y Agrega el Codigo Dentro de Este Texto XD.!
           
            //string[] scoreMessages = new string[50];
            //int count = 0;
            //{
            //    if (client.Map.ID == client.Player.MapID)
            //    {
            //        count++;
            //        scoreMessages[count] = " Mis Asesinatos Totales: " + client.Player.Asesinatos + " Mis Muertes Totales: " + client.Player.Muertes + " ";
            //        Message msg1 = new Message(scoreMessages[count], System.Drawing.Color.Red, count == 1 ? Message.FirstRightCorner : Message.ContinueRightCorner);
            //        client.Send(msg1);

            //    }
            //}
            // }
            #endregion         
            //Hora Loca
            #region HoraLoca
            {
                if (false && (Now64.Hour == 6 && Now64.Minute == 02 && Now64.Second == 10 || Now64.Hour == 12 && Now64.Minute == 02 && Now64.Second == 10 || Now64.Hour == 18 && Now64.Minute == 02 && Now64.Second == 10 || Now64.Hour == 23 && Now64.Minute == 02 && Now64.Second == 10))
                {
                    Game.Entity.name = new object[] { "The Hour of Hunt is open. CPs = [1 to 100,000], Revora Points = [1 per kill] (hours: [6 to 7], [12 to 13], [18 to 19], [23 to 24])" };
                    Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[Hour of Hunt]", System.Drawing.Color.Red, 2500), Server.GamePool);

                }
            }
            #endregion
            #region AñoNuevo[18]
            //if (client.Player.MapID == 6000 || client.Player.MapID == 6001 || client.Player.MapID == 102 || client.Player.MapID == 6002 || client.Player.MapID == 6003 || client.Player.MapID == 6004)
            //    return;
            //if (Now64.Hour == 18 && Now64.Minute == 01)
            //{
            //    Kernel.SendWorldMessage(new Message("Torneo de AñoNuevo acaba de empezar Ganas Espectaculares premios!", Color.White, Message.Center), Server.GamePool);

            //    client.MessageBox("Quiere ir al Evento de Año Nuevo ?",
            //    p => { p.Player.Teleport(1002, 294, 277); }, null, 60);
            //}
            #endregion
            #region AñoNuevo[Letras]
            if (!client.Entity.Dead)
                if (client.Entity.MapID == 10230)
                    if (client.Inventory.Contains(728710, 1) && client.Inventory.Contains(728711, 1)
                                        && client.Inventory.Contains(728712, 1) && client.Inventory.Contains(728713, 1)
                                        && client.Inventory.Contains(728714, 1) && client.Inventory.Contains(728715, 1)
                                         && client.Inventory.Contains(728716, 1) && client.Inventory.Contains(728717, 1)
                                        && client.Inventory.Contains(728718, 1) && client.Inventory.Contains(728719, 1))
                    {//Chavarro
                        client.Entity.Teleport(1002, 563, 526);

                        client.Send(new Message("Congratulations! " + client.Entity.Name + ", you may now claim the New Year event award.", System.Drawing.Color.Red, Message.Whisper));
                    }
            #endregion
            #region Sacar Evento Año Nuevo
            //if (Now64.Hour == 23)
            //{
            //    if (Now64.Minute == 00 && Now64.Second == 1)
            //    {
            //        if (client.Player.MapID == 10230)
            //            client.Player.Teleport(1002, 408, 330);
            //        Kernel.SendWorldMessage(new Message("Evento de Año Nuevo a terminado regresa mañana a las 18:00!", Color.Red, 2011), Server.GamePool);
            //    }
            //}
            #endregion
            #region Sacar Guerra de Cps
            if (Now64.Hour == 11 || Now64.Hour == 22)
            {
                if (Now64.Minute == 30 && Now64.Second == 10)
                {
                    if (client.Entity.MapID == 2072)
                        client.Entity.Teleport(1002, 408, 330);
                    Kernel.SendWorldMessage(new Message("The CP Rain has ended. It returns Monday at 10:00 or 20:00.", Color.Red, 2011), Server.GamePool);
                }
            }
            #endregion

            //Evento de Apuesta de Oro
            #region Apuesta de Oro
            
            #region Inventario
            if (!client.Entity.Dead)
                if (client.Entity.MapID == 40031)
                    if (client.Entity.Money >= 100000000000)
                    {//Chavarro                            

                        client.Send(new Message("Do not leave a single foe alive on the gold wager map.", System.Drawing.Color.Red, Message.Whisper));
                    }
                    else
                    {
                        client.Entity.Teleport(1002, 313, 333);
                        client.Send(new Message("Alas, " + client.Entity.Name + ", you have saved your gold and left the wager.", System.Drawing.Color.Red, Message.Whisper));
                    }
            if (client.Entity.MapID == 40032)
                if (client.Entity.Money >= 50000000000)
                {//Chavarro                            

                    client.Send(new Message("Do not leave a single foe alive on the gold wager map.", System.Drawing.Color.Red, Message.Whisper));
                }
                else
                {
                    client.Entity.Teleport(1002, 313, 333);
                    client.Send(new Message("Alas, " + client.Entity.Name + ", you have saved your gold and left the wager.", System.Drawing.Color.Red, Message.Whisper));
                }
            if (client.Entity.MapID == 40033)
                if (client.Entity.Money >= 20000000000)
                {//Chavarro                            

                    client.Send(new Message("Do not leave a single foe alive on the gold wager map.", System.Drawing.Color.Red, Message.Whisper));
                }
                else
                {
                    client.Entity.Teleport(1002, 313, 333);
                    client.Send(new Message("Alas, " + client.Entity.Name + ", you have saved your gold and left the wager.", System.Drawing.Color.Red, Message.Whisper));
                }
            #endregion
            #region Sangre
            if (client.Entity.MapID == 40031)
                if (client.Entity.Hitpoints <= 0 && client.Entity.Money >= 100000000000)
                {//Chavarro
                    client.Entity.AdjustMoney(-(decimal)(100000000000));
                    Random R = new Random();
                    int G = R.Next(1, 7);
                    if (G == 1) client.Entity.Teleport(1002, 239, 310);
                    if (G == 2) client.Entity.Teleport(1002, 242, 388);
                    if (G == 3) client.Entity.Teleport(1002, 305, 455);
                    if (G == 4) client.Entity.Teleport(1002, 380, 443);
                    if (G == 5) client.Entity.Teleport(1002, 433, 442);
                    if (G == 6) client.Entity.Teleport(1002, 403, 355);
                    if (G == 7) client.Entity.Teleport(1002, 423, 287);

                    client.Send(new Message("Someone killed you and took 100,000,000,000 gold.", System.Drawing.Color.Red, Message.Whisper));
                }
            if (client.Entity.MapID == 40032)
                if (client.Entity.Hitpoints <= 0 && client.Entity.Money >= 50000000000)
                {//Chavarro
                    client.Entity.AdjustMoney(-(decimal)(50000000000));
                    Random R = new Random();
                    int G = R.Next(1, 7);
                    if (G == 1) client.Entity.Teleport(1002, 239, 310);
                    if (G == 2) client.Entity.Teleport(1002, 242, 388);
                    if (G == 3) client.Entity.Teleport(1002, 305, 455);
                    if (G == 4) client.Entity.Teleport(1002, 380, 443);
                    if (G == 5) client.Entity.Teleport(1002, 433, 442);
                    if (G == 6) client.Entity.Teleport(1002, 403, 355);
                    if (G == 7) client.Entity.Teleport(1002, 423, 287);

                    client.Send(new Message("Someone killed you and took 50,000,000,000 gold.", System.Drawing.Color.Red, Message.Whisper));
                }

            if (client.Entity.MapID == 40033)
                if (client.Entity.Hitpoints <= 0 && client.Entity.Money >= 20000000000)
                {//Chavarro
                    client.Entity.AdjustMoney(-(decimal)(20000000000));
                    Random R = new Random();
                    int G = R.Next(1, 7);
                    if (G == 1) client.Entity.Teleport(1002, 239, 310);
                    if (G == 2) client.Entity.Teleport(1002, 242, 388);
                    if (G == 3) client.Entity.Teleport(1002, 305, 455);
                    if (G == 4) client.Entity.Teleport(1002, 380, 443);
                    if (G == 5) client.Entity.Teleport(1002, 433, 442);
                    if (G == 6) client.Entity.Teleport(1002, 403, 355);
                    if (G == 7) client.Entity.Teleport(1002, 423, 287);

                    client.Send(new Message("Someone killed you and took 20,000,000,000 gold.", System.Drawing.Color.Red, Message.Whisper));
                }
            #endregion

            #endregion
            #region [ Vip~Player~War ]
            //if ((Now64.Minute == 50) && (Now64.Second == 05))
            //{
            //    {
            //        Kernel.SendWorldMessage(new Message("[ V.P.W ] Started , Go Join Now At ( Vip~Player~War ) In TwinCity !", Color.White, Message.TopLeft), Server.GamePool);
            //        //foreach (var client in Program.Values)
            //        client.MessageBox("[ V.P.W ] Started ! Would You Like To Join?",
            //        p => { p.Player.Teleport(1002, 308, 237); }, null, 60);
            //    }
            //}
            #endregion

            #region OnlinePoints
            {
                if (client.Entity.VIPLevel <= 5)
                {
                    if (Time32.Now > client.Entity.OnlinePointStamp.AddMinutes(2))
                    {
                        client.Entity.OnlinePoints += 5;
                        client.Entity.OnlinePointStamp = Time32.Now;
                        client.Send(new Message("Congratulations! " + client.Entity.Name + ", VIP 5 earns 5 Online Points every 2 minutes.", System.Drawing.Color.Red, Message.Whisper));
                    }
                }
                if (client.Entity.VIPLevel == 6)
                {
                    if (Time32.Now > client.Entity.OnlinePointStamp.AddMinutes(2))
                    {
                        client.Entity.OnlinePoints += 6;
                        client.Entity.OnlinePointStamp = Time32.Now;
                        client.Send(new Message("Congratulations! " + client.Entity.Name + ", VIP 6 earns 6 Online Points every 2 minutes.", System.Drawing.Color.Red, Message.Whisper));
                    }
                }
                if (client.Entity.VIPLevel >= 7)
                {
                    if (Time32.Now > client.Entity.OnlinePointStamp.AddMinutes(2))
                    {
                        client.Entity.OnlinePoints += 8;
                        client.Entity.OnlinePointStamp = Time32.Now;
                        client.Send(new Message("Congratulations! " + client.Entity.Name + ", VIP 7 earns 8 Online Points every 2 minutes.", System.Drawing.Color.Red, Message.Whisper));
                    }
                }
            }
           
            {
                if (Time32.Now > client.Entity.OnlinePointStamp.AddMinutes(5))
                {
                    client.Entity.OnlinePoints += 5;
                    client.Entity.OnlinePointStamp = Time32.Now;
                    client.Send(new Message("Congratulations! " + client.Entity.Name + ", you have gained 5 extra Online Points in 5 minutes.", System.Drawing.Color.Red, Message.Whisper));
                }
            }
            #endregion
            #region mapa de salir
            // Disabled: VIP Away used to dump players on house map 10363 (mapdoc 10272)
            // with no portal. Twin City scroll was the only way out.
            #endregion
            #region Activar Reclamación de eventos PK
            #region Hombres y Mujeres
            //Activar
            if (false && Now64.Minute == 03 && Now64.Second == 01)
            {
                ReclamarHombres = true;
                ReclamarMujeres = true;
            }
            //Desactivar
            if (Now64.Minute == 50 && Now64.Second == 01)
            {
                if (ReclamarHombres == true)
                {
                    ReclamarHombres = false;
                }
            }
            if (Now64.Minute == 50 && Now64.Second == 01)
            {
                if (ReclamarMujeres == true)
                {
                    ReclamarMujeres = false;
                }
            }
            #endregion
            #region New Events
            //Mr.ConquerHost
            if (false && Now64.Minute == 05 && Now64.Second == 05)
            {
                MrConquerHost = true;
            }
            //Ms.ConquerHost
            if (false && Now64.Minute == 05 && Now64.Second == 10)
            {
                MsConquerHost = true;
            }
            //Mr.ChinaHost
            if (false && Now64.Minute == 25 && Now64.Second == 05)
            {
                MrChinaHost = true;
            }
            //Ms.ChinaHost
            if (false && Now64.Minute == 25 && Now64.Second == 10)
            {
                MsChinaHost = true;
            }
            #endregion
            #region XX: 15
            //activar
            if (false && Now64.Minute == 17 && Now64.Second == 01)
            {
                Evento15 = true;
            }
            //Desactivar
            if (Now64.Minute == 50 && Now64.Second == 01)
            {
                if (Evento15 == true)
                {
                    Evento15 = false;
                }
            }
            #endregion
            #region XX:29
            //Activar
            if (false && Now64.Minute == 31 && Now64.Second == 01)
            {
                Evento30 = true;
            }
            //Desactivar
            if (Now64.Minute == 50 && Now64.Second == 01)
            {
                if (Evento30 == true)
                {
                    Evento30 = false;
                }
            }
            #endregion
            #region XX:45
            //Activar
            if (false && Now64.Minute == 47 && Now64.Second == 01)
            {
                Evento45 = true;
            }
            //Desactivar
            if (Now64.Minute == 59 && Now64.Second == 01)
            {
                if (Evento45 == true)
                {
                    Evento45 = false;
                }
            }
            #endregion
            #endregion

           

        }
        #region Protección a Eventos
        public static bool MrConquerHost
        {
            get { return Server.Vars["MrConquerHost"]; }
            set { Server.Vars["MrConquerHost"] = value; }
        }
        public static bool VipPlayerWar
        {
            get { return Server.Vars["VipPlayerWar"]; }
            set { Server.Vars["VipPlayerWar"] = value; }
        }
        public static bool MsConquerHost
        {
            get { return Server.Vars["MsConquerHost"]; }
            set { Server.Vars["MsConquerHost"] = value; }
        }
        public static bool MrChinaHost
        {
            get { return Server.Vars["MrChinaHost"]; }
            set { Server.Vars["MrChinaHost"] = value; }
        }
        public static bool MsChinaHost
        {
            get { return Server.Vars["MsChinaHost"]; }
            set { Server.Vars["MsChinaHost"] = value; }
        }
        public static bool ReclamarHombres
        {
            get { return Server.Vars["reclamarhombres"]; }
            set { Server.Vars["reclamarhombres"] = value; }
        }
        public static bool ReclamarMujeres
        {
            get { return Server.Vars["reclamarmujeres"]; }
            set { Server.Vars["reclamarmujeres"] = value; }
        }
        public static bool Evento15
        {
            get { return Server.Vars["evento15"]; }
            set { Server.Vars["evento15"] = value; }
        }
        public static bool Evento30
        {
            get { return Server.Vars["evento30"]; }
            set { Server.Vars["evento30"] = value; }
        }
        public static bool Evento45
        {
            get { return Server.Vars["evento45"]; }
            set { Server.Vars["evento45"] = value; }
        }
        #endregion
        private void AutoAttackCallback(GameState client, int time)
        {
            if (!Valid(client)) return;
            Time32 Now = new Time32(time);
            if (client.Entity.AttackPacket != null || client.Entity.VortexAttackStamp != null)
            {
                try
                {
                    if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex))
                    {
                        if (client.Entity.VortexPacket != null && client.Entity.VortexPacket.ToArray() != null)
                        {
                            if (Now > client.Entity.VortexAttackStamp.AddMilliseconds(1400))
                            {
                                client.Entity.VortexAttackStamp = Now;
                                new Game.Attacking.Handle(client.Entity.VortexPacket, client.Entity, null);
                            }
                        }
                    }
                    else
                    {
                        client.Entity.VortexPacket = null;
                        var AttackPacket = client.Entity.AttackPacket;
                        if (AttackPacket != null && AttackPacket.ToArray() != null)
                        {
                            uint AttackType = AttackPacket.AttackType;
                            if (AttackType == Attack.Magic || AttackType == Attack.Melee || AttackType == Attack.Ranged)
                            {
                                if (AttackType == Attack.Magic)
                                {
                                    if (Now > client.Entity.AttackStamp.AddSeconds(1))
                                    {
                                        if (AttackPacket.Damage != 12160 && AttackPacket.Damage != 12170 &&
                                            AttackPacket.Damage != 12120 && AttackPacket.Damage != 12130 &&
                                            AttackPacket.Damage != 12140 && AttackPacket.Damage != 12320 &&
                                            AttackPacket.Damage != 12330 && AttackPacket.Damage != 12340 &&
                                            AttackPacket.Damage != 12210)
                                            new Game.Attacking.Handle(AttackPacket, client.Entity, null);
                                    }
                                }
                                else
                                {
                                    int decrease = -300;
                                    if (client.Entity.OnCyclone())
                                        decrease = 700;
                                    if (client.Entity.OnSuperman())
                                        decrease = 200;
                                    if (Now > client.Entity.AttackStamp.AddMilliseconds((1000 - client.Entity.Agility - decrease - client.Entity.InfinityDamage) * (int)(AttackType == Attack.Ranged ? 1 : 1)))
                                    {
                                        new Game.Attacking.Handle(AttackPacket, client.Entity, null);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Server.SaveException(e);
                    client.Entity.AttackPacket = null;
                    client.Entity.VortexPacket = null;
                }
            }
        }
        private void CompanionsCallback(GameState client, int time)
        {
            if (!Valid(client)) return;
            Time32 Now = new Time32(time);
            if (client.Companion != null)
            {
                short distance = Kernel.GetDistance(client.Companion.X, client.Companion.Y, client.Entity.X, client.Entity.Y);
                if (distance >= 8)
                {
                    ushort X = (ushort)(client.Entity.X + Kernel.Random.Next(2));
                    ushort Y = (ushort)(client.Entity.Y + Kernel.Random.Next(2));
                    if (!client.Map.SelectCoordonates(ref X, ref Y))
                    {
                        X = client.Entity.X;
                        Y = client.Entity.Y;
                    }
                    client.Companion.X = X;
                    client.Companion.Y = Y;
                    Data data = new Data(true);
                    data.ID = Data.Jump;
                    data.dwParam = (uint)((Y << 16) | X);
                    data.wParam1 = X;
                    data.wParam2 = Y;
                    data.UID = client.Companion.UID;
                    client.Companion.MonsterInfo.SendScreen(data);
                }
                else if (distance > 4)
                {
                    Enums.ConquerAngle facing = Kernel.GetAngle(client.Companion.X, client.Companion.Y, client.Companion.Owner.Entity.X, client.Companion.Owner.Entity.Y);
                    if (!client.Companion.Move(facing))
                    {
                        facing = (Enums.ConquerAngle)Kernel.Random.Next(7);
                        if (client.Companion.Move(facing))
                        {
                            client.Companion.Facing = facing;
                            GroundMovement move = new GroundMovement(true);
                            move.Direction = facing;
                            move.UID = client.Companion.UID;
                            move.GroundMovementType = GroundMovement.Run;
                            client.Companion.MonsterInfo.SendScreen(move);
                        }
                    }
                    else
                    {
                        client.Companion.Facing = facing;
                        GroundMovement move = new GroundMovement(true);
                        move.Direction = facing;
                        move.UID = client.Companion.UID;
                        move.GroundMovementType = GroundMovement.Run;
                        client.Companion.MonsterInfo.SendScreen(move);
                    }
                }
                else
                {
                    var monster = client.Companion;
                    if (monster.MonsterInfo.InSight == 0)
                    {
                        if (client.Entity.AttackPacket != null)
                        {
                            if (client.Entity.AttackPacket.AttackType == Attack.Magic)
                            {
                                if (client.Entity.AttackPacket.Decoded)
                                {
                                    if (Database.SpellTable.SpellInformations.ContainsKey((ushort)client.Entity.AttackPacket.Damage))
                                    {
                                        var info = Database.SpellTable.SpellInformations[(ushort)client.Entity.AttackPacket.Damage].Values.ToArray()[client.Spells[(ushort)client.Entity.AttackPacket.Damage].Level];
                                        if (info.CanKill)
                                        {
                                            monster.MonsterInfo.InSight = client.Entity.AttackPacket.Attacked;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                monster.MonsterInfo.InSight = client.Entity.AttackPacket.Attacked;
                            }
                        }
                    }
                    else
                    {
                        if (monster.MonsterInfo.InSight > 400000 && monster.MonsterInfo.InSight < 600000 || monster.MonsterInfo.InSight > 800000 && monster.MonsterInfo.InSight != monster.UID)
                        {
                            Entity attacked = null;

                            if (client.Screen.TryGetValue(monster.MonsterInfo.InSight, out attacked))
                            {
                                if (Now > monster.AttackStamp.AddMilliseconds(monster.MonsterInfo.AttackSpeed))
                                {
                                    monster.AttackStamp = Now;
                                    if (attacked.Dead)
                                    {
                                        monster.MonsterInfo.InSight = 0;
                                    }
                                    else new Game.Attacking.Handle(null, monster, attacked);
                                }
                            }
                            else monster.MonsterInfo.InSight = 0;
                        }
                    }
                }
            }
        }

        private static int restoredTickOwner;
        private static int restoredTickReported;
        private static readonly Dictionary<string,DateTime> restoredTickFailures=new Dictionary<string,DateTime>();
        private static bool RunRestoredTick(string name,Action tick) {
            try {tick();return true;}
            catch(Exception error) {
                DateTime last;var now=DateTime.UtcNow;
                if(!restoredTickFailures.TryGetValue(name,out last) || now>=last.AddSeconds(30)) {
                    restoredTickFailures[name]=now;
                    Console.WriteLine("[roadmap] event_tick_failed name="+name+" error="+error.GetType().Name);
                }
                return false;
            }
        }
        // Only restored services belong here; the retired legacy block below stays disabled.
        private static void TickRestoredEvents(DateTime localNow) {
            if(System.Threading.Interlocked.CompareExchange(ref restoredTickOwner,1,0)!=0)return;
            try {
                bool healthy=RunRestoredTick("team-tournament-calendar",()=>Sistemas.Quarantine.StartScheduled(localNow));
                healthy=RunRestoredTick("SkillTeamPK",()=>TeamElitePk.SkillTeamTournament.OpenAt(localNow)) && healthy;
                healthy=RunRestoredTick("FashionCompetition",FashionCompetition.Tick) && healthy;
                healthy=RunRestoredTick("MrMs2016",MrMs2016.Tick) && healthy;
                healthy=RunRestoredTick("MrMs2017",MrMs2017.Tick) && healthy;
                healthy=RunRestoredTick("GuildContest",GuildContestCoordinator.Tick) && healthy;
                healthy=RunRestoredTick("GuildSeasonWar",GuildSeasonWar.Tick) && healthy;
                healthy=RunRestoredTick("SuperGuildWar",SuperGuildWar.Tick) && healthy;
                healthy=RunRestoredTick("EliteGuildWar",EliteGuildWar.Tick) && healthy;
                healthy=RunRestoredTick("GuildManagers",GuildManagers.Tick) && healthy;
                healthy=RunRestoredTick("KingdomCompetition",KingdomCompetitionField.Tick) && healthy;
                healthy=RunRestoredTick("GuildBeastEvent",GuildBeastEvent.Tick) && healthy;
                healthy=RunRestoredTick("GuildWarPurses",GuildWarPurses.Tick) && healthy;
                healthy=RunRestoredTick("GuildPrizeRollover",GuildPrizeRollover.Tick) && healthy;
                healthy=RunRestoredTick("ClanWar",ClanWar.Tick) && healthy;
                if(healthy && System.Threading.Interlocked.Exchange(ref restoredTickReported,1)==0)
                    Console.WriteLine("[roadmap] restored-event-pulse complete");
            } finally {System.Threading.Volatile.Write(ref restoredTickOwner,0);}
        }
        private void WorldTournaments(int time)
        {
            Time32 Now = new Time32(time);
            DateTime Now64 = DateTime.Now;
            CityGuardSystem.Tick();
            LivingWorldSystem.Tick();
            Nobility.Pulse();
            LastManStandingSystem.Tick();
            NobleWarSystem.Tick();
            ExpeditionEvent.Tick();
            RelicRushSystem.Tick();
            WaveDefenseSystem.Tick();
            BeastTrialSystem.Tick();
            BossRushSystem.Tick();
            DailyWorldBosses.Tick();
            AuctionGateway.Tick();
            DuelPitSystem.Tick();
            SteedRaceSystem.Tick();
            if (CTF != null) CTF.Tick();
            if (ElitePkEvent.Current != null) ElitePkEvent.Current.Tick();
            if (LocalPkWar.Current != null) LocalPkWar.Current.Tick();
            if (CouplePkEvent.Current != null) CouplePkEvent.Current.Tick();
            MoonBoxQuest.Tick();
            PowerArenaEvent.Tick();
            ReleaseSoulsQuest.Tick();
            QuizShowEvent.Tick();
            SummerHunt.Tick();
            SkyPassQuest.Tick();
            SnakeIslandsQuest.Tick();
            LabyrinthQuest.Tick();
            DisCityQuest.Tick();
            DragonSlayerWorld.Tick();
            TeratoEncounter.Tick();
            SwordMasterEncounter.Tick();
            PurpleBansheeEncounter.Tick();
            SnowBansheeEncounter.Tick();
            AbyssTrialWorld.Tick();
            ExorcismWorld.Tick();
            PlanetEncounter.Tick();
            HeartEncounter.Tick();
            HeartCampaign.Tick();
            WarriorEpicQuest.Tick();
            PowerBookQuest.Tick();
            FranticMonkeysQuest.Tick();
            SoulGeneralQuest.Tick();
            MadManQuest.Tick();
            GhostCityQuest.Tick();
            DesertTreasureQuest.Tick();
            DesertExpeditionQuest.Tick();
            DefenseTalismanQuest.Tick();
            AttackTalismanQuest.Tick();
            TempestWingsQuest.Tick();
            FamilyFeudQuest.Tick();
            DesertBloodQuest.Tick();
            SpunGoldQuest.Tick();
            TrojanEpicQuest.Tick();
            AncientDevilQuest.Tick();
            EliminationQuest.Tick();
            RefineryProgress.Tick();
            TickRestoredEvents(Now64);
            Game.Features.Tournaments.LocalTeamPkAdmissions.Tick();
            #region TeamPk Tournament
            if (TeamElitePk.TeamTournament.IsOpeningTime(Now64) && TeamElitePk.TeamTournament.Open())
            {
                foreach (Client.GameState clients in Kernel.GamePool.Values)
                {
                    clients.Entity.ClaimedTeamPK = false;
                }
                Game.Entity.name = new object[] { "Team PK registration is open. Gather at the Team Registrar in Twin City (291,146)!" }; foreach (var client in Server.GamePool)
                    if (Game.Features.QuestInventory.Player(client))
                        client.MessageBox("Team PK registration is open for one minute. Will your team enter?", p => { p.Entity.Teleport(1002, 293, 146); }, null);
                Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[Team PK]", Color.Red, 2500), Server.GamePool);
                foreach (var client in Server.GamePool)
                {
                    if (!client.InQualifier() && client.Entity.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 && !client.Entity.Dead)
                    {
                        AutoInvite alert = new AutoInvite
                        {
                            StrResID = 10543,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10543;
                        client.Send(alert.ToArray());
                    }
                }
            }
            #endregion
            return;
            
            #region Weekly PK PK Semanal
            foreach (var client in Server.GamePool)
            {
                if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                    return;
                if (Now64.DayOfWeek == DayOfWeek.Sunday && Now64.Hour == 20 && Now64.Minute == 5 && Now64.Second <= 0)
                {
                    client.MessageBox("The weekly purse: Damage-3, Revora Points, and a Halo of the Best. Will you attend?", p => { p.Entity.Teleport(1002, 297, 230); }, null, 60);
                    
                }
            }
            
            #endregion
            #region Team Qualifier
            if ((Now64.Hour == 11 || Now64.Hour == 19) && Now64.Minute == 02 && Now64.Second <= 1)
            {
                Kernel.SendWorldMessage(new Message("Team Qualifier Arena is open. Sign up now!", Color.Red, Message.Center));
                foreach (var client in Server.GamePool)
                {
                    if (!client.InQualifier() && client.Entity.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 && !client.Entity.Dead)
                    {
                        AutoInvite alert = new AutoInvite
                        {
                            StrResID = 10562,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10562;
                        client.Send(alert.ToArray());
                    }
                }
            }
            #endregion
            #region ChampionArena
            if (Game.Champion.QualifyEngine.RegistrationOpen(Now64))
            {
                if (Now64.Minute == 55 && Now64.Second <= 1)
                    Kernel.SendWorldMessage(new Message("Champion Arena registration is open until 21:00. Speak with the Champion Registrar in Twin City.", Color.Red, Message.Center));
            }
            #endregion
            #region System Messages
            if (Now64.Minute == 3 && Now64.Second <= 10)
            {
                Game.Entity.SendWorldMessage("Clan War on Sundays ends at 16:00 Server Time");
            }
            if (Now64.Minute == 6 && Now64.Second == 10)
            {
                Game.Entity.SendWorldMessage("New to Twin City? Seek the clerk by the market. A welcome gift waits for those who ask.");
            }
            if (Now64.Minute == 9 && Now64.Second == 10)
            {
                Game.Entity.SendWorldMessage("Guild War on Wednesdays ends at 9:00 p.m. Server Time");
            }
            if (Now64.Minute == 12 && Now64.Second == 10)
            {
                Game.Entity.SendWorldMessage("Golden War is Monday at 20:00 Server time");
            }
            if (Now64.Minute == 15 && Now64.Second == 10)
            {
                Game.Entity.SendWorldMessage("Need a guide? The clerks in Twin City and Dragon City know the roads.");
            }
            if (Now64.Minute == 18 && Now64.Second == 11)
            {
                Kernel.SendWorldMessage(new Message("Class tournaments gather every Monday at 20:00, split by nobility. Team PK registration opens Saturday at 18:56 server time for one minute. Find the Team Registrar in Twin City (291,146).", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 21 && Now64.Second == 11)
            {
                Kernel.SendWorldMessage(new Message("New to these lands? Super gear and Perfect Stones still come out of the mines. Bring a pick and patience.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 24 && Now64.Second == 11)
            {
                Kernel.SendWorldMessage(new Message("Want wings? Finish the work in Phoenix City, then hunt the Sky Roc. It does not part with feathers easily.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 27 && Now64.Second == 11)
            {
                Kernel.SendWorldMessage(new Message("Every six hours the Hour of Hunt opens (6:00, 12:00, 18:00, 23:00). 100,000 CPs and 1 Slayer Point fall with each kill.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 30 && Now64.Second == 10)
            {
                Kernel.SendWorldMessage(new Message("On Tuesday, Thursday, and Saturday, the Perfection task opens at dusk and at night. The clerk waits in Dragon City.", Color.White, Message.Center), Server.GamePool);
            }

            if (Now64.Minute == 33 && Now64.Second == 11)
            {
                Kernel.SendWorldMessage(new Message("No clerk or helper will ever ask for your account name or password. Guard them.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 36 && Now64.Second <= 11)
            {
                Kernel.SendWorldMessage(new Message("Stay online and the mines grow richer. 30 minutes: 1,000 extra CPs. 40 minutes: 2,000 extra CPs. 50 minutes: 3,000 CPs and 3,000 extra gold.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 39 && Now64.Second <= 11)
            {
                Kernel.SendWorldMessage(new Message("Revora is more than a battlefield. Nobility resets every Sunday. Trade fairly in Twin City.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 42 && Now64.Second <= 11)
            {
                Kernel.SendWorldMessage(new Message("Game Masters never ask for your password. Trade only with the clerks of Twin City and Dragon City.", Color.White, Message.Center), Server.GamePool);
            }

            if (Now64.Minute == 45 && Now64.Second == 11)
            {
                Kernel.SendWorldMessage(new Message("Need a Super Cup? The Revora Points clerk in Dragon City keeps them.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 48 && Now64.Second <= 11)
            {
                Kernel.SendWorldMessage(new Message("Fifty thousand Revora Points can be earned in a day. Hunt during the Hour of Hunt.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 51 && Now64.Second <= 11)
            {
                Kernel.SendWorldMessage(new Message("New to these lands? The Twin City clerk holds a welcome gift for those who ask.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 54 && Now64.Second <= 11)
            {
                Kernel.SendWorldMessage(new Message("The Twin City market never sleeps. Trade fairly, and the clerks will remember your name.", Color.White, Message.Center), Server.GamePool);
            }
            if (Now64.Minute == 57 && Now64.Second <= 11)
            {
                Kernel.SendWorldMessage(new Message("Keep your client current. The clerks in Twin City post the latest notices.", Color.White, Message.Center), Server.GamePool);
            }
            #endregion
            #region Couples PK War
            //if (Now64.DayOfWeek == DayOfWeek.Friday && Now64.Hour == 21 && Now64.Minute == 00 && Now64.Second <= 1)
            //{
            //    Game.Entity.name = new object[] { "Couples PkWar has started! You have 5 minute to signup go to TC CouplesPkGuide in TwinCity!" };
            //    Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[Couples PkWar]", System.Drawing.Color.Red, 2500), Server.GamePool);
            //    foreach (var client in Server.GamePool)
            //    {
            //        if (!client.InQualifier() && client.Player.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 &&client.Map.BaseID != 6004 && !client.Player.Dead)
            //        {
            //            AutoInvite alert = new AutoInvite
            //            {
            //                StrResID = 10555,
            //                Countdown = 60,
            //                Action = 1
            //            };
            //            client.Player.StrResID = 10555;
            //            client.Send(alert.ToArray());
            //        }
            //    }
            //}
            #endregion
            #region PlanderWar           
            //foreach (var client in Server.GamePool)
            //if (client.Player.MapID == 6000 || client.Player.MapID == 6001 || client.Player.MapID == 102 || client.Player.MapID == 6002 || client.Player.MapID == 6003 || client.Player.MapID == 6004)
            //        return;
            //    if (Now64.Hour == 17 && Now64.Minute == 01)
            //    {
            //        foreach (var client in Server.GamePool)                       
            //        client.MessageBox("Ya es hora de la Guerra de Union!, Que esperar pata ir ?",
            //        p => { p.Player.Teleport(1002, 306, 163); }, null, 60);
            //        Kernel.SendWorldMessage(new Message("The Union war has begun. Run to the Union War clerk before the field fills.", Color.White, Message.Center), Server.GamePool);
            //        PlanderWar.StartWar();
                    
            //    }
           
            #endregion
            #region Monthly PK
            //if (Now64.Day <= 7 && Now64.DayOfWeek == DayOfWeek.Sunday)
            //{
            //    if (Now64.Hour == 21 && Now64.Minute >= 50 && Now64.Second <= 2)
            //    {
            //        int min = 60 - Now64.Minute;
            //        Kernel.SendWorldMessage(new Message("The Monthly PK War will start in " + min.ToString() + " minutes!", Color.Red, 2012));
            //    }
            //    if (Now64.Hour == 22 && Now64.Minute == 00 && Now64.Second <= 2)
            //    {
            //        Entity.MonthlyPKWar = true;
            //        Kernel.SendWorldMessage(new Message("The Monthly PK War began!", Color.Red, 2012));
            //        foreach (var client in Server.GamePool)
            //        {
            //            if (!client.InQualifier() && client.Player.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 && !client.Player.Dead)
            //            {
            //                AutoInvite alert = new AutoInvite
            //                {
            //                    StrResID = 10523,
            //                    Countdown = 60,
            //                    Action = 1
            //                };
            //                client.Player.StrResID = 10523;
            //                client.Send(alert.ToArray());
            //            }
            //        }
            //    }
            //    if (Now64.Hour == 22 && Now64.Minute >= 25 && Entity.MonthlyPKWar)
            //    {
            //        Entity.MonthlyPKWar = false;
            //        Kernel.SendWorldMessage(new Message("The Monthly PK War ended!", Color.Red, Message.Center));
            //    }
            //}
            #endregion
            #region GuildwarFlame
            if (GuildWar.IsWar)
            {
                if (Time32.Now > GuildWar.ScoreSendStamp.AddSeconds(3))
                {
                    GuildWar.ScoreSendStamp = Time32.Now;
                    GuildWar.SendScores();
                }
                if (!GuildWar.Flame10th)
                {
                    if (Now64.DayOfWeek == DayOfWeek.Sunday && Now64.Hour == 14 && Now64.Minute == 59)
                    {
                        GuildWar.Flame10th = true;
                        Game.Entity.name = new object[] { "Guild Leaders may now light the guild flames." };
                        Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[Guild War]", System.Drawing.Color.Red, 2500), Server.GamePool);
                    }
                }
            }
            if (Now64.DayOfWeek == DayOfWeek.Sunday && Now64.Hour == 15 && Now64.Minute == 00 && Now64.Second >= 01)
            {
                if (!GuildWar.IsWar)
                {
                    GuildWar.Start();
                    foreach (var client in Server.GamePool)
                    {
                        if (!client.InQualifier() && client.Entity.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 && !client.Entity.Dead)
                        {
                            AutoInvite alert = new AutoInvite
                            {
                                StrResID = 10515,
                                Countdown = 60,
                                Action = 1
                            };
                            client.Entity.StrResID = 10515;
                            client.Send(alert.ToArray());
                        }
                    }
                }
            }
            if (GuildWar.IsWar)
            {
                if (Now64.DayOfWeek == DayOfWeek.Sunday && Now64.Hour == 16 && Now64.Minute == 00 && Now64.Second >= 01)
                {
                    GuildWar.Flame10th = false;
                    GuildWar.End();
                }
            }
            #endregion
            #region Golden War [11:00]
            if (Now64.DayOfWeek == DayOfWeek.Monday)
            {
                if (!Game.PoleTwin.IsWar)
                {
                    if (Now64.Hour == 10 && Now64.Minute == 00 && Now64.Second <= 30)
                    {
                        Game.PoleTwin.Start();
                        foreach (var client in Server.GamePool)
                            if (client.Entity.GuildID != 0)
                                client.MessageBox("The CP Rain has begun, with no PK. Will you attend?", p => { p.Entity.Teleport(1002, 288, 159); }, null);
                    }
                }
            }
            if (Game.PoleTwin.IsWar)
            {
                if (Time32.Now > Game.PoleTwin.ScoreSendStamp.AddSeconds(3))
                {
                    Game.PoleTwin.ScoreSendStamp = Time32.Now;
                    Game.PoleTwin.SendScores();
                }
                if (Now64.DayOfWeek == DayOfWeek.Monday && Now64.Hour == 11 && Now64.Minute == 20 && Now64.Second <= 0)
                {
                    Kernel.SendWorldMessage(new Network.GamePackets.Message("Ten minutes remain in the CP Rain.", System.Drawing.Color.White, Network.GamePackets.Message.Center), Server.GamePool);
                }
                else if (PoleTwin.IsWar)
                {
                    if (Now64.DayOfWeek == DayOfWeek.Monday && Now64.Hour == 11 && Now64.Minute == 30 && Now64.Second <= 0)
                    {
                        PoleTwin.End();

                    }
                }
            }
            #endregion
            #region Guerra de Oro[21:00]
            if (Now64.DayOfWeek == DayOfWeek.Monday)
            {
                if (!Game.PoleTwin.IsWar)
                {
                    if (Now64.Hour == 21 && Now64.Minute == 00 && Now64.Second <= 30)
                    {
                        Game.PoleTwin.Start();
                        foreach (var client in Server.GamePool)
                            if (client.Entity.GuildID != 0)
                                client.MessageBox("The Gold Rain has begun, with no PK. Will you attend?", p => { p.Entity.Teleport(1002, 288, 159); }, null);
                    }
                }
            }
            if (Game.PoleTwin.IsWar)
            {
                if (Time32.Now > Game.PoleTwin.ScoreSendStamp.AddSeconds(3))
                {
                    Game.PoleTwin.ScoreSendStamp = Time32.Now;
                    Game.PoleTwin.SendScores();
                }
                if (Now64.DayOfWeek == DayOfWeek.Monday && Now64.Hour == 22 && Now64.Minute == 20 && Now64.Second <= 0)
                {
                    Kernel.SendWorldMessage(new Network.GamePackets.Message("Ten minutes remain in the Gold Rain.", System.Drawing.Color.White, Network.GamePackets.Message.Center), Server.GamePool);
                }
                else if (PoleTwin.IsWar)
                {
                    if (Now64.DayOfWeek == DayOfWeek.Monday && Now64.Hour == 22 && Now64.Minute == 30 && Now64.Second <= 0)
                    {
                        PoleTwin.End();
                        
                    }
                }
            }
            #endregion
            #region ClanWar
            #endregion
            #region ElitePK Tournament
            if (Now64.DayOfWeek == DayOfWeek.Friday && Now64.Hour == ElitePK.EventTime && Now64.Minute >= 55 && !ElitePKTournament.TimersRegistered)
            {
                ElitePKTournament.RegisterTimers();
                ElitePKBrackets brackets = new ElitePKBrackets(true, 0);
                brackets.Type = ElitePKBrackets.EPK_State;
                brackets.OnGoing = true;
                foreach (Client.GameState clients in Kernel.GamePool.Values)
                {
                    clients.Entity.ClaimedElitePk = false;
                }
                Game.Entity.name = new object[] { "The Heroes Tournament has begun. Seek the Hero Clerk in Dragon City and take your place." };
                Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[Heroes Tournament]", Color.Red, 2500), Server.GamePool);
                foreach (var client in Server.GamePool)
                {
                    if (!client.InQualifier() && client.Entity.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 && !client.Entity.Dead)
                    {
                        AutoInvite alert = new AutoInvite
                        {
                            StrResID = 10533,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10533;
                        client.Send(alert.ToArray());
                    }
                    #region RemoveTopElite
                    var EliteChampion = TitlePacket.Titles.ElitePKChamption_High;
                    var EliteSecond = TitlePacket.Titles.ElitePK2ndPlace_High;
                    var EliteThird = TitlePacket.Titles.ElitePK3ndPlace_High;
                    var EliteEightChampion = TitlePacket.Titles.ElitePKChamption_Low;
                    var EliteEightSecond = TitlePacket.Titles.ElitePK2ndPlace_Low;
                    var EliteEightThird = TitlePacket.Titles.ElitePK3ndPlace_Low;
                    var EliteEight = TitlePacket.Titles.ElitePKTopEight_Low;
                    if (client.Entity.Titles.ContainsKey(EliteChampion))
                        client.Entity.RemoveTopStatus((ulong)EliteChampion);
                    if (client.Entity.Titles.ContainsKey(EliteSecond))
                        client.Entity.RemoveTopStatus((ulong)EliteSecond);
                    if (client.Entity.Titles.ContainsKey(EliteThird))
                        client.Entity.RemoveTopStatus((ulong)EliteThird);
                    if (client.Entity.Titles.ContainsKey(EliteEightChampion))
                        client.Entity.RemoveTopStatus((ulong)EliteEightChampion);
                    if (client.Entity.Titles.ContainsKey(EliteEightSecond))
                        client.Entity.RemoveTopStatus((ulong)EliteEightSecond);
                    if (client.Entity.Titles.ContainsKey(EliteEightThird))
                        client.Entity.RemoveTopStatus((ulong)EliteEightThird);
                    if (client.Entity.Titles.ContainsKey(EliteEight))
                        client.Entity.RemoveTopStatus((ulong)EliteEight);
                    #endregion
                }
            }
            if (Now64.Hour >= ElitePK.EventTime + 1 && ElitePKTournament.TimersRegistered)
            {
                bool done = true;
                foreach (var epk in ElitePKTournament.Tournaments)
                    if (epk.Players.Count != 0)
                        done = false;
                if (done)
                {
                    ElitePKTournament.TimersRegistered = false;
                    ElitePKBrackets brackets = new ElitePKBrackets(true, 0);
                    brackets.Type = ElitePKBrackets.EPK_State;
                    brackets.OnGoing = false;
                    foreach (var client in Server.GamePool)
                        client.Send(brackets);
                }
            }
            #endregion
            #region ExtremePK Tournament
            foreach (var client in Server.GamePool)
            {
                if (client.Entity.MapID == 12345 || client.Entity.MapID == 12346)
                {
                    if (Now64.DayOfWeek == DayOfWeek.Sunday && Now64.Hour == 21 && Now64.Minute == 07 && Now64.Second == 02 && client.Entity.Hitpoints > 0)
                    {
                        client.Entity.Teleport(1812, 50, 50);
                    }
                }
            }
            if (Now64.DayOfWeek == DayOfWeek.Sunday && Now64.Hour == 21 && Now64.Minute == 02 && Now64.Second <= 0)
            {
                Game.Entity.name = new object[] { "ExtremePK Tournament has started registering to go to TC ExtremePK join?" };
                Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[ExtremePK Tournament]", System.Drawing.Color.Red, 2500), Server.GamePool);
                foreach (var client in Server.GamePool)
                {
                    if (!client.InQualifier() && client.Entity.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 && !client.Entity.Dead)
                    {
                        AutoInvite alert = new AutoInvite
                        {
                            StrResID = 10553,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10553;
                        client.Send(alert.ToArray());
                    }
                }
            }
            #endregion
            #region Treasure in Blue
            //if (Now64.DayOfWeek == DayOfWeek.Monday || Now64.DayOfWeek == DayOfWeek.Saturday)
            //{
            //    if ((Now64.Hour == 12 || Now64.Hour == 20) && Now64.Minute == 30 && Now64.Second <= 1)
            //    {
            //        Kernel.SendWorldMessage(new Network.GamePackets.Message("Treasure in Blue has begun! Go to Twin City to signup at SquidwardOctopus!", System.Drawing.Color.White, Network.GamePackets.Message.Center), Server.GamePool);
            //        //Game.Entity.name = new object[] { "Treasure in Blue has begun! Go to Twin City to signup at SquidwardOctopus!" };
            //       Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[Treasure in Blue]", System.Drawing.Color.Red, 2500), Server.GamePool);
            //        Kernel.TreasureinBlue = true;
            //    }
            //}
            //if (Now64.DayOfWeek == DayOfWeek.Monday || Now64.DayOfWeek == DayOfWeek.Saturday)
            //{
            //    if ((Now64.Hour == 13 || Now64.Hour == 21) && Now64.Minute == 30 && Now64.Second <= 1)
            //    {
            //        Kernel.SendWorldMessage(new Network.GamePackets.Message("Treasure in Blue has been closed. Please try next time!", System.Drawing.Color.White, Network.GamePackets.Message.Center), Server.GamePool);
            //        //Game.Entity.name = new object[] { "Treasure in Blue has been closed. Please try next time!" };
            //        Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[Treasure in Blue]", System.Drawing.Color.Red, 2500), Server.GamePool);
            //        Kernel.TreasureinBlue = false;
            //        foreach (var client in Server.GamePool)
            //        {
            //            if (client.Player.MapID == 3071)
            //            {
            //                client.Player.Teleport(1002, 297, 282);
            //            }
            //        }
            //    }
            //}
            #endregion
            #region Power Arena
            if (DateTime.Now.Hour == 13 && DateTime.Now.Minute == 05 && DateTime.Now.Second <= 1 || (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 05 && DateTime.Now.Second <= 1))
            {
                foreach (var client in Server.GamePool)
                {
                    if (!client.InQualifier() && client.Entity.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 && !client.Entity.Dead)
                    {
                        AutoInvite alert = new AutoInvite
                        {
                            StrResID = 10531,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10531;
                        client.Send(alert.ToArray());
                    }
                }
            }
            #endregion
        }
        private void ServerFunctions(int time)
        {
            Game.Features.CpGrantDelivery.Pulse();
            Game.Features.VoteGrantDelivery.Pulse();
            DateTime Now64 = DateTime.Now;
            #region CaptureTheFlag
            //if (Now64.DayOfWeek == DayOfWeek.Monday || Now64.DayOfWeek == DayOfWeek.Friday)
            //{
            //    if (Now64.Hour == 21 && Now64.Minute >= 01 && !CaptureTheFlag.IsWar)
            //    {
            //        CaptureTheFlag.IsWar = true;
            //        CaptureTheFlag.StartTime = DateTime.Now;
            //        CaptureTheFlag.ClearHistory();
            //        foreach (var current in Kernel.Guilds.Values)
            //        {
            //            current.CTFFlagScore = 0;
            //            current.CTFPoints = 0;
            //            current.CTFdonationCPs = 0;
            //            current.CTFdonationSilver = 0;
            //            current.CalculateCTFRank(true);
            //            foreach (var current2 in current.Members.Values)
            //            {
            //                current2.Exploits = 0;
            //                current2.ExploitsRank = 0;
            //                current2.CTFCpsReward = 0;
            //                current2.CTFSilverReward = 0;
            //            }
            //            current.CalculateCTFRank(false);
            //        }
            //        Game.Entity.name = new object[] { "Captura la bandera ha comenzado! Si desea unirse valla a Ciudad Dragón y únase en la Controladora de Clanes" };
            //        Kernel.SendWorldMessage(new Message(string.Concat(Game.Entity.name), "ALLUSERS", "[la Bandera]", System.Drawing.Color.Red, 2500), Server.GamePool);
            //        foreach (var client in Server.GamePool)
            //        {
            //            if (!client.InQualifier() && client.Player.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 &&!client.Player.Dead)
            //            {
            //                AutoInvite alert = new AutoInvite
            //                {
            //                    StrResID = 10535,
            //                    Countdown = 60,
            //                    Action = 1
            //                };
            //                client.Player.StrResID = 10535;
            //                client.Send(alert.ToArray());
            //            }
            //        }
            //    }
            //}
            //if (CaptureTheFlag.IsWar)
            //{
            //    foreach (var State in Server.GamePool)
            //    {
            //        if (State.Player.MapID == 2057)
            //        {
            //            CaptureTheFlag.SortScoresJoining(State, out State.Guild);
            //            CaptureTheFlag.SendScores();
            //        }
            //    }
            //    Server.Thread.CTF.SendUpdates();
            //    if (Now64 > CaptureTheFlag.StartTime.AddHours(1))
            //    {
            //        CaptureTheFlag.IsWar = false;
            //        CaptureTheFlag.Close();
            //    }
            //}
            //if (CTF != null)
            //    CTF.SpawnFlags();
            #endregion
            #region Save & Restart
            /*if (((DateTime.Now.Hour == 7) && (DateTime.Now.Minute == 59)))
            {
                Server.CommandsAI("@test");
            }*/
            #endregion
            #region Gold Check & Cps
            if (DateTime.Now.Second > 1)
            {
                foreach (var client in Server.GamePool)
                {
                    //son libre de el oro xd
                    if (client.Entity.ConquerPoints >= 2000000001)
                    {
                        client.Entity.ConquerPoints = 2000000000;
                        client.Send(new Message("Sorry, but you can't have more than 2,000,000,000 Cps!", Color.Red, Message.Talk));
                    }
                }
            }
            #endregion
            #region Dia y Noche
            //Dia
            //if (((DateTime.Now.Hour == 06) && (DateTime.Now.Minute == 00)))
            //{
            //    Random disco = new Random();
            //    uint discocolor = (uint)disco.Next(50000, 999999999);
            //    GameState.ScreenColor = 0;
            //    foreach (Client.GameState C in Kernel.GamePool.Values)
            //    {
            //        Data datas = new Data(true);
            //        datas.UID = C.Player.UID;
            //        datas.ID = 104;
            //        datas.dwParam = 0;
            //        datas.wParam1 = (ushort)GameState.ScreenColor;
            //        datas.wParam2 = (ushort)GameState.ScreenColor;
            //        datas.wParam2 = 104;
            //        datas.wParam4 = (ushort)GameState.ScreenColor;
            //        C.Send(datas);
            //    }
            //}
            //Noche
            if ((DateTime.Now.Day == 31) )
            //if (((DateTime.Now.Hour >= 18) && (DateTime.Now.Hour <= 06)))
            {
                Random disco = new Random();
                uint discocolor = (uint)disco.Next(50000, 999999999);
                GameState.ScreenColor = 5855577;
                foreach (Client.GameState C in Kernel.GamePool.Values)
                {
                    Data datas = new Data(true);
                    datas.UID = C.Entity.UID;
                    datas.ID = 104;
                    GameState.ScreenColor = 5855577;
                    datas.dwParam = 5855577;
                    C.Send(datas);
                }
            }
            #endregion
            #region FakeLoad
            foreach (Client.GameState client in Kernel.GamePool.Values)
            {
                if (client.Fake)
                {
                    if (client.Entity != null && (CityGuardSystem.IsCityGuard(client.Entity)
                        || LivingWorldSystem.IsAdventurer(client.Entity) || ArenaActors.IsBot(client)))
                        continue;
                    if (!client.SignedUpForEPK)
                        ElitePKTournament.SignUp(client);
                    if (client.ElitePKMatch != null)
                    {
                        if (client.ElitePKMatch.OnGoing && client.ElitePKMatch.Inside)
                        {
                            if (Time32.Now > client.FakeQuit.AddSeconds(5))
                            {
                                client.FakeQuit = Time32.Now;
                                if (Kernel.Rate(1, 10))
                                {
                                    client.ElitePKMatch.End(client);
                                }
                            }
                        }
                    }
                }
            }
            #endregion
            #region GoldenTree
            if (Kernel.GoldenTree == null)
            {
                Kernel.GoldenTree = Database.GoldenTreeTable.Conditions.Values.OrderByDescending(i => DateTime.Now <= i.StartTime).FirstOrDefault();
            }
            #endregion
            
            var kvpArray = Kernel.GamePool.ToArray();
            foreach (var kvp in kvpArray)
                if (kvp.Value == null || kvp.Value.Entity == null)
                    Revora.Game.Features.SessionInstall.DropVacant(kvp.Key, kvp.Value);
            Server.GamePool = Kernel.GamePool.Values.ToArray();
            int humans = 0;
            foreach (var online in Kernel.GamePool.Values)
                if (online != null && !online.Fake)
                    humans++;
            if (humans > Server.MaxPlayers)
                Server.MaxPlayers = humans;
            Console.Title = "[ " + Constants.ServerName + " 3.0 ] --  Online players : " + humans + " Max Online : " + Server.MaxPlayers + "  !";
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            Server.SaveAllocationCounters();
            Database.EntityVariableTable.Save(0, Server.Vars);
            if (Kernel.BlackSpoted.Values.Count > 0)
            {
                foreach (var spot in Kernel.BlackSpoted.Values)
                {
                    if (Time32.Now >= spot.BlackSpotStamp.AddSeconds(spot.BlackSpotStepSecs))
                    {
                        if (spot.Dead && spot.EntityFlag == EntityFlag.Player)
                        {
                            foreach (var h in Server.GamePool)
                            {
                                h.Send(Kernel.BlackSpotPacket.ToArray(false, spot.UID));
                            }
                            Kernel.BlackSpoted.Remove(spot.UID); continue;
                        }
                        foreach (var h in Server.GamePool)
                        {
                            h.Send(Kernel.BlackSpotPacket.ToArray(false, spot.UID));
                        }
                        spot.IsBlackSpotted = false;
                        Kernel.BlackSpoted.Remove(spot.UID);
                    }
                }
            }
            DateTime Now = DateTime.Now;
            if (Now > Game.ConquerStructures.Broadcast.LastBroadcast.AddMinutes(1))
            {
                
                if (Game.ConquerStructures.Broadcast.Broadcasts.Count > 0)
                {
                    Game.ConquerStructures.Broadcast.CurrentBroadcast = Game.ConquerStructures.Broadcast.Broadcasts[0];
                    Game.ConquerStructures.Broadcast.Broadcasts.Remove(Game.ConquerStructures.Broadcast.CurrentBroadcast);
                    Game.ConquerStructures.Broadcast.LastBroadcast = Now;
                    Kernel.SendWorldMessage(new Message(Game.ConquerStructures.Broadcast.CurrentBroadcast.Message, "ALLUSERS", Game.ConquerStructures.Broadcast.CurrentBroadcast.EntityName, System.Drawing.Color.Red, Message.BroadcastMessage), Server.GamePool);
                }
                else Game.ConquerStructures.Broadcast.CurrentBroadcast.EntityID = 1;
                
            }
            if (Now > Server.LastRandomReset.AddMinutes(30))
            {
                Server.LastRandomReset = Now;
                Kernel.Random = new FastRandom(Server.RandomSeed);
            }
            Server.Today = Now.DayOfWeek;
        }
        private void ArenaFunctions(int time)
        {
            Game.Qualifier.EngagePlayers();
            Game.Qualifier.CheckGroups();
            Game.Qualifier.VerifyAwaitingPeople();
            Game.Qualifier.Reset();
        }
        private void TeamArenaFunctions(int time)
        {
            Game.TeamQualifier.PickUpTeams();
            Game.TeamQualifier.EngagePlayers();
            Game.TeamQualifier.CheckGroups();
            Game.TeamQualifier.VerifyAwaitingPeople();
            Game.TeamQualifier.Reset();
        }
        private void ChampionFunctions(int time)
        {
            Game.Champion.EngagePlayers();
            Game.Champion.CheckGroups();
            Game.Champion.VerifyAwaitingPeople();
            Game.Champion.Reset();
        }

        #region Functions
        public static void Execute(Action<int> action, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            GenericThreadPool.Subscribe(new LazyDelegate(action, timeOut, priority));
        }
        public static void Execute<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            GenericThreadPool.Subscribe<T>(new LazyDelegate<T>(action, timeOut, priority), param);
        }
        public static IDisposable Subscribe(Action<int> action, int period = 1, ThreadPriority priority = ThreadPriority.Normal)
        {
            return GenericThreadPool.Subscribe(new TimerRule(action, period, priority));
        }
        public static IDisposable Subscribe<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            return GenericThreadPool.Subscribe<T>(new TimerRule<T>(action, timeOut, priority), param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param, StandalonePool pool)
        {
            return pool.Subscribe<T>(rule, param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param, StaticPool pool)
        {
            return pool.Subscribe<T>(rule, param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param)
        {
            return GenericThreadPool.Subscribe<T>(rule, param);
        }
        #endregion
    }
}
