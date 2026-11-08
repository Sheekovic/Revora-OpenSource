using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public class UnionData : Writer, Interfaces.IPacket
    {
        public class CustomCommands
        {
            public const ushort
                ExitQuestion = 1,
                Minimize = 2,
                ShowReviveButton = 1053,
                FlowerPointer = 1067,
                Enchant = 1091,
                RemoveTrap = 434,
                LoginScreen = 1153,
                SelectRecipiet = 30,
                JoinGuild = 34,
                MakeFriend = 38,
                ChatWhisper = 40,
                CloseClient = 43,
                HotKey = 53,
                Furniture = 54,
                TQForum = 79,
                PathFind = 97,
                LockItem = 102,
                ShowRevive = 1053,
                HideRevive = 1054,
                StatueMaker = 1066,
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
                //    RemoveTrap = 434,
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
                JiangHuSetName = 617,
                Reincarnation = 485;


        }
        public enum Mode : uint
        {
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
            AddEntity = 134,
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
            RemoveTrap = 434,
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
            Sash = 256,
            DetainWindowRequest = 153
        }

        public const ushort
            DragonBallDropped = 165,
             JumpOutEffect = 134,
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
                TeamMemberPos = 101,
                UnknownEntity = 102,
                TeamSearchForMember = 106,
                NewCoordonates = 108,
                OwnBooth = 111,
                GetSurroundings = 114,
                OpenCustom = 116,
                ObserveEquipment = 117,
                EndTransformation = 118,
                EndFly = 120,
                ViewEnemyInfo = 123,
                OpenWindow = 126,
                JiangHu = 126,
                CompleteLogin = 251,
                RemoveEntity = 135,
                AddEntity = 134,
                Jump = 137,
                Die = 145,
                EndTeleport = 146,
                ViewFriendInfo = 148,
                ChangeFace = 151,
                ViewPartnerInfo = 152,
                Confiscator = 153,
                OpenShop = 0xA0,
                FlashStep = 156,
                CountDown = 159,
            // Shop = 160,
                Away = 161,
                Bulletin = 166,
                AutoPatcher = 162,
                AppearanceType = 178,
            //AllowAnimation = 251,
                LevelUpSpell = 252,
                ExtraInventory = 256,
                LevelUpProficiency = 253,
                ObserveEquipment2 = 310,
                BeginSteedRace = 401,
                FirstCredit = 255,
                FinishSteedRace = 402,

                DetainWindowRequest = 153;
        public const uint
   RemoveTrap = 434,
   Shop = 160;

        public static UnionData Custom(uint customType, uint uid, ushort wParam1 = 0, ushort wParam2 = 0, ushort X = 0, ushort Y = 0)
        {
            UnionData data = new UnionData(true);
            data.ID = Data.OpenCustom;
            data.UID = uid;
            data.TimeStamp = Time32.Now;
            data.dwParam = customType;
            data.X = X;
            data.Y = Y;
            //  data.wParam1 = wParam1;
            // data.wParam2 = wParam2;
            return data;

        }
        public UnionData(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[49]; // 41 + 8
                WriteUInt16(41, 0, Buffer);
                WriteUInt16(10010, 2, Buffer);
                WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
            }
        }
        public UnionData(uint Identifier, uint Value1, ushort Value2, ushort Value3, ushort Type)
        {
            byte[] Buffer = new byte[40]; // 8 + 32 40
            WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            WriteUInt16((ushort)10010, 2, Buffer);
            WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
            WriteInt32((int)Identifier, 8, Buffer);
            WriteInt32((int)Value1, 12, Buffer);
            //WriteInt32((int)Value5, 8, Buffer);
            WriteInt32(Type, 14, Buffer);
            WriteUInt16(Value2, 16, Buffer);
            WriteUInt16(Value3, 18, Buffer);

        }
        byte[] Buffer;
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { Writer.WriteUInt32(value, 8, Buffer); }
        }
        #region dwParam
        /* public ushort X
        {
            get { return (ushort)dwParam; }
            set { dwParam = (uint)((Y << 16) | value); }
        }

        public ushort Y
        {
            get { return (ushort)(dwParam >> 16); }
            set { dwParam = (uint)((value << 16) | X); }
        }*/
        public ushort Y
        {
            get { return BitConverter.ToUInt16(Buffer, 30); }
            set { WriteUInt32(value, 30, Buffer); }
        }
        public ushort X
        {
            get { return BitConverter.ToUInt16(Buffer, 28); }
            set { WriteUInt32(value, 28, Buffer); }
        }
        #endregion
        public uint dwParam
        {
            get
            {
                return BitConverter.ToUInt32(Buffer, 12);
            }
            set
            {
                WriteUInt32(value, 12, Buffer);
            }
        }
        public ushort Type
        {
            get { return BitConverter.ToUInt16(Buffer, 22); }
            set { WriteUInt32(value, 22, Buffer); }
        }
        public uint Data24_Uint
        {
            get
            {
                return BitConverter.ToUInt32(Buffer, 28);
            }
            set
            {
                WriteUInt32(value, 28, Buffer);
            }
        }

        public Time32 TimeStamp
        {
            get
            {
                return new Time32(BitConverter.ToUInt32(Buffer, 20));
            }
            set
            {
                WriteUInt32((uint)value.GetHashCode(), 20, Buffer);
            }
        }
        public uint TimeStamp2
        {
            get
            {
                return BitConverter.ToUInt32(Buffer, 20);
            }
            set
            {
                WriteUInt32(value, 20, Buffer);
            }
        }
        public uint ForShopId
        {
            get
            {
                return BitConverter.ToUInt16(Buffer, 20);
            }
            set
            {
                WriteUInt32(value, 20, Buffer);
            }
        }
        public uint ShopID
        {
            get
            {
                return BitConverter.ToUInt16(Buffer, 20);
            }
            set
            {
                WriteUInt32(value, 20, Buffer);
            }
        }
        public uint ID
        {
            get
            {
                return BitConverter.ToUInt16(Buffer, 24);
            }
            set
            {
                WriteUInt32(value, 24, Buffer);
            }
        }

        public Game.Enums.ConquerAngle Facing
        {
            get
            {
                return (Game.Enums.ConquerAngle)Buffer[26];
            }
        }

        public ushort wParam1
        {
            get
            {
                return BitConverter.ToUInt16(Buffer, 28);
            }
            set
            {
                WriteUInt16(value, 28, Buffer);
            }
        }

        public ushort wParam2
        {
            get
            {
                return BitConverter.ToUInt16(Buffer, 30);
            }
            set
            {
                WriteUInt16(value, 30, Buffer);
            }
        }



        public uint wParam3
        {
            get
            {
                return BitConverter.ToUInt32(Buffer, 28);
            }
            set
            {
                WriteUInt32(value, 28, Buffer);
            }
        }

        public uint wParam4
        {
            get
            {
                return BitConverter.ToUInt32(Buffer, 30);
            }
            set
            {
                WriteUInt32(value, 30, Buffer);
            }
        }

        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer;
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }

        public uint wParam5
        {
            get
            {
                return Revora.BitConverter.ToUInt32(this.Buffer, 32 + 4);
            }
            set
            {
                Writer.WriteUInt32(value, 32 + 4, this.Buffer);
            }
        }
    }
}
