using System;
using System.IO;
using System.Text;
using Revora.Game;
using Revora.Client;
using Conquer.Database;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Collections.Concurrent;

namespace Revora.Database
{
    public unsafe static class EntityTable
    {
        public static bool LoadEntity(Client.GameState client)
        {
            if (!LoadEntityState(client)) return false;
            try { Game.Features.CpGrantDelivery.ApplyFor(client); } catch (Exception ex) { Console.WriteLine("[shop] load grant " + ex.Message); }
            try { Game.Features.VoteGrantDelivery.ApplyFor(client); } catch (Exception ex) { Console.WriteLine("[vote] load grant " + ex.Message); }
            return true;
        }
        // Game-state loading is independent from external website grant delivery.
        // Production login always follows this with both grant providers above.
        internal static bool LoadEntityState(Client.GameState client)
        {
            CharacterActivity.Forget(client);
            Game.Features.CharacterProgression.Forget(client);
            Game.Features.VipState.Forget(client);
            Game.Features.SilverBalance.Forget(client);
            Game.Features.PkBalance.Forget(client);
            Game.Features.BlessingBalance.Forget(client);
            Game.Features.DoubleExperienceBalance.Forget(client);
            Game.Features.EnlightenmentBalance.Forget(client);
            Game.Features.TimeDropBalance.Forget(client);
            Game.Features.ExpProtectionBalance.Forget(client);
            Game.Features.AttackDefenseBalance.Forget(client);
            Game.Features.HealingMedicineBalance.Forget(client);
            Game.Features.HealthBalance.Forget(client);
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("UID", client.Account.EntityID))
            using (var reader = new MySqlReader(cmd))
            {
                if (reader.Read())
                {
                    client.WarehousePW = reader.ReadUInt32("WarehousePW");
                    client.Entity = new Game.Entity(Game.EntityFlag.Player, false);
                    client.Entity.Name = reader.ReadString("Name").Remove("");
                    client.HeadgearClaim = reader.ReadBoolean("HeadgearClaim");
                    client.Entity.Spouse = reader.ReadString("Spouse");
                    client.Entity.Owner = client;
                    client.Entity.AddFlower = reader.ReadUInt32("Flower");
                    client.MoneySave = reader.ReadUInt64("MoneySave");
                    client.Entity.Experience = reader.ReadUInt64("Experience");
                    //client.Player.Money = reader.ReadUInt32("Money");
                    client.Entity.Money = reader.ReadUInt64("Money");
                    Game.Features.SilverBalance.Loaded(client, reader);
                    Game.Features.BlessingBalance.Loaded(client, reader);
                    client.Entity.NumReferidos = reader.ReadUInt32("NumReferidos");
                    client.Entity.NumReferidosTotal = reader.ReadUInt32("NumReferidosTotal");
                    client.Entity.TareaInner = reader.ReadUInt32("TareaInner");
                    client.Entity.ChangeBranch = reader.ReadByte("ChangeBranch");
                    #region Change branch
                    if (client.Entity.ChangeBranch >= 1)
                    {
                        client.Entity.ChangeBranch -= 1;
                    }
                    #endregion
                    client.Entity.FirstCreditPoints = reader.ReadUInt32("FirstCreditPoints");
                    client.Entity.UID = reader.ReadUInt32("Uid");
                    client.Entity.Hitpoints = reader.ReadUInt32("Hitpoints");
                    Game.Features.HealthBalance.Loaded(client, reader);
                    client.Entity.BankCps = reader.ReadUInt32("BankCps");
                    client.Entity.TareaCopa = reader.ReadUInt32("TareaCopa");
                    client.Entity.OnlinePoints = reader.ReadUInt32("OnlinePoints");
                    #region Saved kill/death counts
                    client.Entity.Asesinatos = reader.ReadUInt32("Asesinatos");
                    client.Entity.Muertes = reader.ReadUInt32("Muertes");
                    #endregion
                    client.Entity.Appearance = (Enums.AppearanceType)reader.ReadUInt32("Appearance");
                    client.Entity.Quest = reader.ReadUInt32("Quest");
                    client.Entity.QuizPoints = reader.ReadUInt32("QuizPoints");
                    client.Entity.Body = reader.ReadUInt16("Body");
                    client.Entity.Face = reader.ReadUInt16("Face");
                    client.Entity.TitlePoints = reader.ReadInt32("TitlePoints");
                    client.Entity.Strength = reader.ReadUInt16("Strength");
                    client.Entity.Titles = new ConcurrentDictionary<TitlePacket.Titles, DateTime>();
                    client.Entity.MyTitle = (TitlePacket.Titles)reader.ReadUInt32("My_Title");
                    client.Entity.Agility = reader.ReadUInt16("Agility");
                    client.Entity.Spirit = reader.ReadUInt16("Spirit");
                    client.Entity.Vitality = reader.ReadUInt16("Vitality");
                    client.Entity.Atributes = reader.ReadUInt16("Atributes");
                    client.ElitePKStats = new ElitePK.FighterStats(client.Entity.UID, client.Entity.Name, client.Entity.Mesh);
                    client.Entity.SubClass = reader.ReadByte("SubClass");
                    client.Entity.SubClassLevel = reader.ReadByte("SubClassLevel");
                    client.Entity.SubClasses.Active = client.Entity.SubClass;
                    client.Entity.SubClassesActive = client.Entity.SubClass;
                    client.Entity.SubClasses.StudyPoints = reader.ReadUInt16("StudyPoints");
                    client.VirtuePoints = (uint)reader.ReadUInt32("VirtuePoints");
                    Game.Features.VirtueBalance.Loaded(client, reader);
                    client.TQPoint = (uint)reader.ReadUInt32("TQPoint");
                    client.Entity.Mana = reader.ReadUInt16("Mana");
                    client.Entity.HairStyle = reader.ReadUInt16("HairStyle");
                    Game.Features.AppearanceState.Loaded(client, reader);
                    if (reader.ReadUInt32("UnionID") > 0)
                    {
                        client.RestoreUnion(Kernel.Unions[reader.ReadUInt32("UnionID")],reader.ReadUInt16("Offical"),
                            reader.ReadUInt16("Harem"),reader.ReadUInt16("Guards"),reader.ReadUInt32("UnionExploits"));
                    }
                    else client.RestoreUnion(null,0,0,0,0);
                    client.Entity.Windwalker = reader.ReadByte("Windwalker");
                    client.Entity.TreasuerPoints = reader.ReadUInt32("TreasuerPoints");;
                    client.Entity.CPSgold = Time32.Now;
                    client.Entity.MapID = reader.ReadUInt16("MapID");
                    Game.Features.BattlePowerTournament.LoadedLocation(client, client.Entity.MapID);
                    client.VendingDisguise = reader.ReadUInt16("VendingDisguise");
                    client.Entity.CountryID = (Game.Enums.CountryID)reader.ReadUInt32("CountryID");
                    client.Entity.BoundCps = (uint)reader.ReadUInt32("BoundCps");
                    client.Entity.Lukypionts = (ushort)reader.ReadUInt32("LuckyPoints");
                    client.Entity.UpdateList = (uint)reader.ReadUInt32("updatelist");
                    client.Entity.ClaimedSTeamPK = reader.ReadBoolean("ClaimedSTeamPK");
                    client.Entity.ClaimedTeamPK = reader.ReadBoolean("ClaimedTeamPK");
                    client.Entity.ClaimedElitePk = reader.ReadBoolean("ClaimedElitePk");
                    Game.Features.PurseBalance.Loaded(client, reader);
                    client.Entity.OnlineTraining = reader.ReadUInt32("OnlineTrainning");
                    client.Entity.BlessedHunting = reader.ReadUInt32("HuntingExp");
                    client.ForgetPassword = reader.ReadBoolean("ForgetPassword");
                    client.Entity.VoteSystem = reader.ReadBoolean("VoteSystem");
                    client.Entity.ClaimedFirstCredit = reader.ReadBoolean("FirstCredit");
                    client.Entity.SwordSoul = reader.ReadBoolean("SwordSoul");
                    client.Entity.BansheeSpirit = reader.ReadBoolean("BansheeSpirit");
                    client.Entity.ClaimedExp = reader.ReadBoolean("ClaimedExp");
                    client.Entity.ExtraInventory = reader.ReadByte("ExtraInventory");

                    #region DayliSignIn
                    client.Entity.DailySignVIPChances = reader.ReadByte("DailySignVIPChances");
                    client.Entity.DailySignRewards = reader.ReadByte("DailySignRewards");
                    client.Entity.DailySignDays = new byte[31];
                    var dailys = reader.ReadString("DailySignedDays").Split('#');
                    if (dailys.Length >= 30)
                    {
                        for (byte x = 0; x < client.Entity.DailySignDays.Length; x++)
                        {
                            client.Entity.DailySignDays[x] = byte.Parse(dailys[x]);
                        }
                    }
                    #endregion
                    if (client.VendingDisguise == 0)
                        client.VendingDisguise = 223;
                    client.Entity.X = reader.ReadUInt16("X");
                    client.Entity.Y = reader.ReadUInt16("Y");

                    if (Game.Features.Housing.Enabled && client.Entity.MapID >= HouseMapRegistry.First && client.Entity.MapID <= HouseMapRegistry.Last)
                    {
                        try { Game.Features.Housing.Load(client); }
                        catch (Exception ex) { Console.WriteLine("[game] house_location_load_failed type=" + ex.GetType().Name); return false; }
                    }

                    if (client.Map == null)
                    {
                        client.Entity.MapID = 1002;
                        client.Entity.X = 300;
                        client.Entity.Y = 278;
                    }
                    if (client.Map != null && (client.Map.BaseID == 10360 || client.Entity.MapID == 1950 ||
                    #region CPs bet
                        /*client.Player.MapID == 4573 || client.Player.MapID == 4574 ||
                        client.Player.MapID == 4575 || client.Player.MapID == 4576 ||
                        client.Player.MapID == 4577 || client.Player.MapID == 4578 ||*/
                    #endregion
                        client.Entity.MapID == 3333 || client.Entity.MapID == 3334 ||
                        client.Entity.MapID == 3335 ||client.Entity.MapID == 7777 ||
                        client.Entity.MapID == 50 || client.Entity.MapID == 60 ||
                        client.Entity.MapID == 1365 || client.Entity.MapID == 1366 ||
                        client.Entity.MapID == 1367 || client.Entity.MapID == 1368 ||
                        client.Entity.MapID == 1090 || client.Entity.MapID == 4021 ||
                        client.Entity.MapID == 4022 || client.Entity.MapID == 4023 ||
                        client.Entity.MapID == 4024 || client.Entity.MapID == 4025 ||
                        client.Entity.MapID == 1508 || client.Entity.MapID == 1518 ||
                        client.Entity.MapID == 7001 || client.Entity.MapID == 1801 ||
                        client.Entity.MapID == 2065 || client.Entity.MapID == 8883 ||
                        client.Entity.MapID == 2014 || client.Entity.MapID == 2072))
                    {
                        client.Entity.MapID = 1002;
                        client.Entity.X = 300;
                        client.Entity.Y = 278;
                    }
                    if (client.Map != null && client.Map.BaseID == 1005 && client.Entity.MapID != 1005)
                    {
                        client.Entity.MapID = 1002;
                        client.Entity.X = 300;
                        client.Entity.Y = 278;
                    }
                    if (client.Entity.MapID == 0)
                    {
                        client.Entity.MapID = 1002;
                        client.Entity.X = 300;
                        client.Entity.Y = 278;
                        if (client.Entity.Level == 0)
                        {
                            client.Entity.Level = 1;
                            client.Entity.AdjustMoney((decimal)(10000));
                        }
                    }
                    client.BlessTime = reader.ReadUInt32("BlessTime");
                    client.NecklaceClaim = reader.ReadBoolean("NecklaceClaim");
                    client.ArmorClaim = reader.ReadBoolean("ArmorClaim");
                    client.WeaponClaim = reader.ReadBoolean("WeaponClaim");
                    client.RingClaim = reader.ReadBoolean("RingClaim");
                    client.BootsClaim = reader.ReadBoolean("BootsClaim");
                    client.FanClaim = reader.ReadBoolean("FanClaim");
                    client.TowerClaim = reader.ReadBoolean("TowerClaim");
                    client.HeadgearClaim = reader.ReadBoolean("HeadgearClaim");
                    client.InLottery = reader.ReadBoolean("InLottery");
                    client.LotteryEntries = reader.ReadByte("LotteryEntries");
                    client.LastLotteryEntry = DateTime.FromBinary(reader.ReadInt64("LastLotteryEntry"));
                    if (client.Entity.MapID >= 7008 && !Game.Features.Housing.OwnsLocation(client) && !Game.Features.PirateEpicWorld.RestoresLocation(client))
                    {
                        client.Entity.MapID = 1002;
                        client.Entity.X = 300;
                        client.Entity.Y = 278;
                    }
                    client.Entity.PreviousMapID = reader.ReadUInt16("PreviousMapID");
                    client.Entity.PKPoints = reader.ReadUInt16("PKPoints");
                    Game.Features.PkBalance.Loaded(client, reader);
                    client.Entity.Class = reader.ReadByte("Class");
                    client.Entity.Reborn = reader.ReadByte("Reborn");
                    client.Entity.Level = reader.ReadByte("Level");
                    client.Entity.FirstRebornClass = reader.ReadByte("FirstRebornClass");
                    client.Entity.SecondRebornClass = reader.ReadByte("SecondRebornClass");
                    client.Entity.FirstRebornLevel = reader.ReadByte("FirstRebornLevel");
                    client.Entity.SecondRebornLevel = reader.ReadByte("SecondRebornLevel");
                    client.LastDragonBallUse = DateTime.FromBinary(reader.ReadInt64("LastDragonBallUse"));
                    client.LastResetTime = DateTime.FromBinary(reader.ReadInt64("LastResetTime"));
                    client.Entity.EnlightenPoints = reader.ReadUInt16("EnlightenPoints");
                    client.Entity.EnlightmentTime = reader.ReadUInt16("EnlightmentWait");
                    client.Entity.PuntosUnion = reader.ReadUInt32("PuntosUnion");
                    client.Entity.ReceivedEnlightenPoints = reader.ReadByte("EnlightsReceived");
                    Game.Features.EnlightenmentBalance.Loaded(client, reader);
                    client.Entity.DoubleExperienceTime = reader.ReadUInt16("DoubleExpTime");
                    Game.Features.DoubleExperienceBalance.Loaded(client, reader);
                    client.Entity.TimeDropSlayer = reader.ReadUInt16("TimeDropSlayer");
                    Game.Features.TimeDropBalance.Loaded(client, reader);
                    client.Entity.ExpProtectionTime = reader.ReadUInt16("ProtectionTime");
                    Game.Features.ExpProtectionBalance.Loaded(client, reader);
                    client.DoubleExpToday = reader.ReadBoolean("DoubleExpToday");
                    client.Entity.HeavenBlessing = reader.ReadUInt32("HeavenBlessingTime");
                    client.Entity.CursedTime = reader.ReadUInt32("CursedTime");
                    client.Entity.VIPLevel = reader.ReadByte("VIPLevel");
                    if (client.Entity.VIPLevel < 4)
                        client.Entity.VIPLevel = 4;
                    client.Entity.VIPDays = reader.ReadByte("VIPDays");
                    #region Reset Day VIP
                    if (DateTime.Now >= client.LastResetTime.AddDays(1))
                    {
                        
                        if (client.Entity.VIPDays >= 1)
                        {
                            client.Entity.VIPDays -= 1;
                            if (client.Entity.VIPLevel >= 6 && client.Entity.VIPDays == 0 )//jheisson   
                            {
                                client.Entity.VIPLevel = 5;
                                client.Entity.MyTitle = 0;
                            }
                        }

                    }
                    #endregion
                    client.Entity.VIPLevelDays = reader.ReadByte("VIPLevelDays");
                    client.Entity.PrevX = reader.ReadUInt16("PreviousX");
                    client.Entity.PrevY = reader.ReadUInt16("PreviousY");
                    client.ExpBalls = reader.ReadByte("ExpBalls");
                    client.Entity.Status4 = reader.ReadUInt32("Status4");
                    client.Entity.ClanId = reader.ReadUInt32("ClanId");
                    client.Entity.ClanRank = (Clan.Ranks)reader.ReadUInt32("ClanRank");
                   
                    CharacterActivity.Loaded(client, reader.ReadNullableInt64Strict("LastLogin"));
                    if (client.Entity.MapID == 601)
                        client.OfflineTGEnterTime = DateTime.FromBinary(reader.ReadInt64("OfflineTGEnterTime"));
                    Game.ConquerStructures.Nobility.Sort(client.Entity.UID);

                    if (Kernel.Guilds.ContainsKey(reader.ReadUInt32("GuildID")))
                    {
                        client.Guild = Kernel.Guilds[reader.ReadUInt32("GuildID")];
                        if (client.Guild.Members.ContainsKey(client.Entity.UID))
                        {
                            client.AsMember = client.Guild.Members[client.Entity.UID];
                            //client.AsMember.LastLogin = reader.ReadUInt32("GuildLastLogin");
                            if (client.AsMember.GuildID == 0)
                            {
                                client.AsMember = null;
                                client.Guild = null;
                            }
                            else
                            {
                                client.Entity.GuildID = (ushort)client.Guild.ID;
                                client.Entity.GuildRank = (ushort)client.AsMember.Rank;
                                // Guild power belongs to the shared guild and is rebuilt from its slots.
                                client.Guild.armors1 = false;
                                client.Guild.armors2 = false;
                                client.Guild.armors3 = false;
                                client.Guild.heads1 = false;
                                client.Guild.heads2 = false;
                                client.Guild.heads3 = false;
                                client.Guild.boots1 = false;
                                client.Guild.boots2 = false;
                                client.Guild.boots3 = false;
                                client.Guild.rings1 = false;
                                client.Guild.rings2 = false;
                                client.Guild.rings3 = false;
                                client.Guild.weapones1 = false;
                                client.Guild.weapones2 = false;
                                client.Guild.weapones3 = false;
                                client.Guild.necklaces1 = false;
                                client.Guild.necklaces2 = false;
                                client.Guild.necklaces3 = false;
                                client.Guild.fans1 = false;
                                client.Guild.fans2 = false;
                                client.Guild.fans3 = false;
                                client.Guild.towers1 = false;
                                client.Guild.towers2 = false;
                                client.Guild.towers3 = false;
                                client.Guild.tower = false;
                                client.Guild.fan = false;
                                client.Guild.necklace = false;
                                client.Guild.weapone = false;
                                client.Guild.ring = false;
                                client.Guild.head = false;
                                client.Guild.armor = false;
                                client.Guild.boot = false;
                                try { }
                                catch { }
                            }
                        }
                        else client.Guild = null;
                    }
                    if (!Game.ConquerStructures.Nobility.Board.TryGetValue(client.Entity.UID, out client.NobilityInformation))
                    {
                        client.NobilityInformation = new Game.ConquerStructures.NobilityInformation();
                        client.NobilityInformation.EntityUID = client.Entity.UID;
                        client.NobilityInformation.Name = client.Entity.Name;
                        client.NobilityInformation.Donation = 0;
                        client.NobilityInformation.Rank = Game.ConquerStructures.NobilityRank.Duke;
                        client.NobilityInformation.Position = -1;
                        client.NobilityInformation.Gender = 1;
                        client.NobilityInformation.Mesh = client.Entity.Mesh;
                        client.NobilityInformation.Rey = 0;
                        if (client.Entity.Body % 10 >= 3)
                            client.NobilityInformation.Gender = 0;
                    }
                    client.Entity.NobilityRank = client.NobilityInformation.Rank;
                    if (DateTime.Now.DayOfYear != client.LastResetTime.DayOfYear)
                    {
                        DialyQuestTable.Reset(client);
                        ActivenessTable.Reset(client);
                        if (client.Entity.Merchant == 1)
                        {
                            client.Entity.Merchant = 255;
                        }
                        client.AdjustChiPoints(500);
                        if (client.ChiPoints > 4000)
                            client.ChiPoints = 4000;
                        if (client.Entity.Level >= 90 && client.Entity.VIPLevel != 0)
                            client.Entity.ActivenessPoint += 5;
                        client.DoubleExpToday = false;
                        client.TimeDropSlayerToday = false;
                        client.ExpBalls = 0;
                        client.Entity.SwordSoul = false;
                        client.Entity.ClaimedExp = false;
                        client.Entity.BansheeSpirit = false;
                        client.LotteryEntries = 0;
                        client.Entity.Quest = 0;
                        client.LastResetTime = DateTime.Now;
                        ResetQuest(client);
                        ResetExpball(client);
                        ResetLottery(client);
                        ResetClaimedExp(client);
                        ResetSwordSoul(client);
                        ResetBansheeSpirit(client);
                    }
                    #region Team Arena
                    client.TeamArenaStatistic = TeamArenaTable.RecoverExisting(client.Entity.UID);
                    if (client.TeamArenaStatistic == null)
                    {
                        client.TeamArenaStatistic = new Network.GamePackets.TeamQualifierStatistic(true);
                        client.TeamArenaStatistic.EntityID = client.Entity.UID;
                        client.TeamArenaStatistic.Name = client.Entity.Name;
                        client.TeamArenaStatistic.Level = client.Entity.Level;
                        client.TeamArenaStatistic.Class = client.Entity.Class;
                        client.TeamArenaStatistic.Model = client.Entity.Mesh;
                        TeamArenaTable.InsertArenaStatistic(client);
                        client.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.NotSignedUp;
                        if (Game.TeamQualifier.ArenaStatistics.ContainsKey(client.Entity.UID))
                            Game.TeamQualifier.ArenaStatistics.Remove(client.Entity.UID);
                        Game.TeamQualifier.ArenaStatistics.Add(client.Entity.UID, client.TeamArenaStatistic);
                    }
                    else if (client.TeamArenaStatistic.EntityID == 0)
                    {
                        client.TeamArenaStatistic = new Network.GamePackets.TeamQualifierStatistic(true);
                        client.TeamArenaStatistic.EntityID = client.Entity.UID;
                        client.TeamArenaStatistic.Name = client.Entity.Name;
                        client.TeamArenaStatistic.Level = client.Entity.Level;
                        client.TeamArenaStatistic.Class = client.Entity.Class;
                        client.TeamArenaStatistic.Model = client.Entity.Mesh;
                        TeamArenaTable.InsertArenaStatistic(client);
                        client.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.NotSignedUp;
                        if (Game.TeamQualifier.ArenaStatistics.ContainsKey(client.Entity.UID))
                            Game.TeamQualifier.ArenaStatistics.Remove(client.Entity.UID);
                        Game.TeamQualifier.ArenaStatistics.Add(client.Entity.UID, client.TeamArenaStatistic);
                    }
                    else
                    {
                        client.TeamArenaStatistic.Level = client.Entity.Level;
                        client.TeamArenaStatistic.Class = client.Entity.Class;
                        client.TeamArenaStatistic.Model = client.Entity.Mesh;
                        client.TeamArenaStatistic.Name = client.Entity.Name;
                    }
                    Game.TeamQualifier.Clear(client);
                    #endregion
                    #region Arena
                    client.ArenaStatistic = ArenaTable.RecoverExisting(client.Entity.UID);
                    if (client.ArenaStatistic == null)
                    {
                        client.ArenaStatistic = new Revora.Network.GamePackets.QualifierStatistic(true);
                        client.ArenaStatistic.EntityID = client.Entity.UID;
                        client.ArenaStatistic.Name = client.Entity.Name;
                        client.ArenaStatistic.Level = client.Entity.Level;
                        client.ArenaStatistic.Class = client.Entity.Class;
                        client.ArenaStatistic.Model = client.Entity.Mesh;
                        client.ArenaPoints = ArenaTable.ArenaPointFill(client.Entity.Level);
                        client.ArenaStatistic.LastArenaPointFill = DateTime.Now;
                        ArenaTable.InsertArenaStatistic(client);
                        client.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.NotSignedUp;
                        if (Game.Qualifier.ArenaStatistics.ContainsKey(client.Entity.UID))
                            Game.Qualifier.ArenaStatistics.Remove(client.Entity.UID);
                        Game.Qualifier.ArenaStatistics.Add(client.Entity.UID, client.ArenaStatistic);
                    }
                    else if (client.ArenaStatistic.EntityID == 0)
                    {
                        client.ArenaStatistic = new Revora.Network.GamePackets.QualifierStatistic(true);
                        client.ArenaStatistic.EntityID = client.Entity.UID;
                        client.ArenaStatistic.Name = client.Entity.Name;
                        client.ArenaStatistic.Level = client.Entity.Level;
                        client.ArenaStatistic.Class = client.Entity.Class;
                        client.ArenaStatistic.Model = client.Entity.Mesh;
                        client.ArenaPoints = ArenaTable.ArenaPointFill(client.Entity.Level);
                        client.ArenaStatistic.LastArenaPointFill = DateTime.Now;
                        ArenaTable.InsertArenaStatistic(client);
                        client.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.NotSignedUp;
                        if (Game.Qualifier.ArenaStatistics.ContainsKey(client.Entity.UID))
                            Game.Qualifier.ArenaStatistics.Remove(client.Entity.UID);
                        Game.Qualifier.ArenaStatistics.Add(client.Entity.UID, client.ArenaStatistic);
                    }
                    else
                    {
                        client.ArenaStatistic.Level = client.Entity.Level;
                        client.ArenaStatistic.Class = client.Entity.Class;
                        client.ArenaStatistic.Model = client.Entity.Mesh;
                        client.ArenaStatistic.Name = client.Entity.Name;
                    }
                    client.ArenaPoints = client.ArenaStatistic.ArenaPoints;
                    client.CurrentHonor = client.ArenaStatistic.CurrentHonor;
                    client.HistoryHonor = client.ArenaStatistic.HistoryHonor;
                    Game.Qualifier.Clear(client);
                    #endregion
                    #region Champion
                    ChampionTable.LoadClient(client);
                    #endregion
                    JiangHu.LoadClient(client);
                    client.Quests.Load();
                    SetFlowers(client);
                    try { RewardDataBase.Load(client); } catch (Exception ex) { Console.WriteLine("[game] load Reward " + ex.Message); }
                    try { MerchantTable.Load(client); } catch (Exception ex) { Console.WriteLine("[game] load Merchant " + ex.Message); }
                    try { DialyQuestTable.Load(client); } catch (Exception ex) { Console.WriteLine("[game] load DialyQuest " + ex.Message); }
                    try { ActivenessTable.Load(client); } catch (Exception ex) { Console.WriteLine("[game] load Activeness " + ex.Message); }
                    try { MailboxTable.Load(client); } catch (Exception ex) { Console.WriteLine("[game] load Mailbox " + ex.Message); }
                    try { HouseTable.Load(client); } catch (Exception ex) { Console.WriteLine("[game] load House " + ex.GetType().Name); if (Game.Features.Housing.Enabled) return false; }
                    try { PkExploitsTable.Load(client); } catch (Exception ex) { Console.WriteLine("[game] load PkExploits " + ex.Message); }
                    client.Entity.LoadTopStatus();
                    try { LoadAchievement(client); }
                    catch (Exception ex) { Console.WriteLine("[game] achievement_load_failed uid=" + client.Entity.UID + " type=" + ex.GetType().Name); return false; }
                    Game.Features.Tournaments.LocalTeamPkAdmissions.Loaded(client);
                    Game.Features.KingdomCompetitionField.Loaded(client);
                    Game.Features.CharacterProgression.Loaded(client, reader);
                    Game.Features.VipState.Loaded(client, reader);
                    Game.Features.EnlightenmentProgress.Loaded(client, DateTime.FromBinary(reader.ReadInt64("LastResetTime")));
                    client.Entity.FullyLoaded = true;
                    Game.Features.AttackDefenseBalance.Loaded(client);
                    Game.Features.HealingMedicineBalance.Loaded(client);
                    // Load boost ownership now; equipment, subclasses and Chi are loaded later.
                    Game.Features.BloodshedSeaBalance.Loaded(client, false);
                    client.Entity.JustCreated = false;
                    if (client.Entity.VIPLevel < 4)
                        client.Entity.VIPLevel = 4;
                    return true;
                }
                else return false;
            }
        }
        public static bool LoadAchievement(GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("achievement").Where("UID", client.Account.EntityID))
            using (var reader = new MySqlReader(cmd))
            {
                if (reader.Read())
                {
                    client.Entity.MyAchievement = new Achievement(client.Entity);
                    client.Entity.MyAchievement.Load(reader.ReadString("Achievement"));
                }
                else
                {
                    Console.WriteLine("New data to save for [" + client.Entity.Name + "]");
                    using (var com = new Database.MySqlCommand(MySqlCommandType.INSERT))
                        com.Insert("Achievement").Insert("UID", client.Account.EntityID)
                            .Insert("Owner", client.Account.Username)
                            .Insert("Name", client.Entity.Name).Execute();
                    client.Entity.MyAchievement = new Achievement(client.Entity);
                }
                return true;
            }
        }
        public static void SaveUnionID(Client.GameState client, uint ID)
        {
            UpdateData(client, "UnionID", ID);
        }
        public static void SaveUnionR(Client.GameState client, Game.Enums.GuildMemberRank ID)
        {
            UpdateData(client, "UnionRank", (ushort)ID);
        } 
        public static void SaveGuildCP(Client.GameState client, ulong ID)
        {
            UpdateData(client, "GuildConquerPointDonation", ID);
        }
        public static void SaveGuildSDs(Client.GameState client, ulong ID)
        {
            UpdateData(client, "GuildSilverDonation", ID);
        }
        public static void SaveGuildR(Client.GameState client, Game.Enums.GuildMemberRank ID)
        {
            UpdateData(client, "GuildRank", (ushort)ID);
        }
        public static void UpdateLuckyPoints(Client.GameState client)
        {
            UpdateData(client, "LuckyPoints", client.Entity.Lukypionts);
        }
        public static void UpdateData(Client.GameState client, string column, object value)
        {
            UpdateData(client.Entity.UID, column, value);
        }
        public static void UpdateReferidos(Client.GameState client)
        {
            UpdateData(client, "NumReferidos", client.Entity.NumReferidos);
            UpdateData(client, "NumReferidosTotal", client.Entity.NumReferidosTotal);
        }
        public static void UpdateReferidosPJ(uint UID)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("entities").Set("NumReferidos", +1).Set("NumReferidosTotal", +1).Where("UID", UID)
                    .Execute();
        }
        public static void UpdateData(uint UID, string column, object value)
        {
            if (string.Equals(column, "money", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use the owned silver persistence API.");
            if (string.Equals(column, "conquerpoints", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column, "revorapoint", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use the owned CP or Revora Point persistence API.");
            UpdateDataCore(UID, column, value);
        }
        private static void UpdateDataCore(uint UID, string column, object value)
        {
            if (value is Boolean)
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("entities").Set(column, (Boolean)value).Where("UID", UID)
                        .Execute();
            }
            else
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("entities").Set(column, value.ToString()).Where("UID", UID)
                        .Execute();
            }
        }
        public static void UpdateGuildRank(uint UID, Game.Enums.GuildMemberRank rank)
        {
            UpdateData(UID, "GuildRank", (int)rank);
        }
        public static void UpdateOnlineStatus(Client.GameState client, bool online, Npgsql.NpgsqlConnection conn)
        {
            if (online || (!online && client.DoSetOffline))
            {
                UpdateData(client, "Online", online);
            }
        }
        public static void UpdateOnlineStatus(Client.GameState client, bool online)
        {
            if (online || (!online && client.DoSetOffline))
            {
                UpdateData(client, "Online", online);
            }
        }
        public static void UpdateOffline(uint UID, string column, object value)
        {
            UpdateData(UID, column, value);
        }
        public static void UpdateArsenalDonation(Client.GameState client)
        {
            UpdateData(client, "ArsenalDonation", client.AsMember.Arsenal_Donation);
        }
        public static void SaveGuildSD(Client.GameState client, ulong ID)
        {
            UpdateData(client, "TotalDoantionGuild", ID);
        }
        public static void SaveGuildID(Client.GameState client, uint ID)
        {
            UpdateData(client, "GuildID", ID);
        }
        public static void LoginNow(Client.GameState client)
        {
            MySqlCommand cmd = new MySqlCommand(MySqlCommandType.UPDATE);
            cmd.Update("entities").Set("GuildLastLogin", Network.PacketHandler.UnixTimestamp).Where("UID", client.Entity.UID).Execute();
        }
        public static void UpdateCps(Client.GameState client)
        {
            if (client == null || client.Entity == null) return;
            lock (client.ItemSyncRoot ?? client)
            {
                if (Game.Features.PurseBalance.Managed(client))
                    Game.Features.PurseBalance.Save(client, true);
                else
                    UpdateDataCore(client.Entity.UID, "ConquerPoints", client.Entity.ConquerPoints);
            }
        }
        public static void UpdateRevoraPoint(Client.GameState client)
        {
            if (client == null || client.Entity == null) return;
            lock (client.ItemSyncRoot ?? client)
            {
                if (Game.Features.PurseBalance.Managed(client))
                    Game.Features.PurseBalance.Save(client, false);
                else
                    UpdateDataCore(client.Entity.UID, "RevoraPoint", client.Entity.RevoraPoint);
            }
        }
        public static void UpdateMoney(GameState client)
        {
            Game.Features.SilverBalance.Save(client);
        }
        public static void UpdateCps(uint uid, uint value)
        {
            if (value == 0) return;
            Client.GameState online;
            if (Kernel.GamePool != null && Kernel.GamePool.TryGetValue(uid, out online)
                && online != null && online.Entity != null && !online.Fake)
            {
                online.Entity.AddConquerPoints(value);
                return;
            }
            using (var conn = DataHolder.MySqlConnection)
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE entities SET conquerpoints=conquerpoints+@add WHERE uid=@uid AND conquerpoints IS NOT NULL AND conquerpoints<=@limit";
                    DataHolder.AddParameter(cmd, "@add", (long)value);
                    DataHolder.AddParameter(cmd, "@uid", (long)uid);
                    DataHolder.AddParameter(cmd, "@limit", (long)(uint.MaxValue - value));
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static void UpdateMoney(uint uid, uint value)
        {
            Game.Features.SilverBalance.Credit(uid, value);
        }
        public static void UpdateLevel(Client.GameState client)
        {
            UpdateData(client, "Level", client.Entity.Level);
        }
        public static void UpdateGuildID(Client.GameState client)
        {
            UpdateData(client, "guildid", client.Entity.GuildID);
        }
        public static void UpdateClanID(Client.GameState client)
        {
            UpdateData(client, "ClanId", client.Entity.ClanId);
        }
        public static void RemoveClan(Client.GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("entities").Set("ClanId", 0).Set("ClanDonation", 0).Set("ClanRank", 0)
                    .Where("ClanId", client.Entity.ClanId).Execute();
        }
        public static void RemoveClanMember(string name)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("entities").Set("ClanId", 0).Set("ClanDonation", 0).Set("ClanRank", 0).Where("Name", name).Execute();
        }
        public static void UpdateClanRank(Client.GameState client)
        {
            UpdateData(client, "ClanRank", (uint)client.Entity.ClanRank);
        }
        public static void UpdateClanRank(uint UID, uint rank)
        {
            UpdateData(UID, "ClanRank", rank);
        }
        public static void UpdateClanDonation(Client.GameState client)
        {
            UpdateData(client, "clandonation", (uint)client.Entity.ClanRank);
        }
        public static void UpdateGuildRank(Client.GameState client)
        {
            UpdateData(client, "GuildRank", client.Entity.GuildRank);
        }
        public static void UpdateSkillExp(Client.GameState client, uint spellid, uint exp)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("skills")
                    .Set("Experience", exp)
                    .Where("EntityID", client.Entity.UID)
                    .And("ID", spellid)
                    .Execute();
        }
        
        public static void ResetLottery(Client.GameState client)
        {
            UpdateData(client, "LotteryEntries", 0);
        }
        public static void ResetQuest(Client.GameState client)
        {
            UpdateData(client, "Quest", 0);
        }
        public static void ResetExpball(Client.GameState client)
        {
            UpdateData(client, "ExpBalls", 0);
        }
        public static void ResetClaimedExp(Client.GameState client)
        {
            UpdateData(client, "ClaimedExp", 0);
        }
        public static void ResetSwordSoul(Client.GameState client)
        {
            UpdateData(client, "SwordSoul", 0);
        }
        public static void ResetBansheeSpirit(Client.GameState client)
        {
            UpdateData(client, "BansheeSpirit", 0);
        }

        public static bool SaveEntity(Client.GameState c, Npgsql.NpgsqlConnection conn)
        {
            lock (c.ItemSyncRoot ?? c)
            lock (c.VirtueSyncRoot)
            lock (c.PkSyncRoot)
            lock (c.Entity.HealthMutationSync)
                return SaveEntityCore(c, conn);
        }
        private static bool SaveEntityCore(Client.GameState c, Npgsql.NpgsqlConnection conn)
        {
            Game.Entity e = c.Entity;
            if (e.PersistenceRequiresReload) return false;
            if (e.JustCreated && !e.FullyLoaded) return true;
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("entities"))
            {
                #region DailySign
                string DailySign = "";
                foreach (var index in e.DailySignDays)
                {
                    DailySign += index.ToString() + "#";
                }
                #endregion
                cmd.Set("WarehousePW", c.WarehousePW)
                    .Set("Spouse", e.Spouse ?? "None")
                    .Set("Money", e.Money)
                    .Set("Experience", e.Experience)
                    .Set("ConquerPoints", e.ConquerPoints)
                    .Set("TareaInner", e.TareaInner)
                    .Set("BankCps", e.BankCps)
                    .Set("TareaCopa", e.TareaCopa)
                    .Set("OnlinePoints", e.OnlinePoints)
                    .Set("Asesinatos", e.Asesinatos)
                    .Set("Muertes", e.Muertes)
                    .Set("Windwalker", e.Windwalker)
                    .Set("ChangeBranch", e.ChangeBranch)
                    .Set("TreasuerPoints", e.TreasuerPoints)
                    .Set("Body", e.Body)
                    .Set("Face", e.Face)
                    .Set("Class", e.Class)
                    .Set("Reborn", e.Reborn)
                    .Set("Level", e.Level)
                    .Set("Status", e.Status)
                    .Set("Status2", e.Status2)
                    .Set("Status3", e.Status3)
                    .Set("Status4", e.Status4)
                    .Set("NumReferidos", e.NumReferidos)
                    .Set("NumReferidosTotal", e.NumReferidosTotal)
                    .Set("My_Title", (byte)e.MyTitle)
                    .Set("StudyPoints", e.SubClasses.StudyPoints)
                    .Set("HairStyle", e.HairStyle)
                    .Set("EnlightsReceived", e.ReceivedEnlightenPoints)
                    .Set("PKPoints", e.PKPoints)
                    .Set("QuizPoints", e.QuizPoints)
                    .Set("ExpBalls", c.ExpBalls)
                    .Set("MoneySave", c.MoneySave)
                    .Set("Hitpoints", e.Hitpoints)
                    .Set("LastDragonBallUse", c.LastDragonBallUse.Ticks)
                    .Set("Strength", e.Strength)
                    .Set("Agility", e.Agility)
                    .Set("Spirit", e.Spirit)
                    .Set("Quest", e.Quest)
                    .Set("PuntosUnion", e.PuntosUnion)
                    .Set("Vitality", e.Vitality)
                    .Set("PreviousX", e.PrevX)
                    .Set("DailySignedDays", DailySign)
                    .Set("DailySignRewards", e.DailySignRewards)
                    .Set("DailySignVIPChances", e.DailySignVIPChances)                    
                    .Set("PreviousY", e.PrevY)
                    .Set("Atributes", e.Atributes)
                    .Set("Mana", e.Mana)
                    .Set("VIPLevel", e.VIPLevel)
                    .Set("VIPDays", e.VIPDays)
                    .Set("VIPLevelDays", e.VIPLevelDays)
                    .Set("MapID", e.MapID)
                    .Set("X", e.X)
                    .Set("Y", e.Y)
                    .Set("VirtuePoints", c.VirtuePoints)
                    .Set("TitlePoints" , e.TitlePoints)
                    .Set("TQPoint", c.TQPoint)
                    .Set("PreviousMapID", e.PreviousMapID)
                    .Set("EnlightenPoints", e.EnlightenPoints)
                    .Set("LastResetTime", c.LastResetTime.Ticks)
                    .Set("DoubleExpTime", e.DoubleExperienceTime)
                    .Set("TimeDropSlayer", e.TimeDropSlayer)
                    .Set("ProtectionTime", e.ExpProtectionTime)
                    .Set("HeavenBlessingTime", e.HeavenBlessing)
                    .Set("CursedTime", e.CursedTime)
                    .Set("RevoraPoint", e.RevoraPoint)
                    //.Set("DoubleExpToday", c.DoubleExpToday)
                    .Set("OnlineTrainning", e.OnlineTraining)
                    .Set("HuntingExp", e.BlessedHunting)
                    .Set("InLottery", c.InLottery)
                    .Set("LotteryEntries", c.LotteryEntries)
                    .Set("LastLotteryEntry", c.LastLotteryEntry.Ticks)
                    .Set("HeadgearClaim", c.HeadgearClaim)
                    .Set("NecklaceClaim", c.NecklaceClaim)
                    .Set("ArmorClaim", c.ArmorClaim)
                    .Set("WeaponClaim", c.WeaponClaim)
                    .Set("RingClaim", c.RingClaim)
                    .Set("Flower", c.Entity.AddFlower)
                    .Set("BootsClaim", c.BootsClaim)
                    .Set("TowerClaim", c.TowerClaim)
                    .Set("FanClaim", c.FanClaim)
                    .Set("ChatBanTime", c.ChatBanTime.Ticks)
                    .Set("ChatBanLasts", c.ChatBanLasts)
                    .Set("ChatBanned", c.ChatBanned)
                    .Set("BlessTime", c.BlessTime)
                    .Set("FirstRebornClass", e.FirstRebornClass)
                    .Set("SecondRebornClass", e.SecondRebornClass)
                    .Set("FirstRebornLevel", e.FirstRebornLevel)
                    .Set("SecondRebornLevel", e.SecondRebornLevel)
                    .Set("EnlightmentWait", e.EnlightmentTime)
                    .Set("CountryID", (ushort)e.CountryID)
                    .Set("ClanId", (uint)e.ClanId)
                    .Set("ClanRank", (uint)e.ClanRank)
                    .Set("updatelist", (int)e.UpdateList)
                    .Set("ClaimedTeamPK", e.ClaimedTeamPK)
                    .Set("ClaimedSTeamPK", e.ClaimedSTeamPK)
                    .Set("ClaimedElitePk", e.ClaimedElitePk)
                    .Set("ForgetPassword", c.ForgetPassword)
                    .Set("VoteSystem", e.VoteSystem)
                    .Set("SwordSoul", e.SwordSoul)
                    .Set("BansheeSpirit", e.BansheeSpirit)
                    .Set("ClaimedExp", e.ClaimedExp)
                    .Set("ExtraInventory", c.Entity.ExtraInventory)
                    .Set("FirstCredit", e.ClaimedFirstCredit);

                if (e.MapID == 601)
                    cmd.Set("OfflineTGEnterTime", c.OfflineTGEnterTime.Ticks);
                else
                    cmd.Set("OfflineTGEnterTime", "0");

                // Guild membership is changed by the guild lifecycle operations.
                // A delayed character save must not create, restore, or clear GuildID
                // from a missing or stale in-memory membership projection.
                if (c.AsMember != null)
                {
                    cmd.Set("GuildRank", (ushort)c.AsMember.Rank)
                    .Set("GuildLilies", c.AsMember.Lilies)
                    .Set("GuildRouses", c.AsMember.Rouses)
                    .Set("GuildOrchids", c.AsMember.Orchids)
                    .Set("GuildTulips", c.AsMember.Tulips)
                    .Set("Exploits", c.AsMember.Exploits)
                    .Set("GuildPkDonation", c.AsMember.PkDonation)
                    .Set("CTFCpsReward", c.AsMember.CTFCpsReward)
                    .Set("CTFSilverReward", c.AsMember.CTFSilverReward)
                    .Set("GuildSilverDonation", c.AsMember.SilverDonation)
                    .Set("GuildConquerPointDonation", c.AsMember.ConquerPointDonation);
                    //.Set("GuildLastlod", (ulong)DateTime.Now.Ticks);
                }
                else
                {
                    // A missing projection is not a durable guild departure.
                    // Leave/disband owns clearing the saved rank with membership.
                    cmd.Set("GuildLilies", 0)
                    .Set("GuildRouses", 0)
                    .Set("GuildOrchids", 0)
                    .Set("GuildTulips", 0)
                    .Set("Exploits", 0)
                    .Set("GuildPkDonation", 0)
                    .Set("CTFCpsReward", 0)
                    .Set("CTFSilverReward", 0)
                    .Set("GuildSilverDonation", 0)
                    .Set("GuildConquerPointDonation", 0);
                }
                cmd.Where("UID", e.UID);
                try
                {
                    var observedActivity = CharacterActivity.PrepareSave(c, cmd);
                    var profile = Game.Features.CharacterProgression.PrepareSave(c, cmd);
                    var health = Game.Features.HealthBalance.PrepareSave(c, cmd);
                    Game.Features.SilverBalance.PrepareSave(c, cmd);
                    Game.Features.PurseBalance.PrepareSave(c, cmd);
                    Game.Features.PkBalance.PrepareSave(c, cmd);
                    Game.Features.BlessingBalance.PrepareSave(c, cmd);
                    Game.Features.DoubleExperienceBalance.PrepareSave(c, cmd);
                    var enlightenment = Game.Features.EnlightenmentBalance.PrepareSave(c, cmd);
                    Game.Features.TimeDropBalance.PrepareSave(c, cmd);
                    Game.Features.ExpProtectionBalance.PrepareSave(c, cmd);
                    Game.Features.AppearanceState.PrepareSave(c, cmd);
                    Game.Features.VipState.PrepareSave(c, cmd);
                    Game.Features.VirtueBalance.PrepareSave(c, cmd);
                    int savedRows = cmd.Execute();
                    if (savedRows != 1) throw new InvalidOperationException("Character save did not match its durable balances and progression.");
                    CharacterActivity.Committed(c, observedActivity);
                    Game.Features.HealthBalance.Committed(c, health);
                    if (profile != null) Game.Features.CharacterProgression.Committed(c, profile);
                    Game.Features.SilverBalance.Committed(c, e.Money);
                    Game.Features.PurseBalance.Committed(c, e.ConquerPoints, e.RevoraPoint);
                    Game.Features.EnlightenmentBalance.Committed(c, enlightenment);
                    Game.Features.PkBalance.Committed(c);
                    Game.Features.BlessingBalance.Committed(c, e.HeavenBlessing);
                    Game.Features.DoubleExperienceBalance.Committed(c, e.DoubleExperienceTime);
                    Game.Features.TimeDropBalance.Committed(c, e.TimeDropSlayer);
                    Game.Features.ExpProtectionBalance.Committed(c, e.ExpProtectionTime);
                    Game.Features.AppearanceState.Committed(c);
                    Game.Features.VipState.Committed(c);
                    Game.Features.VirtueBalance.Committed(c);
                }
                catch (Exception ex)
                {
                    Game.Features.QuestInventory.Fail(c, "character-save", ex);
                    return false;
                }
                 
            }
            PrestigeRanking.RefreshSaved(c);
            return true;
        }
        public static bool SaveEntity(Client.GameState c)
        {
            using (var conn = DataHolder.MySqlConnection)
            {
                conn.Open();
                return SaveEntity(c, conn);
            }
        }
        public static bool IsGirl(uint mesh) { return Game.Features.Flowers.IsGirl(mesh); }
        public static void SetFlowers(GameState client) { Game.Features.Flowers.Attach(client); }
        static bool InvalidCharacters(string Name)
        {
            foreach (char c in Name)
            {
                if (Kernel.InvalidCharacters.Contains(c) || (byte)c < 48)
                {
                    return true;
                }
            }
            return false;
        }
        public static void UpdateNames()
        {
            Dictionary<String, NameChangeC> UPDATE = new Dictionary<string, NameChangeC>();
            MySqlCommand cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("nobility");
            Database.MySqlReader r = new Database.MySqlReader(cmd);
            // String newname = "", name = "";
            int UID;
            ulong Donation;
            int rey;
            while (r.Read())
            {
                //newname = r.ReadString("namechange");
                //name = r.ReadString("name");
                UID = (int)r.ReadInt64("EntityUID");
                Donation = (ulong)r.ReadInt64("Donation");
                rey =(int)r.ReadInt64("Rey");
                if (Donation != 0)
                {
                    MySqlCommand cmdupdate = null;
                    cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                    cmdupdate.Update("entities").Set("Donation", Donation).Where("UID", UID).Execute();
                    Console.WriteLine("Donation Set.");
                }
            }
            r.Close();
            r.Dispose();
        }

        public static bool CreateEntity(Network.GamePackets.EnitityCreate eC, Client.GameState client, ref string message)
        {
            if (eC.Name.Length > 16)
                eC.Name = eC.Name.Substring(0, 16);
            if (eC.Name == "")
                return false;
            if (eC.Name == "ChestDemon")
            {
                message = "Invalid characters inside the name.";
                return false;
            }
            if (InvalidCharacters(eC.Name))
            {
                message = "Invalid characters inside the name.";
                return false;
            }
            using (var rdr = new MySqlReader(new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("name", eC.Name)))
            {
                if (rdr.Read())
                {
                    message = "The chosen name is already in use.";
                    return false;
                }
            }
            client.Entity = new Game.Entity(Game.EntityFlag.Player, false);
            client.Entity.Name = eC.Name;
            switch (eC.Class)
            {
                case 0:
                case 1: eC.Class = 100; break;
                case 2:
                case 3: eC.Class = 10; break;
                case 4:
                case 5: eC.Class = 40; break;
                case 6:
                case 7: eC.Class = 20; break;
                case 8:
                case 9: eC.Class = 50; break;
                case 10:
                case 11: eC.Class = 60; break;
                case 12:
                case 13: eC.Class = 70; break;
                case 14:
                case 15: eC.Class = 80; break;
                case 16:
                case 17:
                    {
                        eC.Class = 160; // Range
                        client.Entity.Windwalker = (byte)7;
                        break;
                    }
                case 18:
                case 19:
                    {
                        eC.Class = 160; // Melee
                        client.Entity.Windwalker = (byte)15;
                        break;
                    }
                default: { Console.WriteLine("Error Class = " + eC.Class); } break;
            }
            DataHolder.GetStats(eC.Class, 1, client);
            client.CalculateStatBonus();
            client.CalculateHPBonus();
            client.Entity.Hitpoints = client.Entity.MaxHitpoints;
            client.Entity.Mana = (ushort)(client.Entity.Spirit * 5);
            client.Entity.Class = eC.Class;
            client.Entity.Level = 1;
            //client.Player.Body = eC.Body;
            //if (eC.Body == 1003 || eC.Body == 1004)
            //    client.Player.Face = (ushort)Kernel.Random.Next(1, 50);
            //else
            //    client.Player.Face = (ushort)Kernel.Random.Next(201, 250);
            //byte Color = (byte)Kernel.Random.Next(4, 8);
            //client.Player.HairStyle = (ushort)(Color * 100 + 10 + (byte)Kernel.Random.Next(4, 9));

            client.Entity.Body = eC.Body;
            #region Body Face Class all 
            if (eC.Body == 1006)
            {
                if (eC.Class == 160)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(174, 178);
                }
                else if (eC.Class == 50)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(103, 107);
                }
                else if (eC.Class == 60)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(109, 113);
                }
                else if (eC.Class == 70)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(154, 158);
                }
                else if (eC.Class == 80)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(164, 168);
                }
                else if (eC.Class == 100)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(1, 102);
                }
                else if (eC.Class == 40)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(1, 102);
                }
                else if (eC.Class == 20)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(1, 102);
                }
                else if (eC.Class == 10)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(1, 102);
                }
            }
            else if (eC.Body == 2005)
            {
                if (eC.Class == 160)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(365, 369);
                }
                else if (eC.Class == 50)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(291, 296);
                }
                else if (eC.Class == 60)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(300, 304);
                }
                else if (eC.Class == 70)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(345, 349);
                }
                else if (eC.Class == 80)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(355, 359);
                }
                else if (eC.Class == 100)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(201, 290);
                }
                else if (eC.Class == 40)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(201, 290);
                }
                else if (eC.Class == 20)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(201, 290);
                }
                else if (eC.Class == 10)
                {
                    client.Entity.Face = (ushort)Kernel.Random.Next(201, 290);
                }
            }
            #endregion
            byte Color = (byte)Kernel.Random.Next(4, 8);
            client.Entity.HairStyle = (ushort)(Color * 100 + 10 + (byte)Kernel.Random.Next(4, 9));
            client.Entity.MapID = CharacterCreation.StartMap;
            client.Entity.X = CharacterCreation.StartX;
            client.Entity.Y = CharacterCreation.StartY;
            client.Entity.Owner = client;
            uint startCps = 10000;
            if (client.Account.State == AccountTable.AccountState.GameMaster
                || client.Account.State == AccountTable.AccountState.Administrador)
                startCps = uint.MaxValue;
            client.Entity.UID = Server.EntityUID.Next;
            client.Entity.VIPLevel = 4;
            client.Entity.JustCreated = true;

            while (true)
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid", client.Entity.UID))
                using (var reader = cmd.CreateReader())
                {
                    if (reader.Read())
                        client.Entity.UID = Server.EntityUID.Next;
                    else
                        break;
                }
            }
            for (int insertTries = 0; insertTries < 8; insertTries++)
            {
                uint createUid = client.Entity.UID;
                CharacterCreation.Outcome outcome = CharacterCreation.Outcome.Failed;
                bool owned = Revora.Game.Features.SessionInstall.TryOwnCreation(createUid, client, delegate
                {
                    outcome = CharacterCreation.Commit(client, eC.Name, createUid, startCps);
                    return outcome == CharacterCreation.Outcome.Saved;
                });
                if (owned)
                {
                    message = "ANSWER_OK";
                    return true;
                }
                if (outcome == CharacterCreation.Outcome.NameTaken)
                {
                    message = "The chosen name is already in use.";
                    return false;
                }
                Console.WriteLine("[game] create-insert-fail uid=" + createUid);
                // Once this attempt was published, failure ends it. Retrying with
                // a new UID would reuse an obsolete generation and hide partial writes.
                if (client.DisconnectStarted || client.ShutdownStarted || client.SessionGeneration != 0)
                    break;
                client.Entity.UID = Server.EntityUID.Next;
            }
            message = "Could not create the character.";
            return false;
        }
       /* public static bool CreateEntity(Network.GamePackets.EnitityCreate eC, Client.GameState client, ref string message)
        {
            if (eC.Name.Length > 16)
                eC.Name = eC.Name.Substring(0, 16);
            if (eC.Name == "")
                return false;

            if (InvalidCharacters(eC.Name))
            {
                message = "Invalid characters in the name.";
                return false;
            }
            using (
                var rdr =
                    new MySqlReader(new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("name", eC.Name))
                )
            {
                if (rdr.Read())
                {
                    message = "This name is already in use.";
                    return false;
                }
            }
            client.Player = new Game.Entity(Game.EntityFlag.Player, false);
            client.Player.Name = eC.Name;


            if (eC.Class == 1 || eC.Class == 0)
            {
                client.Player.Class = 100;
                DataHolder.GetStats(100, 1, client);
            }
            if (eC.Class == 5 || eC.Class == 4)
            {
                client.Player.Class = 40;
                DataHolder.GetStats(40, 1, client);
            }
            if (eC.Class == 12 || eC.Class == 13)
            {
                client.Player.Class = 70;
                DataHolder.GetStats(70, 1, client);
            }
            if (eC.Class == 2 || eC.Class == 3)
            {
                client.Player.Class = 10;
                DataHolder.GetStats(10, 1, client);
            }
            if (eC.Class == 14 || eC.Class == 15)
            {
                client.Player.Class = 80;
                DataHolder.GetStats(80, 1, client);
            }
            if (eC.Class == 10 || eC.Class == 11)
            {
                client.Player.Class = 60;
                DataHolder.GetStats(60, 1, client);
            }
            if (eC.Class == 16 || eC.Class == 17)
            {
                client.Player.Class = 160;
                client.Player.Windwalker = (byte)8;
                DataHolder.GetStats(160, 1, client);
            }
            if (eC.Class == 18 || eC.Class == 19)
            {
                client.Player.Class = 160;
                DataHolder.GetStats(160, 1, client);
            }
            if (eC.Class == 8 || eC.Class == 9)
            {
                client.Player.Class = 50;
                DataHolder.GetStats(50, 1, client);
            }
            if (eC.Class == 6 || eC.Class == 7)
            {
                client.Player.Class = 20;
                DataHolder.GetStats(20, 1, client);
            }
           
            client.CalculateStatBonus();
            client.CalculateHPBonus();
            client.Player.Hitpoints = client.Player.MaxHitpoints;
            client.Player.Mana = (ushort)(client.Player.Spirit * 5);
            client.Player.Body = eC.Body;
            if (eC.Body == 1003 || eC.Body == 1004)
                client.Player.Face = (ushort)Kernel.Random.Next(1, 50);
            else
                client.Player.Face = (ushort)Kernel.Random.Next(201, 250);
            byte Color = (byte)Kernel.Random.Next(4, 8);
            client.Player.HairStyle = (ushort)(Color * 100 + 10 + (byte)Kernel.Random.Next(4, 9));
            client.Player.UID = Server.EntityUID.Next;
            client.Player.JustCreated = true;

            while (true)
            {
                using (
                    var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("entities")
                        .Where("uid", client.Player.UID))
                using (var reader = cmd.CreateReader())
                {
                    if (reader.Read())
                        client.Player.UID = Server.EntityUID.Next;
                    else
                        break;
                }
            }
            while (true)
            {
                try
                {
                    using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                        cmd.Insert("entities")
                            .Insert("Name", eC.Name)
                            .Insert("Windwalker", client.Player.Windwalker)
                            .Insert("Owner", client.Account.Username)
                            .Insert("Class", client.Player.Class)
                            .Insert("UID", client.Player.UID)
                            .Insert("Hitpoints", client.Player.Hitpoints)
                            .Insert("Mana", client.Player.Mana)
                            .Insert("Body", client.Player.Body)
                            .Insert("Face", client.Player.Face)
                            .Insert("HairStyle", client.Player.HairStyle)
                            .Insert("Strength", client.Player.Strength)
                            .Insert("WarehousePW", "")
                            .Insert("Agility", client.Player.Agility)
                            .Insert("Vitality", client.Player.Vitality)
                            .Insert("Spirit", client.Player.Spirit)
                            .Execute();

                    message = "ANSWER_OK";
                    break;
                }
                catch
                {
                    client.Player.UID = Server.EntityUID.Next;
                }
            }

            using (
                var cmd =
                    new MySqlCommand(MySqlCommandType.UPDATE).Update("configuration")
                        .Set("EntityID", client.Player.UID)
                        .Where("Server", Constants.ServerName))
                cmd.Execute();
            client.Account.EntityID = client.Player.UID;
            client.Account.Save();
            return true;
        }*/
        public static void UpdateFirstCreditPoints(Client.GameState client)
        {
            UpdateData(client, "FirstCreditPoints", client.Entity.FirstCreditPoints);
        }
        public static void UpdateTreasuerPoints(Client.GameState client)
        {
            UpdateData(client, "TreasuerPoints", client.Entity.TreasuerPoints);
        }
        
        public static void UpdateBankCps(Client.GameState client)
        {
            UpdateData(client, "BankCps", client.Entity.BankCps);
        }
        public static void UpdateTareaCopa(Client.GameState client)
        {
            UpdateData(client, "TareaCopa", client.Entity.TareaCopa);
        }
        public static void UpdateOnlinePoints(Client.GameState client)
        {
            UpdateData(client, "OnlinePoints", client.Entity.OnlinePoints);
        }

        public static void UpdateTareaInner(Client.GameState client)
        {
            UpdateData(client, "TareaInner", client.Entity.TareaInner);
        }
    }
}
