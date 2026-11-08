using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network;
using Revora.Network.GamePackets;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Revora.Game
{
    public unsafe class Entity : Writer, IBaseEntity, IMapObject
    {
        #region New offsets System
        [ProtoContract]
        public class CMsgPlayer
        {
            [ProtoMember(1, IsRequired = true)]
            public ulong _Mesh;
            [ProtoMember(2, IsRequired = false)]
            public ulong _UID;
            [ProtoMember(3, IsRequired = false)]
            public ulong _GuildID;
            [ProtoMember(4, IsRequired = false)]
            public ulong _GuildRank;
            [ProtoMember(5, IsRequired = true)]
            public ulong _Unknown;//pokerpot
            [ProtoMember(6, IsRequired = true)]
            public ulong[] _StatusFlag = new ulong[7];
            [ProtoMember(7, IsRequired = true)]
            public ulong _Appearance;
            [ProtoMember(8, IsRequired = true)]
            public ulong _Head;
            [ProtoMember(9, IsRequired = true)]
            public ulong _Garment;
            [ProtoMember(10, IsRequired = true)]
            public ulong _Armor;
            [ProtoMember(11, IsRequired = true)]
            public ulong _LeftWeapon;
            [ProtoMember(12, IsRequired = true)]
            public ulong _RightWeapon;
            [ProtoMember(13, IsRequired = true)]
            public ulong _LeftWeaponAccessory;
            [ProtoMember(14, IsRequired = true)]
            public ulong _RightWeaponAccessory;
            [ProtoMember(15, IsRequired = true)]
            public ulong _Steed;
            [ProtoMember(16, IsRequired = true)]
            public ulong _MountArmor;
            [ProtoMember(17, IsRequired = true)]
            public ulong _Wing;
            [ProtoMember(18, IsRequired = true)]
            public ulong _WingPlus;
            [ProtoMember(19, IsRequired = true)]
            public ulong _WingProgress;
            [ProtoMember(20, IsRequired = true)]
            public ulong _unkown;
            [ProtoMember(21, IsRequired = false)]
            public ulong[] unkown7;
            [ProtoMember(22, IsRequired = true)]
            public ulong unkown2;
            [ProtoMember(23, IsRequired = true)]
            public ulong _Hitpoints;
            [ProtoMember(24, IsRequired = true)]
            public ulong unkown4;
            [ProtoMember(25, IsRequired = true)]
            public ulong unkown5;
            [ProtoMember(26, IsRequired = true)]
            public ulong _MonsterLevel;
            [ProtoMember(27, IsRequired = true)]
            public ulong _X;
            [ProtoMember(28, IsRequired = true)]
            public ulong _Y;
            [ProtoMember(29, IsRequired = true)]
            public ulong _HairStyle;
            [ProtoMember(30, IsRequired = true)]
            public ulong _Facing;
            [ProtoMember(31, IsRequired = true)]
            public ulong _Action;
            [ProtoMember(32, IsRequired = true)]
            public ulong _Action2;
            [ProtoMember(33, IsRequired = true)]
            public ulong _unkown6;
            [ProtoMember(34, IsRequired = true)]
            public ulong _Reborn;
            [ProtoMember(35, IsRequired = true)]
            public ulong _Level;
            [ProtoMember(36, IsRequired = true)]
            public ulong __WindowSpawn;
            [ProtoMember(37, IsRequired = true)]
            public ulong _Away;
            [ProtoMember(38, IsRequired = true)]
            public ulong _ExtraBattlePower;
            [ProtoMember(39, IsRequired = true)]//i don't know what start from here 
            public ulong unkown10;
            [ProtoMember(40, IsRequired = true)]
            public ulong unkown11;
            [ProtoMember(41, IsRequired = true)]
            public ulong unkown12;
            [ProtoMember(42, IsRequired = true)]
            public ulong _Flower;
            [ProtoMember(43, IsRequired = true)]
            public ulong _NobilityRank;
            [ProtoMember(44, IsRequired = true)]
            public ulong _ArmorColor;
            [ProtoMember(45, IsRequired = true)]
            public ulong _LeftWeaponColor;
            [ProtoMember(46, IsRequired = true)]
            public ulong _HeadColor;
            [ProtoMember(47, IsRequired = true)]
            public ulong _QuizPoints;
            [ProtoMember(48, IsRequired = true)]
            public ulong _SteedPlus;
            [ProtoMember(49, IsRequired = true)]
            public ulong _SteedPlus1;
            [ProtoMember(50, IsRequired = true)]
            public ulong _Enlighten;
            [ProtoMember(51, IsRequired = true)]
            public ulong _TotalInnerScore;
            [ProtoMember(52, IsRequired = true)]
            public ulong unwkon16;
            [ProtoMember(53, IsRequired = true)]
            public ulong _ClanUID;
            [ProtoMember(54, IsRequired = true)]
            public ulong _ClanRank;
            [ProtoMember(55, IsRequired = true)]
            public ulong _ClanSharedBp;
            [ProtoMember(56, IsRequired = true)]
            public ulong _TitleActivated;
            [ProtoMember(57, IsRequired = true)]
            public ulong unwkon21;
            [ProtoMember(58, IsRequired = true)]
            public ulong unwkon22;
            [ProtoMember(59, IsRequired = true)]
            public ulong _GuildSharedBp;
            [ProtoMember(60, IsRequired = true)]
            public ulong unwkon23;
            [ProtoMember(61, IsRequired = true)]
            public ulong unwkon24;
            [ProtoMember(62, IsRequired = true)]
            public ulong unwkon25;
            [ProtoMember(63, IsRequired = true)]
            public ulong _Boss;
            [ProtoMember(64, IsRequired = true)]
            public ulong _HeadSoul;
            [ProtoMember(65, IsRequired = true)]
            public ulong _ArmorSoul;
            [ProtoMember(66, IsRequired = true)]
            public ulong _LeftWeaponSoul;
            [ProtoMember(67, IsRequired = true)]
            public ulong _RightWeaponSoul;
            [ProtoMember(68, IsRequired = true)]
            public ulong unwkon27;
            [ProtoMember(69, IsRequired = true)]
            public ulong unwkon28;
            [ProtoMember(70, IsRequired = true)]
            public ulong _FirstRebornClass;
            [ProtoMember(71, IsRequired = true)]
            public ulong _SecondRebornClass;
            [ProtoMember(72, IsRequired = true)]
            public ulong _Class;
            [ProtoMember(73, IsRequired = true)]
            public ulong _CountryCode;
            [ProtoMember(74, IsRequired = true)]
            public ulong unwkon30;
            [ProtoMember(75, IsRequired = true)]
            public ulong _BattlePower;
            [ProtoMember(76, IsRequired = true)]
            public ulong _JiangHuTalen;
            [ProtoMember(77, IsRequired = true)]
            public ulong _JiangHuActive;
            [ProtoMember(78, IsRequired = true)]
            public ulong _unkown36;
            [ProtoMember(79, IsRequired = true)]
            public ulong _ServerID;
            [ProtoMember(80, IsRequired = true)]
            public ulong _MainUID;
            [ProtoMember(81, IsRequired = true)]
            public ulong _OwnerUID;
            [ProtoMember(82, IsRequired = true)]
            public ulong unwkon34;
            [ProtoMember(83, IsRequired = true)]
            public ulong _ShadowOwner;
            [ProtoMember(84, IsRequired = true)]
            public ulong _UnionID;//uid
            [ProtoMember(85, IsRequired = true)]
            public uint ExploitsRank;
            [ProtoMember(86, IsRequired = true)]
            public ulong _OfficialRank;
            [ProtoMember(87, IsRequired = true)]
            public ushort Offical_Harem_Guards;
            [ProtoMember(88, IsRequired = true)]
            public byte _UnionType;
            [ProtoMember(89, IsRequired = true)]
            public int _MyTitle;
            [ProtoMember(90, IsRequired = true)]
            public int _MyTitleScore;
            [ProtoMember(91, IsRequired = true)]
            public int _MyWing;
            [ProtoMember(92, IsRequired = true)]
            public ulong _Windwalker;
            [ProtoMember(93, IsRequired = true)]
            public ulong unwkon45;
            [ProtoMember(94, IsRequired = true)]
            public ulong unwkon46;
            [ProtoMember(95, IsRequired = true)]
            public ulong unwkon47;
            [ProtoMember(96, IsRequired = true)]
            public ulong RelicPower;
            [ProtoMember(97, IsRequired = true)]
            public List<byte[]> Names = new List<byte[]>() { new byte[0], new byte[0], new byte[0], new byte[0], new byte[0], new byte[0], new byte[0] };
        }

        public CMsgPlayer CharBuffer = new CMsgPlayer();
        public StatusFlagsBigVector32 BitVector = new StatusFlagsBigVector32(32 * 7);
        #endregion
        #region Runes & Relic
        //Int variables
        public int
            InfinityDamage,
            IronShieldTime,           
            TrueDmg,
            SacrificePercent,
            BossKiller,
            NoMercyDamage,
             BloodSpawn,
             Grabber,
            WitcheryDamage;
        //UInt variables
        public uint
            
            DrainingTouch,
            FreeSoul,
            IronShieldDefence,
            FineRainHitPoints,
            FineRainTime,
            Healer,
            MPMaster,
            ImmunityRate,
            SlayerDamage;
        //Short variables
        public short
            XPKiller,
            XPBooster,
            XPBoosterTimes;
        //Byte variables
        public byte IronShieldLevel;
        //Double variables
        public double TortoiseBreaker;
        //Time32 variables
        public Time32
            HealerStamp,
            FineRainStamp,
            RampageStamp;
        //DateTime variables
        public DateTime IronShieldStamp;
        public void IronShieldPacket()
        {
            var Remain = IronShieldStamp.AddSeconds(IronShieldTime) - DateTime.Now;
            FlagsUpdate aupgrade = new FlagsUpdate(true);
            aupgrade.UID = UID;
            aupgrade.Append(49, 217, (uint)Remain.TotalSeconds, IronShieldDefence, IronShieldLevel);
            Owner.Send(aupgrade);
        }
        public void AddXP(short Time)
        {
            if (ContainsFlag((ulong)FlagsUpdate.Flags.Cyclone))
                UpdateXP((ulong)FlagsUpdate.Flags.Cyclone, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.SuperCyclone))
                UpdateXP((ulong)FlagsUpdate.Flags.SuperCyclone, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                UpdateXP((ulong)FlagsUpdate.Flags.Superman, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.ManiacDance))
                UpdateXP((ulong)FlagsUpdate.Flags.ManiacDance, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.BladeFlurry))
                UpdateXP((ulong)FlagsUpdate.Flags.BladeFlurry, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.FatalStrike))
                UpdateXP((ulong)FlagsUpdate.Flags.FatalStrike, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex))
                UpdateXP((ulong)FlagsUpdate.Flags.ShurikenVortex, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.Oblivion))
                UpdateXP((ulong)FlagsUpdate.Flags.Oblivion, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.DragonCyclone))
                UpdateXP((ulong)FlagsUpdate.Flags.DragonCyclone, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.ChainBoltActive))
                UpdateXP((ulong)FlagsUpdate.Flags.ChainBoltActive, Time, true);
            if (ContainsFlag((ulong)FlagsUpdate.Flags.Omnipotence))
                UpdateXP((ulong)FlagsUpdate.Flags.Omnipotence, Time, true);
        }
        public void RemoveXP(short Time)
        {
            if (Time == 0)
            {
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Cyclone))
                    RemoveFlag((ulong)FlagsUpdate.Flags.Cyclone);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.SuperCyclone))
                    RemoveFlag((ulong)FlagsUpdate.Flags.SuperCyclone);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                    RemoveFlag((ulong)FlagsUpdate.Flags.Superman);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.ManiacDance))
                    RemoveFlag((ulong)FlagsUpdate.Flags.ManiacDance);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.BladeFlurry))
                    RemoveFlag((ulong)FlagsUpdate.Flags.BladeFlurry);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.FatalStrike))
                    RemoveFlag((ulong)FlagsUpdate.Flags.FatalStrike);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex))
                    RemoveFlag((ulong)FlagsUpdate.Flags.ShurikenVortex);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Oblivion))
                    RemoveFlag((ulong)FlagsUpdate.Flags.Oblivion);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.DragonCyclone))
                    RemoveFlag((ulong)FlagsUpdate.Flags.DragonCyclone);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.ChainBoltActive))
                    RemoveFlag((ulong)FlagsUpdate.Flags.ChainBoltActive);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Omnipotence))
                    RemoveFlag((ulong)FlagsUpdate.Flags.Omnipotence);
            }
            else
            {
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Cyclone))
                    UpdateXP((ulong)FlagsUpdate.Flags.Cyclone, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.SuperCyclone))
                    UpdateXP((ulong)FlagsUpdate.Flags.SuperCyclone, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                    UpdateXP((ulong)FlagsUpdate.Flags.Superman, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.ManiacDance))
                    UpdateXP((ulong)FlagsUpdate.Flags.ManiacDance, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.BladeFlurry))
                    UpdateXP((ulong)FlagsUpdate.Flags.BladeFlurry, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.FatalStrike))
                    UpdateXP((ulong)FlagsUpdate.Flags.FatalStrike, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex))
                    UpdateXP((ulong)FlagsUpdate.Flags.ShurikenVortex, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Oblivion))
                    UpdateXP((ulong)FlagsUpdate.Flags.Oblivion, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.DragonCyclone))
                    UpdateXP((ulong)FlagsUpdate.Flags.DragonCyclone, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.ChainBoltActive))
                    UpdateXP((ulong)FlagsUpdate.Flags.ChainBoltActive, Time, false);
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Omnipotence))
                    UpdateXP((ulong)FlagsUpdate.Flags.Omnipotence, Time, false);
            }
        }
        #endregion
        #region Taoist Epic
        public GameState FlameOwner;
        internal TeamQualifier.QualifierList.QualifierGroup FlameMatch;
        public void BringToLifeLotus()
        {
            Hitpoints = MaxHitpoints;
            TransformationID = 0;
            Stamina = 100;
            RemoveFlag((ulong)FlagsUpdate.Flags.FlashingName);
            RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
            RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
            AddFlag((ulong)FlagsUpdate.Flags.Stigma, 80, true);
            StigmaStamp = Time32.Now;
            ShieldTime = 0;
            ShieldStamp = Time32.Now;
            MagicShieldStamp = Time32.Now;
            MagicShieldTime = 0;
            AddFlag((ulong)FlagsUpdate.Flags.MagicShield, 80, true);
            if (EntityFlag == EntityFlag.Player)
                SendCurrentMapStatus();
            if (EntityFlag == Game.EntityFlag.Player)
            {
                Owner.ReviveStamp = Time32.Now;
                Owner.Entity.AutoRev = 0;
                Owner.Attackable = false;
            }

        }
        public bool Encadenado()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle);
        }
        public DateTime LastGetEnergy { get; set; }
        public bool EpicTaoist()
        {
            if (EntityFlag == Game.EntityFlag.Player)
            {
                var weapons = Owner.Weapons;
                if (weapons.Item1 != null)
                    if (weapons.Item1.ID / 1000 == 620)
                        return true;
            }
            return false;
        }
        int _Energy;
        public int Energy
        {
            get { return _Energy; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                {
                    if (DataHolder.IsWater(this.Class) && EpicTaoist())
                    {
                        RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.FlameLotus);
                        if (value > 220)
                            value = 220;
                        FlagsUpdate upgrade = new FlagsUpdate(true);
                        upgrade.UID = UID;
                        upgrade.Append(78, 172, 5, (uint)value, 0);
                        this.Owner.SendScreen(upgrade.ToArray(), true);
                        if (value >= 200)
                        {
                            if (!ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.AuroraLotus))
                            {
                                AddFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.AuroraLotus, StatusFlagsBigVector32.PermanentFlag,true);
                            }
                        }
                    }
                    else if (DataHolder.IsFire(this.Class) && EpicTaoist())
                    {
                        RemoveFlag((ulong)FlagsUpdate.Flags.AuroraLotus);
                        if (value > 320)
                            value = 320;
                        FlagsUpdate upgrade = new FlagsUpdate(true);
                        upgrade.UID = UID;
                        upgrade.Append(78, 173, 5, (uint)value, 0);
                        this.Owner.SendScreen(upgrade.ToArray(), true);
                        if (value >= 300)
                        {
                            if (!ContainsFlag((ulong)FlagsUpdate.Flags.FlameLotus))
                            {
                                AddFlag((ulong)FlagsUpdate.Flags.FlameLotus, StatusFlagsBigVector32.PermanentFlag, true);
                            }
                        }
                    }
                    else
                    {
                        RemoveFlag((ulong)FlagsUpdate.Flags.AuroraLotus);
                        RemoveFlag((ulong)FlagsUpdate.Flags.FlameLotus);
                    }
                }
                _Energy = value;
            }
        }
        #endregion
        #region Shadow Clone
        public bool IAmShadow = false, IAmAI = false;
        public Dictionary<uint, Entity> MyClones = new Dictionary<uint, Entity>();
        public static void AddClone(GameState client, string Name, ushort cloneid)
        {
            if(client==null || client.ItemSyncRoot==null)return;
            lock(client.ItemSyncRoot) {
            if(!Features.QuestInventory.Player(client) || !client.Spells.ContainsKey(12090) || client.Entity.MyClones.Count>=2)return;
            var Entity = new Entity(EntityFlag.Monster, true);
            Entity.Owner = client;
            MonsterInformation template;
            if(!MonsterInformation.MonsterInformations.TryGetValue(9003,out template))throw new InvalidOperationException("Shadow Clone template missing.");
            Entity.MonsterInfo = template.Copy();
            Entity.MonsterInfo.Owner = Entity;
            Entity.IAmShadow = true;
            Entity.ClanName = Name;
            Entity.Name = Name;
            Entity.HairStyle = client.Entity.HairStyle;
            Entity.HairColor = client.Entity.HairColor;
            Entity.CharBuffer._BattlePower = client.Entity.AssassinBP;
            Entity.Action = client.Entity.Action;
            #region Equip same as me
            foreach (ConquerItem item in client.Equipment.Objects)
            {
                if (item == null) continue;
                if (client.Equipment.Free(item.Position)) continue;
                if (!item.IsWorn) continue;
                switch (item.Position)
                {
                    case ConquerItem.AlternateHead:
                    case ConquerItem.Head:
                        if (item.Purification.Available)
                            Entity.CharBuffer._HeadSoul = item.Purification.PurificationItemID;
                        else Entity.CharBuffer._HeadSoul = 0;
                        Entity.CharBuffer._Head = item.ID;
                        Entity.CharBuffer._HeadColor = (ushort)item.Color;
                        break;
                    case ConquerItem.AlternateGarment:
                    case ConquerItem.Garment:
                        Entity.CharBuffer._Garment = item.ID;
                        break;
                    case ConquerItem.AlternateArmor:
                    case ConquerItem.Armor:

                        if (item.Purification.Available)
                            Entity.CharBuffer._ArmorSoul = item.Purification.PurificationItemID;
                        else Entity.CharBuffer._ArmorSoul = 0;
                        Entity.CharBuffer._Armor = item.ID;
                        Entity.CharBuffer._ArmorColor = (byte)item.Color;
                        break;
                    case ConquerItem.AlternateRightWeapon:
                    case ConquerItem.RightWeapon:
                        if (item.Purification.Available)
                            Entity.CharBuffer._RightWeaponSoul = item.Purification.PurificationItemID;
                        else
                            Entity.CharBuffer._RightWeaponSoul = 0;
                        Entity.CharBuffer._RightWeapon = item.ID;
                        if (ItemHandler.IsTwoHand(item.ID))
                        {
                            Entity.CharBuffer._LeftWeaponSoul = 0;
                            Entity.CharBuffer._LeftWeapon = 0;
                            Entity.CharBuffer._LeftWeaponColor = 0;
                        }
                        break;
                    case ConquerItem.RightWeaponAccessory:
                        Entity.CharBuffer._RightWeaponAccessory = item.ID;
                        break;
                    case ConquerItem.AlternateLeftWeapon:
                    case ConquerItem.LeftWeapon:
                        if (item.Purification.Available)
                            Entity.CharBuffer._LeftWeaponSoul = item.Purification.PurificationItemID;
                        else
                            Entity.CharBuffer._LeftWeaponSoul = 0;
                        Entity.CharBuffer._LeftWeapon = item.ID;
                        Entity.CharBuffer._LeftWeaponColor = (byte)item.Color;
                        break;
                    case ConquerItem.LeftWeaponAccessory:
                        Entity.CharBuffer._LeftWeaponAccessory = item.ID;
                        break;
                    case ConquerItem.Steed:
                        Entity.CharBuffer._Steed = item.ID;
                        Entity.CharBuffer._SteedPlus = item.Plus;
                        break;
                    case ConquerItem.SteedArmor:
                        Entity.CharBuffer._MountArmor = item.ID;
                        break;
                }
            }
            #endregion Equip same as me 
            Entity.MinAttack = 1;
            Entity.MaxAttack = Entity.MagicAttack = 1;
            Entity.Hitpoints = Entity.MaxHitpoints = 1;
            Entity.Body = client.Entity.Body;
            Entity.UID = 703400 + Program.ShadowCloneCounter.Next;
            Entity.ShadowID = client.Entity.UID;
            Entity.CharBuffer._OwnerUID = 2;
            Entity.CharBuffer.unwkon34 = cloneid;
            Entity.MapID = client.Entity.MapID;
            Entity.SendUpdates = true;
            Entity.X = client.Entity.X;
            Entity.Y = client.Entity.Y;
            Entity.Companion = true;
            client.Map.AddEntity(Entity);
            client.Entity.MyClones.Add(Entity.UID, Entity);
            client.SendScreenSpawn(Entity, true);
            }
        }
        public uint ShadowID
        {
            get
            {
                return (uint)CharBuffer._ShadowOwner;
            }
            set
            {
                CharBuffer._ShadowOwner = value;
            }
        }
        #endregion
        public bool winnerctf = false;
        public bool ctf = false;
        public uint StrikePoints
        {
            get
            {
                return Owner["StrikePoints"];
            }
            set
            {
                Owner["StrikePoints"] = value;
            }
        }
        #region Bomb and Chain Repair
        public bool EpicMonje()
        {
            if (EntityFlag == Game.EntityFlag.Player)
            {
                var weapons = Owner.Weapons;
                if (weapons.Item1 != null)
                    if (weapons.Item1.ID / 1000 == 622)
                        return true;
                    else if (weapons.Item2 != null)
                        if (weapons.Item2.ID / 1000 == 622)
                            return true;
            }
            return false;
        }
        public Time32 SoulShackleStamp, GaleBombStamp, TwilightDance, Tranquility, SSyFBS;
        #endregion
        #region CP Bank
        public ulong BankCps;
        ulong _CpsBank;
        public ulong CpsBank
        {
            get
            {
                return _CpsBank;
            }
            set
            {
                value = Math.Max(0, value);
                _CpsBank = value;
                Database.EntityTable.UpdateBankCps(this.Owner);

            }
        }
        #endregion
        #region Cup Quest

        uint _TareaCopa;
        public uint TareaCopa
        {
            get
            {
                return _TareaCopa;
            }
            set
            {
                value = Math.Max(0, value);
                _TareaCopa = value;
                Database.EntityTable.UpdateTareaCopa(this.Owner);

            }
        }

        public int JadeDragonPendant = 0;
        public int SupremeJade = 0;
        #endregion
        #region WindWalker
        public bool IsStomper2()
        {
            if (EntityFlag == Game.EntityFlag.Player)
            {
                var weapons = Owner.Weapons;
                if (weapons.Item2 != null)
                    if (weapons.Item2.ID / 1000 == 626)
                        if (this.Class >= 160 && this.Class <= 165)
                            if (this.Windwalker == 7)
                                return true;
            }
            return false;
        }
        public Time32 JusticeChant, HealingSnow, GreenEffectStamp;
        public int HealingRate;
        public WindWalker.ThunderCloud Cloud;
        bool _thbolt;
        public bool ThunderBolt
        {
            get
            {
                return _thbolt;
            }
            set
            {
                if (Cloud != null)
                    Cloud.ThunderBolt = value;
                _thbolt = value;
            }
        }
        public ushort JusticeRateSkill = 0, GreenEffect = 0;
        public String Branch = "";
        private byte _ChangeBranch;
        public byte ChangeBranch
        {
            get
            {
                return _ChangeBranch;
            }
            set
            {
                _ChangeBranch = value;
            }
        }
        public bool SwirlingStorm;
        public Time32 TripleBeastStamp;
        public int SwirlingStormPercent;
        public byte _Windwalker;
        public byte Windwalker
        {
            get
            {

                return _Windwalker;
            }
            set
            {
                _Windwalker = value;
                CharBuffer._Windwalker = value;
                if (Owner != null)
                    foreach (var player in Owner.Screen.Objects.Where(p => p.MapObjType == MapObjectType.Player))
                        if (player != null)
                            if ((player as Entity) != null)
                                (player as Entity).Owner.Send(CharBuffer.ToConquerPacket(10014));
            }
        }
        public byte ChillingSnow = 0, FreezingPelter = 0;
        public Time32 RevengeTailStamp, HealingSnowStamp, RevengeTaillStamp;
        public Time32 OmnipotenceStamp;
        public Time32 JusticeChantStamp;
        public short OmnipotenceTime = 0;
        public short RevengeTailTime = 0;
        public Time32 ThunderCloudStamp, ChillingSnowStamp, FreezingPelterStamp;
        public Time64 SwirlingStamp;
        public Time32 TCSpellUse;
        #endregion
        #region Epic Warrior
        #region HeroCodes
        public Time32 BackFireStamp;
        public bool OnManiacDance()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.ManiacDance);
        }
        public bool OnBackFire()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.BackFire);
        }
        public bool Taoist()
        {
            if (EntityFlag == Game.EntityFlag.Player)
            {
                var weapons = Owner.Weapons;
                if (weapons.Item1 != null)
                    if (weapons.Item1.ID / 1000 == 421)
                        return true;

            }
            return false;
        }
        public Time32 BackfireStamp;
        #region Clicker Variables
        public byte FlowersGiftsTime;
        public bool OnCheckBox { get; set; }

        public static int RelasesBox { get; set; }

        public int CheckBocSec { get; set; }
        public uint PuntosUnion { get; set; }

        public bool OnUnionWar = false;
        public Time32 CheckBoxStamp { get; set; }
        public bool RevivirAuto = false;
        public DateTime PledgeStamp;
        public Time32 KineticSparkStamp;
        public int SparkTime;
        public int BackFireTime;
        public bool AzuredShield;
        public Time32 WaveOfBloodStamp;
        public int Coo;
        public byte GLCTF;
        public uint WarScore;
        internal void SetHP(int p)
        {
            throw new NotImplementedException();
        }
        #endregion
        #endregion    
        public bool Backfire = false;
        public bool EpicWarrior()
        {
            if (EntityFlag == Game.EntityFlag.Player)
            {
                var weapons = Owner.Weapons;
                if (weapons.Item1 != null)
                    if (weapons.Item1.ID / 1000 == 624)
                        return true;
                    else if (weapons.Item2 != null)
                        if (weapons.Item2.ID / 1000 == 624)
                            return true;
            }
            return false;
        }
        public int RupturaDeFuerzaTime;
        public DateTime RDFT;
        public Time32 XpBlueStamp;
        public Time32 WarriorEpicShield;
        public bool RupturaDeFuerza;
        public bool Hundimiento;
        public unsafe Attack ManiacDancePacket;
        public Time32 ManiacDanceStamp;
        public Time32 ManiacDancePulseStamp;
        public int ManiacDanceTime;
        public byte XPCountTwist = 0;
        public int AtaqueContrario;
        public Time32 RupturaDeFuerzaStamp, AtaqueContrarioStamp;
        #endregion
        #region Union System
        private string _unionname = "";
        public string UnionName
        {
            get { return _unionname; }
            set
            {
                _unionname = value;
                CharBuffer.Names[6] = System.Text.Encoding.Default.GetBytes(value);
            }
        }
        #endregion
        #region Perfection
        /*   public PerfectionManager.RefineEffect Perfection
        {
            get
            {
                if (Owner == null || Owner.Equipment == null) return null;
                var tpl = Owner.Equipment.GetTPL();
                return tpl > 0 ? PerfectionManager.ItemsRefineEffects[tpl] : null;
            }
        }*/
        #region Perfection
        public Time32 ShieldBreak;
        public int BlockBreak;
        public ushort GuardDefense;
        public Time32 GuardDefenseStamp;
        public int Prestige;
        public PerfectionManager.RefineEffect Perfection
        {
            get
            {
                if (Owner == null || Owner.Equipment == null) return null;
                var tpl = Owner.Equipment.GetTPL();
                PerfectionManager.RefineEffect effect;
                if (tpl <= 0 || !PerfectionManager.ItemsRefineEffects.TryGetValue(tpl, out effect)) return null;
                // Rune boosters belong to this character; never mutate the shared level table.
                return DrainingTouch == 0 && BloodSpawn == 0 && FreeSoul == 0 ? effect
                    : effect.WithRuneBonuses((int)DrainingTouch, BloodSpawn, (int)FreeSoul);
            }
        }
        #endregion
        #endregion
        #region Taoist Epics
        public Time32 LotusEnergyStamp;
        public Time32 lianhuaranStamp;
        public float lianhuaranPercent;
        public int lianhuaranLeft;
        #endregion
        #region Death Count
        public uint Asesinatos = 0;
        public uint Muertes = 0;
        public uint ArenaHit = 0;
        #endregion
        #region Max Deaths
        public ushort MaximasMuertesRecord;
        public string NewName = "";
        public void InsertXorNameToDB(string oldName, string newName)
        {
            new Database.MySqlCommand(Database.MySqlCommandType.UPDATE).Update("entities").Set(oldName, newName).Where("UID", UID).Execute();
        }
        #endregion
        #region AuroraLotus
        public bool Lotus(uint LotusEnergy, uint aura = FlagsUpdate.AuroraLotus)
        {
            if (Owner.Weapons != null)
            {
                if (Owner.Weapons.Item1 != null)
                {
                    if (Owner.Weapons.Item1.ID / 1000 != 620)
                        return false;
                    FlagsUpdate upgrade = new FlagsUpdate(true);
                    upgrade.UID = UID;
                    upgrade.Append(FlagsUpdate.Lotus, aura, 5, LotusEnergy, 0);
                    Owner.SendScreen(upgrade);
                    return true;
                }
            }
            return false;
        }
        #endregion
        #region CP Wager
        public Time32 EsperaPelea;
        public Time32 WinnerWaiting;
        public bool UnGanador = false;
        #endregion
        #region Pets
        public Time32 TiempoMascotas;
        public Pet.PetType pettype;
        #endregion
        #region Death Effect
        public void DieString()
        {
            _String str = new _String(true);
            str.UID = this.UID;
            str.Type = _String.Effect;
            str.Texts.Add("ghost");
            str.Texts.Add("1ghost");
            str.TextsCount = 1;
            if (EntityFlag == Game.EntityFlag.Player)
            {
                this.SendScreen(str);
            }
        }
        #endregion
        #region Referrals
        #region OnlinePoints
        #region 

        uint _OnlinePoints;
        public uint OnlinePoints
        {
            get
            {
                return _OnlinePoints;
            }
            set
            {
                value = Math.Max(0, value);
                _OnlinePoints = value;
                Database.EntityTable.UpdateOnlinePoints(this.Owner);

            }
        }
        #endregion
        public Time32 OnlinePointStamp;
        #endregion
        public uint _NumReferidos, _NumReferidosTotal;
        public uint NumReferidos
        {
            get
            {
                return _NumReferidos;
            }
            set
            {
                if (value <= 0)
                    value = 0;
                _NumReferidos = value;
                Database.EntityTable.UpdateReferidos(this.Owner);
            }
        }
        public uint NumReferidosTotal
        {
            get
            {
                return _NumReferidosTotal;
            }
            set
            {
                if (value <= 0)
                    value = 0;
                _NumReferidosTotal = value;
                Database.EntityTable.UpdateReferidos(this.Owner);
            }
        }
        #endregion
        #region Variables Jheisson
        #region StorageItems
        public Dictionary<uint, ConquerItem> StorageItems;
        public WardrobeTitles WTitles;
        public int TitlePoints;
        public int EquippedTitle
        {
            get
            {
                return CharBuffer._MyTitle;
            }
            set
            {
                CharBuffer._MyTitle = value;
            }
        }
        public int UTitlePoints
        {
            get
            {
                return WTitles != null ? WTitles.Points : CharBuffer._MyTitleScore;
            }
            set
            {

                CharBuffer._MyTitleScore = Math.Max(0,value);
                Update(89, (uint)Math.Max(0,value), false);
            }
        }
        public int EquippedWing
        {
            get
            {
                return CharBuffer._MyWing;
            }
            set
            {
                CharBuffer._MyWing = value;

            }
        }
        #endregion
        public bool WrathoftheEmperor;
        public DateTime WrathoftheEmperorStamp;
        public Boolean IsGreen(Entity Entity) { return (Entity.Level - Level) >= 3; }
        public Boolean IsWhite(Entity Entity) { return (Entity.Level - Level) >= 0 && (Entity.Level - Level) < 3; }
        public Boolean IsRed(Entity Entity) { return (Entity.Level - Level) >= -4 && (Entity.Level - Level) < 0; }
        public Boolean IsBlack(Entity Entity) { return (Entity.Level - Level) < -4; }
        public bool IsShieldBlock;
        public ushort ShieldBlockPercent;
        #region Fan/Tower/Wings Acessor
        public int getTowerWings(bool Magic)
        {
            if (Owner.Equipment.Free(19))
                return 0;

            ushort magic = 0;
            ushort physical = 0;
            ushort gemVal = 0;

            #region Get
            ConquerItem Item = this.Owner.Equipment.TryGetItem(19);

            if (Item != null)
            {
                if (Item.ID > 0)
                {
                    switch (Item.ID % 10)
                    {
                        case 3:
                        case 4:
                        case 5: physical += 100; magic += 50; break;
                        case 6: physical += 200; magic += 150; break;
                        case 7: physical += 400; magic += 250; break;
                        case 8: physical += 600; magic += 350; break;
                        case 9: physical += 900; magic += 450; break;
                    }

                    switch (Item.Plus)
                    {
                        case 0: break;
                        case 1: physical += 50; magic += 50; break;
                        case 2: physical += 100; magic += 100; break;
                        case 3: physical += 200; magic += 150; break;
                        case 4: physical += 300; magic += 200; break;
                        case 5: physical += 400; magic += 250; break;
                        case 6: physical += 500; magic += 300; break;
                        case 7: physical += 600; magic += 350; break;
                        case 8: physical += 700; magic += 400; break;
                        case 9: physical += 800; magic += 450; break;
                        case 10: physical += 900; magic += 500; break;
                        case 11: physical += 1100; magic += 550; break;
                        case 12: physical += 1400; magic += 600; break;
                    }
                    switch (Item.SocketOne)
                    {
                        case Enums.Gem.NormalGloryGem: gemVal += 100; break;
                        case Enums.Gem.RefinedGloryGem: gemVal += 300; break;
                        case Enums.Gem.SuperGloryGem: gemVal += 500; break;
                    }
                    switch (Item.SocketTwo)
                    {
                        case Enums.Gem.NormalGloryGem: gemVal += 100; break;
                        case Enums.Gem.RefinedGloryGem: gemVal += 300; break;
                        case Enums.Gem.SuperGloryGem: gemVal += 500; break;
                    }
                }
            }
            #endregion

            physical = Math.Max(physical, PhysicalDamageDecrease);
            magic = Math.Max(magic, MagicDamageDecrease);
            magic += gemVal;
            physical += gemVal;

            if (Magic)
                return (int)magic;
            else
                return (int)physical;
        }
        public int getFanWings(bool Magic)
        {
            if (Owner.Equipment.Free(19))
                return 0;

            ushort magic = 0;
            ushort physical = 0;
            ushort gemVal = 0;

            #region Get
            ConquerItem Item = this.Owner.Equipment.TryGetItem(19);

            if (Item != null)
            {
                if (Item.ID > 0)
                {
                    switch (Item.ID % 10)
                    {
                        case 3:
                        case 4:
                        case 5: physical += 200; magic += 100; break;
                        case 6: physical += 300; magic += 200; break;
                        case 7: physical += 500; magic += 300; break;
                        case 8: physical += 700; magic += 400; break;
                        case 9: physical += 1000; magic += 550; break;
                    }

                    switch (Item.Plus)
                    {
                        case 0: break;
                        case 1: physical += 100; magic += 100; break;
                        case 2: physical += 200; magic += 150; break;
                        case 3: physical += 300; magic += 200; break;
                        case 4: physical += 400; magic += 250; break;
                        case 5: physical += 500; magic += 300; break;
                        case 6: physical += 600; magic += 350; break;
                        case 7: physical += 700; magic += 400; break;
                        case 8: physical += 800; magic += 450; break;
                        case 9: physical += 900; magic += 500; break;
                        case 10: physical += 1000; magic += 550; break;
                        case 11: physical += 1200; magic += 600; break;
                        case 12: physical += 1500; magic += 700; break;
                    }
                    switch (Item.SocketOne)
                    {
                        case Enums.Gem.NormalThunderGem: gemVal += 100; break;
                        case Enums.Gem.RefinedThunderGem: gemVal += 300; break;
                        case Enums.Gem.SuperThunderGem: gemVal += 500; break;
                    }
                    switch (Item.SocketTwo)
                    {
                        case Enums.Gem.NormalThunderGem: gemVal += 100; break;
                        case Enums.Gem.RefinedThunderGem: gemVal += 300; break;
                        case Enums.Gem.SuperThunderGem: gemVal += 500; break;
                    }
                }
            }
            #endregion
            physical = Math.Max(physical, PhysicalDamageIncrease);
            magic = Math.Max(magic, MagicDamageIncrease);
            magic += gemVal;
            physical += gemVal;

            if (Magic)
                return (int)magic;
            else
                return (int)physical;
        }
        public int getFan(bool Magic)
        {
            if (Owner.Equipment.Free(10))
                return 0;

            ushort magic = 0;
            ushort physical = 0;
            ushort gemVal = 0;

            #region Get
            ConquerItem Item = this.Owner.Equipment.TryGetItem(10);

            if (Item != null)
            {
                if (Item.ID > 0)
                {
                    switch (Item.ID % 10)
                    {
                        case 3:
                        case 4:
                        case 5: physical += 300; magic += 150; break;
                        case 6: physical += 500; magic += 200; break;
                        case 7: physical += 700; magic += 300; break;
                        case 8: physical += 900; magic += 450; break;
                        case 9: physical += 1200; magic += 750; break;
                    }

                    switch (Item.Plus)
                    {
                        case 0: break;
                        case 1: physical += 200; magic += 100; break;
                        case 2: physical += 400; magic += 200; break;
                        case 3: physical += 600; magic += 300; break;
                        case 4: physical += 800; magic += 400; break;
                        case 5: physical += 1000; magic += 500; break;
                        case 6: physical += 1200; magic += 600; break;
                        case 7: physical += 1300; magic += 700; break;
                        case 8: physical += 1400; magic += 800; break;
                        case 9: physical += 1500; magic += 900; break;
                        case 10: physical += 1600; magic += 950; break;
                        case 11: physical += 1700; magic += 1000; break;
                        case 12: physical += 1800; magic += 1050; break;
                    }
                    switch (Item.SocketOne)
                    {
                        case Enums.Gem.NormalThunderGem: gemVal += 100; break;
                        case Enums.Gem.RefinedThunderGem: gemVal += 300; break;
                        case Enums.Gem.SuperThunderGem: gemVal += 500; break;
                    }
                    switch (Item.SocketTwo)
                    {
                        case Enums.Gem.NormalThunderGem: gemVal += 100; break;
                        case Enums.Gem.RefinedThunderGem: gemVal += 300; break;
                        case Enums.Gem.SuperThunderGem: gemVal += 500; break;
                    }
                }
            }
            #endregion
            physical = Math.Max(physical, PhysicalDamageIncrease);
            magic = Math.Max(magic, MagicDamageIncrease);
            magic += gemVal;
            physical += gemVal;

            if (Magic)
                return (int)magic;
            else
                return (int)physical;
        }
        public int getTower(bool Magic)
        {
            if (Owner.Equipment.Free(11))
                return 0;

            ushort magic = 0;
            ushort physical = 0;
            ushort gemVal = 0;

            #region Get
            ConquerItem Item = this.Owner.Equipment.TryGetItem(11);

            if (Item != null)
            {
                if (Item.ID > 0)
                {
                    switch (Item.ID % 10)
                    {
                        case 3:
                        case 4:
                        case 5: physical += 250; magic += 100; break;
                        case 6: physical += 400; magic += 150; break;
                        case 7: physical += 550; magic += 200; break;
                        case 8: physical += 700; magic += 300; break;
                        case 9: physical += 1100; magic += 600; break;
                    }

                    switch (Item.Plus)
                    {
                        case 0: break;
                        case 1: physical += 150; magic += 50; break;
                        case 2: physical += 350; magic += 150; break;
                        case 3: physical += 550; magic += 250; break;
                        case 4: physical += 750; magic += 350; break;
                        case 5: physical += 950; magic += 450; break;
                        case 6: physical += 1100; magic += 550; break;
                        case 7: physical += 1200; magic += 625; break;
                        case 8: physical += 1300; magic += 700; break;
                        case 9: physical += 1400; magic += 750; break;
                        case 10: physical += 1500; magic += 800; break;
                        case 11: physical += 1600; magic += 850; break;
                        case 12: physical += 1700; magic += 900; break;
                    }
                    switch (Item.SocketOne)
                    {
                        case Enums.Gem.NormalGloryGem: gemVal += 100; break;
                        case Enums.Gem.RefinedGloryGem: gemVal += 300; break;
                        case Enums.Gem.SuperGloryGem: gemVal += 500; break;
                    }
                    switch (Item.SocketTwo)
                    {
                        case Enums.Gem.NormalGloryGem: gemVal += 100; break;
                        case Enums.Gem.RefinedGloryGem: gemVal += 300; break;
                        case Enums.Gem.SuperGloryGem: gemVal += 500; break;
                    }
                }
            }
            #endregion

            physical = Math.Max(physical, PhysicalDamageDecrease);
            magic = Math.Max(magic, MagicDamageDecrease);
            magic += gemVal;
            physical += gemVal;

            if (Magic)
                return (int)magic;
            else
                return (int)physical;
        }

        #endregion
        public int AdjustDefense(int defense)
        {
            var addDefense = 0;


            if (ContainsFlag((ulong)FlagsUpdate.Flags.MagicShield))
            {
                addDefense += Math.Max(0, AdjustData((int)defense, 30)) - (int)defense;// shield defense
            }

            return (defense + (addDefense * defense / 100));
        }
        public int AdjustBowDefense(int defense)
        {
            return defense;
        }
        public int AdjustData(int data, int adjust, int maxData = 0)
        {
            return Revora.Game.Attacking.PvP.MathHelper.AdjustDataEx(data, adjust, maxData);
        }
        public int AdjustAttack(int attack)
        {
            var addAttack = 0;

            if (ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
            {
                addAttack += Math.Max(0, AdjustData((int)attack, 30)) - (int)attack;
            }
            return (attack + (addAttack * attack / 100));
        }
        #endregion
        #region Epic Quests
        public bool StartedEpicQuest = false;
        public bool FinishedFirstStage = false;
        public bool FinishedSecondStage = false;
        public bool DidntPassFirstStage = false;
        public bool DidntPassSecondStage = false;
        public bool DidntPassThirdStage = false;
        public bool GotTrojanScroll = false;
        public bool GotNinjaScroll = false;
        #endregion
        #region Variables
        public bool BlockShieldCheck = false;
        public Features.Flowers Flowers;
        public ConquerItem LotteryPrize;
        public uint LotteryItemID = 0, LotteryItemPlus, LotteryItemColor, LotteryItemSoc1, LastXLocation, LastYLocation;
        public bool UseItem = false;
        public uint LotteryItemSoc2;
        public byte LotteryJadeAdd;
        public int KillCount = 0, KillCount2 = 0;
        public int BlockShield, MagicDefender, DefensiveStance;
        public bool InSteedRace, Invisable, IsBlackSpotted, IsEagleEyeShooted = false;
        public Database.MonsterInformation MonsterInfo;
        public Time64 CloneAttackStamp;
        public Time32 FlameStamp,
        DeathStamp, BlockShieldStamp, ScurbyBombStamp, ScurbyBomb2Stamp,
        VortexAttackStamp, AttackStamp, StaminaStamp,
        CycloneStamp, SuperCycloneStamp, DragonCycloneStamp, SupermanStamp, AutoRevStamp,
        TwoFlod, CannonBarrageStamp, StigmaStamp, InvisibilityStamp, StarOfAccuracyStamp,
        MagicShieldStamp, DodgeStamp, EnlightmentStamp, BlackSpotStamp, BlackbeardsRageStamp,
        DefensiveStanceStamp, AccuracyStamp, ShieldStamp, FlyStamp, NoDrugsStamp, ToxicFogStamp,
        FatalStrikeStamp, TimeDropSlayerStamp, DoubleExpStamp, ProtectionStamp, BladeTempest, MagicDefenderStamp, FlagStamp,
        ShurikenVortexStamp, IntensifyStamp, TransformationStamp, CounterKillStamp, PKPointDecreaseStamp,
        LastPopUPCheck, HeavenBlessingStamp, DragonFlowStamp, DragonFuryStamp, DragonSwingStamp, OblivionStamp,
        BallonStamp, ShackleStamp, AzureStamp, StunStamp, IncreaseFinalMDamageStamp, IncreaseFinalPDamageStamp, IncreaseFinalMAttackStamp,
        IncreaseFinalPAttackStamp, IncreaseImunityStamp, IncreaseAntiBreackStamp, IncreasePStrikeStamp, IncreaseBreackStamp, GodlyShieldStamp,
        IncreaseAttributeStamp, WhilrwindKick, GuildRequest, Confuse, LastTeamLeaderLocationSent = Time32.Now, BladeFlurryStamp, EagleEyeStamp, Cursed,
        ShockStamp, FreezeStamp, spiritFocusStamp, CheckGuardStamp, CheckSaqueoStamp, MortalWoundStamp, ChainboltStamp, SpellStamp, LastExpSave, FrozenStamp, FlagStampCursed, DiceKingTime;
        public bool
        MagicDefenderOwner = false, InteractionInProgress = false,
        IsDropped = false, IsWatching = false, HasMagicDefender = false,
        IsDefensiveStance = false, KillTheTerrorist_IsTerrorist = false,
        SpawnProtection = false, Tournament_Signed = false, InteractionSet = false;
        public byte FreezeTime;
        public int Fright;
        public uint InteractionType = 0;
        public uint InteractionWith = 0;
        public ushort InteractionX = 0;
        public ushort InteractionY = 0;
        public int CurrentTreasureBoxes = 0;
        public uint Points = 0;
        public ushort Postion = 0;
        public ConcurrentDictionary<TitlePacket.Titles, DateTime> Titles = new ConcurrentDictionary<TitlePacket.Titles, DateTime>();
        public ConcurrentDictionary<int, DateTime> Halos;
        public bool IsWarTop(ulong Title)
        {
            return Title >= 11 && Title <= 23;
        }
        public UInt64 IntToTopStatus(UInt64 top)
        {
            switch (top)
            {
                case 1: return (ulong)FlagsUpdate.Flags.TopWaterTaoist;
                case 2: return (ulong)FlagsUpdate.Flags.TopWarrior;
                case 3: return (ulong)FlagsUpdate.Flags.TopTrojan;
                case 4: return (ulong)FlagsUpdate.Flags.TopArcher;
                case 5: return (ulong)FlagsUpdate.Flags.TopNinja;
                case 6: return (ulong)FlagsUpdate.Flags.TopFireTaoist;
                case 7: return (ulong)FlagsUpdate.Flags.TopMonk;
                case 8: return (ulong)FlagsUpdate.Flags.TopSpouse;
                case 9: return (ulong)FlagsUpdate.Flags.TopGuildLeader;
                case 10: return (ulong)FlagsUpdate.Flags.TopDeputyLeader;
                case 20: return (ulong)FlagsUpdate.Flags.MonthlyPKChampion;
                case 21: return (ulong)FlagsUpdate.Flags.WeeklyPKChampion;
                case 22: return (ulong)FlagsUpdate.Flags.TopPirate;
                case 23: return (ulong)FlagsUpdate.Flags.TopPirate2;
                case 24: return (ulong)FlagsUpdate.Flags.Top2Archer;
                case 25: return (ulong)FlagsUpdate.Flags.Top2Fire;
                case 26: return (ulong)FlagsUpdate.Flags.Top2Monk;
                case 27: return (ulong)FlagsUpdate.Flags.Top2Ninja;
                case 28: return (ulong)FlagsUpdate.Flags.Top2Trojan;
                case 29: return (ulong)FlagsUpdate.Flags.Top2Warrior;
                case 30: return (ulong)FlagsUpdate.Flags.Top2Water;
                case 31: return (ulong)FlagsUpdate.Flags.ConuqerSuperBlue;
                case 32: return (ulong)FlagsUpdate.Flags.ConuqerSuperYellow;
                case 33: return (ulong)FlagsUpdate.Flags.ConuqerSuperUnderBlue;
                case 34: return (ulong)FlagsUpdate.Flags.DragonWarriorTop;
            }
            return top;
        }
        public UInt64 TopStatusToInt(UInt64 top)
        {
            switch (top)
            {
                case (ulong)FlagsUpdate.Flags.TopWaterTaoist: return 1;
                case (ulong)FlagsUpdate.Flags.TopWarrior: return 2;
                case (ulong)FlagsUpdate.Flags.TopTrojan: return 3;
                case (ulong)FlagsUpdate.Flags.TopArcher: return 4;
                case (ulong)FlagsUpdate.Flags.TopNinja: return 5;
                case (ulong)FlagsUpdate.Flags.TopFireTaoist: return 6;
                case (ulong)FlagsUpdate.Flags.TopMonk: return 7;
                case (ulong)FlagsUpdate.Flags.TopSpouse: return 8;
                case (ulong)FlagsUpdate.Flags.TopGuildLeader: return 9;
                case (ulong)FlagsUpdate.Flags.TopDeputyLeader: return 10;
                case (ulong)FlagsUpdate.Flags.MonthlyPKChampion: return 20;
                case (ulong)FlagsUpdate.Flags.WeeklyPKChampion: return 21;
                case (ulong)FlagsUpdate.Flags.TopPirate: return 22;
                case (ulong)FlagsUpdate.Flags.TopPirate2: return 23;
                case (ulong)FlagsUpdate.Flags.Top2Archer: return 24;
                case (ulong)FlagsUpdate.Flags.Top2Fire: return 25;
                case (ulong)FlagsUpdate.Flags.Top2Monk: return 26;
                case (ulong)FlagsUpdate.Flags.Top2Ninja: return 27;
                case (ulong)FlagsUpdate.Flags.Top2Trojan: return 28;
                case (ulong)FlagsUpdate.Flags.Top2Warrior: return 29;
                case (ulong)FlagsUpdate.Flags.Top2Water: return 30;
                case (ulong)FlagsUpdate.Flags.ConuqerSuperBlue: return 31;
                case (ulong)FlagsUpdate.Flags.ConuqerSuperYellow: return 32;
                case (ulong)FlagsUpdate.Flags.ConuqerSuperUnderBlue: return 33;
                case (ulong)FlagsUpdate.Flags.DragonWarriorTop: return 34;
            }
            return top;
        }
        public void AddTopStatus(UInt64 Title, DateTime EndsOn, Boolean Db = true)
        {
            Title = TopStatusToInt(Title);
            Boolean HasFlag = false;
            if (IsWarTop(Title))
            {
                HasFlag = Titles.ContainsKey((TitlePacket.Titles)Title);
                Titles.TryAdd((TitlePacket.Titles)Title, EndsOn);
            }
            else
                AddFlag(IntToTopStatus(Title), StatusFlagsBigVector32.PermanentFlag, false);
            if (Db)
            {
                if (HasFlag)
                {
                    MySqlCommand cmd = new MySqlCommand(MySqlCommandType.UPDATE);
                    cmd.Update("status").Set("time", Kernel.ToDateTimeInt(EndsOn))
                        .Where("status", Title).And("entityid", (UInt32)UID);
                    cmd.Execute();
                }
                else
                {
                    MySqlCommand cmd = new MySqlCommand(MySqlCommandType.INSERT);
                    cmd.Insert("status").Insert("entityid", UID).Insert("status", Title).Insert("time", Kernel.ToDateTimeInt(EndsOn));
                    cmd.Execute();
                }
            }
        }
        public void RemoveTopStatus(UInt64 Title)
        {
            ulong baseFlag = TopStatusToInt(Title);
            MySqlCommand cmd = new MySqlCommand(MySqlCommandType.DELETE);
            cmd.Delete("status", "entityid", UID).And("status", baseFlag).Execute();
            RemoveFlag(Title);
        }
        public void LoadTopStatus()
        {
            using (MySqlCommand Command = new MySqlCommand(MySqlCommandType.SELECT))
            {
                Command.Select("status").Where("entityid", UID).Execute();
                using (MySqlReader Reader = new MySqlReader(Command))
                {
                    while (Reader.Read())
                    {
                        UInt64 Title = Reader.ReadUInt64("status");
                        if (Revora.Game.Features.LocalPkWar.LegacyHonor(Title)) continue;
                        DateTime Time = Kernel.FromDateTimeInt(Reader.ReadUInt64("time"));
                        if (DateTime.Now > Time)
                            RemoveTopStatus(Title);
                        else
                        {
                            AddTopStatus(Title, Time, false);
                        }
                    }
                }
            }
        }
        public UInt32 ActivePOPUP;
        public TitlePacket.Titles MyTitle
        {
            get { return (TitlePacket.Titles)CharBuffer._TitleActivated; }
            set
            {
                CharBuffer._TitleActivated = (Byte)value;
                if (FullyLoaded)
                {
                    MySqlCommand cmd = new MySqlCommand(MySqlCommandType.UPDATE);
                    cmd.Update("entities").Set("My_Title", (Byte)value).Where("uid", UID).Execute();
                }
            }
        }
        public int DisKO = 0;
        public static byte ScreenDistance = 0;
        public bool DisQuest = false;
        public static bool dis = false, dis2 = false;
        public static byte DisMax1 = 0;
        public static byte DisMax2 = 0;
        public static byte DisMax3 = 0;
        public Client.GameState ClientStats;
        public double DragonGems;
        public double PhoenixGems;
        public int BlackSpotStepSecs;
        public ushort Detoxication;
        public int Immunity;
        public int FatigueSecs;
        public int CriticalStrike;
        public int SkillCStrike;
        public int Counteraction;
        public int Breaktrough;
        public ushort Intensification;
        public int Block;
        public ushort FinalMagicDmgPlus;
        public ushort FinalMagicDmgReduct;
        public ushort FinalDmgPlus;
        public ushort FinalDmgReduct;
        public int Penetration;
        public int MetalResistance;
        public int WoodResistance;
        public int WaterResistance;
        public int FireResistance;
        public int EarthResistance;
        public bool TeamDeathMatch_BlueTeam = false;
        public int TeamDeathMatch_Hits = 0;
        public byte _SubClass, _SubClassLevel;
        public Game.Subclasses SubClasses = new Game.Subclasses();
        public bool Stunned = false, Confused = false;
        public bool Companion;
        public bool CauseOfDeathIsMagic = false;
        public bool OnIntensify;
        private uint flower_R;
        public uint AddFlower
        {
            get { return flower_R; }
            set
            {
                flower_R = value;
            }
        }
        public short KOSpellTime
        {
            get
            {
                if (KOSpell == 1110)
                {
                    if (ContainsFlag((ulong)FlagsUpdate.Flags.Cyclone))
                    {
                        return CycloneTime;
                    }
                }
                else if (KOSpell == 1025)
                {
                    if (ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                    {
                        return SupermanTime;
                    }
                }
                return 0;
            }
            set
            {
                if (KOSpell == 1110)
                {
                    if (ContainsFlag((ulong)FlagsUpdate.Flags.Cyclone))
                    {
                        int Seconds = CycloneStamp.AddSeconds(value).AllSeconds() - Time32.Now.AllSeconds();
                        if (Seconds >= 20)
                        {
                            CycloneTime = 20;
                            CycloneStamp = Time32.Now;
                        }
                        else
                        {
                            CycloneTime = (short)Seconds;
                            CycloneStamp = Time32.Now;
                        }
                        BitVector.UpdateFlag((int)FlagsUpdate.Flags.Cyclone, CycloneTime, true, 20);
                    }
                }
                if (KOSpell == 1025)
                {
                    if (ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                    {
                        int Seconds = SupermanStamp.AddSeconds(value).AllSeconds() - Time32.Now.AllSeconds();
                        if (Seconds >= 20)
                        {
                            SupermanTime = 20;
                            SupermanStamp = Time32.Now;
                        }
                        else
                        {
                            SupermanTime = (short)Seconds;
                            SupermanStamp = Time32.Now;
                        }
                        BitVector.UpdateFlag((int)FlagsUpdate.Flags.Superman, SupermanTime, true, 20);
                    }
                }
            }
        }
        public uint NobiltyPoleWarScore;
        public short CycloneTime = 0, SupermanTime = 0, BladeFlurryTime = 0, SuperCycloneTime = 0, DragonCycloneTime = 0, NoDrugsTime = 0, FatalStrikeTime = 0, ShurikenVortexTime = 0, OblivionTime = 0, ShackleTime = 0, AzureTime;
        public ushort KOSpell = 0;
        public int AzureDamage = 0;
        private ushort _enlightenPoints;
        private byte _receivedEnlighenPoints;
        private ushort _enlightmenttime;
        public float ToxicFogPercent, StigmaIncrease, FlameIncrease, MagicShieldIncrease, DodgeIncrease, ShieldIncrease;
        public byte ToxicFogLeft, ScurbyBomb, FlameTime, FlyTime, StigmaTime, InvisibilityTime, StarOfAccuracyTime, MagicShieldTime, DodgeTime, AccuracyTime, ShieldTime, MagicDefenderSecs;
        internal Features.TeamArenaPoison.Effect ArenaPoison;
        internal Features.TeamArenaBreakingTouch.Effect ArenaBreakingTouch;
        internal readonly object MonsterLifeSyncRoot = new object();
        private readonly object koSyncRoot = new object();
        private ushort koCount;
        public ushort KOCount
        {
            get { lock (koSyncRoot) return koCount; }
            set { lock (koSyncRoot) koCount = value; }
        }
        internal ushort AdvanceMonsterKo()
        {
            lock (koSyncRoot)
            {
                if (!HasLiveKoMode(Time32.Now)) return 0;
                if (koCount < ushort.MaxValue) koCount++;
                return koCount;
            }
        }
        internal bool HasLiveKoMode(Time32 now)
        {
            return OnCyclone() && CycloneTime > 0 && now < CycloneStamp.AddSeconds(CycloneTime)
                || OnSuperman() && SupermanTime > 0 && now < SupermanStamp.AddSeconds(SupermanTime)
                || OnSuperCyclone() && SuperCycloneTime > 0 && now < SuperCycloneStamp.AddSeconds(SuperCycloneTime)
                || OnDragonCyclone() && DragonCycloneTime > 0 && now < DragonCycloneStamp.AddSeconds(DragonCycloneTime)
                || OnOblivion() && OblivionTime > 0 && now < OblivionStamp.AddSeconds(OblivionTime)
                || OnManiacDance() && ManiacDanceTime > 0 && now < ManiacDanceStamp.AddSeconds(ManiacDanceTime)
                || OnOmnipotence() && OmnipotenceTime > 0 && now < OmnipotenceStamp.AddSeconds(OmnipotenceTime);
        }
        internal ushort ResetExpiredKo()
        {
            lock (koSyncRoot)
            {
                if (HasLiveKoMode(Time32.Now)) return 0;
                ushort previous = koCount;koCount = 0;return previous;
            }
        }
        public bool CounterKillSwitch = false;
        public Attack AttackPacket;
        public Attack VortexPacket;
        public byte[] SpawnPacket;
        private string _Name, _Spouse;
        private ushort _MDefence, _MDefencePercent;
        public ushort BaseDefence;
        public ushort Dexterity;
        private GameState _Owner;
        public uint ItemHP = 0;
        public ushort ItemMP = 0, ItemBless = 0, PhysicalDamageDecrease = 0, PhysicalDamageIncrease = 0, MagicDamageDecrease = 0, MagicDamageIncrease = 0, AttackRange = 1, Vigor = 0, ExtraVigor = 0;
        public ushort[] Gems = new ushort[10];
        private uint _MinAttack, _MaxAttack, _MagicAttack;
        public int Accuracy;
        public uint BaseMinAttack, BaseMaxAttack, BaseMagicAttack, BaseMagicDefence;
        private uint _TransMinAttack, _TransMaxAttack, _TransDodge, _TransPhysicalDefence, _TransMagicDefence;
        public bool Killed = false;
        public bool Transformed
        {
            get
            {
                return TransformationID != 98 && TransformationID != 99 && TransformationID != 0;
            }
        }
        public uint TransformationAttackRange = 0;
        public int TransformationTime = 0;
        public uint TransformationMaxHP = 0;
        private byte _Dodge;
        private Enums.PKMode _PKMode;
        private EntityFlag _EntityFlag;
        private MapObjectType _MapObjectType;
        public Enums.Mode Mode;
        private ulong _experience;
        private ulong _money;
        private uint _heavenblessing, _Cursed, _uid, _hitpoints, _maxhitpoints, _quizpoints, _conquerpoints, _TareaInner, _status, _status2, _status3, _status4, _Quest, _FirstCreditPoints, _TreasuerPoints;
        private ushort _doubleexp, _TimeDropSlayer, _doubleexp2, _body, _transformationid, _face, _strength, _agility, _spirit, _vitality, _atributes, _mana, _maxmana, _hairstyle, _mapid, _previousmapid, _x, _y, _pkpoints;
        private byte _stamina, _class, _reborn, _level;
        byte cls, secls;
        public byte FirstRebornClass
        {
            get
            {
                return cls;
            }
            set
            {
                cls = value;
                Update((byte)FlagsUpdate.DataType.FirsRebornClass, value, false);
                CharBuffer._FirstRebornClass = value;
                if (FullyLoaded)
                    UpdateDatabase("FirstRebornClass", value);
            }
        }
        public byte SecondRebornClass
        {
            get
            {
                return secls;
            }
            set
            {
                secls = value;
                Update((byte)FlagsUpdate.DataType.SecondRebornClass, value, false);
                CharBuffer._SecondRebornClass = value;
                if (FullyLoaded)
                    UpdateDatabase("SecondRebornClass", value);
            }
        }
        public byte FirstRebornLevel, SecondRebornLevel;
        public bool FullyLoaded = false, SendUpdates = false, HandleTiming = false;
        private FlagsUpdate update;
        private uint _namechanges;
        public uint namechanges
        {
            get
            {

                return _namechanges;
            }
            set
            {
                _namechanges = value;
            }
        }
        #endregion
        #region Acessors
        uint _ExtraInventory;
        public uint ExtraInventory
        {
            get { return _ExtraInventory; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                {
                    Update((byte)FlagsUpdate.DataType.AvailableSlots, 200, false);
                    Update((byte)FlagsUpdate.DataType.ExtraInventory, value, false);
                    _ExtraInventory = value;
                }
            }
        }
        private byte _TalentStaus;
        public byte JiangTalent1
        {
            get { return (byte)CharBuffer._JiangHuTalen; }
            set { CharBuffer._JiangHuTalen = value; }
        }
        public bool JiangActive1
        {
            get { return Convert.ToBoolean(CharBuffer._JiangHuActive); }
            set { CharBuffer._JiangHuTalen = Convert.ToUInt32(value); }
        }
        public byte TalentStaus
        {
            get
            {
                return _TalentStaus;
            }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                {
                    _TalentStaus = value;
                    CharBuffer._JiangHuTalen = value;
                    CharBuffer._JiangHuActive = 1;
                }
            }
        }
        public bool JiangActive
        {
            get
            {
                return (CharBuffer._JiangHuActive == 1);
            }
            set
            {
                CharBuffer._JiangHuActive = (value ? ((byte)1) : ((byte)0));
            }
        }
        public byte ServerID
        {
            get { return (byte)CharBuffer._ServerID; }
            set { CharBuffer._ServerID = value; }
        }
        public int BattlePower
        {
            get
            {
                return BattlePowerCalc(this);
            }
        }
        public int NMBattlePower { get { return SourceBattlePower; } }
        internal int SourceBattlePower { get { return BattlePowerBase(this); } }
        internal ushort SkillBattlePower { get { return BattlePowerBase(this, true); } }
        public uint Weight;
        public uint BattlePowerFrom(Entity mentor)
        {
            lock (Features.MentorExperience.Sync)
            {
                var source = Features.BattlePowerSharing.MentorSource(Owner);
                return source != null && ReferenceEquals(source.Entity, mentor) ? Features.BattlePowerSharing.MentorAmount(this, mentor) : 0;
            }
        }
        private DateTime mLastLogin;
        public DateTime LastLogin
        {
            get { return this.mLastLogin; }
            set { this.mLastLogin = value; }
        }
        public bool IPLast = false;
        public string LoweredName;
        public uint OwnerUID
        {
            get
            {
                return (uint)CharBuffer._OwnerUID;
            }
            set
            {
                _uid = value;
                CharBuffer._OwnerUID = value;
            }
        }
        public string Name
        {
            get { return _Name; }
            set
            {
                _Name = value;
                LoweredName = value.ToLower();
                CharBuffer.Names[0] = System.Text.Encoding.Default.GetBytes(value);
                if (IAmShadow)
                {
                    CharBuffer.Names[0] = System.Text.Encoding.Default.GetBytes("ShadowClone");
                    CharBuffer.Names[3] = System.Text.Encoding.Default.GetBytes(_Name);
                }
            }
        }
        public string Spouse
        {
            get { return _Spouse; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                {
                    Update(_String.Spouse, value, false);
                }
                _Spouse = value;
            }
        }
        public ulong NobalityDonation
        {
            get
            {
                return _NobalityDonation;
            }
            set
            {
                _NobalityDonation = value;
            }
        }
        private ulong _NobalityDonation;
        public const ulong MaximumSilver = long.MaxValue; // The existing entities.money column is signed bigint.
        private object MoneySyncRoot { get { return Owner == null ? (object)this : Owner.ItemSyncRoot ?? Owner; } }
        private void SetMoney(ulong value)
        {
            if (PersistenceRequiresReload) throw new InvalidOperationException("Silver balance requires reload.");
            if (value > MaximumSilver) throw new OverflowException("Silver balance exceeds durable storage.");
            System.Threading.Volatile.Write(ref _money, value);
            if (EntityFlag == EntityFlag.Player)
                Update((byte)FlagsUpdate.DataType.Money, value, false);
        }
        public ulong Money
        {
            get { return System.Threading.Volatile.Read(ref _money); }
            set { lock (MoneySyncRoot) SetMoney(value); }
        }
        // A compound property assignment reads before entering its setter. Keep
        // the complete balance calculation under the same lock as purchases/saves.
        public void AdjustMoney(decimal delta)
        {
            lock (MoneySyncRoot)
            {
                decimal next = _money + delta;
                if (next < 0 || next > MaximumSilver || decimal.Truncate(next) != next)
                    throw new OverflowException("Silver adjustment is outside the supported balance range.");
                SetMoney((ulong)next);
            }
        }
        public ulong TakeAllMoney()
        {
            lock (MoneySyncRoot) { ulong amount = _money; SetMoney(0); return amount; }
        }
        
        private byte _vipLevel;
        public byte VIPLevel
        {
            get { return _vipLevel; }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? (object)Owner)
                {
                    if (EntityFlag == EntityFlag.Player && FullyLoaded && !Features.VipState.TrySetLevel(Owner, value))
                        UpdateDatabase("VIPLevel", value);
                    _vipLevel = value;
                    if (EntityFlag == EntityFlag.Player && value > 0)
                        Update((byte)FlagsUpdate.DataType.VIPLevel, value, false);
                }
            }
        }
        private byte _vipDays;
        public byte VIPDays
        {
            get
            {
                return _vipDays;
            }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? Owner) _vipDays = value;
            }
        }
        private byte _vipLevelDays;
        public byte VIPLevelDays
        {
            get
            {
                return _vipLevelDays;
            }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? Owner) _vipLevelDays = value;
            }
        }
        internal void ApplyCommittedVip(byte level, byte days, byte levelDays)
        {
            if (Owner == null || !System.Threading.Monitor.IsEntered(Owner.ItemSyncRoot ?? Owner) || PersistenceRequiresReload)
                throw new InvalidOperationException("Committed VIP requires its healthy owner lock.");
            _vipLevel = level; _vipDays = days; _vipLevelDays = levelDays;
            Update((byte)FlagsUpdate.DataType.VIPLevel, level, false);
        }
        internal bool PersistenceRequiresReload;
        internal void ApplyCommittedConquerPoints(uint value)
        {
            lock (PurseLock)
            {
                Features.PurseBalance.CpCommitted(Owner, value);
                _conquerpoints = value;
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.ConquerPoints, value, false);
            }
        }
        internal void ProjectRevoraPoints(uint value)
        {
            if (value <= 0) value = 0;
            _RevoraPoint = value;
            if (EntityFlag == EntityFlag.Player)
                Update((byte)FlagsUpdate.DataType.RevoraPoints, (ulong)value, false);
        }
        // Call only after this transaction has stored the Revora Point total.
        // It does not write the row and does not treat a matching total as proof.
        internal void ApplyCommittedRevoraPoints(uint value)
        {
            lock (PurseLock)
            {
                if (PersistenceRequiresReload)
                    throw new InvalidOperationException("Committed Revora Points require a healthy owner.");
                if (Owner != null)
                    Features.PurseBalance.RvpCommitted(Owner, value);
                _RevoraPoint = value;
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.RevoraPoints, (ulong)value, false);
            }
        }
        object PurseLock { get { return Owner == null ? (object)this : Owner.ItemSyncRoot ?? Owner; } }
        public bool AddConquerPoints(long amount)
        {
            lock (PurseLock)
            {
                if (PersistenceRequiresReload) return false;
                if (amount < 0 || (ulong)_conquerpoints + (ulong)amount > uint.MaxValue)
                    return false;
                uint next = _conquerpoints + (uint)amount;
                if (!AssignConquerPoints(next)) return false;
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.ConquerPoints, _conquerpoints, false);
                return true;
            }
        }
        public bool TrySpendConquerPoints(uint amount)
        {
            lock (PurseLock)
            {
                if (PersistenceRequiresReload || _conquerpoints < amount) return false;
                if (!AssignConquerPoints(_conquerpoints - amount)) return false;
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.ConquerPoints, _conquerpoints, false);
                return true;
            }
        }
        public bool TrySpendRevoraPoints(uint amount)
        {
            lock (PurseLock)
            {
                if (PersistenceRequiresReload || _RevoraPoint < amount) return false;
                if (!AssignRevoraPoints(_RevoraPoint - amount)) return false;
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.RevoraPoints, (ulong)_RevoraPoint, false);
                return true;
            }
        }
        public bool AddRevoraPoints(long amount)
        {
            lock (PurseLock)
            {
                if (PersistenceRequiresReload) return false;
                if (amount < 0 || (ulong)_RevoraPoint + (ulong)amount > uint.MaxValue)
                    return false;
                uint next = _RevoraPoint + (uint)amount;
                if (!AssignRevoraPoints(next)) return false;
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.RevoraPoints, (ulong)_RevoraPoint, false);
                return true;
            }
        }
        bool AssignConquerPoints(uint value)
        {
            if (Owner != null && Features.PurseBalance.Managed(Owner))
            {
                if (!Features.PurseBalance.TryPersistCp(Owner, value))
                {
                    Features.QuestInventory.Fail(Owner, "cp-assign", new InvalidOperationException("Durable CP changed; reload required."));
                    return false;
                }
                _conquerpoints = value;
                return true;
            }
            _conquerpoints = value;
            if (Owner != null)
                Database.EntityTable.UpdateCps(Owner);
            return true;
        }
        bool AssignRevoraPoints(uint value)
        {
            if (Owner != null && Features.PurseBalance.Managed(Owner))
            {
                if (!Features.PurseBalance.TryPersistRvp(Owner, value))
                {
                    Features.QuestInventory.Fail(Owner, "rvp-assign", new InvalidOperationException("Durable Revora Points changed; reload required."));
                    return false;
                }
            }
            _RevoraPoint = value;
            return true;
        }
        public uint ConquerPoints
        {
            get
            {
                return _conquerpoints;
            }
            set
            {
                if (value <= 0)
                    value = 0;
                lock (PurseLock)
                {
                    if (PersistenceRequiresReload) return;
                    if (!AssignConquerPoints(value)) return;
                    if (EntityFlag == EntityFlag.Player)
                        Update((byte)FlagsUpdate.DataType.ConquerPoints, (uint)value, false);
                }
            }
        }
        public uint TareaInner
        {
            get
            {
                return _TareaInner;
            }
            set
            {
                if (value <= 0)
                    value = 0;

                _TareaInner = value;
                Database.EntityTable.UpdateTareaInner(this.Owner);
            }
        }

        public uint TreasuerPoints
        {
            get
            {
                return _TreasuerPoints;
            }
            set
            {
                if (value <= 0)
                    value = 0;

                _TreasuerPoints = value;
                Database.EntityTable.UpdateTreasuerPoints(this.Owner);
            }
        }
        public uint FirstCreditPoints
        {
            get { return _FirstCreditPoints; }
            set
            {
                if (value <= 0)
                    value = 0;
                _FirstCreditPoints = value;
                Database.EntityTable.UpdateFirstCreditPoints(this.Owner);
            }
        }
        public ushort Body
        {
            get { return _body; }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? (object)Owner)
                {
                    CharBuffer._Mesh = Revora.Game.Features.SpunGoldAppearance.Project(this, (uint)(TransformationID * 10000000 + Face * 10000 + value));
                    _body = value;
                    if (EntityFlag == EntityFlag.Player)
                    {
                        if (Owner != null)
                        {
                            Owner.ArenaStatistic.Model = (uint)(Face * 10000 + value);
                            Update((byte)FlagsUpdate.DataType.Mesh, Mesh, true);
                        }
                    }
                }
            }
        }
        public ushort TimeDropSlayer
        {
            get { return _TimeDropSlayer; }
            set
            {
                if (!FullyLoaded || Owner == null) { _TimeDropSlayer=value; TimeDropSlayerStamp=Time32.Now; return; }
                Features.TimeDropBalance.Set(Owner,value);
            }
        }
        internal void InstallTimeDropSlayer(ushort value)
        {
            if (FullyLoaded && Owner != null && !System.Threading.Monitor.IsEntered(Owner.ItemSyncRoot ?? Owner))
                throw new InvalidOperationException("TimeDrop projection requires its owner lock.");
            ushort before=_TimeDropSlayer; _TimeDropSlayer=value;
            if (value>=before) TimeDropSlayerStamp=Time32.Now;
            if (FullyLoaded && Owner!=null && (value==0 || value>=before)) SendTimeDropSlayer();
        }
        private void SendTimeDropSlayer()
        {
            var update=new FlagsUpdate(true);update.UID=UID;
            update.Append((byte)FlagsUpdate.DataType.TimeDropSlayer,0,_TimeDropSlayer,0,200);Owner.Send(update.ToArray());
        }
        internal void RefreshTimeDropSlayer()
        {
            if (Owner==null) return;
            lock(Owner.ItemSyncRoot ?? Owner) if(FullyLoaded && !PersistenceRequiresReload) SendTimeDropSlayer();
        }
        internal void TickTimeDropSlayer(Time32 now)
        {
            if (Owner==null) return;
            lock(Owner.ItemSyncRoot ?? Owner)
            {
                if (!Features.QuestInventory.Player(Owner,false) || _TimeDropSlayer==0 || now<=TimeDropSlayerStamp.AddMilliseconds(1000)) return;
                TimeDropSlayerStamp=now;InstallTimeDropSlayer((ushort)(_TimeDropSlayer-1));
            }
        }
        public ushort DoubleExperienceTime
        {
            get { return _doubleexp; }
            set
            {
                if (!FullyLoaded || Owner == null) { _doubleexp=value; DoubleExpStamp=Time32.Now; return; }
                Features.DoubleExperienceBalance.Set(Owner,value);
            }
        }
        internal void InstallDoubleExperience(ushort value)
        {
            if (FullyLoaded && Owner != null && !System.Threading.Monitor.IsEntered(Owner.ItemSyncRoot ?? Owner))
                throw new InvalidOperationException("Double EXP projection requires its owner lock.");
            ushort before=_doubleexp; _doubleexp=value;
            if (value>=before) DoubleExpStamp=Time32.Now;
            if (FullyLoaded && Owner!=null && (value==0 || value>=before)) SendDoubleExperience();
        }
        private void SendDoubleExperience()
        {
            var update=new FlagsUpdate(true);update.UID=UID;
            update.Append((byte)FlagsUpdate.DataType.DoubleExpTimer,0,_doubleexp,0,200);Owner.Send(update.ToArray());
        }
        internal void RefreshDoubleExperience()
        {
            if (Owner==null) return;
            lock(Owner.ItemSyncRoot ?? Owner) if(FullyLoaded && !PersistenceRequiresReload) SendDoubleExperience();
        }
        internal void TickDoubleExperience(Time32 now)
        {
            if (Owner==null) return;
            lock(Owner.ItemSyncRoot ?? Owner)
            {
                if (!Features.QuestInventory.Player(Owner,false) || _doubleexp==0 || now<=DoubleExpStamp.AddMilliseconds(1000)) return;
                DoubleExpStamp=now;InstallDoubleExperience((ushort)(_doubleexp-1));
            }
        }
        public ushort ExpProtectionTime
        {
            get { return _doubleexp2; }
            set
            {
                if (!FullyLoaded || Owner == null) { _doubleexp2=value; ProtectionStamp=Time32.Now; return; }
                Features.ExpProtectionBalance.Set(Owner,value);
            }
        }
        internal void InstallExpProtection(ushort value)
        {
            if (FullyLoaded && Owner != null && !System.Threading.Monitor.IsEntered(Owner.ItemSyncRoot ?? Owner))
                throw new InvalidOperationException("EXP protection projection requires its owner lock.");
            ushort before=_doubleexp2; _doubleexp2=value;
            if (value>=before) ProtectionStamp=Time32.Now;
            if (FullyLoaded && Owner!=null && (value==0 || value>=before)) SendExpProtection();
        }
        private void SendExpProtection()
        {
            var update=new FlagsUpdate(true);update.UID=UID;
            update.Append((byte)FlagsUpdate.DataType.ExpProtection,0,_doubleexp2,1,0);Owner.Send(update.ToArray());
        }
        internal void RefreshExpProtection()
        {
            if (Owner==null) return;
            lock(Owner.ItemSyncRoot ?? Owner) if(FullyLoaded && !PersistenceRequiresReload) SendExpProtection();
        }
        internal void TickExpProtection(Time32 now)
        {
            if (Owner==null) return;
            lock(Owner.ItemSyncRoot ?? Owner)
            {
                if (!Features.QuestInventory.Player(Owner,false) || _doubleexp2==0 || now<=ProtectionStamp.AddMilliseconds(1000)) return;
                ProtectionStamp=now;InstallExpProtection((ushort)(_doubleexp2-1));
            }
        }
        public Time32 CPSgold;
        public uint HeavenBlessing
        {
            get { return System.Threading.Volatile.Read(ref _heavenblessing); }
            set
            {
                if (!FullyLoaded || Owner == null) { _heavenblessing = value; return; }
                Features.BlessingBalance.Set(Owner, value);
            }
        }
        internal void InstallHeavenBlessing(uint value)
        {
            if (FullyLoaded && Owner != null && !System.Threading.Monitor.IsEntered(Owner.ItemSyncRoot ?? Owner))
                throw new InvalidOperationException("Blessing projection requires its owner lock.");
            uint old = _heavenblessing; _heavenblessing = value;
            if (!FullyLoaded) return;
            if (value == 0)
            {
                if (ContainsFlag((ulong)FlagsUpdate.Flags.HeavenBlessing)) RemoveFlag((ulong)FlagsUpdate.Flags.HeavenBlessing);
                if (old != 0) Update((byte)FlagsUpdate.DataType.HeavensBlessing, 0, false);
            }
            else if (!ContainsFlag((ulong)FlagsUpdate.Flags.HeavenBlessing) || old <= value)
            {
                AddFlag((ulong)FlagsUpdate.Flags.HeavenBlessing, StatusFlagsBigVector32.PermanentFlag, false);
                Update((byte)FlagsUpdate.DataType.HeavensBlessing, value, false);
                Update(_String.Effect, "bless", true);
            }
        }
        internal void TickHeavenBlessing()
        {
            if (Owner == null) return;
            lock (Owner.ItemSyncRoot ?? Owner)
            {
                if (!PersistenceRequiresReload && _heavenblessing > 0) InstallHeavenBlessing(_heavenblessing - 1);
            }
        }
        public uint CursedTime
        {
            get { return _Cursed; }
            set
            {
                uint oldVal = CursedTime;
                _Cursed = value;
                if (FullyLoaded)
                    if (value > 0)
                        if (!ContainsFlag((ulong)FlagsUpdate.Flags.Cursed) || oldVal <= _Cursed)
                        {
                            Cursed = Time32.Now;
                            AddFlag((ulong)FlagsUpdate.Flags.Cursed, (int)CursedTime, false, 1);
                            Update((byte)FlagsUpdate.DataType.CursedTimer, CursedTime, false);
                            UpdateDatabase("CursedTime", value);
                        }
            }
        }
        public uint PokerTable = 0;
        public PokerTable MyPokerTable
        {
            get
            {
                if (Kernel.PokerTables.ContainsKey(PokerTable))
                    return Kernel.PokerTables[PokerTable];
                else return null;
            }
            set { PokerTable = value.Id; }
        }
        public byte Stamina
        {
            get { return _stamina; }
            set
            {
                _stamina = value;
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.Stamina, value, false);
            }
        }
        public ushort TransformationID
        {
            get { return _transformationid; }
            set
            {
                Features.XpTransformAttribution.Clear(this);
                _transformationid = value;
                CharBuffer._Mesh = Revora.Game.Features.SpunGoldAppearance.Project(this, (uint)(value * 10000000 + Face * 10000 + Body));
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.Mesh, Mesh, true);
            }
        }
        public ushort Face
        {
            get { return _face; }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? (object)Owner)
                {
                    CharBuffer._Mesh = Revora.Game.Features.SpunGoldAppearance.Project(this, (uint)(TransformationID * 10000000 + value * 10000 + Body));
                    _face = value;
                    if (EntityFlag == EntityFlag.Player)
                    {
                        if (Owner != null)
                        {
                            Owner.ArenaStatistic.Model = (uint)(value * 10000 + Body);
                            Update((byte)FlagsUpdate.DataType.Mesh, Mesh, true);
                        }
                    }
                }
            }
        }
        public uint Mesh
        {
            get
            {
                return (uint)CharBuffer._Mesh;
            }
        }
        public byte Class
        {
            get { return _class; }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? (object)Owner)
                {
                    if (EntityFlag == EntityFlag.Player)
                    {
                        if (Owner != null)
                        {
                            if (Owner.ArenaStatistic != null)
                                Owner.ArenaStatistic.Class = value;
                            Update((byte)FlagsUpdate.DataType.Class, value, false);
                        }
                        _class = value;
                        CharBuffer._Class = value;
                        if (FullyLoaded)
                            UpdateDatabase("Class", value);
                        RuneProgress.ProfileChanged(Owner);
                        return;
                    }
                    _class = value;
                    CharBuffer._Class = value;
                }
            }
        }
        public byte Reborn
        {
            get
            {
                return _reborn;
            }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? (object)Owner)
                {
                    if (EntityFlag == EntityFlag.Player)
                    {
                        Update((byte)FlagsUpdate.DataType.Reborn, value, true);
                        if (FullyLoaded)
                            UpdateDatabase("Reborn", value);
                    }
                    _reborn = value;
                    CharBuffer._Reborn = value;
                    if (EntityFlag == EntityFlag.Player) RuneProgress.ProfileChanged(Owner);
                }
            }
        }
        public byte Level
        {
            get
            {
                if (EntityFlag == EntityFlag.Player)
                {
                    return _level;
                }
                else
                {
                    CharBuffer._MonsterLevel = _level;

                    return _level;
                }
            }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? (object)Owner)
                {
                    if (EntityFlag == EntityFlag.Player)
                    {
                        Update((byte)FlagsUpdate.DataType.Level, value, true);
                        Data update = new Data(true);
                        update.UID = UID;
                        update.ID = Data.Leveled;
                        if (Owner != null)
                        {
                            (Owner as Client.GameState).SendScreen(update, true);
                            Owner.ArenaStatistic.Level = value;
                        }
                        if (Owner != null)
                        {
                            if (Owner.AsMember != null)
                            {
                                Owner.AsMember.Level = value;
                            }
                        }
                        CharBuffer._Level = value;

                        Update((byte)FlagsUpdate.DataType.Level, value, true);
                        UpdateDatabase("Level", value);
                    }
                    else
                    {
                        CharBuffer._MonsterLevel = value;
                    }
                    _level = value;
                    if (EntityFlag == EntityFlag.Player) RuneProgress.ProfileChanged(Owner);
                }
            }
        }
        private uint mentorBP, independentBattlePower;
        public uint MentorBattlePower
        {
            get { return mentorBP; }
            set
            {
                lock (Features.MentorExperience.Sync)
                {
                    mentorBP = Owner == null || Features.BattlePowerSharing.MentorSource(Owner) == null ? 0 : Math.Min(200u, value);
                    InstallExtraBattlePower();
                }
            }
        }
        public uint _ExtraBattlePower;
        private void InstallExtraBattlePower()
        {
            uint total = independentBattlePower + mentorBP;
            _ExtraBattlePower = total; CharBuffer._ExtraBattlePower = total;
            if (Owner != null) Update((byte)FlagsUpdate.DataType.ExtraBattlePower, total, false);
        }
        public uint ExtraBattlePower
        {
            get { return _ExtraBattlePower; }
            set
            {
                lock (Features.MentorExperience.Sync)
                {
                    independentBattlePower = value >= mentorBP && value - mentorBP <= 200 ? value - mentorBP : 0;
                    InstallExtraBattlePower();
                }
            }
        }
        public float IntensifyPercent;
        public bool awayTeleported = false;
        public void SetAway(bool isAway)
        {
            if (!isAway && Away == 1)
            {
                Away = 0;
                Owner.SendScreen(CharBuffer.ToConquerPacket(10014), false);
                if (awayTeleported)
                {
                    awayTeleported = false;
                    Teleport(PreviousMapID, PrevX, PrevY);
                }
            }
            else if (isAway && Away == 0)
            {
                if (!Constants.PKFreeMaps.Contains(MapID))
                {
                    if (!(MapID == 1036 || MapID == 1002 || Owner.Mining))
                    {
                        PreviousMapID = MapID;
                        PrevX = X;
                        PrevY = Y;
                        Teleport(1036, 100, 100);
                        awayTeleported = true;
                    }
                }
            }
            Away = isAway ? (byte)1 : (byte)0;
        }
        public byte Away
        {
            get { return (byte)CharBuffer._Away; }
            set { CharBuffer._Away = value; }
        }
        public byte Boss
        {
            get { return (byte)CharBuffer._Boss; }
            set
            {
                CharBuffer._Boss = 1;
            }
        }
        public void ApplyMonsterBossHud()
        {
            if (EntityFlag != EntityFlag.Monster || MonsterInfo == null || !MonsterInfo.Boss)
                return;
            CharBuffer._Boss = 1;
            uint hp = _hitpoints;
            if (MaxHitpoints == 0)
                MaxHitpoints = hp;
            Hitpoints = hp;
        }
        public uint UID
        {
            get
            {
                return (uint)CharBuffer._UID;
            }
            set
            {
                _uid = value;
                CharBuffer._UID = value;
            }
        }
        public ushort GuildID
        {
            get { return (ushort)CharBuffer._GuildID; }
            set
            {
                CharBuffer._GuildID = value;
                if (FullyLoaded)
                    UpdateDatabase("GuildID", value);
            }
        }
        public ushort GuildRank
        {
            get { return (ushort)CharBuffer._GuildRank; }
            set { CharBuffer._GuildRank = value; }
        }
        public ushort Strength
        {
            get { return _strength; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                {
                    Update((byte)FlagsUpdate.DataType.Strength, value, false);
                }
                _strength = value;
            }
        }
        public ushort Agility
        {
            get
            {
                if (OnCyclone())
                    return (ushort)(_agility);
                return _agility;
            }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.Agility, value, false);
                _agility = value;
            }
        }
        public ushort Spirit
        {
            get { return _spirit; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.Spirit, value, false);
                _spirit = value;
            }
        }
        public ushort Vitality
        {
            get { return _vitality; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.Vitality, value, false);
                _vitality = value;
            }
        }
        public ushort Atributes
        {
            get { return _atributes; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.Atributes, value, false);
                _atributes = value;
            }
        }
        internal FlagsUpdate MonsterHealthUpdate(uint value)
        {
            var packet = new FlagsUpdate(true);
            packet.UID = UID;
            packet.Append((byte)FlagsUpdate.DataType.Hitpoints, value);
            return packet;
        }
        internal volatile bool HealthSynchronizationRequired;
        private object healthMutationSync;
        internal object HealthMutationSync { get { return System.Threading.LazyInitializer.EnsureInitialized(ref healthMutationSync); } }
        internal T WithHealthMutation<T>(Func<T> action) { lock(HealthMutationSync)return action(); }
        public uint Hitpoints
        {
            get { return _hitpoints; }
            set { if(EntityFlag==EntityFlag.Player)lock(HealthMutationSync)SetHitpoints(value);else SetHitpoints(value); }
        }
        private void SetHitpoints(uint value)
        {
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.Hitpoints, value, false);
                else if (EntityFlag == EntityFlag.Monster)
                {
                    MonsterInfo.SendScreen(MonsterHealthUpdate(value));
                }
                if (EntityFlag == EntityFlag.Monster)
                {
                    lock (MonsterLifeSyncRoot)
                    {
                        if (_hitpoints == 0 && value > 0) {
                            Features.MonsterKoCounter.Revived(this);
                            Features.WildMonsterVictory.Revived(this);
                        }
                        _hitpoints = value;
                    }
                }
                else _hitpoints = value;

                // 6711's boss HUD reads actual actor HP against profile MaxLife.
                // The protobuf field supports full HP; a 10,000 scale displays incorrectly.
                CharBuffer._Hitpoints = value;
                if (EntityFlag == EntityFlag.Player)
                {
                    if (Owner != null)
                    {
                        if (Owner.Team != null)
                        {
                            foreach (var Team in Owner.Team.Temates)
                            {
                                AddToTeam addme = new AddToTeam();
                                addme.UID = Owner.Entity.UID;
                                addme.Hitpoints = (ushort)Owner.Entity.Hitpoints;
                                addme.Mesh = Owner.Entity.Mesh;
                                addme.Name = Owner.Entity.Name;
                                addme.MaxHitpoints = (ushort)Owner.Entity.MaxHitpoints;
                                Team.entry.Send(addme.ToArray());
                            }
                        }
                    }
                }
        }
        public ushort Mana
        {
            get { return _mana; }
            set
            {
                lock(HealthMutationSync) {
                    if (EntityFlag == EntityFlag.Player)
                        Update((byte)FlagsUpdate.DataType.Mana, value, false);
                    _mana = value;
                }
            }
        }
        public ushort MaxMana
        {
            get { return _maxmana; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.MaxMana, value, false);
                _maxmana = value;
            }
        }
        public ushort HairStyle
        {
            get { return _hairstyle; }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? (object)Owner)
                {
                    if (EntityFlag == EntityFlag.Player)
                    {
                        Update((byte)FlagsUpdate.DataType.HairStyle, value, true);
                    }
                    _hairstyle = value;
                    CharBuffer._HairStyle = value;
                }
            }
        }
        public byte SubClass
        {
            get { return this._SubClass; }
            set
            {
                this._SubClass = value;

                if (EntityFlag == EntityFlag.Player)
                {
                    if (FullyLoaded)
                    {
                        UpdateDatabase("SubClass", _SubClass);
                    }
                }
            }
        }
        public byte SubClassesActive
        {
            get { return 0; }
            set { }
        }
        public byte SubClassLevel
        {
            get { return _SubClassLevel; }
            set
            {
                this._SubClassLevel = value;
                switch (EntityFlag)
                {
                    case EntityFlag.Player:
                        if (FullyLoaded)
                        {
                            UpdateDatabase("SubClassLevel", value);
                        }
                        break;
                }
            }
        }
        public ConquerStructures.NobilityRank NobilityRank
        {
            get { return (Game.ConquerStructures.NobilityRank)CharBuffer._NobilityRank; }
            set
            {
                CharBuffer._NobilityRank = (byte)value;
                if (Owner != null)
                {
                    if (Owner.AsMember != null)
                    {
                        Owner.AsMember.NobilityRank = value;
                    }
                }
            }
        }
        public byte HairColor
        {
            get { return (byte)(HairStyle / 1000); }
            set {
                if (value > 6) throw new ArgumentOutOfRangeException("value");
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? (object)Owner)
                    HairStyle = checked((ushort)(value * 1000 + HairStyle % 1000));
            }
        }
        public Enums.AppearanceType Appearance
        {
            get { return (Enums.AppearanceType)CharBuffer._Appearance; }
            set
            {
                CharBuffer._Appearance = (byte)value;
                if (FullyLoaded)
                {
                    MySqlCommand cmd = new MySqlCommand(Database.MySqlCommandType.UPDATE);
                    cmd.Update("entities").Set("Appearance", (Byte)value).Where("uid", UID).Execute();
                }
            }
        }
        private long mapTransitionVersion;
        internal long MapTransitionVersion { get { return System.Threading.Interlocked.Read(ref mapTransitionVersion); } }
        public ushort MapID
        {
            get { return _mapid; }
            set
            {
                lock (Owner == null ? (object)this : Owner.ItemSyncRoot ?? (object)Owner)
                {
                    // Map-bound paid services share this owner lock. Persist the
                    // transition before exposing it, including durable visit closure.
                    if (EntityFlag == EntityFlag.Player && FullyLoaded && (Owner == null || !Owner.Fake))
                        new Database.MySqlCommand(Database.MySqlCommandType.UPDATE).Update("entities")
                            .Set("MapID", value).Set("PreviousMapID", PreviousMapID)
                            .Set("PreviousX", PrevX).Set("PreviousY", PrevY)
                            .Where("UID", UID).Execute();
                    if (_mapid != value && EntityFlag == EntityFlag.Player) Revora.Game.Features.WarriorStance.Cancel(Owner);
                    if (_mapid != value) System.Threading.Interlocked.Increment(ref mapTransitionVersion);
                    _mapid = value;
                    Revora.Game.Features.MagicDefenderProtection.Moved(this);
                }
            }
        }
        public uint Status
        {
            get { return _status; }
            set { _status = value; }
        }
        public uint Status2
        {
            get { return _status2; }
            set { _status2 = value; }
        }
        public uint Status3
        {
            get { return _status3; }
            set { _status3 = value; }
        }
        public uint Status4
        {
            get { return _status4; }
            set { _status4 = value; }
        }
        public uint Quest
        {
            get { return _Quest; }
            set { _Quest = value; }
        }
        public ushort PreviousMapID
        {
            get { return _previousmapid; }
            set { _previousmapid = value; }
        }
        public ushort X
        {
            get { return _x; }
            set
            {
                _x = value;
                CharBuffer._X = value;
                Revora.Game.Features.MagicDefenderProtection.Moved(this);
                if (EntityFlag == Game.EntityFlag.Player)
                    if (FullyLoaded)
                        UpdateDatabase("X", value);
            }
        }
        public ushort Y
        {
            get { return _y; }
            set
            {
                _y = value;
                CharBuffer._Y = value;
                Revora.Game.Features.MagicDefenderProtection.Moved(this);
                if (EntityFlag == Game.EntityFlag.Player)
                    if (FullyLoaded)
                        UpdateDatabase("Y", value);
            }
        }
        public ushort PX
        {
            get;
            set;
        }
        public ushort PY
        {
            get;
            set;
        }
        public bool Dead
        {
            get { return Hitpoints < 1; }
            set { throw new NotImplementedException(); }
        }
        private ushort _Defence;
        public ushort Defence
        {
            get { return _Defence; }
            set { _Defence = value; }
        }
        public ushort TransformationDefence
        {
            get
            {
                if (ContainsFlag((ulong)FlagsUpdate.Flags.MagicShield))
                {
                    if (ShieldTime > 0)
                        return (ushort)(_TransPhysicalDefence * ShieldIncrease);
                    else
                        return (ushort)(_TransPhysicalDefence * MagicShieldIncrease);
                }
                return (ushort)_TransPhysicalDefence;
            }
            set { _TransPhysicalDefence = value; }
        }
        public ushort MagicDefencePercent
        {
            get { return _MDefencePercent; }
            set { _MDefencePercent = value; }
        }
        public ushort TransformationMagicDefence
        {
            get { return (ushort)_TransMagicDefence; }
            set { _TransMagicDefence = value; }
        }
        public ushort MagicDefence
        {
            get { return _MDefence; }
            set { _MDefence = value; }
        }
        public Client.GameState Owner
        {
            get { return _Owner; }
            set { _Owner = value; }
        }
        public uint TransformationMinAttack
        {
            get
            {
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                    return (uint)(_TransMinAttack * StigmaIncrease);
                return _TransMinAttack;
            }
            set { _TransMinAttack = value; }
        }
        public uint TransformationMaxAttack
        {
            get
            {
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                    return (uint)(_TransMaxAttack * StigmaIncrease);
                return _TransMaxAttack;
            }
            set { _TransMaxAttack = value; }
        }
        public uint MinAttack
        {
            get { return _MinAttack; }
            set { _MinAttack = value; }
        }
        public uint MaxAttack
        {
            get { return _MaxAttack; }
            set { _MaxAttack = value; }
        }
        public uint MaxHitpoints
        {
            get { return _maxhitpoints; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                    if (TransformationID != 0 && TransformationID != 98)
                        Update((byte)FlagsUpdate.DataType.MaxHitpoints, value, true);
                _maxhitpoints = value;
            }
        }
        public uint MagicAttack
        {
            get { return _MagicAttack; }
            set { _MagicAttack = value; }
        }
        public byte Dodge
        {
            get
            {
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Dodge))
                {
                    return (byte)(_Dodge * DodgeIncrease);
                }
                return _Dodge;
            }
            set { _Dodge = value; }
        }
        public byte TransformationDodge
        {
            get
            {
                if (ContainsFlag((ulong)FlagsUpdate.Flags.Dodge))
                    return (byte)(_TransDodge * DodgeIncrease);
                return (byte)_TransDodge;
            }
            set { _TransDodge = value; }
        }
        public MapObjectType MapObjType
        {
            get { return _MapObjectType; }
            set { _MapObjectType = value; }
        }
        public EntityFlag EntityFlag
        {
            get { return _EntityFlag; }
            set { _EntityFlag = value; }
        }
        public ulong Experience
        {
            get { return _experience; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                    Update((byte)FlagsUpdate.DataType.Experience, value, false);
                _experience = value;
            }
        }
        public ushort EnlightenPoints
        {
            get { return _enlightenPoints; }
            set { _enlightenPoints = value; }
        }
        public byte ReceivedEnlightenPoints
        {
            get { return _receivedEnlighenPoints; }
            set { _receivedEnlighenPoints = value; }
        }
        public ushort EnlightmentTime
        {
            get { return _enlightmenttime; }
            set { _enlightmenttime = value; }
        }
        private object PkSyncRoot { get { return Owner == null ? (object)this : Owner.PkSyncRoot; } }
        internal void ChangePkPoints(int delta)
        {
            lock (PkSyncRoot)
                PKPoints = (ushort)Math.Max(0L, Math.Min(ushort.MaxValue, (long)_pkpoints + delta));
        }
        public ushort PKPoints
        {
            get { lock (PkSyncRoot) return _pkpoints; }
            set
            {
                lock (PkSyncRoot)
                {
                _pkpoints = value;
                if (EntityFlag == EntityFlag.Player)
                {
                    Update((byte)FlagsUpdate.DataType.PKPoints, value, false);
                    if (PKPoints > 99)
                    {
                        RemoveFlag((ulong)FlagsUpdate.Flags.RedName);
                        AddFlag((ulong)FlagsUpdate.Flags.BlackName,StatusFlagsBigVector32.PermanentFlag,false, 6 * 60);
                    }
                    else if (PKPoints > 29)
                    {
                        AddFlag((ulong)FlagsUpdate.Flags.RedName, StatusFlagsBigVector32.PermanentFlag, false,6 * 60);
                        RemoveFlag((ulong)FlagsUpdate.Flags.BlackName);
                    }
                    else if (PKPoints < 30)
                    {
                        RemoveFlag((ulong)FlagsUpdate.Flags.RedName);
                        RemoveFlag((ulong)FlagsUpdate.Flags.BlackName);
                    }
                }
                }
            }
        }
        public uint QuizPoints
        {
            get { return _quizpoints; }
            set
            {
                if (EntityFlag == EntityFlag.Player)
                {
                    Update((byte)FlagsUpdate.DataType.QuizPoints, value, true);
                    if (FullyLoaded)
                        UpdateDatabase("QuizPoints", value);
                }
                _quizpoints = value;
                CharBuffer._QuizPoints = value;
            }
        }
        public UInt32 ClanId
        {
            get { return (uint)CharBuffer._ClanUID; }
            set { CharBuffer._ClanUID = value; }
        }
        public Clan Myclan;
        public Clan.Ranks ClanRank
        {
            get { return (Clan.Ranks)CharBuffer._ClanRank; }
            set { CharBuffer._ClanRank = (Byte)value; }
        }
        public Clan GetClan
        {
            get
            {
                Clan cl;
                Kernel.Clans.TryGetValue(ClanId, out cl);
                return cl;
            }
        }
        string clan = "";
        public string ClanName
        {
            get { return clan; }
            set
            {
                clan = value;
                CharBuffer.Names[2] = System.Text.Encoding.Default.GetBytes(value);
            }
        }
        public Enums.PKMode PKMode
        {
            get { return _PKMode; }
            set { _PKMode = value; }
        }
        public ushort Action
        {
            get { return (ushort)CharBuffer._Action; }
            set { CharBuffer._Action = value; }
        }
        public Enums.ConquerAngle Facing
        {
            get { return (Enums.ConquerAngle)CharBuffer._Facing; }
            set { CharBuffer._Facing = (byte)value; }
        }
        /*public void AddFlag(ulong flag)
        {
            if (!BitVector.ContainFlag((int)flag))
            {
                BitVector.TryAdd((int)flag);
                UpdateEffects(BitVector.bits, true);
            }
        }*/
        public void ClearFlagsMobs()
        {
            Features.XpEffectAttribution.Clear(this);
            BitVector.Clear();
            BitVector.ArrayFlags.Clear();
            UpdateEffects(BitVector.bits, true);
        }
        public void ClearFlags()
        {
            Features.XpEffectAttribution.Clear(this);
            BitVector.GetClear();
            UpdateEffects(BitVector.bits, true);
        }
        public void AddSpellFlag(FlagsUpdate.Flags Flag, int Secounds, bool RemoveOnDead, int StampSecounds = 0)
        {
            if (BitVector.ContainFlag((int)Flag))
                BitVector.TryRemove((int)Flag);
            AddFlag((ulong)Flag, Secounds, RemoveOnDead, StampSecounds);
        }
        public bool AddFlag(ulong Flag, int Secounds, bool RemoveOnDead, int StampSecounds = 0, uint showamount = 0, uint amount = 0)
        {
            if (Flag == (uint)FlagsUpdate.Flags.XPList)
            {
                Update((byte)FlagsUpdate.DataType.XPLIST, 1, false);
            }
            if (!BitVector.ContainFlag((int)Flag))
            {
                BitVector.TryAdd((int)Flag, Secounds, RemoveOnDead, StampSecounds);
                Features.XpCastAttribution.Applied(this,Flag,Secounds);
                UpdateEffects(BitVector.bits, true);
                return true;
            }
            Features.XpCastAttribution.Applied(this,Flag,Secounds);
            return false;
        }
        public bool UpdateXP(ulong Flag, int Secounds, bool increase)
        {
            return BitVector.UpdateXP((int)Flag, Secounds, increase);
        }
        public bool ContainsFlag(ulong flag)
        {
            return BitVector.ContainFlag((int)flag);
        }
        public void RemoveFlag(ulong flag)
        {
            Features.XpEffectAttribution.Removed(this,flag);
            if ((FlagsUpdate.Flags)flag == FlagsUpdate.Flags.XPList)
            {
                Update((byte)FlagsUpdate.DataType.XPLIST, 0, false);
            }
            if (BitVector.ContainFlag((int)flag))
            {
                BitVector.TryRemove((int)flag);
                UpdateEffects(BitVector.bits, true);
            }
        }
        public void SetVisible()
        {
            CharBuffer.__WindowSpawn = 0;
        }
        public Game.Enums.CountryID CountryID
        {
            get
            {
                return (Enums.CountryID)CharBuffer._CountryCode;
            }
            set
            {
                CharBuffer._CountryCode = (ushort)value;
            }
        }
        public bool EpicPirate()
        {
            if (EntityFlag == Game.EntityFlag.Player)
            {
                var weapons = Owner.Weapons;
                if (weapons.Item1 != null && weapons.Item2 != null)
                    if (weapons.Item1.ID / 1000 == 670 && weapons.Item2.ID / 1000 == 671)
                        return true;
            }
            return false;
        }
        public bool IsWindWalker()
        {
            if (EntityFlag == Game.EntityFlag.Player)
            {
                var weapons = Owner.Weapons;
                if (weapons.Item1 != null)
                    if (weapons.Item1.ID / 1000 == 626)
                        return true;
                    else if (weapons.Item2 != null)
                        if (weapons.Item2.ID / 1000 == 626)
                            return true;
            }
            return false;
        }
        public bool Assassin()
        {
            if (EntityFlag == Game.EntityFlag.Player)
            {
                var weapons = Owner.Weapons;
                if (weapons.Item1 != null)
                    if (weapons.Item1.ID / 1000 == 613)
                        return true;
                    else if (weapons.Item2 != null)
                        if (weapons.Item2.ID / 1000 == 613)
                            return true;
            }
            return false;
        }
        public bool EpicTrojan()
        {
            if (EntityFlag == Game.EntityFlag.Player)
            {
                var weapons = Owner.Weapons;
                if (weapons.Item1 != null)
                    if (weapons.Item1.ID / 1000 == 614)
                        return true;
                    else if (weapons.Item2 != null)
                        if (weapons.Item2.ID / 1000 == 614)
                            return true;
            }
            return false;
        }
        private bool spiritFocus;
        public bool SpiritFocus
        {
            get
            {
                if (!spiritFocus) return false;
                if (Time32.Now > spiritFocusStamp.AddSeconds(5))
                    return true;
                return false;
            }
            set { spiritFocus = value; if (value) spiritFocusStamp = Time32.Now; }
        }
        public int SuperItemBless;
        public float SpiritFocusPercent;
        public bool Aura_isActive;
        public ulong Aura_actType;
        public uint Aura_actPower;
        public bool JustCreated = false;
        public int ChainboltTime;
        public int FrozenTime;
        public Enums.AppearanceType AppearanceBkp;
        public Enums.PKMode PrevPKMode;
        private uint assassinBP;
        internal void PreviousTeleport()
        {
            Teleport(PreviousMapID, PrevX, PrevY);
            BringToLife();
        }
        public bool IsThisLeftGate(int X, int Y)
        {
            if (Game.GuildWar.RightGate == null)
                return false;
            if (MapID == 1038)
            {
                if ((X == 223 || X == 222) && (Y >= 175 && Y <= 185))
                {
                    if (Game.GuildWar.RightGate.Mesh / 10 == 27)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public bool IsThisRightGate(int X, int Y)
        {
            if (Game.GuildWar.LeftGate == null)
                return false;
            if (MapID == 1038)
            {
                if ((Y == 210 || Y == 209) && (X >= 154 && X <= 166))
                {
                    if (Game.GuildWar.LeftGate.Mesh / 10 == 24)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public bool ThroughGate(int X, int Y)
        {
            return IsThisLeftGate(X, Y) || IsThisRightGate(X, Y);
        }
        public uint AssassinBP
        {
            get { return assassinBP; }
            set
            {
                assassinBP = value;
                CharBuffer._BattlePower = value;
            }
        }
        private uint _GuildSharedBp, _boundCps, _luckypoints, _updatelist, _RevoraPoint;
        private uint Flor;
        public uint FlowerRank
        {
            get { return Flor; }
            set
            {
                Flor = value;
                CharBuffer._Flower = value + 10000;
            }
        }
        public uint GuildSharedBp
        {
            get { return _GuildSharedBp; }
            set
            {
                switch (EntityFlag)
                {
                    case EntityFlag.Player:
                        if (FullyLoaded)
                        {
                            Update(44, Math.Min(value, 15), false);
                            CharBuffer._GuildSharedBp = Math.Min(value, 15);
                        }
                        break;
                }
                _GuildSharedBp = value;
            }
        }
        public uint ClanSharedBp
        {
            get { return (uint)CharBuffer._ClanSharedBp; }
            set
            {
                if ((EntityFlag == Game.EntityFlag.Player) && FullyLoaded)
                {
                    Clan getClan = GetClan;
                    Update((byte)FlagsUpdate.DataType.ClanBattlepower, value, false);
                }
                CharBuffer._ClanSharedBp = value;
            }
        }
        internal void ApplyCommittedBoundCps(uint value)
        {
            _boundCps = value;
            if (EntityFlag == EntityFlag.Player && FullyLoaded)
                Update((byte)FlagsUpdate.DataType.BoundConquerPoints, value, false);
        }
        public uint BoundCps
        {
            get { return _boundCps; }
            set
            {
                switch (EntityFlag)
                {
                    case EntityFlag.Player:
                        {
                            if (FullyLoaded)
                            {

                                UpdateDatabase("BoundCPS", value);
                                Update((byte)FlagsUpdate.DataType.BoundConquerPoints, value, false);
                            }
                            break;
                        }
                }
                _boundCps = value;
            }
        }
        public uint Lukypionts
        {
            get { return _luckypoints; }
            set
            {
                _luckypoints = value;
                if (FullyLoaded)
                    UpdateDatabase("LuckyPoints", value);
            }
        }
        public uint UpdateList
        {
            get { return _updatelist; }
            set
            {
                _updatelist = value;
                UpdateDatabase("updatelist", value);
            }
        }
        public uint RevoraPoint
        {
            get { return _RevoraPoint; }
            set
            {
                if (value <= 0)
                    value = 0;
                lock (PurseLock)
                {
                    if (PersistenceRequiresReload) return;
                    if (!AssignRevoraPoints(value)) return;
                    if (EntityFlag == EntityFlag.Player)
                        Update((byte)FlagsUpdate.DataType.RevoraPoints, (ulong)value, false);
                }
            }
        }
       
        public void Save(String row, String value)
        {
            using (var Command = new MySqlCommand(MySqlCommandType.UPDATE))
                Command.Update("entities").Set(row, value).Where("uid", UID).Execute();
        }
        public void Save(String row, UInt16 value)
        {
            using (var Command = new MySqlCommand(MySqlCommandType.UPDATE))
                Command.Update("entities").Set(row, value).Where("uid", UID).Execute();
        }
        public void Save(String row, Boolean value)
        {
            using (var Command = new MySqlCommand(MySqlCommandType.UPDATE))
                Command.Update("entities").Set(row, value).Where("uid", UID).Execute();
        }
        public void Save(String row, UInt32 value)
        {
            using (var Command = new MySqlCommand(MySqlCommandType.UPDATE))
                Command.Update("entities").Set(row, value).Where("uid", UID).Execute();
        }
        #endregion
        #region DailySignIn
        public byte[] DailySignDays;
        public byte DailySignRewards;
        public byte DailySignVIPChances;
        #endregion
        #region SendScreen
        public void SendScreen(Interfaces.IPacket Data)
        {
            GameState[] Chars = new GameState[Kernel.GamePool.Count];
            Kernel.GamePool.Values.CopyTo(Chars, 0);
            foreach (GameState C in Chars)
                if (C != null)
                    if (C.Entity != null)
                        if (Calculations.PointDistance(X, Y, C.Entity.X, C.Entity.Y) <= 20)
                            C.Send(Data);
            Chars = null;
        }
        #endregion
        #region PK Group
        public static int Leadrinmap;
        public static int Memberrinmap;
        public void SendScoreLAstTeam(GameState client)
        {

            for (uint x = 1; x < 3; x++)
            {
                string Mesage = "";
                Leadrinmap = 0;
                Memberrinmap = 0;
                foreach (GameState clients in Kernel.GamePool.Values)
                {

                    if (clients.Entity.MapID == 16414)
                    {
                        if (clients.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TeamLeader) && !clients.Entity.Dead)
                            Leadrinmap++;
                        else if (!clients.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TeamLeader) && !clients.Entity.Dead && clients.Team != null)
                            Memberrinmap++;
                    }

                }
                if (x == 1)

                    Mesage = "Leading Groups : " + Leadrinmap + "";
                else
                    Mesage = "All players: " + Memberrinmap + "";
                Message msg = new Message(Mesage, System.Drawing.Color.Red, x == 1 ? Message.FirstRightCorner : Message.ContinueRightCorner);
                client.Send(msg);
            }

        }

        #endregion
        #region Functions

        public UInt16 BattlePowerCalc(Entity e) { return Features.BattlePowerSharing.Total(e); }
        private static UInt16 BattlePowerBase(Entity e, bool skill = false)
        {
            if (e == null) return 0;
            if (e.Owner == null) return 0;
            UInt16 BP = e.Level;
            var weapons = e.Owner.Weapons;
            int[] champion = !skill && e.Owner.DoChampStats ? e.Owner.ChampionLimits : null;
            foreach (ConquerItem i in e.Owner.Equipment.Objects)
            {
                if (i == null) continue;
                int pos = i.Position; if (pos > 20) pos -= 20;
                if (pos != ConquerItem.Bottle &&
                    pos != ConquerItem.Garment && pos != ConquerItem.RightWeaponAccessory
                    && pos != ConquerItem.LeftWeaponAccessory && pos != ConquerItem.SteedArmor)
                {
                    if (!i.IsWorn) continue;
                    if (pos == ConquerItem.RightWeapon || pos == ConquerItem.LeftWeapon)
                        continue;
                    if (i.Durability == 0 || !e.Owner.AreStatsLoadable(i)) continue;
                    BP += ItemBatlePower(i, skill, champion);
                }
            }
            if (weapons.Item1 != null)
            {
                var i = weapons.Item1;
                Byte Multiplier = 1;
                if (i.IsTwoHander())
                    Multiplier = weapons.Item2 == null ? (Byte)2 : (Byte)1;
                BP += (ushort)(ItemBatlePower(i, skill, champion) * Multiplier);
            }
            if (weapons.Item2 != null)
                BP += ItemBatlePower(weapons.Item2, skill, champion);

            if (e.EntityFlag == Game.EntityFlag.Player)
            {
                if (skill)
                    BP += (byte)Math.Min((byte)e.NobilityRank, (byte)ConquerStructures.NobilityRank.Earl);
                else if (e.Owner.DoChampStats)
                    BP += (Byte)(Math.Min((byte)e.NobilityRank, e.Owner.ChampionLimits[8]));
                else
                    BP += (Byte)e.NobilityRank;
            }
            BP += (Byte)(e.Reborn * 5);
            if (!skill) BP += (ushort)Math.Min(e.GuildSharedBp, 15u);
            BP += (ushort)e.independentBattlePower;
            //switch (VIPLevel)
            //{
            //    case 7:
            //        Update(FlagsUpdate.MentorBattlePower, 1, 1);
            //        break;
            //    case 8:
            //        Update(FlagsUpdate.MentorBattlePower, 2, 2);
            //        break;
            //    case 9:
            //        Update(FlagsUpdate.MentorBattlePower, 4, 4);
            //        break;
            //}
            return BP;
        }
        internal static bool IsBow(uint p)
        {
            return p / 1000 == 500;
        }
        public bool IsBowEquipped
        {
            get
            {
                if (EntityFlag == Game.EntityFlag.Player)
                {
                    var right = Owner.Equipment.TryGetItem(ConquerItem.RightWeapon);
                    if (right != null)
                    {
                        return Entity.IsBow(right.ID);
                    }
                }
                return false;
            }
        }
        private static ushort ItemBatlePower(ConquerItem i, bool skill = false, int[] champion = null)
        {
            Byte Multiplier = 1;
            Byte quality = (Byte)(i.ID % 10);
            int BP = 0;
            if (quality >= 6)
            {
                BP += (Byte)((quality - 5) * Multiplier);
            }
            if (i.SocketOne != 0 && (champion == null || champion[2] >= 1))
            {
                BP += (Byte)(1 * Multiplier);
                if ((Byte)i.SocketOne % 10 == 3)
                    BP += (Byte)(1 * Multiplier);
                if (!skill && i.SocketTwo != 0 && (champion == null || champion[2] >= 2))
                {
                    BP += (Byte)(1 * Multiplier);
                    if ((Byte)i.SocketTwo % 10 == 3)
                        BP += (Byte)(1 * Multiplier);
                }
            }
            BP += (champion != null ? Math.Min((int)i.Plus, champion[0]) : skill ? Math.Min((int)i.Plus, 4) : i.Plus) * Multiplier;
            return (ushort)BP;
        }
        public Entity(EntityFlag Flag, bool companion)
        {
            Companion = companion;
            this.EntityFlag = Flag;
            Mode = Enums.Mode.None;
            update = new FlagsUpdate(true);
            update.UID = UID;
            switch (Flag)
            {
                case EntityFlag.Player:
                    {
                        MapObjType = Game.MapObjectType.Player;
                        Halos = new ConcurrentDictionary<int, DateTime>();
                        break;
                    }
                case EntityFlag.Monster:
                    {
                        MapObjType = Game.MapObjectType.Monster;
                        break;
                    }
            }
            SpawnPacket = new byte[0];
        }
        public void Ressurect()
        {
            if (EntityFlag == EntityFlag.Player)
                SendCurrentMapStatus();
        }
        public void BringToLife()
        {
            Hitpoints = MaxHitpoints;
            TransformationID = 0;
            Stamina = 100;
            RemoveFlag((ulong)FlagsUpdate.Flags.FlashingName);
            RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
            RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
            if (EntityFlag == EntityFlag.Player)
                Owner.Send(new MapStatus() { BaseID = Owner.Map.BaseID, ID = Owner.Map.ID, Status = Database.MapsTable.MapInformations[Owner.Map.ID].Status });
            if (EntityFlag == Game.EntityFlag.Player)
            {
                Owner.ReviveStamp = Time32.Now;
                Owner.Entity.AutoRev = 0;
                Owner.Attackable = false;
            }
        }
        public void DropRandomStuff(Entity KillerName)
        {
            if (Revora.Game.Features.KingdomCompetitionField.InField(this)) return;
            if (Revora.Game.Features.ElitePkEvent.InEvent(this) || Revora.Game.Features.LocalPkWar.InEvent(this) || Revora.Game.Features.CouplePkEvent.InEvent(this)) return;
            if (Revora.Game.Features.ExpeditionEvent.InEvent(this))
            {
                Revora.Game.Features.ExpeditionEvent.OnDeath(Owner);
                return;
            }
            if (Revora.Game.Features.LastManStandingSystem.InEvent(this))
            {
                Revora.Game.Features.LastManStandingSystem.OnDeath(Owner);
                return;
            }
            if (Revora.Game.Features.NobleWarSystem.InEvent(this))
            {
                Revora.Game.Features.NobleWarSystem.OnDeath(Owner);
                return;
            }
            if (Revora.Game.Features.RelicRushSystem.InEvent(this))
            {
                Revora.Game.Features.RelicRushSystem.OnDeath(Owner);
                return;
            }
            if (Revora.Game.Features.WaveDefenseSystem.InEvent(this))
            {
                Revora.Game.Features.WaveDefenseSystem.OnDeath(Owner);
                return;
            }
            if (Revora.Game.Features.BeastTrialSystem.InEvent(this))
            {
                Revora.Game.Features.BeastTrialSystem.OnDeath(Owner);
                return;
            }
            if (Revora.Game.Features.BossRushSystem.InEvent(this))
            {
                Revora.Game.Features.BossRushSystem.OnDeath(Owner);
                return;
            }
            if (Revora.Game.Features.DuelPitSystem.InEvent(this))
            {
                Revora.Game.Features.DuelPitSystem.OnDeath(Owner);
                return;
            }
            if (Revora.Game.Features.SteedRaceSystem.InEvent(this))
            {
                Revora.Game.Features.SteedRaceSystem.OnDeath(Owner);
                return;
            }
            if (Owner.Inventory.Count > 0)
            {
                var array = Owner.Inventory.Objects.ToArray();
                uint count = (uint)(array.Length / 4);
                byte startfrom = (byte)Kernel.Random.Next((int)count);
                for (int c = 0; c < count; c++)
                {
                    int index = c + startfrom;
                    if (array[index] != null && !Revora.Game.Features.AbyssTrialWorld.ProtectDeathItem(this,array[index].ID))
                    {
                        if (PKPoints > 99)
                        {
                            if (array[index].Lock == 0)
                            {
                                if (array[index].UnlockEnd > DateTime.Now.AddDays(1))
                                {
                                    if (!array[index].Bound && !array[index].Inscribed && array[index].ID != 723753)
                                    {
                                        if (!array[index].Suspicious && array[index].Lock != 1 && array[index].ID != 723755 && array[index].ID != 723767 && array[index].ID != 723772)
                                        {
                                            int rate = 20;
                                            rate = (rate + KillerName.Grabber);
                                            if (Kernel.Rate(rate) && array[index].ID != 723774 && array[index].ID != 723776)
                                            {
                                                var Item = array[index];
                                                if (Item.ID >= 729960 && Item.ID <= 729970) return;
                                                Item.Lock = 0;
                                                var infos = ConquerItemInformation.BaseInformations[Item.ID];
                                                ushort x = X, y = Y;
                                                Game.Map Map = Kernel.Maps[MapID];
                                                if (Map.SelectCoordonates(ref x, ref y))
                                                {
                                                    FloorItem floorItem = new FloorItem(true);
                                                    Owner.Inventory.Remove(Item, Enums.ItemUse.Remove);
                                                    floorItem.Item = Item;
                                                    floorItem.ValueType = FloorItem.FloorValueType.Item;
                                                    floorItem.ItemID = Item.ID;
                                                    floorItem.MapID = MapID;
                                                    floorItem.MapObjType = MapObjectType.Item;
                                                    floorItem.X = x;
                                                    floorItem.Y = y;
                                                    floorItem.Type = FloorItem.Drop;
                                                    floorItem.OnFloor = Time32.Now;
                                                    floorItem.ItemColor = floorItem.Item.Color;
                                                    floorItem.UID = FloorItem.FloorUID.Next;
                                                    while (Map.Npcs.ContainsKey(floorItem.UID))
                                                        floorItem.UID = FloorItem.FloorUID.Next;
                                                    Map.AddFloorItem(floorItem);
                                                    Owner.SendScreenSpawn(floorItem, true);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (PKPoints > 99)
            {
                if (KillerName.EntityFlag == EntityFlag.Player)
                {
                    Kernel.SendWorldMessage(new Message(Name + " has been captured by " + KillerName.Name + " and sent to jail! The world is now safer!", Message.System), Server.GamePool);
                    Teleport(6000, 50, 50);
                   
                }
                else
                {
                    Kernel.SendWorldMessage(new Message(Name + " He has been captured and sent to jail! The world is now safer!", Message.System), Server.GamePool);
                    Teleport(6000, 50, 50);
                    
                }
            }
        }
        public void Die(UInt32 killer)
        {
            if (Features.ArenaBots.TryDie(this, killer)) return;
            if (Features.GuildPkBanner.InBattle(this)) {
                Client.GameState player;
                if (Kernel.GamePool.TryGetValue(killer,out player)) Die(player.Entity);
                return;
            }
            if (EntityFlag == EntityFlag.Player) { Revora.Game.Features.MagicDefenderProtection.Cancel(Owner); Revora.Game.Features.WarriorStance.Cancel(Owner); }
            if (Revora.Game.Features.CityGuardSystem.IsCityGuard(this)
                || Revora.Game.Features.LivingWorldSystem.IsAdventurer(this))
            {
                Entity found = null;
                Client.GameState owner;
                if (Kernel.GamePool.TryGetValue(killer, out owner) && owner != null)
                    found = owner.Entity;
                Die(found);
                return;
            }
            #region Absolution
            if (EntityFlag == EntityFlag.Player)
            {
                if (Owner.Spells.ContainsKey(14410))
                {
                    ISkill ClientSpell;
                    if (Owner.Spells.TryGetValue(14410, out ClientSpell))
                    {
                        if (ClientSpell.GetColdTime <= 0)
                        {
                            if (Class / 10 == 5)
                            {
                                var Absolution = SpellTable.SpellInformations[ClientSpell.ID][ClientSpell.Level];
                                RunesSystem.HandleRedBlueRunesSkills(null, Owner.Entity, null, Absolution, X, Y);
                            }
                        }
                    }
                }
            }
            #endregion

            if (EntityFlag == EntityFlag.Player)
            {
                Owner.XPCount = 0;
                if (Owner.Booth != null)
                {
                    Owner.Booth.Remove();
                    Owner.Booth = null;
                }
            }
            Killed = true;
            Hitpoints = 0;
            DeathStamp = Time32.Now;
            ToxicFogLeft = 0;
            if (Companion)
            {
                if (Hitpoints < 1)
                {
                    Hitpoints = 0;
                    AddFlag((ulong)FlagsUpdate.Flags.Ghost, StatusFlagsBigVector32.PermanentFlag, true);
                    AddFlag((ulong)FlagsUpdate.Flags.Dead, StatusFlagsBigVector32.PermanentFlag, true);
                    AddFlag((ulong)FlagsUpdate.Flags.FadeAway, StatusFlagsBigVector32.PermanentFlag, true);
                    RunesSystem.RemoveRunesSkills(Owner.Entity);
                    Attack attack = new Attack(true);
                    attack.Attacked = UID;
                    attack.AttackType = Attack.Kill;
                    attack.X = X;
                    attack.Y = Y;
                    MonsterInfo.SendScreen(attack);
                    this.RemoveFlag(18014398509481984ul);
                    Owner.Pet.RemovePet(pettype);
                    Owner.Map.RemoveEntity(this);
                    Owner.Companion = null;
                    if (Owner.Entity.MyClones.ContainsKey(UID))
                    {
                        Owner.Entity.MyClones.Remove(UID);
                        return;
                    }
                }
            }
            if (EntityFlag == Game.EntityFlag.Player)
            {
                this.RemoveFlag(18014398509481984ul);
            }
            if (EntityFlag == EntityFlag.Player)
            {
                #region Check for XP
                if (ContainsFlag((ulong)FlagsUpdate.Flags.XPList))
                    Update((byte)FlagsUpdate.DataType.XPLIST, 0, false);
                #endregion
                if (Constants.PKFreeMaps.Contains(MapID))
                    goto Over;
                Over:
                ClearFlags();
                AddFlag((ulong)FlagsUpdate.Flags.Dead, StatusFlagsBigVector32.PermanentFlag, true);
                AddFlag((ulong)FlagsUpdate.Flags.Ghost, StatusFlagsBigVector32.PermanentFlag, true);
                IsDefensiveStance = false;
                BlockShieldCheck = false;
                if (ContainsFlag((ulong)FlagsUpdate.Flags.AuroraLotus))
                {
                    Energy = 0;
                    Lotus((uint)Energy, FlagsUpdate.AuroraLotus);
                }
                if (ContainsFlag((ulong)FlagsUpdate.Flags.FlameLotus))
                {
                    Energy = 0;
                    Lotus((uint)Energy, FlagsUpdate.FlameLotus);
                }
                if (Cloud != null)
                {
                    Cloud.DisposeCloud(Owner);
                }
                Attack attack = new Attack(true);
                attack.AttackType = Attack.Kill;
                attack.X = X;
                attack.Y = Y;
                attack.Attacked = UID;
                attack.Attacker = killer;
                attack.Damage = 0;
                Owner.SendScreen(attack, true);
                GodlyShield = false;

                if (Body == 2005)
                {
                    TransformationID = 99;
                }
                else if (Body == 1006)
                {
                    TransformationID = 98;
                }

                SendCurrentMapStatus();

                Owner.EndQualifier();
                if (Features.ExpeditionEvent.InEvent(this)) Features.ExpeditionEvent.OnDeath(Owner);
            }
            else
            {
                Kernel.Maps[MapID].Floor[X, Y, MapObjType, this] = true;
            }
            if (EntityFlag == EntityFlag.Player)
            {
                if (Revora.Game.Features.LastManStandingSystem.InEvent(this))
                    Revora.Game.Features.LastManStandingSystem.OnDeath(Owner);
                if (Revora.Game.Features.NobleWarSystem.InEvent(this))
                    Revora.Game.Features.NobleWarSystem.OnDeath(Owner);
                if (Revora.Game.Features.RelicRushSystem.InEvent(this))
                    Revora.Game.Features.RelicRushSystem.OnDeath(Owner);
                if (Revora.Game.Features.WaveDefenseSystem.InEvent(this))
                    Revora.Game.Features.WaveDefenseSystem.OnDeath(Owner);
                if (Revora.Game.Features.BeastTrialSystem.InEvent(this))
                    Revora.Game.Features.BeastTrialSystem.OnDeath(Owner);
                if (Revora.Game.Features.BossRushSystem.InEvent(this))
                    Revora.Game.Features.BossRushSystem.OnDeath(Owner);
                if (Revora.Game.Features.DuelPitSystem.InEvent(this))
                    Revora.Game.Features.DuelPitSystem.OnDeath(Owner);
                if (Revora.Game.Features.SteedRaceSystem.InEvent(this))
                    Revora.Game.Features.SteedRaceSystem.OnDeath(Owner);
                Revora.Game.Features.ElitePkEvent.OnDeath(Owner);
                Revora.Game.Features.MoonBoxQuest.OnDeath(Owner);
                Revora.Game.Features.PowerArenaEvent.OnDeath(Owner);
                Revora.Game.Features.SkyPassQuest.OnDeath(Owner);
                Revora.Game.Features.SnakeIslandsQuest.OnDeath(Owner);
                Revora.Game.Features.LabyrinthQuest.OnDeath(Owner);
                Revora.Game.Features.DisCityQuest.OnDeath(Owner);
                if (OnDeath != null) OnDeath(this);
            }
            return;
        }

        public Entity Killer;
        public int SKypoint;
        public ushort Postionsky;
        public byte AutoRev = 0;
        public static Dictionary<uint, GameState> SkyFight = new Dictionary<uint, GameState>(500);
        public SafeDictionary<uint, PkExploits> PkExplorer = new SafeDictionary<uint, PkExploits>();
        public void SendScore(GameState client)
        {
            for (uint x = 1; x < 9; x++)
            {
                string Mesage = "";
                foreach (GameState clients in SkyFight.Values)
                {
                    if (clients.Entity.Postionsky == x)
                    {
                        Mesage = "No." + x + " " + clients.Entity.Name + ": Points: " + clients.Entity.SKypoint + "";
                    }
                }
                Message msg = new Message(Mesage, System.Drawing.Color.Red, x == 1 ? Message.FirstRightCorner : Message.ContinueRightCorner);
                client.Send(msg);
            }
        }
        public void CalculateRank()
        {
            Dictionary<uint, int> ToIndex = new Dictionary<uint, int>();
            uint CurKey = 0; int Rank = 1;
            for (short x = 0; x < SkyFight.Count; x++)
            {
                if (Rank == 9)
                {
                    break;
                }
                int Value = 0;
                foreach (uint K in SkyFight.Keys)
                {
                    if (SkyFight[K].Entity.SKypoint >= Value && !ToIndex.ContainsKey(K))
                    {
                        Value = SkyFight[K].Entity.SKypoint; CurKey = K;
                    }
                }
                if (!ToIndex.ContainsKey(CurKey))
                {
                    ToIndex.Add(CurKey, Value);
                }
                if (SkyFight.ContainsKey(CurKey))
                {
                    SkyFight[CurKey].Entity.Postionsky = (ushort)Rank;
                }
                Rank++;
            }
        }
        public void Die(Entity killer)
        {
            if (Features.ArenaBots.TryDie(this, killer == null ? 0u : killer.UID)) return;
            if (Revora.Game.Features.KingdomCompetitionField.TryDie(this, killer)) return;
            if (EntityFlag == EntityFlag.Player) { Revora.Game.Features.MagicDefenderProtection.Cancel(Owner); Revora.Game.Features.WarriorStance.Cancel(Owner); }
            if (Revora.Game.Features.EliminationRescueWorld.TryDie(this, killer)) return;
            if (Revora.Game.Features.EliminationDenWorld.TryDie(this, killer)) return;
            if (Revora.Game.Features.AncientDevilBossWorld.TryDie(this, killer)) return;
            if (Revora.Game.Features.AncientDevilWorld.TryDie(this, killer)) return;
            if (Revora.Game.Features.AbyssTrialWorld.TryDie(this, killer)) return;
            if (Revora.Game.Features.WarriorEpicCombat.TryDie(this, killer)) return;
            if (Revora.Game.Features.SpunGoldCombat.TryDie(this, killer)) return;
            if (Revora.Game.Features.PowerBookCombat.TryDie(this, killer)) return;
            if (Revora.Game.Features.DesertTreasureEncounter.TryDie(this, killer)) return;
            if (Revora.Game.Features.DesertExpeditionEncounters.TryDie(this, killer)) return;
            if (Revora.Game.Features.DefenseTalismanEncounters.TryDie(this, killer)) return;
            if (Revora.Game.Features.AttackTalismanEncounters.TryDie(this, killer)) return;
            if (Revora.Game.Features.TempestWingsEncounters.TryDie(this, killer)) return;
            if (Revora.Game.Features.FamilyFeudEncounters.TryDie(this, killer)) return;
            if (Revora.Game.Features.GhostCityCombat.TryDie(this, killer)) return;
            if (Revora.Game.Features.FranticMonkeysCombat.TryDie(this, killer)) return;
            if (Revora.Game.Features.FranticMonkeysApes.TryDie(this, killer)) return;
            if (Revora.Game.Features.SoulGeneralPhantom.TryDie(this, killer)) return;
            if (Revora.Game.Features.TrojanEpicCombat.TryDie(this, killer)) return;
            if (Revora.Game.Features.SwordMasterEncounter.TryDie(this, killer)) return;
            if (Revora.Game.Features.PurpleBansheeEncounter.TryDie(this, killer)) return;
            if (Revora.Game.Features.SnowBansheeEncounter.TryDie(this, killer)) return;
            if (Revora.Game.Features.ExorcismWorld.TryDie(this, killer)) return;
            if (Revora.Game.Features.TeratoEncounter.TryDie(this, killer)) return;
            if (Revora.Game.Features.DragonSlayerHunt.TryDie(this, killer)) return;
            if (Revora.Game.Features.PowerArenaEvent.TryDie(this, killer)) return;
            if (Revora.Game.Features.DisCityQuest.TryDie(this, killer)) return;
            if (Revora.Game.Features.SkyPassQuest.TryDie(this, killer)) return;
            if (Revora.Game.Features.SnakeIslandsQuest.TryDie(this, killer)) return;
            if (Revora.Game.Features.LabyrinthQuest.TryDie(this, killer)) return;
            if (Features.MoonBoxQuest.TryDie(this, killer)) return;
            if (Features.HeartEncounter.TryDie(this, killer)) return;
            if (Features.PlanetEncounter.TryDie(this, killer)) return;
            if (Features.DemonExterminators.TryDie(this, killer)) return;
            if (Features.DailyWorldBosses.Die(this, killer)) return;
            if (Features.ExpeditionEvent.Die(this, killer)) return;
            Map map;
            if (MapID == Features.PotencyHunt.MapId && Kernel.Maps.TryGetValue(MapID, out map)
                && map.PotencyPopulation != null && map.PotencyPopulation.Die(this, killer)) return;
            if (Features.GuildBeastEvent.TryDie(this,killer)) return;
            Features.WildMonsterVictory.Die(this,killer);
        }

        internal void DieCore(Entity killer) { DieCoreImpl(killer,false); }
        internal void DieWildCore(Entity killer) { DieCoreImpl(killer,true); }
        private void DieCoreImpl(Entity killer,bool unarmedCredit)
        {
            if(EntityFlag!=EntityFlag.Monster){DieCoreOwned(killer,unarmedCredit);return;}
            if(!System.Threading.Monitor.TryEnter(MonsterLifeSyncRoot))return;
            try { if(!Dead)DieCoreOwned(killer,unarmedCredit); }
            finally { System.Threading.Monitor.Exit(MonsterLifeSyncRoot); }
        }
        private void DieCoreOwned(Entity killer,bool unarmedCredit)
        {
            var defeatCredit = Features.PlayerDefeatCredit.Capture(this, killer);
            if (Revora.Game.Features.CityGuardSystem.IsCityGuard(this))
            {
                _hitpoints = 0;
                CharBuffer._Hitpoints = 0;
                AddFlag((ulong)FlagsUpdate.Flags.Dead, StatusFlagsBigVector32.PermanentFlag, true);
                Revora.Game.Features.CityGuardSystem.OnGuardKilled(this, killer);
                return;
            }
            if (Revora.Game.Features.LivingWorldSystem.IsAdventurer(this))
            {
                _hitpoints = 0;
                CharBuffer._Hitpoints = 0;
                AddFlag((ulong)FlagsUpdate.Flags.Dead, StatusFlagsBigVector32.PermanentFlag, true);
                Revora.Game.Features.LivingWorldSystem.OnAdventurerKilled(this, killer);
                return;
            }
            #region Absolution
            if (EntityFlag == EntityFlag.Player)
            {
                if (Owner.Spells.ContainsKey(14410))
                {
                    ISkill ClientSpell;
                    if (Owner.Spells.TryGetValue(14410, out ClientSpell))
                    {
                        if (ClientSpell.GetColdTime <= 0)
                        {
                            if (Class / 10 == 5)
                            {
                                var Absolution = SpellTable.SpellInformations[ClientSpell.ID][ClientSpell.Level];
                                RunesSystem.HandleRedBlueRunesSkills(null, Owner.Entity, null, Absolution, X, Y);
                                return;
                            }
                        }
                    }
                }
            }
            #endregion
            #region Slayer
            if (killer.EntityFlag == EntityFlag.Player)
            {
                if (killer.Owner.Spells.ContainsKey(14440))
                {
                    var s = killer.Owner.Spells[14440];
                    var Slayer = SpellTable.SpellInformations[s.ID][s.Level];
                    if (killer.EntityFlag == EntityFlag.Player && EntityFlag == EntityFlag.Player)
                        if (Slayer != null && killer.Owner.Spells.ContainsKey(14440))
                            if (killer.Class / 10 == 7)
                                RunesSystem.HandleRedBlueRunesSkills(null, killer, Owner.Entity, Slayer, killer.X, killer.Y);
                }
            }
            #endregion
            if (Features.GuildPkBanner.TryDefeat(this,killer)) return;
            #region Relic
            if (EntityFlag == EntityFlag.Player)
            {
                var relic = Owner.Equipment.TryGetItem(ConquerItem.Relics);
                if (!Owner.Equipment.Free(13))
                {
                    if (relic != null)
                    {
                        Relics.HandleRelicDurability(Owner, relic);
                    }
                }
            }
            #endregion

            #region PK Group
            if (killer.MapID == 16414)
            {
                foreach (GameState clients in Kernel.GamePool.Values)
                {
                    if (clients.Entity.MapID == 16414)
                    {
                        clients.Entity.SendScoreLAstTeam(clients);
                    }
                }
            }
            #endregion
            #region SkyFight
            if (MapID == 6412 && killer.MapID == 6412 && Game.Entity.SkyFightFlase)
            {
                killer.SKypoint++;
                AddFlag((ulong)FlagsUpdate.Flags.Fly, 10, true);
                AddFlag((ulong)FlagsUpdate.Flags.Freeze, 10, true);
                FlyTime = 10;
                FrozenTime = 10;
                FlyStamp = Time32.Now;
                FrozenStamp = Time32.Now;
                GameCharacterUpdates update = new GameCharacterUpdates(true);
                update.UID = UID;
                update.Add(GameCharacterUpdates.Freeze, 0, 10);
                Owner.SendScreen(update, true);
                if (!Entity.SkyFight.ContainsKey(killer.UID))
                    Entity.SkyFight.Add(killer.UID, killer.Owner);
                CalculateRank();
                foreach (GameState current in Kernel.GamePool.Values)
                {
                    if (current.Entity.MapID == 6412)
                    {
                        SendScore(current);
                    }
                }
                return;
            }
            #endregion
            #region CaptureTheFlag
            if (MapID == CaptureTheFlag.MapID && Owner != null)
                Server.Thread.CTF.Killed(Owner, killer == null ? null : killer.Owner);
            #endregion
            #region DieGuildMessage
            if (killer.EntityFlag == EntityFlag.Player && EntityFlag == EntityFlag.Player)
            {
                if (Owner.Guild != null && killer.Owner.Guild != null && killer.GuildID != GuildID && !Constants.PKFreeMaps.Contains(MapID)
                    && !Revora.Game.Features.AbyssTrialWorld.SuppressCrime(killer,this))
                {
                    if (Owner.Guild.pkp_donation >= 3)
                    {
                        Owner.Guild.pkp_donation -= 3;
                    }
                    killer.Owner.Guild.pkp_donation += 3;
                    killer.Owner.Send(new Message(("You have killed the enemy guild " + Owner.Guild.Name + "`s " + Owner.AsMember.Rank + " " + Owner.AsMember.Name + " and received 0 EXP and 3 PK Donation!"), Message.System));
                    Owner.Send(new Message(("You have been killed by the enemy guild " + killer.Owner.Guild.Name + "`s " + killer.Owner.AsMember.Rank + " " + killer.Owner.AsMember.Name + " and lost 0 EXP and 3 PK Donation!"), Message.System));
                }
            }
            #endregion
            #region V.P.W


            if (killer.MapID == 2014)
            {
                if (killer.MapID == 2014)
                {
                    killer.Owner.Vippoints += 1;
                    if (killer.Owner.Vippoints >= 5)
                    {
                        Network.GamePackets.NpcReply npc = new Network.GamePackets.NpcReply(6, "Congratulations, You Have Now " + killer.Owner.Vippoints + "  Points you can claim your prize now!");
                        npc.OptionID = 255;
                        killer.Owner.Send(npc.ToArray());
                    }
                    else
                    {
                        Network.GamePackets.NpcReply npc = new Network.GamePackets.NpcReply(6, "You Have Now " + killer.Owner.Vippoints + "  Points Congratz you still need " + (5 - killer.Owner.Vippoints) + " more!");
                        npc.OptionID = 255;
                        killer.Owner.Send(npc.ToArray());
                    }
                }
            }

            #endregion
            #region PK Perfection

            if (killer.MapID == 40031)
            {
                killer.Owner.Entity.AdjustMoney((decimal)(100000000000));
                Owner.Send("You are strong and have just earned 100,000,000,000 gold.");
            }
            if (killer.MapID == 40032)
            {
                killer.Owner.Entity.AdjustMoney((decimal)(50000000000));
                Owner.Send("You are strong and have just earned 50,000,000,000 gold.");
            }
            if (killer.MapID == 40033)
            {
                killer.Owner.Entity.AdjustMoney((decimal)(20000000000));
                Owner.Send("You are strong and have just earned 20,000,000,000 gold.");
            }

            #endregion
            if (EntityFlag == EntityFlag.Player)
            {
                Owner.XPCount = 0;
                if (Owner.Booth != null)
                {
                    Owner.Booth.Remove();
                    Owner.Booth = null;
                }
            }
            // All cancelling death branches have returned. Persist the accepted
            // ordinary unarmed victory before applying irreversible death effects.
            if (unarmedCredit && !Features.WildMonsterVictory.Accept(this,killer)) return;
            if (EntityFlag==EntityFlag.Monster && !Features.XpKillLedger.Accept(this,killer)) return;
            if (EntityFlag==EntityFlag.Monster && !Features.GuildContestCombat.Accept(this,killer)) return;
            if (killer.CountKilling >= 100 && killer.HeavenBlessing > 0 && killer.BlessedHunting < 4578)
            {
                killer.CountKilling = 0;
                killer.BlessedHunting += 1;
            }
            else if (killer.BlessedHunting >= 4578)
            {
                killer.Owner.MessageBox("You have reached the maximum amount of available EXP bonus hinting. Would you like to claim it, now? ",
                (p) =>
                {
                    killer.Owner.IncreaseExperience(Revora.Game.Features.ExperienceMath.Scale(killer.Owner.ExpBall,killer.BlessedHunting,600), false);
                    killer.BlessedHunting = 0;
                    MentorPremio Premio = new MentorPremio();
                    Premio.Action = MentorPremio.Show;
                    Premio.OnlineTraining = killer.OnlineTraining;
                    Premio.BlessedHunting = killer.BlessedHunting;
                    killer.Owner.Send(Premio.ToArray());
                });
            }
            killer.CountKilling++;
            killer.KillCount++;
            killer.KillCount2++;
            Killer = killer;
            Hitpoints = 0;
            DieString();
            DeathStamp = Time32.Now;
            Features.MonsterKoCounter.AcceptedDeath(this, killer);
            Features.PlayerDefeatCredit.Accepted(defeatCredit);
            ToxicFogLeft = 0;
            if (Companion)
            {
                Hitpoints = 0;
                AddFlag((ulong)FlagsUpdate.Flags.Ghost, StatusFlagsBigVector32.PermanentFlag, true);
                AddFlag((ulong)FlagsUpdate.Flags.Dead, StatusFlagsBigVector32.PermanentFlag, true);
                AddFlag((ulong)FlagsUpdate.Flags.FadeAway, StatusFlagsBigVector32.PermanentFlag, true);
                Attack zattack = new Attack(true);
                zattack.Attacked = UID;
                zattack.AttackType = Attack.Kill;
                zattack.X = X;
                zattack.Y = Y;
                MonsterInfo.SendScreen(zattack);
                Owner.Map.RemoveEntity(this);
                Entity ownedClone;
                if (Owner.Entity.MyClones.TryGetValue(UID,out ownedClone) && ReferenceEquals(ownedClone,this))
                {
                    Owner.Entity.MyClones.Remove(UID);
                    return;
                }
                if (ReferenceEquals(Owner.Companion,this)) Owner.Companion = null;
                Pet.PetInfo ownedPet;
                if (Owner.Pet != null && Owner.Pet.Pets != null && Owner.Pet.Pets.TryGetValue(pettype,out ownedPet)
                    && ownedPet != null && ReferenceEquals(ownedPet.Entity,this)) Owner.Pet.RemovePet(pettype);
            }
            if (EntityFlag == EntityFlag.Player)
            {
                #region Check for XP
                if (ContainsFlag((ulong)FlagsUpdate.Flags.XPList))
                    Update((byte)FlagsUpdate.DataType.XPLIST, 0, false);
                #endregion
                if (killer.EntityFlag == EntityFlag.Player && !Revora.Game.Features.AbyssTrialWorld.SuppressCrime(killer,this))
                {
                    if (killer.Owner == null || killer.Owner.Fake
                        || Revora.Game.Features.CityGuardSystem.IsCityGuard(killer))
                        goto Over;
                    #region PKFreeMaps
                    if (Constants.PKFreeMaps.Contains(killer.MapID))
                        goto Over;
                    if (killer.Owner.Map != null && killer.Owner.Map.BaseID == 700)
                        goto Over;
                    #endregion
                    #region JiangHu
                    if (killer.PKMode == Enums.PKMode.Jiang && killer.JiangActive == true)
                    {
                        if (PKMode == Enums.PKMode.Jiang || JiangActive == true)
                        {
                            if (killer.MyJiang != null && MyJiang != null)
                            {
                                if ((killer.MyJiang.Talent >= 1 && killer.MyJiang.Talent <= 5) && (MyJiang.Talent >= 1 && MyJiang.Talent <= 5))
                                {
                                    #region CPs for Jiang Kills
                                    Features.PurseBalance.TransferResult jiang = Features.PurseBalance.TryTransferConquerPoints(Owner, killer.Owner, 1500000);
                                    if (jiang == Features.PurseBalance.TransferResult.Transferred)
                                    {
                                        Owner.Send("You lost 1,500,000 Cps for letting you kill in JiangHu.");
                                        killer.Owner.Send("You have earned 1,500,000 Cps for killing in JiangHu.");
                                    }
                                    else if (jiang == Features.PurseBalance.TransferResult.Insufficient)
                                    {
                                        killer.Owner.Send("This player has less than 1,500,000 Cps!");
                                    }
                                    else if (jiang == Features.PurseBalance.TransferResult.Overflow)
                                    {
                                        killer.Owner.Send("The JiangHu purse could not be paid. Nothing was taken.");
                                    }
                                    #endregion
                                    goto Over;
                                }
                            }
                        }
                    }
                    #endregion

                    if (Revora.Game.Features.CouplePkEvent.InEvent(this) || Revora.Game.Features.LocalPkWar.InEvent(this) || Revora.Game.Features.ElitePkEvent.InEvent(this)
                        || Revora.Game.Features.ExpeditionEvent.SuppressCrime(killer, this)
                        || Revora.Game.Features.RelicRushSystem.SuppressCrime(killer, this)
                        || Revora.Game.Features.WaveDefenseSystem.InEvent(this)
                        || Revora.Game.Features.BeastTrialSystem.InEvent(this)
                        || Revora.Game.Features.BossRushSystem.InEvent(this)
                        || Revora.Game.Features.DuelPitSystem.InEvent(this)
                        || Revora.Game.Features.LastManStandingSystem.InEvent(this))
                    {
                    }
                    else if (((killer.PKMode != Enums.PKMode.Jiang) && !ContainsFlag((ulong)FlagsUpdate.Flags.FlashingName)) && !ContainsFlag((ulong)FlagsUpdate.Flags.BlackName))
                    {
                        killer.AddFlag((ulong)FlagsUpdate.Flags.FlashingName,20,true);
                        if (killer.GuildID != 0)
                        {
                            if (killer.Owner.Guild.Enemy.ContainsKey(GuildID))
                            {
                                killer.ChangePkPoints(3);
                            }
                            else
                            {
                                if (!killer.Owner.Enemy.ContainsKey(UID))
                                    killer.ChangePkPoints(10);
                                else killer.ChangePkPoints(5);
                            }
                        }
                        else
                        {
                            if (!killer.Owner.Enemy.ContainsKey(UID))
                                killer.ChangePkPoints(10);
                            else killer.ChangePkPoints(5);
                        }
                        if (killer.EntityFlag == EntityFlag.Player)
                        {
                            if (EntityFlag == EntityFlag.Player)
                            {
                                PkExploits pk = new PkExploits();
                                if (!killer.PkExplorer.ContainsKey(UID))
                                {
                                    pk.UID = killer.UID;
                                    pk.killedUID = UID;
                                    pk.Name = Name;
                                    pk.Map = MapID;
                                    pk.Killing = 1;
                                    pk.BattlePower = (uint)BattlePower;
                                    pk.Time = DateTime.Now;
                                    PkExploitsTable.Insert(killer.Owner, pk);
                                }
                                else
                                {
                                    pk.UID = killer.UID;
                                    pk.killedUID = UID;
                                    pk.Name = Name;
                                    killer.PkExplorer[UID].Map = MapID;
                                    pk.Map = killer.PkExplorer[UID].Map;
                                    killer.PkExplorer[UID].Killing += 1;
                                    pk.Killing = killer.PkExplorer[UID].Killing;
                                    killer.PkExplorer[UID].BattlePower = (uint)BattlePower;
                                    pk.BattlePower = killer.PkExplorer[UID].BattlePower;
                                    pk.Time = DateTime.Now;
                                    PkExploitsTable.Update(killer.Owner, pk);
                                }
                                if (killer.HeavenBlessing == 0 && HeavenBlessing != 0)
                                {
                                    killer.CursedTime += 300;
                                    Chat.SendCursed(killer.CursedTime, killer.Owner);
                                    Owner.Send("Your luck had an effect! You didn't lose exp.");
                                }
                                if (ExpProtectionTime > 600)
                                {
                                    ExpProtectionTime -= 600;
                                    FlagsUpdate update = new FlagsUpdate(true);
                                    update.UID = UID;
                                    update.Append((byte)FlagsUpdate.DataType.ExpProtection, 0, ExpProtectionTime, 1, 0);
                                    Owner.Send(update.ToArray());
                                    killer.Owner.Inventory.Add(3002560, 0, 1);
                                    Owner.Send("Your protection had an effect! You didn't lose exp.");
                                }
                                if (InAutoHunt)
                                {
                                    AutoHunt AutoHunt = new AutoHunt();
                                    AutoHunt.Action = AutoHunt.Mode.KilledBy;
                                    AutoHunt.Unknown = 3329;
                                    AutoHunt.KilledName = killer.Name;
                                    AutoHunt.EXPGained = AutoHuntEXP;
                                    Owner.Send(AutoHunt.ToArray());
                                    AutoRevStamp = Time32.Now;
                                    AutoRev = 20;
                                }
                                Revora.Game.Features.CityGuardSystem.OnInnocentKill(killer, this);
                            }
                        }
                    }
                    if (killer.EntityFlag == EntityFlag.Player && EntityFlag == EntityFlag.Player)
                        Revora.Game.Features.CityGuardSystem.OnBlackKilled(killer, this);
                    Network.GamePackets.KnownPersons.AddEnemys(this.Owner, killer.Owner);
                }
                if (killer.PKMode != Enums.PKMode.Jiang)
                {
                    if (killer.EntityFlag == EntityFlag.Player)
                    {
                        this.DropRandomStuff(this.Killer);
                    }
                    else
                    {
                        this.DropRandomStuff(this.Killer);
                    }
                }
            }
        Over:
            Attack attack = new Attack(true);
            attack.Attacker = killer.UID;
            attack.Attacked = UID;
            attack.AttackType = Attack.Kill;
            attack.X = X;
            attack.Y = Y;

            if (EntityFlag == EntityFlag.Player)
            {
                if (Hitpoints < 1)
                {
                    Hitpoints = 0;
                    if (Owner != null) Owner.Mining = false;
                    ClearFlags();
                    AddFlag((ulong)FlagsUpdate.Flags.Dead, StatusFlagsBigVector32.PermanentFlag, false);
                    AddFlag((ulong)FlagsUpdate.Flags.Ghost, StatusFlagsBigVector32.PermanentFlag, false);
                    RunesSystem.RemoveRunesSkills(Owner.Entity);
                    IsDefensiveStance = false;
                    DefensiveStance = 0;
                    lianhuaranLeft = 0;
                    BlockShield = 0;
                    BlockShieldCheck = false;
                    if (ContainsFlag((ulong)FlagsUpdate.Flags.AuroraLotus))
                    {
                        Energy = 0;
                        Lotus((uint)Energy, FlagsUpdate.AuroraLotus);
                    }
                    if (ContainsFlag((ulong)FlagsUpdate.Flags.FlameLotus))
                    {
                        Energy = 0;
                        Lotus((uint)Energy, FlagsUpdate.FlameLotus);
                    }
                    if (Cloud != null)
                        Cloud.DisposeCloud(Owner);
                }
                MagicDefender = 0;
                BlockShield = 0;
                GodlyShield = false;
                SpiritFocus = false;
                if (MyClones.Count > 0)
                {
                    foreach (var item in MyClones.Values)
                    {
                        Owner.Map.RemoveEntity(item);
                        Data data = new Data(true);
                        data.UID = item.UID;
                        data.ID = Data.RemoveEntity;
                        item.MonsterInfo.SendScreen(data);
                    }
                    MyClones.Clear();
                }
                if (Body == 2005)
                    TransformationID = 99;
                else if (Body == 1006)
                    TransformationID = 98;
                Owner.SendScreen(attack, true);
                Owner.Send(new MapStatus()
                {
                    BaseID = Owner.Map.BaseID,
                    ID = Owner.Map.ID,
                    Status = MapsTable.MapInformations[Owner.Map.ID].Status,
                    Weather = MapsTable.MapInformations[Owner.Map.ID].Weather
                });
                Owner.EndQualifier();
            }
            else
            {
                if (!Companion && !IsDropped)
                {
                    Revora.Game.Features.DailySoulBall.OnDeath(this, killer);
                    Revora.Game.Features.AbyssTrialWorld.OnWildDeath(this, killer);
                    Revora.Game.Features.AnniversaryQuest.OnDeath(this, killer);
                    MonsterInfo.Drop(killer);
                }
                Kernel.Maps[MapID].Floor[X, Y, MapObjType, this] = true;
                if (killer.EntityFlag == EntityFlag.Player && killer.Owner != null && !killer.Owner.Fake)
                {
                    killer.Owner.IncreaseExperience(MaxHitpoints, true);
                    Revora.Game.Features.SoulTrialSystem.OnMonsterKill(killer.Owner, this);
                    Revora.Game.Features.PathHuntSystem.OnMonsterKill(killer.Owner, this);
                    Revora.Game.Features.AzurePillQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.ConnectorQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.SpunGoldQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.ArtisanFeatherQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.MysticRingQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.FranticMonkeysQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.SoulGeneralQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.MadManQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.TomboyQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.DesertTreasureQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.DesertExpeditionQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.AttackTalismanQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.FamilyFeudQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.HeroWingsQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.DesertBloodQuest.OnDeath(killer.Owner, this);
                    Revora.Game.Features.CourierLoopSystem.OnMonsterKill(killer.Owner, this);
                    if (killer.Owner.Team != null)
                    {
                        foreach (GameState teammate in killer.Owner.Team.Teammates)
                        {
                            if (Kernel.GetDistance(killer.X, killer.Y, teammate.Entity.X, teammate.Entity.Y) <= Constants.pScreenDistance)
                            {
                                if (killer.UID != teammate.Entity.UID)
                                {
                                    uint extraExperience = MaxHitpoints / 2;
                                    if (killer.Spouse == teammate.Entity.Name)
                                        extraExperience = MaxHitpoints * 2;
                                    byte TLevelN = teammate.Entity.Level;
                                    if (killer.Owner.Team.CanGetNoobExperience(teammate))
                                    {
                                        if (teammate.Entity.Level < 137)
                                        {
                                            extraExperience *= 2;
                                            teammate.IncreaseExperience(extraExperience, false, false);
                                            teammate.Send(Constants.NoobTeamExperience(extraExperience));
                                        }
                                    }
                                    else
                                    {
                                        if (teammate.Entity.Level < 137)
                                        {
                                            teammate.IncreaseExperience(extraExperience, false, false);
                                            teammate.Send(Constants.TeamExperience(extraExperience));
                                        }
                                    }
                                    byte TLevelNn = teammate.Entity.Level;
                                    byte newLevel = (byte)(TLevelNn - TLevelN);
                                    if (newLevel != 0)
                                    {
                                        if (TLevelN < 70)
                                        {
                                            for (int i = TLevelN; i < TLevelNn; i++)
                                            {
                                                var virtueLeader = teammate.Team.Teammates[0];
                                                virtueLeader.AddVirtuePoints((uint)(i * 3.83F));
                                                teammate.Team.SendMessage(new Message("Leader, " + virtueLeader.Entity.Name + ", has won " + (uint)(i * 3.83F) + " virtue points.", System.Drawing.Color.Red, Message.Team));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (killer.Level < 137)
                    {
                        ulong extraExp = Revora.Config.ApplyKillExperience(MaxHitpoints, killer.VIPLevel, killer.Gems, killer.DoubleExperienceTime, killer.HeavenBlessing, killer.Reborn, 0, killer.BattlePower);
                        killer.Owner.Send(Constants.ExtraExperience((uint)Math.Min(extraExp, uint.MaxValue)));
                    }
                    killer.Owner.XPCount++;
                    if (killer.HasLiveKoMode(Time32.Now))
                        killer.KOSpellTime++;
                }
            }
            if (EntityFlag == EntityFlag.Player)
            {
                if (Revora.Game.Features.LastManStandingSystem.InEvent(this))
                    Revora.Game.Features.LastManStandingSystem.OnDeath(Owner);
                if (Revora.Game.Features.NobleWarSystem.InEvent(this))
                    Revora.Game.Features.NobleWarSystem.OnDeath(Owner);
                if (Revora.Game.Features.RelicRushSystem.InEvent(this))
                    Revora.Game.Features.RelicRushSystem.OnDeath(Owner);
                if (Revora.Game.Features.WaveDefenseSystem.InEvent(this))
                    Revora.Game.Features.WaveDefenseSystem.OnDeath(Owner);
                if (Revora.Game.Features.BeastTrialSystem.InEvent(this))
                    Revora.Game.Features.BeastTrialSystem.OnDeath(Owner);
                if (Revora.Game.Features.BossRushSystem.InEvent(this))
                    Revora.Game.Features.BossRushSystem.OnDeath(Owner);
                if (Revora.Game.Features.DuelPitSystem.InEvent(this))
                    Revora.Game.Features.DuelPitSystem.OnDeath(Owner);
                if (Revora.Game.Features.SteedRaceSystem.InEvent(this))
                    Revora.Game.Features.SteedRaceSystem.OnDeath(Owner);
                Revora.Game.Features.ElitePkEvent.OnDeath(Owner);
                Revora.Game.Features.MoonBoxQuest.OnDeath(Owner);
                Revora.Game.Features.PowerArenaEvent.OnDeath(Owner);
                Revora.Game.Features.SkyPassQuest.OnDeath(Owner);
                Revora.Game.Features.SnakeIslandsQuest.OnDeath(Owner);
                Revora.Game.Features.LabyrinthQuest.OnDeath(Owner);
                Revora.Game.Features.DisCityQuest.OnDeath(Owner);
                if (OnDeath != null) OnDeath(this);
            }
            return;
        }
        public void RemoveMagicDefender()
        {
            Revora.Game.Features.MagicDefenderProtection.Cancel(Owner);
        }
        public void Update(byte type, uint value, uint secondvalue)
        {
            FlagsUpdate upd = new FlagsUpdate(true);
            upd.Append(type, value);
            upd.Append(type, secondvalue);
            upd.UID = UID;
            Owner.Send(upd);
        }
        public void Update(byte type, byte value, bool screen)
        {
            if (!SendUpdates)
                return;
            if (this.Owner == null)
                return;
            var update = new FlagsUpdate(true);
            this.update = update;
            update.UID = UID;
            update.Append(type, value);
            if (!screen)
                update.Send(Owner);
            else
                Owner.SendScreen(update, true);
        }
        public void Update(byte type, ushort value, bool screen)
        {
            if (!SendUpdates) return;
            var update = new FlagsUpdate(true);
            this.update = update;
            update.UID = UID;
            update.Append(type, value);
            if (!screen)
                update.Send(Owner as GameState);
            else (Owner as GameState).SendScreen(update, true);
        }
        public void Update(byte type, uint value, bool screen)
        {
            if (!SendUpdates) return;
            var update = new FlagsUpdate(true);
            this.update = update;
            update.UID = UID;
            update.Append(type, value);
            if (!screen)
                update.Send(Owner as GameState);
            else (Owner as GameState).SendScreen(update, true);
        }
        public void Update(byte type, ulong value, bool screen)
        {
            if (!SendUpdates) return;
            var update = new FlagsUpdate(true);
            this.update = update;
            update.UID = UID;
            update.Append(type, value);
            if (EntityFlag == EntityFlag.Player)
            {
                if (!screen)
                    update.Send(Owner as GameState);
                else (Owner as GameState).SendScreen(update, true);
            }
            else MonsterInfo.SendScreen(update);
        }
        public void Update(byte type, string value, bool screen)
        {
            if (!SendUpdates) return;
            _String update = new _String(true);
            update.UID = this.UID;
            update.Type = type;
            update.TextsCount = 1;
            update.Texts.Add(value);
            if (EntityFlag == EntityFlag.Player)
            {
                if (!screen)
                    update.Send(Owner as Client.GameState);
                else (Owner as Client.GameState).SendScreen(update, true);
            }
            else
            {
                MonsterInfo.SendScreen(update);
            }
        }
        public void Update(byte type, string value, uint UIDs, bool screen)
        {
            if (!SendUpdates) return;
            _String update = new _String(true);
            update.UID = UIDs;
            update.Type = type;
            update.TextsCount = 1;
            update.Texts.Add(value);
            if (EntityFlag == EntityFlag.Player)
            {
                if (!screen)
                    update.Send(Owner as Client.GameState);
                else (Owner as Client.GameState).SendScreen(update, true);
            }
            else
            {
                MonsterInfo.SendScreen(update);
            }
        }
        public void UpdateEffects(uint[] Flags, bool screen)
        {
            if (!SendUpdates)
                return;
            CharBuffer._StatusFlag = new ulong[7];
            for (int i = 0; i < Flags.Length; i++)
            {
                CharBuffer._StatusFlag[i] = Flags[i];
            }

            update = new FlagsUpdate(true);
            update.UID = UID;
            update.AppendFull(0x19, Flags);
            if (EntityFlag == EntityFlag.Player)
            {
                if (!screen)
                    update.Send(Owner as GameState);
                else (Owner as GameState).SendScreen(update, true);
            }
            else MonsterInfo.SendScreen(update);
        }
        private void UpdateDatabase(string column, byte value)
        {
            if (Owner != null && Owner.Fake) return;
            if (EntityFlag == Game.EntityFlag.Player && FullyLoaded && Game.Features.CharacterProgression.TrySet(Owner, column, value)) return;
            if (EntityFlag == Game.EntityFlag.Player)
                if (FullyLoaded)
                    new Database.MySqlCommand(Revora.Database.MySqlCommandType.UPDATE).Update("entities").Set(column, value).Where("UID", UID).Execute();

        }
        private void UpdateDatabase(string column, long value)
        {
            if (Owner != null && Owner.Fake) return;
            if (EntityFlag == Game.EntityFlag.Player && FullyLoaded && Game.Features.CharacterProgression.TrySet(Owner, column, value)) return;
            if (EntityFlag == Game.EntityFlag.Player)
                if (FullyLoaded)
                    new Database.MySqlCommand(Database.MySqlCommandType.UPDATE).Update("entities").Set(column, value).Where("UID", UID).Execute();

        }
        public void UpdateDatabase(string column, ulong value)
        {
            if (Owner != null && Owner.Fake) return;
            if (EntityFlag == Game.EntityFlag.Player && FullyLoaded && Game.Features.CharacterProgression.IsColumn(column)
                && Game.Features.CharacterProgression.TrySet(Owner, column, checked((long)value))) return;
            if (EntityFlag == Game.EntityFlag.Player)
                if (FullyLoaded)
                    new Database.MySqlCommand(Revora.Database.MySqlCommandType.UPDATE).Update("entities").Set(column, value).Where("UID", UID).Execute();

        }
        private void UpdateDatabase(string column, string value)
        {
            if (Owner != null && Owner.Fake) return;
            if (EntityFlag == Game.EntityFlag.Player)
                if (FullyLoaded)
                    new Database.MySqlCommand(Revora.Database.MySqlCommandType.UPDATE).Update("entities").Set(column, value).Where("UID", UID).Execute();

        }
        public void UpdateDatabase(string column, bool value)
        {
            if (Owner != null && Owner.Fake) return;
            if (EntityFlag == Game.EntityFlag.Player)
                if (FullyLoaded)
                    new Database.MySqlCommand(Revora.Database.MySqlCommandType.UPDATE).Update("entities").Set(column, value).Where("UID", UID).Execute();
        }
        public static sbyte[] XDir = new sbyte[] { 0, -1, -1, -1, 0, 1, 1, 1 };
        public static sbyte[] YDir = new sbyte[] { 1, 1, 0, -1, -1, -1, 0, 1 };
        public static sbyte[] XDir2 = new sbyte[] { 0, -2, -2, -2, 0, 2, 2, 2, -1, -2, -2, -1, 1, 2, 2, 1, -1, -2, -2, -1, 1, 2, 2, 1 };
        public static sbyte[] YDir2 = new sbyte[] { 2, 2, 0, -2, -2, -2, 0, 2, 2, 1, -1, -2, -2, -1, 1, 2, 2, 1, -1, -2, -2, -1, 1, 2 };
        public bool Move(Enums.ConquerAngle Direction, int teleport = 1)
        {
            if (EntityFlag == EntityFlag.Player && !Features.SteedRaceCourse.CanMove(Owner)) return false;
            if ((Revora.Game.Features.SnowBansheeEncounter.IsEncounter(this) || Revora.Game.Features.PurpleBansheeEncounter.IsEncounter(this)) || (EntityFlag == EntityFlag.Player && (Revora.Game.Features.SnowBansheeCombat.IsStunned(Owner) || Revora.Game.Features.PurpleBansheeCombat.IsStunned(Owner)))) return false;
            ushort _X = X, _Y = Y;
            Facing = Direction;
            int dir = ((int)Direction) % XDir.Length;
            sbyte xi = XDir[dir], yi = YDir[dir];
            _X = (ushort)(X + xi);
            _Y = (ushort)(Y + yi);
            Game.Map Map = null;
            if (Kernel.Maps.TryGetValue(MapID, out Map))
            {
                var objType = MapObjType;
                if (Map.Floor[_X, _Y, objType])
                {
                    if (objType == MapObjectType.Monster)
                    {
                        Map.Floor[_X, _Y, MapObjType] = false;
                        Map.Floor[X, Y, MapObjType] = true;
                    }
                    X = _X;
                    Y = _Y;
                    if (EntityFlag == EntityFlag.Player)
                        Game.Features.TravelProgress.Arrived(Owner, Map, _X, _Y, Game.Features.TravelProgress.Arrival.Walk);
                    return true;
                }
                else
                {
                    if (Mode == Enums.Mode.None)
                        if (EntityFlag != EntityFlag.Monster)
                            if (teleport == 1)
                                Teleport(MapID, X, Y);
                    return false;
                }
            }
            else
            {
                if (EntityFlag != EntityFlag.Monster)
                    Teleport(MapID, X, Y);
                else
                    return false;
            }
            return true;
        }
        public bool Move(Enums.ConquerAngle Direction, bool slide)
        {
            if (EntityFlag == EntityFlag.Player && !Features.SteedRaceCourse.CanMove(Owner)) return false;
            if ((Revora.Game.Features.SnowBansheeEncounter.IsEncounter(this) || Revora.Game.Features.PurpleBansheeEncounter.IsEncounter(this)) || (EntityFlag == EntityFlag.Player && (Revora.Game.Features.SnowBansheeCombat.IsStunned(Owner) || Revora.Game.Features.PurpleBansheeCombat.IsStunned(Owner)))) return false;
            ushort _X = X, _Y = Y;
            if (!slide)
                return Move((Enums.ConquerAngle)((byte)Direction % 8));
            int dir = ((int)Direction) % XDir2.Length;
            Facing = Direction;
            sbyte xi = XDir2[dir], yi = YDir2[dir];
            _X = (ushort)(X + xi);
            _Y = (ushort)(Y + yi);
            Map Map = null;
            if (Kernel.Maps.TryGetValue(MapID, out Map))
            {
                if (Map.Floor[_X, _Y, MapObjType])
                {
                    if (MapObjType == MapObjectType.Monster)
                    {
                        Map.Floor[_X, _Y, MapObjType] = false;
                        Map.Floor[X, Y, MapObjType] = true;
                    }
                    X = _X;
                    Y = _Y;
                    if (EntityFlag == EntityFlag.Player)
                        Game.Features.TravelProgress.Arrived(Owner, Map, _X, _Y, Game.Features.TravelProgress.Arrival.Walk);
                    return true;
                }
                else
                {
                    if (Mode == Enums.Mode.None)
                    {
                        if (EntityFlag != EntityFlag.Monster)
                            Teleport(MapID, X, Y);
                        else
                            return false;
                    }
                }
            }
            else
            {
                if (EntityFlag != EntityFlag.Monster)
                    Teleport(MapID, X, Y);
                else
                    return false;
            }
            return true;
        }
        public void SendSpawn(Client.GameState client)
        {
            SendSpawn(client, true);
        }
        private static void WriteProtoVarint(List<byte> body, uint value)
        {
            do
            {
                byte part = (byte)(value & 127);
                value >>= 7;
                body.Add(value == 0 ? part : (byte)(part | 128));
            } while (value != 0);
        }
        private static void WriteProtoField(List<byte> body, uint field, uint value)
        {
            WriteProtoVarint(body, field << 3);
            WriteProtoVarint(body, value);
        }
        private byte[] ToReviewed6711PlayerSpawn()
        {
            List<byte> body = new List<byte>(160);
            WriteProtoField(body, 1, (uint)CharBuffer._Mesh);
            WriteProtoField(body, 2, UID);
            WriteProtoField(body, 8, (uint)CharBuffer._Head);
            WriteProtoField(body, 9, (uint)CharBuffer._Garment);
            WriteProtoField(body, 10, (uint)CharBuffer._Armor);
            WriteProtoField(body, 11, (uint)CharBuffer._LeftWeapon);
            WriteProtoField(body, 12, (uint)CharBuffer._RightWeapon);
            if (CharBuffer._Wing != 0)
            {
                WriteProtoField(body, 17, (uint)CharBuffer._Wing);
                WriteProtoField(body, 18, (uint)CharBuffer._WingPlus);
            }
            WriteProtoField(body, 23, Hitpoints);
            WriteProtoField(body, 26, Level);
            WriteProtoField(body, 27, X);
            WriteProtoField(body, 28, Y);
            WriteProtoField(body, 29, HairStyle);
            WriteProtoField(body, 30, (byte)Facing);
            WriteProtoField(body, 31, 100);
            if (Features.ArenaActors.IsBot(Owner))
            {
                WriteProtoField(body, 70, FirstRebornClass);
                WriteProtoField(body, 71, SecondRebornClass);
                WriteProtoField(body, 72, Class);
            }
            if (CharBuffer._JiangHuActive != 0)
            {
                WriteProtoField(body, 76, (uint)CharBuffer._JiangHuTalen);
                WriteProtoField(body, 77, (uint)CharBuffer._JiangHuActive);
            }
            byte[] name = System.Text.Encoding.Default.GetBytes(_Name ?? "");
            WriteProtoVarint(body, (97u << 3) | 2);
            WriteProtoVarint(body, (uint)name.Length);
            for (int i = 0; i < name.Length; i++)
                body.Add(name[i]);
            int declared = body.Count + 4;
            byte[] frame = new byte[declared + 8];
            Writer.WriteUInt16((ushort)declared, 0, frame);
            Writer.WriteUInt16(10014, 2, frame);
            body.CopyTo(frame, 4);
            if (UID <= 1100008)
                Console.WriteLine("CityGuard spawn " + _Name + " uid=" + UID + " mesh=" + CharBuffer._Mesh + " " + X + "," + Y + " bytes=" + declared);
            return frame;
        }
        public void SendSpawn(Client.GameState client, bool checkScreen = true)
        {
            if (client == null || this.Invisable)
                return;
            if (Owner != null && Owner.Fake && EntityFlag == EntityFlag.Player)
            {
                if (client.Fake || client.Screen == null) return;
                if (client.Screen.Add(this) || !checkScreen)
                    client.Send(ToReviewed6711PlayerSpawn());
                return;
            }
            if (!this.Invisable)
            {
                if (client.Screen.Add(this) || !checkScreen)
                {
                    if (EntityFlag == Game.EntityFlag.Player)
                    {
                        if (client.InQualifier() && this.Owner.IsWatching()
                            || (this.SkillTeamWatchingElitePKMatch != null
                            || this.Owner.WatchingElitePKMatch != null)
                            || this.Invisable) return;
                        if (Owner.WatchingElitePKMatch != null) return;
                        if (Invisable) return;
                        if (this.Owner.IsFairy)
                        {
                            FairySpawn FS = new FairySpawn(true);
                            FS.SType = this.Owner.SType;
                            FS.FairyType = this.Owner.FairyType;
                            FS.UID = this.UID;
                            client.Send(FS);
                        }
                        Data generalData = new Data(true);
                        generalData.UID = client.Entity.UID;
                        generalData.ID = Data.AppearanceType;
                        generalData.dwParam = (uint)client.Entity.Appearance;
                        client.SendScreen(generalData, true);
                        client.Send(CharBuffer.ToConquerPacket(10014));
                    }
                    else
                    {
                        client.Send(CharBuffer.ToConquerPacket(10014));
                    }
                    if (EntityFlag == EntityFlag.Player)
                    {
                        if (Owner.Booth != null)
                        {
                            client.Send(Owner.Booth);
                            if (Owner.Booth.HawkMessage != null)
                                client.Send(Owner.Booth.HawkMessage);
                        }
                    }
                }
            }
        }
        public void Shift(ushort X, ushort Y, uint mapID, Interfaces.IPacket shift = null)
        {
            if (_mapid != mapID) return;
            if (EntityFlag == EntityFlag.Player)
            {
                if (!Database.MapsTable.MapInformations.ContainsKey(MapID)) return;
            }
            this.X = X;
            this.Y = Y;
            if (shift == null)
            {
                shift = new Network.GamePackets.Data(true)
                {
                    UID = UID,
                    ID = Network.GamePackets.Data.FlashStep,
                    dwParam = MapID,
                    wParam1 = X,
                    wParam2 = Y
                };
            }
            if (EntityFlag == EntityFlag.Player)
            {
                Owner.SendScreen(shift, true);
                Owner.Screen.Reload(shift);
            }
        }
        public void Shift(ushort X, ushort Y)
        {
            if (EntityFlag == EntityFlag.Player)
            {
                if (!Database.MapsTable.MapInformations.ContainsKey(MapID))
                    return;
                this.X = X;
                this.Y = Y;
                Network.GamePackets.Data Data = new Network.GamePackets.Data(true);
                Data.UID = UID;
                Data.ID = Network.GamePackets.Data.FlashStep;
                Data.dwParam = MapID;
                Data.wParam1 = X;
                Data.wParam2 = Y;
                Owner.SendScreen(Data, true);
                Owner.Screen.Reload(null);
            }
        }
        public bool fMove(Enums.ConquerAngle Direction, ref ushort _X, ref ushort _Y)
        {
            Facing = Direction;
            sbyte xi = 0, yi = 0;
            switch (Direction)
            {
                case Enums.ConquerAngle.North: xi = -1; yi = -1; break;
                case Enums.ConquerAngle.South: xi = 1; yi = 1; break;
                case Enums.ConquerAngle.East: xi = 1; yi = -1; break;
                case Enums.ConquerAngle.West: xi = -1; yi = 1; break;
                case Enums.ConquerAngle.NorthWest: xi = -1; break;
                case Enums.ConquerAngle.SouthWest: yi = 1; break;
                case Enums.ConquerAngle.NorthEast: yi = -1; break;
                case Enums.ConquerAngle.SouthEast: xi = 1; break;
            }
            _X = (ushort)(_X + xi);
            _Y = (ushort)(_Y + yi);
            if (EntityFlag == Game.EntityFlag.Player)
            {
                if (Owner.Map.Floor[_X, _Y, MapObjType, null])
                    return true;
                else
                    return false;
            }
            else
            {
                Game.Map Map = null;
                if (Kernel.Maps.TryGetValue(MapID, out Map))
                {
                    if (Map.Floor[_X, _Y, MapObjType, null])
                        return true;
                    else
                        return false;
                }
                return true;
            }
        }
        private void SendCurrentMapStatus()
        {
            if (Owner == null || Owner.Map == null) return;
            ushort lookup = Owner.Map.ID;
            if (!Database.MapsTable.MapInformations.ContainsKey(lookup))
                lookup = Owner.Map.BaseID;
            if (!Database.MapsTable.MapInformations.ContainsKey(lookup))
                return;
            Database.MapsTable.MapInformation info = Database.MapsTable.MapInformations[lookup];
            Owner.Send(new MapStatus() { BaseID = Owner.Map.BaseID, ID = Owner.Map.ID, Status = info.Status, Weather = info.Weather });
        }
        public void Teleport(ushort X, ushort Y)
        {
            if (!Revora.Game.Features.SnakeIslandsQuest.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.Housing.AllowTeleport(Owner, this.MapID)) return;
            if (!Revora.Game.Features.LabyrinthQuest.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.DisCityWorld.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.AncientDevilBossWorld.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.EliminationRescueWorld.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.EliminationDenWorld.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.AncientDevilWorld.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.ExorcismWorld.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.HeartEncounter.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.PlanetEncounter.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.WarriorEpicScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.PowerBookScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.FranticMonkeysScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.DefenseTalismanScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.AttackTalismanScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.TempestWingsScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.MadManScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.GhostCityScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.FamilyFeudScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.DesertBloodScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.TrojanEpicScenes.AllowTeleport(Owner, this.MapID, X, Y)) return;
            if (!Revora.Game.Features.LocalPkWar.AllowTeleport(Owner, this.MapID)) return;
            if (!Revora.Game.Features.CouplePkEvent.AllowTeleport(Owner, this.MapID)) return;
            if (EntityFlag == EntityFlag.Player)
            {
                if (Owner != null) Owner.Mining = false;
                if (MyClones.Count > 0)
                {
                    foreach (var item in MyClones.Values)
                    {
                        Owner.Map.RemoveEntity(item);
                        Data data = new Data(true);
                        data.UID = item.UID;
                        data.ID = Network.GamePackets.Data.RemoveEntity;
                        item.MonsterInfo.SendScreen(data);
                    }
                    MyClones.Clear();
                }
                if (!Database.MapsTable.MapInformations.ContainsKey(MapID) && !Owner.InQualifier())
                {
                    MapID = 1002;
                    X = 300;
                    Y = 278;
                }
                this.X = X;
                this.Y = Y;
                Map travelMap; Kernel.Maps.TryGetValue(this.MapID, out travelMap);
                Network.GamePackets.Data Data = new Network.GamePackets.Data(true);
                Data.UID = UID;
                Data.ID = Network.GamePackets.Data.Teleport;
                Data.dwParam = Database.MapsTable.MapInformations[MapID].BaseID;
                Data.wParam1 = X;
                Data.wParam2 = Y;
                Owner.Send(Data);
                Owner.Screen.Reload(Data);
                Owner.Screen.FullWipe();
                Owner.Screen.Reload(null);
                SendCurrentMapStatus();
                Game.Features.TravelProgress.Arrived(Owner, travelMap, X, Y, Game.Features.TravelProgress.Arrival.Teleport);
            }
        }
        public void SetLocation(ushort MapID, ushort X, ushort Y)
        {
            if (EntityFlag == EntityFlag.Player)
            {
                this.X = X;
                this.Y = Y;
                this.MapID = MapID;
            }
        }
        public void TeleportHouse(ushort MapID, ushort X, ushort Y)
        {
            if (!Revora.Game.Features.SnakeIslandsQuest.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.Housing.AllowTeleport(Owner, MapID)) return;
            if (!Revora.Game.Features.LabyrinthQuest.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.DisCityWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.AncientDevilBossWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.EliminationRescueWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.EliminationDenWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.AncientDevilWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.ExorcismWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.HeartEncounter.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.PlanetEncounter.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.WarriorEpicScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.PowerBookScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.FranticMonkeysScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.DefenseTalismanScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.AttackTalismanScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.TempestWingsScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.MadManScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.GhostCityScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.FamilyFeudScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.DesertBloodScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.TrojanEpicScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.LocalPkWar.AllowTeleport(Owner, MapID)) return;
            if (!Revora.Game.Features.CouplePkEvent.AllowTeleport(Owner, MapID)) return;

            if (EntityFlag == EntityFlag.Player)
            {
                if (Owner != null) Owner.Mining = false;
                if (MyClones.Count > 0)
                {
                    foreach (var item in MyClones.Values)
                    {
                        Owner.Map.RemoveEntity(item);
                        Data data = new Data(true);
                        data.UID = item.UID;
                        data.ID = Network.GamePackets.Data.RemoveEntity;
                        item.MonsterInfo.SendScreen(data);
                    }
                    MyClones.Clear();
                }
                if (!Database.MapsTable.MapInformations.ContainsKey(MapID) && Owner.QualifierGroup == null)
                {
                    MapID = 1002;
                    X = 300;
                    Y = 278;
                }
                if (EntityFlag == EntityFlag.Player)
                {
                    if (Owner.InQualifier())
                        if (MapID != 700 && MapID < 11000)
                            Owner.EndQualifier();
                    if (Owner.Entity.InAutoHunt)
                    {
                        AutoHunt AutoHunt = new AutoHunt();
                        AutoHunt.Action = AutoHunt.Mode.ChangedMap;
                        AutoHunt.EXPGained = Owner.Entity.AutoHuntEXP;
                        Owner.Send(AutoHunt.ToArray());

                        AutoHunt.Action = AutoHunt.Mode.EXPGained;
                        AutoHunt.EXPGained = Owner.Entity.AutoHuntEXP;
                        Owner.Send(AutoHunt.ToArray());

                        Owner.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.AutoHunting);
                        Owner.Entity.AutoHuntEXP = 0;
                        Owner.Entity.InAutoHunt = false;
                    }
                    if (Owner.Companion != null)
                    {
                        Owner.Map.RemoveEntity(Owner.Companion);
                        Data data = new Data(true);
                        data.UID = Owner.Companion.UID;
                        data.ID = Network.GamePackets.Data.RemoveEntity;
                        Owner.Companion.MonsterInfo.SendScreen(data);
                        Owner.Companion = null;
                    }
                }
                if (MapID == this.MapID)
                {
                    Teleport(X, Y);
                    return;
                }
                this.X = X;
                this.Y = Y;
                PX = 0;
                PY = 0;
                this.PreviousMapID = this.MapID;
                this.MapID = MapID;
                Map travelMap; Kernel.Maps.TryGetValue(MapID, out travelMap);
                Data Data = new Data(true);
                Data.UID = UID;
                Data.ID = Data.Teleport;
                Data.dwParam = Database.MapsTable.MapInformations[MapID].BaseID;
                Data.wParam1 = X;
                Data.wParam2 = Y;
                Owner.Send(Data);
                Owner.Screen.Reload(Data);
                SendCurrentMapStatus();
                Game.Features.TravelProgress.Arrived(Owner, travelMap, X, Y, Game.Features.TravelProgress.Arrival.Teleport);
                if (!Owner.Equipment.Free(12))
                    if (Owner.Map.ID == 1036 && Owner.Equipment.TryGetItem((byte)12).Plus < 6)
                        RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
            }
        }
        public void Teleport(ushort MapID, ushort X, ushort Y)
        {
            if (!Revora.Game.Features.SnakeIslandsQuest.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.Housing.AllowTeleport(Owner, MapID)) return;
            if (!Revora.Game.Features.LabyrinthQuest.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.DisCityWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.AncientDevilBossWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.EliminationRescueWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.EliminationDenWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.AncientDevilWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.ExorcismWorld.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.HeartEncounter.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.PlanetEncounter.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.WarriorEpicScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.PowerBookScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.FranticMonkeysScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.DefenseTalismanScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.AttackTalismanScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.TempestWingsScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.MadManScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.GhostCityScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.FamilyFeudScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.DesertBloodScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.TrojanEpicScenes.AllowTeleport(Owner, MapID, X, Y)) return;
            if (!Revora.Game.Features.LocalPkWar.AllowTeleport(Owner, MapID)) return;
            if (!Revora.Game.Features.CouplePkEvent.AllowTeleport(Owner, MapID)) return;
            if (!Revora.Game.Features.MoonBoxQuest.AllowTeleport(Owner, MapID)) return;
            if (!Revora.Game.Features.PowerArenaEvent.AllowTeleport(Owner, MapID)) return;
            if (EntityFlag == EntityFlag.Player)
            {
                if (Owner != null) Owner.Mining = false;
                Revora.Game.Features.ElitePkEvent.OnTeleport(Owner, MapID);
                Revora.Game.Features.ExpeditionEvent.OnTeleport(Owner, MapID);
                if (MyClones.Count > 0)
                {
                    foreach (var item in MyClones.Values)
                    {
                        Owner.Map.RemoveEntity(item);
                        Data data = new Data(true);
                        data.UID = item.UID;
                        data.ID = Network.GamePackets.Data.RemoveEntity;
                        item.MonsterInfo.SendScreen(data);
                    }
                    MyClones.Clear();
                }
                ushort baseID = 0;
                if (!Kernel.Maps.ContainsKey(MapID))
                {
                    if (!Database.MapsTable.MapInformations.ContainsKey(MapID) && Owner.QualifierGroup == null)
                    {
                        baseID = MapID = 1002;
                        X = 300;
                        Y = 278;
                    }
                    else
                    {
                        baseID = Database.MapsTable.MapInformations[MapID].BaseID;
                    }
                }
                else
                {
                    baseID = Kernel.Maps[MapID].BaseID;
                }
                if (EntityFlag == EntityFlag.Player)
                {
                    if (Owner.InQualifier())
                        if (MapID != 700 && MapID < 11000)
                            Owner.EndQualifier();
                    if (Owner.Entity.InAutoHunt)
                    {
                        AutoHunt AutoHunt = new AutoHunt();
                        AutoHunt.Action = AutoHunt.Mode.ChangedMap;
                        AutoHunt.EXPGained = Owner.Entity.AutoHuntEXP;
                        Owner.Send(AutoHunt.ToArray());

                        AutoHunt.Action = AutoHunt.Mode.EXPGained;
                        AutoHunt.EXPGained = Owner.Entity.AutoHuntEXP;
                        Owner.Send(AutoHunt.ToArray());

                        Owner.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.AutoHunting);
                        Owner.Entity.AutoHuntEXP = 0;
                        Owner.Entity.InAutoHunt = false;
                    }
                    if (Owner.Companion != null)
                    {
                        Owner.Map.RemoveEntity(Owner.Companion);
                        Data data = new Data(true);
                        data.UID = Owner.Companion.UID;
                        data.ID = Network.GamePackets.Data.RemoveEntity;
                        Owner.Companion.MonsterInfo.SendScreen(data);
                        Owner.Companion = null;
                    }
                }
                if (MapID == this.MapID)
                {
                    Teleport(X, Y);
                    return;
                }
                this.X = X;
                this.Y = Y;
                this.PreviousMapID = this.MapID;
                if (this.PreviousMapID == CaptureTheFlag.MapID && Server.Thread != null && Server.Thread.CTF != null)
                    Server.Thread.CTF.Leave(Owner);
                this.MapID = MapID;
                Map travelMap; Kernel.Maps.TryGetValue(MapID, out travelMap);
                Data Data = new Data(true);
                Data.UID = UID;
                Data.ID = Data.Teleport;
                Data.dwParam = baseID;
                Data.wParam1 = X;
                Data.wParam2 = Y;
                Owner.Send(Data);
                Owner.Screen.Reload(Data);
                Owner.Screen.FullWipe();
                Owner.Screen.Reload(null);
                SendCurrentMapStatus();
                Game.Features.TravelProgress.Arrived(Owner, travelMap, X, Y, Game.Features.TravelProgress.Arrival.Teleport);
                Owner.Entity.Action = Enums.ConquerAction.None;
                Owner.ReviveStamp = Time32.Now;
                Owner.Attackable = false;
                if (!Owner.Equipment.Free(12))
                    if (Owner.Map.ID == 1036 && Owner.Equipment.TryGetItem((byte)12).Plus < 6)
                        RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
            }
        }
        public ushort PrevX, PrevY;
        public void Teleport(ushort BaseID, ushort DynamicID, ushort X, ushort Y)
        {
            if (!Revora.Game.Features.SnakeIslandsQuest.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.Housing.AllowTeleport(Owner, DynamicID)) return;
            if (!Revora.Game.Features.LabyrinthQuest.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.DisCityWorld.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.AncientDevilBossWorld.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.EliminationRescueWorld.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.EliminationDenWorld.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.AncientDevilWorld.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.ExorcismWorld.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.HeartEncounter.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.PlanetEncounter.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.WarriorEpicScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.PowerBookScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.FranticMonkeysScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.DefenseTalismanScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.AttackTalismanScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.TempestWingsScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.MadManScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.GhostCityScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.FamilyFeudScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.DesertBloodScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.TrojanEpicScenes.AllowTeleport(Owner, DynamicID, X, Y)) return;
            if (!Revora.Game.Features.LocalPkWar.AllowTeleport(Owner, DynamicID)) return;
            if (!Revora.Game.Features.CouplePkEvent.AllowTeleport(Owner, DynamicID)) return;
            if (!Revora.Game.Features.MoonBoxQuest.AllowTeleport(Owner, DynamicID)) return;
            if (!Revora.Game.Features.PowerArenaEvent.AllowTeleport(Owner, DynamicID)) return;
            Revora.Game.Features.ElitePkEvent.OnTeleport(Owner, DynamicID);

            if (EntityFlag == EntityFlag.Player)
            {
                if (Owner != null) Owner.Mining = false;
                if (MyClones.Count > 0)
                {
                    foreach (var item in MyClones.Values)
                    {
                        Owner.Map.RemoveEntity(item);
                        Data data = new Data(true);
                        data.UID = item.UID;
                        data.ID = Network.GamePackets.Data.RemoveEntity;
                        item.MonsterInfo.SendScreen(data);
                    }
                    MyClones.Clear();
                }
                if (!DMaps.MapPaths.ContainsKey(BaseID)) return;
                if (Owner.InQualifier())
                    if (BaseID != 700 && BaseID < 11000)
                        Owner.EndQualifier();
                if (!Kernel.Maps.ContainsKey(DynamicID)) new Map(DynamicID, BaseID, DMaps.MapPaths[BaseID]);
                this.PrevX = this.X;
                this.PrevY = this.Y;
                this.X = X;
                this.Y = Y;
                this.PreviousMapID = this.MapID;
                this.MapID = DynamicID;
                Map travelMap; Kernel.Maps.TryGetValue(DynamicID, out travelMap);
                Data Data = new Data(true);
                Data.UID = UID;
                Data.ID = Data.Teleport;
                Data.dwParam = BaseID;
                Data.wParam1 = X;
                Data.wParam2 = Y;
                Owner.Send(Data);
                Owner.Screen.Reload(Data);
                Owner.Screen.FullWipe();
                Owner.Screen.Reload(null);
                Owner.Entity.Action = Enums.ConquerAction.None;
                Owner.ReviveStamp = Time32.Now;
                Owner.Attackable = false;
                if (Owner.ChampionGroup == null)
                    SendCurrentMapStatus();
                Game.Features.TravelProgress.Arrived(Owner, travelMap, X, Y, Game.Features.TravelProgress.Arrival.Teleport);
                if (!Owner.Equipment.Free(12))
                    if (Owner.Map.ID == 1036 && Owner.Equipment.TryGetItem((byte)12).Plus < 6)
                        RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
            }
        }
        public bool OnBladeFlurry()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.BladeFlurry);
        }
        public bool OnKOSpell()
        {
            return OnCyclone() || OnSuperman() || OnSuperCyclone() || OnDragonCyclone() || OnOblivion() || OnManiacDance() || OnOmnipotence();
        }
        public bool OnManiaticDance()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.ManiacDance);
        }
        public bool OnOmnipotence()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.Omnipotence);
        }
        public bool OnSuperCyclone()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.SuperCyclone);
        }
        public bool OnDragonCyclone()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.DragonCyclone);
        }
        public bool OnOblivion()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.Oblivion);
        }
        public bool OnCyclone()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.Cyclone);
        }
        public bool OnSuperman()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.Superman);
        }
        public bool OnFatalStrike()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.FatalStrike);
        }
        public bool OnScurvyBomb()
        {
            return ContainsFlag((ulong)FlagsUpdate.Flags.ScurvyBomb);
        }
        //public bool Encadenado()
        //{
        //    return ContainsFlag2((ulong)FlagsUpdate.Flags.SoulShackle);
        //}
        public void Untransform()
        {
            if (MapID == 1036 && TransformationTime == 3600) return;
            this.TransformationID = 0;
            double maxHP = TransformationMaxHP;
            double HP = Hitpoints;
            double point = HP / maxHP;
            Hitpoints = (uint)(MaxHitpoints * point);
            Update((byte)FlagsUpdate.DataType.MaxHitpoints, MaxHitpoints, false);
        }
        public byte[] WindowSpawn(uint type = 1)
        {
            CharBuffer.__WindowSpawn = type;
            var buffer = CharBuffer.ToConquerPacket(10014);
            CharBuffer.__WindowSpawn = 0;
            return buffer;
        }
        #endregion
        #region New Work
        public uint SecoundQuestPoint
        {
            get;
            set;
        }
        public bool SecoundQuestDone
        {
            get;
            set;
        }
        public uint Ascendio_mail = 1;
        public void AzureShieldPacket()
        {
            var Remain = AzureShieldStamp.AddSeconds(MagicShieldTime) - DateTime.Now;
            FlagsUpdate aupgrade = new FlagsUpdate(true);
            aupgrade.UID = UID;
            aupgrade.Append(49, 93, (uint)Remain.TotalSeconds, AzureShieldDefence, AzureShieldLevel);
            Owner.Send(aupgrade);
        }
        public uint AzureShieldDefence = 0;
        public int AzureShieldTime;
        public byte AzureShieldLevel = 0;
        public DateTime AzureShieldStamp = DateTime.Now;
        public ushort QuestID = 0;
        public bool OnDragonSwing;
        public bool OnPirateFaction, OnNinjaFaction;
        public ushort DragonSwingPower;
        public int DragonFuryTime;
        public void GiveSkills()
        {
            #region Trojan Spells
            if (Class >= 10 && Class <= 15)
                if (!Owner.Spells.ContainsKey(0x456))
                    Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 0x456 });
            #endregion
            #region Ninja Spells
            if (Class >= 50 && Class <= 55)
            {
                if (Level >= 15)
                {
                    if (!Owner.Spells.ContainsKey(12110))
                        Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 12110 });
                }
                else if (Level >= 20)
                {
                    if (!Owner.Spells.ContainsKey(11170))
                        Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 11170 });

                    if (!Owner.Spells.ContainsKey(11180))
                        Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 11180 });
                }
            }
            #endregion
            #region Warrior Spells
            if (Class >= 20 && Class <= 25)
                if (!Owner.Spells.ContainsKey(0x401))
                    Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 0x401 });
            #endregion
            #region Archer Spells
            if (Class >= 40 && Class <= 45)
                if (!Owner.Spells.ContainsKey(0x1f42))
                    Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 0x1f42 });
            #endregion
            #region Monk Spells
            if (Class >= 60 && Class <= 65)
                if (!Owner.Spells.ContainsKey(0x2896))
                    Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 0x2896 });
            #endregion Pirate Spells
            #region Pirate Spells
            if (Class >= 70 && Class <= 75)
                if (!Owner.Spells.ContainsKey(0x2b2a))
                    Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 0x2b2a });
            #endregion
            #region Taoist Spells
            if (Class > 100)
                if (!Owner.Spells.ContainsKey(0x3f2))
                    Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 0x3f2 });
            #endregion
            #region LeeLong Spells
            if (Class >= 80 && Class <= 85)
                if (!Owner.Spells.ContainsKey(0x3f2))
                    Owner.AddSpell(new Network.GamePackets.Spell(true) { ID = 0x3f2 });
            #endregion
        }
        public uint
            Kill_Reward_Point, King_Reward_Point, Arena_Reward_Point,
            Quest_Reward_Point, Credit_Reward_Point, Online_Reward_Point;
        public ushort
            King_Reward_Clamied, Arena_Reward_Clamied, Kill_Reward_Clamied,
            Online_Reward_Clamied, Credit_Reward_Clamied, Quest_Reward_Clamied;
        public void SendSysMesage(string mesaj)
        {
            byte[] buffer = new Message(mesaj, Message.System).ToArray();
            this.Owner.Send(buffer);
        }
        public Game.JiangHu MyJiang;
        public static bool MonthlyPKWar;
        public bool AllowToAttack = false;
        public bool DiceKing = false;
        public bool SendRouletteAutoInvite = false;
        private uint _TransHitRate;
        public uint HitRate;
        public ushort frequency
        {
            get
            {
                if (EntityFlag == Game.EntityFlag.Monster) return 0;
                ushort Freq = 0;
                foreach (ConquerItem i in Owner.Equipment.Objects)
                {
                    if (i == null)
                    {
                        continue;
                    }
                    Database.ConquerItemInformation dbi = new Database.ConquerItemInformation(i.ID, i.Plus);
                    if (dbi != null)
                    {
                        Freq += dbi.BaseInformation.AgilityItem;
                    }
                }
                return (ushort)((Freq + Agility));
            }
        }
        public byte TransformationHitRate
        {
            get
            {
                if (ContainsFlag((ulong)FlagsUpdate.Flags.StarOfAccuracy))
                    return (byte)(_TransHitRate * HitRate);
                return (byte)_TransHitRate;
            }
            set { _TransHitRate = value; }
        }
        public ConcurrentDictionary<ushort, Game.Attacking.FloorSpell.ClientFloorSpells> FloorSpells = new ConcurrentDictionary<ushort, Game.Attacking.FloorSpell.ClientFloorSpells>();
        public JiangHu.AttackFlag AttackJiang = JiangHu.AttackFlag.None;
        public uint OnMoveNpc;
        public static bool
        SkyFightFlase = false;
        public static object[] name;
        public const uint
        NobilityMapBase = 700,
        ClassPKMapBase = 1730;
        public bool
        SwordSoul = false,
        BansheeSpirit = false,
        SetLocations = false,
        ClaimedFirstCredit = false,
        ClaimedElitePk = false,
        ClaimedTeamPK = false,
        ClaimedSTeamPK = false,
        IncreaseFinalMDamage = false,
        IncreaseFinalPDamage = false,
        IncreaseFinalMAttack = false,
        IncreaseFinalPAttack = false,
        IncreaseImunity = false,
        IncreaseAntiBreack = false,
        IncreasePStrike = false,
        IncreaseBreack = false,
        IncreaseAttribute = false,
        GodlyShield = false,
        VoteSystem = false,
        InSkillPk = false,
        ClaimedExp = false,
        ActivenessDone = false;
        public bool CliamSeed;
        public Features.Tournaments.TeamElitePk.Match SkillTeamWatchingElitePKMatch;
        public Entity SpookDetails;
        public Map SpookMap;
        public bool OnCheckGuard;
        public byte CheckGuardSec;
        public uint GuardUId;
        // plunder
        public bool OnCheckSaqueo;
        public byte CheckSaqueoSec;
        public uint SaqueoUId;
        public bool RelasesTheSaqueo;
        // plunder
        public bool SpiritBeads;
        public uint spiritBeadsCount;
        public byte ThirlSpookLevel;
        public bool RelasesTheSoul;
        public bool MagniloQuest;
        public bool HoringThe;
        public bool EvreyThink;
        public bool ThirllingSpook;
        public bool ThirllingSpookPrice;
        public bool RareMaterials;
        public string PriceName;
        public string MaterialsName;
        public byte Priceamount;
        public byte PriceQuestID;
        public byte PriceMaterialsID;
        public string PriceMonsterName;
        public string PriceLoction;
        public byte PriceTimes;
        public byte MaterialsTimes;
        public byte MaterialsStar;
        public uint PriceItemId;
        public uint MaterialstemId;
        public byte RelasesGuard;
        public byte RelasesSaqueo;
        public uint ActivenessPoint;
        public byte
        VIPTime, HourTime, LotteryTime, DailyTime,
        QualifierTime, ChampionTime,
        HorseRacingTime, TreasureTime, EnlightenmentTime,
        TeamQualifierTime, ChiStudyTime, JiangHuTime, CrystalsTime;
        public static void SendReload(GameState client)
        {
            client.SendScreenSpawn(client.Entity, true);
            client.Screen.FullWipe();
            client.Screen.Reload(null);
        }
        public uint kilid = 0;
        public ulong AutoHuntEXP;
        public bool InAutoHunt;
        public Achievement MyAchievement;
        public uint WeaponColor
        {
            get { return (uint)CharBuffer._BattlePower; }
            set { CharBuffer._BattlePower = value; }
        }
        public int Merchant;
        public bool _TradeOn = false;
        public bool TradeOn //Jheisson
        {
            get
            {
                if (Name.Contains("[GM]"))
                    return _TradeOn;
                else return true;
            }
            set { _TradeOn = value; }
        }
        public void SoulShackleRemover()
        {
            ShackleTime = 0;
            RemoveFlag((ulong)FlagsUpdate.Flags.SoulShackle);
        }
        public Game.Features.Flowers MyFlowers;
        public int Shock = 0;
        public uint StrResID;
        public byte AncestorHonored = 0;
        public byte GuiHeavenBlessing = 0;
        public bool NotHavePassword = false;
        public byte DemonQuest = 0;
        public uint
        TwinCity, BirdIsland, DesertCity, ApeMountain,
        MysticCastle, PhoenixCastle, FrozenGrotto,
        OnlineTraining, BlessedHunting, KillArena1 = 0,
        KillArena2 = 0, KillArena3 = 0, KillArena4 = 0,
        KillArena5 = 0, KillArena6 = 0, KillArena7 = 0,
        KillArena8 = 0;
        public byte CountKilling;
        //public ushort ChangeNameTrue;
        public Action<Entity> OnDeath;
        public static void SendWorldMessage(string Messages)
        {
            Kernel.SendWorldMessage(new Message(Messages, System.Drawing.Color.Yellow, Message.World), Server.GamePool);
        }
        #endregion

        public uint LuckyWheel;
        internal void AddStorageItem(WrapTypeS.StorageType storageType)
        {
            throw new NotImplementedException();
        }

        public uint lacb { get; set; }
    }
    public class StatusFlagsBigVector32 : BitVector32
    {
        public const int PermanentFlag = 60 * 60 * 24 * 30;
        public class Flag
        {
            public int Secounds = 0;
            public int InvokerSecouds = 0;
            public int Key;
            public Time32 TimerInvoker = new Time32();
            public DateTime Timer = new DateTime();
            public bool RemoveOnDead = false;
            public Flag(int _flag, int _Secounds, bool _removeondead, int _InvokerSecouds)
            {
                Secounds = _Secounds;
                Key = _flag;
                Timer = DateTime.Now;
                RemoveOnDead = _removeondead;
                InvokerSecouds = _InvokerSecouds;
                if (InvokerSecouds != 0)
                    TimerInvoker = Time32.Now;
            }

            public bool Expire(DateTime Now)
            {
                if (Now >= Timer.AddSeconds(Secounds))
                    return true;
                return false;
            }
            public bool CheckInvoke(Time32 Now)
            {
                if (InvokerSecouds == 0)
                    return true;
                else
                {
                    if (Now >= TimerInvoker.AddSeconds(InvokerSecouds))
                    {
                        TimerInvoker = Time32.Now;
                        return true;
                    }
                    return false;
                }
            }
        }
        public ConcurrentDictionary<int, Flag> ArrayFlags;
        private Flag[] Array = new Flag[0];
        private bool Update = false;
        public StatusFlagsBigVector32(int Size)
            : base(Size)
        {
            ArrayFlags = new ConcurrentDictionary<int, Flag>();
        }
        public bool TryAdd(int flag, int Secounds, bool RemoveOnDead, int InvokerSecouds)
        {
            if (!ArrayFlags.ContainsKey(flag))
            {
                ArrayFlags.TryAdd(flag, new Flag(flag, Secounds, RemoveOnDead, InvokerSecouds));
                Add(flag);
                Update = true;
                return true;
            }
            return false;
        }
        public bool TryAdd(int flag)
        {
            if (!ArrayFlags.ContainsKey(flag))
            {
                ArrayFlags.TryAdd(flag, new Flag(flag, 0, false, 0));
                Add(flag);
                Update = true;
                return true;
            }
            return false;
        }
        public bool UpdateXP(int flag, int Secounds, bool increase)
        {
            Flag FlagClass;
            if (ArrayFlags.TryGetValue(flag, out FlagClass))
            {
                if (increase)
                    FlagClass.Secounds += Secounds;
                else
                    FlagClass.Secounds -= Secounds;
                return true;
            }
            return false;
        }
        public bool UpdateFlag(int flag, int Secounds, bool SetNewTimer, int MaxSecounds)
        {
            Flag FlagClass;
            if (ArrayFlags.TryGetValue(flag, out FlagClass))
            {
                if (SetNewTimer)
                {
                    FlagClass.Timer = DateTime.Now;
                    FlagClass.Secounds = Secounds;
                }
                else
                {
                    if (FlagClass.Timer.AddSeconds(FlagClass.Secounds) > DateTime.Now.AddSeconds(MaxSecounds))
                    {
                        FlagClass.Timer = DateTime.Now;
                        FlagClass.Secounds = MaxSecounds;
                    }
                    else
                        FlagClass.Secounds += Secounds;
                }
                return true;
            }
            return false;
        }
        public bool TryRemove(int flag)
        {
            Flag FlagClass;
            if (ArrayFlags.TryRemove(flag, out FlagClass))
            {
                Remove(flag);

                Update = true;

                return true;
            }
            return false;
        }
        public bool InLife(int flag, DateTime Now64)
        {
            Flag FlagClass;
            if (ArrayFlags.TryGetValue(flag, out FlagClass))
            {
                return FlagClass.Expire(Now64);
            }
            return false;
        }
        public bool CheckInvoke(int flag, Time32 Now64)
        {
            Flag FlagClass;
            if (ArrayFlags.TryGetValue(flag, out FlagClass))
            {
                return FlagClass.CheckInvoke(Now64);
            }
            return false;
        }
        public bool ContainFlag(int flag)
        {
            return ArrayFlags.ContainsKey(flag);
        }
        
        public void GetClear()
        {
            List<int> remove = new List<int>();

            foreach (var item in GetFlags())
            {
                if (item.RemoveOnDead)
                    remove.Add(item.Key);
            }
            foreach (var item in remove)
                TryRemove(item);
        }
        public Flag[] GetFlags()
        {
            try
            {
                if (Update)
                {
                    Array = ArrayFlags.Values.ToArray();
                    Update = false;
                }
                return Array;
            }
            catch (Exception)
            {
                return new Flag[0];
            }

        }
    }
}
