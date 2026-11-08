using System;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class Warehouse : Writer, Interfaces.IPacket
    {
        Byte[] Packet;
        public Warehouse(Boolean Create)
        {
            if (Create)
            {
                Packet = new Byte[92];
                WriteUInt16((UInt16)(Packet.Length - 8), 0, Packet);
                WriteUInt16(1102, 2, Packet);
                WriteUInt32((UInt32)Time32.timeGetTime().GetHashCode(), 4, Packet);
            }
        }
        public enum Mode : byte
        {
            Entire,
            AddItem,
            RemoveItem
        }
        public UInt32 NpcID
        {
            get { return BitConverter.ToUInt32(Packet, 4); }
            set { WriteUInt32(value, 4, Packet); }
        }
        public Mode Action
        {
            get { return (Mode)Packet[12]; }
            set { Packet[12] = (Byte)value; }
        }
        public Byte Type
        {
            get { return Packet[12]; }
            set { Packet[12] = value; }
        }
        public uint Size
        {
            get { return BitConverter.ToUInt32(Packet, 16); }
            set { WriteUInt32(value, 16, Packet); }
        }
        public UInt32 UID
        {
            get { return BitConverter.ToUInt32(Packet, 18); }
            set { WriteUInt32(value, 18, Packet); }
        }
        public ushort Count
        {
            get { return BitConverter.ToUInt16(Packet, 22); }
            set
            {
                if (value > 20)
                    throw new Exception("Invalid Count value.");
                byte[] packet = new byte[8 + 24 + (140 * value)];
                WriteUInt16((ushort)(packet.Length - 8), 0, packet);
                WriteUInt16(1102, 2, packet);
                WriteUInt32(NpcID, 4, packet);
                WriteByte(Type, 12, packet);
                packet[9 + 4] = Packet[9 + 4];
                WriteUInt16((value), 22, packet);
                Packet = packet;
                offest = 24;
            }
        }
        int offest = 24;
        public void Append(ConquerItem item)
        {
            if (item == null) throw new ArgumentNullException("item");
            if (Packet == null || offest < 24 || offest + 140 > Packet.Length - 8
                || offest + 140 > 24 + 140 * Count)
                throw new InvalidOperationException("Warehouse reply item capacity exceeded.");
            WriteUInt32(item.UID, offest, Packet);
            offest += 4;//32
            WriteUInt32(item.ID, offest, Packet);
            offest += 4;//36
            WriteByte(0, offest, Packet);
            offest += 1;//37
            WriteByte((byte)item.SocketOne, offest, Packet);
            offest += 1;//38
            WriteByte((byte)item.SocketTwo, offest, Packet);
            offest += 1;//39
            WriteUInt32(0, offest, Packet);
            offest += 4;//43
            WriteUInt16(0, offest, Packet);
            offest += 2;//45
            WriteByte(item.Plus, offest, Packet);
            offest += 1;//46
            WriteByte(item.Bless, offest, Packet);
            offest += 1;//47
            WriteByte((byte)(item.Bound == true ? 1 : 0), offest, Packet);
            offest += 1;//48
            WriteByte(item.Enchant, offest, Packet);
            offest += 1;//49
            WriteByte(item.NextGreen, offest, Packet);
            offest += 1;//50
            WriteUInt32((uint)item.Effect, offest, Packet);
            offest += 4;//54
            WriteByte(item.Lock, offest, Packet);
            offest += 1;//55
            WriteByte((byte)item.Color, offest, Packet);
            offest += 1;//56
            WriteUInt32(item.SocketProgress, offest, Packet);
            offest += 4;//60
            WriteUInt64(item.PlusProgress, offest, Packet);
            offest += 4;//64
            WriteUInt32((uint)(item.Inscribed ? 1 : 0), offest, Packet);
            offest += 4;//68
            WriteUInt32((item.TimeLeftInMinutes), offest, Packet);
            offest += 4;//72
            WriteUInt32(0, offest, Packet);
            offest += 4;//76
            WriteUInt16(item.StackSize, offest, Packet);
            offest += 2;//78
            WriteUInt32(item.Purification.PurificationItemID, offest, Packet);
            offest += 4;//82
            WriteUInt16(0, offest, Packet);
            offest += 2;//84
            WriteUInt32(item.PerfectionLevel, offest, Packet);
            offest += 4;//88
            WriteUInt32(item.PerfectionProgress, offest, Packet);
            offest += 4;//92
            WriteUInt32(item.PerfectionOwnerGuid, offest, Packet);
            offest += 4;//96
            WriteString(item.PerfectionOwner, offest, Packet);
            offest += 16;//112
            WriteString(item.Signature + 4, offest, Packet);
            offest += 32;//144
            offest += 4;//146
            WriteUInt32(item.RelicAttrib1, offest, Packet);
            offest += 4;
            WriteUInt32(item.RelicAttrib2, offest, Packet);
            offest += 4;
            WriteUInt32(item.RelicAttrib3, offest, Packet);
            offest += 4;
            WriteUInt32(item.RelicAttrib4, offest, Packet);
            offest += 4;
            WriteUInt32(item.RelicAttrib5, offest, Packet);
            offest += 4;
            //offest += 20;//NEW

        }
        public Byte[] ToArray()
        {
            return Packet;
        }
        public void Deserialize(Byte[] buffer)
        {
            Packet = buffer;
            offest = 24;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Packet);
        }
        public static void Process(Byte[] packet, Client.GameState client)
        {
            if(!ValidRequest(packet) || client==null || client.ItemSyncRoot==null)return;
            lock(client.ItemSyncRoot)
            {
                if(!Revora.Game.Features.QuestInventory.Player(client,false))return;
                ProcessOwned(packet,client);
            }
        }
        internal static bool ValidRequest(byte[] packet)
        {
            return packet!=null && packet.Length==172 && BitConverter.ToUInt16(packet,0)==164
                && BitConverter.ToUInt16(packet,2)==1102 && packet[12]<=(byte)Mode.RemoveItem
                && BitConverter.ToUInt64(packet,164)==Revora.Network.PacketHandler.ClientSeal;
        }
        private static void ProcessOwned(Byte[] packet, Client.GameState client)
        {
            if (client.Action != 2) return;
            Warehouse warehouse = new Warehouse(false);
            warehouse.Deserialize(packet);
            Game.ConquerStructures.Warehouse selected;
            if(TryStorage(client,warehouse.NpcID,out selected) && selected.HouseBox){
                if(packet[13]!=20)return;
                if(warehouse.Action==Mode.Entire)try{
                    using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        Database.SkillTable.LockOwner(c,t,client);
                        if(!Game.Features.HouseFurniture.StorageAccess(c,t,client,warehouse.NpcID))return;
                        t.Commit();
                    }}
                }catch(Exception ex){Game.Features.QuestInventory.Fail(client,"house-box-list",ex);return;}
            }
            switch (warehouse.Action)
            {
                case Mode.Entire:
                    {
                        Game.ConquerStructures.Warehouse wh;
                        if(!TryStorage(client,warehouse.NpcID,out wh))return;
                        if (wh == null) return;
                        Game.Features.NewcomerIntroduction.WarehouseOpened(client,warehouse.NpcID);
                        Queue<ConquerItem> queue = new Queue<ConquerItem>(wh.Objects);
                        if(queue.Count==0){warehouse.Type=(byte)Mode.Entire;warehouse.Count=0;warehouse.Size=wh.MaxCount;client.Send(warehouse);return;}
                        while (queue.Count > 0)
                        {
                            warehouse = new Warehouse(false);
                            warehouse.Deserialize(packet);
                            warehouse.Type = (byte)Mode.Entire;
                            warehouse.Count = (ushort)Math.Min(queue.Count, 7);
                            warehouse.Size = wh.MaxCount;
                            for (int i = 0; i < Math.Min(wh.Count, (byte)7) && queue.Count > 0; i++)
                            {
                                warehouse.Append(queue.Dequeue());
                            }
                            client.Send(warehouse);
                        }
                        break;
                    }
                case Mode.AddItem:
                    {
                        if (client.Booth != null) return;
                        // Sash contents retain their legacy saved key; TryStorage checks the owning sash UID.
                        foreach (ConquerItem itemx in client.Inventory.Objects)
                            if ((itemx.ID == 1100003) || (itemx.ID == 1100006) || (itemx.ID == 1100009))
                                if (!client.Warehouses.ContainsKey((Game.ConquerStructures.Warehouse.WarehouseID)(ushort)itemx.UID))
                                    client.Warehouses.Add((Game.ConquerStructures.Warehouse.WarehouseID)(ushort)itemx.UID, new Game.ConquerStructures.Warehouse(client, (Game.ConquerStructures.Warehouse.WarehouseID)(ushort)itemx.UID));
                        Game.ConquerStructures.Warehouse wh;
                        if(!TryStorage(client,warehouse.NpcID,out wh))return;
                        if (wh == null) return;
                        ConquerItem item = null;
                        if (client.Inventory.TryGetItem(warehouse.UID, out item))
                        {
                            if (item.ID >= 729960 && item.ID <= 729970) return;
                            if (!ConquerItem.isRune(item.ID))
                            {
                                if (wh.Transfer(item.UID, true))
                                {
                                    warehouse.UID = 0;
                                    warehouse.Count = 1;
                                    warehouse.Append(item);
                                    client.Send(warehouse);
                                    ItemLock itemLock = new ItemLock(true);
                                    itemLock.UID = item.UID;
                                    itemLock.ID = ItemLock.UnlockDate;
                                    itemLock.dwParam = (UInt32)(item.UnlockEnd.Year * 10000 + item.UnlockEnd.Month * 100 + item.UnlockEnd.Day);
                                    client.Send(itemLock);
                                    ItemAdding add = new ItemAdding(true);
                                    if (item.Purification.Available)
                                        add.Append(item.Purification);
                                    if (item.ExtraEffect.Available)
                                        add.Append(item.ExtraEffect);
                                    if (item.Purification.Available || item.ExtraEffect.Available)
                                        client.Send(add);
                                    return;
                                }
                            }
                            else client.Send(new Message("You can not store Flame Stone Rune's in Warehouse", System.Drawing.Color.Red, Message.TopLeft));
                        }
                        break;
                    }
                case Mode.RemoveItem:
                    {
                        Game.ConquerStructures.Warehouse wh;
                        if(!TryStorage(client,warehouse.NpcID,out wh))return;
                        if (wh == null) return;
                        if (wh.ContainsUID(warehouse.UID))
                        {
                            if (wh.Transfer(warehouse.UID, false))
                            {
                                client.Send(warehouse);
                                return;
                            }
                        }
                        break;
                    }
                default: Console.WriteLine("[Warehouse] Unknown type: " + warehouse.Action); break;
            }
        }
        private static bool TryStorage(Client.GameState client,uint key,out Game.ConquerStructures.Warehouse storage)
        {
            storage=null;
            if(client.Warehouses==null)return false;
            if(client.Warehouses.TryGetValue((Game.ConquerStructures.Warehouse.WarehouseID)key,out storage))return storage!=null;
            // Preserve documented legacy account/bag aliases, not arbitrary high-bit aliases.
            bool legacy=client.Account!=null && key==client.Account.EntityID;
            if(!legacy && client.Inventory!=null)
                foreach(var item in client.Inventory.Objects)
                    if(item.UID==key && (item.ID==1100003 || item.ID==1100006 || item.ID==1100009)){legacy=true;break;}
            return legacy && client.Warehouses.TryGetValue((Game.ConquerStructures.Warehouse.WarehouseID)(ushort)key,out storage)
                && storage!=null;
        }
    }
}
