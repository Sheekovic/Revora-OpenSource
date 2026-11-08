using Revora.Game;
using System;

namespace Revora.Network.GamePackets
{
    public unsafe class BoothItem2 : Interfaces.IPacket
    {
        private Byte[] mData;
        public void ParseItem(ConquerItem i)
        {
            ItemID = i.ID;
            ItemIdentifier = i.UID;
            Durability = i.Durability;
            MaxDurability = i.MaximDurability;
            SocketProgress = i.SocketProgress;
            SocketOne = (Byte)i.SocketOne;
            SocketTwo = (Byte)i.SocketTwo;
            Effect = (UInt16)i.Effect;
            Plus = i.Plus;
            Bless = i.Bless;
            Bound = i.Bound;
            Enchant = i.Enchant;
            Lock = Convert.ToBoolean(i.Lock);
            Color = (UInt32)i.Color;
            PlusProgress = i.PlusProgress;
            StackSize = i.StackSize;
            TimeLeftInMinutes = i.TimeLeftInMinutes;
            PerfectionLevel = i.PerfectionLevel;
            PerfectionProgress = i.PerfectionProgress;
            PerfectionOwnerGuid = i.PerfectionOwnerGuid;
            PerfectionOwnerName = i.PerfectionOwner;
            Signature = i.Signature;
            RunesEXP = i.RunesEXP;
            RelicAttrib1 = i.RelicAttrib1;
            RelicAttrib2 = i.RelicAttrib2;
            RelicAttrib3 = i.RelicAttrib3;
            RelicAttrib4 = i.RelicAttrib4;
            RelicAttrib5 = i.RelicAttrib5;
        }
        public BoothItem2()
        {
            mData = new Byte[165 + 8];
            Writer.WriteUInt16(((UInt16)(mData.Length - 8)), 0, mData);
            Writer.WriteUInt16((UInt16)1108, 2, mData);
        }
        public UInt32 ItemIdentifier
        {
            get { return BitConverter.ToUInt32(mData, 4); }
            set { Writer.WriteUInt32(value, 4, mData); }
        }
        public UInt32 Identifier
        {
            get { return BitConverter.ToUInt32(mData, 8); }
            set { Writer.WriteUInt32(value, 8, mData); }
        }
        public UInt32 Cost
        {
            get { return BitConverter.ToUInt32(mData, 12); }
            set { Writer.WriteUInt32(value, 12, mData); }
        }
        public UInt32 ItemID
        {
            get { return BitConverter.ToUInt32(mData, 20); }
            set { Writer.WriteUInt32(value, 20, mData); }
        }
        public UInt16 Durability
        {
            get { return BitConverter.ToUInt16(mData, 24); }
            set { Writer.WriteUInt16(value, 24, mData); }
        }
        public UInt16 MaxDurability
        {
            get { return BitConverter.ToUInt16(mData, 26); }
            set { Writer.WriteUInt16(value, 26, mData); }
        }
        public CostTypes CostType
        {
            get { return (CostTypes)BitConverter.ToUInt16(mData, 28); }
            set { Writer.WriteUInt16((UInt16)value, 28, mData); }
        }
        public ItemHandler.Positions Position
        {
            get { return (ItemHandler.Positions)mData[30]; }
            set { mData[30] = (Byte)value; }
        }
        public UInt32 SocketProgress
        {
            get { return BitConverter.ToUInt32(mData, 31); }
            set { Writer.WriteUInt32(value, 31, mData); }
        }
        public Byte SocketOne
        {
            get { return mData[35]; }
            set { mData[35] = value; }
        }
        public Byte SocketTwo
        {
            get { return mData[36]; }
            set { mData[36] = value; }
        }
        public UInt16 Effect
        {
            get { return BitConverter.ToUInt16(mData, 41); }
            set { Writer.WriteUInt16((UInt16)value, 41, mData); }
        }
        public Byte Plus
        {
            get { return mData[42]; }
            set { mData[42] = value; }
        }
        public Byte Bless
        {
            get { return mData[43]; }
            set { mData[43] = value; }
        }
        public Boolean Bound
        {
            get { return mData[44] == 0 ? false : true; }
            set { mData[44] = (Byte)(value ? 1 : 0); }
        }
        public Byte Enchant
        {
            get { return mData[45]; }
            set { mData[45] = value; }
        }
        public Boolean Lock
        {
            get { return mData[52] == 0 ? false : true; }
            set { mData[52] = (Byte)(value ? 1 : 0); }
        }
        public UInt32 Color
        {
            get { return BitConverter.ToUInt32(mData, 54); }
            set { Writer.WriteUInt32((UInt32)value, 54, mData); }
        }
        public UInt32 PlusProgress
        {
            get { return BitConverter.ToUInt32(mData, 56 - 2); }
            set { Writer.WriteUInt32(value, 56 - 2, mData); }
        }
        public uint TimeLeftInMinutes
        {
            get { return BitConverter.ToUInt32(mData, 64 - 2); }
            set { Writer.WriteUInt32(value, 64 - 2, mData); }
        }
        public UInt16 StackSize
        {
            get { return BitConverter.ToUInt16(mData, 72 - 2); }
            set { Writer.WriteUInt16(value, 72 - 2, mData); }
        }
        public UInt32 PurificationID
        {
            get { return BitConverter.ToUInt32(mData, 74); }
            set { Writer.WriteUInt32(value, 74, mData); }
        }
        #region Perfection
        public UInt32 PerfectionLevel
        {
            get { return BitConverter.ToUInt32(mData, 80); }
            set { Writer.WriteUInt32(value, 80, mData); }
        }
        public UInt32 PerfectionProgress
        {
            get { return BitConverter.ToUInt32(mData, 84); }
            set { Writer.WriteUInt32(value, 84, mData); }
        }
        public UInt32 PerfectionOwnerGuid
        {
            get { return BitConverter.ToUInt32(mData, 88); }
            set { Writer.WriteUInt32(value, 88, mData); }
        }
        public string PerfectionOwnerName
        {
            get { return GeneralData.ReadString(mData, 92, 16); }
            set { Writer.WriteStringWithLength(value, 16, 92, mData); }
        }
        public string Signature
        {
            get { return GeneralData.ReadString(mData, 108, 32); }
            set { Writer.WriteStringWithLength(value, 32, 108, mData); }
        }
        #endregion
        #region Runes & Relics
        public uint RunesEXP
        {
            get { return System.BitConverter.ToUInt32(mData, 141); }
            set { Writer.WriteUInt32(value, 141, mData); }
        }
        public uint RelicAttrib1
        {
            get { return System.BitConverter.ToUInt32(mData, 145); }
            set { Writer.WriteUInt32(value, 145, mData); }
        }
        public uint RelicAttrib2
        {
            get { return System.BitConverter.ToUInt32(mData, 149); }
            set { Writer.WriteUInt32(value, 149, mData); }
        }
        public uint RelicAttrib3
        {
            get { return System.BitConverter.ToUInt32(mData, 153); }
            set { Writer.WriteUInt32(value, 153, mData); }
        }
        public uint RelicAttrib4
        {
            get { return System.BitConverter.ToUInt32(mData, 157); }
            set { Writer.WriteUInt32(value, 157, mData); }
        }
        public uint RelicAttrib5
        {
            get { return System.BitConverter.ToUInt32(mData, 161); }
            set { Writer.WriteUInt32(value, 161, mData); }
        }
        #endregion
        public byte[] ToArray()
        {
            return mData;
        }
        public void Deserialize(byte[] buffer)
        {
            mData = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(mData);
        }
        public enum CostTypes : ushort
        {
            Gold = 1,
            CPs = 3,
            ViewEquip = 4
        }
    }
    public class BoothItem : Writer, Interfaces.IPacket
    {
        private byte[] Buffer;
        public BoothItem(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[82 + 8];
                WriteUInt16(((UInt16)(Buffer.Length - 8)), 0, Buffer);
                WriteUInt16(1108, 2, Buffer);
            }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint BoothID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public ulong Cost
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt64(value, 12, Buffer); }
        }
        public uint ID
        {
            get { return BitConverter.ToUInt32(Buffer, 20); }
            set { WriteUInt32(value, 20, Buffer); }
        }
        public ushort Durability
        {
            get { return BitConverter.ToUInt16(Buffer, 24); }
            set { WriteUInt16(value, 24, Buffer); }
        }
        public ushort MaximDurability
        {
            get { return BitConverter.ToUInt16(Buffer, 26); }
            set { WriteUInt16(value, 26, Buffer); }
        }
        public uint CostType
        {
            get { return BitConverter.ToUInt32(Buffer, 28); }
            set { WriteUInt32(value, 28, Buffer); }
        }
        public uint SocketProgress
        {
            get { return BitConverter.ToUInt32(Buffer, 31); }
            set { WriteUInt32(value, 31, Buffer); }
        }
        public Enums.Gem SocketOne
        {
            get { return (Enums.Gem)Buffer[35]; }
            set { Buffer[35] = (byte)value; }
        }
        public Enums.Gem SocketTwo
        {
            get { return (Enums.Gem)Buffer[36]; }
            set { Buffer[36] = (byte)value; }
        }
        public Enums.ItemEffect Effect
        {
            get { return (Enums.ItemEffect)BitConverter.ToUInt16(Buffer, 41); }
            set { WriteUInt16((ushort)value, 41, Buffer); }
        }
        public byte Plus
        {
            get { return Buffer[42]; }
            set { Buffer[42] = value; }
        }
        public byte Bless
        {
            get { return Buffer[43]; }
            set { Buffer[43] = value; }
        }
        public bool Bound
        {
            get { return Buffer[44] == 0 ? false : true; }
            set { Buffer[44] = (byte)(value ? 1 : 0); }
        }
        public byte Enchant
        {
            get { return Buffer[45]; }
            set { Buffer[45] = value; }
        }
        public bool Suspicious
        {
            get { return Buffer[51] == 0 ? false : true; }
            set { Buffer[51] = (byte)(value ? 1 : 0); }
        }
        public byte Lock
        {
            get { return Buffer[52]; }
            set { Buffer[52] = value; }
        }
        public Enums.Color Color
        {
            get { return (Enums.Color)BitConverter.ToUInt32(Buffer, 54); }
            set { WriteUInt32((uint)value, 54, Buffer); }
        }
        public uint PlusProgress
        {
            get { return BitConverter.ToUInt32(Buffer, 56); }
            set { WriteUInt32(value, 56, Buffer); }
        }
        public bool Inscribed
        {
            get { return (BitConverter.ToUInt16(Buffer, 60) == 1); }
            set { Writer.WriteUInt16(value ? ((byte)1) : ((byte)0), 60, Buffer); }
        }
        public uint TimeLeftInMinutes
        {
            get { return BitConverter.ToUInt32(Buffer, 62); }
            set { Writer.WriteUInt32(value, 62, Buffer); }
        }
        public ushort StackSize
        {
            get { return BitConverter.ToUInt16(Buffer, 72); }
            set { WriteUInt16(value, 72, Buffer); }
        }
        public uint PurificationID
        {
            get { return BitConverter.ToUInt32(Buffer, 74); }
            set { WriteUInt32(value, 74, Buffer); }
        }
        #region Perfection
        public UInt32 PerfectionLevel
        {
            get { return BitConverter.ToUInt32(Buffer, 80); }
            set { WriteUInt32(value, 80, Buffer); }
        }
        public UInt32 PerfectionProgress
        {
            get { return BitConverter.ToUInt32(Buffer, 84); }
            set { WriteUInt32(value, 84, Buffer); }
        }
        public UInt32 PerfectionOwnerGuid
        {
            get { return BitConverter.ToUInt32(Buffer, 88); }
            set { WriteUInt32(value, 88, Buffer); }
        }
        public string PerfectionOwnerName
        {
            get { return GeneralData.ReadString(Buffer, 92, 16); }
            set { WriteStringWithLength(value, 16, 92, Buffer); }
        }
        public string Signature
        {
            get { return GeneralData.ReadString(Buffer, 108, 32); }
            set { WriteStringWithLength(value, 32, 108, Buffer); }
        }
        #endregion
        #region Runes & Relics
        public uint RunesEXP
        {
            get { return System.BitConverter.ToUInt32(Buffer, 141); }
            set { WriteUInt32(value, 141, Buffer); }
        }
        public uint RelicAttrib1
        {
            get { return System.BitConverter.ToUInt32(Buffer, 145); }
            set { WriteUInt32(value, 145, Buffer); }
        }
        public uint RelicAttrib2
        {
            get { return System.BitConverter.ToUInt32(Buffer, 149); }
            set { WriteUInt32(value, 149, Buffer); }
        }
        public uint RelicAttrib3
        {
            get { return System.BitConverter.ToUInt32(Buffer, 153); }
            set { WriteUInt32(value, 153, Buffer); }
        }
        public uint RelicAttrib4
        {
            get { return System.BitConverter.ToUInt32(Buffer, 157); }
            set { WriteUInt32(value, 157, Buffer); }
        }
        public uint RelicAttrib5
        {
            get { return System.BitConverter.ToUInt32(Buffer, 161); }
            set { WriteUInt32(value, 161, Buffer); }
        }
        #endregion
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public override int GetHashCode()
        {
            return (int)UID;
        }
        public void Fill(Game.ConquerStructures.BoothItem item, uint boothID)
        {
            UID = item.Item.UID;
            BoothID = boothID;
            Cost = item.Cost;
            ID = item.Item.ID;
            Durability = item.Item.Durability;
            MaximDurability = item.Item.MaximDurability;
            CostType = (byte)item.Cost_Type;
            SocketOne = item.Item.SocketOne;
            SocketTwo = item.Item.SocketTwo;
            Effect = item.Item.Effect;
            Bound = item.Item.Bound;
            Plus = item.Item.Plus;
            Bless = item.Item.Bless;
            Enchant = item.Item.Enchant;
            SocketProgress = item.Item.SocketProgress;
            Color = item.Item.Color;
            PlusProgress = item.Item.PlusProgress;
            StackSize = item.Item.StackSize;
            Inscribed = item.Item.Inscribed;
            TimeLeftInMinutes = item.Item.TimeLeftInMinutes;

            PerfectionLevel = item.Item.PerfectionLevel;
            PerfectionProgress = item.Item.PerfectionProgress;
            PerfectionOwnerGuid = item.Item.PerfectionOwnerGuid;
            PerfectionOwnerName = item.Item.PerfectionOwner;
            Signature = item.Item.Signature;
            RunesEXP = item.Item.RunesEXP;
            RelicAttrib1 = item.Item.RelicAttrib1;
            RelicAttrib2 = item.Item.RelicAttrib2;
            RelicAttrib3 = item.Item.RelicAttrib3;
            RelicAttrib4 = item.Item.RelicAttrib4;
            RelicAttrib5 = item.Item.RelicAttrib5;
        }
        public static void ShowBoothItems(ItemUsage usage, Client.GameState client)
        {
            Client.GameState Owner = null;
            Entity entity = null;
            Game.ConquerStructures.Booth booth = null;
            if (Game.ConquerStructures.Booth.TryGetValue(usage.UID, out booth))
            {
                Owner = booth.Base.Owner;
                entity = Owner.Entity;
                if (client.Trade.InTrade || Owner.Trade.InTrade)
                    return;
                if (Owner != null)
                {
                    if (Owner.Entity.UID != client.Entity.UID)
                    {
                        BoothItem Item = new BoothItem(true);
                        if (Owner.Booth != null)
                        {
                            foreach (Game.ConquerStructures.BoothItem item in Owner.Booth.ItemList.Values)
                            {
                                Item.Fill(item, Owner.Booth.Base.UID);
                                client.Send(Item);
                                item.Item.SendExtras(client);
                            }
                        }
                    }
                }
            }
            if (Game.ConquerStructures.Booth.TryGetValue2(usage.UID, out booth))
            {
                BoothItem Item = new BoothItem(true);
                if (booth != null)
                {
                    foreach (Game.ConquerStructures.BoothItem item in booth.ItemList.Values)
                    {
                        Item.Fill(item, usage.UID);
                        client.Send(Item);
                        item.Item.SendExtras(client);
                    }
                }
            }
        }
        public static void AddItemOnBooth(ItemUsage usage, Client.GameState client)
        {
            if (client.Booth != null && !client.Trade.InTrade)
            {
                if (!client.Booth.ItemList.ContainsKey(usage.UID))
                {
                    if (client.Inventory.ContainsUID(usage.UID))
                    {
                        Game.ConquerStructures.BoothItem item = new Game.ConquerStructures.BoothItem();
                        item.Cost = usage.BoothCost;
                        client.Inventory.TryGetItem(usage.UID, out item.Item);
                        if (item.Item == null) return;
                        if (Game.Features.SummerCrafting.Identity(item.Item.ID)) { client.Send("Exchange unopened summer wings through a direct trade."); return; }
                        Database.ConquerItemInformation infos = new Database.ConquerItemInformation(item.Item.ID, 0);
                        if (item.Item.Lock != 0 || item.Item.Bound || infos.BaseInformation.Type != Database.ConquerItemBaseInformation.ItemType.Dropable)
                        {
                            return;
                        }
                        item.Cost_Type = usage.Action == ItemUsage.Mode.AddItemOnBoothForSilvers ? Game.ConquerStructures.BoothItem.CostType.Silvers : Game.ConquerStructures.BoothItem.CostType.ConquerPoints;
                        client.Booth.ItemList.Add(item.Item.UID, item);
                        client.Send(usage.ToArray());
                        BoothItem Item = new BoothItem(true);
                        Item.Fill(item, client.Booth.Base.UID);
                        client.SendScreen(Item, false);
                    }
                }
            }
        }
        public static void BuyFromBooth(ItemUsage usage, Client.GameState client)
        {
            Client.GameState Owner = null;
            Game.Entity entity = null;
            Game.ConquerStructures.Booth booth = null;
            if (Game.ConquerStructures.Booth.TryGetValue(usage.dwParam, out booth))
            {
                if(booth==null || booth.Base==null || client==null || client.Entity==null || client.Trade==null)return;
                Owner = booth.Base.Owner;
                if(Owner==null || Owner.Entity==null || Owner.Trade==null || !ReferenceEquals(Owner.Booth,booth))return;
                entity = Owner.Entity;
                if (client.Trade.InTrade || Owner.Trade.InTrade) return;
                if (Owner != null)
                {
                    if (Owner.Trade != null)
                    {
                        if (Owner.Trade.InTrade) return;
                    }
                    if (Owner.Entity.UID != client.Entity.UID)
                    {
                        if (Owner.Booth.ItemList.ContainsKey(usage.UID))
                        {
                            Game.ConquerStructures.BoothItem item;
                            if (Owner.Booth.ItemList.TryGetValue(usage.UID, out item))
                            {
                                if (Game.Features.SummerCrafting.Identity(item.Item.ID)) { client.Send("Exchange unopened summer wings through a direct trade."); return; }
                                if (client.Inventory.Count <= 39)
                                {
                                    var settled = Revora.Game.Features.PurseBalance.TrySettleBoothPurchase(client, Owner, item.Item.UID, item.Cost, usage.dwParam);
                                    if (settled != Revora.Game.Features.PurseBalance.TransferResult.Transferred)
                                        return;
                                    client.Send(usage.ToArray());
                                    usage.Action = ItemUsage.Mode.RemoveItemFromBooth;
                                    Owner.Send(usage.ToArray());
                                    usage.Action = ItemUsage.Mode.RemoveInventory;
                                    Owner.Send(usage.ToArray());
                                    Database.ConquerItemInformation infos = new Database.ConquerItemInformation(item.Item.ID, 0);
                                    Owner.Send(Constants.BoothItemSell(client.Entity.Name, infos.BaseInformation.Name,
                                        item.Cost_Type == Game.ConquerStructures.BoothItem.CostType.ConquerPoints, item.Cost));

                                }
                            }
                        }
                    }
                }
            }
            if (Game.ConquerStructures.Booth.TryGetValue2(usage.dwParam, out booth))
            {
                if (booth.ItemList.ContainsKey(usage.UID))
                {
                    Game.ConquerStructures.BoothItem item;
                    if (booth.ItemList.TryGetValue(usage.UID, out item))
                    {
                        if (Game.Features.SummerCrafting.Identity(item.Item.ID)) { client.Send("Exchange unopened summer wings through a direct trade."); return; }
                        if (client.Inventory.Count <= 39)
                        {
                            if (item.Cost_Type == Game.ConquerStructures.BoothItem.CostType.Silvers)
                            {
                                if (client.Entity.Money >= item.Cost)
                                {
                                    client.Entity.AdjustMoney(-(decimal)(item.Cost));
                                    client.Send(usage.ToArray());
                                    client.Inventory.Add(item.Item, Game.Enums.ItemUse.Move);
                                    Database.ConquerItemInformation infos = new Database.ConquerItemInformation(item.Item.ID, 0);
                                    var newitem = new Game.ConquerStructures.BoothItem();
                                    newitem.Regenerate(item, booth);
                                    BoothItem Item = new BoothItem(true);
                                    foreach (Game.ConquerStructures.BoothItem i in booth.ItemList.Values)
                                    {
                                        Item.Fill(i, usage.dwParam);
                                        client.Send(Item);
                                        item.Item.SendExtras(client);
                                    }
                                }
                            }
                            else
                            {
                                if (client.Entity.ConquerPoints >= item.Cost)
                                {
                                    if (!client.Entity.TrySpendConquerPoints((uint)item.Cost))
                                        return;
                                    client.Send(usage.ToArray());
                                    client.Inventory.Add(item.Item, Game.Enums.ItemUse.Move);
                                    Database.ConquerItemInformation infos = new Database.ConquerItemInformation(item.Item.ID, 0);
                                    var newitem = new Game.ConquerStructures.BoothItem();
                                    newitem.Regenerate(item, booth);
                                    BoothItem Item = new BoothItem(true);
                                    foreach (Game.ConquerStructures.BoothItem i in booth.ItemList.Values)
                                    {
                                        Item.Fill(i, usage.dwParam);
                                        client.Send(Item);
                                        item.Item.SendExtras(client);
                                    }
                                }
                            }
                        }

                    }
                }
            }
        }
        public static void RemoveItemFromBooth(ItemUsage usage, Client.GameState client)
        {
            if (client.Booth.ItemList.ContainsKey(usage.UID))
            {
                client.Booth.ItemList.Remove(usage.UID);
                client.SendScreen(usage.ToArray(), true);
            }
        }
    }
}
