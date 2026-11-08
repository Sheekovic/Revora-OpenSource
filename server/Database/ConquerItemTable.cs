using System;
using System.Linq;
using Revora.Network.GamePackets;
using System.Collections.Generic;
using Conquer.Database;
using Revora.Game;

namespace Revora.Database
{
    public unsafe class ConquerItemTable
    {
        #region Legacy Load (do not delete)
      /*  public static void LoadItems(Client.GameState client)
        {
            client.Player.StorageItems = new Dictionary<uint, ConquerItem>();
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("items").Where("EntityID", client.Player.UID))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    var item = deserialzeItem(reader);
                    if (Database.ConquerItemInformation.BaseInformations.ContainsKey(item.UID)) continue;
                    item.RefineItem = reader.ReadUInt32("RefineryItem");
                    UInt64 rTime = reader.ReadUInt32("RefineryTime");
                    if (item.RefineItem > 0 && rTime != 0)
                    {
                        item.RefineryTime = Kernel.FromDateTimeInt(rTime);
                        if (DateTime.Now > item.RefineryTime)
                        {
                            item.RefineryTime = new DateTime(0);
                            item.RefineItem = 0;
                        }
                    }
                    if ((item.ID == 1100003) || (item.ID == 1100006) || (item.ID == 1100009))
                    {
                        if (!client.Warehouses.ContainsKey((Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.UID))
                        {
                            client.Warehouses.Add((Revora.Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.UID, new Game.ConquerStructures.Warehouse(client, (Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.UID));
                        }
                    }
                    if (item.ID == 750000)
                    {
                        client.Player.kilid = item.MaximDurability;
                    }
                    if (item.ID == 720828)
                    {
                        string agate = reader.ReadString("Agate");
                        uint count = 0;
                        string[] maps = agate.Split('#');
                        foreach (string one in maps)
                        {
                            if (one.Length > 6)
                            {
                                item.Agate_map.Add(count, one);
                                count++;
                            }
                        }
                    }
                    ItemAddingTable.GetAddingsForItem(item);
                    if (item.InWardrobe)
                        client.Player.StorageItems.Add(item.UID, item);
                    if (item.Warehouse == 0)
                    {
                        switch (item.Position)
                        {
                            case 0:
                                client.Inventory.Add(item, Enums.ItemUse.None);
                                goto Label_03F9;
                            case 1:
                            case 2:
                            case 3:
                            case 4:
                            case 5:
                            case 6:
                            case 7:
                            case 8:
                            case 9:
                            case 10:
                            case 11:
                            case 12:
                            case 15:
                            case 16:
                            case 17:
                            case 18:
                            case 19:
                            case 21:
                            case 22:
                            case 23:
                            case 24:
                            case 25:
                            case 26:
                            case 27:
                            case 28:
                            case 29:
                                if (!client.Equipment.Free((byte)item.Position))
                                {
                                    break;
                                }
                                client.Equipment.Add(item, Enums.ItemUse.None);
                                goto Label_03F9;

                            case 13:
                            case 14:
                                goto Label_03F9;

                            default:
                                goto Label_03F9;
                        }
                        if (client.Inventory.Count < 40)
                        {
                            item.Position = 0;
                            client.Inventory.Add(item, Enums.ItemUse.None);
                            if (client.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.StoneCity].Count < 60)
                            {
                                client.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.StoneCity].Add(item);
                            }
                            UpdatePosition(item);
                        }
                    }
                    else
                    {
                        if (client.Warehouses.ContainsKey((Revora.Game.ConquerStructures.Warehouse.WarehouseID)item.Warehouse))
                        {
                            client.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.Warehouse].Add(item);
                        }
                        else
                        {

                            client.Warehouses.Add((Revora.Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.Warehouse, new Revora.Game.ConquerStructures.Warehouse(client, (Revora.Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.Warehouse));
                            client.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.Warehouse].Add(item);
                        }

                    }
                Label_03F9:
                    if ((item.ID == 1100003) || (item.ID == 1100006) || (item.ID == 1100009))
                    {
                        if (!client.Warehouses.ContainsKey((Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.UID))
                        {
                            client.Warehouses.Add((Revora.Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.UID, new Revora.Game.ConquerStructures.Warehouse(client, (Revora.Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.UID));
                        }
                    }
                }
            }
        }*/
        #endregion
        public static void LoadItems(Client.GameState client)
        {
            Revora.Game.Features.GuildPrizeRollover.BeforeLoad(client);
            Revora.Game.Features.HouseFurniture.LoadBoxes(client);
            Revora.Game.ConquerStructures.Warehouse.LoadLegacyBags(client);
            client.Entity.StorageItems = new Dictionary<uint, ConquerItem>();
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("items").Where("EntityID", client.Entity.UID))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    var item = deserialzeItem(reader);
                    if (!ConquerItemInformation.BaseInformations.ContainsKey(item.ID))
                        continue;

                    if ((byte)ItemHandler.GetPositionFromID(item.ID) == ConquerItem.Garment || (byte)ItemHandler.GetPositionFromID(item.UID) == ConquerItem.SteedCrop || (byte)ItemHandler.GetPositionFromID(item.UID) == ConquerItem.SteedArmor || (byte)ItemHandler.GetPositionFromID(item.UID) == ConquerItem.Wing || (byte)ItemHandler.GetPositionFromID(item.UID) == ConquerItem.Bottle)
                    {
                        if (item.SocketOne != Game.Enums.Gem.NoSocket || item.SocketTwo != Game.Enums.Gem.NoSocket)
                        {
                            item.SocketOne = Game.Enums.Gem.NoSocket;
                            item.SocketTwo = Game.Enums.Gem.NoSocket;
                        }

                    }
                    #region WareHouse
                    if (item.InWardrobe)
                        client.Entity.StorageItems.Add(item.UID, item);
                    else if (item.Warehouse == 0)
                    {
                        switch (item.Position)
                        {
                            //Primary
                            case 101:
                            case 102:
                            case 103:
                            case 104:
                            case 105:
                            case 106:
                            case 107:
                            case 108:
                            case 211://unequiped rune

                            //Secondary
                            case 121:
                            case 122:
                            case 123:
                            case 124:
                            case 125:
                                {
                                    if (item.Position != 211 && client.RunesItem.Values.Any(i => i.Position == item.Position))
                                    {
                                        client.Entity.PersistenceRequiresReload = true;
                                        client.Action = 0;
                                        throw new InvalidOperationException("Duplicate persisted rune slot requires repair.");
                                    }
                                    ConquerItemInformation infos = new ConquerItemInformation(item.ID, 0);
                                    item.RuneSkill = infos.BaseInformation.subtype;
                                    client.RunesItem.Add(item.UID, item);
                                    break;
                                }
                            case 0:
                                {
                                    // Bag runes use the same catalog-derived skill metadata as
                                    // equipped runes; it is not stored in the items row.
                                    if (RuneProgress.IsRune(item.ID))
                                        item.RuneSkill = ConquerItemInformation.BaseInformations[item.ID].subtype;
                                    client.Inventory.Add(item, Enums.ItemUse.None); break;
                                }
                            default:
                                if (item.Position > 29) continue;
                                if (client.Equipment.Free((byte)item.Position))
                                {
                                    client.Equipment.Add(item, Enums.ItemUse.None);
                                    item.IsWorn = true;
                                }
                                else
                                {
                                    if (client.Inventory.Count < 40)
                                    {
                                        item.Position = 0;
                                        client.Inventory.Add(item, Enums.ItemUse.None);
                                        if (client.Warehouses[Game.ConquerStructures.Warehouse.WarehouseID.Market].Count < 20)
                                            client.Warehouses[Game.ConquerStructures.Warehouse.WarehouseID.Market].Add(item);
                                        UpdatePosition(item);
                                    }
                                }
                                break;
                        }
                    }
                    else if (item != null)
                    {
                        Game.ConquerStructures.Warehouse.WarehouseID whID = (Game.ConquerStructures.Warehouse.WarehouseID)item.Warehouse;
                        if (client.Warehouses.ContainsKey(whID))
                        {
                            client.Warehouses[whID].Add(item);
                        }
                        else
                        {
                            using (var cmdx = new MySqlCommand(MySqlCommandType.SELECT).Select("items").Where("Uid", (uint)item.Warehouse))
                            using (var readerx = new MySqlReader(cmdx))
                                if (readerx.Read())
                                {
                                    client.Warehouses.Add((Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.Warehouse, new Game.ConquerStructures.Warehouse(client, (Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.Warehouse));
                                    client.Warehouses[(Game.ConquerStructures.Warehouse.WarehouseID)(uint)whID].Add(item);
                                }
                        }
                    }
                    #endregion
                    #region WareHouse(CoatStorage) -> DISABLED, DO NOT DELETE
                /*    if (item.Warehouse == 0)
                    {
                        switch (item.Position)
                        {
                            case 0:
                                {
                                    if (item.InWardrobe)
                                        client.Player.StorageItems.Add(item.UID, item);
                                    else
                                    {
                                        client.Inventory.Add(item, Game.Enums.ItemUse.None);
                                        break;
                                    }
                                    break;
                                }
                            default:
                                if (item.Position > 40) continue;
                                if (client.Equipment.Free((byte)item.Position))
                                    client.Equipment.Add(item, Game.Enums.ItemUse.None);
                                else
                                {
                                    if (client.Inventory.Count < 40)
                                    {
                                        if (item.InWardrobe)
                                        {
                                        }
                                        item.Position = 0;
                                        client.Inventory.Add(item, Game.Enums.ItemUse.None);
                                        if (client.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.StoneCity].Count < 20)
                                            client.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.StoneCity].Add(item);
                                        UpdatePosition(item);
                                    }
                                }
                                break;
                        }
                    }
                    else
                    {
                        if (item != null)
                        {
                            Game.ConquerStructures.Warehouse.WarehouseID whID = (Game.ConquerStructures.Warehouse.WarehouseID)item.Warehouse;
                            if (client.Warehouses.ContainsKey(whID))
                            {
                                client.Warehouses[whID].Add(item);
                            }
                            else
                            {
                                using (var cmdx = new MySqlCommand(MySqlCommandType.SELECT).Select("items").Where("Uid", (uint)item.Warehouse))
                                using (var readerx = new MySqlReader(cmdx))
                                    if (readerx.Read())
                                    {
                                        client.Warehouses.Add((Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.Warehouse, new Game.ConquerStructures.Warehouse(client, (Game.ConquerStructures.Warehouse.WarehouseID)(uint)item.Warehouse));
                                        client.Warehouses[(Game.ConquerStructures.Warehouse.WarehouseID)(uint)whID].Add(item);
                                    }
                            }
                        }
                    }*/
                    #endregion WareHouse

                    if (item.ID == 720828)
                    {
                        string str = reader.ReadString("agate");
                        uint key = 0;
                        string[] strArray = str.Split(new char[] { '#' });
                        foreach (string str2 in strArray)
                        {
                            if (str2.Length > 6)
                            {
                                item.Agate_map.Add(key, str2);
                                key++;
                            }
                        }
                    }
                }
            }
        }
        #region Auction House
        public static void SaveAuctionItems()
        {
            // The auction engine persists each operation transactionally.
            if (Kernel.AuctionItems.Count != 0) throw new InvalidOperationException("Legacy auction cache requires recovery.");
        }
        public static void LoadAuctionItems()
        {
            Revora.Game.Features.AuctionGateway.Initialize();
        }
        public static void AddItem(ref ConquerItem Item, uint UID)
        {
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("items"))
                    cmd.Insert("ID", Item.ID).Insert("UID", Item.UID)
                        .Insert("Plus", Item.Plus).Insert("Bless", Item.Bless)
                        .Insert("Enchant", Item.Enchant).Insert("SocketOne", (byte)Item.SocketOne)
                        .Insert("SocketTwo", (byte)Item.SocketTwo).Insert("Durability", Item.Durability)
                        .Insert("MaximDurability", Item.MaximDurability).Insert("SocketProgress", Item.SocketProgress)
                        .Insert("PlusProgress", Item.PlusProgress).Insert("Effect", (ushort)Item.Effect)
                        .Insert("Bound", Item.Bound)
                        .Insert("DayStamp", Item.DayStamp.ToBinary())
                        .Insert("Days", Item.Days)
                        .Insert("Locked", Item.Lock)
                        .Insert("UnlockEnd", Item.UnlockEnd.Ticks)
                        .Insert("Suspicious", Item.Suspicious)
                        .Insert("SuspiciousStart", Item.SuspiciousStart.Ticks)
                        .Insert("NextSteedColor", 0)
                        .Insert("Color", (ushort)Item.Color)
                        .Insert("Position", Item.Position)
                        .Insert("StackSize", Item.StackSize)
                        .Insert("RefineryItem", Item.RefineItem)
                        .Insert("RefineryTime", Item.RefineryTime.Ticks)
                        .Insert("EntityID", UID)
                        .Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                DeleteItem(Item.UID);
            }
        }
        #endregion
        public static ConquerItem deserialzeItem(MySqlReader reader)
        {
            ConquerItem item = new Network.GamePackets.ConquerItem(true);
            item.ID = reader.ReadUInt32("Id");
            item.UID = reader.ReadUInt32("Uid");
            item.Durability = reader.ReadUInt16("Durability");
            item.MaximDurability = reader.ReadUInt16("MaximDurability");
            item.Position = reader.ReadByte("Position");
            item.Agate = reader.ReadString("Agate");
            item.SocketProgress = reader.ReadUInt32("SocketProgress");
            item.PlusProgress = reader.ReadUInt32("PlusProgress");
            item.SocketOne = (Game.Enums.Gem)reader.ReadUInt16("SocketOne");
            item.SocketTwo = (Game.Enums.Gem)reader.ReadUInt16("SocketTwo");
            item.Effect = (Game.Enums.ItemEffect)reader.ReadUInt16("Effect");
            item.Mode = Game.Enums.ItemMode.Default;
            item.Plus = reader.ReadByte("Plus");
            item.Bless = reader.ReadByte("Bless");
            item.Bound = reader.ReadBoolean("Bound");
            item.Inscribed = reader.ReadBoolean("Inscribed");
            item.Enchant = reader.ReadByte("Enchant");
            item.Lock = reader.ReadByte("Locked");
            item.UnlockEnd = DateTime.FromBinary(reader.ReadInt64("UnlockEnd"));
            item.Suspicious = reader.ReadBoolean("Suspicious");
            item.SuspiciousStart = DateTime.FromBinary(reader.ReadInt64("SuspiciousStart"));
            item.Color = (Game.Enums.Color)reader.ReadUInt32("Color");
            item.Warehouse = reader.ReadUInt32("Warehouse");
            item.StackSize = reader.ReadUInt16("StackSize");
            item.RefineItem = reader.ReadUInt32("RefineryItem");
            Int64 rTime = reader.ReadInt64("RefineryTime");
            item.InWardrobe = reader.ReadBoolean("InWardrobe");
            item.DayStamp = DateTime.FromBinary(reader.ReadInt64("DayStamp"));
            item.PerfectionLevel = reader.ReadUInt32("PerfectionLevel");
            item.PerfectionOwner = reader.ReadString("PerfectionOwner");
            item.PerfectionOwnerGuid = reader.ReadUInt32("PerfectionOwnerGuid");
            item.PerfectionProgress = reader.ReadUInt32("PerfectionProgress");
            item.Signature = reader.ReadString("Signature");
            item.RunesEXP = reader.ReadUInt32("RunesEXP");
            item.RelicAttrib1 = reader.ReadUInt32("RelicAttrib1");
            item.RelicAttrib2 = reader.ReadUInt32("RelicAttrib2");
            item.RelicAttrib3 = reader.ReadUInt32("RelicAttrib3");
            item.RelicAttrib4 = reader.ReadUInt32("RelicAttrib4");
            item.RelicAttrib5 = reader.ReadUInt32("RelicAttrib5");
            item.Days = reader.ReadByte("Days");
            if (item.Days != 0)
            {
                uint num;
                if (item.Days != 0)
                {
                    if (DateTime.Now >= item.DayStamp.AddDays((double)item.Days))
                    {
                        DeleteItem(item.UID);
                        ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                    }
                    else if (item.Days > 0)
                    {
                        TimeSpan span = new TimeSpan(item.DayStamp.AddSeconds((double)(item.Days * 24 * 60 * 60)).Ticks);
                        TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                        num = (uint)(span.TotalSeconds - span2.TotalSeconds);
                        item.TimeLeftInMinutes = Convert.ToUInt32(num);
                        ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                    }
                    else
                    {
                        TimeSpan span = new TimeSpan(item.DayStamp.AddSeconds(604800.0).Ticks);
                        TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                        num = (uint)(span.TotalSeconds - span2.TotalSeconds);
                        item.TimeLeftInMinutes = Convert.ToUInt32(num);
                        ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                    }
                }
                else if (DateTime.Now >= item.DayStamp.AddDays(7.0))
                {
                    DeleteItem(item.UID);
                    ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                }
                else
                {
                    TimeSpan span = new TimeSpan(item.DayStamp.AddSeconds(604800.0).Ticks);
                    TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                    num = (uint)(span.TotalSeconds - span2.TotalSeconds);
                    item.TimeLeftInMinutes = Convert.ToUInt32(num);
                    ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                }
            } if (item.Days != 0)
            {
                uint num;
                if (item.Days != 0)
                {
                    if (DateTime.Now >= item.DayStamp.AddDays((double)item.Days))
                    {
                        DeleteItem(item.UID);
                        ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                    }
                    else if (item.Days > 0)
                    {
                        TimeSpan span = new TimeSpan(item.DayStamp.AddSeconds((double)(item.Days * 24 * 60 * 60)).Ticks);
                        TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                        num = (uint)(span.TotalSeconds - span2.TotalSeconds);
                        item.TimeLeftInMinutes = Convert.ToUInt32(num);
                        ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                    }
                    else
                    {
                        TimeSpan span = new TimeSpan(item.DayStamp.AddSeconds(604800.0).Ticks);
                        TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                        num = (uint)(span.TotalSeconds - span2.TotalSeconds);
                        item.TimeLeftInMinutes = Convert.ToUInt32(num);
                        ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                    }
                }
                else if (DateTime.Now >= item.DayStamp.AddDays(7.0))
                {
                    DeleteItem(item.UID);
                    ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                }
                else
                {
                    TimeSpan span = new TimeSpan(item.DayStamp.AddSeconds(604800.0).Ticks);
                    TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                    num = (uint)(span.TotalSeconds - span2.TotalSeconds);
                    item.TimeLeftInMinutes = Convert.ToUInt32(num);
                    ConquerItemBaseInformation information = ConquerItemInformation.BaseInformations[item.ID];
                }
            }
            if (item.RefineItem > 0 && rTime != 0)
            {
                item.RefineryTime = DateTime.FromBinary(rTime);
                if (DateTime.Now > item.RefineryTime)
                {
                    item.RefineryTime = new DateTime(0);
                    item.RefineItem = 0;
                }
            }
            if (item.Lock == 2)
                if (DateTime.Now >= item.UnlockEnd)
                    item.Lock = 0;

            ItemAddingTable.GetAddingsForItem(item);

            if (item.ID == 300000)
            {
                uint NextSteedColor = reader.ReadUInt32("NextSteedColor");
                item.NextGreen = (byte)(NextSteedColor & 0xFF);
                item.NextBlue = (byte)((NextSteedColor >> 8) & 0xFF);
                item.NextRed = (byte)((NextSteedColor >> 16) & 0xFF);
            }
            return item;
        }
        public static ConquerItem LoadItem(uint uid)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("items").Where("uid", uid))
            using (var reader = new MySqlReader(cmd))
            {
                if (!reader.Read()) return new ConquerItem(true);
                var item = deserialzeItem(reader);
                item.Inscribed = reader.ReadBoolean("Inscribed");
                if (item.ID == 720828)
                {
                    uint index = 0;
                    foreach (var entry in item.Agate.Split('#'))
                        if (entry.Length > 6) item.Agate_map.Add(index++, entry);
                }
                return item;
            }
        }
        public static void UpdatePerfection(ConquerItem item)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
            {
                cmd.Update("items").Set("PerfectionLevel", item.PerfectionLevel).Set("PerfectionOwner", item.PerfectionOwner).Set("PerfectionOwnerGuid", item.PerfectionOwnerGuid)
                    .Set("PerfectionProgress", item.PerfectionProgress).Set("Signature", item.Signature).Where("UID", item.UID).Execute();
            }
        }
        public static void AddItem1(ref ConquerItem Item, Client.GameState client)
        {
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("items"))
                    cmd.Insert("ID", Item.ID)
                        .Insert("UID", Item.UID)
                        .Insert("Plus", Item.Plus)
                        .Insert("Bless", Item.Bless)
                        .Insert("Enchant", Item.Enchant)
                        .Insert("SocketOne", (byte)Item.SocketOne)
                        .Insert("SocketTwo", (byte)Item.SocketTwo)
                        .Insert("Durability", Item.Durability)
                        .Insert("MaximDurability", Item.MaximDurability)
                        .Insert("SocketProgress", Item.SocketProgress)
                        .Insert("PlusProgress", Item.PlusProgress)
                        .Insert("Effect", (ushort)Item.Effect)
                        .Insert("Bound", Item.Bound)
                        .Insert("DayStamp", Item.DayStamp.ToBinary())
                        .Insert("Days", Item.Days).Insert("Locked", Item.Lock)
                        .Insert("UnlockEnd", Item.UnlockEnd.Ticks)
                        .Insert("Suspicious", Item.Suspicious)
                        .Insert("SuspiciousStart", Item.SuspiciousStart.Ticks)
                        .Insert("Color", (ushort)Item.Color)
                        .Insert("Position", Item.Position)
                        .Insert("StackSize", Item.StackSize)
                        .Insert("RefineryItem", Item.RefineItem)
                        .Insert("RefineryTime", Item.RefineryTime.Ticks)
                        .Insert("RunesEXP", Item.RunesEXP)
                        .Insert("RelicAttrib1", Item.RelicAttrib1)
                        .Insert("RelicAttrib2", Item.RelicAttrib2)
                        .Insert("RelicAttrib3", Item.RelicAttrib3)
                        .Insert("RelicAttrib4", Item.RelicAttrib4)
                        .Insert("RelicAttrib5", Item.RelicAttrib5)
                        .Insert("EntityID", client.Entity.UID)
                        .Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                DeleteItem(Item.UID);
            }
        }
        //public static void AddItem(ref ConquerItem Item, Client.GameState client)
        //{
        //    try
        //    {
        //    Change:
        //        Item.UID = Network.GamePackets.ConquerItem.ItemUID.Next;
        //        if (Exist(Item.UID))
        //            goto Change;
        //        Conquer.Database.Item.Insert(client.Entity.UID,
        //        Item.ID, Item.UID, Item.Plus, Item.Bless,
        //        Item.Enchant, (byte)Item.SocketOne, (byte)Item.SocketTwo,
        //        Item.Durability, Item.MaximDurability, Item.SocketProgress,
        //        Item.PlusProgress, (short)Item.Effect, Item.Bound,
        //        Item.Lock, (ulong)Item.UnlockEnd.ToBinary(), Item.Suspicious,
        //        (ulong)Item.SuspiciousStart.ToBinary(),
        //        (uint)Item.Color, Item.Position, Item.Warehouse, Item.StackSize, Item.Inscribed, Item.RefineItem, Kernel.ToDateTimeInt(Item.RefineryTime),);
        //    }
        //    catch
        //    {
        //    Change:
        //        Item.UID = Network.GamePackets.ConquerItem.ItemUID.Next;
        //        if (Exist(Item.UID))
        //            goto Change;
        //        Conquer.Database.Item.Insert(client.Entity.UID,
        //        Item.ID, Item.UID, Item.Plus, Item.Bless,
        //        Item.Enchant, (byte)Item.SocketOne, (byte)Item.SocketTwo,
        //        Item.Durability, Item.MaximDurability, Item.SocketProgress,
        //        Item.PlusProgress, (short)Item.Effect, Item.Bound,
        //        Item.Lock, (ulong)Item.UnlockEnd.ToBinary(), Item.Suspicious,
        //        (ulong)Item.SuspiciousStart.ToBinary(),
        //        (uint)Item.Color, Item.Position, Item.Warehouse, Item.StackSize, Item.Inscribed, Item.RefineItem, Kernel.ToDateTimeInt(Item.RefineryTime));
        //    }
        //}
        public static void AddItem(ref ConquerItem Item, Client.GameState client)
        {
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("items"))
                    cmd.Insert("ID", Item.ID)
                        .Insert("UID", Item.UID)
                        .Insert("Plus", Item.Plus)
                        .Insert("Bless", Item.Bless)
                        .Insert("Enchant", Item.Enchant)
                        .Insert("SocketOne", (byte)Item.SocketOne)
                        .Insert("SocketTwo", (byte)Item.SocketTwo)
                        .Insert("Durability", Item.Durability)
                        .Insert("MaximDurability", Item.MaximDurability)
                        .Insert("SocketProgress", Item.SocketProgress)
                        .Insert("PlusProgress", Item.PlusProgress)
                        .Insert("Effect", (ushort)Item.Effect)
                        .Insert("Bound", Item.Bound)
                        .Insert("DayStamp", Item.DayStamp.ToBinary())
                        .Insert("Days", Item.Days)
                        .Insert("Locked", Item.Lock)
                        .Insert("UnlockEnd", Item.UnlockEnd.Ticks)
                        .Insert("SuspiciousStart", Item.SuspiciousStart.Ticks)
                        .Insert("Color", (ushort)Item.Color)
                        .Insert("Position", Item.Position)
                        .Insert("StackSize", Item.StackSize)
                        .Insert("RefineryItem", Item.RefineItem)
                        .Insert("RefineryTime", Item.RefineryTime.Ticks)
                        .Insert("RunesEXP", Item.RunesEXP)
                        .Insert("RelicAttrib1", Item.RelicAttrib1)
                        .Insert("RelicAttrib2", Item.RelicAttrib2)
                        .Insert("RelicAttrib3", Item.RelicAttrib3)
                        .Insert("RelicAttrib4", Item.RelicAttrib4)
                        .Insert("RelicAttrib5", Item.RelicAttrib5)
                        .Insert("EntityID", client.Entity.UID)
                        .Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine("[items] add uid=" + Item.UID + " " + e.Message);
                DeleteItem(Item.UID);
            }
        }
        private static void UpdateData(ConquerItem Item, string column, object value)
        {
            UpdateData(Item.UID, column, value);
        }
        private static void UpdateData(uint UID, string column, object value)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("items").Set(column, value.ToString())
                    .Where("UID", UID).Execute();
        }
        public static void UpdateBless(ConquerItem Item)
        {
            UpdateData(Item, "Bless", Item.Bless);
        }
        public static void UpdateItemAgate(ConquerItem Item)
        {
            string agate = "";
            if (Item.ID == 720828)
            {
                foreach (string coord in Item.Agate_map.Values)
                {
                    agate += coord + "#";
                    UpdateData(Item, "agate", agate);

                }
            }
        }
        public static void UpdateColor(ConquerItem Item)
        {
            UpdateData(Item, "Color", (uint)Item.Color);
        }
        public static void UpdateStack(ConquerItem Item)
        {
            UpdateData(Item, "StackSize", Item.StackSize);
        }
        public static void UpdateEnchant(ConquerItem Item)
        {
            UpdateData(Item, "Enchant", Item.Enchant);
        }
        public static void UpdateWardrobe(bool inWardrobe, uint UID)
        {
            using (MySqlCommand command = new MySqlCommand(MySqlCommandType.UPDATE))
            {
                command.Update("items")
                    .Set("InWardrobe", inWardrobe)
                    .Where("UID", UID).Execute();
            }
        }
        public static void UpdateLock(ConquerItem Item)
        {
            UpdateData(Item, "Locked", Item.Lock);
            UpdateData(Item, "UnlockEnd", Item.UnlockEnd.ToBinary());
        }
        public static void UpdateSockets(ConquerItem Item)
        {
            UpdateData(Item, "SocketOne", (byte)Item.SocketOne);
            UpdateData(Item, "SocketTwo", (byte)Item.SocketTwo);
        }
        public static void UpdateSocketProgress(ConquerItem Item)
        {
            UpdateData(Item, "SocketProgress", Item.SocketProgress);
        }
        public static void UpdateNextSteedColor(ConquerItem Item)
        {
            UpdateData(Item, "NextSteedColor", Item.NextGreen | (Item.NextBlue << 8) | (Item.NextRed << 16));
        }
        public static void UpdateRefineryItem(ConquerItem Item)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("items").Set("RefineryItem", Item.RefineItem).Where("UID", Item.UID).Execute();
        }
        public static void UpdateRefineryTime(ConquerItem Item)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("items").Set("RefineryTime", Item.RefineryTime.Ticks).Where("UID", Item.UID).Execute();
        }
        public static void UpdateDurabilityItem(ConquerItem Item)
        {
            UpdateData(Item, "Durability", Item.Durability);
        }
        public static void UpdateLocation(ConquerItem Item, Client.GameState client)
        {
            if (IsThere(Item.UID))
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("items").Set("EntityID", client.Entity.UID)
                        .Set("Position", Item.Position).Set("Warehouse", (uint)Item.Warehouse)
                        .Where("UID", Item.UID).Execute();
            }
            else
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("items"))
                    cmd.Insert("ID", Item.ID).Insert("UID", Item.UID)
                        .Insert("Plus", Item.Plus).Insert("Bless", Item.Bless)
                        .Insert("Enchant", Item.Enchant).Insert("SocketOne", (byte)Item.SocketOne)
                        .Insert("SocketTwo", (byte)Item.SocketTwo).Insert("Durability", Item.Durability)
                        .Insert("MaximDurability", Item.MaximDurability).Insert("SocketProgress", Item.SocketProgress)
                        .Insert("PlusProgress", Item.PlusProgress).Insert("Effect", (ushort)Item.Effect)
                        .Insert("Bound", Item.Bound).Insert("DayStamp", Item.DayStamp.ToBinary()).Insert("Days", Item.Days).Insert("Locked", Item.Lock).Insert("UnlockEnd", Item.UnlockEnd.Ticks)
                        .Insert("Suspicious", Item.Suspicious).Insert("SuspiciousStart", Item.SuspiciousStart.Ticks)
                        .Insert("Color", (ushort)Item.Color).Insert("Position", Item.Position).Insert("StackSize", Item.StackSize)
                        .Insert("RefineryItem", Item.RefineItem).Insert("RefineryTime", Item.RefineryTime.Ticks).Insert("EntityID", client.Entity.UID)
                        .Execute();
            }
        }
        public static void UpdatePosition(ConquerItem Item)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("items").Set("Position", Item.Position).Set("Warehouse", Item.Warehouse)
                    .Where("UID", Item.UID).Execute();
        }
        public static void SaveWornAndBag(Client.GameState client)
        {
            if (client == null) return;
            lock (client.ItemSyncRoot ?? client)
                SaveWornAndBagCore(client);
        }
        private static void SaveWornAndBagCore(Client.GameState client)
        {
            if (client.Entity == null || client.Entity.PersistenceRequiresReload) return;
            if (client.LoginAt != DateTime.MinValue && DateTime.Now < client.LoginAt.AddSeconds(8))
                return;
            if (client.Equipment != null && client.Equipment.Objects != null)
            {
                foreach (ConquerItem item in client.Equipment.Objects)
                {
                    if (item != null)
                        UpdatePosition(item);
                }
            }
            if (client.Inventory != null && client.Inventory.Objects != null)
            {
                foreach (ConquerItem item in client.Inventory.Objects)
                {
                    if (item != null)
                        UpdatePosition(item);
                }
            }
        }
        public static void UpdatePlus(ConquerItem Item)
        {
            UpdateData(Item, "Plus", Item.Plus);
        }
        public static void UpdatePlusProgress(ConquerItem Item)
        {
            UpdateData(Item, "PlusProgress", Item.PlusProgress);
        }
        public static void UpdateItemID(ConquerItem Item, Client.GameState client)
        {
            UpdateData(Item, "ID", Item.ID);
        }
        public static void UpdateRunesEXP(ConquerItem item, Client.GameState client)
        {
            UpdateData(item, "RunesEXP", item.RunesEXP);
        }
        public static void UpdateInscre1(ConquerItem Item)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("items").Set("Inscribed", 1).Where("UID", Item.UID).Execute();
        }
        public static void UpdateInscre2(ConquerItem Item)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("items").Set("Inscribed", 0).Where("UID", Item.UID).Execute();
        }
        public static void UpdateDurabilityItem2(ConquerItem Item, uint ItemID)
        {
            if (Item != null)
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("items").Set("Durability", Item.Durability).Where("UID", Item.UID).And("ID", ItemID).Execute();
            }
        }
        public static void RemoveItem(uint UID)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("items").Set("EntityID", 0)
                    .Set("Position", 0).Where("UID", UID).Execute();
        }
        public static void DeleteItem(uint UID)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.DELETE))
                cmd.Delete("items", "UID", UID).Execute();
        }
        public static void DropItem(uint UID)
        {
            MySqlCommand cmd = new MySqlCommand(MySqlCommandType.UPDATE);
            cmd.Update("items").Set("EntityID", 0).Set("Position", 0).Where("UID", UID).Execute();
        }
        public static void Update_Free(ConquerItem Item, Client.GameState client)
        {
            MySqlCommand command = new MySqlCommand(MySqlCommandType.UPDATE);
            command.Update("items").Set("EntityID", client.Entity.UID).Set("DayStamp", Item.DayStamp.ToBinary()).Set("Days", Item.Days).Where("UID", Item.UID).Execute();
        }
        public static void UpdateBound(ConquerItem Item, Client.GameState client)
        {
            MySqlCommand command = new MySqlCommand(MySqlCommandType.UPDATE);
            command.Update("items").Set("EntityID", client.Entity.UID).Set("Bound", Item.Bound).Where("UID", Item.UID).Execute();
        }
        public static bool IsThere(uint uid)
        {
            MySqlCommand command = new MySqlCommand(MySqlCommandType.SELECT);
            command.Select("items").Where("UID", (long)uid);
            MySqlReader reader = new MySqlReader(command);
            if (reader.Read())
            {
                return true;
            }
            return false;
        }
        public static void ClearNulledItems()
        {
            // EntityID 0 also owns undelivered mail and auction escrow. It is not
            // proof that an item is disposable; retain rows until a source-specific
            // transaction proves consumption or expiry.
        }
    }
}
