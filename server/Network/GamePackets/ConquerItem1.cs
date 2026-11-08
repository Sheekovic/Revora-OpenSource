using System;
using COServer.Game;
using System.Collections.Generic;

namespace COServer.Network.GamePackets
{
    public unsafe class ConquerItem : Writer, Interfaces.IPacket
    {
        public const ushort
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
        RightWeaponAccessory = 15,
        LeftWeaponAccessory = 16,
        SteedArmor = 17,
        Wing = 19,
        SteedCrop = 18,
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

        public const uint GoldPrize = 2100075;
        public bool IsWorn = false;
        public byte[] Buffer;
        public static Counter ItemUID = new Counter(0);
        private UInt32 mRefineItem = 0;
        private DateTime mRefineryTime;
        private ulong suspiciousStart = 0, unlockEnd = 0;
        private bool unlocking = false;
        private ushort warehouse = 0;
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
        public Auction AuctionInfo
        {
            get;
            set;
        }
        public ConquerItem(bool Create)
        {
            if (Create)
            {
                Agate_map = new Dictionary<uint, string>(10);
                Buffer = new byte[92];
                WriteUInt16(84, 0, Buffer);
                WriteUInt16(1008, 2, Buffer);
                Mode = Game.Enums.ItemMode.Default;
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
        public void SetID(uint ID)
        {
            WriteUInt32(ID, 8, Buffer);
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
        public ushort Position
        {
            get { return BitConverter.ToUInt16(Buffer, 18); }
            set { WriteUInt16(value, 18, Buffer); }
        }
        public ushort Warehouse
        {
            get { return warehouse; }
            set { warehouse = value; }
        }
        public uint SocketProgress
        {
            get { return BitConverter.ToUInt32(Buffer, 20); }
            set { WriteUInt32(value, 20, Buffer); }
        }
        public Enums.Gem SocketOne
        {
            get { return (Enums.Gem)Buffer[24]; }
            set { Buffer[24] = (byte)value; }
        }
        public Enums.Gem SocketTwo
        {
            get { return (Enums.Gem)Buffer[25]; }
            set { Buffer[25] = (byte)value; }
        }
        public Enums.ItemEffect Effect
        {
            get { return (Enums.ItemEffect)BitConverter.ToUInt16(Buffer, 28); }
            set { WriteUInt16((ushort)value, 28, Buffer); }
        }
        public byte Plus
        {
            get { return Buffer[33]; }
            set { Buffer[33] = value; }
        }
        public byte Bless
        {
            get { return Buffer[34]; }
            set { Buffer[34] = value; }
        }
        public bool Bound
        {
            get { return Buffer[35] == 0 ? false : true; }
            set { Buffer[35] = (byte)(value ? 1 : 0); }
        }
        public byte Enchant
        {
            get { return Buffer[36]; }
            set { Buffer[36] = value; }
        }
        public byte NextRed
        {
            get { return Buffer[34]; }
            set { Buffer[34] = value; }
        }
        public byte NextBlue
        {
            get { return Buffer[36]; }
            set { Buffer[36] = value; }
        }
        public byte NextGreen
        {
            get { return Buffer[40]; }
            set { Buffer[40] = value; }
        }
        public bool Suspicious
        {
            get { return Buffer[44] == 0 ? false : true; }
            set { Buffer[44] = (byte)(value ? 1 : 0); }
        }
        public byte Lock
        {
            get { return Buffer[46]; }
            set { Buffer[46] = value; }
        }
        public Enums.Color Color
        {
            get { return (Enums.Color)BitConverter.ToUInt32(Buffer, 48); }
            set { WriteUInt32((uint)value, 48, Buffer); }
        }
        public uint PlusProgress
        {
            get { return BitConverter.ToUInt32(Buffer, 52); }
            set { WriteUInt32(value, 52, Buffer); }
        }
        public bool Inscribed
        {
            get { return (BitConverter.ToUInt16(this.Buffer, 56) == 1); }
            set { Writer.WriteUInt16(value ? ((byte)1) : ((byte)0), 56, this.Buffer); }
        }
        public uint TimeLeftInMinutes
        {
            get { return BitConverter.ToUInt32(Buffer, 60); }
            set { WriteUInt32(value, 60, Buffer); }
        }
        public ushort StackSize
        {
            get { return BitConverter.ToUInt16(Buffer, 68); }
            set { WriteUInt16(value, 68, Buffer); }
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
                        Time = (UInt32)(RefineryTime.Subtract(DateTime.Now).TotalSeconds)
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
                        Time = (UInt32)(RefineryTime.Subtract(DateTime.Now).TotalSeconds)
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
                Kernel.DatabaseRefinery.TryGetValue(RefineItem, out i);
                return i;
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
        public static void CheckItemExtra(ConquerItem i, Client.GameState c)
        {
            if (i.RefineryTime.Ticks != 0)
            {
                if (DateTime.Now > i.RefineryTime)
                {
                    i.RefineItem = 0;
                    i.RefineryTime = new DateTime(0);
                    ItemSoul expire = new ItemSoul()
                    {
                        Identifier = i.UID
                    };
                    expire.Expired(c);
                    i.Send(c);
                    WindowStats Stats = new WindowStats(c);
                    c.Send(Stats.ToArray());
                    Database.ConquerItemTable.UpdateRefineryItem(i);
                    Database.ConquerItemTable.UpdateRefineryTime(i);
                    if (!c.Equipment.Free(i.Position))
                    {
                        c.LoadItemStats();
                    }
                }
            }
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