using Revora.Game;
using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class FlagsUpdate : Writer, Interfaces.IPacket
    {
        [ProtoContract]
        public class CMsgUserAttrib
        {
            [ProtoMember(1, IsRequired = true)]
            public ulong TimeStamp;
            [ProtoMember(2, IsRequired = true)]
            public uint UID;
            [ProtoMember(3, IsRequired = true)]
            public List<CMsgFlags> Values = new List<CMsgFlags>();
        }
        [ProtoContract]
        public class CMsgFlags
        {
            [ProtoMember(1, IsRequired = true)]
            public ulong Type;
            [ProtoMember(2, IsRequired = false)]
            public ulong value;
            [ProtoMember(3, IsRequired = false)]
            public ulong[] Flags;
            [ProtoMember(4, IsRequired = false)]
            public ulong ICOID;
            [ProtoMember(5, IsRequired = false)]
            public ulong Time;
            [ProtoMember(6, IsRequired = false)]
            public ulong Amount;
            [ProtoMember(7, IsRequired = false)]
            public ulong value5;

        }
        public const byte
            StatusFlag = 25,
            Lotus = 78,
            FlameLotus = 173,
            AuroraLotus = 172;
        public const byte MentorBattlePower = 36;
        public class OnlineTraining
        {
            public const byte
            Show = 0,
            InTraining = 1,
            Review = 2,
            IncreasePoints = 3,
            ReceiveExperience = 4,
            Remove = 5;
        }
        [Flags]
        public enum Flags : ulong
        {
            FlashingName = 0,
            Poisoned = 1,
            Invisible = 2,
            Normal = 3,
            XPList = 4,
            Dead = 5,
            TeamLeader = 6,
            StarOfAccuracy = 7,
            MagicShield = 8,
            Stigma = 9,
            Ghost = 10,
            FadeAway = 11,
            RedName = 14,
            BlackName = 15,
            ReflectMelee = 17,
            Superman = 18,
            Ball = 19,
            Ball2 = 20,
            Invisibility = 22,
            Cyclone = 23,
            Dodge = 26,
            Fly = 27,
            Intensify = 28,
            CastPray = 30,
            Praying = 31,
            Cursed = 32,
            HeavenBlessing = 33,
            TopGuildLeader = 34,
            TopDeputyLeader = 35,
            MonthlyPKChampion = 36,
            WeeklyPKChampion = 37,
            TopWarrior = 38,
            TopTrojan = 39,
            TopArcher = 40,
            TopWaterTaoist = 41,
            TopFireTaoist = 42,
            TopNinja = 43,
            ShurikenVortex = 46,
            FatalStrike = 47,
            Flashy = 48,
            Ride = 50,
            TopSpouse = 51,
            OrangeSparkles = 52,
            PurpleSparkles = 53,
            FreezeSmall = 54,
            SpeedIncreased = 55,
            IncMoveSpeed = 56,
            GodlyShield = 57,
            Stun = 58,
            Freeze = 59,
            ChaosCycle = 60,
            Top2Weekly = 65,
            Top2Monthly = 68,
            Top2Fire = 71,
            Top2Water = 74,
            Top2Ninja = 77,
            Top2Warrior = 80,
            Top2Trojan = 83,
            Top2Archer = 86,
            Contestant = 91,
            ChainBoltActive = 92,
            AzureShield = 93,
            ScurvyBomb = 96,
            TyrantAura = 98,
            FendAura = 100,
            MetalAura = 102,
            WoodAura = 104,
            WaterAura = 106,
            FireAura = 108,
            EarthAura = 110,
            SoulShackle = 111,
            Oblivion = 112,
            ShieldBlock = 113,
            TopMonk = 114,
            Top2Monk = 116,
            CarryingFlag = 118,
            PoisonStar = 119,
            CannonBarrage = 120,
            BlackbeardsRage = 121,
            TopPirate = 122,
            TopPirate2 = 124,
            DefensiveStance = 126,
            MagicDefender = 128,
            PurpleBall = 131,
            BlueBall = 132,
            PathOfShadow = 145,
            BladeFlurry = 146,
            KineticSpark = 147,
            DragonFlow = 148,
            SuperCyclone = 150,
            ConuqerSuperYellow = 151,
            ConuqerSuperBlue = 152,
            ConuqerSuperUnderBlue = 153,
            DragonWarriorTop = 154,
            AutoHunting = 155,
            DragonFury = 158,
            DragonCyclone = 159,
            DragonSwing = 160,
            MrConquer = 166,
            MsConquer = 167,
            lianhuaran = 168,
            lianhuaran02 = 169,
            lianhuaran03 = 170,
            lianhuaran04 = 171,
            AuroraLotus = 172,
            FlameLotus = 173,
            ShieldBreak = 176,
            DivineGuard = 177,
            XPWarrior = 178,
            BackFire = 179,
            ManiacDance = 181,
            AuraAzul = 183,
            CrossElite_PK_Champion_High =  184,
            CrossElite_PK_2nd_High =  185,
            CrossElite_PK_3rd_High =  186,
            CrossElite_PK_Top8_High =  187,
            CrossElite_PK_Champion_Low =  188,
            CrossElite_PK_2nd_Low = 189,
            CrossElite_PK_3rd_Low =  190,
            CrossElite_PK_Top8_Low = 191,
            Omnipotence = 192,
            OmnipotenceDamage = 193,
            JusticeChant = 194,
            BlockFrostGaze = 195,
            HealingSnow = 196,
            ChillingSnow = 197,
            xChillingSnow = 198,
            FreezingPelter = 200,
            xFreezingPelter = 201,
            RevengeTaill = 202,
            WindWalkerTop = 203,
            ShadowofChaser = 204,
            Sacrifice = 211,
            FineRain = 212,
            Rampage = 214,
            Infinity = 215,
            Absolution = 216,
            IronShield = 217,
            Slayer = 218,
            FrostArrows = 219,
            ImmortalForce = 220,

            //Unrecognized
            TopPanda = 89,
            TopZorro =  90,
            GoldBrickNormal =  161,
            GoldBrickRefined = 162,
            GoldBrickUnique =  163,
            GoldBrickElite =  164,
            GoldBrickSuper =  165,
            Top2Candy = 81,
            Top2Mujer = 83,
            Top2Hombre =  84,
            TopFamoso =  88,
            TopMarcial =  124,
            TopFantasma =  125,
            TopHombreloco =  77,
            TopConejo = 76,
            TopHada = 78,
            VIP8Slayer =  185,
            VIP7Slayer =  186,
            VIP6Slayer =  187,
            VIP5Slayer =  188,
            VIP4Slayer =  189,
            JQ_dunpaichuantou =  176,// top champion
            Top3Water = 11,
            rygh_hglx = 174,
            rygh_syzs = 175,
            TopConde = 73, // top earl
            TopDuke =  115,// Top Duke
            TopPrincipe = 116, // top prince
            TopRey =  117,// top king

        }
        [Flags]
        public enum DataType : uint
        {
            Hitpoints = 0,
            MaxHitpoints = 1,
            Mana = 2,
            MaxMana = 3,
            Money = 4,
            Experience = 5,
            PKPoints = 6,
            Class = 7,
            Stamina = 8,
            WHMoney = 9,
            Atributes = 10,
            Mesh = 11,
            Level = 12,
            Spirit = 13,
            Vitality = 14,
            Strength = 15,
            Agility = 16,
            HeavensBlessing = 17,
            DoubleExpTimer = 18,
            CursedTimer = 20,
            Reborn = 22,
            StatusFlag = 25,
            HairStyle = 26,
            XPCircle = 27,
            LuckyTimeTimer = 28,
            ConquerPoints = 29,
            Domino = 30,
            RevoraPoints = 91, // 6711 inventory currency setter + UI refresh, distinct from racing points.
            OnlineTraining = 31,
            ExtraBattlePower = 36,
            ArsenalBattlepower = 37,
            Merchant = 38,
            VIPLevel = 39,
            QuizPoints = 40,
            EnlightPoints = 41,
            ClanBattlepower = 42,
            GuildBattlepower = 44,
            BoundConquerPoints = 45,
            RaceShopPoints = 47,
            Contestant = 48,
            AzureShield = 49,
            SecondRebornClass = 50,
            FirsRebornClass = 51,
            Team = 52,
            SoulShackle = 54,
            Fatigue = 55,
            DefensiveStance = 56,
            IncreasePStrike = 59,
            IncreaseMStrike = 60,
            IncreaseImunity = 61,
            IncreaseBreack = 62,
            IncreaseAntiBreack = 63,
            IncreaseMaxHp = 64,
            IncreasePAttack = 65,
            IncreaseMAttack = 66,
            IncreaseFinalPDamage = 67,
            IncreaseFinalMDamage = 68,
            IncreaseFinalPAttack = 69,
            IncreaseFinalMAttack = 70,
            FirstCreditGift = 71,
            ExpProtection = 73,
            DragonFury = 74,
            DragonSwing = 75,
            InnerPowerPotency = 77,
            ExtraInventory = 79,
            AvailableSlots = 80,
            ExploitsRank = 82,
            MilitaryRank = 82,
            UnionRank = 83,
            MilitaryExploits = 84,
            Anger = 90,
            XPLIST = 101,
            DivineGuard = 177,

            //Unrecognized
            TimeDropSlayer = 17,// provisional
            

        }
        /* [Flags]
         public enum Flags : ulong
         {
             FlashingName = 1UL << 0,
             Poisoned = 1UL << 1,
             Invisible = 1UL << 2,
             Normal = 1UL << 3,
             XPList = 1UL << 4,
             Dead = 1UL << 5,
             TeamLeader = 1UL << 6,
             StarOfAccuracy = 1UL << 7,
             MagicShield = 1UL << 8,
             Stigma = 1UL << 9,
             Ghost = 1UL << 10,
             FadeAway = 1UL << 11,
             RedName = 1UL << 14,
             BlackName = 1UL << 15,
             ReflectMelee = 1UL << 17,
             Superman = 1UL << 18,
             Ball = 1UL << 19,
             Ball2 = 1UL << 20,
             Invisibility = 1UL << 22,
             Cyclone = 1UL << 23,
             Dodge = 1UL << 26,
             Fly = 1UL << 27,
             Intensify = 1UL << 28,
             CastPray = 1UL << 30,
             Praying = 1UL << 31,
             Cursed = 1UL << 32,
             HeavenBlessing = 1UL << 33,
             TopGuildLeader = 1UL << 34,
             TopDeputyLeader = 1UL << 35,
             MonthlyPKChampion = 1UL << 36,
             WeeklyPKChampion = 1UL << 37,
             TopWarrior = 1UL << 38,
             TopTrojan = 1UL << 39,
             TopArcher = 1UL << 40,
             TopWaterTaoist = 1UL << 41,
             TopFireTaoist = 1UL << 42,
             TopNinja = 1UL << 43,
             ShurikenVortex = 1UL << 46,
             FatalStrike = 1UL << 47,
             Flashy = 1UL << 48,
             Ride = 1UL << 50,
             TopSpouse = 1UL << 51,
             OrangeSparkles = 1UL << 52,
             PurpleSparkles = 1UL << 53,
             FreezeSmall = 1UL << 54,
             SpeedIncreased = 1UL << 55,
             IncMoveSpeed = 1UL << 56,
             GodlyShield = 1UL << 57,
             Stun = 1UL << 58,
             Freeze = 1UL << 59,
             Infinity = 215,
             ChaosCycle = 1UL << 60,

             Omnipotence = 1UL << 192,
             JusticeChant = 1UL << 194,
             BlockFrostGaze = 1UL << 195,
             HealingSnow = 1UL << 196,
             ChillingSnow = 1UL << 197,
             xChillingSnow = 1UL << 198,
             FreezingPelter = 1UL << 200,
             xFreezingPelter = 1UL << 201,
             RevengeTaill = 1UL << 202,
             WindwalkerTop = 1UL << 203,
             ShadowofChaser = 1UL << 204,

             GoldSparkle = 4503599627370496,
             VioletSparkle = 9007199254740992,
             Top2Weekly = 1UL << 65,
             Top2Monthly = 1UL << 68,
             Top2Fire = 1UL << 71,
             TopConde = 1UL << 73, // top earl
             Top2Water = 1UL << 74,
             TopHombreloco = 1UL << 77,
             TopConejo = 1UL << 76,
             TopHada = 1UL << 78,
             Top2Warrior = 1UL << 80,
             Top2Candy = 1UL << 81,
             Top2Mujer = 1UL << 83,
             Top2Hombre = 1UL << 84,
             Top2Archer = 1UL << 86,
             TopFamoso = 1UL << 88,
             TopPanda = 1UL << 89,
             TopZorro = 1UL << 90,
             Contestant = 1UL << 91,
             ChainBoltActive = 1UL << 92,
             AzureShield = 1UL << 93,
             ScurvyBomb = 1UL << 96,
             TyrantAura = 1UL << 98,
             FendAura = 1UL << 100,
             MetalAura = 1UL << 102,
             WoodAura = 1UL << 104,
             WaterAura = 1UL << 106,
             FireAura = 1UL << 108,
             EarthAura = 1UL << 110,
             SoulShackle = 1UL << 111,
             Oblivion = 1UL << 112,
             ShieldBlock = 1UL << 113,
             TopDuke = 1UL << 115,// Top Duke
             TopMonk = 1UL << 114,
             TopPrincipe = 1UL << 116, // top prince
             TopRey = 1UL << 117,// top king
             CarryingFlag = 1UL << 118,
             PoisonStar = 1UL << 119,
             CannonBarrage = 1UL << 120,
             BlackbeardsRage = 1UL << 121,
             TopPirate = 1UL << 122,
             TopMarcial = 1UL << 124,
             TopFantasma = 1UL << 125,
             DefensiveStance = 1UL << 126,

             #region Epic Warrior
             BackFire = 1UL << 51,
             ManiacDance = 1UL << 53,
             WarriorEpicShield = 1UL << 55,
             RecuperarXP = 1UL << 178,
             RupturaDeFuerza = 1UL << 181,
             AuraAzul = 1UL << 183,
             #endregion
             MagicDefender = 1UL << 128,
             PurpleBall = 1UL << 131,
             BlueBall = 1UL << 132,
             PathOfShadow = 1UL << 145,
             BladeFlurry = 1UL << 146,
             KineticSpark = 1UL << 147,
             DragonFlow = 1UL << 148,
             SuperCyclone = 1UL << 150,
             ConuqerSuperYellow = 1UL << 151,
             ConuqerSuperBlue = 1UL << 152,
             ConuqerSuperUnderBlue = 1UL << 153,
             MRConquerHost = (ulong)1UL << 38,
             MSConquerHostess = (ulong)1UL << 39,
             DragonWarriorTop = 1UL << 154,
             AutoHunting = 1UL << 155,
             DragonFury = 1UL << 158,
             DragonCyclone = 1UL << 159,
             Flam1 = 1UL << 168,
             Flam2 = 1UL << 169,
             Flam3 = 1UL << 170,
             Flam4 = 1UL << 171,
             AuroraLotus = 1UL << 172,
             FlameLotus = 1UL << 173,
             DragonSwing = 1UL << 160,
             GoldBrickNormal = 1UL << 161,
             GoldBrickRefined = 1UL << 162,
             GoldBrickUnique = 1UL << 163,
             GoldBrickElite = 1UL << 164,
             GoldBrickSuper = 1UL << 165,
             lianhuaran01 = 1UL << 168,
             lianhuaran02 = 1UL << 169,
             lianhuaran03 = 1UL << 170,
             lianhuaran04 = 1UL << 171,
             rygh_hglx = 1UL << 174,
             rygh_syzs = 1UL << 175,
             JQ_dunpaichuantou = 1UL << 176,// top champion
             VIP8Slayer = 1UL << 185,
             VIP7Slayer = 1UL << 186,
             VIP6Slayer = 1UL << 187,
             VIP5Slayer = 1UL << 188,
             VIP4Slayer = 1UL << 189,
             TopMaestroDelAire = 1UL << 203,
         }*/
        /*[Flags]
        public enum DataType : uint
        {
            Hitpoints = 0,
            MaxHitpoints = 1,
            Mana = 2,
            MaxMana = 3,
            Money = 4,
            Experience = 5,
            PKPoints = 6,
            Class = 7,
            Stamina = 8,
            WHMoney = 9,
            Atributes = 10,
            Mesh = 11,
            Level = 12,
            Spirit = 13,
            Vitality = 14,
            Strength = 15,
            Agility = 16,
            HeavensBlessing = 17,
            DoubleExpTimer = 18,
            TimeDropSlayer = 17,// provisional
            CursedTimer = 20,
            Reborn = 22,
            StatusFlag = 25,
            HairStyle = 26,
            XPCircle = 27,
            LuckyTimeTimer = 28,
            ConquerPoints = 29,
            OnlineTraining = 31,
            ExtraBattlePower = 36,
            ArsenalBattlepower = 37,
            Merchant = 38,
            VIPLevel = 39,
            QuizPoints = 40,
            EnlightPoints = 41,
            ClanBattlepower = 42,
            ClanBattlePower = 42,
            GuildBattlepower = 44,
            BoundConquerPoints = 45,
            RaceShopPoints = 47,
            Contestant = 48,
            AzureShield = 49,
            SecondRebornClass = 50,
            FirsRebornClass = 51,
            Team = 52,
            SoulShackle = 54,
            Fatigue = 55,
            DefensiveStance = 56,
            IncreasePStrike = 59,
            IncreaseMStrike = 60,
            IncreaseImunity = 61,
            IncreaseBreack = 62,
            IncreaseAntiBreack = 63,
            IncreaseMaxHp = 64,
            IncreasePAttack = 65,
            IncreaseMAttack = 66,
            IncreaseFinalPDamage = 67,
            IncreaseFinalMDamage = 68,
            IncreaseFinalPAttack = 69,
            IncreaseFinalMAttack = 70,
            FirstCreditGift = 71,
            ExpProtection = 73,
            DragonFury = 74,
            DragonSwing = 75,
            InnerPowerPotency = 77,
            ExtraInventory = 79,
            AvailableSlots = 80,
            TareaInner = 85,
            UnionMember = 81,
            MilitaryRank = 82,
            UnionRank = 83,
            MilitaryExploits = 84,
            AngerCoolDown
        }*/
        public CMsgUserAttrib Buffer;
        const byte minBufferSize = 92;
        ushort PacketID = 10017;
        public FlagsUpdate(bool Create)
        {
            if (Create)
            {
                Buffer = new CMsgUserAttrib();
                Buffer.TimeStamp = (uint)Time32.timeGetTime().GetHashCode();
            }
        }
        public uint UID
        {
            get { return Buffer.UID; }
            set { Buffer.UID = value; }
        }
        public void Clear()
        {
            Buffer = new CMsgUserAttrib();
        }
        public void Append(byte type, byte value)
        {
            Buffer.Values.Add(new CMsgFlags() { Type = type, value = value });
        }
        public void Append(uint type, byte value)
        {
            Buffer.Values.Add(new CMsgFlags() { Type = type, value = value });
        }
        public void Append(byte type, ulong value)
        {
            Buffer.Values.Add(new CMsgFlags() { Type = type, value = value });
        }
        public void Append(byte type, uint value)
        {
            Buffer.Values.Add(new CMsgFlags() { Type = type, value = value });
        }
        public void Append(byte type, uint Flag, uint Time, uint Dmg, uint Level)
        {
            Buffer.Values.Add(new CMsgFlags() { Type = type, ICOID = Flag, Time = Time, Amount = Dmg });
        }
        public void AppendFull(byte type, uint[] Flags)
        {
            var f = new CMsgFlags() { Type = type, Flags = new ulong[7] };
            for (int i = 0; i < Flags.Length; i++)
            {
                f.Flags[i] = Flags[i];
            }
            Buffer.Values.Add(f);
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer.ConvertToProto<CMsgUserAttrib>();
        }
        public byte[] ToArray()
        {
            return Buffer.ToConquerPacket(PacketID);
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer.ToConquerPacket(PacketID));
        }
    }
}