#region Old Usage

using System;
using System.Linq;
using System.Text;
using System.Drawing;
using COServer.Client;
using System.Collections.Generic;

namespace COServer.Network.GamePackets
{
    public unsafe class ItemUsage : Writer, Interfaces.IPacket
    {
        public const ushort
            DragonBallUpgrade = 19,
            MeteorUpgrade = 20;
        public enum Mode : byte
        {
            BuyFromNPC = 1,
            SellToNPC = 2,
            RemoveInventory = 3,
            EquipItem = 4,
            UnequipItem = 6,
            ArrowReload = 8,
            ViewWarehouse = 9,
            WarehouseDeposit = 10,
            WarehouseWithdraw = 11,
            Repair = 14,
            VIPRepair = 15,
            DragonBallUpgrade = 19,
            MeteorUpgrade = 20,
            ShowBoothItems = 21,
            AddItemOnBoothForSilvers = 22,
            RemoveItemFromBooth = 23,
            BuyFromBooth = 24,
            UpdateDurability = 25,
            FireWorks = 26,
            AddItemOnBoothForConquerPoints = 29,
            Ping = 27,
            Enchant = 28,
            RedeemGear = 32,
            ClaimGear = 33,
            Unknown = 34,
            SocketTalismanWithItem = 35,
            SocketTalismanWithCPs = 36,
            DropItem = 37,
            DropMoney = 38,
            GemCompose = 39,
            ItemSell = 55,
            Bless = 40,
            Accessories = 41,
            MainEquipment = 44,
            AlternateEquipment = 45,
            ItemsReturned = 50,
            ToristSuper = 51,
            ChatItem = 52,
            ObtinedPoints = 53,
            LowerEquipment = 54,
            SocketerMan = 43,
            MergeStackableItems = 48,
            SplitStack = 49,
            ExtraInventory = 56
        }
        byte[] Packet;
        public uint[] Batch
        {
            get
            {
                uint[] items = new uint[dwExtraInfo];
                for (int i = 0; i < dwExtraInfo; i++)
                {
                    items[i] = BitConverter.ToUInt32(Packet, 87 + 4 * i);
                }
                return items;
            }
            set
            {
                if (value != null)
                {
                    dwExtraInfo = (uint)value.Length;
                    for (int i = 0; i < dwExtraInfo; i++)
                    {
                        WriteUInt32(value[i], 87 + 4 * i, Packet);
                    }
                }
            }
        }
        public ItemUsage(bool Create)
        {
            if (Create)
            {
                Packet = new byte[112];
                WriteUInt16(104, 0, Packet);
                WriteUInt16(1009, 2, Packet);
                WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Packet);
            }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Packet, 8); }
            set { WriteUInt32(value, 8, Packet); }
        }
        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Packet, 12); }
            set { WriteUInt32(value, 12, Packet); }
        }
        public Mode Action
        {
            get { return (Mode)Packet[20]; }
            set { fixed (byte* Pack = Packet) *((byte*)(Pack + 20)) = (byte)value; }
        }
        public uint ID
        {
            get { return BitConverter.ToUInt32(Packet, 16); }
            set { WriteUInt32(value, 16, Packet); }
        }
        public uint ActionOld
        {
            get { return BitConverter.ToUInt32(Packet, 20); }
            set { WriteUInt32(value, 20, Packet); }
        }
        public uint TimeStamp
        {
            get { return BitConverter.ToUInt32(Packet, 22); }
            set { WriteUInt32(value, 22, Packet); }
        }
        public uint dwExtraInfo
        {
            get { return BitConverter.ToUInt32(Packet, 26); }
            set { WriteUInt32(value, 26, Packet); }
        }
        public uint dwExtraInfo2
        {
            get { return BitConverter.ToUInt32(Packet, 30); }
            set { WriteUInt32(value, 30, Packet); }
        }
        public uint dwExtraInfo3
        {
            get { return BitConverter.ToUInt32(Packet, 34); }
            set { WriteUInt32(value, 34, Packet); }
        }
        public uint dwParam2
        {
            get { return BitConverter.ToUInt32(Packet, 91); }
            set { WriteUInt32(value, 91, Packet); }
        }
        public byte[] ToArray()
        {
            return Packet;
        }
        public void Deserialize(byte[] buffer)
        {
            Packet = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Packet);
        }
        public static void Process(byte[] packet, GameState client)
        {
            if (client.Action != 2) return;
            ItemUsage usage = new ItemUsage(false);
            usage.Deserialize(packet);
            if (!client.Entity.Dead || usage.Action == ItemUsage.Mode.Ping)
            {
                switch (usage.Action)
                {
                    case ItemUsage.Mode.ObtinedPoints:
                        {
                            uint ItemAdd = BitConverter.ToUInt32(packet, 12);
                            ConquerItem item_new = null;
                            if (client.Inventory.TryGetItem(ItemAdd, out item_new))
                            {
                                uint obtined_points = 0;
                                Database.ConquerItemInformation iteminfo = new Database.ConquerItemInformation(ItemAdd, 0);
                                Dictionary<uint, uint> amount = new Dictionary<uint, uint>();
                                for (ushort i = 88; i < 92 * packet[24]; i += 4)
                                {
                                    uint item_swap = BitConverter.ToUInt32(packet, i);
                                    if (client.Inventory.ContainsUID(item_swap))
                                    {
                                        ConquerItem item = null;
                                        if (client.Inventory.TryGetItem(item_swap, out item))
                                        {
                                            amount.Add(item_swap, i);
                                        }
                                        switch (item.ID)
                                        {
                                            case 191505:
                                            case 191605:
                                            case 191705:
                                            case 191805:
                                            case 191905:
                                            case 191405:
                                            case 183325:
                                            case 183315:
                                            case 183375:
                                            case 183305:
                                                {
                                                    obtined_points += 300;
                                                    break;
                                                }
                                            default:
                                                obtined_points += 50;
                                                break;
                                        }
                                    }
                                }
                                if (iteminfo.BaseInformation.ConquerPointsWorth > obtined_points)
                                {
                                    uint add_cps = 0;
                                    add_cps = (uint)(iteminfo.BaseInformation.ConquerPointsWorth - obtined_points);
                                    if (add_cps < client.Entity.ConquerPoints)
                                    {
                                        client.Entity.TrySpendConquerPoints((uint)((uint)add_cps));
                                        foreach (uint key in amount.Keys)
                                        {
                                            if (client.Inventory.ContainsUID(key))
                                                client.Inventory.Remove(key, Game.Enums.ItemUse.Remove, true);
                                        }
                                        client.Inventory.Add(ItemAdd, 0, 1);

                                    }
                                }
                                else
                                {
                                    foreach (uint key in amount.Keys)
                                    {
                                        if (client.Inventory.ContainsUID(key))
                                            client.Inventory.Remove(key, Game.Enums.ItemUse.Remove, true);
                                    }
                                    client.Inventory.Add(ItemAdd, 0, 1);

                                }
                            }
                            break;
                        }

                    case ItemUsage.Mode.MainEquipment:
                    case ItemUsage.Mode.AlternateEquipment:
                        {
                            if (client.Entity.ContainsFlag3((ulong)FlagsUpdate.Flags3.PathOfShadow))
                            {
                                client.Entity.RemoveFlag3((ulong)FlagsUpdate.Flags3.PathOfShadow);
                            }
                            GeneralData.SwitchEquipment(client, usage.Action == ItemUsage.Mode.AlternateEquipment);
                            client.Send(new ClientEquip(client));
                            break;
                        }
                  /*  case ItemUsage.Mode.ExtraInventory:
                        {
                            ConquerItem Sash;
                            if (client.Inventory.TryGetItem(usage.UID, out Sash))
                            {
                                if (client.Entity.ExtraInventory < 200)
                                {
                                    if (Sash.ID == 1100003)
                                    {
                                        if (client.Entity.ExtraInventory < 198)
                                        {
                                            client.Entity.ExtraInventory += 3;
                                        }
                                        else
                                        {
                                            client.Entity.ExtraInventory = 200;
                                        }
                                        client.Inventory.Remove(Sash, Game.Enums.ItemUse.Remove);
                                    }
                                    else if (Sash.ID == 1100006)
                                    {
                                        if (client.Entity.ExtraInventory < 195)
                                        {
                                            client.Entity.ExtraInventory += 6;
                                        }
                                        else
                                        {
                                            client.Entity.ExtraInventory = 200;
                                        }
                                        client.Inventory.Remove(Sash, Game.Enums.ItemUse.Remove);
                                    }
                                    else if (Sash.ID == 1100009)
                                    {
                                        if (client.Entity.ExtraInventory < 189)
                                        {
                                            client.Entity.ExtraInventory += 12;
                                        }
                                        else
                                        {
                                            client.Entity.ExtraInventory = 200;
                                        }
                                        client.Inventory.Remove(Sash, Game.Enums.ItemUse.Remove);
                                    }
                                }
                                else
                                {
                                    client.Entity.SendSysMesage("You cannot open more spaces.");
                                }
                            }
                            break;
                        }*/
                    case ItemUsage.Mode.ItemsReturned:
                        {
                            var item = Database.ConquerItemTable.LoadItem(usage.UID);
                            client.Inventory.Add(item, Game.Enums.ItemUse.Move);
                            usage.dwParam = 10;//case id
                            client.Send(usage);
                            break;
                        }
                    case ItemUsage.Mode.ChatItem:
                        {
                            var item = Database.ConquerItemTable.LoadItem(usage.UID);
                            item.Mode = Game.Enums.ItemMode.ChatItem;
                            item.Send(client);
                            ItemLock itemLock = new ItemLock(true);
                            itemLock.UID = item.UID;
                            itemLock.ID = ItemLock.UnlockDate;
                            itemLock.dwParam = (uint)(item.UnlockEnd.Year * 10000 + item.UnlockEnd.Month * 100 + item.UnlockEnd.Day);
                            client.Send(itemLock);
                            break;
                        }
                    case ItemUsage.Mode.Accessories:
                        {
                            ConquerItem item = new ConquerItem(true);
                            item.UID = usage.UID;
                            item.DayStamp = DateTime.Now;
                            item.Days = 7;
                            TimeSpan Remain = item.DayStamp.AddDays(item.Days) - DateTime.Now;
                            item.TimeLeftInMinutes = (uint)Remain.TotalSeconds;
                            Database.ConquerItemTable.Update_Free(item, client);
                            client.Send(usage);
                            break;
                        }
                    case ItemUsage.Mode.ArrowReload:
                        {
                            lock (client.ItemSyncRoot)
                                ItemHandler.ReloadArrows(client.Equipment.TryGetItem(ConquerItem.LeftWeapon), client);
                            break;
                        }
                    case ItemUsage.Mode.ShowBoothItems:
                        {
                            lock (client.ItemSyncRoot)
                                BoothItem.ShowBoothItems(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.AddItemOnBoothForSilvers:
                    case ItemUsage.Mode.AddItemOnBoothForConquerPoints:
                        {
                            lock (client.ItemSyncRoot)
                                BoothItem.AddItemOnBooth(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.BuyFromBooth:
                        {
                            if (!client.Entity.TradeOn) return;
                            if (client.Entity.PokerTable > 0) break;
                            lock (client.ItemSyncRoot)
                                BoothItem.BuyFromBooth(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.RemoveItemFromBooth:
                        {
                            lock (client.ItemSyncRoot)
                                BoothItem.RemoveItemFromBooth(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.EquipItem:
                        {
                            lock (client.ItemSyncRoot)
                                ItemHandler.EquipItem(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.ItemSell:
                        {
                            ItemHandler.HandleBuyFromNPC(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.UnequipItem:
                        {
                            lock (client.ItemSyncRoot)
                                ItemHandler.UnequipItem(usage, client);
                            client.Equipment.UpdateEntityPacket();
                            break;
                        }
                    case ItemUsage.Mode.BuyFromNPC:
                        {
                            if (client.Entity.PokerTable > 0) break;
                            lock (client.ItemSyncRoot)
                                ItemHandler.HandleBuyFromNPC(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.SellToNPC:
                        {
                            lock (client.ItemSyncRoot)
                                ItemHandler.HandleSellToNPC(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.VIPRepair:
                        {
                            ItemHandler.HandleVIPRepair(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.Repair:
                        {
                            ItemHandler.HandleRepair(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.MeteorUpgrade:
                    case ItemUsage.Mode.DragonBallUpgrade:
                        {
                            ItemHandler.UpgradeItem(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.Ping:
                        {
                            client.Send(usage);
                            break;
                        }
                    case ItemUsage.Mode.ViewWarehouse:
                        {
                            usage.dwParam = client.MoneySave;
                            client.Send(usage);
                            break;
                        }
                    case ItemUsage.Mode.WarehouseDeposit:
                        {
                            if (client.Entity.Money >= usage.dwParam)
                            {
                                client.Entity.Money -= usage.dwParam;
                                client.MoneySave += usage.dwParam;
                            }
                            break;
                        }
                    case ItemUsage.Mode.WarehouseWithdraw:
                        {
                            if (client.MoneySave >= usage.dwParam)
                            {
                                client.Entity.Money += usage.dwParam;
                                client.MoneySave -= usage.dwParam;
                            }
                            break;
                        }
                    case ItemUsage.Mode.DropItem:
                        {
                            ItemHandler.DropItem(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.DropMoney:
                        {
                            ItemHandler.DropMoney(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.Enchant:
                        {
                            ItemHandler.EnchantItem(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.SocketTalismanWithItem:
                        {
                            usage.dwParam = BitConverter.ToUInt32(packet, 88);
                            ItemHandler.SocketTalismanWithItem(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.SocketTalismanWithCPs:
                        {
                            ItemHandler.SocketTalismanWithCPs(usage, client);
                            break;
                        }
                    case ItemUsage.Mode.RedeemGear:
                        {
                            var item = client.DeatinedItem[usage.UID];
                            if (item != null)
                            {
                                if (DateTime.Now > item.Date.AddDays(7))
                                {
                                    client.Send(new Message("This item has expired!", System.Drawing.Color.Red, Message.TopLeft));
                                    return;
                                }
                                if (client.Entity.ConquerPoints >= item.ConquerPointsCost && client.Inventory.Count < 40)
                                {
                                    client.Entity.TrySpendConquerPoints((uint)(item.ConquerPointsCost));
                                    Database.EntityTable.UpdateCps(client);
                                    usage.dwParam = client.Entity.UID;
                                    usage.dwExtraInfo3 = item.ConquerPointsCost;
                                    client.Send(usage);
                                    client.Inventory.Add(item.Item, Game.Enums.ItemUse.Add);
                                    Database.ClaimItemTable.Redeem(item, client);
                                    Database.DetainedItemTable.Claim(item, client);
                                    client.DeatinedItem.Remove(item.UID);
                                    if (Kernel.GamePool.ContainsKey(item.GainerUID))
                                    {
                                        GameState pClient;
                                        if (Kernel.GamePool.TryGetValue(item.GainerUID, out pClient))
                                        {
                                            if (pClient.Entity != null && pClient != null && pClient.ClaimableItem != null)
                                            {
                                                pClient.ClaimableItem[item.UID].OwnerUID = 500;
                                                pClient.ClaimableItem[item.UID].MakeItReadyToClaim();
                                                usage.dwParam = pClient.Entity.UID;
                                                usage.Action = ItemUsage.Mode.ClaimGear;
                                                pClient.Send(usage);
                                                pClient.ClaimableItem[item.UID].Send(pClient);
                                            }
                                        }
                                    }
                                    Message message = new Message(" " + client.Entity.Name + " has redeemed their gear and paid " + item.ConquerPointsCost + " CPs. Congratulations!", System.Drawing.Color.Wheat, Message.Talk);
                                    Kernel.SendWorldMessage(message, Server.GamePool);
                                }
                            }
                            else client.Send(new Message("The item you wish to redeem has already been redeemed.", System.Drawing.Color.Red, Message.TopLeft));
                            break;
                        }
                    case ItemUsage.Mode.ClaimGear:
                        {
                            if (client.Inventory.Count < 40)
                            {
                                var item = client.ClaimableItem[usage.UID];
                                if (item != null)
                                {
                                    if (item.Bound && item.OwnerUID != 500)
                                    {
                                        if (DateTime.Now >= item.Date.AddDays(7))
                                        {
                                            Database.ClaimItemTable.Claim(item, client);
                                            client.ClaimableItem.Remove(item.UID);

                                            usage.dwParam = client.Entity.UID;
                                            usage.dwExtraInfo3 = item.ConquerPointsCost;
                                            client.Send(usage);
                                        }
                                        client.Send(new Message("This item cannot be claimed!", System.Drawing.Color.Red, Message.TopLeft));
                                        return;
                                    }
                                    if (DateTime.Now < item.Date.AddDays(7) && item.OwnerUID != 500)
                                    {
                                        client.Send(new Message("This item has not expired. You cannot claim it yet!", System.Drawing.Color.Red, Message.TopLeft));
                                        return;
                                    }
                                    if (item.OwnerUID == 500)
                                        client.Entity.AddConquerPoints(item.ConquerPointsCost);
                                    else
                                    {
                                        client.Inventory.Add(item.Item, Game.Enums.ItemUse.Move);
                                        Message message = new Message("Thanks for arresting " + item.OwnerName + ", " + item.GainerName + ". The prisoner has redeemed their items, and you have received a large CP reward. Congratulations!", System.Drawing.Color.Wheat, Message.Talk);
                                        Kernel.SendWorldMessage(message, Server.GamePool);
                                    }
                                    Database.ClaimItemTable.Claim(item, client);
                                    client.ClaimableItem.Remove(item.UID);
                                    usage.dwParam = client.Entity.UID;
                                    usage.dwExtraInfo3 = item.ConquerPointsCost;
                                    client.Send(usage);
                                    Message message2 = new Message("Thanks for arresting " + client.Entity.Name + ". You have received " + item.ConquerPointsCost + " CPs. Congratulations!", System.Drawing.Color.Wheat, Message.Talk);
                                    Kernel.SendWorldMessage(message2, Server.GamePool);
                                }
                                else client.Send(new Message("The item you wish to claim has already been claimed.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                            break;
                        }
                    case ItemUsage.Mode.SocketerMan:
                        {
                            uint UID = BitConverter.ToUInt32(packet, 8);
                            if (packet[26] == 1)
                            {
                                if (client.Inventory.ContainsUID(BitConverter.ToUInt32(packet, 87)))
                                {
                                    ConquerItem item = null;
                                    if (client.Inventory.TryGetItem(UID, out item))
                                    {
                                        ushort pos = ItemHandler.ItemPosition(item.ID);
                                        if (pos == ConquerItem.Bottle || pos == ConquerItem.Fan || pos == ConquerItem.Garment || pos == ConquerItem.LeftWeaponAccessory || pos == ConquerItem.RightWeaponAccessory || pos == ConquerItem.Steed || pos == ConquerItem.SteedArmor || pos == ConquerItem.SteedCrop || pos == ConquerItem.Tower || pos == ConquerItem.Wing)
                                        {
                                            client.Send(new Message("Sorry, you cannot add a socket to this item!", System.Drawing.Color.Red, Message.Talk));
                                            return;
                                        }
                                        if (item.SocketOne == Game.Enums.Gem.NoSocket)
                                        {
                                            if (item.SocketOne == Game.Enums.Gem.NoSocket)
                                            {
                                                if (item.SocketOne == Game.Enums.Gem.NoSocket)
                                                    item.SocketOne = Game.Enums.Gem.EmptySocket;
                                                client.Inventory.Remove(BitConverter.ToUInt32(packet, 87), Game.Enums.ItemUse.Remove, true);
                                                item.Mode = Game.Enums.ItemMode.Update;
                                                item.Send(client);
                                                usage.dwParam = 1;
                                                client.LoadItemStats();
                                                Database.ConquerItemTable.UpdateSockets(item);
                                            }
                                        }
                                        else if (item.SocketTwo == Game.Enums.Gem.NoSocket)
                                        {
                                            uint rand = (uint)Kernel.Random.Next(1, 700);
                                            if (rand > 320)
                                            {
                                                if (item.SocketTwo == Game.Enums.Gem.NoSocket)
                                                    item.SocketTwo = Game.Enums.Gem.EmptySocket;
                                                client.Inventory.Remove(BitConverter.ToUInt32(packet, 87), Game.Enums.ItemUse.Remove, true);
                                                item.Mode = Game.Enums.ItemMode.Update;
                                                item.Send(client);
                                                usage.dwParam = 1;
                                                client.LoadItemStats();
                                                Database.ConquerItemTable.UpdateSockets(item);
                                            }
                                            else
                                            {
                                                usage.dwParam = 0;
                                                client.Inventory.Add(1200006, 0, 1);
                                                client.Inventory.Remove(BitConverter.ToUInt32(packet, 87), Game.Enums.ItemUse.Remove, true);
                                            }
                                        }
                                    }
                                }
                            }
                            if (packet[26] == 5)
                            {
                                ConquerItem item = null;
                                if (client.Inventory.TryGetItem(UID, out item))
                                {
                                    Dictionary<uint, uint> dbs = new Dictionary<uint, uint>();
                                    for (ushort i = 87; i < 87 + 4 * packet[26]; i += 4)
                                    {
                                        if (client.Inventory.ContainsUID(BitConverter.ToUInt32(packet, i)))
                                        {
                                            dbs.Add(BitConverter.ToUInt32(packet, i), i);
                                        }
                                    }
                                    if (client.Inventory.Contains(1088000, 5))
                                    {
                                        ushort pos = ItemHandler.ItemPosition(item.ID);
                                        if (pos == ConquerItem.Bottle || pos == ConquerItem.Fan || pos == ConquerItem.Garment || pos == ConquerItem.LeftWeaponAccessory || pos == ConquerItem.RightWeaponAccessory || pos == ConquerItem.Steed || pos == ConquerItem.SteedArmor || pos == ConquerItem.SteedCrop || pos == ConquerItem.Tower || pos == ConquerItem.Wing)
                                        {
                                            client.Send(new Message("Sorry can't make socket in this item !", System.Drawing.Color.Red, Message.Talk));
                                            return;
                                        }
                                        if (item.SocketOne == Game.Enums.Gem.NoSocket)
                                            return;
                                        if (item.SocketTwo == Game.Enums.Gem.NoSocket)
                                            item.SocketTwo = Game.Enums.Gem.EmptySocket;
                                        foreach (uint key in dbs.Keys)
                                        {
                                            if (client.Inventory.ContainsUID(key))
                                                client.Inventory.Remove(key, Game.Enums.ItemUse.Remove, true);
                                        }
                                        usage.dwParam = 1;
                                        item.Mode = Game.Enums.ItemMode.Update;
                                        item.Send(client);
                                        client.LoadItemStats();
                                        Database.ConquerItemTable.UpdateSockets(item);
                                    }
                                }
                            }
                            if (packet[26] == 7)
                            {
                                ConquerItem item = null;
                                if (client.Inventory.TryGetItem(UID, out item))
                                {
                                    if (item.SocketOne == Game.Enums.Gem.NoSocket) return;
                                    Dictionary<uint, uint> drill = new Dictionary<uint, uint>();
                                    for (ushort i = 87; i < 87 + 4 * packet[26]; i += 4)
                                    {
                                        if (client.Inventory.ContainsUID(BitConverter.ToUInt32(packet, i)))
                                        {
                                            drill.Add(BitConverter.ToUInt32(packet, i), i);
                                        }
                                    }
                                    if (client.Inventory.Contains(1200006, 7))
                                    {
                                        var itemtype = item.GetItemType();
                                        if (itemtype == ConquerItem.ItemTypes.CropID || itemtype == ConquerItem.ItemTypes.BottleID || itemtype == ConquerItem.ItemTypes.GarmentID_1 || itemtype == ConquerItem.ItemTypes.GarmentID_2 || itemtype == ConquerItem.ItemTypes.GemID || itemtype == ConquerItem.ItemTypes.LeftAccessory || itemtype == ConquerItem.ItemTypes.RightAccessory1 || itemtype == ConquerItem.ItemTypes.RightAccessory2 || itemtype == ConquerItem.ItemTypes.RightAccessory3 || itemtype == ConquerItem.ItemTypes.SteedArmorID)
                                            return;
                                        if (item.SocketTwo == Game.Enums.Gem.NoSocket)
                                            item.SocketTwo = Game.Enums.Gem.EmptySocket;
                                        foreach (uint key in drill.Keys)
                                        {
                                            if (client.Inventory.ContainsUID(key))
                                                client.Inventory.Remove(key, Game.Enums.ItemUse.Remove, true);
                                        }
                                        usage.dwParam = 1;
                                        item.Mode = Game.Enums.ItemMode.Update;
                                        item.Send(client);
                                        client.LoadItemStats();
                                        Database.ConquerItemTable.UpdateSockets(item);
                                    }
                                }
                            }
                            if (packet[26] == 12)
                            {
                                ConquerItem item = null;
                                if (client.Inventory.TryGetItem(UID, out item))
                                {
                                    ushort pos = ItemHandler.ItemPosition(item.ID);
                                    if (pos == ConquerItem.Bottle || pos == ConquerItem.Fan || pos == ConquerItem.Garment || pos == ConquerItem.LeftWeaponAccessory || pos == ConquerItem.RightWeaponAccessory || pos == ConquerItem.Steed || pos == ConquerItem.SteedArmor || pos == ConquerItem.SteedCrop || pos == ConquerItem.Tower || pos == ConquerItem.Wing)
                                    {
                                        client.Send(new Message("Sorry can't make socket in this item !", System.Drawing.Color.Red, Message.Talk));
                                        return;
                                    }
                                    Dictionary<uint, uint> dbs = new Dictionary<uint, uint>();
                                    for (ushort i = 87; i < 87 + 4 * packet[26]; i += 4)
                                    {
                                        if (client.Inventory.ContainsUID(BitConverter.ToUInt32(packet, i)))
                                        {
                                            dbs.Add(BitConverter.ToUInt32(packet, i), i);
                                        }
                                    }
                                    if (client.Inventory.Contains(1088000, 12))
                                    {
                                        var itemtype = item.GetItemType();
                                        if (itemtype == ConquerItem.ItemTypes.CropID || itemtype == ConquerItem.ItemTypes.BottleID || itemtype == ConquerItem.ItemTypes.GarmentID_1 || itemtype == ConquerItem.ItemTypes.GarmentID_2 || itemtype == ConquerItem.ItemTypes.GemID || itemtype == ConquerItem.ItemTypes.LeftAccessory || itemtype == ConquerItem.ItemTypes.RightAccessory1 || itemtype == ConquerItem.ItemTypes.RightAccessory2 || itemtype == ConquerItem.ItemTypes.RightAccessory3 || itemtype == ConquerItem.ItemTypes.SteedArmorID)
                                            return;
                                        if (item.SocketOne != Game.Enums.Gem.NoSocket)
                                            return;
                                        if (item.SocketOne == Game.Enums.Gem.NoSocket)
                                            item.SocketOne = Game.Enums.Gem.EmptySocket;
                                        foreach (uint key in dbs.Keys)
                                        {
                                            if (client.Inventory.ContainsUID(key))
                                                client.Inventory.Remove(key, Game.Enums.ItemUse.Remove, true);
                                        }
                                        usage.dwParam = 1;
                                        item.Mode = Game.Enums.ItemMode.Update;
                                        item.Send(client);
                                        client.LoadItemStats();
                                        Database.ConquerItemTable.UpdateSockets(item);
                                    }
                                }

                            }
                            client.Send(packet);
                            break;
                        }
                    case ItemUsage.Mode.Bless:
                        {
                            uint ItemAdd = BitConverter.ToUInt32(packet, 8);
                            ConquerItem item_new = null;
                            if (client.Inventory.TryGetItem(ItemAdd, out item_new))
                            {
                                var itemtype = item_new.GetItemType();
                                if (itemtype == ConquerItem.ItemTypes.GemID) return;
                                ushort pos = ItemHandler.ItemPosition(item_new.ID);
                                if (pos == ConquerItem.Bottle || pos == ConquerItem.Fan || pos == ConquerItem.Garment || pos == ConquerItem.LeftWeaponAccessory || pos == ConquerItem.RightWeaponAccessory || pos == ConquerItem.Steed || pos == ConquerItem.SteedArmor || pos == ConquerItem.SteedCrop || pos == ConquerItem.Tower || pos == ConquerItem.Wing)
                                {
                                    client.Send(new Message("You cannot bless this item!", System.Drawing.Color.Red, Message.Talk));
                                    return;
                                }
                                if (item_new.Bless >= 7) return;
                                Queue<uint> amount = new Queue<uint>(packet[26]);
                                for (ushort i = 87; i < 87 + 4 * packet[26]; i += 4)
                                {
                                    uint uid = BitConverter.ToUInt32(packet, i);
                                    if (client.Inventory.ContainsUID(uid))
                                        amount.Enqueue(uid);
                                    else return;
                                }
                                byte oldbless = item_new.Bless;
                                if (item_new.Bless == 0 && amount.Count == 5)
                                {
                                    item_new.Bless = 1;
                                    client.Entity.Update(10, "Aegis1", true);
                                }
                                else if (item_new.Bless == 1 && amount.Count == 1)
                                {

                                    item_new.Bless = 3;
                                    client.Entity.Update(10, "Aegis2", true);
                                }
                                else if (item_new.Bless == 3 && amount.Count == 3)
                                {
                                    item_new.Bless = 5;
                                    client.Entity.Update(10, "Aegis3", true);
                                }
                                else if (item_new.Bless == 5 && amount.Count == 5)
                                {
                                    item_new.Bless = 7;
                                    client.Entity.Update(10, "Aegis4", true);
                                }
                                if (oldbless == item_new.Bless) return;
                                while (amount.Count != 0)
                                    client.Inventory.Remove(amount.Dequeue(), Game.Enums.ItemUse.Remove, true);
                                item_new.Mode = Game.Enums.ItemMode.Update;
                                item_new.Send(client);
                                usage.dwParam = 1;
                                Database.ConquerItemTable.UpdateBless(item_new);
                            }
                            client.Send(packet);
                            break;
                        }
                    case ItemUsage.Mode.GemCompose:
                        {
                            UInt32 Ident = usage.UID;
                            client.Inventory.Remove(Ident, 15);
                            client.Inventory.Add(Ident + 1, 0, 1);
                            usage.dwParam = 1;
                            client.Send(usage);
                            break;
                        }
                    case ItemUsage.Mode.ToristSuper:
                        {
                            if (client.Entity.Money >= 100000)
                            {
                                client.Inventory.Remove(700002, 1);
                                client.Inventory.Remove(700012, 1);
                                client.Inventory.Remove(700022, 1);
                                client.Inventory.Remove(700032, 1);
                                client.Inventory.Remove(700042, 1);
                                client.Inventory.Remove(700052, 1);
                                client.Inventory.Remove(700062, 1);
                                client.Entity.Money -= 100000;
                                client.Inventory.Add(700072, 0, 1);
                                client.Send(usage);
                                usage.dwParam = 1;
                            }
                            else
                            {
                                client.Send("Sorry, you do not have 100,000 silver!");
                            }
                            break;
                        }
                    case ItemUsage.Mode.SplitStack:
                        {
                            ConquerItem mainItem = null;
                            ConquerItem minorItem = new ConquerItem(true);
                            Database.ConquerItemInformation infos = null;
                            minorItem.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                            if (client.Inventory.TryGetItem(usage.UID, out mainItem))
                            {
                                infos = new Database.ConquerItemInformation(mainItem.ID, 0);
                                if (mainItem.StackSize > 1 && mainItem.StackSize <= infos.BaseInformation.StackSize)
                                {
                                    if (client.Inventory.Count < 40)
                                    {
                                        ushort Amount = (ushort)usage.dwParam;

                                        mainItem.StackSize -= Amount;
                                        mainItem.Mode = Game.Enums.ItemMode.Update;
                                        mainItem.Send(client);
                                        mainItem.Mode = Game.Enums.ItemMode.Default;
                                        Database.ConquerItemTable.UpdateStack(mainItem);
                                        minorItem.ID = mainItem.ID;
                                        minorItem.StackSize += Amount;
                                        minorItem.Durability = mainItem.Durability;
                                        client.Inventory.Add(minorItem, Game.Enums.ItemUse.CreateAndAdd);
                                    }
                                }
                            }
                            break;
                        }
                    case ItemUsage.Mode.MergeStackableItems:
                        {
                            ConquerItem mainItem = null;
                            ConquerItem minorItem = new ConquerItem(true);
                            Database.ConquerItemInformation infos = null;
                            minorItem.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                            if (client.Inventory.TryGetItem(usage.UID, out mainItem) && client.Inventory.TryGetItem(usage.dwParam, out minorItem))
                            {
                                if (mainItem.ID == minorItem.ID)
                                {
                                    infos = new Database.ConquerItemInformation(mainItem.ID, 0);
                                    if (mainItem.StackSize < 1)
                                        mainItem.StackSize = 1;
                                    if (minorItem.StackSize < 1)
                                        minorItem.StackSize = 1;

                                    if ((mainItem.StackSize + minorItem.StackSize) <= infos.BaseInformation.StackSize)
                                    {
                                        mainItem.StackSize += minorItem.StackSize;
                                        mainItem.Mode = Game.Enums.ItemMode.Update;
                                        mainItem.Send(client);
                                        mainItem.Mode = Game.Enums.ItemMode.Default;
                                        Database.ConquerItemTable.UpdateStack(mainItem);
                                        client.Inventory.Remove(minorItem, Game.Enums.ItemUse.Remove);
                                    }
                                }
                            }
                            break;
                        }
                    case ItemUsage.Mode.Unknown:
                        {
                            break;
                        }
                    case ItemUsage.Mode.LowerEquipment:
                        {
                            ConquerItem item = null;
                            if (client.Inventory.TryGetItem(usage.UID, out item))
                            {
                                if (ItemHandler.IsArrow(item.ID))
                                    return;
                                ConquerItem upgrade = null;
                                if (client.Inventory.TryGetItem(usage.UID, out upgrade))
                                {
                                    if (client.Entity.ConquerPoints < 54)
                                    {
                                        client.Send("You do not have 54 CPs.");
                                        return;
                                    }
                                    client.Entity.TrySpendConquerPoints((uint)(54));
                                    Database.ConquerItemInformation infos = new Database.ConquerItemInformation(item.ID, item.Plus);
                                    if (infos.BaseInformation.Level <= 15) return;
                                    int startwith = (int)(infos.BaseInformation.ID / 1000);
                                    int endwith = (int)(infos.BaseInformation.ID % 10);
                                    var sitem = Database.ConquerItemInformation.BaseInformations.Values.Where(x => (x.ID % 10) == endwith && (x.ID / 1000) == startwith && x.Level >= 15).OrderBy(y => y.Level).First();
                                    item.ID = sitem.ID;
                                    infos = new Database.ConquerItemInformation(item.ID, item.Plus);
                                    item.Durability = item.MaximDurability = infos.BaseInformation.Durability;
                                    Database.ConquerItemTable.UpdateItemID(item, client);
                                    Database.ConquerItemTable.UpdateDurabilityItem(item);
                                    item.Mode = Game.Enums.ItemMode.Update;
                                    item.Send(client);
                                    item = ItemHandler.ItemSocket(item, 2);
                                }
                            }
                            break;
                        }
                    default: if (client.Account.State == Database.AccountTable.AccountState.Administrator)
                            client.Send(new Message("[ItemUsage] Unknown Action: " + usage.Action, Message.System)); break;
                }
            }
        }
    }
}
 
#endregion