// ☺ Created by ThroneProject
// All rights reserved © 2015 - 2017 Throne Digital
// ☺ Throne - Project

using System;
using COServer.Game;

namespace COServer.Network.GamePackets
{
    public unsafe class MsgUpdate : Writer, Interfaces.IPacket
    {
        public class Flags4
        {
            public const ulong
            Omnipotence = 1UL << 192,
            JusticeChant = 1UL << 194,
            BlockFrostGaze = 1UL << 195,
            HealingSnow = 1UL << 196,
            ChillingSnow = 1UL << 197,
            xChillingSnow = 1UL << 198,
            Thunderbolt = 1UL << 199,
            FreezingPelter = 1UL << 200,
            xFreezingPelter = 1UL << 201,
            RevengeTaill = 1UL << 202,
            WindwalkerTop = 1UL << 203,
            ShadowofChaser = 1UL << 204;

        }
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
            ChaosCycle = 1UL << 60,
            ManiacDance = 1UL << 61
        }
        [Flags]
        public enum Flags2 : ulong
        {
            Top2Weekly = 1UL << 65,
            Top2Monthly = 1UL << 68,
            MonthlyTop8Pk = 0x8,
            Top8Fire = 1UL << 6,
            EffectBall = 1UL << 119,
            Top2Fire = 1UL << 7,
            Top3Fire = 1UL << 8,
            Top8Water = 1UL << 9,
            Top2Water = 1UL << 10,
            Top3Water = 1UL << 11,
            Top8Ninja = 1UL << 12,
            Top2Ninja = 1UL << 13,
            Top3Ninja = 1UL << 14,
            Top8Warrior = 1UL << 15,
            Top2Warrior = 1UL << 16,
            Top3Warrior = 1UL << 17,
            Top8Trojan = 1UL << 18,
            Top2Trojan = 1UL << 19,
            Top3Trojan = 1UL << 20,
            Top8Archer = 1UL << 21,
            Top2Archer = 1UL << 22,
            Contestant = 1UL << 91,
            ChainBoltActive = 1UL << 92,
            AzureShield = 1UL << 93,
            ScurvyBomb = 1UL << 96,
            TyrantAuraIcon = 1UL << 97,
            TyrantAura = 1UL << 98,
            FendAuraIcon = 1UL << 99,
            FendAura = 1UL << 100,
            MetalAuraIcon = 1UL << 101,
            MetalAura = 1UL << 102,
            WoodAuraIcon = 1UL << 103,
            WoodAura = 1UL << 104,
            WaterAuraIcon = 1UL << 105,
            WaterAura = 1UL << 106,
            FireAuraIcon = 1UL << 107,
            FireAura = 1UL << 108,
            EarthAuraIcon = 1UL << 109,
            EarthAura = 1UL << 110,
            SoulShackle = 1UL << 111,
            Oblivion = 1UL << 112,
            ShieldBlock = 1UL << 113,
            TopMonk = 1UL << 114,
            Top2Monk = 1UL << 116,
            CarryingFlag = 1UL << 118,
            PoisonStar = 1UL << 119,
            CannonBarrage = 1UL << 120,
            BlackbeardsRage = 1UL << 121,
            TopPirate = 1UL << 122,
            TopPirate2 = 1UL << 124,
            DefensiveStance = 1UL << 126,
            MagicDefenderIcon = 1UL << 127,

        }
        [Flags]
        public enum Flags3 : ulong
        {

            #region Epic Warrior
            BackFire = 1UL << 51,
            ManiacDance = 1UL << 53,
            WarriorEpicShield = 1UL << 55,
            RecuperarXP = 1UL << 178,
            RupturaDeFuerza = 1UL << 181,
            AuraAzul = 1UL << 183,
            #endregion
            MagicDefender = 1UL << 128,
            NinjaFaction = 1UL << 129,
            PirateFaction = 1UL << 130,
            ShieldBreak = 1UL << 176,
            DivineGuard = 1UL << 177,
            skull = (uint)1UL << 4,
            PurpleBall = 1UL << 131,
            BlueBall = 1UL << 132,
            PathOfShadow = 1UL << 145,
            BladeFlurry = 1UL << 146,
            KineticSpark = 1UL << 147,
            DragonFlow = 1UL << 148,
            Test = 1UL << 149,
            SuperCyclone = 1UL << 150,
            ConuqerSuperYellow = 1UL << 151,
            ConuqerSuperBlue = 1UL << 152,
            ConuqerSuperUnderBlue = 1UL << 153,
            DragonWarriorTop = 1UL << 154,
            AutoHunting = 1UL << 155,
            DragonFury = 1UL << 158,
            DragonCyclone = 1UL << 159,
            DragonSwing = 1UL << 160,
            MrConquer = 1UL << 166,
            MsConquer = 1UL << 167,
            FlameLayer = 1UL << 168,
            FlameLayer2 = 1UL << 169,
            FlameLayer3 = 1UL << 170,
            FlameLayer4 = 1UL << 171,
            AuroraLotus = 1UL << 172,
            FlameLotus = 1UL << 173,
            rygh_hglx = 1UL << 174,
            rygh_syzs = 1UL << 175,
            battelpower = 1UL << 174,
            GoldBrickNormal = 1UL << 161,
            GoldBrickRefined = 1UL << 162,
            GoldBrickUnique = 1UL << 163,
            GoldBrickElite = 1UL << 164,
            GoldBrickSuper = 1UL << 165,
            battelpower2 = 1UL << 175,
            BPGoldenHalo = 1UL << 174,
            BPHalo = 1UL << 175,
            XP1WindWalker = 1UL << 176,
        }
        [Flags]
        public enum DataType : uint
        {
            mantos = 82,
            mantos1 = 1,
            mantos2 = 2,
            mantos3 = 3,
            mantos4 = 4,
            mantos5 = 5,
            mantos6 = 6,
            mantos7 = 7,
            mantos8 = 8,
            mantos9 = 9,
            mantos10 = 10,
            mantos11 = 11,
            mantos12 = 12,
            mantos13 = 13,
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
            TimeDropSlayer = 18,// provisional
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
            ClanBattlePower = 42,
            GuildBattlePower = 44,
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
            Lotus = 78,
            ExtraInventory = 79,
            AvailableSlots = 80,
            UnionMember = 81,
            MilitaryRank = 82,
            UnionRank = 83,
            MilitaryExploits = 84,
            FlameLayer = 168,
            FlameLayer2 = 169,
            FlameLayer3 = 170,
            FlameLayer4 = 171,
            AuroraLotus = 172,
            FlameLotus = 173,
            AngerCoolDown
        }
        public const byte
            mantos = 82,
            mantos1 = 1,
            mantos2 = 2,
            mantos3 = 3,
            mantos4 = 4,
            UnionRank = 83,
            MilitaryRank = 82,
            mantos5 = 5,
            mantos6 = 6,
            mantos7 = 7,
            mantos8 = 8,
            mantos9 = 9,
            mantos10 = 10,
            mantos11 = 11,
            mantos12 = 12,
            mantos13 = 13;
        byte[] Packet;
        const byte MinPacketSize = 96;
        public MsgUpdate(bool Count)
        {
            if (Count)
            {
                Packet = new byte[MinPacketSize + 8];
                WriteUInt16(MinPacketSize, 0, Packet);
                WriteUInt16((ushort)PacketID.MsgUserAttrib, 2, Packet);
                WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Packet);
            }
        }
        public UInt32 UID
        {
            get { return BitConverter.ToUInt32(Packet, 8); }
            set { WriteUInt32(value, 8, Packet); }
        }
        public UInt32 UpdateCount
        {
            get { return BitConverter.ToUInt32(Packet, 12); }
            set
            {
                Byte[] buffer = new Byte[MinPacketSize + 8 + 28 * value];
                Packet.CopyTo(buffer, 0);
                WriteUInt16((UInt16)(MinPacketSize + 28 * value), 0, buffer);
                Packet = buffer;
                WriteUInt32(value, 12, Packet);
            }
        }
        public void Clear()
        {
            Byte[] Pack = new Byte[MinPacketSize + 8];
            WriteUInt16(MinPacketSize, 0, Packet);
            WriteUInt16(10017, 2, Packet);
            WriteUInt32(UID, 8, Pack);
            WriteUInt32((UInt32)Time32.timeGetTime().GetHashCode(), 4, Packet);
            Packet = Pack;
        }
        public void AppendFull(byte type, ulong val1, ulong val2, ulong val3)
        {
            UpdateCount = UpdateCount + 1;
            ushort offset = (ushort)(16 + ((UpdateCount - 1) * 28));
//#warning If this misbehaves, raise this to 32
            WriteUInt32(type, offset, Packet);
            WriteUInt64(val1, offset + 4, Packet);
            WriteUInt64(val2, offset + 12, Packet);
            WriteUInt64(val3, offset + 20, Packet);
        }
        public void Append(byte type, byte value)
        {
            UpdateCount++;
            UInt16 offset = (UInt16)(16 + ((UpdateCount - 1) * 28));
//#warning same, plus 4
            WriteUInt32(type, offset, Packet);
            WriteUInt64((UInt32)value, offset + 4, Packet);
            WriteUInt64(value, offset + 8, Packet);
        }
        public void Append(ulong val1, ulong val2, ulong val3, uint val4, uint val5, uint val6, uint val7)
        {
            UpdateCount = 2;
            WriteUInt32(25, 16, Packet);
            WriteUInt64(val1, 20, Packet);
            WriteUInt64(val2, 28, Packet);
            WriteUInt64(val3, 36, Packet);
            WriteUInt32(val4, 48, Packet);
            WriteUInt32(val5, 52, Packet);
            WriteUInt32(val6, 56, Packet);
            WriteUInt32(val7, 70, Packet);
        }
        public void Append(byte type, uint value) // NO
        {
            UpdateCount++;
            UInt16 offset = (UInt16)(16 + ((UpdateCount - 1) * 32));
            WriteUInt32(type, offset, Packet);
            WriteUInt64((UInt64)value, offset + 4, Packet);
        }
        public static void MsgUpdateActivenessTask(Client.GameState client, byte Task, byte value)
        {
            byte[] Buffer = new byte[20];
            Writer.WriteByte(12, 0, Buffer);
            Writer.WriteUInt16(2820, 2, Buffer);//Length 
            Writer.WriteByte(3, 4, Buffer);//Unknown 
            Writer.WriteByte(1, 5, Buffer);//AmountTasksWillUpdate 
            Writer.WriteByte(Task, 6, Buffer);//type 
            Writer.WriteByte(0, 10, Buffer);//Completed 
            Writer.WriteByte(value, 11, Buffer);//value 
            client.Send(Buffer);
        }
        public void Append(Byte type, UInt64 value)
        {
            UpdateCount++;
            UInt16 offset = (UInt16)(16 + ((UpdateCount - 1) * 28));
//#warning same, plus 4
            WriteUInt32(type, offset, Packet);
            WriteUInt64(value, offset + 4, Packet);
        }
        public void Append(Byte type, UInt16 value)
        {
            UpdateCount = UpdateCount + 1; // 0
            UInt16 offset = (UInt16)(16 + ((UpdateCount - 1) * 28));
            WriteUInt32(type, offset, Packet);
            WriteUInt64(value, offset + 4, Packet);
        }
        public void Append(Byte type, UInt32[] value)
        {
            UpdateCount = UpdateCount + 1;
            UInt16 offset = (UInt16)(16 + ((UpdateCount - 1) * 28));
            WriteUInt32(type, offset, Packet);
            Int32 position = offset + 4; // 2
            for (Int32 x = 0; x < value.Length; x++)
            {
                WriteUInt32(value[x], (UInt16)(position), Packet);
                position += 4;
            }
        }
        public void Append(Byte type, UInt64 val1, UInt64 val2, UInt64 val3)
        {
            UpdateCount = UpdateCount + 1;
            UInt16 offset = (UInt16)(16 + ((UpdateCount - 1) * 28));
            WriteUInt32(type, offset, Packet);
            WriteUInt64(val1, offset + 4, Packet);
            WriteUInt64(val2, offset + 12, Packet);
            WriteUInt64(val3, offset + 20, Packet);
        }
        public void Append(byte type, ulong val1, ulong val2, ulong val3, ulong val4)
        {
            UpdateCount = UpdateCount + 1;
            ushort offset = (ushort)(16 + ((UpdateCount - 1) * 28));
            WriteUInt32(type, offset, Packet);
            WriteUInt64(val1, offset + 4, Packet);
            WriteUInt64(val2, offset + 12, Packet);
            WriteUInt64(val3, offset + 20, Packet);
            WriteUInt64(val4, offset + 28, Packet);
        }
        public void Append(Byte type, UInt32 Flag, UInt32 Time, UInt32 Dmg, UInt32 Level)
        {
            UpdateCount = UpdateCount + 1;
            UInt16 offset = (UInt16)(16 + (UpdateCount - 1) * 28); // 28
            WriteUInt32(type, offset, Packet);
            WriteUInt32(Flag, (UInt16)(offset + 4), Packet); // 2
            WriteUInt32(Time, (UInt16)(offset + 8), Packet); // 4
            WriteUInt32(Dmg, (UInt16)(offset + 12), Packet); // 8
            WriteUInt32(Level, (UInt16)(offset + 16), Packet); // 14
        }
        public void Aura(Entity Entity, Enums.AuraDataTypes State, Enums.AuraType AuraType, UInt32 Level, UInt32 Power)
        {
            Byte[] Packet = new Byte[40]; // 40
            fixed (Byte* Pack = Packet)
            {
                *(UInt16*)(Pack + (Packet.Length - 8)) = 0;
                *((UInt16*)(Pack + 2)) = (ushort)PacketID.MsgAura;
                *((UInt32*)(Pack + 8)) = (UInt32)State;
                *((UInt32*)(Pack + 12)) = Entity.UID;
                *((UInt32*)(Pack + 16)) = (UInt32)AuraType;
                *((UInt32*)(Pack + 20)) = Level;
                *((UInt32*)(Pack + 24 + 4)) = Power;
                *((UInt32*)(Pack + 28 + 4)) = Power;
                if (Kernel.GamePool.ContainsKey(Entity.UID))
                {
                    Kernel.GamePool[Entity.UID].Send(Packet);
                }
            }
        }
        public void xSoulShackle(Entity Entity, int Dmg, byte SpellLevel, byte Time_)
        {
            byte[] buffer = new byte[84 + 4 + 8];
            Writer.WriteUInt16(84 + 4 + 8, 0, buffer);
            Writer.WriteUInt16(10017, 2, buffer);
            Writer.WriteUInt32(Entity.UID, 4 + 4, buffer);
            Writer.WriteUInt32(2, 8 + 4, buffer);
            Writer.WriteInt32(25, 12 + 4, buffer);
            Writer.WriteUInt64(Entity.StatusFlag, 16 + 4, buffer);
            Writer.WriteUInt64(Entity.StatusFlag2, 16 + 4 + 8, buffer);
            Writer.WriteUInt32(0x80, 29 + 4, buffer);
            Writer.WriteUInt32(54, 36 + 4, buffer);
            Writer.WriteUInt32(111, 40 + 4, buffer);
            Writer.WriteInt32(Time_, 44 + 4, buffer);
            if (Kernel.GamePool.ContainsKey(Entity.UID))
            {
                Kernel.GamePool[Entity.UID].Send(buffer);
            }
        }
        public void Aura(Entity Entity, Enums.AuraDataTypes State, Enums.AuraType AuraType, Database.SpellInformation spell)
        {
            Aura(Entity, State, AuraType, spell.Level, spell.Power);
        }
        public void Append(Byte type, Byte value, Byte second, Byte second2, Byte second3, Byte second4, Byte second5, Byte second6, Byte second7)
        {
            UpdateCount = UpdateCount + 1;
            UInt16 offset = (UInt16)(UpdateCount * 16);
            WriteUInt32(type, offset, Packet);
            WriteUInt64(value, offset + 4, Packet);
            WriteByte(second, offset + 12, Packet);
            WriteByte(second2, offset + 13, Packet);
            WriteByte(second3, offset + 14, Packet);
            WriteByte(second4, offset + 15, Packet);
            WriteByte(second5, offset + 16, Packet);
            WriteByte(second6, offset + 17, Packet);
            WriteByte(second7, offset + 18, Packet);
        }
        public Byte[] ToArray()
        {
            return Packet;
        }
        public void Deserialize(Byte[] buffer)
        {
            Packet = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Packet);
        }
    }
}