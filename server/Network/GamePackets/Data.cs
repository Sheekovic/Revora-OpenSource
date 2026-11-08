using ProtoBuf;
using System;

namespace Revora.Network.GamePackets
{
    public unsafe class Data : Writer, Interfaces.IPacket
    {
        public class CustomCommands
        {
            public const ushort
            ExitQuestion = 1,
            Minimize = 2,
            AddEntity = 134,
            ShowReviveButton = 1053,
            FlowerPointer = 1067,
            Enchant = 1091,
            LoginScreen = 1153,
            SelectRecipiet = 30,
            JoinGuild = 34,
            MakeFriend = 38,
            ChatWhisper = 40,
            CloseClient = 43,
            HotKey = 53,
            Furniture1 = 54,
            TQForum = 79,
            PathFind = 97,
            LockItem = 102,
            Day = 104,
            MapStatus = 105,
            ShowRevive = 1053,
            HideRevive = 1054,
            StatueMaker = 1066,
            Furniture = 1070,
            GambleOpen = 1077,
            GambleClose = 1078,
            Compose = 1086,
            Craft1 = 1088,
            Craft2 = 1089,
            Warehouse = 1090,
            ShoppingMallShow = 1100,
            ShoppingMallHide = 1101,
            NoOfflineTraining = 1117,
            CenterClient = 1155,
            ClaimCP = 1197,
            ClaimAmount = 1198,
            NewPatch = 1085,
            SendTC = 1140,
            MerchantApply = 1201,
            MerchantDone = 1202,
            RedeemEquipment = 1233,
            ClaimPrize = 1234,
            RepairAll = 1239,
            FlowerIcon = 1244,
            SendFlower = 1246,
            ReciveFlower = 1248,
            WarehouseVIP = 1272,
            UseExpBall = 1288,
            HackProtection = 1298,
            HideGUI = 1307,
            Inscribe = 3059,
            BuyPrayStone = 3069,
            itemsgui = 3112,
            HonorStore = 3104,
            Opponent = 3107,
            CountDownQualifier = 3109,
            QualifierStart = 3111,
            ItemsReturnedShow = 3117,
            ItemsReturnedWindow = 3118,
            ItemsReturnedHide = 3119,
            QuestFinished = 3147,
            QuestPoint = 3148,
            QuestPointSparkle = 3164,
            StudyPointsUp = 3192,
            PKTeamName = 3209,
            CTFFlag = 3211,
            Updates = 3218,
            IncreaseLineage = 3227,
            HorseRacingStore = 3245,
            GuildPKTourny = 3249,
            QuitPK = 3251,
            Spectators = 3252,
            CardPlayOpen = 3270,
            CardPlayClost = 3271,
            ArtifactPurification = 3344,
            SafeguardConvoyShow = 3389,
            SafeguardConvoyHide = 3390,
            RefineryStabilization = 3392,
            ArtifactStabilization = 3398,
            SmallChat = 3406,
            NormalChat = 3407,
            Reincarnation = 3439,
            GuiHeavenBlessing = 3064,
            DoneEffect = 3130,
            Market = 3300,
            Advertise = 3274,
            CTFEnd = 3549,
            CTFScores = 3359;
        }
       
        public class WindowCommands
        {
            public const ushort
            Compose = 1,
            Craft = 2,
            Warehouse = 4,
            DetainRedeem = 336,
            DetainClaim = 337,
            VIPWarehouse = 341,
            Breeding = 368,
            PurificationWindow = 455,
            StabilizationWindow = 459,
            TalismanUpgrade = 347,
            GemComposing = 422,
            OpenSockets = 425,
            Blessing = 426,
            TortoiseGemComposing = 438,
            RefineryStabilization = 448,
            HorseRacingStore = 464,
            Reincarnation = 485;
              
        }
      
        public const ushort
        RemoveTrap = 434,
        SwirlingStorm = 179,
        ObserveKnownPerson = 54,
        SetLocation = 74,
        Hotkeys = 75,
        ConfirmFriends = 76,
        ConfirmProficiencies = 77,
        ConfirmSpells = 78,
        ChangeDirection = 79,
        ChangeAction = 81,
        UsePortal = 85,
        Teleport = 86,
        Leveled = 92,
        XPListEnd = 93,
        Revive = 94,
        DeleteCharacter = 95,
        ChangePKMode = 96,
        ConfirmGuild = 97,
        SwingPickaxe = 99,
        UnknownEntity = 102,
        TeamSearchForMember = 106,
        NewCoordonates = 108,
        DropMagic = 109,
        DropSkill = 110,
        OwnBooth = 111,
        GetSurroundings = 114,
        OpenCustom = 116,
        ObserveEquipment = 117,
        EndTransformation = 118,
        EndFly = 120,
        ViewEnemyInfo = 123,
        OpenWindow = 126,
        CompleteLogin = 251,
        RemoveEntity = 135,
        Jump = 137,
        Die = 145,
        EndTeleport = 146,
        ViewFriendInfo = 148,
        ChangeFace = 151,
        ViewPartnerInfo = 152,
        Confiscator = 153,
        FlashStep = 156,
        CountDown = 159,
        Away = 161,
        Bulletin = 166,
        ClaimableItem = 408,
        AppearanceType = 178,
        LevelUpSpell = 252,
        SpawnEffect = 134,
        GuardJump = 130,
        LevelUpProficiency = 253,
        ObserveEquipment2 = 310,
        BeginSteedRace = 401,
        FinishSteedRace = 402,
        JiangHu = 126,
        TeamMemberPos = 101,
        Shop = 160,
        PathFinding = 162,
        Disconnect = 132,
        ItemDetained = 154,
        DetainItem = 155,
        HideGui = 158,
        SendTC = 177,
        FirstCredit = 255,
        GetMoney = 121,
        DragonBallDropped = 165,
        GoldBricks = 436,
        DetainWindowRequest = 153,
        ExtraInventory = 256;

        [ProtoContract]
        public class CMsgAction
        {
            [ProtoMember(1, IsRequired = true)]
            public uint UID;
            [ProtoMember(2, IsRequired = false)]
            public uint unkown;
            [ProtoMember(3, IsRequired = false)]
            public long dwParam;
            [ProtoMember(4, IsRequired = false)]
            public uint unkown15;
            [ProtoMember(5, IsRequired = true)]
            public long PokerPot;//pokerpot
            [ProtoMember(6, IsRequired = true)]
            public uint wParam1;
            [ProtoMember(7, IsRequired = true)]
            public uint Coords_X;
            [ProtoMember(8, IsRequired = true)]
            public uint Coords_Y;
            [ProtoMember(9, IsRequired = true)]
            public uint TimeStamp;
            [ProtoMember(10, IsRequired = true)]
            public ulong unkown5;
            [ProtoMember(11, IsRequired = true)]
            public ulong unkown6;
            [ProtoMember(12, IsRequired = true)]
            public uint ID;
            [ProtoMember(13, IsRequired = true)]
            public byte Faceing;
            [ProtoMember(14, IsRequired = true)]
            public uint Coords_OldX;
            [ProtoMember(15, IsRequired = true)]
            public uint Coords_OldY;
            [ProtoMember(16, IsRequired = false)]
            public ulong unkown7;
            [ProtoMember(17, IsRequired = false)]
            public ulong MapID;
            [ProtoMember(18, IsRequired = false)]
            public ulong unkown9;
            [ProtoMember(19, IsRequired = false)]
            public ulong unkown10;
            [ProtoMember(20, IsRequired = false)]
            public ulong unkown11;
            [ProtoMember(21, IsRequired = false)]
            public ulong unkown12;
            [ProtoMember(22, IsRequired = false)]
            public ulong Time;
            [ProtoMember(23, IsRequired = false)]
            public byte OnRunSteed;
            [ProtoMember(24, IsRequired = false)]
            public string Text;
        }
        public CMsgAction Buffer;
        public byte[] Buffer2;
        ushort PacketID = 10010;
        public Data(bool Create)
        {
            if (Create)
            {
                Buffer = new CMsgAction();
            }
        }
        public static Data Custom(uint customType, uint uid, ushort wParam1 = 0, ushort wParam2 = 0)
        {
            Data data = new Data(true);
            data.ID = OpenCustom;
            data.UID = uid;
            data.TimeStamp = Time32.Now;
            data.dwParam = customType;
            data.wParam1 = wParam1;
            data.wParam2 = wParam2;
            return data;
        }
        public ushort wParam1//X
        {
            get
            {
                return (ushort)Buffer.Coords_OldX;
            }
            set
            {
                Buffer.Coords_OldX = value;
            }
        }
        public ushort wParam2//Y
        {
            get
            {
                return (ushort)Buffer.Coords_OldY;
            }
            set
            {
                Buffer.Coords_OldY = value;
            }
        }
        public uint UID
        {
            get { return Buffer.UID; }
            set { Buffer.UID = value; }
        }
        public uint dwParam//CustomType
        {
            get
            {
                return (uint)Buffer.dwParam;
            }
            set
            {
                Buffer.dwParam = value;
            }
        }
        public long dwParamPot
        {
            get
            {
                return Buffer.PokerPot;
            }
            set
            {
                Buffer.PokerPot = value;
            }
        }
        public ushort X
        {
            get
            {
                return (ushort)Buffer.Coords_OldX;
            }
            set
            {
                Buffer.Coords_OldX = value;
            }
        }
        public ushort Y
        {
            get
            {
                return (ushort)Buffer.Coords_OldY;
            }
            set
            {
                Buffer.Coords_OldY = value;
            }
        }
        public ushort Xt1//For TwilightDance
        {
            get
            {
                return (ushort)Buffer.Coords_X;
            }
            set
            {
                Buffer.Coords_X = value;
            }
        }
        public ushort Yt1//For TwilightDance
        {
            get
            {
                return (ushort)Buffer.Coords_Y;
            }
            set
            {
                Buffer.Coords_Y = value;
            }
        }
        public Game.Enums.ConquerAngle Facing
        {
            get
            {
                return (Game.Enums.ConquerAngle)Buffer.Faceing;
            }
            set
            {
                Buffer.Faceing = (byte)value;
            }
        }
        public Time32 TimeStamp
        {
            get
            {
                return new Time32(Buffer.TimeStamp);
            }
            set
            {
                Buffer.TimeStamp = (uint)value.GetHashCode();
            }
        }
        public uint TimeStamp2//For Npc Shop
        {
            get
            {
                return (uint)Buffer.unkown5;
            }
            set
            {
                Buffer.unkown5 = value;
            }
        }
        public uint ID
        {
            get
            {
                return Buffer.ID;
            }
            set
            {
                Buffer.ID = value;
            }
        }
        public uint Data28_Uint//For Poker
        {
            get
            {
                return Buffer.wParam1;
            }
            set
            {
                Buffer.wParam1 = value;
            }
        }
        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer.ConvertToProto<CMsgAction>();
            Buffer2 = buffer;
        }
        public byte[] ToArray()
        {
            return Buffer.ToConquerPacket(PacketID);
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer.ToConquerPacket(PacketID));
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (client == null || client.Action != 2 || packet == null || packet.Length < 12 || packet.Length > ushort.MaxValue + 8
                || System.BitConverter.ToUInt16(packet, 0) != packet.Length - 8 || System.BitConverter.ToUInt16(packet, 2) != 10010
                || System.BitConverter.ToUInt64(packet, packet.Length - 8) != PacketHandler.ClientSeal) return;
            Data gData = new Data(false);
            try { gData.Deserialize(packet); }
            catch (ProtoException) { return; }
            catch (System.IO.IOException) { return; }
            catch (ArgumentException) { return; }
            catch (OverflowException) { return; }
            if (gData.Buffer == null) return;
            switch (gData.ID)
            {
                case Data.ExtraInventory:
                    {
                        if (client.Entity.ExtraInventory < 200)
                        {
                            if (client.Entity.ConquerPoints >= 90)
                            {
                                client.Entity.ExtraInventory += 1;
                                client.Entity.TrySpendConquerPoints((uint)(90));
                            }
                            else
                            {
                                client.Entity.SendSysMesage("Are you sure you want to open it?");
                            }
                        }
                        else
                        {
                            client.Entity.SendSysMesage("You have no more slots available.");
                        }
                        break;
                    }
                case Data.DetainWindowRequest:
                    {
                        if (!client.JustOpenedDetain)
                        {
                            if (client.DeatinedItem.Count != 0)
                            {
                                client.Send(new Data(true)
                                {
                                    ID = Data.OpenWindow,
                                    dwParam = Data.WindowCommands.DetainRedeem,
                                    UID = client.Entity.UID
                                });
                            }
                            if (client.ClaimableItem.Count != 0)
                            {
                                client.Send(new Data(true)
                                {
                                    ID = Data.OpenWindow,
                                    dwParam = Data.WindowCommands.DetainClaim,
                                    UID = client.Entity.UID
                                });
                            }
                        }
                        client.JustOpenedDetain = !client.JustOpenedDetain;
                        break;
                    }
                case Data.SendTC:
                    {
                        client.Entity.Teleport(1002, 301, 288);
                        break;
                    }
                case Data.Bulletin:
                    {
                        if (DateTime.Now.DayOfWeek == DayOfWeek.Wednesday && DateTime.Now.Hour ==
                            19 && DateTime.Now.Minute == 45 && DateTime.Now.Minute <= 50)//skill team pk                                  
                        {
                            client.Entity.Teleport(1002, 313, 143);//Teleport
                        }
                        else if (DateTime.Now.DayOfWeek == DayOfWeek.Friday && DateTime.Now.Hour == 19
                        && DateTime.Now.Minute == 55 && DateTime.Now.Minute <= 59)//elite pk                                     
                        {
                            client.Entity.Teleport(1002, 302, 144);//Teleport
                        }
                        else if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday && DateTime.Now.Hour ==
                            18 && DateTime.Now.Minute == 45 && DateTime.Now.Minute <= 50)//team pk                                            
                        {
                            client.Entity.Teleport(1002, 292, 146);//Teleport
                        }
                        else if (DateTime.Now.Day <= 7 && DateTime.Now.DayOfWeek == DayOfWeek.Sunday &&
                            DateTime.Now.Hour == 22 && DateTime.Now.Minute >= 01 && DateTime.Now.Minute <= 14)//Monthly PK                                                                                 
                        {
                            client.Entity.Teleport(1002, 324, 194);//Teleport
                        }
                        else if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday &&
                            DateTime.Now.Hour == 20 && DateTime.Now.Minute >= 05 && DateTime.Now.Minute <= 15)// Weekly PK                                         
                        {
                            client.Entity.Teleport(1002, 324, 194);//Teleport
                        }
                        if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday && DateTime.Now.Hour ==
                             21 && DateTime.Now.Minute == 36 && DateTime.Now.Minute <= 59)//CaptureTheFlag                            
                        {
                            client.Entity.Teleport(1002, 225, 237);//Teleport
                        }
                        //else if (DateTime.Now.DayOfWeek == DayOfWeek.Wednesday || DateTime.Now.DayOfWeek ==
                        //    DayOfWeek.Friday) if ((DateTime.Now.Hour == 12 || DateTime.Now.Hour == 19) && DateTime.Now.Minute ==
                        //         01 && DateTime.Now.Minute <= 05)//Dis City
                        //    {
                        //        client.Player.Teleport(1002, 297, 149);//Teleport
                        //    }
                        break;
                    }
                case Data.ClaimableItem:
                    {
                        if (!client.JustOpenedDetain)
                            if (client.ClaimableItem.Count != 0)
                                client.Send(new Data(true)
                                {
                                    ID = Data.OpenWindow,
                                    dwParam = Data.WindowCommands.DetainClaim,
                                    UID = client.Entity.UID
                                });
                        client.JustOpenedDetain = !client.JustOpenedDetain;
                        break;
                    }
                  
                case Data.FinishSteedRace:
                    {
                        if (client.Entity.MapID == Game.SteedRace.MAPID)
                            Server.Thread.SteedRace.FinishRace(client);
                        break;
                    }
                case Data.Disconnect:
                    {
                        client.Disconnect();
                        break;
                    }
                case Data.AppearanceType:
                    {
                        GeneralData.ChangeAppearance(gData, client);
                        break;
                    }
                case Data.LevelUpSpell:
                    {
                        GeneralData.LevelUpSpell(gData, client);
                        break;
                    }
                case Data.LevelUpProficiency:
                    {
                        GeneralData.LevelUpProficiency(gData, client);
                        break;
                    }
                case Data.SwingPickaxe:
                    {
                        Game.Features.MiningRewards.Begin(client, gData);
                        break;
                    }
                case Data.Revive:
                    {
                        GeneralData.Revive(gData, client);
                        break;
                    }
                case Data.UsePortal:
                    {
                        GeneralData.UsePortal(gData, client);
                        break;
                    }
                case Data.ChangePKMode:
                    {
                        GeneralData.ChangePKMode(gData, client);
                        break;
                    }
                case Data.ChangeAction:
                    {
                        GeneralData.ChangeAction(gData, client);
                        break;
                    }
                case Data.ChangeDirection:
                    {
                        GeneralData.ChangeDirection(gData, client);
                        break;
                    }
                case Data.Hotkeys:
                    {
                        client.Send(packet);
                        break;
                    }
                case Data.ConfirmSpells:
                    {
                        if (client.Spells != null)
                            foreach (Interfaces.ISkill spell in client.Spells.Values)
                                if (spell.ID != 3060)
                                    spell.Send(client);
                        client.Send(packet);
                        break;
                    }
                case Data.ConfirmProficiencies:
                    {
                        if (client.Proficiencies != null)
                            foreach (Interfaces.IProf proficiency in client.Proficiencies.Values)
                                proficiency.Send(client);
                        client.Send(packet);
                        break;
                    }
                case Data.ConfirmGuild:
                    {
                        client.Send(packet);
                        break;
                    }
                case Data.ConfirmFriends:
                    {
                        #region Friends/Enemy/TradePartners/Apprentices
                        Message msg2 = new Message("Your friend, " + client.Entity.Name + ", has logged in.", System.Drawing.Color.Red, Message.TopLeft);

                        foreach (Game.ConquerStructures.Society.Friend friend in client.Friends.Values)
                        {
                            if (friend.IsOnline)
                            {
                                var pckt = new KnownPersons(true)
                                {
                                    UID = client.Entity.UID,
                                    Type = KnownPersons.RemovePerson,
                                    Name = client.Entity.Name,
                                    NobilityRank = client.Entity.NobilityRank,
                                    IsBoy = Game.Features.Flowers.IsBoy(client.Entity.Body),
                                    Online = true
                                };
                                friend.Client.Send(pckt);
                                pckt.Type = KnownPersons.GetInfo;
                                friend.Client.Send(pckt);
                                friend.Client.Send(msg2);
                            }
                            client.Send(new KnownPersons(true)
                            {
                                UID = friend.ID,
                                Type = KnownPersons.GetInfo,
                                Name = friend.Name,
                                NobilityRank = friend.NobilityRank,
                                IsBoy = friend.IsBoy,
                                Online = friend.IsOnline
                            });
                            if (friend.Message != "")
                            {
                                client.Send(new Message(friend.Message, client.Entity.Name, friend.Name, System.Drawing.Color.Red, Message.Whisper));
                                Database.KnownPersons.UpdateMessageOnFriend(friend.ID, client.Entity.UID, "");
                            }
                        }
                        foreach (Game.ConquerStructures.Society.Enemy enemy in client.Enemy.Values)
                        {
                            if (enemy.IsOnline)
                            {
                                var pckt = new KnownPersons(true)
                                {
                                    UID = client.Entity.UID,
                                    Type = KnownPersons.EnemyOnline,
                                    Name = client.Entity.Name,
                                    NobilityRank = client.Entity.NobilityRank,
                                    IsBoy = Game.Features.Flowers.IsBoy(client.Entity.Body),
                                    Online = true
                                };
                                enemy.Client.Send(pckt);
                                pckt.Type = KnownPersons.AddEnemy;
                                enemy.Client.Send(pckt);
                            }
                            client.Send(new KnownPersons(true)
                            {
                                UID = enemy.ID,
                                Type = KnownPersons.AddEnemy,
                                Name = enemy.Name,
                                NobilityRank = enemy.NobilityRank,
                                IsBoy = enemy.IsBoy,
                                Online = enemy.IsOnline
                            });
                        }
                        Message msg3 = new Message("Your partner, " + client.Entity.Name + ", has logged in.", System.Drawing.Color.Red, Message.TopLeft);
                        foreach (Game.ConquerStructures.Society.TradePartner partner in client.Partners.Values)
                        {
                            if (partner.IsOnline)
                            {
                                var packet3 = new TradePartner(true)
                                {
                                    UID = client.Entity.UID,
                                    Type = TradePartner.BreakPartnership,
                                    Name = client.Entity.Name,
                                    MinutesLeft = Game.Features.TradePartnerships.RemainingMinutes(partner.ProbationStartedOn),
                                    Online = true
                                };
                                partner.Client.Send(packet3);
                                packet3.Type = TradePartner.AddPartner;
                                partner.Client.Send(packet3);
                                partner.Client.Send(msg3);
                            }
                            var packet4 = new TradePartner(true)
                            {
                                UID = partner.ID,
                                Type = TradePartner.AddPartner,
                                Name = partner.Name,
                                MinutesLeft = Game.Features.TradePartnerships.RemainingMinutes(partner.ProbationStartedOn),
                                Online = partner.IsOnline
                            };
                            client.Send(packet4);
                        }
                        foreach (Game.ConquerStructures.Society.Apprentice appr in client.Apprentices.Values)
                        {
                            if (appr.IsOnline)
                            {
                                ApprenticeInformation AppInfo = new ApprenticeInformation();
                                AppInfo.Apprentice_ID = appr.ID;
                                AppInfo.Apprentice_Level = appr.Client.Entity.Level;
                                AppInfo.Apprentice_Class = appr.Client.Entity.Class;
                                AppInfo.Apprentice_PkPoints = appr.Client.Entity.PKPoints;
                                AppInfo.Apprentice_Experience = appr.Actual_Experience;
                                AppInfo.Apprentice_Composing = appr.Actual_Plus;
                                AppInfo.Apprentice_Blessing = appr.Actual_HeavenBlessing;
                                AppInfo.Apprentice_Name = appr.Name;
                                AppInfo.Apprentice_Online = true;
                                AppInfo.Apprentice_Spouse_Name = appr.Client.Entity.Spouse;
                                AppInfo.Enrole_date = appr.EnroleDate;
                                AppInfo.Mentor_ID = client.Entity.UID;
                                AppInfo.Mentor_Mesh = client.Entity.Mesh;
                                AppInfo.Mentor_Name = client.Entity.Name;
                                AppInfo.Type = 2;
                                client.Send(AppInfo);

                                MentorInformation Information = new MentorInformation(true);
                                Information.Mentor_Type = 1;
                                Information.Mentor_ID = client.Entity.UID;
                                Information.Apprentice_ID = appr.ID;
                                Information.Enrole_Date = appr.EnroleDate;
                                Information.Mentor_Level = client.Entity.Level;
                                Information.Mentor_Class = client.Entity.Class;
                                Information.Mentor_PkPoints = client.Entity.PKPoints;
                                Information.Mentor_Mesh = client.Entity.Mesh;
                                Information.Mentor_Online = true;
                                Information.Shared_Battle_Power = appr.Client.Entity.BattlePowerFrom(client.Entity);
                                Information.String_Count = 3;
                                Information.Mentor_Name = client.Entity.Name;
                                Information.Apprentice_Name = appr.Name;
                                Information.Mentor_Spouse_Name = client.Entity.Spouse;
                                appr.Client.ReviewMentor();
                                appr.Client.Send(Information);
                            }
                            else
                            {
                                ApprenticeInformation AppInfo = new ApprenticeInformation();
                                AppInfo.Apprentice_ID = appr.ID;
                                AppInfo.Apprentice_Name = appr.Name;
                                AppInfo.Apprentice_Online = false;
                                AppInfo.Enrole_date = appr.EnroleDate;
                                AppInfo.Mentor_ID = client.Entity.UID;
                                AppInfo.Mentor_Mesh = client.Entity.Mesh;
                                AppInfo.Mentor_Name = client.Entity.Name;
                                AppInfo.Type = 2;
                                client.Send(AppInfo);
                            }
                        }
                        #endregion
                        client.Send(packet);
                        break;
                    }
                case Data.EndTeleport:
                    {
                        break;
                    }
                case Data.GetSurroundings:
                    {
                        if (client.Booth != null)
                        {
                            client.Entity.TransformationID = 0;
                            client.Booth.Remove();
                            client.Booth = null;
                        }
                        GeneralData.GetSurroundings(client);
                        if (client.ChampionGroup != null)
                        {
                            byte[] p5442 = new byte[] 
                            { 
                                20, 0, 86, 4, 40, 128, 14, 0,
                                160, 187, 13, 0, 103, 0, 21, 
                                64, 12, 49, 0, 0, 84, 81, 83, 
                                101, 114, 118, 101, 114 
                            };
                            client.Send(p5442);
                        }
                        else
                            client.Send(new MapStatus()
                            {
                                BaseID = client.Map.BaseID,
                                ID = client.Map.ID,
                                Status = Database.MapsTable.MapInformations[client.Map.ID].Status,
                                Weather = Database.MapsTable.MapInformations[client.Map.ID].Weather
                            });
                        Game.WeatherStruct.CurrentWeatherBase.Send(client);
                        client.Send(gData);
                        break;
                    }


                case Data.FirstCredit://Jheisson
                    {
                        if (client.Entity.FirstCreditPoints >= 1)
                        {
                            client.Entity.FirstCreditPoints -= 1;
                            client.Inventory.AddBoundItem(3004085, 0, 1);//CPs Box
                            client.Inventory.AddBoundItem(720121, 0, 1);//Holy Pack
                            client.Inventory.AddBoundItem(720654, 0, 1);//Revora Points Pack
                            client.Inventory.AddBoundItem(3001266, 0, 1);//Fortune Bag
                            client.Inventory.AddBoundItem(3004247, 0, 1);//Holy Soul Pack E7
                            client.Inventory.AddBoundItem(3004248, 0, 1);//Equipment Soul Pack E7
                            client.Inventory.AddBoundItem(3004249, 0, 1);//Sacred Refined Pack
                            client.Inventory.AddBoundItem(3001267, 0, 1);//White Horse +12
                            client.Inventory.AddBoundItem(3300809, 0, 1);//Elite Stone *10
                            FlagsUpdate upgrade = new FlagsUpdate(true);
                            upgrade.UID = client.Entity.UID;
                            upgrade.Append((byte)FlagsUpdate.DataType.FirstCreditGift, 21, 0, 0, 0);
                            client.Send(upgrade.ToArray());
                            client.Entity.ClaimedFirstCredit = true;
                        }
                        else
                        {
                            client.Send(new Message("Sorry, but the GM has not granted you the welcome gift.", Message.System));
                        }
                        break;
                    }
                case Data.SetLocation:
                    {
                        GeneralData.SetLocation(gData, client);
                        break;
                    }
                case Data.Jump:
                    Revora.Game.Features.ExpeditionEvent.NotifyAction(client, "you moved");
                    {
                        client.Entity.SetAway(false);
                        client.LastMove = Time32.Now;
                        GeneralData.PlayerJump(gData, client);
                        #region Refresh MagicDefender
                        Revora.Game.Features.MagicDefenderProtection.Refresh(client,Time32.Now);
                        #endregion
                        client.Entity.SpellStamp = Time32.Now.AddSeconds(-1);
                        break;
                    }
                case Data.UnknownEntity:
                    {
                        #region UnknownEntity
                        Client.GameState pClient = null;
                        if (Kernel.GamePool.TryGetValue(gData.dwParam, out pClient))
                        {
                            if (Kernel.GetDistance(pClient.Entity.X, pClient.Entity.Y, client.Entity.X, client.Entity.Y) <= Constants.pScreenDistance && client.Map.ID == pClient.Map.ID)
                            {
                                if (pClient.Guild != null)
                                    pClient.Guild.SendName(client);
                                if (client.Guild != null)
                                    client.Guild.SendName(pClient);
                                if (pClient.Entity.UID != client.Entity.UID)
                                {
                                    if (pClient.Map.ID == client.Map.ID)
                                    {
                                        if (pClient.Map.BaseID == 700 && pClient.Map.ID != 700)
                                        {
                                            if (client.InQualifier())
                                            {
                                                if (pClient.InQualifier())
                                                {
                                                    client.Entity.SendSpawn(pClient);
                                                    pClient.Entity.SendSpawn(client);
                                                    if (pClient.Guild != null)
                                                        client.Entity.SendSpawn(pClient, false);
                                                    if (client.Guild != null)
                                                        pClient.Entity.SendSpawn(client, false);
                                                }
                                                else
                                                {
                                                    client.Entity.SendSpawn(pClient);
                                                    if (pClient.Guild != null)
                                                        client.Entity.SendSpawn(pClient, false);
                                                    client.Screen.Add(pClient.Entity);
                                                }
                                            }
                                            else
                                            {
                                                if (pClient.InQualifier())
                                                {
                                                    pClient.Entity.SendSpawn(client);
                                                    if (client.Guild != null)
                                                        pClient.Entity.SendSpawn(client, false);
                                                    pClient.Screen.Add(client.Entity);
                                                }
                                                else
                                                {
                                                    client.Entity.SendSpawn(pClient, false);
                                                    pClient.Entity.SendSpawn(client, false);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            client.Entity.SendSpawn(pClient, false);
                                            pClient.Entity.SendSpawn(client, false);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Game.Entity monster = null;
                            if (client.Map.Entities.TryGetValue(gData.dwParam, out monster))
                            {
                                if (Kernel.GetDistance(monster.X, monster.Y, client.Entity.X, client.Entity.Y) <= Constants.pScreenDistance)
                                {
                                    monster.SendSpawn(client, false);
                                }
                            }
                            if (client.Map.Companions.TryGetValue(gData.dwParam, out monster))
                            {
                                if (Kernel.GetDistance(monster.X, monster.Y, client.Entity.X, client.Entity.Y) <= Constants.pScreenDistance)
                                {
                                    monster.SendSpawn(client, false);
                                }
                            }
                        }
                        #endregion
                        break;
                    }
                case Data.CompleteLogin:
                    {
                        GeneralData.LoginMessages(client);
                        if (client.Equipment != null && client.Equipment.Objects != null)
                        {
                            foreach (ConquerItem gear in client.Equipment.Objects)
                            {
                                if (gear == null || !Game.ConquerStructures.Equipment.IsLoginDeferredSlot(gear.Position)) continue;
                                gear.IsWorn = true;
                                gear.Send(client);
                            }
                        }
                        ClientEquip equips = new ClientEquip();
                        equips.DoEquips(client);
                        client.Send(equips);
                        if (client.Equipment != null)
                            client.Equipment.UpdateEntityPacket();
                        client.Send(packet);
                        client.LoadItemStats();
                        new MsgGlobalLottery().Login(client);
                        #region Union
                        if (Kernel.Unions.Count > 0)
                        {
                            Revora.Network.GamePackets.Union.Union.MsgLeagueOpt Res = new Revora.Network.GamePackets.Union.Union.MsgLeagueOpt(true);
                            Res.Action = Revora.Network.GamePackets.Union.Union.Actions.KingdomIcon;
                            if (client.Union != null)
                            {
                                Res.dwParam2 = client.Union.ID;
                            }
                            Res.dwParam3 = Kernel.ServerKingdom;
                            client.Send(Res.ToArray());
                        }
                        if (client.Union != null)
                        {
                            client.Send(client.Union.SendOverheadLeagueInfo(client));
                        }
                        if (client.Union != null)
                        {
                            if (client.Union.IsKingdom())
                            {
                                if (client.Union.IsLeader(client.Entity.UID))
                                {
                                    //union titles
                                    //new TitleStorage().AddTitle(client, 4001, 1000, false);
                                    //new TitleStorage().AddTitle(client, 1, 1000, false);
                                    Revora.Network.GamePackets.Union.Union.MsgLeagueOpt Res = new Revora.Network.GamePackets.Union.Union.MsgLeagueOpt(true);
                                    Res.Action = Revora.Network.GamePackets.Union.Union.Actions.KingdomTitleGui;
                                    Res.dwParam3 = Kernel.ServerKingdom;
                                    client.Send(Res.ToArray());
                                }
                            }
                        }
                        #endregion
                        MsgSignIn.Show(client);
                        client.LoadItemStats();
                        if (client.Equipment != null && client.Equipment.Objects != null)
                        {
                            foreach (ConquerItem worn in client.Equipment.Objects)
                            {
                                if (worn != null)
                                    worn.IsWorn = true;
                            }
                            client.Equipment.UpdateEntityPacket();
                        }
                        break;
                    }
                case Data.ChangeFace:
                    {
                        GeneralData.ChangeFace(gData, client);
                        break;
                    }
                case Data.ObserveEquipment:
                case Data.ObserveEquipment2:
                case Data.ObserveKnownPerson:
                    {
                        GeneralData.ObserveEquipment(gData, client);
                        break;
                    }
                case Data.ViewEnemyInfo:
                    {
                        if (client.Enemy.ContainsKey(gData.dwParam))
                        {
                            if (client.Enemy[gData.dwParam].IsOnline)
                            {
                                KnownPersonInfo info = new KnownPersonInfo(true);
                                info.Fill(client.Enemy[gData.dwParam], true, false);
                                if (client.Enemy[gData.dwParam].Client.Guild != null)
                                    client.Enemy[gData.dwParam].Client.Guild.SendName(client);
                                client.Send(info);
                            }
                        }
                        break;
                    }
                case Data.ViewFriendInfo:
                    {
                        if (client.Friends.ContainsKey(gData.dwParam))
                        {
                            if (client.Friends[gData.dwParam].IsOnline)
                            {
                                KnownPersonInfo info = new KnownPersonInfo(true);
                                info.Fill(client.Friends[gData.dwParam], false, false);
                                if (client.Friends[gData.dwParam].Client.Guild != null)
                                    client.Friends[gData.dwParam].Client.Guild.SendName(client);
                                client.Send(info);
                            }
                        }
                        break;
                    }
                case Data.ViewPartnerInfo:
                    {
                        if (client.Partners.ContainsKey(gData.dwParam))
                        {
                            if (client.Partners[gData.dwParam].IsOnline)
                            {
                                TradePartnerInfo info = new TradePartnerInfo(true);
                                info.Fill(client.Partners[gData.dwParam]);
                                if (client.Partners[gData.dwParam].Client.Guild != null)
                                    client.Partners[gData.dwParam].Client.Guild.SendName(client);
                                client.Send(info);
                            }
                        }
                        break;
                    }
                case Data.EndFly:
                    {
                        if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Freeze))
                        {
                            client.Send(new Message("Sorry can't EndFly now just wait", Message.System));
                            return;
                        }
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);
                        break;
                    }
                case Data.EndTransformation:
                    {
                        client.Entity.Untransform();
                        break;
                    }
                case Data.XPListEnd:
                    {
                        break;
                    }
                case Data.Die:
                    break;
                case Data.OwnBooth:
                    {
                        client.Booth = new Game.ConquerStructures.Booth(client, gData);
                        client.Send(new Data(true)
                        {
                            ID = Data.ChangeAction,
                            UID = client.Entity.UID,
                            dwParam = 0
                        }
                        );
                        client.Send(new MapStatus() { BaseID = client.Map.BaseID, ID = client.Map.ID, Status = Database.MapsTable.MapInformations[1002].Status });
                        break;
                    }
                case Data.Away:
                    {
                        if (client.Entity.Away == 0)
                        {
                            client.Entity.Away = 1;
                        }
                        else
                        {
                            client.Entity.Away = 0;
                            client.SendScreenSpawn(client.Entity, false);
                        }
                        break;
                    }
                case Data.DeleteCharacter:
                    {
                        break;
                    }
                case Data.GoldBricks:
                    {
                        client.Union.GoldBricks += client.Entity.PuntosUnion;
                        client.Entity.lacb += client.Entity.PuntosUnion;
                        client.Entity.PuntosUnion = 0;
                        break;
                    }
                case Data.TeamSearchForMember:
                    {
                        if (client.Team != null)
                        {
                            Client.GameState Client = null;
                            if (!client.Team.IsTeammate(gData.UID)) return;
                            if (Kernel.GamePool.TryGetValue(gData.UID, out Client))
                            {
                                gData.wParam1 = Client.Entity.X;
                                gData.wParam2 = Client.Entity.Y;
                                gData.Send(client);
                            }
                        }
                        break;
                    }
                default: if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                        client.Send("[Data] Unknown Action: " + gData.ID); break;
            }
        }
        public ushort Thundercloud;
    }
}