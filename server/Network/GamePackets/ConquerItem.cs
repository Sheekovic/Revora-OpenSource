using System;
using Revora.Game;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class ConquerItem : Writer, Interfaces.IPacket 
    {
        public const byte
        Inventory = 0,
        Head = 1,
        Necklace = 2,
        Armor = 3,
        RightWeapon = 4,
        LeftWeapon = 5,
        Ring = 6,
        Bottle = 7,
        Boots = 8,
        Garment = 9,
        Fan = 10,
        Tower = 11,
        Steed = 12,
        Relics = 13,
        RightWeaponAccessory = 15,
        LeftWeaponAccessory = 16,
        SteedArmor = 17,
        SteedCrop = 18,
        Wing = 19,
        AlternateHead = 21,
        AlternateNecklace = 22,
        AlternateArmor = 23,
        AlternateRightWeapon = 24,
        AlternateLeftWeapon = 25,
        AlternateRing = 26,
        AlternateBottle = 27,
        AlternateBoots = 28,
        AlternateGarment = 29,
        Remove = 255;
        internal Revora.Game.Features.RefineryProgress.State RefineryState;
        public bool InWardrobe;
        public const uint GoldPrize = 2100075;
        public bool IsWorn = false;
        public byte[] Buffer;
        public static Counter ItemUID = new Counter(0);
        public bool IsWrap;
        public uint CrossID { get; set; }
        private UInt32 mRefineItem = 0;
        private DateTime mRefineryTime;
        private ulong suspiciousStart = 0, unlockEnd = 0;
        private bool unlocking = false;
        private uint warehouse = 0;
        public Dictionary<uint, string> Agate_map
        {
            get;
            set;
        }
        public DateTime RefineryStarted
        {
            get;
            set;
        }
        public Auction AuctionInfo
        {
            get;
            set;
        } 
        public byte Days
        {
            get;
            set;
        }
        private int _Active;
        public int Active
        {
            get
            {
                return _Active;
            }
            set
            {
                _Active = value;
            }
        }
        public DateTime DayStamp
        {
            get;
            set;
        }
        public ushort MaxStackSize
        {
            get;
            set;
        }
        public bool MobDropped
        {
            get;
            set;
        }
        public bool StatsLoaded
        {
            get;
            set;
        }
        public string Agate
        {
            get;
            set;
        }
        public ItemAdding.Purification_ Purification;
        public ItemAdding.Refinery_ ExtraEffect;
        
        public ConquerItem(bool Create)
        {
            if (Create)
            {
                Agate_map = new Dictionary<uint, string>(10);
                Buffer = new byte[165];
                WriteUInt16(157, 0, Buffer);
                WriteUInt16(1008, 2, Buffer);
                Mode = Enums.ItemMode.Default;
                StatsLoaded = false;
            }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint ID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set
            {
                if (value == 0 && ID != 0)
                    throw new Exception("Invalid ID for an Item. Please check the stack trace to find the cause.");
                WriteUInt32(value, 8, Buffer);
            }
        }
        public ushort Durability
        {
            get { return BitConverter.ToUInt16(Buffer, 12); }
            set { WriteUInt16(value, 12, Buffer); }
        }
        public ushort MaximDurability
        {
            get { return BitConverter.ToUInt16(Buffer, 14); }
            set { WriteUInt16(value, 14, Buffer); }
        }
        public Enums.ItemMode Mode
        {
            get { return (Enums.ItemMode)BitConverter.ToUInt16(Buffer, 16); }
            set { WriteUInt16((ushort)value, 16, Buffer); }
        }
        public byte Position
        {
            get { return Buffer[18]; }
            set { Buffer[18] = value; }
        }
        public uint Warehouse
        {
            get { return warehouse; }
            set { warehouse = value; }
        }
        public uint SocketProgress
        {
            get { return BitConverter.ToUInt32(Buffer, 19); }
            set { WriteUInt32(value, 19, Buffer); }
        }
        public Enums.Gem SocketOne
        {
            get { return (Enums.Gem)Buffer[23]; }
            set { Buffer[23] = (byte)value; }
        }
        public Enums.Gem SocketTwo
        {
            get { return (Enums.Gem)Buffer[24]; }
            set { Buffer[24] = (byte)value; }
        }
        public uint RuneSkill
        {
            get { return BitConverter.ToUInt32(Buffer, 25); }
            set { WriteUInt32(value, 25, Buffer); }
        }
        public Enums.ItemEffect Effect
        {
            get { return (Enums.ItemEffect)BitConverter.ToUInt16(Buffer, 28); }
            set { WriteUInt16((ushort)value, 28, Buffer); }
        }
        public byte Plus
        {
            get { return Buffer[30]; }
            set { Buffer[30] = value; }
        }
        public byte Bless
        {
            get { return Buffer[31]; }
            set { Buffer[31] = value; }
        }
        public bool Bound
        {
            get { return Buffer[32] == 0 ? false : true; }
            set { Buffer[32] = (byte)(value ? 1 : 0); }
        }
        public byte Enchant
        {
            get { return Buffer[33]; }
            set { Buffer[33] = value; }
        }
        public byte NextRed
        {
            get { return Buffer[31]; }
            set { Buffer[31] = value; }
        }
        public byte NextBlue
        {
            get { return Buffer[33]; }
            set { Buffer[33] = value; }
        }
        public byte NextGreen
        {
            get { return (byte)BitConverter.ToUInt32(Buffer, 34); }
            set { WriteUInt32(value, 34, Buffer); }
        }
        public bool Suspicious
        {
            get { return BitConverter.ToUInt16(Buffer, 38) != 0; }
            set { WriteUInt16((ushort)(value ? 1 : 0), 38, Buffer); }
        }
        public byte Lock
        {
            get { return Buffer[40]; }
            set { Buffer[40] = value; }
        }
        public Enums.Color Color
        {
            get;
            set;
        }
        public uint PlusProgress
        {
            get { return BitConverter.ToUInt32(Buffer, 42); }
            set { WriteUInt32(value, 42, Buffer); }
        }
        public bool Inscribed
        {
            get
            {
                return (BitConverter.ToUInt16(Buffer, 46) == 1);
            }
            set
            {
                WriteUInt16(value ? ((byte)1) : ((byte)0), 46, Buffer);
            }
        }
        public uint TimeLeftInMinutes
        {
            get { return BitConverter.ToUInt32(Buffer, 50); }
            set { WriteUInt32(value, 50, Buffer); }
        }
        public ushort StackSize
        {
            get { return BitConverter.ToUInt16(Buffer, 58); }
            set { WriteUInt16(value, 58, Buffer); }
        }
        public DateTime SuspiciousStart
        {
            get { return DateTime.FromBinary((long)suspiciousStart); }
            set { suspiciousStart = (ulong)value.Ticks; }
        }
        public DateTime UnlockEnd
        {
            get { return DateTime.FromBinary((long)unlockEnd); }
            set { unlockEnd = (ulong)value.Ticks; }
        }
        public uint RunesEXP
        {
            get { return System.BitConverter.ToUInt32(Buffer, 123); }
            set { WriteUInt32(value, 123, Buffer); }
        }
        public uint RelicAttrib1
        {
            get { return System.BitConverter.ToUInt32(Buffer, 127); }
            set { WriteUInt32(value, 127, Buffer); }
        }
        public uint RelicAttrib2
        {
            get { return System.BitConverter.ToUInt32(Buffer, 131); }
            set { WriteUInt32(value, 131, Buffer); }
        }
        public uint RelicAttrib3
        {
            get { return System.BitConverter.ToUInt32(Buffer, 135); }
            set { WriteUInt32(value, 135, Buffer); }
        }
        public uint RelicAttrib4
        {
            get { return System.BitConverter.ToUInt32(Buffer, 139); }
            set { WriteUInt32(value, 139, Buffer); }
        }
        public uint RelicAttrib5
        {
            get { return System.BitConverter.ToUInt32(Buffer, 143); }
            set { WriteUInt32(value, 143, Buffer); }
        }
        public bool Unlocking
        {
            get { return unlocking; }
            set { unlocking = value; }
        }
        public UInt32 RefineItem
        {
            get { return mRefineItem; }
            set { mRefineItem = value; }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            throw new NotImplementedException();
        }
        public Double GemBonus(Byte type)
        {
            Double bonus = 0;
            if (SocketOne != Enums.Gem.NoSocket)
            {
                if ((Int32)SocketOne / 10 == type)
                    bonus += ItemSocket.GetGemBonus((Byte)SocketOne);
                if (SocketTwo != Enums.Gem.NoSocket)
                {
                    if ((Int32)SocketTwo / 10 == type)
                        bonus += ItemSocket.GetGemBonus((Byte)SocketTwo);
                }
            }
            return bonus;
        }
        public override string ToString()
        {
            return ID.ToString() + "#"
         + Durability.ToString() + "#"
         + MaximDurability.ToString() + "#"
         + Position.ToString() + "#"
         + SocketProgress.ToString() + "#"
         + ((byte)SocketOne).ToString() + "#"
         + ((byte)SocketTwo).ToString() + "#"
         + ((ushort)Effect).ToString() + "#"
         + Plus.ToString() + "#"
         + Bless.ToString() + "#"
         + (Bound ? "1" : "0") + "#"
         + Enchant.ToString() + "#"
         + (Suspicious ? "1" : "0") + "#"
         + Lock.ToString() + "#"
         + (Unlocking ? "1" : "0") + "#"
         + PlusProgress.ToString() + "#"
         + (Inscribed ? "1" : "0") + "#"
         + Days.ToString() + "#"
         + DayStamp.ToString() + "#"
         + suspiciousStart.ToString() + "#"
         + unlockEnd.ToString();
        }
        public string ToLog()
        {
            return "UID: " + UID.ToString() + " | "
         + "ID: " + ID.ToString() + " | "
         + "Plus: " + Plus.ToString();
        }
        public void SendExtras(Client.GameState client)
        {
            if (client == null) return;
            if (RefineItem != 0)
            {
                Refinery.RefineryItem rI = RefineStats;
                if (rI != null)
                {
                    client.Send(new ItemSoul()
                    {
                        ID = rI.Identifier,
                        Identifier = UID,
                        Level = rI.Level,
                        Mode = ItemSoul.Types.Refine,
                        Percent = rI.Percent,
                        Type = 1,
                        Time = RefinerySeconds
                    });
                }
            }
            ItemAdding add = new ItemAdding(true);
            if (Purification.Available)
                add.Append(Purification);
            if (ExtraEffect.Available)
                add.Append(ExtraEffect);
            if (Purification.Available || ExtraEffect.Available)
                client.Send(add);
            if (Lock == 2)
            {
                ItemLock itemLock = new ItemLock(true);
                itemLock.UID = UID;
                itemLock.ID = ItemLock.UnlockDate;
                itemLock.dwParam = (uint)(UnlockEnd.Year * 10000 + UnlockEnd.Month * 100 + UnlockEnd.Day);
                client.Send(itemLock);
            }
        }
        public void SendAgate(Client.GameState client)
        {
            byte[] packet = new byte[8 + 32 + 48 * Agate_map.Count + 48];
            WriteUInt16((ushort)(packet.Length - 8), 0, packet);
            WriteUInt16(2110, 2, packet);
            WriteUInt32(this.UID, 8, packet);
            WriteUInt32((byte)Agate_map.Count, 12, packet);
            WriteUInt32((byte)Agate_map.Count, 16, packet);
            WriteUInt32(Durability, 24, packet);
            WriteUInt32((byte)Agate_map.Count, 28, packet);
            if (Agate_map.Count > 0)
            {
                int position_next = 32;
                uint x = 0;
                for (; x < Agate_map.Count; x++)
                {
                    WriteUInt32(x, position_next, packet);
                    position_next += 4;
                    WriteUInt16(ushort.Parse(Agate_map[x].Split('~')[0].ToString()), position_next, packet);
                    position_next += 4;
                    WriteUInt16(ushort.Parse(Agate_map[x].Split('~')[1].ToString()), position_next, packet);
                    position_next += 4;
                    WriteUInt16(ushort.Parse(Agate_map[x].Split('~')[2].ToString()), position_next, packet);
                    position_next += 36;
                }
            }
            else
            {
            }
            client.Send(packet);
        }
        public void Send(Client.GameState client)
        {
            if (client == null) return;
            if (ID == 300000)
            {
                uint G = SocketProgress & 0xFF;
                uint B = (SocketProgress >> 8) & 0xFF;
                uint R = (SocketProgress >> 16) & 0xFF;
                if (NextRed == 0 && NextBlue == 0 && NextGreen == 0)
                {
                    NextRed = (byte)R;
                    NextBlue = (byte)B;
                    NextGreen = (byte)G;
                    Database.ConquerItemTable.UpdateNextSteedColor(this);
                }
            }
            if (Days > 0)
            {
                if (DateTime.Now >= DayStamp.AddDays(Days))
                {
                    Database.ConquerItemTable.DeleteItem(UID);
                    Database.ConquerItemTable.RemoveItem(UID);
                    client.Send(Database.ConquerItemInformation.BaseInformations[ID].Name + " is Has Expired");
                }
                TimeSpan Remain = DayStamp.AddDays(Days) - DateTime.Now;
                TimeLeftInMinutes = (uint)Remain.TotalSeconds;
            }
            client.Send(Buffer);
            if (RefineItem != 0)
            {
                Refinery.RefineryItem rI = RefineStats;
                if (rI != null)
                {
                    client.Send(new ItemSoul()
                    {
                        ID = rI.Identifier,
                        Identifier = UID,
                        Level = rI.Level,
                        Mode = ItemSoul.Types.Refine,
                        Percent = rI.Percent,
                        Type = 1,
                        Time = RefinerySeconds
                    });
                }
            }
            ItemAdding add = new ItemAdding(true);
            if (Purification.Available)
                add.Append(Purification);
            if (ExtraEffect.Available)
                add.Append(ExtraEffect);
            if (Purification.Available || ExtraEffect.Available)
                client.Send(add);
            if (Lock == 2 && (Mode == Enums.ItemMode.Default || Mode == Enums.ItemMode.Update))
            {
                ItemLock itemLock = new ItemLock(true);
                itemLock.UID = UID;
                itemLock.ID = ItemLock.UnlockDate;
                itemLock.dwParam = (uint)(UnlockEnd.Year * 10000 + UnlockEnd.Month * 100 + UnlockEnd.Day);
                client.Send(itemLock);
            }
            Mode = Enums.ItemMode.Default;
        }
        public static Boolean isRune(UInt32 itemid)
        {
            if (itemid >= 729960 && itemid <= 729970)
                return true;
            return false;
        }
        public ushort Vigor
        {
            get;
            set;
        }
        public short BattlePower
        {
            get
            {
                short potBase = 0;
                byte Quality = (byte)(ID % 10);
                if (Quality >= 5)
                    potBase += (byte)(Quality - 5);
                potBase += Plus;
                if (SocketOne != Enums.Gem.NoSocket) potBase++;
                if (SocketTwo != Enums.Gem.NoSocket) potBase++;
                if (((byte)SocketOne) % 10 == 3) potBase++;
                if (((byte)SocketTwo) % 10 == 3) potBase++;
                if (ID / 1000 == 421 || ItemHandler.IsTwoHand(ID))
                    potBase *= 2;
                return potBase;
            }
        }
        public override int GetHashCode()
        {
            return (int)this.UID;
        }
        public override bool Equals(object obj)
        {
            return (obj as ConquerItem).UID == GetHashCode();
        }
        public Refinery.RefineryItem RefineStats
        {
            get
            {
                Refinery.RefineryItem i = null;
                uint type = RefineryState == null ? RefineItem : RefineryState.Current(Revora.Game.Features.RefineryProgress.Clock().ToLocalTime()).EffectID;
                Kernel.DatabaseRefinery.TryGetValue(type, out i);
                return i;
            }
        }
        internal uint RefinerySeconds {
            get {
                var now = Revora.Game.Features.RefineryProgress.Clock().ToLocalTime();
                if (RefineryState != null) {
                    var active = RefineryState.Current(now);
                    return active.EffectDuration == 0 ? 0 : (uint)Math.Max(0, (active.AddedOn.AddSeconds(active.EffectDuration)-now).TotalSeconds);
                }
                return RefineryTime.Ticks == 0 ? 0 : (uint)Math.Max(0, (RefineryTime-now).TotalSeconds);
            }
        }
        public DateTime RefineryTime
        {
            get { return mRefineryTime; }
            set { mRefineryTime = value; }
        }
        public bool IsTwoHander()
        {
            ItemTypes item_type = (ItemTypes)GetItemType();
            bool check = ((UInt16)item_type >= 500 && (UInt16)item_type <= 580 || (UInt16)item_type == 421 || (UInt16)item_type == 612);
            if (check)
            {
                check = (item_type != ItemTypes.ShieldID);
                if (check)
                {
                    check = (item_type != ItemTypes.NinjaSwordID);
                    if (check)
                    {
                        check = (item_type != ItemTypes.MonkBeadsID);
                    }
                    if (check)
                    {
                        check = (item_type != ItemTypes.Rapier);
                    }
                }
            }
            return check;
        }
        public ItemTypes GetItemType()
        {
            return (ItemTypes)(this.ID / 1000);
        }
        #region Perfection System
        public int SocketsCount()
        {
            var count = 0;
            if ((byte)SocketOne != byte.MinValue)
                count++;
            if ((byte)SocketTwo != byte.MinValue)
                count++;
            return count;
        }
        public byte SocketOne2
        {
            get { return Buffer[23]; }
        }
        public byte SocketTwo2
        {
            get { return Buffer[24]; }
        }
        public ConquerItem DeepCopy()
        {
            return new ConquerItem(true)
            {
                PerfectionLevel = PerfectionLevel,
                PerfectionProgress = PerfectionProgress,
                PerfectionOwner = PerfectionOwner,
                PerfectionOwnerGuid = PerfectionOwnerGuid,
                Signature = Signature
            };
        }
        Client.GameState client;
        public int Prestige
        {
            get
            {
                var prestige = 0;
                var dbPos = ItemHandler.GetPositionFromID(ID);
                if (dbPos == ItemHandler.Positions.SteedArmor ||
                    dbPos == ItemHandler.Positions.Garment || dbPos == ItemHandler.Positions.Bottle || dbPos == ItemHandler.Positions.RightAccessory ||
                    dbPos == ItemHandler.Positions.LeftAccessory)
                    return prestige;
                var abilityScore = new PerfectionManager.AbilityScore();
                var dbItem = Database.ConquerItemInformation.BaseInformations[ID];
                
                if (dbItem.Level <= 0)
                {
                    client.Disconnect();
                }
                if (dbItem.Level > 0)
                {
                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.ItemLevel, dbItem.Level);
                    prestige += (int)(((decimal)dbItem.Level / 100) * abilityScore.Score);
                }
                if (Bless > 0 && Bless < 8)
                {
                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.Protection, Bless);
                    prestige += abilityScore.Score;
                }
                var materialPhase = RefineItem > 0 ? Kernel.DatabaseRefinery.ContainsKey(RefineItem) ? Kernel.DatabaseRefinery[RefineItem].Level : 0 : 0;
                if (materialPhase > 0)
                {
                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.MaterialPhase, materialPhase);
                    prestige += abilityScore.Score;
                }
                if (Purification.PurificationLevel > 0)
                {
                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.SoulPhase, (int)Purification.PurificationLevel);
                    prestige += abilityScore.Score;

                }
                if (Plus > 0)
                {
                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.Plus, Plus);
                    prestige += abilityScore.Score;
                }
                var count = SocketsCount();
                if (count != 0)
                {
                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.Sockets, count);
                    prestige += abilityScore.Score;
                }
                if (count > 0)
                {
                    var gemsQuality = GemsQuality();
                    if (gemsQuality.Length > 0)
                    {
                        for (int i = 0; i < (gemsQuality.Length); i++)
                        {

                            try
                            {
                                var gemQuality = gemsQuality[i];
                                if (gemQuality != 0)
                                {
                                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.GemQuality, gemQuality);
                                    prestige += abilityScore.Score;
                                }
                            }
                            catch
                            {
                                Console.WriteLine("break");
                            }
                        }
                    }
                }
                if (ID % 10 > 0)
                {
                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.Quality, (int)ID % 10);
                    prestige += abilityScore.Score;
                }
                if (PerfectionLevel > 0)
                {
                    var val = PerfectionLevel >= 28 ? 27 : PerfectionLevel;
                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.PerfectionLevel, (int)val);
                    var _prestige = (int)(((decimal)val / 100) * abilityScore.Score);
                    if (PerfectionLevel >= 28)
                    {
                        var index = 28;
                        while (index <= PerfectionLevel)
                        {
                            _prestige += 100;
                            index++;
                        }
                    }
                    prestige += _prestige;
                }
                if (Enchant > 0)
                {
                    abilityScore = PerfectionManager.GetAbilityScore(AbilitiesSort.Enchant, Enchant);
                    prestige += (int)(((decimal)Enchant / 100) * abilityScore.Score);
                }
                return prestige;
            }
        }
        public int[] GemsQuality()
        {
            var count = 0;

            if ((byte)SocketOne != byte.MinValue && (byte)SocketOne != byte.MaxValue)
                count++;
            if ((byte)SocketTwo != byte.MinValue && (byte)SocketTwo != byte.MaxValue)
                count++;

            var ints = new int[count];

            if (count > 0)
                ints[0] = Convert.ToInt32(SocketOne) % 10;
            if (count > 1)
                ints[1] = Convert.ToInt32(SocketTwo) % 10;

            return ints;
        }
        public uint PerfectionLevel
        {
            get { return BitConverter.ToUInt32(Buffer, 60); }
            set { WriteUint(value, 60, Buffer); }
        }
        public uint PerfectionProgress
        {
            get { return BitConverter.ToUInt32(Buffer, 64); }
            set { WriteUInt32(value, 64, Buffer); }
        }
        public uint PerfectionOwnerGuid
        {
            get { return BitConverter.ToUInt32(Buffer, 68); }
            set { WriteUInt32(value, 68, Buffer); }
        }
        public string PerfectionOwner
        {
            get { return PacketHandler.ReadString(Buffer, 72, 16).Replace("\0", string.Empty); }
            set { Array.Clear(Buffer, 72, 16); WriteString(value ?? "", 72, Buffer); }
        }
        public string Signature
        {
            get { return PacketHandler.ReadString(Buffer, 88, 32).Replace("\0", string.Empty); }
            set {
                byte[] bytes = Server.Encoding.GetBytes(value ?? "");
                Array.Clear(Buffer, 88, 32);
                Array.Copy(bytes, 0, Buffer, 88, Math.Min(32, bytes.Length));
            }
        }
        #endregion
        public static void CheckItemExtra(ConquerItem item, Client.GameState client)
        {
            if (item != null) Revora.Game.Features.RefineryProgress.Expire(client, item);
        }
        public enum ItemTypes : ushort
        {
            BowID = 500,
            ArrowID = 1050,
            ShieldID = 900,
            BackswordID = 421,
            BladeID = 410,
            SwordID = 420,
            NinjaSwordID = 601,
            MonkBeadsID = 610,
            FanID = 201,
            TowerID = 202,
            CropID = 203,
            GarmentID_1 = 181,
            GarmentID_2 = 182,
            BottleID = 2100,
            GemID = 700,
            PickaxeID = 562,
            RingID = 150,
            BootID = 160,
            SteedArmorID = 200,
            Rapier = 611,
            Pistol = 612,
            NecklaceID = 120,
            BraceletID = 152,
            BagID = 121,
            RightAccessory1 = 350,
            RightAccessory2 = 360,
            RightAccessory3 = 370,
            LeftAccessory = 380,
        }
    }
    public class ItemSocket
    {
        public const Byte
        Phoenix = 0,
        Dragon = 1,
        Fury = 2,
        Rainbow = 3,
        Kylin = 4,
        Violet = 5,
        Moon = 6,
        Tortoise = 7,
        Thunder = 10,
        Glory = 12,
        OpenSocket = 255,
        NoSocket = 0;
        public static Double GetGemBonus(Byte value)
        {
            switch (value)
            {
                case 71:
                    return 0.02;
                case 72:
                    return 0.04;
                case 1:
                case 11:
                case 21:
                    return 0.05;
                case 73:
                    return 0.06;
                case 2:
                case 12:
                case 22:
                case 31:
                    return 0.10;
                case 3:
                case 23:
                case 32:
                case 61:
                case 13:
                    return 0.15;
                case 33:
                    return 0.25;
                case 51:
                case 62:
                    return 0.30;
                case 41:
                case 52:
                case 63:
                    return 0.50;
                case 42:
                case 53:
                    return 1.00;
                case 43:
                    return 2.00;
                case 101:
                case 121:
                    return 100;
                case 102:
                case 122:
                    return 300;
                case 103:
                case 123:
                    return 500;
                default:
                    return 0;
            }
        }
    }
}